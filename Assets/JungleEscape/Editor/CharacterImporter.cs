using UnityEditor;
using UnityEngine;

namespace JungleEscape.EditorTools
{
    /// <summary>
    /// Automatically configures character files dropped into
    /// Assets/JungleEscape/Resources/Character/ (e.g. downloaded from Mixamo):
    /// humanoid rig, looping run/idle/glide clips, no root drift.
    /// </summary>
    public class CharacterImporter : AssetPostprocessor
    {
        const string Folder = "/Resources/Character/";

        bool IsCharacterFile => assetPath.Replace('\\', '/').Contains(Folder) &&
                                assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        static string FileKey(string path) => System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

        void OnPreprocessModel()
        {
            if (!IsCharacterFile) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = FileKey(assetPath) != "character";
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        /// <summary>
        /// Jungle boy textures (Resources/CharacterModel/Textures/JB_*): transparency for the hair, brows,
        /// lashes and eyes, a linear (non-colour) metallic/smoothness map, and a sharp 2048 skin.
        /// </summary>
        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/CharacterModel/Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            string n = System.IO.Path.GetFileNameWithoutExtension(p);
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = true;
            ti.sRGBTexture = !n.Contains("MetallicSmoothness");
            bool alpha = n.Contains("Hair") || n.Contains("Brows") || n.Contains("Lashes") || n.Contains("Eye");
            ti.alphaSource = alpha || n.Contains("MetallicSmoothness") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = alpha;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.maxTextureSize = n.StartsWith("JB_Skin") ? 2048 : 1024;
            ti.textureCompression = n.StartsWith("JB_Skin") ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
            ti.anisoLevel = n.StartsWith("JB_Skin") ? 4 : 2;
        }

        void OnPreprocessAnimation()
        {
            if (!IsCharacterFile) return;
            var mi = (ModelImporter)assetImporter;
            string key = FileKey(assetPath);
            bool loop = key == "run" || key == "idle" || key == "glide";

            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.loopTime = loop;
                c.loopPose = loop;
                // Keep the body's facing and the run's natural bob in the pose; the game moves the
                // character itself, so horizontal travel in the clip is discarded.
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = key != "jump";   // the jump height comes from the game's physics
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = false;
            }
            mi.clipAnimations = clips;
        }
    }
}
