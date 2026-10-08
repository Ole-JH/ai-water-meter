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
                Id = 33, Date = "2026-10-08", Title = "Greater rifts",
                Items = new[]
                {
                    "A Rift Stone now stands in Hollowmere's square. Open a greater rift at the tier you choose: one level with monsters from three dungeons, tougher with every tier.",
                    "Fill the progress bar, then beat the Rift Guardian within 10 minutes to unlock the next tier, earn a chest's worth of loot and get on the leaderboard.",
                    "Three new achievements, one with the title \"the Riftwalker\".",
                },
            },
            new Entry
            {
                Id = 32, Date = "2026-10-08", Title = "Guilds",
                Items = new[]
                {
                    "Found a guild with /guild create Name TAG (1000 gold). Its tag shows before every member's name.",
                    "Guild chat with /g, a message of the day, officers who can invite and remove members, and the guild window (O). Type /guild for all the commands.",
                },
            },
            new Entry
            {
                Id = 31, Date = "2026-10-08", Title = "Duels",
                Items = new[]
                {
                    "Right-click another hero and challenge them to a duel. After a countdown you fight each other, and only each other, with everything you've got.",
                    "Nobody dies: at your last breath you yield. Running from the duel flag loses too. Three new achievements, one with the title \"the Duelist\".",
                },
            },
            new Entry
            {
                Id = 30, Date = "2026-10-08", Title = "Paragon levels",
                Items = new[]
                {
                    "Level 30 is now the highest level. Past it, experience earns paragon levels, each with a point for Might (damage), Toughness (life), Precision (critical hits) or Swiftness (attack and movement speed). Spend them in the character window (C); resetting them is free.",
                    "Your paragon level shows next to your level on your nameplate. Three new achievements, the last with the title \"the Paragon\".",
                },
            },
            new Entry
            {
                Id = 29, Date = "2026-10-08", Title = "Salvage & reforge",
                Items = new[]
                {
                    "Weapon and armor merchants can now salvage your gear into materials (Scrap Iron, Arcane Dust, Veiled Crystals, Forgotten Souls); socketed gems come back to you. One button salvages all your common and magic gear.",
                    "Reforge one property of a magic, rare, legendary or set item into a new one, for materials and gold. Once you have reforged a property, only that one can be reforged again.",
                    "Three new achievements, one with the title \"the Perfectionist\".",
                },
            },
            new Entry
            {
                Id = 28, Date = "2026-10-08", Title = "World bosses",
                Items = new[]
                {
                    "Four giants sleep in the wilds: Old Bramblehide in Whisperwood, Hrimgar the Mountain in Frostpeak, Gorvash the Dune Reaver in the badlands and the Pyre Colossus in the Ashen Reach. Every hour and a half or so one rises, and everyone is told where.",
                    "They grow tougher with every hero who joins the fight. Watch for the red ring: step out before the slam lands. They call for help as they weaken, and rage at the end.",
                    "Everyone who fought gets the kill and better loot than any dungeon boss. Two new achievements, one with the title \"the Giantsbane\".",
                    "The tracker and markers on the minimap and world map show where the world boss and any town under attack are.",
                },
            },
            new Entry
            {
                Id = 27, Date = "2026-10-08", Title = "Town invasions",
                Items = new[]
                {
                    "Now and then monsters from the wilds attack a walled town where heroes are: Hollowmere, Frosthaven, Saltreach or Emberwatch. Everyone hears of it, and the gate they gather at shows on the minimap and the world map.",
                    "Three waves march on the gate, the last led by a warlord. Kill them before they batter the gate down (the tracker under the minimap shows how it holds), or they plunder the town and leave.",
                    "Hold the town and everyone who fought gets experience and a boss's share of loot. Three new achievements, one with the title \"the Defender\".",
                },
            },
            new Entry
            {
                Id = 26, Date = "2026-10-08", Title = "Faster, with more graphics settings",
                Items = new[]
                {
                    "Settings > Graphics > More... has every option on its own: resolution, frame rate, shadows, shadow distance, lights, grass, small details and effects.",
                    "On Retina and other high-resolution screens the game no longer draws at twice the page's resolution by default (four times the pixels); choose 150% or Native if your machine can take it.",
                    "Fixed slowdowns from the bigger world: hundreds of mithril rocks each had a light, and the map and labels walked through every tree and rock several times a frame.",
                    "Grass is only drawn near you, and the snow and lantern updates are spread out instead of landing on one frame.",
                    "Hollowmere's well, winch and bucket, now stands in the middle of the square instead of off in a corner of it.",
                    "Waystones got a proper look: a stone pillar on a stepped plinth with glowing rune bands, the orb floating right above it.",
                },
            },
            new Entry
            {
                Id = 25, Date = "2026-10-07", Title = "Dungeons beyond, mounts, and a harder world",
                Items = new[]
                {
                    "Three new dungeons in the outer lands: the Frozen Barrow (Frostpeak, level 15+), the Sunken Temple (the badlands, 16+) and the Ashen Citadel (the Reach, 20+), with three new bosses at the bottom.",
                    "Mounts: Beastmaster Orla sells a Riding Horse, a White Charger and a Frostpeak Stag (60-90% faster). Press V to ride; attacking, casting or taking a hit throws you off.",
                    "Death and Recall now take you to the nearest town whose waystone you know, not always back to Hollowmere.",
                    "A new loading screen, with tips while the world loads.",
                    "Hollowmere's fountain no longer stands in the north street, in the way to the north gate.",
                    "Harder: monsters have more life and hit harder, notice you from further away and bring friends, and one in six is an elite. Potions are a big emergency heal on a 15-second cooldown instead of a drip every 3 seconds, life regenerates more slowly, and dying costs 15% of your gold.",
                },
            },
            new Entry
            {
                Id = 24, Date = "2026-10-07", Title = "The outer lands",
                Items = new[]
                {
                    "The world is four times bigger. Beyond a broken ridge north and east of the old lands lie the Frostpeak Wilds (snow all year), the Sunscar Badlands and the Ashen Reach, for heroes of level 12 and up.",
                    "New towns: Frosthaven in the north, Saltreach by the badlands oasis, Emberwatch, the last outpost in the Reach, and the woodcutters' hamlet Pinecrest on the north road. Each has merchants, a healer and quests; the walled towns have a stash chest.",
                    "Waystones: walk up to a town's waystone to attune to it, then use any waystone to travel there.",
                    "Thirteen new monsters, three new bosses (Jarl Frostborn, the Raider Warlord and the Ashen King), twelve new quests and seven new achievements. The Ashen Reach counts as a deep place for loot.",
                    "Town streets no longer get buried in snow: the elves keep shovelling, but the paths stay walkable when they fall behind.",
                    "Press Enter on the login screens to log in, even while typing in a field.",
                },
            },
            new Entry
            {
                Id = 23, Date = "2026-10-07", Title = "A day in Hollowmere, and harder loot",
                Items = new[]
                {
                    "The village lives by the clock: villagers go to work at dawn, lunch outside the tavern, chat on the square in the evening and go home at night. Children play tag, the guards change shifts, the smith hammers.",
                    "Loot is scarcer. Monsters in the open world rarely drop gear and never anything better than magic; rare, set and legendary items come from dungeons, the Crypt of the Lich, elites, chests and above all bosses.",
                    "Right-click anywhere on another player to invite them, whisper or trade.",
                    "The minimap turns with the camera. Prefer north up? Click the R button on the minimap.",
                    "Smoother: lighter network traffic, a cheaper minimap and fewer lights drawn far away.",
                },
            },
            new Entry
            {
                Id = 22, Date = "2026-10-07", Title = "Seasons, weather and festivals",
                Items = new[]
                {
                    "The year turns: spring, summer, autumn and winter, with weather to match. Rain soaks the ground, storms bring thunder and lightning, fog rolls in.",
                    "Snow falls in winter, and up north in spring and autumn. It piles up while it snows, and deep snow slows you down, more the longer it cakes onto your boots.",
                    "Hollowmere celebrates every season: the Bloom Festival, the Midsummer Fair, the Harvest Festival and Winterfest.",
                    "At Winterfest, elves shovel the streets clear, a path at a time. Lakes freeze over.",
                    "In autumn the villagers rake the leaves into big piles. Do <b>not</b> run through them.",
                },
            },
            new Entry
            {
                Id = 21, Date = "2026-10-07", Title = "Music and achievements",
                Items = new[]
                {
                    "Music! Hollowmere, the wilds, the graveyard and the dungeons each have their own, and fights with elites, crowds and bosses get battle music. Volume under Esc > Settings > Music.",
                    "57 achievements to earn: press <b>Y</b>. Slay monsters and bosses, conquer dungeons on every difficulty, explore the world, master your professions, make friends.",
                    "Some achievements give a <b>title</b> to wear under your name, like «Lichbane» or «Kingslayer».",
                    "Your party and the players around you see when you earn one. What your hero already did counts.",
                },
            },
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
