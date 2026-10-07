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

  for (const x of [c, other, r1, r2, r3, legacy]) x.ws.close();
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

async function main() {
  checkDockerfile();
  checkGamedata();
  const db = await testDatabase();
  if (db.url) console.log("Testing against PostgreSQL");
  writeLegacyCharacter("Oldtimer", "oldpass", { level: 7, gold: 99, look: "Mage", x: 144, z: 150 });
  const server = spawn(process.execPath, [path.join(__dirname, "..", "server.js")], {
    env: { ...process.env, PORT: String(PORT), DATA_DIR, PUBLIC_DIR, ELITE_CHANCE: "0", ADMINS: "alice", METRICS_PORT: String(METRICS_PORT), DATABASE_URL: db.url },
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
    const page = await fetch(`http://localhost:${PORT}/`);
    assert.strictEqual(page.status, 200, "index page served");

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

    // ---- achievements: announced to the party once, and titles only for what you've earned
    b.ws.send(JSON.stringify({ t: "ach", id: "first_blood" }));
    b.ws.send(JSON.stringify({ t: "ach", id: "made_up_achievement" }));
    await sleep(400);
    b.ws.send(JSON.stringify({ t: "ach", id: "first_blood" }));
    await sleep(200);
    const achs = d.all("ach");
    assert.ok(achs.length === 1 && achs[0].name === "Bob" && achs[0].k === "First Blood", "achievements are announced to the party, once, by name");
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
    assert.match(a.all("party").at(-1).pm.find((x) => x.name === "Bob")?.dn || "", /^The Catacombs, level 1$/, "the party sees which dungeon a member is in");
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
      const champ = view(e).m.find((m) => m.el);
      assert.ok(champ, "elite monsters appear in snapshots");
      assert.ok(champ.af.split(",").length >= 1, "elites have affixes");
      e.ws.send(JSON.stringify({ t: "hit", mid: champ.id, dmg: 999999 }));
      for (let i = 0; i < 40 && !e.all("kill").some((k) => k.mid === champ.id); i++) {
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
