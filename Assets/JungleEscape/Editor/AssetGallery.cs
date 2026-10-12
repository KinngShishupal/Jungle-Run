using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JungleEscape.EditorTools
{
    /// <summary>
    /// Menu: Jungle Escape > Render Asset Gallery.
    /// Builds every procedural asset (plants, rocks, obstacles, pickups, characters, textures)
    /// in an isolated preview scene and saves contact sheets to _GraphicsPreview/Gallery/.
    /// Nothing is added to your scene.
    /// </summary>
    public static class AssetGallery
    {
        const int Tile = 360;
        static PreviewRenderUtility pru;
        static string outDir;

        [MenuItem("Jungle Escape/Render Asset Gallery")]
        public static void Render()
        {
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../_GraphicsPreview/Gallery"));
            Directory.CreateDirectory(outDir);
            pru = new PreviewRenderUtility();
            try
            {
                Setup();
                var view = new Vector3(0.9f, -0.45f, 1.4f);   // front three-quarter, looking slightly down

                EditorUtility.DisplayProgressBar("Asset Gallery", "Plants & trees", 0.1f);
                Sheet("1_trees_palms", new List<Texture2D>
                {
                    ModelShot(Flora.Tree(0), view), ModelShot(Flora.Tree(1), view), ModelShot(Flora.Tree(2), view), ModelShot(Flora.Tree(3), view),
                    ModelShot(Flora.Palm(0), view), ModelShot(Flora.Palm(1), view), ModelShot(Flora.WallTree(0), view), ModelShot(Flora.WallTree(1), view)
                }, 4);
                Sheet("2_undergrowth_rocks", new List<Texture2D>
                {
                    ModelShot(Flora.Fern(0), view), ModelShot(Flora.Fern(1), view), ModelShot(Flora.BroadPlant(0), view), ModelShot(Flora.BroadPlant(1), view),
                    ModelShot(Flora.Bush(0), view), ModelShot(Flora.Tuft(0), view), ModelShot(Flora.Rock(0), view), ModelShot(Flora.Rock(1), view)
                }, 4);

                Sheet("2b_desert_plants", new List<Texture2D>
                {
                    ModelShot(Flora.Saguaro(0), view), ModelShot(Flora.Saguaro(1), view), ModelShot(Flora.BarrelCactus(0), view), ModelShot(Flora.PricklyPear(0), view),
                    ModelShot(Flora.Agave(0), view), ModelShot(Flora.DesertShrub(0), view), ModelShot(Flora.DeadTree(0), view), ModelShot(Flora.Hoodoo(0), view),
                    ModelShot(Flora.SandstoneRock(0), view), ModelShot(Flora.Skull(0), view), ModelShot(Flora.Tumbleweed(0), view), ModelShot(Flora.Dune(0), view)
                }, 4);

                EditorUtility.DisplayProgressBar("Asset Gallery", "Obstacles", 0.35f);
                var obView = new Vector3(0.8f, -0.4f, 1.5f);
                Sheet("3_obstacles", new List<Texture2D>
                {
                    Shot(r => Obstacle.Create(ObstacleType.Log, 0, 0f, r), obView),
                    Shot(r => Obstacle.Create(ObstacleType.Branch, 0, 0f, r), obView),
                    Shot(r => Obstacle.Create(ObstacleType.Boulder, 0, 0f, r), obView),
                    Shot(r => Obstacle.Create(ObstacleType.Boar, 0, 0f, r), new Vector3(-0.9f, -0.4f, 1.3f)),
                    Shot(r => Obstacle.Create(ObstacleType.Snake, 0, 0f, r), new Vector3(0.6f, -0.7f, 1.2f)),
                    Shot(r => Obstacle.Create(ObstacleType.Tiger, 0, 0f, r), new Vector3(-0.9f, -0.35f, 1.3f)),
                    Shot(r => Obstacle.Create(ObstacleType.Bear, 0, 0f, r), new Vector3(0.4f, -0.2f, 1.5f)),
                    Shot(r => { Animals.SpawnCrocodile(r, 0f, 0f, 0f, 90f); return r; }, new Vector3(0.3f, -0.6f, 1.2f))
                }, 4);

                // World-specific creatures: Ice Age, Water World, Desert
                EditorUtility.DisplayProgressBar("Asset Gallery", "World creatures", 0.42f);
                var cv = new Vector3(0.7f, -0.35f, 1.35f);
                Sheet("3b_world_creatures", new List<Texture2D>
                {
                    Shot(r => Obstacle.Create(ObstacleType.Bear, 0, 0f, r, BiomeType.IceAge), cv),         // mammoth
                    Shot(r => Obstacle.Create(ObstacleType.Boar, 0, 0f, r, BiomeType.IceAge), cv),         // woolly rhino
                    Shot(r => Obstacle.Create(ObstacleType.Tiger, 0, 0f, r, BiomeType.IceAge), cv),        // sabre-tooth
                    Shot(r => Obstacle.Create(ObstacleType.Snake, 0, 0f, r, BiomeType.WaterWorld), cv),    // crab
                    Shot(r => Obstacle.Create(ObstacleType.Boar, 0, 0f, r, BiomeType.WaterWorld), cv),     // crocodile
                    Shot(r => Obstacle.Create(ObstacleType.Tiger, 0, 0f, r, BiomeType.WaterWorld), cv),    // hippo
                    Shot(r => Obstacle.Create(ObstacleType.Bear, 0, 0f, r, BiomeType.WaterWorld), cv),     // octopus
                    Shot(r => { var g = new GameObject("Scorpion").transform; g.SetParent(r, false); Creatures.BuildScorpion(g); return g; }, cv),
                    Shot(r => Obstacle.Create(ObstacleType.Boar, 0, 0f, r, BiomeType.Desert), cv),   // ostrich
                    Shot(r => Obstacle.Create(ObstacleType.Tiger, 0, 0f, r, BiomeType.Desert), cv),  // lion
                    Shot(r => Obstacle.Create(ObstacleType.Bear, 0, 0f, r, BiomeType.Desert), cv)    // camel
                }, 5);
                EditorUtility.DisplayProgressBar("Asset Gallery", "Pickups", 0.5f);
                var pkView = new Vector3(0.4f, -0.3f, 1.5f);
                Sheet("4_pickups", new List<Texture2D>
                {
                    Shot(r => Pickup.Create(PickupType.Banana, Vector3.zero, r), pkView),
                    Shot(r => Pickup.Create(PickupType.Magnet, Vector3.zero, r), pkView),
                    Shot(r => Pickup.Create(PickupType.Glider, Vector3.zero, r), pkView),
                    Shot(r => Pickup.Create(PickupType.Shield, Vector3.zero, r), pkView),
                    Shot(r => Pickup.Create(PickupType.Bonanza, Vector3.zero, r), pkView)
                }, 5);

                EditorUtility.DisplayProgressBar("Asset Gallery", "Characters", 0.65f);
                Sheet("5_characters", new List<Texture2D>
                {
                    RigShot(ExplorerRig.Idle(0f), 0f, new Vector3(-0.35f, -0.15f, -1f)),   // player front
                    RigShot(ExplorerRig.Idle(0f), 0f, new Vector3(0.3f, -0.2f, 1f)),       // player back (game view)
                    RigShot(ExplorerRig.Idle(0f), 0f, new Vector3(-1f, -0.1f, 0f)),        // player side
                    Shot(r => { var m = new GameObject("Monkeys"); m.transform.SetParent(r, false); m.AddComponent<MonkeyChaser>().Init(r); return m; },
                         new Vector3(0.5f, -0.35f, -1.4f))
                }, 4);

                EditorUtility.DisplayProgressBar("Asset Gallery", "Player poses", 0.75f);
                var side = new Vector3(-1f, -0.12f, 0.05f);
                var run = new List<Texture2D>();
                for (int i = 0; i < 8; i++) run.Add(RigShot(ExplorerRig.Run(i / 8f * Mathf.PI * 2f, 0.5f), 0f, side));
                Sheet("6_player_run_cycle", run, 4);
                var t = ExplorerRig.Tuck();
                t.BodyY = -0.42f;
                Sheet("7_player_moves", new List<Texture2D>
                {
                    RigShot(ExplorerRig.Air(6f), 0f, side), RigShot(ExplorerRig.Air(-6f), 0f, side),
                    RigShot(t, 0f, side), RigShot(t, 140f, side),
                    RigShot(ExplorerRig.Glide(0f, 0f, 0f, 0f), 0f, side), RigShot(ExplorerRig.Knocked(), 0f, side, -86f)
                }, 3);

                EditorUtility.DisplayProgressBar("Asset Gallery", "Textures", 0.9f);
                Sheet("8_textures", new List<Texture2D>
                {
                    TexShot(Tex.Trail.Albedo), TexShot(Tex.Ground.Albedo), TexShot(Tex.Bark.Albedo), TexShot(Tex.PalmBark.Albedo),
                    TexShot(Tex.Rock.Albedo), TexShot(Tex.Leaves.Albedo), TexShot(Tex.Frond.Albedo), TexShot(Tex.BroadLeaf.Albedo),
                    TexShot(Tex.Grass.Albedo), TexShot(Tex.Fur.Albedo), TexShot(Tex.WoodRings.Albedo), TexShot(Tex.Coconut.Albedo)
                }, 4);

                Debug.Log("[JungleEscape] Asset gallery saved to " + outDir);
                EditorUtility.RevealInFinder(outDir);
            }
            catch (Exception e)
            {
                Debug.LogError("[JungleEscape] Asset gallery failed: " + e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                pru.Cleanup();
                pru = null;
            }
        }

        // ---------------- Rendering ----------------

        static void Setup()
        {
            var cam = pru.camera;
            cam.fieldOfView = 26f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.68f, 0.58f);
            cam.allowHDR = false;
            pru.lights[0].intensity = 1.25f;
            pru.lights[0].color = Atmosphere.SunColor;
            pru.lights[0].transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            pru.lights[1].intensity = 0.45f;
            pru.lights[1].color = new Color(0.7f, 0.8f, 0.85f);
            pru.lights[1].transform.rotation = Quaternion.Euler(20f, 150f, 0f);
            pru.ambientColor = new Color(0.42f, 0.46f, 0.42f);
        }

        static Texture2D ModelShot(Model m, Vector3 view) =>
            Shot(r => m.Spawn("Model", r, Vector3.zero, Quaternion.identity, Vector3.one).gameObject, view);

        static Texture2D Shot(Func<Transform, object> build, Vector3 view, Action<GameObject> after = null)
        {
            var root = new GameObject("GalleryRoot");
            pru.AddSingleGO(root);
            build(root.transform);
            after?.Invoke(root);
            var tex = Capture(root, view);
            UnityEngine.Object.DestroyImmediate(root);
            return tex;
        }

        static Texture2D RigShot(ExplorerRig.Pose pose, float rollAngle, Vector3 view, float fallAngle = 0f)
        {
            return Shot(r =>
            {
                var rig = new ExplorerRig();
                rig.Build(r);
                rig.Snap(pose);
                rig.Apply(pose, 0f, rollAngle, fallAngle);
                rig.ShieldFx.SetActive(false);
                rig.GliderFx.SetActive(fallAngle == 0f && pose.ShoulderL.x < -150f);
                rig.MagnetFx.gameObject.SetActive(false);
                rig.BonanzaFx.SetActive(false);
                return rig;
            }, view);
        }

        static Texture2D Capture(GameObject root, Vector3 view)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var b = new Bounds(root.transform.position + Vector3.up, Vector3.zero);
            bool first = true;
            foreach (var r in renderers)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r.name == "ContactShadow" || r.name == "Halo") continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }

            var cam = pru.camera;
            float radius = Mathf.Max(0.3f, b.extents.magnitude);
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
            var dir = view.normalized;
            cam.transform.position = b.center - dir * dist;
            cam.transform.rotation = Quaternion.LookRotation(dir);
            cam.nearClipPlane = dist * 0.05f;
            cam.farClipPlane = dist * 4f;

            pru.BeginStaticPreview(new Rect(0, 0, Tile, Tile));
            pru.Render(true, false);
            var src = pru.EndStaticPreview();
            var tex = Readable(src, Tile, Tile);
            return tex;
        }

        static Texture2D TexShot(Texture tex) => Readable(tex, Tile, Tile);

        static Texture2D Readable(Texture src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(w, h, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        static void Sheet(string name, List<Texture2D> tiles, int cols)
        {
            int rows = (tiles.Count + cols - 1) / cols;
            const int gap = 6;
            var sheet = new Texture2D(cols * (Tile + gap) + gap, rows * (Tile + gap) + gap, TextureFormat.RGB24, false);
            var fill = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(24, 28, 24, 255);
            sheet.SetPixels32(fill);
            for (int i = 0; i < tiles.Count; i++)
            {
                int cx = i % cols, cy = rows - 1 - i / cols;
                sheet.SetPixels32(gap + cx * (Tile + gap), gap + cy * (Tile + gap), Tile, Tile, tiles[i].GetPixels32());
                UnityEngine.Object.DestroyImmediate(tiles[i]);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }
}
