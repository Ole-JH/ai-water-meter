// Items, loot and gold, owned by the server.
//
// The game client only displays items; every item is minted here (loot, vendors, crafting, quest rewards, gathering)
// and every change (equip, use, sell, socket, trade...) is validated here. This is a port of the C# generator in
// Assets/Scripts/Items (ItemDatabase.cs, ItemPowers.cs, VendorStock.cs) and the loot rolls in Characters/Enemy.cs;
// items use the same JSON shape as the C# Item class (Unity's JsonUtility: enums as numbers, colours as {r,g,b,a}).
// Quests, companions and recipes come from gamedata.json, extracted from the C# sources (task gamedata).
"use strict";
const fs = require("fs");
const path = require("path");

const GAMEDATA = JSON.parse(fs.readFileSync(path.join(__dirname, "gamedata.json"), "utf8"));

const Rarity = { Common: 0, Magic: 1, Rare: 2, Legendary: 3, Set: 4 };
const Kind = { Equipment: 0, Consumable: 1, Material: 2, Gem: 3 };
const Slot = { None: 0, Weapon: 1, Helm: 2, Chest: 3, Gloves: 4, Legs: 5, Boots: 6, Ring: 7, Amulet: 8 };
const STATS = ["Strength", "Dexterity", "Intelligence", "Vitality", "Health", "Mana", "Armor",
  "CritChance", "AttackSpeed", "LifeOnHit", "MoveSpeed", "HealthRegen", "ManaRegen"];
const Stat = Object.fromEntries(STATS.map((n, i) => [n, i]));

const BAG_SIZE = 40, STASH_SIZE = 40;

// ------------------------------------------------------------------ randomness (Unity's Random.Range semantics)

const rnd = Math.random;
const rangeF = (a, b) => a + rnd() * (b - a);            // floats: inclusive
const rangeI = (a, b) => a + Math.floor(rnd() * (b - a)); // ints: max exclusive
const pick = (arr) => arr[rangeI(0, arr.length)];
const round = (v) => Math.round(v);
const col = (r, g, b, a = 1) => ({ r, g, b, a });

/** A complete item with every field the client's Item class has. */
function item(fields) {
  return {
    Name: "", BaseType: "", Kind: Kind.Equipment, Slot: Slot.None, Rarity: Rarity.Common, ItemLevel: 1, RequiredLevel: 1,
    MinDamage: 0, MaxDamage: 0, AttacksPerSecond: 0, Armor: 0, Mods: [], Value: 0, Count: 1, MaxStack: 1,
    IconColor: col(0.5, 0.5, 0.5), Icon: "?", HealAmount: 0, ManaAmount: 0, Flavor: "", Power: "", Set: "", Sockets: 0, Gems: [],
    ...fields,
  };
}

// ------------------------------------------------------------------ consumables & materials (ItemDatabase.cs)

const healthPotion = () => item({ Name: "Health Potion", Kind: Kind.Consumable, MaxStack: 20, Value: 8, HealAmount: 80,
  IconColor: col(0.85, 0.1, 0.1), Icon: "HP", Flavor: "Tastes faintly of cherries and iron." });
const manaPotion = () => item({ Name: "Mana Potion", Kind: Kind.Consumable, MaxStack: 20, Value: 8, ManaAmount: 60,
  IconColor: col(0.15, 0.3, 0.95), Icon: "MP", Flavor: "It glows. That's probably fine." });

