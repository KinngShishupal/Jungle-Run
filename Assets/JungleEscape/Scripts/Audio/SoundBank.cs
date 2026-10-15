using System;
using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// Everything the game sounds like, composed from <see cref="Synth"/>: a looping theme and an ambient
    /// soundscape for each world, plus every sound effect. Pure computation (thread-safe).
    /// </summary>
    public static class SoundBank
    {
        static float M(int midi) => Synth.Midi(midi);

        // ================= Melody helper =================

        static readonly float[][] Rhythms =
        {
            new[] { 0f, 1f, 2f, 3f }, new[] { 0f, 0.5f, 1.5f, 2f, 3f }, new[] { 0f, 1.5f, 2f, 2.5f, 3.5f },
            new[] { 0f, 2f, 3f }, new[] { 0f, 0.75f, 1.5f, 2.5f, 3f }, new[] { 0f, 1f, 1.5f, 3f }
        };

        /// <summary>
        /// A singable melody: a random walk on the scale (mostly steps, the odd leap), phrases of two bars,
        /// bars 5-6 echoing bars 1-2, and the phrase ends resting on the tonic.
        /// </summary>
        static List<(float beat, float len, int midi)> Melody(Synth.Rng r, int[] scale, int tonicIndex, int bars, float[][] rhythms)
        {
            var notes = new List<(float, float, int)>();
            var phrase = new List<(float, float, int)>[bars];
            int idx = tonicIndex;
            for (int bar = 0; bar < bars; bar++)
            {
                if (bar == 4 || bar == 5)
                {
                    foreach (var nt in phrase[bar - 4]) notes.Add((nt.Item1 + 16f, nt.Item2, nt.Item3));
                    phrase[bar] = phrase[bar - 4];
                    continue;
                }
                var list = new List<(float, float, int)>();
                bool cadence = bar == 3 || bar == 7;
                var rh = cadence ? new[] { 0f, 1f, 2f } : rhythms[r.Int(0, rhythms.Length)];
                for (int i = 0; i < rh.Length; i++)
                {
                    float len = (i + 1 < rh.Length ? rh[i + 1] : 4f) - rh[i];
                    int step = r.Chance(0.15f) ? (r.Chance(0.5f) ? 3 : -3) : r.Int(-2, 3);
                    idx = Mathf.Clamp(idx + step, 0, scale.Length - 1);
                    if (cadence && i == rh.Length - 1) idx = tonicIndex;
                    list.Add((bar * 4f + rh[i], len, scale[idx]));
                }
                phrase[bar] = list;
                notes.AddRange(list);
            }
            return notes;
        }

        // ================= Music =================

        public static float[] Music(BiomeType w)
        {
            float[] m;
            switch (w)
            {
                case BiomeType.IceAge: m = IceTheme(); break;
                case BiomeType.WaterWorld: m = SeaTheme(); break;
                case BiomeType.Desert: m = DesertTheme(); break;
                default: m = JungleTheme(); break;
            }
            return Level(m, 0.15f);
        }

        /// <summary>Matches perceived loudness between tracks (RMS), never pushing peaks past 0.95.</summary>
        static float[] Level(float[] b, float targetRms)
        {
            double sum = 0; float peak = 1e-5f;
            foreach (var s in b) { sum += s * s; peak = Mathf.Max(peak, Mathf.Abs(s)); }
            float rms = (float)Math.Sqrt(sum / b.Length) + 1e-5f;
            float g = Mathf.Min(targetRms / rms, 0.95f / peak);
            for (int i = 0; i < b.Length; i++) b[i] *= g;
            return b;
        }

        /// <summary>Jungle: marimba over log drums, hand drums and shakers in A minor pentatonic.</summary>
        static float[] JungleTheme()
        {
            var r = new Synth.Rng(101);
            float spb = 60f / 104f; int bars = 8;
            float L = bars * 4 * spb;
            var b = Synth.Buffer(L + 3f);
            int[] roots = { 45, 45, 43, 43, 41, 41, 40, 40 };   // A G F E (bass, two bars each)
            int[] pent = { 57, 60, 62, 64, 67, 69, 72, 74, 76, 79, 81 };
            for (int bar = 0; bar < bars; bar++)
            {
                float t = bar * 4 * spb;
                float root = M(roots[bar] - 12);
                Synth.Drum(b, t, root * 2f, 0.55f, 7f, 0.1f, r);
                Synth.Drum(b, t + 1.5f * spb, root * 2f, 0.35f, 9f, 0.1f, r);
                Synth.Drum(b, t + 3f * spb, root * 3f, 0.3f, 9f, 0.1f, r);
                // Hand drums: open tones on 1 and the "and" of 3, slaps on 2, 4 and the last 16th
                Synth.Drum(b, t, 140f, 0.25f, 14f, 0.3f, r);
                Synth.Drum(b, t + 2.5f * spb, 150f, 0.22f, 14f, 0.3f, r);
                Synth.Slap(b, t + 1f * spb, 420f, 0.22f, r);
                Synth.Slap(b, t + 3f * spb, 420f, 0.22f, r);
                Synth.Slap(b, t + 3.75f * spb, 460f, 0.14f, r);
                for (int s = 0; s < 16; s++) Synth.Shaker(b, t + s * spb * 0.25f, s % 2 == 1 ? 0.09f : 0.05f, r);
                // Marimba arpeggio in eighths around the bar's chord
                int[] arp = { 0, 2, 4, 2, 5, 4, 2, 4 };
                int baseIdx = bar < 2 ? 0 : bar < 4 ? 4 : bar < 6 ? 3 : 2;   // A, G, F, E colours
                for (int e = 0; e < 8; e++)
                {
                    int ni = Mathf.Clamp(baseIdx + arp[e], 0, pent.Length - 1);
                    Synth.Marimba(b, t + e * spb * 0.5f, M(pent[ni] - 12), 0.13f, 0.7f);
                }
            }
            foreach (var n in Melody(r, pent, 5, bars, Rhythms))
                Synth.Marimba(b, n.beat * spb, M(n.midi + 12), 0.3f, Mathf.Max(0.8f, n.len * spb + 0.4f));
            Synth.Reverb(b, 0.22f, 0.78f);
            return Synth.Master(Synth.FoldLoop(b, Mathf.RoundToInt(L * Synth.Rate)), 0.8f);
        }

        /// <summary>Ice Age: slow pads, a low drone and sparse celesta bells in D dorian.</summary>
        static float[] IceTheme()
        {
            var r = new Synth.Rng(202);
            float spb = 60f / 72f; int bars = 8;
            float L = bars * 4 * spb;
            var b = Synth.Buffer(L + 4f);
            var drone = Synth.Buffer(L + 4f);
            Synth.Drone(drone, M(38), 0.12f, 0.12f);
            Synth.Drone(drone, M(45), 0.06f, 0.2f);
            for (int i = 0; i < b.Length; i++) b[i] += drone[i];
            int[][] chords = { new[] { 50, 53, 57, 60 }, new[] { 48, 52, 55, 59 }, new[] { 43, 55, 59, 62 }, new[] { 45, 57, 60, 64 } };
            for (int c = 0; c < 4; c++)
                foreach (int n in chords[c])
                    Synth.Pad(b, c * 8 * spb, M(n), 8 * spb + 1.5f, 0.06f, 1400f, r);
            int[] scale = { 62, 64, 65, 67, 69, 71, 72, 74, 76, 77, 79, 81 };
            var sparse = new[] { new[] { 0f, 2f }, new[] { 0f, 1.5f, 3f }, new[] { 1f, 3f }, new[] { 0f, 2f, 2.5f } };
            foreach (var n in Melody(r, scale, 7, bars, sparse))
                Synth.Bell(b, n.beat * spb, M(n.midi + 12), 0.16f, 3.5f, 1.2f);
            // Twinkles, like light on frost
            for (int i = 0; i < 10; i++) Synth.Bell(b, r.Range(0f, L), M(scale[r.Int(6, scale.Length)] + 24), 0.04f, 2.5f, 2f);
            Synth.Reverb(b, 0.45f, 0.86f);
            return Synth.Master(Synth.FoldLoop(b, Mathf.RoundToInt(L * Synth.Rate)), 0.75f);
        }

        /// <summary>Water world: strummed ukulele, steel pan melody, bass, shaker and son clave in C major.</summary>
        static float[] SeaTheme()
        {
            var r = new Synth.Rng(303);
            float spb = 60f / 112f; int bars = 8;
            float L = bars * 4 * spb;
            var b = Synth.Buffer(L + 3f);
            int[][] chords = { new[] { 60, 64, 67, 72 }, new[] { 59, 62, 67, 71 }, new[] { 57, 60, 64, 69 }, new[] { 57, 60, 65, 69 } };
            int[] roots = { 36, 43, 45, 41 };
            float[] strum = { 0f, 1f, 1.5f, 2.5f, 3f, 3.5f };
            bool[] down = { true, true, false, false, true, false };
            for (int bar = 0; bar < bars; bar++)
            {
                float t = bar * 4 * spb;
                int c = bar % 4;
                for (int s = 0; s < strum.Length; s++)
                {
                    var ch = chords[c];
                    for (int j = 0; j < ch.Length; j++)
                    {
                        int jj = down[s] ? j : ch.Length - 1 - j;
                        Synth.Pluck(b, t + strum[s] * spb + j * 0.012f, M(ch[jj]), down[s] ? 0.09f : 0.06f, 0.9f, 0.55f, 0.995f, r);
                    }
                }
                Synth.Bass(b, t, M(roots[c]), 1.4f * spb, 0.42f);
                Synth.Bass(b, t + 2f * spb, M(roots[c] + 7), 1.2f * spb, 0.32f);
                Synth.Bass(b, t + 3.5f * spb, M(roots[c] + 12), 0.5f * spb, 0.22f);
                Synth.Drum(b, t, 58f, 0.32f, 10f);
                Synth.Drum(b, t + 2f * spb, 58f, 0.25f, 10f);
                for (int e = 0; e < 8; e++) Synth.Shaker(b, t + e * spb * 0.5f, e % 2 == 1 ? 0.08f : 0.045f, r);
                float[] clave = bar % 2 == 0 ? new[] { 0f, 1.5f, 3f } : new[] { 1f, 2f };
                foreach (float cb in clave) Synth.Clave(b, t + cb * spb, 0.12f);
            }
            int[] pent = { 60, 62, 64, 67, 69, 72, 74, 76, 79 };
            foreach (var n in Melody(r, pent, 5, bars, Rhythms))
                Synth.SteelPan(b, n.beat * spb, M(n.midi + 12), 0.3f, Mathf.Max(0.8f, n.len * spb + 0.5f));
            Synth.Reverb(b, 0.2f, 0.75f);
            return Synth.Master(Synth.FoldLoop(b, Mathf.RoundToInt(L * Synth.Rate)), 0.8f);
        }

        /// <summary>Desert: oud over a tanpura-like drone, frame drum (maqsum rhythm) and zills, in D Hijaz.</summary>
        static float[] DesertTheme()
        {
            var r = new Synth.Rng(404);
            float spb = 60f / 92f; int bars = 8;
            float L = bars * 4 * spb;
            var b = Synth.Buffer(L + 4f);
            int[] drone = { 50, 57, 62, 38 };
            for (int beat = 0; beat < bars * 4; beat++)
                Synth.Pluck(b, beat * spb, M(drone[beat % 4]), 0.1f, 3f, 0.45f, 0.9985f, r);
            for (int bar = 0; bar < bars; bar++)
            {
                float t = bar * 4 * spb;
                // Maqsum: DUM tek . tek DUM . tek .
                Synth.Drum(b, t, 72f, 0.5f, 6f, 0.25f, r);
                Synth.Slap(b, t + 0.5f * spb, 380f, 0.2f, r);
                Synth.Slap(b, t + 1.5f * spb, 380f, 0.2f, r);
                Synth.Drum(b, t + 2f * spb, 72f, 0.45f, 6f, 0.25f, r);
                Synth.Slap(b, t + 3f * spb, 400f, 0.22f, r);
                Synth.Shaker(b, t + 3.5f * spb, 0.06f, r, 0.15f);
                if (bar % 4 == 0) Synth.Zill(b, t, 0.12f);
                Synth.Bass(b, t, M(38), 3.5f * spb, 0.2f);
            }
            int[] hijaz = { 57, 58, 60, 62, 63, 66, 67, 69, 70, 72, 74 };
            foreach (var n in Melody(r, hijaz, 3, bars, Rhythms))
            {
                // Oud: plucked, slightly dark, with the occasional grace note from above
                if (r.Chance(0.3f))
                    Synth.Pluck(b, n.beat * spb - 0.06f, M(n.midi + 1), 0.16f, 0.2f, 0.35f, 0.99f, r);
                Synth.Pluck(b, n.beat * spb, M(n.midi), 0.32f, Mathf.Max(0.7f, n.len * spb + 0.3f), 0.32f, 0.996f, r);
            }
            Synth.Reverb(b, 0.3f, 0.8f);
            return Synth.Master(Synth.FoldLoop(b, Mathf.RoundToInt(L * Synth.Rate)), 0.78f);
        }

        // ================= Ambience =================

        public static float[] Ambience(BiomeType w)
        {
            var r = new Synth.Rng(500 + (int)w);
            const float L = 12f, fade = 1.5f;
            var b = Synth.Buffer(L + fade);
            int n = b.Length;
            switch (w)
            {
                case BiomeType.IceAge:
                    // Cold wind: gusting, with a faint whistle
                    Synth.FilteredNoise(b, 0, n, t => 0.03f + 0.05f * (0.5f + 0.5f * Mathf.Sin(t * 0.7f)), t => 0.6f + 0.4f * Mathf.Sin(t * 0.45f + 1f), r);
                    for (int i = 0; i < 3; i++) Synth.Sweep(b, r.Range(0f, L - 3f), r.Range(650f, 800f), r.Range(700f, 900f), r.Range(2f, 3f), 0.03f, 0.3f, 0.02f);
                    break;
                case BiomeType.WaterWorld:
                    // Surf: waves swelling and breaking on a ~6 s cycle, plus distant gulls
                    Synth.FilteredNoise(b, 0, n, t => 0.04f + 0.1f * Wave(t), t => 0.25f + 0.75f * Wave(t), r);
                    for (int i = 0; i < 3; i++)
                    {
                        float t0 = r.Range(0f, L - 1f), f = r.Range(1500f, 2000f);
                        Synth.Sweep(b, t0, f, f * 0.7f, 0.35f, 0.05f, 9f, 0.05f, 0.3f);
                        Synth.Sweep(b, t0 + 0.4f, f * 1.05f, f * 0.75f, 0.3f, 0.04f, 9f, 0.05f, 0.3f);
                    }
                    break;
                case BiomeType.Desert:
                    // Dry hot wind with gusts and hissing sand, and a hawk crying far off
                    Synth.FilteredNoise(b, 0, n, t => 0.05f + 0.06f * Mathf.Abs(Mathf.Sin(t * 0.5f)), t => 0.35f + 0.35f * Mathf.Abs(Mathf.Sin(t * 0.37f)), r);
                    Synth.FilteredNoise(b, 0, n, t => 0.5f, t => 0.06f * (0.5f + 0.5f * Mathf.Sin(t * 0.9f)), r, true);
                    Synth.Sweep(b, r.Range(1f, 4f), 2600f, 1700f, 1.1f, 0.05f, 7f, 0.02f, 0.2f);
                    Synth.Sweep(b, r.Range(7f, 9f), 2500f, 1600f, 1.0f, 0.04f, 7f, 0.02f, 0.2f);
                    break;
                default:
                    // Rainforest: buzzing cicadas, chirping birds, a far-off hoot, rustling leaves
                    Synth.FilteredNoise(b, 0, n, t => 0.45f, t => 0.05f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 28f * Mathf.PI * 2f), 2f) * (0.6f + 0.4f * Mathf.Sin(t * 1.1f)), r, true);
                    Synth.FilteredNoise(b, 0, n, t => 0.02f, t => 0.25f, r);
                    for (int i = 0; i < 16; i++)
                    {
                        float t0 = r.Range(0f, L - 1f), f = r.Range(2200f, 4200f);
                        int notes = r.Int(2, 6);
                        for (int k = 0; k < notes; k++)
                            Synth.Sweep(b, t0 + k * r.Range(0.09f, 0.16f), f, f * r.Range(0.8f, 1.35f), r.Range(0.05f, 0.11f), r.Range(0.03f, 0.07f));
                    }
                    for (int i = 0; i < 2; i++) Synth.Sweep(b, r.Range(0f, L - 1f), 420f, 380f, 0.45f, 0.05f, 5f, 0.03f, 0.3f);
                    break;
            }
            Synth.Reverb(b, 0.2f, 0.7f);
            return Synth.Master(Synth.CrossfadeLoop(b, Mathf.RoundToInt(fade * Synth.Rate)), 0.6f);
        }

        static float Wave(float t)
        {
            float c = Mathf.Repeat(t, 6f) / 6f;
            return c < 0.55f ? Mathf.SmoothStep(0f, 0.7f, c / 0.55f) : Mathf.Lerp(1f, 0.15f, (c - 0.55f) / 0.45f);
        }

        // ================= Sound effects =================

        /// <summary>All sound effects, keyed by name (some keys have several variations).</summary>
        public static Dictionary<string, float[][]> Effects()
        {
            var d = new Dictionary<string, float[][]>();
            var r = new Synth.Rng(900);

            d["step_dirt"] = Variants(3, i => Step(r, 0.12f, 90f, 0.08f, 0.3f));
            d["step_snow"] = Variants(3, i => Crunch(r));
            d["step_wood"] = Variants(3, i => Knock(r, 180f + i * 25f));
            d["step_sand"] = Variants(3, i => Step(r, 0.18f, 70f, 0.2f, 0.15f));

            d["jump"] = One(Whoosh(r, 0.32f, 0.05f, 0.35f, 0.7f));
            d["land"] = One(Thump(r, 0.3f, 62f, 0.7f));
            d["roll"] = One(Whoosh(r, 0.55f, 0.08f, 0.2f, 0.6f));
            d["glide_open"] = One(GlideOpen(r));
            d["wind_loop"] = One(WindLoop(r));

            d["pickup_banana"] = One(Chime(new[] { 84f, 88f }, 0.06f, r, pop: true));
            d["pickup_acorn"] = One(Acorn(r));
            d["pickup_coin"] = One(Coin());
            d["coin_collect"] = Variants(3, i => CoinCollect(r, i));
            d["pickup_water"] = One(Glass(r));
            d["powerup"] = One(Chime(new[] { 72f, 76f, 79f, 84f, 88f }, 0.06f, r, pop: false, sparkle: true));
            d["shield"] = One(Crack(r));
            d["stumble"] = One(Stumble(r));
            d["crash"] = One(Crash(r));
            d["world"] = One(WorldSting(r));
            d["click"] = One(Click());

            d["monkey_chatter"] = Variants(2, i => Chatter(r));
            d["monkey_shriek"] = One(Shriek(r));
            d["tarzan"] = One(TarzanYell(r));
            d["roar"] = Variants(2, i => Roar(r, 1f + i * 0.1f));
            d["trumpet"] = One(Trumpet(r));
            d["groan"] = One(Groan(r));
            d["grunt"] = Variants(2, i => Grunt(r));
            d["hiss"] = One(Hiss(r));
            d["clicks"] = One(Clicks(r));
            d["boom"] = One(Boom(r));
            d["bubbles"] = One(Bubbles(r));
            return d;
        }

        static float[][] One(float[] a) => new[] { a };
        static float[][] Variants(int n, Func<int, float[]> make)
        {
            var v = new float[n][];
            for (int i = 0; i < n; i++) v[i] = make(i);
            return v;
        }

        static float[] Step(Synth.Rng r, float dur, float thumpHz, float noiseCoef, float thumpAmp)
        {
            var b = Synth.Buffer(dur);
            Synth.Drum(b, 0f, thumpHz * r.Range(0.9f, 1.1f), thumpAmp, 30f);
            Synth.FilteredNoise(b, 0, b.Length, t => noiseCoef, t => 0.7f * Mathf.Exp(-t * 35f) * Mathf.Clamp01(t * 300f), r);
            return Synth.Master(b, 0.7f);
        }

        static float[] Crunch(Synth.Rng r)
        {
            var b = Synth.Buffer(0.2f);
            for (int g = 0; g < 9; g++)
            {
                int s0 = (int)(r.Range(0f, 0.11f) * Synth.Rate);
                Synth.FilteredNoise(b, s0, (int)(0.006f * Synth.Rate), t => 0.6f, t => r.Range(0.4f, 0.9f), r, true);
            }
            Synth.FilteredNoise(b, 0, b.Length, t => 0.05f, t => 0.5f * Mathf.Exp(-t * 25f), r);
            return Synth.Master(b, 0.6f);
        }

        static float[] Knock(Synth.Rng r, float f)
        {
            var b = Synth.Buffer(0.16f);
            Synth.Drum(b, 0f, f, 0.6f, 32f);
            Synth.Drum(b, 0f, f * 2.3f, 0.25f, 45f);
            Synth.FilteredNoise(b, 0, b.Length, t => 0.4f, t => 0.3f * Mathf.Exp(-t * 90f), r, true);
            return Synth.Master(b, 0.65f);
        }

        static float[] Whoosh(Synth.Rng r, float dur, float c0, float c1, float amp)
        {
            var b = Synth.Buffer(dur);
            Synth.FilteredNoise(b, 0, b.Length, t => Mathf.Lerp(c0, c1, Mathf.Sin(Mathf.PI * t / dur)), t => amp * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur)), r);
            return Synth.Master(b, 0.6f);
        }

        static float[] Thump(Synth.Rng r, float dur, float f, float amp)
        {
            var b = Synth.Buffer(dur);
            Synth.Drum(b, 0f, f, amp, 14f, 0.4f, r);
            Synth.FilteredNoise(b, 0, b.Length, t => 0.08f, t => 0.5f * Mathf.Exp(-t * 18f), r);
            return Synth.Master(b, 0.8f);
        }

        static float[] GlideOpen(Synth.Rng r)
        {
            var b = Synth.Buffer(0.7f);
            for (int k = 0; k < 2; k++)
                Synth.FilteredNoise(b, (int)(k * 0.12f * Synth.Rate), (int)(0.1f * Synth.Rate), t => 0.25f, t => 0.8f * Mathf.Exp(-t * 30f), r);
            Synth.FilteredNoise(b, 0, b.Length, t => 0.06f + 0.1f * t, t => 0.5f * Mathf.Sin(Mathf.PI * t / 0.7f), r);
            return Synth.Master(b, 0.65f);
        }

        static float[] WindLoop(Synth.Rng r)
        {
            var b = Synth.Buffer(3.5f);
            Synth.FilteredNoise(b, 0, b.Length, t => 0.08f + 0.04f * Mathf.Sin(t * 2.2f), t => 0.7f + 0.3f * Mathf.Sin(t * 1.3f), r);
            return Synth.Master(Synth.CrossfadeLoop(b, Synth.Rate / 2), 0.6f);
        }

        static float[] Chime(float[] notes, float gap, Synth.Rng r, bool pop, bool sparkle = false)
        {
            var b = Synth.Buffer(notes.Length * gap + 0.9f);
            if (pop) Synth.Sweep(b, 0f, 500f, 950f, 0.06f, 0.5f);
            for (int i = 0; i < notes.Length; i++) Synth.Bell(b, i * gap, M((int)notes[i]), 0.5f, 0.8f, 6f);
            if (sparkle) for (int i = 0; i < 8; i++) Synth.Bell(b, r.Range(0.05f, 0.4f), M(96 + r.Int(0, 8)), 0.08f, 0.5f, 9f);
            return Synth.Master(b, 0.7f);
        }

        static float[] Acorn(Synth.Rng r)
        {
            var b = Synth.Buffer(0.5f);
            Synth.Drum(b, 0f, 720f, 0.5f, 40f);
            Synth.Drum(b, 0f, 1180f, 0.25f, 55f);
            Synth.Marimba(b, 0.04f, M(79), 0.45f, 0.45f);
            return Synth.Master(b, 0.7f);
        }

        static float[] Coin()
        {
            // Two quick metallic hits, the second higher: the classic clink of a gold coin
            var b = Synth.Buffer(0.7f);
            float[] parts = { 1f, 2.76f, 5.4f };
            for (int hit = 0; hit < 2; hit++)
            {
                float f = hit == 0 ? 1975f : 2637f, t0 = hit * 0.075f;
                int s0 = (int)(t0 * Synth.Rate);
                for (int i = 0; s0 + i < b.Length; i++)
                {
                    float t = i / (float)Synth.Rate;
                    float s = 0f;
                    for (int p = 0; p < parts.Length; p++) s += Mathf.Sin(2f * Mathf.PI * f * parts[p] * t) * Mathf.Exp(-t * (7f + p * 6f)) / (p + 1);
                    b[s0 + i] += 0.5f * s;
                }
            }
            return Synth.Master(b, 0.65f);
        }

        /// <summary>
        /// The classic coin-collect "cha-ching": two quick, bright metallic notes a fourth apart (B6 then E7),
        /// each with a struck attack and ringing coin partials, plus a faint glint of shimmer.
        /// Three slightly different takes so rapid pickups don't sound robotic.
        /// </summary>
        static float[] CoinCollect(Synth.Rng r, int take)
        {
            var b = Synth.Buffer(0.75f);
            float[] parts = { 1f, 2.0f, 2.76f, 4.1f }, amps = { 1f, 0.35f, 0.3f, 0.12f };
            float detune = 1f + (take - 1) * 0.008f;
            float[] notes = { 1975.5f * detune, 2637f * detune };
            float[] starts = { 0f, 0.07f }, gains = { 0.55f, 0.75f };
            for (int nIdx = 0; nIdx < 2; nIdx++)
            {
                int s0 = (int)(starts[nIdx] * Synth.Rate);
                for (int i = 0; s0 + i < b.Length; i++)
                {
                    float t = i / (float)Synth.Rate;
                    float att = 1f - Mathf.Exp(-t * 2500f);
                    float s = 0f;
                    for (int p = 0; p < parts.Length; p++)
                        s += amps[p] * Mathf.Sin(2f * Mathf.PI * notes[nIdx] * parts[p] * t) * Mathf.Exp(-t * (nIdx == 0 ? 14f : 5.5f) * (1f + p * 0.8f));
                    b[s0 + i] += gains[nIdx] * att * s;
                }
                // The tiny "tick" of metal on metal at each strike
                Synth.FilteredNoise(b, s0, (int)(0.012f * Synth.Rate), t => 0.7f, t => 0.25f * Mathf.Exp(-t * 300f), r, true);
            }
            for (int i = 0; i < 4; i++) Synth.Bell(b, r.Range(0.09f, 0.25f), Synth.Midi(100 + r.Int(0, 6)), 0.05f, 0.4f, 10f);
            return Synth.Master(b, 0.7f);
        }

        static float[] Glass(Synth.Rng r)
        {
            // A clear glass "ting" (inharmonic partials, long ring) and a little slosh of water
            var b = Synth.Buffer(0.9f);
            float[] p = { 1820f, 4150f, 6310f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Synth.Rate;
                float s = 0f;
                for (int j = 0; j < p.Length; j++) s += Mathf.Sin(2f * Mathf.PI * p[j] * t) * Mathf.Exp(-t * (4f + j * 3f)) / (j + 1);
                b[i] += 0.45f * s;
            }
            Synth.FilteredNoise(b, (int)(0.05f * Synth.Rate), (int)(0.3f * Synth.Rate), t => 0.08f + 0.1f * Mathf.Sin(t * 40f), t => 0.35f * Mathf.Sin(Mathf.PI * t / 0.3f), r);
            for (int i = 0; i < 3; i++) Synth.Sweep(b, 0.08f + i * 0.06f, r.Range(500f, 700f), r.Range(900f, 1300f), 0.04f, 0.12f);
            return Synth.Master(b, 0.65f);
        }

        static float[] Crack(Synth.Rng r)
        {
            var b = Synth.Buffer(0.5f);
            Synth.FilteredNoise(b, 0, b.Length, t => 0.5f, t => Mathf.Exp(-t * 25f), r, true);
            Snaps(b, r);
            return Synth.Master(b, 0.8f);
        }

        static void Snaps(float[] b, Synth.Rng r)
        {
            for (int k = 0; k < 3; k++) Synth.Drum(b, k * 0.03f, r.Range(300f, 500f), 0.4f, 35f);
        }

        static float[] Stumble(Synth.Rng r)
        {
            var b = Synth.Buffer(0.45f);
            Synth.Drum(b, 0f, 90f, 0.6f, 16f, 0.4f, r);
            Synth.FilteredNoise(b, (int)(0.05f * Synth.Rate), (int)(0.3f * Synth.Rate), t => 0.12f, t => 0.5f * Mathf.Exp(-t * 9f), r);
            return Synth.Master(b, 0.8f);
        }

        static float[] Crash(Synth.Rng r)
        {
            var b = Synth.Buffer(1.6f);
            Synth.Drum(b, 0f, 48f, 1f, 4f, 0.5f, r);
            Synth.FilteredNoise(b, 0, b.Length, t => 0.06f, t => 0.8f * Mathf.Exp(-t * 6f), r);
            // A low, falling sting
            Synth.Pad(b, 0.05f, M(38), 1.4f, 0.25f, 600f, r);
            Synth.Pad(b, 0.05f, M(44), 1.4f, 0.18f, 600f, r);
            return Synth.Master(b, 0.9f);
        }

        static float[] WorldSting(Synth.Rng r)
        {
            var b = Synth.Buffer(2.4f);
            Synth.FilteredNoise(b, 0, (int)(0.8f * Synth.Rate), t => 0.02f + 0.3f * t, t => 0.5f * t / 0.8f, r);
            foreach (int n in new[] { 72, 76, 79, 84 }) Synth.Bell(b, 0.75f, M(n), 0.3f, 1.6f, 1.6f);
            Synth.Reverb(b, 0.4f, 0.8f);
            return Synth.Master(b, 0.75f);
        }

        static float[] Click()
        {
            var b = Synth.Buffer(0.05f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Synth.Rate;
                b[i] = Mathf.Sin(2f * Mathf.PI * 1250f * t) * Mathf.Exp(-t * 120f);
            }
            return Synth.Master(b, 0.5f);
        }

        // ---- Animals ----

        static float[] Chatter(Synth.Rng r)
        {
            // "Ooh-ooh-aah-aah": rapid rising and falling calls
            var b = Synth.Buffer(1.1f);
            int calls = r.Int(6, 10);
            float t = 0f;
            for (int i = 0; i < calls && t < 0.95f; i++)
            {
                float f = r.Range(650f, 1150f), dur = r.Range(0.06f, 0.11f);
                Synth.Sweep(b, t, f, f * r.Range(1.2f, 1.6f), dur, 0.5f, 30f, 0.05f, 0.5f);
                t += dur + r.Range(0.02f, 0.06f);
            }
            return Synth.Master(b, 0.6f);
        }

        static float[] Shriek(Synth.Rng r)
        {
            var b = Synth.Buffer(0.8f);
            Synth.Voice(b, 0f, 0.75f, u => 1300f + 700f * Mathf.Sin(Mathf.PI * u) - 300f * u, 0.8f, 0.35f, 45f, 0.25f, 0.6f, r, 6);
            return Synth.Master(b, 0.7f);
        }

        static float[] TarzanYell(Synth.Rng r)
        {
            // The jungle yell: a long, rising holler that breaks into a yodel, flipping between chest and
            // head voice, and trails off.
            var b = Synth.Buffer(2.1f);
            Synth.Voice(b, 0f, 2f, u =>
            {
                float baseF = Mathf.Lerp(250f, 300f, Mathf.Clamp01(u * 4f));
                if (u < 0.22f) return baseF * (1f + 0.5f * u / 0.22f);                     // the rising "aaa"
                float flip = Mathf.Repeat((u - 0.22f) * 9f, 1f) < 0.5f ? 1f : 1.68f;          // yodel breaks
                return baseF * 1.5f * flip * (1f - 0.15f * Mathf.Clamp01((u - 0.8f) * 5f));
            }, 0.85f, 0.2f, 7f, 0.15f, 0.42f, r, 10);
            Synth.Reverb(b, 0.35f, 0.82f);
            return Synth.Master(b, 0.75f);
        }

        static float[] Roar(Synth.Rng r, float pitch)
        {
            var b = Synth.Buffer(1.6f);
            Synth.Voice(b, 0f, 1.5f, u => pitch * (85f + 70f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.4f)) - 20f * u), 0.9f, 0.55f, 28f, 0.6f, 0.22f, r);
            return Synth.Master(b, 0.85f);
        }

        static float[] Trumpet(Synth.Rng r)
        {
            var b = Synth.Buffer(1.4f);
            Synth.Voice(b, 0f, 1.3f, u => (300f + 260f * Mathf.SmoothStep(0f, 1f, u * 2f)) * (1f + 0.025f * Mathf.Sin(u * 40f)), 0.8f, 0.15f, 60f, 0.25f, 0.55f, r, 16);
            Synth.Reverb(b, 0.2f, 0.7f);
            return Synth.Master(b, 0.8f);
        }

        static float[] Groan(Synth.Rng r)
        {
            var b = Synth.Buffer(1.6f);
            Synth.Voice(b, 0f, 1.5f, u => 80f + 25f * Mathf.Sin(u * 7f) + 15f * u, 0.9f, 0.75f, 17f, 0.4f, 0.18f, r);
            for (int i = 0; i < 6; i++) Synth.Sweep(b, r.Range(0.2f, 1.2f), r.Range(150f, 250f), r.Range(300f, 450f), 0.05f, 0.15f);
            return Synth.Master(b, 0.8f);
        }

        static float[] Grunt(Synth.Rng r)
        {
            var b = Synth.Buffer(0.6f);
            Synth.Voice(b, 0f, 0.55f, u => 70f + 30f * Mathf.Sin(Mathf.PI * u), 0.9f, 0.6f, 22f, 0.5f, 0.2f, r);
            return Synth.Master(b, 0.8f);
        }

        static float[] Hiss(Synth.Rng r)
        {
            var b = Synth.Buffer(1f);
            Synth.FilteredNoise(b, 0, b.Length, t => 0.3f, t => Mathf.Clamp01(t * 8f) * Mathf.Clamp01((1f - t) * 3f), r, true);
            return Synth.Master(b, 0.55f);
        }

        static float[] Clicks(Synth.Rng r)
        {
            var b = Synth.Buffer(0.7f);
            for (int i = 0; i < 9; i++)
                Synth.FilteredNoise(b, (int)(r.Range(0f, 0.6f) * Synth.Rate), (int)(0.008f * Synth.Rate), t => 0.5f, t => r.Range(0.5f, 1f), r, true);
            return Synth.Master(b, 0.55f);
        }

        static float[] Boom(Synth.Rng r)
        {
            // An ostrich's deep, booming call: three hollow low notes
            var b = Synth.Buffer(1.3f);
            for (int i = 0; i < 3; i++) Synth.Drum(b, i * 0.35f, 68f - i * 4f, 0.8f, 5f);
            return Synth.Master(b, 0.8f);
        }

        static float[] Bubbles(Synth.Rng r)
        {
            var b = Synth.Buffer(0.9f);
            for (int i = 0; i < 12; i++)
            {
                float f = r.Range(250f, 500f);
                Synth.Sweep(b, r.Range(0f, 0.8f), f, f * 2.2f, r.Range(0.03f, 0.06f), 0.4f);
            }
            return Synth.Master(b, 0.6f);
        }
    }
}
