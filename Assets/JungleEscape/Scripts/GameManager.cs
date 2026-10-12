using UnityEngine;

namespace JungleEscape
{
    public enum GameState { Menu, Playing, Paused, GameOver }

    /// <summary>
    /// Owns the run: state, speed ramp, score, bananas, banana streak,
    /// saving, game over and revive.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        // Speed (metres per second) ramps up from Start to Max over time.
        public const float StartSpeed = 11f;
        public const float MaxSpeed = 27f;
        public const float Acceleration = 0.11f;

        // Banana streak: keep collecting within this window to grow the multiplier.
        public const float StreakWindow = 2.5f;
        public const int StreakPerLevel = 15;
        public const int MaxMultiplier = 5;

        public const int BaseReviveCost = 50;

        public GameState State { get; private set; } = GameState.Menu;
        public float Speed { get; private set; }
        public float Distance { get; private set; }
        public float Score { get; private set; }
        public int RunBananas { get; private set; }
        public int Streak { get; private set; }
        public int HighScore { get; private set; }
        public int TotalBananas { get; private set; }
        public string GameOverReason { get; private set; } = "";
        public bool NewHighScore { get; private set; }
        public float RunTime => runTime;

        public PlayerController Player { get; set; }
        public TrackGenerator Track { get; set; }
        public MonkeyChaser Monkeys { get; set; }
        public CameraFollow Cam { get; set; }

        public int Multiplier => Mathf.Min(1 + Streak / StreakPerLevel, MaxMultiplier);
        public float StreakProgress => (Streak % StreakPerLevel) / (float)StreakPerLevel;
        public int ReviveCost => BaseReviveCost * (1 << revives); // 50, 100, 200...
        public bool CanRevive => TotalBananas >= ReviveCost;

        /// <summary>0 at start speed, 1 at max speed. Used for spawn difficulty.</summary>
        public float Difficulty => Mathf.InverseLerp(StartSpeed, MaxSpeed, Speed);

        float runTime;
        float lastBananaTime;
        int bankedThisRun;
        int revives;

        const string KeyBest = "je_best";
        const string KeyBananas = "je_bananas";

        void Awake()
        {
            I = this;
            HighScore = PlayerPrefs.GetInt(KeyBest, 0);
            TotalBananas = PlayerPrefs.GetInt(KeyBananas, 0);
        }

