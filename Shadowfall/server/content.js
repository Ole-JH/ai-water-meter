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
};

// [x, z, count, minLevel, maxLevel, [types], radius?, respawnSeconds?]
const SPAWNERS = [
  // Whisperwood (north)
  [80, 117, 4, 1, 3, ["Dire Wolf"]],
  [100, 120, 5, 2, 4, ["Dire Wolf"]],
  [60, 118, 4, 2, 4, ["Dire Wolf"]],
  [70, 140, 5, 4, 6, ["Dire Wolf"]],
  [130, 145, 5, 5, 7, ["Dire Wolf", "Bandit"]],
  // Goblin Encampment (east)
  [117, 80, 4, 3, 5, ["Goblin"]],
  [124, 92, 6, 4, 7, ["Goblin", "Goblin", "Goblin Shaman"]],
  [124, 68, 6, 4, 7, ["Goblin", "Goblin", "Goblin Shaman"]],
  [140, 110, 5, 6, 8, ["Bandit"]],
  [140, 45, 5, 6, 9, ["Goblin", "Bandit"]],
  [130, 80, 1, 10, 10, ["Goblin Warchief"], 1, 90],
  // Forsaken Graveyard (south)
  [62, 45, 5, 6, 8, ["Skeleton", "Zombie"]],
  [98, 45, 5, 6, 9, ["Skeleton", "Zombie"]],
  [80, 34, 6, 7, 10, ["Skeleton", "Skeleton Archer"]],
  [110, 25, 5, 9, 11, ["Zombie", "Skeleton Archer"]],
  // Crypt of the Lich
  [74, 14, 4, 11, 13, ["Skeleton", "Skeleton Archer"]],
  [87, 14, 4, 11, 13, ["Skeleton", "Skeleton Archer"]],
  [80.5, 9.5, 1, 16, 16, ["Lich King"], 0.5, 180],
  // Ironvein Quarry (west)
  [43, 64, 4, 3, 5, ["Bandit"]],
  [36, 82, 4, 8, 11, ["Rock Golem"]],
  [24, 60, 4, 10, 13, ["Rock Golem"]],
  [20, 105, 4, 12, 15, ["Rock Golem"]],
];

// Safe zone: monsters never follow players inside the village walls.
const TOWN = { x0: 52, z0: 52, x1: 109, z1: 109 };

module.exports = { MONSTERS, SPAWNERS, TOWN };
