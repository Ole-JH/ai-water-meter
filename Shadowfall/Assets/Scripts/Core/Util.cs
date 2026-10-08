using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Material cache. Materials are cloned from the render pipeline's default material
    /// (taken from a primitive), so this works in both Built-in and URP projects.
    /// </summary>
    public static class Mat
    {
        static Material baseMat;
        static readonly Dictionary<Color32, Material> cache = new Dictionary<Color32, Material>();
        static readonly Dictionary<Color32, Material> glowCache = new Dictionary<Color32, Material>();

        static Material Base
        {
            get
            {
                if (baseMat == null)
                {
                    var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    baseMat = tmp.GetComponent<Renderer>().sharedMaterial;
                    Object.DestroyImmediate(tmp);
                }
                return baseMat;
            }
        }

        public static Material Get(Color c)
        {
            Color32 key = c;
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = New(c);
            cache[key] = m;
            return m;
        }

        /// <summary>A material that glows (emission), useful for spells, eyes and loot beams.</summary>
        public static Material Glow(Color c)
        {
            Color32 key = c;
            if (glowCache.TryGetValue(key, out var m) && m != null) return m;
            m = New(c);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 1.5f);
            glowCache[key] = m;
            return m;
        }

        /// <summary>A fresh, non-shared material (for things that change color at runtime).</summary>
        public static Material New(Color c)
        {
            var m = new Material(Base);
            m.color = c;
            m.SetFloat("_Glossiness", 0.08f);
            m.SetFloat("_Smoothness", 0.08f);
            m.SetFloat("_Metallic", 0f);
            return m;
        }
    }

    public static class Factory
    {
        /// <summary>
        /// CreatePrimitive adds a collider the WebGL build's code stripping can't see we need (nothing in the scene uses
        /// one): naming the types here keeps them, with Assets/link.xml.
        /// </summary>
        internal static readonly System.Type[] KeepColliders = { typeof(SphereCollider), typeof(CapsuleCollider), typeof(BoxCollider), typeof(MeshCollider) };

        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color,
            bool keepCollider = false, Material material = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material != null ? material : Mat.Get(color);
            // Small bits (gibs, coals, rungs, trinkets) don't cast: their shadows hardly show from up here, and each caster
            // is drawn once more into the shadow map
            if (Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) < 1.4f) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static Transform Empty(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        public static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        public static void Face(Transform t, Vector3 point, float lerp = 1f)
        {
            var d = Flat(point - t.position);
            if (d.sqrMagnitude < 0.0001f) return;
            var target = Quaternion.LookRotation(d);
            t.rotation = lerp >= 1f ? target : Quaternion.Slerp(t.rotation, target, lerp);
        }
    }

    /// <summary>Simple scaling "pulse" effect (explosions, novas, level-up rings).</summary>
    public class FxPulse : MonoBehaviour
    {
        Vector3 fromScale, toScale;
        float duration, t;
        Vector3 velocity;

        public static FxPulse Spawn(Vector3 pos, Color color, Vector3 fromScale, Vector3 toScale, float duration,
            PrimitiveType shape = PrimitiveType.Sphere)
        {
            var go = Factory.Prim(shape, null, pos, fromScale, color, false, Mat.Glow(color));
            go.name = "FX";
            var shadow = go.GetComponent<Renderer>();
            shadow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var fx = go.AddComponent<FxPulse>();
            fx.fromScale = fromScale;
            fx.toScale = toScale;
            fx.duration = duration;
            return fx;
        }

        public static void Burst(Vector3 pos, Color color, float radius, float duration = 0.35f)
        {
            Spawn(pos, color, Vector3.one * 0.2f, Vector3.one * radius * 2f, duration);
        }

        public static void Ring(Vector3 pos, Color color, float radius, float duration = 0.4f)
        {
            Spawn(new Vector3(pos.x, 0.05f, pos.z), color, new Vector3(0.3f, 0.02f, 0.3f),
                new Vector3(radius * 2f, 0.02f, radius * 2f), duration, PrimitiveType.Cylinder);
        }

        public static void Sparks(Vector3 pos, Color color, int count = 6)
        {
            for (int i = 0; i < count; i++)
            {
                var fx = Spawn(pos, color, Vector3.one * 0.15f, Vector3.zero, Random.Range(0.3f, 0.6f), PrimitiveType.Cube);
                fx.velocity = new Vector3(Random.Range(-3f, 3f), Random.Range(2f, 5f), Random.Range(-3f, 3f));
            }
        }

        public FxPulse WithVelocity(Vector3 v)
        {
            velocity = v;
            return this;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            transform.localScale = Vector3.Lerp(fromScale, toScale, 1f - (1f - k) * (1f - k));
            if (velocity != Vector3.zero)
            {
                transform.position += velocity * Time.deltaTime;
                velocity += Vector3.down * 9f * Time.deltaTime;
            }
            if (t >= duration) Destroy(gameObject);
        }
    }
}
