using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace JungleEscape.EditorTools
{
    /// <summary>
    /// Wires up Google AdMob automatically:
    ///  - adds the JE_ADMOB scripting define while the Google Mobile Ads plugin is installed (and removes it if
    ///    the plugin is removed), so the real ad code in AdsService only compiles when it can;
    ///  - fills in Google's TEST App IDs in the plugin's settings if none are set yet, so the app doesn't crash
    ///    on start. Replace them with your real App IDs (Assets &gt; Google Mobile Ads &gt; Settings) before release.
    /// </summary>
    [InitializeOnLoad]
    static class AdMobSetup
    {
        const string Define = "JE_ADMOB";
        const string TestAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        const string TestIosAppId = "ca-app-pub-3940256099942544~1458002511";

        static AdMobSetup() => EditorApplication.delayCall += Run;

        static void Run()
        {
            bool installed = FindType("GoogleMobileAds.Api.MobileAds") != null;
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
                SetDefine(target, installed);
            if (installed) EnsureAppIds();
        }

        static void SetDefine(NamedBuildTarget target, bool on)
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);
            var list = new System.Collections.Generic.List<string>(defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            bool has = list.Contains(Define);
            if (on == has) return;
            if (on) list.Add(Define); else list.Remove(Define);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
            Debug.Log(on ? "[Ads] Google Mobile Ads found: real ads enabled (" + target.TargetName + ")."
                         : "[Ads] Google Mobile Ads not installed: using simulated ads (" + target.TargetName + ").");
        }

        /// <summary>Sets the plugin's App IDs to Google's test IDs if they are still empty.</summary>
        static void EnsureAppIds()
        {
            try
            {
                var settingsType = FindType("GoogleMobileAds.Editor.GoogleMobileAdsSettings");
                if (settingsType == null) return;
                const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                var load = settingsType.GetMethod("LoadInstance", any);
                var settings = load?.Invoke(null, null) as UnityEngine.Object;
                if (settings == null) return;
                bool changed = SetIfEmpty(settings, "GoogleMobileAdsAndroidAppId", TestAndroidAppId)
                             | SetIfEmpty(settings, "GoogleMobileAdsIOSAppId", TestIosAppId);
                if (changed)
                {
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[Ads] Set Google's TEST AdMob App IDs. Replace them with your own in Assets > Google Mobile Ads > Settings before release.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] Couldn't set the AdMob App IDs automatically. Open Assets > Google Mobile Ads > Settings and enter them. (" + e.Message + ")");
            }
        }

        static bool SetIfEmpty(object settings, string property, string value)
        {
            var p = settings.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p == null || !p.CanWrite) return false;
            if (!string.IsNullOrEmpty(p.GetValue(settings) as string)) return false;
            p.SetValue(settings, value);
            return true;
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
