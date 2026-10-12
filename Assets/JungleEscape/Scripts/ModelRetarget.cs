using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace JungleEscape
{
    /// <summary>
    /// Drives an imported, rigged character model (the jungle boy, MakeHuman skeleton) from the procedural
    /// <see cref="ExplorerRig"/> every frame, so the model gets all the game's motion - run, jump, roll,
    /// leaf-glider hang and swing, stumble, knock-out, idle - without any animation clips.
    ///
    /// Retargeting works by direction rather than by copying angles, because the two skeletons have different
    /// rest poses (the model is in a T/A-pose, the rig hangs its arms down):
    ///  - hips: follow the rig pelvis (position and rotation, relative to each rest pose);
    ///  - spine: bent so it points along the rig's spine, spread over all five spine bones;
    ///  - neck and head: neck aimed like the rig's neck, head turned like the rig's head;
    ///  - limbs: each upper arm, forearm, thigh, shin and foot is rotated so it points the same way as the
    ///    matching rig segment.
    /// Every frame starts again from the rest pose, so no error builds up.
    /// </summary>
    public class ModelRetarget : MonoBehaviour
    {
        const string ResourcePath = "CharacterModel/JungleBoy";

        ExplorerRig rig;
        Transform model, player;
        Transform hips, neck0, head;
        Transform[] spine;
        Limb[] limbs;
        readonly List<(Transform t, Quaternion rot, Vector3 pos)> rest = new List<(Transform, Quaternion, Vector3)>();
        Quaternion hipsRestRel, headRestRel;
        Vector3 hipsOffset;
        Renderer[] renderers;

        struct Limb
        {
            public Transform Bone, Child;      // the model bone, and the bone at the far end of the segment
            public System.Func<Vector3> From, To; // the rig segment it should line up with
        }

        /// <summary>Loads the character model if it is in the project, and binds it to the rig.</summary>
        public static ModelRetarget TryCreate(Transform player, ExplorerRig rig)
        {
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null) return null;
            var r = player.gameObject.AddComponent<ModelRetarget>();
            if (!r.Bind(prefab, player, rig))
            {
                Destroy(r);
                return null;
            }
            return r;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name || t.name.EndsWith(":" + name)) return t;
            return null;
        }

        bool Bind(GameObject prefab, Transform player, ExplorerRig rig)
        {
            this.player = player;
            this.rig = rig;
            model = Instantiate(prefab, player, false).transform;
            model.name = "JungleBoy";
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            var anim = model.GetComponentInChildren<Animator>();
            if (anim != null) anim.enabled = false; // we pose the bones ourselves

            hips = Find(model, "root");
            neck0 = Find(model, "neck01");
            head = Find(model, "head");
            var names = new[] { "spine05", "spine04", "spine03", "spine02", "spine01" };
            var sp = new List<Transform>();
            foreach (var n in names) { var t = Find(model, n); if (t != null) sp.Add(t); }
            Transform B(string n) => Find(model, n);
            var need = new[] { hips, neck0, head, B("upperarm01.L"), B("lowerarm01.L"), B("wrist.L"), B("upperleg01.L"), B("lowerleg01.L"), B("foot.L") };
            foreach (var t in need)
                if (t == null)
                {
                    Debug.LogWarning("[JungleBoy] The character model's skeleton isn't the expected MakeHuman rig; using the built-in explorer.");
                    Destroy(model.gameObject);
                    return false;
                }
            // Order the spine from the hips up, whatever the naming convention
            sp.Sort((a, b) => a.position.y.CompareTo(b.position.y));
            spine = sp.ToArray();

            // ---- Face forward (+Z) and match the rig's size ----
            Vector3 right = (B("upperleg01.R").position - B("upperleg01.L").position).normalized;
            Vector3 up = (head.position - hips.position).normalized;
            Vector3 fwd = Vector3.Cross(right, up).normalized;
            model.rotation = player.rotation * Quaternion.Inverse(Quaternion.LookRotation(fwd, up)) * model.rotation;
            // Facing is fixed; now match the leg length (hip joint to ankle) to the rig's
            float footY = Mathf.Min(B("foot.L").position.y, B("foot.R").position.y);
            float hipH = (B("upperleg01.L").position.y + B("upperleg01.R").position.y) * 0.5f - footY;
            float scale = hipH > 1e-4f ? 0.805f / hipH : 1f;   // rig: hip joints 0.90 m, ankles 0.095 m above the ground
            model.localScale *= scale;
            // Stand on the ground
            footY = Mathf.Min(B("foot.L").position.y, B("foot.R").position.y);
            model.position += Vector3.up * (player.position.y - footY + 0.095f);

            // ---- Rest pose ----
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                rest.Add((t, t.localRotation, t.localPosition));
            hipsRestRel = Quaternion.Inverse(player.rotation) * hips.rotation;
            headRestRel = Quaternion.Inverse(player.rotation) * head.rotation;
            hipsOffset = player.InverseTransformPoint(hips.position) - new Vector3(0f, 0.92f, 0f);

            // ---- Limbs (parents before children) ----
            var L = new List<Limb>();
            void Seg(string bone, string child, System.Func<Vector3> from, System.Func<Vector3> to)
            {
                var b = B(bone); var c = B(child);
                if (b != null && c != null) L.Add(new Limb { Bone = b, Child = c, From = from, To = to });
            }
            // The model's ".L" is its own left; the rig's left limbs sit on its -X side.
            foreach (var side in new[] { "L", "R" })
            {
                bool left = side == "L";
                Transform sh = left ? rig.ShoulderL : rig.ShoulderR, el = left ? rig.ElbowL : rig.ElbowR;
                Transform th = left ? rig.ThighL : rig.ThighR, kn = left ? rig.KneeL : rig.KneeR, an = left ? rig.AnkleL : rig.AnkleR;
                Seg("upperarm01." + side, "lowerarm01." + side, () => sh.position, () => el.position);
                Seg("lowerarm01." + side, "wrist." + side, () => el.position, () => el.TransformPoint(0f, -0.28f, 0f));
                Seg("upperleg01." + side, "lowerleg01." + side, () => th.position, () => kn.position);
                Seg("lowerleg01." + side, "foot." + side, () => kn.position, () => an.position);
                string toe = B("toe3-1." + side) != null ? "toe3-1." + side : "toe1-1." + side;
                Seg("foot." + side, toe, () => an.position, () => an.TransformPoint(0f, -0.06f, 0.2f));
            }
            limbs = L.ToArray();

            renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = false;
                    var bnd = smr.localBounds; bnd.Expand(bnd.size.magnitude); smr.localBounds = bnd; // poses reach outside the bind pose
                }
                r.sharedMaterials = Look(r.sharedMaterials);
            }
            AddRimLight();
            return true;
        }

        // ---------------- Jungle boy look ----------------

        const string TexPath = "CharacterModel/Textures/";
        /// <summary>Rendering-layer bit used only by the character, so the rim light touches nothing else.</summary>
        const uint CharacterLayerBit = 1u << 2;

        static Texture2D Tex(string name) => Resources.Load<Texture2D>(TexPath + name);

        static Material Lit(string name, bool cutout, Texture tex, Color tint, float smooth, bool bothSides, float cutoff = 0.5f)
        {
            var m = new Material(Mats.Template(cutout ? "JE_LitCutout" : "JE_Lit")) { name = "JB_" + name };
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BumpMap", null);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Cull", bothSides ? 0f : 2f);
            if (cutout) m.SetFloat("_Cutoff", cutoff);
            return m;
        }

        static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.white;

        /// <summary>
        /// The jungle boy's materials, matched by the FBX material names: textured skin on the soft-skin
        /// shader, alpha-clipped hair cards, brows and lashes, glossy eyes, a woven loincloth, and plain
        /// coloured vine, leaves, bone (knife handle, wolf tooth) and leather (sheath, cord).
        /// Returns null for anything it doesn't recognise.
        /// </summary>
        static Material JungleBoyMaterial(string fbxName)
        {
            string n = fbxName.ToLowerInvariant();
            if (n.StartsWith("skin") || n == "body")
            {
                var src = Resources.Load<Material>("CharacterModel/JB_Skin");
                var albedo = Tex("JB_Skin_Albedo");
                if (src != null && src.shader != null && src.shader.isSupported)
                {
                    var m = new Material(src);
                    m.SetTexture("_BaseMap", albedo);
                    m.SetTexture("_MetallicGlossMap", Tex("JB_Skin_MetallicSmoothness"));
                    return m;
                }
                return Lit("Skin", false, albedo, Color.white, 0.4f, false);
            }
            if (n.StartsWith("hair")) return Lit("Hair", true, Tex("JB_Hair_Albedo"), Color.white, 0.35f, true, 0.35f);
            if (n.StartsWith("eye")) return Lit("Eyes", false, Tex("JB_Eye_Albedo"), Color.white, 0.9f, false);
            if (n.StartsWith("brow")) return Lit("Brows", true, Tex("JB_Brows_Albedo"), Color.white, 0.2f, true, 0.3f);
            if (n.StartsWith("lash")) return Lit("Lashes", true, Tex("JB_Lashes_Albedo"), Color.white, 0.2f, true, 0.3f);
            if (n.StartsWith("loincloth")) return Lit("Loincloth", false, Tex("JB_Loincloth_Albedo"), Color.white, 0.08f, true);
            if (n.StartsWith("vine")) return Lit("Vine", false, null, Hex("#556930"), 0.35f, false);
            if (n.StartsWith("leaf")) return Lit("Leaf", false, null, Hex("#457330"), 0.45f, true);
            if (n.StartsWith("bone")) return Lit("Bone", false, null, Hex("#E1D7BF"), 0.55f, false);
            if (n.StartsWith("leather")) return Lit("Leather", false, null, Hex("#553C27"), 0.3f, false);
            return null;
        }

        static readonly Dictionary<string, Material> lookCache = new Dictionary<string, Material>();

        static Material[] Look(Material[] src)
        {
            var fallback = Convert(src);
            var dst = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i] == null) continue;
                string key = src[i].name.Replace(" (Instance)", "");
                if (!lookCache.TryGetValue(key, out var m) || m == null)
                {
                    m = JungleBoyMaterial(key);
                    if (m != null) lookCache[key] = m;
                }
                dst[i] = m != null ? m : fallback[i];
            }
            return dst;
        }

        /// <summary>
        /// A soft, cool rim light from behind the character (towards the camera), outlining his silhouette
        /// against the scenery. It lights only the character's rendering layer and casts no shadows.
        /// </summary>
        void AddRimLight()
        {
            var go = new GameObject("CharacterRimLight");
            go.transform.SetParent(player, false);
            go.transform.localRotation = Quaternion.Euler(25f, 180f, 0f); // shining back at the camera, slightly down
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = Hex("#DDEEDD");
            l.intensity = 0.6f;
            l.shadows = LightShadows.None;
            l.renderingLayerMask = (int)CharacterLayerBit;
            var data = l.GetUniversalAdditionalLightData();
            if (data != null) data.renderingLayers = CharacterLayerBit;
            foreach (var r in renderers) r.renderingLayerMask |= CharacterLayerBit;
        }

        /// <summary>
        /// Rebuilds the model's materials on the game's own shader templates (kept in mobile builds), with
        /// alpha cut-out for hair, eyebrows and eyelashes.
        /// </summary>
        static Material[] Convert(Material[] src)
        {
            var dst = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var m = src[i];
                if (m == null) continue;
                string n = m.name.ToLowerInvariant();
                bool cut = n.Contains("lash") || n.Contains("brow") || n.Contains("hair");
                var nm = new Material(Mats.Template(cut ? "JE_LitCutout" : "JE_Lit")) { name = m.name };
                Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
                Color col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                nm.SetTexture("_BaseMap", tex);
                nm.SetColor("_BaseColor", new Color(col.r, col.g, col.b, 1f));
                if (m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null) { nm.SetTexture("_BumpMap", m.GetTexture("_BumpMap")); nm.SetFloat("_BumpScale", 1f); }
                else nm.SetTexture("_BumpMap", null);
                nm.SetFloat("_Smoothness", n.Contains("eye") && !cut ? 0.85f : n.Contains("skin") || n.Contains("body") ? 0.35f : 0.2f);
                nm.SetFloat("_Metallic", 0f);
                if (cut) nm.SetFloat("_Cutoff", 0.4f);
                dst[i] = nm;
            }
            return dst;
        }

        public void SetVisible(bool visible)
        {
            foreach (var r in renderers) if (r != null) r.enabled = visible;
        }

        void LateUpdate()
        {
            if (rig == null || model == null) return;

            // Start from the rest pose every frame
            foreach (var r in rest) { r.t.localRotation = r.rot; r.t.localPosition = r.pos; }

            // Hips follow the rig's pelvis
            var pelvis = rig.PelvisT;
            hips.rotation = pelvis.rotation * hipsRestRel;
            hips.position = pelvis.position + rig.BodyT.rotation * hipsOffset;

            // Spine: bend it, a little at each vertebra, until it points along the rig's spine
            Vector3 target = (rig.NeckT.position - rig.SpineT.position).normalized;
            for (int i = 0; i < spine.Length; i++)
            {
                Vector3 cur = (neck0.position - spine[0].position).normalized;
                var delta = Quaternion.FromToRotation(cur, target);
                spine[i].rotation = Quaternion.Slerp(Quaternion.identity, delta, 1f / (spine.Length - i)) * spine[i].rotation;
            }
            // Neck along the rig's neck, then the head turned like the rig's head
            {
                Vector3 cur = (head.position - neck0.position).normalized;
                neck0.rotation = Quaternion.FromToRotation(cur, rig.NeckT.up) * neck0.rotation;
                head.rotation = rig.NeckT.rotation * headRestRel;
            }

            // Limbs: point each segment the way the rig's segment points
            foreach (var l in limbs)
            {
                Vector3 want = l.To() - l.From();
                Vector3 cur = l.Child.position - l.Bone.position;
                if (want.sqrMagnitude < 1e-8f || cur.sqrMagnitude < 1e-8f) continue;
                l.Bone.rotation = Quaternion.FromToRotation(cur, want) * l.Bone.rotation;
            }
        }
    }
}
