using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    public enum PickupType
    {
        Banana,   // currency + streak
        Magnet,   // Vine Magnet: pulls nearby bananas
        Glider,   // Leaf Glider: float above everything
        Shield,   // Coconut Helmet: survive one crash
        Bonanza   // Golden Idol: double bananas
    }

    public class Pickup : MonoBehaviour
    {
        const float MagnetRadius = 10f;

        public PickupType Type;

        Transform visual;
        float phase;

        void Update()
        {
            float dt = Time.deltaTime;
            visual.Rotate(0f, (Type == PickupType.Banana ? 180f : 90f) * dt, 0f, Space.World);

            if (Type != PickupType.Banana)
            {
                visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 3f + phase) * 0.15f, 0f);
                return;
            }

            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing || !gm.Player.Attracting) return;

            Vector3 target = gm.Player.transform.position + Vector3.up * 0.9f;
            if ((transform.position - target).sqrMagnitude < MagnetRadius * MagnetRadius)
                transform.position = Vector3.MoveTowards(transform.position, target, (30f + gm.Speed) * dt);
        }


        public void Collect()
        {
            if (!gameObject.activeSelf) return;
            Fx.Sparkle(visual.position, GlowColor(Type), Type == PickupType.Banana ? 6 : 24);
            GameManager.I.OnPickup(this);
            gameObject.SetActive(false);
        }

        // ---------------- Factory ----------------

        public static Pickup Create(PickupType type, Vector3 pos, Transform parent)
        {
            var go = new GameObject(type.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = type == PickupType.Banana ? 0.6f : 0.8f;

            var p = go.AddComponent<Pickup>();
            p.Type = type;
            p.phase = Random.value * 6f;
            p.visual = Prim.Group("Visual", go.transform, Vector3.zero);
            var v = p.visual;
            BuildMeshes();

            switch (type)
            {
                case PickupType.Banana:
                    Prim.Shape(bananaMesh, "Banana", v, Vector3.zero, Vector3.one, Mats.Banana, new Vector3(0f, 0f, 18f));
                    break;

                case PickupType.Magnet:
                    // Vine Magnet: an amethyst seed wrapped in a living vine
                    Prim.Shape(ballMesh, "Gem", v, Vector3.zero, Vector3.one * 0.46f, Mats.Gem);
                    Prim.Shape(ringMesh, "VineRing", v, Vector3.zero, Vector3.one, Mats.Vine, new Vector3(70f, 0f, 0f));
                    Prim.Shape(ringMesh, "VineRing2", v, Vector3.zero, Vector3.one * 0.92f, Mats.Vine, new Vector3(-50f, 60f, 0f));
                    for (int i = 0; i < 3; i++)
                        Prim.Shape(smallLeaf, "Leaf", v, Quaternion.Euler(0f, i * 120f, 0f) * new Vector3(0.34f, 0.05f, 0f), Vector3.one,
                                   Mats.BroadLeaf, new Vector3(-20f, i * 120f + 90f, 0f), false);
                    break;

                case PickupType.Glider:
                    Prim.Shape(gliderLeaf, "Leaf", v, new Vector3(0f, 0f, -0.65f), Vector3.one, Mats.BroadLeaf, new Vector3(-8f, 0f, 15f));
                    break;

                case PickupType.Shield:
                    Prim.Shape(ballMesh, "Coconut", v, Vector3.zero, new Vector3(0.72f, 0.78f, 0.72f), Mats.Coconut);
                    var dark = Mats.Plain(new Color(0.12f, 0.08f, 0.05f), 0.5f);
                    Prim.Shape(ballMesh, "Eye1", v, new Vector3(-0.1f, 0.24f, -0.24f), Vector3.one * 0.09f, dark);
                    Prim.Shape(ballMesh, "Eye2", v, new Vector3(0.1f, 0.24f, -0.24f), Vector3.one * 0.09f, dark);
                    Prim.Shape(ballMesh, "Eye3", v, new Vector3(0f, 0.31f, -0.16f), Vector3.one * 0.08f, dark);
                    break;

                case PickupType.Bonanza:
                    // Golden Idol with emerald eyes
                    Prim.Shape(idolMesh, "Idol", v, new Vector3(0f, -0.5f, 0f), Vector3.one * 1.1f, Mats.Gold);
                    var emerald = Mats.Emissive("Emerald", Tex.Metal, new Color(0.08f, 0.45f, 0.22f), 0.95f, new Color(0.15f, 1.3f, 0.5f));
                    Prim.Shape(ballMesh, "EyeL", v, new Vector3(-0.06f, 0.33f, -0.19f), Vector3.one * 0.06f, emerald);
                    Prim.Shape(ballMesh, "EyeR", v, new Vector3(0.06f, 0.33f, -0.19f), Vector3.one * 0.06f, emerald);
                    break;
            }

            // Soft halo so collectibles read clearly against the foliage (does not spin).
            float hs = type == PickupType.Banana ? 0.9f : 1.9f;
            Prim.Shape(haloMesh, "Halo", go.transform, new Vector3(0f, hs * 0.5f, 0.15f), Vector3.one * hs, HaloMat(type), null, false);
            return p;
        }

        // ---------------- Shared meshes & materials ----------------

        static Mesh bananaMesh, ballMesh, ringMesh, smallLeaf, gliderLeaf, idolMesh, haloMesh;
        static readonly Material[] haloMats = new Material[5];

        static void BuildMeshes()
        {
            if (bananaMesh != null) return;

            // Curved, tapered banana with a little stalk (V runs stalk -> tip for the texture).
            const float r = 0.33f;
            var path = new List<Vector3>();
            for (int i = 0; i <= 14; i++)
            {
                float a = Mathf.Lerp(-52f, 52f, i / 14f) * Mathf.Deg2Rad;
                path.Add(new Vector3(r * Mathf.Sin(a), r * (1f - Mathf.Cos(a)) - 0.07f, 0f));
            }
            float length = r * 104f * Mathf.Deg2Rad;
            bananaMesh = Meshes.Sweep(path, (t, ang) =>
                t < 0.08f ? 0.022f : Mathf.Max(0.014f, 0.078f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.InverseLerp(0.04f, 1f, t))), 0.55f)),
                10, 1f, 1f / length, true);

            ballMesh = Meshes.Ball(12, 18);

            var ring = new List<Vector3>();
            for (int i = 0; i <= 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                ring.Add(new Vector3(Mathf.Cos(a) * 0.38f, Mathf.Sin(a * 3f) * 0.03f, Mathf.Sin(a) * 0.38f));
            }
            ringMesh = Meshes.Sweep(ring, (t, a) => 0.032f, 6, 1f, 2f);

            smallLeaf = Meshes.Leaf(0.28f, 0.2f, 0.05f, 0.02f);
            gliderLeaf = Meshes.Leaf(1.3f, 0.95f, 0.22f, 0.12f, 8);

            idolMesh = Meshes.Lathe(new[]
            {
                new Vector2(0.001f, 0f), new Vector2(0.26f, 0f), new Vector2(0.27f, 0.07f), new Vector2(0.2f, 0.1f),
                new Vector2(0.21f, 0.3f), new Vector2(0.25f, 0.45f), new Vector2(0.17f, 0.6f), new Vector2(0.11f, 0.64f),
                new Vector2(0.17f, 0.7f), new Vector2(0.2f, 0.8f), new Vector2(0.18f, 0.92f), new Vector2(0.1f, 0.99f),
                new Vector2(0.001f, 1.0f)
            }, 20, 1f, "Idol");

            haloMesh = Meshes.Billboard(1f, 1f);
        }

        static Material HaloMat(PickupType t)
        {
            int i = (int)t;
            if (haloMats[i] != null) return haloMats[i];
            var m = new Material(Mats.ParticleAdd(Tex.Glow)) { name = "Halo" + t };
            Color c = GlowColor(t);
            c.a = t == PickupType.Banana ? 0.22f : 0.4f;
            m.SetColor("_BaseColor", c);
            return haloMats[i] = m;
        }

        static Color GlowColor(PickupType t)
        {
            switch (t)
            {
                case PickupType.Banana: return new Color(1f, 0.85f, 0.35f);
                case PickupType.Magnet: return new Color(0.8f, 0.4f, 1f);
                case PickupType.Glider: return new Color(0.55f, 1f, 0.45f);
                case PickupType.Shield: return new Color(1f, 0.7f, 0.4f);
                default: return new Color(1f, 0.82f, 0.3f);
            }
        }
    }
}
