using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace JungleEscape
{
    /// <summary>
    /// Skinned, motion-captured player character (e.g. from Mixamo). Loaded from
    /// Resources/Character/: "Character.fbx" (the rigged model) plus animation files named
    /// Run, Idle, Jump, Roll, Glide, Stumble and Fall. Clips are blended with the Playables API,
    /// so no Animator Controller asset is needed. Missing optional clips fall back gracefully.
    /// </summary>
    public class HumanoidCharacter
    {
        public enum Clip { Idle, Run, Jump, Roll, Glide, Stumble, Fall }
        static readonly string[] Files = { "Idle", "Run", "Jump", "Roll", "Glide", "Stumble", "Fall" };

        const float TargetHeight = 1.6f; // metres; the character is scaled to this height

        public Transform Root { get; private set; }

        Animator animator;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[Files.Length];
        readonly float[] lengths = new float[Files.Length];
        readonly bool[] has = new bool[Files.Length];
        readonly float[] weights = new float[Files.Length];
        Renderer[] renderers;

        /// <summary>Returns null when no character model has been added to the project.</summary>
        public static HumanoidCharacter TryCreate(Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Character/Character");
            if (prefab == null) return null;
            var runClip = LoadClip("Run");
            if (runClip == null)
            {
                Debug.LogWarning("[JungleEscape] Character model found but no Run animation in Resources/Character/. Using the built-in rig.");
                return null;
            }
            var c = new HumanoidCharacter();
            if (!c.Init(prefab, parent)) return null;
            return c;
        }

        static AnimationClip LoadClip(string file)
        {
            foreach (var clip in Resources.LoadAll<AnimationClip>("Character/" + file))
                if (!clip.name.StartsWith("__preview__")) return clip;
            return null;
        }

        bool Init(GameObject prefab, Transform parent)
        {
            var go = Object.Instantiate(prefab, parent, false);
            go.name = "Character";
            Root = go.transform;
            Root.localPosition = Vector3.zero;
            Root.localRotation = Quaternion.identity;

            animator = go.GetComponentInChildren<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            if (animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogWarning("[JungleEscape] Character.fbx must be imported with a Humanoid rig. Using the built-in rig.");
                Object.Destroy(go);
                return false;
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = null;

            // Scale to a consistent height and make sure it casts nice shadows.
            renderers = go.GetComponentsInChildren<Renderer>();
            var b = new Bounds(Root.position, Vector3.zero);
            foreach (var r in renderers)
            {
                b.Encapsulate(r.bounds);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            if (b.size.y > 0.01f) Root.localScale = Vector3.one * (TargetHeight / b.size.y);

            graph = PlayableGraph.Create("JungleEscape.Character");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Anim", animator);
            mixer = AnimationMixerPlayable.Create(graph, Files.Length);
            output.SetSourcePlayable(mixer);

            for (int i = 0; i < Files.Length; i++)
            {
                var clip = LoadClip(Files[i]);
                if (clip == null) continue;
                has[i] = true;
                lengths[i] = Mathf.Max(0.01f, clip.length);
                playables[i] = AnimationClipPlayable.Create(graph, clip);
                playables[i].SetApplyFootIK(true);
                graph.Connect(playables[i], 0, mixer, i);
            }
            graph.Play();
            return true;
        }

        public bool Has(Clip c) => has[(int)c];

        /// <summary>Target blend weights for this frame (they are smoothed and normalised here).</summary>
        public void Blend(float[] target, float dt, float sharpness = 12f)
        {
            float k = 1f - Mathf.Exp(-sharpness * dt);
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                float t = has[i] ? target[i] : 0f;
                weights[i] = Mathf.Lerp(weights[i], t, k);
                sum += weights[i];
            }
            if (sum < 0.0001f) { weights[(int)Clip.Run] = 1f; sum = 1f; }
            for (int i = 0; i < weights.Length; i++)
                if (has[i]) mixer.SetInputWeight(i, weights[i] / sum);
        }

        /// <summary>Lets a looping clip play on its own at the given speed.</summary>
        public void Play(Clip c, float speed)
        {
            if (!has[(int)c]) return;
            playables[(int)c].SetSpeed(speed);
        }

        /// <summary>Drives a clip to an exact point (0..1), e.g. to sync a jump with the physics.</summary>
        public void Scrub(Clip c, float normalized)
        {
            if (!has[(int)c]) return;
            var p = playables[(int)c];
            p.SetSpeed(0);
            p.SetTime(Mathf.Clamp01(normalized) * lengths[(int)c]);
        }

        /// <summary>Restarts a one-shot clip from the beginning.</summary>
        public void Restart(Clip c)
        {
            if (!has[(int)c]) return;
            playables[(int)c].SetTime(0);
            playables[(int)c].SetSpeed(1);
        }

        public void SnapTo(Clip c)
        {
            for (int i = 0; i < weights.Length; i++) weights[i] = i == (int)c && has[i] ? 1f : 0f;
            if (!has[(int)c]) weights[(int)Clip.Run] = 1f;
            for (int i = 0; i < weights.Length; i++) if (has[i]) mixer.SetInputWeight(i, weights[i]);
        }

        public void SetVisible(bool visible)
        {
            foreach (var r in renderers) if (r != null) r.enabled = visible;
        }

        public Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