const MATERIALS = {
  "Oak Logs": [col(0.55, 0.38, 0.2), "Lg", 4], "Willow Logs": [col(0.6, 0.55, 0.3), "Lg", 9], "Yew Logs": [col(0.4, 0.22, 0.12), "Lg", 20],
  "Copper Ore": [col(0.85, 0.5, 0.25), "Or", 5], "Iron Ore": [col(0.55, 0.35, 0.3), "Or", 11], "Mithril Ore": [col(0.35, 0.5, 0.95), "Or", 24],
  // From salvaging gear at a blacksmith (see salvageYield), spent on reforging
  "Scrap Iron": [col(0.55, 0.55, 0.6), "Sc", 2], "Arcane Dust": [col(0.55, 0.75, 1), "Du", 6],
  "Veiled Crystal": [col(1, 0.85, 0.3), "Cr", 18], "Forgotten Soul": [col(1, 0.5, 0.15), "So", 60],
  "Raw Trout": [col(0.6, 0.65, 0.7), "Fi", 3], "Raw Salmon": [col(0.95, 0.55, 0.45), "Fi", 8], "Burnt Fish": [col(0.15, 0.12, 0.1), "Fi", 1],
};
function material(name) {
  const m = MATERIALS[name] || [col(0.5, 0.5, 0.5), "?", 1];
  return item({ Name: name, Kind: Kind.Material, MaxStack: 50, Value: m[2], IconColor: m[0], Icon: m[1] });
}
function food(name) {
  const salmon = name.includes("Salmon");
  return item({ Name: name, Kind: Kind.Consumable, MaxStack: 50, Value: salmon ? 14 : 6, HealAmount: salmon ? 160 : 70,
    IconColor: salmon ? col(0.9, 0.45, 0.3) : col(0.75, 0.6, 0.4), Icon: "Fd", Flavor: salmon ? "Perfectly flaky." : "Smells like home." });
}
function provision(name) {
  switch (name) {
    case "Bread": return item({ Name: name, Kind: Kind.Consumable, MaxStack: 50, Value: 2, HealAmount: 45, IconColor: col(0.8, 0.6, 0.35), Icon: "Fd", Flavor: "Baked this morning. Probably." });
    case "Hearty Stew": return item({ Name: name, Kind: Kind.Consumable, MaxStack: 20, Value: 12, HealAmount: 240, IconColor: col(0.6, 0.35, 0.2), Icon: "Fd", Flavor: "Rosie won't say what's in it." });
    case "Mulled Wine": return item({ Name: name, Kind: Kind.Consumable, MaxStack: 20, Value: 6, ManaAmount: 50, IconColor: col(0.55, 0.1, 0.25), Icon: "Dr", Flavor: "Warms the soul and the spellbook." });
    default: return null;
  }
}
function byName(name) {
  const p = provision(name);
  if (p) return p;
  if (name === "Health Potion") return healthPotion();
  if (name === "Mana Potion") return manaPotion();
  if (name.startsWith("Cooked")) return food(name);
  const g = parseGem(name);
  if (g) return gem(g.type, g.tier);
  return material(name);
}

const starterWeapon = () => item({ Name: "Worn Shortsword", BaseType: "Sword", Slot: Slot.Weapon, MinDamage: 4, MaxDamage: 8,
  AttacksPerSecond: 1.3, Value: 3, IconColor: col(0.6, 0.6, 0.65), Icon: "Sw", Flavor: "It has seen better days. So have you." });
const starterChest = () => item({ Name: "Padded Tunic", BaseType: "Tunic", Slot: Slot.Chest, Armor: 6, Value: 2, IconColor: col(0.55, 0.45, 0.35), Icon: "Ch" });

// ------------------------------------------------------------------ random equipment (ItemDatabase.cs)

const SLOTS = [Slot.Weapon, Slot.Weapon, Slot.Helm, Slot.Chest, Slot.Gloves, Slot.Legs, Slot.Boots, Slot.Ring, Slot.Amulet];
const SET_SLOTS = [Slot.Helm, Slot.Chest, Slot.Gloves, Slot.Boots];
const WEAPON_BASES = [
  ["Short Sword", "Hand Axe", "Club", "Dagger"], ["Broadsword", "Battle Axe", "Flanged Mace", "Kris"],
  ["Bastard Sword", "War Axe", "Morning Star", "Stiletto"], ["Runeblade", "Executioner Axe", "Warhammer", "Soulreaver Dirk"],
];
const ARMOR_BASES = {
  [Slot.Helm]: ["Leather Cap", "Iron Helm", "Great Helm", "Runic Crown"], [Slot.Chest]: ["Leather Armor", "Chainmail", "Plate Mail", "Runic Plate"],
  [Slot.Gloves]: ["Leather Gloves", "Chain Gloves", "Gauntlets", "Runic Grips"], [Slot.Legs]: ["Leather Pants", "Chain Leggings", "Plate Greaves", "Runic Legplates"],
  [Slot.Boots]: ["Leather Boots", "Chain Boots", "Plate Sabatons", "Runic Treads"], [Slot.Ring]: ["Copper Ring", "Silver Ring", "Gold Ring", "Starmetal Ring"],
  [Slot.Amulet]: ["Bone Charm", "Silver Amulet", "Gold Amulet", "Starmetal Amulet"],
};
const PREFIXES = ["Mighty", "Nimble", "Arcane", "Sturdy", "Vital", "Mystic", "Reinforced", "Keen", "Swift", "Vampiric", "Fleet", "Rejuvenating", "Focused"];
const SUFFIXES = ["of the Bear", "of the Fox", "of the Owl", "of the Ox", "of Life", "of the Magi", "of Warding", "of Precision", "of Haste",
  "of the Leech", "of the Wind", "of the Troll", "of Clarity"];
