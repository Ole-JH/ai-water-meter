using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shadowfall
{
    /// <summary>
    /// The client side of the Catacombs. The server generates each dungeon level and sends its layout; this
    /// builds floors, walls, torches, props, portals and chests from it, far away from the overworld
    /// (at <see cref="Origin"/>), and swaps the walkability grid used for pathfinding.
    /// </summary>
    public static class Dungeon
    {
        public static readonly Vector3 Origin = new Vector3(1000f, 0f, 1000f);
        /// <summary>Which dungeon we are in (index into <see cref="DungeonDef.All"/>) and how many levels it has.</summary>
        public static int Index { get; private set; }
        public static DungeonDef Def => DungeonDef.Get(Index);
        public static int Depths { get; private set; } = 3;

        public static bool Active => root != null;
        public static int Depth { get; private set; }
        public static string Name { get; private set; } = "The Catacombs";
        public static string ZoneName => Name + "  -  Depth " + Depth + (Difficulty > 0 ? "  (" + Difficulties.Names[Difficulty] + ")" : "");
        /// <summary>0 Normal, 1 Veteran, 2 Nightmare, 3 Hell (see DIFFICULTIES in server/content.js).</summary>
        public static int Difficulty { get; private set; }
        public static Texture2D MapTexture { get; private set; }
        public static int Width { get; private set; }
        public static int Height { get; private set; }

        static GameObject root;
        static WorldGrid grid, overworld;

        public static bool Contains(Vector3 p) => p.x > 500f;
        public static Vector3 ToWorld(float x, float z) => new Vector3(x + Origin.x, 0f, z + Origin.z);

        // =====================================================================================
        // Enter / leave
        // =====================================================================================

        public static void Enter(NetMsg m)
        {
            Exit();
            Depth = m.l;
            Index = m.d;
            Difficulty = Mathf.Clamp(m.df, 0, Difficulties.Names.Length - 1);
            Depths = m.n > 0 ? m.n : Def.Depths;
            Name = string.IsNullOrEmpty(m.k) ? Def.Name : m.k;
            Width = m.w;
            Height = m.h;

            overworld = WorldGrid.Instance;
            grid = new WorldGrid(m.w, m.h, false) { Origin = new Vector2Int((int)Origin.x, (int)Origin.z) };
            var bits = System.Convert.FromBase64String(m.cells ?? "");
            var blocked = new bool[m.w * m.h];
            for (int i = 0; i < blocked.Length; i++)
            {
                blocked[i] = i >> 3 < bits.Length && (bits[i >> 3] >> (i & 7) & 1) == 1;
                grid.SetBlocked(i % m.w, i / m.w, blocked[i]);
            }
            WorldGrid.Instance = grid;

            root = new GameObject("Dungeon");
            BuildMap(blocked);
            BuildGeometry(blocked);
            var rng = new System.Random(m.seed);
            var rooms = new List<RectInt>();
            if (m.rooms != null)
                for (int i = 0; i + 3 < m.rooms.Length; i += 4) rooms.Add(new RectInt(m.rooms[i], m.rooms[i + 1], m.rooms[i + 2], m.rooms[i + 3]));
            Decorate(blocked, rooms, rng, m);

            if (m.exit != null && m.exit.Length == 2) DungeonPortal.Create(root.transform, ToWorld(m.exit[0], m.exit[1]), false, Depth);
            if (m.stairs != null && m.stairs.Length == 2) DungeonPortal.Create(root.transform, ToWorld(m.stairs[0], m.stairs[1]), true, Depth + 1);
            if (m.chests != null)
                for (int i = 0; i + 1 < m.chests.Length; i += 2) DungeonChest.Create(root.transform, ToWorld(m.chests[i], m.chests[i + 1]), Depth);
        }

        public static void Exit()
        {
            if (root != null) Object.Destroy(root);
            root = null;
            if (overworld != null) WorldGrid.Instance = overworld;
            overworld = null;
            grid = null;
        }

        // =====================================================================================
        // Building
        // =====================================================================================

        static bool Blocked(bool[] b, int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height || b[y * Width + x];

        static void BuildMap(bool[] blocked)
        {
            MapTexture = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "DungeonMap" };
            var px = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    bool wall = blocked[y * Width + x];
                    bool edge = wall && (!Blocked(blocked, x + 1, y) || !Blocked(blocked, x - 1, y) || !Blocked(blocked, x, y + 1) || !Blocked(blocked, x, y - 1));
                    px[y * Width + x] = !wall ? new Color32(92, 84, 72, 255) : edge ? new Color32(40, 34, 30, 255) : new Color32(8, 7, 7, 255);
                }
            MapTexture.SetPixels32(px);
            MapTexture.Apply();
        }

        static void BuildGeometry(bool[] blocked)
        {
            const float wallH = 2.4f, tex = 3f;
            var fv = new List<Vector3>(); var fuv = new List<Vector2>(); var ft = new List<int>();
            var wv = new List<Vector3>(); var wuv = new List<Vector2>(); var wn = new List<Vector3>(); var sides = new List<int>(); var tops = new List<int>();

            // Wall sides are listed outside-in, so their triangles are flipped to face the floor.
            void Quad(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, List<Vector3> n = null, Vector3 normal = default, bool flip = false)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
                if (n != null) { n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal); }
                if (flip) { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
                else { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            }

            float ox = Origin.x, oz = Origin.z;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    float x0 = x + ox, z0 = y + oz, x1 = x0 + 1f, z1 = z0 + 1f;
                    if (!blocked[y * Width + x])
                    {
                        Quad(fv, fuv, ft, new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0),
                            new Vector2(x0 / tex, z0 / tex), new Vector2(x0 / tex, z1 / tex), new Vector2(x1 / tex, z1 / tex), new Vector2(x1 / tex, z0 / tex));
                        continue;
                    }
                    bool nearFloor = false;
                    // A side face toward every neighbouring floor cell
                    if (!Blocked(blocked, x, y - 1)) { nearFloor = true; Quad(wv, wuv, sides, new Vector3(x1, 0, z0), new Vector3(x1, wallH, z0), new Vector3(x0, wallH, z0), new Vector3(x0, 0, z0), new Vector2(x1 / tex, 0), new Vector2(x1 / tex, wallH / tex), new Vector2(x0 / tex, wallH / tex), new Vector2(x0 / tex, 0), wn, Vector3.back, true); }
                    if (!Blocked(blocked, x, y + 1)) { nearFloor = true; Quad(wv, wuv, sides, new Vector3(x0, 0, z1), new Vector3(x0, wallH, z1), new Vector3(x1, wallH, z1), new Vector3(x1, 0, z1), new Vector2(x0 / tex, 0), new Vector2(x0 / tex, wallH / tex), new Vector2(x1 / tex, wallH / tex), new Vector2(x1 / tex, 0), wn, Vector3.forward, true); }
                    if (!Blocked(blocked, x - 1, y)) { nearFloor = true; Quad(wv, wuv, sides, new Vector3(x0, 0, z0), new Vector3(x0, wallH, z0), new Vector3(x0, wallH, z1), new Vector3(x0, 0, z1), new Vector2(z0 / tex, 0), new Vector2(z0 / tex, wallH / tex), new Vector2(z1 / tex, wallH / tex), new Vector2(z1 / tex, 0), wn, Vector3.left, true); }
                    if (!Blocked(blocked, x + 1, y)) { nearFloor = true; Quad(wv, wuv, sides, new Vector3(x1, 0, z1), new Vector3(x1, wallH, z1), new Vector3(x1, wallH, z0), new Vector3(x1, 0, z0), new Vector2(z1 / tex, 0), new Vector2(z1 / tex, wallH / tex), new Vector2(z0 / tex, wallH / tex), new Vector2(z0 / tex, 0), wn, Vector3.right, true); }
                    bool cornerNear = !Blocked(blocked, x + 1, y + 1) || !Blocked(blocked, x - 1, y - 1) || !Blocked(blocked, x + 1, y - 1) || !Blocked(blocked, x - 1, y + 1);
                    if (nearFloor || cornerNear)
                        Quad(wv, wuv, tops, new Vector3(x0, wallH, z0), new Vector3(x0, wallH, z1), new Vector3(x1, wallH, z1), new Vector3(x1, wallH, z0),
                            Vector2.zero, Vector2.up, Vector2.one, Vector2.right, wn, Vector3.up);
                }

            var def = Def;
            var floorTex = Resources.Load<Texture2D>(def.FloorTex);
            var wallTex = Resources.Load<Texture2D>(def.WallTex);
            var floorMat = Mat.New(def.FloorTint);
            if (floorTex != null) floorMat.mainTexture = floorTex;
            var wallMat = Mat.New(def.WallTint);
            if (wallTex != null) wallMat.mainTexture = wallTex;
            var topMat = Mat.New(new Color(0.05f, 0.045f, 0.04f));

            var floor = new Mesh { name = "DungeonFloor", indexFormat = IndexFormat.UInt32 };
            floor.SetVertices(fv); floor.SetUVs(0, fuv); floor.SetTriangles(ft, 0);
            floor.RecalculateNormals(); floor.RecalculateBounds();
            MeshObject("Floor", floor, new[] { floorMat }, false);

            var walls = new Mesh { name = "DungeonWalls", indexFormat = IndexFormat.UInt32, subMeshCount = 2 };
            walls.SetVertices(wv); walls.SetUVs(0, wuv); walls.SetNormals(wn);
            walls.SetTriangles(sides, 0); walls.SetTriangles(tops, 1);
            walls.RecalculateBounds();
            MeshObject("Walls", walls, new[] { wallMat, topMat }, true);
        }

        static void MeshObject(string name, Mesh mesh, Material[] mats, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        static void Decorate(bool[] blocked, List<RectInt> rooms, System.Random rng, NetMsg m)
        {
            var def = Def;
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            GameObject Art(string path, Vector3 pos, float size, float yaw = 0f, ArtLibrary.Fit fit = ArtLibrary.Fit.Height, bool shadows = true) =>
                ArtLibrary.Spawn(path, root.transform, pos, size, fit, yaw, shadows, true, true);

            // Wall torches: on walls next to the floor, spaced out.
            int lights = 0;
            var used = new HashSet<Vector2Int>();
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            for (int y = 1; y < Height - 1 && lights < 30; y++)
                for (int x = 1; x < Width - 1 && lights < 30; x++)
                {
                    if (blocked[y * Width + x] || (x * 7 + y * 13) % 11 != 0) continue;
                    foreach (var d in dirs)
                    {
                        if (!Blocked(blocked, x + d.x, y + d.y)) continue;
                        var cell = new Vector2Int(x / 6, y / 6);
                        if (!used.Add(cell)) break;
                        var c = ToWorld(x + 0.5f, y + 0.5f);
                        var face = new Vector3(d.x, 0f, d.y);
                        var torchPos = c + face * 0.42f;
                        if (def.Id == "mine")
                            Factory.Prim(PrimitiveType.Sphere, root.transform, torchPos + Vector3.up * 1.7f, Vector3.one * 0.22f, def.TorchColor, false, Mat.Glow(def.TorchColor));
                        else Art("Props/torch_mounted", torchPos + Vector3.up * 1.2f, 0.9f, Quaternion.LookRotation(-face).eulerAngles.y, ArtLibrary.Fit.Height, false);
                        var l = new GameObject("Torch").AddComponent<Light>();
                        l.transform.SetParent(root.transform, false);
                        l.transform.position = c + face * 0.1f + Vector3.up * 1.9f;
                        l.type = LightType.Point;
                        l.color = def.TorchColor;
                        l.range = 7.5f;
                        l.intensity = 1.6f;
                        l.gameObject.AddComponent<Flicker>();
                        if (lights % 4 == 0)
                        {
                            var crackle = Sfx.LoopAt("fire_loop", l.transform.position, 0.35f, 8f);
                            if (crackle != null) crackle.transform.SetParent(root.transform, true);
                        }
                        lights++;
                        break;
                    }
                }

            // Room furnishings (crypts get pillars, coffins and candles; the others their own props)
            foreach (var r in rooms)
            {
                if (def.Id != "catacombs")
                {
                    int n = rng.Next(2, 5);
                    for (int i = 0; i < n; i++)
                    {
                        var at = ToWorld(r.x + R(1.2f, r.width - 1.2f), r.y + R(1.2f, r.height - 1.2f));
                        if (!WorldGrid.Instance.IsWalkable(at)) continue;
                        string prop = def.RoomProps[rng.Next(def.RoomProps.Length)];
                        Art(prop, at, prop.Contains("rock_large") || prop.Contains("tent") ? 1.8f : prop.Contains("rock") ? 0.8f : 1.1f, R(0, 360));
                    }
                    if (def.Id == "mine" && rng.NextDouble() < 0.6)
                    {
                        // glowing ore crystals
                        var at = ToWorld(r.x + R(1f, r.width - 1f), r.y + R(1f, r.height - 1f));
                        var crystal = new Color(0.4f, 0.75f, 1f);
                        Factory.Prim(PrimitiveType.Cube, root.transform, at + Vector3.up * 0.4f, new Vector3(0.25f, 0.8f, 0.25f), crystal, false, Mat.Glow(crystal))
                            .transform.rotation = Quaternion.Euler(R(-20, 20), R(0, 360), R(-20, 20));
                    }
                    continue;
                }
                var center = ToWorld(r.x + r.width / 2f, r.y + r.height / 2f);
                if (r.width >= 8 && r.height >= 8)
                    foreach (var corner in new[] { new Vector2(1.6f, 1.6f), new Vector2(r.width - 1.6f, 1.6f), new Vector2(1.6f, r.height - 1.6f), new Vector2(r.width - 1.6f, r.height - 1.6f) })
                        Art("Graveyard/pillar-large", ToWorld(r.x + corner.x, r.y + corner.y), 2.4f);
                if (rng.NextDouble() < 0.45)
                    Art("Graveyard/coffin", ToWorld(r.x + R(1.5f, r.width - 1.5f), r.y + R(1.5f, r.height - 1.5f)), 2f, R(0, 360), ArtLibrary.Fit.Width);
                int candles = rng.Next(1, 4);
                for (int i = 0; i < candles; i++)
                    Art("Graveyard/candle-multiple", ToWorld(r.x + R(0.8f, r.width - 0.8f), r.y + (rng.NextDouble() < 0.5 ? 0.7f : r.height - 0.7f)), 0.55f, R(0, 360), ArtLibrary.Fit.Height, false);
                if (rng.NextDouble() < 0.4)
                    Art(rng.NextDouble() < 0.5 ? "Props/barrel_small_stack" : "Props/crates_stacked", ToWorld(r.x + 0.9f, r.y + r.height - 0.9f), 1f, R(0, 360));
                if (rng.NextDouble() < 0.35)
                    Art("Graveyard/gravestone-broken", ToWorld(r.x + R(1.5f, r.width - 1.5f), r.y + R(1.5f, r.height - 1.5f)), 1.1f, R(0, 360));
            }

            // The boss room burns red.
            if (m.boss != null && m.boss.Length == 2)
            {
                var b = ToWorld(m.boss[0], m.boss[1]);
                foreach (var off in new[] { new Vector3(-3f, 0, -3f), new Vector3(3f, 0, -3f), new Vector3(-3f, 0, 3f), new Vector3(3f, 0, 3f) })
                {
                    var p = b + off;
                    if (!WorldGrid.Instance.IsWalkable(p)) continue;
                    Art(def.Id == "mine" ? "Nature/rock_tallC" : "Graveyard/fire-basket", p, 1.2f);
                    var l = new GameObject("Brazier").AddComponent<Light>();
                    l.transform.SetParent(root.transform, false);
                    l.transform.position = p + Vector3.up * 1.6f;
                    l.type = LightType.Point;
                    l.color = def.BossFire;
                    l.range = 8f;
                    l.intensity = 2f;
                    l.gameObject.AddComponent<Flicker>();
                }
            }
        }
    }

    /// <summary>Flickering torch light.</summary>
    public class Flicker : MonoBehaviour
    {
        Light l;
        float baseIntensity, seed;

        void Start()
        {
            l = GetComponent<Light>();
            baseIntensity = l.intensity;
            seed = Random.value * 100f;
        }

        void Update()
        {
            if (l != null) l.intensity = baseIntensity * (0.8f + Mathf.PerlinNoise(Time.time * 5f, seed) * 0.4f);
        }
    }

    /// <summary>The way out (back to Hollowmere) or the stairs down to the next depth.</summary>
    public class DungeonPortal : Interactable
    {
        bool down;
        int targetDepth;

        public override Color LabelColor => down ? new Color(1f, 0.55f, 0.3f) : new Color(0.55f, 0.8f, 1f);
        public override float LabelHeight => 3.2f;
        public override string HoverText => down ? "Descend to Depth " + targetDepth : "Return to Hollowmere";

        public static DungeonPortal Create(Transform parent, Vector3 pos, bool down, int targetDepth)
        {
            var go = new GameObject(down ? "Stairs" : "Exit");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var p = go.AddComponent<DungeonPortal>();
            p.down = down;
            p.targetDepth = targetDepth;
            p.DisplayName = p.HoverText;
            p.InteractRange = 2.2f;
            p.AddClickCollider(1f, 2.5f);
            var c = p.LabelColor;
            SpellFx.Emit(new SpellFx.P
            {
                Rate = 40, Duration = 100000f, Life = new Vector2(1f, 1.6f), Speed = new Vector2(0.05f, 0.2f),
                Size = new Vector2(0.08f, 0.2f), Start = Color.Lerp(c, Color.white, 0.4f), End = new Color(c.r, c.g, c.b, 0f),
                Shape = ParticleSystemShapeType.Circle, Radius = 0.9f, Velocity = new Vector3(0f, down ? -0.2f : 1.4f, 0f), Max = 300,
            }, pos + Vector3.up * (down ? 1.4f : 0.1f), go.transform);
            var l = new GameObject("PortalLight").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = Vector3.up * 1.5f;
            l.type = LightType.Point;
            l.color = c;
            l.range = 6f;
            l.intensity = 2.2f;
            if (down) ArtLibrary.Spawn("Graveyard/stone-wall-column", go.transform, new Vector3(-1.1f, 0f, 0f), 2.6f);
            if (down) ArtLibrary.Spawn("Graveyard/stone-wall-column", go.transform, new Vector3(1.1f, 0f, 0f), 2.6f);
            return p;
        }

        float nextRing;

        void Update()
        {
            if (Time.time < nextRing) return;
            nextRing = Time.time + 1.2f;
            SpellFx.Ring(transform.position, LabelColor, 1.3f, 1.1f);
        }

        public override void Interact(Player p)
        {
            if (down) NetClient.I.DescendDungeon();
            else NetClient.I.LeaveDungeon(false);
        }
    }

    /// <summary>A treasure chest: gold and an item or two, once per hero.</summary>
    public class DungeonChest : Interactable
    {
        bool opened;
        int depth;

        public override bool CanInteract => !opened;
        public override Color LabelColor => opened ? Color.gray : new Color(1f, 0.85f, 0.3f);
        public override string HoverText => opened ? "Empty Chest" : "Treasure Chest";
        public override float LabelHeight => 1.4f;

        public static DungeonChest Create(Transform parent, Vector3 pos, int depth)
        {
            var go = new GameObject("Chest");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var c = go.AddComponent<DungeonChest>();
            c.depth = depth;
            c.DisplayName = "Treasure Chest";
            c.InteractRange = 1.8f;
            c.AddClickCollider(0.6f, 1f);
            if (ArtLibrary.Spawn("Props/chest", go.transform, Vector3.zero, 0.9f, ArtLibrary.Fit.Width, Random.Range(0f, 360f)) == null)
                Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0.35f, 0), new Vector3(0.9f, 0.7f, 0.6f), new Color(0.45f, 0.3f, 0.15f));
            return c;
        }

        public override void Interact(Player p)
        {
            if (opened) return;
            opened = true;
            Sfx.Play("loot", transform.position, 0.8f);
            Sfx.Play2D("coins", 0.6f);
            SpellFx.Hit(transform.position + Vector3.up * 0.8f, new Color(1f, 0.85f, 0.3f), false, 16);
            int level = p.Level + depth;
            LootDrop.Spawn(transform.position + Vector3.forward * 0.8f, null, Mathf.Max(10, Mathf.RoundToInt(level * Random.Range(8f, 16f))));
            LootDrop.Spawn(transform.position + Vector3.right * 0.8f, ItemDatabase.RandomEquipment(level, 0.4f, Random.value < 0.25f ? Rarity.Rare : Rarity.Magic), 0);
            if (Random.value < 0.4f) LootDrop.Spawn(transform.position + Vector3.left * 0.8f, ItemDatabase.RandomEquipment(level, 0.4f), 0);
        }
    }

    /// <summary>Dungeon difficulty tiers (must match DIFFICULTIES in server/content.js).</summary>
    public static class Difficulties
    {
        public static readonly string[] Names = { "Normal", "Veteran", "Nightmare", "Hell" };
        public static readonly string[] Blurbs =
        {
            "The dungeon as intended.",
            "Monsters have 70% more life and hit 40% harder; more elites. +50% XP, better loot.",
            "Monsters have almost 3x life and hit almost twice as hard; many elites. +120% XP, much better loot.",
            "4.5x life, 2.6x damage, elites everywhere. For full parties in good gear. +220% XP, the best loot.",
        };
        public static readonly Color[] Colors =
        {
            new Color(0.85f, 0.85f, 0.8f), new Color(0.55f, 0.75f, 1f), new Color(0.85f, 0.45f, 1f), new Color(1f, 0.35f, 0.25f),
        };
    }
}
