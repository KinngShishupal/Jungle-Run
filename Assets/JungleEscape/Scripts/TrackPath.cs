using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// A winding trail. Gameplay runs in "track space": x = sideways offset from the trail
    /// centre, y = height, s = distance along the trail. A path maps track space to the world,
    /// following straights and gentle bends, can bend whole meshes along itself, and can
    /// <see cref="Branch"/> into a second trail at a fork.
    ///
    /// <see cref="Current"/> is the path the runner is on. While a chunk is being built,
    /// <see cref="Use"/> points the static helpers at the path being generated.
    /// </summary>
    public class TrackPath
    {
        const float Step = 1f;            // metres between samples
        const float RootOrigin = -120f;   // first sampled distance of a run
        const float StartStraight = 140f; // the run always begins on a straight
        const float MinRadius = 75f;      // tightest bend (keeps 40 m of scenery from folding)

        struct Segment { public float S0, S1, K; }

        public static TrackPath Current { get; private set; }
        static TrackPath building;

        /// <summary>The path the static helpers act on: the one being built, else the runner's.</summary>
        public static TrackPath Active => building ?? Current ?? (Current = new TrackPath(1, RootOrigin, new Vector3(0f, 0f, RootOrigin), 0f, StartStraight, 0f, null));

        readonly TrackPath parent;
        readonly float origin;
        readonly List<Vector3> points = new List<Vector3>();
        readonly List<float> yaws = new List<float>(); // radians
        readonly List<Segment> plan = new List<Segment>();
        readonly System.Random rng;
        float planEnd;

        TrackPath(int seed, float originS, Vector3 start, float startYaw, float straightUntil, float initialTurn, TrackPath parentPath)
        {
            rng = new System.Random(seed);
            parent = parentPath;
            origin = originS;
            points.Add(start);
            yaws.Add(startYaw);
            float s = originS;
            if (initialTurn != 0f)
            {
                // A decisive bend away from the fork, then a straight so the two trails separate.
                const float len = 30f;
                float ramp = Mathf.Min(18f, len * 0.4f);
                plan.Add(new Segment { S0 = s, S1 = s + len, K = initialTurn / (len - ramp) });
                s += len;
            }
            plan.Add(new Segment { S0 = s, S1 = Mathf.Max(s, straightUntil), K = 0f });
            planEnd = Mathf.Max(s, straightUntil);
        }

        // ---------------- Setup ----------------

        /// <summary>Starts a fresh winding trail for a new run.</summary>
        public static void Reset(int seed)
        {
            Current = new TrackPath(seed, RootOrigin, new Vector3(0f, 0f, RootOrigin), 0f, StartStraight, 0f, null);
            building = null;
        }

        /// <summary>The runner has taken this path (at a fork).</summary>
        public static void SetCurrent(TrackPath p) => Current = p;

        /// <summary>Generation helpers act on <paramref name="p"/> until <see cref="Done"/>.</summary>
        public static void Use(TrackPath p) => building = p;
        public static void Done() => building = null;

        /// <summary>A new trail splitting off this one at distance s, first bending by turnDeg.</summary>
        public TrackPath Branch(float s, float turnDeg, int seed)
        {
            AtLocal(s, out var p, out float yaw);
            return new TrackPath(seed, s, p, yaw, s + 70f, turnDeg * Mathf.Deg2Rad, this);
        }

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>Alternates straights with smooth bends of random direction and sharpness.</summary>
        void PlanTo(float s)
        {
            while (planEnd < s)
            {
                float straight = Rand(50f, 140f);
                plan.Add(new Segment { S0 = planEnd, S1 = planEnd + straight, K = 0f });
                planEnd += straight;

                float len = Rand(45f, 110f);
                float radius = Rand(MinRadius, 160f);
                float dir = rng.NextDouble() < 0.5 ? -1f : 1f;
                plan.Add(new Segment { S0 = planEnd, S1 = planEnd + len, K = dir / radius });
                planEnd += len;
            }
        }

        /// <summary>Curvature (radians per metre) at distance s, eased in and out of each bend.</summary>
        float Curvature(float s)
        {
            for (int i = plan.Count - 1; i >= 0; i--)
            {
                var g = plan[i];
                if (s < g.S0 || s >= g.S1) continue;
                if (g.K == 0f) return 0f;
                float ramp = Mathf.Min(18f, (g.S1 - g.S0) * 0.4f);
                return g.K * Tex.SS(g.S0, g.S0 + ramp, s) * Tex.SS(g.S1, g.S1 - ramp, s);
            }
            return 0f;
        }

        void Extend(float s)
        {
            float last = origin + (points.Count - 1) * Step;
            if (last >= s) return;
            PlanTo(s + 200f);
            while (last < s)
            {
                float yaw = yaws[yaws.Count - 1];
                float mid = last + Step * 0.5f;
                float yawMid = yaw + Curvature(mid) * Step * 0.5f;
                points.Add(points[points.Count - 1] + new Vector3(Mathf.Sin(yawMid), 0f, Mathf.Cos(yawMid)) * Step);
                yaws.Add(yaw + Curvature(mid) * Step);
                last += Step;
            }
        }

        // ---------------- Queries (this path) ----------------

        void AtLocal(float s, out Vector3 p, out float yaw)
        {
            if (s < origin)
            {
                if (parent != null) { parent.AtLocal(s, out p, out yaw); return; }
                p = points[0] + new Vector3(Mathf.Sin(yaws[0]), 0f, Mathf.Cos(yaws[0])) * (s - origin);
                yaw = yaws[0];
                return;
            }
            Extend(s + Step);
            float f = (s - origin) / Step;
            int i = Mathf.FloorToInt(f);
            float t = f - i;
            p = Vector3.LerpUnclamped(points[i], points[i + 1], t);
            yaw = Mathf.LerpUnclamped(yaws[i], yaws[i + 1], t);
        }

        public Vector3 PointOn(float s, float x, float y)
        {
            AtLocal(s, out var p, out float yaw);
            return p + new Vector3(Mathf.Cos(yaw) * x, y, -Mathf.Sin(yaw) * x);
        }

        public float YawOn(float s)
        {
            AtLocal(s, out _, out float yaw);
            return yaw * Mathf.Rad2Deg;
        }

        public void PlaceOn(Transform t, float x, float y, float s, float extraYaw = 0f)
        {
            AtLocal(s, out var p, out float yaw);
            t.SetPositionAndRotation(p + new Vector3(Mathf.Cos(yaw) * x, y, -Mathf.Sin(yaw) * x),
                                     Quaternion.Euler(0f, yaw * Mathf.Rad2Deg + extraYaw, 0f));
        }

        /// <summary>
        /// Bends a mesh authored in straight track space (x sideways, y up, z along the trail,
        /// starting at distance s0) onto this path. Vertices end up in world space.
        /// </summary>
        public void DeformOnto(Mesh m, float s0)
        {
            var v = m.vertices;
            var n = m.normals;
            var tg = m.tangents;
            bool hasN = n != null && n.Length == v.Length;
            bool hasT = tg != null && tg.Length == v.Length;
            for (int i = 0; i < v.Length; i++)
            {
                AtLocal(s0 + v[i].z, out var p, out float yaw);
                float c = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
                var right = new Vector3(c, 0f, -sn);
                var fwd = new Vector3(sn, 0f, c);
                var lv = v[i];
                v[i] = p + right * lv.x + Vector3.up * lv.y;
                if (hasN) { var ln = n[i]; n[i] = right * ln.x + Vector3.up * ln.y + fwd * ln.z; }
                if (hasT) { var lt = tg[i]; var d = right * lt.x + Vector3.up * lt.y + fwd * lt.z; tg[i] = new Vector4(d.x, d.y, d.z, lt.w); }
            }
            m.vertices = v;
            if (hasN) m.normals = n;
            if (hasT) m.tangents = tg;
            m.RecalculateBounds();
        }

        // ---------------- Static helpers (active path) ----------------

        public static Vector3 Point(float s, float x, float y) => Active.PointOn(s, x, y);
        public static float Yaw(float s) => Active.YawOn(s);
        public static Quaternion Rotation(float s) => Quaternion.Euler(0f, Yaw(s), 0f);
        public static void Place(Transform t, float x, float y, float s, float extraYaw = 0f) => Active.PlaceOn(t, x, y, s, extraYaw);
        public static void Deform(Mesh m, float s0) => Active.DeformOnto(m, s0);
    }
}