const RARE_A = ["Grim", "Doom", "Storm", "Blood", "Dread", "Soul", "Shadow", "Bone", "Ash", "Raven", "Wraith", "Gloom"];
const RARE_B = ["Bite", "Song", "Ward", "Grasp", "Fang", "Veil", "Crown", "Reaver", "Shell", "Mark", "Coil", "Spire"];
const LEGENDARY_NAMES = {
  [Slot.Weapon]: ["Dawnbreaker", "The Grave Whisper", "Emberheart", "Kingsbane"], [Slot.Helm]: ["Crown of the Fallen", "Visage of Night"],
  [Slot.Chest]: ["Aegis of Eternity", "Heart of the Mountain"], [Slot.Gloves]: ["Gauntlets of the Titan", "Hands of Ruin"],
  [Slot.Legs]: ["Legplates of the Abyss", "Stormstriders"], [Slot.Boots]: ["Windwalkers", "Boots of the Lost Road"],
  [Slot.Ring]: ["Band of Endless Night", "Ouroboros"], [Slot.Amulet]: ["Eye of the Storm", "Tear of the Moon"],
};
const LEGENDARY_FLAVOR = ["Forged when the world was young.", "It whispers your name at night.",
  "Many have carried it. None have kept it.", "The light bends around it."];

function rollRarity(bonus) {
  const r = rnd();
  if (r < 0.01 + bonus * 0.04) return Rarity.Legendary;
  if (r < 0.02 + bonus * 0.08) return Rarity.Set;
  if (r < 0.10 + bonus * 0.25) return Rarity.Rare;
  if (r < 0.40 + bonus * 0.3) return Rarity.Magic;
  return Rarity.Common;
}

function rollStat(s, ilvl) {
  switch (s) {
    case Stat.Strength: case Stat.Dexterity: case Stat.Intelligence: case Stat.Vitality: return rangeF(1, 3 + ilvl * 0.5);
    case Stat.Health: return rangeF(5, 12 + ilvl * 3);
    case Stat.Mana: return rangeF(5, 10 + ilvl * 2);
    case Stat.Armor: return rangeF(2, 5 + ilvl);
    case Stat.CritChance: return rangeF(1, 3 + ilvl / 8);
    case Stat.AttackSpeed: return rangeF(3, 8 + ilvl / 4);
    case Stat.LifeOnHit: return rangeF(1, 2 + ilvl / 4);
    case Stat.MoveSpeed: return rangeF(3, 9);
    case Stat.HealthRegen: return rangeF(1, 2 + ilvl / 5);
    case Stat.ManaRegen: return rangeF(1, 2 + ilvl / 6);
    default: return 1;
  }
}

const SLOT_ICON = { [Slot.Helm]: "He", [Slot.Chest]: "Ch", [Slot.Gloves]: "Gl", [Slot.Legs]: "Lg", [Slot.Boots]: "Bt", [Slot.Ring]: "Rg", [Slot.Amulet]: "Am" };
function iconTint(slot, tier) {
  if (slot === Slot.Ring || slot === Slot.Amulet)
    return [col(0.8, 0.5, 0.3), col(0.8, 0.8, 0.85), col(0.95, 0.8, 0.3), col(0.5, 0.7, 1)][tier];
  return [col(0.55, 0.42, 0.3), col(0.6, 0.62, 0.65), col(0.78, 0.8, 0.85), col(0.45, 0.6, 0.9)][tier];
}

