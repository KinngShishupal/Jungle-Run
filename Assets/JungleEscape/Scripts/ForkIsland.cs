using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// Where the trail forks: a raised island of forest between the two branches, and a wooden
    /// signpost at its tip with an arrow board for each path naming the world it leads to.
    /// </summary>
    public static class ForkIsland
    {
        const float Step = 2.5f;
        const float Length = 110f;
        const int Cols = 9;

        public static GameObject Build(Transform parent, float forkS, TrackPath left, TrackPath right,
                                       BiomeType leftWorld, BiomeType rightWorld, BiomeType here)
        {
            var root = new GameObject("Fork");
            root.transform.SetParent(parent, false);
            var owned = root.AddComponent<OwnedMeshes>();

            // Inner edges of both trails, from where they've separated enough to fit the island.
            var rowsL = new List<Vector3>();
            var rowsR = new List<Vector3>();
            for (float d = 0f; d <= Length; d += Step)
            {
                var cl = left.PointOn(forkS + d, 0f, 0f);
                var cr = right.PointOn(forkS + d, 0f, 0f);
                if (Vector3.Distance(cl, cr) < 10.8f) continue;
                rowsL.Add(left.PointOn(forkS + d, 5.2f, 0f));
                rowsR.Add(right.PointOn(forkS + d, -5.2f, 0f));
            }
            if (rowsL.Count < 2) return root;

            var ground = IslandMesh(rowsL, rowsR);
            owned.Meshes.Add(ground);
            Prim.Shape(ground, "Island", root.transform, Vector3.zero, Vector3.one, Biomes.Remap(Mats.Ground, here), null, false);

            // Forest on the island: low plants near the tip (the sign must stay visible), trees further in.
            var b = new ChunkBuilder();
            for (int i = 1; i < rowsL.Count; i++)
            {
                float w = Vector3.Distance(rowsL[i], rowsR[i]);
                if (i % 2 != 0) continue;
                int count = w > 20f ? 3 : w > 10f ? 2 : 1;
                for (int k = 0; k < count; k++)
                {
                    float t = Random.Range(0.25f, 0.75f);
                    var p = Vector3.Lerp(rowsL[i], rowsR[i], t);
                    p.y = Hump(w, t) - 0.1f;
                    var rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    int v = Random.Range(0, 4);
                    Model m;
                    float scale = Random.Range(0.85f, 1.2f);
                    if (i < 4) m = Random.value < 0.5f ? Flora.Fern(v) : Flora.Tuft(v);
                    else if (w > 16f && Random.value < 0.55f) m = Random.value < 0.25f ? Flora.Palm(v) : Flora.Tree(v);
                    else { float r = Random.value; m = r < 0.4f ? Flora.Bush(v) : r < 0.75f ? Flora.Fern(v) : Flora.Rock(v); }
                    b.Add(m, Matrix4x4.TRS(p, rot, Vector3.one * scale));
                }
            }
            b.Build(root.transform, float.NaN, m => Biomes.Remap(m, here));

            // Signpost at the tip of the island, facing the runner.
            int tip = Mathf.Min(1, rowsL.Count - 1);
            var signPos = Vector3.Lerp(rowsL[tip], rowsR[tip], 0.5f);
            signPos.y = Hump(Vector3.Distance(rowsL[tip], rowsR[tip]), 0.5f);
            Signpost(root.transform, signPos, left.YawOn(forkS), leftWorld, rightWorld);
            return root;
        }

        // (sin(pi) is a tiny negative number in floating point, and a negative to the power 0.8 is NaN: clamp it)
        static float Hump(float width, float t) => Mathf.Min(2f, width * 0.07f) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.8f);

        static Mesh IslandMesh(List<Vector3> rowsL, List<Vector3> rowsR)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i < rowsL.Count; i++)
            {
                float w = Vector3.Distance(rowsL[i], rowsR[i]);
                float fade = Mathf.Clamp01(i / 2f);
                for (int j = 0; j < Cols; j++)
                {
                    float t = j / (Cols - 1f);
                    var p = Vector3.Lerp(rowsL[i], rowsR[i], t);
                    p.y = Hump(w, t) * fade + (Noise.World(p.x, p.z, 0.25f, 2, 91) - 0.5f) * 0.3f * Mathf.Sin(Mathf.PI * t) - 0.02f;
                    verts.Add(p);
                    uvs.Add(new Vector2(p.x / 6f, p.z / 6f));
                }
            }
            for (int i = 0; i < rowsL.Count - 1; i++)
                for (int j = 0; j < Cols - 1; j++)
                {
                    int a = i * Cols + j, b = a + 1, c = a + Cols, d = c + 1;
                    // Faces upward whichever way the rows run.
                    var n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                    if (n.y >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
                    else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
                }
            var m = new Mesh { name = "ForkIsland" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        // ---------------- Signpost ----------------

        static Font font;

        static void Signpost(Transform parent, Vector3 pos, float yaw, BiomeType leftWorld, BiomeType rightWorld)
        {
            var sign = new GameObject("Signpost").transform;
            sign.SetParent(parent, false);
            sign.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            Prim.Shape(Meshes.Limb(3.4f, 0.13f, 0.11f, 8, 4), "Post", sign, new Vector3(0f, 3.3f, 0f), Vector3.one, Mats.Bark);
            Prim.Shape(Meshes.Ball(8, 10), "Cap", sign, new Vector3(0f, 3.32f, 0f), new Vector3(0.3f, 0.12f, 0.3f), Mats.Bark);
            Board(sign, -1, 2.75f, leftWorld);
            Board(sign, 1, 2.0f, rightWorld);

            // A lantern so the sign reads well in every light.
            var lantern = Mats.Emissive("SignLantern", Tex.Metal, new Color(1f, 0.8f, 0.5f), 0.6f, new Color(2.5f, 1.6f, 0.6f));
            Prim.Shape(Meshes.Ball(8, 10), "Lantern", sign, new Vector3(0f, 3.05f, -0.22f), new Vector3(0.16f, 0.22f, 0.16f), lantern, null, false);
            var lightGo = new GameObject("SignLight");
            lightGo.transform.SetParent(sign, false);
            lightGo.transform.localPosition = new Vector3(0f, 3f, -1f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.85f, 0.6f);
            l.range = 7f;
            l.intensity = 1.6f;
            l.shadows = LightShadows.None;
        }

        /// <summary>An arrow-shaped plank painted in the destination world's colour, with its name.</summary>
        static void Board(Transform sign, int dir, float height, BiomeType world)
        {
            var group = Prim.Group(dir < 0 ? "BoardLeft" : "BoardRight", sign, new Vector3(dir * 1.55f, height, -0.14f));
            group.localRotation = Quaternion.Euler(0f, 0f, dir * -3f);
            Color c = Biomes.PortalColor(world);
            var paint = Mats.Lit("SignPaint" + world, Tex.Planks, Color.Lerp(c, new Color(0.35f, 0.25f, 0.15f), 0.35f), 0.25f);
            Prim.Shape(Meshes.RoundedBox(new Vector3(3.1f, 0.62f, 0.1f), 0.04f), "Plank", group, Vector3.zero, Vector3.one, paint);
            // Arrow point
            Prim.Shape(Meshes.RoundedBox(new Vector3(0.44f, 0.44f, 0.1f), 0.04f), "Point", group, new Vector3(dir * 1.55f, 0f, 0f), Vector3.one, paint,
                       new Vector3(0f, 0f, 45f));

            string label = dir < 0 ? "<  " + Biomes.Title(world) : Biomes.Title(world) + "  >";
            Text(group, label, new Vector3(0.025f, -0.025f, -0.055f), new Color(0f, 0f, 0f, 0.6f)); // shadow
            Text(group, label, new Vector3(0f, 0f, -0.065f), new Color(1f, 0.97f, 0.88f));
        }

        static void Text(Transform parent, string text, Vector3 offset, Color color)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = 0.05f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }
}
