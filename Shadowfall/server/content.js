// Server-side game content: monster stats and where they spawn.
// Monster names must match EnemyDef.Name in the Unity client (Assets/Scripts/Characters/Enemy.cs),
// which only defines how each monster looks.

const MONSTERS = {
  "Dire Wolf":       { hp: 30,   dmg: 5,  speed: 5.6, range: 1.6, cd: 1.1, xp: 18,   aggro: 10, armor: 0 },
  "Goblin":          { hp: 42,   dmg: 7,  speed: 4.6, range: 1.6, cd: 1.2, xp: 26,   aggro: 9,  armor: 0 },
  "Goblin Shaman":   { hp: 32,   dmg: 8,  speed: 4.0, range: 9,   cd: 2.0, xp: 32,   aggro: 11, armor: 0,  ranged: true },
  "Goblin Warchief": { hp: 320,  dmg: 15, speed: 4.6, range: 2.2, cd: 1.4, xp: 350,  aggro: 10, armor: 20, boss: true },
  "Bandit":          { hp: 50,   dmg: 8,  speed: 4.7, range: 1.6, cd: 1.2, xp: 30,   aggro: 9,  armor: 8 },
  "Skeleton":        { hp: 60,   dmg: 10, speed: 4.2, range: 1.6, cd: 1.2, xp: 42,   aggro: 9,  armor: 10 },
  "Skeleton Archer": { hp: 45,   dmg: 10, speed: 4.0, range: 10,  cd: 1.8, xp: 46,   aggro: 12, armor: 0,  ranged: true },
  "Zombie":          { hp: 110,  dmg: 13, speed: 2.6, range: 1.6, cd: 1.6, xp: 52,   aggro: 8,  armor: 0 },
  "Rock Golem":      { hp: 200,  dmg: 20, speed: 3.0, range: 2.2, cd: 2.0, xp: 95,   aggro: 8,  armor: 45 },
  "Crypt Lord":      { hp: 1100, dmg: 26, speed: 3.6, range: 2.4, cd: 1.5, xp: 2400, aggro: 14, armor: 40, boss: true },
  "Lich King":       { hp: 1400, dmg: 26, speed: 3.6, range: 11,  cd: 1.6, xp: 2000, aggro: 14, armor: 35, ranged: true, boss: true },
  // Dungeon bosses
  "Bandit Lord":     { hp: 700,  dmg: 18, speed: 4.8, range: 1.8, cd: 1.1, xp: 900,  aggro: 13, armor: 20, boss: true },
  "Goblin King":     { hp: 950,  dmg: 22, speed: 4.4, range: 2.4, cd: 1.4, xp: 1500, aggro: 13, armor: 30, boss: true },
  "Stone Colossus":  { hp: 1800, dmg: 32, speed: 3.0, range: 2.8, cd: 2.0, xp: 3200, aggro: 13, armor: 60, boss: true },
};

// The world is 288 x 288 tiles. Zones were laid out on the original 160-tile map centred on 80; map() turns those
// design coordinates into world coordinates exactly like WorldGenerator.Map on the client: the town moves with the
// centre (144) and everything farther out is spread 2.25x as far.
const CENTER = 144, INNER = 29, STRETCH = 2.25;
const map = (v) => {
  const d = v - 80, a = Math.abs(d);
  return CENTER + (a <= INNER ? d : Math.sign(d) * (INNER + (a - INNER) * STRETCH));
};

// [x, z, count, minLevel, maxLevel, [types], radius?, respawnSeconds?]   (design coordinates, see map())
const DESIGN_SPAWNERS = [
  // Whisperwood (north)
  [80, 117, 4, 1, 3, ["Dire Wolf"]],
  [100, 120, 5, 2, 4, ["Dire Wolf"]],
  [60, 118, 4, 2, 4, ["Dire Wolf"]],
  [90, 128, 4, 3, 5, ["Dire Wolf"]],
  [44, 126, 5, 3, 5, ["Dire Wolf"]],
  [70, 140, 5, 4, 6, ["Dire Wolf"]],
  [112, 130, 5, 4, 6, ["Dire Wolf", "Bandit"]],
  [96, 146, 6, 5, 7, ["Dire Wolf", "Bandit"]],
  [130, 145, 5, 5, 7, ["Dire Wolf", "Bandit"]],
  [40, 148, 5, 6, 8, ["Bandit"]],
  // Goblin Encampment (east)
  [117, 80, 4, 3, 5, ["Goblin"]],
  [124, 92, 6, 4, 7, ["Goblin", "Goblin", "Goblin Shaman"]],
  [124, 68, 6, 4, 7, ["Goblin", "Goblin", "Goblin Shaman"]],
  [138, 98, 6, 6, 8, ["Goblin", "Goblin Shaman"]],          // the war camp
  [150, 110, 5, 7, 9, ["Goblin", "Goblin", "Goblin Shaman"]],
  [140, 110, 5, 6, 8, ["Bandit"]],
  [140, 45, 5, 6, 9, ["Goblin", "Bandit"]],
  [150, 64, 5, 7, 10, ["Goblin", "Bandit"]],
  [130, 80, 1, 10, 10, ["Goblin Warchief"], 1, 90],
  [146, 104, 1, 13, 13, ["Goblin Warchief"], 1, 120],      // the war camp's chief
  // Forsaken Graveyard (south)
  [62, 45, 5, 6, 8, ["Skeleton", "Zombie"]],
  [98, 45, 5, 6, 9, ["Skeleton", "Zombie"]],
  [80, 34, 6, 7, 10, ["Skeleton", "Skeleton Archer"]],
  [110, 25, 5, 9, 11, ["Zombie", "Skeleton Archer"]],
  [50, 30, 5, 8, 10, ["Zombie", "Skeleton"]],
  [120, 40, 5, 8, 11, ["Skeleton", "Zombie"]],
  [96, 18, 5, 10, 12, ["Skeleton Archer", "Zombie"]],
  // Ironvein Quarry (west)
  [43, 64, 4, 3, 5, ["Bandit"]],
  [36, 82, 4, 8, 11, ["Rock Golem"]],
  [24, 60, 4, 10, 13, ["Rock Golem"]],
  [20, 105, 4, 12, 15, ["Rock Golem"]],
  [44, 96, 5, 6, 9, ["Bandit", "Rock Golem"]],
  [28, 40, 4, 11, 14, ["Rock Golem"]],
  [14, 82, 5, 13, 16, ["Rock Golem"]],
];

