using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    public enum PickupType
    {
        Banana,   // currency + streak
        Magnet,   // Vine Magnet: pulls nearby bananas
        Glider,   // Leaf Glider: float above everything
        Shield,   // Coconut Helmet: survive one crash
        Bonanza   // Golden Idol: double bananas
    }

    public class Pickup : MonoBehaviour
    {
        const float MagnetRadius = 10f;
        const float CollectTime = 0.38f;

        public PickupType Type;

        Transform visual, halo, shadow;
        Collider col;
        float phase, wave, spin, tilt = 18f, wobble = 8f, spinRate = 140f;
        Color glow;
        Currency kind;
        // Magnet pull: once grabbed, a banana homes in with real velocity (pops up, then curves in and accelerates).
        bool pulled;
        Vector3 vel;
        // Collect flourish: flies up past the runner's shoulder, spinning and shrinking, then bursts.
        bool collected;
        float collectT;
        Vector3 collectFrom;

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;
            var gm = GameManager.I;

            if (collected) { CollectFlight(gm, dt); return; }

            if (Type != PickupType.Banana)
            {
                visual.Rotate(0f, 90f * dt, 0f, Space.World);
                visual.localPosition = new Vector3(0f, Mathf.Sin(t * 3f + phase) * 0.15f, 0f);
                return;
            }

            // Idle: a row of bananas bobs as a travelling wave and each one turns with a lazy wobble.
            spin += (pulled ? 900f : spinRate) * dt;
            float bob = 0.09f * Mathf.Sin(t * 2.6f - wave);
            if (!pulled) visual.localPosition = new Vector3(0f, bob, 0f);
            visual.localRotation = Quaternion.Euler(0f, spin, 0f) * Quaternion.Euler(wobble * Mathf.Sin(t * 1.7f + phase), 0f, tilt + wobble * 0.9f * Mathf.Sin(t * 2.1f + phase));
            if (halo != null) halo.localScale = Vector3.one * (0.9f + 0.06f * Mathf.Sin(t * 3.3f + phase));
            if (shadow != null)
            {
                // Contact shadow tightens and darkens as the banana dips toward the ground.
                float k = 1f - bob / 0.18f;
                shadow.localScale = new Vector3(0.62f * k, 1f, 0.36f * k);
            }

            if (gm == null || gm.State != GameState.Playing || gm.Player == null) return;
            Vector3 target = gm.Player.transform.position + Vector3.up * 0.9f;
            Vector3 to = target - transform.position;

            if (!pulled && gm.Player.Attracting && to.sqrMagnitude < MagnetRadius * MagnetRadius)
            {
                pulled = true;
                // Pop up and slightly outward before the pull takes over: reads as being yanked off its spot.
                vel = Vector3.up * 4.5f + Vector3.ProjectOnPlane(-to, Vector3.up).normalized * 1.5f;
                if (shadow != null) shadow.gameObject.SetActive(false);
            }
            if (!pulled) return;

            // Homing: steer toward the runner with strong acceleration; top speed always beats the runner's.
            float top = 34f + gm.Speed;
            Vector3 desired = to.normalized * top;
            vel = Vector3.MoveTowards(vel, desired, 140f * dt);
            transform.position += vel * dt;
            float d = to.magnitude;
            visual.localScale = Vector3.one * Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(d / 3f));
            if (d < 0.7f) Collect();
        }

        void CollectFlight(GameManager gm, float dt)
        {
            collectT += dt / CollectTime;
            float u = Mathf.Clamp01(collectT);
            float e = 1f - (1f - u) * (1f - u); // ease out
            // Follow the runner so the banana doesn't get left behind at full sprint.
            Vector3 anchor = gm != null && gm.Player != null ? gm.Player.transform.position : transform.position - collectFrom;
            Vector3 end = new Vector3(0f, 2.6f, 0f);
            Vector3 p = Vector3.Lerp(collectFrom, end, e) + Vector3.up * (1.2f * Mathf.Sin(u * Mathf.PI));
            transform.position = anchor + p;
            spin += 1100f * dt;
            visual.localRotation = Quaternion.Euler(0f, spin, 25f);
            float s = u < 0.25f ? Mathf.Lerp(1f, 1.35f, u / 0.25f) : Mathf.Lerp(1.35f, 0f, (u - 0.25f) / 0.75f);
            visual.localScale = Vector3.one * s;
            if (halo != null) halo.localScale = Vector3.one * (1f + u);
            if (u >= 1f)
            {
                Fx.Sparkle(visual.position, glow, 8);
                gameObject.SetActive(false);
            }
        }

        public void Collect()
        {
            if (!gameObject.activeSelf || collected) return;
            GameManager.I.OnPickup(this);
            if (Type != PickupType.Banana)
            {
                Fx.Sparkle(visual.position, glow, 24);
                gameObject.SetActive(false);
                return;
            }
            collected = true;
            if (col != null) col.enabled = false;
            if (shadow != null) shadow.gameObject.SetActive(false);
            var gm = GameManager.I;
            collectFrom = gm != null && gm.Player != null ? transform.position - gm.Player.transform.position : Vector3.zero;
            Fx.Sparkle(visual.position, glow, 4);
        }

        // ---------------- Factory ----------------

        public static Pickup Create(PickupType type, Vector3 pos, Transform parent, BiomeType world = BiomeType.Jungle)
        {
            var go = new GameObject(type.ToString());
            go.transform.SetParent(parent, false);
            TrackPath.Place(go.transform, pos.x, pos.y, pos.z); // pos is (sideways, height, distance along trail)

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = type == PickupType.Banana ? 0.6f : 0.8f;

            var p = go.AddComponent<Pickup>();
            p.Type = type;
            p.phase = Random.value * 6f;
            p.visual = Prim.Group("Visual", go.transform, Vector3.zero);
            var v = p.visual;
            BuildMeshes();

            switch (type)
            {
                case PickupType.Banana:
                    // The money is different in every world.
                    p.kind = CurrencyOf(world);
                    p.glow = CurrencyGlow(p.kind);
                    BuildCurrency(p, v);
                    break;

                case PickupType.Magnet:
                    // Vine Magnet: an amethyst seed wrapped in a living vine
                    Prim.Shape(ballMesh, "Gem", v, Vector3.zero, Vector3.one * 0.46f, Mats.Gem);
                    Prim.Shape(ringMesh, "VineRing", v, Vector3.zero, Vector3.one, Mats.Vine, new Vector3(70f, 0f, 0f));
                    Prim.Shape(ringMesh, "VineRing2", v, Vector3.zero, Vector3.one * 0.92f, Mats.Vine, new Vector3(-50f, 60f, 0f));
                    for (int i = 0; i < 3; i++)
                        Prim.Shape(smallLeaf, "Leaf", v, Quaternion.Euler(0f, i * 120f, 0f) * new Vector3(0.34f, 0.05f, 0f), Vector3.one,
                                   Mats.BroadLeaf, new Vector3(-20f, i * 120f + 90f, 0f), false);
                    break;

                case PickupType.Glider:
                    Prim.Shape(gliderLeaf, "Leaf", v, new Vector3(0f, 0f, -0.65f), Vector3.one, Mats.BroadLeaf, new Vector3(-8f, 0f, 15f));
                    break;

                case PickupType.Shield:
                    Prim.Shape(ballMesh, "Coconut", v, Vector3.zero, new Vector3(0.72f, 0.78f, 0.72f), Mats.Coconut);
                    var dark = Mats.Plain(new Color(0.12f, 0.08f, 0.05f), 0.5f);
                    Prim.Shape(ballMesh, "Eye1", v, new Vector3(-0.1f, 0.24f, -0.24f), Vector3.one * 0.09f, dark);
                    Prim.Shape(ballMesh, "Eye2", v, new Vector3(0.1f, 0.24f, -0.24f), Vector3.one * 0.09f, dark);
                    Prim.Shape(ballMesh, "Eye3", v, new Vector3(0f, 0.31f, -0.16f), Vector3.one * 0.08f, dark);
                    break;

                case PickupType.Bonanza:
                    // Golden Idol with emerald eyes
                    Prim.Shape(idolMesh, "Idol", v, new Vector3(0f, -0.5f, 0f), Vector3.one * 1.1f, Mats.Gold);
                    var emerald = Mats.Emissive("Emerald", Tex.Metal, new Color(0.08f, 0.45f, 0.22f), 0.95f, new Color(0.15f, 1.3f, 0.5f));
                    Prim.Shape(ballMesh, "EyeL", v, new Vector3(-0.06f, 0.33f, -0.19f), Vector3.one * 0.06f, emerald);
                    Prim.Shape(ballMesh, "EyeR", v, new Vector3(0.06f, 0.33f, -0.19f), Vector3.one * 0.06f, emerald);
                    break;
            }

            if (type != PickupType.Banana) p.glow = GlowColor(type);
            // Soft halo so collectibles read clearly against the foliage (does not spin).
            float hs = type == PickupType.Banana ? 0.9f : 1.9f;
            p.halo = Prim.Group("HaloPivot", go.transform, new Vector3(0f, 0f, 0.15f));
            Prim.Shape(haloMesh, "Halo", p.halo, new Vector3(0f, hs * 0.5f, 0f), Vector3.one * hs, HaloMat(p.glow, type == PickupType.Banana ? 0.24f : 0.4f), null, false);

            p.col = col;
            if (type == PickupType.Banana)
            {
                p.wave = pos.z * 0.75f;
                // Soft contact shadow on the trail below, so you can judge how high it floats.
                p.shadow = Prim.Shape(shadowMesh, "Shadow", go.transform, new Vector3(0f, -pos.y + 0.04f, 0f),
                                      new Vector3(0.62f, 1f, 0.36f), ShadowMat, null, false).transform;
            }
            return p;
        }

        // ---------------- World currencies ----------------

        public enum Currency { Banana, Acorn, GoldCoin, SoulOrb }

        public static Currency CurrencyOf(BiomeType world)
        {
            switch (world)
            {
                case BiomeType.IceAge: return Currency.Acorn;
                case BiomeType.WaterWorld: return Currency.GoldCoin;
                case BiomeType.HorrorJungle: return Currency.SoulOrb;
                default: return Currency.Banana;
            }
        }

        /// <summary>Plural name of a world's money, for the HUD.</summary>
        public static string CurrencyName(BiomeType world)
        {
            switch (CurrencyOf(world))
            {
                case Currency.Acorn: return "acorns";
                case Currency.GoldCoin: return "gold coins";
                case Currency.SoulOrb: return "soul orbs";
                default: return "bananas";
            }
        }

        static Color CurrencyGlow(Currency k)
        {
            switch (k)
            {
                case Currency.Acorn: return new Color(1f, 0.78f, 0.45f);
                case Currency.GoldCoin: return new Color(1f, 0.8f, 0.3f);
                case Currency.SoulOrb: return new Color(0.45f, 1f, 0.65f);
                default: return new Color(1f, 0.85f, 0.35f);
            }
        }

        static void BuildCurrency(Pickup p, Transform v)
        {
            switch (p.kind)
            {
                case Currency.Banana:
                    // Ridged, tapered banana with a green-brown stalk and a dark blossom-end nub (tilt comes from Update).
                    Prim.Shape(bananaMesh, "Banana", v, Vector3.zero, Vector3.one * 1.12f, Mats.Banana);
                    Prim.Shape(stalkMesh, "Stalk", v, Vector3.zero, Vector3.one * 1.12f, StalkMat, null, false);
                    Prim.Shape(ballMesh, "Nub", v, bananaTip * 1.12f, Vector3.one * 0.035f, NubMat, null, false);
                    Prim.Shape(ballMesh, "StalkEnd", v, stalkEnd * 1.12f, Vector3.one * 0.05f, NubMat, null, false);
                    break;

                case Currency.Acorn:
                {
                    // A plump, glossy acorn in a scaly cap with a little stalk, dusted with frost.
                    var shell = Mats.Lit("AcornShell", Tex.Leather, new Color(0.66f, 0.42f, 0.2f), 0.62f, tiling: new Vector2(1f, 2f), bump: 0.5f);
                    var cap = Mats.Lit("AcornCap", Tex.Bark, new Color(0.62f, 0.5f, 0.34f), 0.15f, tiling: new Vector2(3f, 1.5f), bump: 2.2f);
                    var dark = Mats.Plain(new Color(0.2f, 0.13f, 0.07f), 0.4f);
                    var o = new Vector3(0f, -0.22f, 0f);
                    Prim.Shape(acornNut, "Nut", v, o, Vector3.one, shell);
                    Prim.Shape(acornCap, "Cap", v, o + new Vector3(0f, 0.27f, 0f), Vector3.one, cap);
                    Prim.Shape(ballMesh, "Tip", v, o + new Vector3(0f, 0.005f, 0f), Vector3.one * 0.035f, dark, null, false);
                    Prim.Shape(acornStem, "Stem", v, o + new Vector3(0f, 0.48f, 0f), Vector3.one, dark, new Vector3(0f, 0f, 12f), false);
                    Prim.Shape(ballMesh, "Frost", v, o + new Vector3(0.02f, 0.415f, -0.01f), new Vector3(0.2f, 0.035f, 0.18f), Mats.Snow, null, false);
                    p.tilt = 14f; p.wobble = 7f; p.spinRate = 110f;
                    break;
                }

                case Currency.GoldCoin:
                {
                    // Pirate doubloon: raised rim and a cross stamped on both faces, standing on edge.
                    var gold = Mats.Gold;
                    Prim.Shape(coinMesh, "Coin", v, new Vector3(0f, 0f, -0.03f), Vector3.one, gold, new Vector3(90f, 0f, 0f));
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float z = side * 0.026f;
                        Prim.Shape(barMesh, "CrossV", v, new Vector3(0f, 0f, z), new Vector3(0.05f, 0.24f, 0.014f), gold, null, false);
                        Prim.Shape(barMesh, "CrossH", v, new Vector3(0f, 0.03f, z), new Vector3(0.18f, 0.05f, 0.014f), gold, null, false);
                        Prim.Shape(ballMesh, "Stud", v, new Vector3(0f, 0.03f, z), new Vector3(0.07f, 0.07f, 0.03f), gold, null, false);
                    }
                    p.tilt = 0f; p.wobble = 4f; p.spinRate = 220f;
                    break;
                }

                case Currency.SoulOrb:
                {
                    // A glowing spirit orb with a bright core and wisps circling it (they orbit as it spins).
                    var shell = Mats.Emissive("SoulShell", Tex.Metal, new Color(0.3f, 0.8f, 0.55f), 0.95f, new Color(0.25f, 1.1f, 0.6f));
                    var core = Mats.Emissive("SoulCore", Tex.Metal, new Color(0.9f, 1f, 0.95f), 0.95f, new Color(1.6f, 2.6f, 1.8f));
                    Prim.Shape(ballMesh, "Orb", v, Vector3.zero, Vector3.one * 0.34f, shell);
                    Prim.Shape(ballMesh, "Core", v, new Vector3(0f, 0.02f, -0.12f), Vector3.one * 0.14f, core, null, false);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * 120f * Mathf.Deg2Rad;
                        for (int k = 0; k < 3; k++)
                        {
                            float aa = a - k * 0.35f;
                            Prim.Shape(ballMesh, "Wisp", v, new Vector3(Mathf.Cos(aa) * 0.3f, 0.06f * Mathf.Sin(aa * 2f) + k * 0.04f, Mathf.Sin(aa) * 0.3f),
                                       Vector3.one * (0.075f - k * 0.018f), shell, null, false);
                        }
                    }
                    p.tilt = 0f; p.wobble = 10f; p.spinRate = 260f;
                    break;
                }
            }
        }

        // ---------------- Shared meshes & materials ----------------

        static Mesh acornNut, acornCap, acornStem, coinMesh, barMesh;
        static readonly Dictionary<Color, Material> haloByColor = new Dictionary<Color, Material>();
        static Mesh bananaMesh, stalkMesh, shadowMesh, ballMesh, ringMesh, smallLeaf, gliderLeaf, idolMesh, haloMesh;
        static Vector3 bananaTip, stalkEnd;
        static Material shadowMat;

        static Material StalkMat => Mats.Lit("BananaStalk", Tex.Bark, new Color(0.55f, 0.58f, 0.3f), 0.25f, tiling: new Vector2(1f, 1f));
        static Material NubMat => Mats.Plain(new Color(0.16f, 0.11f, 0.06f), 0.35f);

        static Material ShadowMat
        {
            get
            {
                if (shadowMat != null) return shadowMat;
                shadowMat = new Material(Mats.ParticleAlpha(Tex.Glow)) { name = "PickupShadow" };
                shadowMat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0.42f));
                return shadowMat;
            }
        }

        static void BuildMeshes()
        {
            if (bananaMesh != null) return;

            // Curved banana (V runs stalk -> tip for the texture): narrow neck at the stalk, plump middle,
            // blunt tapering tip, and five faint ridges along its length like the real fruit.
            const float r = 0.34f;
            var path = new List<Vector3>();
            for (int i = 0; i <= 20; i++)
            {
                float a = Mathf.Lerp(-55f, 52f, i / 20f) * Mathf.Deg2Rad;
                path.Add(new Vector3(r * Mathf.Sin(a), r * (1f - Mathf.Cos(a)) - 0.07f, 0f));
            }
            float length = r * 107f * Mathf.Deg2Rad;
            bananaMesh = Meshes.Sweep(path, (t, ang) =>
            {
                float neck = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.26f, t));
                float tip = 1f - 0.75f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t));
                float ridges = 1f + 0.07f * Mathf.Cos(ang * 5f) * neck;
                return Mathf.Lerp(0.024f, 0.084f, neck) * tip * ridges;
            }, 20, 1f, 1f / length, true);
            Vector3 tanEnd = (path[path.Count - 1] - path[path.Count - 2]).normalized;
            bananaTip = path[path.Count - 1] + tanEnd * 0.008f;

            // Short stalk continuing back from the neck, bending up slightly
            Vector3 p0 = path[0], tan0 = (path[1] - path[0]).normalized;
            var stalk = new List<Vector3> { p0 + tan0 * 0.01f, p0 - tan0 * 0.04f, p0 - tan0 * 0.08f + Vector3.up * 0.012f };
            stalkMesh = Meshes.Sweep(stalk, (t, ang) => Mathf.Lerp(0.024f, 0.021f, t), 8, 1f, 4f, true);
            stalkEnd = stalk[2];
            shadowMesh = Meshes.Disc(16);
            acornNut = Meshes.Lathe(new[]
            {
                new Vector2(0.001f, 0f), new Vector2(0.03f, 0.02f), new Vector2(0.09f, 0.08f), new Vector2(0.135f, 0.17f),
                new Vector2(0.15f, 0.26f), new Vector2(0.14f, 0.33f), new Vector2(0.001f, 0.35f)
            }, 18, 1f, "AcornNut");
            acornCap = Meshes.Lathe(new[]
            {
                new Vector2(0.135f, -0.01f), new Vector2(0.165f, 0.015f), new Vector2(0.172f, 0.06f), new Vector2(0.155f, 0.11f),
                new Vector2(0.1f, 0.145f), new Vector2(0.001f, 0.155f)
            }, 18, 3f, "AcornCap");
            acornStem = Meshes.Limb(0.07f, 0.018f, 0.014f, 6, 2);
            coinMesh = Meshes.Lathe(new[]
            {
                new Vector2(0.001f, 0f), new Vector2(0.2f, 0.004f), new Vector2(0.235f, 0f), new Vector2(0.27f, 0.006f), new Vector2(0.28f, 0.03f),
                new Vector2(0.27f, 0.054f), new Vector2(0.235f, 0.06f), new Vector2(0.2f, 0.056f), new Vector2(0.001f, 0.06f)
            }, 28, 1f, "Coin");
            barMesh = Meshes.RoundedBox(Vector3.one, 0.2f, 2);

            ballMesh = Meshes.Ball(12, 18);

            var ring = new List<Vector3>();
            for (int i = 0; i <= 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                ring.Add(new Vector3(Mathf.Cos(a) * 0.38f, Mathf.Sin(a * 3f) * 0.03f, Mathf.Sin(a) * 0.38f));
            }
            ringMesh = Meshes.Sweep(ring, (t, a) => 0.032f, 6, 1f, 2f);

            smallLeaf = Meshes.Leaf(0.28f, 0.2f, 0.05f, 0.02f);
            gliderLeaf = Meshes.Leaf(1.3f, 0.95f, 0.22f, 0.12f, 8);

            idolMesh = Meshes.Lathe(new[]
            {
                new Vector2(0.001f, 0f), new Vector2(0.26f, 0f), new Vector2(0.27f, 0.07f), new Vector2(0.2f, 0.1f),
                new Vector2(0.21f, 0.3f), new Vector2(0.25f, 0.45f), new Vector2(0.17f, 0.6f), new Vector2(0.11f, 0.64f),
                new Vector2(0.17f, 0.7f), new Vector2(0.2f, 0.8f), new Vector2(0.18f, 0.92f), new Vector2(0.1f, 0.99f),
                new Vector2(0.001f, 1.0f)
            }, 20, 1f, "Idol");

            haloMesh = Meshes.Billboard(1f, 1f);
        }

        static Material HaloMat(Color glow, float alpha)
        {
            glow.a = alpha;
            if (haloByColor.TryGetValue(glow, out var m) && m != null) return m;
            m = new Material(Mats.ParticleAdd(Tex.Glow)) { name = "Halo" };
            m.SetColor("_BaseColor", glow);
            return haloByColor[glow] = m;
        }

        static Color GlowColor(PickupType t)
        {
            switch (t)
            {
                case PickupType.Banana: return new Color(1f, 0.85f, 0.35f);
                case PickupType.Magnet: return new Color(0.8f, 0.4f, 1f);
                case PickupType.Glider: return new Color(0.55f, 1f, 0.45f);
                case PickupType.Shield: return new Color(1f, 0.7f, 0.4f);
                default: return new Color(1f, 0.82f, 0.3f);
            }
        }
    }
}
