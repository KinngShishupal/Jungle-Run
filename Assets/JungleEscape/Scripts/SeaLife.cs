using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// Ambient animals for the water world's open sea: leaping dolphin pods, sea turtles paddling at the
    /// surface, circling shark fins, distant whales that spout and dive, and fish jumping near the boardwalk.
    /// They are purely scenic (no colliders) and live on their chunk, so they vanish with it.
    /// The sea surface is opaque, so anything below it is hidden: animals "teleport" only while submerged.
    /// </summary>
    public static class SeaLife
    {
        static Mesh ball, body, fin, bigBody;
        static readonly Color Spray = new Color(0.92f, 0.97f, 1f, 0.75f);

        static void Init()
        {
            if (ball != null) return;
            ball = Meshes.Ball(10, 14);
            // Streamlined torpedo along +Y (rotated to face -Z when used): blunt head, long tapering tail stock.
            body = Meshes.Lathe(new[]
            {
                new Vector2(0.001f, 0f), new Vector2(0.10f, 0.08f), new Vector2(0.22f, 0.25f), new Vector2(0.30f, 0.45f),
                new Vector2(0.30f, 0.62f), new Vector2(0.22f, 0.8f), new Vector2(0.12f, 0.93f), new Vector2(0.001f, 1f)
            }, 14, 1f, "SeaBody");
            fin = Meshes.Lathe(new[] { new Vector2(0.5f, 0f), new Vector2(0.32f, 0.45f), new Vector2(0.1f, 0.85f), new Vector2(0.001f, 1f) }, 8, 1f, "Fin");
            bigBody = body;
        }

        static GameObject P(Mesh m, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null, bool shadows = false)
            => Prim.Shape(m, name, parent, pos, scale, mat, euler, shadows);

        static Material Skin(string key, Color c, float smooth = 0.75f) =>
            Mats.Lit(key, Tex.Skin, c, smooth, tiling: new Vector2(2f, 2f), bump: 0.5f);

        static Material Eye => Mats.Plain(new Color(0.03f, 0.03f, 0.03f), 0.9f);

        /// <summary>Root placed on the sea surface at track position (x, s), facing along the trail (+yaw).</summary>
        static Transform Root(string name, Transform parent, float x, float y, float s, float yaw)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            TrackPath.Place(root, x, y, s, yaw);
            return root;
        }

        // ---------------- Dolphins ----------------

        /// <summary>Builds one dolphin under <paramref name="parent"/>, nose toward -Z, about 2.2 m long.</summary>
        static Transform Dolphin(Transform parent, float size)
        {
            var back = Skin("DolphinBack", new Color(0.36f, 0.43f, 0.5f), 0.85f);
            var belly = Skin("DolphinBelly", new Color(0.85f, 0.87f, 0.88f), 0.8f);
            var d = Prim.Group("Dolphin", parent, Vector3.zero);
            d.localScale = Vector3.one * size;
            P(body, "Body", d, new Vector3(0f, 0f, -1.1f), new Vector3(1.1f, 2.2f, 1.15f), back, new Vector3(90f, 0f, 0f));
            P(body, "Belly", d, new Vector3(0f, -0.07f, -1.0f), new Vector3(0.95f, 1.9f, 0.9f), belly, new Vector3(90f, 0f, 0f));
            P(ball, "Melon", d, new Vector3(0f, 0.04f, -0.92f), new Vector3(0.42f, 0.4f, 0.5f), back);
            P(ball, "Beak", d, new Vector3(0f, -0.06f, -1.18f), new Vector3(0.13f, 0.11f, 0.38f), back);
            P(fin, "Dorsal", d, new Vector3(0f, 0.27f, 0.05f), new Vector3(0.06f, 0.42f, 0.5f), back, new Vector3(55f, 0f, 0f));
            for (int k = -1; k <= 1; k += 2)
            {
                P(fin, "Flipper", d, new Vector3(k * 0.24f, -0.15f, -0.5f), new Vector3(0.22f, 0.36f, 0.04f), back, new Vector3(40f, 0f, k * 115f));
                P(ball, "Eye", d, new Vector3(k * 0.19f, 0.0f, -0.82f), Vector3.one * 0.045f, Eye);
            }
            var tail = Prim.Group("Flukes", d, new Vector3(0f, 0f, 1.05f));
            P(ball, "Fluke", tail, new Vector3(0f, 0f, 0.12f), new Vector3(0.75f, 0.05f, 0.24f), back);
            return d;
        }

        public static void SpawnDolphins(Transform parent, float x, float y, float s, float yaw, int count)
        {
            Init();
            var root = Root("DolphinPod", parent, x, y, s, yaw);
            var pod = root.gameObject.AddComponent<DolphinPod>();
            pod.Members = new Transform[count];
            pod.Tails = new Transform[count];
            pod.Offsets = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                pod.Members[i] = Dolphin(root, Random.Range(0.85f, 1.1f) * (i == count - 1 && count > 2 ? 0.6f : 1f)); // sometimes a calf
                pod.Tails[i] = pod.Members[i].Find("Flukes");
                pod.Offsets[i] = new Vector3(Random.Range(-1.6f, 1.6f), 0f, i * 1.8f + Random.Range(-0.4f, 0.4f));
            }
        }

        // ---------------- Sea turtle ----------------

        public static void SpawnTurtle(Transform parent, float x, float y, float s, float yaw)
        {
            Init();
            var shell = Mats.Lit("TurtleShell", Tex.SnakeSkin, new Color(0.45f, 0.38f, 0.22f), 0.6f, tiling: new Vector2(3f, 3f), bump: 1.8f);
            var skin = Skin("TurtleSkin", new Color(0.5f, 0.52f, 0.38f), 0.5f);
            var root = Root("SeaTurtle", parent, x, y, s, yaw);
            var t = root.gameObject.AddComponent<TurtleSwim>();
            var b = t.Body = Prim.Group("Body", root, Vector3.zero);
            P(ball, "Shell", b, Vector3.zero, new Vector3(0.95f, 0.32f, 1.15f), shell, null, true);
            P(ball, "Plastron", b, new Vector3(0f, -0.08f, 0f), new Vector3(0.85f, 0.18f, 1.05f), skin);
            t.Head = Prim.Group("Head", b, new Vector3(0f, 0f, -0.55f));
            P(ball, "Neck", t.Head, new Vector3(0f, 0f, -0.1f), new Vector3(0.2f, 0.18f, 0.3f), skin);
            P(ball, "Skull", t.Head, new Vector3(0f, 0.03f, -0.28f), new Vector3(0.22f, 0.18f, 0.27f), skin);
            for (int k = -1; k <= 1; k += 2) P(ball, "Eye", t.Head, new Vector3(k * 0.09f, 0.06f, -0.32f), Vector3.one * 0.035f, Eye);
            t.Flippers = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float k = i % 2 == 0 ? -1f : 1f;
                bool front = i < 2;
                var f = t.Flippers[i] = Prim.Group("Flipper", b, new Vector3(k * 0.4f, -0.05f, front ? -0.3f : 0.42f));
                P(ball, "Blade", f, new Vector3(k * (front ? 0.32f : 0.14f), 0f, front ? 0.08f : 0.08f),
                  front ? new Vector3(0.7f, 0.05f, 0.24f) : new Vector3(0.28f, 0.05f, 0.2f), skin, new Vector3(0f, k * (front ? -25f : 30f), 0f));
            }
        }

        // ---------------- Shark ----------------

        public static void SpawnShark(Transform parent, float x, float y, float s, float radius)
        {
            Init();
            var grey = Skin("SharkSkin", new Color(0.4f, 0.44f, 0.48f), 0.7f);
            var root = Root("Shark", parent, x, y, s, 0f);
            var sh = root.gameObject.AddComponent<SharkCircle>();
            sh.Radius = radius;
            var b = sh.Body = Prim.Group("Body", root, Vector3.zero);
            // Mostly submerged: only the dorsal fin and tail tip cut the surface.
            P(body, "Body", b, new Vector3(0f, -0.45f, -1.4f), new Vector3(1.0f, 2.8f, 0.95f), grey, new Vector3(90f, 0f, 0f));
            P(fin, "Dorsal", b, new Vector3(0f, -0.2f, -0.1f), new Vector3(0.05f, 0.75f, 0.85f), grey, new Vector3(30f, 0f, 0f));
            sh.Tail = Prim.Group("Tail", b, new Vector3(0f, -0.4f, 1.35f));
            P(fin, "Caudal", sh.Tail, Vector3.zero, new Vector3(0.05f, 0.55f, 0.55f), grey, new Vector3(40f, 0f, 0f));
        }

        // ---------------- Whale ----------------

        public static void SpawnWhale(Transform parent, float x, float y, float s, float yaw)
        {
            Init();
            var dark = Skin("WhaleSkin", new Color(0.18f, 0.21f, 0.26f), 0.7f);
            var pale = Skin("WhaleBelly", new Color(0.75f, 0.77f, 0.78f), 0.6f);
            var root = Root("Whale", parent, x, y, s, yaw);
            var w = root.gameObject.AddComponent<WhaleSurface>();
            var b = w.Body = Prim.Group("Body", root, Vector3.zero);
            P(bigBody, "Body", b, new Vector3(0f, 0f, -6f), new Vector3(7f, 13f, 6f), dark, new Vector3(90f, 0f, 0f), true);
            P(bigBody, "Throat", b, new Vector3(0f, -0.5f, -5.4f), new Vector3(5.5f, 7f, 4.2f), pale, new Vector3(90f, 0f, 0f));
            P(fin, "Dorsal", b, new Vector3(0f, 1.6f, 2.4f), new Vector3(0.12f, 0.6f, 0.9f), dark, new Vector3(50f, 0f, 0f));
            for (int k = -1; k <= 1; k += 2)
                P(fin, "Pectoral", b, new Vector3(k * 1.8f, -0.7f, -2.5f), new Vector3(0.9f, 4f, 0.12f), pale, new Vector3(-70f, 0f, k * 100f));
            w.Flukes = Prim.Group("Flukes", b, new Vector3(0f, 0f, 6.6f));
            P(ball, "Fluke", w.Flukes, new Vector3(0f, 0f, 0.7f), new Vector3(4.2f, 0.2f, 1.4f), dark, null, true);
            w.Blowhole = Prim.Group("Blowhole", b, new Vector3(0f, 2.1f, -3.2f));
        }

        // ---------------- Jumping fish ----------------

        public static void SpawnFishSchool(Transform parent, float x, float y, float s, float spread)
        {
            Init();
            var silver = Mats.Lit("FishScales", Tex.Metal, new Color(0.75f, 0.8f, 0.85f), 0.9f, metallic: 0.6f);
            var root = Root("FishSchool", parent, x, y, s, 0f);
            var f = root.gameObject.AddComponent<FishJumps>();
            f.Spread = spread;
            f.Fish = new Transform[3];
            for (int i = 0; i < f.Fish.Length; i++)
            {
                var fish = f.Fish[i] = Prim.Group("Fish", root, Vector3.down * 5f);
                P(body, "Body", fish, new Vector3(0f, 0f, -0.18f), new Vector3(0.22f, 0.38f, 0.28f), silver, new Vector3(90f, 0f, 0f));
                P(fin, "Tail", fish, new Vector3(0f, 0f, 0.2f), new Vector3(0.02f, 0.12f, 0.14f), silver, new Vector3(90f, 0f, 0f));
            }
        }

        public static void Splash(Vector3 pos, int count, float size) => Fx.Dust(pos, count, size, Spray);
    }

    /// <summary>A pod swimming in formation, porpoising out of the water in graceful arcs.</summary>
    public class DolphinPod : MonoBehaviour
    {
        public Transform[] Members, Tails;
        public Vector3[] Offsets;
        const float Length = 26f, Speed = 7f, LeapLen = 4.2f, LeapHeight = 1.7f;
        float travel;
        float[] prevY;

        void Start()
        {
            travel = Random.value * Length;
            prevY = new float[Members.Length];
        }

        void Update()
        {
            travel += Speed * Time.deltaTime;
            for (int i = 0; i < Members.Length; i++)
            {
                // Each dolphin runs down a lane of the pod's track; wraps around while out of sight underwater.
                float d = Mathf.Repeat(travel - Offsets[i].z, Length);
                float leapStart = 6f + (i % 2) * 1.1f;
                float y, pitch;
                // Two leaps per pass, then a long underwater stretch.
                float u1 = (d - leapStart) / LeapLen, u2 = (d - leapStart - 9f) / LeapLen;
                float u = u1 >= 0f && u1 <= 1f ? u1 : u2 >= 0f && u2 <= 1f ? u2 : -1f;
                if (u >= 0f)
                {
                    y = LeapHeight * 4f * u * (1f - u) - 0.6f;
                    float slope = LeapHeight * 4f * (1f - 2f * u) / LeapLen; // dy/dd
                    pitch = Mathf.Atan(slope) * Mathf.Rad2Deg;
                }
                else { y = -1.6f; pitch = 0f; }

                var m = Members[i];
                m.localPosition = new Vector3(Offsets[i].x + 0.4f * Mathf.Sin(travel * 0.3f + i), y, -d + Length * 0.5f);
                // Positive X pitch raises the nose (-Z facing): nose up on the way out, down on re-entry.
                m.localRotation = Quaternion.Euler(pitch, 0f, 3f * Mathf.Sin(travel * 0.5f + i));
                if (Tails[i] != null) Tails[i].localRotation = Quaternion.Euler(18f * Mathf.Sin(Time.time * 7f + i), 0f, 0f);

                // Splash as it breaks the surface and as it dives back in
                if ((prevY[i] < 0f) != (y < 0f) && Mathf.Abs(prevY[i] - y) < 1f)
                    SeaLife.Splash(m.position, 5, 0.55f * m.localScale.x);
                prevY[i] = y;
            }
        }
    }

    /// <summary>A sea turtle paddling slowly in a wide loop, lifting its head to breathe.</summary>
    public class TurtleSwim : MonoBehaviour
    {
        public Transform Body, Head;
        public Transform[] Flippers;
        Vector3 centre;
        float angle, radius, dir, time;

        void Start()
        {
            centre = transform.localPosition;
            radius = Random.Range(2f, 4f);
            dir = Random.value < 0.5f ? -1f : 1f;
            angle = Random.value * Mathf.PI * 2f;
            time = Random.value * 10f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            angle += dir * dt * 0.55f / radius;
            transform.localPosition = centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            // Face the direction of travel (-Z forward)
            var tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * dir;
            transform.localRotation = Quaternion.LookRotation(-tangent, Vector3.up);

            float stroke = Mathf.Sin(time * 2.2f);
            Body.localPosition = new Vector3(0f, 0.04f * Mathf.Sin(time * 2.2f + 1f) - 0.08f, 0f);
            Body.localRotation = Quaternion.Euler(2f * stroke, 0f, 3f * Mathf.Sin(time * 0.7f));
            float breathe = Mathf.Max(0f, Mathf.Sin(time * 0.35f));
            Head.localRotation = Quaternion.Euler(22f * breathe * breathe, 10f * Mathf.Sin(time * 0.5f), 0f);
            for (int i = 0; i < Flippers.Length; i++)
            {
                float k = i % 2 == 0 ? -1f : 1f;
                bool front = i < 2;
                // Front flippers fly in a figure-eight; rear ones steer.
                float a = front ? 35f * stroke : 12f * Mathf.Sin(time * 1.1f + i);
                float sweep = front ? 25f * Mathf.Cos(time * 2.2f) : 0f;
                Flippers[i].localRotation = Quaternion.Euler(0f, k * sweep, k * a);
            }
        }
    }

    /// <summary>A shark circling: only the dorsal fin and tail tip slice through the surface.</summary>
    public class SharkCircle : MonoBehaviour
    {
        public Transform Body, Tail;
        public float Radius = 5f;
        Vector3 centre;
        float angle, dir, time, splashT;

        void Start()
        {
            centre = transform.localPosition;
            dir = Random.value < 0.5f ? -1f : 1f;
            angle = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            angle += dir * dt * 3.2f / Radius;
            transform.localPosition = centre + new Vector3(Mathf.Cos(angle) * Radius, 0f, Mathf.Sin(angle) * Radius);
            var tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * dir;
            transform.localRotation = Quaternion.LookRotation(-tangent, Vector3.up) * Quaternion.Euler(0f, 0f, -dir * 8f); // banks into the turn
            Body.localRotation = Quaternion.Euler(0f, 6f * Mathf.Sin(time * 4f), 0f);
            Tail.localRotation = Quaternion.Euler(0f, 22f * Mathf.Sin(time * 4f - 1f), 0f);
            if ((splashT -= dt) <= 0f)
            {
                splashT = Random.Range(0.25f, 0.45f);
                SeaLife.Splash(Body.position + transform.forward * -0.2f, 1, 0.35f); // wake behind the fin
            }
        }
    }

    /// <summary>A distant whale: surfaces with an arching back, blows a spout, then flicks its flukes and dives.</summary>
    public class WhaleSurface : MonoBehaviour
    {
        public Transform Body, Flukes, Blowhole;
        const float Period = 16f;
        float time;
        bool blown, slapped;

        void Start() => time = Random.value * Period;

        void Update()
        {
            time += Time.deltaTime;
            float c = Mathf.Repeat(time, Period) / Period;
            // 0-0.15 rise, 0.15-0.45 cruise at the surface, 0.45-0.65 arch and dive, rest hidden underwater.
            float y, pitch, fluke = 0f;
            if (c < 0.15f) { float u = c / 0.15f; y = Mathf.Lerp(-4f, -1.4f, Mathf.SmoothStep(0f, 1f, u)); pitch = 6f * (1f - u); }
            else if (c < 0.45f) { y = -1.4f + 0.12f * Mathf.Sin(time * 1.2f); pitch = 0f; }
            else if (c < 0.65f)
            {
                float u = (c - 0.45f) / 0.2f;
                pitch = -38f * Mathf.SmoothStep(0f, 1f, u);         // nose down
                y = -1.4f + 0.6f * Mathf.Sin(u * Mathf.PI) - 1.5f * u * u;
                fluke = Mathf.Sin(u * Mathf.PI) * 30f;
            }
            else { y = -9f; pitch = 0f; blown = slapped = false; }

            Body.localPosition = new Vector3(0f, y, 0f);
            Body.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            Flukes.localRotation = Quaternion.Euler(fluke, 0f, 0f);

            if (!blown && c > 0.17f)
            {
                blown = true;
                // The blow: a tall column of mist
                for (int i = 0; i < 6; i++) SeaLife.Splash(Blowhole.position + Vector3.up * (i * 0.8f), 3, 1.6f + i * 0.25f);
            }
            if (!slapped && c > 0.6f)
            {
                slapped = true;
                SeaLife.Splash(Flukes.position, 8, 1.8f);
            }
        }
    }

    /// <summary>Small fish leaping out of the water now and then.</summary>
    public class FishJumps : MonoBehaviour
    {
        public Transform[] Fish;
        public float Spread = 4f;
        float[] t0;
        Vector3[] from, dir;
        float timer;

        void Start()
        {
            t0 = new float[Fish.Length];
            from = new Vector3[Fish.Length];
            dir = new Vector3[Fish.Length];
            for (int i = 0; i < t0.Length; i++) t0[i] = -10f;
            timer = Random.Range(0.5f, 3f);
        }

        void Update()
        {
            float now = Time.time;
            if ((timer -= Time.deltaTime) <= 0f)
            {
                timer = Random.Range(1.2f, 3.5f);
                for (int i = 0; i < Fish.Length; i++)
                {
                    if (now - t0[i] < 1f) continue;
                    t0[i] = now;
                    from[i] = new Vector3(Random.Range(-Spread, Spread), 0f, Random.Range(-Spread, Spread));
                    float a = Random.value * Mathf.PI * 2f;
                    dir[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    SeaLife.Splash(transform.TransformPoint(from[i]), 2, 0.25f);
                    break;
                }
            }
            for (int i = 0; i < Fish.Length; i++)
            {
                float u = (now - t0[i]) / 0.7f;
                var f = Fish[i];
                if (u < 0f || u > 1f) { f.localPosition = Vector3.down * 5f; continue; }
                f.localPosition = from[i] + dir[i] * (u * 1.4f) + Vector3.up * (0.9f * 4f * u * (1f - u) - 0.1f);
                float pitch = Mathf.Lerp(50f, -50f, u);
                f.localRotation = Quaternion.LookRotation(-dir[i], Vector3.up) * Quaternion.Euler(pitch, 0f, 0f);
                if (u > 0.93f && u - Time.deltaTime / 0.7f <= 0.93f) SeaLife.Splash(f.position, 2, 0.25f);
            }
        }
    }
}
