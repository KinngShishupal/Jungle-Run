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
        const float LaneSmoothTime = 0.075f;  // spring-damper settle time toward the steering target
        /// <summary>How far either side of the trail centre the runner can go (the trail is 10 m wide).</summary>
        public const float MaxX = 3.9f;
        const float KeyHoldDelay = 0.22f;     // hold an arrow key this long to keep moving
        const float KeyHoldSpeed = 7.5f;      // m/s while an arrow key is held
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
        const float HangLength = 1.35f;       // grip to the body's centre of mass (pendulum length)
        const float InvulnAfterRevive = 2f;
        const float StandHeight = 1.8f;
        const float RollHeight = 0.8f;
        const float LandDuration = 0.24f;
        const float StumbleDuration = 0.45f;
        const float KnockFallTime = 0.42f;

        // Power-up durations in seconds, indexed by PickupType.
        static readonly float[] Durations = { 0f, 10f, 7f, 20f, 12f };

        /// <summary>The lane (-1, 0, 1) nearest the runner. Movement itself is free across the whole trail.</summary>
        public int Lane => Mathf.Clamp(Mathf.RoundToInt(x / LaneWidth), -1, 1);

        float targetX;   // where the runner is steering to (track-space x)
        float keyHoldT;
        float x, xVel, y, vy, s;

        /// <summary>Distance along the trail (track space).</summary>
        public float TrackS => s;
        /// <summary>Sideways offset from the trail centre.</summary>
        public float TrackX => x;
        public float Height => y;
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
        float wAir, wGlide;
        // Leaf glider flight: time aloft, pilot input (climb/dive), and the hanging body as a 2-axis pendulum
        float glideTime, glideInputT, glideInput, glideOffset;
        float swing, swingVel, swingPitch, swingPitchVel, prevXVel, prevVy, glideLoad, glideFlare;
        readonly float[] power = new float[5];

        BoxCollider box;
        ExplorerRig rig;
        HumanoidCharacter human;              // skinned mocap character, when one has been added
        readonly float[] humanWeights = new float[7];
        float bankNow, yawNow;

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
            human = HumanoidCharacter.TryCreate(transform);
            if (human != null) rig.HideBody();
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
            Steer(dt, gm);
            TickTimers(dt);

            // ---- Lateral: critically damped spring toward the steering target ----
            x = Mathf.SmoothDamp(x, targetX, ref xVel, LaneSmoothTime, MaxLateralSpeed, dt);

            // ---- Vertical ----
            if (IsGliding)
            {
                GlideFlight(dt);
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
            s += gm.Speed * dt;
            TrackPath.Place(transform, x, y, s);
            float z = s;

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
                case SwipeDir.Left: Step(-1, gm); break;
                case SwipeDir.Right: Step(1, gm); break;
                case SwipeDir.Up:
                    if (IsGliding) { glideInput = 1.3f; glideInputT = 1.1f; break; }   // pull up
                    if (grounded) Jump();
                    else jumpBufferTimer = JumpBuffer; // remember it and jump on touchdown
                    break;
                case SwipeDir.Down:
                    if (IsGliding) { glideInput = -3.4f; glideInputT = 1.1f; break; }  // dive
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
            Fx.Dust(Feet(-0.1f), 3, 0.5f); // push-off
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
            var feet = Feet(0f);
            Fx.Dust(feet, 4 + Mathf.RoundToInt(8f * landStrength), 0.55f + 0.45f * landStrength);
            if (slammed) gm.Cam.Shake(0.12f, 0.1f);
            else if (impactSpeed > 9f) gm.Cam.Shake(0.05f, 0.07f);

            if (rollOnLand) { rollOnLand = false; StartRoll(); }
            else if (jumpBufferTimer > 0f) Jump();
        }

        /// <summary>A keyboard tap: one lane-width step sideways (a little shove back if already at the edge).</summary>
        void Step(int dir, GameManager gm)
        {
            if (Mathf.Abs(targetX - dir * MaxX) < 0.05f)
            {
                // Bumped the jungle edge: a little shove back and a shake.
                xVel -= dir * 3f;
                gm.Cam.Shake(0.08f, 0.1f);
                return;
            }
            targetX = Mathf.Clamp(targetX + dir * LaneWidth, -MaxX, MaxX);
            keyHoldT = 0f;
        }

        /// <summary>
        /// Free sideways steering: a sideways drag moves the target by a matching distance; holding an arrow
        /// key keeps moving after a short delay. The runner can go anywhere between the trail edges.
        /// </summary>
        void Steer(float dt, GameManager gm)
        {
            float drag = InputBridge.SteerDelta;
            if (drag != 0f)
            {
                float wanted = targetX + drag;
                if (Mathf.Abs(wanted) > MaxX && Mathf.Abs(targetX) >= MaxX - 0.01f && Mathf.Abs(drag) > 0.15f)
                    gm.Cam.Shake(0.05f, 0.06f); // pushing against the edge
                targetX = Mathf.Clamp(wanted, -MaxX, MaxX);
            }

            float hold = InputBridge.SteerHold;
            if (hold != 0f)
            {
                keyHoldT += dt;
                if (keyHoldT > KeyHoldDelay) targetX = Mathf.Clamp(targetX + hold * KeyHoldSpeed * dt, -MaxX, MaxX);
            }
            else keyHoldT = 0f;
        }

        /// <summary>
        /// Leaf glider flight. Altitude follows a target through a spring-damper (the leaf "catches" the air on
        /// take-off, overshoots a little and settles), riding slow thermals and gusts. Swipe up to pull up,
        /// swipe down to dive. In the last moments the lift fades and the glider sinks to a low, gentle
        /// landing height. The hanging body is simulated as a pendulum under the grip, sideways and fore-aft.
        /// </summary>
        void GlideFlight(float dt)
        {
            glideTime += dt;
            float remain = power[(int)PickupType.Glider];
            glideFlare = Tex.SS(1.5f, 0.35f, remain);

            if (glideInputT > 0f) glideInputT -= dt;
            glideOffset = Mathf.MoveTowards(glideOffset, glideInputT > 0f ? glideInput : 0f, dt * 3.2f);
            float thermal = 0.35f * Mathf.Sin(glideTime * 0.9f) + 0.5f * (Mathf.PerlinNoise(glideTime * 0.45f, 3.7f) - 0.5f);
            float target = Mathf.Lerp(GlideHeight + thermal + glideOffset, 0.6f, glideFlare);

            float acc = 9f * (target - y) - 3.4f * vy;   // damping ratio ~0.57: a small, natural overshoot
            vy += acc * dt;
            y += vy * dt;
            if (y < 0f) { y = 0f; vy = Mathf.Max(vy, 0f); }
            grounded = false;
        }

        /// <summary>The body hanging from the glider grip, as a damped pendulum driven by the glider's accelerations.</summary>
        void SwingPendulum(float dt, bool gliding)
        {
            float g = 9.8f, L = HangLength;
            float ax = dt > 0f ? (xVel - prevXVel) / dt : 0f;
            float ay = dt > 0f ? (vy - prevVy) / dt : 0f;
            prevXVel = xVel; prevVy = vy;
            if (!gliding)
            {
                swing = Mathf.MoveTowards(swing, 0f, dt * 2f); swingVel = 0f;
                swingPitch = Mathf.MoveTowards(swingPitch, 0f, dt * 2f); swingPitchVel = 0f;
                return;
            }
            ax = Mathf.Clamp(ax, -40f, 40f);
            ay = Mathf.Clamp(ay, -30f, 30f);
            float turb = (Mathf.PerlinNoise(glideTime * 0.8f, 11.1f) - 0.5f);

            // Sideways: lane changes accelerate the grip; the body lags, swings out and back, and settles.
            float accS = -(g / L) * Mathf.Sin(swing) - 1.6f * swingVel - (ax / L) * Mathf.Cos(swing) + turb * 1.2f;
            swingVel += accS * dt;
            swing = Mathf.Clamp(swing + swingVel * dt, -0.9f, 0.9f);

            // Fore-aft: surges of lift and the airflow push the legs back; it rocks gently.
            float accP = -(g / L) * Mathf.Sin(swingPitch) - 1.4f * swingPitchVel + ay * 0.04f + 0.6f + turb * 0.8f;
            swingPitchVel += accP * dt;
            swingPitch = Mathf.Clamp(swingPitch + swingPitchVel * dt, -0.5f, 0.6f);

            glideLoad = Mathf.Clamp(1f + ay * 0.08f + Mathf.Abs(ax) * 0.03f, 0.3f, 1.9f) * Mathf.Lerp(1f, 0.55f, glideFlare);
        }

        void EndGlide()
        {
            grounded = false; // fall back down
            vy = Mathf.Min(vy, 0f); // the glider has already sunk low: carry on down from there
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

            // Ran into the side of an obstacle while moving sideways -> stumble and bounce back off it.
            float offset = ob.Lane * LaneWidth - x;
            bool fromSide = Mathf.Abs(xVel) > 1.5f && Mathf.Abs(offset) > 0.9f && Mathf.Sign(offset) == Mathf.Sign(xVel);
            if (fromSide)
            {
                targetX = Mathf.Clamp(x - Mathf.Sign(xVel) * 0.9f, -MaxX, MaxX);
                xVel = -xVel * 0.5f; // rebound off the obstacle
                stumbleTimer = StumbleDuration;
                human?.Restart(HumanoidCharacter.Clip.Stumble);
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
            gm.OnCrash(ob);
        }

        // ---------------- Called by GameManager ----------------

        public void Activate(PickupType t)
        {
            power[(int)t] = Durations[(int)t];
            if (t == PickupType.Glider)
            {
                rollTimer = 0f; fastFalling = false; rollOnLand = false;
                // Leap up into the wind as the leaf opens
                if (grounded) { vy = Mathf.Max(vy, 4f); Fx.Dust(Feet(0f), 4, 0.6f); }
                glideTime = 0f; glideInputT = 0f; glideOffset = 0f; glideFlare = 0f;
                swing = swingVel = swingPitch = swingPitchVel = 0f; prevXVel = xVel; prevVy = vy;
            }
        }

        public void ResetPlayer()
        {
            targetX = 0f; keyHoldT = 0f;
            x = xVel = y = vy = 0f;
            grounded = true;
            fastFalling = false;
            rollTimer = 0f;
            rollOnLand = false;
            jumpBufferTimer = landTimer = stumbleTimer = 0f;
            wAir = wGlide = 0f;
            invuln = 0f;
            knocked = false;
            for (int i = 0; i < power.Length; i++) power[i] = 0f;
            s = 0f;
            TrackPath.Place(transform, 0f, 0f, 0f);
            SetCollider(false);
            rig.Root.gameObject.SetActive(true);
            rig.Snap(ExplorerRig.Idle(animTime));
            human?.SnapTo(HumanoidCharacter.Clip.Idle);
            rig.GroundShadow(0f);
            UpdateFx();
        }

        public void Knockout()
        {
            knocked = true;
            knockTimer = 0f;
            knockLanded = false;
            human?.Restart(HumanoidCharacter.Clip.Fall);
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
            TrackPath.Place(transform, x, 0f, s);
            wAir = wGlide = 0f;
            rig.Snap(ExplorerRig.Run(runPhase, 0f));
            human?.SnapTo(HumanoidCharacter.Clip.Run);
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
            // Cadence rises with speed (about 2.5 to 3.3 strides per second, a sprinter's rhythm).
            runPhase += dt * Mathf.PI * 2f * Mathf.Lerp(2.5f, 3.3f, speedNorm);

            // State weights are smoothed; the run cycle itself plays crisply (no joint lag).
            wAir = Mathf.MoveTowards(wAir, !grounded && !IsGliding ? 1f : 0f, dt * 9f);
            wGlide = Mathf.MoveTowards(wGlide, IsGliding ? 1f : 0f, dt * 3f);

            var pose = ExplorerRig.Run(runPhase, speedNorm);
            float rollAngle = 0f;
            float sharp = 35f;
            float runWeight = grounded && !IsRolling ? 1f - wAir : 0f;

            if (IsRolling && grounded)
            {
                // Tuck, somersault forward once, then come up running.
                float r = 1f - rollTimer / RollDuration;
                float tuck = Tex.SS(0f, 0.12f, r) * Tex.SS(1f, 0.8f, r);
                var t = ExplorerRig.Tuck();
                t.BodyY = -0.42f;
                pose = ExplorerRig.Pose.Lerp(pose, t, tuck);
                rollAngle = 360f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.82f, r));
                if ((rollDustTimer -= dt) <= 0f)
                {
                    rollDustTimer = 0.05f;
                    Fx.Dust(Feet(-0.3f), 1, 0.6f);
                }
            }
            else if (runWeight > 0.8f)
            {
                FootfallDust(z);
            }

            if (wAir > 0f) pose = ExplorerRig.Pose.Lerp(pose, ExplorerRig.Air(vy), Mathf.SmoothStep(0f, 1f, wAir));
            SwingPendulum(dt, IsGliding);
            float hang = Mathf.SmoothStep(0f, 1f, wGlide);
            if (wGlide > 0f)
            {
                var glide = ExplorerRig.Glide(animTime, swingVel * Mathf.Rad2Deg, vy, glideFlare);
                glide.Bank = swing * Mathf.Rad2Deg;   // the body hangs and swings beneath the grip
                pose = ExplorerRig.Pose.Lerp(pose, glide, hang);
            }

            // Absorb the landing: knees bend, body drops, then springs back up.
            if (grounded && !IsRolling && landTimer > 0f)
            {
                float w = Mathf.Sin(Mathf.Clamp01(landTimer / LandDuration) * Mathf.PI * 0.5f) * Mathf.Lerp(0.45f, 1f, landStrength);
                pose = ExplorerRig.Pose.Lerp(pose, ExplorerRig.Land(), w);
                sharp = 28f;
            }

            // Bank into lane changes and turn slightly toward where we're heading.
            pose.Bank += Mathf.Clamp(-xVel * 1.3f, -20f, 20f) * (1f - hang);
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

            bankNow = pose.Bank;
            yawNow = pose.Yaw;
            rig.Hang = hang;
            rig.HangPitch = swingPitch * Mathf.Rad2Deg;
            if (IsGliding)
            {
                // The wing leads into lane changes (rolling toward the new lane), pitches up on a climb and
                // down in a dive, and flexes and flutters with the load and the gusts.
                float wingBank = Mathf.Clamp(-xVel * 3.2f, -26f, 26f) + swing * Mathf.Rad2Deg * 0.25f;
                float wingPitch = Mathf.Clamp(-vy * 4f, -16f, 14f) + swingPitch * Mathf.Rad2Deg * 0.15f - 10f * glideFlare;
                rig.Glider.Drive(wingBank, wingPitch, glideLoad, Mathf.PerlinNoise(glideTime * 0.7f, 5.3f), dt);
            }
            rig.Apply(pose, dt, rollAngle, 0f, Mathf.Lerp(sharp, 45f, hang)); // follow the pendulum closely while hanging
            DriveHuman(dt, speedNorm);
            rig.Secondary(animTime, runPhase, runWeight, speedNorm);
            rig.GroundShadow(y);

            // Blink while invulnerable
            bool visible = invuln <= 0f || Mathf.Repeat(animTime, 0.2f) < 0.12f;
            rig.Root.gameObject.SetActive(visible);
            human?.SetVisible(visible);

            UpdateFx();
            rig.MagnetFx.Rotate(0f, 360f * dt, 0f, Space.Self);
        }

        /// <summary>A small puff each time a foot strikes the ground.</summary>
        /// <summary>World position on the ground under the player, offset along the trail.</summary>
        Vector3 Feet(float along) => TrackPath.Point(s + along, x, 0f);

        void FootfallDust(float z)
        {
            int step = Mathf.FloorToInt((runPhase - Mathf.PI * 0.5f) / Mathf.PI);
            if (step == lastStep) return;
            lastStep = step;
            float side = (step & 1) == 0 ? -0.1f : 0.1f;
            Fx.Dust(TrackPath.Point(s + 0.15f, x + side, 0f), 2, 0.42f);
        }

        void IdleUpdate(float dt)
        {
            rig.Apply(ExplorerRig.Idle(animTime), dt, 0f, 0f, 6f);
            HumanIdle(dt);
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
                TrackPath.Place(transform, x, y, s);
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
                    Fx.Dust(Feet(-1.2f), 10, 0.9f);
                }
            }

            rig.Apply(ExplorerRig.Knocked(), dt, 0f, fall, 12f);
            HumanKnocked(dt, fall);
            rig.Secondary(animTime, 0f, 0f, 0f);
            rig.GroundShadow(y);
        }

        // ---------------- Skinned character (motion capture) ----------------

        /// <summary>
        /// Chooses and blends the mocap clips from the gameplay state. The jump and roll clips are
        /// scrubbed from the physics so the animation always matches the actual movement.
        /// </summary>
        void DriveHuman(float dt, float speedNorm)
        {
            if (human == null) return;
            System.Array.Clear(humanWeights, 0, humanWeights.Length);
            float sharp = 14f;

            if (IsGliding)
            {
                if (human.Has(HumanoidCharacter.Clip.Glide)) { humanWeights[(int)HumanoidCharacter.Clip.Glide] = 1f; human.Play(HumanoidCharacter.Clip.Glide, 1f); }
                else { humanWeights[(int)HumanoidCharacter.Clip.Jump] = 1f; human.Scrub(HumanoidCharacter.Clip.Jump, 0.5f); }
                sharp = 4f;
            }
            else if (!grounded)
            {
                // 0 at take-off, 0.5 at the apex, 1 at touchdown.
                float t = vy > 0f ? 0.5f * (1f - vy / JumpVelocity) : 0.5f + 0.5f * Mathf.Clamp01(-vy / (JumpVelocity * 1.15f));
                humanWeights[(int)HumanoidCharacter.Clip.Jump] = 1f;
                human.Scrub(HumanoidCharacter.Clip.Jump, Mathf.Lerp(0.22f, 0.78f, t));
                sharp = 20f;
            }
            else if (IsRolling && human.Has(HumanoidCharacter.Clip.Roll))
            {
                humanWeights[(int)HumanoidCharacter.Clip.Roll] = 1f;
                human.Scrub(HumanoidCharacter.Clip.Roll, 1f - rollTimer / RollDuration);
                sharp = 25f;
            }
            else
            {
                humanWeights[(int)HumanoidCharacter.Clip.Run] = 1f;
                human.Play(HumanoidCharacter.Clip.Run, Mathf.Lerp(1.0f, 1.45f, speedNorm));
            }

            if (stumbleTimer > 0f && human.Has(HumanoidCharacter.Clip.Stumble))
                humanWeights[(int)HumanoidCharacter.Clip.Stumble] = Mathf.Clamp01(stumbleTimer / StumbleDuration * 1.5f);

            human.Blend(humanWeights, dt, sharp);
            // Lean into lane changes (and roll forward if there is no roll clip).
            float roll = IsRolling && grounded && !human.Has(HumanoidCharacter.Clip.Roll)
                ? 360f * Mathf.SmoothStep(0f, 1f, 1f - rollTimer / RollDuration) : 0f;
            var q = Quaternion.Euler(0f, yawNow, bankNow) * Quaternion.Euler(roll, 0f, 0f);
            var pivot = new Vector3(0f, roll != 0f ? 0.5f : 0f, 0f);
            human.Root.localRotation = q;
            human.Root.localPosition = pivot - q * pivot;
        }

        void HumanIdle(float dt)
        {
            if (human == null) return;
            System.Array.Clear(humanWeights, 0, humanWeights.Length);
            if (human.Has(HumanoidCharacter.Clip.Idle))
            {
                humanWeights[(int)HumanoidCharacter.Clip.Idle] = 1f;
                human.Play(HumanoidCharacter.Clip.Idle, 1f);
            }
            else
            {
                humanWeights[(int)HumanoidCharacter.Clip.Run] = 1f;
                human.Scrub(HumanoidCharacter.Clip.Run, 0.1f);
            }
            human.Blend(humanWeights, dt, 6f);
            human.Root.localRotation = Quaternion.identity;
            human.Root.localPosition = Vector3.zero;
        }

        void HumanKnocked(float dt, float fallAngle)
        {
            if (human == null) return;
            System.Array.Clear(humanWeights, 0, humanWeights.Length);
            if (human.Has(HumanoidCharacter.Clip.Fall))
            {
                humanWeights[(int)HumanoidCharacter.Clip.Fall] = 1f;
                human.Blend(humanWeights, dt, 20f);
                return;
            }
            // No fall clip: topple the whole character backwards around the heels.
            humanWeights[(int)HumanoidCharacter.Clip.Run] = 1f;
            human.Scrub(HumanoidCharacter.Clip.Run, 0.3f);
            human.Blend(humanWeights, dt, 20f);
            var pivot = new Vector3(0f, 0f, -0.12f);
            var q = Quaternion.Euler(fallAngle, 0f, 0f);
            human.Root.localRotation = q;
            human.Root.localPosition = pivot - q * pivot;
        }

        void OnDestroy() => human?.Dispose();

        void UpdateFx()
        {
            rig.ShieldFx.SetActive(HasPower(PickupType.Shield));
            rig.GliderFx.SetActive(IsGliding);
            rig.MagnetFx.gameObject.SetActive(HasPower(PickupType.Magnet));
            rig.BonanzaFx.SetActive(HasPower(PickupType.Bonanza));
        }
    }
}