// World coordinates. The crypt is a fixed building moved by (+64, +5), so its spawners are listed directly.
const SPAWNERS = [
  ...DESIGN_SPAWNERS.map(([x, z, ...rest]) => [map(x), map(z), ...rest]),
  [138, 19, 4, 11, 13, ["Skeleton", "Skeleton Archer"]],
  [151, 19, 4, 11, 13, ["Skeleton", "Skeleton Archer"]],
  [144.5, 14.5, 1, 16, 16, ["Lich King"], 0.5, 180],
];

// Dungeons: each has an entrance in the overworld (design coordinates, resolved on the world map to the nearest
// clear 5x5 spot, the same way the client does it) and is generated fresh for every party. Monster types are per depth.
// style: "rooms" (halls and corridors) or "caves" (natural caverns). Must match DungeonDef.All on the client (ids, order).
const DUNGEONS = [
  { id: "catacombs", name: "The Catacombs", at: [104.5, 27.5], depths: 3, style: "rooms", minLevel: 6, boss: "Crypt Lord",
    types: [["Skeleton", "Zombie"], ["Skeleton", "Skeleton Archer", "Zombie"], ["Skeleton", "Skeleton Archer", "Zombie", "Skeleton"]] },
  { id: "hideout", name: "Bandit Hideout", at: [104, 132], depths: 2, style: "rooms", minLevel: 3, boss: "Bandit Lord",
    types: [["Bandit", "Bandit", "Dire Wolf"], ["Bandit", "Dire Wolf"]] },
  { id: "warrens", name: "Goblin Warrens", at: [138, 74], depths: 3, style: "caves", minLevel: 4, boss: "Goblin King",
    types: [["Goblin", "Goblin", "Goblin Shaman"], ["Goblin", "Goblin Shaman"], ["Goblin", "Goblin Shaman", "Goblin Shaman"]] },
  { id: "mine", name: "Ironvein Deep", at: [26, 96], depths: 3, style: "caves", minLevel: 10, boss: "Stone Colossus",
    types: [["Rock Golem", "Skeleton"], ["Rock Golem", "Zombie", "Skeleton"], ["Rock Golem", "Skeleton Archer", "Zombie"]] },
];

// Global balance: every monster's base health and damage are multiplied by these (the game was too easy).
const BALANCE = { hp: 1.5, dmg: 1.6, hpPerLevel: 0.32, dmgPerLevel: 0.2 };

// Dungeon difficulty, picked by whoever opens the dungeon for the party. Monsters get tougher and more often
// elite; they give more XP and better loot (loot = extra rarity bonus for the client's loot rolls).
const DIFFICULTIES = [
  { name: "Normal",    hp: 1,   dmg: 1,   elite: 0,    xp: 1,   loot: 0 },
  { name: "Veteran",   hp: 1.7, dmg: 1.4, elite: 0.08, xp: 1.5, loot: 0.2 },
  { name: "Nightmare", hp: 2.8, dmg: 1.9, elite: 0.18, xp: 2.2, loot: 0.45 },
  { name: "Hell",      hp: 4.5, dmg: 2.6, elite: 0.35, xp: 3.2, loot: 0.8 },
];

// Safe zone: monsters never follow players inside the village walls (cells 116..172).
const TOWN = { x0: 116, z0: 116, x1: 173, z1: 173 };
// Where new heroes start and dead ones wake up (the plaza, matches WorldGenerator.SpawnPoint).
const SPAWN = { x: 144.5, z: 141.5 };

// Emote ids players may broadcast (Characters/Emotes.cs EmoteDef.All).
const EMOTES = ["wave", "dance", "bow", "cheer", "clap", "point", "flex", "sit", "sleep", "jump", "kick", "shadowbox", "guard"];

module.exports = { MONSTERS, SPAWNERS, TOWN, SPAWN, DUNGEONS, BALANCE, DIFFICULTIES, EMOTES, map };
