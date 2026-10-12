using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// Procedural jungle animals: the obstacle animals (snake, tiger, bear) and ambient
    /// wildlife (crocodiles in rivers, flocks of parrots). Built from the shared meshes
    /// and textures; each animal animates itself.
    /// </summary>
    public static class Animals
    {
        static Mesh ball, segment, tailMesh, limbThick, limbBear, claw, tongue;

        static void Init()
        {
            if (ball != null) return;
            ball = Meshes.Ball(12, 18);
            segment = Meshes.Limb(0.22f, 1f, 1f, 10, 5);      // unit-radius tube, scaled per segment
            limbThick = Meshes.Limb(0.6f, 0.1f, 0.075f, 10, 7);
            limbBear = Meshes.Limb(0.7f, 0.15f, 0.11f, 10, 7);
            claw = Meshes.Lathe(new[] { new Vector2(0.001f, 0f), new Vector2(0.018f, 0.01f), new Vector2(0.008f, 0.06f), new Vector2(0.001f, 0.07f) }, 6);
            tongue = Meshes.RoundedBox(new Vector3(0.02f, 0.006f, 0.12f), 0.003f);
            var tp = new List<Vector3>();
            for (int k = 0; k <= 8; k++)
            {
                float t = k / 8f;
                tp.Add(new Vector3(0f, t * 0.55f + Mathf.Sin(t * 2.5f) * 0.1f, -t * 0.9f));
            }
            tailMesh = Meshes.Sweep(tp, (t, a) => 0.07f * (1f - 0.6f * t), 8, 1f, 1.5f, true);
        }

        // ---------------- Snake ----------------

        /// <summary>A python stretched across the lane, slithering side to side.</summary>
        public static SnakeBody BuildSnake(Transform t)
        {
            Init();
            var body = t.gameObject.AddComponent<SnakeBody>();
            body.Build(t, segment, ball, tongue);
            return body;
        }

        // ---------------- Tiger ----------------

        public static Transform BuildTiger(Transform t, out Transform[] legs, out Transform body)
        {
            Init();
            var fur = Mats.TigerFur;
            var white = Mats.Fur(new Color(0.93f, 0.9f, 0.82f));
            var dark = Mats.Plain(new Color(0.05f, 0.04f, 0.03f), 0.6f);
            var eye = Mats.Emissive("TigerEye", Tex.Metal, new Color(0.9f, 0.6f, 0.1f), 0.95f, new Color(0.5f, 0.3f, 0.02f));

            // Faces -Z (toward the player)
            body = Prim.Group("Body", t, Vector3.zero);
            Prim.Shape(ball, "Torso", body, new Vector3(0f, 0.8f, 0.1f), new Vector3(0.62f, 0.64f, 1.7f), fur);
            Prim.Shape(ball, "Chest", body, new Vector3(0f, 0.84f, -0.55f), new Vector3(0.7f, 0.74f, 0.75f), fur);
            Prim.Shape(ball, "Head", body, new Vector3(0f, 1.02f, -1.05f), new Vector3(0.46f, 0.42f, 0.46f), fur);
            Prim.Shape(ball, "Ruff", body, new Vector3(0f, 0.93f, -0.98f), new Vector3(0.56f, 0.42f, 0.3f), white);
            Prim.Shape(ball, "Muzzle", body, new Vector3(0f, 0.92f, -1.27f), new Vector3(0.24f, 0.17f, 0.18f), white);
            Prim.Shape(ball, "Nose", body, new Vector3(0f, 0.98f, -1.35f), new Vector3(0.07f, 0.05f, 0.05f), dark);
            for (int s = -1; s <= 1; s += 2)
            {
                Prim.Shape(ball, "Eye", body, new Vector3(s * 0.1f, 1.08f, -1.25f), new Vector3(0.06f, 0.045f, 0.04f), eye, null, false);
                Prim.Shape(ball, "Ear", body, new Vector3(s * 0.17f, 1.22f, -1.0f), new Vector3(0.11f, 0.12f, 0.05f), fur);
            }
            var tail = Prim.Shape(tailMesh, "Tail", body, new Vector3(0f, 0.95f, 0.9f), Vector3.one * 1.3f, fur);
            tail.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);

            Vector3[] hips = { new Vector3(-0.2f, 0.62f, -0.55f), new Vector3(0.2f, 0.62f, -0.55f),
                               new Vector3(-0.2f, 0.62f, 0.6f), new Vector3(0.2f, 0.62f, 0.6f) };
            legs = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var leg = Prim.Group("Leg" + i, body, hips[i]);
                Prim.Shape(limbThick, "Leg", leg, Vector3.zero, Vector3.one, fur);
                Prim.Shape(ball, "Paw", leg, new Vector3(0f, -0.6f, -0.04f), new Vector3(0.16f, 0.09f, 0.2f), white);
                legs[i] = leg;
            }
            return body;
        }

        // ---------------- Bear ----------------

        static Mesh[] lumps;

        static Mesh Lump(int i)
        {
            if (lumps == null)
            {
                lumps = new Mesh[4];
                for (int k = 0; k < lumps.Length; k++) lumps[k] = Meshes.Blob(611 + k * 17, 2, 0.26f, 3.4f, 0f, false);
            }
            return lumps[((i % lumps.Length) + lumps.Length) % lumps.Length];
        }

        /// <summary>A shaggy brown bear that lumbers on all fours, then rears up and roars as you approach.</summary>
        public static BearRig BuildBear(Transform t)
        {
            Init();
            var fur = Mats.Lit("BearFur", Tex.Fur, new Color(0.27f, 0.18f, 0.115f), 0.14f, tiling: new Vector2(4f, 4f), bump: 2.2f);
            var furDark = Mats.Lit("BearFurDark", Tex.Fur, new Color(0.15f, 0.10f, 0.065f), 0.14f, tiling: new Vector2(4f, 4f), bump: 2.2f);
            var muzzle = Mats.Lit("BearMuzzle", Tex.Fur, new Color(0.52f, 0.40f, 0.27f), 0.2f, tiling: new Vector2(3f, 3f), bump: 1.5f);
            var dark = Mats.Plain(new Color(0.03f, 0.025f, 0.02f), 0.9f);
            var ivory = Mats.Plain(new Color(0.88f, 0.84f, 0.74f), 0.5f);
            var mouth = Mats.Plain(new Color(0.32f, 0.07f, 0.07f), 0.5f);

            var rig = t.gameObject.AddComponent<BearRig>();
            var model = Prim.Group("Bear", t, Vector3.zero);
            rig.Model = model;

            // Hind legs stay planted; everything else hinges at the hips.
            for (int s = -1; s <= 1; s += 2)
            {
                var leg = Prim.Group(s < 0 ? "HindL" : "HindR", model, new Vector3(s * 0.3f, 0.88f, 0.2f));
                Prim.Shape(Lump(s + 2), "Haunch", leg, new Vector3(0f, -0.22f, 0.04f), new Vector3(0.44f, 0.72f, 0.56f), fur);
                Prim.Shape(limbBear, "Shin", leg, new Vector3(0f, -0.3f, 0f), new Vector3(1f, 0.82f, 1f), fur);
                Prim.Shape(ball, "Foot", leg, new Vector3(0f, -0.86f, -0.1f), new Vector3(0.26f, 0.12f, 0.38f), furDark);
                if (s < 0) rig.LegL = leg; else rig.LegR = leg;
            }

            var spine = Prim.Group("Spine", model, new Vector3(0f, 0.9f, 0.18f));
            rig.Spine = spine;
            Prim.Shape(Lump(0), "Hips", spine, new Vector3(0f, 0.05f, 0f), new Vector3(1.0f, 0.86f, 0.82f), fur);
            Prim.Shape(Lump(1), "Belly", spine, new Vector3(0f, 0.45f, -0.06f), new Vector3(1.04f, 0.9f, 0.86f), fur);
            Prim.Shape(Lump(2), "Chest", spine, new Vector3(0f, 0.78f, -0.1f), new Vector3(1.0f, 0.82f, 0.84f), fur);
            Prim.Shape(Lump(3), "Hump", spine, new Vector3(0f, 1.0f, 0.14f), new Vector3(0.8f, 0.5f, 0.62f), furDark);
            Prim.Shape(Lump(1), "ChestFur", spine, new Vector3(0f, 0.7f, -0.38f), new Vector3(0.52f, 0.6f, 0.22f), muzzle);
            // Shaggy tufts breaking up the silhouette
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * 0.46f, 0.3f + (i % 3) * 0.28f, Mathf.Sin(a) * 0.36f);
                Prim.Shape(Lump(i), "Tuft", spine, p, new Vector3(0.24f, 0.22f, 0.24f), i % 2 == 0 ? fur : furDark);
            }

            var head = Prim.Group("Head", spine, new Vector3(0f, 1.22f, -0.2f));
            rig.Head = head;
            Prim.Shape(Lump(2), "Skull", head, new Vector3(0f, 0.05f, 0f), new Vector3(0.5f, 0.44f, 0.5f), fur);
            Prim.Shape(Lump(3), "Ruff", head, new Vector3(0f, 0f, 0.12f), new Vector3(0.62f, 0.5f, 0.42f), furDark);
            Prim.Shape(ball, "Snout", head, new Vector3(0f, -0.01f, -0.27f), new Vector3(0.24f, 0.2f, 0.32f), muzzle);
            Prim.Shape(ball, "Nose", head, new Vector3(0f, 0.04f, -0.43f), new Vector3(0.1f, 0.07f, 0.07f), dark);
            for (int s = -1; s <= 1; s += 2)
            {
                Prim.Shape(ball, "Eye", head, new Vector3(s * 0.11f, 0.11f, -0.19f), Vector3.one * 0.05f, dark, null, false);
                Prim.Shape(Lump(s + 1), "Ear", head, new Vector3(s * 0.19f, 0.27f, 0.04f), new Vector3(0.14f, 0.14f, 0.09f), furDark);
            }
            var jaw = Prim.Group("Jaw", head, new Vector3(0f, -0.08f, -0.12f));
            rig.Jaw = jaw;
            Prim.Shape(ball, "LowerJaw", jaw, new Vector3(0f, -0.04f, -0.14f), new Vector3(0.2f, 0.09f, 0.26f), muzzle);
            Prim.Shape(ball, "Mouth", jaw, new Vector3(0f, 0.0f, -0.14f), new Vector3(0.15f, 0.04f, 0.2f), mouth, null, false);
            for (int s = -1; s <= 1; s += 2)
            {
                Prim.Shape(claw, "Fang", head, new Vector3(s * 0.06f, -0.08f, -0.36f), Vector3.one * 0.8f, ivory, new Vector3(180f, 0f, 0f), false);
                Prim.Shape(claw, "Fang", jaw, new Vector3(s * 0.055f, 0f, -0.24f), Vector3.one * 0.7f, ivory, null, false);
            }

            for (int s = -1; s <= 1; s += 2)
            {
                var arm = Prim.Group(s < 0 ? "ArmL" : "ArmR", spine, new Vector3(s * 0.47f, 0.98f, -0.1f));
                Prim.Shape(Lump(s + 2), "Shoulder", arm, new Vector3(0f, -0.12f, 0f), new Vector3(0.36f, 0.48f, 0.4f), fur);
                Prim.Shape(limbBear, "Arm", arm, Vector3.zero, Vector3.one, fur);
                Prim.Shape(ball, "Paw", arm, new Vector3(0f, -0.74f, -0.02f), new Vector3(0.24f, 0.18f, 0.26f), furDark);
                for (int c = -1; c <= 1; c++)
                    Prim.Shape(claw, "Claw", arm, new Vector3(c * 0.06f, -0.8f, -0.11f), Vector3.one, ivory, new Vector3(150f, 0f, 0f), false);
                if (s < 0) rig.ArmL = arm; else rig.ArmR = arm;
            }

            rig.Pose(1f, 0f, Time.time); // reared up by default (also what the asset gallery shows)
            return rig;
        }

        // ---------------- Ambient: crocodile ----------------

        public static void SpawnCrocodile(Transform parent, float x, float y, float s, float yaw)
        {
            Init();
            var root = new GameObject("Crocodile").transform;
            root.SetParent(parent, false);
            TrackPath.Place(root, x, y, s, yaw);
            var skin = Mats.CrocSkin;
            var belly = Mats.Plain(new Color(0.55f, 0.55f, 0.38f), 0.3f);
            var eye = Mats.Emissive("CrocEye", Tex.Metal, new Color(0.7f, 0.65f, 0.1f), 0.9f, new Color(0.3f, 0.25f, 0.02f));
            var path = new List<Vector3>();
            for (int k = 0; k <= 14; k++)
            {
                float t = k / 14f;
                path.Add(new Vector3(Mathf.Sin(t * 3f) * 0.15f, 0f, Mathf.Lerp(-1.2f, 2.4f, t)));
            }
            var body = Meshes.Sweep(path, (t, a) =>
            {
                float r = t < 0.25f ? Mathf.Lerp(0.12f, 0.36f, t / 0.25f) : Mathf.Lerp(0.36f, 0.04f, (t - 0.25f) / 0.75f);
                return r * (1f + 0.12f * Mathf.Abs(Mathf.Sin(a * 6f))); // bumpy scutes
            }, 10, 2f, 1.2f, true);
            Prim.Shape(body, "Body", root, Vector3.zero, new Vector3(1f, 0.5f, 1f), skin);
            Prim.Shape(Meshes.RoundedBox(new Vector3(0.32f, 0.14f, 0.85f), 0.06f), "Snout", root, new Vector3(0f, 0f, -1.55f), Vector3.one, skin);
            Prim.Shape(Meshes.RoundedBox(new Vector3(0.3f, 0.06f, 0.8f), 0.03f), "Jaw", root, new Vector3(0f, -0.08f, -1.5f), Vector3.one, belly);
            for (int s2 = -1; s2 <= 1; s2 += 2)
                Prim.Shape(ball, "Eye", root, new Vector3(s2 * 0.11f, 0.11f, -1.15f), new Vector3(0.09f, 0.08f, 0.1f), eye, null, false);
            root.gameObject.AddComponent<Drifter>().Init(root.right * Random.Range(-0.4f, 0.4f) + root.forward * -0.3f, 0.6f);
        }

        // ---------------- Ambient: parrots ----------------

        public static void SpawnFlock(Vector3 from, Vector3 to, float speed)
        {
            Init();
            var flock = new GameObject("ParrotFlock").AddComponent<BirdFlock>();
            flock.Init(from, to, speed, ball);
        }
    }

    /// <summary>Python body made of overlapping tube segments that follow a travelling wave.</summary>
    public class SnakeBody : MonoBehaviour
    {
        const int Count = 14;
        const float Length = 2.5f;
        readonly Transform[] segs = new Transform[Count];
        readonly float[] radii = new float[Count];
        Transform head, tongue;
        float phase;

        public void Build(Transform t, Mesh segment, Mesh ball, Mesh tongueMesh)
        {
            var skin = Mats.SnakeSkin;
            var belly = Mats.Plain(new Color(0.78f, 0.72f, 0.52f), 0.45f);
            var eye = Mats.Emissive("SnakeEye", Tex.Metal, new Color(0.8f, 0.7f, 0.1f), 0.95f, new Color(0.35f, 0.3f, 0.02f));
            for (int i = 0; i < Count; i++)
            {
                float k = i / (Count - 1f);
                radii[i] = Mathf.Lerp(0.07f, 0.14f, Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.8f))) + 0.02f;
                segs[i] = Prim.Shape(segment, "Seg" + i, t, Vector3.zero, Vector3.one, skin).transform;
            }
            head = Prim.Group("Head", t, Vector3.zero);
            Prim.Shape(ball, "Skull", head, new Vector3(0f, 0f, 0f), new Vector3(0.2f, 0.12f, 0.28f), skin);
            Prim.Shape(ball, "Jaw", head, new Vector3(0f, -0.035f, -0.03f), new Vector3(0.17f, 0.07f, 0.24f), belly);
            for (int s = -1; s <= 1; s += 2)
                Prim.Shape(ball, "Eye", head, new Vector3(s * 0.07f, 0.035f, -0.07f), Vector3.one * 0.04f, eye, null, false);
            tongue = Prim.Shape(tongueMesh, "Tongue", head, new Vector3(0f, -0.02f, -0.16f), Vector3.one,
                                Mats.Plain(new Color(0.6f, 0.08f, 0.1f), 0.5f), null, false).transform;
            phase = Random.value * 6f;
            Pose(0f);
        }

        void Update()
        {
            phase += Time.deltaTime * 3.2f;
            Pose(phase);
        }

        Vector3 Spine(float k, float p)
        {
            float x = Mathf.Lerp(Length * 0.5f, -Length * 0.5f, k);  // tail on the right, head on the left
            float z = 0.22f * Mathf.Sin(k * Mathf.PI * 2.6f - p);
            return new Vector3(x, 0f, z);
        }

        void Pose(float p)
        {
            for (int i = 0; i < Count; i++)
            {
                float k0 = i / (float)Count, k1 = (i + 1) / (float)Count;
                Vector3 a = Spine(k0, p), b = Spine(k1, p);
                a.y = b.y = radii[i] * 0.85f;
                var dir = b - a;
                var seg = segs[i];
                seg.localPosition = a;
                seg.localRotation = Quaternion.FromToRotation(Vector3.down, dir.normalized);
                seg.localScale = new Vector3(radii[i], dir.magnitude / 0.22f * 1.25f, radii[i] * 0.85f);
            }
            // Head raised slightly, watching the runner, with a flicking tongue.
            Vector3 h = Spine(1f, p) + new Vector3(-0.12f, 0.2f + 0.04f * Mathf.Sin(p * 0.7f), -0.05f);
            head.localPosition = h;
            head.localRotation = Quaternion.Euler(-12f, -20f + 15f * Mathf.Sin(p * 0.5f), 0f);
            float flick = Mathf.Repeat(p * 0.8f, 2f) < 0.3f ? 1f : 0.2f;
            tongue.localScale = new Vector3(1f, 1f, flick);
        }
    }

    /// <summary>
    /// Brown bear behaviour: lumbers about on all fours while you're far away, then rears up on
    /// its hind legs, roars and swipes as you get close (always fully upright by the time you arrive).
    /// </summary>
    public class BearRig : MonoBehaviour
    {
        const float RearDistance = 36f;

        public Transform Model, Spine, Head, Jaw, ArmL, ArmR, LegL, LegR;

        Obstacle ob;
        float rear;
        float phase;

        void Start()
        {
            ob = GetComponent<Obstacle>();
            phase = Random.value * 10f;
            rear = 0f;
        }

        void Update()
        {
            if (ob != null && ob.Handled) return;
            var gm = GameManager.I;
            float gap = gm != null && gm.Player != null && ob != null ? ob.S - gm.Player.TrackS : 999f;
            float target = gap < RearDistance && gap > -6f ? 1f : 0f;
            rear = Mathf.MoveTowards(rear, target, Time.deltaTime * 1.6f);
            phase += Time.deltaTime;
            float r = Mathf.SmoothStep(0f, 1f, rear);
            Pose(r, 1f - r, phase);
        }

        /// <param name="rear">0 = on all fours, 1 = reared up.</param>
        /// <param name="walk">How much of the lumbering walk to apply.</param>
        public void Pose(float rear, float walk, float t)
        {
            float breath = Mathf.Sin(t * 2.2f);
            float step = Mathf.Sin(t * 3.2f) * walk;

            // On all fours the whole upper body pitches forward over the hips.
            float spineX = Mathf.Lerp(-74f, -6f, rear) + breath * 1.5f;
            float sway = Mathf.Sin(t * 1.3f) * Mathf.Lerp(3f, 6f, rear);
            Spine.localRotation = Quaternion.Euler(spineX, 0f, sway);
            Model.localPosition = new Vector3(0f, Mathf.Lerp(-0.05f, 0f, rear) + Mathf.Abs(step) * 0.03f, 0f);
            // While on all fours it turns about, sniffing the air.
            Model.localRotation = Quaternion.Euler(0f, walk * 28f * Mathf.Sin(t * 0.45f), 0f);

            float hip = Mathf.Lerp(-4f, 8f, rear);
            LegL.localRotation = Quaternion.Euler(hip + 22f * step, 0f, 0f);
            LegR.localRotation = Quaternion.Euler(hip - 22f * step, 0f, 0f);

            // Roar: head tips back and the jaw drops open.
            float roar = rear * Mathf.Max(0f, Mathf.Sin(t * 1.4f));
            float headX = -spineX * Mathf.Lerp(0.85f, 0.2f, rear) + Mathf.Lerp(8f * Mathf.Sin(t * 1.7f), 26f * roar, rear);
            Head.localRotation = Quaternion.Euler(headX, 18f * walk * Mathf.Sin(t * 0.9f), 0f);
            Jaw.localRotation = Quaternion.Euler(-Mathf.Lerp(4f, 38f, roar), 0f, 0f);

            // On all fours the arms are front legs (held vertical, stepping); reared up they swipe.
            float frontL = -spineX - 22f * step, frontR = -spineX + 22f * step;
            float swipeL = -115f + 35f * Mathf.Sin(t * 2.6f);
            float swipeR = -115f + 35f * Mathf.Sin(t * 2.6f + 2.2f);
            ArmL.localRotation = Quaternion.Euler(Mathf.Lerp(frontL, swipeL, rear), 0f, Mathf.Lerp(0f, -28f, rear));
            ArmR.localRotation = Quaternion.Euler(Mathf.Lerp(frontR, swipeR, rear), 0f, Mathf.Lerp(0f, 28f, rear));
            float reach = Mathf.Lerp(1.4f, 1f, rear);
            ArmL.localScale = ArmR.localScale = new Vector3(1f, reach, 1f);
        }
    }

    /// <summary>Slow aimless drift for animals idling in water.</summary>
    public class Drifter : MonoBehaviour
    {
        Vector3 vel;
        float bob;
        Vector3 start;

        public void Init(Vector3 velocity, float bobAmount)
        {
            vel = velocity;
            bob = bobAmount;
            start = transform.position;
        }

        void Update()
        {
            float t = Time.time;
            transform.position = start + vel * Mathf.Sin(t * 0.15f) * 4f + Vector3.up * (Mathf.Sin(t * 0.9f) * 0.03f * bob);
        }
    }

    /// <summary>A small flock of parrots flapping across the sky, then removing itself.</summary>
    public class BirdFlock : MonoBehaviour
    {
        struct Bird { public Transform Root, WingL, WingR; public Vector3 Offset; public float Phase; }

        readonly List<Bird> birds = new List<Bird>();
        Vector3 from, to;
        float speed, t, duration;

        public void Init(Vector3 a, Vector3 b, float spd, Mesh ball)
        {
            from = a; to = b; speed = spd;
            duration = Vector3.Distance(a, b) / spd;
            Color[] plumage =
            {
                new Color(0.85f, 0.12f, 0.1f), new Color(0.1f, 0.45f, 0.85f), new Color(0.15f, 0.6f, 0.2f), new Color(0.95f, 0.75f, 0.1f)
            };
            var wingMesh = Meshes.RoundedBox(new Vector3(0.42f, 0.02f, 0.16f), 0.01f);
            var tail = Meshes.RoundedBox(new Vector3(0.08f, 0.015f, 0.32f), 0.006f);
            var beak = Mats.Plain(new Color(0.15f, 0.13f, 0.12f), 0.5f);
            int n = Random.Range(4, 8);
            for (int i = 0; i < n; i++)
            {
                var mat = Mats.Plain(plumage[Random.Range(0, plumage.Length)], 0.45f);
                var root = new GameObject("Parrot").transform;
                root.SetParent(transform, false);
                Prim.Shape(ball, "Body", root, Vector3.zero, new Vector3(0.14f, 0.14f, 0.34f), mat, null, false);
                Prim.Shape(ball, "Head", root, new Vector3(0f, 0.04f, 0.17f), Vector3.one * 0.11f, mat, null, false);
                Prim.Shape(ball, "Beak", root, new Vector3(0f, 0.02f, 0.23f), new Vector3(0.04f, 0.05f, 0.05f), beak, null, false);
                Prim.Shape(tail, "Tail", root, new Vector3(0f, -0.01f, -0.28f), Vector3.one, mat, null, false);
                var wl = Prim.Group("WingL", root, new Vector3(-0.05f, 0.03f, 0f));
                var wr = Prim.Group("WingR", root, new Vector3(0.05f, 0.03f, 0f));
                Prim.Shape(wingMesh, "Wing", wl, new Vector3(-0.2f, 0f, 0f), Vector3.one, mat, null, false);
                Prim.Shape(wingMesh, "Wing", wr, new Vector3(0.2f, 0f, 0f), Vector3.one, mat, null, false);
                birds.Add(new Bird
                {
                    Root = root, WingL = wl, WingR = wr, Phase = Random.value * 6f,
                    Offset = new Vector3(Random.Range(-3f, 3f), Random.Range(-1.5f, 1.5f), Random.Range(-3f, 3f))
                });
            }
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = t / duration;
            if (k > 1f) { Destroy(gameObject); return; }
            var pos = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 4f;
            var dir = (to - from).normalized;
            var rot = Quaternion.LookRotation(dir);
            foreach (var b in birds)
            {
                float flap = Mathf.Sin(Time.time * 14f + b.Phase);
                b.Root.SetPositionAndRotation(pos + rot * b.Offset + Vector3.up * flap * 0.08f, rot * Quaternion.Euler(-flap * 4f, 0f, 0f));
                b.WingL.localRotation = Quaternion.Euler(0f, 0f, -flap * 45f);
                b.WingR.localRotation = Quaternion.Euler(0f, 0f, flap * 45f);
            }
        }
    }
}
