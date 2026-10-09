using UnityEngine;

namespace Shadowfall
{
    /// <summary>The reeve of a burned town (Sack): give timber, stone or coin to have the fires out sooner.</summary>
    public partial class GameUI
    {
        RebuildReeve rebuildAt;

        public void OpenRebuild(RebuildReeve reeve) => rebuildAt = reeve;

        static readonly string[] Logs = { "Oak Logs", "Willow Logs", "Yew Logs" }, Ores = { "Copper Ore", "Iron Ore", "Mithril Ore" };

        void DrawRebuild(Player p)
        {
            if (rebuildAt == null || p.IsDead || Factory.FlatDistance(p.transform.position, rebuildAt.transform.position) > 7f || Sack.SecondsLeft(rebuildAt.Town) <= 0)
            {
                rebuildAt = null;
                return;
            }
            var r = new Rect((VW - 480) / 2, 120, 480, 400);
            if (UISkin.Window(r, "Rebuild " + rebuildAt.Town.Split(' ')[0], true, true)) { rebuildAt = null; return; }
            Block(r);
            float x = r.x + 30, y = r.y + 58, w = r.width - 60;
            int left = Sack.SecondsLeft(rebuildAt.Town);
            GUI.Label(new Rect(x, y, w, 70),
                "\"The " + Sack.GateOf(rebuildAt.Town) + " quarter burns, and its merchants have fled. Bring timber and stone, or coin for the masons, " +
                "and we'll have the fires out sooner.\"", UISkin.V(UISkin.InkRich, wordWrap: true));
            y += 76;
            GUI.Label(new Rect(x, y, w, 26), "The fires burn for another <b>" + (left / 60) + ":" + (left % 60).ToString("00") + "</b>", UISkin.InkRich);
            y += 38;
            // as the server pays them (server.js, itemOps.rebuild)
            int xp = Mathf.RoundToInt(10 * Mathf.Pow(p.Level, 1.4f)), coinXp = Mathf.RoundToInt(6 * Mathf.Pow(p.Level, 1.4f));
            int gold = Mathf.Max(50, 25 * p.Level);
            y = RebuildRow(p, x, y, w, "Timber", Have(p, Logs), "5 logs of one kind (woodcutting)", "wood", "-1:30", xp);
            y = RebuildRow(p, x, y, w, "Stone", Have(p, Ores), "5 ore of one kind (mining)", "stone", "-1:30", xp);
            GUI.enabled = p.Gold >= gold;
            UISkin.Shadowed(new Rect(x, y + 4, 200, 22), "Coin", UISkin.Label, UISkin.Cream);
            GUI.Label(new Rect(x, y + 26, 260, 20), gold + " gold (you have " + p.Gold + "), +" + coinXp + " xp", UISkin.Ink14);
            if (UISkin.Btn(new Rect(x + w - 190, y + 6, 190, 38), "Give " + gold + " gold  -1:00", UISkin.Button)) NetClient.I?.Op("rebuild", k: "gold");
            GUI.enabled = true;
            y += 58;
            GUI.Label(new Rect(x, y, w, 40), "Timber and stone: <b>+" + xp + " experience</b> each. When the fires are out, everyone hears who helped.", UISkin.V(UISkin.InkRich, wordWrap: true));
        }

        float RebuildRow(Player p, float x, float y, float w, string what, string have, string needs, string kind, string shorter, int xp)
        {
            bool can = !string.IsNullOrEmpty(have);
            UISkin.Shadowed(new Rect(x, y + 4, 200, 22), what, UISkin.Label, UISkin.Cream);
            GUI.Label(new Rect(x, y + 26, 260, 20), can ? "You have " + have : needs, UISkin.Ink14);
            GUI.enabled = can;
            if (UISkin.Btn(new Rect(x + w - 190, y + 6, 190, 38), "Give 5  " + shorter, UISkin.Button)) NetClient.I?.Op("rebuild", k: kind);
            GUI.enabled = true;
            return y + 58;
        }

        /// <summary>The first kind there are five of ("7 Oak Logs"), or null.</summary>
        static string Have(Player p, string[] names)
        {
            foreach (var n in names)
            {
                int c = p.Inventory.CountOf(n);
                if (c >= 5) return c + " " + n;
            }
            return null;
        }
    }
}
