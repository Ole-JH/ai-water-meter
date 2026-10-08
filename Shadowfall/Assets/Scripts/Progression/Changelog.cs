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
                Id = 66, Date = "2026-10-08", Title = "Over here!",
                Items = new[]
                {
                    "Alt+click the world map or minimap to ping a spot for your party: it ripples on their maps, a pillar of light marks it and a bell rings.",
                    "The world map shows the shops, healers, stashes, auction house, bounty boards and dungeon doors you've found.",
                    "The zone you're in stands out on the world map.",
                },
            },
            new Entry
            {
                Id = 65, Date = "2026-10-08", Title = "Points well spent",
                Items = new[]
                {
                    "Talents light up in purple as you put points in, and gold once full; ones you can still learn breathe.",
                    "Spending a talent or paragon point flashes the row with a chime; paragon rows fill up as points go in.",
                },
            },
            new Entry
            {
                Id = 64, Date = "2026-10-08", Title = "Tales well told",
                Items = new[]
                {
                    "Quest markers bob over the givers' heads and glow; a quest ready to hand in pulses.",
                    "Quest givers' words come out as they speak (click to read it all at once).",
                    "Handing in a quest: a ring of gold at your feet, and the coins and reward fly into your bags.",
                },
            },
            new Entry
            {
                Id = 63, Date = "2026-10-08", Title = "A tidier pack",
                Items = new[]
                {
                    "Drag items between bag slots to arrange them; dropping onto the same potion tops up the stack.",
                    "New items sparkle in your bags until you look at them.",
                    "Green and red arrows show whether a piece is better or worse than what you're wearing.",
                },
            },
            new Entry
            {
                Id = 62, Date = "2026-10-08", Title = "Easy on the eyes",
                Items = new[]
                {
                    "Settings > Comfort & effects: turn down screen shake, switch off hit pauses and screen flashes, and show all, big or no damage numbers.",
                    "Colour-blind loot colours: set items teal, legendaries pink.",
                    "Legendary weapons now glow in your hand too, not just set weapons.",
                },
            },
            new Entry
            {
                Id = 61, Date = "2026-10-08", Title = "Weapons with weight",
                Items = new[]
                {
                    "Melee swings leave a streak in your weapon's rarity colour, alternating sides as you strike.",
                    "Set and legendary weapons glow in your hand and shed sparks.",
                },
            },
            new Entry
            {
                Id = 60, Date = "2026-10-08", Title = "Every death its own",
                Items = new[]
                {
                    "Skeletons fall apart into a heap of bones, golems crumble into rocks, and wraiths dissolve into mist.",
                },
            },
            new Entry
            {
                Id = 59, Date = "2026-10-08", Title = "Arrivals",
                Items = new[]
                {
                    "Entering a zone or a dungeon shows its name in big type with a line beneath: the dungeon's depth and difficulty, or a word about the land.",
                    "Dungeons fade up out of the dark when you go in or come out.",
                    "Waystone travel flashes, and the view swoops down from high above onto you.",
                },
            },
            new Entry
            {
                Id = 58, Date = "2026-10-08", Title = "The wild, alive",
                Items = new[]
                {
                    "Fish leap out of the lakes with a splash, and little waves lap at the shore.",
                    "Crows peck about on the ground by day, and burst up and away when you come near.",
                },
            },
            new Entry
            {
                Id = 57, Date = "2026-10-08", Title = "Listen",
                Items = new[]
                {
                    "Golems, giants and bosses thud as they walk, and the biggest shake the ground.",
                    "Wolves howl in the distance at night out in the wilds, leaves stir in the forests by day, and dungeons rumble, drip and groan.",
                    "Windows open with the turn of a page and close with a soft thump, and sand puffs up underfoot.",
                },
            },
            new Entry
            {
                Id = 56, Date = "2026-10-08", Title = "Hear ye!",
                Items = new[]
                {
                    "Every walled town has a town crier who rings a hand bell and cries the news. Invasions and world bosses are cried at once in every town.",
                    "Merchants nod, flip a coin and have a word when you buy or sell.",
                },
            },
            new Entry
            {
                Id = 55, Date = "2026-10-08", Title = "Your bar, your way",
                Items = new[]
                {
                    "Drag an ability onto another slot of the action bar to swap them. Keys 1 to 5 follow the new order, and it's saved with your character.",
                },
            },
            new Entry
            {
                Id = 54, Date = "2026-10-08", Title = "Monsters with manners",
                Items = new[]
                {
                    "Monsters show a \"!\" and cry out when they spot you.",
                    "Archers and casters back away when you close in, while their next shot readies.",
                    "Badly wounded monsters limp.",
                    "Elite affixes show: Fire Enchanted elites leave burning ground behind them (don't stand in it), Fast ones kick up dust, Mighty blows crack the ground under you, and Vampiric hits draw your blood to them.",
                },
            },
            new Entry
            {
                Id = 53, Date = "2026-10-08", Title = "Spells that leave a mark",
                Items = new[]
                {
                    "Fireballs and meteors scorch the ground with glowing embers, Frost Nova leaves rime and ice splinters, Leap and meteors crack the earth, and holy power burns glowing runes. They fade after a while.",
                    "Ability buttons pop in their colour when you cast, and flash when a cooldown comes back.",
                },
            },
            new Entry
            {
                Id = 52, Date = "2026-10-08", Title = "Level up, gear up",
                Items = new[]
                {
                    "A level up flashes gold, and what it brought (life, mana, attribute and talent points) rises off you line by line.",
                    "Putting on gear glints its slot in the character window, and your figure there turns round to show it off.",
                },
            },
            new Entry
            {
                Id = 51, Date = "2026-10-08", Title = "A livelier screen",
                Items = new[]
                {
                    "The health and mana orbs slosh when they change: a big hit or a potion sets the liquid swaying.",
                    "A shimmer runs along the experience bar whenever experience comes in.",
                    "Quest counts pop as they tick up, and a finished objective is struck out before \"Return to\" takes its place.",
                    "Monster health bars fade with distance instead of popping in and out.",
                },
            },
            new Entry
            {
                Id = 50, Date = "2026-10-08", Title = "A death worth remembering",
                Items = new[]
                {
                    "You fall in slow motion as the colour drains from the world, and the gold death costs you spills from your purse.",
                    "The death screen fades in after the fall.",
                    "Releasing your spirit brings you back on a waystone's shimmer: a pale flash, a column of light, and you gather out of the motes as the colour returns.",
                },
            },
            new Entry
            {
                Id = 49, Date = "2026-10-08", Title = "Loot you can hear",
                Items = new[]
                {
                    "Loot bursts out of the monster, tumbles and bounces where it lands. Each rarity sounds different as it hits the ground, so you hear a rare before you see it.",
                    "Magic, rare and set items raise a beam of their colour that breathes and glows. A legendary raises a pillar of light you can see from far off, with a ping on the minimap.",
                    "What you pick up flies into your hands, and gold tinkles with a count.",
                },
            },
            new Entry
            {
                Id = 48, Date = "2026-10-08", Title = "Hits that land",
                Items = new[]
                {
                    "Monsters flash and rock back when you hit them. Your crits freeze the moment for a heartbeat and kick the camera.",
                    "Damage numbers pop out and arc away, bigger for bigger hits. Crits and hits on you shake.",
                    "Health bars show a pale chip of what your last hits took before it drains away.",
                    "A big killing blow throws the body back, elites die in a burst of their aura's colour, and a boss falls in slow motion.",
                    "Slowed monsters frost over, with ice at their feet.",
                },
            },
            new Entry
            {
                Id = 47, Date = "2026-10-08", Title = "Deeper dungeons",
                Items = new[]
                {
                    "Dungeon corridors have traps: pressure plates that fire spikes, and pendulum blades swinging across the way. Watch your step, and time your run.",
                    "When a boss fight starts, iron portcullises slam down over the boss room's doorways. They rise again when the boss falls.",
                    "Treasure chests open properly: the lid swings up and gold light spills out.",
                },
            },
            new Entry
            {
                Id = 46, Date = "2026-10-08", Title = "Puddles and lightning",
                Items = new[]
                {
                    "Puddles gather as the rain soaks the ground, with raindrops rippling on them. They shrink as it dries and freeze over in winter.",
                    "In a thunderstorm, lightning sometimes strikes a tree near you. It burns for a while, then stands charred and bare.",
                    "Your breath steams in the cold.",
                },
            },
            new Entry
            {
                Id = 45, Date = "2026-10-08", Title = "Wanted",
                Items = new[]
                {
                    "Every walled town has a bounty board with your three bounties pinned up as notices. Finish one and its notice is torn off.",
                    "Finish all three and the Bounty Cache falls out of the sky at your feet, trailing light, and bursts open.",
                },
            },
            new Entry
            {
                Id = 44, Date = "2026-10-08", Title = "Going once, going twice",
                Items = new[]
                {
                    "An auctioneer stands at a podium beside every general merchant. He calls out lots, and when something sells he bangs his gavel, rings his bell and calls out the sale.",
                    "A courier now runs up to you with the gold from your sales and any unsold items, wherever you are.",
                },
            },
            new Entry
            {
                Id = 43, Date = "2026-10-08", Title = "Banners of the guilds",
                Items = new[]
                {
                    "Every guild has a banner: two colours and an emblem. Members carry it on their backs, and the leader can change it in the guild window (O).",
                    "The Guild Board on Hollowmere's square hangs the banners of the biggest guilds. Click it to see every guild in the realm.",
                    "Siege ladders lean against the wall walkway properly instead of poking through it.",
                },
            },
            new Entry
            {
                Id = 42, Date = "2026-10-08", Title = "Saddle up",
                Items = new[]
                {
                    "Your mount comes galloping in when you call it, and you swing up into the saddle.",
                    "Mounts wear saddles and blankets, and the White Charger has red-and-gold barding. They kick up dust, sand or snow, and you hear their hooves.",
                    "Get off and your mount trots away. Get knocked off by a hit and it rears up and bolts.",
                    "The forge's quench now sizzles properly, and a few missing sounds were fixed.",
                },
            },
            new Entry
            {
                Id = 41, Date = "2026-10-08", Title = "Duels with a crowd",
                Items = new[]
                {
                    "A duel puts up a ring of pennant posts and rope that everyone nearby can see, with a big 3-2-1 countdown and a gong at each number.",
                    "Villagers stop to watch and cheer. At the end the loser sits down in the dirt, the winner cheers under a golden banner, and the crowd applauds.",
                },
            },
            new Entry
            {
                Id = 40, Date = "2026-10-08", Title = "Rifts with a pulse",
                Items = new[]
                {
                    "Every rift tier has its own colour, from violet through blue, green and gold to blood red. The light breathes faster when time runs short.",
                    "An orb of rift energy floats by your shoulder and fills as you kill, each kill sending a mote of essence into it. When it's full it bursts, and the Rift Guardian steps out of a tear in the air.",
                    "A cleared rift collapses. Rocks fall and the dark closes in, then you're thrown back to the Rift Stone with any loot you hadn't picked up.",
                },
            },
            new Entry
            {
                Id = 39, Date = "2026-10-08", Title = "At the anvil",
                Items = new[]
                {
                    "The smiths of the walled towns have a real forge: an anvil with a glowing ingot, a hearth of coals with smoke and embers, a quench bucket and a pile of salvaged scrap.",
                    "Salvaging and reforging happen there while you watch. The smith hammers your piece. Salvaged gear shatters and its materials fly onto the pile. A reforged property burns away in red, the new one is stamped in gold, and the piece is quenched with a hiss.",
                },
            },
            new Entry
            {
                Id = 38, Date = "2026-10-08", Title = "Giants worth gathering for",
                Items = new[]
                {
                    "World bosses climb out of their land when they rise: Bramblehide from a thicket, Hrimgar from frozen earth, Gorvash from the sand, the Pyre Colossus from a lava crack.",
                    "Each wears three armour plates that break off at three quarters, half and a quarter of its health. Every break lowers its armour and quickens its slams. Once the last is gone, it burns with rage.",
                    "Slams leave craters, and a fallen giant lies where it fell for a minute and a half.",
                },
            },
            new Entry
            {
                Id = 37, Date = "2026-10-08", Title = "Hold the walls",
                Items = new[]
                {
                    "Town gates shut when an invasion begins. Every blow shows: the gate splinters and shakes, darkens and sags as it weakens, and planks and bars break off and fall into the town until it gives way.",
                    "The militia carry ladders to the inside of the wall either side of the gate and lay a walkway along the top. Climb up to shoot and cast down at the invaders. Only their archers and casters can hit you up there.",
                    "Click outside the wall to jump down among the invaders, or inside to get back into town. New achievement: Over the Wall.",
                    "Mounts show as real horses and stags again instead of boxes.",
                },
            },
            new Entry
            {
                Id = 36, Date = "2026-10-08", Title = "Smoother in the browser",
                Items = new[]
                {
                    "The game now draws in step with your screen. Before, the 60 frames-a-second limit was timed apart from the screen's refresh, and many screens ended up showing only 30, whatever the graphics settings.",
                    "The Frame rate setting still caps it: 30 draws every other refresh of a 60 Hz screen, 60 every other one of a 120 Hz screen.",
                    "Laptops with two graphics chips now get the faster one.",
                },
            },
            new Entry
            {
                Id = 35, Date = "2026-10-08", Title = "The auction house",
                Items = new[]
                {
                    "Tidied Hollowmere: a street lantern no longer stands inside a house, the cooking fire moved next to the tavern (it was where the maypole and the winter tree go), the summer bonfire no longer sits on the stash chest, and the winter snowmen and sled stay out of the lanterns and hedges.",
                    "Heroes run a little slower (5.3 instead of 6.2 metres a second): it felt like sprinting. Mounts slow down by the same amount.",
                    "Every general goods merchant now has an Auction House: put items from your bags up for sale at your price, or browse, search and buy what others sell.",
                    "Sales pay out at once, or at your next login if you're away (the house keeps 5%). Unsold items come back after 48 hours. Two new achievements.",
                },
            },
            new Entry
            {
                Id = 34, Date = "2026-10-08", Title = "Daily bounties",
                Items = new[]
                {
                    "Three bounties a day, picked for your level: hunt a kind of monster, slay elites, defeat a dungeon boss. They show above your quests.",
                    "Each pays gold, experience and gear at once; finish all three for the Bounty Cache. Two new achievements.",
                    "Fixes: right-click casts at your duel opponent instead of opening their menu; Esc closes the guild and rift windows.",
                    "Fixed a stream of \"SphereCollider doesn't exist\" errors in the browser build.",
                },
            },
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
