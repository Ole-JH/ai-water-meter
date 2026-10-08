"use strict";
// End-to-end smoke test: starts the server on a temp data dir, connects two fake clients and
// exercises login, world upload, monsters, kill credit, chat, fx, parties, saves and error paths.
// Run with: npm test   (or: task server:test)
// With PG_TEST_URL (e.g. postgres://user:pass@localhost:5432/postgres) the same tests run against PostgreSQL,
// in a throwaway database: npm run test:pg   (or: task server:test:pg)

const { spawn } = require("child_process");
const fs = require("fs");
const os = require("os");
const path = require("path");
const assert = require("assert");
const WebSocket = require("ws");

const PORT = 18000 + Math.floor(Math.random() * 1000);
const URL = `ws://localhost:${PORT}/ws`;
const METRICS_PORT = PORT + 2000;
const DATA_DIR = fs.mkdtempSync(path.join(os.tmpdir(), "shadowfall-test-"));
// A throwaway copy of the web folder (the tests write build.json into it).
const PUBLIC_DIR = fs.mkdtempSync(path.join(os.tmpdir(), "shadowfall-public-"));
fs.writeFileSync(path.join(PUBLIC_DIR, "index.html"), "<!doctype html><title>Shadowfall</title>");
// Game build stamps (they sort by time).
const BUILD_A = "2026.10.07-090000", BUILD_B = "2026.10.08-120000", BUILD_C = "2026.10.09-080000";
const MONSTER_NAMES = Object.keys(require("../content").MONSTERS);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// A fake 576x576 world (the real size): open field with a 4-cell wall around the edge.
const W = 576, H = 576;
const bytes = Buffer.alloc((W * H) / 8);
for (let y = 0; y < H; y++)
  for (let x = 0; x < W; x++)
    if (Math.min(x, y, W - 1 - x, H - 1 - y) < 4) { const i = y * W + x; bytes[i >> 3] |= 1 << (i & 7); }
let h = 2166136261;
for (const b of bytes) { h ^= b; h = Math.imul(h, 16777619) >>> 0; }
const HASH = `${W}x${H}-${h.toString(16).padStart(8, "0")}`;

function worldHash(buf) {
  let x = 2166136261;
  for (const b of buf) { x ^= b; x = Math.imul(x, 16777619) >>> 0; }
  return `${W}x${H}-${x.toString(16).padStart(8, "0")}`;
}

/** Connects, logs in to the account `name` (creating it, with a character of the same name, if needed) and plays. */
function connect(name, pass, hash = HASH, cells = bytes, build = BUILD_A) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(URL);
    const c = { ws, msgs: [], find: (t) => c.msgs.find((m) => m.t === t), all: (t) => c.msgs.filter((m) => m.t === t) };
    ws.on("open", () => ws.send(JSON.stringify({ t: "hello", hash, ver: 5, wv: 4, build })));
    ws.on("error", reject);
    ws.on("message", (d) => {
      const m = JSON.parse(d);
      c.msgs.push(m);
      if (m.t === "hi") ws.send(JSON.stringify({ t: "login", user: name, pass }));
      else if (m.t === "autherr" && /No account/.test(m.err)) ws.send(JSON.stringify({ t: "register", user: name, pass }));
      else if (m.t === "account") {
        const mine = m.chars.find((ch) => ch.name.toLowerCase() === name.toLowerCase());
        ws.send(JSON.stringify(mine ? { t: "play", name: mine.name } : { t: "create", name, look: "Knight" }));
      } else if (m.t === "needworld") ws.send(JSON.stringify({ t: "world", hash, w: W, h: H, cells: cells.toString("base64") }));
      else if (m.t === "grid") ws.send(JSON.stringify({ t: "world", hash: m.hash, w: m.w, h: m.h, cells: "" })); // adopt the server's map
      if (m.t === "welcome" || m.t === "error" || (m.t === "autherr" && !/No account/.test(m.err))) resolve(c);
    });
  });
}

/**
 * What a client knows from its snapshots, the way the game does it: full entries carry everything, later partial
 * entries only what changes (position, health, flags), merged into the last full one. Returns the latest view.
 */
function view(c) {
  const m = new Map(), p = new Map();
  for (const snap of c.all("snap")) {
    const seenM = new Set(), seenP = new Set();
    for (const e of snap.m) { m.set(e.id, { ...(m.get(e.id) || {}), sl: false, st: false, sh: false, ...e }); seenM.add(e.id); }
    for (const e of snap.p) { p.set(e.id, { ...(p.get(e.id) || {}), mv: false, atk: false, dead: false, ...e }); seenP.add(e.id); }
  }
  const last = c.all("snap").at(-1) || { m: [], p: [] };
  return { m: last.m.map((e) => m.get(e.id)), p: last.p.map((e) => p.get(e.id)), allP: [...p.values()] };
}

/** A raw connection for the account tests: send() and wait for the next message of a type. */
function rawClient() {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(URL);
    const c = { ws, msgs: [], waiters: [] };
    c.send = (m) => ws.send(JSON.stringify(m));
    c.next = (...types) => new Promise((res, rej) => {
      const i = c.msgs.findIndex((m) => types.includes(m.t));
      if (i >= 0) return res(c.msgs.splice(i, 1)[0]);
      const timer = setTimeout(() => rej(new Error(`timed out waiting for ${types.join("/")}`)), 4000);
      c.waiters.push({ types, res: (m) => { clearTimeout(timer); res(m); } });
    });
    ws.on("message", (d) => {
      const m = JSON.parse(d);
      const w = c.waiters.findIndex((x) => x.types.includes(m.t));
      if (w >= 0) c.waiters.splice(w, 1)[0].res(m);
      else c.msgs.push(m);
    });
    ws.on("error", reject);
    ws.on("open", async () => { c.send({ t: "hello", hash: HASH, ver: 5, wv: 4, build: BUILD_A }); await c.next("hi"); resolve(c); });
  });
}

/** Sends hello with a build stamp and returns the first reply. */
function rawHello(build) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(URL);
    ws.on("open", () => ws.send(JSON.stringify({ t: "hello", hash: HASH, ver: 5, wv: 4, build })));
    ws.on("message", (d) => { resolve(JSON.parse(d)); ws.close(); });
    ws.on("error", reject);
  });
}

const errOf = (c) => (c.msgs.filter((m) => m.t === "error" || m.t === "autherr").at(-1) || {}).err;

const state = (c, x, z, extra = {}) => c.ws.send(JSON.stringify({ t: "state", x, z, ry: 0, hp: 100, mhp: 100, lvl: 1, mv: false, atk: false, dead: false, ...extra }));

/** The Docker image must contain every module server.js requires (a missing COPY broke a deploy once). */
function checkDockerfile() {
  const docker = fs.readFileSync(path.join(__dirname, "..", "Dockerfile"), "utf8");
  const copiesAllJs = /^COPY \*\.js /m.test(docker);
  const src = fs.readFileSync(path.join(__dirname, "..", "server.js"), "utf8");
  for (const [, mod] of src.matchAll(/require\("\.\/([\w-]+)"\)/g))
    assert.ok(copiesAllJs || new RegExp(`COPY .*\\b${mod}\\.js\\b`).test(docker), `Dockerfile copies ${mod}.js`);
  assert.match(docker, /^COPY gamedata\.json /m, "Dockerfile copies gamedata.json");
}

/** server/gamedata.json must match the C# sources it is extracted from (quest rewards, companion prices). */
function checkGamedata() {
  const tool = path.join(__dirname, "..", "..", "tools", "gamedata", "extract.js");
  if (!fs.existsSync(tool)) return;
  const r = require("child_process").spawnSync(process.execPath, [tool, "--check"], { encoding: "utf8" });
  assert.strictEqual(r.status, 0, `server/gamedata.json is out of date: run task gamedata\n${r.stdout}${r.stderr}`);
}

/** An old-format character file (before accounts): it has its own password. */
function writeLegacyCharacter(name, pass, save) {
  const crypto = require("crypto");
  const salt = crypto.randomBytes(16).toString("hex");
  const hash = crypto.scryptSync(pass, salt, 32).toString("hex");
  fs.mkdirSync(path.join(DATA_DIR, "characters"), { recursive: true });
  fs.writeFileSync(path.join(DATA_DIR, "characters", name.toLowerCase() + ".json"),
    JSON.stringify({ name, salt, hash, created: "2026-10-01T00:00:00Z", save }));
}

