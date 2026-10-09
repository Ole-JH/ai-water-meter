using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The client side of town invasions (server/invasion.js): what the server says about the current one, the
    /// banners when it starts, a wave comes, or it ends, the reward, and the achievements. The tracker under the
    /// minimap and the map markers read <see cref="Current"/>.
    /// </summary>
    public static class Invasion
    {
        public static readonly Color Color = new Color(1f, 0.38f, 0.22f);

        /// <summary>The invasion going on (or just ended), or null.</summary>
        public static NetInvasion Current { get; private set; }
        public static bool Active => Current != null && (Current.phase == "gather" || Current.phase == "wave");
        /// <summary>Scouts have sighted raiders: the town and gate are known, the first wave falls in Countdown seconds.</summary>
        public static bool Warned => Current != null && Current.phase == "warn";
        /// <summary>Worth a marker on the maps: sighted, or attacking.</summary>
        public static bool Marked => Active || Warned;
        public static Vector3 Gate => Current != null ? new Vector3(Current.gx, 0f, Current.gz) : Vector3.zero;
        static float countdownFrom, countdownAt;

        public static void Set(NetInvasion iv)
        {
            var was = Current;
            Current = iv == null || iv.phase == "none" ? null : iv;
            Rampart.Sync(); // the gate, the ladders and the helpers
            WarCamp.Sync(); // the raiders' camp outside the gate
            SiegePrep.Ensure(); // the town getting ready in the warning
            SiegeLife.Sync(); // refugees, the scout, the wounded
            SiegeWorks.Sync(); // the beacons and burning roofs
            TownGuards.Sync(Current != null ? Current.gd : null, Current == null || Current.phase == "won" || Current.phase == "lost" || Dungeon.Active); // (in a dungeon they just go: nobody died)
            if (Current == null) return;
            if (Current.phase == "gather" || Current.phase == "warn") { countdownFrom = Current.left; countdownAt = Time.time; }
            if (iv.phase == "warn" && (was == null || was.phase != "warn" || was.town != iv.town))
            {
                // Scouts' warning: time to come and defend
                GameUI.Banner("Raiders sighted near " + iv.town + "!", Color);
                Sfx.Play2D("bell", 0.7f);
                Sfx.Play2D("war_horn", 0.45f, 0.9f); // far off
                TownCrier.Announce("Hear ye! Scouts have sighted raiders making camp outside the " + iv.gate + " gate! Defenders, to the walls, or strike their camp before they're ready!");
                return;
            }

            bool started = was == null || was.town != iv.town || (was.phase != "gather" && iv.phase == "gather");
            bool fellFromWarning = was != null && was.phase == "warn" && iv.phase == "wave"; // the scouts' warning ran out: no gathering
            if (fellFromWarning)
            {
                GameUI.Banner("The raiders charge " + iv.town + "!", Color);
                Sfx.Play2D("gong", 0.7f);
                Sfx.Play2D("roar", 0.55f);
                WarCamp.Charge();
                TownCrier.Announce("Hear ye! The raiders are at the " + iv.gate + " gate of " + iv.town + "! To arms!");
            }
            else if (started && iv.phase == "gather")
            {
                GameUI.Banner(iv.town + " is under attack!", Color);
                Sfx.Play2D("gong", 0.7f);
                TownCrier.Announce("Hear ye! " + iv.town + " is under attack at its " + iv.gate + " gate! To arms!");
            }
            else if (iv.phase == "wave" && (was == null || was.wave != iv.wave))
            {
                GameUI.Banner(iv.wave == iv.waves ? "The warlord's wave!" : "Wave " + iv.wave + " of " + iv.waves, Color);
                Sfx.Play2D("roar", 0.55f);
            }
            else if (iv.phase == "lost" && was != null && was.phase != "lost")
            {
                GameUI.Banner(iv.town + " has been sacked", new Color(0.75f, 0.7f, 0.65f));
                TownCrier.Announce("Hear ye! " + iv.town + " has fallen! The raiders broke the " + iv.gate + " gate and the town burns!");
            }
            else if (iv.phase == "won" && was != null && was.phase != "won" && !rewarded)
                GameUI.Banner(iv.town + " holds!", new Color(0.55f, 1f, 0.55f));
            if (iv.phase != "won") rewarded = false;
        }

        static bool rewarded;

        /// <summary>We helped beat off an invasion of <paramref name="town"/>: experience (the loot comes as drops).</summary>
        public static void Won(string town, int xp)
        {
            rewarded = true;
            var p = Player.I;
            GameUI.Banner(town + " holds! Thank you, defender.", new Color(0.55f, 1f, 0.55f));
            Sfx.Play2D("quest_done", 0.8f);
            if (p == null) return;
            p.AddXp(xp);
            GameUI.Log("You helped defend " + town + ": " + xp + " experience, and the town's thanks lie at your feet.", new Color(0.55f, 1f, 0.55f));
            p.Achievements.Add("defended");
            p.Achievements.Once("defended_town", town);
        }

        /// <summary>Seconds until the first wave (while they gather).</summary>
        public static int Countdown => Current == null ? 0 : Current.paused ? Mathf.CeilToInt(countdownFrom) : Mathf.Max(0, Mathf.CeilToInt(countdownFrom - (Time.time - countdownAt)));

        /// <summary>The tracker's text, e.g. "Wave 2/3 - 9 invaders - gate 74%".</summary>
        public static string Status
        {
            get
            {
                var c = Current;
                if (c == null) return "";
                // nobody near the town: the raiders wait (and give up after a few minutes of it)
                if (c.paused && c.phase == "warn") return "Raiders sighted near the " + c.gate + " gate - they wait until defenders come";
                if (c.paused && (c.phase == "wave" || c.phase == "gather")) return "The raiders wait at the " + c.gate + " gate: nobody is defending";
                switch (c.phase)
                {
                    case "warn": return c.n + (c.n == 1 ? " raider" : " raiders") + " massing outside the " + c.gate + " gate - they charge in " + Countdown + " s";
                    case "gather": return "Monsters gather at the " + c.gate + " gate - first wave in " + Countdown + " s";
                    case "wave": return "Wave " + c.wave + "/" + c.waves + "  -  " + c.left + (c.left == 1 ? " invader" : " invaders") + "  -  " + c.gate + " gate"
                        + (c.ram == 1 ? "  -  a battering ram!" : "") + (c.fr != null && c.fr.Length > 0 ? "  -  " + c.fr.Length + (c.fr.Length == 1 ? " roof" : " roofs") + " burning" : "");
                    case "won": return "The town holds!";
                    case "lost": return "The gate fell. The invaders plundered the town.";
                    default: return "";
                }
            }
        }
    }
}
