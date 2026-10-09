using UnityEngine;

namespace Shadowfall
{
    /// <summary>A town or hamlet: safe from monsters (the server keeps the same list in content.js TOWNS).</summary>
    public class Settlement
    {
        public string Name;
        public RectInt Rect;
        public bool Walled;
        /// <summary>Where its waystone stands (see <see cref="Waystone"/>).</summary>
        public Vector3 Waystone;
        public Vector3 Center => new Vector3(Rect.center.x, 0f, Rect.center.y);
        public bool Contains(float x, float z) => x >= Rect.xMin && x < Rect.xMax && z >= Rect.yMin && z < Rect.yMax;
    }

    /// <summary>
    /// The outer lands, added when the world grew from 288 to 576 tiles a side. The old world (Hollowmere and its four
    /// zones) stays in the south-west corner exactly where it was; around it, behind a broken ridge with passes:
    ///   north:      the Frostpeak Wilds, pine forest and highlands under snow most of the year; Frosthaven, Pinecrest
    ///   east:       the Sunscar Badlands, dry steppe, sand and rock with an oasis; Saltreach
    ///   north-east: the Ashen Reach, dead land full of ruins; Emberwatch, the last outpost
    /// Roads link Hollowmere to Frosthaven and Saltreach and both of those to Emberwatch.
    /// </summary>
    public partial class WorldGenerator
    {
        public const string Frostpeak = "Frostpeak Wilds", Badlands = "Sunscar Badlands", Ashen = "Ashen Reach";

        static readonly Color frostC = new Color(0.27f, 0.35f, 0.3f);
        static readonly Color badlandsC = new Color(0.62f, 0.52f, 0.33f);
        static readonly Color ashC = new Color(0.3f, 0.27f, 0.25f);

        // Pinecrest straddles the north road: centre it on where the road runs at its middle.
        const int PinecrestZ = 336;
        static readonly int PinecrestX = Mathf.RoundToInt(Center + Mathf.Sin((PinecrestZ + 11) * 0.05f) * 6f);

        /// <summary>Every town and hamlet. Hollowmere first (its rect is <see cref="Town"/>, written out: static fields in
        /// the other part of this class may not be initialized yet when this one is).</summary>
        public static readonly Settlement[] Towns =
        {
            new Settlement { Name = "Hollowmere Village", Rect = new RectInt(116, 116, 57, 57), Walled = true, Waystone = new Vector3(140.5f, 0f, 154.5f) },
            new Settlement { Name = "Frosthaven", Rect = new RectInt(126, 446, 37, 37), Walled = true, Waystone = new Vector3(140.5f, 0f, 460.5f) },
            new Settlement { Name = "Saltreach", Rect = new RectInt(446, 126, 37, 37), Walled = true, Waystone = new Vector3(460.5f, 0f, 140.5f) },
            new Settlement { Name = "Emberwatch", Rect = new RectInt(446, 446, 37, 37), Walled = true, Waystone = new Vector3(460.5f, 0f, 460.5f) },
            new Settlement { Name = "Pinecrest", Rect = new RectInt(PinecrestX - 14, PinecrestZ, 29, 22), Walled = false,
                             Waystone = new Vector3(PinecrestX + 3.5f, 0f, PinecrestZ + 11.5f) },
        };

        public static Settlement TownAt(Vector3 p)
        {
            foreach (var t in Towns) if (t.Contains(p.x, p.z)) return t;
            return null;
        }

        public static Settlement TownNamed(string name)
        {
            foreach (var t in Towns) if (t.Name == name) return t;
            return null;
        }

        static bool InWalledTown(int x, int y)
        {
            foreach (var t in Towns) if (t.Walled && t.Contains(x, y)) return true;
            return false;
        }

        /// <summary>
        /// A walking path in a town: Hollowmere's cobbled streets, square and the lane inside its wall; the small towns'
        /// cross streets and plazas; Pinecrest's stretch of road. Snow never piles up deep there (<see cref="SnowField"/>).
        /// </summary>
        public static bool IsTownStreet(int x, int y)
        {
            foreach (var t in Towns)
            {
                if (!t.Contains(x, y)) continue;
                var r = t.Rect;
                if (t.Name == "Hollowmere Village")
                    return IsHollowmereStreet(x, y) || x <= r.xMin + 2 || x >= r.xMax - 3 || y <= r.yMin + 2 || y >= r.yMax - 3;
                if (!t.Walled) return Mathf.Abs(x - PinecrestX) <= 3;
                int cx = r.xMin + r.width / 2, cy = r.yMin + r.height / 2;
                return SmallTownStreet(x, y, cx, cy);
            }
            return false;
        }

        static bool SmallTownStreet(int x, int y, int cx, int cy) =>
            Mathf.Abs(x - cx) <= 2 || Mathf.Abs(y - cy) <= 2 || (Mathf.Abs(x - cx) <= 5 && Mathf.Abs(y - cy) <= 5);

