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
        public const float FloorY = WaterY - 0.12f;   // the floor board inside the hull (the rim is ~12 cm above the water)

        public float S, X;          // centre of the canoe
        public TrackPath Path;
        public bool Used;

        Transform hull, sail;
        bool ridden;
        float speed, time, prevX, xVel;
        float heave, heaveV, pitch, pitchV, roll, rollV;
        float splashT;

        public float Floor => FloorY + heave;
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
        public bool Covers(float s, float x) => Mathf.Abs(s - S) < Length * 0.5f - 0.25f && Mathf.Abs(x - X) < 0.95f;

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

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            time += dt;
            if (!ridden) speed = Mathf.MoveTowards(speed, 0f, 6f * dt);   // drifts to a stop when empty
            S += speed * dt;

            // Wave surface under the bow and the stern
            float bow = Wave(S + Length * 0.4f), stern = Wave(S - Length * 0.4f);
            float targetHeave = (bow + stern) * 0.5f;
            float targetPitch = -Mathf.Atan2(bow - stern, Length * 0.8f) * Mathf.Rad2Deg - speed * 0.25f;  // bow lifts with speed
            xVel = (X - prevX) / Mathf.Max(dt, 1e-4f);
            prevX = X;
            float targetRoll = 4f * Mathf.Sin(time * 1.1f + S * 0.3f) - Mathf.Clamp(xVel * 2.2f, -12f, 12f);

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

        float Wave(float s) => 0.07f * Mathf.Sin(0.8f * s - 1.9f * time) + 0.04f * Mathf.Sin(1.9f * s + 1.3f * time + X);

        void Place()
        {
            if (Path == null) return;
            Path.PlaceOn(transform, X, WaterY + heave, S);
            hull.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        // ---------------- Model ----------------

        static Mesh hullMesh, sailMesh;

        void Build()
        {
            hull = Prim.Group("Hull", transform, Vector3.zero);
            var wood = Mats.Lit("CanoeWood", Tex.Planks, new Color(0.78f, 0.58f, 0.4f), 0.3f);
            var dark = Mats.Lit("CanoeInside", Tex.Planks, new Color(0.45f, 0.32f, 0.22f), 0.2f);
            if (hullMesh == null) { hullMesh = HullMesh(); sailMesh = SailMesh(); }
            Prim.Shape(hullMesh, "DugoutHull", hull, Vector3.zero, Vector3.one, wood);
            // Thwarts (seats), and a floor board the runner stands on
            var box = Meshes.RoundedBox(Vector3.one, 0.1f, 2);
            for (int i = -1; i <= 1; i += 2)
                Prim.Shape(box, "Thwart", hull, new Vector3(0f, 0.1f, i * 1.15f), new Vector3(Beam * 0.95f, 0.06f, 0.22f), wood);
            Prim.Shape(box, "FloorBoard", hull, new Vector3(0f, -0.13f, 0f), new Vector3(0.5f, 0.04f, Length * 0.6f), dark, null, false);

            // Outrigger: a float log on two curved booms
            var log = Meshes.Limb(1f, 0.13f, 0.11f, 8, 4);
            var floatLog = Prim.Shape(log, "Outrigger", hull, new Vector3(1.55f, -0.05f, -1.5f), new Vector3(1f, 3f, 1f), Mats.Bark, new Vector3(-90f, 0f, 0f));
            floatLog.name = "OutriggerFloat";
            var boom = Meshes.Limb(1.7f, 0.045f, 0.04f, 6, 3);
            for (int i = -1; i <= 1; i += 2)
                Prim.Shape(boom, "Boom", hull, new Vector3(0.05f, 0.22f, i * 0.8f), Vector3.one, Mats.Bark, new Vector3(0f, 0f, 96f));

            // Mast and a small cloth sail
            Prim.Shape(Meshes.Limb(2.8f, 0.05f, 0.035f, 6, 3), "Mast", hull, new Vector3(0f, 2.9f, 0.7f), Vector3.one, Mats.Bark);
            sail = Prim.Group("Sail", hull, new Vector3(0f, 0.6f, 0.68f));
            Prim.Shape(sailMesh, "Cloth", sail, Vector3.zero, Vector3.one, Mats.Fabric(new Color(0.92f, 0.86f, 0.72f)), null, true);
        }

        /// <summary>
        /// An open dugout hull: U-shaped cross-sections tapering to points at bow and stern, with an outer
        /// shell, an inner shell and a rim joining them, so it is genuinely hollow.
        /// </summary>
        static Mesh HullMesh()
        {
            const int rings = 17, sides = 10;
            const float wall = 0.06f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int shell = 0; shell < 2; shell++)
            {
                int start = verts.Count;
                for (int i = 0; i < rings; i++)
                {
                    float u = i / (rings - 1f), z = (u - 0.5f) * Length;
                    float taper = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(2f * u - 1f, 2f)), 0.55f);
                    float half = Beam * 0.5f * taper - (shell == 1 ? wall : 0f);
                    float depth = 0.32f * Mathf.Lerp(0.6f, 1f, taper) - (shell == 1 ? wall : 0f);
                    float rise = 0.12f * Mathf.Pow(2f * u - 1f, 4f);        // ends sweep up a little
                    for (int k = 0; k <= sides; k++)
                    {
                        float a = Mathf.PI * (1f + k / (float)sides);       // 180..360 degrees: the bottom half
                        verts.Add(new Vector3(Mathf.Cos(a) * Mathf.Max(0.005f, half), 0.12f + rise + Mathf.Sin(a) * Mathf.Max(0.005f, depth), z));
                        uvs.Add(new Vector2(k / (float)sides, u * 3f));
                    }
                }
                for (int i = 0; i < rings - 1; i++)
                    for (int k = 0; k < sides; k++)
                    {
                        int a = start + i * (sides + 1) + k, b = a + 1, c = a + sides + 1, d = c + 1;
                        if (shell == 0) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
                        else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
                    }
            }
            // Rims joining the two shells along both top edges
            int perShell = rings * (sides + 1);
            for (int i = 0; i < rings - 1; i++)
                foreach (int k in new[] { 0, sides })
                {
                    int o0 = i * (sides + 1) + k, o1 = o0 + sides + 1, i0 = perShell + o0, i1 = perShell + o1;
                    if (k == 0) { tris.Add(o0); tris.Add(o1); tris.Add(i0); tris.Add(i0); tris.Add(o1); tris.Add(i1); }
                    else { tris.Add(o0); tris.Add(i0); tris.Add(o1); tris.Add(i0); tris.Add(i1); tris.Add(o1); }
                }
            var m = new Mesh { name = "CanoeHull" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
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