        void Update()
        {
            if (InputBridge.BackPressed())
            {
                if (State == GameState.Playing) Pause();
                else if (State == GameState.Paused) Resume();
            }

            // Developer shortcut (editor & development builds): keys 1-4 restart in a given world.
            if (Debug.isDebugBuild)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    BiomeType? pick = kb.digit1Key.wasPressedThisFrame ? BiomeType.Jungle
                                    : kb.digit2Key.wasPressedThisFrame ? BiomeType.IceAge
                                    : kb.digit3Key.wasPressedThisFrame ? BiomeType.WaterWorld
                                    : kb.digit4Key.wasPressedThisFrame ? BiomeType.Desert
                                    : (BiomeType?)null;
                    if (pick.HasValue) PlayWorld(pick.Value);
                }
            }

            if (State != GameState.Playing) return;

            float dt = Time.deltaTime;
            runTime += dt;
            Speed = Mathf.Min(MaxSpeed, StartSpeed + runTime * Acceleration);
            Distance += Speed * dt;
            Score += Speed * dt * Multiplier;

            if (Streak > 0 && Time.time - lastBananaTime > StreakWindow) Streak = 0;
        }

        // ---------------- Run flow ----------------

        public void StartRun()
        {
            Time.timeScale = 1f;
            runTime = 0f;
            Speed = StartSpeed;
            Distance = 0f;
            Score = 0f;
            RunBananas = 0;
            Streak = 0;
            bankedThisRun = 0;
            revives = 0;
            NewHighScore = false;
            GameOverReason = "";

            Player.ResetPlayer();
            Track.ResetTrack();
            Monkeys.BeginChase();
            State = GameState.Playing;
        }

        /// <summary>Starts a fresh run directly in a world (for testing a world on its own).</summary>
        public void PlayWorld(BiomeType world)
        {
            Biomes.StartWorld = world;
            StartRun();
        }

        /// <summary>Shows a world behind the menu without starting a run.</summary>
        public void PreviewWorld(BiomeType world)
        {
            Biomes.StartWorld = world;
            GoToMenu();
        }

        public void GoToMenu()
        {
            Time.timeScale = 1f;
            State = GameState.Menu;
            Speed = 0f;
            Player.ResetPlayer();
            Track.ResetTrack();
            Monkeys.Idle();
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            State = GameState.Paused;
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            Time.timeScale = 1f;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Pause();
        }

        // ---------------- Events from gameplay ----------------

        public void OnPickup(Pickup p)
        {
            if (State != GameState.Playing) return;

            if (p.Type == PickupType.Banana)
            {
                int amount = Player.HasPower(PickupType.Bonanza) ? 2 : 1;
                RunBananas += amount;
                Streak++;
                lastBananaTime = Time.time;
                Score += 10 * Multiplier;
            }
            else
            {
                Player.Activate(p.Type);
                Score += 50 * Multiplier;
            }
        }

        /// <summary>Took the path into another world at a fork.</summary>
        public void OnEnterWorld(BiomeType world)
        {
            if (State != GameState.Playing) return;
            Score += 500 * Multiplier;
            Cam.Shake(0.15f, 0.25f);
            GameUI.Announce(Biomes.Title(world), Biomes.PortalColor(world));
        }

        /// <summary>Side bump: first one wakes the monkeys, a second one soon after = caught.</summary>
        public void OnStumble()
        {
            if (State != GameState.Playing) return;
            Streak = 0;
            Cam.Shake(0.25f, 0.2f);
            Vibrate();

            if (Monkeys.IsClose)
            {
                Monkeys.Catch();
                EndRun("The monkeys caught you!");
            }
            else
            {
                Monkeys.Approach();
            }
        }

        public void OnCrash(Obstacle ob)
        {
            if (State != GameState.Playing) return;
            Cam.Shake(0.45f, 0.35f);
            Vibrate();
            Monkeys.Catch();
            EndRun(Creatures.CrashLine(ob.Creature) ?? CrashMessage(ob.Type));
        }

        static string CrashMessage(ObstacleType t)
        {
            switch (t)
            {
                case ObstacleType.Log: return "Tripped over a log!";
                case ObstacleType.Branch: return "Bonked by a branch!";
                case ObstacleType.Boulder: return "Smacked into a boulder!";
                case ObstacleType.Boar: return "Flattened by a wild boar!";
                case ObstacleType.Snake: return "Tangled up with a python!";
                case ObstacleType.Tiger: return "Pounced on by a tiger!";
                case ObstacleType.Bear: return "Ran into a bear hug!";
                default: return "Ouch!";
            }
        }

        void EndRun(string reason)
        {
            State = GameState.GameOver;
            GameOverReason = reason;
            Player.Knockout();

            // Bank only the bananas not banked yet (matters after a revive).
            TotalBananas += RunBananas - bankedThisRun;
            bankedThisRun = RunBananas;

            int s = Mathf.FloorToInt(Score);
            if (s > HighScore)
            {
                HighScore = s;
                NewHighScore = true;
            }
            Save();
        }

        public void Revive()
        {
            if (State != GameState.GameOver || !CanRevive) return;
            // Hook: you could also offer a rewarded ad here instead of bananas.
            TotalBananas -= ReviveCost;
            revives++;
            Save();

            Track.ClearAhead(Player.TrackS, 45f);
            Player.Revive();
            Monkeys.FallBack();
            State = GameState.Playing;
        }

        void Save()
        {
            PlayerPrefs.SetInt(KeyBest, HighScore);
            PlayerPrefs.SetInt(KeyBananas, TotalBananas);
            PlayerPrefs.Save();
        }

        static void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
