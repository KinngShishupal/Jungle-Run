using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// Small, allocation-free noise toolkit used to paint textures and shape meshes.
    /// The 2D functions tile seamlessly over the unit square (pass a whole-number period).
    /// </summary>
    public static class Noise
    {
        static uint Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 2147483647 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        public static float Hash01(int x, int y, int seed) => (Hash(x, y, 0, seed) & 0xFFFFFF) / 16777215f;
        static float Hash01(int x, int y, int z, int seed) => (Hash(x, y, z, seed) & 0xFFFFFF) / 16777215f;

        static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        static int Wrap(int i, int p) => ((i % p) + p) % p;

        /// <summary>Tileable value noise in [0,1]. x,y are in lattice units, period in cells.</summary>
        public static float Value(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = Smooth(x - x0), fy = Smooth(y - y0);
            int ax = Wrap(x0, period), bx = Wrap(x0 + 1, period);
            int ay = Wrap(y0, period), by = Wrap(y0 + 1, period);
            float a = Hash01(ax, ay, seed), b = Hash01(bx, ay, seed);
            float c = Hash01(ax, by, seed), d = Hash01(bx, by, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Tileable fractal noise in [0,1] over u,v in [0,1).</summary>
        public static float Fbm(float u, float v, int period, int octaves, int seed, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int p = period;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(u * p, v * p, p, seed + i * 31) * amp;
                norm += amp;
                amp *= gain;
                p *= 2;
            }
            return sum / norm;
        }

        /// <summary>Anisotropic tileable fBm: different periods along u and v (bark, fur, grain).</summary>
        public static float FbmXY(float u, float v, int periodU, int periodV, int octaves, int seed, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int pu = periodU, pv = periodV;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value2(u * pu, v * pv, pu, pv, seed + i * 31) * amp;
                norm += amp;
                amp *= gain;
                pu *= 2; pv *= 2;
            }
            return sum / norm;
        }

        static float Value2(float x, float y, int pu, int pv, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = Smooth(x - x0), fy = Smooth(y - y0);
            int ax = Wrap(x0, pu), bx = Wrap(x0 + 1, pu);
            int ay = Wrap(y0, pv), by = Wrap(y0 + 1, pv);
            float a = Hash01(ax, ay, seed), b = Hash01(bx, ay, seed);
            float c = Hash01(ax, by, seed), d = Hash01(bx, by, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>
        /// Tileable cellular (Worley) noise. Returns distance to nearest (f1) and second nearest (f2)
        /// feature point in cell units, plus a random id for the nearest cell.
        /// </summary>
        public static void Cells(float u, float v, int period, int seed, out float f1, out float f2, out float id)
        {
            float x = u * period, y = v * period;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            f1 = f2 = 9f; id = 0f;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int gx = cx + i, gy = cy + j;
                    int wx = Wrap(gx, period), wy = Wrap(gy, period);
                    float px = gx + Hash01(wx, wy, seed);
                    float py = gy + Hash01(wx, wy, seed + 7);
                    float dx = px - x, dy = py - y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < f1) { f2 = f1; f1 = d; id = Hash01(wx, wy, seed + 13); }
                    else if (d < f2) f2 = d;
                }
        }

        /// <summary>Smooth 3D value noise in [0,1] (not tiled) for displacing meshes.</summary>
        public static float Value3(Vector3 p, int seed)
        {
            int x0 = Mathf.FloorToInt(p.x), y0 = Mathf.FloorToInt(p.y), z0 = Mathf.FloorToInt(p.z);
            float fx = Smooth(p.x - x0), fy = Smooth(p.y - y0), fz = Smooth(p.z - z0);
            float c000 = Hash01(x0, y0, z0, seed), c100 = Hash01(x0 + 1, y0, z0, seed);
            float c010 = Hash01(x0, y0 + 1, z0, seed), c110 = Hash01(x0 + 1, y0 + 1, z0, seed);
            float c001 = Hash01(x0, y0, z0 + 1, seed), c101 = Hash01(x0 + 1, y0, z0 + 1, seed);
            float c011 = Hash01(x0, y0 + 1, z0 + 1, seed), c111 = Hash01(x0 + 1, y0 + 1, z0 + 1, seed);
            float x00 = Mathf.Lerp(c000, c100, fx), x10 = Mathf.Lerp(c010, c110, fx);
            float x01 = Mathf.Lerp(c001, c101, fx), x11 = Mathf.Lerp(c011, c111, fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        public static float Fbm3(Vector3 p, int octaves, int seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value3(p, seed + i * 17) * amp;
                norm += amp;
                amp *= 0.5f;
                p *= 2.03f;
            }
            return sum / norm;
        }

        /// <summary>Non-tiled 2D fBm for world-space terrain heights.</summary>
        public static float World(float x, float z, float scale, int octaves, int seed)
            => Fbm3(new Vector3(x * scale, 0.5f, z * scale), octaves, seed);
    }
}
