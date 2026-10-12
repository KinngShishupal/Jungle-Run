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
        static Mesh ball, tailMesh, limbThick, limbBear, claw;

        static void Init()
        {
            if (ball != null) return;
            ball = Meshes.Ball(12, 18);
            limbThick = Meshes.Limb(0.6f, 0.1f, 0.075f, 10, 7);
            limbBear = Meshes.Limb(0.7f, 0.15f, 0.11f, 10, 7);
            claw = Meshes.Lathe(new[] { new Vector2(0.001f, 0f), new Vector2(0.018f, 0.01f), new Vector2(0.008f, 0.06f), new Vector2(0.001f, 0.07f) }, 6);
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
            body.Build(t);
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

    /// <summary>
    /// Python crossing the trail. The body is one continuous mesh rebuilt every frame from a spine
    /// that slides along a fixed S-shaped track on the ground (real lateral undulation: the waves
    /// stay put while the snake flows through them). It rests while you're far off, starts crossing
    /// as you approach, raises its head to watch you and strikes if you get close.
    /// </summary>
    public class SnakeBody : MonoBehaviour
    {
        const float Length = 2.6f;
        const int Rings = 48, Sides = 12;
        const float Wavelength = 1.3f;
        const float Amplitude = 0.23f;
        const float MaxTravel = 0.85f;
        const float WakeDistance = 32f;
        const float TableStep = 0.01f;

        Mesh mesh;
        int[] tris;
        bool windingChecked;
        Vector3[] verts, norms;
        Vector4[] tans;
        readonly Vector3[] centres = new Vector3[Rings];
        readonly float[] arcTable = new float[900]; // cumulative arc length of the ground track
        Transform head, jaw, tongue;
        BoxCollider col;
        Obstacle ob;
        float dir, originX, sHead0;
        float travel, alert, time, flickTimer, strikeTime = -1f;
        bool struck;

        public void Build(Transform t)
        {
            col = t.GetComponent<BoxCollider>();
            dir = Random.value < 0.5f ? 1f : -1f;
            time = Random.value * 10f;
            flickTimer = Random.Range(0.5f, 2f);

            // Arc-length table of the ground track so the body keeps its true length on the curves.
            float acc = 0f;
            arcTable[0] = 0f;
            for (int i = 1; i < arcTable.Length; i++)
            {
                acc += Vector3.Distance(TrackRaw((i - 1) * TableStep), TrackRaw(i * TableStep));
                arcTable[i] = acc;
            }
            sHead0 = Length + 0.6f;
            // Start with the body centred a little to one side so it ends centred the other way.
            float uMid = UAt(sHead0 - Length * 0.5f);
            originX = dir * 0.4f + dir * uMid;

            // Fixed topology: UVs and triangles never change, only vertex positions.
            int n = Rings * (Sides + 1);
            verts = new Vector3[n];
            norms = new Vector3[n];
            tans = new Vector4[n];
            var uvs = new Vector2[n];
            for (int i = 0; i < Rings; i++)
                for (int k = 0; k <= Sides; k++)
                    uvs[i * (Sides + 1) + k] = new Vector2(k / (float)Sides, i / (Rings - 1f));
            tris = new int[(Rings - 1) * Sides * 6];
            int ti = 0;
            for (int i = 0; i < Rings - 1; i++)
                for (int k = 0; k < Sides; k++)
                {
                    int a = i * (Sides + 1) + k, b = a + 1, c = a + Sides + 1, d = c + 1;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
            mesh = new Mesh { name = "SnakeBody" };
            mesh.MarkDynamic();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            var bodyGo = Prim.Shape(mesh, "Body", t, Vector3.zero, Vector3.one, Mats.SnakeSkin);
            bodyGo.AddComponent<OwnedMeshes>().Meshes.Add(mesh);

            BuildHead(t);
            alert = 1f; // shown alert (head up) in previews; resets when the game runs
            Pose(0f);
        }

        void BuildHead(Transform t)
        {
            var ball = Meshes.Ball(10, 14);
            var skin = Mats.SnakeSkin;
            var belly = Mats.Plain(new Color(0.80f, 0.74f, 0.56f), 0.45f);
            var eye = Mats.Emissive("SnakeEye", Tex.Metal, new Color(0.85f, 0.7f, 0.12f), 0.95f, new Color(0.35f, 0.28f, 0.02f));
            var black = Mats.Plain(new Color(0.02f, 0.02f, 0.02f), 0.9f);
            var pink = Mats.Plain(new Color(0.55f, 0.06f, 0.1f), 0.5f);

            // Built facing +Z, then aimed along the spine every frame.
            head = Prim.Group("Head", t, Vector3.zero);
            Prim.Shape(ball, "Skull", head, new Vector3(0f, 0.005f, 0.07f), new Vector3(0.13f, 0.075f, 0.2f), skin);
            Prim.Shape(ball, "Cheeks", head, new Vector3(0f, -0.005f, 0.02f), new Vector3(0.15f, 0.07f, 0.12f), skin);
            Prim.Shape(ball, "Snout", head, new Vector3(0f, -0.004f, 0.16f), new Vector3(0.085f, 0.05f, 0.07f), skin);
            for (int s = -1; s <= 1; s += 2)
            {
                Prim.Shape(ball, "Eye", head, new Vector3(s * 0.052f, 0.022f, 0.1f), Vector3.one * 0.034f, eye, null, false);
                Prim.Shape(Meshes.RoundedBox(new Vector3(0.006f, 0.022f, 0.006f), 0.002f), "Pupil", head,
                           new Vector3(s * 0.066f, 0.022f, 0.104f), Vector3.one, black, new Vector3(0f, s * 60f, 0f), false);
                Prim.Shape(ball, "Nostril", head, new Vector3(s * 0.022f, 0.012f, 0.19f), Vector3.one * 0.012f, black, null, false);
            }
            jaw = Prim.Group("Jaw", head, new Vector3(0f, -0.022f, 0.02f));
            Prim.Shape(ball, "LowerJaw", jaw, new Vector3(0f, -0.008f, 0.075f), new Vector3(0.11f, 0.03f, 0.17f), belly);
            Prim.Shape(ball, "MouthInside", jaw, new Vector3(0f, 0.004f, 0.08f), new Vector3(0.09f, 0.012f, 0.14f), pink, null, false);

            // Forked tongue (flicks out and back)
            tongue = Prim.Group("Tongue", head, new Vector3(0f, -0.018f, 0.17f));
            var strand = Meshes.RoundedBox(new Vector3(0.01f, 0.004f, 0.08f), 0.002f);
            Prim.Shape(strand, "Base", tongue, new Vector3(0f, 0f, 0.04f), Vector3.one, pink, null, false);
            Prim.Shape(strand, "ForkL", tongue, new Vector3(-0.008f, 0f, 0.1f), new Vector3(0.7f, 1f, 0.5f), pink, new Vector3(0f, -18f, 0f), false);
            Prim.Shape(strand, "ForkR", tongue, new Vector3(0.008f, 0f, 0.1f), new Vector3(0.7f, 1f, 0.5f), pink, new Vector3(0f, 18f, 0f), false);
        }

        void Start()
        {
            ob = GetComponent<Obstacle>();
            alert = 0f;
        }

        void Update()
        {
            if (ob != null && ob.Handled) return;
            float dt = Time.deltaTime;
            time += dt;
            var gm = GameManager.I;
            float gap = gm != null && gm.Player != null && ob != null ? ob.S - gm.Player.TrackS : 999f;

            // Rest, then cross the trail as the runner approaches.
            float speed = gap < WakeDistance ? 0.33f : 0.03f;
            travel = Mathf.Min(MaxTravel, travel + speed * dt);
            alert = Mathf.MoveTowards(alert, gap < 20f ? 1f : 0f, dt * 1.5f);

            // A single strike when the runner gets close.
            if (!struck && gap < 7f && gap > 0.5f) { struck = true; strikeTime = 0f; }
            if (strikeTime >= 0f) { strikeTime += dt; if (strikeTime > 0.5f) strikeTime = -1f; }

            Pose(dt);
        }

        // ---------------- Spine ----------------

        /// <summary>Ground track in local space, as a function of the raw sideways parameter u.</summary>
        Vector3 TrackRaw(float u) => new Vector3(-dir * u, 0f, Amplitude * Mathf.Sin(u * Mathf.PI * 2f / Wavelength));

        float UAt(float arc)
        {
            if (arc <= 0f) return arc;
            int lo = 0, hi = arcTable.Length - 1;
            if (arc >= arcTable[hi]) return hi * TableStep;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (arcTable[mid] < arc) lo = mid; else hi = mid;
            }
            float f = Mathf.InverseLerp(arcTable[lo], arcTable[hi], arc);
            return (lo + f) * TableStep;
        }

        Vector3 Track(float arc)
        {
            var p = TrackRaw(UAt(arc));
            p.x += originX;
            return p;
        }

        static float Radius(float t)
        {
            // t = 0 at the head, 1 at the tail tip: slim neck, thick middle, long tapering tail.
            if (t < 0.07f) return Mathf.Lerp(0.062f, 0.058f, t / 0.07f);
            if (t < 0.38f) return Mathf.Lerp(0.058f, 0.118f, Mathf.SmoothStep(0f, 1f, (t - 0.07f) / 0.31f));
            return 0.006f + 0.112f * Mathf.Pow(1f - (t - 0.38f) / 0.62f, 1.35f);
        }

        void Pose(float dt)
        {
            float sHead = sHead0 + travel;
            float breath = 1f + 0.025f * Mathf.Sin(time * 1.6f);
            float lunge = strikeTime >= 0f ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(strikeTime / 0.5f)) : 0f;

            // Spine centres: on the ground track, with the front lifted and swaying when alert.
            for (int i = 0; i < Rings; i++)
            {
                float t = i / (Rings - 1f);
                var p = Track(sHead - t * Length);
                float front = Tex.SS(0.3f, 0f, t);
                p.y = Radius(t) * 0.78f * breath + front * (0.12f + 0.22f * alert + 0.1f * lunge);
                p.z += front * (Mathf.Sin(time * 2.4f) * 0.07f * alert - 0.35f * lunge);
                centres[i] = p;
            }

            // Rings: flattened ellipse with a flatter belly, oriented along the spine.
            for (int i = 0; i < Rings; i++)
            {
                float t = i / (Rings - 1f);
                Vector3 fwd = i == 0 ? centres[0] - centres[1] : centres[i - 1] - centres[i];
                fwd.Normalize();
                var side = Vector3.Cross(Vector3.up, fwd).normalized;
                var up = Vector3.Cross(fwd, side);
                float r = Radius(t) * breath;
                float w = r * 1.15f;
                for (int k = 0; k <= Sides; k++)
                {
                    float a = k / (float)Sides * Mathf.PI * 2f;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    float h = sa < 0f ? r * 0.6f : r * 0.85f;
                    int vi = i * (Sides + 1) + k;
                    verts[vi] = centres[i] + side * (ca * w) + up * (sa * h);
                    norms[vi] = (side * (ca / w) + up * (sa / h)).normalized;
                    var tg = (side * (-sa * w) + up * (ca * h)).normalized;
                    tans[vi] = new Vector4(tg.x, tg.y, tg.z, 1f);
                }
            }
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.tangents = tans;
            mesh.RecalculateBounds();
            if (!windingChecked) FixWinding();

            // Head: on the front of the spine, turning to watch the runner (who comes from -Z).
            var neckDir = (centres[0] - centres[2]).normalized;
            var look = Vector3.Slerp(neckDir, new Vector3(0f, -0.15f, -1f).normalized, alert * 0.55f);
            head.localPosition = centres[0] + neckDir * 0.02f;
            head.localRotation = Quaternion.LookRotation(look, Vector3.up) * Quaternion.Euler(6f - 10f * lunge, 0f, 0f);
            jaw.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 32f, lunge), 0f, 0f);

            // Tongue: quick double flicks every second or two.
            flickTimer -= dt;
            float flick = 0f;
            if (flickTimer < 0f)
            {
                float k = -flickTimer / 0.4f;
                flick = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f));
                if (k >= 1f) flickTimer = Random.Range(0.8f, 2.2f) * (1.2f - 0.6f * alert);
            }
            tongue.localScale = new Vector3(1f, 1f, Mathf.Max(0.05f, flick));
            tongue.localRotation = Quaternion.Euler(-10f + 12f * Mathf.Sin(time * 40f) * flick, 0f, 0f);

            // Keep the hitbox on the body as it crosses.
            if (col != null)
            {
                float mid = (centres[0].x + centres[Rings - 1].x) * 0.5f;
                col.center = new Vector3(mid, 0.25f, 0f);
            }
        }

        /// <summary>Makes sure the triangles face outward (checked once on the first pose).</summary>
        void FixWinding()
        {
            windingChecked = true;
            Vector3 a = verts[tris[0]], b = verts[tris[1]], c = verts[tris[2]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            Vector3 outward = (a + b + c) / 3f - centres[0];
            if (Vector3.Dot(n, outward) >= 0f) return;
            for (int k = 0; k < tris.Length; k += 3) (tris[k + 1], tris[k + 2]) = (tris[k + 2], tris[k + 1]);
            mesh.triangles = tris;
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
