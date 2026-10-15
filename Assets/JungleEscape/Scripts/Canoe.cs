using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// An outrigger sailing canoe waiting where the boardwalk is broken (water world). Run or jump off the end of
    /// the boardwalk in its lane and the runner lands in it (see PlayerController); it carries him across the
    /// open water at running pace and he hops out at the far side.
    ///
    /// It floats on a simple wave field: heave, pitch and roll are damped springs pulled toward the wave
    /// surface under the hull, so it bobs and rocks naturally, dips when he lands in it, leans as he steers
    /// and lifts its bow as it picks up speed, throwing spray and a wake. Track space: X across, S along.
    /// </summary>
    public class Canoe : MonoBehaviour
    {
        public static readonly List<Canoe> All = new List<Canoe>();

        public const float Length = 4.8f, Beam = 0.95f, WaterY = -0.45f;
        /// <summary>Height of the canoe floor (where the runner stands) at rest, in track space.</summary>
        /// <summary>How high the hull's origin rides above the water: it draws ~11 cm, its inside floor stays dry.</summary>
        public const float HullY = 0.16f;
        public const float FloorY = WaterY + HullY - 0.03f;   // top of the floor board inside the hull (well above the waterline)

        public float S, X;          // centre of the canoe
        public TrackPath Path;
        public bool Used;

        Transform hull, sail;
        bool ridden;
        float speed, time, prevX, xVel;
        float heave, heaveV, pitch, pitchV, roll, rollV;
        float splashT;

        public float Floor => FloorY + heave;
        /// <summary>Height of the canoe's rim, in track space: anything dropping below it inside the hull is in the boat.</summary>
        public float Rim => WaterY + HullY + 0.12f + heave;
        public float RollDeg => roll;
        public float Speed => speed;

        public static Canoe Spawn(Transform parent, float s, float x)
        {
            var go = new GameObject("Canoe");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canoe>();
            c.S = s; c.X = x; c.prevX = x; c.Path = TrackPath.Active;
            c.Build();
            c.Place();
            return c;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        /// <summary>Is a runner at (s, x) standing over the inside of the canoe?</summary>
        public bool Covers(float s, float x) => Mathf.Abs(s - S) < Length * 0.5f - 0.2f && Mathf.Abs(x - X) < 1.15f;

        public void Board(float impactVy)
        {
            ridden = true;
            Used = true;
            heaveV += Mathf.Min(0f, impactVy) * 0.12f;    // the hull dips under his weight...
            pitchV += -impactVy * 2.5f;                    // ...and rocks
            SeaLife.Splash(Path.PointOn(S, X, WaterY + 0.1f), 6, 0.8f);
        }

        /// <summary>Carried along: matches the runner's pace and follows his steering.</summary>
        public void Ride(float dt, float targetSpeed, float runnerX)
        {
            speed = Mathf.MoveTowards(speed, targetSpeed, 25f * dt);
            X = runnerX;
        }

        public void Release() => ridden = false;

        /// <summary>Removes every canoe (new run).</summary>
        public static void ClearAll()
        {
            foreach (var c in All.ToArray()) if (c != null) Destroy(c.gameObject);
            All.Clear();
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            time += dt;
            if (!ridden) speed = Mathf.MoveTowards(speed, 0f, 6f * dt);   // drifts to a stop when empty
            // Left far behind (or never used): remove it
            var gm = GameManager.I;
            if (!ridden && gm != null && gm.Player != null && gm.Player.TrackS - S > 80f) { Destroy(gameObject); return; }
            S += speed * dt;

            // Wave surface under the bow and the stern
            float bow = Wave(S + Length * 0.4f), stern = Wave(S - Length * 0.4f);
            float targetHeave = (bow + stern) * 0.25f;   // (the drawn sea is flat: keep the bob modest)
            float targetPitch = -Mathf.Atan2(bow - stern, Length * 0.8f) * Mathf.Rad2Deg - Mathf.Min(speed, 20f) * 0.08f;  // bow lifts a little with speed
            xVel = (X - prevX) / Mathf.Max(dt, 1e-4f);
            prevX = X;
            // An outrigger canoe is very stable: only a gentle roll, so the inside never dips under the water
            float targetRoll = 2.2f * Mathf.Sin(time * 1.1f + S * 0.3f) - Mathf.Clamp(xVel * 1.1f, -5f, 5f);

            heaveV += (40f * (targetHeave - heave) - 6f * heaveV) * dt; heave += heaveV * dt;
            pitchV += (30f * (targetPitch - pitch) - 5f * pitchV) * dt; pitch += pitchV * dt;
            rollV += (22f * (targetRoll - roll) - 3f * rollV) * dt; roll += rollV * dt;

            // Spray at the bow and a wake behind, and the sail filling
            if (speed > 2f && (splashT -= dt) <= 0f)
            {
                splashT = Mathf.Lerp(0.18f, 0.06f, speed / 25f);
                SeaLife.Splash(Path.PointOn(S + Length * 0.5f, X + Random.Range(-0.4f, 0.4f), WaterY + 0.1f), 2, 0.45f);
                SeaLife.Splash(Path.PointOn(S - Length * 0.5f, X, WaterY + 0.05f), 1, 0.6f);
            }
            if (sail != null) sail.localScale = new Vector3(1f + 0.08f * Mathf.Sin(time * 3.3f) + Mathf.Min(speed, 20f) * 0.01f, 1f, 1f);
            Place();
        }

        float Wave(float s) => 0.05f * Mathf.Sin(0.8f * s - 1.9f * time) + 0.03f * Mathf.Sin(1.9f * s + 1.3f * time + X);

        void Place()
        {
            if (Path == null) return;
            Path.PlaceOn(transform, X, WaterY + HullY + heave, S);
            hull.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        // ---------------- Model ----------------

        static Mesh outerMesh, innerMesh, sailMesh;
        static List<Vector3>[] rimPaths, bandPaths;

        void Build()
        {
            hull = Prim.Group("Hull", transform, Vector3.zero);
            // Dark, oiled outer hull; lighter, adze-carved interior; a painted band along the sides
            var outer = Mats.Lit("CanoeHull", Tex.Bark, new Color(0.42f, 0.29f, 0.19f), 0.38f, tiling: new Vector2(2f, 1f), bump: 0.7f);
            var inner = Mats.Lit("CanoeCarved", Tex.Bark, new Color(0.86f, 0.66f, 0.46f), 0.22f, tiling: new Vector2(3f, 1f), bump: 0.5f);
            var ochre = Mats.Plain(new Color(0.6f, 0.2f, 0.1f), 0.3f);
            var pale = Mats.Plain(new Color(0.9f, 0.84f, 0.7f), 0.3f);
            var rope = Mats.Rope;
            if (outerMesh == null) { BuildHullMeshes(); sailMesh = SailMesh(); }
            Prim.Shape(outerMesh, "HullOuter", hull, Vector3.zero, Vector3.one, outer);
            Prim.Shape(innerMesh, "HullInner", hull, Vector3.zero, Vector3.one, inner, null, false);
            for (int side = 0; side < 2; side++)
            {
                // Gunwale pole along the rim, and painted bands just below it
                Prim.Shape(Meshes.Sweep(rimPaths[side], (t, a) => 0.026f, 6, 1f, 2f, true), "Gunwale", hull, Vector3.zero, Vector3.one, outer);
                Prim.Shape(Meshes.Sweep(bandPaths[side], (t, a) => 0.016f, 5, 1f, 2f, true), "PaintBand", hull, Vector3.zero, Vector3.one, ochre, null, false);
                var lower = new List<Vector3>();
                foreach (var p in bandPaths[side]) lower.Add(p + new Vector3(Mathf.Sign(p.x) * -0.004f, -0.045f, 0f));
                Prim.Shape(Meshes.Sweep(lower, (t, a) => 0.008f, 4, 1f, 2f, true), "PaintLine", hull, Vector3.zero, Vector3.one, pale, null, false);
            }
            // Carved prow and stern pieces sweeping up from the ends
            for (int end = -1; end <= 1; end += 2)
            {
                float z = end * Length * 0.5f;
                var prow = new List<Vector3>
                {
                    new Vector3(0f, 0.2f, z - end * 0.25f), new Vector3(0f, 0.3f, z), new Vector3(0f, 0.48f, z + end * 0.12f), new Vector3(0f, 0.56f, z + end * 0.06f)
                };
                Prim.Shape(Meshes.Sweep(prow, (t, a) => Mathf.Lerp(0.07f, 0.025f, t), 6, 1f, 2f, true), end > 0 ? "Prow" : "Sternpost", hull, Vector3.zero, Vector3.one, outer);
            }

            // Seats (thwarts) and the floor board he kneels on
            var box = Meshes.RoundedBox(Vector3.one, 0.1f, 2);
            for (int i = -1; i <= 1; i += 2)
                Prim.Shape(box, "Thwart", hull, new Vector3(0f, 0.09f, i * 1.15f), new Vector3(Beam * 0.9f, 0.05f, 0.2f), inner);
            Prim.Shape(box, "FloorBoard", hull, new Vector3(0f, -0.05f, 0f), new Vector3(0.42f, 0.04f, Length * 0.55f), inner, null, false);

            // Outrigger: a pointed float log riding on the water, two arched booms lashed across the hull,
            // and crossed pegs joining each boom to the float
            float floatY = -HullY;   // half under the water
            var ama = new List<Vector3>();
            for (int i = 0; i <= 8; i++) ama.Add(new Vector3(1.6f, floatY + 0.02f * Mathf.Sin(i / 8f * Mathf.PI), Mathf.Lerp(-1.7f, 1.7f, i / 8f)));
            Prim.Shape(Meshes.Sweep(ama, (t, a) => 0.13f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.4f) + 0.01f, 8, 1f, 1f, true),
                       "OutriggerFloat", hull, Vector3.zero, Vector3.one, Mats.Bark);
            var knot = Meshes.Ball(6, 8);
            for (int i = -1; i <= 1; i += 2)
            {
                float z = i * 0.85f;
                var boom = new List<Vector3> { new Vector3(-0.5f, 0.17f, z), new Vector3(0.6f, 0.28f, z), new Vector3(1.7f, 0.22f, z) };
                Prim.Shape(Meshes.Sweep(boom, (t, a) => 0.04f, 6, 1f, 2f, true), "Boom", hull, Vector3.zero, Vector3.one, Mats.Bark);
                for (int p = -1; p <= 1; p += 2)
                {
                    var peg = new List<Vector3> { new Vector3(1.6f + p * 0.12f, 0.23f, z), new Vector3(1.6f - p * 0.05f, floatY + 0.08f, z + p * 0.04f) };
                    Prim.Shape(Meshes.Sweep(peg, (t, a) => 0.022f, 5, 1f, 2f), "Peg", hull, Vector3.zero, Vector3.one, Mats.Bark);
                }
                foreach (float x in new[] { -0.47f, 0.47f, 1.6f })
                    Prim.Shape(knot, "Lashing", hull, new Vector3(x, x > 1f ? 0.22f : 0.15f, z), new Vector3(0.09f, 0.07f, 0.09f), rope, null, false);
            }

            // Mast stepped in the floor, and a crab-claw sail spread between a yard and a boom
            Prim.Shape(Meshes.Limb(2.9f, 0.05f, 0.035f, 6, 3), "Mast", hull, new Vector3(0f, 2.85f, 0.7f), Vector3.one, Mats.Bark);
            sail = Prim.Group("Sail", hull, new Vector3(0f, 0.6f, 0.68f));
            Prim.Shape(sailMesh, "Cloth", sail, Vector3.zero, Vector3.one, Mats.Fabric(new Color(0.9f, 0.82f, 0.64f)), null, true);
            Prim.Shape(Meshes.Sweep(new List<Vector3> { new Vector3(0f, 0f, 0.05f), new Vector3(0.05f, 0.02f, -0.8f), new Vector3(0f, 0.05f, -1.62f) },
                       (t, a) => 0.028f, 5, 1f, 2f, true), "SailBoom", sail, Vector3.zero, Vector3.one, Mats.Bark);
            Prim.Shape(Meshes.Sweep(new List<Vector3> { new Vector3(0f, -0.05f, 0.08f), new Vector3(0.05f, 1.1f, 0.02f), new Vector3(0f, 2.2f, -0.12f) },
                       (t, a) => 0.025f, 5, 1f, 2f, true), "SailYard", sail, Vector3.zero, Vector3.one, Mats.Bark);
        }

        /// <summary>
        /// A dugout hull carved from one log: U-shaped cross-sections tapering to points, a thin side wall and a
        /// thick bottom (so it floats with its floor above the water), solid carved ends, and rims joining the
        /// outer and inner surfaces. Outer and inner are separate meshes so they can be finished differently.
        /// </summary>
        static void BuildHullMeshes()
        {
            const int rings = 19, sides = 12;
            const float sideWall = 0.055f, bottomWall = 0.2f, outerDepth = 0.38f, rimY = 0.12f;
            var ov = new List<Vector3>(); var ou = new List<Vector2>(); var ot = new List<int>();
            var iv = new List<Vector3>(); var iu = new List<Vector2>(); var it = new List<int>();
            rimPaths = new[] { new List<Vector3>(), new List<Vector3>() };
            bandPaths = new[] { new List<Vector3>(), new List<Vector3>() };
            for (int i = 0; i < rings; i++)
            {
                float u = i / (rings - 1f), z = (u - 0.5f) * Length;
                float e = 2f * u - 1f;
                float taper = Mathf.Pow(Mathf.Max(0f, 1f - e * e), 0.55f);
                float rise = 0.12f * e * e * e * e;                             // ends sweep up a little
                float half = Mathf.Max(0.006f, Beam * 0.5f * taper);
                float depth = Mathf.Max(0.006f, outerDepth * Mathf.Lerp(0.55f, 1f, taper));
                float halfIn = Mathf.Max(0.004f, half - sideWall);
                float depthIn = Mathf.Max(0.004f, depth - bottomWall);         // solid towards the ends
                for (int k = 0; k <= sides; k++)
                {
                    float a = Mathf.PI * (1f + k / (float)sides);               // the bottom half of an ellipse
                    ov.Add(new Vector3(Mathf.Cos(a) * half, rimY + rise + Mathf.Sin(a) * depth, z));
                    ou.Add(new Vector2(k / (float)sides, u * 3f));
                    iv.Add(new Vector3(Mathf.Cos(a) * halfIn, rimY + rise + Mathf.Sin(a) * depthIn, z));
                    iu.Add(new Vector2(k / (float)sides, u * 3f));
                }
                for (int side = 0; side < 2; side++)
                {
                    float sx = side == 0 ? -1f : 1f;
                    rimPaths[side].Add(new Vector3(sx * (half - sideWall * 0.5f), rimY + rise + 0.01f, z));
                    float ba = Mathf.PI * (side == 0 ? 1.12f : 1.88f);             // just below the rim on each side
                    bandPaths[side].Add(new Vector3(Mathf.Cos(ba) * (half + 0.004f), rimY + rise + Mathf.Sin(ba) * depth, z));
                }
            }
            int ring = sides + 1;
            for (int i = 0; i < rings - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = i * ring + k, b = a + 1, c = a + ring, d = c + 1;
                    ot.Add(a); ot.Add(b); ot.Add(c); ot.Add(b); ot.Add(d); ot.Add(c);     // outer faces outward
                    it.Add(a); it.Add(c); it.Add(b); it.Add(b); it.Add(c); it.Add(d);     // inner faces into the boat
                }
            // Rims: strips between the outer and inner top edges, part of the outer mesh
            int baseI = ov.Count;
            ov.AddRange(iv); ou.AddRange(iu);
            for (int i = 0; i < rings - 1; i++)
                foreach (int k in new[] { 0, sides })
                {
                    int o0 = i * ring + k, o1 = o0 + ring, i0 = baseI + o0, i1 = baseI + o1;
                    if (k == 0) { ot.Add(o0); ot.Add(o1); ot.Add(i0); ot.Add(i0); ot.Add(o1); ot.Add(i1); }
                    else { ot.Add(o0); ot.Add(i0); ot.Add(o1); ot.Add(i0); ot.Add(i1); ot.Add(o1); }
                }
            outerMesh = Make("CanoeHullOuter", ov, ou, ot);
            innerMesh = Make("CanoeHullInner", iv, iu, it);
        }

        static Mesh Make(string name, List<Vector3> v, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        /// <summary>A triangular sail, both sides, gently curved like cloth filled by the wind.</summary>
        static Mesh SailMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            const int n = 6;
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n - i; j++)
                {
                    float u = i / (float)n, v = j / (float)n;   // u up the mast, v out along the boom
                    float belly = 0.18f * Mathf.Sin(Mathf.PI * u) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(v / Mathf.Max(0.01f, 1f - u)));
                    verts.Add(new Vector3(belly, u * 2.1f, -v * 1.5f));
                    uvs.Add(new Vector2(v, u));
                }
            int Idx(int i, int j) { int k = 0; for (int r = 0; r < i; r++) k += n - r + 1; return k + j; }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n - i; j++)
                {
                    tris.Add(Idx(i, j)); tris.Add(Idx(i + 1, j)); tris.Add(Idx(i, j + 1));
                    tris.Add(Idx(i, j)); tris.Add(Idx(i, j + 1)); tris.Add(Idx(i + 1, j));   // back face
                    if (j < n - i - 1)
                    {
                        tris.Add(Idx(i + 1, j)); tris.Add(Idx(i + 1, j + 1)); tris.Add(Idx(i, j + 1));
                        tris.Add(Idx(i + 1, j)); tris.Add(Idx(i, j + 1)); tris.Add(Idx(i + 1, j + 1));
                    }
                }
            var m = new Mesh { name = "Sail" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
