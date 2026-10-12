using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// Shared PBR materials built from the templates in Resources/JungleEscape
    /// (the templates keep the right shader variants alive in mobile builds).
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        static readonly int BumpScale = Shader.PropertyToID("_BumpScale");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int Metallic = Shader.PropertyToID("_Metallic");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        // ---------------- Environment ----------------

        public static Material Trail => Lit("Trail", Tex.Trail, Color.white, 0.18f, bump: 1.2f);
        public static Material Ground => Lit("Ground", Tex.Ground, Color.white, 0.12f, tiling: new Vector2(1f, 1f));
        public static Material Bark => Lit("Bark", Tex.Bark, Color.white, 0.1f, tiling: new Vector2(2f, 1f), bump: 1.3f);
        public static Material PalmBark => Lit("PalmBark", Tex.PalmBark, Color.white, 0.12f, tiling: new Vector2(2f, 1f));
        public static Material Rock => Lit("Rock", Tex.Rock, Color.white, 0.22f, tiling: new Vector2(2f, 1f), bump: 1.2f);
        public static Material Canopy => Lit("Canopy", Tex.Leaves, Color.white, 0.3f, tiling: new Vector2(3f, 2f));
        public static Material CanopyDark => Lit("CanopyDark", Tex.Leaves, new Color(0.72f, 0.8f, 0.7f), 0.25f, tiling: new Vector2(3f, 2f));
        public static Material Frond => Cutout("Frond", Tex.Frond, Color.white, 0.35f);
        public static Material BroadLeaf => Cutout("BroadLeaf", Tex.BroadLeaf, Color.white, 0.5f);
        public static Material Grass => Cutout("Grass", Tex.Grass, Color.white, 0.2f);
        public static Material WoodEnd => Lit("WoodEnd", Tex.WoodRings, Color.white, 0.15f);
        public static Material Vine => Lit("Vine", Tex.Bark, new Color(0.55f, 0.7f, 0.45f), 0.2f, tiling: new Vector2(1f, 4f));
        public static Material Water => Lit("Water", Tex.Water, Color.white, 0.93f, tiling: new Vector2(4f, 4f), bump: 0.6f);
        public static Material Planks => Lit("Planks", Tex.Planks, Color.white, 0.2f);
        public static Material Rope => Lit("Rope", Tex.Fabric, new Color(0.62f, 0.52f, 0.36f), 0.1f, tiling: new Vector2(1f, 8f));
        public static Material Crystal => Emissive("Crystal", Tex.Metal, new Color(0.25f, 0.75f, 0.85f), 0.95f, new Color(0.25f, 1.4f, 1.6f));
        public static Material Flame => Emissive("Flame", Tex.Metal, new Color(1f, 0.6f, 0.2f), 0.1f, new Color(4f, 1.6f, 0.3f));
        public static Material CaveRock => Lit("CaveRock", Tex.CaveStone, Color.white, 0.38f, bump: 1.4f);

        // ---------------- Ice Age flora (own materials, so the world re-skin leaves them alone) ----------------

        public static Material PineNeedles => Lit("PineNeedles", Tex.Leaves, new Color(0.34f, 0.5f, 0.46f), 0.22f, tiling: new Vector2(5f, 3f), bump: 1.4f);
        public static Material Snow => Lit("Snow", Tex.Skin, new Color(0.95f, 0.97f, 1f), 0.5f, tiling: new Vector2(3f, 3f), bump: 0.6f);
        public static Material BirchBark => Lit("BirchBark", Tex.PalmBark, new Color(0.95f, 0.93f, 0.9f), 0.2f, tiling: new Vector2(1f, 2f));
        public static Material TundraGrass => Cutout("TundraGrass", Tex.Grass, new Color(1f, 0.85f, 0.62f), 0.2f);
        public static Material IceRock => Lit("IceRock", Tex.CaveStone, new Color(0.72f, 0.88f, 1f), 0.9f, tiling: new Vector2(1f, 1f), bump: 0.8f);
        public static Material IceCrystal => Emissive("IceCrystal", Tex.Metal, new Color(0.7f, 0.9f, 1f), 0.95f, new Color(0.15f, 0.35f, 0.5f));

        // ---------------- Animals ----------------

        public static Material SnakeSkin => Lit("SnakeSkin", Tex.SnakeSkin, Color.white, 0.55f, tiling: new Vector2(1f, 6f));
        public static Material TigerFur => Lit("TigerFur", Tex.TigerFur, Color.white, 0.2f);
        public static Material CrocSkin => Lit("CrocSkin", Tex.SnakeSkin, new Color(0.42f, 0.5f, 0.32f), 0.4f, tiling: new Vector2(2f, 6f), bump: 1.6f);

        /// <summary>Unlit, fog-free material for the distant mountain ranges.</summary>
        public static Material Backdrop
        {
            get
            {
                if (cache.TryGetValue("Backdrop", out var m) && m != null) return m;
                m = new Material(Template("JE_Backdrop")) { name = "Backdrop" };
                return cache["Backdrop"] = m;
            }
        }

        // ---------------- Pickups ----------------

        public static Material Banana => Emissive("Banana", Tex.Banana, Color.white, 0.6f, new Color(0.35f, 0.24f, 0.02f));
        public static Material Gold => Emissive("Gold", Tex.Metal, new Color(1f, 0.80f, 0.40f), 0.86f, new Color(0.06f, 0.035f, 0f), metallic: 1f);
        public static Material Gem => Emissive("Gem", Tex.Metal, new Color(0.42f, 0.12f, 0.55f), 0.92f, new Color(0.9f, 0.25f, 1.4f));
        public static Material Coconut => Lit("Coconut", Tex.Coconut, Color.white, 0.25f, bump: 1.5f);

        // ---------------- Generic tinted surfaces (characters) ----------------

        public static Material Fur(Color tint) => Lit("Fur" + Key(tint), Tex.Fur, tint, 0.18f, tiling: new Vector2(2f, 2f));
        public static Material Fabric(Color tint) => Lit("Fabric" + Key(tint), Tex.Fabric, tint, 0.12f, tiling: new Vector2(2f, 2f));
        public static Material Leather(Color tint) => Lit("Leather" + Key(tint), Tex.Leather, tint, 0.42f);
        public static Material Skin(Color tint) => Lit("Skin" + Key(tint), Tex.Skin, tint, 0.38f);
        public static Material Plain(Color tint, float smooth = 0.3f) => Lit("Plain" + Key(tint) + smooth, null, tint, smooth);

        // ---------------- Effects ----------------

        public static Material ParticleAdd(Texture tex) => Particle("Add" + tex.name, "JE_ParticleAdd", tex);
        public static Material ParticleAlpha(Texture tex) => Particle("Alpha" + tex.name, "JE_ParticleAlpha", tex);

        // ---------------- Builders ----------------

        public static Material Lit(string key, Surface? s, Color tint, float smooth, Vector2? tiling = null,
                                   float bump = 1f, float metallic = 0f)
            => Build(key, "JE_Lit", s, tint, smooth, tiling, bump, metallic, null);

        public static Material Cutout(string key, Surface s, Color tint, float smooth)
            => Build(key, "JE_LitCutout", s, tint, smooth, null, 1f, 0f, null);

        public static Material Emissive(string key, Surface s, Color tint, float smooth, Color emission, float metallic = 0f)
            => Build(key, "JE_LitEmissive", s, tint, smooth, null, 1f, metallic, emission);

        static Material Build(string key, string template, Surface? s, Color tint, float smooth, Vector2? tiling,
                              float bump, float metallic, Color? emission)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Template(template)) { name = key };
            if (s.HasValue)
            {
                m.SetTexture(BaseMap, s.Value.Albedo);
                m.SetTexture(BumpMap, s.Value.Normal);
                m.SetFloat(BumpScale, bump);
            }
            if (tiling.HasValue) m.SetTextureScale(BaseMap, tiling.Value);
            m.SetColor(BaseColor, tint);
            m.color = tint;
            m.SetFloat(Smoothness, smooth);
            m.SetFloat(Metallic, metallic);
            if (emission.HasValue)
            {
                m.SetColor(EmissionColor, emission.Value);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            m.enableInstancing = true;
            cache[key] = m;
            return m;
        }

        static Material Particle(string key, string template, Texture tex)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Template(template)) { name = key };
            m.SetTexture(BaseMap, tex);
            m.SetColor(BaseColor, Color.white);
            cache[key] = m;
            return m;
        }

        static readonly Dictionary<string, Material> templates = new Dictionary<string, Material>();

        public static Material Template(string name)
        {
            if (templates.TryGetValue(name, out var t) && t != null) return t;
            t = Resources.Load<Material>("JungleEscape/" + name);
            if (t == null)
            {
                // Fallback so the game still renders if the template assets are missing.
                Debug.LogWarning($"[JungleEscape] Material template '{name}' not found in Resources, using a default.");
                bool particle = name.StartsWith("JE_Particle");
                var shader = Shader.Find(particle ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Lit");
                t = new Material(shader);
            }
            templates[name] = t;
            return t;
        }

        static string Key(Color c) => ColorUtility.ToHtmlStringRGBA(c);
    }
}
