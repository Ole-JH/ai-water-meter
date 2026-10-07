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
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// A fake 288x288 world (the real size): open field with a 4-cell wall around the edge.
const W = 288, H = 288;
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
function connect(name, pass, hash = HASH, cells = bytes, wv = 1) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(URL);
    const c = { ws, msgs: [], find: (t) => c.msgs.find((m) => m.t === t), all: (t) => c.msgs.filter((m) => m.t === t) };
    ws.on("open", () => ws.send(JSON.stringify({ t: "hello", hash, ver: 5, wv })));
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
      if (m.t === "welcome" || m.t === "error" || (m.t === "autherr" && !/No account/.test(m.err))) resolve(c);
    });
  });
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
    ws.on("open", async () => { c.send({ t: "hello", hash: HASH, ver: 5, wv: 1 }); await c.next("hi"); resolve(c); });
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

async function main() {
  checkDockerfile();
  const db = await testDatabase();
  if (db.url) console.log("Testing against PostgreSQL");
  writeLegacyCharacter("Oldtimer", "oldpass", { level: 7, gold: 99, look: "Mage", x: 144, z: 150 });
  const server = spawn(process.execPath, [path.join(__dirname, "..", "server.js")], {
    env: { ...process.env, PORT: String(PORT), DATA_DIR, PUBLIC_DIR: path.join(__dirname, "..", "public"), ELITE_CHANCE: "0", ADMINS: "alice", METRICS_PORT: String(METRICS_PORT), DATABASE_URL: db.url },
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
    const snap = a.all("snap").at(-1);
    assert.ok(snap.m.length > 0, "snapshot contains monsters");
    assert.ok(snap.p.some((p) => p.name === "Bob"), "snapshot contains the other player");
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

    b.ws.send(JSON.stringify({ t: "chat", msg: "/p group up" }));
    b.ws.send(JSON.stringify({ t: "pshare", q: "wolves" }));
    await sleep(300);
    assert.strictEqual(d.all("chat").find((m) => m.ch === "p")?.msg, "group up", "party chat reaches members");
    assert.strictEqual(d.find("qshare")?.k, "wolves", "quests can be shared with the party");

    // Dana never hits the wolf but is nearby and in the party: she shares the kill.
    const snap2 = b.all("snap").at(-1);
    const wolf2 = snap2.m.filter((x) => x.id !== wolf.id).sort((p, q) => Math.hypot(p.x - 144, p.z - 187) - Math.hypot(q.x - 144, q.z - 187))[0];
    b.ws.send(JSON.stringify({ t: "hit", mid: wolf2.id, dmg: 999999 }));
    await sleep(300);
    assert.ok(d.all("kill").some((k) => k.mid === wolf2.id), "nearby party member shares kill credit");

    // ---- class abilities: stun (Shield Bash, Judgement) and vanish (Smoke Bomb)
    const wolf3 = b.all("snap").at(-1).m.find((x) => x.id !== wolf.id && x.id !== wolf2.id);
    if (wolf3) {
      a.ws.send(JSON.stringify({ t: "stun", mid: wolf3.id, dur: 2 }));
      await sleep(250);
      assert.ok(a.all("snap").at(-1).m.find((x) => x.id === wolf3.id)?.st, "stunned monsters are flagged in snapshots");
    }
    // ---- companions are shown to other players (unknown ids are dropped)
    state(a, 144, 187, { cp: "hound" });
    state(b, 145, 187, { cp: "dragon" });
    await sleep(300);
    assert.strictEqual(b.all("snap").at(-1).p.find((x) => x.name === "Alice")?.cp, "hound", "companions are relayed");
    assert.strictEqual(a.all("snap").at(-1).p.find((x) => x.name === "Bob")?.cp, "", "unknown companions are rejected");

    // ---- trading between Alice and Bob (standing next to each other)
    const aliceId = a.find("welcome").id, bobId = b.find("welcome").id;
    a.ws.send(JSON.stringify({ t: "treq", id: bobId }));
    await sleep(200);
    assert.strictEqual(b.find("tinv")?.name, "Alice", "Bob receives the trade request");
    b.ws.send(JSON.stringify({ t: "tacc" }));
    await sleep(200);
    assert.strictEqual(a.find("topen")?.id, bobId, "the trade window opens for Alice");
    assert.strictEqual(b.find("topen")?.id, aliceId, "the trade window opens for Bob");
    a.ws.send(JSON.stringify({ t: "toffer", items: ['{"Name":"Sword"}'], gold: 0 }));
    b.ws.send(JSON.stringify({ t: "toffer", items: [], gold: 25 }));
    await sleep(200);
    assert.strictEqual(b.find("tupd")?.items[0], '{"Name":"Sword"}', "offers are relayed");
    a.ws.send(JSON.stringify({ t: "tok" }));
    await sleep(100);
    b.ws.send(JSON.stringify({ t: "toffer", items: [], gold: 30 })); // a change resets acceptance
    a.ws.send(JSON.stringify({ t: "tok" }));
    await sleep(100);
    assert.ok(!a.find("tdone"), "the trade does not finish until both accept");
    b.ws.send(JSON.stringify({ t: "tok" }));
    await sleep(200);
    assert.strictEqual(a.find("tdone")?.gold, 30, "Alice receives Bob's gold");

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
    assert.strictEqual(b.find("tdone")?.items[0], '{"Name":"Sword"}', "Bob receives Alice's item");

    // ---- admin module: Alice is an admin (ADMINS=alice), Bob is not
    assert.strictEqual(a.find("welcome").admin, true, "admins are told so at login");
    assert.ok(!b.find("welcome").admin, "normal players are not admins");
    b.ws.send(JSON.stringify({ t: "adm", c: "spawn", type: "Goblin", n: 3 }));
    await sleep(150);
    assert.ok(b.all("sys").some((m) => /not an admin/.test(m.msg)), "non-admins can't run admin commands");
    a.ws.send(JSON.stringify({ t: "chat", msg: "/a spawn Goblin 7 3 elite" }));
    await sleep(300);
    assert.ok(a.all("sys").some((m) => /Spawned 3 Goblin \(level 7, elite\)/.test(m.msg)), "admins can spawn monsters from chat");
    assert.ok(a.all("snap").at(-1).m.filter((x) => x.n === "Goblin" && x.el).length >= 3, "spawned elites show up");
    a.ws.send(JSON.stringify({ t: "adm", c: "killall", r: 15 }));
    await sleep(300);
    assert.ok(a.all("kill").some((k) => k.name === "Goblin"), "killall gives kills");
    a.ws.send(JSON.stringify({ t: "adm", c: "time", phase: "night" }));
    await sleep(150);
    assert.ok(b.find("clock"), "changing the time is broadcast");
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
    const inside = b.all("snap").at(-1);
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
    a.ws.send(JSON.stringify({ t: "adm", c: "regen" }));
    await sleep(300);
    assert.notStrictEqual(a.all("dungeon").at(-1).seed, war.seed, "admins can regenerate a level");
    a.ws.send(JSON.stringify({ t: "dleave" }));
    await sleep(200);

    a.ws.close();
    await sleep(300);
    assert.ok(b.find("leave"), "others see a logout");

    const wrong = await connect("alice", "wrong");
    assert.match(errOf(wrong), /Wrong password/, "wrong password rejected");
    wrong.ws.close();

    const again = await connect("Alice", "secret1");
    const w = again.find("welcome");
    assert.strictEqual(w.hasSave, true, "character was saved");
    assert.strictEqual(w.save.gold, 55, "save data round-trips");

    const mismatch = await connect("Carl", "secret3", "288x288-deadbeef", bytes, 2);
    assert.match(errOf(mismatch), /previous version of the world/, "a changed world is refused while others play on the old one");
    mismatch.ws.close();

    await accountTests(again);

    again.ws.close();
    b.ws.close();
    await sleep(300);

    // Once nobody is online, an updated client's new world replaces the old one (monsters respawn on it).
    const bytes2 = Buffer.from(bytes);
    bytes2[(80 * W + 90) >> 3] |= 1 << ((80 * W + 90) & 7); // one more blocked cell
    const hash2 = worldHash(bytes2);
    const stale = await connect("Carl", "secret3", hash2, bytes2, 1);
    assert.match(errOf(stale), /older than this server's world/, "a client with an old layout version can't replace the world");
    const updated = await connect("Carl", "secret3", hash2, bytes2, 2);
    assert.ok(updated.find("needworld") && updated.find("welcome"), "the updated client uploads the new world and logs in");
    const old = await connect("Dana", "secret4");
    assert.match(errOf(old), /older than this server's world/, "old clients are refused after the world changed");
    updated.ws.close();
    old.ws.close();

    // ---- elites: a second server where every monster is a champion
    server.kill("SIGTERM");
    await sleep(300);
    const elite = spawn(process.execPath, [path.join(__dirname, "..", "server.js")], {
      env: { ...process.env, PORT: String(PORT), DATA_DIR, PUBLIC_DIR: path.join(__dirname, "..", "public"), ELITE_CHANCE: "1", DATABASE_URL: db.url },
      stdio: ["ignore", "pipe", "pipe"],
    });
    elite.stdout.on("data", (d) => (serverLog += d));
    await sleep(700);
    try {
      const e = await connect("Erin", "secret5", hash2, bytes2, 2); // the world was replaced above
      state(e, 144, 187);
      await sleep(1200);
      const champ = e.all("snap").at(-1).m.find((m) => m.el);
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
    await db.drop().catch((e) => console.error("could not drop the test database:", e.message));
    process.exit(ok ? 0 : 1);
  }
}

main();
