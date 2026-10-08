using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Builds the whole world procedurally from a fixed seed: ground texture, town, zones,
    /// gathering nodes and NPCs. Monsters are spawned and simulated by the server (see /server).
    /// </summary>
    public partial class WorldGenerator
    {
        public const int W = 576, H = 576;
        /// <summary>
        /// The original world, Hollowmere and its four zones, fills the south-west corner (0..OldSize on both axes).
        /// The rest (north, east and north-east of it) is the outer lands: see WorldGenerator.Regions.cs.
        /// </summary>
        public const int OldSize = 288;
        /// <summary>The middle of Hollowmere's plaza (the old world's centre).</summary>
        public const float Center = 144f;
        /// <summary>
        /// The zones were designed on the original 160-tile map centred on 80. <see cref="Map(float)"/> turns those
        /// design coordinates into world coordinates: the town (within 29 tiles of the centre) just moves with the
        /// centre, and everything farther out is spread <see cref="Stretch"/> times as far, so each zone has
        /// about twice the room. Saves from before the bigger world are converted with it too.
        /// </summary>
        public const float Stretch = 2.25f;
        const float Inner = 29f;
        public static float Map(float v)
        {
            float d = v - 80f, a = Mathf.Abs(d);
            return Center + (a <= Inner ? d : Mathf.Sign(d) * (Inner + (a - Inner) * Stretch));
        }
        public static Vector3 Map(Vector3 p) => new Vector3(Map(p.x), p.y, Map(p.z));
        static int MapI(float v) => Mathf.RoundToInt(Map(v));
        public const int Seed = 20261006;
        public static readonly RectInt Town = new RectInt(116, 116, 57, 57); // cells 116..172
        /// <summary>
        /// Only used to convert saves from older maps (positions saved before version 3 are remapped). It no longer needs
        /// bumping when the map changes: the server takes a changed map from the newest game build (see the server's
        /// "Game versions" section). 4: the layout moved to its own random generator.
        /// </summary>
        public const int LayoutVersion = 4;
        public static readonly RectInt Crypt = new RectInt(132, 9, 25, 19);  // cells 132..156, 9..27

        public Texture2D MapTexture { get; private set; }
        public Vector3 SpawnPoint => new Vector3(144.5f, 0f, 141.5f);

        WorldGrid grid;
        Color[] pixels;
        bool[] reserved; // no trees / rocks here (roads, town, water)
        bool[] road;     // road tiles (the ridge leaves passes around them)
        Transform root, nodes, npcs, deco;
        GroundSurface surface;    // splat-mapped ground, grass and water (visual only)
        bool fancyGround;         // terrain shaders available
        bool art;                 // CC0 models available (falls back to primitives if not)
        System.Random vr;         // visual-only randomness: never touches the layout RNG, so the
                                  // walkability grid (and the server's world hash) stays the same

        float VR(float a, float b) => a + (float)vr.NextDouble() * (b - a);

        // Layout randomness (where trees, rocks, graves go): a private generator, so nothing else that draws random
        // numbers while the world is built (effects, art, particles) can ever shift the map. Visual-only code paths
        // that skip a layout draw must still consume it ("keep in step") so art and primitive worlds match.
        System.Random lr;
        float LR(float a, float b) => a + (float)lr.NextDouble() * (b - a);
        int LRI(int a, int b) => lr.Next(a, b);
        float LV => (float)lr.NextDouble();
        string Pick(params string[] options) => options[vr.Next(options.Length)];

        GameObject Art(string path, Vector3 pos, float size, ArtLibrary.Fit fit = ArtLibrary.Fit.Height, float yaw = 0f, bool shadows = true) =>
            art ? ArtLibrary.Spawn(path, deco, pos, size, fit, yaw, shadows, true, true) : null;

        GameObject ArtBox(string path, Vector3 pos, Vector3 size, float yaw = 0f) =>
            art ? ArtLibrary.SpawnBox(path, deco, pos, size, yaw) : null;

        /// <summary>How grassy the ground is at a point (0..1), for ambient critters.</summary>
        public float GrassAt(Vector3 p) => fancyGround ? surface.GrassAmount(p.x, p.z) : 0.5f;

        /// <summary>In any town or hamlet (safe from monsters, town music, recall counts as home).</summary>
        public static bool InTown(Vector3 p) => TownAt(p) != null;

        /// <summary>Inside Hollowmere's walls (its festivals, townsfolk and leaf piles).</summary>
        public static bool InHollowmere(Vector3 p) =>
            p.x >= Town.xMin && p.x < Town.xMax && p.z >= Town.yMin && p.z < Town.yMax;

        public static bool InCrypt(Vector3 p) =>
            p.x >= Crypt.xMin && p.x < Crypt.xMax && p.z >= Crypt.yMin && p.z < Crypt.yMax;

        public static string ZoneAt(Vector3 p)
        {
            if (Dungeon.Contains(p)) return Dungeon.ZoneName;
            var town = TownAt(p);
            if (town != null) return town.Name;
            if (InCrypt(p)) return "Crypt of the Lich";
            if (p.x >= OldSize || p.z >= OldSize) return p.x < OldSize ? Frostpeak : p.z < OldSize ? Badlands : Ashen;
            float dx = p.x - Center, dz = p.z - Center;
            if (Mathf.Abs(dz) > Mathf.Abs(dx)) return dz > 0 ? "Whisperwood" : "Forsaken Graveyard";
            return dx > 0 ? "Goblin Encampment" : "Ironvein Quarry";
        }

        public void Generate()
        {
            var oldState = Random.state;
            Random.InitState(Seed);
            art = ArtLibrary.Available;
            vr = new System.Random(Seed ^ 0x5eed);
            lr = new System.Random(Seed);

            grid = new WorldGrid(W, H);
            pixels = new Color[W * H];
            reserved = new bool[W * H];
            road = new bool[W * H];
            root = new GameObject("World").transform;
            nodes = Factory.Empty("Resources", root, Vector3.zero);
            npcs = Factory.Empty("NPCs", root, Vector3.zero);
            deco = Factory.Empty("Decoration", root, Vector3.zero);
            surface = new GroundSurface(W, H);
            foreach (var t in Towns) if (t.Walled) surface.ExcludeRoads(t.Rect);
            fancyGround = GroundSurface.Supported;

            PaintBase();
            PaintRoads();
            BuildLake(new Vector2(Map(55), Map(125)), 11f, 0);
            BuildLake(new Vector2(Map(112), Map(138)), 9f, 1);
            BuildLake(new Vector2(Map(140), Map(122)), 8f, 1);   // north-east woods
            BuildLake(new Vector2(Map(28), Map(130)), 8f, 1);    // north-west, by the quarry
            BuildLake(new Vector2(Map(132), Map(40)), 7f, 0);    // south-east, a murky graveyard mere
            BuildRegionLakes();
            BuildBorder();
            BuildTown();
            BuildForest();
            BuildGoblinCamp();
            BuildGraveyard();
            BuildCrypt();
            BuildQuarry();
            BuildOuterTowns();
            BuildRegions();
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
                    // Inside the old world the zone colours come from the direction to Hollowmere (clamped, so the
                    // north and east zones carry on to the old edge); the outer regions fade in across it.
                    float dx = (Mathf.Min(x, OldSize) - Center) / Stretch, dz = (Mathf.Min(y, OldSize) - Center) / Stretch;
                    float wn = Mathf.Pow(Mathf.Max(0, dz), 3), ws = Mathf.Pow(Mathf.Max(0, -dz), 3);
                    float we = Mathf.Pow(Mathf.Max(0, dx), 3), ww = Mathf.Pow(Mathf.Max(0, -dx), 3);
                    float sum = wn + ws + we + ww + 0.0001f;
                    Color c = (forestC * wn + graveC * ws + steppeC * we + quarryC * ww) / sum;
                    if (sum < 1f) c = Color.Lerp(new Color(0.3f, 0.42f, 0.2f), c, sum);
                    RegionWeights(x, y, out float rOld, out float rFrost, out float rBad, out float rAsh);
                    c = c * rOld + frostC * rFrost + badlandsC * rBad + ashC * rAsh;
                    float n = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.25f + Mathf.PerlinNoise(x * 0.6f, y * 0.6f) * 0.1f;
                    pixels[Idx(x, y)] = c * (0.85f + n);
                    BaseSurface(x, y, wn / sum, ws / sum, we / sum, ww / sum);
                    if (rOld < 0.999f) RegionSurface(x, y, rOld, rFrost, rBad, rAsh);
                }

            // The village: cobbled cross streets and central square, grassy yards with trodden dirt elsewhere.
            for (int y = Town.yMin; y < Town.yMax; y++)
                for (int x = Town.xMin; x < Town.xMax; x++)
                {
                    Reserve(x, y);
                    if (IsHollowmereStreet(x, y))
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
        static bool IsHollowmereStreet(int x, int y)
        {
            bool cross = (x >= 142 && x <= 146) || (y >= 142 && y <= 146);
            bool square = x >= 136 && x <= 152 && y >= 136 && y <= 152;
            return cross || square;
        }

        /// <summary>Which ground textures a tile gets, from how much it belongs to each zone.</summary>
        void BaseSurface(int x, int y, float north, float south, float east, float west)
        {
            float a = Mathf.PerlinNoise(x * 0.07f + 11f, y * 0.07f + 5f);
            float b = Mathf.PerlinNoise(x * 0.12f + 31f, y * 0.12f + 71f);
            var w = new float[GroundSurface.Layers];
            // Whisperwood: meadow grass giving way to dark forest floor deeper in.
            float deep = Mathf.Clamp01((y - Map(100f)) / (40f * Stretch));
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
            float rock = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((a - 0.35f) * 3f + Mathf.Clamp01((Map(40f) - x) / (30f * Stretch))));
            w[GroundSurface.Gravel] += west * rock;
            w[GroundSurface.Dry] += west * (1f - rock) * 0.6f;
            w[GroundSurface.Dirt] += west * (1f - rock) * 0.4f;
            // Green meadow around the village.
            float meadow = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(34f, 50f, Vector2.Distance(new Vector2(x, y), new Vector2(Center, Center))));
            for (int i = 0; i < w.Length; i++) w[i] *= 1f - meadow;
            w[GroundSurface.Grass] += meadow;
            surface.SetWeights(x, y, w);
            float t = 0.9f + Mathf.PerlinNoise(x * 0.045f + 3f, y * 0.045f) * 0.2f;
            surface.Tint(x, y, new Color(t, t * (0.97f + b * 0.06f), t * 0.97f));
        }

        void PaintRoads()
        {
            // Smooth centerlines for the ground shader (the tiles below stay the walkable/reserved road).
            // Four roads leading out of Hollowmere's gates, gently meandering: north on to Frosthaven, east on to
            // Saltreach, south and west to the old edge of the world. Then the road between Frosthaven and Emberwatch
            // and the one between Saltreach and Emberwatch. Each road runs straight into a town's gate.
            var frost = TownNamed("Frosthaven").Rect; var salt = TownNamed("Saltreach").Rect; var ember = TownNamed("Emberwatch").Rect;
            Road(true, Center, 0.05f, 6f, Town.yMax - 1, frost.yMin, Town.yMax, frost.yMin);                       // north
            Road(true, Center, 0.06f, 5f, Town.yMin, 6, Town.yMin, -999);                                         // south
            Road(false, Center, 0.055f, 6f, Town.xMax - 1, salt.xMin, Town.xMax, salt.xMin);                       // east
            Road(false, Center, 0.045f, 6f, Town.xMin, 6, Town.xMin, -999);                                       // west
            Road(false, frost.center.y - 0.5f, 0.05f, 7f, frost.xMax - 1, ember.xMin, frost.xMax, ember.xMin);     // Frosthaven - Emberwatch
            Road(true, salt.center.x - 0.5f, 0.045f, 7f, salt.yMax - 1, ember.yMin, salt.yMax, ember.yMin);        // Saltreach - Emberwatch
        }

        /// <summary>
        /// A road along one axis from <paramref name="from"/> to <paramref name="to"/> (either direction), meandering
        /// around <paramref name="line"/> but straightening out within 18 tiles of the gates at <paramref name="gateA"/>
        /// and <paramref name="gateB"/> (-999: no gate at that end). Paints the smooth centreline for the ground shader
        /// and the walkable, reserved road tiles.
        /// </summary>
        void Road(bool northSouth, float line, float freq, float amp, int from, int to, int gateA, int gateB)
        {
            float Calm(float i) => Mathf.Min(gateA == -999 ? 1f : Mathf.Clamp01(Mathf.Abs(i - gateA) / 18f),
                                             gateB == -999 ? 1f : Mathf.Clamp01(Mathf.Abs(i - gateB) / 18f));
            float At(float i) => line + Mathf.Sin(i * freq) * amp * Calm(i);
            int step = to >= from ? 1 : -1;
            var pts = new System.Collections.Generic.List<Vector2>();
            for (int i = from; i != to; i += step)
            {
                float c = Mathf.Round(At(i)) + 0.5f;
                pts.Add(northSouth ? new Vector2(c, i + 0.5f) : new Vector2(i + 0.5f, c));
            }
            surface.AddRoad(Smooth(pts));
            for (int i = from + step; i != to; i += step)
                if (northSouth) RoadDot(At(i), i); else RoadDot(i, At(i));
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
                    if (px < 0 || py < 0 || px >= W || py >= H || InWalledTown(px, py)) continue;
                    float n = Mathf.PerlinNoise(px * 0.7f, py * 0.7f) * 0.2f;
                    Paint(px, py, dirtC * (0.9f + n));
                    Reserve(px, py);
                    road[Idx(px, py)] = true;
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
                var dir = Quaternion.Euler(0, a + LRI(0, 15), 0) * Vector3.forward;
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
            BuildOldRidge();
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
            float h = LR(3f, 7f);
            float sx = LR(3.5f, 5f), sz = LR(3.5f, 5f), shade = LR(0.85f, 1.1f), rot = LR(0, 90f);
            if (Art(Pick("Rocks/Boulder_1", "Rocks/Boulder_2", "Rocks/Boulder_3"), p, h * 0.75f, ArtLibrary.Fit.Height, VR(0, 360)) != null)
            {
                Art(Pick("Rocks/Boulder_1", "Rocks/Boulder_2", "Rocks/Boulder_3"), p + new Vector3(VR(-0.8f, 0.8f), 0f, VR(-0.8f, 0.8f)), Mathf.Max(sx, sz) * 0.9f,
                    ArtLibrary.Fit.Width, VR(0, 360));
                return;
            }
            Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * h * 0.5f, new Vector3(sx, h, sz),
                new Color(0.34f, 0.32f, 0.3f) * shade).transform.rotation = Quaternion.Euler(0, rot, 0);
        }

        // ------------------------------------------------------------------ town

        // Hollowmere, cells 52..108 with gates at 78..82 on every side. Four districts around a central plaza:
        //   north-west: tavern, windmill, Wren by the north gate      north-east: church, Sister Mae, Jenkins, the captain
        //   south-west: farm and market (Lysa, Vex)                    south-east: smithy (Gorrin, Hilda, Brann), training yard, Orla's pen
        // NPCs never block tiles, so they can be moved around without changing the world map.
        void BuildTown()
        {
            var wood = new Color(0.42f, 0.3f, 0.18f);
            int x0 = Town.xMin, x1 = Town.xMax - 1, y0 = Town.yMin, y1 = Town.yMax - 1;
            // Palisade with gates at the centre of each side (cells 78..82)
            for (int i = x0; i <= x1; i++)
            {
                if (i >= 142 && i <= 146) continue;
                Palisade(i, y0, wood);
                Palisade(i, y1, wood);
            }
            for (int i = y0 + 1; i < y1; i++)
            {
                if (i >= 142 && i <= 146) continue;
                Palisade(x0, i, wood);
                Palisade(x1, i, wood);
            }
            // Gate towers
            foreach (var g in new[] { new Vector2(141, y0), new Vector2(147, y0), new Vector2(141, y1), new Vector2(147, y1),
                                      new Vector2(x0, 141), new Vector2(x0, 147), new Vector2(x1, 141), new Vector2(x1, 147) })
            {
                if (Art("Buildings/building_tower_A_blue", new Vector3(g.x + 0.5f, 0, g.y + 0.5f), 5.2f, ArtLibrary.Fit.Height, VR(0, 4) * 90f) != null) continue;
                Factory.Prim(PrimitiveType.Cube, deco, new Vector3(g.x + 0.5f, 2f, g.y + 0.5f), new Vector3(1.4f, 4f, 1.4f), wood * 0.85f);
                var torchC = new Color(1f, 0.6f, 0.2f);
                var tc = Factory.Prim(PrimitiveType.Sphere, deco, new Vector3(g.x + 0.5f, 4.3f, g.y + 0.5f), Vector3.one * 0.3f, torchC, false, Mat.Glow(torchC));
                PropFire.Add(deco, new Vector3(g.x + 0.5f, 4.2f, g.y + 0.5f), torchC, 0.45f, true, null, tc.transform);
            }

            // ---- buildings
            House(new RectInt(121, 153, 8, 7), new Color(0.75f, 0.68f, 0.55f), new Color(0.55f, 0.2f, 0.15f), "Buildings/building_tavern_blue", 90f);
            House(new RectInt(120, 162, 8, 8), new Color(0.72f, 0.65f, 0.55f), new Color(0.5f, 0.3f, 0.2f), "Buildings/building_windmill_blue", 135f);
            House(new RectInt(131, 163, 6, 6), new Color(0.72f, 0.62f, 0.5f), new Color(0.3f, 0.45f, 0.25f), "Buildings/building_home_A_green", 180f);
            House(new RectInt(121, 147, 6, 5), new Color(0.72f, 0.65f, 0.55f), new Color(0.3f, 0.35f, 0.55f), "Buildings/building_home_A_blue", 90f);
            House(new RectInt(156, 159, 10, 8), new Color(0.75f, 0.73f, 0.68f), new Color(0.3f, 0.32f, 0.45f), "Buildings/building_church_blue", 180f);
            House(new RectInt(150, 163, 6, 6), new Color(0.7f, 0.65f, 0.55f), new Color(0.25f, 0.3f, 0.5f), "Buildings/building_home_B_blue", 180f);
            House(new RectInt(163, 148, 6, 6), new Color(0.7f, 0.64f, 0.55f), new Color(0.3f, 0.35f, 0.55f), "Buildings/building_home_B_blue", 270f);
            House(new RectInt(120, 131, 7, 7), new Color(0.74f, 0.66f, 0.52f), new Color(0.55f, 0.25f, 0.2f), "Buildings/building_market_blue", 90f);
            House(new RectInt(157, 130, 7, 6), new Color(0.6f, 0.55f, 0.5f), new Color(0.35f, 0.25f, 0.18f), "Buildings/building_blacksmith_blue", -90f);

            // The well, winch and all, in the middle of the plaza (a fountain stood here for a while, with the well off
            // to one side; one centrepiece reads better)
            grid.BlockRect(143, 143, 145, 145);
            if (Art("Buildings/building_well_blue", new Vector3(144.5f, 0, 144.5f), 2.8f, ArtLibrary.Fit.Width) == null)
            {
                Factory.Prim(PrimitiveType.Cylinder, deco, new Vector3(144.5f, 0.5f, 144.5f), new Vector3(2.2f, 0.5f, 2.2f), new Color(0.5f, 0.48f, 0.45f));
                Factory.Prim(PrimitiveType.Cylinder, deco, new Vector3(144.5f, 1.01f, 144.5f), new Vector3(1.7f, 0.01f, 1.7f), waterC);
            }

            // Market stalls west of the plaza
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(130.5f + i * 2f, 0, 138.5f);
                grid.SetBlocked((int)p.x, (int)p.z, true);
                if (Art(i % 2 == 0 ? "Town/stall-red" : "Town/stall-green", p, 1.8f, ArtLibrary.Fit.Width) != null) continue;
                Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * 0.4f, new Vector3(1.4f, 0.8f, 1f), wood);
                Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * 2f, new Vector3(1.7f, 0.1f, 1.3f), i % 2 == 0 ? new Color(0.7f, 0.2f, 0.2f) : new Color(0.85f, 0.8f, 0.6f));
            }

            // Farm plot (south-west) and Orla's animal pen (south-east): fenced, with a gap to walk in
            FencedPlot(new RectInt(120, 119, 13, 7), 126, true, wood);
            FencedPlot(new RectInt(163, 121, 6, 5), 165, false, wood);

            // Training yard (south): posts and banners
            for (int y = 119; y < 126; y++)
                for (int x = 148; x < 160; x++)
                    surface.Set(x, y, GroundSurface.Dirt, 0.8f);
            foreach (var px in new[] { 150.5f, 153.5f, 156.5f })
            {
                grid.SetBlocked((int)px, 122, true);
                if (Art("Town/pillar-wood", new Vector3(px, 0, 122.5f), 2f, ArtLibrary.Fit.Height) == null)
                    Factory.Prim(PrimitiveType.Cylinder, deco, new Vector3(px, 1f, 122.5f), new Vector3(0.4f, 1f, 0.4f), wood);
            }

            // ---- crafting stations and people
            CraftingStation.Create(SkillType.Smithing, new Vector3(155.5f, 0, 130.5f), root);
            // The cooking fire by the tavern (it stood where the maypole and the winter tree go up)
            CraftingStation.Create(SkillType.Cooking, new Vector3(134.5f, 0, 151.0f), root);

            Npc.Create("Captain Aldric", "Captain of the Guard", NpcRole.QuestGiver, new Vector3(148.5f, 0, 168.5f), new Color(0.6f, 0.15f, 0.12f),
                "Stay sharp, traveller. These are dark days for Hollowmere.", npcs, true, null, false);
            Npc.Create("Forester Wren", "Woodcutting & Fishing", NpcRole.QuestGiver, new Vector3(139.5f, 0, 167.5f), new Color(0.25f, 0.45f, 0.2f),
                "The forest provides, if you know how to ask.", npcs, false, null, false);
            Npc.Create("Smith Gorrin", "Blacksmith", NpcRole.QuestGiver, new Vector3(154.5f, 0, 133.5f), new Color(0.35f, 0.3f, 0.28f),
                "Bring me ore and I'll teach you to work it.", npcs, true, null, false);
            Npc.Create("Merchant Lysa", "General Goods", NpcRole.Vendor, new Vector3(128.5f, 0, 134.5f), new Color(0.55f, 0.3f, 0.6f),
                "Potions! Fresh potions! I also buy anything you drag out of those monsters.", npcs, false, null, false).SellsAs(VendorKind.General);
            Npc.Create("Sister Mae", "Healer", NpcRole.Healer, new Vector3(160.5f, 0, 156.5f), new Color(0.9f, 0.9f, 0.85f),
                "The Light watches over you, child. Let me tend your wounds.", npcs, false, null, false);
            Npc.Create("Thomas", "Farmer", NpcRole.QuestGiver, new Vector3(134.5f, 0, 123.5f), new Color(0.45f, 0.55f, 0.3f),
                "Morning! Don't mind the mud. Bandits ran off with half my harvest again.", npcs, false, false, false);
            Npc.Create("Jenkins", "Butler of Automation", NpcRole.QuestGiver, new Vector3(152.5f, 0, 154.5f), new Color(0.08f, 0.08f, 0.1f),
                "Good day. I have taken the liberty of automating the village. Nearly all of it. The rest is merely failing.", npcs, false, false, false)
                .DressAsButler();
            Npc.Create("Armorer Brann", "Armor", NpcRole.Vendor, new Vector3(162.5f, 0, 138.5f), new Color(0.45f, 0.42f, 0.4f),
                "Helms, mail, boots. Everything a body needs to stay a body.", npcs, true, false, false).SellsAs(VendorKind.Armor);
            Npc.Create("Weaponsmith Hilda", "Weapons", NpcRole.Vendor, new Vector3(154.5f, 0, 139.5f), new Color(0.55f, 0.3f, 0.2f),
                "Looking for something with an edge? You've come to the right woman.", npcs, true, false, false).SellsAs(VendorKind.Weapons);
            Npc.Create("Innkeeper Rosie", "Food & Drink", NpcRole.Vendor, new Vector3(130.5f, 0, 155.5f), new Color(0.75f, 0.45f, 0.35f),
                "Welcome to the Prancing Boar! Sit, eat, drink, and don't start any fights.", npcs, false, true, false).SellsAs(VendorKind.Food);
            Npc.Create("Curio Dealer Vex", "Rings & Amulets", NpcRole.Vendor, new Vector3(135.5f, 0, 134.5f), new Color(0.3f, 0.2f, 0.45f),
                "Trinkets with a past. Some of them even have a future.", npcs, false, true, false).SellsAs(VendorKind.Curios);
            Npc.Create("Beastmaster Orla", "Companions for Hire", NpcRole.Vendor, new Vector3(166.5f, 0, 128.5f), new Color(0.4f, 0.3f, 0.2f),
                "Hounds, blades, spells and stone. Nobody should walk these roads alone.", npcs, false, false, false).SellsAs(VendorKind.Companions);

            if (art)
            {
                // Lantern posts around the plaza and along the streets (visual only).
                var posts = new System.Collections.Generic.List<Vector3>
                {
                    new Vector3(135.6f, 0, 135.6f), new Vector3(153.4f, 0, 135.6f), new Vector3(135.6f, 0, 153.4f), new Vector3(153.4f, 0, 153.4f),
                    new Vector3(141.6f, 0, 141.6f), new Vector3(147.4f, 0, 141.6f), new Vector3(141.6f, 0, 147.4f), new Vector3(147.4f, 0, 147.4f),
                };
                foreach (float d in new[] { 121.5f, 129.5f, 157.5f, 166.5f })
                {
                    posts.Add(new Vector3(141.4f, 0, d));
                    posts.Add(new Vector3(147.6f, 0, d));
                    float ex = d == 121.5f ? 119.4f : d; // (121.5, 147.6) would stand inside the blue house
                    posts.Add(new Vector3(ex, 0, 141.4f));
                    posts.Add(new Vector3(ex, 0, 147.6f));
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
                Art("Town/cart", new Vector3(132.5f, 0, 141.0f), 2.2f, ArtLibrary.Fit.Width, 35f);
                Art("Props/barrel_small_stack", new Vector3(127.5f, 0, 139.6f), 1.0f);
                Art("Props/box_stacked", new Vector3(137.4f, 0, 139.0f), 1.0f, ArtLibrary.Fit.Height, 15f);
                Art("Props/barrel_large", new Vector3(153.0f, 0, 130.6f), 0.9f);
                Art("Props/crates_stacked", new Vector3(155.0f, 0, 136.6f), 1.1f, ArtLibrary.Fit.Height, 20f);
                Art("Props/barrel_large", new Vector3(158.5f, 0, 120.0f), 1f);
                Art("Town/banner-red", new Vector3(160.0f, 0, 125.0f), 3f, ArtLibrary.Fit.Height, 270f);
                Art("Props/barrel_small_stack", new Vector3(129.5f, 0, 152.0f), 1.0f);
                Art("Town/hedge", new Vector3(155.5f, 0, 157.5f), 1.2f, ArtLibrary.Fit.Height, 90f);
                // Banners by the gates
                Art("Town/banner-green", new Vector3(140.6f, 0, Town.yMax - 2.5f), 3f, ArtLibrary.Fit.Height, 180f);
                Art("Town/banner-green", new Vector3(148.4f, 0, Town.yMax - 2.5f), 3f, ArtLibrary.Fit.Height, 180f);
                Art("Town/banner-red", new Vector3(140.6f, 0, Town.yMin + 2.5f), 3f, ArtLibrary.Fit.Height, 0f);
                Art("Town/banner-red", new Vector3(148.4f, 0, Town.yMin + 2.5f), 3f, ArtLibrary.Fit.Height, 0f);
            }
        }

        /// <summary>A fenced rectangle (farm plot, animal pen) with a two-cell gap in its top side at <paramref name="gapX"/>.</summary>
        void FencedPlot(RectInt r, int gapX, bool crops, Color wood)
        {
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    surface.Set(x, y, GroundSurface.Dirt, crops ? 0.95f : 0.7f);
                    if (crops) { surface.Tint(x, y, new Color(0.8f, 0.75f, 0.7f)); Paint(x, y, dirtC * 0.8f); }
                    bool edge = x == r.xMin || x == r.xMax - 1 || y == r.yMin || y == r.yMax - 1;
                    if (edge)
                    {
                        if (y == r.yMax - 1 && (x == gapX || x == gapX + 1)) continue; // gap to walk in
                        grid.SetBlocked(x, y, true);
                        bool alongX = y == r.yMin || y == r.yMax - 1;
                        if (ArtBox("Town/fence", new Vector3(x + 0.5f, 0, y + 0.5f), new Vector3(0.3f, 1f, 1.02f), alongX ? 90f : 0f) == null)
                            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(x + 0.5f, 0.5f, y + 0.5f), alongX ? new Vector3(1f, 1f, 0.2f) : new Vector3(0.2f, 1f, 1f), wood);
                    }
                    else if (crops && art && (y - r.yMin) % 2 == 1)
                        Art(Pick("Plants/Plant", "Plants/Plant"), new Vector3(x + 0.5f, 0, y + 0.5f), VR(0.5f, 0.75f), ArtLibrary.Fit.Height, VR(0, 360), false);
                    else if (!crops && art && vr.NextDouble() < 0.25)
                        Art("Nature/grass_large", new Vector3(x + 0.5f, 0, y + 0.5f), VR(0.4f, 0.6f), ArtLibrary.Fit.Height, VR(0, 360), false);
                }
        }

        void Palisade(int x, int y, Color wood) => Palisade(x, y, wood, y == Town.yMin || y == Town.yMax - 1);

        void Palisade(int x, int y, Color wood, bool alongX)
        {
            float shade = LR(0.85f, 1.05f);
            grid.SetBlocked(x, y, true);
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
            int forestFrom = MapI(96);
            for (int y = forestFrom; y < OldSize - 5; y++)
                for (int x = 5; x < OldSize - 5; x++)
                {
                    float dx = x - Center, dz = y - Center;
                    if (Mathf.Abs(dz) < Mathf.Abs(dx) * 0.8f) continue;
                    float density = 0.05f + Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.08f;
                    if (LV > density || !Free(x, y) || !SpacedFrom(x, y, 1)) continue;
                    int tier = y < Map(115) ? 0 : y < Map(136) ? (LV < 0.7f ? 1 : 0) : (LV < 0.6f ? 2 : 1);
                    ResourceNode.Create(ResourceKind.Tree, tier, new Vector3(x + 0.5f, 0, y + 0.5f), nodes);
                    Paint(x, y, pixels[Idx(x, y)] * 0.7f);
                    surface.Set(x, y, GroundSurface.Forest, 0.6f);
                }
            // Scattered decorative pines elsewhere
            for (int i = 0; i < 700; i++)
            {
                int x = LRI(6, OldSize - 6), y = LRI(6, forestFrom);
                if (!Free(x, y) || !SpacedFrom(x, y, 2) || InCrypt(new Vector3(x, 0, y))) continue;
                if (ZoneAt(new Vector3(x, 0, y)) == "Ironvein Quarry" && LV < 0.7f) continue;
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
                LR(0f, dead ? 180f : 1f); // keep the layout RNG in step with the primitive version
                if (dead) Art(Pick("Trees/Dead_1", "Trees/Dead_2", "Trees/Dead_3"), p, VR(4f, 6f), ArtLibrary.Fit.Height, VR(0, 360));
                else Art(Pick("Trees/Pine_1", "Trees/Pine_2", "Trees/Pine_3", "Trees/Pine_1", "Trees/Broadleaf_1", "Trees/Broadleaf_2"), p, VR(5.5f, 8f), ArtLibrary.Fit.Height, VR(0, 360));
                return;
            }
            var trunk = new Color(0.3f, 0.22f, 0.15f);
            Factory.Prim(PrimitiveType.Cylinder, deco, p + Vector3.up, new Vector3(0.3f, 1f, 0.3f), dead ? new Color(0.2f, 0.18f, 0.16f) : trunk);
            if (dead)
            {
                Factory.Prim(PrimitiveType.Cube, deco, p + new Vector3(0.3f, 1.8f, 0), new Vector3(0.8f, 0.1f, 0.1f), new Color(0.2f, 0.18f, 0.16f))
                    .transform.rotation = Quaternion.Euler(0, LR(0, 180f), 30);
                return;
            }
            var leaf = new Color(0.15f, 0.32f, 0.18f) * LR(0.85f, 1.15f);
            for (int i = 0; i < 3; i++)
                Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * (1.6f + i * 0.7f), new Vector3(1.6f - i * 0.45f, 0.7f, 1.6f - i * 0.45f), leaf)
                    .transform.rotation = Quaternion.Euler(0, i * 30f, 0);
        }

        void BuildGoblinCamp()
        {
            GoblinCamp(new Vector3(MapI(128) + 0.5f, 0, MapI(80) + 0.5f));
            GoblinCamp(new Vector3(MapI(146) + 0.5f, 0, MapI(104) + 0.5f));
        }

        void GoblinCamp(Vector3 center)
        {
            var hide = new Color(0.55f, 0.42f, 0.28f);
            for (int i = 0; i < 7; i++)
            {
                var p = center + Quaternion.Euler(0, i * (360f / 7f) + 10f, 0) * Vector3.forward * 9f;
                var c = grid.WorldToCell(p);
                if (grid.IsBlocked(c)) continue;
                float shade = LR(0.8f, 1.1f);
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
                    .transform.rotation = Quaternion.Euler(LR(-20f, 20f), 0, LR(-20f, 20f));
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
            int gx = (int)Center - 26, gy = MapI(30);
            for (int row = 0; row < 11; row++)
                for (int col = 0; col < 18; col++)
                {
                    if (col == 8 || col == 9) continue; // aisle for the road
                    int x = gx + col * 3 + LRI(0, 2), y = gy + row * 4 + LRI(0, 2);
                    if (!Free(x, y)) continue;
                    var p = new Vector3(x + 0.5f, 0, y + 0.5f);
                    bool cross = LV < 0.3f;
                    if (art)
                    {
                        if (!cross) { LR(0.8f, 1.1f); LR(-8f, 8f); LR(-8f, 8f); } // keep RNG in step
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
                        Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.25f), stone * LR(0.8f, 1.1f))
                            .transform.rotation = Quaternion.Euler(LR(-8f, 8f), 0, LR(-8f, 8f));
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
                    if (!edge || (y == Crypt.yMax - 1 && x >= 142 && x <= 146)) continue;
                    grid.SetBlocked(x, y, true);
                    float shade = (x + y) % 2 == 0 ? LR(0.85f, 1.1f) : 1f;
                    bool sideX = y == Crypt.yMin || y == Crypt.yMax - 1;
                    // Graveyard/stone-wall is a 1-unit wall piece running along X.
                    if (ArtBox("Graveyard/stone-wall", new Vector3(x + 0.5f, 0, y + 0.5f), new Vector3(1.05f, 2.6f, 0.7f), sideX ? 0f : 90f) != null) continue;
                    if ((x + y) % 2 == 0)
                        Factory.Prim(PrimitiveType.Cube, deco, new Vector3(x + 0.5f, 1.6f, y + 0.5f), new Vector3(2f, 3.2f, 2f), wall * shade);
                }
            // pillars and braziers
            var fire = new Color(0.3f, 0.85f, 1f);
            foreach (var p in new[] { new Vector3(138.5f, 0, 23.5f), new Vector3(150.5f, 0, 23.5f), new Vector3(138.5f, 0, 14.5f), new Vector3(150.5f, 0, 14.5f) })
            {
                if (Art("Graveyard/pillar-large", p, 3f, ArtLibrary.Fit.Height) != null)
                    Art("Graveyard/fire-basket", p + Vector3.up * 3f, 0.7f, ArtLibrary.Fit.Height, 0f, false);
                else Factory.Prim(PrimitiveType.Cylinder, deco, p + Vector3.up * 1.5f, new Vector3(0.9f, 1.5f, 0.9f), wall);
                var bc = Factory.Prim(PrimitiveType.Sphere, deco, p + Vector3.up * 3.45f, Vector3.one * 0.32f, fire, false, Mat.Glow(fire));
                var l = new GameObject("Brazier").AddComponent<Light>();
                l.transform.SetParent(deco, false);
                l.transform.position = p + Vector3.up * 3.5f;
                l.type = LightType.Point;
                l.color = fire;
                l.range = 9f;
                l.intensity = 1.5f;
                NightLight.Add(l, 1f, 1.1f);
                PropFire.Add(deco, p + Vector3.up * 3.3f, fire, 0.65f, true, l, bc.transform);
                Sfx.LoopAt("fire_loop", p + Vector3.up * 3f, 0.6f, 10f);
                grid.SetBlocked((int)p.x, (int)p.z, true);
            }
            // throne
            if (Art("Graveyard/altar-stone", new Vector3(144.5f, 0, 11.2f), 4.5f, ArtLibrary.Fit.Width) != null)
            {
                Art("Graveyard/candle-multiple", new Vector3(141.8f, 0, 12.4f), 0.8f, ArtLibrary.Fit.Height, 0f, false);
                Art("Graveyard/candle-multiple", new Vector3(147.2f, 0, 12.4f), 0.8f, ArtLibrary.Fit.Height, 90f, false);
                Art("Graveyard/coffin", new Vector3(137.5f, 0, 11.5f), 2f, ArtLibrary.Fit.Width, 90f);
                Art("Graveyard/coffin", new Vector3(151.5f, 0, 11.5f), 2f, ArtLibrary.Fit.Width, 90f);
            }
            else Factory.Prim(PrimitiveType.Cube, deco, new Vector3(144.5f, 0.3f, 11.5f), new Vector3(5f, 0.6f, 2f), wall * 1.2f);
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(144.5f, 2f, 10.5f), new Vector3(2f, 3.5f, 0.6f), new Color(0.25f, 0.1f, 0.35f));
            grid.BlockRect(142, 10, 147, 11);
        }

        void BuildQuarry()
        {
            for (int y = 8; y < OldSize - 8; y++)
                for (int x = 6; x < MapI(64); x++)
                {
                    float dx = x - Center, dz = y - Center;
                    if (Mathf.Abs(dx) < Mathf.Abs(dz) * 0.9f) continue;
                    if (LV > 0.035f || !Free(x, y) || !SpacedFrom(x, y, 1)) continue;
                    int tier = x > Map(42) ? 0 : x > Map(22) ? (LV < 0.75f ? 1 : 0) : (LV < 0.6f ? 2 : 1);
                    ResourceNode.Create(ResourceKind.Rock, tier, new Vector3(x + 0.5f, 0, y + 0.5f), nodes);
                }
            // Big boulders
            for (int i = 0; i < 110; i++)
            {
                int x = LRI(8, MapI(60)), y = LRI(MapI(40), MapI(120));
                if (ZoneAt(new Vector3(x, 0, y)) != "Ironvein Quarry" || !Free(x, y) || !SpacedFrom(x, y, 2)) continue;
                float s = LR(1.5f, 2.6f);
                float shade = LR(0.85f, 1.1f);
                var rot = Quaternion.Euler(LR(0f, 360f), LR(0f, 360f), LR(0f, 360f));
                grid.BlockRect(x - 1, y - 1, x + 1, y + 1);
                if (Art(Pick("Rocks/Boulder_1", "Rocks/Boulder_2", "Rocks/Boulder_3"), new Vector3(x + 0.5f, 0, y + 0.5f), s * 1.5f, ArtLibrary.Fit.Width, VR(0, 360)) != null)
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
        /// <summary>The small scattered props (flowers, pebbles, tufts): switched off by Settings > Graphics > Small details.</summary>
        public static Transform DetailRoot { get; private set; }

        void ScatterDetail()
        {
            if (!art) return;
            // Under the decoration (so they are batched with it) but in a group of their own the settings can hide.
            var decoration = deco;
            DetailRoot = Factory.Empty("Details", deco, Vector3.zero);
            deco = DetailRoot;
            try { ScatterDetailInto(); } finally { deco = decoration; }
            GameSettings.Apply();
        }

        void ScatterDetailInto()
        {
            for (int y = 5; y < H - 5; y++)
                for (int x = 5; x < W - 5; x++)
                {
                    if (!Free(x, y)) continue;
                    // The world is big: thin out the small props far from the village to keep the object count sane.
                    float far = FarFromTowns(x, y);
                    if (far > 60f && vr.NextDouble() < Mathf.Lerp(0.35f, 0.7f, (far - 60f) / 80f)) continue;
                    var p = new Vector3(x + (float)vr.NextDouble(), 0, y + (float)vr.NextDouble());
                    string zone = ZoneAt(p);
                    double r = vr.NextDouble();
                    float road = surface.RoadWeight(p.x, p.z);
                    if (road > 0.05f && road < 0.6f)
                    {
                        // Stones and weeds along the edges of the roads
                        if (r < 0.07) Art(Pick("Rocks/Pebble_1", "Rocks/Pebble_2", "Rocks/Pebble_3"), p, VR(0.25f, 0.5f), ArtLibrary.Fit.Width, VR(0, 360), false);
                        continue;
                    }
                    if (fancyGround && r < 0.10 && IsGrassModelRoll(zone, r)) continue; // blade grass replaces the 3D tufts
                    switch (zone)
                    {
                        case "Whisperwood":
                            if (r < 0.10) Art(Pick("Nature/grass", "Nature/grass_large", "Nature/grass_leafsLarge"), p, VR(0.4f, 0.8f), ArtLibrary.Fit.Height, VR(0, 360), false);
                            else if (r < 0.13) Art(Pick("Plants/Flowers_Yellow", "Plants/Flowers_Purple", "Plants/Fern"), p, VR(0.45f, 0.8f), ArtLibrary.Fit.Width, VR(0, 360), false);
                            else if (r < 0.145) Art(Pick("Plants/Bush", "Plants/Bush_Flowers"), p, VR(0.7f, 1.2f), ArtLibrary.Fit.Height, VR(0, 360));
                            else if (r < 0.155) Art("Plants/Mushrooms", p, VR(0.3f, 0.5f), ArtLibrary.Fit.Height, VR(0, 360), false);
                            break;
                        case "Goblin Encampment":
                            if (r < 0.05) Art(Pick("Nature/grass", "Nature/grass_large"), p, VR(0.4f, 0.7f), ArtLibrary.Fit.Height, VR(0, 360), false);
                            else if (r < 0.06) Art(Pick("Plants/Bush", "Rocks/Pebble_2"), p, VR(0.5f, 0.9f), ArtLibrary.Fit.Height, VR(0, 360));
                            break;
                        case "Ironvein Quarry":
                            if (r < 0.04) Art(Pick("Rocks/Pebble_1", "Rocks/Pebble_2", "Rocks/Pebble_3"), p, VR(0.5f, 0.9f), ArtLibrary.Fit.Width, VR(0, 360), false);
                            else if (r < 0.06) Art("Nature/grass", p, VR(0.3f, 0.5f), ArtLibrary.Fit.Height, VR(0, 360), false);
                            break;
                        case "Forsaken Graveyard":
                            if (r < 0.04) Art(Pick("Nature/grass_leafsLarge", "Nature/grass"), p, VR(0.4f, 0.7f), ArtLibrary.Fit.Height, VR(0, 360), false);
                            else if (r < 0.048) Art("Plants/Mushrooms", p, VR(0.3f, 0.45f), ArtLibrary.Fit.Height, VR(0, 360), false);
                            break;
                        default:
                            ScatterRegionDetail(zone, p, r);
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
                case Frostpeak: return r < 0.05;
                case Badlands: return r < 0.02;
                case Ashen: return r < 0.02;
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
