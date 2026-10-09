using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shadowfall
{
    /// <summary>
    /// The look of the ground. The world generator marks which surface each tile has (grass, dirt, cobblestone...);
    /// this turns that into splat control maps for the Shadowfall/Terrain shader, with smooth, noisy edges and
    /// curving roads, builds the ground mesh (with carved lake beds), the water and the grass blades.
    /// Purely visual: nothing here touches the walkability grid.
    /// </summary>
    public class GroundSurface
    {
        public const int Grass = 0, Forest = 1, Dry = 2, Dead = 3, Dirt = 4, Cobble = 5, Gravel = 6, Sand = 7, Layers = 8;
        static readonly string[] LayerNames = { "grass", "forest", "dry", "dead", "dirt", "cobble", "gravel", "sand" };

        /// <summary>Average color of each layer's texture (printed by tools/art/make_ground.py), used for grass blades.</summary>
        static readonly Color[] Avg =
        {
            new Color(0.269f, 0.352f, 0.150f),
            new Color(0.209f, 0.263f, 0.100f),
            new Color(0.478f, 0.430f, 0.211f),
            new Color(0.278f, 0.282f, 0.204f),
            new Color(0.381f, 0.279f, 0.167f),
            new Color(0.446f, 0.424f, 0.389f),
            new Color(0.375f, 0.361f, 0.336f),
            new Color(0.558f, 0.494f, 0.353f),
        };

        const int R = 3;            // control map pixels per tile (3 since the world grew to 576 tiles: 1728 px a side)
        const float WaterLevel = -0.15f;

        readonly int W, H, CW, CH;
        readonly float[] tile;      // W*H*Layers weights
        readonly Color[] tint;      // W*H, 1 = neutral
        readonly List<List<Vector2>> roads = new List<List<Vector2>>();
        readonly List<Vector3> lakes = new List<Vector3>(); // x, z, radius
        readonly List<RectInt> noRoads = new List<RectInt>();
        Color32[] c0, c1;
        float[] roadW;              // CW*CH road weight

        public static bool Supported => Shader.Find("Shadowfall/Terrain") != null || Resources.Load<Shader>("Shaders/ShadowfallTerrain") != null;

        public GroundSurface(int w, int h)
        {
            W = w; H = h; CW = W * R; CH = H * R;
            tile = new float[W * H * Layers];
            tint = new Color[W * H];
            for (int i = 0; i < W * H; i++) { tile[i * Layers] = 1f; tint[i] = Color.white; }
        }

        // ------------------------------------------------------------------ painting API

        /// <summary>Blends tile (x, y) toward <paramref name="layer"/> by <paramref name="amount"/> (0..1).</summary>
        public void Set(int x, int y, int layer, float amount = 1f)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            amount = Mathf.Clamp01(amount);
            int b = (y * W + x) * Layers;
            for (int i = 0; i < Layers; i++) tile[b + i] *= 1f - amount;
            tile[b + layer] += amount;
        }

        /// <summary>Replaces the tile's weights (they are normalized).</summary>
        public void SetWeights(int x, int y, float[] w)
        {
            int b = (y * W + x) * Layers;
            float sum = 0f;
            for (int i = 0; i < Layers; i++) sum += w[i];
            for (int i = 0; i < Layers; i++) tile[b + i] = sum > 0f ? w[i] / sum : (i == 0 ? 1f : 0f);
        }

        /// <summary>Blends tile (x, y) toward the weights <paramref name="w"/> (normalized here) by <paramref name="amount"/>.</summary>
        public void Blend(int x, int y, float[] w, float amount)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || amount <= 0f) return;
            amount = Mathf.Clamp01(amount);
            float sum = 0f;
            for (int i = 0; i < Layers; i++) sum += w[i];
            if (sum <= 0f) return;
            int b = (y * W + x) * Layers;
            for (int i = 0; i < Layers; i++) tile[b + i] = tile[b + i] * (1f - amount) + w[i] / sum * amount;
        }

        public void Tint(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            tint[y * W + x] *= c;
        }

        public void AddRoad(List<Vector2> centerline) => roads.Add(centerline);
        public void AddLake(Vector2 center, float radius) => lakes.Add(new Vector3(center.x, center.y, radius));
        /// <summary>Roads are not drawn inside these rectangles (the cobbled towns).</summary>
        public void ExcludeRoads(RectInt r) => noRoads.Add(r);

        // ------------------------------------------------------------------ baking

        public void Bake()
        {
            Current = this;
            BakeRoads();
            c0 = new Color32[CW * CH];
            c1 = new Color32[CW * CH];
            var w = new float[Layers];
            for (int py = 0; py < CH; py++)
            {
                float wz = (py + 0.5f) / R;
                for (int px = 0; px < CW; px++)
                {
                    float wx = (px + 0.5f) / R;
                    // Domain warp so tile edges become organic blobs instead of blurry squares.
                    float ox = (Mathf.PerlinNoise(wx * 0.23f, wz * 0.23f) - 0.5f) * 1.6f;
                    float oz = (Mathf.PerlinNoise(wx * 0.23f + 41.3f, wz * 0.23f + 17.9f) - 0.5f) * 1.6f;
                    SampleTiles(wx + ox, wz + oz, w);

                    float road = roadW[py * CW + px];
                    if (road > 0f)
                        for (int i = 0; i < Layers; i++) w[i] = Mathf.Lerp(w[i], i == Dirt ? 1f : 0f, road);

                    float sum = 0f;
                    for (int i = 0; i < Layers; i++) sum += w[i];
                    float inv = sum > 0f ? 255f / sum : 0f;
                    int idx = py * CW + px;
                    c0[idx] = new Color32((byte)(w[0] * inv), (byte)(w[1] * inv), (byte)(w[2] * inv), (byte)(w[3] * inv));
                    c1[idx] = new Color32((byte)(w[4] * inv), (byte)(w[5] * inv), (byte)(w[6] * inv), (byte)(w[7] * inv));
                }
            }
        }

        void SampleTiles(float x, float z, float[] outW)
        {
            float fx = Mathf.Clamp(x - 0.5f, 0f, W - 1.001f), fz = Mathf.Clamp(z - 0.5f, 0f, H - 1.001f);
            int x0 = (int)fx, z0 = (int)fz, x1 = Mathf.Min(x0 + 1, W - 1), z1 = Mathf.Min(z0 + 1, H - 1);
            float tx = fx - x0, tz = fz - z0;
            int a = (z0 * W + x0) * Layers, b = (z0 * W + x1) * Layers, c = (z1 * W + x0) * Layers, d = (z1 * W + x1) * Layers;
            for (int i = 0; i < Layers; i++)
                outW[i] = Mathf.Lerp(Mathf.Lerp(tile[a + i], tile[b + i], tx), Mathf.Lerp(tile[c + i], tile[d + i], tx), tz);
        }

        void BakeRoads()
        {
            var dist = new float[CW * CH];
            for (int i = 0; i < dist.Length; i++) dist[i] = 99f;
            const float reach = 3.5f;
            foreach (var line in roads)
                for (int s = 0; s + 1 < line.Count; s++)
                {
                    Vector2 a = line[s], b = line[s + 1], ab = b - a;
                    float len2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
                    int xMin = Mathf.Max(0, (int)((Mathf.Min(a.x, b.x) - reach) * R)), xMax = Mathf.Min(CW - 1, (int)((Mathf.Max(a.x, b.x) + reach) * R));
                    int yMin = Mathf.Max(0, (int)((Mathf.Min(a.y, b.y) - reach) * R)), yMax = Mathf.Min(CH - 1, (int)((Mathf.Max(a.y, b.y) + reach) * R));
                    for (int py = yMin; py <= yMax; py++)
                        for (int px = xMin; px <= xMax; px++)
                        {
                            var p = new Vector2((px + 0.5f) / R, (py + 0.5f) / R);
                            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                            float d = (p - (a + ab * t)).magnitude;
                            int i = py * CW + px;
                            if (d < dist[i]) dist[i] = d;
                        }
                }

            roadW = new float[CW * CH];
            for (int py = 0; py < CH; py++)
                for (int px = 0; px < CW; px++)
                {
                    float d = dist[py * CW + px];
                    if (d > reach) continue;
                    float wx = (px + 0.5f) / R, wz = (py + 0.5f) / R;
                    bool town = false;
                    foreach (var nr in noRoads) if (wx >= nr.xMin && wx < nr.xMax && wz >= nr.yMin && wz < nr.yMax) { town = true; break; }
                    if (town) continue;
                    // Wobbly edges: the half-width varies along the road, plus fine noise.
                    float half = 1.9f + (Mathf.PerlinNoise(wx * 0.21f, wz * 0.21f) - 0.5f) * 1.1f
                                      + (Mathf.PerlinNoise(wx * 1.3f + 7f, wz * 1.3f) - 0.5f) * 0.5f;
                    roadW[py * CW + px] = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half - 0.8f, half + 0.5f, d));
                }
        }

        // ------------------------------------------------------------------ queries (after Bake)

        int Pix(float x, float z) =>
            Mathf.Clamp((int)(z * R), 0, CH - 1) * CW + Mathf.Clamp((int)(x * R), 0, CW - 1);

        public float RoadWeight(float x, float z) => roadW != null ? roadW[Pix(x, z)] : 0f;

        public float Weight(float x, float z, int layer)
        {
            int i = Pix(x, z);
            var c = layer < 4 ? c0[i] : c1[i];
            switch (layer & 3)
            {
                case 0: return c.r / 255f;
                case 1: return c.g / 255f;
                case 2: return c.b / 255f;
                default: return c.a / 255f;
            }
        }

        /// <summary>How much grass grows here (0..1).</summary>
        public float GrassAmount(float x, float z)
        {
            var a = c0[Pix(x, z)];
            return (a.r * 1f + a.g * 0.85f + a.b * 0.8f + a.a * 0.65f) / 255f;
        }

        /// <summary>The ground's average color at a point, including the large-scale tint.</summary>
        public Color ColorAt(float x, float z)
        {
            int i = Pix(x, z);
            var a = c0[i];
            var b = c1[i];
            var c = (Avg[0] * a.r + Avg[1] * a.g + Avg[2] * a.b + Avg[3] * a.a +
                     Avg[4] * b.r + Avg[5] * b.g + Avg[6] * b.b + Avg[7] * b.a) / 255f;
            var t = tint[Mathf.Clamp((int)z, 0, H - 1) * W + Mathf.Clamp((int)x, 0, W - 1)];
            return c * t;
        }

        // ------------------------------------------------------------------ building

        public GameObject BuildGround(Transform parent)
        {
            var shader = Resources.Load<Shader>("Shaders/ShadowfallTerrain");
            if (shader == null) shader = Shader.Find("Shadowfall/Terrain");
            if (shader == null) return null;

            var mat = new Material(shader) { name = "Terrain" };
            mat.SetTexture("_Control0", MakeTex(c0, CW, CH, "Control0"));
            mat.SetTexture("_Control1", MakeTex(c1, CW, CH, "Control1"));
            var tintPx = new Color32[W * H];
            for (int i = 0; i < tintPx.Length; i++) tintPx[i] = tint[i] * 0.5f;
            mat.SetTexture("_Tint", MakeTex(tintPx, W, H, "Tint"));
            for (int i = 0; i < Layers; i++)
            {
                var t = Resources.Load<Texture2D>("Ground/" + LayerNames[i]);
                if (t != null) { t.wrapMode = TextureWrapMode.Repeat; mat.SetTexture("_L" + i, t); }
            }
            mat.SetFloat("_Tiling", 5f);
            mat.SetVector("_WorldSize", new Vector4(W, H, 0, 0));

            // In 16x16 tile chunks: the parts off screen are culled, and (as forward rendering picks the brightest lights
            // per object) each patch of ground is lit per pixel by the lanterns next to it; with big chunks the other
            // lanterns fell back to per-vertex light and left blotchy patches on the ground at night.
            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            const int chunk = 16;
            for (int cz = 0; cz < H; cz += chunk)
                for (int cx = 0; cx < W; cx += chunk)
                {
                    var part = new GameObject("GroundChunk");
                    part.transform.SetParent(go.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = BuildMesh(cx, cz, Mathf.Min(chunk, W - cx), Mathf.Min(chunk, H - cz));
                    var mr = part.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                }
            return go;
        }

        static Texture2D MakeTex(Color32[] px, int w, int h, string name)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        Mesh BuildMesh(int x0, int z0, int w, int h)
        {
            int vw = w + 1, vh = h + 1;
            var verts = new Vector3[vw * vh];
            for (int z = 0; z < vh; z++)
                for (int x = 0; x < vw; x++)
                    verts[z * vw + x] = new Vector3(x0 + x, LakeDepth(x0 + x, z0 + z), z0 + z);
            var tris = new int[w * h * 6];
            int k = 0;
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    int i = z * vw + x;
                    tris[k++] = i; tris[k++] = i + vw; tris[k++] = i + 1;
                    tris[k++] = i + 1; tris[k++] = i + vw; tris[k++] = i + vw + 1;
                }
            var mesh = new Mesh { name = "Ground", indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>Lake beds slope down below the water surface; everywhere else the ground is flat at y = 0.</summary>
        float LakeDepth(float x, float z)
        {
            float y = 0f;
            foreach (var l in lakes)
            {
                if (Mathf.Abs(x - l.x) > l.z + 3f || Mathf.Abs(z - l.y) > l.z + 3f) continue; // too far to matter
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(l.x, l.y));
                float edge = l.z + (Mathf.PerlinNoise(x * 0.3f, z * 0.3f) - 0.5f) * 3f;
                float depth = Mathf.Clamp01((edge - d) / 2.5f) * 1.1f;
                y = Mathf.Min(y, -depth);
            }
            return y;
        }

        public void BuildWater(Transform parent)
        {
            var shader = Resources.Load<Shader>("Shaders/ShadowfallWater");
            if (shader == null) shader = Shader.Find("Shadowfall/Water");
            if (shader == null) return;
            var mat = new Material(shader) { name = "Water" };
            var normal = Resources.Load<Texture2D>("Ground/water_normal");
            if (normal != null) { normal.wrapMode = TextureWrapMode.Repeat; mat.SetTexture("_Normal", normal); }

            foreach (var l in lakes)
            {
                float s = l.z + 4f;
                var mesh = new Mesh { name = "Water" };
                mesh.vertices = new[] { new Vector3(-s, 0, -s), new Vector3(-s, 0, s), new Vector3(s, 0, s), new Vector3(s, 0, -s) };
                mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                var tan = new Vector4(1, 0, 0, -1);
                mesh.tangents = new[] { tan, tan, tan, tan };
                mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.RecalculateBounds();
                var go = new GameObject("Water");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(l.x, WaterLevel, l.y);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ------------------------------------------------------------------ grass

        /// <summary>
        /// Thousands of vertex-colored grass blades, in 16x16 tile chunks so off-screen ones are culled.
        /// Blades take the color of the ground they grow from, so they blend in instead of looking pasted on.
        /// </summary>
        /// <summary>All grass chunks (hidden on Low graphics).</summary>
        public static GameObject GrassRoot { get; private set; }
        /// <summary>The overworld's ground (for what a mount's hooves kick up).</summary>
        public static GroundSurface Current { get; private set; }

        public void BuildGrass(Transform parent, WorldGrid grid, int seed)
        {
            var shader = Resources.Load<Shader>("Shaders/ShadowfallGrass");
            if (shader == null) shader = Shader.Find("Shadowfall/Grass");
            if (shader == null) return;
            var mat = new Material(shader) { name = "Grass" };
            var rng = new System.Random(seed);
            var root = new GameObject("Grass").transform;
            GrassRoot = root.gameObject;
            GameSettings.Apply();
            root.SetParent(parent, false);
            root.gameObject.AddComponent<GrassWind>();

            const int chunk = 16;
            var verts = new List<Vector3>(20000);
            var cols = new List<Color32>(20000);
            var tris = new List<int>(20000);
            for (int cz = 0; cz < H; cz += chunk)
                for (int cx = 0; cx < W; cx += chunk)
                {
                    verts.Clear(); cols.Clear(); tris.Clear();
                    for (int z = cz; z < cz + chunk && z < H; z++)
                        for (int x = cx; x < cx + chunk && x < W; x++)
                        {
                            if (x < 4 || z < 4 || x >= W - 4 || z >= H - 4 || grid.IsBlocked(x, z)) continue;
                            // Patchy meadows: dense clumps and bare spots.
                            float patch = Mathf.Clamp01(Mathf.PerlinNoise(x * 0.09f + 3.1f, z * 0.09f + 8.7f) * 1.7f - 0.35f);
                            // Thinner far from the towns: the world is large and grass is mostly seen up close in town and on the roads.
                            float far = WorldGenerator.FarFromTowns(x, z);
                            float keep = far < 60f ? 1f : Mathf.Lerp(1f, 0.35f, (far - 60f) / 80f);
                            int tufts = (int)(5f * patch * keep + rng.NextDouble());
                            for (int t = 0; t < tufts; t++)
                            {
                                float px = x + (float)rng.NextDouble(), pz = z + (float)rng.NextDouble();
                                float amount = GrassAmount(px, pz) * (1f - RoadWeight(px, pz) * 1.4f);
                                if (amount <= 0.2f || rng.NextDouble() > amount) continue;
                                Tuft(px, pz, rng, verts, cols, tris);
                            }
                        }
                    if (verts.Count == 0) continue;
                    var mesh = new Mesh { name = "Grass", indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                    mesh.SetVertices(verts);
                    mesh.SetColors(cols);
                    mesh.SetTriangles(tris, 0);
                    mesh.RecalculateBounds();
                    var b = mesh.bounds; b.Expand(new Vector3(0.6f, 0.2f, 0.6f)); mesh.bounds = b; // room for the wind sway
                    mesh.UploadMeshData(true);
                    var go = new GameObject("GrassChunk");
                    GrassWind.Chunks.Add((go, new Vector2(cx + chunk * 0.5f, cz + chunk * 0.5f)));
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = true;
                }
        }

        void Tuft(float x, float z, System.Random rng, List<Vector3> verts, List<Color32> cols, List<int> tris)
        {
            float dry = Weight(x, z, Dry), dead = Weight(x, z, Dead), forest = Weight(x, z, Forest);
            float hMin = 0.28f + dry * 0.2f - forest * 0.05f, hMax = 0.55f + dry * 0.3f - dead * 0.1f;
            var ground = ColorAt(x, z);
            int blades = 3 + rng.Next(4);
            for (int i = 0; i < blades; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = (float)rng.NextDouble() * 0.16f;
                var root = new Vector3(x + Mathf.Cos(a) * r, 0f, z + Mathf.Sin(a) * r);
                float h = Mathf.Lerp(hMin, hMax, (float)rng.NextDouble());
                float w = 0.035f + (float)rng.NextDouble() * 0.035f;
                float face = (float)rng.NextDouble() * Mathf.PI * 2f;
                var side = new Vector3(Mathf.Cos(face), 0, Mathf.Sin(face)) * w;
                // lean outward from the tuft center
                var lean = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * h * (0.15f + (float)rng.NextDouble() * 0.3f);

                float v = 0.85f + (float)rng.NextDouble() * 0.3f;
                var baseC = ground * 0.55f * v;
                var tipC = (ground * 1.25f + new Color(0.06f, 0.06f, 0.0f)) * v;
                int s = verts.Count;
                verts.Add(root - side);
                verts.Add(root + side);
                verts.Add(root + Vector3.up * h + lean);
                var bc = (Color32)new Color(baseC.r, baseC.g, baseC.b, 0f);
                cols.Add(bc);
                cols.Add(bc);
                cols.Add(new Color(tipC.r, tipC.g, tipC.b, 1f));
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
            }
        }
    }

    /// <summary>Feeds the hero's position to the grass shader so blades bend out of the way.</summary>
    /// <summary>Also draws grass only near the hero (Settings > Graphics > Grass: Near 30, Far 60 paces), a slice of chunks a frame.</summary>
    public class GrassWind : MonoBehaviour
    {
        static readonly int PlayerPos = Shader.PropertyToID("_SfPlayerPos");
        public static readonly List<(GameObject go, Vector2 center)> Chunks = new List<(GameObject, Vector2)>();
        int cursor;

        void Update()
        {
            var p = Player.I != null ? Player.I.transform.position : new Vector3(-999f, 0f, -999f);
            Shader.SetGlobalVector(PlayerPos, p);
            if (Chunks.Count == 0) return;
            // the far corner of a 16-tile chunk is ~11.3 from its centre
            float reach = GameSettings.GrassDistance + 11.3f, reach2 = reach * reach;
            int n = Mathf.Max(1, Chunks.Count / 10);
            for (int i = 0; i < n; i++)
            {
                if (cursor >= Chunks.Count) cursor = 0;
                var (go, c) = Chunks[cursor++];
                if (go == null) continue;
                float dx = c.x - p.x, dz = c.y - p.z;
                bool show = Player.I == null || dx * dx + dz * dz < reach2;
                if (go.activeSelf != show) go.SetActive(show);
            }
        }
    }
}
