using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// A troop of cheeky monkeys chasing the player. They stay out of view
    /// normally, rush in close after a stumble, and catch you on a second one.
    ///
    /// Each monkey is a jointed rig (pelvis, spine, head and jaw, two-segment arms and legs, a six-part tail).
    /// Chasing, they gallop on all fours like real monkeys: the legs drive together, the arms reach and land,
    /// the spine flexes and the body flies between strides. Waiting, they sit, look around, scratch and
    /// wave. Close behind you they shriek; when they catch you they rise up and grab.
    /// Their fur changes with the world (brown jungle monkeys, snow macaques, red-orange coast monkeys,
    /// black horror monkeys with glowing eyes).
    /// </summary>
    public class MonkeyChaser : MonoBehaviour
    {
        const float FarDistance = 20f;
        const float CloseDistance = 3f;
        const float CatchDistance = 0.9f;
        const float IdleDistance = 3.2f;
        const float CloseTime = 7f;

        class Monkey
        {
            public Transform Root, Pelvis, Torso, Head, Jaw, Eyes;
            public Transform[] Shoulder = new Transform[2], Elbow = new Transform[2];
            public Transform[] Hip = new Transform[2], Knee = new Transform[2];
            public Transform[] Tail;
            public Vector3 Spot;
            public float Phase, Cycle, Run, Excite, Grab, BlinkT, Seed;
        }

        Transform player;
        readonly List<Monkey> troop = new List<Monkey>();
        readonly List<Renderer> furR = new List<Renderer>(), darkR = new List<Renderer>(), bellyR = new List<Renderer>(),
                                faceR = new List<Renderer>(), pupilR = new List<Renderer>();
        float distance = IdleDistance;
        float closeTimer;
        bool caught;
        bool idle = true;
        BiomeType skin = (BiomeType)(-1);

        public bool IsClose => !idle && closeTimer > 0f;

        public void Init(Transform target)
        {
            player = target;
            Build();
            ApplySkin(Biomes.Current);
        }

        public void Idle() { idle = true; caught = false; closeTimer = 0f; distance = IdleDistance; }
        public void BeginChase() { idle = false; caught = false; closeTimer = 2f; distance = IdleDistance; }
        public void Approach() { closeTimer = CloseTime; }
        public void Catch() { caught = true; }
        public void FallBack() { caught = false; closeTimer = 0f; }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || player == null) return;
            float dt = Time.deltaTime;

            if (skin != Biomes.Current) ApplySkin(Biomes.Current);
            if (closeTimer > 0f && gm.State == GameState.Playing) closeTimer -= dt;

            float target = idle ? IdleDistance
                         : caught ? CatchDistance
                         : closeTimer > 0f ? CloseDistance
                         : FarDistance;
            float rate = caught ? 6f : 1.5f;
            distance = Mathf.Lerp(distance, target, 1f - Mathf.Exp(-rate * dt));

            var pc = gm.Player;
            float ms = pc.TrackS - distance;
            transform.SetPositionAndRotation(TrackPath.Point(ms, pc.TrackX * 0.85f, 0f), TrackPath.Rotation(ms));

            bool running = !idle && gm.State == GameState.Playing;
            bool excited = !idle && (closeTimer > 0f || caught);
            float t = Time.time;
            foreach (var k in troop)
            {
                k.Run = Mathf.MoveTowards(k.Run, running ? 1f : 0f, dt * 2.5f);
                k.Excite = Mathf.MoveTowards(k.Excite, excited ? 1f : 0f, dt * 2f);
                k.Grab = Mathf.MoveTowards(k.Grab, caught ? 1f : 0f, dt * 3f);
                float hz = Mathf.Lerp(2.5f, 3.3f, k.Excite) * Mathf.Lerp(1f, 0.6f, k.Grab);
                k.Cycle += hz * dt * k.Run;

                // Each monkey weaves and surges a little inside the troop so they never move in lockstep.
                float weave = 0.3f * Mathf.Sin(t * 0.7f + k.Seed) * k.Run;
                float surge = 0.5f * Mathf.Sin(t * 0.9f + k.Seed * 2f) * k.Run;
                k.Root.localPosition = k.Spot + new Vector3(weave, 0f, surge);
                Pose(k, t);
            }
        }

        // ---------------- Animation ----------------

        void Pose(Monkey k, float t)
        {
            float w = k.Cycle * Mathf.PI * 2f + k.Phase;
            float run = k.Run, sit = 1f - run, grab = k.Grab;
            float breathe = Mathf.Sin(t * 2f + k.Seed);

            // --- Pelvis and spine (rig faces -Z: negative X pitches the front down) ---
            float airborne = 0.5f + 0.5f * Mathf.Sin(w - 0.6f);
            float pelvisY = Mathf.Lerp(0.22f + 0.006f * breathe, 0.36f + 0.16f * airborne, run);
            pelvisY = Mathf.Lerp(pelvisY, 0.46f, grab);
            k.Pelvis.localPosition = new Vector3(0f, pelvisY, 0f);
            float pelvisPitch = Mathf.Lerp(0f, -10f + 9f * Mathf.Sin(w + Mathf.PI), run);
            k.Pelvis.localRotation = Quaternion.Euler(Mathf.Lerp(pelvisPitch, 6f, grab), 0f, 2f * Mathf.Sin(w * 0.5f) * run);
            float torso = Mathf.Lerp(-8f + 1.5f * breathe, -40f + 7f * Mathf.Sin(w), run);
            k.Torso.localRotation = Quaternion.Euler(Mathf.Lerp(torso, 8f, grab), 0f, 0f);

            // --- Head: level with the ground while galloping, curious while sitting, shrieking when excited ---
            float look = Mathf.Sin(t * 0.6f + k.Seed) * 35f * sit;
            float shake = Mathf.Sin(t * 11f + k.Seed) * 9f * k.Excite;
            float headPitch = Mathf.Lerp(6f + 4f * Mathf.Sin(t * 0.4f + k.Seed), 36f - 6f * Mathf.Sin(w), run);
            k.Head.localRotation = Quaternion.Euler(Mathf.Lerp(headPitch, -6f, grab), look + shake, 6f * Mathf.Sin(t * 0.5f + k.Seed) * sit);
            float shriek = k.Excite * (0.5f + 0.5f * Mathf.Sin(t * 7f + k.Seed)) + 0.15f * sit * Mathf.Max(0f, Mathf.Sin(t * 0.8f + k.Seed * 3f));
            k.Jaw.localRotation = Quaternion.Euler(-30f * shriek, 0f, 0f);

            // Blink every few seconds
            k.BlinkT -= Time.deltaTime;
            if (k.BlinkT < -0.12f) k.BlinkT = Random.Range(2f, 5f);
            k.Eyes.localScale = new Vector3(1f, k.BlinkT < 0f ? 0.15f : 1f, 1f);

            // --- Arms: front legs of the gallop; scratch and wave while sitting; reach out to grab ---
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1f : 1f;
                float off = s * 0.35f;
                float sh = 38f + 48f * Mathf.Sin(w + off);
                float el = -12f - 45f * Mathf.Max(0f, Mathf.Cos(w + off));

                // Sitting: left arm rests on the knee; right arm now and then scratches the head or waves.
                float act = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Sin(t * 0.45f + k.Seed * 1.3f) * 3f - 1.5f));
                float sitSh = s == 0 ? 35f + 3f * breathe : Mathf.Lerp(30f, 165f, act);
                float sitEl = s == 0 ? -35f : Mathf.Lerp(-30f, -95f + 18f * Mathf.Sin(t * 13f), act);
                float sitZ = s == 0 ? 0f : side * act * 10f;

                float x = Mathf.Lerp(sitSh, sh, run);
                float e = Mathf.Lerp(sitEl, el, run);
                x = Mathf.Lerp(x, 105f + 12f * Mathf.Sin(t * 6f + s), grab);
                e = Mathf.Lerp(e, -15f, grab);
                k.Shoulder[s].localRotation = Quaternion.Euler(x, 0f, Mathf.Lerp(sitZ, side * -8f, run) + side * -12f * grab);
                k.Elbow[s].localRotation = Quaternion.Euler(e, 0f, 0f);
            }

            // --- Legs: drive the gallop together (slightly staggered); fold up when sitting ---
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1f : 1f;
                float lw = w + Mathf.PI + s * 0.3f;
                float hip = 8f + 42f * Mathf.Sin(lw);
                float knee = -15f - 58f * Mathf.Max(0f, Mathf.Cos(lw));
                float x = Mathf.Lerp(82f, hip, run);
                float kn = Mathf.Lerp(-100f, knee, run);
                x = Mathf.Lerp(x, 10f * Mathf.Sin(w + s * Mathf.PI), grab);
                kn = Mathf.Lerp(kn, -14f, grab);
                k.Hip[s].localRotation = Quaternion.Euler(x, 0f, side * Mathf.Lerp(14f, 4f, run));
                k.Knee[s].localRotation = Quaternion.Euler(kn, 0f, 0f);
            }

            // --- Tail: carried up in a question-mark curve; a wave travels down it ---
            for (int i = 0; i < k.Tail.Length; i++)
            {
                float wave = Mathf.Sin(t * (2.2f + 1.6f * run) - i * 0.7f + k.Seed);
                float baseX = i == 0 ? Mathf.Lerp(-120f, -105f, run) : -14f - 4f * i * run;
                k.Tail[i].localRotation = Quaternion.Euler(baseX + 6f * wave * run, 0f, (6f + 6f * sit) * wave);
            }
        }

        // ---------------- Construction ----------------

        void Build()
        {
            var placeholder = Mats.Fur(new Color(0.38f, 0.25f, 0.15f));
            var eyeWhite = Mats.Plain(new Color(0.92f, 0.88f, 0.8f), 0.85f);
            var mouth = Mats.Plain(new Color(0.25f, 0.06f, 0.07f), 0.6f);
            var teeth = Mats.Plain(new Color(0.95f, 0.92f, 0.82f), 0.7f);

            var ball = Meshes.Ball(12, 16);
            var upperArm = Meshes.Limb(0.3f, 0.06f, 0.048f, 8, 5);
            var foreArm = Meshes.Limb(0.29f, 0.048f, 0.038f, 8, 5);
            var thigh = Meshes.Limb(0.24f, 0.075f, 0.055f, 8, 5);
            var shin = Meshes.Limb(0.22f, 0.052f, 0.04f, 8, 5);
            var tailSeg = new Mesh[6];
            for (int i = 0; i < tailSeg.Length; i++) tailSeg[i] = Meshes.Limb(0.14f, 0.045f - i * 0.005f, 0.04f - i * 0.005f, 6, 3);

            Vector3[] spots = { new Vector3(-1.3f, 0f, 0.3f), new Vector3(0f, 0f, -0.6f), new Vector3(1.3f, 0f, 0.1f) };
            float[] sizes = { 1f, 1.12f, 0.9f };

            GameObject S(Mesh m, string n, Transform p, Vector3 pos, Vector3 sc, List<Renderer> role, Material mat = null, Vector3? eu = null, bool sh = true)
            {
                var go = Prim.Shape(m, n, p, pos, sc, mat ?? placeholder, eu, sh);
                role?.Add(go.GetComponent<Renderer>());
                return go;
            }

            for (int i = 0; i < spots.Length; i++)
            {
                var k = new Monkey { Spot = spots[i], Phase = i * 1.9f, Seed = i * 2.3f + 0.7f, BlinkT = Random.Range(0.5f, 4f) };
                k.Root = Prim.Group("Monkey" + i, transform, spots[i]);
                k.Root.localScale = Vector3.one * sizes[i];
                var rig = Prim.Group("Facing", k.Root, Vector3.zero);
                rig.localRotation = Quaternion.Euler(0f, 180f, 0f); // rig is built facing -Z; the troop runs toward +Z

                // Pelvis & hips
                k.Pelvis = Prim.Group("Pelvis", rig, new Vector3(0f, 0.46f, 0f));
                S(ball, "Hips", k.Pelvis, new Vector3(0f, 0.02f, 0.03f), new Vector3(0.32f, 0.28f, 0.3f), furR);

                // Spine: chest, belly, shoulders
                k.Torso = Prim.Group("Torso", k.Pelvis, new Vector3(0f, 0.06f, 0f));
                S(ball, "Chest", k.Torso, new Vector3(0f, 0.27f, -0.01f), new Vector3(0.4f, 0.48f, 0.34f), furR);
                S(ball, "Back", k.Torso, new Vector3(0f, 0.32f, 0.06f), new Vector3(0.36f, 0.36f, 0.26f), darkR);
                S(ball, "Belly", k.Torso, new Vector3(0f, 0.2f, -0.12f), new Vector3(0.26f, 0.34f, 0.16f), bellyR);

                // Head
                k.Head = Prim.Group("Head", k.Torso, new Vector3(0f, 0.54f, -0.05f));
                S(ball, "Skull", k.Head, new Vector3(0f, 0.1f, 0f), new Vector3(0.36f, 0.34f, 0.34f), furR);
                S(ball, "Crown", k.Head, new Vector3(0f, 0.2f, 0.03f), new Vector3(0.34f, 0.18f, 0.3f), darkR);
                S(ball, "Face", k.Head, new Vector3(0f, 0.08f, -0.13f), new Vector3(0.27f, 0.25f, 0.14f), faceR);
                S(ball, "Muzzle", k.Head, new Vector3(0f, 0.02f, -0.19f), new Vector3(0.17f, 0.11f, 0.12f), faceR);
                S(ball, "Brow", k.Head, new Vector3(0f, 0.15f, -0.18f), new Vector3(0.24f, 0.05f, 0.07f), darkR);
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    S(ball, "Nostril", k.Head, new Vector3(sd * 0.02f, 0.04f, -0.25f), Vector3.one * 0.02f, null, mouth, null, false);
                    S(ball, "Ear", k.Head, new Vector3(sd * 0.19f, 0.1f, 0f), new Vector3(0.05f, 0.11f, 0.09f), faceR, null, new Vector3(0f, sd * 20f, 0f));
                }
                k.Eyes = Prim.Group("Eyes", k.Head, new Vector3(0f, 0.11f, -0.18f));
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    S(ball, "EyeWhite", k.Eyes, new Vector3(sd * 0.055f, 0f, 0f), new Vector3(0.06f, 0.055f, 0.04f), null, eyeWhite, null, false);
                    S(ball, "Pupil", k.Eyes, new Vector3(sd * 0.055f, 0f, -0.018f), new Vector3(0.034f, 0.034f, 0.02f), pupilR, null, null, false);
                }
                k.Jaw = Prim.Group("Jaw", k.Head, new Vector3(0f, -0.01f, -0.1f));
                S(ball, "Mouth", k.Jaw, new Vector3(0f, 0.0f, -0.08f), new Vector3(0.11f, 0.04f, 0.09f), null, mouth, null, false);
                S(ball, "Teeth", k.Jaw, new Vector3(0f, 0.01f, -0.12f), new Vector3(0.08f, 0.02f, 0.03f), null, teeth, null, false);
                S(ball, "Chin", k.Jaw, new Vector3(0f, -0.03f, -0.08f), new Vector3(0.14f, 0.05f, 0.12f), faceR);

                // Arms (two segments) with long hands
                for (int s = 0; s < 2; s++)
                {
                    float sd = s == 0 ? -1f : 1f;
                    k.Shoulder[s] = Prim.Group("Shoulder", k.Torso, new Vector3(sd * 0.19f, 0.42f, -0.02f));
                    S(ball, "Deltoid", k.Shoulder[s], Vector3.zero, Vector3.one * 0.13f, furR);
                    S(upperArm, "Upper", k.Shoulder[s], Vector3.zero, Vector3.one, furR);
                    k.Elbow[s] = Prim.Group("Elbow", k.Shoulder[s], new Vector3(0f, -0.3f, 0f));
                    S(foreArm, "Fore", k.Elbow[s], Vector3.zero, Vector3.one, furR);
                    S(ball, "Hand", k.Elbow[s], new Vector3(0f, -0.31f, -0.01f), new Vector3(0.075f, 0.11f, 0.06f), darkR);
                    S(ball, "Thumb", k.Elbow[s], new Vector3(sd * -0.03f, -0.28f, -0.035f), new Vector3(0.03f, 0.05f, 0.03f), darkR, null, null, false);
                }

                // Legs with long grasping feet
                for (int s = 0; s < 2; s++)
                {
                    float sd = s == 0 ? -1f : 1f;
                    k.Hip[s] = Prim.Group("Hip", k.Pelvis, new Vector3(sd * 0.12f, -0.02f, 0.04f));
                    S(thigh, "Thigh", k.Hip[s], Vector3.zero, Vector3.one, furR);
                    k.Knee[s] = Prim.Group("Knee", k.Hip[s], new Vector3(0f, -0.24f, 0f));
                    S(shin, "Shin", k.Knee[s], Vector3.zero, Vector3.one, furR);
                    S(ball, "Foot", k.Knee[s], new Vector3(0f, -0.23f, -0.05f), new Vector3(0.085f, 0.05f, 0.19f), darkR);
                }

                // Tail: a chain of segments, each hanging off the end of the last
                k.Tail = new Transform[tailSeg.Length];
                Transform parent = k.Pelvis;
                var at = new Vector3(0f, 0.02f, 0.15f);
                for (int j = 0; j < tailSeg.Length; j++)
                {
                    k.Tail[j] = Prim.Group("Tail" + j, parent, at);
                    S(tailSeg[j], "Seg", k.Tail[j], Vector3.zero, Vector3.one, j < 5 ? furR : darkR, null, null, j < 3);
                    parent = k.Tail[j];
                    at = new Vector3(0f, -0.13f, 0f);
                }

                troop.Add(k);
                Pose(k, 0f);
            }
        }

        // ---------------- World looks ----------------

        void ApplySkin(BiomeType world)
        {
            skin = world;
            Color fur, dark, belly, face;
            Material pupil;
            switch (world)
            {
                case BiomeType.IceAge: // snow macaques: thick grey-beige coats, red faces
                    fur = new Color(0.64f, 0.6f, 0.54f); dark = new Color(0.46f, 0.42f, 0.37f);
                    belly = new Color(0.8f, 0.77f, 0.72f); face = new Color(0.86f, 0.42f, 0.4f);
                    pupil = Mats.Plain(new Color(0.08f, 0.05f, 0.03f), 0.95f);
                    break;
                case BiomeType.WaterWorld: // coastal monkeys: red-orange backs, pale bellies
                    fur = new Color(0.66f, 0.38f, 0.17f); dark = new Color(0.42f, 0.23f, 0.11f);
                    belly = new Color(0.86f, 0.8f, 0.68f); face = new Color(0.8f, 0.58f, 0.46f);
                    pupil = Mats.Plain(new Color(0.06f, 0.04f, 0.02f), 0.95f);
                    break;
                case BiomeType.HorrorJungle: // black-furred, ashen faces, eyes glowing red
                    fur = new Color(0.12f, 0.11f, 0.1f); dark = new Color(0.05f, 0.05f, 0.05f);
                    belly = new Color(0.24f, 0.21f, 0.19f); face = new Color(0.55f, 0.54f, 0.5f);
                    pupil = Mats.Emissive("MonkeyGlowEye", Tex.Metal, new Color(1f, 0.15f, 0.1f), 0.9f, new Color(3f, 0.35f, 0.2f));
                    break;
                default: // jungle: brown monkeys with tan faces
                    fur = new Color(0.38f, 0.25f, 0.15f); dark = new Color(0.17f, 0.11f, 0.07f);
                    belly = new Color(0.66f, 0.52f, 0.36f); face = new Color(0.8f, 0.62f, 0.47f);
                    pupil = Mats.Plain(new Color(0.04f, 0.03f, 0.02f), 0.95f);
                    break;
            }
            Set(furR, Mats.Fur(fur));
            Set(darkR, Mats.Fur(dark));
            Set(bellyR, Mats.Fur(belly));
            Set(faceR, Mats.Skin(face));
            Set(pupilR, pupil);
        }

        static void Set(List<Renderer> rs, Material m)
        {
            foreach (var r in rs) if (r != null) r.sharedMaterial = m;
        }
    }
}
