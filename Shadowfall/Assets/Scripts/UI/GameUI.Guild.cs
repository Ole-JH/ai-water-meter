using UnityEngine;

namespace Shadowfall
{
    /// <summary>The guild window (O): the message of the day and the roster, online first. Managing it is chat commands.</summary>
    public partial class GameUI
    {
        bool showGuild;
        float guildScroll;

        void DrawGuild(Player p)
        {
            var g = Guild.Current;
            var r = new Rect(VW - 470 - 20, 120, 470, Mathf.Min(620, VH - 260));
            if (UISkin.Window(r, g != null ? g.name + "  <" + g.tag + ">" : "Guild")) { showGuild = false; return; }
            Block(r);
            float x = r.x + 26, y = r.y + 58, w = r.width - 52;
            if (g == null)
            {
                GUI.Label(new Rect(x, y, w, 200),
                    "You are not in a guild.\n\nFound one with <b>/guild create Name TAG</b> (1000 gold): a name of 3 to 24 letters and a tag of 2 to 4 letters, " +
                    "shown before your name. Or ask an officer of a guild to <b>/ginvite</b> you.",
                    UISkin.V(UISkin.Rich, wordWrap: true));
                return;
            }
            if (!string.IsNullOrEmpty(g.motd))
            {
                GUI.Label(new Rect(x, y, w, 44), "<i>\"" + g.motd + "\"</i>", UISkin.V(UISkin.Rich, wordWrap: true));
                y += 48;
            }
            UISkin.Shadowed(new Rect(x, y, w, 22), g.members.Length + " members, " + Guild.Online + " online  -  you are " + (g.rank == "leader" ? "the leader" : "a" + (g.rank == "officer" ? "n officer" : " member")),
                UISkin.Small, UISkin.Muted);
            y += 28;

            var list = new System.Collections.Generic.List<NetGuildMember>(g.members);
            list.Sort((a, b) => a.on != b.on ? (a.on ? -1 : 1) : RankOrder(b.rank) != RankOrder(a.rank) ? RankOrder(b.rank) - RankOrder(a.rank) : string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            var view = new Rect(x, y, w, r.yMax - y - 74);
            float rowH = 26;
            var content = new Rect(0, 0, w - 18, list.Count * rowH);
            guildScroll = GUI.BeginScrollView(view, new Vector2(0, guildScroll), content).y;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                var c = m.on ? UISkin.Cream : new Color(0.6f, 0.58f, 0.55f);
                UISkin.Shadowed(new Rect(0, i * rowH, 200, rowH), m.name, UISkin.Label, c);
                UISkin.Shadowed(new Rect(200, i * rowH + 2, 100, rowH), m.rank == "member" ? "" : m.rank, UISkin.Small, m.rank == "leader" ? UISkin.Gold : Guild.Color);
                UISkin.Shadowed(new Rect(300, i * rowH + 2, 110, rowH), m.on ? "level " + m.lvl : "offline", UISkin.Small, c);
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(x, r.yMax - 66, w, 50),
                "<b>/g</b> text  guild chat  -  <b>/ginvite</b>, <b>/gkick</b>, <b>/gpromote</b>, <b>/gdemote</b>, <b>/gleader</b> name  -  <b>/gmotd</b> text  -  <b>/gleave</b>",
                UISkin.V(UISkin.RichSmall, wordWrap: true));
        }

        static int RankOrder(string rank) => rank == "leader" ? 2 : rank == "officer" ? 1 : 0;
    }
}