/** heroClass ("Knight", "Mage"...) biases legendary powers and set pieces toward the hero's class. */
function randomEquipment(itemLevel, rarityBonus = 0, forced = null, forcedSlot = null, heroClass = null) {
  itemLevel = Math.max(1, Math.round(itemLevel));
  let slot = forcedSlot != null ? forcedSlot : pick(SLOTS);
  let rarity = forced != null ? forced : rollRarity(rarityBonus);
  if (rarity === Rarity.Set && forcedSlot == null) slot = pick(SET_SLOTS);
  if (rarity === Rarity.Set && !SET_SLOTS.includes(slot)) rarity = Rarity.Legendary;
  const tier = Math.max(0, Math.min(3, Math.floor(itemLevel / 6)));
  const it = item({ Kind: Kind.Equipment, Slot: slot, Rarity: rarity, ItemLevel: itemLevel, RequiredLevel: Math.max(1, itemLevel - 2) });
  const rarityMul = rarity >= Rarity.Legendary ? 1.35 : rarity === Rarity.Rare ? 1.15 : 1;
  if (slot === Slot.Weapon) {
    const type = rangeI(0, 4);
    it.BaseType = WEAPON_BASES[tier][type];
    const aps = [1.3, 1.1, 1.0, 1.6][type], dmgMul = [1, 1.2, 1.35, 0.75][type];
    it.MinDamage = Math.max(1, round((3 + itemLevel * 1.3) * dmgMul * rarityMul * rangeF(0.85, 1.15)));
    it.MaxDamage = round(it.MinDamage * rangeF(1.5, 2.0)) + 2;
    it.AttacksPerSecond = aps;
    it.Icon = ["Sw", "Ax", "Mc", "Dg"][type];
  } else {
    it.BaseType = ARMOR_BASES[slot][tier];
    const slotMul = slot === Slot.Chest ? 1 : slot === Slot.Legs ? 0.8 : slot === Slot.Helm ? 0.6 : slot === Slot.Ring || slot === Slot.Amulet ? 0 : 0.4;
    it.Armor = round((4 + itemLevel * 1.6) * slotMul * rarityMul * rangeF(0.85, 1.15));
    it.Icon = SLOT_ICON[slot] || "?";
  }
  let affixes = rarity === Rarity.Common ? 0 : rarity === Rarity.Magic ? rangeI(1, 3) : rarity === Rarity.Rare ? rangeI(3, 5) : rarity === Rarity.Set ? 4 : 5;
  if ((slot === Slot.Ring || slot === Slot.Amulet) && affixes < 5) affixes++;
  const pool = STATS.map((_, i) => i);
  for (let i = 0; i < affixes && pool.length; i++) {
    const stat = pool.splice(rangeI(0, pool.length), 1)[0];
    it.Mods.push({ Stat: stat, Value: Math.max(1, round(rollStat(stat, itemLevel) * rarityMul)) });
  }
  switch (rarity) {
    case Rarity.Common: it.Name = it.BaseType; break;
    case Rarity.Magic: it.Name = `${PREFIXES[it.Mods[0].Stat]} ${it.BaseType}${it.Mods.length > 1 ? " " + SUFFIXES[it.Mods[1].Stat] : ""}`; break;
    case Rarity.Rare: it.Name = `${pick(RARE_A)} ${pick(RARE_B)}`; break;
    case Rarity.Legendary:
      it.Name = pick(LEGENDARY_NAMES[slot]);
      it.Flavor = pick(LEGENDARY_FLAVOR);
      giveLegendaryPower(it, heroClass);
      break;
    case Rarity.Set: makeSetPiece(it, heroClass); break;
  }
  if (slot === Slot.Weapon || slot === Slot.Helm || slot === Slot.Chest || slot === Slot.Legs) {
    const r = rnd(), b = rarity >= Rarity.Rare ? 0.15 : 0;
    it.Sockets = r < 0.06 + b ? 2 : r < 0.3 + b ? 1 : 0;
  }
  const rarityValue = rarity === Rarity.Common ? 1 : rarity === Rarity.Magic ? 3 : rarity === Rarity.Rare ? 7 : 20;
  it.Value = Math.max(1, (3 + itemLevel * 2) * rarityValue);
  it.IconColor = iconTint(slot, tier);
  return it;
}

// ------------------------------------------------------------------ salvage & reforge (the blacksmiths; Forge.cs mirrors the numbers)

/** What salvaging a piece of gear gives: [[material, count]]. Socketed gems come back too (server.js). */
function salvageYield(it) {
  const r = it.Rarity, big = it.ItemLevel >= 12 ? 1 : 0;
  if (r === Rarity.Common) return [["Scrap Iron", 1 + big + (rnd() < 0.5 ? 1 : 0)]];
  if (r === Rarity.Magic) return [["Arcane Dust", 1 + (rnd() < 0.5 ? 1 : 0)], ["Scrap Iron", 1]];
  if (r === Rarity.Rare) return [["Veiled Crystal", 1], ["Arcane Dust", 1 + big + (rnd() < 0.5 ? 1 : 0)]];
  return [["Forgotten Soul", 1], ["Veiled Crystal", 1 + big]]; // legendary and set
}

/** What reforging one of an item's affixes costs: materials and gold. Null: it can't be reforged. */
function reforgeCost(it) {
  const r = it.Rarity;
  if (!it.Mods || !it.Mods.length || r === Rarity.Common) return null;
  const gold = Math.max(25, Math.round(it.Value * 1.5));
  if (r === Rarity.Magic) return { mats: [["Arcane Dust", 3]], gold };
  if (r === Rarity.Rare) return { mats: [["Veiled Crystal", 2], ["Arcane Dust", 4]], gold };
  return { mats: [["Forgotten Soul", 1], ["Veiled Crystal", 2]], gold };
}

/**
 * Rerolls affix `idx` of an item: a new stat (any the item doesn't have on its other affixes) with a fresh value for its
 * level. Like Diablo's enchanting: once an item has been reforged, only that same affix can be reforged again
 * (Reforged = index + 1). Magic items get the name that fits their new affixes.
 */
