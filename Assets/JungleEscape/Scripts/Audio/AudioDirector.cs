using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// Plays the game's music, ambience and sound effects. All audio is synthesized (see <see cref="SoundBank"/>)
    /// on a background thread at start-up, so nothing hitches; clips become available as they finish.
    /// Music and ambience crossfade when the world changes, duck on game over and pause with the game.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector I { get; private set; }

        const float MusicVolume = 0.42f, AmbienceVolume = 0.5f, Fade = 2f;
        const string KeySound = "je_sound";

        readonly ConcurrentQueue<(string key, float[][] data)> ready = new ConcurrentQueue<(string, float[][])>();
        readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        readonly AudioSource[] music = new AudioSource[2], amb = new AudioSource[2];
        AudioSource wind;
        readonly List<AudioSource> pool2D = new List<AudioSource>(), pool3D = new List<AudioSource>();
        int next2D, next3D, activeMusic;
        BiomeType? playing;
        float chatterT;

        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt(KeySound, 1) == 1;
            set { PlayerPrefs.SetInt(KeySound, value ? 1 : 0); AudioListener.volume = value ? 1f : 0f; }
        }

        void Awake()
        {
            I = this;
            AudioListener.volume = SoundOn ? 1f : 0f;
            for (int i = 0; i < 2; i++)
            {
                music[i] = Source2D("Music", true);
                amb[i] = Source2D("Ambience", true);
            }
            wind = Source2D("GliderWind", true);
            for (int i = 0; i < 10; i++) { var s = Source2D("Sfx", false); s.ignoreListenerPause = true; pool2D.Add(s); }
            for (int i = 0; i < 6; i++)
            {
                var s = new GameObject("Sfx3D").AddComponent<AudioSource>();
                s.transform.SetParent(transform, false);
                s.playOnAwake = false;
                s.spatialBlend = 0.85f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 5f;
                s.maxDistance = 70f;
                s.dopplerLevel = 0f;
                pool3D.Add(s);
            }

            // Synthesize everything off the main thread: effects first, then the starting world, then the rest.
            var first = Biomes.StartWorld;
            Task.Run(() =>
            {
                try
                {
                    foreach (var kv in SoundBank.Effects()) ready.Enqueue((kv.Key, kv.Value));
                    Generate(first);
                    for (int w = 0; w < 4; w++) if ((BiomeType)w != first) Generate((BiomeType)w);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[Audio] Generating sounds failed: " + e);
                }
            });
        }

        void OnDisable()
        {
            // Never leave the engine's listener paused or silenced behind us (play mode without a domain reload).
            AudioListener.pause = false;
        }

        float listenerCheckT;

        /// <summary>Makes sure exactly one enabled AudioListener exists (on the main camera), otherwise nothing is heard.</summary>
        void EnsureListener()
        {
            if ((listenerCheckT -= Time.unscaledDeltaTime) > 0f) return;
            listenerCheckT = 1f;
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
            int enabled = 0;
            foreach (var l in listeners) if (l.enabled) enabled++;
            if (enabled > 0) return;
            var cam = Camera.main;
            if (cam == null) return;
            var al = cam.GetComponent<AudioListener>();
            if (al == null) al = cam.gameObject.AddComponent<AudioListener>();
            al.enabled = true;
            Debug.Log("[Audio] No active AudioListener found - added one to the main camera.");
        }

        void Generate(BiomeType w)
        {
            ready.Enqueue(("music_" + w, new[] { SoundBank.Music(w) }));
            ready.Enqueue(("amb_" + w, new[] { SoundBank.Ambience(w) }));
        }

        AudioSource Source2D(string name, bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        void Update()
        {
            if (music[0] == null) return; // scripts were recompiled during play: sources are gone until the next run
            EnsureListener();
            if (AudioListener.volume <= 0f && SoundOn) AudioListener.volume = 1f;
            UpdateAudio();
        }

        void UpdateAudio()
        {
            // Turn finished buffers into clips (a few per frame)
            for (int n = 0; n < 4 && ready.TryDequeue(out var item); n++)
            {
                var arr = new AudioClip[item.data.Length];
                for (int i = 0; i < arr.Length; i++)
                {
                    var c = AudioClip.Create(item.key + i, item.data[i].Length, 1, Synth.Rate, false);
                    c.SetData(item.data[i], 0);
                    arr[i] = c;
                }
                clips[item.key] = arr;
                if (item.key == "click") Debug.Log("[Audio] Sound effects ready.");
                if (item.key.StartsWith("music_")) Debug.Log("[Audio] Music ready: " + item.key.Substring(6));
            }

            var gm = GameManager.I;
            var state = gm != null ? gm.State : GameState.Menu;
            AudioListener.pause = state == GameState.Paused;

            // Music & ambience follow the current world, crossfading when it changes
            var world = Biomes.Current;
            if (playing != world && clips.ContainsKey("music_" + world) && clips.ContainsKey("amb_" + world))
            {
                activeMusic = 1 - activeMusic;
                Begin(music[activeMusic], clips["music_" + world][0]);
                Begin(amb[activeMusic], clips["amb_" + world][0]);
                playing = world;
            }
            float duck = state == GameState.GameOver ? 0.35f : state == GameState.Menu ? 0.75f : 1f;
            float step = Time.unscaledDeltaTime / Fade;
            for (int i = 0; i < 2; i++)
            {
                bool on = i == activeMusic && playing.HasValue;
                music[i].volume = Mathf.MoveTowards(music[i].volume, on ? MusicVolume * duck : 0f, step * MusicVolume);
                amb[i].volume = Mathf.MoveTowards(amb[i].volume, on ? AmbienceVolume : 0f, step * AmbienceVolume);
                if (!on && music[i].volume <= 0f && music[i].isPlaying) { music[i].Stop(); amb[i].Stop(); }
            }

            // Wind rushing past while gliding
            var pc = gm != null ? gm.Player : null;
            bool gliding = pc != null && pc.IsGliding && state == GameState.Playing;
            if (gliding && !wind.isPlaying && Has("wind_loop")) { wind.clip = clips["wind_loop"][0]; wind.Play(); }
            wind.volume = Mathf.MoveTowards(wind.volume, gliding ? 0.45f : 0f, Time.unscaledDeltaTime * 1.5f);
            if (gm != null) wind.pitch = 0.85f + 0.3f * Mathf.InverseLerp(GameManager.StartSpeed, GameManager.MaxSpeed, gm.Speed);
            if (!gliding && wind.volume <= 0f && wind.isPlaying) wind.Stop();

            // The monkeys chatter while they're close behind
            if (state == GameState.Playing && gm.Monkeys != null && gm.Monkeys.IsClose && (chatterT -= Time.deltaTime) <= 0f)
            {
                chatterT = Random.Range(1.2f, 2.4f);
                Play("monkey_chatter", 0.6f, Random.Range(0.9f, 1.15f));
            }
        }

        static void Begin(AudioSource s, AudioClip c)
        {
            s.clip = c;
            s.volume = 0f;
            s.time = 0f;
            s.Play();
        }

        bool Has(string key) => clips.ContainsKey(key);

        AudioClip Pick(string key)
        {
            if (!clips.TryGetValue(key, out var arr) || arr.Length == 0) return null;
            return arr[Random.Range(0, arr.Length)];
        }

        // ---------------- Public API ----------------

        /// <summary>Plays a sound effect (non-positional). Silently does nothing if it isn't ready yet.</summary>
        public static void Play(string key, float volume = 1f, float pitch = 1f)
        {
            if (I == null) return;
            var c = I.Pick(key);
            if (c == null) return;
            var s = I.pool2D[I.next2D];
            I.next2D = (I.next2D + 1) % I.pool2D.Count;
            s.pitch = pitch;
            s.PlayOneShot(c, volume);
        }

        /// <summary>Plays a sound effect from a point in the world (animals).</summary>
        public static void PlayAt(string key, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            if (I == null) return;
            var c = I.Pick(key);
            if (c == null) return;
            var s = I.pool3D[I.next3D];
            I.next3D = (I.next3D + 1) % I.pool3D.Count;
            s.transform.position = pos;
            s.pitch = pitch;
            s.PlayOneShot(c, volume);
        }

        /// <summary>Footstep sound for the current world's ground.</summary>
        public static void Footstep(float volume)
        {
            string key;
            switch (Biomes.Current)
            {
                case BiomeType.IceAge: key = "step_snow"; break;
                case BiomeType.WaterWorld: key = "step_wood"; break;   // the boardwalk
                case BiomeType.Desert: key = "step_sand"; break;
                default: key = "step_dirt"; break;
            }
            Play(key, volume, Random.Range(0.9f, 1.1f));
        }
    }
}
