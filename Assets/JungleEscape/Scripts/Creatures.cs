using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>The animal filling an obstacle role in a given world.</summary>
    public enum Creature { None, Python, Boar, Tiger, Bear, Mammoth, WoollyRhino, Sabertooth, Crab, Crocodile, Hippo, Octopus, Scorpion, Ostrich, Lion, Camel }

    /// <summary>
    /// World-specific creatures. Each world fills the same gameplay roles with its own animals:
    /// low crawler (jump over), charger, fast charger, and lane blocker.
    /// </summary>
    public static class Creatures
    {
        public static Creature For(ObstacleType role, BiomeType world)
        {
            switch (role)
            {
                case ObstacleType.Snake:
                    // In the desert: a giant scorpion, or a sand-coloured horned viper (the python, re-skinned).
                    if (world == BiomeType.Desert) return Random.value < 0.55f ? Creature.Scorpion : Creature.Python;
                    return world == BiomeType.WaterWorld ? Creature.Crab : Creature.Python;
                case ObstacleType.Boar:
                    return world == BiomeType.IceAge ? Creature.WoollyRhino : world == BiomeType.WaterWorld ? Creature.Crocodile
                         : world == BiomeType.Desert ? Creature.Ostrich : Creature.Boar;
                case ObstacleType.Tiger:
                    return world == BiomeType.IceAge ? Creature.Sabertooth : world == BiomeType.WaterWorld ? Creature.Hippo
                         : world == BiomeType.Desert ? Creature.Lion : Creature.Tiger;
                case ObstacleType.Bear:
                    return world == BiomeType.IceAge ? Creature.Mammoth : world == BiomeType.WaterWorld ? Creature.Octopus
                         : world == BiomeType.Desert ? Creature.Camel : Creature.Bear;
                default: return Creature.None;
            }
        }

        public static string CrashLine(Creature c)
        {
            switch (c)
            {
                case Creature.Mammoth: return "Squashed by a woolly mammoth!";
                case Creature.WoollyRhino: return "Gored by a woolly rhino!";
                case Creature.Sabertooth: return "Caught by a sabre-tooth cat!";
                case Creature.Crab: return "Pinched by a giant crab!";
                case Creature.Crocodile: return "Snapped up by a crocodile!";
                case Creature.Hippo: return "Flattened by a charging hippo!";
                case Creature.Octopus: return "Wrapped up by a giant octopus!";
                case Creature.Scorpion: return "Stung by a giant scorpion!";
                case Creature.Ostrich: return "Bowled over by an ostrich!";
                case Creature.Lion: return "Brought down by a desert lion!";
                case Creature.Camel: return "Ran straight into a camel!";
                default: return null;
            }
        }

        // ---------------- Shared parts ----------------

        static Mesh ball;
        static Mesh[] lumps;
        static readonly Dictionary<string, Mesh> limbs = new Dictionary<string, Mesh>();

        static Mesh Ball => ball != null ? ball : (ball = Meshes.Ball(12, 16));

        static Mesh Lump(int i)
        {
            if (lumps == null)
            {
                lumps = new Mesh[4];
                for (int k = 0; k < 4; k++) lumps[k] = Meshes.Blob(733 + k * 19, 2, 0.26f, 3.2f, 0f, false);
            }
            return lumps[((i % 4) + 4) % 4];
        }

        static Mesh Limb(float len, float r0, float r1)
        {
            string key = len + "_" + r0 + "_" + r1;
            if (!limbs.TryGetValue(key, out var m)) limbs[key] = m = Meshes.Limb(len, r0, r1, 10, 6);
            return m;
        }

        static Mesh Cone(float len, float r)
        {
            string key = "cone" + len + "_" + r;
            if (!limbs.TryGetValue(key, out var m))
                limbs[key] = m = Meshes.Lathe(new[] { new Vector2(r, 0f), new Vector2(r * 0.75f, len * 0.5f), new Vector2(r * 0.3f, len * 0.92f), new Vector2(0.001f, len) }, 10);
            return m;
        }

        static Mesh Curve(List<Vector3> path, float r0, float r1)
        {
            return Meshes.Sweep(path, (t, a) => Mathf.Lerp(r0, r1, t), 8, 1f, 2f, true);
        }

        static Material Fur(string key, Color c, float tiling = 4f) =>
            Mats.Lit(key, Tex.Fur, c, 0.14f, tiling: new Vector2(tiling, tiling), bump: 2.2f);

        static Material Hide(string key, Color c) =>
            Mats.Lit(key, Tex.Leather, c, 0.32f, tiling: new Vector2(3f, 3f), bump: 2.6f);

        static Material Glow(string key, Color c, Color emission) => Mats.Emissive(key, Tex.Metal, c, 0.95f, emission);

        static Material Ivory => Mats.Plain(new Color(0.9f, 0.86f, 0.74f), 0.5f);
        static Material Black => Mats.Plain(new Color(0.03f, 0.03f, 0.03f), 0.9f);

        static GameObject P(Mesh m, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null, bool shadows = true)
            => Prim.Shape(m, name, parent, pos, scale, mat, euler, shadows);

        // ---------------- Articulated legs ----------------

        /// <summary>A leg with a hip and a knee joint; the foot sits at the end of the lower segment.</summary>
        static void Leg(Transform parent, Vector3 hipPos, float upper, float lower, float r0, float r1, float r2,
                        Material mat, Material footMat, Vector3 footScale, out Transform hip, out Transform knee)
        {
            hip = Prim.Group("Hip", parent, hipPos);
            P(Limb(upper, r0, r1), "Upper", hip, Vector3.zero, Vector3.one, mat);
            knee = Prim.Group("Knee", hip, new Vector3(0f, -upper, 0f));
            P(Ball, "Joint", knee, Vector3.zero, Vector3.one * r1 * 2.1f, mat);
            P(Limb(lower, r1, r2), "Lower", knee, Vector3.zero, Vector3.one, mat);
            P(Ball, "Foot", knee, new Vector3(0f, -lower, -0.03f), footScale, footMat);
        }

        /// <summary>Flattened mounds of snow resting on an animal's back.</summary>
        static void SnowOnBack(Transform parent, Vector3 pos, Vector3 size)
        {
            P(Lump(3), "Snow", parent, pos, size, Mats.Snow, null, false);
        }

        // ---------------- Big cats (fast chargers): sabre-tooth & desert lion ----------------

        public enum CatKind { Sabertooth, Lion }

        public static void BuildBigCat(Transform t, CatKind kind, out Transform[] legs, out Transform body)
        {
            bool lion = kind == CatKind.Lion;
            var fur = lion ? Fur("LionFur", new Color(0.78f, 0.6f, 0.38f)) : Fur("SabreFur", new Color(0.72f, 0.55f, 0.34f));
            var dark = lion ? Fur("LionMane", new Color(0.42f, 0.25f, 0.12f)) : Fur("SabreDark", new Color(0.46f, 0.32f, 0.18f));
            var light = lion ? Fur("LionBelly", new Color(0.92f, 0.82f, 0.64f)) : Fur("SabreBelly", new Color(0.9f, 0.84f, 0.7f));
            var maneLight = Fur("LionManeLight", new Color(0.62f, 0.42f, 0.22f));
            var eye = Glow(lion ? "LionEye" : "SabreEye", new Color(0.8f, 0.6f, 0.1f), new Color(0.4f, 0.3f, 0.02f));

            var rig = t.gameObject.AddComponent<QuadRig>();
            rig.Cat = true;
            rig.DustColor = lion ? new Color(0.86f, 0.72f, 0.5f, 0.55f) : new Color(0.95f, 0.97f, 1f, 0.65f);
            body = rig.Body = Prim.Group("Body", t, Vector3.zero);
            var front = rig.Front = Prim.Group("Front", body, new Vector3(0f, 0.95f, -0.15f));
            var rear = rig.Rear = Prim.Group("Rear", body, new Vector3(0f, 0.93f, 0.2f));
            float s = lion ? 1.04f : 1f;

            // Heavy, muscular forequarters; leaner hindquarters
            P(Ball, "Chest", front, new Vector3(0f, 0.02f, -0.35f), new Vector3(0.78f, 0.86f, 0.9f) * s, fur);
            P(Ball, "Shoulders", front, new Vector3(0f, 0.24f, -0.22f), new Vector3(0.72f, 0.5f, 0.62f) * s, lion ? fur : dark);
            P(Ball, "ChestFur", front, new Vector3(0f, -0.24f, -0.3f), new Vector3(0.5f, 0.32f, 0.7f), light);
            P(Ball, "Neck", front, new Vector3(0f, 0.16f, -0.65f), new Vector3(0.46f, 0.48f, 0.52f) * s, fur);
            P(Ball, "Back", rear, new Vector3(0f, 0f, 0.15f), new Vector3(0.64f, 0.66f, 1.0f) * s, fur);
            P(Ball, "Haunch", rear, new Vector3(0f, 0.03f, 0.55f), new Vector3(0.7f, 0.74f, 0.6f) * s, fur);
            P(Ball, "Belly", rear, new Vector3(0f, -0.24f, 0.15f), new Vector3(0.46f, 0.28f, 0.85f), light);
            if (lion)
            {
                // A shaggy dark mane over the neck, shoulders and chest
                for (int i = 0; i < 14; i++)
                {
                    float a = i / 14f * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(a) * 0.3f, 0.18f + Mathf.Sin(a) * 0.32f, -0.62f + Random.Range(-0.05f, 0.08f));
                    P(Lump(i), "Mane", front, p, new Vector3(0.34f, 0.34f, 0.42f) * Random.Range(0.85f, 1.15f), i % 3 == 0 ? maneLight : dark);
                }
                P(Lump(2), "ManeBack", front, new Vector3(0f, 0.36f, -0.4f), new Vector3(0.52f, 0.36f, 0.5f), dark);
                P(Lump(1), "ManeChest", front, new Vector3(0f, -0.12f, -0.6f), new Vector3(0.48f, 0.5f, 0.38f), maneLight);
            }
            else
            {
                // Faint spots on the flanks, and snow settled on the back
                for (int i = 0; i < 12; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    var parent = i < 6 ? front : rear;
                    P(Ball, "Spot", parent, new Vector3(side * 0.33f, Random.Range(-0.05f, 0.22f), Random.Range(-0.5f, 0.6f)),
                      new Vector3(0.04f, Random.Range(0.08f, 0.12f), Random.Range(0.1f, 0.15f)), dark, null, false);
                }
                SnowOnBack(front, new Vector3(0f, 0.42f, -0.25f), new Vector3(0.45f, 0.1f, 0.55f));
                SnowOnBack(rear, new Vector3(0f, 0.32f, 0.3f), new Vector3(0.4f, 0.09f, 0.6f));
            }

            var head = rig.Head = Prim.Group("Head", front, new Vector3(0f, 0.22f, -0.88f));
            P(Ball, "Skull", head, Vector3.zero, new Vector3(0.46f, 0.42f, 0.46f), fur);
            P(Ball, "Cheeks", head, new Vector3(0f, -0.06f, -0.05f), new Vector3(0.52f, 0.3f, 0.36f), fur);
            P(Ball, "Muzzle", head, new Vector3(0f, -0.1f, lion ? -0.25f : -0.22f), new Vector3(0.26f, 0.18f, lion ? 0.26f : 0.2f), light);
            P(Ball, "Nose", head, new Vector3(0f, -0.04f, lion ? -0.36f : -0.31f), new Vector3(0.08f, 0.055f, 0.05f), lion ? Mats.Plain(new Color(0.35f, 0.18f, 0.14f), 0.5f) : Black);
            for (int k = -1; k <= 1; k += 2)
            {
                P(Ball, "Ear", head, new Vector3(k * 0.16f, 0.19f, 0.05f), new Vector3(0.1f, 0.1f, 0.05f), lion ? fur : dark);
                P(Ball, "Eye", head, new Vector3(k * 0.1f, 0.05f, -0.2f), new Vector3(0.06f, 0.045f, 0.04f), eye, null, false);
                if (lion)
                {
                    P(Ball, "Brow", head, new Vector3(k * 0.1f, 0.09f, -0.2f), new Vector3(0.09f, 0.03f, 0.05f), dark, null, false);
                    P(Cone(0.07f, 0.014f), "Fang", head, new Vector3(k * 0.05f, -0.18f, -0.3f), Vector3.one, Ivory, new Vector3(180f, 0f, 0f), false);
                }
                else
                {
                    // The famous sabre fangs, curving down past the jaw
                    var fang = new List<Vector3> { Vector3.zero, new Vector3(0f, -0.12f, -0.015f), new Vector3(0f, -0.24f, 0.01f), new Vector3(0f, -0.32f, 0.05f) };
                    P(Curve(fang, 0.03f, 0.004f), "Sabre", head, new Vector3(k * 0.06f, -0.13f, -0.24f), Vector3.one, Ivory);
                }
            }

            rig.Tail = Prim.Group("Tail", rear, new Vector3(0f, 0.1f, 0.82f));
            if (lion)
            {
                // Long tail hanging in a curve, ending in the black tuft
                var tp = new List<Vector3> { Vector3.zero, new Vector3(0f, -0.25f, 0.2f), new Vector3(0f, -0.55f, 0.32f), new Vector3(0f, -0.72f, 0.5f) };
                P(Curve(tp, 0.06f, 0.035f), "Tail", rig.Tail, Vector3.zero, Vector3.one, fur);
                P(Lump(3), "Tuft", rig.Tail, new Vector3(0f, -0.74f, 0.56f), new Vector3(0.12f, 0.12f, 0.18f), Fur("LionTuft", new Color(0.12f, 0.08f, 0.05f)));
            }
            else P(Ball, "BobTail", rig.Tail, Vector3.zero, new Vector3(0.14f, 0.14f, 0.22f), fur);

            rig.Hips = new Transform[4];
            rig.Knees = new Transform[4];
            rig.LowerLength = new[] { 0.36f, 0.36f, 0.38f, 0.38f };
            var paw = new Vector3(0.18f, 0.1f, 0.22f);
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                bool isFront = i < 2;
                Leg(isFront ? front : rear, isFront ? new Vector3(side * 0.22f, -0.15f, -0.42f) : new Vector3(side * 0.22f, -0.12f, 0.55f),
                    isFront ? 0.42f : 0.44f, rig.LowerLength[i], 0.13f * s, 0.095f, 0.07f, fur, lion ? fur : dark, paw, out rig.Hips[i], out rig.Knees[i]);
            }
            rig.StrideLength = 3.4f;
            rig.HipSwing = 42f;
            rig.KneeBend = 70f;
            rig.Offsets = new[] { 0f, 0.1f, 0.48f, 0.6f };   // rotary gallop: front pair, then hind pair
            rig.Bob = 0.09f;
            rig.Flex = 9f;
            rig.Pitch = 4f;
            rig.Snap();
            legs = rig.Hips;
        }

        // ---------------- Woolly rhino (charger) ----------------

        public static void BuildWoollyRhino(Transform t, out Transform[] legs, out Transform body)
        {
            var fur = Fur("WoollyRhinoFur", new Color(0.38f, 0.25f, 0.15f));
            var dark = Fur("WoollyRhinoDark", new Color(0.2f, 0.13f, 0.08f));
            var horn = Mats.Lit("Horn", Tex.Bark, new Color(0.55f, 0.48f, 0.38f), 0.35f, tiling: new Vector2(1f, 3f));

            var rig = t.gameObject.AddComponent<QuadRig>();
            body = rig.Body = Prim.Group("Body", t, Vector3.zero);
            var front = rig.Front = Prim.Group("Front", body, new Vector3(0f, 1.15f, -0.3f));
            var rear = rig.Rear = Prim.Group("Rear", body, new Vector3(0f, 1.12f, 0.4f));

            P(Lump(0), "Shoulders", front, new Vector3(0f, 0.1f, -0.3f), new Vector3(1.15f, 1.2f, 1.1f), fur);
            P(Lump(2), "Hump", front, new Vector3(0f, 0.62f, -0.2f), new Vector3(0.8f, 0.55f, 0.8f), dark);
            P(Lump(1), "Torso", rear, new Vector3(0f, 0f, 0.3f), new Vector3(1.1f, 1.1f, 1.5f), fur);
            P(Lump(3), "Rump", rear, new Vector3(0f, 0f, 0.95f), new Vector3(1.0f, 0.95f, 0.7f), fur);
            // Long shaggy hair hanging below the body
            for (int i = 0; i < 14; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var parent = i < 7 ? front : rear;
                float z = i < 7 ? Mathf.Lerp(-0.75f, 0.1f, (i / 2) / 3f) : Mathf.Lerp(0f, 1.1f, ((i - 7) / 2) / 3f);
                P(Lump(i), "Shag", parent, new Vector3(side * Random.Range(0.3f, 0.5f), -0.48f, z), new Vector3(0.24f, 0.72f, 0.24f), dark,
                  new Vector3(Random.Range(-8f, 8f), 0f, side * Random.Range(4f, 12f)));
            }
            SnowOnBack(front, new Vector3(0f, 0.88f, -0.15f), new Vector3(0.6f, 0.14f, 0.6f));
            SnowOnBack(rear, new Vector3(0f, 0.55f, 0.5f), new Vector3(0.7f, 0.12f, 0.9f));

            var head = rig.Head = Prim.Group("Head", front, new Vector3(0f, -0.05f, -0.98f));
            var tilt = Prim.Group("Tilt", head, Vector3.zero);
            tilt.localRotation = Quaternion.Euler(-18f, 0f, 0f); // carried low, horn forward
            P(Lump(1), "Skull", tilt, Vector3.zero, new Vector3(0.62f, 0.62f, 0.92f), fur);
            P(Ball, "Snout", tilt, new Vector3(0f, -0.1f, -0.45f), new Vector3(0.5f, 0.42f, 0.42f), dark);
            P(Cone(1.0f, 0.14f), "Horn", tilt, new Vector3(0f, 0.08f, -0.56f), Vector3.one, horn, new Vector3(-55f, 0f, 0f));
            P(Cone(0.4f, 0.09f), "Horn2", tilt, new Vector3(0f, 0.22f, -0.25f), Vector3.one, horn, new Vector3(-22f, 0f, 0f));
            for (int k = -1; k <= 1; k += 2)
            {
                P(Cone(0.18f, 0.06f), "Ear", tilt, new Vector3(k * 0.2f, 0.3f, 0.25f), Vector3.one, fur, new Vector3(0f, 0f, k * -20f));
                P(Ball, "Eye", tilt, new Vector3(k * 0.27f, 0.06f, -0.15f), Vector3.one * 0.05f, Black, null, false);
            }
            rig.Tail = Prim.Group("Tail", rear, new Vector3(0f, 0.15f, 1.3f));
            P(Lump(2), "Tail", rig.Tail, new Vector3(0f, -0.15f, 0.05f), new Vector3(0.12f, 0.35f, 0.12f), dark);

            rig.Hips = new Transform[4];
            rig.Knees = new Transform[4];
            rig.LowerLength = new[] { 0.42f, 0.42f, 0.42f, 0.42f };
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                bool isFront = i < 2;
                Leg(isFront ? front : rear, isFront ? new Vector3(side * 0.38f, -0.25f, -0.45f) : new Vector3(side * 0.38f, -0.22f, 1.0f),
                    0.48f, 0.42f, 0.2f, 0.17f, 0.15f, fur, dark, new Vector3(0.32f, 0.12f, 0.32f), out rig.Hips[i], out rig.Knees[i]);
            }
            rig.StrideLength = 2.6f;
            rig.HipSwing = 28f;
            rig.KneeBend = 50f;
            rig.Offsets = new[] { 0f, 0.07f, 0.5f, 0.57f };  // heavy transverse gallop
            rig.Bob = 0.11f;
            rig.Flex = 3f;
            rig.Pitch = 5f;
            rig.Snap();
            legs = rig.Hips;
        }

        // ---------------- Woolly mammoth (blocker) ----------------

        public static void BuildMammoth(Transform t)
        {
            var fur = Fur("MammothFur", new Color(0.4f, 0.24f, 0.13f));
            var dark = Fur("MammothDark", new Color(0.22f, 0.13f, 0.07f));
            var rig = t.gameObject.AddComponent<MammothRig>();
            var body = rig.Body = Prim.Group("Body", t, Vector3.zero);

            // Pillar legs with knees, long hair hanging over them
            rig.Hips = new Transform[4];
            rig.Knees = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                bool isFront = i < 2;
                float upper = isFront ? 1.0f : 0.92f, lower = isFront ? 0.95f : 0.85f;
                var hipPos = isFront ? new Vector3(side * 0.62f, 2.05f, -0.95f) : new Vector3(side * 0.58f, 1.87f, 1.05f);
                Leg(body, hipPos, upper, lower, 0.36f, 0.32f, 0.3f, fur, dark, new Vector3(0.66f, 0.2f, 0.66f), out rig.Hips[i], out rig.Knees[i]);
                P(Lump(i), "LegHair", rig.Hips[i], new Vector3(0f, -0.45f, 0f), new Vector3(0.82f, 1.05f, 0.82f), dark);
            }

            // Sloping back: tall shoulder hump, lower rump
            var torso = P(Lump(0), "Torso", body, new Vector3(0f, 2.3f, 0.05f), new Vector3(2.1f, 2.0f, 3.2f), fur);
            torso.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
            P(Lump(1), "Hump", body, new Vector3(0f, 3.3f, -0.75f), new Vector3(1.5f, 1.1f, 1.4f), fur);
            P(Lump(2), "Rump", body, new Vector3(0f, 2.05f, 1.25f), new Vector3(1.6f, 1.5f, 1.2f), fur);
            for (int i = 0; i < 22; i++)
            {
                float a = i / 22f * Mathf.PI * 2f;
                P(Lump(i), "HairSkirt", body, new Vector3(Mathf.Cos(a) * 1.0f, 1.45f, Mathf.Sin(a) * 1.55f), new Vector3(0.32f, 1.1f, 0.32f), dark,
                  new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f)));
            }
            SnowOnBack(body, new Vector3(0f, 3.82f, -0.7f), new Vector3(1.1f, 0.22f, 1.0f));
            SnowOnBack(body, new Vector3(0.1f, 3.2f, 0.45f), new Vector3(1.2f, 0.2f, 1.3f));
            var tail = Prim.Group("Tail", body, new Vector3(0f, 2.5f, 1.8f));
            P(Limb(0.6f, 0.06f, 0.04f), "Tail", tail, Vector3.zero, Vector3.one, fur, new Vector3(-20f, 0f, 0f));
            P(Lump(3), "Tuft", tail, new Vector3(0f, -0.58f, 0.2f), new Vector3(0.16f, 0.25f, 0.16f), dark);

            var head = rig.Head = Prim.Group("Head", body, new Vector3(0f, 2.9f, -1.85f));
            P(Lump(3), "Skull", head, new Vector3(0f, 0.15f, 0f), new Vector3(1.15f, 1.3f, 1.05f), fur);
            P(Lump(1), "Dome", head, new Vector3(0f, 0.78f, 0.1f), new Vector3(0.75f, 0.55f, 0.7f), dark);
            for (int k = -1; k <= 1; k += 2)
            {
                var ear = Prim.Group("Ear", head, new Vector3(k * 0.5f, 0.2f, 0.15f));
                P(Lump(k + 2), "EarFlap", ear, new Vector3(k * 0.12f, -0.1f, 0f), new Vector3(0.12f, 0.45f, 0.35f), dark);
                if (k < 0) rig.EarL = ear; else rig.EarR = ear;
                P(Ball, "Eye", head, new Vector3(k * 0.36f, 0.15f, -0.42f), Vector3.one * 0.07f, Black, null, false);
                var tusk = new List<Vector3>
                {
                    Vector3.zero, new Vector3(k * 0.14f, -0.5f, -0.5f), new Vector3(k * 0.24f, -0.68f, -1.2f),
                    new Vector3(k * 0.12f, -0.4f, -1.8f), new Vector3(k * -0.18f, 0.18f, -2.1f)
                };
                P(Curve(tusk, 0.14f, 0.035f), "Tusk", head, new Vector3(k * 0.3f, -0.35f, -0.4f), Vector3.one, Ivory);
            }

            // Trunk: a chain of segments that swings, curls and raises to trumpet; two "fingers" at the tip.
            rig.Trunk = new Transform[9];
            Transform parentSeg = head;
            var pos = new Vector3(0f, -0.2f, -0.5f);
            for (int i = 0; i < rig.Trunk.Length; i++)
            {
                float k = i / (float)rig.Trunk.Length;
                var seg = Prim.Group("Trunk" + i, parentSeg, pos);
                P(Limb(0.31f, Mathf.Lerp(0.2f, 0.075f, k), Mathf.Lerp(0.18f, 0.065f, k)), "Seg", seg, Vector3.zero, Vector3.one, fur);
                rig.Trunk[i] = seg;
                parentSeg = seg;
                pos = new Vector3(0f, -0.28f, 0f);
            }
            P(Ball, "TipA", parentSeg, new Vector3(0.025f, -0.3f, -0.02f), new Vector3(0.06f, 0.08f, 0.06f), dark);
            P(Ball, "TipB", parentSeg, new Vector3(-0.025f, -0.31f, 0.02f), new Vector3(0.05f, 0.06f, 0.05f), dark);
            rig.Pose(0.2f, 0f, 0f);
        }

        // ---------------- Water world: crocodile & hippo (chargers) ----------------

        public static void BuildCrocodile(Transform t, out Transform[] legs, out Transform body)
        {
            var skin = Mats.CrocSkin;
            var belly = Mats.Plain(new Color(0.62f, 0.6f, 0.45f), 0.35f);
            var eye = Glow("CrocEye", new Color(0.75f, 0.65f, 0.1f), new Color(0.35f, 0.28f, 0.02f));
            body = Prim.Group("Body", t, Vector3.zero);

            // One long, flattened body from snout tip (-Z) to tail tip (+Z).
            var path = new List<Vector3>();
            for (int k = 0; k <= 20; k++)
            {
                float u = k / 20f;
                path.Add(new Vector3(Mathf.Sin(u * 4f) * 0.05f, 0f, Mathf.Lerp(-1.6f, 2.3f, u)));
            }
            var mesh = Meshes.Sweep(path, (u, a) =>
            {
                float r = u < 0.18f ? Mathf.Lerp(0.1f, 0.2f, u / 0.18f)
                        : u < 0.5f ? Mathf.Lerp(0.2f, 0.36f, Mathf.Sin((u - 0.18f) / 0.32f * Mathf.PI * 0.5f))
                        : Mathf.Lerp(0.36f, 0.03f, (u - 0.5f) / 0.5f);
                return r * (1f + 0.12f * Mathf.Abs(Mathf.Sin(a * 6f)));
            }, 12, 2f, 1.2f, true);
            P(mesh, "Body", body, new Vector3(0f, 0.32f, 0f), new Vector3(1f, 0.55f, 1f), skin);
            P(Meshes.RoundedBox(new Vector3(0.28f, 0.07f, 0.85f), 0.03f), "LowerJaw", body, new Vector3(0f, 0.22f, -1.15f), Vector3.one, belly);
            for (int k = -1; k <= 1; k += 2)
            {
                P(Ball, "EyeBump", body, new Vector3(k * 0.11f, 0.45f, -0.95f), new Vector3(0.1f, 0.09f, 0.12f), skin);
                P(Ball, "Eye", body, new Vector3(k * 0.12f, 0.48f, -0.99f), Vector3.one * 0.05f, eye, null, false);
                for (int i = 0; i < 6; i++)
                    P(Cone(0.07f, 0.014f), "Tooth", body, new Vector3(k * 0.12f, 0.25f, -1.5f + i * 0.12f), Vector3.one, Ivory, new Vector3(180f, 0f, 0f), false);
            }
            for (int i = 0; i < 9; i++) // armoured ridge down the back and tail
                P(Cone(0.12f, 0.05f), "Scute", body, new Vector3(0f, 0.48f - i * 0.025f, -0.3f + i * 0.28f), Vector3.one, skin);

            Vector3[] hips = { new Vector3(-0.32f, 0.3f, -0.55f), new Vector3(0.32f, 0.3f, -0.55f),
                               new Vector3(-0.34f, 0.3f, 0.6f), new Vector3(0.34f, 0.3f, 0.6f) };
            legs = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var leg = Prim.Group("Leg" + i, body, hips[i]);
                P(Limb(0.26f, 0.08f, 0.07f), "Upper", leg, Vector3.zero, Vector3.one, skin, new Vector3(0f, 0f, side * 70f));
                P(Limb(0.24f, 0.065f, 0.05f), "Lower", leg, new Vector3(side * 0.24f, -0.08f, 0f), Vector3.one, skin);
                P(Ball, "Foot", leg, new Vector3(side * 0.26f, -0.3f, -0.04f), new Vector3(0.12f, 0.05f, 0.16f), skin);
                legs[i] = leg;
            }
        }

        public static void BuildHippo(Transform t, out Transform[] legs, out Transform body)
        {
            var skin = Hide("HippoHide", new Color(0.44f, 0.37f, 0.4f));
            var pink = Hide("HippoPink", new Color(0.66f, 0.46f, 0.46f));
            var mouth = Mats.Plain(new Color(0.55f, 0.2f, 0.25f), 0.5f);
            body = Prim.Group("Body", t, Vector3.zero);
            P(Ball, "Barrel", body, new Vector3(0f, 1.05f, 0.1f), new Vector3(1.3f, 1.25f, 2.4f), skin);
            P(Ball, "Belly", body, new Vector3(0f, 0.78f, 0.05f), new Vector3(1.05f, 0.6f, 1.9f), pink);

            var head = Prim.Group("Head", body, new Vector3(0f, 1.15f, -1.3f));
            P(Ball, "Skull", head, Vector3.zero, new Vector3(0.82f, 0.7f, 0.85f), skin);
            P(Ball, "Muzzle", head, new Vector3(0f, -0.08f, -0.48f), new Vector3(0.95f, 0.5f, 0.6f), skin);
            P(Ball, "Mouth", head, new Vector3(0f, -0.32f, -0.5f), new Vector3(0.8f, 0.2f, 0.5f), mouth, null, false);
            var jaw = Prim.Group("Jaw", head, new Vector3(0f, -0.3f, -0.2f));
            jaw.localRotation = Quaternion.Euler(-28f, 0f, 0f); // charging with its huge jaws agape
            P(Ball, "LowerJaw", jaw, new Vector3(0f, -0.1f, -0.32f), new Vector3(0.85f, 0.25f, 0.62f), pink);
            for (int k = -1; k <= 1; k += 2)
            {
                P(Cone(0.28f, 0.05f), "Tusk", jaw, new Vector3(k * 0.28f, 0f, -0.55f), Vector3.one, Ivory, new Vector3(-15f, 0f, k * -8f), false);
                P(Ball, "Nostril", head, new Vector3(k * 0.17f, 0.17f, -0.68f), Vector3.one * 0.12f, skin);
                P(Ball, "EyeBump", head, new Vector3(k * 0.24f, 0.33f, -0.12f), Vector3.one * 0.16f, skin);
                P(Ball, "Eye", head, new Vector3(k * 0.26f, 0.37f, -0.19f), Vector3.one * 0.06f, Black, null, false);
                P(Ball, "Ear", head, new Vector3(k * 0.22f, 0.4f, 0.12f), new Vector3(0.1f, 0.14f, 0.06f), pink);
            }
            P(Ball, "Tail", body, new Vector3(0f, 1.25f, 1.32f), new Vector3(0.1f, 0.25f, 0.08f), skin);

            Vector3[] hips = { new Vector3(-0.42f, 0.6f, -0.75f), new Vector3(0.42f, 0.6f, -0.75f),
                               new Vector3(-0.42f, 0.6f, 0.8f), new Vector3(0.42f, 0.6f, 0.8f) };
            legs = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var leg = Prim.Group("Leg" + i, body, hips[i]);
                P(Limb(0.6f, 0.21f, 0.19f), "Leg", leg, Vector3.zero, Vector3.one, skin);
                P(Ball, "Foot", leg, new Vector3(0f, -0.6f, 0f), new Vector3(0.36f, 0.12f, 0.36f), skin);
                legs[i] = leg;
            }
        }

        // ---------------- Water world: giant octopus (blocker) ----------------

        public static void BuildOctopus(Transform t)
        {
            var skin = Mats.Lit("OctopusSkin", Tex.Leather, new Color(0.66f, 0.22f, 0.24f), 0.62f, tiling: new Vector2(3f, 3f), bump: 1.8f);
            var sucker = Mats.Plain(new Color(0.94f, 0.78f, 0.7f), 0.5f);
            var eyeMat = Mats.Plain(new Color(0.95f, 0.85f, 0.4f), 0.8f);
            var rig = t.gameObject.AddComponent<OctopusRig>();

            var mantle = rig.Mantle = Prim.Group("Mantle", t, new Vector3(0f, 1.25f, 0.2f));
            P(Lump(0), "Head", mantle, Vector3.zero, new Vector3(1.05f, 0.8f, 1.0f), skin);
            P(Lump(1), "Sac", mantle, new Vector3(0f, 0.75f, 0.35f), new Vector3(1.0f, 1.25f, 1.05f), skin, new Vector3(-20f, 0f, 0f));
            for (int k = -1; k <= 1; k += 2)
            {
                P(Ball, "Eye", mantle, new Vector3(k * 0.38f, 0.12f, -0.36f), Vector3.one * 0.24f, eyeMat);
                P(Meshes.RoundedBox(new Vector3(0.14f, 0.04f, 0.02f), 0.01f), "Pupil", mantle, new Vector3(k * 0.41f, 0.12f, -0.47f), Vector3.one, Black, null, false);
            }

            rig.Arms = new Transform[8][];
            for (int a = 0; a < 8; a++)
            {
                rig.Arms[a] = new Transform[OctopusRig.Segments];
                for (int i = 0; i < OctopusRig.Segments; i++)
                {
                    float k = i / (OctopusRig.Segments - 1f);
                    var seg = Prim.Group("Arm" + a + "_" + i, t, Vector3.zero);
                    float r = Mathf.Lerp(0.15f, 0.03f, k);
                    P(Limb(OctopusRig.SegLength, r, r * 0.9f), "Seg", seg, Vector3.zero, Vector3.one, skin);
                    if (i % 2 == 0) P(Ball, "Sucker", seg, new Vector3(0f, -0.1f, r * 0.8f), Vector3.one * (r * 0.8f), sucker, null, false);
                    rig.Arms[a][i] = seg;
                }
            }
            rig.Pose(0f, 0f);
        }

        // ---------------- Water world: giant crab (crawler) ----------------

        public static void BuildCrab(Transform t)
        {
            var shell = Mats.Lit("CrabShell", Tex.Leather, new Color(0.86f, 0.33f, 0.16f), 0.6f, tiling: new Vector2(2f, 2f), bump: 1.4f);
            var under = Mats.Plain(new Color(0.95f, 0.82f, 0.66f), 0.45f);
            var rig = t.gameObject.AddComponent<CrabRig>();
            var model = rig.Model = Prim.Group("Crab", t, Vector3.zero);
            var body = rig.Body = Prim.Group("Body", model, new Vector3(0f, 0.42f, 0f));

            P(Lump(2), "Carapace", body, Vector3.zero, new Vector3(1.0f, 0.36f, 0.75f), shell);
            P(Ball, "Underside", body, new Vector3(0f, -0.1f, 0f), new Vector3(0.85f, 0.24f, 0.62f), under);
            for (int k = -1; k <= 1; k += 2)
            {
                P(Limb(0.18f, 0.025f, 0.02f), "Stalk", body, new Vector3(k * 0.14f, 0.3f, -0.3f), Vector3.one, shell, new Vector3(180f, 0f, 0f));
                P(Ball, "Eye", body, new Vector3(k * 0.14f, 0.49f, -0.3f), Vector3.one * 0.07f, Black, null, false);
            }

            // Two big claws held up in front, snapping
            rig.Claws = new Transform[2];
            rig.Pincers = new Transform[2];
            for (int k = -1; k <= 1; k += 2)
            {
                var arm = Prim.Group(k < 0 ? "ClawL" : "ClawR", body, new Vector3(k * 0.42f, 0f, -0.25f));
                P(Limb(0.36f, 0.08f, 0.07f), "Arm", arm, Vector3.zero, Vector3.one, shell);
                var hand = Prim.Group("Hand", arm, new Vector3(0f, -0.38f, 0f));
                P(Ball, "Palm", hand, Vector3.zero, new Vector3(0.26f, 0.32f, 0.2f), shell);
                P(Cone(0.24f, 0.06f), "Finger", hand, new Vector3(0.05f, -0.12f, 0f), Vector3.one, shell, new Vector3(180f, 0f, 0f));
                var pincer = Prim.Group("Pincer", hand, new Vector3(-0.06f, -0.08f, 0f));
                P(Cone(0.22f, 0.05f), "Dactyl", pincer, Vector3.zero, Vector3.one, shell, new Vector3(180f, 0f, 0f));
                rig.Claws[k < 0 ? 0 : 1] = arm;
                rig.Pincers[k < 0 ? 0 : 1] = pincer;
            }

            rig.Hips = new Transform[8];
            rig.Knees = new Transform[8];
            for (int i = 0; i < 8; i++)
            {
                float side = i < 4 ? -1f : 1f;
                int idx = i % 4;
                var hip = Prim.Group("Hip" + i, body, new Vector3(side * 0.4f, -0.05f, 0.1f - idx * 0.11f));
                var knee = Prim.Group("Knee", hip, new Vector3(side * 0.4f, 0f, 0f));
                P(Limb(0.4f, 0.045f, 0.04f), "Upper", hip, Vector3.zero, Vector3.one, shell, new Vector3(0f, 0f, side * 90f));
                P(Limb(0.42f, 0.04f, 0.015f), "Lower", knee, Vector3.zero, Vector3.one, shell);
                rig.Hips[i] = hip;
                rig.Knees[i] = knee;
            }
            rig.Init();
        }

        // ---------------- Desert: giant scorpion (crawler) ----------------

        public static void BuildScorpion(Transform t)
        {
            var shell = Mats.Lit("ScorpionShell", Tex.Leather, new Color(0.62f, 0.45f, 0.2f), 0.68f, tiling: new Vector2(2f, 2f), bump: 1.2f);
            var dark = Mats.Lit("ScorpionDark", Tex.Leather, new Color(0.3f, 0.18f, 0.08f), 0.72f, tiling: new Vector2(2f, 2f), bump: 1.2f);
            var rig = t.gameObject.AddComponent<ScorpionRig>();
            var model = rig.Model = Prim.Group("Scorpion", t, Vector3.zero);
            var facing = Prim.Group("Facing", model, Vector3.zero);
            facing.localRotation = Quaternion.Euler(0f, 180f, 0f); // built facing -Z; crawlers walk toward their +Z
            var body = rig.Body = Prim.Group("Body", facing, new Vector3(0f, 0.3f, 0f));

            // Carapace and the segmented abdomen behind it (faces -Z)
            P(Lump(1), "Carapace", body, new Vector3(0f, 0.02f, -0.3f), new Vector3(0.46f, 0.16f, 0.42f), shell);
            for (int i = 0; i < 6; i++)
            {
                float w = Mathf.Lerp(0.5f, 0.34f, i / 5f);
                P(Ball, "Tergite", body, new Vector3(0f, 0.02f, -0.08f + i * 0.11f), new Vector3(w, 0.15f, 0.15f), i % 2 == 0 ? shell : dark);
            }
            for (int k = -1; k <= 1; k += 2)
                P(Ball, "Eye", body, new Vector3(k * 0.04f, 0.1f, -0.4f), Vector3.one * 0.035f, Black, null, false);

            // Tail (metasoma): five segments arching up and over, ending in the bulb and the stinger
            rig.Tail = new Transform[5];
            Transform parent = body;
            var at = new Vector3(0f, 0.03f, 0.58f);
            for (int i = 0; i < rig.Tail.Length; i++)
            {
                var seg = Prim.Group("Tail" + i, parent, at);
                float r = Mathf.Lerp(0.085f, 0.06f, i / 4f);
                P(Ball, "Seg", seg, new Vector3(0f, 0.11f, 0f), new Vector3(r * 2.1f, 0.26f, r * 2.1f), i % 2 == 0 ? shell : dark);
                rig.Tail[i] = seg;
                parent = seg;
                at = new Vector3(0f, 0.22f, 0f);
            }
            rig.Sting = Prim.Group("Telson", parent, new Vector3(0f, 0.22f, 0f));
            P(Ball, "Bulb", rig.Sting, new Vector3(0f, 0.06f, 0f), new Vector3(0.14f, 0.17f, 0.13f), shell);
            P(Cone(0.17f, 0.035f), "Stinger", rig.Sting, new Vector3(0f, 0.12f, -0.03f), Vector3.one, dark, new Vector3(-60f, 0f, 0f));

            // Pedipalps: big pincers held out in front
            rig.Palps = new Transform[2];
            rig.Pincers = new Transform[2];
            for (int k = 0; k < 2; k++)
            {
                float sd = k == 0 ? -1f : 1f;
                var arm = Prim.Group("Palp", body, new Vector3(sd * 0.16f, 0.02f, -0.45f));
                P(Limb(0.3f, 0.04f, 0.035f), "Humerus", arm, Vector3.zero, Vector3.one, shell);
                var hand = Prim.Group("Hand", arm, new Vector3(0f, -0.3f, 0f));
                P(Ball, "Chela", hand, new Vector3(0f, -0.08f, 0f), new Vector3(0.12f, 0.2f, 0.1f), shell);
                P(Cone(0.18f, 0.03f), "Finger", hand, new Vector3(sd * 0.025f, -0.16f, 0f), Vector3.one, dark, new Vector3(180f, 0f, 0f));
                var pincer = Prim.Group("Pincer", hand, new Vector3(-sd * 0.03f, -0.15f, 0f));
                P(Cone(0.16f, 0.025f), "Dactyl", pincer, Vector3.zero, Vector3.one, dark, new Vector3(180f, 0f, 0f));
                rig.Palps[k] = arm;
                rig.Pincers[k] = pincer;
            }

            // Eight walking legs, splayed out to the sides
            rig.Hips = new Transform[8];
            rig.Knees = new Transform[8];
            for (int i = 0; i < 8; i++)
            {
                float side = i < 4 ? -1f : 1f;
                int idx = i % 4;
                var hip = Prim.Group("Hip" + i, body, new Vector3(side * 0.18f, -0.02f, -0.25f + idx * 0.13f));
                var knee = Prim.Group("Knee", hip, new Vector3(side * 0.28f, 0f, 0f));
                P(Limb(0.28f, 0.03f, 0.026f), "Femur", hip, Vector3.zero, Vector3.one, shell, new Vector3(0f, 0f, side * 90f));
                P(Limb(0.36f, 0.025f, 0.01f), "Tibia", knee, Vector3.zero, Vector3.one, dark);
                rig.Hips[i] = hip;
                rig.Knees[i] = knee;
            }
            rig.Init();
        }

        // ---------------- Desert: ostrich (charger) ----------------

        public static void BuildOstrich(Transform t, out Transform[] legs, out Transform body)
        {
            var plumage = Fur("OstrichBlack", new Color(0.1f, 0.09f, 0.09f), 3f);
            var plume = Fur("OstrichPlume", new Color(0.93f, 0.9f, 0.84f), 3f);
            var skin = Mats.Lit("OstrichSkin", Tex.Skin, new Color(0.78f, 0.58f, 0.52f), 0.35f);
            var scales = Mats.Lit("OstrichScales", Tex.SnakeSkin, new Color(0.72f, 0.56f, 0.48f), 0.4f, tiling: new Vector2(1f, 4f));
            var beak = Mats.Plain(new Color(0.75f, 0.62f, 0.45f), 0.5f);

            var rig = t.gameObject.AddComponent<OstrichRig>();
            body = rig.Body = Prim.Group("Body", t, new Vector3(0f, 1.22f, 0f));
            P(Lump(0), "Torso", body, new Vector3(0f, 0.1f, 0.05f), new Vector3(0.82f, 0.72f, 1.15f), plumage);
            P(Lump(2), "Breast", body, new Vector3(0f, 0.02f, -0.38f), new Vector3(0.6f, 0.6f, 0.5f), plumage);
            // White tail plumes and the wings held out for balance
            rig.TailPlume = Prim.Group("Tail", body, new Vector3(0f, 0.25f, 0.6f));
            for (int i = 0; i < 4; i++)
                P(Lump(i), "Plume", rig.TailPlume, new Vector3((i - 1.5f) * 0.09f, 0.04f * i, 0.12f), new Vector3(0.16f, 0.2f, 0.35f), plume, new Vector3(-30f, 0f, 0f));
            rig.Wings = new Transform[2];
            for (int k = 0; k < 2; k++)
            {
                float sd = k == 0 ? -1f : 1f;
                var w = rig.Wings[k] = Prim.Group("Wing", body, new Vector3(sd * 0.38f, 0.22f, -0.05f));
                P(Lump(k + 1), "Coverts", w, new Vector3(sd * 0.12f, 0f, 0.05f), new Vector3(0.28f, 0.1f, 0.5f), plumage);
                for (int i = 0; i < 3; i++)
                    P(Lump(i + 2), "Plume", w, new Vector3(sd * (0.22f + i * 0.04f), -0.05f, 0.2f + i * 0.08f), new Vector3(0.18f, 0.12f, 0.32f), plume);
            }

            // Long bare neck: a chain that holds the head steady while the body pitches and bobs
            rig.Neck = new Transform[5];
            Transform parent = body;
            var at = new Vector3(0f, 0.25f, -0.48f);
            for (int i = 0; i < rig.Neck.Length; i++)
            {
                var seg = Prim.Group("Neck" + i, parent, at);
                P(Limb(0.2f, Mathf.Lerp(0.075f, 0.045f, i / 4f), Mathf.Lerp(0.07f, 0.042f, i / 4f)), "Seg", seg, Vector3.zero, Vector3.one, skin,
                  new Vector3(180f, 0f, 0f));
                if (i == 0) P(Lump(3), "Collar", seg, new Vector3(0f, 0.04f, 0f), new Vector3(0.22f, 0.16f, 0.22f), plumage);
                rig.Neck[i] = seg;
                parent = seg;
                at = new Vector3(0f, 0.2f, 0f);
            }
            var head = rig.Head = Prim.Group("Head", parent, new Vector3(0f, 0.22f, 0f));
            P(Ball, "Skull", head, Vector3.zero, new Vector3(0.13f, 0.12f, 0.17f), skin);
            P(Ball, "Beak", head, new Vector3(0f, -0.02f, -0.12f), new Vector3(0.09f, 0.04f, 0.16f), beak);
            for (int k = -1; k <= 1; k += 2)
            {
                P(Ball, "Eye", head, new Vector3(k * 0.055f, 0.02f, -0.03f), new Vector3(0.035f, 0.045f, 0.045f), Black, null, false);
                P(Ball, "Lash", head, new Vector3(k * 0.06f, 0.045f, -0.03f), new Vector3(0.03f, 0.008f, 0.04f), Black, null, false);
            }

            // Legs: thigh, long shank, backward-bending ankle and a scaly tarsus down to the big two-toed foot
            rig.Hips = new Transform[2];
            rig.Knees = new Transform[2];
            rig.Ankles = new Transform[2];
            for (int k = 0; k < 2; k++)
            {
                float sd = k == 0 ? -1f : 1f;
                var hip = rig.Hips[k] = Prim.Group("Hip", body, new Vector3(sd * 0.2f, -0.12f, 0.08f));
                P(Lump(k), "Thigh", hip, new Vector3(0f, -0.12f, 0f), new Vector3(0.22f, 0.32f, 0.24f), skin);
                P(Limb(0.3f, 0.1f, 0.07f), "Femur", hip, Vector3.zero, Vector3.one, skin);
                var knee = rig.Knees[k] = Prim.Group("Knee", hip, new Vector3(0f, -0.3f, 0f));
                P(Limb(0.5f, 0.065f, 0.04f), "Shank", knee, Vector3.zero, Vector3.one, skin);
                var ankle = rig.Ankles[k] = Prim.Group("Ankle", knee, new Vector3(0f, -0.5f, 0f));
                P(Ball, "Heel", ankle, Vector3.zero, Vector3.one * 0.08f, scales);
                P(Limb(0.46f, 0.04f, 0.035f), "Tarsus", ankle, Vector3.zero, Vector3.one, scales);
                P(Ball, "Toe", ankle, new Vector3(0.012f * sd, -0.47f, -0.08f), new Vector3(0.09f, 0.05f, 0.22f), scales);
                P(Ball, "SmallToe", ankle, new Vector3(0.06f * sd, -0.47f, -0.03f), new Vector3(0.05f, 0.04f, 0.12f), scales);
            }
            rig.Snap();
            legs = rig.Hips;
        }

        // ---------------- Desert: dromedary camel (blocker) ----------------

        public static void BuildCamel(Transform t)
        {
            var hide = Fur("CamelHide", new Color(0.74f, 0.58f, 0.4f), 3f);
            var dark = Fur("CamelDark", new Color(0.52f, 0.38f, 0.24f), 3f);
            var muzzle = Mats.Lit("CamelMuzzle", Tex.Skin, new Color(0.6f, 0.46f, 0.34f), 0.3f);
            var rug = Mats.Fabric(new Color(0.62f, 0.14f, 0.1f));
            var rugTrim = Mats.Fabric(new Color(0.9f, 0.68f, 0.2f));
            var rope = Mats.Rope;

            var rig = t.gameObject.AddComponent<CamelRig>();
            var body = rig.Body = Prim.Group("Body", t, Vector3.zero);
            var barrel = Prim.Group("Barrel", body, new Vector3(0f, 1.62f, 0f));

            P(Lump(0), "Torso", barrel, new Vector3(0f, 0f, 0f), new Vector3(0.82f, 0.82f, 1.8f), hide);
            P(Lump(1), "Chest", barrel, new Vector3(0f, -0.04f, -0.72f), new Vector3(0.7f, 0.8f, 0.6f), hide);
            P(Lump(2), "Rump", barrel, new Vector3(0f, 0.04f, 0.75f), new Vector3(0.72f, 0.75f, 0.55f), hide);
            P(Ball, "ChestPad", barrel, new Vector3(0f, -0.42f, -0.55f), new Vector3(0.26f, 0.12f, 0.3f), dark);

            // Single hump; it sways on its own (spring) as the camel moves
            rig.Hump = Prim.Group("Hump", barrel, new Vector3(0f, 0.32f, 0f));
            P(Lump(3), "Hump", rig.Hump, new Vector3(0f, 0.18f, 0f), new Vector3(0.62f, 0.56f, 0.82f), hide);
            // Saddle blanket with a fringe, roped around the belly
            P(Meshes.RoundedBox(new Vector3(1f, 0.04f, 0.75f), 0.02f), "Rug", barrel, new Vector3(0f, 0.36f, 0.5f), new Vector3(0.95f, 1f, 1f), rug, new Vector3(-4f, 0f, 0f));
            for (int k = -1; k <= 1; k += 2)
            {
                P(Meshes.RoundedBox(new Vector3(0.05f, 0.55f, 0.75f), 0.02f), "RugSide", barrel, new Vector3(k * 0.44f, 0.12f, 0.5f), Vector3.one, rug, new Vector3(0f, 0f, k * 10f));
                P(Meshes.RoundedBox(new Vector3(0.06f, 0.06f, 0.77f), 0.02f), "Trim", barrel, new Vector3(k * 0.47f, -0.16f, 0.5f), Vector3.one, rugTrim, new Vector3(0f, 0f, k * 10f));
            }
            P(Meshes.RoundedBox(new Vector3(0.88f, 0.05f, 0.06f), 0.02f), "Girth", barrel, new Vector3(0f, -0.38f, 0.42f), Vector3.one, rope);

            // Long legs with knees (and the calloused knee pads camels kneel on)
            rig.Hips = new Transform[4];
            rig.Knees = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                bool isFront = i < 2;
                var hipPos = isFront ? new Vector3(side * 0.26f, 1.36f, -0.62f) : new Vector3(side * 0.24f, 1.4f, 0.66f);
                Leg(body, hipPos, isFront ? 0.72f : 0.7f, isFront ? 0.64f : 0.7f, 0.13f, 0.08f, 0.065f, hide, dark,
                    new Vector3(0.24f, 0.08f, 0.3f), out rig.Hips[i], out rig.Knees[i]);
                P(Ball, "KneePad", rig.Knees[i], new Vector3(0f, 0f, isFront ? -0.05f : 0.04f), Vector3.one * 0.16f, dark, null, false);
                // Thigh muscle up near the body
                P(Lump(i), "Thigh", rig.Hips[i], new Vector3(0f, -0.12f, 0f), new Vector3(0.28f, 0.42f, 0.32f), hide);
            }

            // Neck: dips forward and down from the chest, then rises to the head
            rig.Neck = new Transform[4];
            Transform parent = barrel;
            var at = new Vector3(0f, 0.12f, -0.9f);
            float[] bends = { -110f, 20f, 45f, 40f };
            for (int i = 0; i < rig.Neck.Length; i++)
            {
                var seg = Prim.Group("Neck" + i, parent, at);
                seg.localRotation = Quaternion.Euler(bends[i], 0f, 0f);
                P(Limb(0.36f, Mathf.Lerp(0.2f, 0.12f, i / 3f), Mathf.Lerp(0.18f, 0.11f, i / 3f)), "Seg", seg, Vector3.zero, Vector3.one, hide,
                  new Vector3(180f, 0f, 0f));
                rig.Neck[i] = seg;
                parent = seg;
                at = new Vector3(0f, 0.34f, 0f);
            }
            rig.NeckRest = bends;

            var head = rig.Head = Prim.Group("Head", parent, new Vector3(0f, 0.36f, 0f));
            var face = Prim.Group("Face", head, Vector3.zero);
            face.localRotation = Quaternion.Euler(5f, 0f, 0f); // undo what is left of the neck tilt so the head points forward
            P(Lump(2), "Skull", face, new Vector3(0f, 0.04f, 0f), new Vector3(0.26f, 0.25f, 0.32f), hide);
            P(Ball, "Snout", face, new Vector3(0f, -0.02f, -0.27f), new Vector3(0.19f, 0.18f, 0.36f), muzzle);
            P(Ball, "UpperLip", face, new Vector3(0f, -0.06f, -0.42f), new Vector3(0.17f, 0.1f, 0.12f), muzzle);
            rig.Jaw = Prim.Group("Jaw", face, new Vector3(0f, -0.1f, -0.2f));
            P(Ball, "LowerLip", rig.Jaw, new Vector3(0f, -0.02f, -0.2f), new Vector3(0.14f, 0.08f, 0.24f), muzzle);
            for (int k = -1; k <= 1; k += 2)
            {
                P(Ball, "Eye", face, new Vector3(k * 0.12f, 0.08f, -0.08f), new Vector3(0.05f, 0.05f, 0.05f), Black, null, false);
                P(Ball, "Lid", face, new Vector3(k * 0.12f, 0.11f, -0.08f), new Vector3(0.07f, 0.035f, 0.065f), dark, null, false);
                P(Ball, "Nostril", face, new Vector3(k * 0.05f, 0.02f, -0.44f), new Vector3(0.025f, 0.012f, 0.03f), Black, null, false);
                var ear = Prim.Group("Ear", face, new Vector3(k * 0.1f, 0.17f, 0.1f));
                P(Cone(0.1f, 0.035f), "EarFlap", ear, Vector3.zero, Vector3.one, hide, new Vector3(-20f, 0f, k * -25f));
                if (k < 0) rig.EarL = ear; else rig.EarR = ear;
            }
            // Halter rope around the muzzle
            P(Meshes.RoundedBox(new Vector3(0.22f, 0.03f, 0.03f), 0.01f), "Halter", face, new Vector3(0f, 0.04f, -0.32f), Vector3.one, rope);

            rig.Tail = Prim.Group("Tail", barrel, new Vector3(0f, 0.12f, 1.02f));
            P(Limb(0.5f, 0.04f, 0.03f), "Tail", rig.Tail, Vector3.zero, Vector3.one, hide);
            P(Lump(1), "Tuft", rig.Tail, new Vector3(0f, -0.5f, 0f), new Vector3(0.07f, 0.16f, 0.07f), dark);

            rig.Barrel = barrel;
            rig.Pose(0f, 0f, 0f);
        }
    }

    // ===================== Rigs =====================

    /// <summary>Base for animals that react to the runner's approach.</summary>
    public abstract class CreatureRig : MonoBehaviour
    {
        protected Obstacle ob;
        protected float time;

        protected virtual void Start()
        {
            ob = GetComponent<Obstacle>();
            time = Random.value * 10f;
        }

        /// <summary>Distance from the runner along the trail (large when not playing).</summary>
        protected float Gap()
        {
            var gm = GameManager.I;
            return gm != null && gm.Player != null && ob != null ? ob.S - gm.Player.TrackS : 999f;
        }

        protected bool Active => ob == null || !ob.Handled;
    }

    /// <summary>
    /// An animal with a procedural run cycle. The owning Obstacle sets <see cref="Speed"/> (ground speed)
    /// and <see cref="Waiting"/> (poised before a charge); the rig turns that into a gait.
    /// </summary>
    public abstract class GaitRig : MonoBehaviour
    {
        [System.NonSerialized] public float Speed;
        [System.NonSerialized] public bool Waiting;
    }

    /// <summary>
    /// Articulated four-legged gait: every leg has a hip and a knee, plants during its stance phase and
    /// folds up as it swings forward. The spine flexes, the body rises and pitches with the stride, the head
    /// counter-rotates to stay steady, and feet kick up snow on touchdown. The owning Obstacle sets
    /// <see cref="Speed"/> (ground speed) and <see cref="Waiting"/> (crouched, about to pounce).
    /// </summary>
    public class QuadRig : GaitRig
    {
        public Transform Body, Front, Rear, Head, Tail;
        public Transform[] Hips, Knees;
        public float[] LowerLength, Offsets;
        public float StrideLength = 2.5f, HipSwing = 35f, KneeBend = 60f, Bob = 0.1f, Flex = 5f, Pitch = 4f;
        public float Duty = 0.45f;   // fraction of the cycle a foot spends on the ground
        public bool Cat;
        public Color DustColor = new Color(0.95f, 0.97f, 1f, 0.6f);


        float cycle, run, crouch, time;
        float[] prev;

        void Start() => time = Random.value * 10f;

        public void Snap()
        {
            prev = new float[Hips.Length];
            Pose(0f, 0f, 0f, 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            run = Mathf.MoveTowards(run, Speed > 0.5f ? 1f : 0f, dt * 3f);
            crouch = Mathf.MoveTowards(crouch, Waiting && Cat ? 1f : 0f, dt * 2.5f);
            cycle += Speed / Mathf.Max(0.1f, StrideLength) * dt;
            if (cycle > 1000f) cycle -= 1000f;

            Pose(cycle, run, crouch, time);

            // Snow kicked up as each foot strikes the ground
            if (run > 0.5f && Speed > 2f)
            {
                if (prev == null || prev.Length != Hips.Length) prev = new float[Hips.Length];
                for (int i = 0; i < Hips.Length; i++)
                {
                    float p = Mathf.Repeat(cycle + Offsets[i], 1f);
                    if (p < prev[i])
                        Fx.Dust(Knees[i].TransformPoint(0f, -LowerLength[i], 0f), i < 2 ? 2 : 3, Cat ? 0.35f : 0.5f, DustColor);
                    prev[i] = p;
                }
            }
        }

        public void Pose(float cyc, float run, float crouch, float t)
        {
            float w = cyc * Mathf.PI * 2f;
            float breathe = Mathf.Sin(t * 1.8f);

            for (int i = 0; i < Hips.Length; i++)
            {
                bool front = i < 2;
                float p = Mathf.Repeat(cyc + Offsets[i], 1f);
                float hip, knee;
                if (p < Duty)
                {
                    // Stance: the planted foot sweeps backward under the body, knee flexing to take the weight.
                    float u = p / Duty;
                    hip = Mathf.Lerp(HipSwing, -HipSwing * 0.85f, u);
                    knee = -12f * Mathf.Sin(u * Mathf.PI);
                }
                else
                {
                    // Swing: lift, fold the lower leg up and reach forward for the next strike.
                    float u = (p - Duty) / (1f - Duty);
                    float e = u * u * (3f - 2f * u);
                    hip = Mathf.Lerp(-HipSwing * 0.85f, HipSwing * 1.05f, e);
                    knee = -KneeBend * Mathf.Sin(u * Mathf.PI) * (front ? 1f : 0.8f);
                }
                if (!front) hip -= 6f; // hind legs angle back slightly

                // Idle stance and the coiled pounce crouch
                float idleHip = front ? 0f : -4f;
                float idleKnee = 1.5f * breathe;
                hip = Mathf.Lerp(idleHip, hip, run);
                knee = Mathf.Lerp(idleKnee, knee, run);
                hip = Mathf.Lerp(hip, front ? 28f : 38f, crouch);
                knee = Mathf.Lerp(knee, front ? -55f : -75f, crouch);

                Hips[i].localRotation = Quaternion.Euler(hip, 0f, 0f);
                Knees[i].localRotation = Quaternion.Euler(knee, 0f, 0f);
            }

            // Body: rises during the airborne phase, pitches with the push-off; low and tense when crouched.
            float lift = (0.5f + 0.5f * Mathf.Sin(w)) * Bob * run;
            float sway = 0.03f * breathe * (1f - run);
            Body.localPosition = new Vector3(0f, lift + sway * 0.5f - 0.2f * crouch + 0.02f * breathe * crouch, 0f);
            float pitch = Pitch * Mathf.Sin(w + Mathf.PI * 0.5f) * run;
            Body.localRotation = Quaternion.Euler(pitch + 3f * crouch, 0f, 1.2f * Mathf.Sin(w * 0.5f) * run);

            // Spine: gathers and extends (strongly in cats, barely in rhinos)
            float flex = Flex * Mathf.Sin(w) * run;
            if (Front != null) Front.localRotation = Quaternion.Euler(flex - 4f * crouch, 0f, 0f);
            if (Rear != null) Rear.localRotation = Quaternion.Euler(-flex + 3f * crouch, 0f, 0f);

            // Head holds steady against the bounce; looks around a little while idle.
            if (Head != null)
            {
                float look = Mathf.Sin(t * 0.45f) * 14f * (1f - run) * (1f - crouch);
                Head.localRotation = Quaternion.Euler(-pitch - flex * 0.6f + 2f * breathe * (1f - run) + 4f * crouch, look, 0f);
            }
            if (Tail != null)
            {
                float twitch = Cat ? 18f * Mathf.Sin(t * 5f) * Mathf.Sin(t * 1.1f) * crouch : 0f;
                Tail.localRotation = Quaternion.Euler(-15f - 12f * Mathf.Sin(w) * run + 6f * Mathf.Sin(t * 1.4f) * (1f - run),
                                                      twitch + 8f * Mathf.Sin(t * 0.9f) * (1f - run), 0f);
            }
        }
    }

    /// <summary>
    /// Woolly mammoth: shifts its weight, swings its trunk and flaps its ears; as the runner approaches it
    /// raises its trunk to trumpet and stamps its front feet, throwing up snow.
    /// </summary>
    public class MammothRig : CreatureRig
    {
        public Transform Body, Head, EarL, EarR;
        public Transform[] Trunk, Hips, Knees;
        float alert, stampClock;
        int stampLeg;
        bool down = true;

        void Update()
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            time += dt;
            alert = Mathf.MoveTowards(alert, Gap() < 30f ? 1f : 0f, dt * 1.2f);
            float a = Mathf.SmoothStep(0f, 1f, alert);

            // Stamp cycle: lift a front foot slowly, hold, slam it down.
            float lift = 0f;
            if (a > 0.5f)
            {
                stampClock += dt / 1.6f;
                if (stampClock >= 1f) { stampClock -= 1f; stampLeg = 1 - stampLeg; }
                float c = stampClock;
                lift = c < 0.4f ? Mathf.SmoothStep(0f, 1f, c / 0.4f) : c < 0.6f ? 1f : c < 0.68f ? 1f - (c - 0.6f) / 0.08f : 0f;
                bool isDown = lift <= 0f;
                if (isDown && !down)
                {
                    var foot = Knees[stampLeg].TransformPoint(0f, -0.95f, 0f);
                    Fx.Dust(foot, 7, 1.3f, new Color(0.95f, 0.97f, 1f, 0.7f));
                }
                down = isDown;
            }
            else stampClock = 0f;

            Pose(a, time, lift, stampLeg);
        }

        public void Pose(float alert, float t, float lift, int leg = 0)
        {
            // Slow weight shift from side to side; rears back a touch when alarmed.
            float shift = Mathf.Sin(t * 0.8f);
            Body.localPosition = new Vector3(0.06f * shift * (1f - alert), 0.03f * Mathf.Sin(t * 1.6f) + 0.12f * lift, 0.15f * alert);
            Body.localRotation = Quaternion.Euler(4f * alert + 2.5f * lift, 0f, 2f * shift * (1f - alert) + (leg == 0 ? -2f : 2f) * lift);

            Head.localRotation = Quaternion.Euler(Mathf.Lerp(4f * Mathf.Sin(t * 0.7f), 14f, alert) + 4f * lift,
                                                  6f * Mathf.Sin(t * 0.5f) * (1f - alert) + 8f * Mathf.Sin(t * 2.2f) * alert, 0f);
            float flap = Mathf.Sin(t * (1.6f + 3f * alert));
            EarL.localRotation = Quaternion.Euler(0f, 12f * flap + 30f * alert, 0f);
            EarR.localRotation = Quaternion.Euler(0f, -12f * flap - 30f * alert, 0f);

            for (int i = 0; i < Trunk.Length; i++)
            {
                // Relaxed: hangs and swings with a wave down its length. Alert: curls up and back to trumpet.
                float swing = 9f * Mathf.Sin(t * 1.3f - i * 0.55f);
                float x = Mathf.Lerp(5f + swing, 22f + i * 3.5f + 4f * Mathf.Sin(t * 6f - i), alert);
                float z = Mathf.Lerp(7f * Mathf.Sin(t * 0.9f - i * 0.5f), 0f, alert);
                Trunk[i].localRotation = Quaternion.Euler(x, 0f, z);
            }

            for (int i = 0; i < Hips.Length; i++)
            {
                bool front = i < 2;
                float settle = 2f * Mathf.Sin(t * 0.8f + i * 1.6f) * (1f - alert);
                float hip = settle, knee = -1.5f * Mathf.Abs(settle);
                if (front && i == leg) { hip += 30f * lift; knee -= 55f * lift; }
                if (!front) { hip -= 6f * alert; knee += 4f * alert; } // hind legs brace
                Hips[i].localRotation = Quaternion.Euler(hip, 0f, 0f);
                Knees[i].localRotation = Quaternion.Euler(knee, 0f, 0f);
            }
        }
    }

    /// <summary>Crawling animal crossing the trail sideways while the runner approaches (crab, scorpion).</summary>
    public abstract class CrawlerRig : CreatureRig
    {
        public Transform Model;
        protected float dir, travel, stride;
        protected BoxCollider col;
        const float MaxTravel = 1.1f;

        protected virtual bool FaceTravel => true;

        public virtual void Init()
        {
            dir = Random.value < 0.5f ? 1f : -1f;
            col = GetComponent<BoxCollider>();
            // Faces the direction it crosses in (unless it walks sideways).
            Model.localRotation = FaceTravel ? Quaternion.Euler(0f, dir > 0f ? -90f : 90f, 0f) : Quaternion.identity;
            Model.localPosition = new Vector3(dir * MaxTravel * 0.5f, 0f, 0f);
            Animate(0f, 0f);
        }

        void Update()
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            time += dt;
            float speed = Gap() < 30f && travel < MaxTravel ? 0.55f : 0f;
            travel += speed * dt;
            stride += speed * dt * 4f + dt * 0.4f;
            Model.localPosition = new Vector3(dir * (MaxTravel * 0.5f - travel), 0f, 0f);
            if (col != null) col.center = new Vector3(Model.localPosition.x, col.center.y, 0f);
            Animate(stride, speed);
        }

        protected abstract void Animate(float stride, float speed);
    }

    /// <summary>
    /// Giant octopus hauled up on the boardwalk: its eight arms curl and writhe across the planks,
    /// then rear up and reach toward the runner as they approach.
    /// </summary>
    public class OctopusRig : CreatureRig
    {
        public const int Segments = 12;
        public const float SegLength = 0.2f;
        public Transform Mantle;
        public Transform[][] Arms;
        float alert;
        readonly Vector3[] pts = new Vector3[Segments + 1];

        void Update()
        {
            if (!Active) return;
            time += Time.deltaTime;
            alert = Mathf.MoveTowards(alert, Gap() < 24f ? 1f : 0f, Time.deltaTime * 1.3f);
            Pose(Mathf.SmoothStep(0f, 1f, alert), time);
        }

        public void Pose(float alert, float t)
        {
            float breathe = 1f + 0.06f * Mathf.Sin(t * 1.8f);
            Mantle.localScale = new Vector3(breathe, 1f + 0.04f * Mathf.Sin(t * 1.8f + 1f), breathe);
            Mantle.localRotation = Quaternion.Euler(4f * Mathf.Sin(t * 0.9f), 0f, 5f * Mathf.Sin(t * 0.7f));

            for (int a = 0; a < Arms.Length; a++)
            {
                float ang = a / (float)Arms.Length * Mathf.PI * 2f + 0.2f;
                var outDir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                // Arms on the runner's side (-Z) reach toward them when alert.
                float front = Mathf.Clamp01(-outDir.z);
                var reach = (outDir + new Vector3(0f, 0f, -0.9f) * alert * front).normalized;
                var side = Vector3.Cross(Vector3.up, reach);
                var start = new Vector3(0f, 0.95f, 0.2f) + outDir * 0.38f;
                for (int k = 0; k <= Segments; k++)
                {
                    float u = k / (float)Segments;
                    float horizontal = 0.15f + u * Mathf.Lerp(1.5f, 1.15f, alert);
                    // Resting: draped down onto the boardwalk and curling. Alert: raised in a writhing arc.
                    float rest = Mathf.Lerp(0f, -0.9f, Mathf.Sqrt(u)) + 0.25f * u * u;
                    float raised = 1.7f * Mathf.Sin(Mathf.PI * u * 0.85f) * (0.6f + 0.4f * front);
                    float y = Mathf.Lerp(rest, raised, alert);
                    float writhe = (0.12f + 0.25f * alert) * Mathf.Sin(t * (2.2f + a * 0.17f) + u * 6f + a) * u;
                    var p = start + reach * horizontal + Vector3.up * y + side * writhe;
                    p.y = Mathf.Max(0.06f, p.y);
                    pts[k] = p;
                }
                for (int i = 0; i < Segments; i++)
                {
                    var d = pts[i + 1] - pts[i];
                    var seg = Arms[a][i];
                    seg.localPosition = pts[i];
                    seg.localRotation = Quaternion.FromToRotation(Vector3.down, d.normalized);
                    seg.localScale = new Vector3(1f, d.magnitude / SegLength * 1.15f, 1f);
                }
            }
        }
    }

    /// <summary>Giant crab: scuttles sideways across the boardwalk, claws raised and snapping.</summary>
    public class CrabRig : CrawlerRig
    {
        public Transform Body;
        public Transform[] Claws, Pincers, Hips, Knees;

        protected override bool FaceTravel => false; // crabs face forward and walk sideways

        protected override void Animate(float stride, float speed)
        {
            float moving = Mathf.Clamp01(speed * 3f);
            Body.localPosition = new Vector3(0f, 0.42f + 0.025f * Mathf.Abs(Mathf.Sin(stride * 6f)) * moving + 0.01f * Mathf.Sin(time * 2f), 0f);
            Body.localRotation = Quaternion.Euler(0f, 0f, 3f * Mathf.Sin(stride * 6f) * moving);

            for (int i = 0; i < Hips.Length; i++)
            {
                float side = i < 4 ? -1f : 1f;
                int idx = i % 4;
                float phase = ((idx % 2 == 0) == (side < 0)) ? 0f : Mathf.PI;
                float spread = Mathf.Lerp(25f, -25f, idx / 3f);
                float swing = Mathf.Sin(stride * 3f + phase) * 12f * moving;
                float lift = Mathf.Max(0f, Mathf.Cos(stride * 3f + phase)) * 22f * moving;
                Hips[i].localRotation = Quaternion.Euler(0f, -side * spread, side * (25f + lift + swing));
                Knees[i].localRotation = Quaternion.Euler(0f, 0f, -side * (10f - lift * 0.3f));
            }

            // Claws held up and forward (the crab faces -Z), waving and snapping.
            for (int k = 0; k < 2; k++)
            {
                float s = k == 0 ? -1f : 1f;
                float wave = Mathf.Sin(time * 1.7f + k * 1.3f);
                Claws[k].localRotation = Quaternion.Euler(105f + 12f * wave, 0f, s * -25f);
                float snap = Mathf.Pow(Mathf.Abs(Mathf.Sin(time * 4f + k)), 0.5f);
                Pincers[k].localRotation = Quaternion.Euler(0f, 0f, s * Mathf.Lerp(2f, 32f, snap));
            }
        }
    }

    /// <summary>
    /// Giant scorpion: crosses the trail on eight legs in an alternating tetrapod gait (L1-R2-L3-R4 together),
    /// pincers held out and snapping, tail arched over the back. Close to the runner it rears and strikes.
    /// </summary>
    public class ScorpionRig : CrawlerRig
    {
        public Transform Body, Sting;
        public Transform[] Tail, Palps, Pincers, Hips, Knees;
        static readonly float[] TailRest = { 48f, -28f, -34f, -36f, -30f };
        float strike, strikeT = -1f;

        protected override void Animate(float stride, float speed)
        {
            float moving = Mathf.Clamp01(speed * 3f);
            float gap = Gap();

            // Strike: a fast whip of the tail toward the runner (each time it comes in range).
            if (strikeT < 0f && gap < 9f && gap > 0f) strikeT = 0f;
            if (strikeT >= 0f)
            {
                strikeT += Time.deltaTime;
                strike = strikeT < 0.18f ? strikeT / 0.18f : Mathf.Max(0f, 1f - (strikeT - 0.18f) / 0.5f);
                if (strikeT > 1.6f) strikeT = -1f;
            }
            else strike = 0f;

            float step = stride * 3.2f;
            Body.localPosition = new Vector3(0f, 0.3f + 0.015f * Mathf.Abs(Mathf.Sin(step)) * moving + 0.006f * Mathf.Sin(time * 2f), 0f);
            Body.localRotation = Quaternion.Euler(-3f * strike + 1.2f * Mathf.Sin(step * 2f) * moving, 2f * Mathf.Sin(step) * moving, 1.5f * Mathf.Sin(step) * moving);

            for (int i = 0; i < Hips.Length; i++)
            {
                float side = i < 4 ? -1f : 1f;
                int idx = i % 4;
                // Alternating tetrapod: L1, R2, L3, R4 move together; the other four are planted.
                bool groupA = (idx % 2 == 0) == (side < 0);
                float ph = step + (groupA ? 0f : Mathf.PI);
                float spread = Mathf.Lerp(42f, -48f, idx / 3f);
                float swing = Mathf.Sin(ph) * 16f * moving;
                float lift = Mathf.Max(0f, Mathf.Cos(ph)) * 20f * moving;
                Hips[i].localRotation = Quaternion.Euler(0f, -side * (spread + swing), side * (28f + lift));
                Knees[i].localRotation = Quaternion.Euler(0f, 0f, -side * (14f - lift * 0.4f));
            }

            // Tail: a slow sway down its length; the strike curls it forward and drives the sting down.
            for (int i = 0; i < Tail.Length; i++)
            {
                float sway = Mathf.Sin(time * 1.4f - i * 0.6f);
                float x = TailRest[i] + 3f * sway + (i == 0 ? 10f * strike : -14f * strike);
                Tail[i].localRotation = Quaternion.Euler(x, 0f, 4f * Mathf.Sin(time * 0.9f - i * 0.5f));
            }
            Sting.localRotation = Quaternion.Euler(-20f - 45f * strike + 6f * Mathf.Sin(time * 3f), 0f, 0f);

            // Pincers: held forward, weaving, snapping open and shut
            for (int k = 0; k < 2; k++)
            {
                float s = k == 0 ? -1f : 1f;
                float weave = Mathf.Sin(time * 1.5f + k * 1.7f);
                Palps[k].localRotation = Quaternion.Euler(95f + 10f * weave + 15f * strike, s * -22f, s * -10f);
                float snap = Mathf.Pow(Mathf.Abs(Mathf.Sin(time * 3.4f + k * 1.3f)), 0.6f);
                Pincers[k].localRotation = Quaternion.Euler(0f, 0f, s * Mathf.Lerp(3f, 30f, snap));
            }
        }
    }

    /// <summary>
    /// Ostrich sprint. Two long legs with a backward-bending ankle: each foot strikes ahead, sweeps back
    /// under the body and, on the swing, folds tightly up before reaching forward again. The body rises and
    /// rolls with each step, the wings spread for balance, and the long neck absorbs the bounce so the
    /// head stays level and steady.
    /// </summary>
    public class OstrichRig : GaitRig
    {
        public Transform Body, Head, TailPlume;
        public Transform[] Neck, Wings, Hips, Knees, Ankles;
        public float StrideLength = 3.6f;   // metres per full cycle (two steps)
        const float Duty = 0.38f, BodyY = 1.22f;
        static readonly float[] NeckIdle = { -12f, 4f, 8f, 6f, 2f };
        static readonly float[] NeckRun = { -42f, 10f, 12f, 10f, 8f };
        static readonly Color Dust = new Color(0.86f, 0.72f, 0.5f, 0.55f);

        float cycle, run, time;
        readonly float[] prev = new float[2];

        void Start() => time = Random.value * 10f;

        public void Snap() => Pose(0f, 0f, 0f);

        void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            run = Mathf.MoveTowards(run, Speed > 0.5f ? 1f : 0f, dt * 3f);
            cycle += Speed / StrideLength * dt;
            if (cycle > 1000f) cycle -= 1000f;
            Pose(cycle, run, time);

            if (run > 0.5f && Speed > 2f)
                for (int k = 0; k < 2; k++)
                {
                    float p = Mathf.Repeat(cycle + k * 0.5f, 1f);
                    if (p < prev[k]) Fx.Dust(Ankles[k].TransformPoint(0f, -0.47f, -0.08f), 3, 0.45f, Dust);
                    prev[k] = p;
                }
        }

        public void Pose(float cyc, float run, float t)
        {
            float w = cyc * Mathf.PI * 2f;
            for (int k = 0; k < 2; k++)
            {
                float p = Mathf.Repeat(cyc + k * 0.5f, 1f);
                float hip, knee, ankle;
                if (p < Duty)
                {
                    // Stance: foot planted, sweeping back; the leg compresses mid-stance like a spring.
                    float u = p / Duty;
                    float sq = Mathf.Sin(u * Mathf.PI);
                    hip = Mathf.Lerp(52f, -2f, u);
                    knee = -75f - 12f * sq;
                    ankle = 45f + 18f * sq;
                }
                else
                {
                    // Swing: fold up tight under the body, then reach out for the next strike.
                    float u = (p - Duty) / (1f - Duty);
                    float e = u * u * (3f - 2f * u);
                    float fold = Mathf.Sin(u * Mathf.PI);
                    hip = Mathf.Lerp(-2f, 52f, e);
                    knee = -75f - 40f * fold;
                    ankle = 45f + 85f * fold;
                }
                float idleShift = 2f * Mathf.Sin(t * 0.7f + k * Mathf.PI);
                Hips[k].localRotation = Quaternion.Euler(Mathf.Lerp(30f + idleShift, hip, run), 0f, 0f);
                Knees[k].localRotation = Quaternion.Euler(Mathf.Lerp(-75f, knee, run), 0f, 0f);
                Ankles[k].localRotation = Quaternion.Euler(Mathf.Lerp(45f, ankle, run), 0f, 0f);
            }

            // Body: two bounces per cycle (one per step), a lean into the run and a roll over the stance leg.
            float bounce = 0.5f + 0.5f * Mathf.Cos(w * 2f);
            float breathe = Mathf.Sin(t * 1.5f);
            Body.localPosition = new Vector3(0f, BodyY + 0.07f * bounce * run + 0.005f * breathe, 0f);
            float pitch = Mathf.Lerp(-2f, -11f, run) + 2.5f * Mathf.Sin(w * 2f) * run;
            float roll = 3.5f * Mathf.Sin(w) * run;
            Body.localRotation = Quaternion.Euler(pitch, 0f, roll);

            // Neck: stretched forward when running, absorbing the bounce; pecks and looks around when idle.
            float peck = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 0.5f + 1f)), 8f) * (1f - run);
            float sum = pitch;
            for (int i = 0; i < Neck.Length; i++)
            {
                float a = Mathf.Lerp(NeckIdle[i], NeckRun[i], run);
                if (i == 0) a += -4f * Mathf.Cos(w * 2f) * run - 55f * peck;
                if (i == 1) a += 3f * Mathf.Cos(w * 2f) * run + 18f * peck;
                float yaw = i == 0 ? 25f * Mathf.Sin(t * 0.4f) * (1f - run) : 0f;
                Neck[i].localRotation = Quaternion.Euler(a, yaw, -roll * 0.3f);
                sum += a;
            }
            // Keep the beak pointing forward, whatever the neck does
            Head.localRotation = Quaternion.Euler(-sum + 4f * Mathf.Sin(t * 1.3f) * (1f - run) - 40f * peck, 0f, 0f);

            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                float raise = Mathf.Lerp(-6f, 28f + 10f * Mathf.Sin(w * 2f + k), run);
                Wings[k].localRotation = Quaternion.Euler(0f, side * Mathf.Lerp(0f, -15f, run), side * raise);
            }
            TailPlume.localRotation = Quaternion.Euler(-8f * bounce * run + 3f * breathe, 0f, 5f * Mathf.Sin(t * 1.1f));
        }
    }

    /// <summary>
    /// Dromedary blocking the trail. It chews the cud with a sideways grinding jaw, flicks its ears and
    /// tail and shifts its weight. As the runner approaches it lifts its head, pins its ears, groans and
    /// stamps its front feet, kicking up sand. The hump is a damped spring: it wobbles and settles with
    /// every movement of the body.
    /// </summary>
    public class CamelRig : CreatureRig
    {
        public Transform Body, Barrel, Hump, Head, Jaw, EarL, EarR, Tail;
        public Transform[] Neck, Hips, Knees;
        public float[] NeckRest;
        /// <summary>Walking speed in m/s (caravans). Camels pace: both legs on one side swing together.</summary>
        [System.NonSerialized] public float Walk;
        float cycle;
        static readonly Color Sand = new Color(0.86f, 0.72f, 0.5f, 0.6f);

        float alert, stampClock, earTimer, earFlick, swishT;
        int stampLeg;
        bool down = true;
        // Hump spring state (offset and velocity) and the previous barrel motion it reacts to
        float humpY, humpV, humpRoll, humpRollV, prevY, prevVy, prevRoll, prevRollV;

        void Update()
        {
            if (!Active) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            time += dt;
            alert = Mathf.MoveTowards(alert, Gap() < 28f ? 1f : 0f, dt * 1.1f);
            float a = Mathf.SmoothStep(0f, 1f, alert);

            // Stamp: lift a front foot, hold it, then bring it down hard.
            float lift = 0f;
            if (a > 0.5f)
            {
                stampClock += dt / 1.3f;
                if (stampClock >= 1f) { stampClock -= 1f; stampLeg = 1 - stampLeg; }
                float c = stampClock;
                lift = c < 0.35f ? Mathf.SmoothStep(0f, 1f, c / 0.35f) : c < 0.5f ? 1f : c < 0.57f ? 1f - (c - 0.5f) / 0.07f : 0f;
                bool isDown = lift <= 0f;
                if (isDown && !down)
                {
                    Fx.Dust(Knees[stampLeg].TransformPoint(0f, -0.66f, 0f), 6, 0.9f, Sand);
                    humpV -= 1.6f; // the jolt sets the hump wobbling
                }
                down = isDown;
            }
            else stampClock = 0f;

            // Random ear flicks and tail swishes
            if ((earTimer -= dt) <= 0f) { earTimer = Random.Range(1.5f, 4f); earFlick = 1f; }
            earFlick = Mathf.MoveTowards(earFlick, 0f, dt * 4f);
            swishT += dt * (1.5f + 1.5f * a);

            cycle += Walk / 2.6f * dt;
            Pose(a, time, lift);

            // Hump: spring-damper driven by the barrel's vertical and rolling acceleration.
            float y = Barrel.localPosition.y, roll = Barrel.localEulerAngles.z;
            if (roll > 180f) roll -= 360f;
            float vy = (y - prevY) / dt, ay = (vy - prevVy) / dt;
            float vr = (roll - prevRoll) / dt, ar = (vr - prevRollV) / dt;
            prevY = y; prevVy = vy; prevRoll = roll; prevRollV = vr;
            ay = Mathf.Clamp(ay, -40f, 40f);
            ar = Mathf.Clamp(ar, -2000f, 2000f);
            humpV += (-90f * humpY - 7f * humpV - ay * 0.05f) * dt;
            humpY += humpV * dt;
            humpRollV += (-70f * humpRoll - 6f * humpRollV - ar * 0.02f) * dt;
            humpRoll += humpRollV * dt;
            humpY = Mathf.Clamp(humpY, -0.08f, 0.08f);
            humpRoll = Mathf.Clamp(humpRoll, -8f, 8f);
            Hump.localPosition = new Vector3(0f, 0.32f + humpY, 0f);
            Hump.localRotation = Quaternion.Euler(0f, 0f, humpRoll);
            Hump.localScale = new Vector3(1f - humpY * 0.8f, 1f + humpY * 1.6f, 1f - humpY * 0.8f);
        }

        public void Pose(float alert, float t, float lift, int leg = 0)
        {
            // Weight shift from side to side; settles back a touch when alarmed; dips as a foot stamps down.
            // Walking (pacing), the body rocks strongly from side to side and bobs twice per stride.
            float walking = Walk > 0.05f ? 1f : 0f;
            float w = cycle * Mathf.PI * 2f;
            float shift = Mathf.Sin(t * 0.55f);
            Barrel.localPosition = new Vector3(0.04f * shift * (1f - alert) + 0.05f * Mathf.Sin(w) * walking,
                                               1.62f + 0.012f * Mathf.Sin(t * 1.3f) + 0.05f * lift + 0.03f * Mathf.Cos(w * 2f) * walking, 0.08f * alert);
            Barrel.localRotation = Quaternion.Euler(2f * alert + 1.5f * Mathf.Sin(w * 2f) * walking, 0f,
                                                    1.6f * shift * (1f - alert) + (leg == 0 ? -2.5f : 2.5f) * lift + 4.5f * Mathf.Sin(w) * walking);

            // Neck: slow bob while chewing, head lifted high and turned to face the runner when alert
            for (int i = 0; i < Neck.Length; i++)
            {
                float x = NeckRest[i];
                if (i == 0) x += 3f * Mathf.Sin(t * 0.6f) + 16f * alert + 5f * Mathf.Sin(w * 2f + 0.6f) * walking;
                if (i == 3) x -= 8f * alert;
                float yaw = i == 1 ? 18f * Mathf.Sin(t * 0.25f) * (1f - alert) : 0f;
                Neck[i].localRotation = Quaternion.Euler(x, yaw, 0f);
            }
            Head.localRotation = Quaternion.Euler(4f * Mathf.Sin(t * 0.9f) - 8f * alert, 0f, 3f * Mathf.Sin(t * 0.7f));

            // Chewing the cud: the lower jaw grinds sideways. Alert: periodic open-mouthed groan.
            float chew = Mathf.Sin(t * 3.4f);
            float groan = alert * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 0.9f)), 3f);
            Jaw.localRotation = Quaternion.Euler(-4f - 3f * Mathf.Abs(chew) - 24f * groan, 9f * chew * (1f - groan), 0f);

            float pin = 35f * alert;
            EarL.localRotation = Quaternion.Euler(pin + 20f * earFlick, 0f, 10f * earFlick);
            EarR.localRotation = Quaternion.Euler(pin + 12f * earFlick * Mathf.Sin(t * 5f), 0f, -6f * earFlick);
            Tail.localRotation = Quaternion.Euler(8f + 6f * Mathf.Sin(swishT * 0.7f), 0f, 24f * Mathf.Sin(swishT));

            for (int i = 0; i < Hips.Length; i++)
            {
                bool front = i < 2;
                float settle = 1.5f * Mathf.Sin(t * 0.55f + (i % 2) * Mathf.PI) * (1f - alert) * (1f - walking);
                float hip = settle, knee = -1.2f * Mathf.Abs(settle);
                if (front && i == leg) { hip += 32f * lift; knee -= 70f * lift; }
                if (!front) { hip -= 5f * alert; knee += 6f * alert; } // hind legs brace
                if (walking > 0f)
                {
                    // Pace: left legs (0, 2) together, right legs (1, 3) half a cycle later.
                    float ph = w + (i % 2) * Mathf.PI + (front ? 0f : 0.25f);
                    hip += 20f * Mathf.Sin(ph);
                    knee -= (front ? 45f : 35f) * Mathf.Max(0f, Mathf.Cos(ph));
                }
                Hips[i].localRotation = Quaternion.Euler(hip, 0f, 0f);
                Knees[i].localRotation = Quaternion.Euler(knee, 0f, 0f);
            }
        }
    }
}
