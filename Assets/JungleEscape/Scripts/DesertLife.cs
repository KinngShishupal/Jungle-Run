using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// Ambient desert life (scenery only, no colliders): tumbleweeds bouncing across the trail on the wind,
    /// vultures circling on thermals, camel caravans plodding along the dunes and ostriches that bolt
    /// alongside the runner.
    /// </summary>
    public static class DesertLife
    {
        static readonly List<GameObject> roaming = new List<GameObject>();
        static Mesh ball;

        static Mesh Ball => ball != null ? ball : (ball = Meshes.Ball(8, 12));

        /// <summary>Removes caravans and ostrich groups (they roam beyond their own chunk).</summary>
        public static void ClearRoaming()
        {
            foreach (var g in roaming) if (g != null) Object.Destroy(g);
            roaming.Clear();
        }

        // ---------------- Tumbleweed ----------------

        public static void SpawnTumbleweed(Transform parent, float x, float s, float wind)
        {
            var root = new GameObject("Tumbleweed").transform;
            root.SetParent(parent, false);
            var visual = Prim.Shape(Flora.TumbleweedMesh, "Weed", root, Vector3.zero, Vector3.one * Random.Range(0.8f, 1.25f), Mats.Straw, null, true);
            visual.transform.localRotation = Random.rotation;
            var tw = root.gameObject.AddComponent<Tumbleweed>();
            tw.Init(TrackPath.Active, x, s, wind, visual.transform);
        }

        // ---------------- Vultures ----------------

        public static void SpawnVultures(Transform parent, float x, float height, float s)
        {
            var root = new GameObject("Vultures").transform;
            root.SetParent(parent, false);
            root.position = TrackPath.Point(s, x, height);
            var k = root.gameObject.AddComponent<VultureKettle>();
            int n = Random.Range(3, 7);
            var feathers = Mats.Fur(new Color(0.18f, 0.13f, 0.1f));
            var ruff = Mats.Fur(new Color(0.82f, 0.76f, 0.66f));
            var headSkin = Mats.Skin(new Color(0.75f, 0.45f, 0.4f));
            var wingBox = Meshes.RoundedBox(new Vector3(1f, 0.04f, 0.5f), 0.02f);
            var finger = Meshes.RoundedBox(new Vector3(0.32f, 0.02f, 0.07f), 0.01f);
            for (int i = 0; i < n; i++)
            {
                var bird = new GameObject("Vulture").transform;
                bird.SetParent(root, false);
                Prim.Shape(Ball, "Body", bird, Vector3.zero, new Vector3(0.36f, 0.3f, 0.8f), feathers, null, false);
                Prim.Shape(Ball, "Ruff", bird, new Vector3(0f, 0.04f, -0.36f), new Vector3(0.28f, 0.22f, 0.2f), ruff, null, false);
                Prim.Shape(Ball, "Head", bird, new Vector3(0f, 0.06f, -0.52f), new Vector3(0.12f, 0.12f, 0.17f), headSkin, null, false);
                Prim.Shape(Ball, "Tail", bird, new Vector3(0f, 0f, 0.5f), new Vector3(0.3f, 0.04f, 0.32f), feathers, null, false);
                var wings = new Transform[2];
                for (int w = 0; w < 2; w++)
                {
                    float sd = w == 0 ? -1f : 1f;
                    var wing = wings[w] = Prim.Group("Wing", bird, new Vector3(sd * 0.15f, 0.05f, -0.05f));
                    Prim.Shape(wingBox, "Inner", wing, new Vector3(sd * 0.55f, 0f, 0f), new Vector3(1.1f, 1f, 1f), feathers, null, false);
                    Prim.Shape(wingBox, "Outer", wing, new Vector3(sd * 1.35f, 0.04f, -0.03f), new Vector3(0.6f, 1f, 0.85f), feathers, null, false);
                    // Splayed primary feathers at the wing tips, the vulture's signature silhouette
                    for (int f = 0; f < 4; f++)
                        Prim.Shape(finger, "Primary", wing, new Vector3(sd * 1.78f, 0.06f, -0.16f + f * 0.1f), Vector3.one, feathers,
                                   new Vector3(0f, sd * (-12f + f * 9f), sd * 6f), false);
                }
                k.Add(bird, wings, Random.Range(6f, 13f), Random.Range(-3f, 3f));
            }
        }

        // ---------------- Camel caravan ----------------

        public static void SpawnCaravan(Transform parent, float x, float s, float dir, int count)
        {
            var root = new GameObject("Caravan");
            root.transform.SetParent(parent, false);
            roaming.Add(root);
            var c = root.AddComponent<Caravan>();
            var path = TrackPath.Active;
            var members = new List<CamelRig>();
            for (int i = 0; i < count; i++)
            {
                var camel = new GameObject("CaravanCamel").transform;
                camel.SetParent(root.transform, false);
                Creatures.BuildCamel(camel);
                var rig = camel.GetComponent<CamelRig>();
                members.Add(rig);
            }
            c.Init(path, x, s, dir, members);
        }

        // ---------------- Ostriches ----------------

        public static void SpawnOstriches(Transform parent, float x, float s, int count)
        {
            var root = new GameObject("Ostriches");
            root.transform.SetParent(parent, false);
            roaming.Add(root);
            var g = root.AddComponent<OstrichGroup>();
            var birds = new List<OstrichRig>();
            for (int i = 0; i < count; i++)
            {
                var o = new GameObject("Ostrich").transform;
                o.SetParent(root.transform, false);
                Creatures.BuildOstrich(o, out _, out _);
                birds.Add(o.GetComponent<OstrichRig>());
            }
            g.Init(TrackPath.Active, x, s, birds);
        }
    }

    /// <summary>
    /// A tumbleweed blown by gusting wind: it rolls without slipping (spin matches ground speed), falls under
    /// gravity, bounces with a loss of energy and is kicked into the air by gusts and bumps.
    /// Simulated in track space (x across, y up, s along) and following the terrain.
    /// </summary>
    public class Tumbleweed : MonoBehaviour
    {
        const float Radius = 0.42f, Gravity = 9.8f, Restitution = 0.42f;
        TrackPath path;
        Transform visual;
        Vector3 pos, vel;     // (x, y above the trail, s)
        float wind, gustT, seed, life;

        public void Init(TrackPath p, float x, float s, float windDir, Transform vis)
        {
            path = p;
            visual = vis;
            wind = windDir;
            seed = Random.value * 100f;
            pos = new Vector3(x, Ground(x, s) + Radius, s);
            vel = new Vector3(windDir * Random.Range(2f, 3.5f), 0f, Random.Range(-0.6f, 0.6f));
            Place();
        }

        static float Ground(float x, float s) => Mathf.Abs(x) < 4.8f ? 0f : TrackGenerator.GroundHeight(x, s);

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            life += dt;
            if (path == null || Mathf.Abs(pos.x) > 42f || life > 40f) { Destroy(gameObject); return; }

            // Gusting wind: a target speed that rises and falls; drag pulls the weed toward it.
            float gust = 3f + 2.2f * Mathf.PerlinNoise(seed, Time.time * 0.35f) + 1.2f * Mathf.Sin(Time.time * 1.7f + seed);
            Vector3 windVel = new Vector3(wind * gust, 0f, 0.4f * Mathf.Sin(Time.time * 0.3f + seed));
            float ground = Ground(pos.x, pos.z) + Radius;
            bool onGround = pos.y <= ground + 0.02f;
            Vector3 dv = windVel - vel;
            dv.y = 0f;
            vel += dv * (onGround ? 1.6f : 0.6f) * dt; // more grip on the ground, less in the air
            vel.y -= Gravity * dt;

            // A strong gust or a bump now and then throws it up into a hop
            if ((gustT -= dt) <= 0f)
            {
                gustT = Random.Range(0.6f, 1.8f);
                if (onGround) vel.y += Random.Range(1.5f, 3.8f);
            }

            pos += vel * dt;
            ground = Ground(pos.x, pos.z) + Radius;
            if (pos.y < ground)
            {
                pos.y = ground;
                if (vel.y < -0.6f) vel.y = -vel.y * Restitution; else vel.y = 0f;
                vel.x *= 0.96f; vel.z *= 0.96f; // a little energy lost to the impact
            }

            // Roll without slipping: angular speed = ground speed / radius, about the axis across the motion.
            var horiz = new Vector3(vel.x, 0f, vel.z);
            float speed = horiz.magnitude;
            if (speed > 0.01f)
            {
                var axis = Vector3.Cross(Vector3.up, horiz / speed);
                visual.Rotate(transform.TransformDirection(axis), speed / Radius * Mathf.Rad2Deg * dt, Space.World);
            }
            Place();
        }

        void Place()
        {
            transform.position = path.PointOn(pos.z, pos.x, pos.y);
            transform.rotation = Quaternion.Euler(0f, path.YawOn(pos.z), 0f);
        }
    }

    /// <summary>Vultures riding a thermal: wide circles at slightly different heights, banking into the turn, rarely flapping.</summary>
    public class VultureKettle : MonoBehaviour
    {
        struct Bird { public Transform T; public Transform[] Wings; public float Radius, Height, Angle, Speed, FlapT; }
        readonly List<Bird> birds = new List<Bird>();
        Vector3 drift;

        public void Add(Transform t, Transform[] wings, float radius, float height)
        {
            birds.Add(new Bird { T = t, Wings = wings, Radius = radius, Height = height, Angle = Random.value * Mathf.PI * 2f,
                                 Speed = Random.Range(7f, 10f), FlapT = Random.Range(2f, 8f) });
            drift = new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            transform.position += drift * dt; // the thermal drifts with the wind
            for (int i = 0; i < birds.Count; i++)
            {
                var b = birds[i];
                b.Angle += b.Speed / b.Radius * dt;
                // The circle tilts slightly, so each bird rises and sinks a little as it goes round.
                var p = new Vector3(Mathf.Cos(b.Angle) * b.Radius, b.Height + Mathf.Sin(b.Angle + i) * 0.8f, Mathf.Sin(b.Angle) * b.Radius);
                var tangent = new Vector3(-Mathf.Sin(b.Angle), 0f, Mathf.Cos(b.Angle));
                float bank = Mathf.Atan(b.Speed * b.Speed / (b.Radius * 9.8f)) * Mathf.Rad2Deg; // coordinated turn
                b.T.localPosition = p;
                b.T.rotation = Quaternion.LookRotation(-tangent, Vector3.up) * Quaternion.Euler(0f, 0f, -bank);

                // Mostly soaring with wings slightly raised (dihedral); a few slow flaps now and then.
                b.FlapT -= dt;
                float flap = 0f;
                if (b.FlapT < 0f)
                {
                    flap = Mathf.Sin(-b.FlapT * 6f) * 28f;
                    if (b.FlapT < -1.05f) b.FlapT = Random.Range(4f, 10f);
                }
                b.Wings[0].localRotation = Quaternion.Euler(0f, 0f, -(6f + flap));
                b.Wings[1].localRotation = Quaternion.Euler(0f, 0f, 6f + flap);
                birds[i] = b;
            }
        }
    }

    /// <summary>A line of camels walking nose to tail along the dunes, each following the trail of the one ahead.</summary>
    public class Caravan : MonoBehaviour
    {
        TrackPath path;
        float x, s, dir, life;
        List<CamelRig> camels;
        const float Speed = 1.5f, Spacing = 3.6f;

        public void Init(TrackPath p, float x0, float s0, float d, List<CamelRig> members)
        {
            path = p; x = x0; s = s0; dir = d; camels = members;
            for (int i = 0; i < camels.Count; i++) camels[i].Walk = Speed * Random.Range(0.95f, 1.05f);
            Place();
        }

        void Update()
        {
            life += Time.deltaTime;
            var gm = GameManager.I;
            if (path == null || life > 120f || (gm != null && gm.Player != null && gm.Player.TrackS - s > 120f)) { Destroy(gameObject); return; }
            s += dir * Speed * Time.deltaTime;
            Place();
        }

        void Place()
        {
            for (int i = 0; i < camels.Count; i++)
            {
                if (camels[i] == null) continue;
                // Each camel walks a little off the line, swaying along a gentle meander.
                float ci = s - dir * i * Spacing;
                float cx = x + Mathf.Sin(ci * 0.08f) * 1.2f;
                float y = TrackGenerator.GroundHeight(cx, ci);
                path.PlaceOn(camels[i].transform, cx, y, ci, dir > 0f ? 180f : 0f);
            }
        }
    }

    /// <summary>
    /// A small group of ostriches that bolts as the runner approaches: they accelerate hard to a sprint,
    /// run alongside for a while, then tire and slow to a walk. They follow the ground and weave a little.
    /// </summary>
    public class OstrichGroup : MonoBehaviour
    {
        TrackPath path;
        float x, s, speed, life, run;
        List<OstrichRig> birds;
        Vector2[] offsets;
        bool started;

        public void Init(TrackPath p, float x0, float s0, List<OstrichRig> members)
        {
            path = p; x = x0; s = s0; birds = members;
            offsets = new Vector2[birds.Count];
            for (int i = 0; i < offsets.Length; i++) offsets[i] = new Vector2(Random.Range(-1.8f, 1.8f), -i * Random.Range(2.2f, 3.4f));
            Place(0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life += dt;
            var gm = GameManager.I;
            if (path == null || life > 90f || (gm != null && gm.Player != null && gm.Player.TrackS - s > 120f)) { Destroy(gameObject); return; }

            float target = 0f;
            if (gm != null && gm.Player != null && gm.State == GameState.Playing)
            {
                float gap = s - gm.Player.TrackS;
                if (!started && gap < 35f) started = true;
                if (started)
                {
                    run += dt;
                    // Sprint for a while (ostriches top out around 15 m/s), then tire and slow to a trot.
                    target = run < 9f ? Mathf.Max(13f, gm.Speed * 0.95f) : 3f;
                }
            }
            // Strong acceleration from a standstill, gentler braking
            float accel = target > speed ? 7f : 3f;
            speed = Mathf.MoveTowards(speed, target, accel * dt);
            s += speed * dt;
            Place(speed);
        }

        void Place(float spd)
        {
            for (int i = 0; i < birds.Count; i++)
            {
                if (birds[i] == null) continue;
                birds[i].Speed = spd * Random.Range(0.98f, 1.02f);
                float bs = s + offsets[i].y;
                float bx = x + offsets[i].x + Mathf.Sin(bs * 0.15f + i * 2f) * 0.6f;
                path.PlaceOn(birds[i].transform, bx, TrackGenerator.GroundHeight(bx, bs), bs, 180f);
            }
        }
    }
}
