using UnityEngine;

namespace Shadowfall
{
    /// <summary>The guild window (O): the message of the day and the roster, online first. Managing it is chat commands.</summary>
    public partial class GameUI
    {
        bool showGuild, guildBoardOpen;
        float guildScroll, boardScroll;
        int[] bannerEdit; // the leader's banner being changed (colour, colour, emblem), null when not editing

        public void OpenGuildBoard() { guildBoardOpen = true; }

        /// <summary>The guild board: every guild, its banner, size and leader.</summary>
        void DrawGuildBoard(Player p)
        {
            if (Factory.FlatDistance(p.transform.position, GuildBoard.Spot) > 8f || Dungeon.Active) { guildBoardOpen = false; return; }
            var r = new Rect(14, 110, 520, Mathf.Min(620, VH - 130));
            if (UISkin.Window(r, "The Guilds of the Realm")) { guildBoardOpen = false; return; }
            Block(r);
            float x = r.x + 26, y = r.y + 58, w = r.width - 52;
            var board = Guild.Board;
            if (board.Length == 0)
            {
                GUI.Label(new Rect(x, y, w, 60), "No guild has been founded yet. Found one with <b>/guild create Name TAG</b>.", UISkin.V(UISkin.Rich, wordWrap: true));
                return;
            }
            const float rowH = 52;
            var view = new Rect(x, y, w, r.yMax - y - 20);
            var content = new Rect(0, 0, w - 18, board.Length * rowH);
            boardScroll = GUI.BeginScrollView(view, new Vector2(0, boardScroll), content).y;
            for (int i = 0; i < board.Length; i++)
            {
                var f = board[i].Split('|');
                if (f.Length < 5) continue;
                float ry = i * rowH;
                GUI.DrawTexture(new Rect(0, ry + 2, 30, 45), GuildHeraldry.Flag(f[3]));
                bool mine = Guild.Current != null && Guild.Current.name == f[0];
                UISkin.Shadowed(new Rect(42, ry + 4, 330, 24), f[0] + "  <" + f[1] + ">", UISkin.Label, mine ? Guild.Color : UISkin.Cream);
                UISkin.Shadowed(new Rect(42, ry + 26, 330, 20), f[2] + (f[2] == "1" ? " member" : " members") + "  -  led by " + f[4], UISkin.Small, UISkin.Muted);
            }
            GUI.EndScrollView();
        }

        /// <summary>The guild's flag, and for the leader buttons to change it (sent as /gbanner).</summary>
        float DrawBannerEditor(NetGuild g, float x, float y, float w)
        {
            string hb = g.hb ?? "";
            if (bannerEdit != null) hb = bannerEdit[0] + "," + bannerEdit[1] + "," + bannerEdit[2];
            GUI.DrawTexture(new Rect(x, y, 40, 60), GuildHeraldry.Flag(hb));
            if (g.rank != "leader") return 66;
            GuildHeraldry.Parse(hb, out int c1, out int c2, out int e);
            var vals = new[] { c1, c2, e };
            string[] labels = { "Field", "Charge", "Emblem" };
            float bx = x + 56;
            for (int i = 0; i < 3; i++)
            {
                int max = i == 2 ? GuildHeraldry.Emblems.Length : GuildHeraldry.Colours.Length;
                string val = i == 2 ? GuildHeraldry.Emblems[vals[i]] : GuildHeraldry.ColourNames[vals[i]];
                UISkin.Shadowed(new Rect(bx, y + 2 + i * 20, 70, 18), labels[i], UISkin.Small, UISkin.Muted);
                if (UISkin.Btn(new Rect(bx + 66, y + i * 20, 22, 18), "<", UISkin.V(UISkin.Button, fontSize: 11))) { vals[i] = (vals[i] + max - 1) % max; bannerEdit = vals; }
                UISkin.Shadowed(new Rect(bx + 92, y + 2 + i * 20, 80, 18), val, UISkin.Small, UISkin.Cream);
                if (UISkin.Btn(new Rect(bx + 174, y + i * 20, 22, 18), ">", UISkin.V(UISkin.Button, fontSize: 11))) { vals[i] = (vals[i] + 1) % max; bannerEdit = vals; }
            }
            if (bannerEdit != null)
            {
                bool same = bannerEdit[0] == bannerEdit[1];
                GUI.enabled = !same;
                if (UISkin.Btn(new Rect(bx + 210, y + 4, 120, 30), same ? "Colours match" : "Raise banner", UISkin.V(UISkin.Button, fontSize: 13)))
                {
                    NetClient.I?.SendChat("/gbanner " + bannerEdit[0] + " " + bannerEdit[1] + " " + bannerEdit[2]);
                    bannerEdit = null;
                }
                GUI.enabled = true;
            }
            return 66;
        }

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
            y += DrawBannerEditor(g, x, y, w);
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
