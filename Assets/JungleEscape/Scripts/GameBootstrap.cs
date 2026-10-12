using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// Builds the whole game from code: camera, lighting, atmosphere, player, monkeys,
    /// track and UI. Just press Play in any scene.
    /// Set AutoStart to false if you'd rather add this component to a scene yourself.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        static readonly bool AutoStart = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Auto()
        {
            if (!AutoStart) return;
            if (FindAnyObjectByType<GameBootstrap>() != null) return;
            new GameObject("JungleEscape").AddComponent<GameBootstrap>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Camera
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.nearClipPlane = 0.3f;

            // Sun, sky, haze, post-processing
            Atmosphere.Setup(cam);
            Prewarm();

            // Managers
            var gm = gameObject.AddComponent<GameManager>();
            var track = gameObject.AddComponent<TrackGenerator>();
            gameObject.AddComponent<GameUI>();
            gameObject.AddComponent<AudioDirector>();
            if (cam.GetComponent<AudioListener>() == null && Object.FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            // Player
            var player = new GameObject("Player").AddComponent<PlayerController>();

            // Monkeys
            var monkeys = new GameObject("MonkeyTroop").AddComponent<MonkeyChaser>();
            monkeys.Init(player.transform);

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.Target = player.transform;

            // Wire up
            gm.Player = player;
            gm.Track = track;
            gm.Monkeys = monkeys;
            gm.Cam = follow;
            track.Init(player);

            gm.GoToMenu();
        }

        /// <summary>
        /// Generates every texture and material up front so nothing is painted mid-run
        /// (which would cause a hitch the first time a power-up appears).
        /// </summary>
        static void Prewarm()
        {
            _ = new[]
            {
                Mats.Trail, Mats.Ground, Mats.Bark, Mats.PalmBark, Mats.Rock, Mats.Canopy, Mats.CanopyDark,
                Mats.Frond, Mats.BroadLeaf, Mats.Grass, Mats.WoodEnd, Mats.Vine,
                Mats.Banana, Mats.Gold, Mats.Gem, Mats.Coconut
            };
            Fx.Sparkle(new Vector3(0f, -100f, 0f), Color.clear, 0);
            Fx.Dust(new Vector3(0f, -100f, 0f), 0);
            Fx.Debris(new Vector3(0f, -100f, 0f), Color.clear, 0);
        }
    }
}
