using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Our guild (server/guild.js): the roster the server sends, an invitation waiting for an answer, and the guild
    /// window (O). Everything else is chat commands: /guild, /g, /ginvite, /gleave, /gkick, /gpromote, /gdemote,
    /// /gleader, /gmotd.
    /// </summary>
    public static class Guild
    {
        public static readonly Color Color = new Color(0.45f, 0.95f, 0.5f);
        public static NetGuild Current { get; private set; }

        public static string InviteFrom { get; private set; }
        public static string InviteGuild { get; private set; }
        public static float InviteTime { get; private set; }

        public static void Set(NetGuild g)
        {
            bool joined = Current == null && g != null && !string.IsNullOrEmpty(g.name);
            Current = g != null && !string.IsNullOrEmpty(g.name) ? g : null;
            if (joined) Player.I?.Achievements.Max("guild", 1);
        }

        public static void Invited(string from, string guild)
        {
            InviteFrom = from;
            InviteGuild = guild;
            InviteTime = Time.time;
            GameUI.Log(from + " invites you to join " + guild + ".", Color);
        }

        public static void Answer(bool yes)
        {
            if (InviteFrom != null) NetClient.I?.AnswerGuildInvite(yes);
            InviteFrom = null;
        }

        public static void Reset() { Current = null; InviteFrom = null; }

        public static int Online { get { int n = 0; if (Current?.members != null) foreach (var m in Current.members) if (m.on) n++; return n; } }
    }
}
