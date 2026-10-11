using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleEscape
{
    /// <summary>
    /// Procedural mesh generators: swept tubes (trunks, limbs, bananas), lathes (heads,
    /// hats, idols), noisy blobs (rocks, canopies), rounded boxes and leaf strips.
    /// </summary>
    public static class Meshes
    {
        // ---------------- Tubes ----------------

        /// <summary>
        /// Sweeps a circle along a path. radius(t, angle) lets the cross-section vary
        /// (taper, buttress roots...). UV.x wraps around, UV.y runs along the length.
        /// </summary>
        public static Mesh Sweep(IList<Vector3> path, Func<float, float, float> radius, int sides,
                                 float uRepeat = 1f, float vPerMeter = 0.5f, bool capEnd = false, string name = "Sweep")
        {
            int rings = path.Count;
            var verts = new List<Vector3>(rings * (sides + 1) + sides + 2);
            var norms = new List<Vector3>(verts.Capacity);
            var uvs = new List<Vector2>(verts.Capacity);
            var tris = new List<int>((rings - 1) * sides * 6 + sides * 3);

            Vector3 n = Vector3.zero;
            float len = 0f;
            for (int i = 0; i < rings; i++)
            {
                Vector3 tan = (path[Mathf.Min(i + 1, rings - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                n = i == 0 ? Perp(tan) : Vector3.ProjectOnPlane(n, tan).normalized;
                if (n.sqrMagnitude < 0.5f) n = Perp(tan);
                Vector3 b = Vector3.Cross(tan, n);
                if (i > 0) len += Vector3.Distance(path[i], path[i - 1]);
                float t = rings > 1 ? i / (float)(rings - 1) : 0f;

                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = Mathf.Cos(a) * n + Mathf.Sin(a) * b;
                    verts.Add(path[i] + dir * radius(t, a));
                    norms.Add(dir);
                    uvs.Add(new Vector2(s / (float)sides * uRepeat, len * vPerMeter));
                }
            }

            for (int i = 0; i < rings - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = i * (sides + 1) + s, b = a + 1, c = a + sides + 1, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }

            if (capEnd)
            {
                Vector3 end = path[rings - 1];
                Vector3 endDir = (path[rings - 1] - path[rings - 2]).normalized;
                int center = verts.Count;
                verts.Add(end); norms.Add(endDir); uvs.Add(new Vector2(0.5f, len * vPerMeter));
                int ring = (rings - 1) * (sides + 1);
                for (int s = 0; s < sides; s++)
                {
                    tris.Add(center); tris.Add(ring + s + 1); tris.Add(ring + s);
                }
            }

            return Finish(name, verts, norms, uvs, tris, true);
        }

        /// <summary>Simple tapered tube between two points.</summary>
        public static Mesh Limb(float length, float r0, float r1, int sides = 10, int rings = 8, float bend = 0f)
        {
            var path = new List<Vector3>();
            for (int i = 0; i < rings; i++)
            {
                float t = i / (float)(rings - 1);
                path.Add(new Vector3(0f, -t * length, Mathf.Sin(t * Mathf.PI) * bend));
            }
            return Sweep(path, (t, a) => Mathf.Lerp(r0, r1, t) * Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Pow(Mathf.Abs(t * 2f - 1f), 8f))),
                         sides, 1f, 1f, false, "Limb");
        }

        // ---------------- Lathe ----------------

        /// <summary>Revolves a (radius, height) profile around Y. Profile runs bottom to top.</summary>
        public static Mesh Lathe(IList<Vector2> profile, int sides, float uRepeat = 1f, string name = "Lathe")
        {
            int rings = profile.Count;
            var verts = new List<Vector3>(rings * (sides + 1));
            var norms = new List<Vector3>(verts.Capacity);
            var uvs = new List<Vector2>(verts.Capacity);
            var tris = new List<int>();

            float total = 0f;
            for (int i = 1; i < rings; i++) total += Vector2.Distance(profile[i], profile[i - 1]);
            float len = 0f;
            for (int i = 0; i < rings; i++)
            {
                if (i > 0) len += Vector2.Distance(profile[i], profile[i - 1]);
                Vector2 tan = (profile[Mathf.Min(i + 1, rings - 1)] - profile[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 n2 = new Vector2(tan.y, -tan.x);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    verts.Add(dir * profile[i].x + Vector3.up * profile[i].y);
                    norms.Add((dir * n2.x + Vector3.up * n2.y).normalized);
                    uvs.Add(new Vector2(s / (float)sides * uRepeat, total > 0f ? len / total : 0f));
                }
            }
            for (int i = 0; i < rings - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = i * (sides + 1) + s, b = a + 1, c = a + sides + 1, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            return Finish(name, verts, norms, uvs, tris, true);
        }

        /// <summary>Ellipsoid-ish lathe used for heads, bodies and fruit.</summary>
        public static Mesh Ball(int rings = 10, int sides = 16)
        {
            var p = new List<Vector2>();
            for (int i = 0; i <= rings; i++)
            {
                float a = -Mathf.PI / 2f + Mathf.PI * i / rings;
                p.Add(new Vector2(Mathf.Max(0.0001f, Mathf.Cos(a) * 0.5f), Mathf.Sin(a) * 0.5f));
            }
            return Lathe(p, sides, 1f, "Ball");
        }

        // ---------------- Blobs (rocks, canopies, bushes) ----------------

        /// <summary>
        /// Noise-displaced icosphere with smooth normals. rockUV maps V to normalized height
        /// (so the rock texture can put moss on top); otherwise V is latitude.
        /// </summary>
        public static Mesh Blob(int seed, int subdiv, float amp, float freq, float flattenBottom, bool rockUV)
        {
            Icosphere(subdiv, out var basePos, out var baseTris);
            var offset = new Vector3(seed * 3.17f, seed * 1.31f, seed * 2.71f);
            var pos = new Vector3[basePos.Length];
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < basePos.Length; i++)
            {
                Vector3 p = basePos[i];
                float d = (Noise.Fbm3(p * freq + offset, 3, seed) - 0.5f) * 2f;
                Vector3 q = p * 0.5f * (1f + amp * d);
                if (flattenBottom > 0f && q.y < -flattenBottom) q.y = -flattenBottom + (q.y + flattenBottom) * 0.15f;
                pos[i] = q;
                minY = Mathf.Min(minY, q.y);
                maxY = Mathf.Max(maxY, q.y);
            }

            // Smooth normals from the shared topology.
            var smooth = new Vector3[pos.Length];
            for (int t = 0; t < baseTris.Length; t += 3)
            {
                int a = baseTris[t], b = baseTris[t + 1], c = baseTris[t + 2];
                Vector3 fn = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]);
                smooth[a] += fn; smooth[b] += fn; smooth[c] += fn;
            }
            for (int i = 0; i < smooth.Length; i++) smooth[i].Normalize();

            // Unshare vertices per triangle so the spherical UV seam can be fixed cleanly.
            var verts = new List<Vector3>(baseTris.Length);
            var norms = new List<Vector3>(baseTris.Length);
            var uvs = new List<Vector2>(baseTris.Length);
            var tris = new List<int>(baseTris.Length);
            var tu = new float[3];
            for (int t = 0; t < baseTris.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    Vector3 bp = basePos[baseTris[t + k]];
                    tu[k] = Mathf.Atan2(bp.z, bp.x) / (Mathf.PI * 2f) + 0.5f;
                }
                if (Mathf.Max(tu[0], Mathf.Max(tu[1], tu[2])) - Mathf.Min(tu[0], Mathf.Min(tu[1], tu[2])) > 0.5f)
                    for (int k = 0; k < 3; k++) if (tu[k] < 0.5f) tu[k] += 1f;

                for (int k = 0; k < 3; k++)
                {
                    int vi = baseTris[t + k];
                    Vector3 p = pos[vi];
                    float v = rockUV ? Mathf.InverseLerp(minY, maxY, p.y)
                                     : Mathf.Acos(Mathf.Clamp(-basePos[vi].y, -1f, 1f)) / Mathf.PI;
                    tris.Add(verts.Count);
                    verts.Add(p);
                    norms.Add(smooth[vi]);
                    uvs.Add(new Vector2(tu[k], v));
                }
            }
            return Finish("Blob", verts, norms, uvs, tris, true);
        }

        static readonly Dictionary<int, (Vector3[] pos, int[] tris)> icoCache = new Dictionary<int, (Vector3[], int[])>();

        static void Icosphere(int subdiv, out Vector3[] positions, out int[] triangles)
        {
            if (icoCache.TryGetValue(subdiv, out var cached))
            {
                positions = cached.pos;
                triangles = cached.tris;
                return;
            }

            float g = (1f + Mathf.Sqrt(5f)) / 2f;
            var p = new List<Vector3>
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1)
            };
            for (int i = 0; i < p.Count; i++) p[i] = p[i].normalized;
            var t = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };

            for (int s = 0; s < subdiv; s++)
            {
                var mid = new Dictionary<long, int>();
                var nt = new List<int>(t.Count * 4);
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(key, out int idx)) return idx;
                    idx = p.Count;
                    p.Add(((p[a] + p[b]) * 0.5f).normalized);
                    mid[key] = idx;
                    return idx;
                }
                for (int i = 0; i < t.Count; i += 3)
                {
                    int a = t[i], b = t[i + 1], c = t[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                t = nt;
            }

            // Make sure every triangle faces outward for Unity's winding.
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = p[t[i]], b = p[t[i + 1]], c = p[t[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f)
                {
                    int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp;
                }
            }

            positions = p.ToArray();
            triangles = t.ToArray();
            icoCache[subdiv] = (positions, triangles);
        }

        // ---------------- Rounded box ----------------

        public static Mesh RoundedBox(Vector3 size, float radius, int seg = 5)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            Vector3 half = size * 0.5f;
            Vector3 inner = new Vector3(Mathf.Max(0f, half.x - radius), Mathf.Max(0f, half.y - radius), Mathf.Max(0f, half.z - radius));
            Vector3[] faceN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

            foreach (var fn in faceN)
            {
                Vector3 fu = Mathf.Abs(fn.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, fn);
                Vector3 fv = Vector3.Cross(fn, fu);
                int start = verts.Count;
                for (int j = 0; j <= seg; j++)
                    for (int i = 0; i <= seg; i++)
                    {
                        float a = i / (float)seg - 0.5f, b = j / (float)seg - 0.5f;
                        Vector3 p = Vector3.Scale(fn * 0.5f + fu * a + fv * b, size);
                        Vector3 c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 n = (p - c).sqrMagnitude > 1e-8f ? (p - c).normalized : fn;
                        verts.Add(c + n * radius);
                        norms.Add(n);
                        uvs.Add(new Vector2(i / (float)seg, j / (float)seg));
                    }
                for (int j = 0; j < seg; j++)
                    for (int i = 0; i < seg; i++)
                    {
                        int a = start + j * (seg + 1) + i, b = a + 1, c = a + seg + 1, d = c + 1;
                        AddQuadFacing(tris, verts, a, b, c, d, fn);
                    }
            }
            return Finish("RoundedBox", verts, norms, uvs, tris, true);
        }

        // ---------------- Foliage ----------------

        /// <summary>
        /// A bending leaf strip along +Z (base at origin). The texture defines the leaf outline.
        /// droop pulls the tip down, fold raises the midrib to give the leaf a V shape.
        /// </summary>
        public static Mesh Leaf(float length, float width, float droop, float fold, int segs = 6, float twist = 0f)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                float z = t * length;
                float y = -droop * t * t;
                float tw = twist * t;
                for (int j = 0; j < 3; j++)
                {
                    float x = (j - 1) * width * 0.5f;
                    float yy = y + (j == 1 ? fold * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.3f)) : 0f) + x * tw;
                    verts.Add(new Vector3(x, yy, z));
                    uvs.Add(new Vector2(j * 0.5f, t));
                }
            }
            for (int i = 0; i < segs; i++)
                for (int j = 0; j < 2; j++)
                {
                    int a = i * 3 + j, b = a + 1, c = a + 3, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            return Finish("Leaf", verts, null, uvs, tris, true);
        }

        /// <summary>Crossed vertical cards for grass tufts. Normals point up so tufts light like the ground.</summary>
        public static Mesh Tuft(float width, float height, int cards = 3)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int k = 0; k < cards; k++)
            {
                float a = k * Mathf.PI / cards;
                var right = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (width * 0.5f);
                int s = verts.Count;
                verts.Add(-right); verts.Add(right);
                verts.Add(-right * 1.15f + Vector3.up * height); verts.Add(right * 1.15f + Vector3.up * height);
                for (int i = 0; i < 4; i++) norms.Add(Vector3.up);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
                tris.Add(s + 1); tris.Add(s + 2); tris.Add(s + 3);
            }
            return Finish("Tuft", verts, norms, uvs, tris, true);
        }

        /// <summary>Flat disc facing +Y with planar UVs (log end grain).</summary>
        public static Mesh Disc(int sides = 20)
        {
            var verts = new List<Vector3> { Vector3.zero };
            var norms = new List<Vector3> { Vector3.up };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var tris = new List<int>();
            for (int s = 0; s <= sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int s = 1; s <= sides; s++) { tris.Add(0); tris.Add(s + 1); tris.Add(s); }
            var m = Finish("Disc", verts, norms, uvs, tris, true);
            var cols = new List<Color>(verts.Count);
            for (int i = 0; i < verts.Count; i++) cols.Add(Color.white); // lets particle shaders use it too
            m.SetColors(cols);
            return m;
        }

        /// <summary>Upright quad facing -Z (toward the chase camera), pivot at its top centre.</summary>
        public static Mesh Billboard(float width, float height)
        {
            var verts = new List<Vector3>
            {
                new Vector3(-width / 2, -height, 0), new Vector3(width / 2, -height, 0),
                new Vector3(-width / 2, 0, 0), new Vector3(width / 2, 0, 0)
            };
            var norms = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            var uvs = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            var tris = new List<int> { 0, 2, 1, 1, 2, 3 };
            var m = Finish("Billboard", verts, norms, uvs, tris, false);
            m.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            return m;
        }

        // ---------------- Combining ----------------

        public static Mesh Combine(List<CombineInstance> parts, string name)
        {
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.CombineMeshes(parts.ToArray(), true, true);
            m.RecalculateBounds();
            return m;
        }

        // ---------------- Helpers ----------------

        static Vector3 Perp(Vector3 t) =>
            Mathf.Abs(t.y) < 0.99f ? Vector3.Cross(t, Vector3.up).normalized : Vector3.Cross(t, Vector3.right).normalized;

        static void AddQuadFacing(List<int> tris, List<Vector3> verts, int a, int b, int c, int d, Vector3 facing)
        {
            Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(n, facing) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
            else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
        }

        static Mesh Finish(string name, List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, bool tangents)
        {
            // Never hand the GPU a NaN/infinite vertex: it produces invalid bounds and can crash the editor.
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 v = verts[i];
                if (!float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z))
                    verts[i] = i > 0 ? verts[i - 1] : Vector3.zero;
            }
            if (norms != null)
                for (int i = 0; i < norms.Count; i++)
                {
                    Vector3 n = norms[i];
                    if (!float.IsFinite(n.x) || !float.IsFinite(n.y) || !float.IsFinite(n.z) || n.sqrMagnitude < 1e-8f)
                        norms[i] = Vector3.up;
                }

            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            if (norms != null) m.SetNormals(norms); else m.RecalculateNormals();
            if (tangents) m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }
    }
}
