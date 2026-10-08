using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Prints left in soft ground: boot prints and paw prints pressed into sand, snow and wet mud by the hero, other
    /// players and monsters, fading out over half a minute. All of them are one mesh (drawn like the blood stains,
    /// with the ground-decal shader), rebuilt only when a print is added or fades a step. Only near the hero, and
    /// never in dungeons or on paved town streets.
    /// </summary>
    public class Footprints : MonoBehaviour
    {
        const int Max = 260;
        const float Life = 26f, Fade = 6f, Near = 35f;

        struct Print { public Vector3 Pos, Fwd; public float Born, W, L; public Color Color; public bool Paw; }

        static Footprints I;
        readonly List<Print> prints = new List<Print>();
        Mesh mesh;
        Material material;
        bool dirty;
        float nextFade;

        /// <summary>One step at <paramref name="pos"/>, heading <paramref name="fwd"/>; <paramref name="left"/> foot or right.</summary>
        public static void Step(Vector3 pos, Vector3 fwd, bool left, bool paw, float scale)
        {
            var hero = Player.I;
            if (Dungeon.Active || hero == null || Factory.FlatDistance(hero.transform.position, pos) > Near || !Ground(pos, out var c)) return;
            if (I == null) I = new GameObject("Footprints").AddComponent<Footprints>();
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) return;
            fwd.Normalize();
            var side = Vector3.Cross(Vector3.up, fwd) * (left ? -1f : 1f) * 0.12f * scale;
            I.prints.Add(new Print
            {
                Pos = new Vector3(pos.x, 0.02f, pos.z) + side, Fwd = Quaternion.Euler(0f, left ? -6f : 6f, 0f) * fwd, Born = Time.time, Color = c, Paw = paw,
                W = (paw ? 0.2f : 0.15f) * scale, L = (paw ? 0.2f : 0.3f) * scale,
            });
            if (I.prints.Count > Max) I.prints.RemoveAt(0);
            I.dirty = true;
        }

        /// <summary>Does the ground here take prints, and in what colour (the dent's shade)?</summary>
        static bool Ground(Vector3 p, out Color c)
        {
            c = default;
            if (WorldGenerator.InTown(p) || WorldGenerator.InCrypt(p)) return false;
            if (SnowField.DepthAt(p) > 0.1f) { c = new Color(0.5f, 0.58f, 0.7f, 0.5f); return true; } // blue shadow in the snow
            var g = GroundSurface.Current;
            if (g == null) return false;
            if (g.Weight(p.x, p.z, GroundSurface.Sand) > 0.5f) { c = new Color(0.52f, 0.42f, 0.28f, 0.45f); return true; }
            if (Weather.Wet > 0.35f && (g.Weight(p.x, p.z, GroundSurface.Dirt) > 0.4f || g.Weight(p.x, p.z, GroundSurface.Forest) > 0.5f || g.Weight(p.x, p.z, GroundSurface.Dry) > 0.5f))
            { c = new Color(0.16f, 0.12f, 0.08f, 0.6f * Mathf.Clamp01((Weather.Wet - 0.35f) / 0.3f + 0.4f)); return true; } // mud after rain
            return false;
        }

        /// <summary>Dust kicked up by a step on sand or dry earth (null: none here).</summary>
        public static Color? DustAt(Vector3 p)
        {
            if (Dungeon.Active || WorldGenerator.InTown(p) || SnowField.DepthAt(p) > 0.1f || Weather.Wet > 0.4f) return null;
            var g = GroundSurface.Current;
            if (g == null) return null;
            if (g.Weight(p.x, p.z, GroundSurface.Sand) > 0.5f) return new Color(0.82f, 0.7f, 0.5f);
            if (g.Weight(p.x, p.z, GroundSurface.Dry) > 0.6f) return new Color(0.7f, 0.62f, 0.48f);
            return null;
        }

        void Start()
        {
            var shader = Resources.Load<Shader>("Shaders/ShadowfallDecal");
            if (shader == null) shader = Shader.Find("Shadowfall/Decal");
            material = new Material(shader) { mainTexture = MakeAtlas(), name = "Footprints" };
            material.SetFloat("_Wet", 0.2f);
            mesh = new Mesh { name = "Footprints" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void LateUpdate()
        {
            if (Dungeon.Active && prints.Count > 0) { prints.Clear(); dirty = true; }
            float now = Time.time;
            if (now >= nextFade)
            {
                nextFade = now + 0.5f;
                int before = prints.Count;
                prints.RemoveAll(x => now - x.Born > Life);
                if (prints.Count != before || prints.Exists(x => now - x.Born > Life - Fade)) dirty = true;
            }
            if (!dirty || mesh == null) return;
            dirty = false;
            Rebuild(now);
        }

        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> cols = new List<Color>();
        readonly List<int> tris = new List<int>();

        void Rebuild(float now)
        {
            verts.Clear(); uvs.Clear(); cols.Clear(); tris.Clear();
            foreach (var p in prints)
            {
                float a = Mathf.Clamp01((Life - (now - p.Born)) / Fade);
                var c = new Color(p.Color.r, p.Color.g, p.Color.b, p.Color.a * a);
                var f = p.Fwd * (p.L * 0.5f);
                var s = Vector3.Cross(Vector3.up, p.Fwd) * (p.W * 0.5f);
                int i = verts.Count;
                verts.Add(p.Pos - f - s); verts.Add(p.Pos + f - s); verts.Add(p.Pos + f + s); verts.Add(p.Pos - f + s);
                float u0 = p.Paw ? 0.5f : 0f, u1 = u0 + 0.5f;
                uvs.Add(new Vector2(u0, 0)); uvs.Add(new Vector2(u0, 1)); uvs.Add(new Vector2(u1, 1)); uvs.Add(new Vector2(u1, 0));
                cols.Add(c); cols.Add(c); cols.Add(c); cols.Add(c);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        /// <summary>Two prints side by side: a boot (sole and heel) and a paw (pad and four toes), soft-edged.</summary>
        static Texture2D MakeAtlas()
        {
            const int h = 64, w = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "FootprintAtlas", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            float Blob(float x, float y, float cx, float cy, float rx, float ry)
            {
                float dx = (x - cx) / rx, dy = (y - cy) / ry;
                return Mathf.Clamp01(1f - Mathf.SmoothStep(0.6f, 1f, Mathf.Sqrt(dx * dx + dy * dy)));
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a;
                    if (x < h)
                    {
                        float u = (x + 0.5f) / h, v = (y + 0.5f) / h; // boot: toe at the top (v = 1)
                        a = Mathf.Max(Blob(u, v, 0.5f, 0.66f, 0.3f, 0.3f), Blob(u, v, 0.5f, 0.2f, 0.22f, 0.17f));
                    }
                    else
                    {
                        float u = (x - h + 0.5f) / h, v = (y + 0.5f) / h;
                        a = Blob(u, v, 0.5f, 0.38f, 0.26f, 0.22f);
                        for (int t = 0; t < 4; t++)
                        {
                            float tx = 0.22f + t * 0.187f, ty = t == 0 || t == 3 ? 0.7f : 0.8f;
                            a = Mathf.Max(a, Blob(u, v, tx, ty, 0.1f, 0.11f));
                        }
                    }
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }
}
