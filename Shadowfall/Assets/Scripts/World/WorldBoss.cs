using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The client side of world bosses (server/worldboss.js): which one is up and where (sent every few seconds while it
    /// lives), the banner when one rises, and what the slam looks like. The tracker under the minimap and the map
    /// markers read <see cref="Current"/>.
    /// </summary>
    public static class WorldBoss
    {
        public static readonly Color Color = new Color(1f, 0.6f, 0.15f);
        public static readonly string[] Names = { "Old Bramblehide", "Hrimgar the Mountain", "Gorvash the Dune Reaver", "The Pyre Colossus" };
        // Must match server/worldboss.js (SLAM_RADIUS, SLAM_WINDUP)
        public const float SlamRadius = 6f, SlamWindup = 1.5f;

        public static NetWorldBoss Current { get; private set; }
        public static bool Up => Current != null;
        public static Vector3 Position => Current != null ? new Vector3(Current.x, 0f, Current.z) : Vector3.zero;

        public static bool Is(string name) => System.Array.IndexOf(Names, name) >= 0;

        public static void Set(NetWorldBoss wb)
        {
            var was = Current;
            Current = wb == null || wb.phase != "up" ? null : wb;
            if (Current != null && (was == null || was.name != wb.name))
            {
                GameUI.Banner(wb.name + " has risen in " + wb.region + "!", Color);
                Sfx.Play2D("gong", 0.6f, 0.8f);
            }
        }

        public static string Status
        {
            get
            {
                var c = Current;
                if (c == null) return "";
                var p = Player.I;
                string where = c.region;
                if (p != null && !Dungeon.Active)
                {
                    float d = Factory.FlatDistance(p.transform.position, Position);
                    where = d < 40f ? "close by" : Mathf.RoundToInt(d) + " m " + Compass(Position - p.transform.position);
                }
                return "Level " + c.l + "  -  " + c.hp + "% health  -  " + (c.n > 0 ? c.n + (c.n == 1 ? " hero" : " heroes") + " fighting  -  " : "") + where;
            }
        }

        static string Compass(Vector3 d)
        {
            float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            string[] dirs = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            return dirs[Mathf.RoundToInt((a + 360f) % 360f / 45f) % 8];
        }
    }
}
