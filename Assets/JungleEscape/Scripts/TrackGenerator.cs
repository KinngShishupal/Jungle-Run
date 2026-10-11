using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// Builds the endless jungle trail in 50 m chunks ahead of the player and
    /// removes chunks behind. Each chunk gets rows of obstacles, banana trails
    /// and the occasional power-up.
    /// </summary>
    public class TrackGenerator : MonoBehaviour
    {
        const float ChunkLength = 50f;
        const int ChunksAhead = 3;
        const float SafeStart = 45f;       // obstacle-free run-up at the start
        const float PowerUpChance = 0.07f; // per row

        // Lane contents for a row
        const int Empty = -1;

        readonly List<GameObject> chunks = new List<GameObject>();
        readonly int[] lastRow = { Empty, Empty, Empty };
        Transform player;
        float spawnZ;
        float nextRowZ;

        Material shaftMat;
        Mesh shaftMesh;
        readonly Model scratch = new Model();

        public void Init(Transform target)
        {
            player = target;
            shaftMat = new Material(Mats.ParticleAdd(Tex.Shaft)) { name = "SunShaft" };
            shaftMat.SetColor("_BaseColor", new Color(1f, 0.86f, 0.62f, 0.09f));
            shaftMesh = Meshes.Billboard(1f, 1f);
        }

        public void ResetTrack()
        {
            foreach (var c in chunks) if (c != null) Destroy(c);
            chunks.Clear();
            spawnZ = -20f;
            nextRowZ = SafeStart;
            for (int i = 0; i < 3; i++) lastRow[i] = Empty;
            while (spawnZ < ChunkLength * ChunksAhead) SpawnChunk();
        }

        void Update()
        {
            if (player == null) return;
            float pz = player.position.z;
            while (spawnZ < pz + ChunkLength * ChunksAhead) SpawnChunk();
            // Generous margin: banana trails can extend a little past their chunk's end.
            while (chunks.Count > 0 && chunks[0].transform.position.z + ChunkLength < pz - 35f)
            {
                Destroy(chunks[0]);
                chunks.RemoveAt(0);
            }
        }

        /// <summary>Removes obstacles just ahead of the player (used on revive).</summary>
        public void ClearAhead(float fromZ, float length)
        {
            foreach (var c in chunks)
                foreach (var ob in c.GetComponentsInChildren<Obstacle>())
                {
                    float z = ob.transform.position.z;
                    if (z > fromZ - 3f && z < fromZ + length) ob.Smash();
                }
        }

        // ---------------- Generation ----------------

        void SpawnChunk()
        {
            var chunk = new GameObject("Chunk");
            chunk.transform.SetParent(transform, false);
            chunk.transform.position = new Vector3(0f, 0f, spawnZ);
            BuildScenery(chunk.transform);

            float end = spawnZ + ChunkLength;
            while (nextRowZ < end)
            {
                BuildRow(chunk.transform, nextRowZ);
                nextRowZ += RowGap();
            }

            chunks.Add(chunk);
            spawnZ = end;
        }

        float Difficulty => GameManager.I != null ? GameManager.I.Difficulty : 0f;

        float RowGap() => Mathf.Lerp(22f, 14f, Difficulty) + Random.Range(-1f, 3f);

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
                // A charging boar would run through whatever was in its lane in the previous row.
                if (row[i] == (int)ObstacleType.Boar && lastRow[i] != Empty) row[i] = (int)ObstacleType.Log;
            }

            // Always leave at least one lane you can get through without changing lane
            // (empty, log = jump, branch = roll).
            bool passable = false;
            for (int i = 0; i < 3; i++)
                if (row[i] == Empty || row[i] == (int)ObstacleType.Log || row[i] == (int)ObstacleType.Branch) passable = true;
            if (!passable) row[order[0]] = Random.value < 0.5f ? Empty : (int)ObstacleType.Log;

            for (int i = 0; i < 3; i++)
                if (row[i] != Empty) Obstacle.Create((ObstacleType)row[i], i - 1, z, parent);

            PlaceRewards(parent, row, z);
            System.Array.Copy(row, lastRow, 3);
        }

        void PlaceRewards(Transform parent, int[] row, float z)
        {
            var empties = new List<int>();
            var logs = new List<int>();
            var branches = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                if (row[i] == Empty) empties.Add(i);
                else if (row[i] == (int)ObstacleType.Log) logs.Add(i);
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
            if (logs.Count > 0 && r < 0.3f)
            {
                // Arc of bananas over a log: rewards the jump
                int lane = logs[Random.Range(0, logs.Count)] - 1;
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
            float boarWeight = d > 0.2f ? 12f : 0f;
            float total = 30f + 25f + 33f + boarWeight;
            float r = Random.value * total;
            if ((r -= 30f) < 0f) return ObstacleType.Log;
            if ((r -= 25f) < 0f) return ObstacleType.Branch;
            if ((r -= 33f) < 0f) return ObstacleType.Boulder;
            return ObstacleType.Boar;
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

        /// <summary>Height of the forest floor at a world position (0 at the trail edge, rising into banks).</summary>
        public static float GroundHeight(float x, float worldZ)
        {
            float ax = Mathf.Abs(x);
            float bank = Tex.SS(5f, 14f, ax) * 1.0f + Tex.SS(14f, 42f, ax) * 4f;
            float n = (Noise.World(x, worldZ, 0.12f, 3, 77) - 0.5f) * 1.8f * Tex.SS(5.5f, 10f, ax);
            return bank + n;
        }

        void BuildScenery(Transform c)
        {
            float z0 = c.position.z;
            var owned = c.gameObject.AddComponent<OwnedMeshes>();

            var trail = TrailMesh(z0);
            owned.Meshes.Add(trail);
            Prim.Shape(trail, "Trail", c, Vector3.zero, Vector3.one, Mats.Trail, null, false);
            for (int s = -1; s <= 1; s += 2)
            {
                var ground = GroundMesh(z0, s);
                owned.Meshes.Add(ground);
                Prim.Shape(ground, "Ground", c, Vector3.zero, Vector3.one, Mats.Ground, null, false);
            }

            var b = new ChunkBuilder();
            for (int s = -1; s <= 1; s += 2)
            {
                EdgePlants(b, s, z0);
                MidForest(b, s, z0);
                JungleWall(b, s, z0);
            }
            if (Random.value < 0.55f) Liana(b, Random.Range(5f, 45f));
            int shafts = Random.Range(1, 4);
            for (int i = 0; i < shafts; i++) SunShaft(b, Random.Range(0f, ChunkLength));
            b.Build(c);
        }

        static Vector3 OnGround(float x, float localZ, float z0, float sink = 0.05f) =>
            new Vector3(x, GroundHeight(x, z0 + localZ) - sink, localZ);

        /// <summary>Ferns, grass, broad-leaf plants and stones lining the trail.</summary>
        static void EdgePlants(ChunkBuilder b, int s, float z0)
        {
            for (float z = Random.Range(0f, 1.2f); z < ChunkLength; z += Random.Range(0.8f, 1.6f))
            {
                float r = Random.value;
                int v = Random.Range(0, 4);
                float yaw = Random.Range(0f, 360f);
                if (r < 0.40f)
                    b.Add(Flora.Tuft(v), OnGround(s * Random.Range(4.5f, 5.6f), z, z0, 0.02f), yaw, Random.Range(0.8f, 1.35f));
                else if (r < 0.68f)
                    b.Add(Flora.Fern(v), OnGround(s * Random.Range(5.3f, 6.8f), z, z0), yaw, Random.Range(0.8f, 1.25f));
                else if (r < 0.88f)
                    b.Add(Flora.BroadPlant(v), OnGround(s * Random.Range(5.6f, 7.4f), z, z0), yaw, Random.Range(0.9f, 1.5f));
                else if (r < 0.95f)
                    b.Add(Flora.Rock(v), OnGround(s * Random.Range(5.2f, 6.6f), z, z0, 0.12f), yaw, Random.Range(0.35f, 0.8f));
                else
                    b.Add(Flora.Bush(v), OnGround(s * Random.Range(6.2f, 7.8f), z, z0), yaw, Random.Range(0.9f, 1.3f));
            }
        }

        /// <summary>Big rainforest trees and leaning palms with undergrowth around their roots.</summary>
        static void MidForest(ChunkBuilder b, int s, float z0)
        {
            for (float z = Random.Range(0f, 4f); z < ChunkLength; z += Random.Range(6f, 9.5f))
            {
                float x = s * Random.Range(7.8f, 12f);
                int v = Random.Range(0, 4);
                if (Random.value < 0.32f)
                {
                    // Palms lean in over the trail (the model leans toward its local +X).
                    float yaw = (s < 0 ? 0f : 180f) + Random.Range(-35f, 35f);
                    b.Add(Flora.Palm(v), OnGround(x, z, z0, 0.1f), yaw, Random.Range(0.9f, 1.15f));
                }
                else
                {
                    b.Add(Flora.Tree(v), OnGround(x, z, z0, 0.15f), Random.Range(0f, 360f), Random.Range(0.85f, 1.2f));
                }

                // Undergrowth around the base
                for (int k = 0; k < 2; k++)
                {
                    float ux = x + Random.Range(-1.8f, 1.8f);
                    float uz = Mathf.Clamp(z + Random.Range(-2f, 2f), 0f, ChunkLength);
                    if (Mathf.Abs(ux) < 6f) continue;
                    var plant = Random.value < 0.5f ? Flora.Fern(Random.Range(0, 4)) : Flora.Bush(Random.Range(0, 4));
                    b.Add(plant, OnGround(ux, uz, z0), Random.Range(0f, 360f), Random.Range(0.9f, 1.4f));
                }
            }

            if (Random.value < 0.6f)
            {
                float z = Random.Range(3f, ChunkLength - 3f);
                float x = s * Random.Range(7f, 11f);
                b.Add(Flora.Rock(Random.Range(0, 4)), OnGround(x, z, z0, 0.3f), Random.Range(0f, 360f), Random.Range(1.4f, 2.6f));
            }
        }

        /// <summary>Dense, darker trees further out that close the jungle in on both sides.</summary>
        static void JungleWall(ChunkBuilder b, int s, float z0)
        {
            for (float z = Random.Range(0f, 3f); z < ChunkLength; z += Random.Range(4.5f, 7f))
                b.Add(Flora.WallTree(Random.Range(0, 4)), OnGround(s * Random.Range(14f, 19f), z, z0, 0.2f),
                      Random.Range(0f, 360f), Random.Range(0.9f, 1.2f));
            for (float z = Random.Range(0f, 4f); z < ChunkLength; z += Random.Range(6f, 9f))
                b.Add(Flora.WallTree(Random.Range(0, 4)), OnGround(s * Random.Range(22f, 32f), z, z0, 0.2f),
                      Random.Range(0f, 360f), Random.Range(1.1f, 1.5f));
        }

        /// <summary>A vine sagging high across the trail (well above glider height).</summary>
        void Liana(ChunkBuilder b, float z)
        {
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

        /// <summary>Soft beam of sunlight slanting down through the canopy, aligned with the sun.</summary>
        void SunShaft(ChunkBuilder b, float z)
        {
            Vector3 f = Atmosphere.Sun != null ? Atmosphere.Sun.transform.forward : new Vector3(-0.3f, -0.6f, 0.7f);
            float angle = Mathf.Atan2(f.x, -f.y) * Mathf.Rad2Deg;
            float w = Random.Range(1.5f, 4f), h = Random.Range(14f, 20f);
            var pos = new Vector3(Random.Range(-9f, 9f), Random.Range(13f, 17f), z);
            b.Add(shaftMesh, shaftMat, Matrix4x4.TRS(pos, Quaternion.Euler(0f, 0f, angle), new Vector3(w, h, 1f)), false);
        }

        // ---------------- Ground meshes ----------------

        static Mesh TrailMesh(float z0)
        {
            const int cols = 11;
            const float step = 2.5f;
            int rows = Mathf.RoundToInt(ChunkLength / step) + 1;
            float zOff = Mathf.Repeat(z0, ZTile);
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
            Grid(tris, rows, cols);
            return MakeMesh("TrailMesh", verts, norms, uvs, tris);
        }

        static readonly float[] GroundCols = { 5f, 5.6f, 6.5f, 7.5f, 9f, 11f, 13.5f, 16.5f, 20f, 24f, 29f, 35f, 42f };

        static Mesh GroundMesh(float z0, int side)
        {
            const float step = 2.5f;
            const float uvScale = 1f / 6f;
            const float e = 0.3f;
            int rows = Mathf.RoundToInt(ChunkLength / step) + 1;
            int cols = GroundCols.Length;
            float zOff = Mathf.Repeat(z0, ZTile);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    // Columns ordered by increasing world X on both sides keeps the winding consistent.
                    float x = side < 0 ? -GroundCols[cols - 1 - j] : GroundCols[j];
                    float z = i * step;
                    float wz = z0 + z;
                    verts.Add(new Vector3(x, GroundHeight(x, wz), z));
                    float dx = (GroundHeight(x + e, wz) - GroundHeight(x - e, wz)) / (2f * e);
                    float dz = (GroundHeight(x, wz + e) - GroundHeight(x, wz - e)) / (2f * e);
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
}
