using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    public enum BiomeType { Jungle, IceAge, WaterWorld, HorrorJungle }

    /// <summary>Everything about a world's light and air: sky, sun, ambient, fog and colour grade.</summary>
    public class BiomeLook
    {
        public Color Zenith, Horizon, Haze, Sun, AmbSky, AmbEquator, AmbGround;
        public Color SplitShadows, SplitHighlights, ColorFilter, VignetteColor, MountainTint;
        public Vector3 SunEuler;
        public float SunIntensity, FogStart, FogEnd, Exposure, Contrast, Saturation, Temperature, Bloom, Vignette;

        public static BiomeLook Lerp(BiomeLook a, BiomeLook b, float t)
        {
            return new BiomeLook
            {
                Zenith = Color.Lerp(a.Zenith, b.Zenith, t), Horizon = Color.Lerp(a.Horizon, b.Horizon, t),
                Haze = Color.Lerp(a.Haze, b.Haze, t), Sun = Color.Lerp(a.Sun, b.Sun, t),
                AmbSky = Color.Lerp(a.AmbSky, b.AmbSky, t), AmbEquator = Color.Lerp(a.AmbEquator, b.AmbEquator, t),
                AmbGround = Color.Lerp(a.AmbGround, b.AmbGround, t),
                SplitShadows = Color.Lerp(a.SplitShadows, b.SplitShadows, t), SplitHighlights = Color.Lerp(a.SplitHighlights, b.SplitHighlights, t),
                ColorFilter = Color.Lerp(a.ColorFilter, b.ColorFilter, t), VignetteColor = Color.Lerp(a.VignetteColor, b.VignetteColor, t),
                MountainTint = Color.Lerp(a.MountainTint, b.MountainTint, t),
                SunEuler = new Vector3(Mathf.LerpAngle(a.SunEuler.x, b.SunEuler.x, t), Mathf.LerpAngle(a.SunEuler.y, b.SunEuler.y, t), 0f),
                SunIntensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, t),
                FogStart = Mathf.Lerp(a.FogStart, b.FogStart, t), FogEnd = Mathf.Lerp(a.FogEnd, b.FogEnd, t),
                Exposure = Mathf.Lerp(a.Exposure, b.Exposure, t), Contrast = Mathf.Lerp(a.Contrast, b.Contrast, t),
                Saturation = Mathf.Lerp(a.Saturation, b.Saturation, t), Temperature = Mathf.Lerp(a.Temperature, b.Temperature, t),
                Bloom = Mathf.Lerp(a.Bloom, b.Bloom, t), Vignette = Mathf.Lerp(a.Vignette, b.Vignette, t)
            };
        }
    }

    /// <summary>
    /// The four worlds reachable through portals. The same jungle content is re-skinned per world
    /// (recoloured textures, tinted fur, glowing lava and embers), and the atmosphere is blended.
    /// </summary>
    public static class Biomes
    {
        public static BiomeType Current { get; private set; } = BiomeType.Jungle;

        /// <summary>Water/lava/ice materials of every world (they scroll to look like flowing liquid).</summary>
        public static readonly List<Material> FlowingMaterials = new List<Material>();

        // ---------------- Presentation ----------------

        public static string Title(BiomeType b)
        {
            switch (b)
            {
                case BiomeType.IceAge: return "ICE AGE";
                case BiomeType.WaterWorld: return "WATER WORLD";
                case BiomeType.HorrorJungle: return "HORROR JUNGLE";
                default: return "THE JUNGLE";
            }
        }

        public static Color PortalColor(BiomeType b)
        {
            switch (b)
            {
                case BiomeType.IceAge: return new Color(0.45f, 0.85f, 1f);
                case BiomeType.WaterWorld: return new Color(0.2f, 0.78f, 1f);
                case BiomeType.HorrorJungle: return new Color(0.45f, 1f, 0.5f);
                default: return new Color(0.75f, 1f, 0.4f);
            }
        }

        public static BiomeType PickNext(BiomeType from)
        {
            if (from != BiomeType.Jungle && UnityEngine.Random.value < 0.4f) return BiomeType.Jungle;
            BiomeType b;
            do b = (BiomeType)UnityEngine.Random.Range(1, 4); while (b == from);
            return b;
        }

        // ---------------- Looks ----------------

        static BiomeLook jungle, ice, water, horror;

        public static BiomeLook Look(BiomeType b)
        {
            if (jungle == null) BuildLooks();
            switch (b)
            {
                case BiomeType.IceAge: return ice;
                case BiomeType.WaterWorld: return water;
                case BiomeType.HorrorJungle: return horror;
                default: return jungle;
            }
        }

        static void BuildLooks()
        {
            jungle = new BiomeLook
            {
                Zenith = Atmosphere.Zenith, Horizon = Atmosphere.Horizon, Haze = Atmosphere.Haze, Sun = Atmosphere.SunColor,
                SunIntensity = 2.2f, SunEuler = new Vector3(36f, -32f, 0f),
                AmbSky = new Color(0.64f, 0.72f, 0.76f), AmbEquator = new Color(0.52f, 0.56f, 0.45f), AmbGround = new Color(0.28f, 0.25f, 0.18f),
                FogStart = 22f, FogEnd = 120f, Exposure = 0.85f, Contrast = 12f, Saturation = 8f, Temperature = 6f,
                ColorFilter = new Color(1f, 0.985f, 0.95f), SplitShadows = new Color(0.42f, 0.52f, 0.56f), SplitHighlights = new Color(0.60f, 0.54f, 0.44f),
                Bloom = 0.55f, Vignette = 0.3f, VignetteColor = new Color(0.04f, 0.06f, 0.03f), MountainTint = Color.white
            };
            ice = new BiomeLook
            {
                Zenith = new Color(0.42f, 0.6f, 0.82f), Horizon = new Color(0.84f, 0.9f, 0.96f), Haze = new Color(0.8f, 0.86f, 0.93f),
                Sun = new Color(1f, 0.97f, 0.92f), SunIntensity = 2.3f, SunEuler = new Vector3(30f, -30f, 0f),
                AmbSky = new Color(0.74f, 0.82f, 0.92f), AmbEquator = new Color(0.66f, 0.72f, 0.82f), AmbGround = new Color(0.6f, 0.66f, 0.74f),
                FogStart = 18f, FogEnd = 110f, Exposure = 0.55f, Contrast = 10f, Saturation = -6f, Temperature = -14f,
                ColorFilter = new Color(0.95f, 0.98f, 1f), SplitShadows = new Color(0.40f, 0.50f, 0.64f), SplitHighlights = new Color(0.55f, 0.56f, 0.6f),
                Bloom = 0.75f, Vignette = 0.22f, VignetteColor = new Color(0.1f, 0.14f, 0.2f), MountainTint = new Color(1.25f, 1.35f, 1.5f)
            };
            // Bright tropical sea: high sun, clear blue sky, light sea haze over turquoise water.
            water = new BiomeLook
            {
                Zenith = new Color(0.18f, 0.48f, 0.86f), Horizon = new Color(0.72f, 0.88f, 0.96f), Haze = new Color(0.64f, 0.82f, 0.92f),
                Sun = new Color(1f, 0.96f, 0.88f), SunIntensity = 2.6f, SunEuler = new Vector3(48f, -30f, 0f),
                AmbSky = new Color(0.62f, 0.76f, 0.92f), AmbEquator = new Color(0.56f, 0.7f, 0.74f), AmbGround = new Color(0.48f, 0.56f, 0.56f),
                FogStart = 28f, FogEnd = 140f, Exposure = 0.7f, Contrast = 10f, Saturation = 14f, Temperature = 2f,
                ColorFilter = new Color(0.98f, 1f, 1f), SplitShadows = new Color(0.38f, 0.52f, 0.6f), SplitHighlights = new Color(0.58f, 0.55f, 0.48f),
                Bloom = 0.7f, Vignette = 0.22f, VignetteColor = new Color(0.02f, 0.06f, 0.1f), MountainTint = new Color(0.8f, 1f, 1.15f)
            };
            // Moonlit night in a glowing green-grey mist: dark and eerie, but everything still reads
            // as a silhouette against the fog. The moon is behind the runner so faces stay lit.
            horror = new BiomeLook
            {
                Zenith = new Color(0.07f, 0.1f, 0.15f), Horizon = new Color(0.3f, 0.38f, 0.4f), Haze = new Color(0.27f, 0.34f, 0.33f),
                Sun = new Color(0.7f, 0.82f, 1f), SunIntensity = 1.5f, SunEuler = new Vector3(40f, -30f, 0f),
                AmbSky = new Color(0.42f, 0.48f, 0.58f), AmbEquator = new Color(0.3f, 0.35f, 0.36f), AmbGround = new Color(0.17f, 0.18f, 0.17f),
                FogStart = 14f, FogEnd = 110f, Exposure = 1.0f, Contrast = 12f, Saturation = -12f, Temperature = -8f,
                ColorFilter = new Color(0.92f, 1f, 0.96f), SplitShadows = new Color(0.38f, 0.5f, 0.47f), SplitHighlights = new Color(0.53f, 0.56f, 0.6f),
                Bloom = 0.85f, Vignette = 0.3f, VignetteColor = new Color(0f, 0.03f, 0.02f), MountainTint = new Color(0.5f, 0.58f, 0.6f)
            };
        }

        // ---------------- Switching worlds ----------------

        static AtmosphereDriver driver;

        /// <summary>Called when the runner passes through a portal: blends the sky, light and air.</summary>
        public static void Enter(BiomeType b)
        {
            var from = Look(Current);
            Current = b;
            Driver().Blend(from, Look(b), 1.6f);
            Atmosphere.SetAirborne(b);
        }

        /// <summary>
        /// The world a new run starts in. Normally the jungle; developers can pick another one
        /// (menu buttons or keys 1-4 in the editor / development builds) to test a world directly.
        /// </summary>
        public static BiomeType StartWorld = BiomeType.Jungle;

        /// <summary>Instantly switches to a world (no blend), generating its textures right away.</summary>
        public static void ResetTo(BiomeType b)
        {
            var warm = Prewarm(b);
            while (warm.MoveNext()) { }
            Current = b;
            Driver().Snap(Look(b));
            Atmosphere.SetAirborne(b);
        }

        static AtmosphereDriver Driver()
        {
            if (driver == null) driver = new GameObject("BiomeAtmosphere").AddComponent<AtmosphereDriver>();
            return driver;
        }

        // ---------------- Re-skinning ----------------

        static readonly Dictionary<(Material, BiomeType), Material> remaps = new Dictionary<(Material, BiomeType), Material>();

        /// <summary>The version of a material for the given world (the material itself in the jungle).</summary>
        public static Material Remap(Material m, BiomeType b)
        {
            if (m == null || b == BiomeType.Jungle) return m;
            if (remaps.TryGetValue((m, b), out var v) && v != null) return v;
            v = MakeVariant(m, b) ?? m;
            remaps[(m, b)] = v;
            return v;
        }

        public static void RemapRenderers(GameObject go, BiomeType b)
        {
            if (b == BiomeType.Jungle) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                r.sharedMaterial = Remap(r.sharedMaterial, b);
            }
        }

        /// <summary>Generates a world's textures a few per frame so entering it causes no hitch.</summary>
        public static IEnumerator Prewarm(BiomeType b)
        {
            if (b == BiomeType.Jungle) yield break;
            foreach (var tex in TexturedFamilies.Keys)
            {
                string key = tex + "_" + b;
                if (!Tex.HasSurface(key))
                {
                    Tex.Variant(tex, b.ToString(), Recolorer(b, TexturedFamilies[tex]));
                    yield return null;
                }
            }
        }

        // Texture name -> family used to pick the recolour.
        static readonly Dictionary<string, string> TexturedFamilies = new Dictionary<string, string>
        {
            { "Trail", "ground" }, { "Ground", "ground" },
            { "Leaves", "leaf" }, { "Frond", "leaf" }, { "BroadLeaf", "leaf" }, { "Grass", "leaf" },
            { "Bark", "wood" }, { "PalmBark", "wood" }, { "WoodRings", "wood" }, { "Planks", "wood" },
            { "Rock", "rock" }, { "CaveStone", "rock" }, { "Water", "water" }, { "TigerFur", "tiger" }
        };

        // Material name -> texture it uses.
        static readonly Dictionary<string, string> MaterialTextures = new Dictionary<string, string>
        {
            { "Trail", "Trail" }, { "Ground", "Ground" }, { "Bark", "Bark" }, { "Vine", "Bark" }, { "PalmBark", "PalmBark" },
            { "Rock", "Rock" }, { "Canopy", "Leaves" }, { "CanopyDark", "Leaves" }, { "Frond", "Frond" }, { "BroadLeaf", "BroadLeaf" },
            { "Grass", "Grass" }, { "WoodEnd", "WoodRings" }, { "Planks", "Planks" }, { "CaveRock", "CaveStone" },
            { "Water", "Water" }, { "TigerFur", "TigerFur" }
        };

        static Material MakeVariant(Material m, BiomeType b)
        {
            string n = m.name;
            if (n == "Crystal") return Glow(m, b == BiomeType.WaterWorld ? new Color(0.3f, 1.6f, 1.2f)
                                             : b == BiomeType.HorrorJungle ? new Color(0.4f, 1.7f, 0.6f) : new Color(0.3f, 1.4f, 1.7f));
            if (n.StartsWith("Fur") || n.StartsWith("BearFur") || n == "BearMuzzle")
            {
                var v = new Material(m) { name = n + "_" + b };
                var c = m.GetColor("_BaseColor");
                v.SetColor("_BaseColor", FurTint(b, n, c));
                return v;
            }
            if (!MaterialTextures.TryGetValue(n, out var texName)) return null;

            string family = TexturedFamilies[texName];
            var surface = Tex.Variant(texName, b.ToString(), Recolorer(b, family));
            Color? emission = null;
            Texture emissionMap = null;
            float smooth = m.GetFloat("_Smoothness");

            switch (b)
            {
                case BiomeType.IceAge:
                    if (family == "ground") smooth = 0.45f;
                    if (family == "water") smooth = 0.97f;
                    break;
                case BiomeType.WaterWorld:
                    if (family == "ground") smooth = 0.3f;   // damp sand
                    if (family == "water") smooth = 0.96f;   // glassy sea
                    if (n == "Trail")
                    {
                        // The trail becomes a boardwalk: planks laid across the path.
                        surface = Tex.Planks;
                        smooth = 0.22f;
                    }
                    break;
                case BiomeType.HorrorJungle:
                    if (family == "ground") smooth = 0.35f;
                    if (family == "water") smooth = 0.95f;
                    break;
            }

            Material v2;
            if (emission.HasValue && !m.IsKeywordEnabled("_EMISSION"))
            {
                v2 = new Material(Mats.Template("JE_LitEmissive"));
                v2.SetTextureScale("_BaseMap", m.GetTextureScale("_BaseMap"));
                v2.SetFloat("_BumpScale", m.GetFloat("_BumpScale"));
                v2.SetColor("_BaseColor", m.GetColor("_BaseColor"));
                v2.SetFloat("_Metallic", m.GetFloat("_Metallic"));
            }
            else v2 = new Material(m);
            v2.name = n + "_" + b;
            v2.SetTexture("_BaseMap", surface.Albedo);
            v2.SetTexture("_BumpMap", surface.Normal);
            v2.SetFloat("_Smoothness", smooth);
            if (emission.HasValue)
            {
                v2.EnableKeyword("_EMISSION");
                v2.SetColor("_EmissionColor", emission.Value);
                v2.SetTexture("_EmissionMap", emissionMap != null ? emissionMap : Texture2D.whiteTexture);
                v2.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (family == "water") FlowingMaterials.Add(v2);
            return v2;
        }

        static Material Glow(Material m, Color emission)
        {
            var v = new Material(m);
            v.SetColor("_EmissionColor", emission);
            v.SetColor("_BaseColor", new Color(Mathf.Clamp01(emission.r * 0.3f), Mathf.Clamp01(emission.g * 0.3f), Mathf.Clamp01(emission.b * 0.3f)));
            return v;
        }

        static Color FurTint(BiomeType b, string name, Color c)
        {
            switch (b)
            {
                case BiomeType.IceAge:
                    // Polar bears and frosty, pale coats.
                    if (name.StartsWith("BearFur") || name == "BearMuzzle")
                        return name == "BearFurDark" ? new Color(0.8f, 0.77f, 0.7f) : new Color(0.93f, 0.9f, 0.84f);
                    return Color.Lerp(c, new Color(0.86f, 0.86f, 0.9f), 0.4f);
                case BiomeType.WaterWorld:
                    return c;
                case BiomeType.HorrorJungle:
                    if (name.StartsWith("BearFur") || name == "BearMuzzle") return new Color(0.07f, 0.06f, 0.06f);
                    float l = c.grayscale;
                    return Color.Lerp(c, new Color(l, l, l), 0.6f) * 0.5f;
                default:
                    return c;
            }
        }

        // ---------------- Recolours ----------------

        static Color Gray(float l) => new Color(l, l, l);

        static Func<Color, Color> Recolorer(BiomeType b, string family)
        {
            switch (b)
            {
                case BiomeType.IceAge: return c => Ice(family, c);
                case BiomeType.WaterWorld: return c => Water(family, c);
                case BiomeType.HorrorJungle: return c => Horror(family, c);
                default: return c => c;
            }
        }

        static Color Ice(string family, Color c)
        {
            float l = c.grayscale;
            var snow = Color.Lerp(new Color(0.58f, 0.66f, 0.77f), new Color(0.96f, 0.97f, 1f), Tex.SS(0.1f, 0.48f, l));
            switch (family)
            {
                case "ground": return snow;
                case "leaf": return Color.Lerp(c * new Color(0.85f, 0.95f, 1f), snow * (0.75f + 0.35f * l), 0.8f);
                case "wood": return Color.Lerp(c, Gray(l) * new Color(0.82f, 0.87f, 0.94f), 0.55f);
                case "rock":
                    float moss = Tex.SS(0f, 0.06f, c.g - c.r);
                    return Color.Lerp(Gray(l) * new Color(0.8f, 0.86f, 0.95f), snow, Mathf.Max(moss, 0.25f));
                case "water": return Color.Lerp(new Color(0.52f, 0.7f, 0.82f), new Color(0.8f, 0.9f, 0.97f), l * 2f);
                case "tiger": return l < 0.15f ? c : new Color(0.93f, 0.93f, 0.95f) * (0.85f + 0.2f * l); // white tiger
                default: return c;
            }
        }

        static Color Water(string family, Color c)
        {
            float l = c.grayscale;
            switch (family)
            {
                case "ground": return Color.Lerp(new Color(0.62f, 0.55f, 0.42f), new Color(0.92f, 0.86f, 0.72f), Tex.SS(0.1f, 0.5f, l)); // sand
                case "leaf": return Color.Lerp(c, c * new Color(1.15f, 1.35f, 1.05f), 0.7f);                                         // lush tropical green
                case "wood": return Color.Lerp(c, Gray(l) * new Color(0.95f, 0.9f, 0.82f) + Gray(0.1f), 0.5f);                    // sun-bleached driftwood
                case "rock": return Color.Lerp(new Color(0.72f, 0.56f, 0.5f), new Color(0.96f, 0.86f, 0.78f), Mathf.Clamp01(l * 1.6f)); // coral & limestone
                case "water": return Color.Lerp(new Color(0.04f, 0.4f, 0.47f), new Color(0.24f, 0.76f, 0.78f), Mathf.Clamp01(l * 2f)); // turquoise
                default: return c;
            }
        }

        static Color Horror(string family, Color c)
        {
            float l = c.grayscale;
            switch (family)
            {
                case "ground": return Color.Lerp(Gray(l), new Color(0.2f, 0.22f, 0.17f), 0.5f) * 0.85f;
                case "leaf": return Color.Lerp(new Color(0.16f, 0.2f, 0.15f), new Color(0.2f, 0.16f, 0.22f), 0.35f) * (0.7f + 1.5f * l);
                case "wood": return Gray(l) * 0.85f + new Color(0.04f, 0.04f, 0.03f);
                case "rock": return Gray(l) * new Color(0.7f, 0.78f, 0.74f);
                case "water": return new Color(0.02f, 0.04f, 0.035f) * (1f + l);
                case "tiger": return new Color(0.06f, 0.055f, 0.06f) * (0.8f + 0.6f * l); // black panther
                default: return c;
            }
        }
    }

    /// <summary>Blends the atmosphere from one world's look to another's.</summary>
    public class AtmosphereDriver : MonoBehaviour
    {
        BiomeLook from, to;
        float t = 1f, duration = 1f;

        public void Blend(BiomeLook a, BiomeLook b, float seconds)
        {
            from = a;
            to = b;
            duration = seconds;
            t = 0f;
        }

        public void Snap(BiomeLook look)
        {
            from = to = look;
            t = 1f;
            Atmosphere.ApplyLook(look);
            Atmosphere.Reflect(look.Zenith, look.Horizon, look.Haze, look.Sun);
        }

        void Update()
        {
            if (t >= 1f || to == null) return;
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duration);
            Atmosphere.ApplyLook(BiomeLook.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
            if (t >= 1f) Atmosphere.Reflect(to.Zenith, to.Horizon, to.Haze, to.Sun);
        }
    }
}
