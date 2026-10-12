using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>The animal (or monster) filling an obstacle role in a given world.</summary>
    public enum Creature { None, Python, Boar, Tiger, Bear, Mammoth, WoollyRhino, Sabertooth, Crab, Crocodile, Hippo, Octopus, Spider, Werewolf, Ghoul, PoisonPods, StranglerVines, Flytrap }

    /// <summary>
    /// World-specific creatures. Each world fills the same gameplay roles with its own animals:
    /// low crawler (jump over), charger, fast charger, and lane blocker.
    /// </summary>
    public static class Creatures
    {
        public static Creature For(ObstacleType role, BiomeType world)
        {
            // The horror jungle turns its plants against you: most logs, branches and boulders
            // there become toxic pods, strangler vines and giant flytraps.
            if (world == BiomeType.HorrorJungle && Random.value < 0.7f)
            {
                if (role == ObstacleType.Log) return Creature.PoisonPods;
                if (role == ObstacleType.Branch) return Creature.StranglerVines;
                if (role == ObstacleType.Boulder) return Creature.Flytrap;
            }
            switch (role)
            {
                case ObstacleType.Snake:
                    return world == BiomeType.WaterWorld ? Creature.Crab : world == BiomeType.HorrorJungle ? Creature.Spider : Creature.Python;
                case ObstacleType.Boar:
                    return world == BiomeType.IceAge ? Creature.WoollyRhino : world == BiomeType.WaterWorld ? Creature.Crocodile : Creature.Boar;
                case ObstacleType.Tiger:
                    return world == BiomeType.IceAge ? Creature.Sabertooth : world == BiomeType.WaterWorld ? Creature.Hippo
                         : world == BiomeType.HorrorJungle ? Creature.Werewolf : Creature.Tiger;
                case ObstacleType.Bear:
                    return world == BiomeType.IceAge ? Creature.Mammoth : world == BiomeType.WaterWorld ? Creature.Octopus
                         : world == BiomeType.HorrorJungle ? Creature.Ghoul : Creature.Bear;
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
                case Creature.Spider: return "Snared by a giant spider!";
                case Creature.Werewolf: return "Mauled by a werewolf!";
                case Creature.Ghoul: return "Grabbed by a swamp ghoul!";
                case Creature.PoisonPods: return "Poisoned by toxic pods!";
                case Creature.StranglerVines: return "Strangled by killer vines!";
                case Creature.Flytrap: return "Snapped up by a giant flytrap!";
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

        // ---------------- Big cats & wolves (fast chargers) ----------------

        public enum CatKind { Sabertooth, Werewolf }

        public static void BuildBigCat(Transform t, CatKind kind, out Transform[] legs, out Transform body)
        {
            bool wolf = kind == CatKind.Werewolf;
            var fur = wolf ? Fur("WerewolfFur", new Color(0.2f, 0.19f, 0.18f)) : Fur("SabreFur", new Color(0.72f, 0.55f, 0.34f));
            var dark = wolf ? Fur("WerewolfDark", new Color(0.09f, 0.085f, 0.08f)) : Fur("SabreDark", new Color(0.46f, 0.32f, 0.18f));
            var light = wolf ? Fur("WerewolfBelly", new Color(0.32f, 0.3f, 0.28f)) : Fur("SabreBelly", new Color(0.9f, 0.84f, 0.7f));
            var eye = wolf ? Glow("WolfEye", new Color(1f, 0.85f, 0.1f), new Color(3f, 2.2f, 0.2f)) : Glow("SabreEye", new Color(0.8f, 0.6f, 0.1f), new Color(0.4f, 0.3f, 0.02f));

            var rig = t.gameObject.AddComponent<QuadRig>();
            rig.Cat = true;
            rig.DustColor = wolf ? new Color(0.25f, 0.27f, 0.25f, 0.5f) : new Color(0.95f, 0.97f, 1f, 0.65f);
            body = rig.Body = Prim.Group("Body", t, Vector3.zero);
            var front = rig.Front = Prim.Group("Front", body, new Vector3(0f, 0.95f, -0.15f));
            var rear = rig.Rear = Prim.Group("Rear", body, new Vector3(0f, 0.93f, 0.2f));
            float s = wolf ? 1.08f : 1f;

            // Heavy, muscular forequarters; leaner hindquarters
            P(wolf ? Lump(1) : Ball, "Chest", front, new Vector3(0f, 0.02f, -0.35f), new Vector3(0.78f, 0.86f, 0.9f) * s, fur);
            P(Ball, "Shoulders", front, new Vector3(0f, 0.24f, -0.22f), new Vector3(0.72f, 0.5f, 0.62f) * s, dark);
            P(Ball, "ChestFur", front, new Vector3(0f, -0.24f, -0.3f), new Vector3(0.5f, 0.32f, 0.7f), light);
            P(Ball, "Neck", front, new Vector3(0f, 0.16f, -0.65f), new Vector3(0.46f, 0.48f, 0.52f) * s, fur);
            P(wolf ? Lump(0) : Ball, "Back", rear, new Vector3(0f, 0f, 0.15f), new Vector3(0.64f, 0.66f, 1.0f) * s, fur);
            P(Ball, "Haunch", rear, new Vector3(0f, 0.03f, 0.55f), new Vector3(0.7f, 0.74f, 0.6f) * s, fur);
            P(Ball, "Belly", rear, new Vector3(0f, -0.24f, 0.15f), new Vector3(0.46f, 0.28f, 0.85f), light);
            if (wolf)
            {
                for (int i = 0; i < 6; i++)
                    P(Lump(i), "Mane", i < 3 ? front : rear, new Vector3(0f, 0.32f - i * 0.02f, i < 3 ? -0.55f + i * 0.2f : -0.1f + (i - 3) * 0.25f),
                      new Vector3(0.42f, 0.32f, 0.42f), i % 2 == 0 ? dark : fur);
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
            if (wolf)
            {
                P(Lump(2), "Skull", head, Vector3.zero, new Vector3(0.42f, 0.4f, 0.44f), fur);
                P(Ball, "Snout", head, new Vector3(0f, -0.07f, -0.33f), new Vector3(0.2f, 0.18f, 0.44f), dark);
                P(Ball, "Nose", head, new Vector3(0f, -0.02f, -0.55f), new Vector3(0.08f, 0.06f, 0.06f), Black);
                P(Ball, "Jaw", head, new Vector3(0f, -0.15f, -0.28f), new Vector3(0.16f, 0.07f, 0.34f), dark);
                for (int k = -1; k <= 1; k += 2)
                {
                    P(Cone(0.26f, 0.07f), "Ear", head, new Vector3(k * 0.13f, 0.17f, 0.04f), Vector3.one, dark, new Vector3(-10f, 0f, k * -15f));
                    P(Cone(0.08f, 0.012f), "Fang", head, new Vector3(k * 0.05f, -0.15f, -0.42f), Vector3.one, Ivory, new Vector3(180f, 0f, 0f), false);
                    P(Ball, "Eye", head, new Vector3(k * 0.1f, 0.06f, -0.18f), new Vector3(0.06f, 0.04f, 0.04f), eye, null, false);
                }
            }
            else
            {
                P(Ball, "Skull", head, Vector3.zero, new Vector3(0.46f, 0.42f, 0.46f), fur);
                P(Ball, "Cheeks", head, new Vector3(0f, -0.06f, -0.05f), new Vector3(0.52f, 0.3f, 0.36f), fur);
                P(Ball, "Muzzle", head, new Vector3(0f, -0.1f, -0.22f), new Vector3(0.26f, 0.18f, 0.2f), light);
                P(Ball, "Nose", head, new Vector3(0f, -0.04f, -0.31f), new Vector3(0.07f, 0.05f, 0.05f), Black);
                for (int k = -1; k <= 1; k += 2)
                {
                    P(Ball, "Ear", head, new Vector3(k * 0.16f, 0.19f, 0.05f), new Vector3(0.1f, 0.1f, 0.05f), dark);
                    P(Ball, "Eye", head, new Vector3(k * 0.1f, 0.05f, -0.2f), new Vector3(0.06f, 0.045f, 0.04f), eye, null, false);
                    // The famous sabre fangs, curving down past the jaw
                    var fang = new List<Vector3> { Vector3.zero, new Vector3(0f, -0.12f, -0.015f), new Vector3(0f, -0.24f, 0.01f), new Vector3(0f, -0.32f, 0.05f) };
                    P(Curve(fang, 0.03f, 0.004f), "Sabre", head, new Vector3(k * 0.06f, -0.13f, -0.24f), Vector3.one, Ivory);
                }
            }

            rig.Tail = Prim.Group("Tail", rear, new Vector3(0f, 0.1f, 0.82f));
            if (wolf)
                for (int i = 0; i < 3; i++)
                    P(Lump(i + 1), "Tail", rig.Tail, new Vector3(0f, -i * 0.12f, i * 0.28f), new Vector3(0.26f, 0.26f, 0.36f) * (1f - i * 0.15f), dark);
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
                    isFront ? 0.42f : 0.44f, rig.LowerLength[i], 0.13f * s, 0.095f, 0.07f, fur, dark, paw, out rig.Hips[i], out rig.Knees[i]);
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

        // ---------------- Swamp ghoul (blocker) ----------------

        public static void BuildGhoul(Transform t)
        {
            var skin = Mats.Lit("GhoulSkin", Tex.Skin, new Color(0.42f, 0.48f, 0.4f), 0.45f, bump: 2f);
            var rag = Mats.Fabric(new Color(0.18f, 0.2f, 0.16f));
            var hair = Fur("GhoulHair", new Color(0.08f, 0.08f, 0.07f));
            var eye = Glow("GhoulEye", new Color(0.6f, 1f, 0.5f), new Color(1.2f, 3.5f, 1f));
            var rig = t.gameObject.AddComponent<GhoulRig>();
            var body = rig.Body = Prim.Group("Body", t, Vector3.zero);

            for (int k = -1; k <= 1; k += 2)
            {
                var leg = Prim.Group("Leg", body, new Vector3(k * 0.12f, 0.92f, 0f));
                P(Limb(0.46f, 0.075f, 0.06f), "Thigh", leg, Vector3.zero, Vector3.one, rag);
                P(Limb(0.46f, 0.055f, 0.045f), "Shin", leg, new Vector3(0f, -0.44f, 0f), Vector3.one, skin);
                P(Ball, "Foot", leg, new Vector3(0f, -0.9f, -0.05f), new Vector3(0.1f, 0.06f, 0.24f), skin);
            }
            var spine = rig.Spine = Prim.Group("Spine", body, new Vector3(0f, 0.95f, 0f));
            P(Lump(0), "Torso", spine, new Vector3(0f, 0.32f, 0f), new Vector3(0.42f, 0.62f, 0.3f), skin);
            P(Lump(1), "Rags", spine, new Vector3(0f, 0.18f, 0.01f), new Vector3(0.46f, 0.42f, 0.33f), rag);
            for (int i = 0; i < 5; i++)
                P(Meshes.RoundedBox(new Vector3(0.09f, 0.32f, 0.015f), 0.006f), "Tatter", spine,
                  new Vector3(-0.18f + i * 0.09f, -0.08f, -0.15f), Vector3.one, rag, new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-12f, 12f)));

            var head = rig.Head = Prim.Group("Head", spine, new Vector3(0f, 0.72f, -0.04f));
            P(Ball, "Skull", head, new Vector3(0f, 0.08f, 0f), new Vector3(0.2f, 0.25f, 0.23f), skin);
            P(Lump(2), "Hair", head, new Vector3(0f, 0.15f, 0.03f), new Vector3(0.22f, 0.16f, 0.22f), hair);
            for (int k = -1; k <= 1; k += 2)
            {
                P(Ball, "Socket", head, new Vector3(k * 0.045f, 0.1f, -0.09f), new Vector3(0.05f, 0.04f, 0.03f), Black, null, false);
                P(Ball, "Eye", head, new Vector3(k * 0.045f, 0.1f, -0.105f), Vector3.one * 0.018f, eye, null, false);
            }
            var jaw = rig.Jaw = Prim.Group("Jaw", head, new Vector3(0f, -0.02f, -0.05f));
            P(Ball, "Jaw", jaw, new Vector3(0f, -0.03f, -0.03f), new Vector3(0.14f, 0.07f, 0.13f), skin);
            P(Ball, "Mouth", jaw, new Vector3(0f, 0.005f, -0.07f), new Vector3(0.08f, 0.03f, 0.04f), Black, null, false);

            for (int k = -1; k <= 1; k += 2)
            {
                var arm = Prim.Group(k < 0 ? "ArmL" : "ArmR", spine, new Vector3(k * 0.22f, 0.55f, 0f));
                P(Limb(0.32f, 0.05f, 0.04f), "UpperArm", arm, Vector3.zero, Vector3.one, skin);
                var fore = Prim.Group("Forearm", arm, new Vector3(0f, -0.32f, 0f));
                P(Limb(0.32f, 0.04f, 0.032f), "Forearm", fore, Vector3.zero, Vector3.one, skin);
                for (int f = -1; f <= 1; f++)
                    P(Limb(0.12f, 0.01f, 0.006f), "Finger", fore, new Vector3(f * 0.018f, -0.33f, 0f), Vector3.one, skin, new Vector3(0f, 0f, f * 10f));
                if (k < 0) { rig.ArmL = arm; rig.ForeL = fore; } else { rig.ArmR = arm; rig.ForeR = fore; }
            }
            rig.Pose(0f, 0f);
        }

        // ---------------- Crawlers: giant crab & giant spider ----------------

        public static void BuildSpider(Transform t)
        {
            var fur = Fur("SpiderFur", new Color(0.1f, 0.08f, 0.07f), 6f);
            var band = Fur("SpiderBand", new Color(0.55f, 0.25f, 0.08f), 6f);
            var eye = Glow("SpiderEye", new Color(0.9f, 0.1f, 0.1f), new Color(2.5f, 0.15f, 0.1f));
            var rig = t.gameObject.AddComponent<SpiderRig>();
            var model = rig.Model = Prim.Group("Spider", t, Vector3.zero);
            var body = rig.Body = Prim.Group("Body", model, new Vector3(0f, 0.48f, 0f));

            P(Lump(0), "Thorax", body, Vector3.zero, new Vector3(0.55f, 0.34f, 0.6f), fur);
            P(Lump(1), "Abdomen", body, new Vector3(0f, 0.1f, -0.62f), new Vector3(0.8f, 0.66f, 0.95f), fur);
            P(Ball, "Mark", body, new Vector3(0f, 0.38f, -0.6f), new Vector3(0.22f, 0.06f, 0.42f), band);
            for (int i = 0; i < 8; i++)
            {
                float x = (i % 4 - 1.5f) * 0.05f;
                float y = 0.1f + (i / 4) * 0.05f;
                P(Ball, "Eye", body, new Vector3(x, y, 0.28f), Vector3.one * (i % 3 == 0 ? 0.05f : 0.035f), eye, null, false);
            }
            for (int k = -1; k <= 1; k += 2)
                P(Cone(0.14f, 0.035f), "Fang", body, new Vector3(k * 0.06f, -0.08f, 0.3f), Vector3.one, Black, new Vector3(160f, 0f, 0f), false);

            rig.Hips = new Transform[8];
            rig.Knees = new Transform[8];
            for (int i = 0; i < 8; i++)
            {
                float side = i < 4 ? -1f : 1f;
                int idx = i % 4;
                var hip = Prim.Group("Hip" + i, body, new Vector3(side * 0.2f, -0.02f, 0.18f - idx * 0.12f));
                var knee = Prim.Group("Knee", hip, new Vector3(side * 0.55f, 0f, 0f));
                P(Limb(0.55f, 0.055f, 0.045f), "Femur", hip, Vector3.zero, Vector3.one, fur, new Vector3(0f, 0f, side * 90f));
                P(Ball, "Joint", knee, Vector3.zero, Vector3.one * 0.08f, band);
                P(Limb(0.7f, 0.045f, 0.02f), "Tibia", knee, Vector3.zero, Vector3.one, fur);
                rig.Hips[i] = hip;
                rig.Knees[i] = knee;
            }
            rig.Init();
        }

        // ---------------- Horror plants ----------------

        /// <summary>Low clump of glowing, spiky poison pods across the lane that puff toxic gas (jump over).</summary>
        public static void BuildPoisonPods(Transform t)
        {
            var stem = Mats.Lit("PodStem", Tex.Bark, new Color(0.24f, 0.27f, 0.21f), 0.3f, tiling: new Vector2(1f, 2f));
            var podMat = Glow("PoisonPod", new Color(0.42f, 0.22f, 0.5f), new Color(0.6f, 1.7f, 0.3f));
            var leaf = Mats.Lit("PodLeaf", Tex.Leaves, new Color(0.34f, 0.28f, 0.38f), 0.4f, tiling: new Vector2(2f, 2f));
            var rig = t.gameObject.AddComponent<PodRig>();
            const int n = 6;
            rig.Pods = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                float x = Mathf.Lerp(-0.95f, 0.95f, i / (n - 1f)) + Random.Range(-0.08f, 0.08f);
                float h = Random.Range(0.32f, 0.5f);
                var pod = Prim.Group("Pod" + i, t, new Vector3(x, h, Random.Range(-0.15f, 0.15f)));
                P(Limb(h, 0.045f, 0.055f), "Stem", pod, Vector3.zero, Vector3.one, stem);
                float s = Random.Range(0.32f, 0.45f);
                P(Lump(i), "Bulb", pod, new Vector3(0f, 0.05f, 0f), new Vector3(s, s * 1.15f, s), podMat);
                for (int k = 0; k < 7; k++)
                {
                    var dir = Random.onUnitSphere;
                    if (dir.y < -0.2f) dir.y = -dir.y;
                    P(Cone(0.16f, 0.025f), "Spike", pod, new Vector3(0f, 0.05f, 0f) + dir * s * 0.45f, Vector3.one, stem,
                      Quaternion.FromToRotation(Vector3.up, dir).eulerAngles, false);
                }
                rig.Pods[i] = pod;
            }
            for (int k = 0; k < 5; k++)
                P(Lump(k), "Leaves", t, new Vector3(Mathf.Lerp(-1f, 1f, k / 4f), 0.08f, Random.Range(-0.2f, 0.2f)), new Vector3(0.55f, 0.18f, 0.5f), leaf);
        }

        /// <summary>Thorny vines writhing across the lane at head height between two dead stumps (roll under).</summary>
        public static void BuildStranglerVines(Transform t)
        {
            var bark = Mats.Lit("DeadBark", Tex.Bark, new Color(0.32f, 0.31f, 0.3f), 0.15f, tiling: new Vector2(2f, 1f));
            var vineMat = Mats.Lit("StranglerVine", Tex.Bark, new Color(0.24f, 0.32f, 0.18f), 0.35f, tiling: new Vector2(1f, 3f));
            var thorn = Mats.Plain(new Color(0.6f, 0.55f, 0.45f), 0.4f);
            var rig = t.gameObject.AddComponent<VineRig>();
            for (int s = -1; s <= 1; s += 2)
            {
                P(Limb(3.4f, 0.25f, 0.17f), "Stump", t, new Vector3(s * 1.45f, 3.3f, 0.1f), Vector3.one, bark);
                P(Lump(s + 2), "Knot", t, new Vector3(s * 1.45f, 3.25f, 0.1f), new Vector3(0.55f, 0.5f, 0.55f), bark);
            }
            float[] heights = { 1.5f, 1.95f, 2.45f, 2.95f };
            rig.Heights = heights;
            rig.Vines = new Transform[heights.Length][];
            for (int v = 0; v < heights.Length; v++)
            {
                rig.Vines[v] = new Transform[VineRig.Segments];
                for (int i = 0; i < VineRig.Segments; i++)
                {
                    var seg = Prim.Group("Vine" + v + "_" + i, t, Vector3.zero);
                    P(Limb(VineRig.SegLength, 0.075f, 0.065f), "Seg", seg, Vector3.zero, Vector3.one, vineMat);
                    if (i % 2 == 1)
                        P(Cone(0.12f, 0.02f), "Thorn", seg, new Vector3(0f, -0.1f, 0.06f), Vector3.one, thorn, new Vector3(-90f + Random.Range(-30f, 30f), 0f, 0f), false);
                    rig.Vines[v][i] = seg;
                }
            }
            rig.Pose(0f, 0f);
        }

        /// <summary>A giant flytrap on a thick stalk that lunges and snaps at the runner (change lane).</summary>
        public static void BuildFlytrap(Transform t)
        {
            var stalk = Mats.Lit("FlytrapStalk", Tex.Bark, new Color(0.26f, 0.36f, 0.19f), 0.35f, tiling: new Vector2(1f, 3f));
            var lobe = Mats.Lit("FlytrapLobe", Tex.Leaves, new Color(0.5f, 0.6f, 0.32f), 0.5f, tiling: new Vector2(2f, 2f));
            var maw = Glow("FlytrapMaw", new Color(0.7f, 0.08f, 0.1f), new Color(1.9f, 0.15f, 0.2f));
            var tooth = Mats.Plain(new Color(0.75f, 0.8f, 0.55f), 0.4f);
            var baseLeaf = Mats.Cutout("FlytrapLeaf", Tex.BroadLeaf, new Color(0.6f, 0.7f, 0.5f), 0.4f);
            var rig = t.gameObject.AddComponent<FlytrapRig>();

            var leafMesh = Meshes.Leaf(1.6f, 0.9f, 0.6f, 0.12f, 8);
            for (int i = 0; i < 6; i++)
                P(leafMesh, "BaseLeaf", t, new Vector3(0f, 0.15f, 0.2f), Vector3.one, baseLeaf, new Vector3(-25f, i * 60f + Random.Range(-15f, 15f), 0f));

            rig.Stalk = new Transform[FlytrapRig.Segments];
            for (int i = 0; i < rig.Stalk.Length; i++)
            {
                var seg = Prim.Group("Stalk" + i, t, Vector3.zero);
                float k = i / (rig.Stalk.Length - 1f);
                P(Limb(FlytrapRig.SegLength, Mathf.Lerp(0.2f, 0.12f, k), Mathf.Lerp(0.19f, 0.11f, k)), "Seg", seg, Vector3.zero, Vector3.one, stalk);
                rig.Stalk[i] = seg;
            }

            // Head: two hinged lobes (mouth faces -Z, toward the runner) with teeth along the rims.
            var head = rig.Head = Prim.Group("Head", t, Vector3.zero);
            P(Ball, "Hinge", head, Vector3.zero, new Vector3(0.5f, 0.35f, 0.4f), stalk);
            for (int s = -1; s <= 1; s += 2)
            {
                var l = Prim.Group(s > 0 ? "UpperLobe" : "LowerLobe", head, Vector3.zero);
                P(Lump(s + 2), "Lobe", l, new Vector3(0f, s * 0.08f, -0.45f), new Vector3(1.15f, 0.24f, 0.95f), lobe);
                P(Ball, "Maw", l, new Vector3(0f, s * -0.02f, -0.42f), new Vector3(0.95f, 0.1f, 0.78f), maw, null, false);
                for (int k = 0; k < 11; k++)
                {
                    float a = Mathf.Lerp(-80f, 80f, k / 10f) * Mathf.Deg2Rad;
                    var rim = new Vector3(Mathf.Sin(a) * 0.55f, s * 0.06f, -0.45f - Mathf.Cos(a) * 0.45f);
                    P(Cone(0.22f, 0.025f), "Tooth", l, rim, Vector3.one, tooth, new Vector3(s > 0 ? 160f : 20f, a * Mathf.Rad2Deg, 0f), false);
                }
                if (s > 0) rig.Upper = l; else rig.Lower = l;
            }
            rig.Pose(0f, 0f, 0.4f);
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
    /// Articulated four-legged gait: every leg has a hip and a knee, plants during its stance phase and
    /// folds up as it swings forward. The spine flexes, the body rises and pitches with the stride, the head
    /// counter-rotates to stay steady, and feet kick up snow on touchdown. The owning Obstacle sets
    /// <see cref="Speed"/> (ground speed) and <see cref="Waiting"/> (crouched, about to pounce).
    /// </summary>
    public class QuadRig : MonoBehaviour
    {
        public Transform Body, Front, Rear, Head, Tail;
        public Transform[] Hips, Knees;
        public float[] LowerLength, Offsets;
        public float StrideLength = 2.5f, HipSwing = 35f, KneeBend = 60f, Bob = 0.1f, Flex = 5f, Pitch = 4f;
        public float Duty = 0.45f;   // fraction of the cycle a foot spends on the ground
        public bool Cat;
        public Color DustColor = new Color(0.95f, 0.97f, 1f, 0.6f);

        [System.NonSerialized] public float Speed;
        [System.NonSerialized] public bool Waiting;

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

    /// <summary>Swamp ghoul: sways and twitches, then lurches toward the runner with arms outstretched.</summary>
    public class GhoulRig : CreatureRig
    {
        public Transform Body, Spine, Head, Jaw, ArmL, ArmR, ForeL, ForeR;
        float lurch, jerkTimer, jerk;

        void Update()
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            time += dt;
            lurch = Mathf.MoveTowards(lurch, Gap() < 24f ? 1f : 0f, dt * 0.8f);
            if ((jerkTimer -= dt) <= 0f) { jerkTimer = Random.Range(0.4f, 1.4f); jerk = Random.Range(-1f, 1f); }
            Pose(lurch, time);
        }

        public void Pose(float lurch, float t)
        {
            Body.localPosition = new Vector3(0f, 0f, -0.45f * lurch);
            Body.localRotation = Quaternion.Euler(0f, 0f, 6f * Mathf.Sin(t * 1.1f));
            Spine.localRotation = Quaternion.Euler(-(28f - 8f * lurch) + 3f * Mathf.Sin(t * 1.7f), 0f, 4f * Mathf.Sin(t * 0.8f));
            Head.localRotation = Quaternion.Euler(20f + 6f * jerk, 12f * jerk, 18f + 10f * jerk);
            Jaw.localRotation = Quaternion.Euler(-(14f + 12f * Mathf.Abs(Mathf.Sin(t * 2.3f))), 0f, 0f);
            float twitch = 6f * Mathf.Sin(t * 9f) * Mathf.Sin(t * 1.3f);
            ArmL.localRotation = Quaternion.Euler(Mathf.Lerp(45f, 95f, lurch) + twitch, 0f, -8f);
            ArmR.localRotation = Quaternion.Euler(Mathf.Lerp(30f, 90f, lurch) - twitch, 0f, 8f);
            ForeL.localRotation = Quaternion.Euler(20f + twitch, 0f, 0f);
            ForeR.localRotation = Quaternion.Euler(30f - twitch, 0f, 0f);
        }
    }

    /// <summary>Crawling animal crossing the trail sideways while the runner approaches (komodo, spider).</summary>
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

    public class SpiderRig : CrawlerRig
    {
        public Transform Body;
        public Transform[] Hips, Knees;

        protected override void Animate(float stride, float speed)
        {
            float moving = Mathf.Clamp01(speed * 3f);
            Body.localPosition = new Vector3(0f, 0.48f + 0.02f * Mathf.Sin(stride * 6f) * moving + 0.01f * Mathf.Sin(time * 2f), 0f);
            for (int i = 0; i < Hips.Length; i++)
            {
                float side = i < 4 ? -1f : 1f;
                int idx = i % 4;
                // Alternating tetrapod gait: legs 1 & 3 on one side move with 2 & 4 on the other.
                float phase = ((idx % 2 == 0) == (side < 0)) ? 0f : Mathf.PI;
                float spread = Mathf.Lerp(35f, -45f, idx / 3f);         // front legs point forward, back legs back
                float swing = Mathf.Sin(stride * 3f + phase) * 14f * moving;
                float lift = Mathf.Max(0f, Mathf.Cos(stride * 3f + phase)) * 20f * moving;
                Hips[i].localRotation = Quaternion.Euler(0f, -side * (spread + swing), side * (35f + lift));
                Knees[i].localRotation = Quaternion.Euler(0f, 0f, -side * (20f - lift * 0.3f));
            }
        }
    }

    /// <summary>Poison pods: swell and shrink like they're breathing, puffing toxic gas (faster when you're close).</summary>
    public class PodRig : CreatureRig
    {
        public Transform[] Pods;
        float puffTimer;

        void Update()
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            time += dt;
            for (int i = 0; i < Pods.Length; i++)
            {
                float p = 1f + 0.09f * Mathf.Sin(time * 2.5f + i * 1.3f);
                Pods[i].localScale = new Vector3(p, p, p);
            }
            float gap = Gap();
            if ((puffTimer -= dt) <= 0f)
            {
                bool near = gap < 25f;
                puffTimer = near ? Random.Range(0.3f, 0.6f) : Random.Range(1.2f, 2.4f);
                var pod = Pods[Random.Range(0, Pods.Length)];
                Fx.Gas(pod.position + Vector3.up * 0.3f, near ? 4 : 2);
            }
        }
    }

    /// <summary>Strangler vines: writhe across the lane, reaching toward the runner as they approach.</summary>
    public class VineRig : CreatureRig
    {
        public const int Segments = 12;
        public const float SegLength = 0.26f;
        public float[] Heights;
        public Transform[][] Vines;
        float alert;
        readonly Vector3[] pts = new Vector3[Segments + 1];

        void Update()
        {
            if (!Active) return;
            time += Time.deltaTime;
            alert = Mathf.MoveTowards(alert, Gap() < 20f ? 1f : 0f, Time.deltaTime * 1.5f);
            Pose(alert, time);
        }

        public void Pose(float alert, float t)
        {
            float amp = Mathf.Lerp(0.07f, 0.17f, alert);
            for (int v = 0; v < Vines.Length; v++)
            {
                for (int k = 0; k <= Segments; k++)
                {
                    float u = k / (float)Segments;
                    float sag = Mathf.Sin(Mathf.PI * u);
                    float y = Heights[v] - 0.1f * sag + amp * Mathf.Sin(t * 2.6f + u * 6f + v * 1.7f) * sag;
                    float z = 0.1f + amp * 1.4f * Mathf.Sin(t * 1.9f + u * 5f + v * 2.3f) * sag - alert * 0.3f * sag;
                    pts[k] = new Vector3(Mathf.Lerp(-1.4f, 1.4f, u), Mathf.Max(1.3f, y), z); // never dips into the roll gap
                }
                for (int i = 0; i < Segments; i++)
                {
                    var d = pts[i + 1] - pts[i];
                    var seg = Vines[v][i];
                    seg.localPosition = pts[i];
                    seg.localRotation = Quaternion.FromToRotation(Vector3.down, d.normalized);
                    seg.localScale = new Vector3(1f, d.magnitude / SegLength * 1.15f, 1f);
                }
            }
        }
    }

    /// <summary>Giant flytrap: sways with its jaws ajar, then lunges at the runner snapping hungrily.</summary>
    public class FlytrapRig : CreatureRig
    {
        public const int Segments = 7;
        public const float SegLength = 0.42f;
        public Transform[] Stalk;
        public Transform Head, Upper, Lower;
        float alert;
        readonly Vector3[] pts = new Vector3[Segments + 1];

        void Update()
        {
            if (!Active) return;
            time += Time.deltaTime;
            alert = Mathf.MoveTowards(alert, Gap() < 18f ? 1f : 0f, Time.deltaTime * 2f);
            float idleOpen = 0.35f + 0.15f * Mathf.Sin(time * 0.8f);
            float snapping = Mathf.Pow(Mathf.Abs(Mathf.Sin(time * 3.2f)), 0.4f) * 0.95f;
            Pose(Mathf.SmoothStep(0f, 1f, alert), time, Mathf.Lerp(idleOpen, snapping, alert));
        }

        public void Pose(float alert, float t, float open)
        {
            var idleHead = new Vector3(0.18f * Mathf.Sin(t * 0.7f), 2.35f + 0.1f * Mathf.Sin(t * 1.1f), 0.05f);
            var lungeHead = new Vector3(0f, 1.8f + 0.12f * Mathf.Sin(t * 3f), -0.9f);
            var headPos = Vector3.Lerp(idleHead, lungeHead, alert);
            var root = new Vector3(0f, 0f, 0.2f);
            var ctrl = new Vector3(0f, 1.4f, 0.5f);
            for (int i = 0; i <= Segments; i++)
            {
                float u = i / (float)Segments;
                pts[i] = (1 - u) * (1 - u) * root + 2 * (1 - u) * u * ctrl + u * u * headPos;
            }
            for (int i = 0; i < Segments; i++)
            {
                var d = pts[i + 1] - pts[i];
                Stalk[i].localPosition = pts[i];
                Stalk[i].localRotation = Quaternion.FromToRotation(Vector3.down, d.normalized);
                Stalk[i].localScale = new Vector3(1f, d.magnitude / SegLength * 1.1f, 1f);
            }
            Head.localPosition = headPos;
            Head.localRotation = Quaternion.Euler(Mathf.Lerp(5f, -15f, alert) + 6f * Mathf.Sin(t * 1.3f), 14f * Mathf.Sin(t * 0.6f) * (1f - alert), 0f);
            float ang = Mathf.Lerp(8f, 55f, open);
            Upper.localRotation = Quaternion.Euler(ang, 0f, 0f);
            Lower.localRotation = Quaternion.Euler(-ang * 0.6f, 0f, 0f);
        }
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
}
