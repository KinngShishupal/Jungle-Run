using System;
using UnityEngine;
#if JE_ADMOB
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
#endif

namespace JungleEscape
{
    /// <summary>
    /// Rewarded video ads (Google AdMob), used to buy an extra chance when the runner is caught.
    ///
    /// The real AdMob code compiles only when the Google Mobile Ads plugin is installed: the editor script
    /// AdMobSetup adds the JE_ADMOB define as soon as it detects the plugin. Without it, a short simulated
    /// "ad" stands in so the flow can still be tested.
    ///
    /// Ad unit IDs: the editor and Development builds use Google's TEST unit (safe to watch and click);
    /// release builds use the live unit from the AdMob console. Never click live ads on your own device.
    /// The live App ID is set in Assets &gt; Google Mobile Ads &gt; Settings.
    /// </summary>
    public static class AdsService
    {
#if UNITY_IOS
        const string TestRewardedUnitId = "ca-app-pub-3940256099942544/1712485313";   // TEST rewarded (iOS)
        const string LiveRewardedUnitId = TestRewardedUnitId;                         // no iOS app in AdMob yet
#else
        const string TestRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";   // TEST rewarded (Android)
        const string LiveRewardedUnitId = "ca-app-pub-8284285672679424/3869722465";   // Jungle Escape - Rewarded_ExtraChance
#endif
        static string RewardedUnitId => Debug.isDebugBuild ? TestRewardedUnitId : LiveRewardedUnitId;

        public static bool Showing { get; private set; }
        static bool initialized;

        /// <summary>True when a rewarded ad is loaded and can be shown right now.</summary>
        public static bool Ready
        {
            get
            {
#if JE_ADMOB
                return rewarded != null && rewarded.CanShowAd();
#else
                return true;
#endif
            }
        }

        /// <summary>
        /// Starts ads: first asks for consent where the law requires it (EEA, UK, Switzerland and some US states,
        /// via Google's User Messaging Platform; the form is set up in AdMob &gt; Privacy &amp; messaging), then
        /// starts the Mobile Ads SDK and loads a rewarded ad, but only once ads may be requested.
        /// </summary>
        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
#if JE_ADMOB
            MobileAds.RaiseAdEventsOnUnityMainThread = true;   // ad callbacks arrive on Unity's main thread
            GatherConsent();
#endif
        }

        /// <summary>
        /// True when the user's region requires a way to change their ad consent later: show a "Privacy" button
        /// that calls <see cref="ShowPrivacyOptions"/>.
        /// </summary>
        public static bool PrivacyOptionsRequired
        {
            get
            {
#if JE_ADMOB
                return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
#else
                return false;
#endif
            }
        }

        /// <summary>Re-opens the consent form so the user can change their choice.</summary>
        public static void ShowPrivacyOptions()
        {
#if JE_ADMOB
            ConsentForm.ShowPrivacyOptionsForm((FormError error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (error != null) Debug.LogWarning("[Ads] Privacy options form failed: " + error.Message);
                StartSdkIfAllowed();   // they may have just given consent
            }));
#endif
        }

#if JE_ADMOB
        static bool sdkStarted;

        static void GatherConsent()
        {
            var request = new ConsentRequestParameters
            {
                TagForUnderAgeOfConsent = false,   // the game is not aimed at children
            };

            ConsentInformation.Update(request, (FormError updateError) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (updateError != null)
                {
                    // Offline or a config problem: fall back to whatever was decided in an earlier session.
                    Debug.LogWarning("[Ads] Consent info update failed: " + updateError.Message);
                    StartSdkIfAllowed();
                    return;
                }
                ConsentForm.LoadAndShowConsentFormIfRequired((FormError formError) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (formError != null) Debug.LogWarning("[Ads] Consent form failed: " + formError.Message);
                    StartSdkIfAllowed();
                }));
            }));

            // Consent from an earlier session counts right away, so ads can load while the update is in flight.
            StartSdkIfAllowed();
        }

        static void StartSdkIfAllowed()
        {
            if (sdkStarted || !ConsentInformation.CanRequestAds()) return;
            sdkStarted = true;
            MobileAds.Initialize(status => Load());
        }
#endif

        /// <summary>
        /// Shows a rewarded ad. <paramref name="onRewarded"/> runs after the ad closes if the viewer earned the
        /// reward; <paramref name="onFailed"/> runs if it couldn't be shown or was closed early.
        /// </summary>
        public static void ShowRewarded(Action onRewarded, Action<string> onFailed)
        {
            if (Showing) return;
#if JE_ADMOB
            if (!sdkStarted) { onFailed?.Invoke("Ads aren't available right now."); StartSdkIfAllowed(); return; }
            if (!Ready) { onFailed?.Invoke("The ad isn't ready yet - try again in a moment."); Load(); return; }
            var ad = rewarded;
            rewarded = null;
            bool earned = false;
            Showing = true;
            AudioListener.pause = true;
            ad.OnAdFullScreenContentClosed += () =>
            {
                Showing = false;
                AudioListener.pause = false;
                ad.Destroy();
                Load();   // get the next one ready
                if (earned) onRewarded?.Invoke(); else onFailed?.Invoke("Watch the whole ad to earn it.");
            };
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                Showing = false;
                AudioListener.pause = false;
                ad.Destroy();
                Load();
                onFailed?.Invoke("The ad couldn't be shown - try again.");
            };
            ad.Show((Reward reward) => earned = true);
#else
            // No ad plugin yet: a stand-in "ad" that rewards after a short wait, so the flow can be tested.
            Showing = true;
            Simulator.Run(1.5f, () => { Showing = false; onRewarded?.Invoke(); });
#endif
        }

#if JE_ADMOB
        static RewardedAd rewarded;
        static bool loading;

        static void Load()
        {
            if (loading || rewarded != null) return;
            loading = true;
            RewardedAd.Load(RewardedUnitId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
            {
                loading = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[Ads] Rewarded ad failed to load: " + error);
                    return;
                }
                rewarded = ad;
            });
        }
#else
        /// <summary>Runs a callback after a delay (simulated ad), independent of Time.timeScale.</summary>
        class Simulator : MonoBehaviour
        {
            float t;
            Action done;

            public static void Run(float seconds, Action onDone)
            {
                var s = new GameObject("SimulatedAd").AddComponent<Simulator>();
                s.t = seconds;
                s.done = onDone;
            }

            void Update()
            {
                if ((t -= Time.unscaledDeltaTime) > 0f) return;
                done?.Invoke();
                Destroy(gameObject);
            }

            // Covers the whole screen, like a real full-screen ad
            void OnGUI()
            {
                GUI.depth = -1000;
                var full = new Rect(0f, 0f, Screen.width, Screen.height);
                GUI.DrawTexture(full, Texture2D.blackTexture);
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.height * 0.035f), wordWrap = true
                };
                style.normal.textColor = Color.white;
                GUI.Label(full, "Advertisement\n(simulated - install the AdMob plugin for real ads)", style);
            }
        }
#endif
    }
}
