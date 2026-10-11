using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// The explorer. Runs forward automatically; swipe left/right to change lane,
    /// up to jump, down to roll (or slam down while in the air).
    ///
    /// Movement is tuned to feel physical: lane changes accelerate and settle like a
    /// sprung mass, jumps rise quickly, hang briefly at the apex and fall harder, landings
    /// absorb impact, and jump presses just before touchdown are buffered.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public const float LaneWidth = 2.5f;

        // Lateral
        const float LaneSmoothTime = 0.075f;  // spring-damper settle time for a lane change
        const float MaxLateralSpeed = 24f;

        // Vertical (apex ~1.6 m, air time ~0.68 s: clears logs with room to spare)
        const float JumpVelocity = 9.5f;
        const float RiseGravity = 28f;
        const float FallGravity = 36f;
        const float ApexBand = 1.6f;          // |vy| below this counts as the apex...
        const float ApexGravityScale = 0.7f;  // ...where gravity eases off for a moment of hang time
        const float FastFallVelocity = -16f;
        const float FastFallGravity = 70f;
        const float JumpBuffer = 0.15f;

        const float RollDuration = 0.75f;
        const float GlideHeight = 5f;
        const float InvulnAfterRevive = 2f;
        const float StandHeight = 1.8f;
        const float RollHeight = 0.8f;
        const float LandDuration = 0.24f;
        const float StumbleDuration = 0.45f;
        const float KnockFallTime = 0.42f;

        // Power-up durations in seconds, indexed by PickupType.
        static readonly float[] Durations = { 0f, 10f, 7f, 20f, 12f };

        public int Lane { get; private set; }

        int prevLane;
        float x, xVel, y, vy;
        bool grounded = true;
        bool fastFalling;
        float rollTimer;
        bool rollOnLand;
        float jumpBufferTimer;
        float landTimer, landStrength;
        float stumbleTimer;
        float invuln;
        bool knocked;
        float knockTimer;
        bool knockLanded;
        float animTime;
        float runPhase;
        int lastStep;
        float rollDustTimer;
        readonly float[] power = new float[5];

        BoxCollider box;
        ExplorerRig rig;

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
            rig = new ExplorerRig();
            rig.Build(transform);
            SetCollider(false);
            UpdateFx();
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            float dt = Time.deltaTime;
            animTime += dt;

            if (knocked) { KnockedUpdate(dt); return; }
            if (gm.State == GameState.Menu) { IdleUpdate(dt); return; }
            if (gm.State != GameState.Playing) return;

            HandleInput(InputBridge.Read(), gm);
            TickTimers(dt);

            // ---- Lateral: critically damped spring toward the lane centre ----
            x = Mathf.SmoothDamp(x, Lane * LaneWidth, ref xVel, LaneSmoothTime, MaxLateralSpeed, dt);

            // ---- Vertical ----
            if (IsGliding)
            {
                float targetY = GlideHeight;
                float yv = vy;
                y = Mathf.SmoothDamp(y, targetY, ref yv, 0.35f, 12f, dt);
                vy = yv;
                grounded = false;
            }
            else if (!grounded)
            {
                float g = vy > 0f ? RiseGravity : FallGravity;
                if (!fastFalling && Mathf.Abs(vy) < ApexBand) g *= ApexGravityScale;
                if (fastFalling) g = FastFallGravity;
                vy -= g * dt;
                y += vy * dt;
                if (y <= 0f) Land(gm, -vy);
            }

            SetCollider(IsRolling);

            // ---- Forward ----
            float z = transform.position.z + gm.Speed * dt;
            transform.position = new Vector3(x, y, z);

            Animate(gm, dt, z);
        }

        void TickTimers(float dt)
        {
            for (int i = 1; i < power.Length; i++)
            {
                if (power[i] <= 0f) continue;
                power[i] -= dt;
                if (power[i] <= 0f && i == (int)PickupType.Glider) EndGlide();
            }
            if (rollTimer > 0f) rollTimer -= dt;
            if (jumpBufferTimer > 0f) jumpBufferTimer -= dt;
            if (landTimer > 0f) landTimer -= dt;
            if (stumbleTimer > 0f) stumbleTimer -= dt;
            if (invuln > 0f) invuln -= dt;
        }

        void HandleInput(SwipeDir s, GameManager gm)
        {
            switch (s)
            {
                case SwipeDir.Left: ChangeLane(-1, gm); break;
                case SwipeDir.Right: ChangeLane(1, gm); break;
                case SwipeDir.Up:
                    if (IsGliding) break;
                    if (grounded) Jump();
                    else jumpBufferTimer = JumpBuffer; // remember it and jump on touchdown
                    break;
                case SwipeDir.Down:
                    if (IsGliding) break;
                    if (!grounded)
                    {
                        fastFalling = true;
                        vy = Mathf.Min(vy, FastFallVelocity);
                        rollOnLand = true;
                        jumpBufferTimer = 0f;
                    }
                    else StartRoll();
                    break;
            }
        }

        void Jump()
        {
            vy = JumpVelocity;
            grounded = false;
            fastFalling = false;
            rollTimer = 0f;
            jumpBufferTimer = 0f;
            landTimer = 0f;
            Fx.Dust(new Vector3(x, 0f, transform.position.z - 0.1f), 3, 0.5f); // push-off
        }

        void StartRoll()
        {
            rollTimer = RollDuration;
            landTimer = 0f;
        }

        void Land(GameManager gm, float impactSpeed)
        {
            y = 0f;
            vy = 0f;
            grounded = true;
            bool slammed = fastFalling;
            fastFalling = false;

            landStrength = Mathf.Clamp01(impactSpeed / 14f);
            landTimer = LandDuration;
            var feet = new Vector3(x, 0f, transform.position.z);
            Fx.Dust(feet, 4 + Mathf.RoundToInt(8f * landStrength), 0.55f + 0.45f * landStrength);
            if (slammed) gm.Cam.Shake(0.12f, 0.1f);
            else if (impactSpeed > 9f) gm.Cam.Shake(0.05f, 0.07f);

            if (rollOnLand) { rollOnLand = false; StartRoll(); }
            else if (jumpBufferTimer > 0f) Jump();
        }

        void ChangeLane(int dir, GameManager gm)
        {
            int target = Mathf.Clamp(Lane + dir, -1, 1);
            if (target == Lane)
            {
                // Bumped the jungle edge: a little shove back and a shake.
                xVel -= dir * 3f;
                gm.Cam.Shake(0.08f, 0.1f);
                return;
            }
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
                xVel = -xVel * 0.5f; // rebound off the obstacle
                stumbleTimer = StumbleDuration;
                gm.OnStumble();
                return;
            }

            // Head-on hit
            if (HasPower(PickupType.Shield))
            {
                power[(int)PickupType.Shield] = 0f;
                ob.Smash();
                gm.Cam.Shake(0.2f, 0.15f);
                stumbleTimer = StumbleDuration * 0.6f;
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
            if (t == PickupType.Glider) { rollTimer = 0f; fastFalling = false; rollOnLand = false; }
        }

        public void ResetPlayer()
        {
            Lane = prevLane = 0;
            x = xVel = y = vy = 0f;
            grounded = true;
            fastFalling = false;
            rollTimer = 0f;
            rollOnLand = false;
            jumpBufferTimer = landTimer = stumbleTimer = 0f;
            invuln = 0f;
            knocked = false;
            for (int i = 0; i < power.Length; i++) power[i] = 0f;
            transform.position = Vector3.zero;
            SetCollider(false);
            rig.Root.gameObject.SetActive(true);
            rig.Snap(ExplorerRig.Idle(animTime));
            rig.GroundShadow(0f);
            UpdateFx();
        }

        public void Knockout()
        {
            knocked = true;
            knockTimer = 0f;
            knockLanded = false;
            rollTimer = 0f;
            rig.Root.gameObject.SetActive(true);
        }

        public void Revive()
        {
            knocked = false;
            y = 0f; vy = 0f;
            grounded = true;
            fastFalling = false;
            rollTimer = 0f;
            power[(int)PickupType.Glider] = 0f;
            invuln = InvulnAfterRevive;
            var p = transform.position;
            transform.position = new Vector3(x, 0f, p.z);
            rig.Snap(ExplorerRig.Run(runPhase, 0f));
        }

        // ---------------- Animation ----------------

        void SetCollider(bool rolling)
        {
            float h = rolling ? RollHeight : StandHeight;
            box.size = new Vector3(0.8f, h, 0.8f);
            box.center = new Vector3(0f, h * 0.5f, 0f);
        }

        void Animate(GameManager gm, float dt, float z)
        {
            float speedNorm = Mathf.InverseLerp(GameManager.StartSpeed, GameManager.MaxSpeed, gm.Speed);
            // Stride frequency rises with speed (about 1.5 to 2 strides per second).
            runPhase += dt * Mathf.PI * 2f * Mathf.Lerp(1.5f, 2.05f, speedNorm);

            ExplorerRig.Pose pose;
            float rollAngle = 0f;
            float sharp = 16f;
            float runWeight = 0f;

            if (IsGliding)
            {
                pose = ExplorerRig.Glide(animTime);
                sharp = 6f;
            }
            else if (!grounded)
            {
                pose = ExplorerRig.Air(vy);
                sharp = 12f;
            }
            else if (IsRolling)
            {
                // Tuck, somersault forward once, then come up running.
                float r = 1f - rollTimer / RollDuration;
                float tuck = Tex.SS(0f, 0.12f, r) * Tex.SS(1f, 0.8f, r);
                var t = ExplorerRig.Tuck();
                t.BodyY = -0.42f;
                pose = ExplorerRig.Pose.Lerp(ExplorerRig.Run(runPhase, speedNorm), t, tuck);
                rollAngle = 360f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.82f, r));
                sharp = 24f;
                if ((rollDustTimer -= dt) <= 0f)
                {
                    rollDustTimer = 0.05f;
                    Fx.Dust(new Vector3(x, 0f, z - 0.3f), 1, 0.6f);
                }
            }
            else
            {
                pose = ExplorerRig.Run(runPhase, speedNorm);
                runWeight = 1f;
                FootfallDust(z);
            }

            // Absorb the landing: knees bend, body drops, then springs back up.
            if (grounded && !IsRolling && landTimer > 0f)
            {
                float w = Mathf.Sin(Mathf.Clamp01(landTimer / LandDuration) * Mathf.PI * 0.5f) * Mathf.Lerp(0.45f, 1f, landStrength);
                pose = ExplorerRig.Pose.Lerp(pose, ExplorerRig.Land(), w);
                sharp = 28f;
            }

            // Bank into lane changes and turn slightly toward where we're heading.
            pose.Bank += Mathf.Clamp(-xVel * 1.3f, -20f, 20f);
            pose.Yaw += Mathf.Clamp(xVel * 1.1f, -14f, 14f);

            // Stagger after a side bump or a shield hit.
            if (stumbleTimer > 0f)
            {
                float k = stumbleTimer / StumbleDuration;
                float wob = Mathf.Sin(animTime * 32f) * k;
                pose.Spine += new Vector3(-12f * k, 0f, 20f * wob);
                pose.Neck += new Vector3(10f * k, 0f, -12f * wob);
                pose.ShoulderL += new Vector3(-40f * k, 0f, -45f * k);
                pose.ShoulderR += new Vector3(-40f * k, 0f, 45f * k);
                pose.Bank += 10f * wob;
            }

            rig.Apply(pose, dt, rollAngle, 0f, sharp);
            rig.Secondary(animTime, runPhase, runWeight, speedNorm);
            rig.GroundShadow(y);

            // Blink while invulnerable
            bool visible = invuln <= 0f || Mathf.Repeat(animTime, 0.2f) < 0.12f;
            rig.Root.gameObject.SetActive(visible);

            UpdateFx();
            rig.MagnetFx.Rotate(0f, 360f * dt, 0f, Space.Self);
        }

        /// <summary>A small puff each time a foot strikes the ground.</summary>
        void FootfallDust(float z)
        {
            int step = Mathf.FloorToInt((runPhase - Mathf.PI * 0.5f) / Mathf.PI);
            if (step == lastStep) return;
            lastStep = step;
            float side = (step & 1) == 0 ? -0.1f : 0.1f;
            Fx.Dust(new Vector3(x + side, 0f, z + 0.15f), 2, 0.42f);
        }

        void IdleUpdate(float dt)
        {
            rig.Apply(ExplorerRig.Idle(animTime), dt, 0f, 0f, 6f);
            rig.Secondary(animTime, 0f, 0f, 0f);
            rig.GroundShadow(0f);
        }

        /// <summary>Topples backwards with a little bounce when caught or crashing.</summary>
        void KnockedUpdate(float dt)
        {
            knockTimer += dt;

            // Finish any fall from mid-air.
            if (y > 0f)
            {
                vy -= FallGravity * dt;
                y = Mathf.Max(0f, y + vy * dt);
                var p = transform.position;
                transform.position = new Vector3(x, y, p.z);
            }

            float t = Mathf.Clamp01(knockTimer / KnockFallTime);
            float fall = -86f * t * t; // accelerates like a toppling body
            if (knockTimer > KnockFallTime)
            {
                float a = knockTimer - KnockFallTime;
                fall += 7f * Mathf.Sin(a * 18f) * Mathf.Exp(-a * 7f);
                if (!knockLanded)
                {
                    knockLanded = true;
                    Fx.Dust(transform.position + new Vector3(0f, 0f, -1.2f), 10, 0.9f);
                }
            }

            rig.Apply(ExplorerRig.Knocked(), dt, 0f, fall, 12f);
            rig.Secondary(animTime, 0f, 0f, 0f);
            rig.GroundShadow(y);
        }

        void UpdateFx()
        {
            rig.ShieldFx.SetActive(HasPower(PickupType.Shield));
            rig.GliderFx.SetActive(IsGliding);
            rig.MagnetFx.gameObject.SetActive(HasPower(PickupType.Magnet));
            rig.BonanzaFx.SetActive(HasPower(PickupType.Bonanza));
        }
    }
}
