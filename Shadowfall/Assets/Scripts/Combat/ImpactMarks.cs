using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// What spells leave on the ground for a while after the flash: scorch with embers where fire landed, rime and ice
    /// splinters after frost, cracked earth where something heavy came down, and glowing runes after holy power. They
    /// fade and sink away; only the newest few dozen are kept.
    /// </summary>
    public static class ImpactMarks
    {
        public enum Kind { Scorch, Frost, Crack, Holy }
        const int Max = 40;
        static readonly Queue<GameObject> marks = new Queue<GameObject>();

        public static void Place(Vector3 at, Kind kind, float radius, float seconds = 18f)
        {
            if (WorldGrid.Instance != null && !WorldGrid.Instance.IsWalkable(at)) return;
            var root = new GameObject("Mark " + kind).transform;
            root.position = new Vector3(at.x, 0f, at.z);
            root.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            switch (kind)
            {
                case Kind.Scorch:
                    Disc(root, radius, new Color(0.08f, 0.06f, 0.05f, 0.75f));
                    Disc(root, radius * 0.55f, new Color(0.03f, 0.025f, 0.02f, 0.6f), 0.016f);
                    for (int i = 0; i < 6; i++) // embers still glowing in it
                    {
                        var e = Bit(root, radius * 0.7f, new Vector3(0.08f, 0.03f, 0.08f), new Color(1f, 0.4f, 0.08f), true);
                        Object.Destroy(e, Random.Range(3f, 7f));
                    }
                    break;
                case Kind.Frost:
                    Disc(root, radius, new Color(0.8f, 0.9f, 1f, 0.55f));
                    for (int i = 0; i < 10; i++)
                    {
                        var s = Bit(root, radius * 0.9f, new Vector3(0.06f, Random.Range(0.15f, 0.35f), 0.06f), new Color(0.7f, 0.9f, 1f), true);
                        s.transform.localRotation = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 90f), Random.Range(-30f, 30f));
                        Object.Destroy(s, Random.Range(4f, 8f)); // the splinters melt first
                    }
                    break;
                case Kind.Crack:
                    Disc(root, radius * 0.6f, new Color(0.18f, 0.15f, 0.12f, 0.6f));
                    int n = Random.Range(6, 9);
                    for (int i = 0; i < n; i++)
                    {
                        float ang = i * 360f / n + Random.Range(-14f, 14f), len = radius * Random.Range(0.7f, 1.3f);
                        var c = Factory.Prim(PrimitiveType.Cube, root, Vector3.zero, new Vector3(Random.Range(0.06f, 0.12f), 0.012f, len), new Color(0.1f, 0.08f, 0.07f));
                        c.transform.localRotation = Quaternion.Euler(0f, ang, 0f);
                        c.transform.localPosition = c.transform.localRotation * new Vector3(0f, 0.022f, len * 0.5f + radius * 0.2f);
                        NoShadow(c);
                    }
                    for (int i = 0; i < 5; i++) Bit(root, radius, Vector3.one * Random.Range(0.12f, 0.25f), new Color(0.35f, 0.3f, 0.26f), false); // stones thrown up
                    break;
                case Kind.Holy:
                {
                    // a faint golden sheen on the ground (no solid plate: it read as a big dark disc with a rim)
                    var gold = new Color(1f, 0.85f, 0.4f);
                    Disc(root, radius, new Color(1f, 0.88f, 0.5f, 0.28f));
                    for (int i = 0; i < 8; i++) // runes round the edge
                    {
                        float a = i * Mathf.PI / 4f;
                        var r = Factory.Prim(PrimitiveType.Cube, root, new Vector3(Mathf.Cos(a), 0.022f, Mathf.Sin(a)) * radius * 0.75f, new Vector3(0.14f, 0.01f, 0.22f), gold, false, Mat.Glow(gold));
                        r.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                        NoShadow(r);
                        Object.Destroy(r, seconds * 0.5f); // the runes go out before the burn fades
                    }
                    break;
                }
            }
            root.gameObject.AddComponent<FadeAway>().Seconds = seconds;
            marks.Enqueue(root.gameObject);
            while (marks.Count > Max) { var old = marks.Dequeue(); if (old != null) Object.Destroy(old); }
        }

        static Material decal;
        static Texture2D soft;

        /// <summary>A soft stain on the ground: a round splotch with a ragged, faded edge, drawn with the ground-decal
        /// shader (like blood), tinted and see-through by <paramref name="c"/>'s alpha.</summary>
        static void Disc(Transform root, float radius, Color c, float y = 0.012f)
        {
            if (decal == null)
            {
                var shader = Resources.Load<Shader>("Shaders/ShadowfallDecal");
                if (shader == null) shader = Shader.Find("Shadowfall/Decal");
                decal = new Material(shader) { mainTexture = SoftTexture(), name = "ImpactMark" };
                decal.SetFloat("_Wet", 0f);
            }
            float rx = radius, rz = radius * Random.Range(0.8f, 1f);
            var mesh = new Mesh { name = "Mark" };
            mesh.vertices = new[] { new Vector3(-rx, 0f, -rz), new Vector3(-rx, 0f, rz), new Vector3(rx, 0f, rz), new Vector3(rx, 0f, -rz) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.colors = new[] { c, c, c, c };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            var go = new GameObject("Stain");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = decal;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<DestroyMesh>().Mesh = mesh;
        }

        /// <summary>White, with alpha falling off from the middle to a ragged edge (a little noise, so it's not a perfect circle).</summary>
        static Texture2D SoftTexture()
        {
            if (soft != null) return soft;
            const int n = 64;
            soft = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "SoftMark", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float ang = Mathf.Atan2(dy, dx);
                    float edge = 0.82f + 0.1f * Mathf.Sin(ang * 5f + 1.3f) + 0.06f * Mathf.Sin(ang * 11f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / edge;
                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.55f, 1f, d));
                    a *= 0.85f + 0.15f * Mathf.PerlinNoise(x * 0.25f, y * 0.25f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            soft.SetPixels32(px);
            soft.Apply(true);
            return soft;
        }

        /// <summary>Frees a mark's own mesh along with it.</summary>
        class DestroyMesh : MonoBehaviour
        {
            public Mesh Mesh;
            void OnDestroy() { if (Mesh != null) Destroy(Mesh); }
        }

        static GameObject Bit(Transform root, float spread, Vector3 size, Color c, bool glow)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(0.1f, spread);
            var b = Factory.Prim(PrimitiveType.Cube, root, new Vector3(Mathf.Cos(a) * r, size.y * 0.5f, Mathf.Sin(a) * r), size, c, false, glow ? Mat.Glow(c) : null);
            b.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            NoShadow(b);
            return b;
        }

        static void NoShadow(GameObject go) => go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
