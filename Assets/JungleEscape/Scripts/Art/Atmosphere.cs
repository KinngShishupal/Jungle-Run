using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JungleEscape
{
    /// <summary>
    /// The look of the game: late-afternoon sun through humid jungle air.
    /// Sets up sun, sky, ambient light, haze, reflections, post-processing,
    /// camera quality and floating pollen. Tweak the palette here.
    /// </summary>
    public static class Atmosphere
    {
        // Palette (sRGB)
        public static readonly Color Zenith = new Color(0.36f, 0.55f, 0.68f);
        public static readonly Color Horizon = new Color(0.74f, 0.78f, 0.64f);
        public static readonly Color Haze = new Color(0.66f, 0.71f, 0.57f);
        public static readonly Color SunColor = new Color(1f, 0.86f, 0.66f);
        static readonly Color AmbientSky = new Color(0.64f, 0.72f, 0.76f);
        static readonly Color AmbientEquator = new Color(0.52f, 0.56f, 0.45f);
        static readonly Color AmbientGround = new Color(0.28f, 0.25f, 0.18f);

        public static Light Sun { get; private set; }
        public static bool HighQuality { get; private set; }
        public static ParticleSystem Pollen { get; private set; }

        static Material skyMat;
        static ColorAdjustments colorAdj;
        static WhiteBalance whiteBal;
        static SplitToning splitTone;
        static Bloom bloomFx;
        static Vignette vignette;

        public static void Setup(Camera cam)
        {
            HighQuality = !Application.isMobilePlatform;
            SetupSun();
            SetupSky();
            SetupAmbientAndFog();
            SetupReflections();
            SetupPostProcessing();
            SetupCamera(cam);
            SetupAirborneParticles(cam.transform);
            SetupMountains(cam.transform);
        }

        /// <summary>Applies a world's sky, sun, ambient light, haze, mountains and colour grade.</summary>
        public static void ApplyLook(BiomeLook l)
        {
            if (Sun != null)
            {
                Sun.transform.rotation = Quaternion.Euler(l.SunEuler);
                Sun.color = l.Sun;
                Sun.intensity = l.SunIntensity;
            }
            if (skyMat != null)
            {
                skyMat.SetColor("_ZenithColor", l.Zenith);
                skyMat.SetColor("_HorizonColor", l.Horizon);
                skyMat.SetColor("_GroundColor", l.Haze * 0.8f);
                skyMat.SetColor("_SunColor", l.Sun);
                if (Sun != null) skyMat.SetVector("_SunDirection", -Sun.transform.forward);
            }
            RenderSettings.ambientSkyColor = l.AmbSky;
            RenderSettings.ambientEquatorColor = l.AmbEquator;
            RenderSettings.ambientGroundColor = l.AmbGround;
            RenderSettings.fogColor = l.Haze;
            RenderSettings.fogStartDistance = l.FogStart;
            RenderSettings.fogEndDistance = l.FogEnd;

            if (colorAdj != null)
            {
                colorAdj.postExposure.Override(l.Exposure);
                colorAdj.contrast.Override(l.Contrast);
                colorAdj.saturation.Override(l.Saturation);
                colorAdj.colorFilter.Override(l.ColorFilter);
                whiteBal.temperature.Override(l.Temperature);
                splitTone.shadows.Override(l.SplitShadows);
                splitTone.highlights.Override(l.SplitHighlights);
                bloomFx.intensity.Override(l.Bloom);
                vignette.intensity.Override(l.Vignette);
                vignette.color.Override(l.VignetteColor);
            }
            var backdrop = Mats.Backdrop;
            backdrop.SetColor("_HazeColor", l.Horizon);
            backdrop.SetColor("_Tint", l.MountainTint);
        }

        /// <summary>Airborne particles for each world: pollen, snowfall, sea spray or blowing sand.</summary>
        public static void SetAirborne(BiomeType b)
        {
            if (Pollen == null) return;
            var main = Pollen.main;
            var em = Pollen.emission;
            var noise = Pollen.noise;
            switch (b)
            {
                case BiomeType.IceAge:
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.75f), new Color(0.85f, 0.92f, 1f, 0.45f));
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
                    main.gravityModifier = 0.04f;
                    em.rateOverTime = 140f;
                    noise.strength = 0.5f;
                    break;
                case BiomeType.WaterWorld:
                    // Sun glints and fine sea spray
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.7f), new Color(0.7f, 0.95f, 1f, 0.35f));
                    main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
                    main.gravityModifier = -0.01f;
                    em.rateOverTime = 60f;
                    noise.strength = 0.35f;
                    break;
                case BiomeType.Desert:
                    // Fine sand grains and dust carried on the hot wind
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.82f, 0.6f, 0.5f), new Color(0.85f, 0.7f, 0.48f, 0.25f));
                    main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
                    main.gravityModifier = 0.02f;
                    em.rateOverTime = 90f;
                    noise.strength = 0.9f;
                    break;
                default:
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.65f, 0.55f), new Color(1f, 0.98f, 0.85f, 0.25f));
                    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
                    main.gravityModifier = 0f;
                    em.rateOverTime = 40f;
                    noise.strength = 0.25f;
                    break;
            }
        }

        static void SetupSun()
        {
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>())
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            // Low, warm sun from behind-right of the runner: long shadows fall across the trail
            // and every obstacle face toward the camera is lit and easy to read.
            sun.transform.rotation = Quaternion.Euler(36f, -32f, 0f);
            sun.color = SunColor;
            sun.intensity = 2.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.88f;
            Sun = sun;
            RenderSettings.sun = sun;
        }

        static void SetupSky()
        {
            var sky = skyMat = new Material(Mats.Template("JE_Sky"));
            sky.SetColor("_ZenithColor", Zenith);
            sky.SetColor("_HorizonColor", Horizon);
            sky.SetColor("_GroundColor", Haze * 0.8f);
            sky.SetColor("_SunColor", SunColor);
            sky.SetVector("_SunDirection", -Sun.transform.forward);
            RenderSettings.skybox = sky;
        }

        static void SetupAmbientAndFog()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.ambientIntensity = 1f;

            // Thick humid haze: gives depth and hides new chunks appearing at the horizon.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Haze;
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 120f;
        }

        /// <summary>Small HDR cubemap of the sky so shiny things (gold idol, gems, wet leaves) reflect something believable.</summary>
        static void SetupReflections() => Reflect(Zenith, Horizon, Haze, SunColor);

        static Cubemap reflection;
        static Color pZenith, pHorizon, pHaze, pSun;

        /// <summary>Rebuilds the reflection cubemap from a sky palette (used when the world changes).</summary>
        public static void Reflect(Color zenith, Color horizon, Color haze, Color sunColor)
        {
            pZenith = zenith; pHorizon = horizon; pHaze = haze; pSun = sunColor;
            if (reflection != null) Object.Destroy(reflection);
            const int size = 32;
            var cube = reflection = new Cubemap(size, TextureFormat.RGBAHalf, true) { name = "SkyReflection" };
            var px = new Color[size * size];
            Vector3 sunDir = -Sun.transform.forward;
            for (int f = 0; f < 6; f++)
            {
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                        px[y * size + x] = SkyColor(FaceDir((CubemapFace)f, u, v).normalized, sunDir);
                    }
                cube.SetPixels(px, (CubemapFace)f);
            }
            cube.Apply(true, true);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
            RenderSettings.reflectionIntensity = 0.8f;
        }

        static Vector3 FaceDir(CubemapFace f, float u, float v)
        {
            switch (f)
            {
                case CubemapFace.PositiveX: return new Vector3(1f, -v, -u);
                case CubemapFace.NegativeX: return new Vector3(-1f, -v, u);
                case CubemapFace.PositiveY: return new Vector3(u, 1f, v);
                case CubemapFace.NegativeY: return new Vector3(u, -1f, -v);
                case CubemapFace.PositiveZ: return new Vector3(u, -v, 1f);
                default: return new Vector3(-u, -v, -1f);
            }
        }

        static Color SkyColor(Vector3 d, Vector3 sunDir)
        {
            Color c;
            if (d.y >= 0f) c = Color.Lerp(pHorizon.linear, pZenith.linear, Mathf.Pow(d.y, 0.45f));
            else c = Color.Lerp(pHorizon.linear, (pHaze * 0.35f).linear, Mathf.Clamp01(-d.y * 3f)); // canopy & ground below
            float s = Mathf.Max(0f, Vector3.Dot(d, sunDir));
            c += pSun.linear * (Mathf.Pow(s, 6f) * 0.3f + Mathf.Pow(s, 48f) * 2f);
            c.a = 1f;
            return c;
        }

        static void SetupPostProcessing()
        {
            var go = new GameObject("JungleLook");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 100f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            p.name = "JungleLookProfile";
            vol.sharedProfile = p;

            var tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var ca = colorAdj = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.85f);
            ca.contrast.Override(12f);
            ca.saturation.Override(8f);
            ca.colorFilter.Override(new Color(1f, 0.985f, 0.95f));

            var wb = whiteBal = p.Add<WhiteBalance>(true);
            wb.temperature.Override(6f);
            wb.tint.Override(0f);

            // Teal-leaning shadows, warm highlights: the classic filmic jungle grade.
            var split = splitTone = p.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.42f, 0.52f, 0.56f));
            split.highlights.Override(new Color(0.60f, 0.54f, 0.44f));
            split.balance.Override(15f);

            var bloom = bloomFx = p.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.93f, 0.8f));
            bloom.highQualityFiltering.Override(HighQuality);

            var vig = vignette = p.Add<Vignette>(true);
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(new Color(0.04f, 0.06f, 0.03f));

            var mb = p.Add<MotionBlur>(true);
            mb.intensity.Override(0f);

            var grain = p.Add<FilmGrain>(true);
            grain.intensity.Override(0f);

            if (HighQuality)
            {
                // Soft focus on the far jungle; keeps the eye on the trail.
                var dof = p.Add<DepthOfField>(true);
                dof.mode.Override(DepthOfFieldMode.Gaussian);
                dof.gaussianStart.Override(28f);
                dof.gaussianEnd.Override(95f);
                dof.gaussianMaxRadius.Override(1.1f);
                dof.highQualitySampling.Override(true);
            }
        }

        static void SetupCamera(Camera cam)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            cam.farClipPlane = 170f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = HighQuality ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                                            : AntialiasingMode.FastApproximateAntialiasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.dithering = true; // removes banding in the sky gradient and haze
            data.renderShadows = true;
        }

        // ---------------- Pollen ----------------

        // ---------------- Distant mountains ----------------

        /// <summary>
        /// Two rings of mountain ridges around the camera (a hazy blue far range and a darker,
        /// greener near range). They ignore the fog so they stay visible above the tree line.
        /// </summary>
        static void SetupMountains(Transform cam)
        {
            var root = new GameObject("Mountains");
            root.AddComponent<FollowCamera>().Target = cam;
            var mat = Mats.Backdrop;
            mat.SetColor("_HazeColor", Horizon);
            AddRidge(root.transform, mat, 158f, 22f, 72f, new Color(0.40f, 0.50f, 0.56f), 0.55f, 101);
            AddRidge(root.transform, mat, 132f, 10f, 38f, new Color(0.22f, 0.32f, 0.24f), 0.75f, 202);
        }

        static void AddRidge(Transform parent, Material mat, float radius, float minH, float maxH, Color color, float strength, int seed)
        {
            const int steps = 256;
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps;
                float a = u * Mathf.PI * 2f;
                // Tileable noise around the ring: broad ranges with sharper peaks.
                float n = Noise.Fbm(u, 0.37f, 5, 5, seed, 0.55f);
                float peaks = Mathf.Pow(n, 1.6f);
                float hgt = Mathf.Lerp(minH, maxH, peaks);
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float shade = 0.82f + 0.36f * Noise.Fbm(u, 0.71f, 24, 3, seed + 9);
                verts.Add(dir * radius + Vector3.up * -12f);
                cols.Add(new Color(color.r, color.g, color.b, 0f));                      // base melts into the haze
                verts.Add(dir * radius + Vector3.up * hgt);
                cols.Add(new Color(color.r * shade, color.g * shade, color.b * shade, strength));
                if (i == steps) continue;
                int b0 = i * 2;
                tris.Add(b0); tris.Add(b0 + 1); tris.Add(b0 + 2);
                tris.Add(b0 + 1); tris.Add(b0 + 3); tris.Add(b0 + 2);
            }
            var m = new Mesh { name = "Ridge" };
            m.SetVertices(verts);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2.2f);
            var go = new GameObject("Ridge");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        static void SetupAirborneParticles(Transform cam)
        {
            var root = new GameObject("Airborne").transform;
            root.gameObject.AddComponent<FollowZ>().Target = cam;

            // Sunlit pollen and dust motes drifting in the air.
            var motes = Pollen = Fx.MakeSystem("Pollen", root, Mats.ParticleAdd(Tex.Glow), 260);
            motes.transform.localPosition = new Vector3(0f, 3f, 22f);
            var main = motes.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.65f, 0.55f), new Color(1f, 0.98f, 0.85f, 0.25f));
            var em = motes.emission;
            em.rateOverTime = 40f;
            var shape = motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18f, 6f, 40f);
            var noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.3f;
            var col = motes.colorOverLifetime;
            col.enabled = true;
            col.color = Fx.FadeInOut();
            motes.Play();

        }
    }

    /// <summary>Keeps an object level with the target along the track (used for airborne particles).</summary>
    public class FollowZ : MonoBehaviour
    {
        public Transform Target;

        void LateUpdate()
        {
            if (Target == null) return;
            var p = Target.position;
            transform.SetPositionAndRotation(new Vector3(p.x, 0f, p.z), Quaternion.Euler(0f, Target.eulerAngles.y, 0f));
        }
    }

    /// <summary>Keeps an object centred on the camera horizontally (used for the mountain rings).</summary>
    public class FollowCamera : MonoBehaviour
    {
        public Transform Target;

        void LateUpdate()
        {
            if (Target == null) return;
            var p = Target.position;
            transform.position = new Vector3(p.x, 0f, p.z);
        }
    }

    /// <summary>Pooled one-shot particle effects (sparkles, dust, debris).</summary>
    public static class Fx
    {
        static ParticleSystem sparkle, dust, debris;

        public static void Sparkle(Vector3 pos, Color color, int count = 12)
        {
            if (sparkle == null)
            {
                sparkle = MakeSystem("Sparkles", Root(), Mats.ParticleAdd(Tex.Glow), 300);
                var col = sparkle.colorOverLifetime;
                col.enabled = true;
                col.color = Fade();
                var size = sparkle.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
                var main = sparkle.main;
                main.gravityModifier = -0.15f;
                sparkle.Play();
            }
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + Random.insideUnitSphere * 0.2f;
                ep.velocity = Random.insideUnitSphere * 3f + Vector3.up;
                ep.startSize = Random.Range(0.12f, 0.3f);
                ep.startLifetime = Random.Range(0.35f, 0.7f);
                ep.startColor = Color.Lerp(color, Color.white, Random.value * 0.4f);
                sparkle.Emit(ep, 1);
            }
        }

        public static void Dust(Vector3 pos, int count = 2, float size = 0.5f, Color? color = null)
        {
            if (dust == null)
            {
                dust = MakeSystem("Dust", Root(), Mats.ParticleAlpha(Tex.Puff), 200);
                var col = dust.colorOverLifetime;
                col.enabled = true;
                col.color = Fade();
                var sz = dust.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));
                var rot = dust.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
                dust.Play();
            }
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + new Vector3(Random.Range(-0.15f, 0.15f), 0.05f, Random.Range(-0.15f, 0.15f));
                ep.velocity = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.3f, 0.9f), Random.Range(-1.5f, 0f));
                ep.startSize = size * Random.Range(0.7f, 1.3f);
                ep.startLifetime = Random.Range(0.5f, 0.9f);
                ep.rotation = Random.Range(0f, 360f);
                ep.startColor = color ?? new Color(0.62f, 0.52f, 0.40f, 0.45f);
                dust.Emit(ep, 1);
            }
        }

        static ParticleSystem gas;

        /// <summary>A slow, rising puff of toxic green gas (poisonous plants).</summary>
        public static void Gas(Vector3 pos, int count = 4)
        {
            if (gas == null)
            {
                gas = MakeSystem("ToxicGas", Root(), Mats.ParticleAlpha(Tex.Puff), 200);
                var col = gas.colorOverLifetime;
                col.enabled = true;
                col.color = FadeInOut();
                var sz = gas.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.2f));
                var rot = gas.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
                gas.Play();
            }
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + Random.insideUnitSphere * 0.25f;
                ep.velocity = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.3f, 0.7f), Random.Range(-0.25f, 0.25f));
                ep.startSize = Random.Range(0.5f, 0.9f);
                ep.startLifetime = Random.Range(1.6f, 2.6f);
                ep.rotation = Random.Range(0f, 360f);
                ep.startColor = new Color(0.45f, 0.9f, 0.25f, 0.35f);
                gas.Emit(ep, 1);
            }
        }

        /// <summary>Chunks flying off a smashed obstacle.</summary>
        public static void Debris(Vector3 pos, Color color, int count = 16)
        {
            if (debris == null)
            {
                debris = MakeSystem("Debris", Root(), Mats.ParticleAlpha(Tex.Puff), 200);
                var main = debris.main;
                main.gravityModifier = 1.5f;
                var col = debris.colorOverLifetime;
                col.enabled = true;
                col.color = Fade();
                debris.Play();
            }
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + Random.insideUnitSphere * 0.5f;
                ep.velocity = Random.insideUnitSphere * 5f + Vector3.up * 3f;
                ep.startSize = Random.Range(0.2f, 0.5f);
                ep.startLifetime = Random.Range(0.6f, 1.1f);
                ep.startColor = color * Random.Range(0.7f, 1.1f);
                debris.Emit(ep, 1);
            }
            Dust(pos, 6, 1.2f);
        }

        // ---------------- Helpers ----------------

        static Transform root;

        static Transform Root()
        {
            if (root == null) root = new GameObject("FX").transform;
            return root;
        }

        public static ParticleSystem MakeSystem(string name, Transform parent, Material mat, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 0f;
            return ps;
        }

        public static ParticleSystem.MinMaxGradient FadeInOut()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        static ParticleSystem.MinMaxGradient Fade()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }
    }
}
