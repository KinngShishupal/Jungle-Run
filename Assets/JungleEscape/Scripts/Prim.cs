using UnityEngine;
using UnityEngine.Rendering;

namespace JungleEscape
{
    /// <summary>
    /// Helpers that place shapes in the world: Unity primitives with a tinted PBR
    /// material, or procedural meshes from <see cref="Meshes"/> with any material.
    /// </summary>
    public static class Prim
    {
        public static GameObject Make(PrimitiveType type, string name, Transform parent,
                                      Vector3 localPos, Vector3 scale, Color color,
                                      Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            // Visual-only: colliders are added explicitly where needed.
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.GetComponent<Renderer>().sharedMaterial = Mats.Plain(color);
            return go;
        }

        /// <summary>Places a procedural mesh with the given material.</summary>
        public static GameObject Shape(Mesh mesh, string name, Transform parent, Vector3 localPos, Vector3 scale,
                                      Material mat, Vector3? euler = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Creates an empty child transform (used as a pivot or group).</summary>
        public static Transform Group(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