async function accountTests(admin) {
  const c = await rawClient();
  c.send({ t: "register", user: "1x", pass: "fernpass1" });
  assert.match((await c.next("autherr")).err, /Account names must be/, "account names are validated");
  c.send({ t: "register", user: "Fern", pass: "abc" });
  assert.match((await c.next("autherr")).err, /at least 6/, "short passwords are refused");
  c.send({ t: "register", user: "Fern", pass: "fernpass1", email: "fern@example.com" });
  const acc = await c.next("account");
  assert.ok(acc.user === "Fern" && acc.chars.length === 0 && /^[A-Z2-9]{4}(-[A-Z2-9]{4}){3}$/.test(acc.rc), "a new account gets a recovery code");
  const recovery = acc.rc;

  c.send({ t: "create", name: "Fern", look: "Mage" });
  const w1 = await c.next("welcome");
  assert.ok(w1.name === "Fern" && w1.look === "Mage" && !w1.hasSave, "a new character enters the world");
  c.send({ t: "save", save: { level: 4, gold: 12, look: "Mage", x: 144, z: 150 } });
  await sleep(150);
  c.send({ t: "leave" });
  const back = await c.next("account");
  assert.ok(back.chars.length === 1 && back.chars[0].lvl === 4, "back to character select, with the character's level");
  c.send({ t: "create", name: "Fernling", look: "Rogue" });
  await c.next("welcome");
  c.send({ t: "leave" });
  assert.strictEqual((await c.next("account")).chars.length, 2, "an account can have several characters");
  c.send({ t: "create", name: "fernling", look: "Rogue" });
  assert.match((await c.next("autherr")).err, /taken/, "character names are unique");

  const other = await rawClient();
  other.send({ t: "register", user: "fern", pass: "whatever1" });
  assert.match((await other.next("autherr")).err, /taken/, "account names are unique (any case)");
  other.send({ t: "login", user: "Fernling", pass: "fernpass1" });
  assert.match((await other.next("autherr")).err, /character name/, "logging in with a character name explains what to do");
  other.send({ t: "forgot", user: "Fern" });
  assert.match((await other.next("autherr")).err, /can't send emails/, "without SMTP, email resets say so");

  c.send({ t: "chpass", old: "nope", pass: "fernpass2" });
  assert.match((await c.next("autherr")).err, /current password is wrong/, "changing the password needs the current one");
  c.send({ t: "chpass", old: "fernpass1", pass: "fernpass2" });
  assert.match((await c.next("authok")).msg, /Password changed/, "the password can be changed");

  other.send({ t: "login", user: "Fern", pass: "fernpass1" });
  assert.match((await other.next("autherr")).err, /Wrong password/, "the old password stops working");
  other.send({ t: "login", user: "Fern", pass: "fernpass2" });
  await other.next("account");
  assert.match((await c.next("error")).err, /another location/, "logging in elsewhere logs the old session out");

  // Recovery code: works once, then a new one is issued.
  const r1 = await rawClient();
  r1.send({ t: "reset", user: "Fern", code: recovery.toLowerCase().replace(/-/g, " "), pass: "fernpass3" });
  const reset = await r1.next("account");
  assert.ok(reset.rc && reset.rc !== recovery && reset.rcWhy === "used", "a recovery code resets the password and is replaced");
  const r2 = await rawClient();
  r2.send({ t: "reset", user: "Fern", code: recovery, pass: "fernpass4" });
  assert.match((await r2.next("autherr")).err, /doesn't match/, "a used recovery code no longer works");

  // Admin reset code
  admin.ws.send(JSON.stringify({ t: "adm", c: "resetpw", name: "Fernling" }));
  await sleep(300);
  const line = admin.all("sys").map((m) => m.msg).find((m) => /Reset code for account Fern:/.test(m));
  assert.ok(line, "admins get a reset code (by character name too)");
  const code = line.match(/Fern: ([A-Z0-9-]+)/)[1];
  r2.send({ t: "reset", user: "Fern", code, pass: "fernpass5" });
  await r2.next("account");
  const r3 = await rawClient();
  r3.send({ t: "reset", user: "Fern", code, pass: "fernpass6" });
  assert.match((await r3.next("autherr")).err, /already used/, "admin reset codes work once");

  r2.send({ t: "delchar", name: "Fernling", pass: "wrong" });
  assert.match((await r2.next("autherr")).err, /Wrong password/, "deleting a character needs the password");
  r2.send({ t: "delchar", name: "Fernling", pass: "fernpass5" });
  const afterDelete = await r2.next("account");
  assert.deepStrictEqual(afterDelete.chars.map((x) => x.name), ["Fern"], "characters can be deleted");

  // Five wrong passwords lock the account for a while.
  for (let i = 0; i < 5; i++) { r3.send({ t: "login", user: "Fern", pass: "bad" + i }); await r3.next("autherr"); }
  r3.send({ t: "login", user: "Fern", pass: "fernpass5" });
  assert.match((await r3.next("autherr")).err, /Too many wrong attempts/, "repeated wrong passwords lock the account briefly");

  // A character from before accounts became an account with the same name and password.
  const legacy = await rawClient();
  legacy.send({ t: "login", user: "oldtimer", pass: "oldpass" });
  const old = await legacy.next("account");
  assert.ok(old.chars.length === 1 && old.chars[0].name === "Oldtimer" && old.chars[0].lvl === 7 && old.chars[0].look === "Mage",
    "old characters are imported as accounts");
  assert.ok(old.rc && old.rcWhy === "new", "imported accounts get a recovery code at their first login");
  legacy.send({ t: "play", name: "Oldtimer" });
  const ow = await legacy.next("welcome");
  assert.strictEqual(ow.save.gold, 99, "imported characters keep their progress");

  // Resume tokens: back into the world without the password (after a restart or reload), once, and never as a reset code.
  const tok = await legacy.next("resume");
  assert.ok(tok.user === "Oldtimer" && tok.name === "Oldtimer" && /^[0-9a-f]{40}$/.test(tok.k), "entering the world hands out a resume token");
  const r4 = await rawClient();
  r4.send({ t: "reset", user: "oldtimer", code: tok.k, pass: "hijacked1" });
  assert.match((await r4.next("autherr")).err, /doesn't match/, "a resume token is no password reset code");
  const r5 = await rawClient();
  r5.send({ t: "resume", user: "oldtimer", code: tok.k, name: "Oldtimer" });
  const rw = await r5.next("welcome");
  assert.ok(rw.name === "Oldtimer" && rw.save.gold === 99, "a resume token signs in and plays the same hero");
  assert.ok((await r5.next("resume")).k !== tok.k, "and a fresh token comes with it");
  const r6 = await rawClient();
  r6.send({ t: "resume", user: "oldtimer", code: tok.k, name: "Oldtimer" });
  const again = await r6.next("autherr");
  assert.ok(again.k === "resume", "a resume token works once");

  for (const x of [c, other, r1, r2, r3, legacy, r4, r5, r6]) x.ws.close();
  await sleep(200);
}

/** With PG_TEST_URL: a fresh database for this run; returns its URL (or "" for the file store). */
async function testDatabase() {
  if (!process.env.PG_TEST_URL) return { url: "", drop: async () => {} };
  const { Client } = require("pg");
  const name = `shadowfall_test_${process.pid}_${Date.now()}`;
  const admin = new Client({ connectionString: process.env.PG_TEST_URL });
  await admin.connect();
  await admin.query(`CREATE DATABASE ${name}`);
  await admin.end();
  const url = new globalThis.URL(process.env.PG_TEST_URL);
  url.pathname = "/" + name;
  return {
    url: url.toString(),
    drop: async () => {
      const c = new Client({ connectionString: process.env.PG_TEST_URL });
      await c.connect();
      await c.query(`DROP DATABASE IF EXISTS ${name} WITH (FORCE)`);
      await c.end();
    },
  };
}

/** Items and gold live on the server: the client asks, the server answers with the new inventory. */
async function economyTests(a, b) {
  const inv = () => a.all("inv").at(-1);
  const iop = async (op, extra, wait = 200) => { a.ws.send(JSON.stringify({ t: "iop", op, ...extra })); await sleep(wait); };
  const lastErr = () => a.all("ierr").at(-1);
  const slotOf = (name) => inv().bag.findIndex((x) => x.Name === name);

  const start = a.find("inv");
  assert.ok(start, "the inventory is sent at login");
  assert.strictEqual(start.gold, 0, "new heroes start without gold");
  assert.strictEqual(start.bag.length, 40, "the bags have 40 slots");
  assert.strictEqual(start.bag.find((x) => x.Name === "Health Potion")?.Count, 5, "new heroes get five health potions");
  assert.ok(start.eq.filter((x) => x && x.Name).length >= 2, "and a weapon and armour to wear");
  assert.ok(Array.isArray(a.find("kill")?.drops), "kills carry the killer's own loot");

  // The client's save can't touch gold or items.
  a.ws.send(JSON.stringify({ t: "save", save: { level: 3, gold: 99999, inventory: [{ index: 0, item: { Name: "Godslayer", Kind: 0, Slot: 1 } }] } }));
  await sleep(200);
  assert.strictEqual(inv().gold, 0, "a client save does not mint gold");

  // Equipment: off and back on.
  const weapon = inv().eq.find((x) => x && x.Kind === 0 && x.Slot === 1);
  await iop("unequip", { slot: 1 });
  assert.ok(inv().bag.some((x) => x.Name === weapon.Name), "unequipping moves the weapon to the bags");
  await iop("equip", { i: slotOf(weapon.Name) });
  assert.ok(inv().eq.some((x) => x && x.Name === weapon.Name), "equipping wears it again");

  // Dropping and picking up.
  const mana = slotOf("Mana Potion");
  await iop("drop", { i: mana });
  const dropped = a.all("drops").at(-1)?.drops[0];
  assert.ok(dropped && dropped.item.Name === "Mana Potion" && slotOf("Mana Potion") < 0, "dropping puts it on the ground");
  await iop("pickup", { id: dropped.id });
  assert.strictEqual(inv().bag.find((x) => x.Name === "Mana Potion")?.Count, 3, "and it can be picked up again");
  await iop("pickup", { id: dropped.id });
  assert.strictEqual(inv().bag.find((x) => x.Name === "Mana Potion")?.Count, 3, "but only once");
  await iop("use", { i: slotOf("Mana Potion") });
  assert.strictEqual(inv().bag.find((x) => x.Name === "Mana Potion")?.Count, 2, "drinking uses one up");

  // Trading with merchants only works in town.
  await iop("buy", { k: "General", i: 0 });
  assert.match(lastErr()?.msg || "", /in town/, "merchants only trade in town");
  a.ws.send(JSON.stringify({ t: "adm", c: "give", what: "gold", n: 500 }));
  b.ws.send(JSON.stringify({ t: "adm", c: "give", what: "gold", n: 500 }));
  await sleep(200);
  assert.strictEqual(inv().gold, 500, "admins can give themselves gold");
  assert.strictEqual(b.all("inv").at(-1).gold, 0, "nobody else can");
  state(a, 144.5, 141.5);
  await sleep(150);
  await iop("vendor", { k: "General" });
  const stock = a.find("stock");
  assert.ok(stock && stock.k === "General" && stock.stock.length > 0, "vendors send their stock");
  const hp = stock.stock.findIndex((x) => x.Name === "Health Potion");
  await iop("buy", { k: "General", i: hp, n: 4 });
  assert.strictEqual(inv().bag.find((x) => x.Name === "Health Potion")?.Count, 9, "buying stacks potions");
  await iop("buy", { k: "General", i: hp, n: 1, name: "Excalibur" });
  assert.match(lastErr()?.msg || "", /no longer for sale/, "buying checks the item is still the one shown");
  const spent = 500 - inv().gold;
  assert.ok(spent > 0, "buying costs gold");
  await iop("sell", { i: slotOf("Mana Potion") });
  assert.ok(a.find("iok") && inv().gold > 500 - spent && slotOf("Mana Potion") < 0, "selling pays gold");
  a.ws.send(JSON.stringify({ t: "adm", c: "give", what: "potions" }));
  a.ws.send(JSON.stringify({ t: "adm", c: "give", what: "potions" }));
  await sleep(250);
  const stacks = inv().bag.filter((x) => x.Name === "Health Potion").map((x) => x.Count).sort((p, q) => q - p);
  assert.deepStrictEqual(stacks, [20, 9], "potions stack to 20");
  {
    // drag and drop in the bags: onto an empty slot moves, onto another item swaps
    const bag = () => inv().bag;
    const small = bag().findIndex((x) => x && x.Name === "Health Potion" && x.Count === 9);
    const empty = bag().findIndex((x) => !x || !x.Name);
    await iop("move", { i: small, j: empty });
    assert.ok(bag()[empty]?.Count === 9 && !bag()[small]?.Name, "moving an item to an empty slot");
    const full = bag().findIndex((x) => x && x.Name === "Health Potion" && x.Count === 20);
    const other = bag().findIndex((x, k) => x && x.Name && x.Name !== "Health Potion" && k !== empty);
    const otherName = bag()[other].Name;
    await iop("move", { i: full, j: other });
    assert.ok(bag()[other]?.Count === 20 && bag()[full]?.Name === otherName, "dropping onto another item swaps them");
  }

  // Quests pay once, and collect quests need the goods.
  await iop("quest", { k: "timber" });
  assert.match(lastErr()?.msg || "", /Oak Logs/, "collect quests need their items");
  const before = inv().gold;
  await iop("quest", { k: "wolves" });
  assert.strictEqual(inv().gold, before + 40, "quests pay their gold");
  assert.ok(a.all("iok").some((m) => m.op === "quest" && m.k === "wolves" && m.item), "and an item");
  await iop("quest", { k: "wolves" });
  assert.strictEqual(inv().gold, before + 40, "but only once");

  // The outer towns are towns too: merchants and the stash work there, not in the wilds between.
  state(a, 144.5, 464.5); // Frosthaven
  await sleep(150);
  const goldBefore = inv().gold;
  await iop("buy", { k: "General", i: hp, n: 1 });
  assert.ok(inv().gold < goldBefore, "Frosthaven's merchants trade");
  state(a, 300, 300); // the wilds at the old world's corner
  await sleep(150);
  await iop("stash", { i: slotOf("Health Potion") });
  assert.match(lastErr()?.msg || "", /stash is in town/, "the stash only opens in a town");
  state(a, 464.5, 464.5); // Emberwatch
  await sleep(150);
  await iop("stash", { i: slotOf("Health Potion") });
  assert.ok((inv().stash || []).some((x) => x && x.Name === "Health Potion"), "Emberwatch has a stash chest");

  // Mounts: bought like companions (as "mount:<id>"), and others only see a mount its rider owns.
  const aId = a.find("welcome").id;
  const seenMount = async () => {
    const from = b.msgs.length;
    await sleep(500);
    const entries = b.msgs.slice(from).filter((m) => m.t === "snap").flatMap((m) => m.p || []).filter((x) => x.id === aId && "mt" in x);
    return entries.length ? entries.at(-1).mt : undefined;
  };
  state(b, 466, 466);
  state(a, 464.5, 464.5, { lvl: 10, mt: "horse" });
  assert.strictEqual(await seenMount(), "", "nobody rides a mount they don't own");
  a.ws.send(JSON.stringify({ t: "adm", c: "give", what: "gold", n: 2000 }));
  await sleep(150);
  await iop("hire", { k: "mount:horse" });
  assert.ok(inv().comp.includes("mount:horse"), "Orla sells mounts");
  state(a, 464.5, 464.5, { lvl: 10, mt: "horse" });
  assert.strictEqual(await seenMount(), "horse", "other players see the mount");
  // Paragon levels (past the level cap) show on nameplates too.
  const from = b.msgs.length;
  state(a, 464.5, 464.5, { lvl: 30, pl: 7 });
  await sleep(500);
  const seenPl = b.msgs.slice(from).filter((m) => m.t === "snap").flatMap((m) => m.p || []).filter((x) => x.id === aId && "pl" in x).at(-1);
  assert.ok(seenPl && seenPl.pl === 7 && seenPl.lvl === 30, "other players see the paragon level");
  state(b, 146, 187);

  // The forge: salvage gear into materials (gems come back), reforge one property for materials and gold.
  for (let i = 0; i < 3; i++) a.ws.send(JSON.stringify({ t: "adm", c: "give", what: "legendary" }));
  a.ws.send(JSON.stringify({ t: "adm", c: "give", what: "gems" }));
  a.ws.send(JSON.stringify({ t: "adm", c: "give", what: "gold", n: 50000 }));
  await sleep(300);
  const legs = () => inv().bag.map((x, i) => [x, i]).filter(([x]) => x.Kind === 0 && x.Rarity === 3);
  assert.strictEqual(legs().length, 3, "three legendaries to work with");
  const gemSlot = inv().bag.findIndex((x) => x.Kind === 3);
  const gemName = inv().bag[gemSlot].Name, gemsBefore = inv().bag.filter((x) => x.Name === gemName).reduce((n, x) => n + x.Count, 0);
  state(a, 300, 300);
  await sleep(150);
  await iop("salvage", { i: legs()[0][1] });
  assert.match(lastErr()?.msg || "", /blacksmith in town/, "salvaging needs a blacksmith in town");
  state(a, 464.5, 464.5);
  await sleep(150);
  const withSocket = legs().find(([x]) => x.Sockets > 0);
  if (withSocket) await iop("socket", { i: gemSlot, j: withSocket[1] });
  const toSalvage = withSocket ? [withSocket[1], legs().find(([, i]) => i !== withSocket[1])[1]] : [legs()[0][1], legs()[1][1]];
  const keep = legs().find(([, i]) => !toSalvage.includes(i))[0].Name;
  for (const i of toSalvage) await iop("salvage", { i });
  const count = (name) => inv().bag.filter((x) => x.Name === name).reduce((n, x) => n + x.Count, 0);
  assert.strictEqual(count("Forgotten Soul"), 2, "salvaging a legendary gives a Forgotten Soul");
  assert.ok(count("Veiled Crystal") >= 2, "and Veiled Crystals");
  assert.strictEqual(inv().bag.filter((x) => x.Name === gemName).reduce((n, x) => n + x.Count, 0), gemsBefore, "socketed gems come back when salvaging");
  const slotKeep = inv().bag.findIndex((x) => x.Name === keep);
  const item0 = inv().bag[slotKeep], gold0 = inv().gold;
  await iop("reforge", { i: slotKeep, j: 0 });
  const after = inv().bag[slotKeep];
  assert.ok(after.Reforged === 1 && after.Mods.length === item0.Mods.length, "reforging marks which property was reforged");
  assert.ok(!after.Mods.slice(1).some((m) => m.Stat === after.Mods[0].Stat), "the new property is not one the item already has");
  assert.ok(inv().gold < gold0 && count("Forgotten Soul") === 1, "reforging costs gold and materials");
  await iop("reforge", { i: slotKeep, j: 1 });
  assert.match(lastErr()?.msg || "", /only that same property/, "after a reforge only the same property can be reforged again");
  await iop("reforge", { i: slotKeep, j: 0 });
  assert.strictEqual(count("Forgotten Soul"), 0, "the same one can be reforged again");
  await iop("reforge", { i: slotKeep, j: 0 });
  assert.match(lastErr()?.msg || "", /needs 1 Forgotten Soul/, "not without the materials");
  await iop("salvagejunk", {});
  assert.ok(a.all("iok").some((m) => m.op === "salvagejunk") || /no common or magic/.test(lastErr()?.msg || ""), "salvaging all junk at once");
  assert.ok(!inv().bag.some((x) => x.Kind === 0 && x.Rarity <= 1), "leaves no common or magic gear in the bags");

  state(a, 144, 187);
  await sleep(150);
}

/** The outer lands have their own monsters: a hero at a spawner there sees them. */
async function outerLandsTests(a) {
  const seen = new Set();
  for (const [x, z, want] of [[330, 400, ["Ember Skeleton", "Ash Wraith"]], [110, 306, ["Frost Wolf"]], [320, 136, ["Desert Raider"]]]) {
    const from = a.msgs.length;
    state(a, x, z);
    await sleep(700);
    for (const snap of a.msgs.slice(from).filter((m) => m.t === "snap")) for (const m of snap.m || []) if (m.n) seen.add(m.n);
    assert.ok(want.some((n) => seen.has(n)), `${want.join(" / ")} roam near ${x}, ${z} (saw ${[...seen].join(", ") || "nothing"})`);
  }
  state(a, 144, 187);
  await sleep(150);
}

async function invasionTests(a, b) {
  // An admin starts one at Hollowmere's south gate; everyone hears of it and gets its state.
  a.ws.send(JSON.stringify({ t: "chat", msg: "/a invasion hollowmere south" }));
  await sleep(300);
  assert.ok(b.all("sys").some((m) => /Hollowmere Village is under attack! .* south gate/.test(m.msg)), "an invasion is announced to everyone");
  const iv0 = b.all("invasion").at(-1)?.iv;
  assert.ok(iv0 && iv0.phase === "gather" && iv0.town === "Hollowmere Village" && iv0.hp === 100 && iv0.gx === 144.5 && iv0.gz === 114.5,
    "players get the invasion's state: town, gate, phase, the gate's integrity");
  a.ws.send(JSON.stringify({ t: "adm", c: "invasion", town: "Saltreach" }));
  await sleep(150);
  assert.ok(a.all("sys").some((m) => /already under attack/.test(m.msg)), "one invasion at a time");
  await sleep(1200); // INVASION_GATHER_S=1
  const iv1 = a.all("invasion").at(-1).iv;
  assert.ok(iv1.phase === "wave" && iv1.wave === 1 && iv1.left > 0, "after the gathering the first wave marches");
  assert.ok(iv1.gd && iv1.gd.filter((g) => g.k === "a").length === 4 && iv1.gd.some((g) => g.k === "s"),
    "the town posts its guards: archers on the wall, soldiers before the gate");

  // Alice fights them in front of the gate (Bob stays in town): three waves, the last with a warlord.
  state(a, 144.5, 104); // close enough to the gate that the invaders who come for her meet its guards
  await sleep(300);
  assert.ok(view(a).m.length >= iv1.left, "the invaders are out there");
  for (let i = 0; i < 20 && !a.all("gev").some((e) => e.k === "shot" || e.k === "swing"); i++) await sleep(300);
  assert.ok(a.all("gev").some((e) => e.k === "shot" || e.k === "swing"), "the guards fight the invaders");
  for (let i = 0; i < 3; i++) {
    a.ws.send(JSON.stringify({ t: "adm", c: "killall", r: 60 }));
    await sleep(400);
  }
  const win = a.find("invwin");
  assert.ok(win && win.k === "Hollowmere Village" && win.xp > 0 && win.drops.length >= 3, "beating every wave rewards the defenders with experience and loot");
  assert.ok(a.all("kill").some((k) => /^Warlord /.test(k.el || "")), "the last wave is led by a warlord");
  assert.ok(!b.find("invwin"), "heroes who didn't fight get nothing");
  assert.strictEqual(b.all("invasion").at(-1).iv.phase, "won", "everyone sees the town hold");
  assert.ok(b.all("sys").some((m) => /Hollowmere Village holds! 1 defender/.test(m.msg)), "the victory is announced");
  // The town remembers: its record, the defenders' names, a feast in the square and cheaper prices
  const holl = (b.all("chron").at(-1)?.rec || []).find((c) => c.k === "Hollowmere Village");
  assert.ok(holl && holl.h === 1 && holl.last === "h" && holl.d[0] === "Alice" && holl.p === 1, "the siege record: held once, by Alice, and the town prospers");
  assert.ok((b.all("after").at(-1)?.fe || []).some((f) => f.k === "Hollowmere Village" && f.left > 500), "a feast in the square");
  assert.ok((b.all("after").at(-1)?.rp || []).some((f) => f.k === "Hollowmere Village" && f.g === "south"), "carpenters mend the gate");
  state(b, 146, 150);
  await sleep(150);
  b.ws.send(JSON.stringify({ t: "iop", op: "vendor", k: "General" }));
  await sleep(250);
  assert.strictEqual(b.all("stock").at(-1).pmul, 0.96, "a prosperous town's merchants charge 4% less");

  // A new one can be started once that one is over; an admin can call it off.
  a.ws.send(JSON.stringify({ t: "adm", c: "invasion", town: "Frost" }));
  await sleep(200);
  assert.ok(a.all("sys").some((m) => /Frosthaven is under attack/.test(m.msg)), "towns are found by the start of their name");
  a.ws.send(JSON.stringify({ t: "adm", c: "invasion", stop: true }));
  await sleep(200);
  assert.strictEqual(b.all("invasion").at(-1).iv.phase, "none", "calling an invasion off clears it for everyone");
  b.ws.send(JSON.stringify({ t: "adm", c: "invasion" }));
  await sleep(150);
  assert.strictEqual(b.all("invasion").at(-1).iv.phase, "none", "only admins start invasions");

  // Nobody answers: the invaders march on the gate by themselves, batter it down and sack the town. (Bob is in it, out of
  // their reach: with nobody at all in town the siege would stand still.)
  state(b, 478.5, 144.5);
  await sleep(150);
  a.ws.send(JSON.stringify({ t: "adm", c: "invasion", town: "Saltreach", gate: "west" }));
  let last;
  for (let i = 0; i < 120 && (last = b.all("invasion").at(-1).iv).phase !== "lost"; i++) await sleep(500); // (the guards hold them up a while)
  assert.strictEqual(last.phase, "lost", "unopposed invaders march on the gate and break it");
  assert.strictEqual(last.hp, 0, "the gate's integrity runs out");
  assert.ok(b.all("sys").some((m) => /broke through the west gate of Saltreach/.test(m.msg)), "the sack is announced");
  assert.ok(b.all("invasion").some((m) => m.iv.phase === "wave" && m.iv.hp > 0 && m.iv.hp < 100), "the gate's integrity drops while they batter it");

  // The quarter behind the broken gate burns: its merchants are gone until the fires are out
  const sk = b.all("sack").at(-1)?.sk || [];
  assert.ok(sk.some((k) => k.k === "Saltreach" && k.g === "west" && k.left > 700 && k.r > 10), "the quarter behind the broken gate is set on fire for twelve minutes");
  assert.ok(b.all("sys").some((m) => /west quarter on fire/.test(m.msg)), "and everyone hears of it");
  const salt = (b.all("chron").at(-1)?.rec || []).find((c) => c.k === "Saltreach");
  assert.ok(salt && salt.f === 1 && salt.last === "f" && salt.p === -2, "the record: Saltreach fell, and it is poorer for it");
  // The raiders took captives to their camp: kill the captors and they go free, and the fires burn half as long
  const cp = (b.all("after").at(-1)?.cp || [])[0];
  assert.ok(cp && cp.k === "Saltreach" && cp.n >= 3 && cp.c >= 3 && cp.left > 500, "townsfolk are dragged off to the raiders' camp, guarded");
  const fireLeft = sk.find((k) => k.k === "Saltreach").left;
  state(a, cp.x, cp.z);
  await sleep(200);
  a.ws.send(JSON.stringify({ t: "adm", c: "killall", r: 20 }));
  await sleep(600);
  assert.ok(b.all("sys").some((m) => /captives of Saltreach are free/.test(m.msg)), "with the captors dead, the captives go free");
  assert.ok(b.all("sack").at(-1).sk.find((k) => k.k === "Saltreach").left < fireLeft * 0.6, "and the fires burn half as long");
  assert.strictEqual((b.all("chron").at(-1).rec || []).find((c) => c.k === "Saltreach").p, -1, "which the town remembers too");
  state(a, 144.5, 150);
  await sleep(150);
  const shop = async (x, z) => {
    state(b, x, z);
    await sleep(150);
    const errs = b.all("ierr").length, stocks = b.all("stock").length;
    b.ws.send(JSON.stringify({ t: "iop", op: "vendor", k: "General" }));
    await sleep(250);
    return { refused: b.all("ierr").slice(errs).some((m) => /fled the fire/.test(m.msg)), served: b.all("stock").length > stocks };
  };
  const burning = await shop(450.5, 144.5);
  assert.ok(burning.refused && !burning.served, "no trade in the burning quarter");
  const elsewhere = await shop(478.5, 144.5);
  assert.ok(!elsewhere.refused && elsewhere.served, "the far side of town still trades");
  // The rebuilding: the reeve takes timber, stone or coin, each delivery shortens the fires and pays experience
  state(b, 470.5, 150.5); // in Saltreach, clear of the fire
  await sleep(150);
  const leftNow = () => (b.all("sack").at(-1)?.sk || []).find((k) => k.k === "Saltreach")?.left || 0;
  const errs0 = b.all("ierr").length;
  b.ws.send(JSON.stringify({ t: "iop", op: "rebuild", k: "wood" }));
  await sleep(250);
  assert.ok(b.all("ierr").slice(errs0).some((m) => m.op === "rebuild" && /needs 5 logs/.test(m.msg)), "the reeve needs five logs");
  for (let i = 0; i < 5; i++) { b.ws.send(JSON.stringify({ t: "iop", op: "gather", name: "Oak Logs" })); await sleep(1300); }
  const before = leftNow();
  b.ws.send(JSON.stringify({ t: "iop", op: "rebuild", k: "wood" }));
  await sleep(300);
  const ok = b.all("iok").filter((m) => m.op === "rebuild").at(-1);
  assert.ok(ok && ok.xp > 0, "a delivery pays experience");
  assert.ok(leftNow() <= before - 85, "and the fires burn a minute and a half shorter");
  assert.strictEqual(b.all("inv").at(-1).bag.filter((x) => x && x.Name === "Oak Logs").length, 0, "the logs are taken");
  a.ws.send(JSON.stringify({ t: "adm", c: "sack", town: "stop" }));
  await sleep(200);
  assert.strictEqual((b.all("sack").at(-1)?.sk || []).length, 0, "an admin can put the fires out");
  const errs1 = b.all("ierr").length;
  b.ws.send(JSON.stringify({ t: "iop", op: "rebuild", k: "gold" }));
  await sleep(250);
  assert.ok(b.all("ierr").slice(errs1).some((m) => m.op === "rebuild" && /Nothing here is burning/.test(m.msg)), "no rebuilding where nothing burns");

  // Scouts see raiders coming: the town and gate are known before the first wave falls on it
  a.ws.send(JSON.stringify({ t: "adm", c: "invasion", town: "Frost", gate: "north", warn: true }));
  await sleep(400);
  const warned = b.all("invasion").at(-1)?.iv;
  assert.ok(warned && warned.phase === "warn" && warned.town === "Frosthaven" && warned.left > 0, "raiders are sighted before they attack");
  assert.ok(b.all("sys").some((m) => /Scouts sight raiders massing outside the north gate of Frosthaven/.test(m.msg)), "and everyone is warned");
  assert.strictEqual(warned.paused, true, "with nobody in Frosthaven the countdown stands still");
  await sleep(2200); // longer than INVASION_WARN_S=2
  assert.strictEqual(b.all("invasion").at(-1).iv.phase, "warn", "so no attack comes while nobody is there");
  state(b, 144.5, 470.5); // into Frosthaven
  let attacked;
  for (let i = 0; i < 20 && (attacked = b.all("invasion").at(-1).iv).phase === "warn"; i++) await sleep(250);
  assert.ok(b.all("invasion").some((m) => m.iv.phase === "warn" && m.iv.n > 0 && m.iv.sx && m.iv.gd.some((g) => g.k === "s")),
    "during the warning the raiders mass in a camp outside the gate and the guards take their posts");
  assert.ok(attacked.phase === "wave" && attacked.wave === 1, "then the first wave falls at once, no gathering");
  assert.strictEqual(attacked.gate, warned.gate, "on the gate that was named");
  assert.ok(attacked.ram === 1 && attacked.bc.length === 2 && attacked.bc.every((x) => x.l === ""), "a battering ram comes with them, and the gate has two unlit beacons");
  // Bob lights a beacon: the wall's archers shoot fire arrows
  state(b, attacked.bc[0].x, attacked.bc[0].z);
  await sleep(150);
  b.ws.send(JSON.stringify({ t: "beacon", i: 0 }));
  await sleep(250);
  assert.strictEqual(b.all("invasion").at(-1).iv.bc[0].l, "Bob", "a hero by a beacon lights it");
  assert.ok(b.all("sys").some((m) => /Bob lit a beacon/.test(m.msg)), "and everyone hears of it");
  // A fire arrow sets a roof alight; buckets of water put it out
  a.ws.send(JSON.stringify({ t: "adm", c: "invasion", fire: true }));
  await sleep(250);
  const fire = (b.all("invasion").at(-1).iv.fr || [])[0];
  assert.ok(fire && fire.s >= 40, "a roof behind the gate burns");
  state(b, fire.x, fire.z);
  await sleep(150);
  for (let i = 0; i < 2; i++) { b.ws.send(JSON.stringify({ t: "douse", id: fire.i })); await sleep(1300); }
  assert.ok(b.find("doused")?.xp > 0, "throwing water on it pays a little experience");
  assert.ok(!(b.all("invasion").at(-1).iv.fr || []).some((f) => f.i === fire.i), "two buckets put a fresh fire out");
  a.ws.send(JSON.stringify({ t: "adm", c: "invasion", stop: true }));
  await sleep(200);

  // A siege of a town nobody is in stands still, and the raiders give up after a while (INVASION_ABANDON_S=3): no sack
  a.ws.send(JSON.stringify({ t: "adm", c: "invasion", town: "Saltreach", gate: "west" }));
  await sleep(1500);
  const waiting = b.all("invasion").at(-1).iv;
  assert.ok(waiting.town === "Saltreach" && waiting.paused === true && waiting.hp === 100, "nobody in Saltreach: the siege waits, the gate untouched");
  let gone;
  for (let i = 0; i < 20 && (gone = b.all("invasion").at(-1).iv).phase !== "none"; i++) await sleep(250);
  assert.strictEqual(gone.phase, "none", "with nobody there the raiders withdraw");
  assert.ok(b.all("sys").some((m) => /raiders outside its west gate lose heart and withdraw/.test(m.msg)), "and the town is spared");
  assert.ok(!(b.all("sack").at(-1)?.sk || []).length, "nothing burns");
  state(b, 146, 150);
  state(a, 144, 150);
  await sleep(150);
}

/** Heroes on a town wall during a siege (the client's Rampart): archers outside can shoot them, nothing else can reach them. */
async function wallTests(a, b) {
  const bId = b.find("welcome").id;
  const shotsAtB = () => b.all("matk").filter((m) => m.tid === bId && m.k !== "spot").length;
  state(a, 150.5, 108.5);
  state(b, 150.5, 116.2, { w: true }); // on Hollowmere's south wall
  a.ws.send(JSON.stringify({ t: "vanish", dur: 6 })); // the admin who spawns them stays out of it
  await sleep(150);
  const seen = b.all("matk").length;
  a.ws.send(JSON.stringify({ t: "adm", c: "spawn", type: "Skeleton Archer", l: 1, n: 2 }));
  await sleep(2500);
  const shots = b.all("matk").slice(seen).filter((m) => m.tid === bId && m.k !== "spot");
  assert.ok(shots.length > 0 && shots.every((m) => m.k === "shot"), "archers outside shoot a hero up on the town wall");
  assert.ok(b.all("matk").slice(seen).some((m) => m.k === "spot" && m.dmg === 0), "a monster that spots someone shows it (the client's \"!\")");
  const snap = a.all("snap").at(-1);
  if (snap && snap.p) assert.ok(snap.p.some((x) => x.id === bId && x.w === true), "others see that hero on the wall");

  state(b, 150.5, 117.5); // down off the wall, inside the town
  await sleep(300);
  const before = shotsAtB();
  await sleep(2000);
  assert.strictEqual(shotsAtB(), before, "inside the town, nothing reaches them");
  a.ws.send(JSON.stringify({ t: "adm", c: "killall", r: 30 }));
  await sleep(300);

  state(b, 150.5, 116.2, { w: true });
  const seen2 = b.all("matk").length;
  a.ws.send(JSON.stringify({ t: "adm", c: "spawn", type: "Zombie", l: 1, n: 2 }));
  await sleep(2000);
  assert.ok(!b.all("matk").slice(seen2).some((m) => m.tid === bId && m.k !== "spot"), "melee monsters can't reach a hero on the wall");
  a.ws.send(JSON.stringify({ t: "adm", c: "killall", r: 30 }));
  state(b, 146, 150);
  state(a, 144, 150);
  await sleep(300);
}

async function auctionTests(a, b) {
  const iop = async (c, op, extra = {}) => { c.ws.send(JSON.stringify({ t: "iop", op, ...extra })); await sleep(200); };
  const listing = (c) => (c.all("auction").at(-1)?.items || []).map((x) => JSON.parse(x));
  const bInv = () => b.all("inv").at(-1);
  state(b, 300, 300);
  await sleep(150);
  const slot = bInv().bag.findIndex((x) => x && x.Name), what = bInv().bag[slot].Name;
  await iop(b, "aulist", { i: slot, n: 100 });
  assert.match(b.all("ierr").at(-1)?.msg || "", /general merchants in town/, "listing needs a town");
  state(b, 146, 150);
  state(a, 144, 150);
  await sleep(150);
  await iop(b, "aulist", { i: slot, n: 0 });
  assert.match(b.all("ierr").at(-1)?.msg || "", /price between/, "listings need a price");
  await iop(b, "aulist", { i: slot, n: 100 });
  assert.ok(!bInv().bag[slot].Name, "listing takes the item out of the bags");
  const mine = listing(b);
  assert.ok(mine.length === 1 && mine[0].seller === "Bob" && mine[0].price === 100 && mine[0].left > 2800, "your listings show for 48 hours");
  await iop(a, "aubrowse", { k: what.slice(0, 4) });
  const hit = listing(a).find((l) => l.seller === "Bob");
  assert.ok(hit && hit.item.Name === what, "others find it by name");
  await iop(a, "aubrowse", { k: "excalibur" });
  assert.ok(!listing(a).some((l) => l.seller === "Bob"), "searches filter");
  const goldA = a.all("inv").at(-1).gold, goldB = bInv().gold;
  await iop(a, "aubuy", { id: hit.id });
  await sleep(100);
  assert.strictEqual(a.all("inv").at(-1).gold, goldA - 100, "buying costs the price");
  assert.strictEqual(bInv().gold, goldB + 95, "the seller gets it less the house's 5%, at once when online");
  assert.ok(b.all("sys").some((m) => m.msg.startsWith(`[Auction] Alice bought your ${what}: 95 gold`)), "and is told");
  assert.ok(b.all("aumail").some((m) => m.gold === 95 && /Alice bought your/.test(m.k)), "a courier brings the seller the gold");
  assert.ok(a.all("ausold").some((m) => m.name === what && m.gold === 100), "the auctioneer rings the sale out to those nearby");
  await iop(a, "aubuy", { id: hit.id });
  assert.match(a.all("ierr").at(-1)?.msg || "", /already sold/, "an item sells once");
  const next = bInv().bag.findIndex((x) => x && x.Name), nextName = bInv().bag[next].Name;
  await iop(b, "aulist", { i: next, n: 50 });
  const own = listing(b)[0];
  await iop(b, "aubuy", { id: own.id });
  assert.match(b.all("ierr").at(-1)?.msg || "", /your own listing/, "you can't buy your own");
  await iop(b, "aucancel", { id: own.id });
  assert.ok(bInv().bag.some((x) => x.Name === nextName) && listing(b).length === 0, "cancelling brings it back");
  state(b, 145, 187);
}

async function bountyTests(a) {
  const list = () => a.all("bounties").at(-1).items.map((x) => x.split("|"));
  assert.ok(a.find("bounties"), "bounties are sent at login");
  a.ws.send(JSON.stringify({ t: "adm", c: "bounties" }));
  await sleep(200);
  const b0 = list();
  assert.strictEqual(b0.length, 3, "three bounties a day");
  const [text, have, need, done, kind, target] = b0[0];
  assert.ok(kind === "kill" && MONSTER_NAMES.includes(target) && have === "0" && +need >= 15 && done === "0" && text.startsWith(`Slay ${need} ${target}`), "the first is a hunt for a monster of the hero's level");
  const gearCount = () => a.all("inv").at(-1).bag.filter((x) => x.Kind === 0).length, gear0 = gearCount();
  for (let left = +need; left > 0; left -= 20) {
    a.ws.send(JSON.stringify({ t: "adm", c: "spawn", type: target, n: Math.min(20, left), l: 3 }));
    await sleep(150);
  }
  a.ws.send(JSON.stringify({ t: "adm", c: "killall", r: 15 }));
  await sleep(400);
  const b1 = list();
  assert.ok(b1[0][3] === "1" && b1[0][1] === need, "killing them finishes it");
  const paid = a.all("bounty").at(-1);
  assert.ok(paid && paid.k === text && paid.xp > 0 && paid.gold > 0, "a finished bounty pays experience and gold");
  assert.ok(gearCount() > gear0 || a.all("bounty").at(-1).drops.length > 0, "and an item");
}

async function riftTests(a) {
  state(a, 300, 300);
  await sleep(150);
  a.ws.send(JSON.stringify({ t: "ropen", n: 1 }));
  await sleep(150);
  assert.ok(a.all("sys").some((m) => /Stand at the Rift Stone/.test(m.msg)), "rifts open at the Rift Stone");
  state(a, 149.5, 151.5);
  await sleep(150);
  a.ws.send(JSON.stringify({ t: "rinfo" }));
  await sleep(150);
  assert.ok(a.all("rinfo").at(-1)?.k === "0" && a.all("rinfo").at(-1).items.length === 0, "a new hero has beaten no tier, and the board is empty");
  a.ws.send(JSON.stringify({ t: "ropen", n: 5 }));
  await sleep(300);
  const dg = a.all("dungeon").at(-1), r0 = a.all("rift").at(-1);
  assert.ok(dg && dg.id > 0 && dg.k === "Greater Rift  -  Tier 1" && dg.n === 1, "opening a rift takes you in (at most one tier above your best)");
  assert.ok(r0 && r0.n === 1 && r0.i === 0 && r0.left > 500 && r0.k === "run", "with a progress bar and a timer");
  state(a, dg.start[0], dg.start[1]);
  await sleep(150);
  a.ws.send(JSON.stringify({ t: "adm", c: "killall", r: 200 }));
  await sleep(400);
  assert.ok(a.all("rift").at(-1).i >= 100 && a.all("rift").at(-1).k === "guardian", "killing fills the bar, and the guardian comes");
  assert.ok(a.all("sys").some((m) => /The Rift Guardian, .* has come/.test(m.msg)), "it is announced");
  a.ws.send(JSON.stringify({ t: "adm", c: "killall", r: 200 }));
  await sleep(400);
  assert.strictEqual(a.all("rift").at(-1).k, "won", "beating the guardian in time clears the rift");
  assert.ok(a.all("sys").some((m) => /Rift tier 1 cleared in .* Tier 2 is open to you/.test(m.msg)), "and opens the next tier");
  const chest = a.all("drops").at(-1).drops;
  assert.ok(chest.length >= 2, "with a chest's worth of loot");
  assert.ok(a.all("rift").at(-1).c > 0, "a cleared rift starts to collapse");
  await sleep(2600); // RIFT_COLLAPSE_S=2
  const back = a.all("dungeon").at(-1);
  assert.ok(back.id === 0 && Math.abs(back.x - 149.5) < 0.1, "when it collapses it throws you back to the Rift Stone");
  const moved = a.all("drops").at(-1).drops;
  assert.ok(moved.length >= chest.length && moved.every((d) => Math.abs(d.x - 149.5) < 3 && d.z > 150), "with the loot you left in there, around you");
  a.ws.send(JSON.stringify({ t: "iop", op: "pickup", id: moved[0].id }));
  await sleep(200);
  assert.ok(a.all("iok").some((m) => m.op === "pickup" && m.id === moved[0].id), "and you can pick it up there");
  a.ws.send(JSON.stringify({ t: "rinfo" }));
  await sleep(150);
  const info = a.all("rinfo").at(-1);
  assert.ok(info.k === "1" && /^1\|\d+\|Alice$/.test(info.items[0]), "the best tier is kept, and the run is on the leaderboard");
  a.ws.send(JSON.stringify({ t: "ropen", n: 9 }));
  await sleep(300);
  assert.strictEqual(a.all("dungeon").at(-1).k, "Greater Rift  -  Tier 2", "the next tier opens");
  a.ws.send(JSON.stringify({ t: "dleave" }));
  await sleep(200);
  state(a, 144, 150);
  await sleep(150);
}

/** The admin window's newer buttons: server status, rifts by tier, forge materials, every mount. */
async function adminContentTests(a) {
  const result = async (cmd) => { const n = a.all("sys").length; a.ws.send(JSON.stringify({ t: "adm", ...cmd })); await sleep(300); return a.all("sys").slice(n).map((m) => m.msg).join("\n"); };
  assert.match(await result({ c: "status" }), /\[admin\] \d+ online \| invasion: .* \| world boss: .* auction listing.* guild.* rift record: tier/, "status sums up the live content");
  assert.match(await result({ c: "riftbest", n: 7 }), /best rift tier is now 7/, "admins can set their best rift tier");
  assert.strictEqual(a.all("rinfo").at(-1).k, "7", "and the client hears of it");
  await result({ c: "rift", n: 12 });
  assert.strictEqual(a.all("dungeon").at(-1).k, "Greater Rift  -  Tier 12", "admins open any tier from anywhere");
  a.ws.send(JSON.stringify({ t: "dleave" }));
  await sleep(200);
  await result({ c: "give", what: "materials" });
  const got = a.all("inv").at(-1).bag.map((x) => x.Name).concat(a.all("drops").flatMap((d) => d.drops.map((x) => x.item && x.item.Name)));
  assert.ok(["Scrap Iron", "Arcane Dust", "Veiled Crystal", "Forgotten Soul"].every((n) => got.includes(n)), "admins can give themselves forge materials");
  assert.match(await result({ c: "give", what: "mounts" }), /You now own .*stag/, "and every mount");
  assert.ok(a.all("inv").at(-1).comp.includes("mount:warhorse"), "the mounts are theirs");
  state(a, 144, 150);
  await sleep(150);
}

async function guildTests(a, b) {
  const say = async (c, msg) => { c.ws.send(JSON.stringify({ t: "chat", msg })); await sleep(600); }; // chat: two lines a second
  await say(a, "/guild create Hollow Guard HG");
  const g0 = a.all("guild").at(-1)?.g;
  assert.ok(g0 && g0.name === "Hollow Guard" && g0.tag === "HG" && g0.rank === "leader" && g0.members.length === 1, "founding a guild makes you its leader");
  assert.ok(a.all("sys").some((m) => /You founded Hollow Guard <HG> \(1000 gold\)/.test(m.msg)), "it costs gold");
  assert.match(g0.hb, /^\d,\d,\d$/, "a new guild gets heraldry: two colours and an emblem");
  await say(a, "/gbanner 3 3 1");
  assert.ok(a.all("sys").some((m) => /Usage: \/gbanner/.test(m.msg)), "the two colours must differ");
  await say(a, "/gbanner 2 7 5");
  assert.strictEqual(a.all("guild").at(-1).g.hb, "2,7,5", "the leader picks the banner");
  a.ws.send(JSON.stringify({ t: "glist" }));
  await sleep(200);
  assert.ok(a.all("glist").at(-1).items.some((x) => x === "Hollow Guard|HG|1|2,7,5|Alice"), "the guild board lists the guilds with their banners");
  const snapA = b.all("snap").filter((m) => m.p && m.p.some((x) => x.name === "Alice")).at(-1);
  if (snapA) await sleep(1000), assert.ok(b.all("snap").some((m) => m.p && m.p.some((x) => x.name === "Alice" && x.gb === "2,7,5")), "others see the banner on the member's back");
  await say(b, "/guild create Hollow Guard XX");
  assert.ok(b.all("sys").some((m) => /already a guild called Hollow Guard|costs 1000 gold/.test(m.msg)), "names are unique (and it costs gold)");
  await say(a, "/ginvite Bob");
  assert.strictEqual(b.all("ginv").at(-1)?.k, "Hollow Guard <HG>", "an invitation reaches the hero");
  b.ws.send(JSON.stringify({ t: "ganswer", yes: true }));
  await sleep(200);
  const gb = b.all("guild").at(-1)?.g;
  assert.ok(gb && gb.rank === "member" && gb.members.length === 2 && gb.members.every((m) => m.on), "accepting joins: everyone gets the roster with who's online");
  assert.ok(a.all("sys").some((m) => /\[HG\] Bob has joined the guild/.test(m.msg)), "the guild hears about it");
  await say(a, "/g hello guild");
  assert.ok(b.all("chat").some((m) => m.ch === "g" && m.msg === "hello guild" && m.name === "Alice"), "guild chat reaches the members");
  state(b, 146, 150);
  await sleep(500);
  const aId = a.find("welcome").id;
  const seen = b.all("snap").flatMap((m) => m.p || []).filter((x) => x.id === aId && "gt" in x).at(-1);
  assert.strictEqual(seen?.gt, "HG", "the guild tag shows on nameplates");
  await say(b, "/gkick Alice");
  assert.ok(b.all("sys").some((m) => /only remove members of a lower rank/.test(m.msg)), "members can't remove the leader");
  await say(a, "/gpromote Bob");
  assert.strictEqual(b.all("guild").at(-1).g.rank, "officer", "the leader promotes members to officers");
  await say(a, "/gmotd Raid at dusk");
  assert.strictEqual(b.all("guild").at(-1).g.motd, "Raid at dusk", "officers and the leader set the message of the day");
  await say(b, "/gleave");
  assert.strictEqual(b.all("guild").at(-1).g, null, "leaving clears the guild");
  assert.strictEqual(a.all("guild").at(-1).g.members.length, 1, "and the others see them go");
  state(b, 145, 187);
}

async function duelTests(a, b) {
  const aId = a.find("welcome").id, bId = b.find("welcome").id;
  state(a, 140, 150);
  state(b, 146, 150);
  await sleep(200);
  a.ws.send(JSON.stringify({ t: "dreq", id: bId }));
  await sleep(200);
  assert.strictEqual(b.all("dreq").at(-1)?.id, aId, "a duel challenge reaches the other hero");
  b.ws.send(JSON.stringify({ t: "dans", yes: true }));
  await sleep(200);
  const ca = a.all("duel").at(-1), cb = b.all("duel").at(-1);
  assert.ok(ca && cb && ca.k === "count" && ca.id === bId && cb.id === aId && Math.abs(ca.x - 143) < 0.01, "accepting starts a countdown for both, with a flag between them");
  a.ws.send(JSON.stringify({ t: "dhit", id: bId, dmg: 30 }));
  await sleep(150);
  assert.ok(!b.find("dhit"), "no hits before the fight starts");
  for (let i = 0; i < 25 && a.all("duel").at(-1).k !== "fight"; i++) await sleep(200);
  assert.strictEqual(a.all("duel").at(-1).k, "fight", "then the fight starts");
  const rings = a.all("duelring");
  assert.ok(rings.some((r) => r.k === "count" && r.name === "Alice|Bob" && Math.abs(r.x - 143) < 0.01) && rings.some((r) => r.k === "fight"),
    "everyone near sees the ring go up and the fight start");
  a.ws.send(JSON.stringify({ t: "dhit", id: bId, dmg: 30 }));
  b.ws.send(JSON.stringify({ t: "dhit", id: 999, dmg: 30 }));
  await sleep(200);
  assert.deepStrictEqual([b.all("dhit").at(-1)?.id, b.all("dhit").at(-1)?.dmg], [aId, 30], "hits on the opponent reach them");
  assert.ok(!a.find("dhit"), "hits only count on your own opponent");
  b.ws.send(JSON.stringify({ t: "dyield" }));
  await sleep(200);
  assert.ok(a.all("duel").at(-1).k === "end" && a.all("duel").at(-1).win === aId && b.all("duel").at(-1).win === aId, "yielding ends the duel: the other one wins");
  assert.ok(b.all("sys").some((m) => /Alice has defeated Bob in a duel/.test(m.msg)), "the result is announced nearby");

  // Running from the flag loses.
  b.ws.send(JSON.stringify({ t: "dreq", id: aId }));
  await sleep(150);
  a.ws.send(JSON.stringify({ t: "dans", yes: true }));
  for (let i = 0; i < 25 && b.all("duel").at(-1)?.k !== "fight"; i++) await sleep(200);
  state(a, 144, 210); // 60 m away
  await sleep(800);
  const end = b.all("duel").at(-1);
  assert.ok(end.k === "end" && end.win === bId, "fleeing from the duel loses it");
  a.ws.send(JSON.stringify({ t: "dreq", id: bId }));
  await sleep(150);
  assert.ok(a.all("sys").some((m) => /too far away/.test(m.msg)), "you can only challenge heroes nearby");
  state(a, 144, 150);
  state(b, 145, 187);
  await sleep(200);
}

async function worldBossTests(a, b) {
  a.ws.send(JSON.stringify({ t: "adm", c: "worldboss", name: "bramble" }));
  await sleep(300);
  assert.ok(b.all("sys").some((m) => /Old Bramblehide has risen in Whisperwood/.test(m.msg)), "a world boss rising is announced to everyone");
  const wb = b.all("wboss").at(-1)?.wb;
  assert.ok(wb && wb.phase === "up" && wb.name === "Old Bramblehide" && wb.l === 10 && wb.hp === 100 && wb.x > 0, "everyone gets where it is and how it fares");
  a.ws.send(JSON.stringify({ t: "adm", c: "worldboss", name: "pyre" }));
  await sleep(150);
  assert.ok(a.all("sys").some((m) => /already up/.test(m.msg)), "one world boss at a time");

  // Alice and Bob fight it: it gets tougher with the second hero, slams (after a warning ring), and drops for both.
  state(a, wb.x + 2, wb.z);
  state(b, wb.x - 2, wb.z);
  await sleep(400);
  const boss = () => view(a).m.find((x) => x.n === "Old Bramblehide");
  assert.ok(boss(), "the world boss is out there");
  const hp0 = boss().mhp;
  a.ws.send(JSON.stringify({ t: "hit", mid: boss().id, dmg: 50 }));
  b.ws.send(JSON.stringify({ t: "hit", mid: boss().id, dmg: 50 }));
  let slammed = false;
  for (let i = 0; i < 40 && !slammed; i++) { await sleep(100); slammed = a.all("matk").some((m) => m.k === "slam"); }
  const warn = a.all("matk").find((m) => m.k === "warn"), slam = a.all("matk").find((m) => m.k === "slam");
  assert.ok(warn && slam && Math.abs(warn.x - slam.x) < 0.01 && slam.dmg > 0, "it warns with a ring, then slams the same spot");
  assert.ok(a.msgs.indexOf(warn) < a.msgs.indexOf(slam), "the warning comes first");
  await sleep(300); // the next snapshot carries the new maximum
  assert.ok(boss().mhp > hp0, `a second hero makes it tougher (${hp0} -> ${boss().mhp})`);
  assert.strictEqual(b.all("wboss").at(-1).wb.n, 2, "the tracker counts the heroes fighting it");
  assert.ok(wb.age >= 0 && wb.age < 5 && wb.pl === 3, "a new world boss: how long it has been up (for its entrance) and its three armour plates");
  const ar0 = boss().ar;
  for (let i = 0; i < 80 && !a.all("matk").some((m) => m.k === "phase"); i++) {
    a.ws.send(JSON.stringify({ t: "hit", mid: boss().id, dmg: 160 }));
    await sleep(40);
  }
  const phase = a.all("matk").find((m) => m.k === "phase");
  assert.ok(phase && phase.dmg === 2, "at three quarters of its health an armour plate breaks off");
  await sleep(300);
  assert.strictEqual(b.all("wboss").at(-1).wb.pl, 2, "everyone hears it has two plates left");
  assert.ok(boss().ar < ar0, `and it has less armour (${ar0} -> ${boss().ar})`);
  a.ws.send(JSON.stringify({ t: "adm", c: "killall", r: 15 }));
  await sleep(500);
  const ka = a.all("kill").find((k) => k.name === "Old Bramblehide"), kb = b.all("kill").find((k) => k.name === "Old Bramblehide");
  assert.ok(ka && kb && ka.lb > 0 && ka.drops.length >= 3, "everyone who fought it gets the kill, with a boss's loot and a bonus");
  assert.strictEqual(b.all("wboss").at(-1).wb.phase, "none", "the tracker ends when it dies");

  // Admins can put one back to sleep.
  a.ws.send(JSON.stringify({ t: "adm", c: "worldboss", name: "Hrimgar" }));
  await sleep(200);
  assert.strictEqual(b.all("wboss").at(-1).wb.name, "Hrimgar the Mountain", "admins raise a world boss by name");
  a.ws.send(JSON.stringify({ t: "adm", c: "worldboss", stop: true }));
  await sleep(200);
  assert.strictEqual(b.all("wboss").at(-1).wb.phase, "none", "and put it back to sleep");
  assert.ok(b.all("sys").some((m) => /Hrimgar the Mountain returns to its slumber/.test(m.msg)), "which everyone hears");
  state(a, 144, 150);
  state(b, 145, 187);
  await sleep(200);
}

async function main() {
  checkDockerfile();
  checkGamedata();
  const db = await testDatabase();
  if (db.url) console.log("Testing against PostgreSQL");
  writeLegacyCharacter("Oldtimer", "oldpass", { level: 7, gold: 99, look: "Mage", x: 144, z: 150 });
  const server = spawn(process.execPath, [path.join(__dirname, "..", "server.js")], {
    env: { ...process.env, PORT: String(PORT), DATA_DIR, PUBLIC_DIR, ELITE_CHANCE: "0", INVASION_MINUTES: "0", INVASION_GATHER_S: "1", INVASION_WARN_S: "2", INVASION_ABANDON_S: "3", INVASION_SIEGE_RATE: "15", WORLD_BOSS_MINUTES: "0", WORLD_BOSS_SLAM_S: "1", RIFT_COLLAPSE_S: "2", ADMINS: "alice", METRICS_PORT: String(METRICS_PORT), DATABASE_URL: db.url },
    stdio: ["ignore", "pipe", "pipe"],
  });
  let serverLog = "";
  server.stdout.on("data", (d) => (serverLog += d));
  server.stderr.on("data", (d) => (serverLog += d));
  await sleep(700);

  let ok = false;
  try {
    const res = await fetch(`http://localhost:${PORT}/healthz`);
    assert.strictEqual(res.status, 200, "healthz responds");
    const health = await res.clone().json();
    assert.ok(Number.isInteger(health.online) && Number.isInteger(health.monsters) && typeof health.build === "string",
      "healthz reports players online, monsters and the build (the dashboard shows them)");
    const page = await fetch(`http://localhost:${PORT}/`);
    assert.strictEqual(page.status, 200, "index page served");
    // Errors from players' games: logged and counted, at most 30 a minute from one address.
    const report = (body) => fetch(`http://localhost:${PORT}/client-error`, { method: "POST", headers: { "Content-Type": "application/json" }, body });
    assert.strictEqual((await report(JSON.stringify({ kind: "exception", msg: "NullReferenceException: boom\nsecond line", stack: "at Foo.Bar()", build: BUILD_A, ua: "test", page: 12 }))).status, 204, "client errors are accepted");
    await report("not json");
    await sleep(100);
    assert.match(serverLog, /client error \[exception\] build 2026\.10\.07-090000, 12 s after loading: NullReferenceException: boom \| second line \|\| at Foo\.Bar\(\)/, "client errors are logged on one line");
    let limited = 0;
    for (let i = 0; i < 32; i++) if ((await report(JSON.stringify({ kind: "js", msg: "x" + i }))).status === 429) limited++;
    assert.ok(limited >= 2, "client error reports are rate limited");

    const a = await connect("Alice", "secret1");
    assert.ok(a.find("needworld"), "first client is asked for the world");
    assert.ok(a.find("welcome"), "Alice logs in");
    assert.strictEqual(a.find("welcome").hasSave, false, "new character has no save");

    const b = await connect("Bob", "secret2");
    assert.ok(b.find("welcome"), "Bob logs in");

    // Stand next to the wolves north of town
    state(a, 144, 187);
    state(b, 145, 187);
    await sleep(1500);
    const snap = view(a);
    assert.ok(snap.m.length > 0, "snapshot contains monsters");
    assert.ok(snap.p.some((p) => p.name === "Bob"), "snapshot contains the other player");
    const raw = a.all("snap").at(-1);
    assert.ok(raw.m.every((x) => !("n" in x) || x.n) && raw.m.some((x) => !("n" in x)), "monsters seen recently come as partial entries (no type)");
    assert.ok(a.all("matk").length > 0, "monsters attack players");

    const wolf = snap.m.slice().sort((p, q) => Math.hypot(p.x - 144, p.z - 187) - Math.hypot(q.x - 144, q.z - 187))[0];
    a.ws.send(JSON.stringify({ t: "hit", mid: wolf.id, dmg: 5 }));
    b.ws.send(JSON.stringify({ t: "hit", mid: wolf.id, dmg: 999999 })); // capped, but enough to kill a wolf
    await sleep(300);
    assert.strictEqual(a.find("kill")?.mid, wolf.id, "Alice gets kill credit");
    assert.strictEqual(b.find("kill")?.mid, wolf.id, "Bob gets kill credit");
    assert.ok(a.find("kill").xp > 0, "kill grants xp");

    a.ws.send(JSON.stringify({ t: "chat", msg: "hello <b>there</b>" }));
    a.ws.send(JSON.stringify({ t: "fx", k: "fireball", x: 144, z: 187, tx: 149, tz: 192 }));
    a.ws.send(JSON.stringify({ t: "save", save: { level: 3, xp: 10, gold: 55, x: 144, z: 187 } }));
    await sleep(300);
    assert.strictEqual(b.find("chat")?.msg, "hello bthere/b", "chat is relayed and sanitised");
    assert.ok(b.find("fx"), "spell effects are relayed");
    await sleep(400);
    a.ws.send(JSON.stringify({ t: "chat", msg: "look at my [Worn Shortsword]", le: 1 }));
    await sleep(300);
    const linkMsg = b.all("chat").find((m) => /Worn Shortsword/.test(m.msg));
    assert.ok(linkMsg && linkMsg.it && linkMsg.it.Name === "Worn Shortsword", "chat can link an item the sender wears (the server's copy)");
    await sleep(400);
    a.ws.send(JSON.stringify({ t: "chat", msg: "fake [Excalibur]", li: 39 }));
    await sleep(300);
    assert.ok(!b.all("chat").find((m) => /Excalibur/.test(m.msg)).it, "a link to an empty slot carries no item");

    a.ws.send(JSON.stringify({ t: "emote", e: "dance" }));
    await sleep(300);
    const emote = b.find("emote");
    assert.ok(emote && emote.e === "dance" && emote.name === "Alice", "emotes are shown to players nearby");
    await sleep(900);
    a.ws.send(JSON.stringify({ t: "emote", e: "backflip-into-the-void" }));
    await sleep(300);
    assert.strictEqual(b.all("emote").length, 1, "unknown emotes are ignored");
    assert.ok(b.find("welcome").now > 0, "welcome carries the server clock");

    await economyTests(a, b);
    await outerLandsTests(a);

    // ---- parties
    const d = await connect("Dana", "secret4");
    state(d, 146, 187);
    a.ws.send(JSON.stringify({ t: "pinvite", name: "bob" }));
    await sleep(200);
    assert.strictEqual(b.find("pinv")?.name, "Alice", "Bob receives Alice's invitation");
    b.ws.send(JSON.stringify({ t: "paccept" }));
    a.ws.send(JSON.stringify({ t: "chat", msg: "/invite Dana" }));
    await sleep(200);
    d.ws.send(JSON.stringify({ t: "paccept" }));
    await sleep(300);
    const party = d.all("party").at(-1);
    assert.strictEqual(party.pm.length, 3, "party has three members");
    assert.strictEqual(party.id, a.find("welcome").id, "Alice leads the party");
    const bobInParty = party.pm.find((x) => x.name === "Bob");
    assert.ok(bobInParty && bobInParty.mdl === "Knight" && "mp" in bobInParty && "mmp" in bobInParty && "wk" in bobInParty && "ry" in bobInParty && bobInParty.dn === "",
      "party members carry what the party frames show (class, mana, weapon, facing, where)");

    b.ws.send(JSON.stringify({ t: "chat", msg: "/p group up" }));
    b.ws.send(JSON.stringify({ t: "pshare", q: "wolves" }));
    await sleep(300);
    assert.strictEqual(d.all("chat").find((m) => m.ch === "p")?.msg, "group up", "party chat reaches members");
    assert.strictEqual(d.find("qshare")?.k, "wolves", "quests can be shared with the party");
    b.ws.send(JSON.stringify({ t: "pping", x: 150.5, z: 140.25 }));
    b.ws.send(JSON.stringify({ t: "pping", x: 1, z: 1 })); // too soon: dropped
    await sleep(200);
    const pings = d.all("ping");
    assert.ok(pings.length === 1 && pings[0].x === 150.5 && pings[0].name === "Bob" && pings[0].di === 0, "map pings reach the party, not too often");

    // ready check (leader only), answers seen by all; the loot rule (leader only)
    b.ws.send(JSON.stringify({ t: "pready" }));
    await sleep(150);
    assert.ok(!d.find("rcheck"), "only the leader can start a ready check");
    a.ws.send(JSON.stringify({ t: "pready" }));
    await sleep(200);
    assert.ok(b.find("rcheck") && d.find("rcheck")?.name === "Alice", "a ready check reaches every member");
    b.ws.send(JSON.stringify({ t: "pans", yes: true }));
    d.ws.send(JSON.stringify({ t: "pans", yes: false }));
    await sleep(250);
    assert.ok(a.all("rans").length === 2 && a.find("rdone")?.ok === false, "answers come back, and one 'not ready' makes it a no");
    a.ws.send(JSON.stringify({ t: "ploot", yes: true }));
    await sleep(200);
    assert.strictEqual(d.all("party").at(-1).sg, true, "the leader can turn on shared gold, and the party frames know");

    // ---- achievements: announced to the party once, and titles only for what you've earned
    b.ws.send(JSON.stringify({ t: "ach", id: "first_blood" }));
    b.ws.send(JSON.stringify({ t: "ach", id: "made_up_achievement" }));
    await sleep(400);
    b.ws.send(JSON.stringify({ t: "ach", id: "first_blood" }));
    await sleep(200);
    const achs = d.all("ach");
    assert.ok(achs.length === 1 && achs[0].name === "Bob" && achs[0].k === "First Blood", "achievements are announced to the party, once, by name");
    assert.strictEqual(achs[0].rare, false, "a common achievement isn't announced to the world");
    b.ws.send(JSON.stringify({ t: "ach", id: "slayer_5000" }));
    await sleep(400);
    assert.strictEqual(d.all("ach").find((x) => x.k === "Unstoppable")?.rare, true, "a rare achievement (50+ points) is announced to everyone");
    state(b, 145, 187, { ti: "boss_lich" });
    await sleep(300);
    assert.strictEqual(view(a).allP.find((x) => x.name === "Bob")?.ti, "", "titles you haven't earned are not shown");
    b.ws.send(JSON.stringify({ t: "ach", id: "boss_lich" }));
    await sleep(400);
    state(b, 145, 187, { ti: "boss_lich" });
    await sleep(300);
    assert.strictEqual(view(a).allP.find((x) => x.name === "Bob")?.ti, "Lichbane", "an earned title is shown to everyone (the details are resent when they change)");
    state(b, 145, 187);

    // Dana never hits the wolf but is nearby and in the party: she shares the kill.
    const snap2 = view(b);
    const wolf2 = snap2.m.filter((x) => x.id !== wolf.id).sort((p, q) => Math.hypot(p.x - 144, p.z - 187) - Math.hypot(q.x - 144, q.z - 187))[0];
    b.ws.send(JSON.stringify({ t: "hit", mid: wolf2.id, dmg: 999999 }));
    await sleep(300);
    assert.ok(d.all("kill").some((k) => k.mid === wolf2.id), "nearby party member shares kill credit");

    // ---- class abilities: stun (Shield Bash, Judgement) and vanish (Smoke Bomb)
    const wolf3 = view(b).m.find((x) => x.id !== wolf.id && x.id !== wolf2.id);
    if (wolf3) {
      a.ws.send(JSON.stringify({ t: "stun", mid: wolf3.id, dur: 2 }));
      await sleep(250);
      assert.ok(view(a).m.find((x) => x.id === wolf3.id)?.st, "stunned monsters are flagged in snapshots");
    }
    // ---- companions are shown to other players (unknown ids are dropped)
    state(a, 144, 187, { cp: "hound" });
    state(b, 145, 187, { cp: "dragon" });
    await sleep(300);
    assert.strictEqual(view(b).allP.find((x) => x.name === "Alice")?.cp, "hound", "companions are relayed");
    assert.strictEqual(view(a).allP.find((x) => x.name === "Bob")?.cp, "", "unknown companions are rejected");

    // ---- trading between Alice and Bob (standing next to each other)
    const aliceId = a.find("welcome").id, bobId = b.find("welcome").id;
    a.ws.send(JSON.stringify({ t: "treq", id: bobId }));
    await sleep(200);
    assert.strictEqual(b.find("tinv")?.name, "Alice", "Bob receives the trade request");
    b.ws.send(JSON.stringify({ t: "tacc" }));
    await sleep(200);
    assert.strictEqual(a.find("topen")?.id, bobId, "the trade window opens for Alice");
    assert.strictEqual(b.find("topen")?.id, aliceId, "the trade window opens for Bob");
    const bobPotion = b.all("inv").at(-1).bag.findIndex((x) => x.Name === "Health Potion");
    a.ws.send(JSON.stringify({ t: "toffer", slots: [], gold: 0 }));
    b.ws.send(JSON.stringify({ t: "toffer", slots: [bobPotion], gold: 0 }));
    await sleep(200);
    assert.strictEqual(JSON.parse(a.find("tupd")?.items[0] || "{}").Name, "Health Potion", "offers are relayed");
    assert.deepStrictEqual(b.find("tmine")?.slots, [bobPotion], "the offer is confirmed to its owner");
    a.ws.send(JSON.stringify({ t: "toffer", slots: [], gold: 1e9 }));
    await sleep(150);
    assert.ok(!b.all("tupd").some((u) => u.gold > 1e6), "you can't offer gold you don't have");
    b.ws.send(JSON.stringify({ t: "tok" }));
    await sleep(100);
    a.ws.send(JSON.stringify({ t: "toffer", slots: [], gold: 30 })); // a change resets acceptance
    a.ws.send(JSON.stringify({ t: "tok" }));
    await sleep(100);
    assert.ok(!a.find("tdone"), "the trade does not finish until both accept");
    const aliceGold = a.all("inv").at(-1).gold, bobGold = b.all("inv").at(-1).gold;
    b.ws.send(JSON.stringify({ t: "tok" }));
    await sleep(200);
    assert.strictEqual(b.find("tdone")?.gold, 30, "Bob receives Alice's gold");
    assert.strictEqual(b.all("inv").at(-1).gold, bobGold + 30, "the gold lands in Bob's ledger");
    assert.strictEqual(a.all("inv").at(-1).gold, aliceGold - 30, "and leaves Alice's");
    assert.ok(a.all("inv").at(-1).bag.some((x) => x.Name === "Health Potion" && x.Count >= 5), "Alice receives Bob's potions");
    assert.ok(!b.all("inv").at(-1).bag[bobPotion].Name, "Bob's potions are gone from his bags");

    // Prometheus metrics: on their own port, not on the public game port.
    assert.strictEqual((await fetch(`http://localhost:${PORT}/metrics`)).status, 404, "metrics are not on the game port");
    const prom = await (await fetch(`http://localhost:${METRICS_PORT}/metrics`)).text();
    const metric = (name) => { const m = prom.match(new RegExp(`^${name} (\\S+)$`, "m")); return m ? Number(m[1]) : NaN; };
    assert.strictEqual(metric("shadowfall_players_online"), 3, "metrics count players online (Alice, Bob, Dana)");
    assert.strictEqual(metric("shadowfall_trades_completed_total"), 1, "metrics count completed trades");
    assert.match(prom, /^shadowfall_client_errors_total\{kind="exception"\} 1$/m, "metrics count client errors by kind");
    assert.match(prom, /^shadowfall_client_errors_total\{kind="load"\} 0$/m, "client error kinds start at zero");
    assert.match(prom, /^shadowfall_logins_total\{result="new"\} 3$/m, "metrics count new characters");
    assert.match(prom, /^shadowfall_messages_received_total\{type="hello"\} \d+$/m, "metrics count messages by type");
    assert.match(prom, /^shadowfall_tick_duration_seconds_count \d+$/m, "metrics time the simulation tick");
    assert.match(prom, /^# TYPE shadowfall_tick_duration_seconds histogram$/m, "tick duration is a histogram");
    assert.ok(metric("process_resident_memory_bytes") > 0, "process metrics are exported");
    assert.strictEqual(JSON.parse(a.find("tdone")?.items[0] || "{}").Name, "Health Potion", "the trade names what was received");

    // ---- admin module: Alice is an admin (ADMINS=alice), Bob is not
    assert.strictEqual(a.find("welcome").admin, true, "admins are told so at login");
    assert.ok(!b.find("welcome").admin, "normal players are not admins");
    b.ws.send(JSON.stringify({ t: "adm", c: "spawn", type: "Goblin", n: 3 }));
    await sleep(150);
    assert.ok(b.all("sys").some((m) => /not an admin/.test(m.msg)), "non-admins can't run admin commands");
    a.ws.send(JSON.stringify({ t: "chat", msg: "/a spawn Goblin 7 3 elite" }));
    await sleep(300);
    assert.ok(a.all("sys").some((m) => /Spawned 3 Goblin \(level 7, elite\)/.test(m.msg)), "admins can spawn monsters from chat");
    assert.ok(view(a).m.filter((x) => x.n === "Goblin" && x.el).length >= 3, "spawned elites show up");
    a.ws.send(JSON.stringify({ t: "adm", c: "killall", r: 15 }));
    await sleep(300);
    assert.ok(a.all("kill").some((k) => k.name === "Goblin"), "killall gives kills");
    a.ws.send(JSON.stringify({ t: "adm", c: "time", phase: "night" }));
    await sleep(150);
    assert.ok(b.find("clock"), "changing the time is broadcast");
    // ---- seasons and weather: sent at login, changed by admins and broadcast
    const w0 = a.find("weather");
    assert.ok(w0 && w0.s >= 0 && w0.s < 4 && ["clear", "cloudy", "rain", "storm", "fog"].includes(w0.sky) && w0.left > 0, "the weather is sent at login");
    a.ws.send(JSON.stringify({ t: "chat", msg: "/a season winter" }));
    await sleep(600); // chat is limited to two lines a second
    a.ws.send(JSON.stringify({ t: "chat", msg: "/a weather storm 5" }));
    await sleep(300);
    const w1 = b.all("weather").at(-1);
    assert.ok(w1 && w1.s === 3 && w1.sky === "storm", "admins can set the season and the weather, and everyone sees it");
    b.ws.send(JSON.stringify({ t: "adm", c: "weather", kind: "clear" }));
    await sleep(150);
    assert.strictEqual(a.all("weather").at(-1).sky, "storm", "only admins change the weather");
    a.ws.send(JSON.stringify({ t: "adm", c: "who" }));
    await sleep(150);
    assert.ok(a.find("admwho").items.some((x) => x.includes("|Bob|")), "admins can list players");
    await invasionTests(a, b);
    await worldBossTests(a, b);
    await duelTests(a, b);
    await guildTests(a, b);
    await riftTests(a);
    await bountyTests(a);
    await auctionTests(a, b);
    await adminContentTests(a);
    await wallTests(a, b);

    const danaId = d.find("welcome").id;
    d.ws.send(JSON.stringify({ t: "vanish", dur: 3 }));
    await sleep(400);
    const seen = a.msgs.length;
    await sleep(1200);
    assert.ok(!a.msgs.slice(seen).some((m) => m.t === "matk" && m.tid === danaId), "vanished heroes are not attacked");

    d.ws.send(JSON.stringify({ t: "pleave" }));
    await sleep(200);
    assert.strictEqual(a.all("party").at(-1).pm.length, 2, "leaving updates the party");
    d.ws.close();

    // ---- dungeons: Bob enters the Catacombs from the graveyard entrance
    state(b, 168.5, 62.5);
    await sleep(150);
    b.ws.send(JSON.stringify({ t: "denter" }));
    await sleep(400);
    const dg = b.all("dungeon").at(-1);
    assert.ok(dg && dg.id > 0 && dg.cells && dg.w === 72, "entering the Catacombs sends a dungeon layout");
    assert.ok(dg.rooms.length >= 4 * 4 && dg.start.length === 2, "the layout has rooms and a start");
    state(b, dg.start[0], dg.start[1]);
    await sleep(500);
    const inside = view(b);
    const bobWhere = () => a.all("party").at(-1).pm.find((x) => x.name === "Bob")?.dn || "";
    for (let i = 0; i < 15 && !bobWhere(); i++) await sleep(100); // party frames update once a second
    assert.match(bobWhere(), /^The Catacombs, level 1$/, "the party sees which dungeon a member is in");
    assert.ok(!inside.p.some((p) => p.name === "Alice"), "players in a dungeon don't see the overworld");
    b.ws.send(JSON.stringify({ t: "dleave" }));
    await sleep(300);
    assert.strictEqual(b.all("dungeon").at(-1).id, 0, "leaving returns to the overworld");

    // ---- another dungeon: the Bandit Hideout (index 1), entered at its own entrance; admins can jump anywhere
    state(b, 168.5, 224.5);
    await sleep(150);
    b.ws.send(JSON.stringify({ t: "denter", d: 1, df: 2 })); // Nightmare
    await sleep(300);
    const hide = b.all("dungeon").at(-1);
    assert.strictEqual(hide.d, 1, "the hideout is its own dungeon");
    assert.strictEqual(hide.df, 2, "the dungeon opens at the requested difficulty");
    assert.strictEqual(hide.k, "Bandit Hideout", "dungeon names come from the server");
    b.ws.send(JSON.stringify({ t: "dleave" }));
    await sleep(200);
    assert.ok(Math.abs(b.all("dungeon").at(-1).z - 222) < 1, "leaving puts you back at that dungeon's entrance");
    a.ws.send(JSON.stringify({ t: "chat", msg: "/a dungeon warrens 2" }));
    await sleep(300);
    const war = a.all("dungeon").at(-1);
    assert.ok(war.d === 2 && war.l === 2, "admins can enter any dungeon at any depth");
    a.ws.send(JSON.stringify({ t: "adm", c: "dungeon", d: "citadel", l: 3 }));
    await sleep(300);
    const cit = a.all("dungeon").at(-1);
    assert.ok(cit.d === 6 && cit.k === "The Ashen Citadel" && cit.boss.length === 2, "the outer lands' dungeons have a boss at the bottom");
    a.ws.send(JSON.stringify({ t: "adm", c: "dungeon", d: "warrens", l: 2 }));
    await sleep(300);
    a.ws.send(JSON.stringify({ t: "adm", c: "regen" }));
    await sleep(300);
    assert.notStrictEqual(a.all("dungeon").at(-1).seed, war.seed, "admins can regenerate a level");
    a.ws.send(JSON.stringify({ t: "dleave" }));
    await sleep(200);

    const goldAtLogout = a.all("inv").at(-1).gold;
    a.ws.close();
    await sleep(300);
    assert.ok(b.find("leave"), "others see a logout");

    const wrong = await connect("alice", "wrong");
    assert.match(errOf(wrong), /Wrong password/, "wrong password rejected");
    wrong.ws.close();

    const again = await connect("Alice", "secret1");
    const w = again.find("welcome");
    assert.strictEqual(w.hasSave, true, "character was saved");
    assert.strictEqual(w.save.level, 3, "save data round-trips");
    assert.strictEqual(w.save.gold, goldAtLogout, "gold is saved from the server's ledger, not the client's save");
    await sleep(100);
    assert.strictEqual(again.find("inv")?.gold, goldAtLogout, "the ledger is sent at login");
    assert.strictEqual(again.find("guild")?.g?.name, "Hollow Guard", "guilds are kept: members get theirs at login");
    assert.ok(again.all("sys").some((m) => m.msg === "[HG] Raid at dusk"), "with the message of the day");

    // Same build, different map (a determinism bug): the player isn't locked out but plays on the server's map.
    const mismatch = await connect("Carl", "secret3", "288x288-deadbeef", bytes, BUILD_A);
    assert.ok(mismatch.find("grid") && mismatch.find("grid").hash === HASH && mismatch.find("welcome"), "a client with a different map in the same build adopts the server's");
    mismatch.ws.close();

    await accountTests(again);

    again.ws.close();
    b.ws.close();
    await sleep(300);

    // A newer game build with a changed map replaces the stored map; players still on the old game are saved
    // and told to reload; old clients (stale caches) are told to reload too.
    const bytes2 = Buffer.from(bytes);
    bytes2[(80 * W + 90) >> 3] |= 1 << ((80 * W + 90) & 7); // one more blocked cell
    const hash2 = worldHash(bytes2);
    const oldTimer = await connect("Dana", "secret4");
    assert.ok(oldTimer.find("welcome"), "a player on the old build is in the world");
    const updated = await connect("Carl", "secret3", hash2, bytes2, BUILD_B);
    assert.ok(updated.find("needworld") && updated.find("welcome"), "the newer build uploads its map and logs in, even with others online");
    await sleep(200);
    assert.strictEqual(oldTimer.find("error")?.reload, BUILD_B, "players on the old build are told to reload");
    const stale = await connect("Dana", "secret4");
    assert.strictEqual(stale.find("error")?.reload, BUILD_B, "a stale client is told to reload into the current build");
    const same = await connect("Erin", "secret5", hash2, bytes2, BUILD_B);
    assert.ok(same.find("welcome") && !same.find("needworld"), "clients of the current build play on the stored map");
    same.ws.close();

    // The build being served (public/build.json): older clients reload before they even log in.
    fs.writeFileSync(path.join(PUBLIC_DIR, "build.json"), JSON.stringify({ build: BUILD_C }));
    const behind = await rawHello(BUILD_B);
    assert.strictEqual(behind.reload, BUILD_C, "clients older than the served build reload right away");
    fs.rmSync(path.join(PUBLIC_DIR, "build.json"));
    updated.ws.close();
    oldTimer.ws.close();
    stale.ws.close();

    // ---- elites: a second server where every monster is a champion
    server.kill("SIGTERM");
    await sleep(300);
    const elite = spawn(process.execPath, [path.join(__dirname, "..", "server.js")], {
      env: { ...process.env, PORT: String(PORT), DATA_DIR, PUBLIC_DIR, ELITE_CHANCE: "1", DATABASE_URL: db.url },
      stdio: ["ignore", "pipe", "pipe"],
    });
    elite.stdout.on("data", (d) => (serverLog += d));
    await sleep(700);
    try {
      const e = await connect("Erin", "secret5", hash2, bytes2, BUILD_B); // the world was replaced above
      state(e, 144, 187);
      await sleep(1200);
      const nearGate = view(e).m.filter((m) => Math.hypot(m.x - 144, m.z - 191) < 12);
      assert.ok(nearGate.length > 0 && !nearGate.some((m) => m.el), "no elites in the beginner pack by the town gate, even at 100%");
      state(e, 134, 240); // out in the woods, well away from town
      await sleep(1200);
      const champ = view(e).m.filter((m) => m.el).sort((p, q) => Math.hypot(p.x - 134, p.z - 240) - Math.hypot(q.x - 134, q.z - 240))[0];
      if (champ) { state(e, champ.x + 1, champ.z); await sleep(300); } // right next to it, so the hits count
      assert.ok(champ, "elite monsters appear in snapshots");
      assert.ok(champ.af.split(",").length >= 1, "elites have affixes");
      e.ws.send(JSON.stringify({ t: "hit", mid: champ.id, dmg: 999999 }));
      // (hits are capped at 100 + 60 a level: a tough elite, Extra Health or Shielding, takes many)
      for (let i = 0; i < 160 && !e.all("kill").some((k) => k.mid === champ.id); i++) {
        const now = view(e).m.find((m) => m.id === champ.id); // it moves (it fights back): keep next to it
        if (now && i % 5 === 4) state(e, now.x + 1, now.z);
        e.ws.send(JSON.stringify({ t: "hit", mid: champ.id, dmg: 999999 }));
        await sleep(60);
      }
      const kill = e.all("kill").find((k) => k.mid === champ.id);
      assert.ok(kill && kill.el === champ.el, "elite kills are flagged for better loot");
      e.ws.close();
    } finally {
      elite.kill("SIGTERM");
    }
    ok = true;
    console.log("All smoke tests passed.");
    if (process.env.SHOW_LOG) console.log(serverLog);
  } catch (e) {
    console.error("TEST FAILED:", e.stack);
    console.error("--- server log ---\n" + serverLog);
  } finally {
    server.kill("SIGTERM");
    await sleep(200);
    fs.rmSync(DATA_DIR, { recursive: true, force: true });
    fs.rmSync(PUBLIC_DIR, { recursive: true, force: true });
    await db.drop().catch((e) => console.error("could not drop the test database:", e.message));
    process.exit(ok ? 0 : 1);
  }
}

main();
