using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// Something to steer the canoe around on the open water (water world): a jagged coral rock breaking the
    /// surface, with foam washing around it, or a floating log that drifts slowly across the lanes, bobbing and
    /// rolling on the swell. Ramming one costs a chance (see PlayerController / GameManager).
    /// Track space: X across, S along.
    /// </summary>
    public class WaterHazard : MonoBehaviour
    {
        public enum Kind { Rock, Log }

        public Kind Type;
        public bool Hit;
        float s, x, drift, time, foamT;
        TrackPath path;
        Transform visual;

        public string CrashLine => Type == Kind.Rock ? "Wrecked on the rocks!" : "Rammed a floating log!";

        public static WaterHazard Spawn(Transform parent, Kind kind, float s, float x)
        {
            var go = new GameObject(kind == Kind.Rock ? "CoralRock" : "DriftLog");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<WaterHazard>();
            h.Type = kind; h.s = s; h.x = x; h.path = TrackPath.Active;
            h.time = Random.value * 10f;
            h.visual = Prim.Group("Visual", go.transform, Vector3.zero);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            if (kind == Kind.Rock)
            {
                // A cluster of coral/limestone rock, mostly above the water
                var rockMat = Biomes.Remap(Mats.Rock, BiomeType.WaterWorld);
                float sc = Random.Range(1.4f, 1.9f);
                Prim.Shape(Flora.Rock(Random.Range(0, 4)).Parts[0].Mesh, "Rock", h.visual, new Vector3(0f, -0.35f, 0f),
                           new Vector3(sc, sc * 1.25f, sc), rockMat, new Vector3(0f, Random.Range(0f, 360f), 0f));
                Prim.Shape(Flora.Rock(Random.Range(0, 4)).Parts[0].Mesh, "Rock2", h.visual, new Vector3(Random.Range(-0.6f, 0.6f), -0.45f, 0.5f),
                           Vector3.one * sc * 0.6f, rockMat, new Vector3(0f, Random.Range(0f, 360f), 0f));
                col.center = new Vector3(0f, 0.2f, 0f);
                col.size = new Vector3(1.7f, 2.2f, 1.6f);
            }
            else
            {
                // A sodden log lying across the lanes, with a couple of broken branch stubs
                var bark = Biomes.Remap(Mats.Bark, BiomeType.WaterWorld);
                var log = Meshes.Limb(3.2f, 0.3f, 0.26f, 10, 4);
                Prim.Shape(log, "Log", h.visual, new Vector3(1.6f, 0f, 0f), Vector3.one, bark, new Vector3(0f, 0f, -90f));
                Prim.Shape(Meshes.Limb(0.6f, 0.08f, 0.04f, 6, 2), "Stub", h.visual, new Vector3(0.5f, 0.2f, 0f), Vector3.one, bark, new Vector3(0f, 0f, 160f));
                Prim.Shape(Meshes.Limb(0.4f, 0.07f, 0.03f, 6, 2), "Stub", h.visual, new Vector3(-0.9f, 0.18f, 0.1f), Vector3.one, bark, new Vector3(30f, 0f, -150f));
                Prim.Shape(Meshes.Disc(16), "EndRings", h.visual, new Vector3(-1.6f, 0f, 0f), new Vector3(0.55f, 1f, 0.55f), Mats.WoodEnd, new Vector3(0f, 0f, 90f));
                h.drift = (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.6f, 1.4f);
                col.center = Vector3.zero;
                col.size = new Vector3(3.2f, 0.9f, 0.9f);
                var rb = go.AddComponent<Rigidbody>();   // a moving trigger
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            h.Place();
            return h;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            if (Type == Kind.Log)
            {
                // Drifts across the lanes and turns back near the edges of the water
                x += drift * dt;
                if (Mathf.Abs(x) > 4.2f) { x = Mathf.Sign(x) * 4.2f; drift = -drift; }
                visual.localRotation = Quaternion.Euler(4f * Mathf.Sin(time * 1.1f), 6f * Mathf.Sin(time * 0.3f), 0f)
                                       * Quaternion.Euler(10f * Mathf.Sin(time * 1.4f), 0f, 0f);   // rocking on its long axis in the swell
            }
            if ((foamT -= dt) <= 0f)
            {
                foamT = Random.Range(0.5f, 1.1f);   // the swell washing around it
                SeaLife.Splash(transform.position + Random.insideUnitSphere * 0.6f, 2, Type == Kind.Rock ? 0.7f : 0.45f);
            }
            Place();
        }

        void Place()
        {
            if (path == null) return;
            float bob = Type == Kind.Log ? 0.06f * Mathf.Sin(time * 1.9f) : 0f;
            path.PlaceOn(transform, x, Canoe.WaterY + (Type == Kind.Log ? 0.05f : 0f) + bob, s);
        }

        /// <summary>Breaks up when rammed (debris and a burst of spray), leaving the way clear.</summary>
        public void Smash()
        {
            Hit = true;
            Fx.Debris(transform.position + Vector3.up * 0.4f, Type == Kind.Rock ? new Color(0.85f, 0.78f, 0.7f) : new Color(0.4f, 0.3f, 0.2f));
            SeaLife.Splash(transform.position + Vector3.up * 0.3f, 10, 1.2f);
            gameObject.SetActive(false);
        }
    }
}
