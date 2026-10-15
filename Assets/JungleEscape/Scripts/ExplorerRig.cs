using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// The explorer's articulated body: hips, knees, spine, neck, shoulders and elbows,
    /// plus procedural poses (run, leap, land, tuck-roll, glide, idle, stumble, knockout)
    /// that are blended and smoothed every frame.
    ///
    /// Joint conventions (local Euler X, degrees): thigh negative = leg forward,
    /// knee positive = bend, shoulder negative = arm forward, elbow negative = bend,
    /// spine/neck positive = lean forward.
    /// </summary>
    public class ExplorerRig
    {
        public struct Pose
        {
            public Vector3 ThighL, ThighR, KneeL, KneeR, AnkleL, AnkleR, ShoulderL, ShoulderR, ElbowL, ElbowR, Spine, Neck, Pelvis;
            public float BodyY, Bank, Yaw;

            public static Pose Lerp(Pose a, Pose b, float t)
            {
                return new Pose
                {
                    ThighL = Vector3.LerpUnclamped(a.ThighL, b.ThighL, t), ThighR = Vector3.LerpUnclamped(a.ThighR, b.ThighR, t),
                    KneeL = Vector3.LerpUnclamped(a.KneeL, b.KneeL, t), KneeR = Vector3.LerpUnclamped(a.KneeR, b.KneeR, t),
                    AnkleL = Vector3.LerpUnclamped(a.AnkleL, b.AnkleL, t), AnkleR = Vector3.LerpUnclamped(a.AnkleR, b.AnkleR, t),
                    ShoulderL = Vector3.LerpUnclamped(a.ShoulderL, b.ShoulderL, t), ShoulderR = Vector3.LerpUnclamped(a.ShoulderR, b.ShoulderR, t),
                    ElbowL = Vector3.LerpUnclamped(a.ElbowL, b.ElbowL, t), ElbowR = Vector3.LerpUnclamped(a.ElbowR, b.ElbowR, t),
                    Spine = Vector3.LerpUnclamped(a.Spine, b.Spine, t), Neck = Vector3.LerpUnclamped(a.Neck, b.Neck, t),
                    Pelvis = Vector3.LerpUnclamped(a.Pelvis, b.Pelvis, t),
                    BodyY = Mathf.LerpUnclamped(a.BodyY, b.BodyY, t),
                    Bank = Mathf.LerpUnclamped(a.Bank, b.Bank, t),
                    Yaw = Mathf.LerpUnclamped(a.Yaw, b.Yaw, t)
                };
            }
        }

        const float HipHeight = 0.92f;

        public Transform Root;
        public GameObject ShieldFx, GliderFx, BonanzaFx;
        public Transform MagnetFx;
        public LeafGlider Glider;

        /// <summary>0..1: how much the body hangs from the glider grip (pivots there instead of at the feet).</summary>
        public float Hang;
        /// <summary>Fore-aft swing of the hanging body, in degrees (positive = feet swing back).</summary>
        public float HangPitch;
        /// <summary>Where the hands hold the glider bar, in model space.</summary>
        public static readonly Vector3 GripPoint = new Vector3(0f, 1.95f, 0.1f);

        Transform body, pelvis, spine, neck, thighL, thighR, kneeL, kneeR, ankleL, ankleR, shoulderL, shoulderR, elbowL, elbowR;

        // Joints, for driving an imported character model from these poses (see ModelRetarget)
        public Transform BodyT => body;
        public Transform PelvisT => pelvis;
        public Transform SpineT => spine;
        public Transform NeckT => neck;
        public Transform ThighL => thighL; public Transform ThighR => thighR;
        public Transform KneeL => kneeL; public Transform KneeR => kneeR;
        public Transform AnkleL => ankleL; public Transform AnkleR => ankleR;
        public Transform ShoulderL => shoulderL; public Transform ShoulderR => shoulderR;
        public Transform ElbowL => elbowL; public Transform ElbowR => elbowR;
        Transform frontFlap, backFlap, shadow;
        Pose cur;
        Material shadowMat;

        // ---------------- Poses ----------------

        // Sprint gait keyframes for the LEFT leg over one stride (u = 0 is heel strike).
        // Pairs of (u, degrees). The right leg plays the same curves half a stride later.
        static readonly float[] ThighKeys = { 0f, -30f, 0.12f, -12f, 0.38f, 24f, 0.5f, 18f, 0.68f, -30f, 0.85f, -58f, 0.95f, -40f };
        static readonly float[] KneeKeys = { 0f, 14f, 0.12f, 34f, 0.38f, 14f, 0.58f, 122f, 0.72f, 105f, 0.86f, 45f, 0.96f, 16f };
        static readonly float[] AnkleKeys = { 0f, -6f, 0.12f, -16f, 0.36f, 32f, 0.48f, 28f, 0.66f, -6f, 0.86f, -14f, 0.96f, -8f };

        /// <summary>
        /// Natural running cycle. p is the stride phase in radians. Stance (foot on the ground,
        /// knee absorbing then pushing off through the toes) is followed by swing (heel kicks up
        /// under the hips, knee drives forward, foot reaches for the next strike).
        /// </summary>
        public static Pose Run(float p, float speedNorm)
        {
            float u = p / (Mathf.PI * 2f);
            u -= Mathf.Floor(u);
            float uR = u + 0.5f - Mathf.Floor(u + 0.5f);
            float amp = Mathf.Lerp(0.85f, 1.1f, speedNorm);

            float thL = Cyclic(u, ThighKeys) * amp, thR = Cyclic(uR, ThighKeys) * amp;
            float knL = Cyclic(u, KneeKeys) * Mathf.Lerp(0.85f, 1.05f, speedNorm), knR = Cyclic(uR, KneeKeys) * Mathf.Lerp(0.85f, 1.05f, speedNorm);
            float anL = Cyclic(u, AnkleKeys), anR = Cyclic(uR, AnkleKeys);

            // Arms swing straight forward and back from the shoulder, in time with the opposite
            // leg and centred on the hip (hand travels from beside the hip up to chest height).
            const float thighMean = -15f;
            float shL = 0.8f * (thR - thighMean) - 4f, shR = 0.8f * (thL - thighMean) - 4f;
            float fwdL = Mathf.Clamp01(-shL / 35f), fwdR = Mathf.Clamp01(-shR / 35f);
            float backL = Mathf.Clamp01(shL / 30f), backR = Mathf.Clamp01(shR / 30f);

            float s = Mathf.Sin(u * Mathf.PI * 2f);
            float lean = 11f + 8f * speedNorm;
            float twist = 7f * s;

            return new Pose
            {
                // Legs stay under the hips (no splay), knees track straight ahead.
                ThighL = new Vector3(thL, 0f, 1f), ThighR = new Vector3(thR, 0f, -1f),
                KneeL = new Vector3(knL, 0f, 0f), KneeR = new Vector3(knR, 0f, 0f),
                AnkleL = new Vector3(anL, 0f, 0f), AnkleR = new Vector3(anR, 0f, 0f),
                // Arms hang close to the body; the forward hand drifts slightly toward the midline.
                ShoulderL = new Vector3(shL, 0f, -3f + 7f * fwdL),
                ShoulderR = new Vector3(shR, 0f, 3f - 7f * fwdR),
                // ~90 degree elbows: a little more bent going forward, opening slightly behind.
                ElbowL = new Vector3(-(85f + 20f * fwdL - 15f * backL), 0f, 0f),
                ElbowR = new Vector3(-(85f + 20f * fwdR - 15f * backR), 0f, 0f),
                // Hips rotate with the legs, shoulders counter-rotate; no side-to-side rocking.
                Pelvis = new Vector3(0f, -8f * s, 0f),
                Spine = new Vector3(lean, twist, 0f),
                Neck = new Vector3(-lean * 0.75f + 4f, -0.4f * twist, 0f),
                // Lowest at each mid-stance, highest in the flight phase: two bounces per stride.
                BodyY = -0.04f * Mathf.Cos(4f * Mathf.PI * (u - 0.15f)) - 0.008f
            };
        }

        /// <summary>Smooth (Catmull-Rom) sampling of looping keyframes given as (u, value) pairs.</summary>
        static float Cyclic(float u, float[] keys)
        {
            int n = keys.Length / 2;
            int i = n - 1;
            for (int k = 0; k < n; k++)
                if (keys[k * 2] > u) { i = k - 1; break; }
            if (i < 0) i = n - 1;

            int i0 = (i - 1 + n) % n, i2 = (i + 1) % n, i3 = (i + 2) % n;
            float t1 = keys[i * 2], t2 = keys[i2 * 2];
            if (t2 <= t1) t2 += 1f;
            float uu = u < t1 ? u + 1f : u;
            float f = Mathf.Clamp01((uu - t1) / (t2 - t1));

            float p0 = keys[i0 * 2 + 1], p1 = keys[i * 2 + 1], p2 = keys[i2 * 2 + 1], p3 = keys[i3 * 2 + 1];
            return 0.5f * (2f * p1 + (-p0 + p2) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f + (-p0 + 3f * p1 - 3f * p2 + p3) * f * f * f);
        }

        public static Pose Air(float vy)
        {
            var leap = new Pose
            {
                ThighL = new Vector3(-62f, 0f, 0f), KneeL = new Vector3(80f, 0f, 0f),
                ThighR = new Vector3(22f, 0f, 0f), KneeR = new Vector3(75f, 0f, 0f),
                AnkleL = new Vector3(-5f, 0f, 0f), AnkleR = new Vector3(35f, 0f, 0f),
                ShoulderL = new Vector3(40f, 0f, -6f), ElbowL = new Vector3(-70f, 0f, 0f),
                ShoulderR = new Vector3(-75f, 0f, 6f), ElbowR = new Vector3(-55f, 0f, 0f),
                Spine = new Vector3(16f, 0f, 0f), Neck = new Vector3(-8f, 0f, 0f)
            };
            var drop = new Pose
            {
                ThighL = new Vector3(-30f, 0f, 0f), KneeL = new Vector3(38f, 0f, 0f),
                ThighR = new Vector3(-8f, 0f, 0f), KneeR = new Vector3(48f, 0f, 0f),
                AnkleL = new Vector3(-12f, 0f, 0f), AnkleR = new Vector3(5f, 0f, 0f),
                ShoulderL = new Vector3(-25f, 0f, -28f), ElbowL = new Vector3(-50f, 0f, 0f),
                ShoulderR = new Vector3(-25f, 0f, 28f), ElbowR = new Vector3(-50f, 0f, 0f),
                Spine = new Vector3(6f, 0f, 0f), Neck = new Vector3(4f, 0f, 0f)
            };
            return Pose.Lerp(leap, drop, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, -7f, vy)));
        }

        public static Pose Land()
        {
            return new Pose
            {
                ThighL = new Vector3(-48f, 0f, 0f), KneeL = new Vector3(88f, 0f, 0f),
                ThighR = new Vector3(-30f, 0f, 0f), KneeR = new Vector3(78f, 0f, 0f),
                AnkleL = new Vector3(-28f, 0f, 0f), AnkleR = new Vector3(-24f, 0f, 0f),
                ShoulderL = new Vector3(-30f, 0f, -12f), ElbowL = new Vector3(-65f, 0f, 0f),
                ShoulderR = new Vector3(-30f, 0f, 12f), ElbowR = new Vector3(-65f, 0f, 0f),
                Spine = new Vector3(28f, 0f, 0f), Neck = new Vector3(-18f, 0f, 0f),
                BodyY = -0.24f
            };
        }

        /// <summary>
        /// Running on all fours like a gorilla: a knuckle-walking gallop. The torso is pitched forward almost
        /// level with the head up, the arms work as front legs (both reaching and landing a beat apart), then
        /// the legs swing through together to push off; the body rises and rocks with each bound.
        /// p is the stride phase in radians (one bound per stride).
        /// </summary>
        public static Pose Gallop(float p, float speedNorm)
        {
            float amp = Mathf.Lerp(0.85f, 1.05f, speedNorm);
            float s = Mathf.Sin(p), push = Mathf.Max(0f, Mathf.Sin(2f * p));

            // Arms: front legs. Shoulder ~-75 holds the arm vertical under a torso pitched 75 degrees forward;
            // with the hips down at ~0.5 m the knuckles just reach the ground (shoulder ~0.66 m, arm ~0.62 m).
            float armL = -75f + 40f * amp * Mathf.Sin(p), armR = -75f + 40f * amp * Mathf.Sin(p + 0.5f);
            float elbL = -(8f + 38f * Mathf.Max(0f, Mathf.Cos(p))), elbR = -(8f + 38f * Mathf.Max(0f, Mathf.Cos(p + 0.5f)));
            // Legs: half a stride behind the arms, swinging through together with a slight stagger.
            float lp = p + Mathf.PI;
            float thL = -55f + 30f * amp * Mathf.Sin(lp), thR = -55f + 30f * amp * Mathf.Sin(lp + 0.35f);   // deep crouch
            float knL = 98f + 32f * Mathf.Max(0f, Mathf.Cos(lp)), knR = 98f + 32f * Mathf.Max(0f, Mathf.Cos(lp + 0.35f));

            return new Pose
            {
                ThighL = new Vector3(thL, 0f, 6f), ThighR = new Vector3(thR, 0f, -6f),
                KneeL = new Vector3(knL, 0f, 0f), KneeR = new Vector3(knR, 0f, 0f),
                AnkleL = new Vector3(-10f + 20f * Mathf.Max(0f, Mathf.Cos(lp)), 0f, 0f), AnkleR = new Vector3(-10f + 20f * Mathf.Max(0f, Mathf.Cos(lp + 0.35f)), 0f, 0f),
                // Arms slightly out to the sides, like a gorilla's
                ShoulderL = new Vector3(armL, 0f, -10f), ShoulderR = new Vector3(armR, 0f, 10f),
                ElbowL = new Vector3(elbL, 0f, 0f), ElbowR = new Vector3(elbR, 0f, 0f),
                // Back nearly level and flexing with the bound; head up, looking ahead
                Spine = new Vector3(75f + 6f * s, 4f * Mathf.Sin(p + 0.3f), 0f),
                Neck = new Vector3(-60f - 5f * s, 0f, 0f),
                Pelvis = new Vector3(-6f * s, 0f, 3f * Mathf.Sin(p)),
                // Low to the ground, lifting off with each push
                BodyY = -0.42f + 0.08f * push * amp
            };
        }

        public static Pose Tuck()
        {
            return new Pose
            {
                ThighL = new Vector3(-118f, 0f, 3f), KneeL = new Vector3(140f, 0f, 0f),
                ThighR = new Vector3(-118f, 0f, -3f), KneeR = new Vector3(140f, 0f, 0f),
                AnkleL = new Vector3(30f, 0f, 0f), AnkleR = new Vector3(30f, 0f, 0f),
                ShoulderL = new Vector3(-80f, 0f, -12f), ElbowL = new Vector3(-95f, 0f, 0f),
                ShoulderR = new Vector3(-80f, 0f, 12f), ElbowR = new Vector3(-95f, 0f, 0f),
                Spine = new Vector3(55f, 0f, 0f), Neck = new Vector3(35f, 0f, 0f)
            };
        }

        /// <summary>
        /// Hanging from the leaf glider's bar. Arms reach up to the grip (pulling in when climbing), the body
        /// hangs below, and the legs dangle loosely: they trail behind the pendulum swing, drift independently,
        /// tuck up on a climb, stretch back on a dive and reach down ahead of a landing.
        /// </summary>
        /// <param name="swingVel">Sideways swing speed of the body, deg/s (legs lag behind it).</param>
        /// <param name="vy">Vertical speed, m/s.</param>
        /// <param name="flare">0..1: preparing to land.</param>
        public static Pose Glide(float t, float swingVel, float vy, float flare)
        {
            float climb = Mathf.Clamp01(vy / 3f), dive = Mathf.Clamp01(-vy / 4f) * (1f - flare);
            float driftL = Mathf.Sin(t * 1.7f), driftR = Mathf.Sin(t * 1.7f + 1.3f);
            float lag = Mathf.Clamp(-swingVel * 0.12f, -18f, 18f);    // legs trail the swing
            float tuck = climb * 30f, reach = flare;

            float thigh = Mathf.Lerp(-8f, -32f, climb) + 18f * dive;
            thigh = Mathf.Lerp(thigh, -24f, reach);
            float knee = Mathf.Lerp(16f, 60f, climb) - 6f * dive;
            knee = Mathf.Lerp(knee, 22f, reach);
            float pull = 18f * climb;                                   // arms pull the bar in on a climb
            return new Pose
            {
                ThighL = new Vector3(thigh + 6f * driftL, 0f, -3f + lag), KneeL = new Vector3(knee + 8f * Mathf.Max(0f, driftL) + tuck * 0.3f, 0f, 0f),
                ThighR = new Vector3(thigh + 6f * driftR - 4f, 0f, 3f + lag), KneeR = new Vector3(knee + 10f + 8f * Mathf.Max(0f, driftR), 0f, 0f),
                AnkleL = new Vector3(Mathf.Lerp(32f, 8f, reach), 0f, 0f), AnkleR = new Vector3(Mathf.Lerp(26f, 6f, reach), 0f, 0f),
                ShoulderL = new Vector3(-168f + pull, 0f, -14f), ElbowL = new Vector3(-22f - pull * 1.6f, 0f, 0f),
                ShoulderR = new Vector3(-168f + pull, 0f, 14f), ElbowR = new Vector3(-22f - pull * 1.6f, 0f, 0f),
                // Back gently arched, head up and looking ahead; the hips swing a little with the legs.
                Spine = new Vector3(-6f + 8f * dive - 4f * climb, 0f, lag * 0.3f), Neck = new Vector3(4f - 8f * dive, 0f, 0f),
                Pelvis = new Vector3(0f, 0f, lag * 0.4f),
                BodyY = 0f
            };
        }

        public static Pose Idle(float t)
        {
            float breath = Mathf.Sin(t * 1.6f);
            float look = Mathf.Sin(t * 0.37f) * Mathf.Sin(t * 0.19f);
            return new Pose
            {
                ThighL = new Vector3(-2f, 0f, -3f), KneeL = new Vector3(6f, 0f, 0f),
                ThighR = new Vector3(2f, 0f, 3f), KneeR = new Vector3(4f, 0f, 0f),
                ShoulderL = new Vector3(3f * breath, 0f, -7f), ElbowL = new Vector3(-14f, 0f, 0f),
                ShoulderR = new Vector3(-3f * breath, 0f, 7f), ElbowR = new Vector3(-12f, 0f, 0f),
                Spine = new Vector3(2f + 1.5f * breath, 0f, 1.5f), Neck = new Vector3(-2f, 25f * look, 0f),
                Pelvis = new Vector3(0f, 0f, -2f),
                BodyY = 0.006f * breath - 0.01f
            };
        }

        public static Pose Knocked()
        {
            return new Pose
            {
                ThighL = new Vector3(-28f, 0f, -12f), KneeL = new Vector3(35f, 0f, 0f),
                ThighR = new Vector3(-6f, 0f, 10f), KneeR = new Vector3(12f, 0f, 0f),
                AnkleL = new Vector3(20f, 0f, 0f), AnkleR = new Vector3(25f, 0f, 0f),
                ShoulderL = new Vector3(-30f, 0f, -75f), ElbowL = new Vector3(-35f, 0f, 0f),
                ShoulderR = new Vector3(-50f, 0f, 70f), ElbowR = new Vector3(-20f, 0f, 0f),
                Spine = new Vector3(-12f, 0f, 0f), Neck = new Vector3(22f, 15f, 0f)
            };
        }

        // ---------------- Applying ----------------

        /// <summary>Snaps straight to a pose (used on reset).</summary>
        public void Snap(Pose p)
        {
            cur = p;
            Apply(p, 0f, 0f, 0f, 1f);
        }

        /// <summary>
        /// Eases the skeleton toward the target pose. rollAngle spins the whole body around the
        /// tucked hips (somersault); fallAngle tips it backwards around the heels (knockout).
        /// </summary>
        public void Apply(Pose target, float dt, float rollAngle, float fallAngle, float sharpness = 18f)
        {
            float k = dt <= 0f ? 1f : 1f - Mathf.Exp(-sharpness * dt);
            cur = Pose.Lerp(cur, target, k);

            thighL.localRotation = Quaternion.Euler(cur.ThighL);
            thighR.localRotation = Quaternion.Euler(cur.ThighR);
            kneeL.localRotation = Quaternion.Euler(cur.KneeL);
            kneeR.localRotation = Quaternion.Euler(cur.KneeR);
            ankleL.localRotation = Quaternion.Euler(cur.AnkleL);
            ankleR.localRotation = Quaternion.Euler(cur.AnkleR);
            shoulderL.localRotation = Quaternion.Euler(cur.ShoulderL);
            shoulderR.localRotation = Quaternion.Euler(cur.ShoulderR);
            elbowL.localRotation = Quaternion.Euler(cur.ElbowL);
            elbowR.localRotation = Quaternion.Euler(cur.ElbowR);
            spine.localRotation = Quaternion.Euler(cur.Spine);
            neck.localRotation = Quaternion.Euler(cur.Neck);
            pelvis.localRotation = Quaternion.Euler(cur.Pelvis);

            // Whole-body transform: bank/yaw about the feet, plus somersault or fall about a pivot.
            Quaternion baseRot = Quaternion.Euler(0f, cur.Yaw, cur.Bank);
            Vector3 offset = new Vector3(0f, cur.BodyY, 0f);
            if (Mathf.Abs(rollAngle) > 0.01f)
            {
                Vector3 pivot = new Vector3(0f, HipHeight - 0.05f, 0.08f);
                Quaternion r = Quaternion.Euler(rollAngle, 0f, 0f);
                body.localRotation = baseRot * r;
                body.localPosition = offset + pivot - (baseRot * (r * pivot));
            }
            else if (Mathf.Abs(fallAngle) > 0.01f)
            {
                Vector3 pivot = new Vector3(0f, 0f, -0.12f);
                Quaternion r = Quaternion.Euler(fallAngle, 0f, 0f);
                body.localRotation = r;
                body.localPosition = pivot - r * pivot + new Vector3(0f, Mathf.Sin(-fallAngle * Mathf.Deg2Rad) * 0.12f, 0f);
            }
            else
            {
                // Hanging from the glider, the body swings like a pendulum about the hands, not the feet.
                Quaternion rot = Quaternion.Euler(HangPitch * Hang, cur.Yaw, cur.Bank);
                Vector3 pivot = GripPoint * Hang;
                body.localRotation = rot;
                body.localPosition = offset * (1f - Hang) + pivot - rot * pivot;
            }
        }

        /// <summary>Loincloth flaps: the front one is pushed by the thighs, the back one trails in the wind.</summary>
        public void Secondary(float time, float runPhase, float runWeight, float speedNorm)
        {
            float front = Mathf.Min(cur.ThighL.x, cur.ThighR.x);
            float back = Mathf.Max(cur.ThighL.x, cur.ThighR.x);
            float flutter = Mathf.Sin(time * 15f) * (2f + 6f * runWeight);
            frontFlap.localRotation = Quaternion.Euler(Mathf.Min(0f, front * 0.9f) + flutter, 0f, 3f * Mathf.Sin(time * 9f) * runWeight);
            backFlap.localRotation = Quaternion.Euler(Mathf.Max(0f, back * 0.6f) + runWeight * (22f + 25f * speedNorm) + 1.4f * flutter,
                                                      0f, 4f * Mathf.Sin(time * 11f + 1f) * runWeight);
        }

        /// <summary>
        /// Hides the built-in body (used when a skinned character model is present) while keeping
        /// the power-up effects, which still follow this invisible skeleton.
        /// </summary>
        public void HideBody()
        {
            var keep = new[] { ShieldFx.transform, GliderFx.transform, MagnetFx, BonanzaFx.transform };
            foreach (var r in Root.GetComponentsInChildren<Renderer>(true))
            {
                bool fx = false;
                foreach (var k in keep) if (r.transform.IsChildOf(k)) { fx = true; break; }
                if (!fx) r.enabled = false;
            }
        }

        /// <summary>Keeps the soft contact shadow on the ground under the feet.</summary>
        public void GroundShadow(float height)
        {
            shadow.localPosition = new Vector3(0f, -height + 0.02f, 0f);
            float s = Mathf.Lerp(1.0f, 0.55f, Mathf.Clamp01(height / 3f));
            shadow.localScale = new Vector3(s * 0.8f, 1f, s * 1.0f);
            var c = new Color(0f, 0f, 0f, Mathf.Lerp(0.55f, 0.12f, Mathf.Clamp01(height / 4f)));
            shadowMat.SetColor("_BaseColor", c);
        }

        // ---------------- Building ----------------

        /// <summary>
        /// A feral jungle boy in the spirit of Kipling's Mowgli: slim, sun-browned, barefoot,
        /// a mop of black hair, a cloth loincloth tied with a vine, vine bands on the wrists.
        /// </summary>
        public void Build(Transform parent)
        {
            var skin = Mats.Skin(new Color(0.53f, 0.34f, 0.22f));
            var skinDark = Mats.Skin(new Color(0.40f, 0.24f, 0.16f));
            var hair = Mats.Lit("JungleHair", Tex.Fur, new Color(0.055f, 0.045f, 0.04f), 0.45f, tiling: new Vector2(3f, 2f));
            var cloth = Mats.Fabric(new Color(0.60f, 0.13f, 0.08f));
            var clothDark = Mats.Fabric(new Color(0.46f, 0.10f, 0.06f));
            var vine = Mats.Vine;
            var tooth = Mats.Plain(new Color(0.9f, 0.85f, 0.72f), 0.5f);
            var eyeWhite = Mats.Plain(new Color(0.93f, 0.91f, 0.87f), 0.75f);
            var iris = Mats.Plain(new Color(0.10f, 0.06f, 0.03f), 0.95f);
            var brow = Mats.Plain(new Color(0.05f, 0.04f, 0.035f), 0.3f);

            var ball = Meshes.Ball(12, 18);

            Root = Prim.Group("Model", parent, Vector3.zero);
            Root.localScale = Vector3.one * 0.92f; // a lean teenager, a little shorter than an adult
            body = Prim.Group("Body", Root, Vector3.zero);
            pelvis = Prim.Group("Pelvis", body, new Vector3(0f, HipHeight, 0f));

            // ---- Hips & loincloth ----
            var hipsMesh = Meshes.Lathe(new[]
            {
                new Vector2(0.09f, -0.15f), new Vector2(0.128f, -0.11f), new Vector2(0.14f, -0.04f), new Vector2(0.135f, 0.05f)
            }, 18, 2f, "Hips");
            Prim.Shape(hipsMesh, "Hips", pelvis, Vector3.zero, new Vector3(1f, 1f, 0.74f), skin);
            var wrap = Meshes.Lathe(new[]
            {
                new Vector2(0.118f, -0.13f), new Vector2(0.14f, -0.09f), new Vector2(0.148f, -0.03f), new Vector2(0.142f, 0.035f)
            }, 18, 3f, "Wrap");
            Prim.Shape(wrap, "Loincloth", pelvis, Vector3.zero, new Vector3(1.02f, 1f, 0.78f), cloth);
            var band = Meshes.Lathe(new[] { new Vector2(0.144f, 0f), new Vector2(0.151f, 0.016f), new Vector2(0.144f, 0.032f) }, 18, 4f);
            Prim.Shape(band, "VineBelt", pelvis, new Vector3(0f, 0.02f, 0f), new Vector3(1f, 1f, 0.78f), vine);
            Prim.Shape(ball, "Knot", pelvis, new Vector3(0.11f, 0.03f, 0.06f), new Vector3(0.05f, 0.04f, 0.04f), vine);

            var flap = Meshes.RoundedBox(new Vector3(0.15f, 0.26f, 0.018f), 0.008f);
            frontFlap = Prim.Group("FrontFlap", pelvis, new Vector3(0f, 0.0f, 0.112f));
            Prim.Shape(flap, "Flap", frontFlap, new Vector3(0f, -0.12f, 0f), Vector3.one, cloth);
            backFlap = Prim.Group("BackFlap", pelvis, new Vector3(0f, 0.0f, -0.108f));
            Prim.Shape(flap, "Flap", backFlap, new Vector3(0f, -0.13f, 0f), new Vector3(1.12f, 1.08f, 1f), clothDark);

            // ---- Bare legs & feet ----
            var thigh = Meshes.Limb(0.42f, 0.072f, 0.055f, 12, 8);
            var shin = Meshes.Limb(0.405f, 0.054f, 0.038f, 12, 8);
            var foot = Meshes.RoundedBox(new Vector3(0.085f, 0.055f, 0.2f), 0.026f);

            thighL = Prim.Group("ThighL", pelvis, new Vector3(-0.08f, -0.02f, 0f));
            thighR = Prim.Group("ThighR", pelvis, new Vector3(0.08f, -0.02f, 0f));
            kneeL = Prim.Group("KneeL", thighL, new Vector3(0f, -0.42f, 0f));
            kneeR = Prim.Group("KneeR", thighR, new Vector3(0f, -0.42f, 0f));
            ankleL = Prim.Group("AnkleL", kneeL, new Vector3(0f, -0.405f, 0f));
            ankleR = Prim.Group("AnkleR", kneeR, new Vector3(0f, -0.405f, 0f));
            for (int s = -1; s <= 1; s += 2)
            {
                var th = s < 0 ? thighL : thighR;
                var kn = s < 0 ? kneeL : kneeR;
                var an = s < 0 ? ankleL : ankleR;
                Prim.Shape(thigh, "Thigh", th, Vector3.zero, Vector3.one, skin);
                Prim.Shape(ball, "KneeCap", kn, new Vector3(0f, 0f, 0.012f), new Vector3(0.1f, 0.1f, 0.1f), skin);
                Prim.Shape(shin, "Shin", kn, Vector3.zero, Vector3.one, skin);
                Prim.Shape(ball, "AnkleBone", an, Vector3.zero, new Vector3(0.075f, 0.07f, 0.08f), skin);
                Prim.Shape(foot, "Foot", an, new Vector3(0f, -0.04f, 0.045f), Vector3.one, skin);
                Prim.Shape(ball, "Heel", an, new Vector3(0f, -0.04f, -0.04f), new Vector3(0.075f, 0.06f, 0.07f), skin);
                Prim.Shape(ball, "Toes", an, new Vector3(0f, -0.05f, 0.14f), new Vector3(0.085f, 0.04f, 0.055f), skinDark);
            }

            // ---- Bare torso ----
            spine = Prim.Group("Spine", pelvis, new Vector3(0f, 0.05f, 0f));
            var torso = Meshes.Lathe(new[]
            {
                new Vector2(0.13f, -0.06f), new Vector2(0.134f, 0.04f), new Vector2(0.14f, 0.16f), new Vector2(0.162f, 0.3f),
                new Vector2(0.176f, 0.4f), new Vector2(0.163f, 0.47f), new Vector2(0.1f, 0.52f), new Vector2(0.05f, 0.54f),
                new Vector2(0.001f, 0.545f)
            }, 22, 2f, "Torso");
            Prim.Shape(torso, "Torso", spine, Vector3.zero, new Vector3(1f, 1f, 0.66f), skin);
            // Subtle chest and shoulder-blade forms so the bare torso does not read as a tube.
            Prim.Shape(ball, "ChestL", spine, new Vector3(-0.06f, 0.36f, 0.075f), new Vector3(0.13f, 0.1f, 0.06f), skin);
            Prim.Shape(ball, "ChestR", spine, new Vector3(0.06f, 0.36f, 0.075f), new Vector3(0.13f, 0.1f, 0.06f), skin);
            Prim.Shape(ball, "BladeL", spine, new Vector3(-0.07f, 0.37f, -0.07f), new Vector3(0.12f, 0.14f, 0.05f), skin);
            Prim.Shape(ball, "BladeR", spine, new Vector3(0.07f, 0.37f, -0.07f), new Vector3(0.12f, 0.14f, 0.05f), skin);

            // Cord necklace with a single tooth
            var cord = new List<Vector3>();
            for (int i = 0; i <= 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                cord.Add(new Vector3(Mathf.Cos(a) * 0.09f, -Mathf.Max(0f, Mathf.Sin(a)) * 0.05f, Mathf.Sin(a) * 0.075f));
            }
            Prim.Shape(Meshes.Sweep(cord, (t, a) => 0.007f, 5, 1f, 4f), "Necklace", spine, new Vector3(0f, 0.5f, 0.005f), Vector3.one, clothDark, null, false);
            var fang = Meshes.Lathe(new[] { new Vector2(0.001f, 0f), new Vector2(0.012f, 0.03f), new Vector2(0.008f, 0.045f), new Vector2(0.001f, 0.05f) }, 8);
            Prim.Shape(fang, "Fang", spine, new Vector3(0f, 0.395f, 0.088f), Vector3.one, tooth, new Vector3(180f, 0f, 0f), false);

            // ---- Neck & head ----
            neck = Prim.Group("Neck", spine, new Vector3(0f, 0.5f, 0f));
            Prim.Shape(Meshes.Limb(0.12f, 0.048f, 0.045f, 10, 5), "NeckMesh", neck, new Vector3(0f, 0.12f, 0f), Vector3.one, skin);

            var head = Prim.Group("Head", neck, new Vector3(0f, 0.165f, 0.01f));
            Prim.Shape(ball, "Skull", head, Vector3.zero, new Vector3(0.19f, 0.22f, 0.205f), skin);
            Prim.Shape(ball, "Jaw", head, new Vector3(0f, -0.06f, 0.022f), new Vector3(0.15f, 0.115f, 0.15f), skin);
            Prim.Shape(ball, "Nose", head, new Vector3(0f, -0.012f, 0.103f), new Vector3(0.032f, 0.045f, 0.04f), skin);
            Prim.Shape(ball, "EarL", head, new Vector3(-0.094f, -0.005f, -0.005f), new Vector3(0.032f, 0.06f, 0.035f), skin);
            Prim.Shape(ball, "EarR", head, new Vector3(0.094f, -0.005f, -0.005f), new Vector3(0.032f, 0.06f, 0.035f), skin);
            Prim.Shape(Meshes.RoundedBox(new Vector3(0.04f, 0.007f, 0.01f), 0.003f), "Mouth", head, new Vector3(0f, -0.058f, 0.094f), Vector3.one, skinDark, null, false);
            for (int s = -1; s <= 1; s += 2)
            {
                Prim.Shape(ball, "Eye", head, new Vector3(s * 0.037f, 0.016f, 0.086f), new Vector3(0.036f, 0.026f, 0.02f), eyeWhite, null, false);
                Prim.Shape(ball, "Iris", head, new Vector3(s * 0.037f, 0.016f, 0.095f), Vector3.one * 0.016f, iris, null, false);
                Prim.Shape(Meshes.RoundedBox(new Vector3(0.042f, 0.009f, 0.012f), 0.004f), "Brow", head,
                           new Vector3(s * 0.039f, 0.043f, 0.094f), Vector3.one, brow, new Vector3(0f, 0f, s * -10f), false);
            }

            // Messy mop of black hair: several noisy clumps (shaggy on top, longer at the back).
            Prim.Shape(Meshes.Blob(41, 2, 0.5f, 2.6f, 0f, false), "HairTop", head, new Vector3(0f, 0.05f, -0.012f), new Vector3(0.235f, 0.2f, 0.255f), hair);
            Prim.Shape(Meshes.Blob(42, 2, 0.45f, 2.4f, 0f, false), "HairBack", head, new Vector3(0f, -0.02f, -0.06f), new Vector3(0.235f, 0.23f, 0.18f), hair);
            Prim.Shape(Meshes.Blob(43, 2, 0.6f, 3f, 0f, false), "Fringe", head, new Vector3(0f, 0.075f, 0.07f), new Vector3(0.2f, 0.085f, 0.1f), hair, new Vector3(25f, 0f, 0f));
            Prim.Shape(Meshes.Blob(44, 2, 0.55f, 2.8f, 0f, false), "SideL", head, new Vector3(-0.092f, 0.01f, -0.02f), new Vector3(0.07f, 0.15f, 0.15f), hair);
            Prim.Shape(Meshes.Blob(45, 2, 0.55f, 2.8f, 0f, false), "SideR", head, new Vector3(0.092f, 0.01f, -0.02f), new Vector3(0.07f, 0.15f, 0.15f), hair);
            Prim.Shape(Meshes.Blob(46, 2, 0.7f, 3.2f, 0f, false), "Tuft", head, new Vector3(0.03f, 0.13f, -0.03f), new Vector3(0.1f, 0.07f, 0.12f), hair, new Vector3(-20f, 30f, 15f));

            // ---- Bare arms ----
            var upperArm = Meshes.Limb(0.29f, 0.05f, 0.042f, 12, 7);
            var forearm = Meshes.Limb(0.26f, 0.04f, 0.031f, 12, 7);
            var palm = Meshes.RoundedBox(new Vector3(0.062f, 0.08f, 0.03f), 0.014f);
            var bracelet = Meshes.Lathe(new[] { new Vector2(0.034f, 0f), new Vector2(0.04f, 0.012f), new Vector2(0.034f, 0.024f) }, 12, 2f);

            shoulderL = Prim.Group("ShoulderL", spine, new Vector3(-0.19f, 0.43f, -0.005f));
            shoulderR = Prim.Group("ShoulderR", spine, new Vector3(0.19f, 0.43f, -0.005f));
            elbowL = Prim.Group("ElbowL", shoulderL, new Vector3(0f, -0.29f, 0f));
            elbowR = Prim.Group("ElbowR", shoulderR, new Vector3(0f, -0.29f, 0f));
            for (int s = -1; s <= 1; s += 2)
            {
                var sh = s < 0 ? shoulderL : shoulderR;
                var el = s < 0 ? elbowL : elbowR;
                Prim.Shape(ball, "Deltoid", sh, new Vector3(0f, -0.025f, 0f), new Vector3(0.105f, 0.12f, 0.105f), skin);
                Prim.Shape(upperArm, "UpperArm", sh, Vector3.zero, Vector3.one, skin);
                Prim.Shape(ball, "Elbow", el, Vector3.zero, Vector3.one * 0.07f, skin);
                Prim.Shape(forearm, "Forearm", el, Vector3.zero, Vector3.one, skin);
                Prim.Shape(bracelet, "VineBand", el, new Vector3(0f, -0.235f, 0f), Vector3.one, vine);
                Prim.Shape(palm, "Hand", el, new Vector3(0f, -0.295f, 0.005f), Vector3.one, skin);
                Prim.Shape(ball, "Thumb", el, new Vector3(s * -0.03f, -0.272f, 0.022f), new Vector3(0.022f, 0.045f, 0.022f), skin, new Vector3(20f, 0f, s * 25f));
            }

            // ---- Contact shadow ----
            shadowMat = new Material(Mats.ParticleAlpha(Tex.Glow)) { name = "ContactShadow" };
            shadow = Prim.Shape(Meshes.Disc(24), "ContactShadow", parent, new Vector3(0f, 0.02f, 0f), Vector3.one, shadowMat, null, false).transform;

            // ---- Power-up effects ----
            ShieldFx = Prim.Shape(ball, "CoconutHelmet", head, new Vector3(0f, 0.11f, 0f), new Vector3(0.34f, 0.25f, 0.36f), Mats.Coconut);

            // Leaf glider: hangs from the grip point in model space, so the body can swing beneath it.
            var gliderRoot = Prim.Group("LeafGlider", Root, GripPoint);
            GliderFx = gliderRoot.gameObject;
            Glider = GliderFx.AddComponent<LeafGlider>();
            Glider.Build();

            MagnetFx = Prim.Group("VineMagnet", body, new Vector3(0f, 1.1f, 0f));
            Prim.Shape(ball, "Orb", MagnetFx, new Vector3(0.75f, 0f, 0f), Vector3.one * 0.22f, Mats.Gem, null, false);
            Prim.Shape(ball, "Orb", MagnetFx, new Vector3(-0.75f, 0f, 0f), Vector3.one * 0.22f, Mats.Gem, null, false);

            var crown = Prim.Group("GoldenCrown", head, new Vector3(0f, 0.2f, 0f));
            BonanzaFx = crown.gameObject;
            var ring = Meshes.Lathe(new[] { new Vector2(0.12f, 0f), new Vector2(0.13f, 0.02f), new Vector2(0.14f, 0.08f), new Vector2(0.13f, 0.09f) }, 20);
            Prim.Shape(ring, "Band", crown, Vector3.zero, Vector3.one, Mats.Gold);
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad;
                Prim.Shape(ball, "Point", crown, new Vector3(Mathf.Cos(ang) * 0.135f, 0.11f, Mathf.Sin(ang) * 0.135f), Vector3.one * 0.045f, Mats.Gold);
            }

            Snap(Idle(0f));
        }
    }

    /// <summary>
    /// The Leaf Glider: one huge leaf spread like a wing (midrib across the span, pointed tips drooping into an
    /// arch, a cambered airfoil chord), held from a wooden control bar by four vine lines.
    /// The leaf is a live mesh: its tips flex up under load, the trailing edge flutters in the airflow and
    /// gusts ripple across it. The veins and midrib bend with it. Banking and pitching are driven by the
    /// player's glide physics through <see cref="Drive"/>.
    /// </summary>
    public class LeafGlider : MonoBehaviour
    {
        const float Span = 2.9f, Chord = 1.5f, CanopyHeight = 0.95f;
        const int NU = 9, NV = 17;

        Transform canopy;
        Mesh leaf, ribs;
        Vector3[] leafBase, ribsBase, leafNow, ribsNow;
        Transform[] lines;
        Vector3[] lineAttach, lineBar;
        float load, gust, time;

        /// <summary>Vertical offset of the leaf surface at canopy-space (x, z), from load, flutter and gusts.</summary>
        float Deform(float x, float z)
        {
            float v = x / Span + 0.5f, u = z / Chord + 0.5f;
            float s = 2f * v - 1f;
            float flex = load * 0.16f * s * s;                                           // tips bend up under load
            float trail = Mathf.Clamp01(1f - u);
            float flutter = (0.022f + 0.03f * gust) * trail * trail * Mathf.Sin(time * 17f + v * 8f); // trailing edge
            float ripple = 0.012f * gust * Mathf.Sin(time * 6f - v * 9f + u * 4f);        // gust ripples across the leaf
            return flex + flutter + ripple;
        }

        static float Shape(float u, float v)
        {
            float s = 2f * v - 1f;
            // Arched span (tips droop), cambered chord (airfoil hump, thinner toward the tips), leading edge raised.
            return -0.5f * s * s + 0.15f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)) * (1f - 0.6f * s * s) + 0.06f * (u - 0.5f);
        }

        public void Build()
        {
            canopy = Prim.Group("Canopy", transform, new Vector3(0f, CanopyHeight, 0.05f));

            // ---- The leaf: a grid mapped so the leaf texture's length runs across the span ----
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int j = 0; j < NV; j++)
                for (int i = 0; i < NU; i++)
                {
                    float u = i / (NU - 1f), v = j / (NV - 1f);
                    verts.Add(new Vector3((v - 0.5f) * Span, Shape(u, v), (u - 0.5f) * Chord));
                    uvs.Add(new Vector2(u, v));
                }
            for (int j = 0; j < NV - 1; j++)
                for (int i = 0; i < NU - 1; i++)
                {
                    int a = j * NU + i, b = a + 1, c = a + NU, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            leaf = new Mesh { name = "GliderLeaf" };
            leaf.MarkDynamic();
            leaf.SetVertices(verts);
            leaf.SetUVs(0, uvs);
            leaf.SetTriangles(tris, 0);
            leaf.RecalculateNormals();
            leaf.RecalculateTangents();
            leaf.bounds = new Bounds(Vector3.zero, new Vector3(Span + 1f, 2f, Chord + 1f));
            Prim.Shape(leaf, "Leaf", canopy, Vector3.zero, Vector3.one, Mats.BroadLeaf);

            // ---- Midrib, stalk and veins (combined; they bend with the leaf) ----
            var parts = new List<CombineInstance>();
            var mid = new List<Vector3>();
            for (int j = 0; j <= 16; j++)
            {
                float v = j / 16f;
                mid.Add(new Vector3((v - 0.5f) * Span, Shape(0.5f, v) + 0.012f, 0f));
            }
            // The stalk continues past the base of the leaf and curls down a little
            var stalk = new List<Vector3> { mid[0], mid[0] + new Vector3(-0.14f, 0.0f, 0f), mid[0] + new Vector3(-0.26f, -0.06f, 0.02f) };
            parts.Add(new CombineInstance { mesh = Meshes.Sweep(mid, (t, a) => Mathf.Lerp(0.026f, 0.006f, t), 6, 1f, 2f), transform = Matrix4x4.identity });
            parts.Add(new CombineInstance { mesh = Meshes.Sweep(stalk, (t, a) => 0.026f, 6, 1f, 2f, true), transform = Matrix4x4.identity });
            for (int k = 1; k <= 7; k++)
            {
                float v0 = k / 8.5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    var vein = new List<Vector3>();
                    for (int q = 0; q <= 4; q++)
                    {
                        float f = q / 4f;
                        float u = 0.5f + side * 0.44f * f;
                        float v = v0 + 0.05f * f;            // veins sweep toward the tip
                        vein.Add(new Vector3((v - 0.5f) * Span, Shape(u, v) + 0.008f, (u - 0.5f) * Chord));
                    }
                    parts.Add(new CombineInstance { mesh = Meshes.Sweep(vein, (t, a) => Mathf.Lerp(0.007f, 0.002f, t), 4, 1f, 2f), transform = Matrix4x4.identity });
                }
            }
            ribs = Meshes.Combine(parts, "GliderRibs");
            ribs.MarkDynamic();
            Prim.Shape(ribs, "Ribs", canopy, Vector3.zero, Vector3.one, Mats.Vine, null, false);

            leafBase = leaf.vertices; leafNow = (Vector3[])leafBase.Clone();
            ribsBase = ribs.vertices; ribsNow = (Vector3[])ribsBase.Clone();

            // ---- Control bar the explorer holds ----
            var bar = new List<Vector3> { new Vector3(-0.5f, 0f, 0f), new Vector3(0f, 0.012f, 0f), new Vector3(0.5f, 0f, 0f) };
            Prim.Shape(Meshes.Sweep(bar, (t, a) => 0.022f, 8, 1f, 2f, true), "Bar", transform, Vector3.zero, Vector3.one, Mats.Bark);
            var knot = Meshes.Ball(6, 8);
            for (int k = -1; k <= 1; k += 2)
                Prim.Shape(knot, "Lashing", transform, new Vector3(k * 0.45f, 0f, 0f), new Vector3(0.06f, 0.05f, 0.05f), Mats.Rope, null, false);

            // ---- Four vine lines from the bar ends up to the leaf ----
            var lineMesh = Meshes.Limb(1f, 0.008f, 0.008f, 5, 2);
            lines = new Transform[4];
            lineAttach = new Vector3[4];
            lineBar = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? 1f : -1f;
                lineBar[i] = new Vector3(sx * 0.45f, 0f, 0f);
                lineAttach[i] = new Vector3(sx * 0.8f, 0f, sz * 0.38f);
                lineAttach[i].y = Shape(lineAttach[i].z / Chord + 0.5f, lineAttach[i].x / Span + 0.5f) - 0.01f;
                lines[i] = Prim.Shape(lineMesh, "Line", transform, Vector3.zero, Vector3.one, Mats.Vine, null, false).transform;
            }
            Drive(0f, 0f, 0.5f, 0f, 0f);
        }

        /// <summary>Called each frame while gliding.</summary>
        /// <param name="bank">Roll of the wing into the turn (deg).</param>
        /// <param name="pitch">Nose-up (negative) / nose-down (positive) pitch (deg).</param>
        /// <param name="loadFactor">Aerodynamic load: 0 slack, ~1 level flight, more in a climb or turn.</param>
        /// <param name="gustiness">0..1 turbulence.</param>
        public void Drive(float bank, float pitch, float loadFactor, float gustiness, float dt)
        {
            time += dt;
            load = Mathf.Lerp(load, loadFactor, 1f - Mathf.Exp(-6f * dt));
            gust = gustiness;
            canopy.localRotation = Quaternion.Euler(pitch, 0f, bank);

            for (int i = 0; i < leafBase.Length; i++)
            {
                var p = leafBase[i];
                p.y += Deform(p.x, p.z);
                leafNow[i] = p;
            }
            leaf.vertices = leafNow;
            leaf.RecalculateNormals();
            for (int i = 0; i < ribsBase.Length; i++)
            {
                var p = ribsBase[i];
                p.y += Deform(p.x, p.z);
                ribsNow[i] = p;
            }
            ribs.vertices = ribsNow;

            // Lines stay taut between the bar and their points on the (moving) leaf.
            for (int i = 0; i < lines.Length; i++)
            {
                var a = lineAttach[i];
                a.y += Deform(a.x, a.z);
                Vector3 top = canopy.localPosition + canopy.localRotation * a;
                Vector3 bottom = lineBar[i];
                Vector3 d = top - bottom;
                lines[i].localPosition = top;
                lines[i].localRotation = Quaternion.FromToRotation(Vector3.down, -d.normalized);
                lines[i].localScale = new Vector3(1f, d.magnitude, 1f);
            }
        }
    }
}