function reforge(it, idx) {
  const others = new Set(it.Mods.filter((_, i) => i !== idx).map((m) => m.Stat));
  const pool = STATS.map((_, i) => i).filter((st) => !others.has(st));
  const stat = pick(pool);
  const rarityMul = it.Rarity >= Rarity.Legendary ? 1.35 : it.Rarity === Rarity.Rare ? 1.15 : 1;
  it.Mods[idx] = { Stat: stat, Value: Math.max(1, round(rollStat(stat, it.ItemLevel) * rarityMul)) };
  it.Reforged = idx + 1;
  if (it.Rarity === Rarity.Magic && it.BaseType)
    it.Name = `${PREFIXES[it.Mods[0].Stat]} ${it.BaseType}${it.Mods.length > 1 ? " " + SUFFIXES[it.Mods[1].Stat] : ""}`;
  return it.Mods[idx];
}

// ------------------------------------------------------------------ legendary powers, sets, gems (ItemPowers.cs)

const POWERS = [
  { id: "fireball_split", cls: "Mage" }, { id: "whirl_slow", cls: "Barbarian" }, { id: "multishot_plus", cls: "Rogue" }, { id: "holy_stun", cls: "Knight" },
  { id: "explode_kill" }, { id: "heal_kill" }, { id: "chain_proc" }, { id: "elitebane" },
];
const SETS = [
  { id: "lightbringer", cls: "Knight", pieces: { [Slot.Helm]: "Lightbringer's Visage", [Slot.Chest]: "Lightbringer's Cuirass", [Slot.Gloves]: "Lightbringer's Gauntlets", [Slot.Boots]: "Lightbringer's Greaves" } },
  { id: "ancients", cls: "Barbarian", pieces: { [Slot.Helm]: "Ancient's Horned Helm", [Slot.Chest]: "Ancient's Hauberk", [Slot.Gloves]: "Ancient's Fists", [Slot.Boots]: "Ancient's Stride" } },
  { id: "tempest", cls: "Mage", pieces: { [Slot.Helm]: "Tempest Cowl", [Slot.Chest]: "Tempest Robes", [Slot.Gloves]: "Tempest Wraps", [Slot.Boots]: "Tempest Slippers" } },
  { id: "nightstalker", cls: "Rogue", pieces: { [Slot.Helm]: "Nightstalker's Hood", [Slot.Chest]: "Nightstalker's Jerkin", [Slot.Gloves]: "Nightstalker's Grips", [Slot.Boots]: "Nightstalker's Treads" } },
];
function giveLegendaryPower(it, heroClass) {
  const pool = POWERS.filter((p) => !p.cls || p.cls === heroClass);
  it.Power = pick(pool).id;
}
function makeSetPiece(it, heroClass) {
  let set = rnd() < 0.75 ? SETS.find((s) => s.cls === heroClass) : null;
  if (!set) set = pick(SETS);
  const name = set.pieces[it.Slot];
  if (!name) return false;
  it.Set = set.id;
  it.Name = name;
  it.Rarity = Rarity.Set;
  return true;
}

const GEM_TYPES = ["Ruby", "Emerald", "Sapphire", "Topaz", "Diamond"];
const GEM_TIERS = ["Chipped", "Flawless", "Perfect"];
const GEM_TIER_VALUE = [3, 6, 10];
const GEM_STAT = { Ruby: Stat.Strength, Emerald: Stat.Dexterity, Sapphire: Stat.Intelligence, Topaz: Stat.Vitality, Diamond: Stat.Health };
const GEM_COLOR = { Ruby: col(1, 0.25, 0.25), Emerald: col(0.3, 1, 0.4), Sapphire: col(0.35, 0.55, 1), Topaz: col(1, 0.8, 0.25), Diamond: col(0.95, 0.95, 1) };
const STAT_TEXT = ["Strength", "Dexterity", "Intelligence", "Vitality", "Maximum Life"];
function parseGem(name) {
  const parts = String(name || "").split(" ");
  if (parts.length !== 2) return null;
  const tier = GEM_TIERS.indexOf(parts[0]);
  return tier >= 0 && GEM_TYPES.includes(parts[1]) ? { type: parts[1], tier } : null;
}
function gem(type, tier) {
  tier = Math.max(0, Math.min(2, tier));
  const v = GEM_TIER_VALUE[tier] * (type === "Diamond" ? 4 : 1);
  return item({ Name: `${GEM_TIERS[tier]} ${type}`, BaseType: "Gem", Kind: Kind.Gem, MaxStack: 50, Value: [15, 60, 220][tier],
    IconColor: GEM_COLOR[type], Icon: "gem", Flavor: `Socket into an item: +${v} ${STAT_TEXT[GEM_STAT[type]]}.` });
}
const randomGem = (monsterLevel) => gem(pick(GEM_TYPES), monsterLevel >= 14 && rnd() < 0.3 ? 1 : 0);

