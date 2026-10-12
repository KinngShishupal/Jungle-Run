using System;
using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// A small offline synthesizer: instruments (FM marimba and bells, Karplus-Strong plucked strings,
    /// steel pan, pads, drums and percussion), noise shaping and a Schroeder reverb. Everything renders into
    /// plain float arrays with no Unity API calls, so it runs safely on a background thread.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 22050;
        const float Tau = Mathf.PI * 2f;

        public class Rng
        {
            readonly System.Random r;
            public Rng(int seed) { r = new System.Random(seed); }
            public float Next() => (float)r.NextDouble();
            public float Range(float a, float b) => a + (b - a) * Next();
            public int Int(int a, int b) => r.Next(a, b);
            public float Noise() => Next() * 2f - 1f;
            public bool Chance(float p) => Next() < p;
        }

        public static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];
        public static float Midi(float n) => 440f * Mathf.Pow(2f, (n - 69f) / 12f);
        static int Idx(float t) => (int)(t * Rate);
        static float Exp(float x) => (float)Math.Exp(x);
        static float Sin(float x) => (float)Math.Sin(x);

        /// <summary>Band-limited sawtooth by additive synthesis (no aliasing at our low sample rate).</summary>
        static float Saw(float phase, float freq, int maxHarm = 12)
        {
            float s = 0f;
            int n = Mathf.Min(maxHarm, Mathf.Max(1, (int)(Rate * 0.45f / Mathf.Max(freq, 1f))));
            for (int k = 1; k <= n; k++) s += Sin(phase * k) / k;
            return s * 0.6f;
        }

        // ================= Tuned instruments =================

        /// <summary>Marimba / balafon: FM with a fast-decaying 4:1 modulator, wooden attack, warm tail.</summary>
        public static void Marimba(float[] b, float t0, float f, float amp, float dur = 1.3f)
        {
            int s0 = Idx(t0), n = Idx(dur);
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i; if (k < 0) continue; if (k >= b.Length) break;
                float t = i / (float)Rate;
                float env = (1f - Exp(-t * 900f)) * Exp(-t * (4.5f + f * 0.004f));
                float mod = 2.2f * Exp(-t * 28f) * Sin(Tau * f * 4f * t);
                b[k] += amp * env * (Sin(Tau * f * t + mod) + 0.2f * Sin(Tau * f * 9.9f * t) * Exp(-t * 60f));
            }
        }

        /// <summary>Bell / celesta: inharmonic FM (3.5:1) with a long shimmering decay.</summary>
        public static void Bell(float[] b, float t0, float f, float amp, float dur = 3f, float decay = 1.6f)
        {
            int s0 = Idx(t0), n = Idx(dur);
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i; if (k < 0) continue; if (k >= b.Length) break;
                float t = i / (float)Rate;
                float env = (1f - Exp(-t * 1500f)) * Exp(-t * decay);
                float mod = 2.6f * Exp(-t * 2.5f) * Sin(Tau * f * 3.5f * t);
                b[k] += amp * env * Sin(Tau * f * t + mod);
            }
        }

        /// <summary>Steel pan: a struck metal note with slightly stretched partials and a bright ring.</summary>
        public static void SteelPan(float[] b, float t0, float f, float amp, float dur = 1.4f)
        {
            float[] ratio = { 1f, 2.0f, 3.02f, 4.07f }, a = { 1f, 0.55f, 0.28f, 0.14f };
            int s0 = Idx(t0), n = Idx(dur);
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i; if (k < 0) continue; if (k >= b.Length) break;
                float t = i / (float)Rate;
                float att = 1f - Exp(-t * 700f);
                float s = 0f;
                for (int p = 0; p < ratio.Length; p++) s += a[p] * Sin(Tau * f * ratio[p] * t) * Exp(-t * (2.6f + p * 1.8f));
                b[k] += amp * att * s * 0.6f;
            }
        }

        /// <summary>
        /// Karplus-Strong plucked string (guitar, ukulele, oud, tanpura). The delay line is excited with
        /// filtered noise (brightness) and lossy averaging makes it ring and darken like a real string.
        /// </summary>
        public static void Pluck(float[] b, float t0, float f, float amp, float dur, float bright, float decay, Rng rng)
        {
            int N = Mathf.Max(2, (int)(Rate / f));
            var d = new float[N];
            float lp = 0f, rms = 0f;
            for (int i = 0; i < N; i++) { lp += (rng.Noise() - lp) * bright; d[i] = lp; rms += lp * lp; }
            rms = Mathf.Sqrt(rms / N) + 1e-4f;
            for (int i = 0; i < N; i++) d[i] *= 0.5f / rms;
            int s0 = Idx(t0), n = Idx(dur), fadeN = Idx(0.06f);
            int p = 0;
            for (int i = 0; i < n; i++)
            {
                float y = d[p];
                int q = p + 1 == N ? 0 : p + 1;
                d[p] = (y + d[q]) * 0.5f * decay;
                p = q;
                int k = s0 + i; if (k < 0) continue; if (k >= b.Length) break;
                float fade = i > n - fadeN ? (n - i) / (float)fadeN : 1f;
                b[k] += amp * y * fade;
            }
        }

        /// <summary>Warm pad: three detuned band-limited saws, softened, with slow attack and release.</summary>
        public static void Pad(float[] b, float t0, float f, float dur, float amp, float cutoff, Rng rng)
        {
            float[] det = { 1f, 1.006f, 0.994f };
            float[] ph = { rng.Next() * Tau, rng.Next() * Tau, rng.Next() * Tau };
            int s0 = Idx(t0), n = Idx(dur);
            float att = Mathf.Min(1.2f, dur * 0.35f), rel = Mathf.Min(1.4f, dur * 0.4f);
            int harm = Mathf.Max(1, Mathf.Min(10, (int)(cutoff / f)));
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i; if (k < 0) continue; if (k >= b.Length) break;
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / att) * Mathf.Clamp01((dur - t) / rel);
                float s = 0f;
                for (int v = 0; v < 3; v++) s += Saw(ph[v] + Tau * f * det[v] * t, f, harm);
                b[k] += amp * env * s * 0.33f;
            }
        }

        /// <summary>Round sine bass with a soft pluck.</summary>
        public static void Bass(float[] b, float t0, float f, float dur, float amp)
        {
            int s0 = Idx(t0), n = Idx(dur);
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i; if (k < 0) continue; if (k >= b.Length) break;
                float t = i / (float)Rate;
                float env = (1f - Exp(-t * 300f)) * Exp(-t * 2.2f) * Mathf.Clamp01((dur - t) / 0.05f);
                b[k] += amp * env * (Sin(Tau * f * t) + 0.25f * Sin(Tau * 2f * f * t) + 0.08f * Sin(Tau * 3f * f * t));
            }
        }

        /// <summary>Long sine drone with gentle beating (for ice and desert).</summary>
        public static void Drone(float[] b, float f, float amp, float beat = 0.15f)
        {
            for (int k = 0; k < b.Length; k++)
            {
                float t = k / (float)Rate;
                b[k] += amp * (Sin(Tau * f * t) * 0.6f + Sin(Tau * (f + beat) * t) * 0.4f + 0.15f * Sin(Tau * 2f * f * t));
            }
        }

        // ================= Drums & percussion =================

        /// <summary>Tuned drum (log drum, kick, doum): sine with a pitch drop at the strike.</summary>
        public static void Drum(float[] b, float t0, float f, float amp, float decay = 8f, float noise = 0.15f, Rng rng = null)
        {
            int s0 = Idx(t0), n = Idx(Mathf.Min(1.5f, 6f / decay));
            float ph = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i; if (k < 0) continue; if (k >= b.Length) break;
                float t = i / (float)Rate;
                float fr = f * (1f + 1.4f * Exp(-t * 45f));
                ph += Tau * fr / Rate;
                float env = (1f - Exp(-t * 2000f)) * Exp(-t * decay);
                float nz = 0f;
                if (rng != null) { lp += (rng.Noise() - lp) * 0.3f; nz = lp * Exp(-t * 70f) * noise * 3f; }
                b[k] += amp * (env * Sin(ph) + nz);
            }
        }

        /// <summary>Hand drum slap / tek: a high, short tone with a bright noise crack.</summary>
        public static void Slap(float[] b, float t0, float f, float amp, Rng rng)
        {
            int s0 = Idx(t0), n = Idx(0.18f);
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i; if (k < 0) continue; if (k >= b.Length) break;
                float t = i / (float)Rate;
                float nz = rng.Noise(); float hp = nz - prev; prev = nz;
                b[k] += amp * (Sin(Tau * f * t) * Exp(-t * 30f) * 0.6f + hp * Exp(-t * 55f) * 0.5f);
            }
        }

        /// <summary>Shaker: a burst of high-passed noise.</summary>
        public static void Shaker(float[] b, float t0, float amp, Rng rng, float length = 0.1f)
        {
            int s0 = Idx(t0), n = Idx(length);
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i; if (k < 0) continue; if (k >= b.Length) break;
                float t = i / (float)Rate;
                float nz = rng.Noise(); float hp = nz - prev; prev = nz;
                float env = (1f - Exp(-t * 300f)) * Exp(-t * 40f);
                b[k] += amp * hp * env;
            }
        }

        public static void Clave(float[] b, float t0, float amp)
        {
            int s0 = Idx(t0), n = Idx(0.15f);
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i; if (k < 0) continue; if (k >= b.Length) break;
                float t = i / (float)Rate;
                b[k] += amp * Exp(-t * 38f) * (Sin(Tau * 2450f * t) + 0.4f * Sin(Tau * 1680f * t));
            }
        }

        /// <summary>Finger cymbals (zills): high inharmonic partials with a long ring.</summary>
        public static void Zill(float[] b, float t0, float amp)
        {
            float[] p = { 3150f, 4230f, 5620f, 7050f };
            int s0 = Idx(t0), n = Idx(2.5f);
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i; if (k < 0) continue; if (k >= b.Length) break;
                float t = i / (float)Rate;
                float s = 0f;
                for (int j = 0; j < p.Length; j++) s += Sin(Tau * p[j] * t) * Exp(-t * (1.6f + j * 0.7f));
                b[k] += amp * s * 0.3f;
            }
        }

        // ================= Mixing =================

        /// <summary>Schroeder reverb (four combs into two all-passes), mixed in place.</summary>
        public static void Reverb(float[] b, float wet, float room = 0.8f)
        {
            int[] combs = { 558, 594, 638, 678 };
            int[] alls = { 278, 111 };
            var outBuf = new float[b.Length];
            foreach (int len in combs)
            {
                var d = new float[len];
                int p = 0; float lp = 0f;
                for (int i = 0; i < b.Length; i++)
                {
                    float y = d[p];
                    lp = y * 0.75f + lp * 0.25f;          // damping
                    d[p] = b[i] * 0.25f + lp * room;
                    p = p + 1 == len ? 0 : p + 1;
                    outBuf[i] += y;
                }
            }
            foreach (int len in alls)
            {
                var d = new float[len];
                int p = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float x = outBuf[i], y = d[p];
                    d[p] = x + y * 0.5f;
                    outBuf[i] = y - x * 0.5f;
                    p = p + 1 == len ? 0 : p + 1;
                }
            }
            for (int i = 0; i < b.Length; i++) b[i] += outBuf[i] * wet;
        }

        /// <summary>Folds a rendered tail back onto the start, so a loop of <paramref name="loopLen"/> samples repeats seamlessly.</summary>
        public static float[] FoldLoop(float[] b, int loopLen)
        {
            var o = new float[loopLen];
            Array.Copy(b, o, loopLen);
            for (int i = loopLen; i < b.Length; i++) o[i % loopLen] += b[i];
            return o;
        }

        /// <summary>Crossfades the last <paramref name="fade"/> samples into the start (for noise-based loops).</summary>
        public static float[] CrossfadeLoop(float[] b, int fade)
        {
            int len = b.Length - fade;
            var o = new float[len];
            Array.Copy(b, o, len);
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                o[i] = b[i] * w + b[len + i] * (1f - w);
            }
            return o;
        }

        /// <summary>Normalizes to a peak and soft-clips (tanh) so nothing distorts harshly.</summary>
        public static float[] Master(float[] b, float peak = 0.85f)
        {
            float m = 1e-5f;
            for (int i = 0; i < b.Length; i++) m = Mathf.Max(m, Mathf.Abs(b[i]));
            float g = peak / m * 1.15f;
            for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * g) * peak / (float)Math.Tanh(peak * 1.15f);
            // De-click the very ends of one-shots
            int f = Mathf.Min(64, b.Length / 4);
            for (int i = 0; i < f; i++) { float w = i / (float)f; b[i] *= w; }
            return b;
        }

        // ================= Noise helpers =================

        /// <summary>One-pole low-pass on noise with a time-varying coefficient (0..1).</summary>
        public static void FilteredNoise(float[] b, int s0, int n, Func<float, float> coef, Func<float, float> amp, Rng rng, bool highpass = false)
        {
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i; if (k < 0) continue; if (k >= b.Length) break;
                float t = i / (float)Rate;
                float x = rng.Noise();
                lp += (x - lp) * coef(t);
                b[k] += amp(t) * (highpass ? x - lp : lp);
            }
        }

        /// <summary>A sine sweep (bird chirp, bubble, cry) with an envelope and optional vibrato.</summary>
        public static void Sweep(float[] b, float t0, float f0, float f1, float dur, float amp, float vibHz = 0f, float vibDepth = 0f, float harm2 = 0f)
        {
            int s0 = Idx(t0), n = Idx(dur);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i; if (k < 0) continue; if (k >= b.Length) break;
                float t = i / (float)Rate, u = t / dur;
                float f = Mathf.Lerp(f0, f1, u) * (1f + vibDepth * Sin(Tau * vibHz * t));
                ph += Tau * f / Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)) ;
                b[k] += amp * env * (Sin(ph) + harm2 * Sin(ph * 2f));
            }
        }

        /// <summary>
        /// Rough, voiced animal call: a band-limited saw following a pitch contour, with amplitude roughness
        /// (growl), a breathy noise layer and a low-pass "mouth". Used for roars, trumpets, groans and grunts.
        /// </summary>
        public static void Voice(float[] b, float t0, float dur, Func<float, float> pitch, float amp, float rough, float roughHz,
                                 float breath, float mouth, Rng rng, int harm = 14)
        {
            int s0 = Idx(t0), n = Idx(dur);
            float ph = 0f, lp = 0f, nlp = 0f;
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i; if (k < 0) continue; if (k >= b.Length) break;
                float t = i / (float)Rate, u = t / dur;
                float f = pitch(u);
                ph += Tau * f / Rate;
                float g = 1f - rough * (0.5f + 0.5f * Sin(Tau * roughHz * t + rng.Range(-0.4f, 0.4f)));
                float env = Mathf.Clamp01(u / 0.08f) * Mathf.Clamp01((1f - u) / 0.25f);
                float v = Saw(ph, f, harm) * g;
                nlp += (rng.Noise() - nlp) * 0.35f;
                float x = v + nlp * breath;
                lp += (x - lp) * mouth;
                b[k] += amp * env * lp;
            }
        }
    }
}
