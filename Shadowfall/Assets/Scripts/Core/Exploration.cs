using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Fog of war for the minimap and world map. The hero reveals the area around them as they walk; what the
    /// overworld looks like is remembered (saved with the character), dungeon levels are forgotten on leaving.
    /// Admins can reveal everything (<see cref="AdminTools.RevealMap"/>).
    /// </summary>
    public static class Exploration
    {
        public const float Radius = 15f;      // tiles fully revealed around the hero
        const float Soft = 4f;                // fading edge beyond that

        static byte[] world, dungeon;
        static int dw, dh;
        static Vector3 lastReveal = new Vector3(-999f, 0f, -999f);
        static bool lastInDungeon;

        /// <summary>Bumped whenever something new is revealed (the maps re-render when it changes).</summary>
        public static int Version { get; private set; }

        static byte[] World
        {
            get
            {
                if (world == null) ResetWorld();
                return world;
            }
        }

        /// <summary>How much of the walkable overworld this hero has explored, in percent.</summary>
        public static int WorldPercent()
        {
            var w = World;
            var grid = WorldGrid.Instance;
            int seen = 0, total = 0;
            for (int y = 0; y < WorldGenerator.H; y++)
                for (int x = 0; x < WorldGenerator.W; x++)
                {
                    if (!grid.IsWalkable(new Vector3(x + 0.5f, 0f, y + 0.5f))) continue;
                    total++;
                    if (w[y * WorldGenerator.W + x] > 128) seen++;
                }
            return total > 0 ? Mathf.FloorToInt(seen * 100f / total) : 0;
        }

        /// <summary>A fresh hero knows only the village.</summary>
        public static void ResetWorld()
        {
            world = new byte[WorldGenerator.W * WorldGenerator.H];
            var t = WorldGenerator.Town;
            for (int y = t.yMin - 2; y < t.yMax + 2; y++)
                for (int x = t.xMin - 2; x < t.xMax + 2; x++)
                    if (x >= 0 && y >= 0 && x < WorldGenerator.W && y < WorldGenerator.H) world[y * WorldGenerator.W + x] = 255;
            Version++;
        }

        /// <summary>A new dungeon level starts completely dark.</summary>
        public static void ResetDungeon(int w, int h)
        {
            dw = w;
            dh = h;
            dungeon = new byte[w * h];
            lastReveal = new Vector3(-999f, 0f, -999f);
            Version++;
        }

        /// <summary>Call often with the hero's position; it only does work after moving a little.</summary>
        public static void Reveal(Vector3 pos)
        {
            bool inDungeon = Dungeon.Active;
            if (inDungeon == lastInDungeon && Factory.FlatDistance(pos, lastReveal) < 1.5f) return;
            lastInDungeon = inDungeon;
            lastReveal = pos;
            byte[] map;
            int w, h;
            float ox, oz;
            if (inDungeon)
            {
                if (dungeon == null || dungeon.Length != Dungeon.Width * Dungeon.Height) ResetDungeon(Dungeon.Width, Dungeon.Height);
                map = dungeon; w = dw; h = dh; ox = Dungeon.Origin.x; oz = Dungeon.Origin.z;
            }
            else { map = World; w = WorldGenerator.W; h = WorldGenerator.H; ox = 0f; oz = 0f; }

            float reach = Radius + Soft;
            int cx = Mathf.FloorToInt(pos.x - ox), cz = Mathf.FloorToInt(pos.z - oz), r = Mathf.CeilToInt(reach);
            bool changed = false;
            for (int y = Mathf.Max(0, cz - r); y <= Mathf.Min(h - 1, cz + r); y++)
                for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(w - 1, cx + r); x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - (pos.x - ox)) * (x + 0.5f - (pos.x - ox)) + (y + 0.5f - (pos.z - oz)) * (y + 0.5f - (pos.z - oz)));
                    if (d > reach) continue;
                    byte v = (byte)(Mathf.Clamp01((reach - d) / Soft) * 255f);
                    int i = y * w + x;
                    if (v > map[i]) { map[i] = v; changed = true; }
                }
            if (changed) Version++;
        }

        /// <summary>How well a world position is known: 0 = never seen, 1 = explored.</summary>
        public static float At(Vector3 p)
        {
            if (AdminTools.RevealMap) return 1f;
            if (Dungeon.Contains(p))
            {
                if (dungeon == null) return 0f;
                int x = Mathf.FloorToInt(p.x - Dungeon.Origin.x), y = Mathf.FloorToInt(p.z - Dungeon.Origin.z);
                return x < 0 || y < 0 || x >= dw || y >= dh ? 0f : dungeon[y * dw + x] / 255f;
            }
            int wx = Mathf.FloorToInt(p.x), wy = Mathf.FloorToInt(p.z);
            if (wx < 0 || wy < 0 || wx >= WorldGenerator.W || wy >= WorldGenerator.H) return 0f;
            return World[wy * WorldGenerator.W + wx] / 255f;
        }

        public static bool Seen(Vector3 p) => At(p) > 0.5f;

        // ---- saving the overworld map with the character (1 bit per tile, base64)

        public static string Save()
        {
            var map = World;
            var bits = new byte[(map.Length + 7) / 8];
            for (int i = 0; i < map.Length; i++) if (map[i] >= 128) bits[i >> 3] |= (byte)(1 << (i & 7));
            return System.Convert.ToBase64String(bits);
        }

        public static void Load(string data)
        {
            ResetWorld();
            if (string.IsNullOrEmpty(data)) return;
            try
            {
                var bits = System.Convert.FromBase64String(data);
                if (bits.Length * 8 < world.Length) return; // saved on a different world size: start over
                for (int i = 0; i < world.Length; i++) if ((bits[i >> 3] >> (i & 7) & 1) == 1) world[i] = 255;
                Version++;
            }
            catch (System.FormatException) { }
        }
    }

    /// <summary>
    /// Client side of the admin module. The server tells the client it is an admin at login; server-side commands
    /// are re-checked by the server, the rest (map overrides, god mode, handing yourself gold) are local helpers.
    /// </summary>
    public static class AdminTools
    {
        public static bool IsAdmin;
        static bool reveal, enemies, dungeons, god, fast;

        public static bool RevealMap { get => IsAdmin && reveal; set => reveal = value; }
        public static bool ShowEnemies { get => IsAdmin && enemies; set => enemies = value; }
        public static bool ShowDungeons { get => IsAdmin && dungeons; set => dungeons = value; }
        public static bool God { get => IsAdmin && god; set => god = value; }
        public static bool Fast { get => IsAdmin && fast; set { fast = value; Player.I?.RecalculateStats(); } }

        public static void Reset()
        {
            IsAdmin = false;
            reveal = enemies = dungeons = god = fast = false;
        }

        public static void Send(AdminCmd cmd)
        {
            if (IsAdmin) NetClient.I?.SendAdmin(cmd);
        }
    }
}
