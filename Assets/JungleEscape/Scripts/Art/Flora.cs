using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleEscape
{
    /// <summary>A reusable multi-material model: a list of (mesh, material, local matrix) parts.</summary>
    public class Model
    {
        public struct Part
        {
            public Mesh Mesh;
            public Material Mat;
            public Matrix4x4 M;
            public bool Shadows;
        }

        public readonly List<Part> Parts = new List<Part>();

        public Model Add(Mesh mesh, Material mat, Matrix4x4 m, bool shadows = true)
        {
            Parts.Add(new Part { Mesh = mesh, Mat = mat, M = m, Shadows = shadows });
            return this;
        }

        public Model Add(Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale, bool shadows = true)
            => Add(mesh, mat, Matrix4x4.TRS(pos, rot, scale), shadows);

        /// <summary>Spawns the model as live GameObjects (for things that move or get removed).</summary>
        public Transform Spawn(string name, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 scale)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            root.localRotation = localRot;
            root.localScale = scale;
            foreach (var p in Parts)
            {
                var go = new GameObject(p.Mesh.name);
                go.transform.SetParent(root, false);
                go.transform.localPosition = p.M.GetColumn(3);
                go.transform.localRotation = p.M.rotation;
                go.transform.localScale = p.M.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = p.Mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = p.Mat;
                r.shadowCastingMode = p.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            return root;
        }
    }

    /// <summary>
    /// Collects static scenery for one track chunk and merges it into one renderer per
    /// material, which keeps draw calls low on phones.
    /// </summary>
    public class ChunkBuilder
    {
        readonly Dictionary<(Material, bool), List<CombineInstance>> groups = new Dictionary<(Material, bool), List<CombineInstance>>();
        readonly List<Mesh> temps = new List<Mesh>();

        /// <summary>Adds a one-off mesh that is destroyed once it has been merged.</summary>
        public void AddTemp(Mesh mesh, Material mat, Matrix4x4 m, bool shadows = true)
        {
            Add(mesh, mat, m, shadows);
            temps.Add(mesh);
        }

        public void Add(Mesh mesh, Material mat, Matrix4x4 m, bool shadows = true)
        {
            var key = (mat, shadows);
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mesh, transform = m });
        }

        public void Add(Model model, Matrix4x4 m)
        {
            foreach (var p in model.Parts) Add(p.Mesh, p.Mat, m * p.M, p.Shadows);
        }

        public void Add(Model model, Vector3 pos, float yaw, float scale) =>
            Add(model, Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale));

        /// <summary>Merges everything and, if a trail distance is given, bends it onto the winding path.</summary>
        public void Build(Transform parent, float trackS0 = float.NaN, System.Func<Material, Material> remap = null)
        {
            var owner = parent.gameObject.GetComponent<OwnedMeshes>();
            if (owner == null) owner = parent.gameObject.AddComponent<OwnedMeshes>();
            foreach (var kv in groups)
            {
                var mesh = Meshes.Combine(kv.Value, "Batch_" + kv.Key.Item1.name);
                if (!float.IsNaN(trackS0)) TrackPath.Deform(mesh, trackS0);
                owner.Meshes.Add(mesh);
                var go = new GameObject(mesh.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = remap != null ? remap(kv.Key.Item1) : kv.Key.Item1;
                r.shadowCastingMode = kv.Key.Item2 ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            groups.Clear();
            foreach (var t in temps) Object.Destroy(t);
            temps.Clear();
        }
    }

    /// <summary>Destroys runtime-generated meshes together with their chunk (avoids leaks).</summary>
    public class OwnedMeshes : MonoBehaviour
    {
        public readonly List<Mesh> Meshes = new List<Mesh>();

        void OnDestroy()
        {
            foreach (var m in Meshes) if (m != null) Destroy(m);
        }
    }

    /// <summary>Library of jungle plants, trees and rocks. Several random variants of each are built once.</summary>
    public static class Flora
    {
        const int Variants = 4;

        static List<Model> trees, palms, ferns, broadPlants, bushes, rocks, tufts, wallTrees;
        static List<Model> pines, birches, junipers, tundraTufts, drifts, iceBoulders, iceCrystals;

        public static Model Tree(int i) => Pick(ref trees, BuildTree, i);
        public static Model Palm(int i) => Pick(ref palms, BuildPalm, i);
        public static Model Fern(int i) => Pick(ref ferns, BuildFern, i);
        public static Model BroadPlant(int i) => Pick(ref broadPlants, BuildBroadPlant, i);
        public static Model Bush(int i) => Pick(ref bushes, BuildBush, i);
        public static Model Rock(int i) => Pick(ref rocks, BuildRock, i);
        public static Model Tuft(int i) => Pick(ref tufts, BuildTuft, i);
        public static Model WallTree(int i) => Pick(ref wallTrees, BuildWallTree, i);

        // Ice Age flora
        public static Model Pine(int i) => Pick(ref pines, BuildPine, i);
        public static Model Birch(int i) => Pick(ref birches, BuildBirch, i);
        public static Model Juniper(int i) => Pick(ref junipers, BuildJuniper, i);
        public static Model TundraTuft(int i) => Pick(ref tundraTufts, BuildTundraTuft, i);
        public static Model Snowdrift(int i) => Pick(ref drifts, BuildSnowdrift, i);
        public static Model IceBoulder(int i) => Pick(ref iceBoulders, BuildIceBoulder, i);
        public static Model IceCrystals(int i) => Pick(ref iceCrystals, BuildIceCrystals, i);

        static Model Pick(ref List<Model> list, System.Func<int, Model> build, int i)
        {
            if (list == null)
            {
                list = new List<Model>();
                var state = Random.state;
                for (int k = 0; k < Variants; k++)
                {
                    Random.InitState(9173 + k * 101 + build.Method.Name.GetHashCode());
                    list.Add(build(k));
                }
                Random.state = state;
            }
            return list[((i % Variants) + Variants) % Variants];
        }

        // ---------------- Trees ----------------

        /// <summary>Tall rainforest tree with buttress roots, a few limbs and a lumpy crown.</summary>
        static Model BuildTree(int seed)
        {
            var m = new Model();
            float h = Random.Range(8f, 12f);
            float r0 = Random.Range(0.32f, 0.45f);
            var bend = new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f));
            var path = new List<Vector3>();
            for (int i = 0; i <= 9; i++)
            {
                float t = i / 9f;
                path.Add(new Vector3(0f, t * h, 0f) + bend * (t * t) + new Vector3(Mathf.Sin(t * 7f + seed) * 0.08f, 0f, 0f));
            }
            float phase = Random.value * 6f;
            var trunk = Meshes.Sweep(path, (t, a) =>
            {
                float taper = r0 * (1f - 0.45f * t);
                float flare = Mathf.Pow(Mathf.Clamp01(1f - t * 7f), 2.5f);
                return taper * (1f + flare * (1.2f + 0.9f * Mathf.Cos(a * 5f + phase)));
            }, 12, 2f, 1f / (r0 * Mathf.PI * 2f));
            m.Add(trunk, Mats.Bark, Matrix4x4.identity);

            Vector3 top = path[path.Count - 1];
            int limbs = Random.Range(2, 4);
            var crown = new List<Vector3> { top + Vector3.up * 0.6f };
            for (int i = 0; i < limbs; i++)
            {
                float yaw = i * (360f / limbs) + Random.Range(-30f, 30f);
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(1f, 0f, 0f);
                Vector3 start = Vector3.Lerp(path[6], top, Random.value);
                Vector3 end = start + dir * Random.Range(2f, 3.2f) + Vector3.up * Random.Range(1.2f, 2.2f);
                var lp = new List<Vector3>();
                for (int k = 0; k <= 4; k++)
                {
                    float t = k / 4f;
                    lp.Add(Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.4f);
                }
                float lr = r0 * 0.38f;
                m.Add(Meshes.Sweep(lp, (t, a) => lr * (1f - 0.6f * t), 7, 1f, 1f), Mats.Bark, Matrix4x4.identity);
                crown.Add(end + Vector3.up * 0.3f);
            }

            foreach (var c in crown)
            {
                int lumps = 2;
                for (int k = 0; k < lumps; k++)
                {
                    float s = Random.Range(2.6f, 4.2f);
                    var off = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.3f, 0.6f), Random.Range(-1.2f, 1.2f));
                    m.Add(Meshes.Blob(seed * 31 + k * 7 + crown.IndexOf(c) * 3, 2, 0.45f, 1.6f, 0f, false), Mats.Canopy,
                          c + off, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), new Vector3(s, s * 0.62f, s));
                }
            }

            // A couple of epiphyte ferns clinging to the trunk.
            for (int i = 0; i < 2; i++)
            {
                Vector3 at = path[Random.Range(3, 7)];
                AddFrondRing(m, Mats.Frond, at, 5, 0.8f, 0.35f, 20f, 0.4f, Random.value * 360f);
            }
            return m;
        }

        /// <summary>Background tree: simpler, darker and bigger, used to build the jungle wall.</summary>
        static Model BuildWallTree(int seed)
        {
            var m = new Model();
            float h = Random.Range(9f, 14f);
            var path = new List<Vector3>();
            for (int i = 0; i <= 5; i++) path.Add(new Vector3(Mathf.Sin(i + seed) * 0.2f, i / 5f * h, 0f));
            float r0 = Random.Range(0.4f, 0.6f);
            m.Add(Meshes.Sweep(path, (t, a) => r0 * (1f - 0.4f * t) * (1f + Mathf.Pow(Mathf.Clamp01(1f - t * 6f), 2f)), 8, 2f, 0.4f),
                  Mats.Bark, Matrix4x4.identity);
            for (int k = 0; k < 4; k++)
            {
                float s = Random.Range(4.5f, 7f);
                var off = new Vector3(Random.Range(-2f, 2f), h - 1f + Random.Range(-1.5f, 1.5f), Random.Range(-2f, 2f));
                m.Add(Meshes.Blob(seed * 13 + k + 500, 1, 0.5f, 1.4f, 0f, false), Mats.CanopyDark,
                      off, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), new Vector3(s, s * 0.7f, s));
            }
            // Low undergrowth mass at the foot of the wall.
            for (int k = 0; k < 2; k++)
            {
                float s = Random.Range(3f, 4.5f);
                m.Add(Meshes.Blob(seed * 17 + k + 900, 1, 0.5f, 1.8f, 0.25f, false), Mats.CanopyDark,
                      new Vector3(Random.Range(-2.5f, 2.5f), 0.6f, Random.Range(-2.5f, 2.5f)), Quaternion.identity,
                      new Vector3(s, s * 0.6f, s), false);
            }
            return m;
        }

        /// <summary>Curved coconut palm. Leans toward local +X.</summary>
        static Model BuildPalm(int seed)
        {
            var m = new Model();
            float h = Random.Range(6f, 8.5f);
            float lean = Random.Range(1.2f, 2.6f);
            var path = new List<Vector3>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                path.Add(new Vector3(lean * t * t, t * h, 0f));
            }
            m.Add(Meshes.Sweep(path, (t, a) => Mathf.Lerp(0.24f, 0.15f, t) * (1f + 0.5f * Mathf.Pow(1f - t, 8f)), 10, 2f, 0.8f),
                  Mats.PalmBark, Matrix4x4.identity);

            Vector3 top = path[path.Count - 1];
            AddFrondRing(m, Mats.Frond, top, 10, Random.Range(3.0f, 3.7f), 1.0f, 28f, 1.7f, Random.value * 360f);
            // Coconuts tucked under the crown
            var nut = Meshes.Ball(8, 12);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f + Random.Range(-20f, 20f);
                Vector3 off = Quaternion.Euler(0f, a, 0f) * new Vector3(0.22f, -0.25f, 0f);
                m.Add(nut, Mats.Coconut, top + off, Quaternion.Euler(Random.Range(0, 360f), a, 0f), Vector3.one * 0.3f);
            }
            return m;
        }

        // ---------------- Undergrowth ----------------

        static Model BuildFern(int seed)
        {
            var m = new Model();
            AddFrondRing(m, Mats.Frond, Vector3.zero, Random.Range(7, 10), Random.Range(1.1f, 1.6f), 0.5f, 52f, 0.9f, Random.value * 360f);
            return m;
        }

        static Model BuildBroadPlant(int seed)
        {
            var m = new Model();
            int leaves = Random.Range(4, 7);
            for (int i = 0; i < leaves; i++)
            {
                float yaw = i * (360f / leaves) + Random.Range(-25f, 25f);
                float stemH = Random.Range(0.35f, 0.9f);
                var rot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 tip = rot * new Vector3(0f, stemH, 0.25f);
                var stem = new List<Vector3> { Vector3.zero, rot * new Vector3(0f, stemH * 0.6f, 0.08f), tip };
                m.Add(Meshes.Sweep(stem, (t, a) => 0.025f * (1f - 0.5f * t), 5, 1f, 1f), Mats.Vine, Matrix4x4.identity, false);
                float len = Random.Range(0.8f, 1.25f);
                var leaf = Meshes.Leaf(len, len * 0.75f, len * 0.35f, len * 0.08f, 6, Random.Range(-0.2f, 0.2f));
                m.Add(leaf, Mats.BroadLeaf, tip, rot * Quaternion.Euler(Random.Range(-35f, -10f), 0f, 0f), Vector3.one);
            }
            return m;
        }

        static Model BuildBush(int seed)
        {
            var m = new Model();
            float s = Random.Range(1.3f, 2.0f);
            m.Add(Meshes.Blob(seed * 5 + 700, 2, 0.5f, 1.9f, 0.2f, false), Mats.Canopy,
                  new Vector3(0f, s * 0.25f, 0f), Quaternion.identity, new Vector3(s, s * 0.65f, s));
            AddFrondRing(m, Mats.Frond, new Vector3(0f, 0.1f, 0f), 6, s * 0.8f, 0.45f, 35f, 0.6f, Random.value * 360f);
            return m;
        }

        static Model BuildTuft(int seed)
        {
            var m = new Model();
            float h = Random.Range(0.45f, 0.75f);
            m.Add(Meshes.Tuft(h * 1.2f, h, 3), Mats.Grass, Matrix4x4.identity, false);
            return m;
        }

        static Model BuildRock(int seed)
        {
            var m = new Model();
            m.Add(Meshes.Blob(seed * 7 + 300, 2, 0.32f, 1.5f, 0.28f, true), Mats.Rock, Matrix4x4.identity);
            return m;
        }

        // ---------------- Ice Age ----------------

        static Mesh tierCone, prism;

        /// <summary>Cone of pine branches (closed underneath, so tiers look solid from below).</summary>
        static Mesh TierCone => tierCone != null ? tierCone : (tierCone = Meshes.Lathe(new[]
        {
            new Vector2(0.001f, 0.12f), new Vector2(1f, 0f), new Vector2(0.72f, 0.28f), new Vector2(0.36f, 0.66f), new Vector2(0.001f, 1f)
        }, 14, 3f, "PineTier"));

        static Mesh Prism => prism != null ? prism : (prism = Meshes.Lathe(new[]
        {
            new Vector2(0.001f, 0f), new Vector2(0.1f, 0.02f), new Vector2(0.1f, 0.75f), new Vector2(0.001f, 1f)
        }, 6, 1f, "IcePrism"));

        /// <summary>Snow-laden spruce: stacked tiers of dark needles, each topped with a cap of snow.</summary>
        static Model BuildPine(int seed)
        {
            var m = new Model();
            float h = Random.Range(7f, 12f);
            var path = new List<Vector3>();
            for (int i = 0; i <= 6; i++) path.Add(new Vector3(Mathf.Sin(i * 0.9f + seed) * 0.05f, i / 6f * h, 0f));
            m.Add(Meshes.Sweep(path, (t, a) => Mathf.Lerp(0.3f, 0.06f, t), 8, 2f, 0.5f), Mats.Bark, Matrix4x4.identity);

            int tiers = Random.Range(6, 10);
            for (int k = 0; k < tiers; k++)
            {
                float t = k / (tiers - 1f);
                float y = Mathf.Lerp(h * 0.15f, h * 0.9f, t);
                float r = Mathf.Lerp(2.4f, 0.45f, t) * Random.Range(0.9f, 1.1f);
                float th = Mathf.Lerp(1.7f, 0.9f, t);
                var rot = Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(0f, 360f), Random.Range(-3f, 3f));
                m.Add(TierCone, Mats.PineNeedles, new Vector3(0f, y, 0f), rot, new Vector3(r, th, r));
                // Snow sits just above the needles over most of the tier, leaving a dark fringe at the rim.
                m.Add(TierCone, Mats.Snow, new Vector3(0f, y + th * 0.2f, 0f), rot, new Vector3(r * 0.85f, th * 0.85f, r * 0.85f), false);
            }
            m.Add(TierCone, Mats.Snow, new Vector3(0f, h * 0.97f, 0f), Quaternion.identity, new Vector3(0.18f, 0.7f, 0.18f), false);
            return m;
        }

        /// <summary>Bare winter birch: slender white trunk and frosted branches.</summary>
        static Model BuildBirch(int seed)
        {
            var m = new Model();
            float h = Random.Range(5.5f, 8.5f);
            var lean = new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
            var path = new List<Vector3>();
            for (int i = 0; i <= 7; i++)
            {
                float t = i / 7f;
                path.Add(new Vector3(0f, t * h, 0f) + lean * t * t);
            }
            m.Add(Meshes.Sweep(path, (t, a) => Mathf.Lerp(0.17f, 0.05f, t), 9, 1f, 1.5f), Mats.BirchBark, Matrix4x4.identity);

            int branches = Random.Range(5, 9);
            for (int b = 0; b < branches; b++)
            {
                float t = Random.Range(0.4f, 0.92f);
                var start = Vector3.Lerp(path[0], path[path.Count - 1], t);
                var dir = Quaternion.Euler(0f, b * (360f / branches) + Random.Range(-20f, 20f), 0f) * new Vector3(1f, Random.Range(0.6f, 1.3f), 0f).normalized;
                float len = Random.Range(1.1f, 2.4f) * (1.2f - t * 0.5f);
                var bp = new List<Vector3> { start, start + dir * len * 0.5f + Vector3.up * 0.1f, start + dir * len + Vector3.up * 0.25f };
                m.Add(Meshes.Sweep(bp, (u, a) => Mathf.Lerp(0.05f, 0.012f, u), 5, 1f, 2f), Mats.BirchBark, Matrix4x4.identity);
                for (int k = 0; k < 2; k++) // twigs
                {
                    var tStart = Vector3.Lerp(bp[1], bp[2], Random.Range(0.2f, 0.8f));
                    var tDir = (dir + Random.insideUnitSphere * 0.8f + Vector3.up * 0.4f).normalized;
                    var tp = new List<Vector3> { tStart, tStart + tDir * 0.5f };
                    m.Add(Meshes.Sweep(tp, (u, a) => Mathf.Lerp(0.015f, 0.004f, u), 4, 1f, 2f), Mats.BirchBark, Matrix4x4.identity, false);
                }
                if (b % 3 == 0) // a little snow caught where the branch meets the trunk
                    m.Add(Meshes.Blob(seed * 7 + b, 1, 0.3f, 2.5f, 0f, false), Mats.Snow, start + dir * 0.25f + Vector3.up * 0.06f,
                          Quaternion.identity, new Vector3(0.3f, 0.1f, 0.3f), false);
            }
            return m;
        }

        static Model BuildJuniper(int seed)
        {
            var m = new Model();
            int lumps = Random.Range(2, 4);
            for (int i = 0; i < lumps; i++)
            {
                float s = Random.Range(0.9f, 1.5f);
                var p = new Vector3(Random.Range(-0.5f, 0.5f), s * 0.25f, Random.Range(-0.5f, 0.5f));
                m.Add(Meshes.Blob(seed * 11 + i + 40, 2, 0.4f, 2.4f, 0.2f, false), Mats.PineNeedles, p, Quaternion.identity, new Vector3(s, s * 0.6f, s));
                m.Add(Meshes.Blob(seed * 11 + i + 60, 1, 0.3f, 2f, 0.2f, false), Mats.Snow, p + Vector3.up * s * 0.2f,
                      Quaternion.identity, new Vector3(s * 0.85f, s * 0.28f, s * 0.85f), false);
            }
            return m;
        }

        static Model BuildTundraTuft(int seed)
        {
            var m = new Model();
            float h = Random.Range(0.35f, 0.6f);
            m.Add(Meshes.Tuft(h * 1.3f, h, 3), Mats.TundraGrass, Matrix4x4.identity, false);
            return m;
        }

        static Model BuildSnowdrift(int seed)
        {
            var m = new Model();
            m.Add(Meshes.Blob(seed * 5 + 80, 2, 0.22f, 1.6f, 0.15f, false), Mats.Snow, new Vector3(0f, 0.05f, 0f), Quaternion.identity,
                  new Vector3(Random.Range(1.8f, 3f), Random.Range(0.45f, 0.8f), Random.Range(1.2f, 1.8f)), false);
            return m;
        }

        static Model BuildIceBoulder(int seed)
        {
            var m = new Model();
            m.Add(Meshes.Blob(seed * 7 + 320, 2, 0.3f, 1.3f, 0.28f, true), Mats.IceRock, Matrix4x4.identity);
            m.Add(Meshes.Blob(seed * 7 + 340, 1, 0.3f, 2f, 0.2f, false), Mats.Snow, new Vector3(0f, 0.3f, 0f), Quaternion.identity,
                  new Vector3(0.75f, 0.22f, 0.75f), false);
            return m;
        }

        static Model BuildIceCrystals(int seed)
        {
            var m = new Model();
            int n = Random.Range(5, 9);
            for (int i = 0; i < n; i++)
            {
                var rot = Quaternion.Euler(Random.Range(-35f, 35f), Random.Range(0f, 360f), Random.Range(-35f, 35f));
                float s = Random.Range(0.6f, 1.8f);
                m.Add(Prism, Mats.IceCrystal, new Vector3(Random.Range(-0.3f, 0.3f), -0.05f, Random.Range(-0.3f, 0.3f)), rot,
                      new Vector3(s, s * Random.Range(0.8f, 1.4f), s), false);
            }
            return m;
        }

        // ---------------- Shared parts ----------------

        static readonly Dictionary<string, Mesh> leafCache = new Dictionary<string, Mesh>();

        /// <summary>Ring of fronds radiating from a point (palm crowns, ferns, epiphytes).</summary>
        public static void AddFrondRing(Model m, Material mat, Vector3 at, int count, float length, float width,
                                        float pitchUp, float droop, float yaw0)
        {
            for (int i = 0; i < count; i++)
            {
                float l = length * Random.Range(0.8f, 1.1f);
                string key = $"{Mathf.Round(l * 10f)}_{Mathf.Round(width * 10f)}_{Mathf.Round(droop * 10f)}";
                if (!leafCache.TryGetValue(key, out var leaf))
                    leafCache[key] = leaf = Meshes.Leaf(l, width, droop * l / length, width * 0.12f, 7);
                float yaw = yaw0 + i * (360f / count) + Random.Range(-12f, 12f);
                float pitch = -pitchUp + Random.Range(-12f, 12f);
                m.Add(leaf, mat, at, Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(pitch, 0f, Random.Range(-15f, 15f)), Vector3.one);
            }
        }
    }
}
