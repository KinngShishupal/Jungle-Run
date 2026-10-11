using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// The young explorer. Runs forward automatically; swipe left/right to change
    /// lane, up to jump, down to roll (or slam down while in the air).
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public const float LaneWidth = 2.5f;
        const float LaneChangeSpeed = 16f;
        const float JumpVelocity = 9.5f;
        const float Gravity = 28f;
        const float FastFallVelocity = -22f;
        const float RollDuration = 0.75f;
        const float GlideHeight = 5f;
        const float InvulnAfterRevive = 2f;
        const float StandHeight = 1.8f;
        const float RollHeight = 0.8f;

        // Power-up durations in seconds, indexed by PickupType.
        static readonly float[] Durations = { 0f, 10f, 7f, 20f, 12f };

        public int Lane { get; private set; }

        int prevLane;
        float x, y, vy;
        bool grounded = true;
        float rollTimer;
        bool rollOnLand;
        float invuln;
        bool knocked;
        float animTime;
        float dustTimer;
        readonly float[] power = new float[5];

        BoxCollider box;
        Transform model, legL, legR, armL, armR, magnetFx;
        GameObject shieldFx, gliderFx, bonanzaFx;

        public bool IsRolling => rollTimer > 0f;
        public bool IsGliding => power[(int)PickupType.Glider] > 0f;
        public bool Attracting => HasPower(PickupType.Magnet) || IsGliding;
        public bool HasPower(PickupType t) => power[(int)t] > 0f;
        public float PowerFraction(PickupType t) =>
            Durations[(int)t] > 0f ? Mathf.Clamp01(power[(int)t] / Durations[(int)t]) : 0f;

        void Awake()
        {
            box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            BuildModel();
            SetCollider(false);
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            float dt = Time.deltaTime;
            animTime += dt;

            if (gm.State == GameState.Menu) { IdleAnim(); return; }
            if (gm.State != GameState.Playing) return;

            HandleInput(InputBridge.Read(), gm);

            // Power-up timers
            for (int i = 1; i < power.Length; i++)
            {
                if (power[i] <= 0f) continue;
                power[i] -= dt;
                if (power[i] <= 0f && i == (int)PickupType.Glider) EndGlide();
            }

            // Sideways
            x = Mathf.MoveTowards(x, Lane * LaneWidth, LaneChangeSpeed * dt);

            // Up / down
            if (IsGliding)
            {
                y = Mathf.MoveTowards(y, GlideHeight, 10f * dt);
                vy = 0f;
                grounded = false;
            }
            else if (!grounded)
            {
                vy -= Gravity * dt;
                y += vy * dt;
                if (y <= 0f)
                {
                    y = 0f;
                    vy = 0f;
                    grounded = true;
                    Fx.Dust(new Vector3(x, 0f, transform.position.z), 7, 0.75f);
                    if (rollOnLand) { rollTimer = RollDuration; rollOnLand = false; }
                }
            }

            if (rollTimer > 0f) rollTimer -= dt;
            SetCollider(IsRolling);
            if (invuln > 0f) invuln -= dt;

            // Forward
            float z = transform.position.z + gm.Speed * dt;
            transform.position = new Vector3(x, y, z);

            // Puffs of dust kicked up while running
            if (grounded && (dustTimer -= dt) <= 0f)
            {
                dustTimer = IsRolling ? 0.05f : 0.11f;
                Fx.Dust(new Vector3(x, 0f, z - 0.2f), 1, IsRolling ? 0.6f : 0.4f);
            }

            Animate(gm);
        }

        void HandleInput(SwipeDir s, GameManager gm)
        {
            switch (s)
            {
                case SwipeDir.Left: ChangeLane(-1, gm); break;
                case SwipeDir.Right: ChangeLane(1, gm); break;
                case SwipeDir.Up:
                    if (IsGliding || !grounded) break;
                    vy = JumpVelocity;
                    grounded = false;
                    rollTimer = 0f;
                    break;
                case SwipeDir.Down:
                    if (IsGliding) break;
                    if (!grounded) { vy = FastFallVelocity; rollOnLand = true; }
                    else rollTimer = RollDuration;
                    break;
            }
        }

        void ChangeLane(int dir, GameManager gm)
        {
            int target = Mathf.Clamp(Lane + dir, -1, 1);
            if (target == Lane) { gm.Cam.Shake(0.08f, 0.1f); return; } // bumped the jungle edge
            prevLane = Lane;
            Lane = target;
        }

        void EndGlide()
        {
            grounded = false; // fall back down
            vy = 0f;
            invuln = Mathf.Max(invuln, 0.6f);
        }

        void OnTriggerEnter(Collider other)
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;

            var pickup = other.GetComponent<Pickup>();
            if (pickup != null) { pickup.Collect(); return; }

            var ob = other.GetComponent<Obstacle>();
            if (ob == null || ob.Handled) return;
            if (IsGliding || invuln > 0f) return;

            // Hit the side of an obstacle while switching lanes -> stumble and bounce back.
            bool switching = Mathf.Abs(x - Lane * LaneWidth) > 0.35f;
            if (switching && ob.Lane == Lane)
            {
                Lane = prevLane;
                gm.OnStumble();
                return;
            }

            // Head-on hit
            if (HasPower(PickupType.Shield))
            {
                power[(int)PickupType.Shield] = 0f;
                ob.Smash();
                gm.Cam.Shake(0.2f, 0.15f);
                invuln = 0.5f;
                return;
            }

            ob.Handled = true;
            gm.OnCrash(ob.Type);
        }

        // ---------------- Called by GameManager ----------------

        public void Activate(PickupType t)
        {
            power[(int)t] = Durations[(int)t];
            if (t == PickupType.Glider) rollTimer = 0f;
        }

        public void ResetPlayer()
        {
            Lane = prevLane = 0;
            x = y = vy = 0f;
            grounded = true;
            rollTimer = 0f;
            rollOnLand = false;
            invuln = 0f;
            knocked = false;
            for (int i = 0; i < power.Length; i++) power[i] = 0f;
            transform.position = Vector3.zero;
            SetCollider(false);
            ResetPose();
            UpdateFx();
        }

        public void Knockout()
        {
            knocked = true;
            model.gameObject.SetActive(true);
            model.localScale = Vector3.one;
            model.localRotation = Quaternion.Euler(-80f, 0f, 0f); // falls flat on their back
        }

        public void Revive()
        {
            knocked = false;
            y = 0f; vy = 0f;
            grounded = true;
            rollTimer = 0f;
            power[(int)PickupType.Glider] = 0f;
            invuln = InvulnAfterRevive;
            var p = transform.position;
            transform.position = new Vector3(x, 0f, p.z);
            ResetPose();
        }

        // ---------------- Visuals ----------------

        void SetCollider(bool rolling)
        {
            float h = rolling ? RollHeight : StandHeight;
            box.size = new Vector3(0.8f, h, 0.8f);
            box.center = new Vector3(0f, h * 0.5f, 0f);
        }

        void Animate(GameManager gm)
        {
            if (knocked) return;
            float t = animTime * (8f + gm.Speed * 0.35f);
            float swing = Mathf.Sin(t);

            if (IsGliding)
            {
                SetLimbs(10f, -10f, -160f, -160f); // arms up holding the leaf
                model.localPosition = new Vector3(0f, Mathf.Sin(animTime * 3f) * 0.1f, 0f);
            }
            else if (!grounded)
            {
                SetLimbs(-50f, -20f, -120f, -120f);
                model.localPosition = Vector3.zero;
            }
            else
            {
                SetLimbs(swing * 50f, -swing * 50f, -swing * 45f, swing * 45f);
                model.localPosition = new Vector3(0f, Mathf.Abs(swing) * 0.08f, 0f);
            }

            float targetY = IsRolling ? 0.45f : 1f;
            var sc = model.localScale;
            sc.y = Mathf.MoveTowards(sc.y, targetY, Time.deltaTime * 8f);
            model.localScale = sc;

            // Blink while invulnerable
            bool visible = invuln <= 0f || Mathf.Repeat(animTime, 0.2f) < 0.12f;
            model.gameObject.SetActive(visible);

            UpdateFx();
            magnetFx.Rotate(0f, 360f * Time.deltaTime, 0f, Space.Self);
        }

        void IdleAnim()
        {
            float b = Mathf.Sin(animTime * 2f);
            SetLimbs(0f, 0f, b * 5f, -b * 5f);
            model.localPosition = new Vector3(0f, b * 0.02f, 0f);
        }

        void SetLimbs(float l, float r, float al, float ar)
        {
            legL.localRotation = Quaternion.Euler(l, 0f, 0f);
            legR.localRotation = Quaternion.Euler(r, 0f, 0f);
            armL.localRotation = Quaternion.Euler(al, 0f, 0f);
            armR.localRotation = Quaternion.Euler(ar, 0f, 0f);
        }

        void ResetPose()
        {
            model.gameObject.SetActive(true);
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;
            model.localPosition = Vector3.zero;
            SetLimbs(0f, 0f, 0f, 0f);
        }

        void UpdateFx()
        {
            shieldFx.SetActive(HasPower(PickupType.Shield));
            gliderFx.SetActive(IsGliding);
            magnetFx.gameObject.SetActive(HasPower(PickupType.Magnet));
            bonanzaFx.SetActive(HasPower(PickupType.Bonanza));
        }


        void BuildModel()
        {
            var skin = Mats.Skin(new Color(0.86f, 0.65f, 0.48f));
            var shirt = Mats.Fabric(new Color(0.88f, 0.82f, 0.68f));
            var trousers = Mats.Fabric(new Color(0.50f, 0.42f, 0.30f));
            var boots = Mats.Leather(new Color(0.30f, 0.19f, 0.11f));
            var felt = Mats.Fabric(new Color(0.47f, 0.34f, 0.21f));
            var band = Mats.Leather(new Color(0.13f, 0.09f, 0.07f));
            var scarf = Mats.Fabric(new Color(0.72f, 0.13f, 0.08f));
            var canvas = Mats.Fabric(new Color(0.38f, 0.40f, 0.25f));
            var bedroll = Mats.Fabric(new Color(0.45f, 0.20f, 0.12f));
            var hair = Mats.Fur(new Color(0.22f, 0.14f, 0.09f));

            var ball = Meshes.Ball(12, 18);
            var leg = Meshes.Limb(0.74f, 0.088f, 0.062f, 10, 8);
            var bootShaft = Meshes.Limb(0.2f, 0.074f, 0.07f, 10, 5);
            var sleeve = Meshes.Limb(0.32f, 0.072f, 0.062f, 10, 6);
            var forearm = Meshes.Limb(0.28f, 0.052f, 0.042f, 10, 6);

            model = Prim.Group("Model", transform, Vector3.zero);

            // Legs (pivot at the hip)
            legL = Prim.Group("LegL", model, new Vector3(-0.1f, 0.82f, 0f));
            legR = Prim.Group("LegR", model, new Vector3(0.1f, 0.82f, 0f));
            foreach (var l in new[] { legL, legR })
            {
                Prim.Shape(leg, "Leg", l, Vector3.zero, Vector3.one, trousers);
                Prim.Shape(bootShaft, "BootShaft", l, new Vector3(0f, -0.56f, 0f), Vector3.one, boots);
                Prim.Shape(Meshes.RoundedBox(new Vector3(0.15f, 0.13f, 0.28f), 0.05f), "Boot", l, new Vector3(0f, -0.75f, 0.04f), Vector3.one, boots);
            }

            // Torso: tailored safari shirt with a leather belt
            var torso = Meshes.Lathe(new[]
            {
                new Vector2(0.16f, 0f), new Vector2(0.175f, 0.08f), new Vector2(0.19f, 0.25f), new Vector2(0.215f, 0.42f),
                new Vector2(0.2f, 0.52f), new Vector2(0.12f, 0.58f), new Vector2(0.06f, 0.6f), new Vector2(0.001f, 0.6f)
            }, 18, 2f, "Torso");
            Prim.Shape(torso, "Torso", model, new Vector3(0f, 0.76f, 0f), new Vector3(1f, 1f, 0.72f), shirt);
            var belt = Meshes.Lathe(new[] { new Vector2(0.178f, 0f), new Vector2(0.184f, 0.03f), new Vector2(0.178f, 0.06f) }, 18);
            Prim.Shape(belt, "Belt", model, new Vector3(0f, 0.79f, 0f), new Vector3(1f, 1f, 0.74f), boots);
            Prim.Shape(Meshes.RoundedBox(new Vector3(0.07f, 0.05f, 0.02f), 0.008f), "Buckle", model, new Vector3(0f, 0.82f, 0.135f), Vector3.one, Mats.Gold);

            // Neck, scarf, head, hair
            Prim.Shape(ball, "Neck", model, new Vector3(0f, 1.4f, 0f), new Vector3(0.13f, 0.18f, 0.13f), skin);
            var scarfRing = new List<Vector3>();
            for (int i = 0; i <= 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                scarfRing.Add(new Vector3(Mathf.Cos(a) * 0.085f, 0f, Mathf.Sin(a) * 0.085f));
            }
            Prim.Shape(Meshes.Sweep(scarfRing, (t, a) => 0.04f, 8, 1f, 3f), "Scarf", model, new Vector3(0f, 1.37f, 0f), Vector3.one, scarf);
            Prim.Shape(ball, "ScarfKnot", model, new Vector3(0.03f, 1.33f, 0.1f), new Vector3(0.08f, 0.1f, 0.06f), scarf);

            Prim.Shape(ball, "Head", model, new Vector3(0f, 1.54f, 0f), new Vector3(0.27f, 0.31f, 0.29f), skin);
            Prim.Shape(ball, "Hair", model, new Vector3(0f, 1.57f, -0.02f), new Vector3(0.285f, 0.26f, 0.29f), hair);
            Prim.Shape(ball, "EarL", model, new Vector3(-0.135f, 1.54f, 0f), new Vector3(0.05f, 0.08f, 0.04f), skin);
            Prim.Shape(ball, "EarR", model, new Vector3(0.135f, 1.54f, 0f), new Vector3(0.05f, 0.08f, 0.04f), skin);
            Prim.Shape(ball, "Nose", model, new Vector3(0f, 1.53f, 0.145f), new Vector3(0.045f, 0.06f, 0.05f), skin);
            var eye = Mats.Plain(new Color(0.06f, 0.04f, 0.03f), 0.9f);
            Prim.Shape(ball, "EyeL", model, new Vector3(-0.05f, 1.565f, 0.128f), new Vector3(0.035f, 0.045f, 0.03f), eye, null, false);
            Prim.Shape(ball, "EyeR", model, new Vector3(0.05f, 1.565f, 0.128f), new Vector3(0.035f, 0.045f, 0.03f), eye, null, false);

            // Fedora
            var hat = Meshes.Lathe(new[]
            {
                new Vector2(0.15f, 0.025f), new Vector2(0.33f, 0f), new Vector2(0.335f, 0.02f), new Vector2(0.2f, 0.035f),
                new Vector2(0.18f, 0.06f), new Vector2(0.172f, 0.15f), new Vector2(0.14f, 0.19f), new Vector2(0.05f, 0.172f),
                new Vector2(0.001f, 0.18f)
            }, 22, 2f, "Hat");
            Prim.Shape(hat, "Hat", model, new Vector3(0f, 1.62f, 0f), new Vector3(1f, 1f, 1.08f), felt, new Vector3(-6f, 0f, 0f));
            var hatBand = Meshes.Lathe(new[] { new Vector2(0.181f, 0f), new Vector2(0.18f, 0.035f) }, 22);
            Prim.Shape(hatBand, "HatBand", model, new Vector3(0f, 1.685f, 0.005f), new Vector3(1f, 1f, 1.08f), band, new Vector3(-6f, 0f, 0f));

            // Backpack with bedroll and straps
            Prim.Shape(Meshes.RoundedBox(new Vector3(0.34f, 0.4f, 0.19f), 0.06f), "Backpack", model, new Vector3(0f, 1.08f, -0.21f), Vector3.one, canvas);
            Prim.Shape(Meshes.RoundedBox(new Vector3(0.35f, 0.13f, 0.21f), 0.04f), "Flap", model, new Vector3(0f, 1.24f, -0.21f), Vector3.one, boots);
            Prim.Shape(Meshes.RoundedBox(new Vector3(0.2f, 0.14f, 0.07f), 0.03f), "Pocket", model, new Vector3(0f, 0.98f, -0.32f), Vector3.one, canvas);
            var roll = new List<Vector3> { new Vector3(-0.22f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0f, 0f) };
            Prim.Shape(Meshes.Sweep(roll, (t, a) => 0.075f, 12, 2f, 2f), "Bedroll", model, new Vector3(0f, 1.37f, -0.22f), Vector3.one, bedroll);
            var strap = Meshes.RoundedBox(new Vector3(0.045f, 0.44f, 0.02f), 0.008f);
            Prim.Shape(strap, "StrapL", model, new Vector3(-0.1f, 1.1f, 0.142f), Vector3.one, boots, new Vector3(-6f, 0f, 0f));
            Prim.Shape(strap, "StrapR", model, new Vector3(0.1f, 1.1f, 0.142f), Vector3.one, boots, new Vector3(-6f, 0f, 0f));

            // Arms (pivot at the shoulder)
            armL = Prim.Group("ArmL", model, new Vector3(-0.255f, 1.3f, 0f));
            armR = Prim.Group("ArmR", model, new Vector3(0.255f, 1.3f, 0f));
            foreach (var a in new[] { armL, armR })
            {
                Prim.Shape(sleeve, "Sleeve", a, Vector3.zero, Vector3.one, shirt);
                Prim.Shape(forearm, "Forearm", a, new Vector3(0f, -0.28f, 0f), Vector3.one, skin);
                Prim.Shape(ball, "Hand", a, new Vector3(0f, -0.58f, 0.01f), new Vector3(0.085f, 0.1f, 0.075f), skin);
            }

            // ---- Power-up effects ----
            shieldFx = Prim.Shape(ball, "CoconutHelmet", model, new Vector3(0f, 1.74f, 0f), new Vector3(0.46f, 0.32f, 0.48f), Mats.Coconut);

            gliderFx = Prim.Group("LeafGlider", model, new Vector3(0f, 2.45f, 0f)).gameObject;
            Prim.Shape(Meshes.Leaf(2.4f, 1.8f, 0.35f, 0.22f, 10), "Leaf", gliderFx.transform, new Vector3(0f, 0f, -1.2f), Vector3.one, Mats.BroadLeaf);
            var stem = new List<Vector3> { new Vector3(0f, 0f, -1.25f), new Vector3(0f, 0.05f, -1.5f), new Vector3(0f, 0f, -1.7f) };
            Prim.Shape(Meshes.Sweep(stem, (t, a) => 0.03f, 6, 1f, 1f), "Stem", gliderFx.transform, Vector3.zero, Vector3.one, Mats.Vine);

            magnetFx = Prim.Group("VineMagnet", model, new Vector3(0f, 1.1f, 0f));
            Prim.Shape(ball, "Orb", magnetFx, new Vector3(0.75f, 0f, 0f), Vector3.one * 0.22f, Mats.Gem, null, false);
            Prim.Shape(ball, "Orb", magnetFx, new Vector3(-0.75f, 0f, 0f), Vector3.one * 0.22f, Mats.Gem, null, false);

            var crown = Prim.Group("GoldenCrown", model, new Vector3(0f, 1.8f, 0f));
            bonanzaFx = crown.gameObject;
            var ring = Meshes.Lathe(new[] { new Vector2(0.15f, 0f), new Vector2(0.16f, 0.02f), new Vector2(0.17f, 0.09f), new Vector2(0.16f, 0.1f) }, 20);
            Prim.Shape(ring, "Band", crown, Vector3.zero, Vector3.one, Mats.Gold);
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad;
                Prim.Shape(ball, "Point", crown, new Vector3(Mathf.Cos(ang) * 0.165f, 0.12f, Mathf.Sin(ang) * 0.165f), Vector3.one * 0.05f, Mats.Gold);
            }
        }
    }
}