        /// <summary>Within <paramref name="margin"/> tiles of a town or hamlet (nothing grows there).</summary>
        static bool NearTown(int x, int y, int margin)
        {
            foreach (var t in Towns)
                if (x >= t.Rect.xMin - margin && x < t.Rect.xMax + margin && y >= t.Rect.yMin - margin && y < t.Rect.yMax + margin) return true;
            return false;
        }

        /// <summary>How far (chessboard distance) a tile is from the nearest town's edge: small props thin out with it.</summary>
        public static float FarFromTowns(float x, float y)
        {
            float best = float.MaxValue;
            foreach (var t in Towns)
            {
                float dx = Mathf.Max(0f, Mathf.Max(t.Rect.xMin - x, x - t.Rect.xMax));
                float dy = Mathf.Max(0f, Mathf.Max(t.Rect.yMin - y, y - t.Rect.yMax));
                best = Mathf.Min(best, Mathf.Max(dx, dy) + 30f); // the old rule measured from Hollowmere's middle
            }
            return best;
        }

        /// <summary>How much a tile belongs to the old world and to each outer region (sums to 1, wobbly seams at the old edge).</summary>
        static void RegionWeights(float x, float y, out float old, out float frost, out float bad, out float ash)
        {
            float wob = (Mathf.PerlinNoise(x * 0.03f + 5f, y * 0.03f + 9f) - 0.5f) * 20f;
            float n = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(OldSize - 14f, OldSize + 14f, y + wob));
            float e = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(OldSize - 14f, OldSize + 14f, x - wob));
            old = (1f - n) * (1f - e);
            frost = n * (1f - e);
            bad = e * (1f - n);
            ash = n * e;
        }

        /// <summary>Ground textures of the outer regions, blended over the old world's by how much the tile belongs to them.</summary>
        void RegionSurface(int x, int y, float old, float frost, float bad, float ash)
        {
            float a = Mathf.PerlinNoise(x * 0.07f + 11f, y * 0.07f + 5f);
            float b = Mathf.PerlinNoise(x * 0.12f + 31f, y * 0.12f + 71f);
            var w = new float[GroundSurface.Layers];
            // Frostpeak: meadow and forest floor, rocky highlands in the far north.
            float high = Mathf.Clamp01((y - 480f) / 50f);
            float forest = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((a - 0.4f) * 4f));
            w[GroundSurface.Grass] += frost * (1f - forest) * (1f - high);
            w[GroundSurface.Forest] += frost * forest * (1f - high);
            w[GroundSurface.Gravel] += frost * high * (b > 0.45f ? 1f : 0.5f);
            w[GroundSurface.Grass] += frost * high * (b > 0.45f ? 0f : 0.5f);
            // Sunscar Badlands: dry grass, sand drifts, bare dirt and rock.
            w[GroundSurface.Dry] += bad * (b > 0.58f ? 0.2f : 0.75f);
            w[GroundSurface.Sand] += bad * (b > 0.58f ? 0.8f : 0.1f);
            w[GroundSurface.Gravel] += bad * (a > 0.68f ? 0.6f : 0f);
            w[GroundSurface.Dirt] += bad * 0.15f;
            // Ashen Reach: dead grass, ash-grey gravel and scorched dirt.
            w[GroundSurface.Dead] += ash * (a > 0.5f ? 0.3f : 0.7f);
            w[GroundSurface.Gravel] += ash * (a > 0.5f ? 0.6f : 0.15f);
            w[GroundSurface.Dirt] += ash * (b > 0.6f ? 0.5f : 0.15f);
            surface.Blend(x, y, w, 1f - old);
            var tint = Color.white * old + new Color(0.92f, 0.98f, 1.02f) * frost + new Color(1.06f, 1f, 0.9f) * bad + new Color(0.72f, 0.68f, 0.66f) * ash;
            tint.a = 1f;
            surface.Tint(x, y, tint);
        }

        // ------------------------------------------------------------------ the old edge

        /// <summary>
        /// Where the old world's north and east edges were, a broken ridge of rock: long stretches of cliff with gaps,
        /// always open where a road crosses.
        /// </summary>
        void BuildOldRidge()
        {
            var mountain = new Color(0.32f, 0.3f, 0.28f);
            for (int side = 0; side < 2; side++)
                for (int t = 6; t < OldSize + 4; t++)
                {
                    if (Mathf.PerlinNoise(t * 0.045f + side * 40f, 7.7f) < 0.5f) continue; // a pass
                    int line = OldSize + Mathf.RoundToInt((Mathf.PerlinNoise(t * 0.08f + side * 13f, 3.3f) - 0.5f) * 6f);
                    int px = side == 0 ? t : line, py = side == 0 ? line : t;
                    if (NearRoad(px, py, 7)) continue;
                    for (int k = -1; k <= 1; k++)
                    {
                        int cx = side == 0 ? px : px + k, cy = side == 0 ? py + k : py;
                        if (cx < 0 || cy < 0 || cx >= W || cy >= H || (reserved[Idx(cx, cy)] && !road[Idx(cx, cy)] && grid.IsBlocked(cx, cy))) continue;
                        grid.SetBlocked(cx, cy, true);
                        Reserve(cx, cy);
                        Paint(cx, cy, mountain * (0.8f + Mathf.PerlinNoise(cx * 0.4f, cy * 0.4f) * 0.3f));
                        surface.Set(cx, cy, GroundSurface.Gravel);
                    }
                    if (t % 3 == 0) Cliff(new Vector3(px + 0.5f, 0, py + 0.5f));
                }
        }

        bool NearRoad(int x, int y, int r)
        {
            for (int j = -r; j <= r; j++)
                for (int i = -r; i <= r; i++)
                {
                    int px = x + i, py = y + j;
                    if (px >= 0 && py >= 0 && px < W && py < H && road[Idx(px, py)]) return true;
                }
            return false;
        }

        void BuildRegionLakes()
        {
            // Frostpeak: cold mountain tarns
            BuildLake(new Vector2(60, 400), 10f, 1);
            BuildLake(new Vector2(212, 374), 8f, 1);
            BuildLake(new Vector2(236, 530), 9f, 1);
            BuildLake(new Vector2(42, 522), 7f, 1);
            // Sunscar: the oasis and two salt pans
            BuildLake(new Vector2(392, 232), 12f, 1);
            BuildLake(new Vector2(530, 58), 9f, 0);
            BuildLake(new Vector2(362, 60), 7f, 0);
            // Ashen Reach: murky pools
            BuildLake(new Vector2(362, 522), 9f, 1);
            BuildLake(new Vector2(530, 382), 8f, 1);
        }

        /// <summary>Which outer region a tile is mostly in (null in the old world).</summary>
        static string RegionOf(int x, int y)
        {
            RegionWeights(x, y, out float old, out float frost, out float bad, out float ash);
            if (old >= frost && old >= bad && old >= ash) return null;
            return frost >= bad && frost >= ash ? Frostpeak : bad >= ash ? Badlands : Ashen;
        }

        // ------------------------------------------------------------------ the wilds

        void BuildRegions()
        {
            // The Ashen King's throne: the biggest ruin, in the heart of the Reach (server spawners at 373.5, 373.5).
            if (Ruin(366, 366, 15, true))
                Art("Graveyard/altar-stone", new Vector3(373.5f, 0, 378.6f), 4f, ArtLibrary.Fit.Width, 180f);
            // A clearing at each outer-land dungeon entrance.
            foreach (var d in DungeonDef.All)
            {
                if (!d.World) continue;
                for (int j = -6; j <= 6; j++)
                    for (int i = -6; i <= 6; i++)
                        if (i * i + j * j <= 36) Reserve(Mathf.FloorToInt(d.Design.x) + i, Mathf.FloorToInt(d.Design.y) + j);
            }
            // The camps go up before the trees, rocks and ruins, which keep clear of them.
            foreach (var camp in Camps) GoblinCamp(camp);

            // Trees to cut, rocks to mine, and the scenery that makes each region its own.
            for (int y = 6; y < H - 6; y++)
                for (int x = 6; x < W - 6; x++)
                {
                    if (x < OldSize - 6 && y < OldSize - 6) continue;
                    string region = RegionOf(x, y);
                    if (region == null || NearTown(x, y, 4) || NearCamp(x, y)) continue;
                    if (region == Frostpeak)
                    {
                        float density = 0.008f + Mathf.PerlinNoise(x * 0.06f + 3f, y * 0.06f) * 0.028f;
                        if (y > 500) density *= 0.4f; // the bare highlands
                        if (LV > density || !Free(x, y) || !SpacedFrom(x, y, 1)) continue;
                        ResourceNode.Create(ResourceKind.Tree, y < 380 ? (LV < 0.6f ? 2 : 1) : 2, new Vector3(x + 0.5f, 0, y + 0.5f), nodes);
                        Paint(x, y, pixels[Idx(x, y)] * 0.7f);
                        surface.Set(x, y, GroundSurface.Forest, 0.5f);
                    }
                    else
                    {
                        if (LV > (region == Badlands ? 0.007f : 0.008f) || !Free(x, y) || !SpacedFrom(x, y, 1)) continue;
                        ResourceNode.Create(ResourceKind.Rock, x + y > 640 ? 2 : (LV < 0.65f ? 2 : 1), new Vector3(x + 0.5f, 0, y + 0.5f), nodes);
                    }
                }

            // Scenery: pines in the north, dead trees in the badlands and the Reach, boulders everywhere.
            for (int i = 0; i < 2600; i++)
            {
                int x = LRI(6, W - 6), y = LRI(6, H - 6);
                if (x < OldSize && y < OldSize) continue;
                string region = RegionOf(x, y);
                if (region == null || NearTown(x, y, 3) || NearCamp(x, y) || !Free(x, y) || !SpacedFrom(x, y, 2)) continue;
                float roll = LV;
                if (region == Frostpeak)
                {
                    if (roll < 0.85f) { Pine(new Vector3(x + 0.5f, 0, y + 0.5f), false); grid.SetBlocked(x, y, true); }
                    else Boulder(x, y);
                }
                else if (region == Badlands)
                {
                    if (roll < 0.3f) { Pine(new Vector3(x + 0.5f, 0, y + 0.5f), true); grid.SetBlocked(x, y, true); }
                    else if (roll < 0.55f) Boulder(x, y);
                }
                else
                {
                    if (roll < 0.55f) { Pine(new Vector3(x + 0.5f, 0, y + 0.5f), true); grid.SetBlocked(x, y, true); }
                    else if (roll < 0.7f) Boulder(x, y);
                }
            }

            // The Ashen Reach is littered with the ruins of whatever stood there before.
            int ruins = 0, lit = 0;
            for (int tries = 0; tries < 200 && ruins < 16; tries++)
            {
                int x = LRI(300, W - 20), y = LRI(300, H - 20);
                if (NearTown(x, y, 14) || NearRoad(x, y, 8) || NearCamp(x, y)) continue;
                if (Ruin(x, y, LRI(7, 12), lit < 7)) { ruins++; lit++; }
            }

        }

        /// <summary>
        /// Raider camps in the badlands (the last is the Warlord's) and Jarl Frostborn's camp in the far north-west: the
        /// same tents and barricades as the goblins'. The server has spawners at these points.
        /// </summary>
        static readonly Vector3[] Camps =
        {
            new Vector3(410.5f, 0, 84.5f), new Vector3(520.5f, 0, 250.5f), new Vector3(505.5f, 0, 72.5f), new Vector3(70.5f, 0, 470.5f),
        };

        static bool NearCamp(int x, int y)
        {
            foreach (var c in Camps)
                if (Mathf.Abs(x - c.x) < 17f && Mathf.Abs(y - c.z) < 17f) return true;
            return false;
        }

        void Boulder(int x, int y)
        {
            float s = LR(1.5f, 2.8f);
            float shade = LR(0.85f, 1.1f);
            var rot = Quaternion.Euler(LR(0f, 360f), LR(0f, 360f), LR(0f, 360f));
            grid.BlockRect(x - 1, y - 1, x + 1, y + 1);
            if (Art(Pick("Rocks/Boulder_1", "Rocks/Boulder_2", "Rocks/Boulder_3"), new Vector3(x + 0.5f, 0, y + 0.5f), s * 1.5f, ArtLibrary.Fit.Width, VR(0, 360)) != null)
                return;
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(x + 0.5f, s * 0.4f, y + 0.5f), Vector3.one * s, new Color(0.42f, 0.4f, 0.37f) * shade)
                .transform.rotation = rot;
        }

        /// <summary>A broken square of stone wall with a pillar or two; some keep a brazier burning. False if it didn't fit.</summary>
        bool Ruin(int x0, int y0, int size, bool brazier)
        {
            for (int y = y0 - 1; y <= y0 + size; y++)
                for (int x = x0 - 1; x <= x0 + size; x++)
                    if (!Free(x, y)) return false;
            var wall = new Color(0.3f, 0.29f, 0.31f);
            for (int y = y0; y < y0 + size; y++)
                for (int x = x0; x < x0 + size; x++)
                {
                    Paint(x, y, new Color(0.22f, 0.21f, 0.22f) * (0.9f + Mathf.PerlinNoise(x, y) * 0.2f));
                    surface.Set(x, y, GroundSurface.Cobble, 0.7f);
                    surface.Tint(x, y, new Color(0.6f, 0.58f, 0.6f));
                    Reserve(x, y);
                    bool edge = x == x0 || x == x0 + size - 1 || y == y0 || y == y0 + size - 1;
                    if (!edge || LV < 0.38f) continue; // fallen stretches
                    grid.SetBlocked(x, y, true);
                    bool alongX = y == y0 || y == y0 + size - 1;
                    float h = LR(1.2f, 2.6f), shade = LR(0.8f, 1.1f); // drawn either way: keeps art and primitive worlds in step
                    if (ArtBox("Graveyard/stone-wall", new Vector3(x + 0.5f, 0, y + 0.5f), new Vector3(1.05f, h, 0.7f), alongX ? 0f : 90f) != null) continue;
                    Factory.Prim(PrimitiveType.Cube, deco, new Vector3(x + 0.5f, h * 0.5f, y + 0.5f), new Vector3(1f, h, 1f), wall * shade);
                }
            var c = new Vector3(x0 + size * 0.5f, 0, y0 + size * 0.5f);
            int cx = Mathf.FloorToInt(c.x), cy = Mathf.FloorToInt(c.z);
            grid.SetBlocked(cx, cy, true);
            if (Art("Graveyard/pillar-large", new Vector3(cx + 0.5f, 0, cy + 0.5f), LR(2f, 3.2f)) == null)
                Factory.Prim(PrimitiveType.Cylinder, deco, new Vector3(cx + 0.5f, 1.4f, cy + 0.5f), new Vector3(0.9f, 1.4f, 0.9f), wall);
            if (brazier)
            {
                var fire = new Color(1f, 0.45f, 0.15f);
                var p = new Vector3(cx + 0.5f, 0, cy + 0.5f);
                Art("Graveyard/fire-basket", p + Vector3.up * 2.9f, 0.7f, ArtLibrary.Fit.Height, 0f, false);
                var l = new GameObject("RuinFire").AddComponent<Light>();
                l.transform.SetParent(deco, false);
                l.transform.position = p + Vector3.up * 3.4f;
                l.type = LightType.Point;
                l.color = fire;
                l.range = 9f;
                l.intensity = 1.4f;
                NightLight.Add(l, 0.8f, 1.1f);
                PropFire.Add(deco, p + Vector3.up * 3.2f, fire, 0.55f, true, l, null);
                Sfx.LoopAt("fire_loop", p + Vector3.up * 3f, 0.5f, 10f);
            }
            if (LV < 0.5f && art) Art(Pick("Graveyard/coffin", "Graveyard/trunk", "Graveyard/gravestone-broken"), c + new Vector3(1.5f, 0, -1.2f), 1.4f, ArtLibrary.Fit.Width, VR(0, 360));
            return true;
        }

        /// <summary>Small props for the outer regions (see ScatterDetail).</summary>
        void ScatterRegionDetail(string zone, Vector3 p, double r)
        {
            switch (zone)
            {
                case Frostpeak:
                    if (r < 0.05) Art(Pick("Nature/grass", "Nature/grass_large"), p, VR(0.35f, 0.6f), ArtLibrary.Fit.Height, VR(0, 360), false);
                    else if (r < 0.07) Art(Pick("Plants/Fern", "Plants/Bush", "Rocks/Pebble_1"), p, VR(0.5f, 0.9f), ArtLibrary.Fit.Height, VR(0, 360), false);
                    else if (r < 0.075) Art(Pick("Nature/stump_old", "Nature/log_large"), p, VR(0.6f, 1f), ArtLibrary.Fit.Height, VR(0, 360), false);
                    break;
                case Badlands:
                    if (r < 0.02) Art("Nature/grass", p, VR(0.3f, 0.5f), ArtLibrary.Fit.Height, VR(0, 360), false);
                    else if (r < 0.045) Art(Pick("Rocks/Pebble_1", "Rocks/Pebble_2", "Rocks/Pebble_3"), p, VR(0.4f, 0.9f), ArtLibrary.Fit.Width, VR(0, 360), false);
                    break;
                case Ashen:
                    if (r < 0.02) Art("Nature/grass_leafsLarge", p, VR(0.3f, 0.5f), ArtLibrary.Fit.Height, VR(0, 360), false);
                    else if (r < 0.04) Art(Pick("Rocks/Pebble_2", "Rocks/Pebble_3", "Nature/stump_old"), p, VR(0.4f, 0.8f), ArtLibrary.Fit.Width, VR(0, 360), false);
                    else if (r < 0.043) Art("Plants/Mushrooms", p, VR(0.3f, 0.45f), ArtLibrary.Fit.Height, VR(0, 360), false);
                    break;
            }
        }

        // ------------------------------------------------------------------ towns

        class TownStyle
        {
            public bool StoneWalls;
            public Color Wood, Yard;
            public int YardLayer;
            public string Banner;
            public string[] Houses;   // four models: south-west, south-east, north-west, north-east
            public Color[] Roofs;
        }

        void BuildOuterTowns()
        {
            var frost = TownNamed("Frosthaven");
            SmallTown(frost, new TownStyle
            {
                Wood = new Color(0.36f, 0.27f, 0.2f), Yard = new Color(0.3f, 0.38f, 0.3f), YardLayer = GroundSurface.Grass, Banner = "Town/banner-green",
                Houses = new[] { "Buildings/building_tavern_blue", "Buildings/building_blacksmith_blue", "Buildings/building_home_B_blue", "Buildings/building_church_blue" },
                Roofs = new[] { new Color(0.3f, 0.35f, 0.55f), new Color(0.35f, 0.25f, 0.18f), new Color(0.3f, 0.35f, 0.55f), new Color(0.3f, 0.32f, 0.45f) },
            });
            var c = frost.Center;
            CraftingStation.Create(SkillType.Smithing, c + new Vector3(6.5f, 0, -8f), root);
            CraftingStation.Create(SkillType.Cooking, c + new Vector3(-7.5f, 0, 8f), root);
            Npc.Create("Jarl Sigrun", "Lord of Frosthaven", NpcRole.QuestGiver, c + new Vector3(-2.5f, 0, 5.5f), new Color(0.3f, 0.4f, 0.6f),
                "Frosthaven stands because we stand. The wolves of the Wilds have grown bold.", npcs, true, null, false);
            Npc.Create("Trader Olaf", "General Goods", NpcRole.Vendor, c + new Vector3(5.5f, 0, 2.5f), new Color(0.5f, 0.35f, 0.25f),
                "Furs, rope, potions. Up here you buy what keeps you alive.", npcs, false, false, false).SellsAs(VendorKind.General);
            Npc.Create("Runesmith Halvard", "Weapons", NpcRole.Vendor, c + new Vector3(5.5f, 0, -3.5f), new Color(0.35f, 0.35f, 0.4f),
                "Steel folded in snowmelt. It remembers the cold.", npcs, true, false, false).SellsAs(VendorKind.Weapons);
            Npc.Create("Furrier Eska", "Armor", NpcRole.Vendor, c + new Vector3(-5.5f, 0, -3.5f), new Color(0.6f, 0.55f, 0.5f),
                "Wolf pelt over mail. Warm and hard to bite through.", npcs, false, false, false).SellsAs(VendorKind.Armor);
            Npc.Create("Healer Ingrid", "Healer", NpcRole.Healer, c + new Vector3(2.5f, 0, 5.5f), new Color(0.85f, 0.9f, 0.95f),
                "Frostbite or fangs? Sit by the fire and let me look.", npcs, false, null, false);

            var salt = TownNamed("Saltreach");
            SmallTown(salt, new TownStyle
            {
                Wood = new Color(0.55f, 0.42f, 0.28f), Yard = new Color(0.6f, 0.5f, 0.32f), YardLayer = GroundSurface.Sand, Banner = "Town/banner-red",
                Houses = new[] { "Buildings/building_market_blue", "Buildings/building_home_A_green", "Buildings/building_tavern_blue", "Buildings/building_windmill_blue" },
                Roofs = new[] { new Color(0.55f, 0.25f, 0.2f), new Color(0.3f, 0.45f, 0.25f), new Color(0.55f, 0.2f, 0.15f), new Color(0.5f, 0.3f, 0.2f) },
            });
            c = salt.Center;
            CraftingStation.Create(SkillType.Cooking, c + new Vector3(-7.5f, 0, 8f), root);
            Npc.Create("Caravan Master Rahim", "Caravans", NpcRole.QuestGiver, c + new Vector3(-2.5f, 0, 5.5f), new Color(0.75f, 0.6f, 0.35f),
                "Three caravans out, one came back. The Badlands are eating my business.", npcs, false, false, false);
            Npc.Create("Spice Trader Nadia", "Food & Drink", NpcRole.Vendor, c + new Vector3(5.5f, 0, 2.5f), new Color(0.7f, 0.35f, 0.25f),
                "Dates, salt fish, pepper from the far east. Taste first, pay after.", npcs, false, true, false).SellsAs(VendorKind.Food);
            Npc.Create("Gemcutter Zafir", "Rings & Amulets", NpcRole.Vendor, c + new Vector3(-5.5f, 0, -3.5f), new Color(0.3f, 0.3f, 0.6f),
                "The sand gives up the prettiest stones. And the strangest.", npcs, false, true, false).SellsAs(VendorKind.Curios);
            Npc.Create("Sandsmith Tariq", "Weapons", NpcRole.Vendor, c + new Vector3(5.5f, 0, -3.5f), new Color(0.6f, 0.45f, 0.3f),
                "A curved blade cuts on the way out too. Remember that.", npcs, true, false, false).SellsAs(VendorKind.Weapons);
            Npc.Create("Healer Amara", "Healer", NpcRole.Healer, c + new Vector3(2.5f, 0, 5.5f), new Color(0.9f, 0.85f, 0.7f),
                "Water first. Always water first. Then we see to the cuts.", npcs, false, null, false);

            var ember = TownNamed("Emberwatch");
            SmallTown(ember, new TownStyle
            {
                StoneWalls = true, Wood = new Color(0.3f, 0.24f, 0.2f), Yard = new Color(0.32f, 0.29f, 0.27f), YardLayer = GroundSurface.Gravel, Banner = "Town/banner-red",
                Houses = new[] { "Buildings/building_blacksmith_blue", "Buildings/building_home_B_blue", "Buildings/building_church_blue", "Buildings/building_tavern_blue" },
                Roofs = new[] { new Color(0.35f, 0.25f, 0.18f), new Color(0.3f, 0.3f, 0.35f), new Color(0.3f, 0.32f, 0.45f), new Color(0.45f, 0.18f, 0.12f) },
            });
            c = ember.Center;
            CraftingStation.Create(SkillType.Smithing, c + new Vector3(6.5f, 0, -8f), root);
            CraftingStation.Create(SkillType.Cooking, c + new Vector3(-7.5f, 0, 8f), root);
            Npc.Create("Commander Varek", "Warden of Emberwatch", NpcRole.QuestGiver, c + new Vector3(-2.5f, 0, 5.5f), new Color(0.45f, 0.12f, 0.1f),
                "This is the last wall between the Reach and everything you love. Make yourself useful.", npcs, true, null, false);
            Npc.Create("Quartermaster Bryn", "Armor", NpcRole.Vendor, c + new Vector3(5.5f, 0, 2.5f), new Color(0.4f, 0.38f, 0.36f),
                "Plate, mail, shields. Sign here. Try to bring it back in one piece.", npcs, true, false, false).SellsAs(VendorKind.Armor);
            Npc.Create("Smuggler Kett", "General Goods", NpcRole.Vendor, c + new Vector3(-5.5f, 0, -3.5f), new Color(0.25f, 0.22f, 0.2f),
                "Officially, I'm not here. Unofficially: potions, half price. Well. Most of the price.", npcs, false, false, false).SellsAs(VendorKind.General);
            Npc.Create("Ashwarden Lyra", "Healer", NpcRole.Healer, c + new Vector3(2.5f, 0, 5.5f), new Color(0.95f, 0.8f, 0.6f),
                "The ash gets in the lungs and the wounds. Breathe out. Slowly.", npcs, false, null, false);

            Hamlet(TownNamed("Pinecrest"));
        }

        /// <summary>
        /// A small walled town on a 37x37 square: gates in the middle of every side, two cobbled streets crossing at a
        /// plaza with a well, a building in each corner block, lanterns, gate towers and banners.
        /// </summary>
        void SmallTown(Settlement t, TownStyle st)
        {
            var r = t.Rect;
            int x0 = r.xMin, y0 = r.yMin, x1 = r.xMax - 1, y1 = r.yMax - 1, cx = x0 + r.width / 2, cy = y0 + r.height / 2;
            bool Street(int x, int y) => SmallTownStreet(x, y, cx, cy);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Reserve(x, y);
                    if (Street(x, y))
                    {
                        Paint(x, y, cobbleC * (0.9f + Mathf.PerlinNoise(x * 0.9f, y * 0.9f) * 0.15f));
                        surface.Set(x, y, GroundSurface.Cobble);
                        if (Mathf.PerlinNoise(x * 0.21f + 9f, y * 0.21f) > 0.68f) surface.Set(x, y, GroundSurface.Dirt, 0.4f);
                    }
                    else
                    {
                        bool lane = x <= x0 + 1 || x >= x1 - 1 || y <= y0 + 1 || y >= y1 - 1;
                        surface.Set(x, y, st.YardLayer, 0.85f);
                        if (lane || Mathf.PerlinNoise(x * 0.17f + 3f, y * 0.17f + 11f) > 0.62f) surface.Set(x, y, GroundSurface.Dirt, lane ? 0.75f : 0.5f);
                        Paint(x, y, Color.Lerp(st.Yard, dirtC, lane ? 0.7f : 0.2f));
                    }
                }

            // Walls with gates (cells cx-2..cx+2) and a tower either side of each gate
            for (int i = x0; i <= x1; i++)
            {
                if (Mathf.Abs(i - cx) <= 2) continue;
                TownWall(i, y0, true, st);
                TownWall(i, y1, true, st);
            }
            for (int i = y0 + 1; i < y1; i++)
            {
                if (Mathf.Abs(i - cy) <= 2) continue;
                TownWall(x0, i, false, st);
                TownWall(x1, i, false, st);
            }
            foreach (var g in new[] { new Vector2(cx - 3, y0), new Vector2(cx + 3, y0), new Vector2(cx - 3, y1), new Vector2(cx + 3, y1),
                                      new Vector2(x0, cy - 3), new Vector2(x0, cy + 3), new Vector2(x1, cy - 3), new Vector2(x1, cy + 3) })
            {
                var gp = new Vector3(g.x + 0.5f, 0, g.y + 0.5f);
                if (Art("Buildings/building_tower_A_blue", gp, 5.2f * BuildingScale, ArtLibrary.Fit.Height, VR(0, 4) * 90f) != null) continue;
                Factory.Prim(PrimitiveType.Cube, deco, gp + Vector3.up * 2f, new Vector3(1.4f, 4f, 1.4f), st.Wood * 0.85f);
                var torchC = new Color(1f, 0.6f, 0.2f);
                var tc = Factory.Prim(PrimitiveType.Sphere, deco, gp + Vector3.up * 4.3f, Vector3.one * 0.3f, torchC, false, Mat.Glow(torchC));
                PropFire.Add(deco, gp + Vector3.up * 4.2f, torchC, 0.45f, true, null, tc.transform);
            }

            // A building in each corner block, doors toward the cross street
            var blocks = new[] { new Vector2Int(x0 + 3, y0 + 3), new Vector2Int(x1 - 10, y0 + 3), new Vector2Int(x0 + 3, y1 - 10), new Vector2Int(x1 - 10, y1 - 10) };
            float[] yaws = { 90f, -90f, 90f, -90f };
            for (int i = 0; i < 4; i++)
                House(new RectInt(blocks[i].x, blocks[i].y, 8, 8), new Color(0.72f, 0.65f, 0.55f), st.Roofs[i], st.Houses[i], yaws[i]);

            // Well in the plaza
            if (Art("Buildings/building_well_blue", new Vector3(cx + 0.5f, 0, cy + 0.5f), 2.3f, ArtLibrary.Fit.Width) == null)
                Factory.Prim(PrimitiveType.Cylinder, deco, new Vector3(cx + 0.5f, 0.5f, cy + 0.5f), new Vector3(1.8f, 0.5f, 1.8f), new Color(0.5f, 0.48f, 0.45f));
            grid.SetBlocked(cx, cy, true);

            if (!art) return;
            var posts = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(cx - 5.4f, 0, cy - 5.4f), new Vector3(cx + 6.4f, 0, cy - 5.4f), new Vector3(cx - 5.4f, 0, cy + 6.4f), new Vector3(cx + 6.4f, 0, cy + 6.4f),
                new Vector3(cx - 2.4f, 0, y0 + 3.5f), new Vector3(cx + 3.4f, 0, y1 - 2.5f), new Vector3(x0 + 3.5f, 0, cy + 3.4f), new Vector3(x1 - 2.5f, 0, cy - 2.4f),
            };
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
            Art(st.Banner, new Vector3(cx - 3.4f, 0, y0 + 2.5f), 3f, ArtLibrary.Fit.Height, 0f);
            Art(st.Banner, new Vector3(cx + 4.4f, 0, y0 + 2.5f), 3f, ArtLibrary.Fit.Height, 0f);
            Art(st.Banner, new Vector3(cx - 3.4f, 0, y1 - 1.5f), 3f, ArtLibrary.Fit.Height, 180f);
            Art(st.Banner, new Vector3(cx + 4.4f, 0, y1 - 1.5f), 3f, ArtLibrary.Fit.Height, 180f);
            Art(Pick("Town/stall-red", "Town/stall-green"), new Vector3(cx - 9.5f, 0, cy - 3.6f), 1.8f, ArtLibrary.Fit.Width, 90f);
            Art("Props/barrel_small_stack", new Vector3(cx + 9.5f, 0, cy + 4.0f), 1f);
            Art("Props/crates_stacked", new Vector3(cx - 9.5f, 0, cy + 4.0f), 1.1f, ArtLibrary.Fit.Height, 20f);
            Art("Town/cart", new Vector3(cx + 10.5f, 0, cy - 4.0f), 2.2f, ArtLibrary.Fit.Width, 35f);
        }

        void TownWall(int x, int y, bool alongX, TownStyle st)
        {
            if (!st.StoneWalls) { Palisade(x, y, st.Wood, alongX); return; }
            grid.SetBlocked(x, y, true);
            float shade = LR(0.85f, 1.05f);
            // Graveyard/stone-wall is a 1-unit wall piece running along X.
            if (ArtBox("Graveyard/stone-wall", new Vector3(x + 0.5f, 0, y + 0.5f), new Vector3(1.05f, 2.8f, 0.7f), alongX ? 0f : 90f) != null) return;
            Factory.Prim(PrimitiveType.Cube, deco, new Vector3(x + 0.5f, 1.5f, y + 0.5f), new Vector3(1f, 3f, 1f), new Color(0.33f, 0.31f, 0.32f) * shade);
        }

        /// <summary>Pinecrest: a woodcutters' hamlet on the north road, no wall, a few houses either side and log piles.</summary>
        void Hamlet(Settlement t)
        {
            var r = t.Rect;
            int rx = PinecrestX;
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    surface.Set(x, y, GroundSurface.Dirt, 0.35f);
                    Paint(x, y, Color.Lerp(pixels[Idx(x, y)], dirtC, 0.25f));
                }
            House(new RectInt(rx - 12, r.yMin + 3, 7, 7), new Color(0.7f, 0.62f, 0.5f), new Color(0.3f, 0.45f, 0.25f), "Buildings/building_home_A_green", 90f);
            House(new RectInt(rx - 12, r.yMin + 12, 7, 7), new Color(0.7f, 0.62f, 0.5f), new Color(0.35f, 0.25f, 0.18f), "Buildings/building_home_B_blue", 90f);
            House(new RectInt(rx + 5, r.yMin + 5, 8, 8), new Color(0.72f, 0.65f, 0.55f), new Color(0.55f, 0.2f, 0.15f), "Buildings/building_tavern_blue", -90f);
            // Log piles and stumps by the road
            foreach (var lp in new[] { new Vector2Int(rx + 5, r.yMin + 16), new Vector2Int(rx + 9, r.yMin + 17), new Vector2Int(rx - 4, r.yMin + 20) })
            {
                grid.SetBlocked(lp.x, lp.y, true);
                if (Art("Nature/log_stack", new Vector3(lp.x + 0.5f, 0, lp.y + 0.5f), 1.2f, ArtLibrary.Fit.Height, VR(0, 360)) == null)
                    Factory.Prim(PrimitiveType.Cube, deco, new Vector3(lp.x + 0.5f, 0.4f, lp.y + 0.5f), new Vector3(1.2f, 0.8f, 0.8f), new Color(0.4f, 0.28f, 0.16f));
            }
            var c = new Vector3(rx + 0.5f, 0, r.yMin + 11.5f);
            CraftingStation.Create(SkillType.Cooking, c + new Vector3(-4f, 0, 0f), root);
            Npc.Create("Woodsman Garrick", "Pinecrest Lumber", NpcRole.QuestGiver, c + new Vector3(4f, 0, -2f), new Color(0.35f, 0.45f, 0.25f),
                "Mind the stumps. And the wolves. Mostly the wolves.", npcs, true, null, false);
            Npc.Create("Old Martha", "Food & Drink", NpcRole.Vendor, c + new Vector3(4f, 0, 2f), new Color(0.6f, 0.4f, 0.35f),
                "Stew's hot. Bread's yesterday's. Both are better than what's out there.", npcs, false, true, false).SellsAs(VendorKind.Food);
            if (!art) return;
            foreach (var lp in new[] { c + new Vector3(-2.4f, 0, -6f), c + new Vector3(2.4f, 0, 6f) })
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
        }
    }
}