// ------------------------------------------------------------------ loot (Enemy.cs RollLoot / RollEliteLoot, Dungeon.cs chests)

/** Personal loot for one player: [{ item } | { gold }]. */
/**
 * Where loot comes from decides how good it can be. Ordinary monsters on the surface drop little, and nothing better
 * than magic; the better tiers live in dungeons and the Crypt of the Lich, with elites, in treasure chests and above all
 * with bosses. Harder dungeon difficulties (bonus 0.25 Veteran .. 0.8 Hell) push every roll up.
 *   source: "surface" | "deep" (a dungeon or the Crypt)
 */
const LOOT = {
  // chance of an item from an ordinary monster, and the best rarity it can be
  surface: { item: 0.07, gold: 0.35, cap: Rarity.Magic, potion: 0.08, gem: 0.012 },
  deep: { item: 0.13, gold: 0.5, cap: Rarity.Legendary, potion: 0.1, gem: 0.025 },
};

/** Rarity for a drop: each better tier much rarer than the last; `cap` is the best it may be. */
function lootRarity(bonus, cap, floor = Rarity.Common) {
  const r = rnd() / (1 + bonus);
  let rarity = r < 0.003 ? Rarity.Legendary : r < 0.008 ? Rarity.Set : r < 0.05 ? Rarity.Rare : r < 0.28 ? Rarity.Magic : Rarity.Common;
  if (rarity > cap) rarity = cap;
  return rarity < floor ? floor : rarity;
}

function rollLoot(monster, level, bonus, heroClass, elite, source = "surface") {
  const out = [];
  const t = LOOT[source] || LOOT.surface;
  const eq = (lvl, b, r = null) => out.push({ item: randomEquipment(lvl, b, r, null, heroClass) });
  const boss = !!monster.boss;
  if (boss) {
    // Bosses: plenty of gold, a guaranteed rare, more gear, and the best chance at legendaries and set pieces.
    out.push({ gold: Math.max(1, round(level * rangeF(2, 6) * 8 * (1 + bonus))) });
    let n = monster.name === "Lich King" ? 3 : 2;
    if (bonus > 0) n++;
    eq(level + 1, 1 + bonus, Rarity.Rare);
    for (let i = 1; i < n; i++) eq(level + 1, 1 + bonus, lootRarity(bonus * 2 + 1, Rarity.Legendary, Rarity.Magic));
    if (rnd() < 0.12 + bonus * 0.3) eq(level + 2, 1, Rarity.Legendary);
    if (rnd() < 0.18 + bonus * 0.3) eq(level + 1, 1, Rarity.Set);
    out.push({ item: randomGem(level + 6) });
    return out;
  }
  if (elite) {
    // Elites: gold and one or two pieces; only deep down can they be set or legendary.
    out.push({ gold: Math.max(5, round(level * rangeF(6, 12) * (1 + bonus))) });
    const n = 1 + (rnd() < 0.35 + bonus * 0.5 ? 1 : 0);
    const cap = source === "deep" ? Rarity.Legendary : Rarity.Rare;
    for (let i = 0; i < n; i++) eq(level, 0.5 + bonus, lootRarity(bonus + (source === "deep" ? 1.5 : 0.6), cap, Rarity.Magic));
    if (rnd() < 0.3) { const hp = healthPotion(); hp.Count = 2; out.push({ item: hp }); }
    if (rnd() < 0.2) out.push({ item: randomGem(level) });
    return out;
  }
  if (rnd() < t.gold) out.push({ gold: Math.max(1, round(level * rangeF(1.5, 4.5))) });
  if (rnd() < t.item * (1 + bonus)) eq(level, bonus, lootRarity(bonus, t.cap));
  if (rnd() < t.potion) out.push({ item: rnd() < 0.6 ? healthPotion() : manaPotion() });
  if (rnd() < t.gem) out.push({ item: randomGem(level) });
  return out;
}

/** A dungeon treasure chest: gold and a piece of gear that is at least magic, often rare, now and then better. */
function rollChest(level, heroClass, bonus = 0) {
  const out = [{ gold: Math.max(10, round(level * rangeF(6, 12) * (1 + bonus))) }];
  out.push({ item: randomEquipment(level, 0.4 + bonus, lootRarity(bonus + 2.5, Rarity.Legendary, Rarity.Magic), null, heroClass) });
  if (rnd() < 0.25 + bonus * 0.3) out.push({ item: randomEquipment(level, 0.4, lootRarity(bonus + 1, Rarity.Rare, Rarity.Magic), null, heroClass) });
  return out;
}

// ------------------------------------------------------------------ vendors (VendorStock.cs)

