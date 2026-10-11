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

        Transform body, pelvis, spine, neck, thighL, thighR, kneeL, kneeR, ankleL, ankleR, shoulderL, shoulderR, elbowL, elbowR;
        Transform pack, scarf1, scarf2, shadow;
        Vector3 packBase;
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

        public static Pose Glide(float t)
        {
            float sway = Mathf.Sin(t * 2.2f);
            return new Pose
            {
                ThighL = new Vector3(-12f + 10f * sway, 0f, -4f), KneeL = new Vector3(25f + 10f * sway, 0f, 0f),
                ThighR = new Vector3(-12f - 10f * sway, 0f, 4f), KneeR = new Vector3(25f - 10f * sway, 0f, 0f),
                AnkleL = new Vector3(38f, 0f, 0f), AnkleR = new Vector3(32f, 0f, 0f),
                ShoulderL = new Vector3(-168f, 0f, -14f), ElbowL = new Vector3(-12f, 0f, 0f),
                ShoulderR = new Vector3(-168f, 0f, 14f), ElbowR = new Vector3(-12f, 0f, 0f),
                Spine = new Vector3(-4f, 0f, 0f), Neck = new Vector3(-12f, 0f, 0f),
                BodyY = 0.05f * Mathf.Sin(t * 3f)
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
                body.localRotation = baseRot;
                body.localPosition = offset;
            }
        }

        /// <summary>Backpack bounce and scarf flutter.</summary>
        public void Secondary(float time, float runPhase, float runWeight, float speedNorm)
        {
            pack.localPosition = packBase + new Vector3(0f, -0.022f * Mathf.Cos(runPhase * 2f) * runWeight, 0f);
            float trail = runWeight * (40f + 35f * speedNorm);
            scarf1.localRotation = Quaternion.Euler(12f + trail + 9f * Mathf.Sin(time * 17f), 0f, 6f * Mathf.Sin(time * 11f));
            scarf2.localRotation = Quaternion.Euler(10f + 0.35f * trail + 14f * Mathf.Sin(time * 21f + 1.3f), 0f, 0f);
        }

        /// <summary>Keeps the soft contact shadow on the ground under the feet.</summary>
        public void GroundShadow(float height)
        {
            shadow.localPosition = new Vector3(0f, -height + 0.02f, 0f);
            float s = Mathf.Lerp(1.0f, 0.55f, Mathf.Clamp01(height / 3f));
            shadow.localScale = new Vector3(s * 0.9f, 1f, s * 1.1f);
            var c = new Color(0f, 0f, 0f, Mathf.Lerp(0.55f, 0.12f, Mathf.Clamp01(height / 4f)));
            shadowMat.SetColor("_BaseColor", c);
        }

        // ---------------- Building ----------------

        public void Build(Transform parent)
        {
            var skin = Mats.Skin(new Color(0.84f, 0.62f, 0.46f));
            var shirt = Mats.Fabric(new Color(0.87f, 0.81f, 0.67f));
            var shirtDark = Mats.Fabric(new Color(0.76f, 0.70f, 0.56f));
            var trousers = Mats.Fabric(new Color(0.47f, 0.40f, 0.29f));
            var trouserDark = Mats.Fabric(new Color(0.40f, 0.34f, 0.24f));
            var leather = Mats.Leather(new Color(0.30f, 0.19f, 0.11f));
            var sole = Mats.Leather(new Color(0.10f, 0.08f, 0.07f));
            var felt = Mats.Fabric(new Color(0.46f, 0.33f, 0.20f));
            var band = Mats.Leather(new Color(0.13f, 0.09f, 0.07f));
            var scarf = Mats.Fabric(new Color(0.70f, 0.12f, 0.07f));
            var canvas = Mats.Fabric(new Color(0.37f, 0.39f, 0.24f));
            var bedroll = Mats.Fabric(new Color(0.44f, 0.19f, 0.11f));
            var hair = Mats.Fur(new Color(0.20f, 0.13f, 0.08f));
            var steel = Mats.Lit("Steel", Tex.Metal, new Color(0.55f, 0.56f, 0.55f), 0.7f, metallic: 0.9f);
            var eyeWhite = Mats.Plain(new Color(0.92f, 0.9f, 0.86f), 0.7f);
            var iris = Mats.Plain(new Color(0.12f, 0.08f, 0.05f), 0.95f);

            var ball = Meshes.Ball(12, 18);

            Root = Prim.Group("Model", parent, Vector3.zero);
            body = Prim.Group("Body", Root, Vector3.zero);
            pelvis = Prim.Group("Pelvis", body, new Vector3(0f, HipHeight, 0f));

            // ---- Hips & legs ----
            var hipsMesh = Meshes.Lathe(new[]
            {
                new Vector2(0.10f, -0.16f), new Vector2(0.155f, -0.12f), new Vector2(0.168f, -0.04f), new Vector2(0.163f, 0.06f)
            }, 18, 2f, "Hips");
            Prim.Shape(hipsMesh, "Hips", pelvis, Vector3.zero, new Vector3(1f, 1f, 0.72f), trousers);
            var belt = Meshes.Lathe(new[] { new Vector2(0.168f, 0f), new Vector2(0.174f, 0.025f), new Vector2(0.168f, 0.05f) }, 18);
            Prim.Shape(belt, "Belt", pelvis, new Vector3(0f, 0.01f, 0f), new Vector3(1f, 1f, 0.74f), leather);
            Prim.Shape(Meshes.RoundedBox(new Vector3(0.065f, 0.045f, 0.02f), 0.008f), "Buckle", pelvis, new Vector3(0f, 0.035f, 0.128f), Vector3.one, Mats.Gold);

            // Canteen on the right hip
            var canteen = Meshes.Lathe(new[]
            {
                new Vector2(0.001f, 0f), new Vector2(0.06f, 0.005f), new Vector2(0.075f, 0.05f), new Vector2(0.075f, 0.12f),
                new Vector2(0.05f, 0.16f), new Vector2(0.02f, 0.17f), new Vector2(0.02f, 0.19f), new Vector2(0.001f, 0.19f)
            }, 14);
            Prim.Shape(canteen, "Canteen", pelvis, new Vector3(0.19f, -0.17f, -0.03f), new Vector3(1f, 1f, 0.55f), steel, new Vector3(0f, 0f, -6f));
            Prim.Shape(Meshes.RoundedBox(new Vector3(0.11f, 0.09f, 0.06f), 0.02f), "Pouch", pelvis, new Vector3(-0.17f, -0.04f, 0.05f), Vector3.one, leather);

            var thigh = Meshes.Limb(0.42f, 0.088f, 0.066f, 12, 8);
            var shin = Meshes.Limb(0.40f, 0.066f, 0.05f, 12, 8);
            var bootShaft = Meshes.Limb(0.16f, 0.064f, 0.06f, 12, 5);
            var boot = Meshes.RoundedBox(new Vector3(0.12f, 0.1f, 0.27f), 0.045f);
            var soleMesh = Meshes.RoundedBox(new Vector3(0.125f, 0.03f, 0.28f), 0.012f);
            var cargo = Meshes.RoundedBox(new Vector3(0.05f, 0.13f, 0.1f), 0.02f);

            thighL = Prim.Group("ThighL", pelvis, new Vector3(-0.095f, -0.02f, 0f));
            thighR = Prim.Group("ThighR", pelvis, new Vector3(0.095f, -0.02f, 0f));
            kneeL = Prim.Group("KneeL", thighL, new Vector3(0f, -0.42f, 0f));
            kneeR = Prim.Group("KneeR", thighR, new Vector3(0f, -0.42f, 0f));
            ankleL = Prim.Group("AnkleL", kneeL, new Vector3(0f, -0.405f, 0f));
            ankleR = Prim.Group("AnkleR", kneeR, new Vector3(0f, -0.405f, 0f));
            for (int s = -1; s <= 1; s += 2)
            {
                var th = s < 0 ? thighL : thighR;
                var kn = s < 0 ? kneeL : kneeR;
                Prim.Shape(thigh, "Thigh", th, Vector3.zero, Vector3.one, trousers);
                Prim.Shape(cargo, "CargoPocket", th, new Vector3(s * 0.075f, -0.24f, 0f), Vector3.one, trouserDark);
                Prim.Shape(ball, "KneeCap", kn, Vector3.zero, new Vector3(0.13f, 0.12f, 0.13f), trousers);
                Prim.Shape(shin, "Shin", kn, Vector3.zero, Vector3.one, trousers);
                Prim.Shape(bootShaft, "BootShaft", kn, new Vector3(0f, -0.27f, 0f), Vector3.one, leather);
                var an = s < 0 ? ankleL : ankleR;
                Prim.Shape(boot, "Boot", an, new Vector3(0f, -0.025f, 0.045f), Vector3.one, leather);
                Prim.Shape(soleMesh, "Sole", an, new Vector3(0f, -0.07f, 0.045f), Vector3.one, sole);
            }

            // ---- Torso ----
            spine = Prim.Group("Spine", pelvis, new Vector3(0f, 0.05f, 0f));
            var torso = Meshes.Lathe(new[]
            {
                new Vector2(0.158f, -0.06f), new Vector2(0.165f, 0.04f), new Vector2(0.172f, 0.16f), new Vector2(0.198f, 0.3f),
                new Vector2(0.212f, 0.4f), new Vector2(0.195f, 0.47f), new Vector2(0.12f, 0.52f), new Vector2(0.055f, 0.54f),
                new Vector2(0.001f, 0.545f)
            }, 20, 2f, "Torso");
            Prim.Shape(torso, "Torso", spine, Vector3.zero, new Vector3(1f, 1f, 0.68f), shirt);
            // Chest pockets, placket and collar
            var pocket = Meshes.RoundedBox(new Vector3(0.1f, 0.1f, 0.025f), 0.015f);
            Prim.Shape(pocket, "PocketL", spine, new Vector3(-0.09f, 0.33f, 0.128f), Vector3.one, shirtDark, new Vector3(-10f, 0f, 0f));
            Prim.Shape(pocket, "PocketR", spine, new Vector3(0.09f, 0.33f, 0.128f), Vector3.one, shirtDark, new Vector3(-10f, 0f, 0f));
            Prim.Shape(Meshes.RoundedBox(new Vector3(0.03f, 0.42f, 0.015f), 0.006f), "Placket", spine, new Vector3(0f, 0.22f, 0.118f), Vector3.one, shirtDark, new Vector3(-6f, 0f, 0f));
            var collar = Meshes.RoundedBox(new Vector3(0.09f, 0.06f, 0.015f), 0.01f);
            Prim.Shape(collar, "CollarL", spine, new Vector3(-0.05f, 0.5f, 0.06f), Vector3.one, shirt, new Vector3(-55f, 25f, 20f));
            Prim.Shape(collar, "CollarR", spine, new Vector3(0.05f, 0.5f, 0.06f), Vector3.one, shirt, new Vector3(-55f, -25f, -20f));

            // Shoulder straps
            var strap = Meshes.RoundedBox(new Vector3(0.042f, 0.42f, 0.018f), 0.007f);
            Prim.Shape(strap, "StrapL", spine, new Vector3(-0.1f, 0.27f, 0.132f), Vector3.one, leather, new Vector3(-9f, 0f, 0f));
            Prim.Shape(strap, "StrapR", spine, new Vector3(0.1f, 0.27f, 0.132f), Vector3.one, leather, new Vector3(-9f, 0f, 0f));

            // Backpack (bounces with the stride)
            pack = Prim.Group("Backpack", spine, new Vector3(0f, 0.3f, -0.2f));
            packBase = pack.localPosition;
            Prim.Shape(Meshes.RoundedBox(new Vector3(0.33f, 0.4f, 0.18f), 0.06f), "Bag", pack, Vector3.zero, Vector3.one, canvas);
            Prim.Shape(Meshes.RoundedBox(new Vector3(0.34f, 0.13f, 0.2f), 0.04f), "Flap", pack, new Vector3(0f, 0.15f, 0f), Vector3.one, leather);
            Prim.Shape(Meshes.RoundedBox(new Vector3(0.2f, 0.14f, 0.07f), 0.03f), "Pocket", pack, new Vector3(0f, -0.08f, -0.11f), Vector3.one, canvas);
            var roll = new List<Vector3> { new Vector3(-0.21f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.21f, 0f, 0f) };
            Prim.Shape(Meshes.Sweep(roll, (t, a) => 0.072f, 14, 2f, 2f), "Bedroll", pack, new Vector3(0f, 0.27f, -0.01f), Vector3.one, bedroll);

            // ---- Neck, scarf & head ----
            neck = Prim.Group("Neck", spine, new Vector3(0f, 0.5f, 0f));
            Prim.Shape(Meshes.Limb(0.12f, 0.055f, 0.05f, 10, 5), "NeckMesh", neck, new Vector3(0f, 0.12f, 0f), Vector3.one, skin);
            var scarfRing = new List<Vector3>();
            for (int i = 0; i <= 18; i++)
            {
                float a = i / 18f * Mathf.PI * 2f;
                scarfRing.Add(new Vector3(Mathf.Cos(a) * 0.075f, 0f, Mathf.Sin(a) * 0.072f));
            }
            Prim.Shape(Meshes.Sweep(scarfRing, (t, a) => 0.032f, 8, 1f, 3f), "Scarf", neck, new Vector3(0f, 0.015f, 0f), Vector3.one, scarf);
            // Scarf tail streaming behind (two hinged segments)
            var tailSeg = Meshes.RoundedBox(new Vector3(0.07f, 0.16f, 0.015f), 0.007f);
            scarf1 = Prim.Group("ScarfTail1", neck, new Vector3(0.03f, 0.0f, -0.08f));
            Prim.Shape(tailSeg, "Seg", scarf1, new Vector3(0f, -0.08f, 0f), Vector3.one, scarf);
            scarf2 = Prim.Group("ScarfTail2", scarf1, new Vector3(0f, -0.16f, 0f));
            Prim.Shape(tailSeg, "Seg", scarf2, new Vector3(0f, -0.07f, 0f), new Vector3(0.85f, 0.9f, 1f), scarf);

            var head = Prim.Group("Head", neck, new Vector3(0f, 0.17f, 0.01f));
            Prim.Shape(ball, "Skull", head, Vector3.zero, new Vector3(0.2f, 0.24f, 0.225f), skin);
            Prim.Shape(ball, "Jaw", head, new Vector3(0f, -0.065f, 0.025f), new Vector3(0.17f, 0.13f, 0.17f), skin);
            Prim.Shape(ball, "Hair", head, new Vector3(0f, 0.035f, -0.022f), new Vector3(0.21f, 0.2f, 0.215f), hair);
            Prim.Shape(ball, "Nose", head, new Vector3(0f, -0.01f, 0.112f), new Vector3(0.035f, 0.055f, 0.045f), skin);
            Prim.Shape(ball, "EarL", head, new Vector3(-0.1f, 0f, -0.005f), new Vector3(0.035f, 0.065f, 0.035f), skin);
            Prim.Shape(ball, "EarR", head, new Vector3(0.1f, 0f, -0.005f), new Vector3(0.035f, 0.065f, 0.035f), skin);
            for (int s = -1; s <= 1; s += 2)
            {
                Prim.Shape(ball, "Eye", head, new Vector3(s * 0.04f, 0.022f, 0.093f), new Vector3(0.032f, 0.022f, 0.02f), eyeWhite, null, false);
                Prim.Shape(ball, "Iris", head, new Vector3(s * 0.04f, 0.022f, 0.101f), Vector3.one * 0.013f, iris, null, false);
                Prim.Shape(Meshes.RoundedBox(new Vector3(0.045f, 0.01f, 0.012f), 0.004f), "Brow", head,
                           new Vector3(s * 0.042f, 0.047f, 0.1f), Vector3.one, hair, new Vector3(0f, 0f, s * -8f), false);
            }

            // Fedora
            var hat = Meshes.Lathe(new[]
            {
                new Vector2(0.12f, 0.022f), new Vector2(0.27f, 0f), new Vector2(0.275f, 0.017f), new Vector2(0.165f, 0.03f),
                new Vector2(0.148f, 0.05f), new Vector2(0.142f, 0.125f), new Vector2(0.115f, 0.158f), new Vector2(0.04f, 0.143f),
                new Vector2(0.001f, 0.15f)
            }, 24, 2f, "Hat");
            Prim.Shape(hat, "Hat", head, new Vector3(0f, 0.068f, -0.005f), new Vector3(1f, 1f, 1.08f), felt, new Vector3(-5f, 0f, 0f));
            var hatBand = Meshes.Lathe(new[] { new Vector2(0.15f, 0f), new Vector2(0.148f, 0.03f) }, 24);
            Prim.Shape(hatBand, "HatBand", head, new Vector3(0f, 0.122f, -0.003f), new Vector3(1f, 1f, 1.08f), band, new Vector3(-5f, 0f, 0f));

            // ---- Arms ----
            var upperArm = Meshes.Limb(0.29f, 0.06f, 0.052f, 12, 7);
            var cuff = Meshes.Lathe(new[] { new Vector2(0.056f, 0f), new Vector2(0.062f, 0.025f), new Vector2(0.056f, 0.05f) }, 12);
            var forearm = Meshes.Limb(0.26f, 0.047f, 0.037f, 12, 7);
            var palm = Meshes.RoundedBox(new Vector3(0.075f, 0.09f, 0.035f), 0.017f);

            shoulderL = Prim.Group("ShoulderL", spine, new Vector3(-0.228f, 0.43f, -0.005f));
            shoulderR = Prim.Group("ShoulderR", spine, new Vector3(0.228f, 0.43f, -0.005f));
            elbowL = Prim.Group("ElbowL", shoulderL, new Vector3(0f, -0.29f, 0f));
            elbowR = Prim.Group("ElbowR", shoulderR, new Vector3(0f, -0.29f, 0f));
            for (int s = -1; s <= 1; s += 2)
            {
                var sh = s < 0 ? shoulderL : shoulderR;
                var el = s < 0 ? elbowL : elbowR;
                Prim.Shape(ball, "Deltoid", sh, new Vector3(0f, -0.02f, 0f), new Vector3(0.13f, 0.13f, 0.13f), shirt);
                Prim.Shape(upperArm, "Sleeve", sh, Vector3.zero, Vector3.one, shirt);
                Prim.Shape(cuff, "RolledCuff", sh, new Vector3(0f, -0.26f, 0f), Vector3.one, shirtDark);
                Prim.Shape(ball, "Elbow", el, Vector3.zero, Vector3.one * 0.085f, skin);
                Prim.Shape(forearm, "Forearm", el, Vector3.zero, Vector3.one, skin);
                Prim.Shape(palm, "Hand", el, new Vector3(0f, -0.3f, 0.005f), Vector3.one, skin);
                Prim.Shape(ball, "Thumb", el, new Vector3(s * -0.035f, -0.275f, 0.025f), new Vector3(0.025f, 0.05f, 0.025f), skin, new Vector3(20f, 0f, s * 25f));
            }

            // ---- Contact shadow ----
            shadowMat = new Material(Mats.ParticleAlpha(Tex.Glow)) { name = "ContactShadow" };
            shadow = Prim.Shape(Meshes.Disc(24), "ContactShadow", parent, new Vector3(0f, 0.02f, 0f), Vector3.one, shadowMat, null, false).transform;

            // ---- Power-up effects ----
            ShieldFx = Prim.Shape(ball, "CoconutHelmet", head, new Vector3(0f, 0.1f, 0f), new Vector3(0.36f, 0.26f, 0.38f), Mats.Coconut);

            GliderFx = Prim.Group("LeafGlider", body, new Vector3(0f, 2.35f, 0f)).gameObject;
            Prim.Shape(Meshes.Leaf(2.4f, 1.8f, 0.35f, 0.22f, 10), "Leaf", GliderFx.transform, new Vector3(0f, 0f, -1.2f), Vector3.one, Mats.BroadLeaf);
            var stem = new List<Vector3> { new Vector3(0f, 0f, -1.25f), new Vector3(0f, 0.05f, -1.5f), new Vector3(0f, 0f, -1.7f) };
            Prim.Shape(Meshes.Sweep(stem, (t, a) => 0.03f, 6, 1f, 1f), "Stem", GliderFx.transform, Vector3.zero, Vector3.one, Mats.Vine);

            MagnetFx = Prim.Group("VineMagnet", body, new Vector3(0f, 1.1f, 0f));
            Prim.Shape(ball, "Orb", MagnetFx, new Vector3(0.75f, 0f, 0f), Vector3.one * 0.22f, Mats.Gem, null, false);
            Prim.Shape(ball, "Orb", MagnetFx, new Vector3(-0.75f, 0f, 0f), Vector3.one * 0.22f, Mats.Gem, null, false);

            var crown = Prim.Group("GoldenCrown", head, new Vector3(0f, 0.22f, 0f));
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
}
