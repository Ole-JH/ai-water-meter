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
        public static readonly RectInt Town = new RectInt(66, 66, 29, 29); // cells 66..94
        public static readonly RectInt Crypt = new RectInt(68, 4, 25, 19);  // cells 68..92, 4..22

        public Texture2D MapTexture { get; private set; }
        public Vector3 SpawnPoint => new Vector3(80.5f, 0f, 77.5f);

        WorldGrid grid;
        Color[] pixels;
        bool[] reserved; // no trees / rocks here (roads, town, water)
        Transform root, nodes, npcs, deco;

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

            grid = new WorldGrid(W, H);
            pixels = new Color[W * H];
            reserved = new bool[W * H];
            root = new GameObject("World").transform;
            nodes = Factory.Empty("Resources", root, Vector3.zero);
            npcs = Factory.Empty("NPCs", root, Vector3.zero);
            deco = Factory.Empty("Decoration", root, Vector3.zero);

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
            BuildGround();

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
                }

            for (int y = Town.yMin; y < Town.yMax; y++)
                for (int x = Town.xMin; x < Town.xMax; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.9f, y * 0.9f) * 0.15f;
                    Paint(x, y, cobbleC * (0.9f + n));
                    Reserve(x, y);
                }
        }

        void PaintRoads()
        {
            // Four roads leading out of the gates, gently meandering.
            for (int i = Town.yMax; i < H - 6; i++) RoadDot(80 + Mathf.Sin(i * 0.08f) * 4f, i);         // north
            for (int i = Town.yMin; i > 6; i--) RoadDot(80 + Mathf.Sin(i * 0.1f) * 3f, i);              // south
            for (int i = Town.xMax; i < W - 6; i++) RoadDot(i, 80 + Mathf.Sin(i * 0.09f) * 4f);         // east
            for (int i = Town.xMin; i > 6; i--) RoadDot(i, 80 + Mathf.Sin(i * 0.07f) * 4f);             // west
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
                    }
                    else if (d < edge + 1.5f)
                    {
                        Paint(x, y, new Color(0.55f, 0.5f, 0.35f)); // sandy shore
                        Reserve(x, y);
                    }
                }

            // Water surface plane for a bit of shine
            var water = Factory.Prim(PrimitiveType.Cylinder, deco, new Vector3(center.x, -0.02f, center.y),
                new Vector3(radius * 2f + 1f, 0.01f, radius * 2f + 1f), waterC);
            water.GetComponent<Renderer>().sharedMaterial.SetFloat("_Glossiness", 0.8f);

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
            Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * h * 0.5f, new Vector3(Random.Range(3.5f, 5f), h, Random.Range(3.5f, 5f)),
                new Color(0.34f, 0.32f, 0.3f) * Random.Range(0.85f, 1.1f)).transform.rotation = Quaternion.Euler(0, Random.Range(0, 90f), 0);
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
                Factory.Prim(PrimitiveType.Cube, deco, new Vector3(g.x + 0.5f, 2f, g.y + 0.5f), new Vector3(1.4f, 4f, 1.4f), wood * 0.85f);
                var torchC = new Color(1f, 0.6f, 0.2f);
                Factory.Prim(PrimitiveType.Sphere, deco, new Vector3(g.x + 0.5f, 4.3f, g.y + 0.5f), Vector3.one * 0.4f, torchC, false, Mat.Glow(torchC));
            }

            House(new RectInt(68, 86, 7, 6), new Color(0.75f, 0.68f, 0.55f), new Color(0.55f, 0.2f, 0.15f));
            House(new RectInt(86, 86, 7, 6), new Color(0.7f, 0.65f, 0.55f), new Color(0.25f, 0.3f, 0.5f));
            House(new RectInt(68, 68, 6, 6), new Color(0.72f, 0.62f, 0.5f), new Color(0.3f, 0.45f, 0.25f));
            // Smithy: open-sided shelter
            var smithy = new RectInt(87, 68, 6, 5);
            for (int i = 0; i < 4; i++)
            {
                float px = i % 2 == 0 ? smithy.xMin + 0.5f : smithy.xMax - 0.5f;
                float pz = i < 2 ? smithy.yMin + 0.5f : smithy.yMax - 0.5f;
                Factory.Prim(PrimitiveType.Cube, deco, new Vector3(px, 1.5f, pz), new Vector3(0.4f, 3f, 0.4f), wood);
                grid.SetBlocked((int)px, (int)pz, true);
            }
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(smithy.center.x, 3.1f, smithy.center.y), new Vector3(smithy.width + 0.6f, 0.3f, smithy.height + 0.6f), new Color(0.35f, 0.25f, 0.18f));
            var forgeC = new Color(1f, 0.35f, 0.05f);
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(91.5f, 0.6f, 71.5f), new Vector3(1.6f, 1.2f, 1.6f), new Color(0.3f, 0.28f, 0.27f));
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(91.5f, 1.25f, 71.5f), new Vector3(1.2f, 0.1f, 1.2f), forgeC, false, Mat.Glow(forgeC));
            grid.BlockRect(91, 71, 92, 72);

            // Well in the square
            Factory.Prim(PrimitiveType.Cylinder, deco, new Vector3(80.5f, 0.5f, 82.5f), new Vector3(1.8f, 0.5f, 1.8f), new Color(0.5f, 0.48f, 0.45f));
            Factory.Prim(PrimitiveType.Cylinder, deco, new Vector3(80.5f, 1.01f, 82.5f), new Vector3(1.4f, 0.01f, 1.4f), waterC);
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(80.5f, 2.3f, 82.5f), new Vector3(2.2f, 0.2f, 0.3f), wood);
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
                "Potions! Fresh potions! I also buy anything you drag out of those monsters.", npcs);
            Npc.Create("Sister Mae", "Healer", NpcRole.Healer, new Vector3(85.5f, 0, 84.5f), new Color(0.9f, 0.9f, 0.85f),
                "The Light watches over you, child. Let me tend your wounds.", npcs);

            // Market stalls & crates
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(70.5f + i * 2f, 0, 76.5f);
                Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * 0.4f, new Vector3(1.4f, 0.8f, 1f), wood);
                Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * 2f, new Vector3(1.7f, 0.1f, 1.3f), i % 2 == 0 ? new Color(0.7f, 0.2f, 0.2f) : new Color(0.85f, 0.8f, 0.6f));
                grid.SetBlocked((int)p.x, (int)p.z, true);
            }
        }

        void Palisade(int x, int y, Color wood)
        {
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(x + 0.5f, 1.4f, y + 0.5f), new Vector3(0.9f, 2.8f, 0.9f), wood * Random.Range(0.85f, 1.05f));
            grid.SetBlocked(x, y, true);
        }

        void House(RectInt r, Color wall, Color roof)
        {
            var c = new Vector3(r.center.x, 0, r.center.y);
            Factory.Prim(PrimitiveType.Cube, deco, c + Vector3.up * 1.5f, new Vector3(r.width, 3f, r.height), wall);
            float side = r.width / 1.414f;
            var roofGo = Factory.Prim(PrimitiveType.Cube, deco, c + Vector3.up * 3f, new Vector3(side, side, r.height + 0.6f), roof);
            roofGo.transform.rotation = Quaternion.Euler(0, 0, 45);
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(c.x, 0.9f, r.yMin - 0.01f), new Vector3(1f, 1.8f, 0.1f), new Color(0.3f, 0.2f, 0.12f));
            var win = new Color(1f, 0.85f, 0.4f);
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(c.x - r.width * 0.3f, 1.8f, r.yMin - 0.01f), new Vector3(0.7f, 0.6f, 0.1f), win, false, Mat.Glow(win * 0.6f));
            grid.BlockRect(r.xMin, r.yMin, r.xMax - 1, r.yMax - 1);
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
                var tent = Factory.Prim(PrimitiveType.Cube, deco, grid.CellToWorld(c) + Vector3.up * 0.9f, new Vector3(2f, 2f, 2.4f), hide * Random.Range(0.8f, 1.1f));
                tent.transform.rotation = Quaternion.LookRotation(center - p) * Quaternion.Euler(0, 0, 45);
                grid.BlockRect(c.x - 1, c.y - 1, c.x + 1, c.y + 1);
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
                    if (cross)
                    {
                        Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * 0.75f, new Vector3(0.2f, 1.5f, 0.2f), stone);
                        Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * 1.1f, new Vector3(0.8f, 0.2f, 0.2f), stone);
                    }
                    else
                        Factory.Prim(PrimitiveType.Cube, deco, p + Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.25f), stone * Random.Range(0.8f, 1.1f))
                            .transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), 0, Random.Range(-8f, 8f));
                    Paint(x, y - 1, new Color(0.25f, 0.2f, 0.15f));
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
                    Reserve(x, y);
                    bool edge = x == Crypt.xMin || x == Crypt.xMax - 1 || y == Crypt.yMin || y == Crypt.yMax - 1;
                    if (!edge || (y == Crypt.yMax - 1 && x >= 78 && x <= 82)) continue;
                    grid.SetBlocked(x, y, true);
                    if ((x + y) % 2 == 0)
                        Factory.Prim(PrimitiveType.Cube, deco, new Vector3(x + 0.5f, 1.6f, y + 0.5f), new Vector3(2f, 3.2f, 2f), wall * Random.Range(0.85f, 1.1f));
                }
            // pillars and braziers
            var fire = new Color(0.3f, 0.85f, 1f);
            foreach (var p in new[] { new Vector3(74.5f, 0, 18.5f), new Vector3(86.5f, 0, 18.5f), new Vector3(74.5f, 0, 9.5f), new Vector3(86.5f, 0, 9.5f) })
            {
                Factory.Prim(PrimitiveType.Cylinder, deco, p + Vector3.up * 1.5f, new Vector3(0.9f, 1.5f, 0.9f), wall);
                Factory.Prim(PrimitiveType.Sphere, deco, p + Vector3.up * 3.3f, Vector3.one * 0.6f, fire, false, Mat.Glow(fire));
                var l = new GameObject("Brazier").AddComponent<Light>();
                l.transform.SetParent(deco, false);
                l.transform.position = p + Vector3.up * 3.5f;
                l.type = LightType.Point;
                l.color = fire;
                l.range = 9f;
                l.intensity = 1.5f;
                grid.SetBlocked((int)p.x, (int)p.z, true);
            }
            // throne
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(80.5f, 0.3f, 6.5f), new Vector3(5f, 0.6f, 2f), wall * 1.2f);
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
                Factory.Prim(PrimitiveType.Cube, deco, new Vector3(x + 0.5f, s * 0.4f, y + 0.5f), Vector3.one * s, new Color(0.42f, 0.4f, 0.37f) * Random.Range(0.85f, 1.1f))
                    .transform.rotation = Random.rotation;
                grid.BlockRect(x - 1, y - 1, x + 1, y + 1);
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

            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "Ground";
            ground.transform.SetParent(root, false);
            ground.transform.position = new Vector3(W * 0.5f, 0f, H * 0.5f);
            ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.localScale = new Vector3(W, H, 1f);
            var mat = Mat.New(Color.white);
            mat.mainTexture = MapTexture;
            ground.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