const VENDOR_KINDS = ["General", "Armor", "Weapons", "Food", "Curios"];
const RESTOCK_MS = 10 * 60000;
const ARMOR_SLOTS = [Slot.Helm, Slot.Chest, Slot.Gloves, Slot.Legs, Slot.Boots];
function vendorStock(kind, level, heroClass) {
  const items = [];
  switch (kind) {
    case "General": items.push(healthPotion(), manaPotion()); break;
    case "Food": items.push(provision("Bread"), food("Cooked Trout"), food("Cooked Salmon"), provision("Hearty Stew"), provision("Mulled Wine")); break;
    case "Armor":
      for (let i = 0; i < 6; i++) items.push(randomEquipment(level + rangeI(-1, 2), 0, rnd() < 0.35 ? Rarity.Magic : Rarity.Common, pick(ARMOR_SLOTS), heroClass));
      break;
    case "Weapons":
      for (let i = 0; i < 6; i++) items.push(randomEquipment(level + rangeI(-1, 2), 0, rnd() < 0.35 ? Rarity.Magic : Rarity.Common, Slot.Weapon, heroClass));
      break;
    case "Curios":
      for (let i = 0; i < 5; i++) items.push(randomEquipment(level + rangeI(0, 2), 0, rnd() < 0.25 ? Rarity.Rare : Rarity.Magic, rnd() < 0.5 ? Slot.Ring : Slot.Amulet, heroClass));
      for (let i = 0; i < 2; i++) items.push(gem(pick(GEM_TYPES), 0));
      break;
  }
  return items;
}
const isStackable = (it) => (it.MaxStack | 0) > 1;
/** Buying costs more than the item sells for. */
function price(it) {
  if (it.Kind !== Kind.Equipment) return it.Name.includes("Potion") ? 25 : Math.max(3, it.Value * 3);
  return Math.max(10, it.Value * (it.Rarity === Rarity.Rare ? 5 : 4));
}

// ------------------------------------------------------------------ skills, recipes, gathering

/** Total XP for a skill level (RuneScape formula, SkillSet.XpForLevel). */
const XP_TABLE = (() => {
  const t = [0];
  let points = 0;
  for (let l = 1; l <= 100; l++) { t[l] = Math.floor(points / 4); points += Math.floor(l + 300 * Math.pow(2, l / 7)); }
  return t;
})();
const SKILLS = ["Woodcutting", "Mining", "Fishing", "Smithing", "Cooking"];
function skillLevel(save, skill) {
  const xp = ((save && save.skillXp) || [])[SKILLS.indexOf(skill)] | 0;
  let lvl = 1;
  while (lvl < 99 && xp >= XP_TABLE[lvl + 1]) lvl++;
  return lvl;
}

const RECIPES = {
  "Forge Copper Gear": { skill: "Smithing", input: "Copper Ore", count: 3, level: 1, make: (lvl, cls) => randomEquipment(3 + Math.floor(lvl / 3), lvl * 0.01, null, null, cls) },
  "Forge Iron Gear": { skill: "Smithing", input: "Iron Ore", count: 3, level: 8, make: (lvl, cls) => randomEquipment(10 + Math.floor(lvl / 3), 0.1 + lvl * 0.01, null, null, cls) },
  "Forge Mithril Gear": { skill: "Smithing", input: "Mithril Ore", count: 3, level: 15, make: (lvl, cls) => randomEquipment(18 + Math.floor(lvl / 3), 0.25 + lvl * 0.01, null, null, cls) },
  "Cook Trout": { skill: "Cooking", input: "Raw Trout", count: 1, level: 1, make: () => food("Cooked Trout"), fail: "Burnt Fish" },
  "Cook Salmon": { skill: "Cooking", input: "Raw Salmon", count: 1, level: 8, make: () => food("Cooked Salmon"), fail: "Burnt Fish" },
};

/** What gathering can yield, with the skill and level it needs. */
const GATHER = {
  "Oak Logs": ["Woodcutting", 1], "Willow Logs": ["Woodcutting", 8], "Yew Logs": ["Woodcutting", 15],
  "Copper Ore": ["Mining", 1], "Iron Ore": ["Mining", 8], "Mithril Ore": ["Mining", 15],
  "Raw Trout": ["Fishing", 1], "Raw Salmon": ["Fishing", 8],
};

// ------------------------------------------------------------------ the bags

/** A character's items and gold, as stored in their save and sent to the client. */
function emptyLedger() {
  return { gold: 0, bag: new Array(BAG_SIZE).fill(null), eq: {}, stash: new Array(STASH_SIZE).fill(null), companions: [], riftBest: 0 };
}

