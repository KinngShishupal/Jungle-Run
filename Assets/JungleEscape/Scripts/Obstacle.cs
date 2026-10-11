using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    public enum ObstacleType
    {
        Log,      // low: jump over
        Branch,   // high: roll under
        Boulder,  // tall: change lane
        Boar      // charges toward you: jump over or dodge
    }

    public class Obstacle : MonoBehaviour
    {
        const float BoarSpeed = 6f;
        const float BoarWakeDistance = 40f;

        public ObstacleType Type;
        public int Lane;
        public bool Handled;

        bool charging;
        Transform body;
        Transform[] legs;

        void Update()
        {
            if (Type != ObstacleType.Boar || Handled) return;
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;

            float gap = transform.position.z - gm.Player.transform.position.z;
            if (!charging && gap < BoarWakeDistance && gap > 0f) charging = true;
            if (!charging) return;

            transform.position += Vector3.back * (BoarSpeed * Time.deltaTime);

            // Gallop: diagonal leg pairs swing together, body bobs and pitches.
            float t = Time.time * 16f;
            for (int i = 0; i < legs.Length; i++)
            {
                float phase = (i == 0 || i == 3) ? 0f : Mathf.PI;
                legs[i].localRotation = Quaternion.Euler(Mathf.Sin(t + phase) * 38f, 0f, 0f);
            }
            body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(t)) * 0.08f, 0f);
            body.localRotation = Quaternion.Euler(Mathf.Sin(t * 2f) * 2.5f, 0f, 0f);
        }

        public void Smash()
        {
            Handled = true;
            if (gameObject.activeSelf)
            {
                Color c = Type == ObstacleType.Boulder ? new Color(0.45f, 0.43f, 0.40f)
                        : Type == ObstacleType.Branch ? new Color(0.25f, 0.38f, 0.12f)
                        : new Color(0.40f, 0.28f, 0.18f);
                Fx.Debris(transform.position + Vector3.up * 0.8f, c);
            }
            gameObject.SetActive(false);
        }

        // ---------------- Factory ----------------

        public static Obstacle Create(ObstacleType type, int lane, float z, Transform parent)
        {
            var go = new GameObject(type.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(lane * PlayerController.LaneWidth, 0f, z);

            var ob = go.AddComponent<Obstacle>();
            ob.Type = type;
            ob.Lane = lane;
            var col = go.AddComponent<BoxCollider>();
            var t = go.transform;
            int variant = Random.Range(0, 3);

            switch (type)
            {
                case ObstacleType.Log:
                    col.center = new Vector3(0f, 0.4f, 0f);
                    col.size = new Vector3(2.0f, 0.8f, 0.7f);
                    BuildLog(t, variant);
                    break;

                case ObstacleType.Branch:
                    // Solid from 1.1 m up: standing or jumping hits it, rolling passes under.
                    col.center = new Vector3(0f, 2.3f, 0f);
                    col.size = new Vector3(2.2f, 2.4f, 0.5f);
                    BuildBranch(t, variant);
                    break;

                case ObstacleType.Boulder:
                    col.center = new Vector3(0f, 1.5f, 0f);
                    col.size = new Vector3(2.2f, 3.0f, 1.6f);
                    BuildBoulder(t, variant);
                    break;

                case ObstacleType.Boar:
                    col.center = new Vector3(0f, 0.55f, 0f);
                    col.size = new Vector3(1.2f, 1.0f, 1.7f);
                    var rb = go.AddComponent<Rigidbody>(); // moving collider
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    BuildBoar(ob, t);
                    break;
            }
            return ob;
        }

        // ---------------- Visuals ----------------

        static Mesh[] logMeshes, branchMeshes, vineMeshes;
        static Vector3[] logEndL, logEndR;
        static Mesh drapeLeaf;
        static Mesh disc, ball, tusk, hoofLeg, tail, snout;

        static void BuildLog(Transform t, int v)
        {
            if (logMeshes == null)
            {
                logMeshes = new Mesh[3];
                logEndL = new Vector3[3];
                logEndR = new Vector3[3];
                for (int i = 0; i < 3; i++)
                {
                    float seed = i * 2.3f;
                    var path = new List<Vector3>();
                    for (int k = 0; k <= 6; k++)
                    {
                        float s = k / 6f;
                        path.Add(new Vector3(Mathf.Lerp(-1.08f, 1.08f, s), Mathf.Sin(s * 3f + seed) * 0.03f, Mathf.Sin(s * 2.4f + seed) * 0.06f));
                    }
                    logMeshes[i] = Meshes.Sweep(path, (s, a) => 0.39f * (1f - 0.08f * s) * (1f + 0.05f * Mathf.Sin(s * 11f + a * 3f + seed)),
                                                14, 3f, 0.8f);
                    logEndL[i] = path[0];
                    logEndR[i] = path[path.Count - 1];
                }
                disc = Meshes.Disc(20);
            }
            float rot = Random.Range(-6f, 6f);
            var root = Prim.Group("LogVisual", t, new Vector3(0f, 0.4f, 0f));
            root.localRotation = Quaternion.Euler(0f, rot, 0f);
            Prim.Shape(logMeshes[v], "Log", root, Vector3.zero, Vector3.one, Mats.Bark);
            // Sawn end grain on both ends
            Prim.Shape(disc, "EndR", root, logEndR[v], Vector3.one * 0.76f, Mats.WoodEnd, new Vector3(0f, 0f, -90f));
            Prim.Shape(disc, "EndL", root, logEndL[v], Vector3.one * 0.82f, Mats.WoodEnd, new Vector3(0f, 0f, 90f));
            // Moss cushion and a little fern growing on top
            Prim.Shape(Flora.Bush(v).Parts[0].Mesh, "Moss", root, new Vector3(Random.Range(-0.5f, 0.5f), 0.36f, 0f),
                       new Vector3(0.8f, 0.22f, 0.5f), Mats.Canopy);
            var fern = new Model();
            Flora.AddFrondRing(fern, Mats.Frond, Vector3.zero, 5, 0.55f, 0.28f, 45f, 0.3f, Random.value * 360f);
            fern.Spawn("Fern", root, new Vector3(Random.Range(-0.7f, 0.7f), 0.33f, -0.05f), Quaternion.identity, Vector3.one);
        }

        static void BuildBranch(Transform t, int v)
        {
            if (branchMeshes == null)
            {
                branchMeshes = new Mesh[3];
                vineMeshes = new Mesh[3];
                for (int i = 0; i < 3; i++)
                {
                    float seed = i * 1.7f;
                    var path = new List<Vector3>();
                    for (int k = 0; k <= 7; k++)
                    {
                        float s = k / 7f;
                        float x = Mathf.Lerp(-1.25f, 1.25f, s);
                        path.Add(new Vector3(x, 1.5f + 0.25f * x * x + Mathf.Sin(s * 5f + seed) * 0.05f, Mathf.Sin(s * 3f + seed) * 0.12f));
                    }
                    branchMeshes[i] = Meshes.Sweep(path, (s, a) => Mathf.Lerp(0.2f, 0.14f, s) * (1f + 0.08f * Mathf.Sin(s * 13f + a * 2f)), 10, 2f, 1f);

                    var vine = new List<Vector3>();
                    for (int k = 0; k <= 6; k++)
                        vine.Add(new Vector3(Mathf.Sin(k * 0.7f + seed) * 0.06f, k * 0.8f, Mathf.Cos(k * 0.5f) * 0.05f));
                    vineMeshes[i] = Meshes.Sweep(vine, (s, a) => 0.045f, 6, 1f, 2f);
                }
            }
            Prim.Shape(branchMeshes[v], "Bough", t, Vector3.zero, Vector3.one, Mats.Bark);

            // Lianas holding the bough up from the canopy
            Prim.Shape(vineMeshes[v], "VineL", t, new Vector3(-1.05f, 1.75f, 0f), Vector3.one, Mats.Vine);
            Prim.Shape(vineMeshes[(v + 1) % 3], "VineR", t, new Vector3(1.05f, 1.75f, 0f), Vector3.one, Mats.Vine);

            // Leafy mass on top of the bough
            for (int i = 0; i < 3; i++)
            {
                float s = Random.Range(1.1f, 1.5f);
                Prim.Shape(Flora.Bush(v + i).Parts[0].Mesh, "Leaves", t,
                           new Vector3((i - 1) * 0.75f, 2.25f + Random.Range(-0.1f, 0.25f), Random.Range(-0.1f, 0.1f)),
                           new Vector3(s, s * 0.85f, s * 0.8f), Mats.Canopy, new Vector3(0f, Random.Range(0f, 360f), 0f));
            }

            // Leaves drooping off the front edge (stay above the 1.1 m roll gap)
            if (drapeLeaf == null) drapeLeaf = Meshes.Leaf(0.42f, 0.3f, 0.1f, 0.03f);
            var drape = new Model();
            for (int i = 0; i < 6; i++)
            {
                float x = Mathf.Lerp(-1.0f, 1.0f, i / 5f) + Random.Range(-0.1f, 0.1f);
                drape.Add(drapeLeaf, Mats.BroadLeaf, new Vector3(x, 1.62f + 0.25f * x * x, -0.15f),
                          Quaternion.Euler(Random.Range(50f, 65f), 180f + Random.Range(-25f, 25f), 0f), Vector3.one * Random.Range(0.85f, 1.1f));
            }
            drape.Spawn("Drape", t, Vector3.zero, Quaternion.identity, Vector3.one);
        }

        static void BuildBoulder(Transform t, int v)
        {
            var rock = Flora.Rock(v).Parts[0].Mesh;
            Prim.Shape(rock, "Rock", t, new Vector3(0f, 0.86f, 0f), new Vector3(2.4f, 3.0f, 2.0f), Mats.Rock,
                       new Vector3(0f, Random.Range(0f, 360f), 0f));
            Prim.Shape(Flora.Rock(v + 1).Parts[0].Mesh, "Rock2", t, new Vector3(0.75f, 0.25f, -0.55f), new Vector3(1.0f, 0.9f, 0.9f), Mats.Rock,
                       new Vector3(0f, Random.Range(0f, 360f), 0f));
            // Undergrowth hugging the base
            var plants = Flora.Fern(v).Spawn("Fern", t, new Vector3(-0.9f, 0f, -0.6f), Quaternion.identity, Vector3.one * 0.8f);
            plants.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Flora.Tuft(v).Spawn("Grass", t, new Vector3(0.3f, 0f, -0.95f), Quaternion.identity, Vector3.one);
        }

        static void BuildBoar(Obstacle ob, Transform t)
        {
            if (ball == null)
            {
                ball = Meshes.Ball(12, 18);
                hoofLeg = Meshes.Limb(0.42f, 0.085f, 0.05f, 8, 7);
                var tp = new List<Vector3>();
                for (int k = 0; k <= 6; k++)
                {
                    float s = k / 6f;
                    tp.Add(new Vector3(0f, s * 0.2f, -s * 0.06f + s * s * 0.1f));
                }
                tusk = Meshes.Sweep(tp, (s, a) => 0.028f * (1f - s * 0.85f), 6, 1f, 1f, true);
                var tl = new List<Vector3>();
                for (int k = 0; k <= 5; k++) tl.Add(new Vector3(Mathf.Sin(k) * 0.03f, -k * 0.06f, k * 0.03f));
                tail = Meshes.Sweep(tl, (s, a) => 0.025f * (1f - s * 0.7f), 5, 1f, 1f);
                snout = Meshes.Lathe(new[] { new Vector2(0.001f, 0f), new Vector2(0.13f, 0f), new Vector2(0.14f, 0.05f), new Vector2(0.11f, 0.09f), new Vector2(0.001f, 0.095f) }, 14);
            }

            Material fur = Mats.Fur(new Color(0.30f, 0.23f, 0.18f));
            Material furDark = Mats.Fur(new Color(0.14f, 0.11f, 0.09f));
            Material skin = Mats.Skin(new Color(0.48f, 0.36f, 0.33f));
            Material ivory = Mats.Plain(new Color(0.92f, 0.87f, 0.74f), 0.55f);
            Material eye = Mats.Plain(new Color(0.05f, 0.03f, 0.02f), 0.95f);

            // Faces -Z (toward the player)
            ob.body = Prim.Group("Body", t, Vector3.zero);
            var b = ob.body;
            Prim.Shape(ball, "Torso", b, new Vector3(0f, 0.72f, 0.05f), new Vector3(0.82f, 0.78f, 1.5f), fur);
            Prim.Shape(ball, "Shoulders", b, new Vector3(0f, 0.8f, -0.45f), new Vector3(0.86f, 0.86f, 0.8f), fur);
            Prim.Shape(ball, "Mane", b, new Vector3(0f, 1.1f, -0.15f), new Vector3(0.18f, 0.22f, 1.25f), furDark);
            Prim.Shape(ball, "Head", b, new Vector3(0f, 0.66f, -0.95f), new Vector3(0.5f, 0.52f, 0.72f), fur, new Vector3(-18f, 0f, 0f));
            Prim.Shape(snout, "Snout", b, new Vector3(0f, 0.5f, -1.3f), new Vector3(1f, 1f, 0.8f), skin, new Vector3(-108f, 0f, 0f));
            Prim.Shape(tusk, "TuskL", b, new Vector3(-0.12f, 0.47f, -1.2f), Vector3.one, ivory, new Vector3(0f, 0f, 20f));
            Prim.Shape(tusk, "TuskR", b, new Vector3(0.12f, 0.47f, -1.2f), Vector3.one, ivory, new Vector3(0f, 0f, -20f));
            Prim.Shape(ball, "EyeL", b, new Vector3(-0.17f, 0.8f, -1.05f), Vector3.one * 0.07f, eye);
            Prim.Shape(ball, "EyeR", b, new Vector3(0.17f, 0.8f, -1.05f), Vector3.one * 0.07f, eye);
            Prim.Shape(ball, "EarL", b, new Vector3(-0.2f, 0.98f, -0.78f), new Vector3(0.14f, 0.22f, 0.06f), furDark, new Vector3(0f, 0f, 25f));
            Prim.Shape(ball, "EarR", b, new Vector3(0.2f, 0.98f, -0.78f), new Vector3(0.14f, 0.22f, 0.06f), furDark, new Vector3(0f, 0f, -25f));
            Prim.Shape(tail, "Tail", b, new Vector3(0f, 0.85f, 0.8f), Vector3.one, furDark);

            Vector3[] hips = { new Vector3(-0.24f, 0.45f, -0.5f), new Vector3(0.24f, 0.45f, -0.5f),
                               new Vector3(-0.24f, 0.45f, 0.5f), new Vector3(0.24f, 0.45f, 0.5f) };
            ob.legs = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var leg = Prim.Group("Leg" + i, b, hips[i]);
                Prim.Shape(hoofLeg, "Leg", leg, Vector3.zero, Vector3.one, fur);
                Prim.Shape(ball, "Hoof", leg, new Vector3(0f, -0.42f, -0.01f), new Vector3(0.1f, 0.07f, 0.12f), eye);
                ob.legs[i] = leg;
            }
        }
    }
}
