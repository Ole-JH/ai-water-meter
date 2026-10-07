namespace Shadowfall
{
    /// <summary>
    /// Social emotes: /wave, /dance, /sit... Each plays an animation on the hero (the KayKit rig's own clips plus the
    /// ones authored by tools/art/emotes.py), prints a line in chat and is shown to nearby players.
    /// Keep <see cref="All"/> in sync with EMOTES in server/content.js.
    /// </summary>
    public class EmoteDef
    {
        public string Id, Label, Clip, Then, You, Other, Sound;
        public bool Loop;           // keeps going until you move (dance)
        public string[] Aliases;

        public static readonly EmoteDef[] All =
        {
            new EmoteDef { Id = "wave", Label = "Wave", Clip = "Wave", You = "You wave.", Other = "{0} waves.", Aliases = new[] { "hi", "hello", "bye" } },
            new EmoteDef { Id = "dance", Label = "Dance", Clip = "Dance", Loop = true, You = "You burst into dance!", Other = "{0} bursts into dance!" },
            new EmoteDef { Id = "bow", Label = "Bow", Clip = "Bow", You = "You bow gracefully.", Other = "{0} bows gracefully." },
            new EmoteDef { Id = "cheer", Label = "Cheer", Clip = "Cheer", You = "You cheer!", Other = "{0} cheers!", Aliases = new[] { "yay" } },
            new EmoteDef { Id = "clap", Label = "Clap", Clip = "Clap", You = "You clap.", Other = "{0} claps.", Aliases = new[] { "applaud" } },
            new EmoteDef { Id = "point", Label = "Point", Clip = "Point", You = "You point ahead.", Other = "{0} points ahead." },
            new EmoteDef { Id = "flex", Label = "Flex", Clip = "Flex", You = "You flex. Impressive.", Other = "{0} flexes. Impressive.", Aliases = new[] { "strong" } },
            new EmoteDef { Id = "sit", Label = "Sit", Clip = "Sit_Floor_Down", Then = "Sit_Floor_Idle", You = "You sit down.", Other = "{0} sits down." },
            new EmoteDef { Id = "sleep", Label = "Sleep", Clip = "Lie_Down", Then = "Lie_Idle", You = "You lie down for a nap.", Other = "{0} lies down for a nap.", Aliases = new[] { "lie", "rest" } },
            new EmoteDef { Id = "jump", Label = "Jump", Clip = "Jump_Full_Short", You = "You jump for joy.", Other = "{0} jumps for joy." },
            new EmoteDef { Id = "kick", Label = "Kick", Clip = "Unarmed_Melee_Attack_Kick", You = "You kick the dirt.", Other = "{0} kicks the dirt.", Sound = "swing" },
            new EmoteDef { Id = "shadowbox", Label = "Shadowbox", Clip = "Unarmed_Melee_Attack_Punch_A", You = "You throw a few punches at the air.", Other = "{0} throws a few punches at the air.", Sound = "swing", Aliases = new[] { "punch" } },
            new EmoteDef { Id = "guard", Label = "Guard", Clip = "Blocking", Loop = true, You = "You raise your guard.", Other = "{0} raises their guard.", Aliases = new[] { "block" } },
        };

        public static EmoteDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            id = id.ToLowerInvariant();
            foreach (var e in All)
            {
                if (e.Id == id) return e;
                if (e.Aliases != null) foreach (var a in e.Aliases) if (a == id) return e;
            }
            return null;
        }

        /// <summary>"/wave" (or "/e wave") typed in chat → the emote, else null.</summary>
        public static EmoteDef FromChat(string text)
        {
            if (string.IsNullOrEmpty(text) || text[0] != '/') return null;
            var parts = text.Substring(1).Trim().Split(' ');
            if (parts.Length == 0) return null;
            if ((parts[0] == "e" || parts[0] == "emote") && parts.Length > 1) return Get(parts[1]);
            return parts.Length == 1 ? Get(parts[0]) : null;
        }
    }
}
