namespace Shadowfall
{
    /// <summary>
    /// The in-game "What's New" list. Add an entry at the TOP with the next <see cref="Entry.Id"/> whenever a
    /// player-visible change ships; heroes see it marked NEW until they open the window.
    /// The last id a hero has read is saved with the character (<see cref="SaveData.news"/>).
    /// </summary>
    public static class Changelog
    {
        public class Entry
        {
            public int Id;
            public string Date, Title;
            public string[] Items;
        }

        /// <summary>Characters saved before the changelog existed have read everything up to this entry.</summary>
        public const int Baseline = 10;

        public static int Latest => Entries[0].Id;

        /// <summary>Newest first.</summary>
        public static readonly Entry[] Entries =
        {
            new Entry
            {
                Id = 20, Date = "2026-10-07", Title = "See your party",
                Items = new[]
                {
                    "Party frames show each member's live portrait in their own gear, their level and class, life and mana, and where they are.",
                    "Every party member has a color. On the minimap they are arrows pointing where they face; out of range, they wait on the edge pointing the way.",
                    "The world map (M) shows your party with their names, wherever they are.",
                },
            },
            new Entry
            {
                Id = 19, Date = "2026-10-07", Title = "Your loot, kept safe",
                Items = new[]
                {
                    "Gold and items are now kept by the server, so nobody can cheat them in, and they're never lost to a crashed browser.",
                    "Loot is personal: what drops is rolled for you alone, and only you see it.",
                    "Trading: offered items stay in your bags, marked TRADE, until the trade completes.",
                    "Merchants keep the same stock for you until they restock, and won't sell you something that just sold out.",
                },
            },
            new Entry
            {
                Id = 18, Date = "2026-10-07", Title = "A greener, wilder world",
                Items = new[]
                {
                    "New trees everywhere: leafy oaks, golden willows and tall dark yews to chop, pine and broadleaf forests, gnarled dead trees in the graveyard.",
                    "Mining rocks are mossy boulders studded with real copper, iron and glowing blue mithril ore.",
                    "New boulders and cliffs, bushes, ferns, flowers, mushrooms and pebbles.",
                    "Updating no longer locks anyone out: an outdated game reloads itself into the new version.",
                },
            },
            new Entry
            {
                Id = 17, Date = "2026-10-07", Title = "Blood and guts",
                Items = new[]
                {
                    "Hits spray blood away from the blow, and it stays on the ground, drying darker over a few minutes.",
                    "Kills burst: splatter all around, a pool spreading under the body, and chunks flying on crits and heavy blows.",
                    "Skeletons shatter into bone chips, golems into rubble, goblins bleed green. Badly wounded monsters leave a trail.",
                    "Corpses lie a while longer before they sink away.",
                    "Too much? Esc > Settings > Gore: Off, Normal or Extra.",
                },
            },
            new Entry
            {
                Id = 16, Date = "2026-10-07", Title = "Accounts, heroes and password resets",
                Items = new[]
                {
                    "You now log in with an <b>account</b> that can hold up to 10 heroes. Pick one on the new hero screen, or create another.",
                    "Your old hero became an account with the same name and password. Nothing is lost.",
                    "Every account has a <b>recovery code</b>. Keep it safe: with it you can choose a new password if you forget yours.",
                    "Forgot your password? Use your recovery code, a code emailed to you (add an email under Esc > Account), or ask an admin.",
                    "Esc > Account: change your password, set your email, get a new recovery code. Esc > Character Select switches heroes.",
                    "Typos on the login screen no longer create new heroes, and repeated wrong passwords lock an account for a short while.",
                },
            },
            new Entry
            {
                Id = 15, Date = "2026-10-07", Title = "Emotes",
                Items = new[]
                {
                    "Press <b>G</b> for the emote menu, or type /wave, /dance, /bow, /clap, /flex, /point, /cheer, /sit, /sleep, /jump and more in chat.",
                    "Every emote is fully animated, and players around you see it too.",
                    "The camera now starts at the normal angle after logging in, instead of skimming the ground.",
                },
            },
            new Entry
            {
                Id = 14, Date = "2026-10-07", Title = "What's New and server monitoring",
                Items = new[]
                {
                    "This window! New changes are marked <b>NEW</b> until you've read them. Find it again in the Esc menu.",
                    "Server operators get a Prometheus and Grafana monitoring stack with a ready-made dashboard.",
                },
            },
            new Entry
            {
                Id = 13, Date = "2026-10-07", Title = "Companions and crafting come alive",
                Items = new[]
                {
                    "Companions carry their own gear: Edric's helm and shield, Nell's witch hat, Kestrel's crossbow, Mira's wand and spellbook.",
                    "Each companion has its own presence: fire in Nell's hand, light motes around Mira, glowing runes on the Golem.",
                    "Companions arrive in a pillar of light and every attack has its own effect: bites, cleaves, shield bashes, ground slams.",
                    "Chopping throws wood chips, mining strikes sparks, fishing splashes; the anvil sparks and cooking fires flare.",
                    "Potions swirl, Recall draws a rune circle, teleports arrive in a pillar of light.",
                },
            },
            new Entry
            {
                Id = 12, Date = "2026-10-07", Title = "Living fire",
                Items = new[]
                {
                    "Campfires, torches and braziers now burn with real flames, embers and smoke, and their light flickers.",
                },
            },
            new Entry
            {
                Id = 11, Date = "2026-10-07", Title = "Spells with more punch",
                Items = new[]
                {
                    "Every cast draws a glowing rune circle; shouts and prayers raise a larger one in your class's colour.",
                    "War Cry, Divine Shield and Vanished show an aura while they last; stunned monsters see stars.",
                    "Whirlwind spins a blood vortex, Leap trails embers, fireballs leave burning ground, meteors send out shockwaves.",
                },
            },
            new Entry
            {
                Id = 10, Date = "2026-10-07", Title = "Harder, and rarer loot",
                Items = new[]
                {
                    "Tooltips show an item's type and rarity tier (Common to Legendary, tier 1-5).",
                    "Monsters hit harder and have more life. Potions have a 3 second cooldown.",
                    "Dungeons have four difficulties: Normal, Veteran, Nightmare and Hell, with better loot and more experience.",
                    "Three new dungeon bosses: the Bandit Lord, the Goblin King and the Stone Colossus.",
                },
            },
            new Entry
            {
                Id = 9, Date = "2026-10-06", Title = "Your hero, on screen",
                Items = new[]
                {
                    "A live portrait of your hero, wearing your gear, in the top left and in the character window.",
                    "Hold <b>Shift</b> over an item to compare it with what you're wearing.",
                    "Interface scale setting (70% to 150%) in Esc > Settings.",
                },
            },
            new Entry
            {
                Id = 8, Date = "2026-10-06", Title = "Party dungeons and fog of war",
                Items = new[]
                {
                    "Four procedurally generated dungeons: the Catacombs, the Bandit Hideout, the Goblin Warrens and the Deep Mine. Your party shares one instance.",
                    "The map is covered in fog until you explore it. Monsters and dungeon entrances no longer show on the map.",
                },
            },
            new Entry
            {
                Id = 7, Date = "2026-10-06", Title = "A much larger world",
                Items = new[]
                {
                    "The world is more than twice as large, with goblin camps, more lakes and a bigger graveyard.",
                    "Hollowmere is split into districts with room to breathe.",
                    "A new round minimap and an Esc menu with settings and log out.",
                    "Jenkins the butler now runs a very literal DevOps quest line. Check the logs.",
                },
            },
            new Entry
            {
                Id = 6, Date = "2026-10-06", Title = "Companions for hire",
                Items = new[]
                {
                    "Beastmaster Orla hires out six companions that fight at your side: a War Hound, a squire, a hedge witch, a ranger, a healer and a Stone Golem.",
                    "Each class's spells have their own sounds and look.",
                },
            },
            new Entry
            {
                Id = 5, Date = "2026-10-06", Title = "Classes, talents and legendary loot",
                Items = new[]
                {
                    "Each class has five unique abilities and a talent tree.",
                    "Legendary items with special powers, item sets, gem sockets, a personal stash and player trading.",
                },
            },
            new Entry
            {
                Id = 4, Date = "2026-10-06", Title = "Elites and the Catacombs",
                Items = new[]
                {
                    "Elite monsters with affixes roam the world.",
                    "The Catacombs open beneath the graveyard: an instanced dungeon with a boss at the bottom.",
                },
            },
            new Entry
            {
                Id = 3, Date = "2026-10-06", Title = "Sound and fury",
                Items = new[]
                {
                    "Sound effects, real spell effects and animated combat.",
                    "A dark gothic interface and a new login screen.",
                },
            },
            new Entry
            {
                Id = 2, Date = "2026-10-06", Title = "A living village",
                Items = new[]
                {
                    "Vendors, chatty villagers, patrolling guards, parties and speech bubbles.",
                    "A day and night cycle, a free camera, grass and water.",
                },
            },
            new Entry
            {
                Id = 1, Date = "2026-10-06", Title = "Shadowfall opens",
                Items = new[] { "The first release: explore, fight, gather, craft and quest with friends in your browser." },
            },
        };

        /// <summary>How many entries are newer than <paramref name="seen"/>.</summary>
        public static int UnreadCount(int seen)
        {
            int n = 0;
            foreach (var e in Entries) if (e.Id > seen) n++;
            return n;
        }
    }
}
