using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Renders the round minimap image on the CPU a few times a second: the world's map colors sampled smoothly around
    /// the hero, walls and buildings darkened with a light outline so streets read clearly, a soft circular mask
    /// and a darker rim. Works for the overworld and for dungeon levels.
    /// </summary>
    public static class Minimap
    {
        public const int Size = 192;
        public static float Span = 64f;           // world units across the map (zoom)
        public const float MinSpan = 36f, MaxSpan = 140f;

        static Texture2D tex, arrow;
        static Color32[] buf;
        static Vector3 lastCenter = new Vector3(-999f, 0f, -999f);
        static float lastSpan, nextRender;
        static bool lastUnderground;
        static int lastFog = -1;

        /// <summary>The map image centered on <paramref name="center"/> (re-rendered when the hero moves or zooms).</summary>
        public static Texture2D Render(Vector3 center)
        {
            if (tex == null)
            {
                tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Minimap" };
                buf = new Color32[Size * Size];
            }
            bool underground = Dungeon.Active;
            bool moved = Factory.FlatDistance(center, lastCenter) > Span / Size * 0.75f;
            bool fogChanged = Exploration.Version != lastFog || AdminTools.RevealMap != lastReveal;
            if ((!moved && !fogChanged && Mathf.Approximately(lastSpan, Span) && underground == lastUnderground) || Time.unscaledTime < nextRender) return tex;
            lastFog = Exploration.Version;
            lastReveal = AdminTools.RevealMap;
            nextRender = Time.unscaledTime + 0.1f;
            lastCenter = center;
            lastSpan = Span;
            lastUnderground = underground;

            var map = underground ? Dungeon.MapTexture : GameManager.I.World.MapTexture;
            var grid = WorldGrid.Instance;
            Vector3 origin = underground ? Dungeon.Origin : Vector3.zero;
            float mapW = underground ? Dungeon.Width : WorldGenerator.W, mapH = underground ? Dungeon.Height : WorldGenerator.H;
            float step = Span / Size, half = Size / 2f;
            var outside = new Color(0.04f, 0.035f, 0.03f);

            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / half;   // 0 center .. 1 rim
                    int i = y * Size + x;
                    if (r > 1f) { buf[i] = new Color32(0, 0, 0, 0); continue; }
                    float wx = center.x + dx * step, wz = center.z + dy * step;
                    float u = (wx - origin.x) / mapW, v = (wz - origin.z) / mapH;
                    Color c;
                    if (map == null || u < 0f || v < 0f || u > 1f || v > 1f) c = outside;
                    else
                    {
                        c = map.GetPixelBilinear(u, v);
                        var p = new Vector3(wx, 0f, wz);
                        if (!grid.IsWalkable(p))
                        {
                            // Blocked: darker, with a light edge where it meets open ground (buildings, walls, cliffs).
                            bool edge = grid.IsWalkable(p + new Vector3(step * 1.5f, 0, 0)) || grid.IsWalkable(p - new Vector3(step * 1.5f, 0, 0)) ||
                                        grid.IsWalkable(p + new Vector3(0, 0, step * 1.5f)) || grid.IsWalkable(p - new Vector3(0, 0, step * 1.5f));
                            c = edge ? Color.Lerp(c, new Color(0.85f, 0.75f, 0.55f), 0.55f) : c * 0.45f;
                        }
                    }
                    // gritty grade: a little desaturated, darker toward the rim
                    float g = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    c = Color.Lerp(new Color(g, g, g), c, 0.8f);
                    // fog of war over what hasn't been explored
                    float seen = Exploration.At(new Vector3(wx, 0f, wz));
                    if (seen < 1f) c = Color.Lerp(Fog(wx, wz), c, seen);
                    c *= Mathf.Lerp(1.05f, 0.55f, r * r * r);
                    float alpha = Mathf.Clamp01((1f - r) * half * 0.9f); // ~1 px anti-aliased edge
                    buf[i] = new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), (byte)(alpha * 255));
                }
            tex.SetPixels32(buf);
            tex.Apply(false);
            return tex;
        }

        static bool lastReveal;

        /// <summary>The unexplored color: a dark, faintly cloudy murk.</summary>
        static Color Fog(float x, float z)
        {
            float n = Mathf.PerlinNoise(x * 0.11f + 7f, z * 0.11f + 3f) * 0.6f + Mathf.PerlinNoise(x * 0.37f, z * 0.37f) * 0.4f;
            return new Color(0.07f, 0.065f, 0.075f) * (0.7f + n * 0.7f);
        }

        static Texture2D fogged;
        static int foggedVersion = -1;
        static bool foggedUnderground, foggedReveal;

        /// <summary>The whole map (world or dungeon level) with fog over the unexplored parts, for the M window.</summary>
        public static Texture2D FoggedMap(bool underground)
        {
            var map = underground ? Dungeon.MapTexture : GameManager.I.World.MapTexture;
            if (map == null) return Texture2D.blackTexture;
            if (fogged != null && fogged.width == map.width && fogged.height == map.height && foggedVersion == Exploration.Version &&
                foggedUnderground == underground && foggedReveal == AdminTools.RevealMap) return fogged;
            if (fogged == null || fogged.width != map.width || fogged.height != map.height)
                fogged = new Texture2D(map.width, map.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "FoggedMap" };
            var src = map.GetPixels32();
            Vector3 origin = underground ? Dungeon.Origin : Vector3.zero;
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    int i = y * map.width + x;
                    float seen = Exploration.At(new Vector3(origin.x + x + 0.5f, 0f, origin.z + y + 0.5f));
                    if (seen >= 1f) continue;
                    src[i] = Color.Lerp(Fog(origin.x + x, origin.z + y), src[i], seen);
                }
            fogged.SetPixels32(src);
            fogged.Apply(false);
            foggedVersion = Exploration.Version;
            foggedUnderground = underground;
            foggedReveal = AdminTools.RevealMap;
            return fogged;
        }

        /// <summary>A small white arrowhead pointing up, for the hero's marker (rotated to the hero's facing).</summary>
        public static Texture2D Arrow
        {
            get
            {
                if (arrow != null) return arrow;
                const int n = 32;
                arrow = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "MinimapArrow" };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        // Triangle: tip at the top center, base at the bottom, with a notch in the base.
                        float fx = (x + 0.5f) / n - 0.5f, fy = (y + 0.5f) / n;
                        float halfWidth = (1f - fy) * 0.45f;
                        bool inside = Mathf.Abs(fx) < halfWidth && fy > 0.06f && fy < 0.95f && !(fy < 0.3f && Mathf.Abs(fx) < (0.3f - fy) * 0.8f);
                        float edge = Mathf.Abs(fx) / Mathf.Max(0.001f, halfWidth);
                        byte a = inside ? (byte)255 : (byte)0;
                        byte shade = (byte)(inside ? (edge > 0.75f || fy < 0.12f ? 30 : 255) : 0); // dark outline
                        px[y * n + x] = new Color32(shade, shade, shade, a);
                    }
                arrow.SetPixels32(px);
                arrow.Apply();
                return arrow;
            }
        }
    }
}
