using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// Builds the endless, winding jungle trail in 50 m chunks ahead of the player and removes
    /// chunks behind. Chunks are authored in straight "track space" and bent onto
    /// <see cref="TrackPath"/>. Each chunk gets rows of obstacles, banana trails, the occasional
    /// power-up, and sometimes a landmark: a river crossing, a cave or a rocky pass.
    /// </summary>
    public class TrackGenerator : MonoBehaviour
    {
        const float ChunkLength = 50f;
        const int ChunksAhead = 3;
        const float SafeStart = 45f;       // obstacle-free run-up at the start
        const float PowerUpChance = 0.07f; // per row

        // River crossing span inside a chunk (local metres) and cave span.
        const float RiverA = 17.5f, RiverB = 30f;
        const float CaveA = 6f, CaveB = 44f;
        const float WaterLevel = -1.35f;

        // Lane contents for a row
        const int Empty = -1;

        enum Feature { None, River, Cave, RockyPass }

        struct Span { public float S0, S1; }

        static readonly List<Span> rivers = new List<Span>();

        readonly List<GameObject> chunks = new List<GameObject>();
        readonly List<float> chunkStarts = new List<float>();
        readonly int[] lastRow = { Empty, Empty, Empty };
        PlayerController player;
        float spawnZ;
        float nextRowZ;
        int chunkIndex;
        Feature lastFeature;
        float birdTimer = 6f;
        float riverLocalA = -100f, riverLocalB = -100f; // river span of the chunk being built

        Material shaftMat;
        Mesh shaftMesh, plankMesh, beamMesh, postMesh, stiltMesh, coneMesh, crystalMesh, torchMesh, flameMesh;
        readonly Model scratch = new Model();

        public void Init(PlayerController target)
        {
            player = target;
            shaftMat = new Material(Mats.ParticleAdd(Tex.Shaft)) { name = "SunShaft" };
            shaftMat.SetColor("_BaseColor", new Color(1f, 0.86f, 0.62f, 0.09f));
            shaftMesh = Meshes.Billboard(1f, 1f);
            plankMesh = Meshes.RoundedBox(new Vector3(8.6f, 0.09f, 0.3f), 0.02f);
            beamMesh = Meshes.RoundedBox(new Vector3(0.25f, 0.3f, 1f), 0.04f);
            postMesh = Meshes.Limb(1.35f, 0.09f, 0.08f, 8, 4);
            stiltMesh = Meshes.Limb(2.8f, 0.11f, 0.1f, 8, 4);
            coneMesh = Meshes.Lathe(new[] { new Vector2(0.001f, -1f), new Vector2(0.08f, -0.7f), new Vector2(0.2f, -0.25f), new Vector2(0.3f, 0f) }, 10, 1f, "Stalactite");
            crystalMesh = Meshes.Lathe(new[] { new Vector2(0.001f, 0f), new Vector2(0.09f, 0.02f), new Vector2(0.09f, 0.7f), new Vector2(0.001f, 0.9f) }, 6, 1f, "Crystal");
            torchMesh = Meshes.Limb(0.55f, 0.04f, 0.03f, 6, 3);
            flameMesh = Meshes.Ball(8, 10);
        }

        public void ResetTrack()
        {
            foreach (var c in chunks) if (c != null) Destroy(c);
            chunks.Clear();
            chunkStarts.Clear();
            rivers.Clear();
            TrackPath.Reset(Random.Range(1, int.MaxValue));
            spawnZ = -20f;
            nextRowZ = SafeStart;
            chunkIndex = 0;
            lastFeature = Feature.None;
            for (int i = 0; i < 3; i++) lastRow[i] = Empty;
            while (spawnZ < ChunkLength * ChunksAhead) SpawnChunk();
        }

        void Update()
        {
            if (player == null) return;
            float ps = player.TrackS;
            while (spawnZ < ps + ChunkLength * ChunksAhead) SpawnChunk();
            // Generous margin: banana trails can extend a little past their chunk's end.
            while (chunks.Count > 0 && chunkStarts[0] + ChunkLength < ps - 35f)
            {
                Destroy(chunks[0]);
                chunks.RemoveAt(0);
                chunkStarts.RemoveAt(0);
            }
            while (rivers.Count > 0 && rivers[0].S1 < ps - 120f) rivers.RemoveAt(0);

            // Flowing water
            float t = Time.time;
            Mats.Water.SetTextureOffset("_BaseMap", new Vector2(t * 0.035f, t * 0.01f));

            // Now and then a flock of parrots crosses the sky ahead.
            if (GameManager.I != null && GameManager.I.State == GameState.Playing && (birdTimer -= Time.deltaTime) <= 0f)
            {
                birdTimer = Random.Range(8f, 16f);
                float side = Random.value < 0.5f ? -1f : 1f;
                Animals.SpawnFlock(TrackPath.Point(ps + Random.Range(45f, 70f), side * 45f, Random.Range(13f, 18f)),
                                   TrackPath.Point(ps + Random.Range(70f, 100f), -side * 45f, Random.Range(14f, 20f)),
                                   Random.Range(8f, 11f));
            }
        }

        /// <summary>Removes obstacles just ahead of the player (used on revive).</summary>
        public void ClearAhead(float fromS, float length)
        {
            foreach (var c in chunks)
                foreach (var ob in c.GetComponentsInChildren<Obstacle>())
                    if (ob.S > fromS - 3f && ob.S < fromS + length) ob.Smash();
        }

        // ---------------- Generation ----------------

        Feature PickFeature()
        {
            if (chunkIndex < 2) return Feature.None;
            float r = Random.value;
            var f = r < 0.16f ? Feature.River : r < 0.30f ? Feature.Cave : r < 0.46f ? Feature.RockyPass : Feature.None;
            return f == lastFeature ? Feature.None : f;
        }

        void SpawnChunk()
        {
            float s0 = spawnZ;
            var feature = PickFeature();
            if (feature == Feature.River) rivers.Add(new Span { S0 = s0 + RiverA, S1 = s0 + RiverB });

            var chunk = new GameObject("Chunk " + feature);
            chunk.transform.SetParent(transform, false);
            BuildScenery(chunk.transform, s0, feature);

            float end = s0 + ChunkLength;
            while (nextRowZ < end)
            {
                if (!OnBridge(nextRowZ)) BuildRow(chunk.transform, nextRowZ);
                nextRowZ += RowGap();
            }

            chunks.Add(chunk);
            chunkStarts.Add(s0);
            spawnZ = end;
            chunkIndex++;
            lastFeature = feature;
        }

        static bool OnBridge(float s)
        {
            foreach (var r in rivers) if (s > r.S0 - 4f && s < r.S1 + 4f) return true;
            return false;
        }

        float Difficulty => GameManager.I != null ? GameManager.I.Difficulty : 0f;

        float RowGap() => Mathf.Lerp(22f, 14f, Difficulty) + Random.Range(-1f, 3f);

        static bool Jumpable(int t) => t == (int)ObstacleType.Log || t == (int)ObstacleType.Snake;
        static bool Passable(int t) => t == Empty || Jumpable(t) || t == (int)ObstacleType.Branch;
        static bool Charger(int t) => t == (int)ObstacleType.Boar || t == (int)ObstacleType.Tiger;

        void BuildRow(Transform parent, float z)
        {
            float d = Difficulty;
            int[] row = { Empty, Empty, Empty };

            // How many lanes get an obstacle
            int blocked = Random.value < Mathf.Lerp(0.25f, 0.6f, d) ? 2 : 1;
            if (Random.value < 0.12f * d) blocked = 3;

            int[] order = Shuffle(new[] { 0, 1, 2 });
            for (int i = 0; i < blocked; i++) row[order[i]] = (int)RandomType(d);

            for (int i = 0; i < 3; i++)
            {
                // A charging animal would run through whatever was in its lane in the previous row.
                if (Charger(row[i]) && lastRow[i] != Empty) row[i] = (int)ObstacleType.Log;
            }

            // Always leave at least one lane you can get through without changing lane
            // (empty, log/snake = jump, branch = roll).
            bool passable = false;
            for (int i = 0; i < 3; i++) if (Passable(row[i])) passable = true;
            if (!passable) row[order[0]] = Random.value < 0.5f ? Empty : (int)ObstacleType.Log;

            for (int i = 0; i < 3; i++)
                if (row[i] != Empty) Obstacle.Create((ObstacleType)row[i], i - 1, z, parent);

            PlaceRewards(parent, row, z);
            System.Array.Copy(row, lastRow, 3);
        }

        void PlaceRewards(Transform parent, int[] row, float z)
        {
            var empties = new List<int>();
            var jumps = new List<int>();
            var branches = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                if (row[i] == Empty) empties.Add(i);
                else if (Jumpable(row[i])) jumps.Add(i);
                else if (row[i] == (int)ObstacleType.Branch) branches.Add(i);
            }

            // Occasional power-up in a free lane
            if (empties.Count > 0 && Random.value < PowerUpChance)
            {
                int lane = empties[Random.Range(0, empties.Count)] - 1;
                var type = (PickupType)Random.Range(1, 5);
                Pickup.Create(type, new Vector3(lane * PlayerController.LaneWidth, 1.1f, z), parent);
                return;
            }

            float r = Random.value;
            if (jumps.Count > 0 && r < 0.3f)
            {
                // Arc of bananas over a log or snake: rewards the jump
                int lane = jumps[Random.Range(0, jumps.Count)] - 1;
                for (int k = -3; k <= 3; k++)
                {
                    float t = k / 3.5f;
                    float h = 0.9f + 1.4f * (1f - t * t);
                    Pickup.Create(PickupType.Banana, new Vector3(lane * PlayerController.LaneWidth, h, z + k * 1.4f), parent);
                }
            }
            else if (branches.Count > 0 && r < 0.5f)
            {
                // Low line under a branch: rewards the roll
                int lane = branches[Random.Range(0, branches.Count)] - 1;
                for (int k = -2; k <= 2; k++)
                    Pickup.Create(PickupType.Banana, new Vector3(lane * PlayerController.LaneWidth, 0.45f, z + k * 1.5f), parent);
            }
            else if (empties.Count > 0 && r < 0.9f)
            {
                // Straight trail through a free lane
                int lane = empties[Random.Range(0, empties.Count)] - 1;
                for (int k = -3; k <= 3; k++)
                    Pickup.Create(PickupType.Banana, new Vector3(lane * PlayerController.LaneWidth, 0.9f, z + k * 1.8f), parent);
            }
        }

        static ObstacleType RandomType(float d)
        {
            // Animals join in as the run gets faster.
            float[] w =
            {
                26f,                       // Log
                22f,                       // Branch
                24f,                       // Boulder
                d > 0.2f ? 10f : 0f,       // Boar
                d > 0.05f ? 12f : 0f,      // Snake
                d > 0.4f ? 8f : 0f,        // Tiger
                d > 0.15f ? 9f : 0f        // Bear
            };
            ObstacleType[] types =
            {
                ObstacleType.Log, ObstacleType.Branch, ObstacleType.Boulder, ObstacleType.Boar,
                ObstacleType.Snake, ObstacleType.Tiger, ObstacleType.Bear
            };
            float total = 0f;
            foreach (var x in w) total += x;
            float r = Random.value * total;
            for (int i = 0; i < w.Length; i++)
            {
                if ((r -= w[i]) < 0f) return types[i];
            }
            return ObstacleType.Log;
        }

        static int[] Shuffle(int[] a)
        {
            for (int i = a.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
            return a;
        }

        // ---------------- Scenery ----------------

        const float ZTile = 300f; // texture coordinates wrap every 300 m to keep UV precision

        /// <summary>
        /// Height of the forest floor at a track-space point (0 at the trail edge, rising into
        /// banks, dipping into river channels).
        /// </summary>
        public static float GroundHeight(float x, float s)
        {
            float ax = Mathf.Abs(x);
            float bank = Tex.SS(5f, 14f, ax) * 1.0f + Tex.SS(14f, 42f, ax) * 4f;
            float n = (Noise.World(x, s, 0.12f, 3, 77) - 0.5f) * 1.8f * Tex.SS(5.5f, 10f, ax);
            float h = bank + n;
            foreach (var r in rivers)
            {
                float half = (r.S1 - r.S0) * 0.5f;
                float d = Mathf.Abs(s - (r.S0 + r.S1) * 0.5f);
                h = Mathf.Lerp(h, -2.6f, Tex.SS(half + 3f, half - 0.5f, d));
            }
            return h;
        }

        bool Blocked(float localZ) => localZ > riverLocalA - 3f && localZ < riverLocalB + 3f;

        void BuildScenery(Transform c, float s0, Feature feature)
        {
            var owned = c.gameObject.AddComponent<OwnedMeshes>();
            bool river = feature == Feature.River;
            riverLocalA = river ? RiverA : -100f;
            riverLocalB = river ? RiverB : -100f;

            var trail = TrailMesh(s0, river);
            TrackPath.Deform(trail, s0);
            owned.Meshes.Add(trail);
            Prim.Shape(trail, "Trail", c, Vector3.zero, Vector3.one, Mats.Trail, null, false);
            for (int s = -1; s <= 1; s += 2)
            {
                var ground = GroundMesh(s0, s);
                TrackPath.Deform(ground, s0);
                owned.Meshes.Add(ground);
                Prim.Shape(ground, "Ground", c, Vector3.zero, Vector3.one, Mats.Ground, null, false);
            }

            var b = new ChunkBuilder();
            for (int s = -1; s <= 1; s += 2)
            {
                switch (feature)
                {
                    case Feature.Cave:
                        JungleWall(b, s, s0);
                        break;
                    case Feature.RockyPass:
                        EdgePlants(b, s, s0);
                        RockyWall(b, s, s0);
                        JungleWall(b, s, s0);
                        break;
                    default:
                        EdgePlants(b, s, s0);
                        MidForest(b, s, s0);
                        JungleWall(b, s, s0);
                        break;
                }
            }

            if (river) BuildRiver(b, c, s0, owned);
            if (feature == Feature.Cave) BuildCave(b, c, s0, owned);
            else
            {
                if (Random.value < 0.55f) Liana(b, Random.Range(5f, 45f));
                int shafts = Random.Range(1, 4);
                for (int i = 0; i < shafts; i++) SunShaft(b, Random.Range(0f, ChunkLength));
            }
            b.Build(c, s0);
        }

        static Vector3 OnGround(float x, float localZ, float s0, float sink = 0.05f) =>
            new Vector3(x, GroundHeight(x, s0 + localZ) - sink, localZ);

        /// <summary>Ferns, grass, broad-leaf plants and stones lining the trail.</summary>
        void EdgePlants(ChunkBuilder b, int s, float s0)
        {
            for (float z = Random.Range(0f, 1.2f); z < ChunkLength; z += Random.Range(0.8f, 1.6f))
            {
                if (Blocked(z)) continue;
                float r = Random.value;
                int v = Random.Range(0, 4);
                float yaw = Random.Range(0f, 360f);
                if (r < 0.40f)
                    b.Add(Flora.Tuft(v), OnGround(s * Random.Range(4.5f, 5.6f), z, s0, 0.02f), yaw, Random.Range(0.8f, 1.35f));
                else if (r < 0.68f)
                    b.Add(Flora.Fern(v), OnGround(s * Random.Range(5.3f, 6.8f), z, s0), yaw, Random.Range(0.8f, 1.25f));
                else if (r < 0.88f)
                    b.Add(Flora.BroadPlant(v), OnGround(s * Random.Range(5.6f, 7.4f), z, s0), yaw, Random.Range(0.9f, 1.5f));
                else if (r < 0.95f)
                    b.Add(Flora.Rock(v), OnGround(s * Random.Range(5.2f, 6.6f), z, s0, 0.12f), yaw, Random.Range(0.35f, 0.8f));
                else
                    b.Add(Flora.Bush(v), OnGround(s * Random.Range(6.2f, 7.8f), z, s0), yaw, Random.Range(0.9f, 1.3f));
            }
        }

        /// <summary>Big rainforest trees and leaning palms with undergrowth around their roots.</summary>
        void MidForest(ChunkBuilder b, int s, float s0)
        {
            for (float z = Random.Range(0f, 4f); z < ChunkLength; z += Random.Range(6f, 9.5f))
            {
                if (Blocked(z)) continue;
                float x = s * Random.Range(7.8f, 12f);
                int v = Random.Range(0, 4);
                if (Random.value < 0.32f)
                {
                    // Palms lean in over the trail (the model leans toward its local +X).
                    float yaw = (s < 0 ? 0f : 180f) + Random.Range(-35f, 35f);
                    b.Add(Flora.Palm(v), OnGround(x, z, s0, 0.1f), yaw, Random.Range(0.9f, 1.15f));
                }
                else
                {
                    b.Add(Flora.Tree(v), OnGround(x, z, s0, 0.15f), Random.Range(0f, 360f), Random.Range(0.85f, 1.2f));
                }

                // Undergrowth around the base
                for (int k = 0; k < 2; k++)
                {
                    float ux = x + Random.Range(-1.8f, 1.8f);
                    float uz = Mathf.Clamp(z + Random.Range(-2f, 2f), 0f, ChunkLength);
                    if (Mathf.Abs(ux) < 6f || Blocked(uz)) continue;
                    var plant = Random.value < 0.5f ? Flora.Fern(Random.Range(0, 4)) : Flora.Bush(Random.Range(0, 4));
                    b.Add(plant, OnGround(ux, uz, s0), Random.Range(0f, 360f), Random.Range(0.9f, 1.4f));
                }
            }

            if (Random.value < 0.6f)
            {
                float z = Random.Range(3f, ChunkLength - 3f);
                if (!Blocked(z))
                    b.Add(Flora.Rock(Random.Range(0, 4)), OnGround(s * Random.Range(7f, 11f), z, s0, 0.3f), Random.Range(0f, 360f), Random.Range(1.4f, 2.6f));
            }
        }

        /// <summary>Rocky pass: huge boulders crowd the trail and cliffs tower behind them.</summary>
        void RockyWall(ChunkBuilder b, int s, float s0)
        {
            for (float z = Random.Range(0f, 3f); z < ChunkLength; z += Random.Range(4.5f, 8f))
            {
                float scale = Random.Range(3f, 6.5f);
                b.Add(Flora.Rock(Random.Range(0, 4)), OnGround(s * Random.Range(7.5f, 10.5f), z, s0, scale * 0.25f),
                      Random.Range(0f, 360f), scale);
                if (Random.value < 0.6f)
                    b.Add(Flora.Fern(Random.Range(0, 4)), OnGround(s * Random.Range(6.2f, 7f), z + Random.Range(-1.5f, 1.5f), s0),
                          Random.Range(0f, 360f), Random.Range(0.8f, 1.2f));
            }
            for (float z = Random.Range(0f, 6f); z < ChunkLength; z += Random.Range(9f, 15f))
            {
                var pos = OnGround(s * Random.Range(13f, 19f), z, s0, 1.5f);
                var size = new Vector3(Random.Range(5f, 8f), Random.Range(13f, 20f), Random.Range(5f, 8f));
                b.Add(Flora.Rock(Random.Range(0, 4)), Matrix4x4.TRS(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), size));
            }
        }

        /// <summary>Dense, darker trees further out that close the jungle in on both sides.</summary>
        void JungleWall(ChunkBuilder b, int s, float s0)
        {
            for (float z = Random.Range(0f, 3f); z < ChunkLength; z += Random.Range(4.5f, 7f))
                if (!Blocked(z))
                    b.Add(Flora.WallTree(Random.Range(0, 4)), OnGround(s * Random.Range(14f, 19f), z, s0, 0.2f),
                          Random.Range(0f, 360f), Random.Range(0.9f, 1.2f));
            for (float z = Random.Range(0f, 4f); z < ChunkLength; z += Random.Range(6f, 9f))
                if (!Blocked(z))
                    b.Add(Flora.WallTree(Random.Range(0, 4)), OnGround(s * Random.Range(22f, 32f), z, s0, 0.2f),
                          Random.Range(0f, 360f), Random.Range(1.1f, 1.5f));
        }

        // ---------------- River crossing ----------------

        /// <summary>A river cutting across the trail, crossed on a plank bridge with rope rails.</summary>
        void BuildRiver(ChunkBuilder b, Transform c, float s0, OwnedMeshes owned)
        {
            // Water surface spanning the whole valley
            var water = WaterMesh(s0, RiverA - 4f, RiverB + 4f);
            TrackPath.Deform(water, s0);
            owned.Meshes.Add(water);
            Prim.Shape(water, "River", c, Vector3.zero, Vector3.one, Mats.Water, null, false);

            // Deck of planks (slightly uneven, like a real rope bridge)
            float a = RiverA - 2.2f, e = RiverB + 2.2f;
            for (float z = a; z <= e; z += 0.34f)
                b.Add(plankMesh, Mats.Planks, Matrix4x4.TRS(new Vector3(Random.Range(-0.05f, 0.05f), -0.045f + Random.Range(-0.008f, 0.008f), z),
                      Quaternion.Euler(0f, Random.Range(-1.5f, 1.5f), 0f), Vector3.one));
            float len = e - a;
            for (int s = -1; s <= 1; s += 2)
            {
                b.Add(beamMesh, Mats.Planks, Matrix4x4.TRS(new Vector3(s * 3.9f, -0.25f, (a + e) * 0.5f), Quaternion.identity, new Vector3(1f, 1f, len)));

                // Posts at the ends, stilts in the middle, ropes slung between them
                var postZ = new[] { a, (a + e) * 0.5f, e };
                for (int i = 0; i < postZ.Length; i++)
                {
                    bool stilt = i == 1;
                    b.Add(stilt ? stiltMesh : postMesh, Mats.Bark, Matrix4x4.TRS(new Vector3(s * 4.35f, 1.15f, postZ[i]), Quaternion.identity, Vector3.one));
                }
                for (int i = 0; i < postZ.Length - 1; i++)
                    foreach (float h in new[] { 1.05f, 0.6f })
                    {
                        var rope = new List<Vector3>();
                        for (int k = 0; k <= 10; k++)
                        {
                            float t = k / 10f;
                            rope.Add(new Vector3(s * 4.35f, h - 0.25f * 4f * t * (1f - t), Mathf.Lerp(postZ[i], postZ[i + 1], t)));
                        }
                        b.AddTemp(Meshes.Sweep(rope, (t, ang) => 0.025f, 5, 1f, 3f), Mats.Rope, Matrix4x4.identity, false);
                    }
            }

            // Reeds and rocks on the banks
            for (int k = 0; k < 24; k++)
            {
                float x = (Random.value < 0.5f ? -1f : 1f) * Random.Range(5.5f, 30f);
                float z = Random.value < 0.5f ? Random.Range(RiverA - 3f, RiverA) : Random.Range(RiverB, RiverB + 3f);
                var p = OnGround(x, z, s0, 0.05f);
                if (p.y < WaterLevel - 0.2f) continue;
                b.Add(Random.value < 0.75f ? Flora.Tuft(k) : Flora.Rock(k), p, Random.Range(0f, 360f), Random.Range(0.7f, 1.3f));
            }

            // Sometimes a crocodile lurks in the water
            if (Random.value < 0.6f)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                Animals.SpawnCrocodile(c, side * Random.Range(9f, 24f), WaterLevel - 0.12f,
                                       s0 + (RiverA + RiverB) * 0.5f + Random.Range(-2f, 2f), side * 90f + Random.Range(-30f, 30f));
            }
        }

        static Mesh WaterMesh(float s0, float z0, float z1)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float zOff = Mathf.Repeat(s0, ZTile);
            int cols = 23;
            int rows = Mathf.CeilToInt(z1 - z0) + 1;
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    float x = Mathf.Lerp(-44f, 44f, j / (cols - 1f));
                    float z = Mathf.Lerp(z0, z1, i / (rows - 1f));
                    verts.Add(new Vector3(x, WaterLevel, z));
                    norms.Add(Vector3.up);
                    uvs.Add(new Vector2(x / 8f, (z + zOff) / 8f));
                }
            Grid(tris, rows, cols);
            return MakeMesh("WaterMesh", verts, norms, uvs, tris);
        }

        // ---------------- Cave ----------------

        /// <summary>A rock tunnel over the trail, with stalactites, glowing crystals and torches.</summary>
        void BuildCave(ChunkBuilder b, Transform c, float s0, OwnedMeshes owned)
        {
            var cave = CaveMesh(CaveA, CaveB, s0);
            TrackPath.Deform(cave, s0);
            owned.Meshes.Add(cave);
            Prim.Shape(cave, "Cave", c, Vector3.zero, Vector3.one, Mats.CaveRock, null, true);

            const float a = 7.2f, h = 6.4f;
            // Stalactites high on the ceiling (well above the runner) and stalagmites by the walls
            for (int k = 0; k < 16; k++)
            {
                float th = Random.Range(0.3f, 0.7f) * Mathf.PI;
                float z = Random.Range(CaveA + 2f, CaveB - 2f);
                var top = new Vector3(Mathf.Cos(th) * a * 0.97f, Mathf.Sin(th) * h * 0.97f, z);
                float len = Random.Range(0.5f, Mathf.Min(1.6f, top.y - 4.6f));
                if (len < 0.3f) continue;
                b.Add(coneMesh, Mats.CaveRock, Matrix4x4.TRS(top, Quaternion.identity, new Vector3(Random.Range(0.8f, 1.4f), len, Random.Range(0.8f, 1.4f))));
            }
            for (int k = 0; k < 10; k++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                var p = new Vector3(side * Random.Range(5.8f, 6.8f), 0f, Random.Range(CaveA + 2f, CaveB - 2f));
                b.Add(coneMesh, Mats.CaveRock, Matrix4x4.TRS(p, Quaternion.Euler(180f, 0f, 0f), new Vector3(1.2f, Random.Range(0.6f, 1.8f), 1.2f)));
            }

            // Glowing crystal clusters, each lighting its patch of wall
            for (int k = 0; k < 3; k++)
            {
                float side = k % 2 == 0 ? -1f : 1f;
                float z = Mathf.Lerp(CaveA + 6f, CaveB - 6f, k / 2f) + Random.Range(-2f, 2f);
                var basePos = new Vector3(side * 6.3f, 0f, z);
                for (int i = 0; i < 6; i++)
                {
                    var rot = Quaternion.Euler(Random.Range(-25f, 25f), 0f, -side * Random.Range(15f, 45f));
                    b.Add(crystalMesh, Mats.Crystal, Matrix4x4.TRS(basePos + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.4f, 0.4f)), rot,
                          Vector3.one * Random.Range(0.6f, 1.4f)), false);
                }
                AddLight(c, new Color(0.35f, 0.9f, 1f), 2.2f, 8f, side * 5.6f, 1.0f, s0 + z, false);
            }

            // Torches on the walls
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                float z = Mathf.Lerp(CaveA + 10f, CaveB - 10f, k) + Random.Range(-2f, 2f);
                var holder = new Vector3(side * 6.5f, 2.3f, z);
                b.Add(torchMesh, Mats.Bark, Matrix4x4.TRS(holder, Quaternion.Euler(0f, 0f, side * 25f), Vector3.one));
                var flame = Prim.Shape(flameMesh, "TorchFlame", c, Vector3.zero, new Vector3(0.16f, 0.26f, 0.16f), Mats.Flame, null, false);
                TrackPath.Place(flame.transform, side * 6.45f, 2.38f, s0 + z);
                AddLight(c, new Color(1f, 0.62f, 0.28f), 3f, 10f, side * 6.0f, 2.5f, s0 + z, true);
            }
        }

        static void AddLight(Transform parent, Color color, float intensity, float range, float x, float y, float s, bool flicker)
        {
            var go = new GameObject("CaveLight");
            go.transform.SetParent(parent, false);
            go.transform.position = TrackPath.Point(s, x, y);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            if (flicker) go.AddComponent<Flicker>();
        }

        /// <summary>Tunnel: rough inner arch, a rocky hill over it, and the two cave mouths.</summary>
        static Mesh CaveMesh(float z0, float z1, float s0)
        {
            const int arc = 30;
            const float a = 7.2f, h = 6.4f, oa = 12.5f, oh = 9.8f;
            int rings = Mathf.CeilToInt(z1 - z0) + 1;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            Vector3 Arch(float rx, float ry, int k, float z, float rough, int seed)
            {
                float th = Mathf.PI * k / arc;
                float n = 1f + rough * (Noise.Fbm3(new Vector3(th * 2.5f, (z + s0) * 0.35f, seed), 3, seed) - 0.5f) * 2f;
                // Flare out at the mouths so the openings look like natural rock rather than a pipe.
                float flare = 1f + 0.12f * (Tex.SS(z0 + 2f, z0, z) + Tex.SS(z1 - 2f, z1, z));
                return new Vector3(Mathf.Cos(th) * rx * n * flare, Mathf.Max(0f, Mathf.Sin(th) * ry * n * flare), z);
            }

            // Inner (faces the axis) and outer (faces away) surfaces
            int innerStart = verts.Count;
            for (int i = 0; i < rings; i++)
                for (int k = 0; k <= arc; k++)
                {
                    float z = Mathf.Lerp(z0, z1, i / (rings - 1f));
                    verts.Add(Arch(a, h, k, z, 0.1f, 5));
                    uvs.Add(new Vector2(k / (float)arc * 5.5f, (z + Mathf.Repeat(s0, ZTile)) / 4f));
                }
            int outerStart = verts.Count;
            for (int i = 0; i < rings; i++)
                for (int k = 0; k <= arc; k++)
                {
                    float z = Mathf.Lerp(z0, z1, i / (rings - 1f));
                    verts.Add(Arch(oa, oh, k, z, 0.16f, 9));
                    uvs.Add(new Vector2(k / (float)arc * 8f, (z + Mathf.Repeat(s0, ZTile)) / 4f));
                }
            for (int i = 0; i < rings - 1; i++)
                for (int k = 0; k < arc; k++)
                {
                    int ia = innerStart + i * (arc + 1) + k, ib = ia + 1, ic = ia + arc + 1, id = ic + 1;
                    Tri(tris, verts, ia, ib, ic, false); Tri(tris, verts, ib, id, ic, false);
                    int oA = outerStart + i * (arc + 1) + k, oB = oA + 1, oC = oA + arc + 1, oD = oC + 1;
                    Tri(tris, verts, oA, oB, oC, true); Tri(tris, verts, oB, oD, oC, true);
                }

            // Mouth faces: rings of rock joining inner and outer arches at each end
            foreach (bool end in new[] { false, true })
            {
                int row = end ? rings - 1 : 0;
                int start = verts.Count;
                for (int k = 0; k <= arc; k++)
                {
                    verts.Add(verts[innerStart + row * (arc + 1) + k]);
                    uvs.Add(new Vector2(k / (float)arc * 5.5f, 0f));
                    verts.Add(verts[outerStart + row * (arc + 1) + k]);
                    uvs.Add(new Vector2(k / (float)arc * 8f, 1.2f));
                }
                for (int k = 0; k < arc; k++)
                {
                    int i0 = start + k * 2, o0 = i0 + 1, i1 = i0 + 2, o1 = i0 + 3;
                    var facing = end ? Vector3.forward : Vector3.back;
                    TriFacing(tris, verts, i0, o0, i1, facing);
                    TriFacing(tris, verts, o0, o1, i1, facing);
                }
            }

            var m = new Mesh { name = "CaveMesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Adds a triangle wound so it faces toward (or away from) the tunnel's axis.</summary>
        static void Tri(List<int> tris, List<Vector3> v, int a, int b, int c, bool outward)
        {
            Vector3 centre = (v[a] + v[b] + v[c]) / 3f;
            var axis = new Vector3(0f, 0f, centre.z);
            Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            bool facesOut = Vector3.Dot(n, centre - axis) > 0f;
            if (facesOut == outward) { tris.Add(a); tris.Add(b); tris.Add(c); }
            else { tris.Add(a); tris.Add(c); tris.Add(b); }
        }

        static void TriFacing(List<int> tris, List<Vector3> v, int a, int b, int c, Vector3 facing)
        {
            Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(n, facing) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); }
            else { tris.Add(a); tris.Add(c); tris.Add(b); }
        }

        // ---------------- Decoration ----------------

        /// <summary>A vine sagging high across the trail (well above glider height).</summary>
        void Liana(ChunkBuilder b, float z)
        {
            if (Blocked(z)) return;
            var path = new List<Vector3>();
            float zEnd = z + Random.Range(-3f, 3f);
            float sag = Random.Range(1.2f, 2.2f);
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                float y = Mathf.Lerp(10.5f, 10f, t) - sag * 4f * t * (1f - t);
                path.Add(new Vector3(Mathf.Lerp(-9f, 9f, t), y, Mathf.Lerp(z, zEnd, t)));
            }
            float r = Random.Range(0.05f, 0.08f);
            b.AddTemp(Meshes.Sweep(path, (t, a) => r, 6, 1f, 2f), Mats.Vine, Matrix4x4.identity);

            // Strands hang down at the sides only, so they never read as an obstacle.
            for (int k = 0; k < 2; k++)
            {
                float t = k == 0 ? Random.Range(0.06f, 0.18f) : Random.Range(0.82f, 0.94f);
                Vector3 top = path[Mathf.RoundToInt(t * 12f)];
                float len = Random.Range(2.5f, 4f);
                var strand = new List<Vector3>();
                for (int i = 0; i <= 5; i++)
                    strand.Add(top + new Vector3(Mathf.Sin(i * 0.9f) * 0.12f, -len * i / 5f, 0f));
                b.AddTemp(Meshes.Sweep(strand, (tt, a) => r * 0.6f * (1f - tt * 0.5f), 5, 1f, 2f), Mats.Vine, Matrix4x4.identity);
                scratch.Parts.Clear();
                Flora.AddFrondRing(scratch, Mats.Frond, Vector3.zero, 3, 0.5f, 0.25f, -40f, 0.2f, Random.value * 360f);
                b.Add(scratch, Matrix4x4.Translate(strand[strand.Count - 1]));
            }
        }

        /// <summary>Soft beam of sunlight slanting down through the canopy.</summary>
        void SunShaft(ChunkBuilder b, float z)
        {
            Vector3 f = Atmosphere.Sun != null ? Atmosphere.Sun.transform.forward : new Vector3(-0.3f, -0.6f, 0.7f);
            float angle = Mathf.Atan2(f.x, -f.y) * Mathf.Rad2Deg;
            float w = Random.Range(1.5f, 4f), h = Random.Range(14f, 20f);
            var pos = new Vector3(Random.Range(-9f, 9f), Random.Range(13f, 17f), z);
            b.Add(shaftMesh, shaftMat, Matrix4x4.TRS(pos, Quaternion.Euler(0f, 0f, angle), new Vector3(w, h, 1f)), false);
        }

        // ---------------- Ground meshes ----------------

        static Mesh TrailMesh(float s0, bool riverGap)
        {
            const int cols = 11;
            const float step = 2.5f;
            int rows = Mathf.RoundToInt(ChunkLength / step) + 1;
            float zOff = Mathf.Repeat(s0, ZTile);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    float x = -Tex.TrailWidth / 2f + j * Tex.TrailWidth / (cols - 1);
                    float z = i * step;
                    verts.Add(new Vector3(x, 0f, z));
                    norms.Add(Vector3.up);
                    uvs.Add(new Vector2(j / (float)(cols - 1), (z + zOff) / Tex.TrailRepeat));
                }
            for (int i = 0; i < rows - 1; i++)
            {
                float mid = (i + 0.5f) * step;
                if (riverGap && mid > RiverA && mid < RiverB) continue; // the bridge spans this gap
                for (int j = 0; j < cols - 1; j++)
                {
                    int a = i * cols + j, b = a + 1, c = a + cols, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
            return MakeMesh("TrailMesh", verts, norms, uvs, tris);
        }

        static readonly float[] GroundCols = { 5f, 5.6f, 6.5f, 7.5f, 9f, 11f, 13.5f, 16.5f, 20f, 24f, 29f, 35f, 42f };

        static Mesh GroundMesh(float s0, int side)
        {
            const float step = 1.25f;
            const float uvScale = 1f / 6f;
            const float e = 0.3f;
            int rows = Mathf.RoundToInt(ChunkLength / step) + 1;
            int cols = GroundCols.Length;
            float zOff = Mathf.Repeat(s0, ZTile);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    // Columns ordered by increasing X on both sides keeps the winding consistent.
                    float x = side < 0 ? -GroundCols[cols - 1 - j] : GroundCols[j];
                    float z = i * step;
                    float ws = s0 + z;
                    verts.Add(new Vector3(x, GroundHeight(x, ws), z));
                    float dx = (GroundHeight(x + e, ws) - GroundHeight(x - e, ws)) / (2f * e);
                    float dz = (GroundHeight(x, ws + e) - GroundHeight(x, ws - e)) / (2f * e);
                    norms.Add(new Vector3(-dx, 1f, -dz).normalized);
                    uvs.Add(new Vector2(x * uvScale, (z + zOff) * uvScale));
                }
            Grid(tris, rows, cols);
            return MakeMesh("GroundMesh", verts, norms, uvs, tris);
        }

        static void Grid(List<int> tris, int rows, int cols)
        {
            for (int i = 0; i < rows - 1; i++)
                for (int j = 0; j < cols - 1; j++)
                {
                    int a = i * cols + j, b = a + 1, c = a + cols, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
        }

        static Mesh MakeMesh(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>Flickering torch light.</summary>
    public class Flicker : MonoBehaviour
    {
        Light l;
        float baseIntensity, seed;

        void Start()
        {
            l = GetComponent<Light>();
            baseIntensity = l.intensity;
            seed = Random.value * 10f;
        }

        void Update()
        {
            float t = Time.time * 9f + seed;
            l.intensity = baseIntensity * (0.8f + 0.2f * Mathf.PerlinNoise(t, seed));
        }
    }
}
