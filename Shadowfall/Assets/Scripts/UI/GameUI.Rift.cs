using UnityEngine;

namespace Shadowfall
{
    /// <summary>The Rift Stone's window (pick a tier, see the leaderboard) and the tracker while inside a rift.</summary>
    public partial class GameUI
    {
        bool riftOpen;
        int riftTier = 1;

        public void OpenRift() { riftOpen = true; riftTier = Mathf.Max(1, Rift.Best + 1); }

        void DrawRiftWindow(Player p)
        {
            if (Dungeon.Active || p.IsDead || Factory.FlatDistance(p.transform.position, Rift.StonePos) > 6f) { riftOpen = false; return; }
            var r = new Rect((VW - 500) / 2, 110, 500, Mathf.Min(620, VH - 140));
            if (UISkin.Window(r, "Greater Rifts", true, true)) { riftOpen = false; return; }
            Block(r);
            float x = r.x + 30, y = r.y + 58, w = r.width - 60;
            GUI.Label(new Rect(x, y, w, 64),
                "Kill the rift's monsters to fill the bar and bring out the <b>Rift Guardian</b>. Beat it within <b>10 minutes</b> to open the next tier. " +
                "Your party at the stone comes along.", UISkin.V(UISkin.InkRich, wordWrap: true));
            y += 70;
            int max = Rift.Best + 1;
            riftTier = Mathf.Clamp(riftTier, 1, max);
            GUI.Label(new Rect(x, y + 8, 200, 26), "<b>Tier " + riftTier + "</b>   (best: " + (Rift.Best > 0 ? Rift.Best.ToString() : "none") + ")", UISkin.InkRich);
            if (UISkin.Btn(new Rect(x + 230, y, 46, 40), "-", UISkin.SquareButton)) riftTier = Mathf.Max(1, riftTier - 1);
            if (UISkin.Btn(new Rect(x + 282, y, 46, 40), "+", UISkin.SquareButton)) riftTier = Mathf.Min(max, riftTier + 1);
            if (UISkin.Btn(new Rect(x + w - 120, y, 120, 40), "Open", UISkin.Button)) { NetClient.I?.SendRift("ropen", riftTier); riftOpen = false; }
            y += 48;
            GUI.Label(new Rect(x, y, w, 22), "Monsters: +" + Mathf.RoundToInt((Mathf.Pow(1.17f, riftTier - 1) - 1) * 100) + "% health, +" +
                Mathf.RoundToInt((Mathf.Pow(1.1f, riftTier - 1) - 1) * 100) + "% damage, +" + Mathf.RoundToInt(15 * riftTier) + "% experience", UISkin.Ink14);
            y += 34;
            UISkin.Shadowed(new Rect(x, y, w, 26), "Leaderboard", UISkin.Heading, UISkin.Gold);
            y += 30;
            if (Rift.Board.Length == 0) GUI.Label(new Rect(x, y, w, 24), "<i>Nobody has cleared a rift yet.</i>", UISkin.InkRich);
            for (int i = 0; i < Rift.Board.Length && y < r.yMax - 30; i++)
            {
                var parts = Rift.Board[i].Split('|');
                if (parts.Length < 3 || !int.TryParse(parts[1], out var secs)) continue;
                GUI.Label(new Rect(x, y, 40, 22), (i + 1) + ".", UISkin.Ink14);
                GUI.Label(new Rect(x + 34, y, 80, 22), "<b>Tier " + parts[0] + "</b>", UISkin.InkRich);
                GUI.Label(new Rect(x + 118, y, 60, 22), (secs / 60) + ":" + (secs % 60).ToString("00"), UISkin.Ink14);
                GUI.Label(new Rect(x + 180, y, w - 180, 22), parts[2], UISkin.Ink14);
                y += 24;
            }
        }

        /// <summary>Inside a rift: the tier, the progress bar and the time left. Returns the height it took.</summary>
        float DrawRiftTracker(float y0)
        {
            if (!Rift.Inside) return 0f;
            float x = VW - 330, y = y0;
            UISkin.Shadowed(new Rect(x, y, 300, 26), "Greater Rift  -  Tier " + Rift.Tier, UISkin.Heading, Rift.Color);
            y += 28;
            string status = Rift.Phase == "guardian" ? "Slay the Rift Guardian!" : Rift.Phase == "won" ? "Cleared! Leave by the exit." : Rift.Progress + "%";
            UISkin.Bar(new Rect(x + 12, y + 2, 230, 13), Rift.Progress / 100f, "Purple", status, Rift.Color);
            y += 20;
            int t = Rift.SecondsLeft;
            UISkin.Shadowed(new Rect(x + 12, y, 300, 20), Rift.Phase == "late" ? "Out of time: loot only" : Rift.Phase == "won" ? "" : (t / 60) + ":" + (t % 60).ToString("00") + " left",
                UISkin.Small, t < 60 && Rift.Phase != "won" ? new Color(1f, 0.5f, 0.4f) : UISkin.Cream);
            y += 30;
            return y - y0;
        }
    }
}
