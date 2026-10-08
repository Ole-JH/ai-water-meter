using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Today's three bounties (server/bounty.js): the list the server keeps up to date, shown with the quest tracker,
    /// and the reward when one is finished.
    /// </summary>
    public static class Bounties
    {
        public static readonly Color Color = new Color(1f, 0.7f, 0.35f);

        public struct Line { public string Text; public int Have, Need; public bool Done; }
        public static Line[] List { get; private set; } = new Line[0];

        public static void Set(string[] items)
        {
            var list = new System.Collections.Generic.List<Line>();
            foreach (var it in items ?? new string[0])
            {
                var p = it.Split('|');
                if (p.Length < 4) continue;
                int.TryParse(p[1], out var have);
                int.TryParse(p[2], out var need);
                list.Add(new Line { Text = p[0], Have = have, Need = need, Done = p[3] == "1" });
            }
            List = list.ToArray();
        }

        public static void Finished(NetMsg m)
        {
            var p = Player.I;
            bool all = m.n == 1;
            GameUI.Banner(all ? "All bounties done!" : "Bounty done: " + m.k, Color);
            Sfx.Play2D("quest_done", 0.7f);
            GameUI.Log("Bounty done: " + m.k + "  -  " + m.gold + " gold, " + m.xp + " experience" + (all ? ", and the Bounty Cache" : "") + ".", Color);
            if (p == null) return;
            p.AddXp(m.xp);
            p.Achievements.Add("bounties");
            if (all) p.Achievements.Add("bounty_days");
        }
    }
}
