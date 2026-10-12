using System;
using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>An albedo map plus a matching tangent-space normal map.</summary>
    public struct Surface
    {
        public Texture2D Albedo;
        public Texture2D Normal;
    }

    /// <summary>
    /// Paints every texture the game uses from noise at startup (no imported art needed).
    /// Each surface is generated once, cached, and shared by all materials.
    /// </summary>
    public static class Tex
    {
        public const float TrailWidth = 10f;   // metres covered by the trail texture across U
        public const float TrailRepeat = 12f;  // metres covered along V before it tiles

        delegate void Painter(float u, float v, out Color albedo, out float height);

        static readonly Dictionary<string, Surface> surfaces = new Dictionary<string, Surface>();
        static readonly Dictionary<string, Texture2D> masks = new Dictionary<string, Texture2D>();

        // ---------------- Public library ----------------

        public static Surface Trail => Get("Trail", 512, 512, 3.5f, PaintTrail, wrapU: TextureWrapMode.Clamp);
        public static Surface Ground => Get("Ground", 512, 512, 6f, PaintGround);
        public static Surface Bark => Get("Bark", 256, 256, 9f, PaintBark);
        public static Surface PalmBark => Get("PalmBark", 256, 256, 8f, PaintPalmBark);
        public static Surface Rock => Get("Rock", 256, 256, 5f, PaintRock, wrapV: TextureWrapMode.Clamp);
        public static Surface Leaves => Get("Leaves", 256, 256, 3f, PaintLeaves);
        public static Surface Frond => Get("Frond", 256, 256, 3f, PaintFrond, cutout: true, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp);
        public static Surface BroadLeaf => Get("BroadLeaf", 256, 256, 4f, PaintBroadLeaf, cutout: true, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp);
        public static Surface Grass => Get("Grass", 256, 256, 2f, PaintGrass, cutout: true, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp);
        public static Surface Fur => Get("Fur", 256, 256, 5f, PaintFur);
        public static Surface Fabric => Get("Fabric", 128, 128, 3f, PaintFabric);
        public static Surface Leather => Get("Leather", 128, 128, 4f, PaintLeather);
        public static Surface Skin => Get("Skin", 64, 64, 1f, PaintSkin);
        public static Surface WoodRings => Get("WoodRings", 256, 256, 5f, PaintWoodRings, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp);
        public static Surface Banana => Get("Banana", 64, 256, 2f, PaintBanana, wrapV: TextureWrapMode.Clamp);
        public static Surface Coconut => Get("Coconut", 128, 128, 6f, PaintCoconut);
        public static Surface Metal => Get("Metal", 64, 64, 1.5f, PaintMetal);
        public static Surface Water => Get("Water", 256, 256, 2.5f, PaintWater);
        public static Surface SnakeSkin => Get("SnakeSkin", 256, 256, 4f, PaintSnakeSkin);
        public static Surface TigerFur => Get("TigerFur", 256, 256, 4f, PaintTigerFur, wrapV: TextureWrapMode.Clamp);
        public static Surface Planks => Get("Planks", 256, 256, 5f, PaintPlanks);
        public static Surface CaveStone => Get("CaveStone", 256, 256, 7f, PaintCaveStone);
        public static Surface Cactus => Get("Cactus", 128, 256, 6f, PaintCactus);
        public static Surface Sandstone => Get("Sandstone", 256, 256, 6f, PaintSandstone);
        public static Surface Sand => Get("Sand", 256, 256, 3f, PaintSand);

        /// <summary>Soft round sprite for glows, sparkles and pollen.</summary>
        public static Texture2D Glow => Mask("Glow", 64, 64, (u, v) =>
        {
            float r = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
            return Mathf.Pow(1f - r, 2.2f);
        });

        /// <summary>Long feathered strip for sun shafts (bright at the top, fading down).</summary>
        public static Texture2D Shaft => Mask("Shaft", 64, 128, (u, v) =>
        {
            float across = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 2f);
            return across * SS(0f, 0.35f, v) * SS(1f, 0.85f, v);
        });

        /// <summary>Wispy puff for dust kicked up by footsteps.</summary>
        public static Texture2D Puff => Mask("Puff", 64, 64, (u, v) =>
        {
            float r = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
            float n = Noise.Fbm(u, v, 4, 3, 991);
            return Mathf.Clamp01(Mathf.Pow(1f - r, 1.5f) * (0.5f + n));
        });

        // ---------------- Painters ----------------

        static readonly float[] LaneX = { -2.5f, 0f, 2.5f };

        static void PaintTrail(float u, float v, out Color c, out float h)
        {
            float x = (u - 0.5f) * TrailWidth;
            // Periods chosen so features are roughly square in metres (10 m across, 12 m along).
            float n = Noise.FbmXY(u, v, 10, 12, 5, 11);
            float big = Noise.FbmXY(u, v, 2, 2, 3, 23);
            float fine = Noise.FbmXY(u, v, 80, 96, 2, 37);

            float wear = 0f;
            foreach (float lx in LaneX) wear += Mathf.Exp(-Sq((x - lx) / 0.65f));
            wear = Mathf.Clamp01(wear);

            Color dirt = Color.Lerp(C(0.27f, 0.20f, 0.13f), C(0.46f, 0.35f, 0.24f), n);
            dirt = Color.Lerp(dirt, dirt * 0.7f, SS(0.55f, 0.75f, big));          // damp patches
            dirt *= 0.88f + fine * 0.24f;
            dirt = Color.Lerp(dirt, C(0.52f, 0.42f, 0.31f), wear * 0.3f);       // packed running lines

            Noise.Cells(u, v, 40, 51, out float f1, out _, out float id);
            float pebble = id > 0.88f ? SS(0.12f, 0.05f, f1) : 0f;
            pebble *= 1f - wear * 0.6f;
            Color stone = Color.Lerp(C(0.52f, 0.48f, 0.42f), C(0.68f, 0.63f, 0.56f), Frac(id * 5f));
            c = Color.Lerp(dirt, stone, pebble);

            Noise.Cells(u + 0.37f, v + 0.11f, 24, 77, out float g1, out _, out float lid);
            float leaf = 0f; // round litter read as dimples from the chase camera
            leaf *= 1f - wear * 0.8f;
            Color leafCol = Color.Lerp(C(0.42f, 0.26f, 0.11f), C(0.36f, 0.36f, 0.14f), Frac(lid * 7f));
            c = Color.Lerp(c, leafCol, leaf);

            // Forest floor creeping in at the edges, with a ragged border.
            float edge = Mathf.Abs(x) + (Noise.FbmXY(u, v, 6, 7, 3, 91) - 0.5f) * 1.8f;
            float grass = SS(3.6f, 4.7f, edge);
            Color floor = Color.Lerp(C(0.14f, 0.22f, 0.07f), C(0.27f, 0.34f, 0.12f), fine * 0.6f + n * 0.4f);
            c = Color.Lerp(c, floor, grass);

            h = n * 0.5f + pebble * 0.1f + leaf * 0.15f + fine * 0.12f - wear * 0.15f + grass * fine * 0.3f;
        }

        static void PaintGround(float u, float v, out Color c, out float h)
        {
            float n = Noise.Fbm(u, v, 6, 5, 101);
            float big = Noise.Fbm(u, v, 2, 3, 113);
            float fine = Noise.Fbm(u, v, 64, 2, 127);

            Color moss = Color.Lerp(C(0.11f, 0.19f, 0.06f), C(0.24f, 0.33f, 0.10f), n);
            Color litter = Color.Lerp(C(0.22f, 0.16f, 0.10f), C(0.37f, 0.27f, 0.15f), n);
            float lm = SS(0.42f, 0.62f, big);
            c = Color.Lerp(moss, litter, lm);
            c *= 0.85f + fine * 0.3f;

            Noise.Cells(u, v, 30, 131, out float f1, out float f2, out float id);
            float leaf = id > 0.55f ? SS(0.4f, 0.28f, f1) : 0f;
            Color leafCol = id > 0.85f ? C(0.48f, 0.30f, 0.12f)
                          : id > 0.7f ? C(0.33f, 0.22f, 0.12f)
                          : C(0.28f, 0.32f, 0.12f);
            c = Color.Lerp(c, leafCol * (0.8f + 0.4f * f1), leaf * (0.35f + lm * 0.65f));

            Noise.Cells(u, v, 70, 151, out float s1, out _, out float sid);
            float sprout = sid > 0.9f ? SS(0.25f, 0.1f, s1) : 0f;
            c = Color.Lerp(c, C(0.32f, 0.48f, 0.13f), sprout * (1f - lm));

            h = n * 0.4f + leaf * 0.3f * SS(0f, 0.1f, f2 - f1) + fine * 0.2f + sprout * 0.3f;
        }

        static void PaintBark(float u, float v, out Color c, out float h)
        {
            float n = Noise.FbmXY(u, v, 8, 2, 5, 201);
            float ridge = Mathf.Pow(1f - Mathf.Abs(2f * n - 1f), 1.4f);
            float fiss = SS(0.28f, 0.05f, ridge);
            float fine = Noise.FbmXY(u, v, 32, 16, 3, 211);

            c = Color.Lerp(C(0.28f, 0.23f, 0.18f), C(0.48f, 0.42f, 0.35f), ridge * 0.7f + fine * 0.3f);
            c = Color.Lerp(c, C(0.10f, 0.08f, 0.06f), fiss * 0.85f);

            float moss = SS(0.55f, 0.72f, Noise.Fbm(u, v, 3, 4, 221));
            c = Color.Lerp(c, Color.Lerp(C(0.16f, 0.25f, 0.07f), C(0.30f, 0.38f, 0.11f), fine), moss * 0.85f);

            Noise.Cells(u, v, 16, 231, out float f1, out _, out float id);
            float lichen = id > 0.86f ? SS(0.28f, 0.15f, f1) : 0f;
            c = Color.Lerp(c, C(0.58f, 0.60f, 0.50f), lichen * 0.7f);

            h = ridge * 0.8f + fine * 0.2f + moss * 0.1f + lichen * 0.1f;
        }

        static void PaintPalmBark(float u, float v, out Color c, out float h)
        {
            float t = v * 10f + Noise.Fbm(u, v, 4, 2, 305) * 0.4f;
            float ring = Frac(t);
            float band = SS(0f, 0.18f, ring) * SS(1f, 0.75f, ring);
            float fibers = Noise.FbmXY(u, v, 24, 4, 3, 301);
            c = Color.Lerp(C(0.33f, 0.28f, 0.22f), C(0.50f, 0.44f, 0.35f), band * 0.35f + fibers * 0.65f);
            c *= 0.85f + 0.3f * Noise.Fbm(u, v, 3, 3, 311);
            h = band * 0.7f + fibers * 0.3f;
        }

        static void PaintRock(float u, float v, out Color c, out float h)
        {
            float n = Noise.Fbm(u, v, 4, 5, 401);
            float fine = Noise.Fbm(u, v, 32, 3, 421);
            // Thin, irregular fractures that fade in and out (not a regular plate pattern).
            Noise.Cells(u, v, 9, 411, out float f1, out float f2, out _);
            float crackMask = SS(0.45f, 0.7f, Noise.Fbm(u, v, 6, 3, 415));
            float crack = SS(0.05f, 0f, f2 - f1) * crackMask * 0.35f;
            float strata = Noise.FbmXY(u, v, 2, 12, 3, 417);

            c = Color.Lerp(C(0.34f, 0.33f, 0.30f), C(0.60f, 0.58f, 0.53f), n * 0.55f + fine * 0.25f + strata * 0.2f);
            c = Color.Lerp(c, C(0.52f, 0.44f, 0.36f), SS(0.6f, 0.8f, Noise.Fbm(u, v, 3, 3, 419)) * 0.35f); // warm iron stains
            c = Color.Lerp(c, c * 0.55f, crack);

            Noise.Cells(u, v, 40, 441, out float l1, out _, out float lid);
            float lichen = lid > 0.9f ? SS(0.3f, 0.1f, l1) * SS(0.4f, 0.6f, n) : 0f;
            c = Color.Lerp(c, C(0.68f, 0.68f, 0.56f), lichen * 0.45f);

            // Moss gathers on the top of the rock (V = normalized height).
            float m = SS(0.55f, 0.8f, v + (Noise.Fbm(u, v, 5, 4, 431) - 0.5f) * 0.6f);
            Color mossCol = Color.Lerp(C(0.16f, 0.25f, 0.06f), C(0.33f, 0.42f, 0.12f), fine);
            c = Color.Lerp(c, mossCol, m);

            h = n * 0.6f + fine * 0.25f + m * 0.15f * fine;
        }

        static void PaintLeaves(float u, float v, out Color c, out float h)
        {
            // Dense small leaves with soft shading: reads as foliage, not scales.
            Noise.Cells(u, v, 48, 501, out float f1, out float f2, out float id);
            Noise.Cells(u + 0.31f, v + 0.17f, 19, 507, out float g1, out _, out float gid);
            float edge = SS(0f, 0.25f, f2 - f1);
            float clump = Noise.Fbm(u, v, 4, 4, 511);
            Color leaf = Color.Lerp(C(0.10f, 0.21f, 0.05f), C(0.22f, 0.36f, 0.09f), id * 0.5f + clump * 0.5f);
            leaf = Color.Lerp(leaf, C(0.18f, 0.30f, 0.08f) * (0.8f + 0.4f * gid), SS(0.3f, 0.1f, g1) * 0.5f);
            float shade = 0.85f + 0.15f * edge;
            c = leaf * shade * (0.75f + 0.5f * clump);
            h = edge * 0.3f + clump * 0.7f;
        }

        static void PaintFrond(float u, float v, out Color c, out float h)
        {
            float d = Mathf.Abs(u - 0.5f) * 2f;
            float env = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(v * 0.97f + 0.02f))), 0.6f) * 0.95f;
            float t = v * 26f - d * 3f;
            int idx = Mathf.FloorToInt(t);
            float f = t - idx;
            float lenVar = 0.85f + 0.15f * Noise.Hash01(idx, u > 0.5f ? 1 : 0, 61);
            env *= lenVar;

            float rachisW = 0.035f * (1f - v * 0.7f);
            float rachis = SS(rachisW, rachisW * 0.5f, d);

            float along = Mathf.Clamp01(d / Mathf.Max(env, 0.001f));
            float bladeW = 0.32f * Mathf.Sqrt(Mathf.Max(0f, 1f - along));
            float blade = SS(bladeW, bladeW - 0.08f, Mathf.Abs(f - 0.5f)) * SS(env, env - 0.03f, d);

            float alpha = Mathf.Max(rachis, blade);
            float tone = along * 0.6f + Noise.Hash01(idx, 3, 67) * 0.3f;
            c = Color.Lerp(C(0.09f, 0.22f, 0.05f), C(0.30f, 0.46f, 0.12f), tone);
            c = Color.Lerp(c, C(0.46f, 0.42f, 0.17f), SS(0.85f, 1f, along) * 0.35f);
            c = Color.Lerp(c, C(0.34f, 0.37f, 0.15f), rachis);
            c.a = alpha;
            h = bladeW > 0f ? blade * (1f - Mathf.Abs(f - 0.5f) / Mathf.Max(bladeW, 0.01f)) : 0f;
        }

        static void PaintBroadLeaf(float u, float v, out Color c, out float h)
        {
            float x = (u - 0.5f) * 2f;
            float ax = Mathf.Abs(x);
            float w = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(v), 0.8f))), 0.75f) * 0.92f;
            float alpha = SS(w, w - 0.035f, ax) * SS(0f, 0.02f, v);

            float midrib = SS(0.03f, 0f, ax);
            float lv = Frac(v * 7f - ax * 1.6f);
            float vein = SS(0.07f, 0f, Mathf.Min(lv, 1f - lv)) * SS(0.05f, 0.15f, ax);
            float mott = Noise.Fbm(u, v, 4, 4, 701);

            c = Color.Lerp(C(0.06f, 0.18f, 0.035f), C(0.17f, 0.36f, 0.07f), mott * 0.7f + (1f - ax) * 0.3f);
            c = Color.Lerp(c, C(0.30f, 0.46f, 0.15f), Mathf.Max(midrib, vein * 0.55f));
            c.a = alpha;
            h = midrib * 0.6f - vein * 0.5f + (1f - ax / Mathf.Max(w, 0.01f)) * 0.4f;
        }

        static void PaintGrass(float u, float v, out Color c, out float h)
        {
            c = C(0.2f, 0.3f, 0.1f);
            c.a = 0f;
            h = 0f;
            for (int i = 0; i < 18; i++)
            {
                float x0 = 0.12f + 0.76f * Noise.Hash01(i, 1, 811);
                float bh = 0.45f + 0.53f * Noise.Hash01(i, 2, 811);
                if (v > bh) continue;
                float lean = (Noise.Hash01(i, 3, 811) - 0.5f) * 0.35f;
                float w0 = 0.018f + 0.022f * Noise.Hash01(i, 4, 811);
                float t = v / bh;
                float center = x0 + lean * t * t;
                float width = w0 * (1f - t * 0.9f);
                float a = SS(width, width * 0.4f, Mathf.Abs(u - center));
                if (a <= c.a) continue;
                bool dry = Noise.Hash01(i, 5, 811) > 0.8f;
                Color tip = dry ? C(0.55f, 0.52f, 0.25f) : C(0.38f, 0.52f, 0.16f);
                Color col = Color.Lerp(C(0.07f, 0.15f, 0.04f), tip, t);
                col.a = a;
                c = col;
                h = 1f - Mathf.Abs(u - center) / Mathf.Max(width, 0.001f);
            }
        }

        static void PaintFur(float u, float v, out Color c, out float h)
        {
            float s = Noise.FbmXY(u, v, 32, 4, 4, 601);
            float s2 = Noise.FbmXY(u, v, 64, 8, 2, 611);
            float val = 0.55f + 0.45f * (s * 0.7f + s2 * 0.3f);
            c = new Color(val, val * 0.97f, val * 0.94f);
            h = s * 0.8f + s2 * 0.2f;
        }

        static void PaintFabric(float u, float v, out Color c, out float h)
        {
            float wu = Mathf.Sin(u * Mathf.PI * 2f * 48f), wv = Mathf.Sin(v * Mathf.PI * 2f * 48f);
            float weave = 0.5f + 0.5f * wu * wv;
            float mott = Noise.Fbm(u, v, 4, 4, 711);
            float val = 0.80f + 0.10f * weave + 0.16f * (mott - 0.5f);
            c = new Color(val, val, val);
            h = weave;
        }

        static void PaintLeather(float u, float v, out Color c, out float h)
        {
            Noise.Cells(u, v, 24, 721, out float f1, out float f2, out _);
            float grain = SS(0f, 0.12f, f2 - f1);
            float mott = Noise.Fbm(u, v, 3, 4, 731);
            float val = 0.70f + 0.18f * grain + 0.2f * (mott - 0.5f);
            c = new Color(val, val, val);
            h = grain;
        }

        static void PaintSkin(float u, float v, out Color c, out float h)
        {
            float n = Noise.Fbm(u, v, 4, 3, 741);
            float val = 0.92f + 0.08f * n;
            c = new Color(val, val * 0.98f, val * 0.97f);
            h = n;
        }

        static void PaintWoodRings(float u, float v, out Color c, out float h)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float warp = (Noise.Fbm(u, v, 4, 3, 801) - 0.5f) * 0.08f;
            float rings = Frac((r + warp) * 12f);
            float line = SS(0.14f, 0f, rings);

            c = Color.Lerp(C(0.78f, 0.63f, 0.43f), C(0.60f, 0.45f, 0.29f), r * 0.7f);
            c = Color.Lerp(c, C(0.43f, 0.29f, 0.17f), line * 0.65f);
            c = Color.Lerp(c, C(0.52f, 0.36f, 0.22f), SS(0.14f, 0f, r));

            float ang = Mathf.Atan2(dy, dx);
            float crack = SS(0.05f, 0f, Mathf.Abs(Mathf.Sin(ang * 2f + warp * 40f))) * SS(0.15f, 0.5f, r) * SS(0.85f, 0.6f, r);
            c = Color.Lerp(c, C(0.18f, 0.12f, 0.07f), crack * 0.8f);

            float rim = SS(0.84f, 0.92f, r);
            c = Color.Lerp(c, C(0.22f, 0.16f, 0.11f) * (0.8f + 0.4f * Noise.Fbm(u, v, 16, 2, 811)), rim);
            h = -line * 0.4f - crack * 0.6f + rim * 0.6f;
        }

        static void PaintBanana(float u, float v, out Color c, out float h)
        {
            float ridges = 0.5f + 0.5f * Mathf.Cos(u * Mathf.PI * 2f * 5f);
            // Fine fibres running the length of the peel
            float fibres = Noise.FbmXY(u, v, 24, 2, 3, 853);
            c = C(0.98f, 0.80f, 0.17f) * (0.88f + 0.08f * ridges + 0.1f * fibres);
            // Ridge edges catch a slightly greener, paler tone
            c = Color.Lerp(c, C(0.93f, 0.86f, 0.36f), Mathf.Pow(ridges, 6f) * 0.35f);
            c = Color.Lerp(c, C(0.60f, 0.66f, 0.20f), SS(0.2f, 0.05f, v));
            c = Color.Lerp(c, C(0.32f, 0.26f, 0.13f), SS(0.05f, 0.02f, v));
            c = Color.Lerp(c, C(0.20f, 0.14f, 0.08f), SS(0.93f, 0.98f, v));
            // A few ripe brown patches and many tiny sugar freckles
            Noise.Cells(u, v, 8, 851, out float f1, out _, out float id);
            float spot = id > 0.88f ? SS(0.18f, 0.08f, f1) : 0f;
            Noise.Cells(u, v, 28, 857, out float g1, out _, out float gid);
            float freckle = gid > 0.72f ? SS(0.12f, 0.04f, g1) : 0f;
            c = Color.Lerp(c, C(0.55f, 0.38f, 0.12f), spot * 0.5f);
            c = Color.Lerp(c, C(0.36f, 0.24f, 0.09f), freckle * 0.55f * SS(0.12f, 0.3f, v));
            h = ridges * 0.6f + fibres * 0.25f - freckle * 0.1f;
        }

        static void PaintCoconut(float u, float v, out Color c, out float h)
        {
            float s = Noise.FbmXY(u, v, 16, 2, 4, 861);
            float s2 = Noise.FbmXY(u, v, 48, 6, 2, 871);
            c = Color.Lerp(C(0.24f, 0.15f, 0.08f), C(0.50f, 0.36f, 0.22f), s * 0.75f + s2 * 0.25f);
            h = s * 0.7f + s2 * 0.3f;
        }

        static void PaintWater(float u, float v, out Color c, out float h)
        {
            // Ripples stretched across the flow, plus small chop.
            float r = Noise.FbmXY(u, v, 3, 8, 4, 901);
            float chop = Noise.Fbm(u, v, 16, 3, 911);
            c = Color.Lerp(C(0.05f, 0.12f, 0.11f), C(0.09f, 0.18f, 0.15f), r);
            h = r * 0.7f + chop * 0.3f;
        }

        static void PaintSnakeSkin(float u, float v, out Color c, out float h)
        {
            // Python: olive-tan base, dark saddle blotches with pale rims, fine scales.
            Noise.Cells(u, v, 6, 921, out float f1, out float f2, out float id);
            float blotch = SS(0.42f, 0.3f, f1);
            float rim = SS(0.5f, 0.42f, f1) * (1f - blotch);
            Noise.Cells(u, v, 48, 931, out float s1, out float s2, out _);
            float scale = SS(0f, 0.15f, s2 - s1);
            float n = Noise.Fbm(u, v, 4, 3, 941);
            c = Color.Lerp(C(0.50f, 0.44f, 0.27f), C(0.62f, 0.55f, 0.34f), n);
            c = Color.Lerp(c, C(0.72f, 0.64f, 0.42f), rim * 0.8f);
            c = Color.Lerp(c, C(0.20f, 0.14f, 0.08f) * (0.8f + 0.4f * id), blotch);
            c *= 0.8f + 0.2f * scale;
            // U wraps around the body with the belly at u = 0.75: cream ventral scutes there.
            float belly = SS(0.13f, 0.07f, Mathf.Abs(u - 0.75f));
            float scute = Mathf.Abs(Mathf.Sin(v * 12f * Mathf.PI));
            c = Color.Lerp(c, C(0.80f, 0.74f, 0.56f) * (0.82f + 0.18f * scute), belly);
            h = Mathf.Lerp(scale * 0.6f + blotch * 0.2f, scute * 0.5f, belly);
        }

        static void PaintTigerFur(float u, float v, out Color c, out float h)
        {
            // U wraps around the body, V runs belly (0) to back (1): vertical stripes, cream belly.
            float wob = (Noise.FbmXY(u, v, 6, 3, 3, 951) - 0.5f) * 0.12f;
            float t = Frac(u * 13f + wob * 6f + v * 0.4f);
            float width = 0.22f * SS(0.15f, 0.55f, v) * (0.7f + 0.6f * Noise.Fbm(u, v, 8, 2, 961));
            float stripe = SS(width, width * 0.5f, Mathf.Abs(t - 0.5f) * 2f * 0.5f);
            float hair = Noise.FbmXY(u, v, 64, 8, 2, 971);
            c = Color.Lerp(C(0.92f, 0.86f, 0.74f), C(0.86f, 0.46f, 0.13f), SS(0.18f, 0.4f, v));
            c = Color.Lerp(c, C(0.07f, 0.05f, 0.04f), stripe);
            c *= 0.85f + 0.3f * hair;
            h = hair;
        }

        static void PaintCaveStone(float u, float v, out Color c, out float h)
        {
            // Damp, layered cave rock: strata, cracks and mineral streaks (tiles in both directions).
            float n = Noise.Fbm(u, v, 4, 5, 1001);
            float strata = Noise.FbmXY(u, v, 2, 10, 3, 1011);
            Noise.Cells(u, v, 7, 1021, out float f1, out float f2, out _);
            float crack = SS(0.05f, 0f, f2 - f1) * SS(0.4f, 0.65f, Noise.Fbm(u, v, 5, 2, 1031));
            float wet = SS(0.55f, 0.8f, Noise.FbmXY(u, v, 8, 2, 3, 1041));
            c = Color.Lerp(C(0.24f, 0.22f, 0.20f), C(0.46f, 0.42f, 0.37f), n * 0.6f + strata * 0.4f);
            c = Color.Lerp(c, C(0.40f, 0.30f, 0.20f), SS(0.62f, 0.85f, Noise.Fbm(u, v, 3, 3, 1051)) * 0.4f);
            c = Color.Lerp(c, c * 0.45f, crack);
            c *= 1f - 0.25f * wet;
            h = n * 0.6f + strata * 0.3f - crack * 0.3f;
        }

        static void PaintCactus(float u, float v, out Color c, out float h)
        {
            // Twelve ribs around the stem (matching the ribbed geometry), with areoles - felted
            // spots carrying clusters of spines - running up each rib crest.
            float rib = 0.5f + 0.5f * Mathf.Cos(u * Mathf.PI * 2f * 12f);
            float crest = Mathf.Pow(rib, 6f);
            float streak = Noise.FbmXY(u, v, 12, 3, 3, 1101);
            c = Color.Lerp(C(0.16f, 0.27f, 0.14f), C(0.36f, 0.5f, 0.3f), rib * 0.75f + streak * 0.25f);
            // Waxy bloom on the crests, corky scarring low down
            c = Color.Lerp(c, C(0.5f, 0.6f, 0.44f), crest * 0.35f);
            c = Color.Lerp(c, C(0.5f, 0.44f, 0.32f), SS(0.62f, 0.85f, Noise.Fbm(u, v, 4, 3, 1111)) * 0.35f);
            float ru = Mathf.Repeat(u * 12f, 1f) - 0.0f;
            float dv = Mathf.Repeat(v * 16f, 1f);
            float du = Mathf.Min(ru, 1f - ru);
            float areole = SS(0.07f, 0.03f, Mathf.Sqrt(du * du + (dv - 0.5f) * (dv - 0.5f) * 0.6f));
            float spines = SS(0.11f, 0.06f, Mathf.Sqrt(du * du * 4f + (dv - 0.5f) * (dv - 0.5f) * 0.25f)) * (1f - areole);
            c = Color.Lerp(c, C(0.82f, 0.78f, 0.66f), areole);
            c = Color.Lerp(c, C(0.9f, 0.85f, 0.7f), spines * 0.6f);
            h = rib * 0.7f + areole * 0.4f + spines * 0.2f;
        }

        static void PaintSandstone(float u, float v, out Color c, out float h)
        {
            // Horizontal strata (v runs up the rock): red, orange and cream bands, cross-bedding and pitting.
            float warp = Noise.FbmXY(u, v, 3, 2, 3, 1201) * 0.08f;
            float band = Noise.FbmXY(0.5f, v + warp, 1, 12, 3, 1211);
            float fine = Mathf.Abs(Mathf.Sin((v + warp) * Mathf.PI * 48f));
            c = Color.Lerp(C(0.58f, 0.27f, 0.15f), C(0.86f, 0.55f, 0.32f), SS(0.3f, 0.6f, band));
            c = Color.Lerp(c, C(0.93f, 0.8f, 0.62f), SS(0.68f, 0.8f, band));
            c *= 0.9f + 0.1f * fine;
            float grain = Noise.Fbm(u, v, 16, 3, 1221);
            Noise.Cells(u, v, 10, 1231, out float f1, out _, out float id);
            float pit = id > 0.7f ? SS(0.16f, 0.04f, f1) : 0f;
            // Dark desert-varnish streaks running down from ledges
            float varnish = SS(0.6f, 0.85f, Noise.FbmXY(u, v, 10, 1, 3, 1241)) * SS(0.3f, 0.9f, v);
            c = Color.Lerp(c, C(0.3f, 0.17f, 0.1f), varnish * 0.45f);
            c = Color.Lerp(c, c * 0.6f, pit);
            c *= 0.92f + 0.16f * grain;
            h = band * 0.4f + fine * 0.2f + grain * 0.3f - pit * 0.4f;
        }

        static void PaintSand(float u, float v, out Color c, out float h)
        {
            // Wind ripples: parallel crests, slightly wavy, with darker heavy grains collecting in the troughs.
            float warp = Noise.Fbm(u, v, 3, 3, 1301) * 0.6f;
            float ripple = 0.5f + 0.5f * Mathf.Sin((u * 18f + warp * 3f + Noise.Fbm(u, v, 6, 2, 1311) * 0.6f) * Mathf.PI * 2f);
            float crest = Mathf.Pow(ripple, 3f);
            float grain = Noise.Fbm(u, v, 32, 2, 1321);
            c = Color.Lerp(C(0.74f, 0.56f, 0.36f), C(0.96f, 0.84f, 0.64f), 0.35f + 0.5f * ripple + 0.15f * grain);
            c = Color.Lerp(c, C(0.56f, 0.4f, 0.26f), SS(0.25f, 0.05f, ripple) * 0.35f);
            c = Color.Lerp(c, C(1f, 0.92f, 0.76f), crest * 0.2f);
            h = ripple * 0.75f + grain * 0.25f;
        }

        static void PaintPlanks(float u, float v, out Color c, out float h)
        {
            float grain = Noise.FbmXY(u, v, 2, 24, 4, 981);
            float lines = Mathf.Abs(Mathf.Sin((v * 30f + grain * 4f) * Mathf.PI));
            Noise.Cells(u, v, 5, 991, out float f1, out _, out float id);
            float knot = id > 0.75f ? SS(0.12f, 0.03f, f1) : 0f;
            c = Color.Lerp(C(0.36f, 0.25f, 0.15f), C(0.56f, 0.41f, 0.26f), grain * 0.7f + lines * 0.3f);
            c = Color.Lerp(c, C(0.22f, 0.14f, 0.08f), knot);
            c *= 0.8f + 0.2f * Noise.Fbm(u, v, 3, 2, 993);
            h = lines * 0.4f + grain * 0.4f - knot * 0.3f;
        }

        static void PaintMetal(float u, float v, out Color c, out float h)
        {
            float n = Noise.Fbm(u, v, 8, 3, 881);
            float val = 0.9f + 0.1f * n;
            c = new Color(val, val, val);
            h = n;
        }

        // ---------------- Building ----------------

        // ---------------- Biome variants ----------------

        struct Spec
        {
            public int W, H;
            public float Strength;
            public Painter Paint;
            public bool Cutout;
            public TextureWrapMode WrapU, WrapV;
        }

        static readonly Dictionary<string, Spec> specs = new Dictionary<string, Spec>();

        /// <summary>
        /// Same texture as <paramref name="baseName"/> but recoloured (e.g. snowy, charred, ghostly).
        /// The base texture must have been generated at least once.
        /// </summary>
        public static Surface Variant(string baseName, string suffix, Func<Color, Color> recolor)
        {
            string key = baseName + "_" + suffix;
            if (surfaces.TryGetValue(key, out var existing) && existing.Albedo != null) return existing;
            if (!specs.ContainsKey(baseName)) EnsureBase(baseName);
            if (!specs.TryGetValue(baseName, out var sp)) return surfaces.TryGetValue(baseName, out var b) ? b : default;
            void Paint(float u, float v, out Color c, out float h)
            {
                sp.Paint(u, v, out c, out h);
                float a = c.a;
                c = recolor(c);
                c.a = a;
            }
            return Get(key, sp.W, sp.H, sp.Strength / (sp.W / 256f), Paint, sp.Cutout, sp.WrapU, sp.WrapV);
        }

        public static bool HasSurface(string name) => surfaces.ContainsKey(name);

        /// <summary>Generates a base texture by name (so its recipe is known for variants).</summary>
        static void EnsureBase(string name)
        {
            Surface s;
            switch (name)
            {
                case "Trail": s = Trail; break;
                case "Ground": s = Ground; break;
                case "Bark": s = Bark; break;
                case "PalmBark": s = PalmBark; break;
                case "Rock": s = Rock; break;
                case "Leaves": s = Leaves; break;
                case "Frond": s = Frond; break;
                case "BroadLeaf": s = BroadLeaf; break;
                case "Grass": s = Grass; break;
                case "WoodRings": s = WoodRings; break;
                case "Planks": s = Planks; break;
                case "CaveStone": s = CaveStone; break;
                case "Water": s = Water; break;
                case "TigerFur": s = TigerFur; break;
                case "SnakeSkin": s = SnakeSkin; break;
                default: return;
            }
            if (s.Albedo == null) Debug.LogWarning("[JungleEscape] Could not generate base texture " + name);
        }

        static Surface Get(string name, int w, int h, float normalStrength, Painter painter, bool cutout = false,
                           TextureWrapMode wrapU = TextureWrapMode.Repeat, TextureWrapMode wrapV = TextureWrapMode.Repeat)
        {
            if (!specs.ContainsKey(name))
                specs[name] = new Spec { W = w, H = h, Strength = normalStrength * (w / 256f), Paint = painter, Cutout = cutout, WrapU = wrapU, WrapV = wrapV };
            if (surfaces.TryGetValue(name, out var s) && s.Albedo != null) return s;

            var cols = new Color32[w * h];
            var hts = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    painter((x + 0.5f) / w, (y + 0.5f) / h, out var col, out var ht);
                    if (!cutout) col.a = 1f;
                    int i = y * w + x;
                    cols[i] = col;
                    hts[i] = ht;
                }

            var albedo = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
            {
                name = name, wrapModeU = wrapU, wrapModeV = wrapV,
                filterMode = FilterMode.Trilinear, anisoLevel = 4
            };
            if (cutout) SetCoverageMips(albedo, cols, w, h, 0.45f);
            else albedo.SetPixels32(cols);
            albedo.Apply(!cutout, true);

            s = new Surface { Albedo = albedo, Normal = BuildNormal(name, hts, w, h, normalStrength * (w / 256f), wrapU, wrapV) };
            surfaces[name] = s;
            return s;
        }

        static Texture2D BuildNormal(string name, float[] hts, int w, int h, float strength,
                                     TextureWrapMode wrapU, TextureWrapMode wrapV)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float l = hts[y * w + Idx(x - 1, w, wrapU)], r = hts[y * w + Idx(x + 1, w, wrapU)];
                    float d = hts[Idx(y - 1, h, wrapV) * w + x], u = hts[Idx(y + 1, h, wrapV) * w + x];
                    var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    // RGB holds xyz and A = 1 so both the AG (desktop) and RGB (mobile) decoders read it correctly.
                    px[y * w + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f),
                                                (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true)
            {
                name = name + "_N", wrapModeU = wrapU, wrapModeV = wrapV,
                filterMode = FilterMode.Trilinear, anisoLevel = 4
            };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        static int Idx(int i, int n, TextureWrapMode mode) =>
            mode == TextureWrapMode.Repeat ? ((i % n) + n) % n : Mathf.Clamp(i, 0, n - 1);

        /// <summary>
        /// Builds mip levels for alpha-tested foliage, rescaling alpha on each level so leaves keep
        /// the same coverage in the distance instead of thinning out to nothing.
        /// </summary>
        static void SetCoverageMips(Texture2D tex, Color32[] level0, int w, int h, float cutoff)
        {
            tex.SetPixels32(level0, 0);
            float target = Coverage(level0, cutoff, 1f);
            var prev = level0;
            int pw = w, ph = h;
            for (int m = 1; m < tex.mipmapCount; m++)
            {
                int mw = Mathf.Max(1, pw / 2), mh = Mathf.Max(1, ph / 2);
                var cur = new Color32[mw * mh];
                for (int y = 0; y < mh; y++)
                    for (int x = 0; x < mw; x++)
                    {
                        int x0 = Mathf.Min(x * 2, pw - 1), x1 = Mathf.Min(x * 2 + 1, pw - 1);
                        int y0 = Mathf.Min(y * 2, ph - 1), y1 = Mathf.Min(y * 2 + 1, ph - 1);
                        Color32 a = prev[y0 * pw + x0], b = prev[y0 * pw + x1], c = prev[y1 * pw + x0], d = prev[y1 * pw + x1];
                        // Colour weighted by alpha so transparent texels don't darken leaf edges.
                        float wa = a.a + 1f, wb = b.a + 1f, wc = c.a + 1f, wd = d.a + 1f, ws = wa + wb + wc + wd;
                        cur[y * mw + x] = new Color32(
                            (byte)((a.r * wa + b.r * wb + c.r * wc + d.r * wd) / ws),
                            (byte)((a.g * wa + b.g * wb + c.g * wc + d.g * wd) / ws),
                            (byte)((a.b * wa + b.b * wb + c.b * wc + d.b * wd) / ws),
                            (byte)((a.a + b.a + c.a + d.a) / 4));
                    }

                // Binary search the alpha scale that restores the original coverage.
                float lo = 1f, hi = 4f;
                for (int it = 0; it < 10; it++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Coverage(cur, cutoff, mid) < target) lo = mid; else hi = mid;
                }
                var scaled = new Color32[cur.Length];
                for (int i = 0; i < cur.Length; i++)
                {
                    var p = cur[i];
                    p.a = (byte)Mathf.Min(255f, p.a * hi);
                    scaled[i] = p;
                }
                tex.SetPixels32(scaled, m);
                prev = cur;
                pw = mw; ph = mh;
            }
        }

        static float Coverage(Color32[] px, float cutoff, float scale)
        {
            int count = 0;
            float c = cutoff * 255f;
            for (int i = 0; i < px.Length; i++) if (px[i].a * scale > c) count++;
            return count / (float)px.Length;
        }

        static Texture2D Mask(string name, int w, int h, Func<float, float, float> alpha)
        {
            if (masks.TryGetValue(name, out var t) && t != null) return t;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha((x + 0.5f) / w, (y + 0.5f) / h)) * 255f));
            t = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear
            };
            t.SetPixels32(px);
            t.Apply(true, true);
            masks[name] = t;
            return t;
        }

        // ---------------- Math helpers ----------------

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);
        static float Sq(float x) => x * x;
        static float Frac(float x) => x - Mathf.Floor(x);

        /// <summary>GLSL-style smoothstep that also works with edge0 > edge1.</summary>
        public static float SS(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }
    }
}
