#!/usr/bin/env node
// Extracts the game data the server needs (quest rewards, companion and mount prices, achievement names and titles) from the C# sources into
// server/gamedata.json, so the server and the client always agree.
//   node tools/gamedata/extract.js          writes server/gamedata.json
//   node tools/gamedata/extract.js --check  exits 1 if server/gamedata.json is out of date (the smoke test runs this)
"use strict";
const fs = require("fs");
const path = require("path");

const ROOT = path.join(__dirname, "..", "..");
const read = (rel) => fs.readFileSync(path.join(ROOT, rel), "utf8");

/** The `{ ... }` object initializers following each `new <Type>` in a C# source. */
function blocks(src, type) {
  const out = [];
  const re = new RegExp(`new ${type}\\s*\\{`, "g");
  let m;
  while ((m = re.exec(src))) {
    let depth = 1, i = m.index + m[0].length;
    for (; i < src.length && depth; i++) { if (src[i] === "{") depth++; else if (src[i] === "}") depth--; }
    out.push(src.slice(m.index + m[0].length, i - 1));
  }
  return out;
}
const str = (b, k) => { const m = b.match(new RegExp(`\\b${k}\\s*=\\s*"([^"]*)"`)); return m ? m[1] : null; };
const num = (b, k, d = 0) => { const m = b.match(new RegExp(`\\b${k}\\s*=\\s*(-?[\\d.]+)f?\\b`)); return m ? Number(m[1]) : d; };
const enm = (b, k, e) => { const m = b.match(new RegExp(`\\b${k}\\s*=\\s*${e}\\.(\\w+)`)); return m ? m[1] : null; };

const RARITY = ["Common", "Magic", "Rare", "Legendary", "Set"];

function extract() {
  const quests = {};
  for (const b of blocks(read("Assets/Scripts/Progression/Quests.cs"), "QuestDef")) {
    const id = str(b, "Id");
    if (!id) continue;
    const rarity = enm(b, "RewardRarity", "Rarity");
    quests[id] = {
      type: enm(b, "Type", "QuestType"), target: str(b, "Target"), count: num(b, "Count", 1), minLevel: num(b, "MinLevel", 1),
      gold: num(b, "RewardGold"), xp: num(b, "RewardXp"), itemLevel: num(b, "RewardItemLevel"), rarity: rarity ? RARITY.indexOf(rarity) : 0,
    };
  }
  const companions = {};
  for (const b of blocks(read("Assets/Scripts/Characters/Companion.cs"), "CompanionDef")) {
    const id = str(b, "Id");
    if (id) companions[id] = { price: num(b, "Price"), level: num(b, "RequiredLevel", 1) };
  }
  // Mounts are bought the same way, under "mount:<id>".
  for (const b of blocks(read("Assets/Scripts/Characters/Mount.cs"), "MountDef")) {
    const id = str(b, "Id");
    if (id) companions["mount:" + id] = { price: num(b, "Price"), level: num(b, "RequiredLevel", 1) };
  }
  const achievements = {};
  for (const b of blocks(read("Assets/Scripts/Progression/Achievements.cs"), "AchievementDef")) {
    const id = str(b, "Id");
    if (id) achievements[id] = { name: str(b, "Name"), title: str(b, "Title") || "" };
  }
  return { generatedFrom: "Assets/Scripts (tools/gamedata/extract.js)", quests, companions, achievements };
}

const out = path.join(ROOT, "server", "gamedata.json");
const json = JSON.stringify(extract(), null, 1) + "\n";
if (process.argv.includes("--check")) {
  const current = fs.existsSync(out) ? fs.readFileSync(out, "utf8") : "";
  if (current !== json) { console.error("server/gamedata.json is out of date: run  task gamedata  (node tools/gamedata/extract.js)"); process.exit(1); }
} else {
  fs.writeFileSync(out, json);
  const d = JSON.parse(json);
  console.log(`Wrote server/gamedata.json: ${Object.keys(d.quests).length} quests, ${Object.keys(d.companions).length} companions, ${Object.keys(d.achievements).length} achievements`);
}