/** Reads the item fields of a save (the format the client used to write) into a ledger. */
function ledgerFromSave(save) {
  const L = emptyLedger();
  if (!save) return L;
  const clean = (it) => (it && typeof it === "object" && typeof it.Name === "string" && it.Name ? item(it) : null);
  L.gold = Math.max(0, parseInt(save.gold, 10) || 0);
  for (const s of save.inventory || []) if (s && s.index >= 0 && s.index < BAG_SIZE) L.bag[s.index] = clean(s.item);
  for (const s of save.stash || []) if (s && s.index >= 0 && s.index < STASH_SIZE) L.stash[s.index] = clean(s.item);
  for (const it of save.equipped || []) { const c = clean(it); if (c && c.Kind === Kind.Equipment && c.Slot > 0) L.eq[c.Slot] = c; }
  L.companions = Array.isArray(save.companions) ? save.companions.filter((c) => typeof c === "string").slice(0, 20) : [];
  L.riftBest = Math.max(0, parseInt(save.riftBest, 10) || 0); // the highest rift tier beaten in time (rift.js)
  L.bounties = save.bounties && typeof save.bounties === "object" ? save.bounties : null; // today's bounties (bounty.js)
  return L;
}

/** Writes a ledger back into save fields. */
function ledgerToSave(L, save) {
  save.gold = L.gold;
  save.inventory = L.bag.map((it, index) => (it ? { index, item: it } : null)).filter(Boolean);
  save.stash = L.stash.map((it, index) => (it ? { index, item: it } : null)).filter(Boolean);
  save.equipped = Object.values(L.eq).filter(Boolean);
  save.companions = L.companions.slice();
  save.riftBest = L.riftBest || 0;
  save.bounties = L.bounties || null;
  return save;
}

/** Adds an item (stacking when possible). Returns the count that didn't fit (0 = all added). */
function addItem(slots, it) {
  it = { ...it };
  if (isStackable(it)) {
    for (const s of slots) {
      if (!s || s.Name !== it.Name || s.Count >= s.MaxStack) continue;
      const move = Math.min(it.Count, s.MaxStack - s.Count);
      s.Count += move;
      it.Count -= move;
      if (it.Count <= 0) return 0;
    }
  }
  const free = slots.indexOf(null);
  if (free < 0) return it.Count;
  slots[free] = it;
  return 0;
}
const freeSlots = (slots) => slots.filter((s) => s === null).length;
/** Room for these items (taking stacking into account) in a copy of the slots. */
function fits(slots, items) {
  const copy = slots.map((s) => (s ? { ...s } : null));
  return items.every((it) => addItem(copy, it) === 0);
}
const countOf = (slots, name) => slots.reduce((n, s) => n + (s && s.Name === name ? s.Count : 0), 0);
function removeByName(slots, name, count) {
  if (countOf(slots, name) < count) return false;
  for (let i = slots.length - 1; i >= 0 && count > 0; i--) {
    const s = slots[i];
    if (!s || s.Name !== name) continue;
    const take = Math.min(count, s.Count);
    s.Count -= take;
    count -= take;
    if (s.Count <= 0) slots[i] = null;
  }
  return true;
}
/** Merges stacks and orders the bags like Inventory.Sort on the client. */
function sortSlots(slots) {
  const merged = [];
  for (const it of slots.filter(Boolean).map((s) => ({ ...s }))) {
    if (isStackable(it))
      for (const m of merged) {
        if (m.Name !== it.Name || m.Count >= m.MaxStack) continue;
        const move = Math.min(it.Count, m.MaxStack - m.Count);
        m.Count += move;
        it.Count -= move;
        if (it.Count <= 0) break;
      }
    if (it.Count > 0) merged.push(it);
  }
  const group = (it) => (it.Kind === Kind.Equipment ? 0 : it.Kind === Kind.Gem ? 1 : it.Kind === Kind.Consumable ? 2 : 3);
  merged.sort((a, b) => group(a) - group(b) ||
    (a.Kind === Kind.Equipment ? (b.Rarity - a.Rarity) || (a.Slot - b.Slot) || (b.ItemLevel - a.ItemLevel) : 0) ||
    (a.Name < b.Name ? -1 : a.Name > b.Name ? 1 : 0));
  for (let i = 0; i < slots.length; i++) slots[i] = merged[i] || null;
}

module.exports = {
  Rarity, Kind, Slot, Stat, BAG_SIZE, STASH_SIZE, GAMEDATA, VENDOR_KINDS, RESTOCK_MS, RECIPES, GATHER, GEM_TIERS, GEM_TYPES,
  salvageYield, reforgeCost, reforge,
  item, healthPotion, manaPotion, material, food, provision, byName, starterWeapon, starterChest, randomEquipment, gem, parseGem, randomGem,
  rollLoot, rollChest, vendorStock, price, isStackable, skillLevel,
  emptyLedger, ledgerFromSave, ledgerToSave, addItem, fits, freeSlots, countOf, removeByName, sortSlots,
};
