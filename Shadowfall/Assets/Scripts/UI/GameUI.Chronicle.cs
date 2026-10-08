using UnityEngine;

namespace Shadowfall
{
    /// <summary>The siege record board (MemorialBoard): every walled town's defences, its last defenders and its prosperity.</summary>
    public partial class GameUI
    {
        string chronicleFor;
        Vector2 chronicleScroll;

        public void OpenChronicle(string town) { chronicleFor = town; chronicleScroll = Vector2.zero; }

        void DrawChronicle(Player p)
        {
            var r = new Rect((VW - 560) / 2, 100, 560, 560);
            if (UISkin.Window(r, "Siege Record", true, true)) { chronicleFor = null; return; }
            Block(r);
            float x = r.x + 30, y = r.y + 58, w = r.width - 60;
            GUI.Label(new Rect(x, y, w, 40), "Every walled town keeps the tally of its sieges, and the names of those who stood on its walls.",
                UISkin.V(UISkin.InkRich, wordWrap: true));
            y += 46;
            var view = new Rect(x, y, w, r.yMax - y - 24);
            var list = SiegeChronicle.Records;
            float rowH = 112f;
            var content = new Rect(0, 0, w - 18, Mathf.Max(view.height, list.Length * rowH));
            chronicleScroll = GUI.BeginScrollView(view, chronicleScroll, content);
            float cy = 0f;
            // this town first
            for (int pass = 0; pass < 2; pass++)
                foreach (var c in list)
                {
                    if (c == null || (pass == 0) != (c.k == chronicleFor)) continue;
                    DrawChronicleRow(c, content.width, cy);
                    cy += rowH;
                }
            GUI.EndScrollView();
        }

        void DrawChronicleRow(NetChron c, float w, float y)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.08f);
            GUI.DrawTexture(new Rect(0, y, w, 104f), UISkin.White);
            GUI.color = Color.white;
            UISkin.Shadowed(new Rect(8, y + 4, w - 16, 24), c.k, UISkin.V(UISkin.Heading, fontSize: 17), UISkin.Gold);
            GUI.Label(new Rect(8, y + 30, w - 16, 22),
                "Held <b>" + c.h + "</b>   Fell <b>" + c.f + "</b>   Spared <b>" + c.sp + "</b>   -   last " + SiegeChronicle.LastWord(c) + (string.IsNullOrEmpty(c.g) ? "" : " (" + c.g + " gate)"),
                UISkin.InkRich);
            string prices = SiegeChronicle.PriceWord(c.p);
            GUI.color = SiegeChronicle.ProsperityColor(c.p);
            GUI.Label(new Rect(8, y + 52, w - 16, 22), "<b>" + SiegeChronicle.ProsperityWord(c.p) + "</b>" + (prices.Length > 0 ? "  -  its merchants are " + prices : ""), UISkin.InkRich);
            GUI.color = Color.white;
            string defenders = c.d != null && c.d.Length > 0 ? string.Join(", ", c.d) : "-";
            GUI.Label(new Rect(8, y + 74, w - 16, 34), "<i>Last defenders: " + defenders + "</i>", UISkin.V(UISkin.InkRich, wordWrap: true));
        }

        /// <summary>Under the minimap: the Heroes' Feast while it lasts, and captives waiting to be freed.</summary>
        float DrawAftermathTracker(float y0)
        {
            if (Dungeon.Active) return 0f;
            float x = VW - 330, y = y0;
            if (SiegeAftermath.Feasting)
            {
                int left = Mathf.CeilToInt(SiegeAftermath.FeastUntil - Time.time);
                UISkin.Shadowed(new Rect(x, y, 300, 20), "Heroes' Feast: +" + Mathf.RoundToInt((SiegeAftermath.FeastXp - 1f) * 100f) + "% experience  " + (left / 60) + ":" + (left % 60).ToString("00"), UISkin.Small, UISkin.Gold);
                y += 22;
            }
            var cp = SiegeAftermath.Captives;
            if (cp != null)
            {
                int left = Mathf.Max(0, Mathf.CeilToInt(SiegeAftermath.CaptivesUntil - Time.time));
                UISkin.Shadowed(new Rect(x, y, 300, 24), "Captives of " + cp.k, UISkin.Heading, new Color(1f, 0.75f, 0.4f));
                y += 26;
                UISkin.Shadowed(new Rect(x + 12, y, 300, 20), cp.n + " held at the raiders' camp by " + cp.c + " captors - " + (left / 60) + ":" + (left % 60).ToString("00"), UISkin.Small, UISkin.Cream);
                y += 26;
            }
            return y - y0;
        }
    }
}
