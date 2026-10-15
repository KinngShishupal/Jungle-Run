using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// A Tarzan vine: a liana hanging from a huge bough that crosses the trail high overhead.
    /// Jump as you reach it and the runner grabs it (see PlayerController), swings down and through as a
    /// pendulum, and is flung up and forward on release. When nobody holds it, the vine is itself a damped
    /// pendulum: it sways in the breeze, and after being let go it swings on and slowly settles.
    /// Positions are in track space: X across, S along the trail, heights above the trail.
    /// </summary>
    public class VineSwing : MonoBehaviour
    {
        public static readonly List<VineSwing> All = new List<VineSwing>();

        public const float PivotHeight = 12.4f;
        /// <summary>Rope length: long enough to swing low, short enough that the runner's feet clear the ground.</summary>
        public const float Length = PivotHeight - 2.35f;
        const int Segments = 10;

        public float S, X;
        public TrackPath Path;

        Transform[] segs;
        float angle, angVel, time;      // free swing (radians; positive = toward +S)
        bool held;
        Vector2 heldEnd;                // (s, height) of the runner's hands while held

        public static VineSwing Spawn(Transform parent, float s, float x, bool withBough = true)
        {
            var go = new GameObject("VineSwing");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<VineSwing>();
            v.S = s; v.X = x; v.Path = TrackPath.Active;
            v.Build(withBough);
            return v;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void Build(bool withBough)
        {
            // The bough across the trail, from one great tree to the other, with leafy clumps along it
            // (several vines can share one bough)
            if (withBough) BuildBough();
            BuildVine();
        }

        void BuildBough()
        {
            var b = new ChunkBuilder();
            var bough = new List<Vector3>();
            for (int i = 0; i <= 10; i++)
            {
                float u = i / 10f, x = Mathf.Lerp(-8f, 8f, u);
                bough.Add(new Vector3(x, PivotHeight + 0.4f + 0.6f * Mathf.Abs(x) / 8f + 0.15f * Mathf.Sin(u * 7f), Mathf.Sin(u * 4f) * 0.4f));
            }
            b.Add(Meshes.Sweep(bough, (t, a) => Mathf.Lerp(0.5f, 0.42f, Mathf.Abs(t - 0.5f) * 2f) * (1f + 0.06f * Mathf.Sin(a * 3f + t * 20f)), 10, 2f, 0.5f),
                  Mats.Bark, Matrix4x4.TRS(new Vector3(0f, 0f, S), Quaternion.identity, Vector3.one));
            for (int side = -1; side <= 1; side += 2)
            {
                var trunk = new List<Vector3>();
                for (int i = 0; i <= 6; i++)
                {
                    float u = i / 6f;
                    trunk.Add(new Vector3(side * (8.6f - 1.2f * u * u), u * (PivotHeight + 3f), Mathf.Sin(u * 3f + side) * 0.3f));
                }
                b.Add(Meshes.Sweep(trunk, (t, a) => Mathf.Lerp(1.0f, 0.55f, t) * (1f + 0.5f * Mathf.Pow(1f - t, 6f)), 12, 2f, 0.4f),
                      Mats.Bark, Matrix4x4.TRS(new Vector3(0f, -0.3f, S), Quaternion.identity, Vector3.one));
            }
            for (int i = 0; i < 6; i++)
            {
                float x = Mathf.Lerp(-7f, 7f, i / 5f) + Random.Range(-0.5f, 0.5f);
                float sc = Random.Range(2f, 3f);
                b.Add(Flora.Bush(i).Parts[0].Mesh, Mats.Canopy, Matrix4x4.TRS(new Vector3(x, PivotHeight + 1.1f + Random.Range(0f, 0.5f), S + Random.Range(-0.6f, 0.6f)),
                      Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), new Vector3(sc, sc * 0.7f, sc)));
            }
            b.Build(transform, 0f, m => m);
        }

        void BuildVine()
        {

            // The vine: a chain of segments, each re-aimed every frame along the current rope curve
            var seg = Meshes.Limb(1f, 0.05f, 0.045f, 6, 2);
            var leaf = Meshes.Leaf(0.3f, 0.2f, 0.06f, 0.02f);
            segs = new Transform[Segments];
            for (int i = 0; i < Segments; i++)
            {
                var s = Prim.Shape(seg, "Vine", transform, Vector3.zero, Vector3.one, Mats.Vine).transform;
                if (i % 2 == 1)
                    Prim.Shape(leaf, "Leaf", s, new Vector3(0.04f, -0.5f, 0f), Vector3.one * 2.4f, Mats.BroadLeaf,
                               new Vector3(Random.Range(-40f, 40f), Random.Range(0f, 360f), Random.Range(40f, 80f)), false);
                segs[i] = s;
            }
            angle = Random.Range(-0.08f, 0.08f);
            time = Random.value * 10f;
            Draw();
        }

        /// <summary>Called by the runner each frame while holding the vine (hands at track position s, height h).</summary>
        public void Hold(float handS, float handH)
        {
            held = true;
            heldEnd = new Vector2(handS, handH);
        }

        /// <summary>The runner let go: the vine keeps swinging with the speed it had.</summary>
        public void Release(float ropeAngle, float ropeAngVel)
        {
            held = false;
            angle = ropeAngle;
            angVel = ropeAngVel;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            time += dt;
            if (!held)
            {
                // Free pendulum, lightly damped, nudged by a gentle breeze
                float breeze = 0.05f * Mathf.Sin(time * 0.7f) + 0.03f * Mathf.Sin(time * 1.9f);
                angVel += (-(9.8f / Length) * Mathf.Sin(angle) - 0.35f * angVel + breeze) * dt;
                angle += angVel * dt;
            }
            Draw();
        }

        /// <summary>Lays the segments along the rope: taut and straight when held, gently curved when free.</summary>
        void Draw()
        {
            if (Path == null || segs == null) return;
            Vector3 pivot = Path.PointOn(S, X, PivotHeight);
            Vector3 end;
            float bow;
            if (held)
            {
                end = Path.PointOn(heldEnd.x, X, heldEnd.y);
                bow = 0.05f;
            }
            else
            {
                end = Path.PointOn(S + Length * Mathf.Sin(angle), X, PivotHeight - Length * Mathf.Cos(angle));
                bow = 0.35f + 0.2f * Mathf.Sin(time * 1.3f);   // a free vine hangs in a soft curve
            }
            Vector3 side = Quaternion.Euler(0f, Path.YawOn(S), 0f) * Vector3.right;
            Vector3 prev = pivot;
            for (int i = 0; i < segs.Length; i++)
            {
                float u = (i + 1f) / segs.Length;
                Vector3 p = Vector3.Lerp(pivot, end, u) + side * bow * Mathf.Sin(u * Mathf.PI) * 0.6f
                            + Vector3.Cross(side, end - pivot).normalized * bow * Mathf.Sin(u * Mathf.PI);
                Vector3 d = p - prev;
                var t = segs[i];
                t.position = prev;
                t.rotation = Quaternion.FromToRotation(Vector3.down, d.normalized);
                t.localScale = new Vector3(1f, d.magnitude, 1f);
                prev = p;
            }
        }
    }
}
