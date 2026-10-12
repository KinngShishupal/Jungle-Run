using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// The winding trail. Gameplay runs in "track space": x = sideways offset from the trail
    /// centre, y = height, s = distance along the trail. This class maps track space to the world,
    /// following a smooth path of straights and gentle left/right bends, and can bend whole
    /// meshes along it.
    /// </summary>
    public static class TrackPath
    {
        const float Step = 1f;           // metres between samples
        const float Origin = -120f;      // first sampled distance
        const float StartStraight = 140f; // the run always begins on a straight
        const float MinRadius = 75f;     // tightest bend (keeps 40 m of scenery from folding)

        struct Segment { public float S0, S1, K; }

        static readonly List<Vector3> points = new List<Vector3>();
        static readonly List<float> yaws = new List<float>(); // radians
        static readonly List<Segment> plan = new List<Segment>();
        static System.Random rng = new System.Random(1);
        static float planEnd;

        // ---------------- Setup ----------------

        public static void Reset(int seed)
        {
            rng = new System.Random(seed);
            points.Clear();
            yaws.Clear();
            plan.Clear();
            points.Add(new Vector3(0f, 0f, Origin));
            yaws.Add(0f);
            plan.Add(new Segment { S0 = Origin, S1 = StartStraight, K = 0f });
            planEnd = StartStraight;
        }

        static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>Alternates straights with smooth bends of random direction and sharpness.</summary>
        static void PlanTo(float s)
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
        static float Curvature(float s)
        {
            for (int i = plan.Count - 1; i >= 0; i--)
            {
                var g = plan[i];
                if (s < g.S0 || s >= g.S1) continue;
                if (g.K == 0f) return 0f;
                float ramp = Mathf.Min(18f, (g.S1 - g.S0) * 0.4f);
                float w = Tex.SS(g.S0, g.S0 + ramp, s) * Tex.SS(g.S1, g.S1 - ramp, s);
                return g.K * w;
            }
            return 0f;
        }

        static void Extend(float s)
        {
            if (points.Count == 0) Reset(1);
            float last = Origin + (points.Count - 1) * Step;
            if (last >= s) return;
            PlanTo(s + 200f);
            while (last < s)
            {
                float yaw = yaws[yaws.Count - 1];
                float mid = last + Step * 0.5f;
                float yawMid = yaw + Curvature(mid) * Step * 0.5f;
                var p = points[points.Count - 1] + new Vector3(Mathf.Sin(yawMid), 0f, Mathf.Cos(yawMid)) * Step;
                points.Add(p);
                yaws.Add(yaw + Curvature(mid) * Step);
                last += Step;
            }
        }

        // ---------------- Queries ----------------

        static void At(float s, out Vector3 p, out float yaw)
        {
            if (s < Origin)
            {
                p = points.Count > 0 ? points[0] + Vector3.forward * (s - Origin) : new Vector3(0f, 0f, s);
                yaw = 0f;
                return;
            }
            Extend(s + Step);
            float f = (s - Origin) / Step;
            int i = Mathf.FloorToInt(f);
            float t = f - i;
            p = Vector3.LerpUnclamped(points[i], points[i + 1], t);
            yaw = Mathf.LerpUnclamped(yaws[i], yaws[i + 1], t);
        }

        /// <summary>World position of a point in track space.</summary>
        public static Vector3 Point(float s, float x, float y)
        {
            At(s, out var p, out float yaw);
            return p + new Vector3(Mathf.Cos(yaw) * x, y, -Mathf.Sin(yaw) * x);
        }

        /// <summary>Heading of the trail at s, in degrees (0 = world forward).</summary>
        public static float Yaw(float s)
        {
            At(s, out _, out float yaw);
            return yaw * Mathf.Rad2Deg;
        }

        public static Quaternion Rotation(float s) => Quaternion.Euler(0f, Yaw(s), 0f);

        /// <summary>Puts a transform at a track-space point, facing along the trail.</summary>
        public static void Place(Transform t, float x, float y, float s, float extraYaw = 0f)
        {
            At(s, out var p, out float yaw);
            t.SetPositionAndRotation(p + new Vector3(Mathf.Cos(yaw) * x, y, -Mathf.Sin(yaw) * x),
                                     Quaternion.Euler(0f, yaw * Mathf.Rad2Deg + extraYaw, 0f));
        }

        /// <summary>
        /// Bends a mesh authored in straight track space (x sideways, y up, z along the trail,
        /// starting at distance s0) onto the winding path. Vertices end up in world space.
        /// </summary>
        public static void Deform(Mesh m, float s0)
        {
            var v = m.vertices;
            var n = m.normals;
            var tg = m.tangents;
            bool hasN = n != null && n.Length == v.Length;
            bool hasT = tg != null && tg.Length == v.Length;
            for (int i = 0; i < v.Length; i++)
            {
                At(s0 + v[i].z, out var p, out float yaw);
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
    }
}
