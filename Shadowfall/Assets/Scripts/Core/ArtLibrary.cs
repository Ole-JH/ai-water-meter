using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shadowfall
{
    /// <summary>
    /// Loads the CC0 models in Assets/Resources/Art (imported by glTFast), scales them to a target size
    /// and places them. Returns null when a model is missing so callers can fall back to primitives.
    /// </summary>
    public static class ArtLibrary
    {
        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
        static readonly HashSet<Material> instanced = new HashSet<Material>();
        static readonly HashSet<string> warned = new HashSet<string>();

        public static bool Available => Load("Characters/Knight") != null;

        public static GameObject Load(string path)
        {
            if (cache.TryGetValue(path, out var go)) return go;
            go = Resources.Load<GameObject>("Art/" + path);
            if (go == null && warned.Add(path))
                Debug.LogWarning("[Shadowfall] Missing model Art/" + path + " (is the glTFast package installed?) - using primitives.");
            cache[path] = go;
            return go;
        }

        public enum Fit { Height, Width, Largest }

        /// <summary>
        /// Instantiates a model under <paramref name="parent"/>, scaled so its height/width equals
        /// <paramref name="size"/> (0 = keep native size), with its bottom resting at the local origin.
        /// </summary>
        public static GameObject Spawn(string path, Transform parent, Vector3 localPos, float size, Fit fit = Fit.Height,
            float yaw = 0f, bool castShadows = true, bool groundAlign = true, bool centerXZ = false)
        {
            return SpawnInternal(path, parent, localPos, b =>
            {
                if (size <= 0f) return Vector3.one;
                float measure = fit == Fit.Height ? b.size.y
                    : fit == Fit.Width ? Mathf.Max(b.size.x, b.size.z)
                    : Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                return Vector3.one * (measure > 0.0001f ? size / measure : 1f);
            }, yaw, castShadows, groundAlign, centerXZ);
        }

        /// <summary>
        /// Like <see cref="Spawn"/>, but stretches the model to exactly fill <paramref name="size"/>
        /// (in the model's own axes, before <paramref name="yaw"/>). Used for wall and fence pieces.
        /// </summary>
        public static GameObject SpawnBox(string path, Transform parent, Vector3 localPos, Vector3 size, float yaw = 0f, bool castShadows = true)
        {
            return SpawnInternal(path, parent, localPos, b => new Vector3(
                b.size.x > 0.0001f ? size.x / b.size.x : 1f,
                b.size.y > 0.0001f ? size.y / b.size.y : 1f,
                b.size.z > 0.0001f ? size.z / b.size.z : 1f), yaw, castShadows, true, true);
        }

        static GameObject SpawnInternal(string path, Transform parent, Vector3 localPos, System.Func<Bounds, Vector3> scaleFor,
            float yaw, bool castShadows, bool groundAlign, bool centerXZ)
        {
            var prefab = Load(path);
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab);
            go.name = path.Substring(path.LastIndexOf('/') + 1);
            var t = go.transform;
            t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            t.localScale = Vector3.one;

            var bounds = WorldBounds(go);
            if (bounds.size.sqrMagnitude > 0f)
            {
                t.localScale = scaleFor(bounds);
                bounds = WorldBounds(go);
            }

            // Wrap in a pivot so the model's feet sit on y = 0 and it can be rotated around its base.
            var pivot = new GameObject(go.name).transform;
            t.SetParent(pivot, false);
            t.localPosition = new Vector3(
                centerXZ ? -bounds.center.x : 0f,
                groundAlign ? -bounds.min.y : 0f,
                centerXZ ? -bounds.center.z : 0f);
            pivot.SetParent(parent, false);
            pivot.localPosition = localPos;
            pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                foreach (var m in r.sharedMaterials)
                    if (m != null && instanced.Add(m)) { m.enableInstancing = true; Matte(m); }
            }
            return pivot.gameObject;
        }

        static readonly int RoughnessId = Shader.PropertyToID("roughnessFactor"), MetallicId = Shader.PropertyToID("metallicFactor"),
            GlossId = Shader.PropertyToID("_Glossiness"), MetalId = Shader.PropertyToID("_Metallic");

        /// <summary>
        /// The low-poly models are painted flat: a glossy or metallic setting from the file only turns roofs, crates and
        /// stone into a white glare under the sun seen from above. Once per material: rough, barely metallic.
        /// </summary>
        static void Matte(Material m)
        {
            if (m.HasProperty(RoughnessId)) m.SetFloat(RoughnessId, Mathf.Max(0.8f, m.GetFloat(RoughnessId)));
            if (m.HasProperty(MetallicId)) m.SetFloat(MetallicId, Mathf.Min(0.1f, m.GetFloat(MetallicId)));
            if (m.HasProperty(GlossId)) m.SetFloat(GlossId, Mathf.Min(0.2f, m.GetFloat(GlossId)));
            if (m.HasProperty(MetalId)) m.SetFloat(MetalId, Mathf.Min(0.1f, m.GetFloat(MetalId)));
        }

        public static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Multiplies the color of every material on the object (creates material instances).</summary>
        public static void Tint(GameObject go, Color tint)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                foreach (var m in mats) m.color *= tint;
                r.materials = mats;
            }
        }
    }
}
