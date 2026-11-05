using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace JungleEscape.EditorTools
{
    /// <summary>
    /// Sets the app icons from Assets/JungleEscape/Icons:
    ///  - import settings suited to icons (uncompressed, no mipmaps, alpha kept);
    ///  - the default icon and Android's adaptive (background + foreground), round and legacy icons
    ///    in Player Settings. Unity scales each texture down to every screen density itself.
    /// Runs automatically whenever the icon PNGs are (re)imported; also under Jungle Escape &gt; Apply App Icons.
    /// </summary>
    class AppIconSetup : AssetPostprocessor
    {
        const string Dir = "Assets/JungleEscape/Icons/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Dir)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 1024;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Any(p => p.StartsWith(Dir))) EditorApplication.delayCall += Apply;
        }

        [MenuItem("Jungle Escape/Apply App Icons")]
        static void Apply()
        {
            var legacy = Load("icon_legacy");
            var round = Load("icon_round");
            var bg = Load("icon_adaptive_background");
            var fg = Load("icon_adaptive_foreground");
            var def = Load("icon_default");
            if (legacy == null || round == null || bg == null || fg == null || def == null) return;

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { def }, IconKind.Any);

            var android = NamedBuildTarget.Android;
            Set(android, AndroidPlatformIconKind.Adaptive, bg, fg);
            Set(android, AndroidPlatformIconKind.Round, round);
            Set(android, AndroidPlatformIconKind.Legacy, legacy);
            AssetDatabase.SaveAssets();
            Debug.Log("[Icons] App icons applied (default, Android adaptive, round and legacy).");
        }

        static void Set(NamedBuildTarget target, PlatformIconKind kind, params Texture2D[] layers)
        {
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var icon in icons) icon.SetTextures(layers);
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }

        static Texture2D Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + name + ".png");
    }
}
