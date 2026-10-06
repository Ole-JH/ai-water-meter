using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Builds the whole world procedurally from a fixed seed: ground texture, town, zones,
    /// gathering nodes and NPCs. Monsters are spawned and simulated by the server (see /server).
    /// </summary>
    public class WorldGenerator
    {
        public const int W = 160, H = 160;
        public const int Seed = 20261006;
        public static readonly RectInt Town = new RectInt(58, 58, 45, 45); // cells 58..102
        public static readonly RectInt Crypt = new RectInt(68, 4, 25, 19);  // cells 68..92, 4..22

        public Texture2D MapTexture { get; private set; }
        public Vector3 SpawnPoint => new Vector3(80.5f, 0f, 77.5f);

        WorldGrid grid;
        Color[] pixels;
        bool[] reserved; // no trees / rocks here (roads, town, water)
        Transform root, nodes, npcs, deco;
        GroundSurface surface;    // splat-mapped ground, grass and water (visual only)
        bool fancyGround;         // terrain shaders available
        bool art;                 // CC0 models available (falls back to primitives if not)
        System.Random vr;         // visual-only randomness: never touches the layout RNG, so the
                                  // walkability grid (and the server's world hash) stays the same

        float VR(float a, float b) => a + (float)vr.NextDouble() * (b - a);
        string Pick(params string[] options) => options[vr.Next(options.Length)];

        GameObject Art(string path, Vector3 pos, float size, ArtLibrary.Fit fit = ArtLibrary.Fit.Height, float yaw = 0f, bool shadows = true) =>
            art ? ArtLibrary.Spawn(path, deco, pos, size, fit, yaw, shadows, true, true) : null;

        GameObject ArtBox(string path, Vector3 pos, Vector3 size, float yaw = 0f) =>
            art ? ArtLibrary.SpawnBox(path, deco, pos, size, yaw) : null;

        /// <summary>How grassy the ground is at a point (0..1), for ambient critters.</summary>
        public float GrassAt(Vector3 p) => fancyGround ? surface.GrassAmount(p.x, p.z) : 0.5f;

        public static bool InTown(Vector3 p) =>
            p.x >= Town.xMin && p.x < Town.xMax && p.z >= Town.yMin && p.z < Town.yMax;

        public static bool InCrypt(Vector3 p) =>
            p.x >= Crypt.xMin && p.x < Crypt.xMax && p.z >= Crypt.yMin && p.z < Crypt.yMax;

        public static string ZoneAt(Vector3 p)
        {
            if (InTown(p)) return "Hollowmere Village";
            if (InCrypt(p)) return "Crypt of the Lich";
            float dx = p.x - 80f, dz = p.z - 80f;
            if (Mathf.Abs(dz) > Mathf.Abs(dx)) return dz > 0 ? "Whisperwood" : "Forsaken Graveyard";
            return dx > 0 ? "Goblin Encampment" : "Ironvein Quarry";
        }

        public void Generate()
        {
            var oldState = Random.state;
            Random.InitState(Seed);
            art = ArtLibrary.Available;
            vr = new System.Random(Seed ^ 0x5eed);

            grid = new WorldGrid(W, H);
            pixels = new Color[W * H];
            reserved = new bool[W * H];
            root = new GameObject("World").transform;
            nodes = Factory.Empty("Resources", root, Vector3.zero);
            npcs = Factory.Empty("NPCs", root, Vector3.zero);
            deco = Factory.Empty("Decoration", root, Vector3.zero);
            surface = new GroundSurface(W, H);
            surface.ExcludeRoads(Town);
            fancyGround = GroundSurface.Supported;

            PaintBase();
            PaintRoads();
            BuildLake(new Vector2(55, 125), 8f, 0);
            BuildLake(new Vector2(112, 138), 6f, 1);
            BuildBorder();
            BuildTown();
            BuildForest();
            BuildGoblinCamp();
            BuildGraveyard();
            BuildCrypt();
            BuildQuarry();
            surface.Bake();
            ScatterDetail();
            BuildGround();
            if (art) StaticBatchingUtility.Combine(deco.gameObject);

            Random.state = oldState;
        }

        // ------------------------------------------------------------------ painting

        int Idx(int x, int y) => y * W + x;
        void Paint(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) pixels[Idx(x, y)] = c; }
        void Reserve(int x, int y) { if (x >= 0 && y >= 0 && x < W && y < H) reserved[Idx(x, y)] = true; }
        bool Free(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && !reserved[Idx(x, y)] && !grid.IsBlocked(x, y);

        static readonly Color forestC = new Color(0.2f, 0.36f, 0.14f);
        static readonly Color steppeC = new Color(0.48f, 0.45f, 0.24f);
        static readonly Color graveC = new Color(0.27f, 0.3f, 0.24f);
        static readonly Color quarryC = new Color(0.45f, 0.4f, 0.34f);
        static readonly Color dirtC = new Color(0.45f, 0.35f, 0.22f);
        static readonly Color cobbleC = new Color(0.52f, 0.5f, 0.45f);
        static readonly Color waterC = new Color(0.15f, 0.32f, 0.55f);

        void PaintBase()
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = x - 80f, dz = y - 80f;
                    float wn = Mathf.Pow(Mathf.Max(0, dz), 3), ws = Mathf.Pow(Mathf.Max(0, -dz), 3);
                    float we = Mathf.Pow(Mathf.Max(0, dx), 3), ww = Mathf.Pow(Mathf.Max(0, -dx), 3);
                    float sum = wn + ws + we + ww + 0.0001f;
                    Color c = (forestC * wn + graveC * ws + steppeC * we + quarryC * ww) / sum;
                    if (sum < 1f) c = Color.Lerp(new Color(0.3f, 0.42f, 0.2f), c, sum);
                    float n = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.25f + Mathf.PerlinNoise(x * 0.6f, y * 0.6f) * 0.1f;
                    pixels[Idx(x, y)] = c * (0.85f + n);
                    BaseSurface(x, y, wn / sum, ws / sum, we / sum, ww / sum);
                }

            // The village: cobbled cross streets and central square, grassy yards with trodden dirt elsewhere.
            for (int y = Town.yMin; y < Town.yMax; y++)
                for (int x = Town.xMin; x < Town.xMax; x++)
                {
                    Reserve(x, y);
                    if (IsTownStreet(x, y))
                    {
                        float n = Mathf.PerlinNoise(x * 0.9f, y * 0.9f) * 0.15f;
                        Paint(x, y, cobbleC * (0.9f + n));
                        surface.Set(x, y, GroundSurface.Cobble);
                        if (Mathf.PerlinNoise(x * 0.21f + 9f, y * 0.21f) > 0.66f) surface.Set(x, y, GroundSurface.Dirt, 0.45f); // worn patches
                    }
                    else
                    {
                        surface.Set(x, y, GroundSurface.Grass, 0.85f);
                        float worn = Mathf.PerlinNoise(x * 0.17f + 3f, y * 0.17f + 11f);
                        bool lane = x <= Town.xMin + 2 || x >= Town.xMax - 3 || y <= Town.yMin + 2 || y >= Town.yMax - 3; // path inside the wall
                        if (lane || worn > 0.62f) surface.Set(x, y, GroundSurface.Dirt, lane ? 0.75f : 0.5f);
                        Paint(x, y, Color.Lerp(new Color(0.3f, 0.4f, 0.2f), dirtC, lane ? 0.7f : 0.2f));
                    }
                }
        }

        /// <summary>Cobbled parts of the village: the two cross streets between the gates and the central square.</summary>
        static bool IsTownStreet(int x, int y)
        {
            bool cross = (x >= 78 && x <= 82) || (y >= 78 && y <= 82);
            bool square = x >= 72 && x <= 89 && y >= 72 && y <= 89;
            return cross || square;
        }

        /// <summary>Which ground textures a tile gets, from how much it belongs to each zone.</summary>
        void BaseSurface(int x, int y, float north, float south, float east, float west)
        {
            float a = Mathf.PerlinNoise(x * 0.07f + 11f, y * 0.07f + 5f);
            float b = Mathf.PerlinNoise(x * 0.12f + 31f, y * 0.12f + 71f);
            var w = new float[GroundSurface.Layers];
            // Whisperwood: meadow grass giving way to dark forest floor deeper in.
            float deep = Mathf.Clamp01((y - 100f) / 40f);
            float forest = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((a - 0.45f) * 4f + deep));
            w[GroundSurface.Grass] += north * (1f - forest);
            w[GroundSurface.Forest] += north * forest;
            // Forsaken Graveyard: dead grass with bare dirt.
            float bare = b > 0.62f ? 0.6f : 0f;
            w[GroundSurface.Dead] += south * (1f - bare);
            w[GroundSurface.Dirt] += south * bare;
            // Goblin Encampment: dry steppe with trampled dirt.
            float trampled = b > 0.6f ? 0.7f : 0f;
            w[GroundSurface.Dry] += east * (1f - trampled);
            w[GroundSurface.Dirt] += east * trampled;
            // Ironvein Quarry: gravel and rock with scrubby dry grass.
            float rock = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((a - 0.35f) * 3f + Mathf.Clamp01((40f - x) / 30f)));
            w[GroundSurface.Gravel] += west * rock;
            w[GroundSurface.Dry] += west * (1f - rock) * 0.6f;
            w[GroundSurface.Dirt] += west * (1f - rock) * 0.4f;
            // Green meadow around the village.
            float meadow = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(28f, 44f, Vector2.Distance(new Vector2(x, y), new Vector2(80f, 80f))));
            for (int i = 0; i < w.Length; i++) w[i] *= 1f - meadow;
            w[GroundSurface.Grass] += meadow;
            surface.SetWeights(x, y, w);
            float t = 0.9f + Mathf.PerlinNoise(x * 0.045f + 3f, y * 0.045f) * 0.2f;
            surface.Tint(x, y, new Color(t, t * (0.97f + b * 0.06f), t * 0.97f));
        }

        void PaintRoads()
        {
            // Smooth centerlines for the ground shader (the tiles below stay the walkable/reserved road).
            var n = new System.Collections.Generic.List<Vector2>();
            for (int i = Town.yMax - 1; i < H - 6; i++) n.Add(new Vector2(Mathf.Round(80 + Mathf.Sin(i * 0.08f) * 4f) + 0.5f, i + 0.5f));
            var so = new System.Collections.Generic.List<Vector2>();
            for (int i = Town.yMin; i > 6; i--) so.Add(new Vector2(Mathf.Round(80 + Mathf.Sin(i * 0.1f) * 3f) + 0.5f, i + 0.5f));
            var e = new System.Collections.Generic.List<Vector2>();
            for (int i = Town.xMax - 1; i < W - 6; i++) e.Add(new Vector2(i + 0.5f, Mathf.Round(80 + Mathf.Sin(i * 0.09f) * 4f) + 0.5f));
            var wst = new System.Collections.Generic.List<Vector2>();
            for (int i = Town.xMin; i > 6; i--) wst.Add(new Vector2(i + 0.5f, Mathf.Round(80 + Mathf.Sin(i * 0.07f) * 4f) + 0.5f));
            foreach (var line in new[] { n, so, e, wst }) surface.AddRoad(Smooth(line));

            // Four roads leading out of the gates, gently meandering.
            for (int i = Town.yMax; i < H - 6; i++) RoadDot(80 + Mathf.Sin(i * 0.08f) * 4f, i);         // north
            for (int i = Town.yMin; i > 6; i--) RoadDot(80 + Mathf.Sin(i * 0.1f) * 3f, i);              // south
            for (int i = Town.xMax; i < W - 6; i++) RoadDot(i, 80 + Mathf.Sin(i * 0.09f) * 4f);         // east
            for (int i = Town.xMin; i > 6; i--) RoadDot(i, 80 + Mathf.Sin(i * 0.07f) * 4f);             // west
        }

        /// <summary>Averages neighbouring points so the rounded tile steps become a smooth curve.</summary>
        static System.Collections.Generic.List<Vector2> Smooth(System.Collections.Generic.List<Vector2> pts)
        {
            var o = new System.Collections.Generic.List<Vector2>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 sum = Vector2.zero; int c = 0;
                for (int j = Mathf.Max(0, i - 3); j <= Mathf.Min(pts.Count - 1, i + 3); j++) { sum += pts[j]; c++; }
                o.Add(sum / c);
            }
            return o;
        }

        void RoadDot(float cx, float cy)
        {
            for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                {
                    int px = Mathf.RoundToInt(cx) + x, py = Mathf.RoundToInt(cy) + y;
                    if (px < 0 || py < 0 || px >= W || py >= H || InTown(new Vector3(px, 0, py))) continue;
                    float n = Mathf.PerlinNoise(px * 0.7f, py * 0.7f) * 0.2f;
                    Paint(px, py, dirtC * (0.9f + n));
                    Reserve(px, py);
                }
        }

        void BuildLake(Vector2 center, float radius, int fishTier)
        {
            int r = Mathf.CeilToInt(radius + 3);
            for (int y = (int)center.y - r; y <= center.y + r; y++)
                for (int x = (int)center.x - r; x <= center.x + r; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float edge = radius + (Mathf.PerlinNoise(x * 0.3f, y * 0.3f) - 0.5f) * 3f;
                    if (d < edge)
                    {
                        Paint(x, y, waterC * (0.9f + Mathf.PerlinNoise(x * 0.5f, y * 0.5f) * 0.2f));
                        grid.SetBlocked(x, y, true);
                        Reserve(x, y);
                        surface.Set(x, y, GroundSurface.Sand);
                        surface.Tint(x, y, new Color(0.55f, 0.6f, 0.6f)); // darker lake bed
                    }
                    else if (d < edge + 1.5f)
                    {
                        Paint(x, y, new Color(0.55f, 0.5f, 0.35f)); // sandy shore
                        Reserve(x, y);
                        surface.Set(x, y, GroundSurface.Sand, 0.85f);
                    }
                }

            surface.AddLake(center, radius);
            Sfx.LoopAt("water_loop", new Vector3(center.x, 0f, center.y), 0.35f, radius + 10f);
            // Water surface plane for a bit of shine (the water shader version is built with the ground)
            if (!fancyGround)
            {
            var water = Factory.Prim(PrimitiveType.Cylinder, deco, new Vector3(center.x, -0.02f, center.y),
                new Vector3(radius * 2f + 1f, 0.01f, radius * 2f + 1f), waterC);
            water.GetComponent<Renderer>().sharedMaterial.SetFloat("_Glossiness", 0.8f);
            }

            // Fishing spots on water cells next to the shore
            int placed = 0;
            for (int a = 0; a < 360 && placed < 4; a += 23)
            {
                var dir = Quaternion.Euler(0, a + Random.Range(0, 15), 0) * Vector3.forward;
                for (float d = radius + 2f; d > 1f; d -= 0.5f)
                {
                    var p = new Vector3(center.x, 0, center.y) + dir * d;
                    var cell = grid.WorldToCell(p);
                    if (!grid.IsBlocked(cell)) continue;
                    // must be within reach of a walkable cell
                    var shore = new Vector3(center.x, 0, center.y) + dir * (d + 1.6f);
                    if (!grid.IsWalkable(shore)) break;
                    ResourceNode.Create(ResourceKind.FishingSpot, fishTier, grid.CellToWorld(cell), nodes);
                    placed++;
                    a += 60;
                    break;
                }
            }
        }

        void BuildBorder()
        {
            var mountain = new Color(0.32f, 0.3f, 0.28f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(W - 1 - x, H - 1 - y));
                    if (edge >= 4) continue;
                    grid.SetBlocked(x, y, true);
                    Paint(x, y, mountain * (0.8f + Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.3f));
                    surface.Set(x, y, GroundSurface.Gravel);
                }
            // Chunky cliffs along the edge
            for (int i = 0; i < W; i += 3)
            {
                Cliff(new Vector3(i + 1.5f, 0, 1.5f));
                Cliff(new Vector3(i + 1.5f, 0, H - 1.5f));
            }
            for (int i = 3; i < H - 3; i += 3)
            {
                Cliff(new Vector3(1.5f, 0, i + 1.5f));
                Cliff(new Vector3(W - 1.5f, 0, i + 1.5f));
            }
        }

        void Cliff(Vector3 p)
        {
            float h = Random.Range(3f, 7f);
            float sx = Random.Range(3.5f, 5f), sz = Random.Range(3.5f, 5f), shade = Random.Range(0.85f, 1.1f), rot = Random.Range(0, 90f);
            if (Art(Pick("Nature/rock_tallA", "Nature/rock_tallC", "Nature/rock_tallF"), p, h, ArtLibrary.Fit.Height, VR(0, 360)) != null)
            {
                Art(Pick("Nature/rock_largeA", "Nature/rock_largeB", "Nature/rock_largeC", "Nature/rock_largeD"), p, Mathf.Max(sx, sz) * 1.2f,
                    ArtLibrary.Fit.Width, VR(0, 360));
                return;
            }
            Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * h * 0.5f, new Vector3(sx, h, sz),
                new Color(0.34f, 0.32f, 0.3f) * shade).transform.rotation = Quaternion.Euler(0, rot, 0);
        }

        // ------------------------------------------------------------------ town

        void BuildTown()
        {
            var wood = new Color(0.42f, 0.3f, 0.18f);
            int x0 = Town.xMin, x1 = Town.xMax - 1, y0 = Town.yMin, y1 = Town.yMax - 1;
            // Palisade with gates at the centre of each side (cells 78..82)
            for (int i = x0; i <= x1; i++)
            {
                if (i >= 78 && i <= 82) continue;
                Palisade(i, y0, wood);
                Palisade(i, y1, wood);
            }
            for (int i = y0 + 1; i < y1; i++)
            {
                if (i >= 78 && i <= 82) continue;
                Palisade(x0, i, wood);
                Palisade(x1, i, wood);
            }
            // Gate towers
            foreach (var g in new[] { new Vector2(77, y0), new Vector2(83, y0), new Vector2(77, y1), new Vector2(83, y1),
                                      new Vector2(x0, 77), new Vector2(x0, 83), new Vector2(x1, 77), new Vector2(x1, 83) })
            {
                if (Art("Buildings/building_tower_A_blue", new Vector3(g.x + 0.5f, 0, g.y + 0.5f), 5.2f, ArtLibrary.Fit.Height, VR(0, 4) * 90f) != null) continue;
                Factory.Prim(PrimitiveType.Cube, deco, new Vector3(g.x + 0.5f, 2f, g.y + 0.5f), new Vector3(1.4f, 4f, 1.4f), wood * 0.85f);
                var torchC = new Color(1f, 0.6f, 0.2f);
                Factory.Prim(PrimitiveType.Sphere, deco, new Vector3(g.x + 0.5f, 4.3f, g.y + 0.5f), Vector3.one * 0.4f, torchC, false, Mat.Glow(torchC));
            }

            House(new RectInt(68, 86, 7, 6), new Color(0.75f, 0.68f, 0.55f), new Color(0.55f, 0.2f, 0.15f), "Buildings/building_tavern_blue", 180f);
            House(new RectInt(86, 86, 7, 6), new Color(0.7f, 0.65f, 0.55f), new Color(0.25f, 0.3f, 0.5f), "Buildings/building_home_B_blue", 180f);
            House(new RectInt(68, 68, 6, 6), new Color(0.72f, 0.62f, 0.5f), new Color(0.3f, 0.45f, 0.25f), "Buildings/building_home_A_green", 0f);
            // Smithy: open-sided shelter
            var smithy = new RectInt(87, 68, 6, 5);
            for (int i = 0; i < 4; i++)
            {
                float px = i % 2 == 0 ? smithy.xMin + 0.5f : smithy.xMax - 0.5f;
                float pz = i < 2 ? smithy.yMin + 0.5f : smithy.yMax - 0.5f;
                if (!art) Factory.Prim(PrimitiveType.Cube, deco, new Vector3(px, 1.5f, pz), new Vector3(0.4f, 3f, 0.4f), wood);
                grid.SetBlocked((int)px, (int)pz, true);
            }
            var forgeC = new Color(1f, 0.35f, 0.05f);
            if (Art("Buildings/building_blacksmith_blue", new Vector3(91.7f, 0, 71.3f), 3.8f, ArtLibrary.Fit.Width, -90f) != null)
            {
                Art("Props/barrel_large", new Vector3(92.4f, 0, 68.6f), 0.9f);
                Art("Props/crates_stacked", new Vector3(87.6f, 0, 68.6f), 1.2f, ArtLibrary.Fit.Height, 20f);
            }
            else
            {
                Factory.Prim(PrimitiveType.Cube, deco, new Vector3(smithy.center.x, 3.1f, smithy.center.y), new Vector3(smithy.width + 0.6f, 0.3f, smithy.height + 0.6f), new Color(0.35f, 0.25f, 0.18f));
                Factory.Prim(PrimitiveType.Cube, deco, new Vector3(91.5f, 0.6f, 71.5f), new Vector3(1.6f, 1.2f, 1.6f), new Color(0.3f, 0.28f, 0.27f));
                Factory.Prim(PrimitiveType.Cube, deco, new Vector3(91.5f, 1.25f, 71.5f), new Vector3(1.2f, 0.1f, 1.2f), forgeC, false, Mat.Glow(forgeC));
            }
            grid.BlockRect(91, 71, 92, 72);

            // Well in the square
            if (Art("Buildings/building_well_blue", new Vector3(80.5f, 0, 82.5f), 2.3f, ArtLibrary.Fit.Width) == null)
            {
                Factory.Prim(PrimitiveType.Cylinder, deco, new Vector3(80.5f, 0.5f, 82.5f), new Vector3(1.8f, 0.5f, 1.8f), new Color(0.5f, 0.48f, 0.45f));
                Factory.Prim(PrimitiveType.Cylinder, deco, new Vector3(80.5f, 1.01f, 82.5f), new Vector3(1.4f, 0.01f, 1.4f), waterC);
                Factory.Prim(PrimitiveType.Cube, deco, new Vector3(80.5f, 2.3f, 82.5f), new Vector3(2.2f, 0.2f, 0.3f), wood);
            }
            grid.BlockRect(80, 82, 80, 82);

            CraftingStation.Create(SkillType.Smithing, new Vector3(89.5f, 0, 70.5f), root);
            CraftingStation.Create(SkillType.Cooking, new Vector3(75.5f, 0, 80.5f), root);

            Npc.Create("Captain Aldric", "Captain of the Guard", NpcRole.QuestGiver, new Vector3(80.5f, 0, 86.5f), new Color(0.6f, 0.15f, 0.12f),
                "Stay sharp, traveller. These are dark days for Hollowmere.", npcs, true);
            Npc.Create("Forester Wren", "Woodcutting & Fishing", NpcRole.QuestGiver, new Vector3(76.5f, 0, 84.5f), new Color(0.25f, 0.45f, 0.2f),
                "The forest provides, if you know how to ask.", npcs);
            Npc.Create("Smith Gorrin", "Blacksmith", NpcRole.QuestGiver, new Vector3(87.5f, 0, 74.5f), new Color(0.35f, 0.3f, 0.28f),
                "Bring me ore and I'll teach you to work it.", npcs, true);
            Npc.Create("Merchant Lysa", "General Goods", NpcRole.Vendor, new Vector3(75.5f, 0, 74.5f), new Color(0.55f, 0.3f, 0.6f),
                "Potions! Fresh potions! I also buy anything you drag out of those monsters.", npcs).SellsAs(VendorKind.General);
            Npc.Create("Sister Mae", "Healer", NpcRole.Healer, new Vector3(85.5f, 0, 84.5f), new Color(0.9f, 0.9f, 0.85f),
                "The Light watches over you, child. Let me tend your wounds.", npcs);
            Npc.Create("Thomas", "Farmer", NpcRole.QuestGiver, new Vector3(71.5f, 0, 82.5f), new Color(0.45f, 0.55f, 0.3f),
                "Morning! Don't mind the mud. Bandits ran off with half my harvest again.", npcs, false, false);
            Npc.Create("Jenkins", "Butler of Automation", NpcRole.QuestGiver, new Vector3(89.5f, 0, 83.5f), new Color(0.08f, 0.08f, 0.1f),
                "Good day. I have taken the liberty of automating the village. Nearly all of it. The rest is merely failing.", npcs, false, false)
                .DressAsButler();

            // Shopkeepers (added later: they don't block tiles, so existing servers' world maps stay valid)
            Npc.Create("Armorer Brann", "Armor", NpcRole.Vendor, new Vector3(85.5f, 0, 71.5f), new Color(0.45f, 0.42f, 0.4f),
                "Helms, mail, boots. Everything a body needs to stay a body.", npcs, true, false, false).SellsAs(VendorKind.Armor);
            Npc.Create("Weaponsmith Hilda", "Weapons", NpcRole.Vendor, new Vector3(85.5f, 0, 76.5f), new Color(0.55f, 0.3f, 0.2f),
                "Looking for something with an edge? You've come to the right woman.", npcs, true, false, false).SellsAs(VendorKind.Weapons);
            Npc.Create("Innkeeper Rosie", "Food & Drink", NpcRole.Vendor, new Vector3(73.5f, 0, 84.5f), new Color(0.75f, 0.45f, 0.35f),
                "Welcome to the Prancing Boar! Sit, eat, drink, and don't start any fights.", npcs, false, true, false).SellsAs(VendorKind.Food);
            Npc.Create("Curio Dealer Vex", "Rings & Amulets", NpcRole.Vendor, new Vector3(71.5f, 0, 78.5f), new Color(0.3f, 0.2f, 0.45f),
                "Trinkets with a past. Some of them even have a future.", npcs, false, true, false).SellsAs(VendorKind.Curios);

            // Market stalls & crates
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(70.5f + i * 2f, 0, 76.5f);
                grid.SetBlocked((int)p.x, (int)p.z, true);
                if (Art(i % 2 == 0 ? "Town/stall-red" : "Town/stall-green", p, 1.8f, ArtLibrary.Fit.Width) != null) continue;
                Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * 0.4f, new Vector3(1.4f, 0.8f, 1f), wood);
                Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * 2f, new Vector3(1.7f, 0.1f, 1.3f), i % 2 == 0 ? new Color(0.7f, 0.2f, 0.2f) : new Color(0.85f, 0.8f, 0.6f));
            }

            BuildOuterTown(wood);

            if (art)
            {
                // Lantern posts around the square and along the streets (visual only).
                var posts = new System.Collections.Generic.List<Vector3>
                    { new Vector3(77.6f, 0, 79.4f), new Vector3(83.4f, 0, 79.4f), new Vector3(77.6f, 0, 85.6f), new Vector3(83.4f, 0, 85.6f) };
                foreach (float d in new[] { 63.5f, 69.5f, 92.5f, 98.5f })
                {
                    posts.Add(new Vector3(77.4f, 0, d));
                    posts.Add(new Vector3(83.6f, 0, d));
                    posts.Add(new Vector3(d, 0, 77.4f));
                    posts.Add(new Vector3(d, 0, 83.6f));
                }
                foreach (var lp in posts)
                {
                    Art("Town/lantern", lp, 2.4f);
                    var l = new GameObject("LanternLight").AddComponent<Light>();
                    l.transform.SetParent(deco, false);
                    l.transform.position = lp + Vector3.up * 2.3f;
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.75f, 0.4f);
                    l.range = 6f;
                    l.intensity = 1.4f;
                    NightLight.Add(l, 0.15f);
                }
                Art("Town/cart", new Vector3(73.5f, 0, 73.0f), 2.2f, ArtLibrary.Fit.Width, 35f);
                Art("Props/barrel_small_stack", new Vector3(69.0f, 0, 76.6f), 1.0f);
                Art("Props/box_stacked", new Vector3(76.4f, 0, 77.0f), 1.0f, ArtLibrary.Fit.Height, 15f);
            }
        }

        /// <summary>The newer, outer part of the village: church, windmill, market hall, homes, a farm plot and a training yard.</summary>
        void BuildOuterTown(Color wood)
        {
            // Visual variety only uses vr (see the class comment); blocking uses fixed rectangles, so the layout stays deterministic.
            House(new RectInt(86, 92, 10, 8), new Color(0.75f, 0.73f, 0.68f), new Color(0.3f, 0.32f, 0.45f), "Buildings/building_church_blue", 180f);
            House(new RectInt(60, 92, 8, 8), new Color(0.72f, 0.65f, 0.55f), new Color(0.5f, 0.3f, 0.2f), "Buildings/building_windmill_blue", 135f);
            House(new RectInt(60, 84, 6, 6), new Color(0.72f, 0.65f, 0.55f), new Color(0.3f, 0.35f, 0.55f), "Buildings/building_home_A_blue", 90f);
            House(new RectInt(60, 69, 7, 7), new Color(0.74f, 0.66f, 0.52f), new Color(0.55f, 0.25f, 0.2f), "Buildings/building_market_blue", 90f);
            House(new RectInt(96, 86, 6, 6), new Color(0.7f, 0.64f, 0.55f), new Color(0.3f, 0.35f, 0.55f), "Buildings/building_home_B_blue", 270f);
            House(new RectInt(96, 68, 6, 6), new Color(0.7f, 0.64f, 0.55f), new Color(0.3f, 0.45f, 0.25f), "Buildings/building_home_A_blue", 270f);

            // Fountain on the north street
            grid.BlockRect(79, 92, 81, 94);
            if (Art("Town/fountain-round", new Vector3(80.5f, 0, 93.5f), 3.2f, ArtLibrary.Fit.Width) == null)
                Factory.Prim(PrimitiveType.Cylinder, deco, new Vector3(80.5f, 0.4f, 93.5f), new Vector3(3f, 0.4f, 3f), new Color(0.55f, 0.53f, 0.5f));

            // Farm plot (south-west): tilled soil with rows of crops behind a fence
            var farm = new RectInt(61, 60, 13, 7);
            for (int y = farm.yMin; y < farm.yMax; y++)
                for (int x = farm.xMin; x < farm.xMax; x++)
                {
                    surface.Set(x, y, GroundSurface.Dirt, 0.95f);
                    surface.Tint(x, y, new Color(0.8f, 0.75f, 0.7f));
                    Paint(x, y, dirtC * 0.8f);
                    bool edge = x == farm.xMin || x == farm.xMax - 1 || y == farm.yMin || y == farm.yMax - 1;
                    if (edge)
                    {
                        if (y == farm.yMax - 1 && (x == 67 || x == 68)) continue; // gap to walk in
                        grid.SetBlocked(x, y, true);
                        bool alongX = y == farm.yMin || y == farm.yMax - 1;
                        if (ArtBox("Town/fence", new Vector3(x + 0.5f, 0, y + 0.5f), new Vector3(0.3f, 1f, 1.02f), alongX ? 90f : 0f) == null)
                            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(x + 0.5f, 0.5f, y + 0.5f), alongX ? new Vector3(1f, 1f, 0.2f) : new Vector3(0.2f, 1f, 1f), wood);
                    }
                    else if (art && (y - farm.yMin) % 2 == 1)
                        Art(Pick("Nature/plant_bush", "Nature/grass_large"), new Vector3(x + 0.5f, 0, y + 0.5f), VR(0.5f, 0.75f), ArtLibrary.Fit.Height, VR(0, 360), false);
                }

            // Training yard (south-east): posts, targets and banners
            for (int y = 60; y < 67; y++)
                for (int x = 87; x < 100; x++)
                    surface.Set(x, y, GroundSurface.Dirt, 0.8f);
            foreach (var px in new[] { 89.5f, 92.5f, 95.5f })
            {
                grid.SetBlocked((int)px, 63, true);
                if (Art("Town/pillar-wood", new Vector3(px, 0, 63.5f), 2f, ArtLibrary.Fit.Height) == null)
                    Factory.Prim(PrimitiveType.Cylinder, deco, new Vector3(px, 1f, 63.5f), new Vector3(0.4f, 1f, 0.4f), wood);
            }
            if (art)
            {
                Art("Town/banner-red", new Vector3(98.5f, 0, 65.5f), 3f, ArtLibrary.Fit.Height, 270f);
                Art("Props/barrel_large", new Vector3(98.5f, 0, 61.5f), 1f);
                Art("Props/crates_stacked", new Vector3(88.0f, 0, 60.8f), 1.1f, ArtLibrary.Fit.Height, 10f);
                // Banners by the north and south gates
                Art("Town/banner-green", new Vector3(76.6f, 0, Town.yMax - 2.5f), 3f, ArtLibrary.Fit.Height, 180f);
                Art("Town/banner-green", new Vector3(84.4f, 0, Town.yMax - 2.5f), 3f, ArtLibrary.Fit.Height, 180f);
                Art("Town/banner-red", new Vector3(76.6f, 0, Town.yMin + 2.5f), 3f, ArtLibrary.Fit.Height, 0f);
                Art("Town/banner-red", new Vector3(84.4f, 0, Town.yMin + 2.5f), 3f, ArtLibrary.Fit.Height, 0f);
            }
        }

        void Palisade(int x, int y, Color wood)
        {
            float shade = Random.Range(0.85f, 1.05f);
            grid.SetBlocked(x, y, true);
            bool alongX = y == Town.yMin || y == Town.yMax - 1;
            // Town/wall-wood is a 1-unit wall piece running along Z.
            if (ArtBox("Town/wall-wood", new Vector3(x + 0.5f, 0, y + 0.5f), new Vector3(0.45f, 2.6f, 1.04f), alongX ? 90f : 0f) != null) return;
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(x + 0.5f, 1.4f, y + 0.5f), new Vector3(0.9f, 2.8f, 0.9f), wood * shade);
        }

        void House(RectInt r, Color wall, Color roof, string model, float yaw)
        {
            var c = new Vector3(r.center.x, 0, r.center.y);
            grid.BlockRect(r.xMin, r.yMin, r.xMax - 1, r.yMax - 1);
            if (Art(model, c, Mathf.Min(r.width, r.height) - 0.4f, ArtLibrary.Fit.Width, yaw) != null)
            {
                // A few props in the yard (inside the blocked footprint).
                Art(Pick("Props/barrel_large", "Props/barrel_small_stack"), new Vector3(r.xMin + 0.5f, 0, r.yMin + 0.5f), 1f, ArtLibrary.Fit.Height, VR(0, 360));
                Art(Pick("Props/crates_stacked", "Props/box_stacked"), new Vector3(r.xMax - 0.5f, 0, r.yMax - 0.5f), 1.1f, ArtLibrary.Fit.Height, VR(0, 360));
                // Warm glow from the windows after dark
                var glow = new GameObject("WindowLight").AddComponent<Light>();
                glow.transform.SetParent(deco, false);
                glow.transform.position = c + Vector3.up * 2.2f;
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.7f, 0.35f);
                glow.range = 7.5f;
                glow.intensity = 1.1f;
                NightLight.Add(glow, 0f);
                return;
            }
            Factory.Prim(PrimitiveType.Cube, deco, c + Vector3.up * 1.5f, new Vector3(r.width, 3f, r.height), wall);
            float side = r.width / 1.414f;
            var roofGo = Factory.Prim(PrimitiveType.Cube, deco, c + Vector3.up * 3f, new Vector3(side, side, r.height + 0.6f), roof);
            roofGo.transform.rotation = Quaternion.Euler(0, 0, 45);
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(c.x, 0.9f, r.yMin - 0.01f), new Vector3(1f, 1.8f, 0.1f), new Color(0.3f, 0.2f, 0.12f));
            var win = new Color(1f, 0.85f, 0.4f);
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(c.x - r.width * 0.3f, 1.8f, r.yMin - 0.01f), new Vector3(0.7f, 0.6f, 0.1f), win, false, Mat.Glow(win * 0.6f));
        }

        // ------------------------------------------------------------------ zones

        void BuildForest()
        {
            for (int y = 96; y < H - 5; y++)
                for (int x = 5; x < W - 5; x++)
                {
                    float dx = x - 80f, dz = y - 80f;
                    if (Mathf.Abs(dz) < Mathf.Abs(dx) * 0.8f) continue;
                    float density = 0.05f + Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.08f;
                    if (Random.value > density || !Free(x, y) || !SpacedFrom(x, y, 1)) continue;
                    int tier = y < 115 ? 0 : y < 136 ? (Random.value < 0.7f ? 1 : 0) : (Random.value < 0.6f ? 2 : 1);
                    ResourceNode.Create(ResourceKind.Tree, tier, new Vector3(x + 0.5f, 0, y + 0.5f), nodes);
                    Paint(x, y, pixels[Idx(x, y)] * 0.7f);
                    surface.Set(x, y, GroundSurface.Forest, 0.6f);
                }
            // Scattered decorative pines elsewhere
            for (int i = 0; i < 260; i++)
            {
                int x = Random.Range(6, W - 6), y = Random.Range(6, 96);
                if (!Free(x, y) || !SpacedFrom(x, y, 2) || InCrypt(new Vector3(x, 0, y))) continue;
                if (ZoneAt(new Vector3(x, 0, y)) == "Ironvein Quarry" && Random.value < 0.7f) continue;
                Pine(new Vector3(x + 0.5f, 0, y + 0.5f), ZoneAt(new Vector3(x, 0, y)) == "Forsaken Graveyard");
                grid.SetBlocked(x, y, true);
            }
        }

        bool SpacedFrom(int x, int y, int r)
        {
            for (int j = -r; j <= r; j++)
                for (int i = -r; i <= r; i++)
                    if (grid.IsBlocked(x + i, y + j)) return false;
            return true;
        }

        void Pine(Vector3 p, bool dead)
        {
            if (art)
            {
                Random.Range(0f, dead ? 180f : 1f); // keep the layout RNG in step with the primitive version
                if (dead) Art(Pick("Graveyard/pine-crooked", "Graveyard/pine-fall-crooked"), p, VR(3.2f, 4.6f), ArtLibrary.Fit.Height, VR(0, 360));
                else Art(Pick("Nature/tree_pineRoundC", "Nature/tree_pineTallB", "Nature/tree_cone_dark", "Nature/tree_tall"), p, VR(4f, 6f), ArtLibrary.Fit.Height, VR(0, 360));
                return;
            }
            var trunk = new Color(0.3f, 0.22f, 0.15f);
            Factory.Prim(PrimitiveType.Cylinder, deco, p + Vector3.up, new Vector3(0.3f, 1f, 0.3f), dead ? new Color(0.2f, 0.18f, 0.16f) : trunk);
            if (dead)
            {
                Factory.Prim(PrimitiveType.Cube, deco, p + new Vector3(0.3f, 1.8f, 0), new Vector3(0.8f, 0.1f, 0.1f), new Color(0.2f, 0.18f, 0.16f))
                    .transform.rotation = Quaternion.Euler(0, Random.Range(0, 180f), 30);
                return;
            }
            var leaf = new Color(0.15f, 0.32f, 0.18f) * Random.Range(0.85f, 1.15f);
            for (int i = 0; i < 3; i++)
                Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * (1.6f + i * 0.7f), new Vector3(1.6f - i * 0.45f, 0.7f, 1.6f - i * 0.45f), leaf)
                    .transform.rotation = Quaternion.Euler(0, i * 30f, 0);
        }

        void BuildGoblinCamp()
        {
            var center = new Vector3(128.5f, 0, 80.5f);
            var hide = new Color(0.55f, 0.42f, 0.28f);
            for (int i = 0; i < 7; i++)
            {
                var p = center + Quaternion.Euler(0, i * (360f / 7f) + 10f, 0) * Vector3.forward * 9f;
                var c = grid.WorldToCell(p);
                if (grid.IsBlocked(c)) continue;
                float shade = Random.Range(0.8f, 1.1f);
                grid.BlockRect(c.x - 1, c.y - 1, c.x + 1, c.y + 1);
                float face = Quaternion.LookRotation(center - p).eulerAngles.y;
                if (Art(Pick("Nature/tent_detailedOpen", "Nature/tent_detailedClosed", "Nature/tent_smallClosed"), grid.CellToWorld(c), 3.2f, ArtLibrary.Fit.Width, face) != null)
                {
                    Art(Pick("Nature/log_stack", "Props/barrel_large", "Props/crates_stacked"), grid.CellToWorld(c) + Quaternion.Euler(0, face, 0) * new Vector3(1.3f, 0, -0.6f), 0.9f, ArtLibrary.Fit.Height, VR(0, 360));
                    continue;
                }
                var tent = Factory.Prim(PrimitiveType.Cube, deco, grid.CellToWorld(c) + Vector3.up * 0.9f, new Vector3(2f, 2f, 2.4f), hide * shade);
                tent.transform.rotation = Quaternion.LookRotation(center - p) * Quaternion.Euler(0, 0, 45);
            }
            // spiked barricade arcs
            for (int a = 0; a < 360; a += 12)
            {
                if (a > 255 && a < 290) continue; // opening facing the road (west)
                var p = center + Quaternion.Euler(0, a, 0) * Vector3.forward * 14f;
                var c = grid.WorldToCell(p);
                if (grid.IsBlocked(c) || reserved[Idx(c.x, c.y)]) continue;
                Factory.Prim(PrimitiveType.Cube, deco, grid.CellToWorld(c) + Vector3.up * 0.8f, new Vector3(0.25f, 1.8f, 0.25f), new Color(0.35f, 0.25f, 0.15f))
                    .transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f), 0, Random.Range(-20f, 20f));
                grid.SetBlocked(c.x, c.y, true);
            }
            CraftingStation.Create(SkillType.Cooking, center + new Vector3(0, 0, 3f), root);
            for (int y = -16; y <= 16; y++)
                for (int x = -16; x <= 16; x++)
                    if (x * x + y * y < 15 * 15)
                    {
                        int px = (int)center.x + x, py = (int)center.z + y;
                        Paint(px, py, Color.Lerp(pixels[Idx(px, py)], dirtC * 0.9f, 0.5f));
                        surface.Set(px, py, GroundSurface.Dirt, 0.65f * Mathf.Clamp01((15f * 15f - (x * x + y * y)) / 60f));
                    }
        }

        void BuildGraveyard()
        {
            var stone = new Color(0.55f, 0.55f, 0.55f);
            for (int row = 0; row < 6; row++)
                for (int col = 0; col < 12; col++)
                {
                    if (col == 5 || col == 6) continue; // aisle for the road
                    int x = 64 + col * 3 + Random.Range(0, 2), y = 30 + row * 4 + Random.Range(0, 2);
                    if (!Free(x, y)) continue;
                    var p = new Vector3(x + 0.5f, 0, y + 0.5f);
                    bool cross = Random.value < 0.3f;
                    if (art)
                    {
                        if (!cross) { Random.Range(0.8f, 1.1f); Random.Range(-8f, 8f); Random.Range(-8f, 8f); } // keep RNG in step
                        Art(cross ? "Graveyard/gravestone-cross" : Pick("Graveyard/gravestone-round", "Graveyard/gravestone-bevel", "Graveyard/gravestone-broken", "Graveyard/gravestone-decorative"),
                            p, VR(1.1f, 1.5f), ArtLibrary.Fit.Height, 180f + VR(-12f, 12f));
                        Art("Graveyard/grave", p + new Vector3(0, 0, -0.9f), 1.1f, ArtLibrary.Fit.Width, 180f, false);
                        if (vr.NextDouble() < 0.15) Art("Graveyard/candle-multiple", p + new Vector3(0.45f, 0, -0.35f), 0.45f, ArtLibrary.Fit.Height, VR(0, 360), false);
                    }
                    else if (cross)
                    {
                        Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * 0.75f, new Vector3(0.2f, 1.5f, 0.2f), stone);
                        Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * 1.1f, new Vector3(0.8f, 0.2f, 0.2f), stone);
                    }
                    else
                        Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.25f), stone * Random.Range(0.8f, 1.1f))
                            .transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), 0, Random.Range(-8f, 8f));
                    Paint(x, y - 1, new Color(0.25f, 0.2f, 0.15f));
                    surface.Set(x, y - 1, GroundSurface.Dirt, 0.8f);
                    surface.Tint(x, y - 1, new Color(0.8f, 0.78f, 0.75f));
                    grid.SetBlocked(x, y, true);
                }
        }

        void BuildCrypt()
        {
            var wall = new Color(0.28f, 0.27f, 0.3f);
            for (int y = Crypt.yMin; y < Crypt.yMax; y++)
                for (int x = Crypt.xMin; x < Crypt.xMax; x++)
                {
                    Paint(x, y, new Color(0.18f, 0.17f, 0.2f) * (0.9f + Mathf.PerlinNoise(x, y) * 0.2f));
                    surface.Set(x, y, GroundSurface.Cobble);
                    surface.Tint(x, y, new Color(0.55f, 0.55f, 0.65f));
                    Reserve(x, y);
                    bool edge = x == Crypt.xMin || x == Crypt.xMax - 1 || y == Crypt.yMin || y == Crypt.yMax - 1;
                    if (!edge || (y == Crypt.yMax - 1 && x >= 78 && x <= 82)) continue;
                    grid.SetBlocked(x, y, true);
                    float shade = (x + y) % 2 == 0 ? Random.Range(0.85f, 1.1f) : 1f;
                    bool sideX = y == Crypt.yMin || y == Crypt.yMax - 1;
                    // Graveyard/stone-wall is a 1-unit wall piece running along X.
                    if (ArtBox("Graveyard/stone-wall", new Vector3(x + 0.5f, 0, y + 0.5f), new Vector3(1.05f, 2.6f, 0.7f), sideX ? 0f : 90f) != null) continue;
                    if ((x + y) % 2 == 0)
                        Factory.Prim(PrimitiveType.Cube, deco, new Vector3(x + 0.5f, 1.6f, y + 0.5f), new Vector3(2f, 3.2f, 2f), wall * shade);
                }
            // pillars and braziers
            var fire = new Color(0.3f, 0.85f, 1f);
            foreach (var p in new[] { new Vector3(74.5f, 0, 18.5f), new Vector3(86.5f, 0, 18.5f), new Vector3(74.5f, 0, 9.5f), new Vector3(86.5f, 0, 9.5f) })
            {
                if (Art("Graveyard/pillar-large", p, 3f, ArtLibrary.Fit.Height) != null)
                    Art("Graveyard/fire-basket", p + Vector3.up * 3f, 0.7f, ArtLibrary.Fit.Height, 0f, false);
                else Factory.Prim(PrimitiveType.Cylinder, deco, p + Vector3.up * 1.5f, new Vector3(0.9f, 1.5f, 0.9f), wall);
                Factory.Prim(PrimitiveType.Sphere, deco, p + Vector3.up * 3.6f, Vector3.one * 0.45f, fire, false, Mat.Glow(fire));
                var l = new GameObject("Brazier").AddComponent<Light>();
                l.transform.SetParent(deco, false);
                l.transform.position = p + Vector3.up * 3.5f;
                l.type = LightType.Point;
                l.color = fire;
                l.range = 9f;
                l.intensity = 1.5f;
                NightLight.Add(l, 1f, 1.1f);
                Sfx.LoopAt("fire_loop", p + Vector3.up * 3f, 0.6f, 10f);
                grid.SetBlocked((int)p.x, (int)p.z, true);
            }
            // throne
            if (Art("Graveyard/altar-stone", new Vector3(80.5f, 0, 6.2f), 4.5f, ArtLibrary.Fit.Width) != null)
            {
                Art("Graveyard/candle-multiple", new Vector3(77.8f, 0, 7.4f), 0.8f, ArtLibrary.Fit.Height, 0f, false);
                Art("Graveyard/candle-multiple", new Vector3(83.2f, 0, 7.4f), 0.8f, ArtLibrary.Fit.Height, 90f, false);
                Art("Graveyard/coffin", new Vector3(73.5f, 0, 6.5f), 2f, ArtLibrary.Fit.Width, 90f);
                Art("Graveyard/coffin", new Vector3(87.5f, 0, 6.5f), 2f, ArtLibrary.Fit.Width, 90f);
            }
            else Factory.Prim(PrimitiveType.Cube, deco, new Vector3(80.5f, 0.3f, 6.5f), new Vector3(5f, 0.6f, 2f), wall * 1.2f);
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(80.5f, 2f, 5.5f), new Vector3(2f, 3.5f, 0.6f), new Color(0.25f, 0.1f, 0.35f));
            grid.BlockRect(78, 5, 83, 6);
        }

        void BuildQuarry()
        {
            for (int y = 8; y < H - 8; y++)
                for (int x = 6; x < 64; x++)
                {
                    float dx = x - 80f, dz = y - 80f;
                    if (Mathf.Abs(dx) < Mathf.Abs(dz) * 0.9f) continue;
                    if (Random.value > 0.035f || !Free(x, y) || !SpacedFrom(x, y, 1)) continue;
                    int tier = x > 42 ? 0 : x > 22 ? (Random.value < 0.75f ? 1 : 0) : (Random.value < 0.6f ? 2 : 1);
                    ResourceNode.Create(ResourceKind.Rock, tier, new Vector3(x + 0.5f, 0, y + 0.5f), nodes);
                }
            // Big boulders
            for (int i = 0; i < 40; i++)
            {
                int x = Random.Range(8, 60), y = Random.Range(40, 120);
                if (ZoneAt(new Vector3(x, 0, y)) != "Ironvein Quarry" || !Free(x, y) || !SpacedFrom(x, y, 2)) continue;
                float s = Random.Range(1.5f, 2.6f);
                float shade = Random.Range(0.85f, 1.1f);
                var rot = Random.rotation;
                grid.BlockRect(x - 1, y - 1, x + 1, y + 1);
                if (Art(Pick("Nature/rock_largeC", "Nature/rock_largeD", "Nature/rock_tallF", "Nature/rock_largeA"), new Vector3(x + 0.5f, 0, y + 0.5f), s * 1.5f, ArtLibrary.Fit.Width, VR(0, 360)) != null)
                    continue;
                Factory.Prim(PrimitiveType.Cube, deco, new Vector3(x + 0.5f, s * 0.4f, y + 0.5f), Vector3.one * s, new Color(0.42f, 0.4f, 0.37f) * shade)
                    .transform.rotation = rot;
            }
        }

        /// <summary>
        /// Higher resolution ground texture: tile colors blended smoothly, plus fine per-pixel noise so
        /// grass, dirt and stone don't look like flat smeared color. (MapTexture stays 1 px/tile for the minimap.)
        /// </summary>
        Texture2D BuildDetailedGround(int res)
        {
            int tw = W * res, th = H * res;
            var px = new Color32[tw * th];
            for (int y = 0; y < th; y++)
            {
                float fy = (y + 0.5f) / res - 0.5f;
                int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, H - 1), y1 = Mathf.Min(y0 + 1, H - 1);
                float ty = Mathf.Clamp01(fy - y0);
                for (int x = 0; x < tw; x++)
                {
                    float fx = (x + 0.5f) / res - 0.5f;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, W - 1), x1 = Mathf.Min(x0 + 1, W - 1);
                    float tx = Mathf.Clamp01(fx - x0);
                    Color c = Color.Lerp(
                        Color.Lerp(pixels[Idx(x0, y0)], pixels[Idx(x1, y0)], tx),
                        Color.Lerp(pixels[Idx(x0, y1)], pixels[Idx(x1, y1)], tx), ty);
                    float n = Mathf.PerlinNoise(x * 0.37f, y * 0.37f) * 0.16f + Mathf.PerlinNoise(x * 1.3f + 50f, y * 1.3f) * 0.1f;
                    c *= 0.88f + n;
                    px[y * tw + x] = c;
                }
            }
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4,
                name = "Ground"
            };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        // ------------------------------------------------------------------ detail

        /// <summary>Grass, flowers, bushes, mushrooms and pebbles. Purely visual: never blocks tiles.</summary>
        void ScatterDetail()
        {
            if (!art) return;
            for (int y = 5; y < H - 5; y++)
                for (int x = 5; x < W - 5; x++)
                {
                    if (!Free(x, y)) continue;
                    var p = new Vector3(x + (float)vr.NextDouble(), 0, y + (float)vr.NextDouble());
                    string zone = ZoneAt(p);
                    double r = vr.NextDouble();
                    float road = surface.RoadWeight(p.x, p.z);
                    if (road > 0.05f && road < 0.6f)
                    {
                        // Stones and weeds along the edges of the roads
                        if (r < 0.07) Art(Pick("Nature/rock_smallA", "Nature/rock_smallC", "Nature/rock_smallFlatA", "Nature/rock_smallE"), p, VR(0.25f, 0.5f), ArtLibrary.Fit.Width, VR(0, 360), false);
                        continue;
                    }
                    if (fancyGround && r < 0.10 && IsGrassModelRoll(zone, r)) continue; // blade grass replaces the 3D tufts
                    switch (zone)
                    {
                        case "Whisperwood":
                            if (r < 0.10) Art(Pick("Nature/grass", "Nature/grass_large", "Nature/grass_leafsLarge"), p, VR(0.4f, 0.8f), ArtLibrary.Fit.Height, VR(0, 360), false);
                            else if (r < 0.13) Art(Pick("Nature/flower_redA", "Nature/flower_yellowA", "Nature/flower_purpleA"), p, VR(0.35f, 0.55f), ArtLibrary.Fit.Height, VR(0, 360), false);
                            else if (r < 0.145) Art(Pick("Nature/plant_bush", "Nature/plant_bushLarge"), p, VR(0.7f, 1.2f), ArtLibrary.Fit.Height, VR(0, 360));
                            else if (r < 0.155) Art(Pick("Nature/mushroom_redGroup", "Nature/mushroom_tanGroup"), p, VR(0.3f, 0.5f), ArtLibrary.Fit.Height, VR(0, 360), false);
                            break;
                        case "Goblin Encampment":
                            if (r < 0.05) Art(Pick("Nature/grass", "Nature/grass_large"), p, VR(0.4f, 0.7f), ArtLibrary.Fit.Height, VR(0, 360), false);
                            else if (r < 0.06) Art(Pick("Nature/plant_bush", "Nature/rock_smallA"), p, VR(0.5f, 0.9f), ArtLibrary.Fit.Height, VR(0, 360));
                            break;
                        case "Ironvein Quarry":
                            if (r < 0.04) Art(Pick("Nature/rock_smallA", "Nature/rock_smallC", "Nature/rock_smallE", "Nature/rock_smallFlatA"), p, VR(0.5f, 0.9f), ArtLibrary.Fit.Width, VR(0, 360), false);
                            else if (r < 0.06) Art("Nature/grass", p, VR(0.3f, 0.5f), ArtLibrary.Fit.Height, VR(0, 360), false);
                            break;
                        case "Forsaken Graveyard":
                            if (r < 0.04) Art(Pick("Nature/grass_leafsLarge", "Nature/grass"), p, VR(0.4f, 0.7f), ArtLibrary.Fit.Height, VR(0, 360), false);
                            else if (r < 0.048) Art("Nature/mushroom_tanGroup", p, VR(0.3f, 0.45f), ArtLibrary.Fit.Height, VR(0, 360), false);
                            break;
                    }
                }
        }

        /// <summary>True when this roll would have placed one of the 3D grass tufts in <see cref="ScatterDetail"/>.</summary>
        static bool IsGrassModelRoll(string zone, double r)
        {
            switch (zone)
            {
                case "Whisperwood": return r < 0.10;
                case "Goblin Encampment": return r < 0.05;
                case "Ironvein Quarry": return r >= 0.04 && r < 0.06;
                case "Forsaken Graveyard": return r < 0.04;
                default: return false;
            }
        }

        // ------------------------------------------------------------------ ground

        void BuildGround()
        {
            MapTexture = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "WorldMap"
            };
            MapTexture.SetPixels(pixels);
            MapTexture.Apply();

            if (fancyGround && surface.BuildGround(root) != null)
            {
                surface.BuildWater(root);
                surface.BuildGrass(root, grid, Seed ^ 0x6a55);
                return;
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "Ground";
            ground.transform.SetParent(root, false);
            ground.transform.position = new Vector3(W * 0.5f, 0f, H * 0.5f);
            ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.localScale = new Vector3(W, H, 1f);
            var mat = Mat.New(Color.white);
            mat.mainTexture = BuildDetailedGround(4);
            ground.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
