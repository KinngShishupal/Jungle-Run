using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// Builds the endless, winding jungle trail in 50 m chunks ahead of the player and removes
    /// chunks behind. Chunks are authored in straight "track space" and bent onto a
    /// <see cref="TrackPath"/>. Each chunk gets rows of obstacles, banana trails, the occasional
    /// power-up, and sometimes a landmark: a river crossing, a cave or a rocky pass.
    ///
    /// Every so often the trail forks. A signpost on the island between the two paths shows
    /// where each leads: one continues in the current world, the other enters a new one
    /// (Ice Age, Water World, Desert). The runner's lane at the split decides the path.
    /// </summary>
    public class TrackGenerator : MonoBehaviour
    {
        const float ChunkLength = 50f;
        const float ViewAhead = 150f;
        const float SafeStart = 45f;       // obstacle-free run-up at the start
        const float PowerUpChance = 0.07f; // per row

        // River crossing span inside a chunk (local metres) and cave span.
        const float RiverA = 17.5f, RiverB = 30f;
        // A ravine across the trail (jungle): swing over it on a vine, or fall in
        const float DitchA = 20f, DitchB = 32.5f, DitchDepth = 9f;
        // A broken stretch of boardwalk (water world): cross the open water in a canoe, or fall in
        const float WaterGapA = 8f, WaterGapLength = 160f;   // a long canoe trip, running on over several chunks

        /// <summary>Where a gap (ravine or open water) lies within a chunk, for the given world.</summary>
        static Vector2 GapSpan(BiomeType b) => b == BiomeType.WaterWorld ? new Vector2(WaterGapA, WaterGapA + WaterGapLength) : new Vector2(DitchA, DitchB);

        /// <summary>The part of a gap lying inside the chunk being built (chunk-local), or (-100, -100).</summary>
        Vector2 chunkGap = new Vector2(-100f, -100f);
        const float CaveA = 6f, CaveB = 44f;
        const float WaterLevel = -1.35f;
        const float SeaLevel = -0.45f;          // water world sea surface, just below the boardwalk
        static float groundDrop;                // how far the land is sunk (water world)

        // Forks
        const float ForkTurn = 34f;         // each branch bends this far away from the other
        const float ForkLength = 100f;      // both branches are built this far before the runner chooses
        const float ForkEntryClear = 30f;   // obstacle-free stretch at the start of each branch

        // Lane contents for a row
        const int Empty = -1;

        enum Feature { None, River, Cave, RockyPass, Ditch }

        struct Span { public float S0, S1, X; }   // X: the canoe's lane (water gaps)

        /// <summary>One trail being generated: its path, world and progress.</summary>
        class Cursor
        {
            public TrackPath Path;
            public BiomeType Biome;
            public float Spawn, NextRow;
            public readonly int[] LastRow = { Empty, Empty, Empty };
            public int InnerSide;            // side facing the other branch (no scenery there near the fork)
            public float ForkS = -1e9f;
        }

        static readonly List<Span> rivers = new List<Span>();
        static readonly List<Span> ditches = new List<Span>();

        readonly List<GameObject> chunks = new List<GameObject>();
        readonly List<float> chunkStarts = new List<float>();
        PlayerController player;
        Cursor main, forkL, forkR;
        bool forkPending;
        float forkS, nextForkS;
        int newWorldSide;
        BiomeType nextForkWorld;
        bool prewarmStarted;
        int chunkIndex;
        Feature lastFeature;
        float birdTimer = 6f;
        float riverLocalA = -100f, riverLocalB = -100f; // river span of the chunk being built
        BiomeType chunkBiome;                            // world of the chunk being built
        float trailLift;                                 // keeps overlapping fork trails from flickering

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
            DesertLife.ClearRoaming();
            Canoe.ClearAll();
            chunks.Clear();
            chunkStarts.Clear();
            rivers.Clear();
            ditches.Clear();
            TrackPath.Reset(Random.Range(1, int.MaxValue));
            var startWorld = Biomes.StartWorld;
            Biomes.ResetTo(startWorld);
            main = new Cursor { Path = TrackPath.Current, Biome = startWorld, Spawn = -20f, NextRow = SafeStart };
            forkL = forkR = null;
            forkPending = false;
            nextForkS = 300f;
            nextForkWorld = Biomes.PickNext(startWorld);
            prewarmStarted = false;
            chunkIndex = 0;
            lastFeature = Feature.None;
            Generate(0f);
        }

        void Update()
        {
            if (player == null) return;
            float ps = player.TrackS;
            Generate(ps);
            if (forkPending && ps >= forkS) Commit();

            // Generous margin: banana trails can extend a little past their chunk's end.
            for (int i = chunks.Count - 1; i >= 0; i--)
            {
                if (chunkStarts[i] + ChunkLength >= ps - 35f) continue;
                Destroy(chunks[i]);
                chunks.RemoveAt(i);
                chunkStarts.RemoveAt(i);
            }
            while (rivers.Count > 0 && rivers[0].S1 < ps - 120f) rivers.RemoveAt(0);
            while (ditches.Count > 0 && ditches[0].S1 < ps - 120f) ditches.RemoveAt(0);

            // Flowing water (and lava, and drifting ice)
            float t = Time.time;
            Mats.Water.SetTextureOffset("_BaseMap", new Vector2(t * 0.035f, t * 0.01f));
            foreach (var m in Biomes.FlowingMaterials)
                if (m != null) m.SetTextureOffset("_BaseMap", new Vector2(t * 0.012f, t * 0.004f));

            // Now and then a flock of parrots (seagulls over the sea) crosses the sky ahead.
            var world = Biomes.Current;
            bool flocks = world == BiomeType.Jungle || world == BiomeType.WaterWorld; // the desert has circling vultures instead
            if (flocks && GameManager.I != null && GameManager.I.State == GameState.Playing && (birdTimer -= Time.deltaTime) <= 0f)
            {
                birdTimer = Random.Range(8f, 16f);
                float side = Random.value < 0.5f ? -1f : 1f;
                Animals.SpawnFlock(TrackPath.Point(ps + Random.Range(45f, 70f), side * 45f, Random.Range(13f, 18f)),
                                   TrackPath.Point(ps + Random.Range(70f, 100f), -side * 45f, Random.Range(14f, 20f)),
                                   Random.Range(8f, 11f), false, world == BiomeType.WaterWorld);
            }
        }

        /// <summary>Removes obstacles just ahead of the player (used on revive).</summary>
        public void ClearAhead(float fromS, float length)
        {
            foreach (var c in chunks)
                foreach (var ob in c.GetComponentsInChildren<Obstacle>())
                    if (ob.Path == TrackPath.Current && ob.S > fromS - 3f && ob.S < fromS + length) ob.Smash();
        }

        // ---------------- Generation ----------------

        /// <summary>Keeps chunks built up to <see cref="ViewAhead"/> metres in front of the runner.</summary>
        void Generate(float ps)
        {
            if (!forkPending)
            {
                if (!prewarmStarted && nextForkS - main.Spawn < 260f)
                {
                    prewarmStarted = true;
                    StartCoroutine(Biomes.Prewarm(nextForkWorld));
                }
                while (main.Spawn < ps + ViewAhead)
                {
                    if (main.Spawn >= nextForkS) { CreateFork(); break; }
                    SpawnChunk(main);
                }
            }
            if (forkPending)
            {
                float limit = Mathf.Min(ps + ViewAhead, forkS + ForkLength);
                while (forkL.Spawn < limit) SpawnChunk(forkL);
                while (forkR.Spawn < limit) SpawnChunk(forkR);
            }
        }

        /// <summary>The trail splits: one branch stays in this world, the other leads to a new one.</summary>
        void CreateFork()
        {
            forkPending = true;
            forkS = main.Spawn;
            newWorldSide = Random.value < 0.5f ? -1 : 1;
            var left = main.Path.Branch(forkS, -ForkTurn, Random.Range(1, int.MaxValue));
            var right = main.Path.Branch(forkS, ForkTurn, Random.Range(1, int.MaxValue));
            forkL = new Cursor
            {
                Path = left, Biome = newWorldSide < 0 ? nextForkWorld : main.Biome,
                Spawn = forkS, NextRow = forkS + ForkEntryClear, InnerSide = 1, ForkS = forkS
            };
            forkR = new Cursor
            {
                Path = right, Biome = newWorldSide > 0 ? nextForkWorld : main.Biome,
                Spawn = forkS, NextRow = forkS + ForkEntryClear, InnerSide = -1, ForkS = forkS
            };
            var island = ForkIsland.Build(transform, forkS, left, right, forkL.Biome, forkR.Biome, main.Biome);
            chunks.Add(island);
            chunkStarts.Add(forkS + 40f);
        }

        /// <summary>The runner reached the split: their lane decides the path (centre keeps the current world).</summary>
        void Commit()
        {
            forkPending = false;
            float x = player.TrackX;
            var stay = newWorldSide < 0 ? forkR : forkL;
            var chosen = x < -0.6f ? forkL : x > 0.6f ? forkR : stay;
            TrackPath.SetCurrent(chosen.Path);
            main = chosen;
            forkL = forkR = null;
            if (chosen.Biome != Biomes.Current)
            {
                Biomes.Enter(chosen.Biome);
                if (GameManager.I != null) GameManager.I.OnEnterWorld(chosen.Biome);
            }
            nextForkS = forkS + Random.Range(650f, 900f);
            nextForkWorld = Biomes.PickNext(main.Biome);
            prewarmStarted = false;
        }

        Feature PickFeature()
        {
            if (chunkIndex < 2) return Feature.None;
            float r = Random.value;
            var f = r < 0.16f ? Feature.River : r < 0.30f ? Feature.Cave : r < 0.46f ? Feature.RockyPass : r < 0.62f ? Feature.Ditch : Feature.None;
            return f == lastFeature ? Feature.None : f;
        }

        void SpawnChunk(Cursor c)
        {
            float s0 = c.Spawn;
            bool nearFork = s0 < c.ForkS + ForkLength;
            var feature = nearFork ? Feature.None : PickFeature();
            if (c.Biome == BiomeType.WaterWorld && feature == Feature.River) feature = Feature.None; // all water already
            // Gaps: ravines with swing vines in the jungle, open water with a canoe in the water world
            if (feature == Feature.Ditch && c.Biome != BiomeType.Jungle && c.Biome != BiomeType.WaterWorld) feature = Feature.None;
            // A long stretch of open water can run on from the previous chunk into this one...
            bool continuing = c == main && ditches.Count > 0 && ditches[ditches.Count - 1].S0 < s0 && ditches[ditches.Count - 1].S1 > s0;
            if (continuing) feature = Feature.Ditch;
            // ...and is only started where no fork is due before it ends
            if (!continuing && feature == Feature.Ditch && c.Biome == BiomeType.WaterWorld && (c != main || s0 + WaterGapA + WaterGapLength + ChunkLength > nextForkS))
                feature = Feature.None;
            if (feature == Feature.River) rivers.Add(new Span { S0 = s0 + RiverA, S1 = s0 + RiverB });
            if (feature == Feature.Ditch && !continuing) { var g = GapSpan(c.Biome); ditches.Add(new Span { S0 = s0 + g.x, S1 = s0 + g.y, X = Random.Range(-1, 2) * PlayerController.LaneWidth }); }
            chunkGap = new Vector2(-100f, -100f);
            if (feature == Feature.Ditch)
            {
                var d = ditches[ditches.Count - 1];
                chunkGap = new Vector2(Mathf.Max(0f, d.S0 - s0), Mathf.Min(ChunkLength, d.S1 - s0));
            }

            TrackPath.Use(c.Path);
            var chunk = new GameObject("Chunk " + c.Biome + " " + feature);
            chunk.transform.SetParent(transform, false);
            trailLift = nearFork && c.InnerSide > 0 ? 0.012f : 0f;
            BuildScenery(chunk.transform, s0, feature, c.Biome, nearFork ? c.InnerSide : 0);
            if (feature == Feature.Ditch && c.Biome == BiomeType.WaterWorld) SpawnWaterHazards(chunk.transform, s0, ditches[ditches.Count - 1], c.Biome);
            if (feature == Feature.Ditch && !continuing)
            {
                if (c.Biome == BiomeType.WaterWorld) SpawnCanoe(chunk.transform, s0, c.Biome, ditches[ditches.Count - 1].X);   // sail across the open water
                else SpawnDitchVines(chunk.transform, s0, c.Biome);                             // swing across the ravine
            }

            float end = s0 + ChunkLength;
            while (c.NextRow < end)
            {
                if (!OnBridge(c.NextRow)) BuildRow(chunk.transform, c.NextRow, c.Biome, c.LastRow);
                c.NextRow += RowGap();
            }
            TrackPath.Done();

            chunks.Add(chunk);
            chunkStarts.Add(s0);
            c.Spawn = end;
            chunkIndex++;
            lastFeature = feature;
        }

        static bool OnBridge(float s)
        {
            foreach (var r in rivers) if (s > r.S0 - 4f && s < r.S1 + 4f) return true;
            // Keep the run-up to a ravine clear, so the runner can line up and jump for a vine
            foreach (var r in ditches) if (s > r.S0 - 14f && s < r.S1 + 14f) return true;   // ...and room to land and recover after
            return false;
        }

        /// <summary>True over a ravine (the trail has a gap here).</summary>
        public static bool InDitch(float s)
        {
            foreach (var r in ditches) if (s > r.S0 + 0.2f && s < r.S1 - 0.2f) return true;
            return false;
        }

        /// <summary>How far across a ravine s is (0 at the near edge, 1 at the far edge), or -1 if not over one.</summary>
        public static float DitchProgress(float s)
        {
            foreach (var r in ditches) if (s > r.S0 && s < r.S1) return (s - r.S0) / (r.S1 - r.S0);
            return -1f;
        }

        /// <summary>Where the trail resumes on the far side of the ravine at <paramref name="s"/> (or s itself).</summary>
        public static float DitchFarEdge(float s)
        {
            foreach (var r in ditches) if (s > r.S0 - 1f && s < r.S1 + 1f) return r.S1;
            return s;
        }

        /// <summary>A canoe waiting just past the broken end of the boardwalk in its lane, and a few coins to start the trip.</summary>
        void SpawnCanoe(Transform parent, float s0, BiomeType biome, float x)
        {
            // Running off the end he drops into it 1.9 m (slowest) to 4.6 m (fastest) out: the canoe spans 0.75-5.05 m
            Canoe.Spawn(transform, s0 + WaterGapA + 2.9f, x);   // owned by the generator: the ride outlasts this chunk
            for (float z = WaterGapA + 7f; z < WaterGapA + 15f; z += 3f)
                Pickup.Create(PickupType.Banana, new Vector3(x, Canoe.FloorY + 0.9f, s0 + z), parent, biome);
        }

        /// <summary>
        /// Hazards on the open water, for the part of the gap inside this chunk: coral rocks and drifting logs
        /// blocking one or two lanes at a time (more often as the run speeds up), with coins in a clear lane.
        /// The first and last 16 m stay clear for boarding and hopping out.
        /// </summary>
        void SpawnWaterHazards(Transform chunk, float s0, Span gap, BiomeType biome)
        {
            float z0 = Mathf.Max(s0, gap.S0 + 16f), z1 = Mathf.Min(s0 + ChunkLength, gap.S1 - 16f);
            float d = Difficulty;
            var lanes = new List<int> { -1, 0, 1 };
            for (float s = z0 + Random.Range(1f, 5f); s < z1; s += Random.Range(Mathf.Lerp(16f, 11f, d), Mathf.Lerp(22f, 15f, d)))
            {
                for (int i = lanes.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (lanes[i], lanes[j]) = (lanes[j], lanes[i]); }
                int blocked = Random.value < Mathf.Lerp(0.25f, 0.6f, d) ? 2 : 1;
                for (int k = 0; k < blocked; k++)
                {
                    bool rock = Random.value < 0.6f;
                    float x = lanes[k] * PlayerController.LaneWidth + (rock ? Random.Range(-0.4f, 0.4f) : 0f);
                    WaterHazard.Spawn(chunk, rock ? WaterHazard.Kind.Rock : WaterHazard.Kind.Log, s, x);
                }
                float free = lanes[blocked] * PlayerController.LaneWidth;
                for (int c = 0; c < 3; c++)
                    Pickup.Create(PickupType.Banana, new Vector3(free, Canoe.FloorY + 0.9f, s + 3f + c * 2.5f), chunk, biome);
            }
        }

        /// <summary>Three vines (one per lane) hanging just over the near edge, and a line of money tracing a swing.</summary>
        void SpawnDitchVines(Transform parent, float s0, BiomeType biome)
        {
            float pivot = s0 + DitchA - 0.4f;
            for (int lane = -1; lane <= 1; lane++)
                VineSwing.Spawn(parent, pivot, lane * PlayerController.LaneWidth, lane == 0);
            const float R = VineSwing.Length;
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.Lerp(-0.25f, 0.68f, i / 6f);
                float handS = pivot + R * Mathf.Sin(a), handH = VineSwing.PivotHeight - R * Mathf.Cos(a);
                Pickup.Create(PickupType.Banana, new Vector3(0f, handH - ExplorerRig.GripPoint.y + 0.6f, handS - ExplorerRig.GripPoint.z), parent, biome);
            }
        }

        /// <summary>
        /// The ravine where the trail is cut: rock walls under both edges, a rocky floor with a stream far below,
        /// boulders on the bottom, stones on the lips and roots dangling over the drop.
        /// </summary>
        void BuildRavine(ChunkBuilder b, Transform c, float s0, OwnedMeshes owned)
        {
            const float W = 6.2f, D = DitchDepth;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Quad(Vector3 a, Vector3 bb, Vector3 cc, Vector3 d, Vector3 n)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(bb); verts.Add(cc); verts.Add(d);
                for (int k = 0; k < 4; k++) norms.Add(n);
                uvs.Add(new Vector2(a.x, a.y + a.z) / 3f); uvs.Add(new Vector2(bb.x, bb.y + bb.z) / 3f);
                uvs.Add(new Vector2(cc.x, cc.y + cc.z) / 3f); uvs.Add(new Vector2(d.x, d.y + d.z) / 3f);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
            }
            // Subdivide each face so it follows the curve of the trail when deformed
            for (int k = 0; k < 6; k++)
            {
                float x0 = Mathf.Lerp(-W, W, k / 6f), x1 = Mathf.Lerp(-W, W, (k + 1) / 6f);
                Quad(new Vector3(x0, 0f, DitchA), new Vector3(x1, 0f, DitchA), new Vector3(x1, -D, DitchA), new Vector3(x0, -D, DitchA), Vector3.forward);
                Quad(new Vector3(x1, 0f, DitchB), new Vector3(x0, 0f, DitchB), new Vector3(x0, -D, DitchB), new Vector3(x1, -D, DitchB), Vector3.back);
                for (int z = 0; z < 5; z++)
                {
                    float z0 = Mathf.Lerp(DitchA, DitchB, z / 5f), z1 = Mathf.Lerp(DitchA, DitchB, (z + 1) / 5f);
                    Quad(new Vector3(x0, -D, z0), new Vector3(x0, -D, z1), new Vector3(x1, -D, z1), new Vector3(x1, -D, z0), Vector3.up);
                }
            }
            var m = MakeMesh("Ravine", verts, norms, uvs, tris);
            TrackPath.Deform(m, s0);
            owned.Meshes.Add(m);
            Prim.Shape(m, "Ravine", c, Vector3.zero, Vector3.one, Biomes.Remap(Mats.CaveRock, chunkBiome), null, false);

            // A stream far below
            var water = WaterMesh(s0, DitchA - 1.5f, DitchB + 1.5f, -D + 0.5f);
            TrackPath.Deform(water, s0);
            owned.Meshes.Add(water);
            Prim.Shape(water, "RavineStream", c, Vector3.zero, Vector3.one, Biomes.Remap(Mats.Water, chunkBiome), null, false);

            // Boulders on the floor, stones along both lips, roots dangling over the drop
            for (int i = 0; i < 8; i++)
                b.Add(Flora.Rock(i), new Vector3(Random.Range(-5f, 5f), -D + 0.2f, Random.Range(DitchA + 1f, DitchB - 1f)), Random.Range(0f, 360f), Random.Range(0.8f, 2f));
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? DitchA - 0.25f : DitchB + 0.25f;
                for (int i = 0; i < 4; i++)
                {
                    float x = (i % 2 == 0 ? -1f : 1f) * Random.Range(3.6f, 5f);
                    b.Add(Flora.Rock(i + side), new Vector3(x, -0.15f, z + Random.Range(-0.3f, 0.3f)), Random.Range(0f, 360f), Random.Range(0.35f, 0.6f));
                }
                for (int i = 0; i < 7; i++)
                {
                    float x = Random.Range(-5.5f, 5.5f), len = Random.Range(1.5f, 4f);
                    float dz = side == 0 ? 0.15f : -0.15f;
                    var root = new List<Vector3> { new Vector3(x, 0f, z - dz), new Vector3(x + 0.1f, -len * 0.5f, z + dz), new Vector3(x - 0.05f, -len, z + dz * 1.5f) };
                    b.Add(Meshes.Sweep(root, (u, a) => Mathf.Lerp(0.05f, 0.015f, u), 5, 1f, 1f), Mats.Vine, Matrix4x4.identity, false);
                }
            }
        }

        float Difficulty => GameManager.I != null ? GameManager.I.Difficulty : 0f;

        float RowGap() => Mathf.Lerp(22f, 14f, Difficulty) + Random.Range(-1f, 3f);

        static bool Jumpable(int t) => t == (int)ObstacleType.Log || t == (int)ObstacleType.Snake;
        static bool Passable(int t) => t == Empty || Jumpable(t) || t == (int)ObstacleType.Branch;
        static bool Charger(int t) => t == (int)ObstacleType.Boar || t == (int)ObstacleType.Tiger;

        void BuildRow(Transform parent, float z, BiomeType biome, int[] lastRow)
        {
            float d = Difficulty;
            int[] row = { Empty, Empty, Empty };

            // How many lanes get an obstacle
            int blocked = Random.value < Mathf.Lerp(0.25f, 0.6f, d) ? 2 : 1;
            if (Random.value < 0.12f * d) blocked = 3;

            int[] order = Shuffle(new[] { 0, 1, 2 });
            for (int i = 0; i < blocked; i++) row[order[i]] = (int)RandomType(d, biome);

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
            {
                if (row[i] == Empty) continue;
                var ob = Obstacle.Create((ObstacleType)row[i], i - 1, z, parent, biome);
                if (!ob.Native) Biomes.RemapRenderers(ob.gameObject, biome); // world-specific animals keep their own look
            }

            PlaceRewards(parent, row, z, biome);
            System.Array.Copy(row, lastRow, 3);
        }

        void PlaceRewards(Transform parent, int[] row, float z, BiomeType biome)
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
                    Pickup.Create(PickupType.Banana, new Vector3(lane * PlayerController.LaneWidth, h, z + k * 1.4f), parent, biome);
                }
            }
            else if (branches.Count > 0 && r < 0.5f)
            {
                // Low line under a branch: rewards the roll
                int lane = branches[Random.Range(0, branches.Count)] - 1;
                for (int k = -2; k <= 2; k++)
                    Pickup.Create(PickupType.Banana, new Vector3(lane * PlayerController.LaneWidth, 0.45f, z + k * 1.5f), parent, biome);
            }
            else if (empties.Count > 0 && r < 0.9f)
            {
                // Straight trail through a free lane
                int lane = empties[Random.Range(0, empties.Count)] - 1;
                for (int k = -3; k <= 3; k++)
                    Pickup.Create(PickupType.Banana, new Vector3(lane * PlayerController.LaneWidth, 0.9f, z + k * 1.8f), parent, biome);
            }
        }

        static ObstacleType RandomType(float d, BiomeType biome)
        {
            // Animals join in as the run gets faster.
            float[] w =
            {
                26f,                       // Log
                22f,                       // Branch
                24f,                       // Boulder
                d > 0.2f ? 10f : 0f,       // Boar
                d > 0.05f && biome != BiomeType.IceAge ? 12f : 0f, // Snake (too cold in the ice age)
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
            foreach (var r in ditches)
            {
                // Sheer ravine walls: the ground falls away within about a metre of the edge
                float half = (r.S1 - r.S0) * 0.5f;
                float d = Mathf.Abs(s - (r.S0 + r.S1) * 0.5f);
                h = Mathf.Lerp(h, -DitchDepth, Tex.SS(half + 0.9f, half - 0.2f, d));
            }
            return h;
        }

        bool Blocked(float localZ) => localZ > riverLocalA - 3f && localZ < riverLocalB + 3f;

        void BuildScenery(Transform c, float s0, Feature feature, BiomeType biome, int skipSide)
        {
            var owned = c.gameObject.AddComponent<OwnedMeshes>();
            chunkBiome = biome;
            // In the water world the land sinks: only sandbars and islands break the surface.
            groundDrop = biome == BiomeType.WaterWorld ? 1.4f : 0f;
            bool river = feature == Feature.River;
            bool ditch = feature == Feature.Ditch;
            var gap = chunkGap;
            riverLocalA = river ? RiverA : ditch ? gap.x : -100f;
            riverLocalB = river ? RiverB : ditch ? gap.y : -100f;

            var trail = TrailMesh(s0, river, ditch ? gap.x : -100f, ditch ? gap.y : -100f);
            TrackPath.Deform(trail, s0);
            owned.Meshes.Add(trail);
            Prim.Shape(trail, "Trail", c, new Vector3(0f, trailLift, 0f), Vector3.one, Biomes.Remap(Mats.Trail, biome), null, false);
            for (int s = -1; s <= 1; s += 2)
            {
                if (s == skipSide) continue; // the other branch of a fork lies this way
                var ground = GroundMesh(s0, s);
                TrackPath.Deform(ground, s0);
                owned.Meshes.Add(ground);
                Prim.Shape(ground, "Ground", c, new Vector3(0f, -groundDrop, 0f), Vector3.one, Biomes.Remap(Mats.Ground, biome), null, false);
            }

            var b = new ChunkBuilder();
            for (int s = -1; s <= 1; s += 2)
            {
                if (s == skipSide) continue;
                if (biome == BiomeType.Desert)
                {
                    DesertScenery(b, s, s0, feature);
                    continue;
                }
                if (biome == BiomeType.IceAge && feature != Feature.Cave)
                {
                    IceScenery(b, s, s0, feature == Feature.RockyPass);
                    continue;
                }
                if (biome == BiomeType.WaterWorld && feature != Feature.Cave)
                {
                    WaterScenery(b, s, s0);
                    JungleWall(b, s, s0);
                    continue;
                }
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
            if (biome == BiomeType.WaterWorld)
            {
                BuildSea(b, c, s0, owned, skipSide, ditch ? gap : new Vector2(-100f, -100f));
                if (feature != Feature.Cave) SeaAnimals(c, s0, skipSide);
            }
            if (feature == Feature.Ditch && biome == BiomeType.Jungle) BuildRavine(b, c, s0, owned);
            if (feature == Feature.Cave) BuildCave(b, c, s0, owned);
            else
            {
                bool vines = biome == BiomeType.Jungle;
                if (vines && skipSide == 0 && Random.value < 0.55f) Liana(b, Random.Range(5f, 45f));
                int shafts = biome == BiomeType.Desert ? 0 : Random.Range(1, 4);
                for (int i = 0; i < shafts; i++) SunShaft(b, Random.Range(0f, ChunkLength));
            }
            if (biome == BiomeType.Desert) DesertAnimals(c, s0, skipSide, feature);
            b.Build(c, s0, m => Biomes.Remap(m, biome));
        }

        static Vector3 OnGround(float x, float localZ, float s0, float sink = 0.05f) =>
            new Vector3(x, GroundHeight(x, s0 + localZ) - sink - groundDrop, localZ);

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
            {
                var p = OnGround(s * Random.Range(14f, 19f), z, s0, 0.2f);
                if (!Blocked(z) && AboveWater(p))
                    b.Add(Flora.WallTree(Random.Range(0, 4)), p, Random.Range(0f, 360f), Random.Range(0.9f, 1.2f));
            }
            for (float z = Random.Range(0f, 4f); z < ChunkLength; z += Random.Range(6f, 9f))
            {
                var p = OnGround(s * Random.Range(22f, 32f), z, s0, 0.2f);
                if (!Blocked(z) && AboveWater(p))
                    b.Add(Flora.WallTree(Random.Range(0, 4)), p, Random.Range(0f, 360f), Random.Range(1.1f, 1.5f));
            }
        }

        /// <summary>In the water world, only places on dry sand get plants.</summary>
        static bool AboveWater(Vector3 p) => groundDrop <= 0f || p.y > SeaLevel + 0.15f;

        // ---------------- Desert ----------------

        /// <summary>
        /// Sonoran-style desert: dry grass, barrel cactus, agave, prickly pear, shrubs and the odd bleached skull
        /// by the trail; saguaros and dead trees further out; rippled dunes and sandstone buttes on the horizon.
        /// Rocky passes become sandstone hoodoos; a river becomes a palm-fringed oasis.
        /// </summary>
        void DesertScenery(ChunkBuilder b, int s, float s0, Feature feature)
        {
            bool cave = feature == Feature.Cave, pass = feature == Feature.RockyPass;
            if (!cave)
            {
                // Trail edge
                for (float z = Random.Range(0f, 1.5f); z < ChunkLength; z += Random.Range(1.3f, 2.6f))
                {
                    if (Blocked(z)) continue;
                    int v = Random.Range(0, 4);
                    float yaw = Random.Range(0f, 360f);
                    float r = Random.value;
                    if (r < 0.28f) b.Add(Flora.Tuft(v), OnGround(s * Random.Range(4.6f, 6.6f), z, s0, 0.02f), yaw, Random.Range(0.8f, 1.3f));
                    else if (r < 0.42f) b.Add(Flora.SandstoneRock(v), OnGround(s * Random.Range(5.5f, 7.5f), z, s0, 0.12f), yaw, Random.Range(0.35f, 0.8f));
                    else if (r < 0.56f) b.Add(Flora.BarrelCactus(v), OnGround(s * Random.Range(5.4f, 7.4f), z, s0, 0.04f), yaw, Random.Range(0.8f, 1.2f));
                    else if (r < 0.68f) b.Add(Flora.Agave(v), OnGround(s * Random.Range(5.8f, 8f), z, s0, 0.02f), yaw, Random.Range(0.8f, 1.2f));
                    else if (r < 0.8f) b.Add(Flora.DesertShrub(v), OnGround(s * Random.Range(5.5f, 8f), z, s0, 0.02f), yaw, Random.Range(0.8f, 1.3f));
                    else if (r < 0.9f) b.Add(Flora.PricklyPear(v), OnGround(s * Random.Range(6f, 8f), z, s0, 0.02f), yaw, Random.Range(0.9f, 1.3f));
                    else if (r < 0.95f) b.Add(Flora.Skull(v), OnGround(s * Random.Range(5f, 6.5f), z, s0, 0.06f), yaw, Random.Range(0.9f, 1.2f));
                    else b.Add(Flora.Tumbleweed(v), OnGround(s * Random.Range(5.5f, 7.5f), z, s0, 0.05f), yaw, Random.Range(0.8f, 1.1f));
                }

                // Middle distance: saguaro, big prickly pear, dead trees (hoodoos and boulders in a pass)
                for (float z = Random.Range(0f, 3f); z < ChunkLength; z += Random.Range(3.5f, 6.5f))
                {
                    if (Blocked(z)) continue;
                    float x = s * Random.Range(8.5f, 14f);
                    int v = Random.Range(0, 4);
                    float yaw = Random.Range(0f, 360f);
                    if (pass)
                    {
                        if (Random.value < 0.55f) b.Add(Flora.Hoodoo(v), OnGround(x, z, s0, 0.3f), yaw, Random.Range(0.8f, 1.3f));
                        else { float sc = Random.Range(2f, 4f); b.Add(Flora.SandstoneRock(v), OnGround(x, z, s0, sc * 0.25f), yaw, sc); }
                        continue;
                    }
                    float r = Random.value;
                    if (r < 0.5f) b.Add(Flora.Saguaro(v), OnGround(x, z, s0, 0.05f), yaw, Random.Range(0.85f, 1.15f));
                    else if (r < 0.64f) b.Add(Flora.PricklyPear(v), OnGround(x, z, s0, 0.02f), yaw, Random.Range(1.3f, 1.8f));
                    else if (r < 0.76f) b.Add(Flora.DeadTree(v), OnGround(x, z, s0, 0.05f), yaw, Random.Range(0.9f, 1.3f));
                    else if (r < 0.9f) b.Add(Flora.DesertShrub(v), OnGround(x, z, s0, 0.02f), yaw, Random.Range(1.2f, 1.7f));
                    else b.Add(Flora.Hoodoo(v), OnGround(x, z, s0, 0.3f), yaw, Random.Range(0.5f, 0.8f));
                }
            }

            // Far: saguaro scattered over the flats, rippled dunes, and buttes on the skyline
            for (float z = Random.Range(0f, 4f); z < ChunkLength; z += Random.Range(5f, 9f))
                if (!Blocked(z))
                    b.Add(Flora.Saguaro(Random.Range(0, 4)), OnGround(s * Random.Range(15f, 26f), z, s0, 0.05f), Random.Range(0f, 360f), Random.Range(0.9f, 1.3f));
            for (float z = Random.Range(0f, 8f); z < ChunkLength; z += Random.Range(9f, 15f))
                b.Add(Flora.Dune(Random.Range(0, 4)), OnGround(s * Random.Range(17f, 36f), z, s0, 0.6f), Random.Range(-25f, 25f), Random.Range(0.9f, 1.6f));
            if (Random.value < 0.4f)
            {
                float z = Random.Range(5f, ChunkLength - 5f);
                b.Add(Flora.Hoodoo(Random.Range(0, 4)), OnGround(s * Random.Range(28f, 38f), z, s0, 0.5f), Random.Range(0f, 360f), Random.Range(2.2f, 3.6f));
            }

            // Oasis: date palms, reeds and shrubs around the water
            if (feature == Feature.River)
                for (int i = 0; i < 6; i++)
                {
                    bool before = i % 2 == 0;
                    float z = before ? riverLocalA - 3.2f - Random.Range(0f, 4f) : riverLocalB + 3.2f + Random.Range(0f, 4f);
                    var p = OnGround(s * Random.Range(5.5f, 14f), z, s0, 0.1f);
                    if (i < 4) b.Add(Flora.Palm(Random.Range(0, 4)), p, (s < 0 ? 0f : 180f) + Random.Range(-60f, 60f), Random.Range(0.8f, 1.1f));
                    else b.Add(Flora.Tuft(Random.Range(0, 4)), p, Random.Range(0f, 360f), Random.Range(1.3f, 1.8f));
                }
        }

        /// <summary>Desert wildlife around the trail: tumbleweeds, circling vultures, a camel caravan, racing ostriches.</summary>
        void DesertAnimals(Transform c, float s0, int skipSide, Feature feature)
        {
            int side = skipSide != 0 ? -skipSide : (Random.value < 0.5f ? -1 : 1);
            if (Random.value < 0.6f)
            {
                // Blown across the trail by the wind
                float wind = Random.value < 0.5f ? -1f : 1f;
                int n = Random.Range(1, 3);
                for (int i = 0; i < n; i++)
                    DesertLife.SpawnTumbleweed(c, wind * -Random.Range(9f, 18f), s0 + Random.Range(5f, ChunkLength - 5f), wind);
            }
            if (Random.value < 0.3f)
                DesertLife.SpawnVultures(c, side * Random.Range(8f, 26f), Random.Range(20f, 32f), s0 + Random.Range(10f, ChunkLength - 10f));
            if (feature == Feature.Cave) return;
            if (Random.value < 0.14f)
                DesertLife.SpawnCaravan(transform, side * Random.Range(19f, 26f), s0 + Random.Range(10f, 30f), Random.value < 0.5f ? 1f : -1f, Random.Range(3, 6));
            else if (Random.value < 0.16f)
                DesertLife.SpawnOstriches(transform, side * Random.Range(11f, 17f), s0 + Random.Range(20f, 40f), Random.Range(2, 4));
        }

        // ---------------- Ice Age ----------------

        /// <summary>
        /// Frozen taiga: tundra grass, snowdrifts, junipers and ice by the trail; spruce and bare
        /// birch further out; a dense wall of big snowy spruce behind. Rocky passes become ice boulders.
        /// </summary>
        void IceScenery(ChunkBuilder b, int s, float s0, bool icePass)
        {
            // Trail edge
            for (float z = Random.Range(0f, 1.2f); z < ChunkLength; z += Random.Range(1.1f, 2.2f))
            {
                if (Blocked(z)) continue;
                int v = Random.Range(0, 4);
                float yaw = Random.Range(0f, 360f);
                float r = Random.value;
                if (r < 0.4f) b.Add(Flora.TundraTuft(v), OnGround(s * Random.Range(4.5f, 6.2f), z, s0, 0.02f), yaw, Random.Range(0.9f, 1.5f));
                else if (r < 0.62f) b.Add(Flora.Snowdrift(v), OnGround(s * Random.Range(5.6f, 7.5f), z, s0, 0.1f), yaw, Random.Range(0.8f, 1.3f));
                else if (r < 0.82f) b.Add(Flora.Juniper(v), OnGround(s * Random.Range(5.8f, 7.6f), z, s0, 0.05f), yaw, Random.Range(0.8f, 1.2f));
                else if (r < 0.93f) b.Add(Flora.IceBoulder(v), OnGround(s * Random.Range(5.6f, 7.2f), z, s0, 0.15f), yaw, Random.Range(0.5f, 1.1f));
                else b.Add(Flora.IceCrystals(v), OnGround(s * Random.Range(5.4f, 6.8f), z, s0, 0f), yaw, Random.Range(0.8f, 1.3f));
            }

            // Middle distance: spruce and bare birch (or huge ice boulders in a pass)
            for (float z = Random.Range(0f, 3f); z < ChunkLength; z += Random.Range(4f, 7f))
            {
                if (Blocked(z)) continue;
                float x = s * Random.Range(8f, 12.5f);
                int v = Random.Range(0, 4);
                if (icePass)
                {
                    float scale = Random.Range(2.5f, 5.5f);
                    b.Add(Flora.IceBoulder(v), OnGround(x, z, s0, scale * 0.25f), Random.Range(0f, 360f), scale);
                }
                else if (Random.value < 0.62f) b.Add(Flora.Pine(v), OnGround(x, z, s0, 0.1f), Random.Range(0f, 360f), Random.Range(0.8f, 1.15f));
                else b.Add(Flora.Birch(v), OnGround(x, z, s0, 0.05f), Random.Range(0f, 360f), Random.Range(0.85f, 1.15f));
                if (Random.value < 0.6f)
                    b.Add(Flora.Snowdrift(v), OnGround(x + Random.Range(-1.5f, 1.5f), z + Random.Range(-1.5f, 1.5f), s0, 0.1f),
                          Random.Range(0f, 360f), Random.Range(1f, 1.6f));
            }

            // Dense spruce wall
            for (float z = Random.Range(0f, 2f); z < ChunkLength; z += Random.Range(3.2f, 5.2f))
                if (!Blocked(z))
                    b.Add(Flora.Pine(Random.Range(0, 4)), OnGround(s * Random.Range(14f, 20f), z, s0, 0.15f),
                          Random.Range(0f, 360f), Random.Range(1.1f, 1.5f));
            for (float z = Random.Range(0f, 3f); z < ChunkLength; z += Random.Range(4.5f, 7f))
                if (!Blocked(z))
                    b.Add(Flora.Pine(Random.Range(0, 4)), OnGround(s * Random.Range(22f, 34f), z, s0, 0.15f),
                          Random.Range(0f, 360f), Random.Range(1.4f, 1.9f));
        }

        // ---------------- Water world ----------------

        /// <summary>Palm groves on sandbars and islands, coral rocks breaking the surface near the boardwalk.</summary>
        void WaterScenery(ChunkBuilder b, int s, float s0)
        {
            for (float z = Random.Range(0f, 3f); z < ChunkLength; z += Random.Range(3f, 6f))
            {
                var p = OnGround(s * Random.Range(9f, 30f), z, s0, 0.1f);
                if (!AboveWater(p)) continue;
                int v = Random.Range(0, 4);
                float r = Random.value;
                if (r < 0.55f)
                {
                    float yaw = (s < 0 ? 0f : 180f) + Random.Range(-60f, 60f);
                    b.Add(Flora.Palm(v), p, yaw, Random.Range(0.85f, 1.2f));
                }
                else if (r < 0.8f) b.Add(Flora.Bush(v), p, Random.Range(0f, 360f), Random.Range(0.9f, 1.4f));
                else b.Add(Flora.Tuft(v), p, Random.Range(0f, 360f), Random.Range(1f, 1.6f));
            }
            // Coral and limestone rocks in the shallows beside the boardwalk
            for (float z = Random.Range(0f, 6f); z < ChunkLength; z += Random.Range(6f, 12f))
            {
                var p = new Vector3(s * Random.Range(6.5f, 12f), SeaLevel - Random.Range(0.2f, 0.5f), z);
                b.Add(Flora.Rock(Random.Range(0, 4)), p, Random.Range(0f, 360f), Random.Range(0.8f, 2.2f));
            }
        }


        /// <summary>True when the sea floor around (x, z) lies well below the surface: no sandbar or island there.</summary>
        bool OpenWater(float x, float z, float s0, float radius)
        {
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    float lz = z + j * radius;
                    if (lz < 0f || lz > ChunkLength) return false;
                    if (OnGround(x + i * radius, lz, s0, 0f).y > SeaLevel - 0.5f) return false;
                }
            return true;
        }

        /// <summary>Life in the open sea either side of the boardwalk, so the water never feels empty.</summary>
        void SeaAnimals(Transform c, float s0, int skipSide)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                if (s == skipSide) continue;
                float yawAlong = Random.value < 0.5f ? 0f : 180f;

                // Fish jumping in the shallows near the boardwalk (almost always)
                for (int tries = 0; tries < 3; tries++)
                {
                    float x = s * Random.Range(9f, 15f), z = Random.Range(8f, ChunkLength - 8f);
                    if (OpenWater(x, z, s0, 3f)) { SeaLife.SpawnFishSchool(c, x, SeaLevel, s0 + z, 3f); break; }
                }
                // A dolphin pod porpoising alongside the trail
                if (Random.value < 0.55f)
                    for (int tries = 0; tries < 4; tries++)
                    {
                        float x = s * Random.Range(13f, 22f), z = Random.Range(14f, ChunkLength - 14f);
                        if (OpenWater(x, z, s0, 9f)) { SeaLife.SpawnDolphins(c, x, SeaLevel, s0 + z, yawAlong, Random.Range(2, 5)); break; }
                    }
                // A sea turtle paddling at the surface
                if (Random.value < 0.45f)
                    for (int tries = 0; tries < 4; tries++)
                    {
                        float x = s * Random.Range(10f, 18f), z = Random.Range(6f, ChunkLength - 6f);
                        if (OpenWater(x, z, s0, 4.5f)) { SeaLife.SpawnTurtle(c, x, SeaLevel, s0 + z, Random.Range(0f, 360f)); break; }
                    }
                // A shark fin circling
                if (Random.value < 0.3f)
                    for (int tries = 0; tries < 4; tries++)
                    {
                        float x = s * Random.Range(14f, 24f), z = Random.Range(10f, ChunkLength - 10f);
                        float r = Random.Range(3.5f, 6f);
                        if (OpenWater(x, z, s0, r + 1.5f)) { SeaLife.SpawnShark(c, x, SeaLevel, s0 + z, r); break; }
                    }
                // A whale surfacing far out
                if (Random.value < 0.22f)
                    for (int tries = 0; tries < 3; tries++)
                    {
                        float x = s * Random.Range(27f, 36f), z = Random.Range(18f, ChunkLength - 18f);
                        if (OpenWater(x, z, s0, 8f)) { SeaLife.SpawnWhale(c, x, SeaLevel, s0 + z, yawAlong + Random.Range(-20f, 20f)); break; }
                    }
            }
        }

        /// <summary>The sea around the boardwalk: water to the horizon, pilings and edge boards.</summary>
        void BuildSea(ChunkBuilder b, Transform c, float s0, OwnedMeshes owned, int skipSide, Vector2 gap)
        {
            var sea = WaterMesh(s0, -1f, ChunkLength + 1f, SeaLevel);
            TrackPath.Deform(sea, s0);
            owned.Meshes.Add(sea);
            Prim.Shape(sea, "Sea", c, Vector3.zero, Vector3.one, Biomes.Remap(Mats.Water, BiomeType.WaterWorld), null, false);

            bool hasGap = gap.x > 0f;
            for (int s = -1; s <= 1; s += 2)
            {
                // Edge board along the boardwalk (in two pieces where the boardwalk is broken)
                if (!hasGap) Beam(b, s, 0f, ChunkLength);
                else { Beam(b, s, 0f, gap.x); Beam(b, s, gap.y, ChunkLength); }
                if (s == skipSide) continue;
                // Pilings driven into the sea floor
                for (float z = 1.5f; z < ChunkLength; z += 3.5f)
                    if (!hasGap || z < gap.x - 0.5f || z > gap.y + 0.5f)
                        b.Add(stiltMesh, Mats.Bark, Matrix4x4.TRS(new Vector3(s * 5.1f, 0.25f, z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one));
            }
            if (!hasGap) return;

            // The break: splintered plank ends sticking out over the water, a few broken stumps of piling,
            // and a mooring post at each end of the gap.
            for (int end = 0; end < 2; end++)   // (only where the boardwalk actually breaks off within this chunk)
            {
                float z = end == 0 ? gap.x : gap.y, dir = end == 0 ? 1f : -1f;
                if (z <= 0.01f || z >= ChunkLength - 0.01f) continue;
                for (int i = 0; i < 6; i++)
                {
                    float x = Mathf.Lerp(-4.2f, 4.2f, i / 5f) + Random.Range(-0.3f, 0.3f);
                    float len = Random.Range(0.3f, 1.1f);
                    b.Add(plankMesh, Mats.Planks, Matrix4x4.TRS(new Vector3(x, -0.06f - Random.Range(0f, 0.05f), z + dir * len * 0.5f),
                          Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(-12f, 12f) + 90f, Random.Range(-4f, 4f)), new Vector3(len / 8.6f, 1f, Random.Range(0.6f, 1f))));   // plankMesh is 8.6 m long
                }
                for (int s = -1; s <= 1; s += 2)
                    b.Add(stiltMesh, Mats.Bark, Matrix4x4.TRS(new Vector3(s * 4.6f, 1.3f, z - dir * 0.3f), Quaternion.identity, new Vector3(1.3f, 1f, 1.3f)));
            }
            for (int i = 0; i < 5; i++)
            {
                float z = Random.Range(gap.x + 2f, gap.y - 2f);
                float s = Random.value < 0.5f ? -1f : 1f;
                b.Add(stiltMesh, Mats.Bark, Matrix4x4.TRS(new Vector3(s * Random.Range(4.5f, 5.5f), Random.Range(-1.6f, -1.1f), z),
                      Quaternion.Euler(Random.Range(-15f, 15f), 0f, Random.Range(-15f, 15f)), Vector3.one));
            }
        }

        void Beam(ChunkBuilder b, int side, float z0, float z1)
        {
            if (z1 - z0 < 0.2f) return;
            b.Add(beamMesh, Mats.Planks, Matrix4x4.TRS(new Vector3(side * 5.05f, -0.12f, (z0 + z1) * 0.5f), Quaternion.identity, new Vector3(1f, 0.8f, z1 - z0)));
        }

        // ---------------- River crossing ----------------

        /// <summary>A river cutting across the trail, crossed on a plank bridge with rope rails.</summary>
        void BuildRiver(ChunkBuilder b, Transform c, float s0, OwnedMeshes owned)
        {
            // Water surface spanning the whole valley
            var water = WaterMesh(s0, RiverA - 4f, RiverB + 4f);
            TrackPath.Deform(water, s0);
            owned.Meshes.Add(water);
            Prim.Shape(water, "River", c, Vector3.zero, Vector3.one, Biomes.Remap(Mats.Water, chunkBiome), null, false);

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
            if (Random.value < 0.6f && chunkBiome == BiomeType.Jungle)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                Animals.SpawnCrocodile(c, side * Random.Range(9f, 24f), WaterLevel - 0.12f,
                                       s0 + (RiverA + RiverB) * 0.5f + Random.Range(-2f, 2f), side * 90f + Random.Range(-30f, 30f));
            }
        }

        static Mesh WaterMesh(float s0, float z0, float z1, float level = WaterLevel)
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
                    verts.Add(new Vector3(x, level, z));
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
            Prim.Shape(cave, "Cave", c, Vector3.zero, Vector3.one, Biomes.Remap(Mats.CaveRock, chunkBiome), null, true);

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
                AddLight(c, CrystalLight(chunkBiome), 2.2f, 8f, side * 5.6f, 1.0f, s0 + z, false);
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

        static Color CrystalLight(BiomeType b)
        {
            switch (b)
            {
                case BiomeType.WaterWorld: return new Color(1f, 0.5f, 0.15f);
                case BiomeType.Desert: return new Color(1f, 0.7f, 0.35f);
                default: return new Color(0.35f, 0.9f, 1f);
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

        static Mesh TrailMesh(float s0, bool riverGap, float gapA, float gapB)
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
                if (mid > gapA && mid < gapB) continue;                // a ravine or broken boardwalk
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
