using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    public enum AchievementCategory { Combat, Bosses, Dungeons, Exploration, Hero, Professions, Social }

    /// <summary>
    /// One achievement: earned when the hero's counter <see cref="Stat"/> reaches <see cref="Goal"/>.
    /// Some award a <see cref="Title"/> the hero can wear under their name.
    /// The server reads the ids, names and titles from this file (task gamedata), so keep ids stable.
    /// </summary>
    public class AchievementDef
    {
        public string Id, Name, Description, Icon, Title;
        public AchievementCategory Category;
        public int Points = 10;
        public string Stat;
        public int Goal = 1;
    }

    public static class AchievementDatabase
    {
        public static readonly AchievementDef[] All =
        {
            // ---- combat
            new AchievementDef { Id = "first_blood", Name = "First Blood", Description = "Slay your first monster.", Category = AchievementCategory.Combat, Icon = "ach_kills", Points = 5, Stat = "kills", Goal = 1 },
            new AchievementDef { Id = "slayer_100", Name = "Monster Slayer", Description = "Slay 100 monsters.", Category = AchievementCategory.Combat, Icon = "ach_kills", Points = 10, Stat = "kills", Goal = 100 },
            new AchievementDef { Id = "slayer_1000", Name = "Scourge of the Wilds", Description = "Slay 1,000 monsters.", Category = AchievementCategory.Combat, Icon = "ach_kills", Points = 25, Stat = "kills", Goal = 1000, Title = "the Slayer" },
            new AchievementDef { Id = "slayer_5000", Name = "Unstoppable", Description = "Slay 5,000 monsters.", Category = AchievementCategory.Combat, Icon = "ach_kills", Points = 50, Stat = "kills", Goal = 5000, Title = "the Unstoppable" },
            new AchievementDef { Id = "wolves", Name = "Wolfbane", Description = "Slay 50 Dire Wolves.", Category = AchievementCategory.Combat, Icon = "ach_wolf", Points = 10, Stat = "slain.wolves", Goal = 50, Title = "Wolfbane" },
            new AchievementDef { Id = "goblins", Name = "Goblin Smasher", Description = "Slay 100 goblins, shamans and warchiefs.", Category = AchievementCategory.Combat, Icon = "ach_goblin", Points = 10, Stat = "slain.goblins", Goal = 100 },
            new AchievementDef { Id = "undead", Name = "Bone Collector", Description = "Put 100 skeletons and zombies back in the ground.", Category = AchievementCategory.Combat, Icon = "ach_bones", Points = 10, Stat = "slain.undead", Goal = 100, Title = "Bonebreaker" },
            new AchievementDef { Id = "bandits", Name = "Law of Hollowmere", Description = "Slay 50 bandits.", Category = AchievementCategory.Combat, Icon = "ach_bandit", Points = 10, Stat = "slain.bandits", Goal = 50 },
            new AchievementDef { Id = "golems", Name = "Rockbreaker", Description = "Shatter 25 Rock Golems.", Category = AchievementCategory.Combat, Icon = "ach_golem", Points = 10, Stat = "slain.golems", Goal = 25 },
            new AchievementDef { Id = "elite_1", Name = "Champion Slayer", Description = "Slay an elite champion.", Category = AchievementCategory.Combat, Icon = "ach_elite", Points = 10, Stat = "elites", Goal = 1 },
            new AchievementDef { Id = "elite_50", Name = "Champion Hunter", Description = "Slay 50 elite champions.", Category = AchievementCategory.Combat, Icon = "ach_elite", Points = 25, Stat = "elites", Goal = 50, Title = "Champion Hunter" },

            // ---- bosses
            new AchievementDef { Id = "boss_warchief", Name = "Warchief's End", Description = "Defeat the Goblin Warchief in his camp.", Category = AchievementCategory.Bosses, Icon = "ach_goblin", Points = 10, Stat = "boss.Goblin Warchief", Goal = 1 },
            new AchievementDef { Id = "boss_lich", Name = "Lichbane", Description = "Defeat the Lich King in his crypt.", Category = AchievementCategory.Bosses, Icon = "ach_lich", Points = 25, Stat = "boss.Lich King", Goal = 1, Title = "Lichbane" },
            new AchievementDef { Id = "boss_crypt_lord", Name = "Rest in Pieces", Description = "Defeat the Crypt Lord at the bottom of the Catacombs.", Category = AchievementCategory.Bosses, Icon = "ach_grave", Points = 10, Stat = "boss.Crypt Lord", Goal = 1 },
            new AchievementDef { Id = "boss_bandit_lord", Name = "Hideout Raided", Description = "Defeat the Bandit Lord in his hideout.", Category = AchievementCategory.Bosses, Icon = "ach_bandit", Points = 10, Stat = "boss.Bandit Lord", Goal = 1 },
            new AchievementDef { Id = "boss_goblin_king", Name = "Regicide", Description = "Defeat the Goblin King in the Warrens.", Category = AchievementCategory.Bosses, Icon = "ach_boss", Points = 10, Stat = "boss.Goblin King", Goal = 1 },
            new AchievementDef { Id = "boss_colossus", Name = "Colossus Toppled", Description = "Defeat the Stone Colossus in Ironvein Deep.", Category = AchievementCategory.Bosses, Icon = "ach_golem", Points = 10, Stat = "boss.Stone Colossus", Goal = 1 },
            new AchievementDef { Id = "boss_all", Name = "Kingslayer", Description = "Defeat six different bosses.", Category = AchievementCategory.Bosses, Icon = "ach_boss", Points = 50, Stat = "boss", Goal = 6, Title = "Kingslayer" },
            new AchievementDef { Id = "boss_jarl", Name = "Giant Slayer", Description = "Defeat Jarl Frostborn in his high camp in the Frostpeak Wilds.", Category = AchievementCategory.Bosses, Icon = "ach_boss", Points = 25, Stat = "boss.Jarl Frostborn", Goal = 1 },
            new AchievementDef { Id = "boss_warlord", Name = "Warlord Down", Description = "Defeat the Raider Warlord in the Sunscar Badlands.", Category = AchievementCategory.Bosses, Icon = "ach_bandit", Points = 25, Stat = "boss.Raider Warlord", Goal = 1 },
            new AchievementDef { Id = "boss_ashen", Name = "Ashes to Ashes", Description = "Defeat the Ashen King in the heart of the Ashen Reach.", Category = AchievementCategory.Bosses, Icon = "ach_lich", Points = 50, Stat = "boss.The Ashen King", Goal = 1, Title = "the Unburnt" },
            new AchievementDef { Id = "boss_frost_witch", Name = "Thaw", Description = "Defeat the Frost Witch at the bottom of the Frozen Barrow.", Category = AchievementCategory.Bosses, Icon = "ach_lich", Points = 25, Stat = "boss.The Frost Witch", Goal = 1 },
            new AchievementDef { Id = "boss_sand_colossus", Name = "Sand in the Gears", Description = "Defeat the Sand Colossus in the Sunken Temple.", Category = AchievementCategory.Bosses, Icon = "ach_golem", Points = 25, Stat = "boss.The Sand Colossus", Goal = 1 },
            new AchievementDef { Id = "boss_cinder_lord", Name = "Snuffed Out", Description = "Defeat the Cinder Lord in the Ashen Citadel.", Category = AchievementCategory.Bosses, Icon = "ach_boss", Points = 50, Stat = "boss.The Cinder Lord", Goal = 1, Title = "the Fireproof" },
            new AchievementDef { Id = "boss_nine", Name = "Crownbreaker", Description = "Defeat nine different bosses.", Category = AchievementCategory.Bosses, Icon = "ach_boss", Points = 100, Stat = "boss", Goal = 9, Title = "Crownbreaker" },
            new AchievementDef { Id = "boss_twelve", Name = "Nothing Left Standing", Description = "Defeat all twelve bosses.", Category = AchievementCategory.Bosses, Icon = "ach_boss", Points = 150, Stat = "boss", Goal = 12, Title = "the Undefeated" },

            // ---- the forge
            new AchievementDef { Id = "salvage_50", Name = "Scrapper", Description = "Salvage 50 pieces of gear at a blacksmith.", Category = AchievementCategory.Professions, Icon = "ach_anvil", Points = 10, Stat = "salvaged", Goal = 50 },
            new AchievementDef { Id = "reforge_first", Name = "Second Opinion", Description = "Reforge a property of an item.", Category = AchievementCategory.Professions, Icon = "ach_anvil", Points = 5, Stat = "reforged", Goal = 1 },
            new AchievementDef { Id = "reforge_25", Name = "Never Satisfied", Description = "Reforge 25 times.", Category = AchievementCategory.Professions, Icon = "ach_anvil", Points = 10, Stat = "reforged", Goal = 25, Title = "the Perfectionist" },

            // ---- world bosses
            new AchievementDef { Id = "worldboss_first", Name = "Giant Slayer", Description = "Help slay a world boss.", Category = AchievementCategory.Bosses, Icon = "ach_boss", Points = 10, Stat = "world_boss", Goal = 1 },
            new AchievementDef { Id = "worldboss_all", Name = "Bane of Giants", Description = "Help slay Old Bramblehide, Hrimgar the Mountain, Gorvash the Dune Reaver and the Pyre Colossus.", Category = AchievementCategory.Bosses, Icon = "ach_boss", Points = 50, Stat = "world_boss", Goal = 4, Title = "the Giantsbane" },

            // ---- town invasions
            new AchievementDef { Id = "defend_first", Name = "Hold the Gate", Description = "Help beat off an invasion of a town.", Category = AchievementCategory.Combat, Icon = "ach_castle", Points = 10, Stat = "defended", Goal = 1 },
            new AchievementDef { Id = "defend_10", Name = "Shield of the Realm", Description = "Help beat off ten invasions.", Category = AchievementCategory.Combat, Icon = "ach_castle", Points = 25, Stat = "defended", Goal = 10, Title = "the Defender" },
            new AchievementDef { Id = "defend_all", Name = "Warden of the Walls", Description = "Defend Hollowmere, Frosthaven, Saltreach and Emberwatch from invasions.", Category = AchievementCategory.Combat, Icon = "ach_castle", Points = 25, Stat = "defended_town", Goal = 4 },

            // ---- dungeons
            new AchievementDef { Id = "dungeon_first", Name = "Into the Dark", Description = "Enter a dungeon.", Category = AchievementCategory.Dungeons, Icon = "ach_dungeon", Points = 5, Stat = "dungeon", Goal = 1 },
            new AchievementDef { Id = "dungeon_all", Name = "Delver", Description = "Enter four different dungeons.", Category = AchievementCategory.Dungeons, Icon = "ach_dungeon", Points = 10, Stat = "dungeon", Goal = 4 },
            new AchievementDef { Id = "dungeon_seven", Name = "Underworld Tourist", Description = "Enter all seven dungeons, the Frozen Barrow, the Sunken Temple and the Ashen Citadel included.", Category = AchievementCategory.Dungeons, Icon = "ach_dungeon", Points = 25, Stat = "dungeon", Goal = 7 },
            new AchievementDef { Id = "dungeon_bottom", Name = "Rock Bottom", Description = "Reach the deepest level of a dungeon.", Category = AchievementCategory.Dungeons, Icon = "ach_stairs", Points = 10, Stat = "bottom", Goal = 1 },
            new AchievementDef { Id = "dungeon_veteran", Name = "Veteran", Description = "Defeat a dungeon boss on Veteran or harder.", Category = AchievementCategory.Dungeons, Icon = "ach_hell", Points = 10, Stat = "hardest_boss", Goal = 1 },
            new AchievementDef { Id = "dungeon_nightmare", Name = "Nightmare Walker", Description = "Defeat a dungeon boss on Nightmare or harder.", Category = AchievementCategory.Dungeons, Icon = "ach_hell", Points = 25, Stat = "hardest_boss", Goal = 2, Title = "Nightmare Walker" },
            new AchievementDef { Id = "dungeon_hell", Name = "Through Hell", Description = "Defeat a dungeon boss on Hell.", Category = AchievementCategory.Dungeons, Icon = "ach_hell", Points = 50, Stat = "hardest_boss", Goal = 3, Title = "the Hellborn" },

            // ---- exploration
            new AchievementDef { Id = "four_seasons", Name = "Four Seasons", Description = "Be in Hollowmere for the Bloom Festival, the Midsummer Fair, the Harvest Festival and Winterfest.", Category = AchievementCategory.Exploration, Icon = "ach_castle", Points = 25, Stat = "season", Goal = 4 },
            new AchievementDef { Id = "explore_25", Name = "Pathfinder", Description = "Explore a quarter of the world.", Category = AchievementCategory.Exploration, Icon = "ach_footprint", Points = 10, Stat = "explored", Goal = 25 },
            new AchievementDef { Id = "explore_75", Name = "Cartographer", Description = "Explore three quarters of the world.", Category = AchievementCategory.Exploration, Icon = "ach_explore", Points = 25, Stat = "explored", Goal = 75, Title = "Cartographer" },
            new AchievementDef { Id = "zones_all", Name = "Wanderer", Description = "Visit Hollowmere, Whisperwood, the Goblin Encampment, the Forsaken Graveyard, Ironvein Quarry and the Crypt of the Lich.", Category = AchievementCategory.Exploration, Icon = "ach_compass", Points = 25, Stat = "zone", Goal = 6, Title = "the Wanderer" },
            new AchievementDef { Id = "zones_outer", Name = "World Walker", Description = "Visit every zone and town, the outer lands included: Frostpeak, the Sunscar Badlands, the Ashen Reach, Pinecrest, Frosthaven, Saltreach and Emberwatch.", Category = AchievementCategory.Exploration, Icon = "ach_explore", Points = 50, Stat = "zone", Goal = 13, Title = "the World Walker" },
            new AchievementDef { Id = "waystones_all", Name = "Attuned", Description = "Attune to the waystones of Pinecrest, Frosthaven, Saltreach and Emberwatch.", Category = AchievementCategory.Exploration, Icon = "ach_compass", Points = 25, Stat = "waystone", Goal = 4 },
            new AchievementDef { Id = "mount_first", Name = "Saddled Up", Description = "Ride a mount.", Category = AchievementCategory.Exploration, Icon = "ach_footprint", Points = 10, Stat = "mounted", Goal = 1 },
            new AchievementDef { Id = "mount_all", Name = "Stable Master", Description = "Ride all three mounts: the Riding Horse, the White Charger and the Frostpeak Stag.", Category = AchievementCategory.Exploration, Icon = "ach_footprint", Points = 25, Stat = "mounted", Goal = 3, Title = "the Rider" },
            new AchievementDef { Id = "waystone_trips", Name = "Frequent Traveller", Description = "Travel by waystone 25 times.", Category = AchievementCategory.Exploration, Icon = "ach_footprint", Points = 10, Stat = "waystone_trips", Goal = 25 },

            // ---- the hero
            new AchievementDef { Id = "level_5", Name = "Adventurer", Description = "Reach level 5.", Category = AchievementCategory.Hero, Icon = "ach_level", Points = 5, Stat = "level", Goal = 5 },
            new AchievementDef { Id = "level_10", Name = "Seasoned", Description = "Reach level 10.", Category = AchievementCategory.Hero, Icon = "ach_level", Points = 10, Stat = "level", Goal = 10 },
            new AchievementDef { Id = "level_20", Name = "Veteran Hero", Description = "Reach level 20.", Category = AchievementCategory.Hero, Icon = "ach_level", Points = 25, Stat = "level", Goal = 20 },
            new AchievementDef { Id = "level_30", Name = "Living Legend", Description = "Reach level 30.", Category = AchievementCategory.Hero, Icon = "ach_level", Points = 50, Stat = "level", Goal = 30, Title = "the Legendary" },
            new AchievementDef { Id = "paragon_1", Name = "Beyond the Peak", Description = "Reach paragon level 1.", Category = AchievementCategory.Hero, Icon = "ach_level", Points = 10, Stat = "paragon", Goal = 1 },
            new AchievementDef { Id = "paragon_25", Name = "Ascendant", Description = "Reach paragon level 25.", Category = AchievementCategory.Hero, Icon = "ach_level", Points = 25, Stat = "paragon", Goal = 25 },
            new AchievementDef { Id = "paragon_100", Name = "Paragon", Description = "Reach paragon level 100.", Category = AchievementCategory.Hero, Icon = "ach_level", Points = 50, Stat = "paragon", Goal = 100, Title = "the Paragon" },
            new AchievementDef { Id = "quests_10", Name = "Helping Hand", Description = "Complete 10 quests.", Category = AchievementCategory.Hero, Icon = "ach_quest", Points = 10, Stat = "quests", Goal = 10 },
            new AchievementDef { Id = "quests_all", Name = "Hero of Hollowmere", Description = "Complete 24 quests.", Category = AchievementCategory.Hero, Icon = "ach_quest", Points = 25, Stat = "quests", Goal = 24, Title = "Hero of Hollowmere" },
            new AchievementDef { Id = "gold_1000", Name = "Well-Off", Description = "Have 1,000 gold.", Category = AchievementCategory.Hero, Icon = "ach_wealth", Points = 10, Stat = "gold", Goal = 1000 },
            new AchievementDef { Id = "gold_25000", Name = "Dragon's Hoard", Description = "Have 25,000 gold.", Category = AchievementCategory.Hero, Icon = "ach_wealth", Points = 25, Stat = "gold", Goal = 25000, Title = "the Wealthy" },
            new AchievementDef { Id = "legendary_1", Name = "Legendary!", Description = "Pick up a legendary item.", Category = AchievementCategory.Hero, Icon = "ach_legendary", Points = 10, Stat = "legendaries", Goal = 1 },
            new AchievementDef { Id = "legendary_10", Name = "Hoarder of Legends", Description = "Pick up 10 legendary items.", Category = AchievementCategory.Hero, Icon = "ach_legendary", Points = 25, Stat = "legendaries", Goal = 10 },
            new AchievementDef { Id = "set_1", Name = "Part of a Set", Description = "Pick up a set item.", Category = AchievementCategory.Hero, Icon = "ach_set", Points = 10, Stat = "sets", Goal = 1 },
            new AchievementDef { Id = "socket_10", Name = "Jeweler", Description = "Socket 10 gems.", Category = AchievementCategory.Hero, Icon = "ach_gem", Points = 10, Stat = "sockets", Goal = 10 },
            new AchievementDef { Id = "deaths_10", Name = "Death's Regular", Description = "Die 10 times. It happens to the best of us.", Category = AchievementCategory.Hero, Icon = "ach_death", Points = 5, Stat = "deaths", Goal = 10, Title = "Death's Regular" },

            // ---- professions
            new AchievementDef { Id = "woodcutting_10", Name = "Lumberjack", Description = "Reach Woodcutting level 10.", Category = AchievementCategory.Professions, Icon = "ach_axe", Points = 10, Stat = "skill.Woodcutting", Goal = 10 },
            new AchievementDef { Id = "woodcutting_25", Name = "Timberlord", Description = "Reach Woodcutting level 25.", Category = AchievementCategory.Professions, Icon = "ach_axe", Points = 25, Stat = "skill.Woodcutting", Goal = 25 },
            new AchievementDef { Id = "mining_10", Name = "Prospector", Description = "Reach Mining level 10.", Category = AchievementCategory.Professions, Icon = "ach_mine", Points = 10, Stat = "skill.Mining", Goal = 10 },
            new AchievementDef { Id = "mining_25", Name = "Deep Delver", Description = "Reach Mining level 25.", Category = AchievementCategory.Professions, Icon = "ach_mine", Points = 25, Stat = "skill.Mining", Goal = 25 },
            new AchievementDef { Id = "fishing_10", Name = "Angler", Description = "Reach Fishing level 10.", Category = AchievementCategory.Professions, Icon = "ach_fish", Points = 10, Stat = "skill.Fishing", Goal = 10 },
            new AchievementDef { Id = "fishing_25", Name = "Master Angler", Description = "Reach Fishing level 25.", Category = AchievementCategory.Professions, Icon = "ach_fish", Points = 25, Stat = "skill.Fishing", Goal = 25 },
            new AchievementDef { Id = "smithing_10", Name = "Apprentice Smith", Description = "Reach Smithing level 10.", Category = AchievementCategory.Professions, Icon = "ach_anvil", Points = 10, Stat = "skill.Smithing", Goal = 10 },
            new AchievementDef { Id = "smithing_25", Name = "Master Smith", Description = "Reach Smithing level 25.", Category = AchievementCategory.Professions, Icon = "ach_anvil", Points = 25, Stat = "skill.Smithing", Goal = 25, Title = "Master Smith" },
            new AchievementDef { Id = "cooking_10", Name = "Camp Cook", Description = "Reach Cooking level 10.", Category = AchievementCategory.Professions, Icon = "ach_cook", Points = 10, Stat = "skill.Cooking", Goal = 10 },
            new AchievementDef { Id = "cooking_25", Name = "Master Chef", Description = "Reach Cooking level 25.", Category = AchievementCategory.Professions, Icon = "ach_cook", Points = 25, Stat = "skill.Cooking", Goal = 25, Title = "Master Chef" },
            new AchievementDef { Id = "crafted_50", Name = "Busy Hands", Description = "Forge or cook 50 times.", Category = AchievementCategory.Professions, Icon = "ach_anvil", Points = 10, Stat = "crafted", Goal = 50 },

            // ---- social
            new AchievementDef { Id = "hooligan", Name = "Hooligan", Description = "Run through a villager's leaf pile, and get what's coming to you.", Category = AchievementCategory.Social, Icon = "ach_footprint", Points = 5, Stat = "raked", Goal = 1, Title = "the Hooligan" },
            new AchievementDef { Id = "party_1", Name = "Better Together", Description = "Join a party.", Category = AchievementCategory.Social, Icon = "ach_party", Points = 5, Stat = "parties", Goal = 1 },
            new AchievementDef { Id = "trade_1", Name = "Fair Deal", Description = "Complete a trade with another player.", Category = AchievementCategory.Social, Icon = "ach_trade", Points = 5, Stat = "trades", Goal = 1 },
            new AchievementDef { Id = "trade_25", Name = "Merchant Prince", Description = "Complete 25 trades.", Category = AchievementCategory.Social, Icon = "ach_trade", Points = 25, Stat = "trades", Goal = 25, Title = "Merchant Prince" },
            new AchievementDef { Id = "emotes_all", Name = "Life of the Party", Description = "Use every emote.", Category = AchievementCategory.Social, Icon = "ach_emote", Points = 10, Stat = "emote", Goal = 13, Title = "Life of the Party" },
            new AchievementDef { Id = "companion_1", Name = "A Loyal Friend", Description = "Hire a companion.", Category = AchievementCategory.Social, Icon = "ach_companion", Points = 5, Stat = "companions", Goal = 1 },
            new AchievementDef { Id = "companion_all", Name = "Beastmaster", Description = "Hire all six companions.", Category = AchievementCategory.Social, Icon = "ach_companion", Points = 25, Stat = "companions", Goal = 6, Title = "Beastmaster" },
        };

        public static AchievementDef Get(string id)
        {
            foreach (var a in All) if (a.Id == id) return a;
            return null;
        }

        public static int TotalPoints
        {
            get { int n = 0; foreach (var a in All) n += a.Points; return n; }
        }

        /// <summary>Which counter a monster kill also counts toward (besides "kills").</summary>
        public static string KillGroup(string monster)
        {
            switch (monster)
            {
                case "Dire Wolf": case "Frost Wolf": return "slain.wolves";
                case "Goblin": case "Goblin Shaman": case "Goblin Warchief": case "Goblin King": return "slain.goblins";
                case "Skeleton": case "Skeleton Archer": case "Zombie": case "Ice Wraith": case "Ash Ghoul": case "Ember Skeleton": case "Ash Wraith": return "slain.undead";
                case "Bandit": case "Bandit Lord": case "Desert Raider": case "Raider Marksman": case "Raider Warlord": return "slain.bandits";
                case "Rock Golem": case "Sand Golem": case "Cinder Golem": return "slain.golems";
                default: return null;
            }
        }
    }

    /// <summary>
    /// A hero's achievement progress: counters ("kills", "skill.Mining", "zone.Whisperwood"...), the achievements earned
    /// (with the date) and the title worn. Saved with the character.
    /// </summary>
    public class AchievementLog
    {
        public readonly Dictionary<string, int> Stats = new Dictionary<string, int>();
        public readonly Dictionary<string, string> Earned = new Dictionary<string, string>(); // id -> yyyy-MM-dd
        /// <summary>The title worn under the hero's name (the id of the achievement that gave it), or null.</summary>
        public string TitleFrom;
        bool quiet;

        public int Points
        {
            get
            {
                int n = 0;
                foreach (var id in Earned.Keys) { var a = AchievementDatabase.Get(id); if (a != null) n += a.Points; }
                return n;
            }
        }

        public string Title => TitleFrom != null && Earned.ContainsKey(TitleFrom) ? AchievementDatabase.Get(TitleFrom)?.Title : null;
        public int Get(string stat) => Stats.TryGetValue(stat, out var v) ? v : 0;
        public bool Has(string id) => Earned.ContainsKey(id);

        /// <summary>Adds to a counter.</summary>
        public void Add(string stat, int n = 1)
        {
            if (string.IsNullOrEmpty(stat) || n <= 0) return;
            Stats[stat] = Get(stat) + n;
            Check(stat);
        }

        /// <summary>Raises a counter to at least <paramref name="value"/> (levels, gold, exploration).</summary>
        public void Max(string stat, int value)
        {
            if (value <= Get(stat)) return;
            Stats[stat] = value;
            Check(stat);
        }

        /// <summary>Counts something once: Once("zone", "Whisperwood") sets "zone.Whisperwood" and adds one to "zone".</summary>
        public void Once(string group, string key)
        {
            string k = group + "." + key;
            if (Get(k) > 0) return;
            Stats[k] = 1;
            Check(k);
            Add(group);
        }

        void Check(string stat)
        {
            foreach (var a in AchievementDatabase.All)
                if (a.Stat == stat && !Earned.ContainsKey(a.Id) && Get(stat) >= a.Goal) Earn(a);
        }

        void Earn(AchievementDef a)
        {
            Earned[a.Id] = System.DateTime.Now.ToString("yyyy-MM-dd");
            if (quiet) return;
            GameUI.AchievementToast(a);
            Sfx.Play2D("quest_done", 0.7f, 1.15f);
            GameUI.Log("Achievement earned: " + a.Name + "  (" + a.Points + " points)" + (a.Title != null ? "  -  title: " + a.Title : ""), AchievementColor);
            NetClient.I?.SendAchievement(a.Id);
            NetClient.I?.SaveSoon();
        }

        public static readonly Color AchievementColor = new Color(1f, 0.8f, 0.3f);

        /// <summary>
        /// Catches up with what a hero did before (or outside) the counters: level, skills, quests, companions, gold.
        /// Earned this way, achievements arrive quietly (one summary line instead of a toast each).
        /// </summary>
        public void CatchUp(Player p, bool announce)
        {
            int before = Earned.Count;
            quiet = !announce;
            Max("level", p.Level);
            Max("paragon", p.Paragon.Level);
            foreach (SkillType s in System.Enum.GetValues(typeof(SkillType))) Max("skill." + s, p.Skills.Level(s));
            Max("quests", p.Quests.Completed.Count);
            Max("companions", p.OwnedCompanions.Count);
            Max("gold", p.Gold);
            quiet = false;
            if (!announce && Earned.Count > before)
                GameUI.Log("You have earned " + (Earned.Count - before) + " achievement" + (Earned.Count - before == 1 ? "" : "s") + " for what you've already done. Press Y to see them.", AchievementColor);
        }

        public string[] SaveStats()
        {
            var list = new List<string>();
            foreach (var kv in Stats) list.Add(kv.Key + "=" + kv.Value);
            return list.ToArray();
        }

        public string[] SaveEarned()
        {
            var list = new List<string>();
            foreach (var kv in Earned) list.Add(kv.Key + "@" + kv.Value);
            return list.ToArray();
        }

        public void Load(string[] stats, string[] earned, string title)
        {
            Stats.Clear();
            Earned.Clear();
            if (stats != null)
                foreach (var s in stats)
                {
                    int eq = (s ?? "").LastIndexOf('=');
                    if (eq > 0 && int.TryParse(s.Substring(eq + 1), out int v)) Stats[s.Substring(0, eq)] = v;
                }
            if (earned != null)
                foreach (var e in earned)
                {
                    var parts = (e ?? "").Split('@');
                    if (AchievementDatabase.Get(parts[0]) != null) Earned[parts[0]] = parts.Length > 1 ? parts[1] : "";
                }
            TitleFrom = !string.IsNullOrEmpty(title) && Earned.ContainsKey(title) ? title : null;
        }
    }
}
