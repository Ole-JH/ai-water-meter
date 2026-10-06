"use strict";
// End-to-end smoke test: starts the server on a temp data dir, connects two fake clients and
// exercises login, world upload, monsters, kill credit, chat, fx, parties, saves and error paths.
// Run with: npm test   (or: task server:test)

const { spawn } = require("child_process");
const fs = require("fs");
const os = require("os");
const path = require("path");
const assert = require("assert");
const WebSocket = require("ws");

const PORT = 18000 + Math.floor(Math.random() * 1000);
const URL = `ws://localhost:${PORT}/ws`;
const DATA_DIR = fs.mkdtempSync(path.join(os.tmpdir(), "shadowfall-test-"));
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// A fake 160x160 world: open field with a 4-cell wall around the edge.
const W = 160, H = 160;
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

function connect(name, pass, hash = HASH, cells = bytes, wv = 1) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(URL);
    const c = { ws, msgs: [], find: (t) => c.msgs.find((m) => m.t === t), all: (t) => c.msgs.filter((m) => m.t === t) };
    ws.on("open", () => ws.send(JSON.stringify({ t: "hello", name, pass, hash, ver: 3, wv })));
    ws.on("error", reject);
    ws.on("message", (d) => {
      const m = JSON.parse(d);
      c.msgs.push(m);
      if (m.t === "needworld") ws.send(JSON.stringify({ t: "world", hash, w: W, h: H, cells: cells.toString("base64") }));
      if (m.t === "welcome" || m.t === "error") resolve(c);
    });
  });
}

const state = (c, x, z, extra = {}) => c.ws.send(JSON.stringify({ t: "state", x, z, ry: 0, hp: 100, mhp: 100, lvl: 1, mv: false, atk: false, dead: false, ...extra }));

/** The Docker image must contain every module server.js requires (a missing COPY broke a deploy once). */
function checkDockerfile() {
  const docker = fs.readFileSync(path.join(__dirname, "..", "Dockerfile"), "utf8");
  const copiesAllJs = /^COPY \*\.js /m.test(docker);
  const src = fs.readFileSync(path.join(__dirname, "..", "server.js"), "utf8");
  for (const [, mod] of src.matchAll(/require\("\.\/([\w-]+)"\)/g))
    assert.ok(copiesAllJs || new RegExp(`COPY .*\\b${mod}\\.js\\b`).test(docker), `Dockerfile copies ${mod}.js`);
}

async function main() {
  checkDockerfile();
  const server = spawn(process.execPath, [path.join(__dirname, "..", "server.js")], {
    env: { ...process.env, PORT: String(PORT), DATA_DIR, PUBLIC_DIR: path.join(__dirname, "..", "public"), ELITE_CHANCE: "0" },
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
    state(a, 80, 113);
    state(b, 81, 113);
    await sleep(1500);
    const snap = a.all("snap").at(-1);
    assert.ok(snap.m.length > 0, "snapshot contains monsters");
    assert.ok(snap.p.some((p) => p.name === "Bob"), "snapshot contains the other player");
    assert.ok(a.all("matk").length > 0, "monsters attack players");

    const wolf = snap.m.slice().sort((p, q) => Math.hypot(p.x - 80, p.z - 113) - Math.hypot(q.x - 80, q.z - 113))[0];
    a.ws.send(JSON.stringify({ t: "hit", mid: wolf.id, dmg: 5 }));
    b.ws.send(JSON.stringify({ t: "hit", mid: wolf.id, dmg: 999999 })); // capped, but enough to kill a wolf
    await sleep(300);
    assert.strictEqual(a.find("kill")?.mid, wolf.id, "Alice gets kill credit");
    assert.strictEqual(b.find("kill")?.mid, wolf.id, "Bob gets kill credit");
    assert.ok(a.find("kill").xp > 0, "kill grants xp");

    a.ws.send(JSON.stringify({ t: "chat", msg: "hello <b>there</b>" }));
    a.ws.send(JSON.stringify({ t: "fx", k: "fireball", x: 80, z: 113, tx: 85, tz: 118 }));
    a.ws.send(JSON.stringify({ t: "save", save: { level: 3, xp: 10, gold: 55, x: 80, z: 113 } }));
    await sleep(300);
    assert.strictEqual(b.find("chat")?.msg, "hello bthere/b", "chat is relayed and sanitised");
    assert.ok(b.find("fx"), "spell effects are relayed");
    assert.ok(b.find("welcome").now > 0, "welcome carries the server clock");

    // ---- parties
    const d = await connect("Dana", "secret4");
    state(d, 82, 113);
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
    const wolf2 = snap2.m.filter((x) => x.id !== wolf.id).sort((p, q) => Math.hypot(p.x - 80, p.z - 113) - Math.hypot(q.x - 80, q.z - 113))[0];
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
    state(a, 80, 113, { cp: "hound" });
    state(b, 81, 113, { cp: "dragon" });
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
    assert.strictEqual(b.find("tdone")?.items[0], '{"Name":"Sword"}', "Bob receives Alice's item");

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
    state(b, 104.5, 27.5);
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

    a.ws.close();
    await sleep(300);
    assert.ok(b.find("leave"), "others see a logout");

    const wrong = await connect("alice", "wrong");
    assert.match(wrong.find("error").err, /Wrong password/, "wrong password rejected");

    const again = await connect("Alice", "secret1");
    const w = again.find("welcome");
    assert.strictEqual(w.hasSave, true, "character was saved");
    assert.strictEqual(w.save.gold, 55, "save data round-trips");

    const mismatch = await connect("Carl", "1234", "160x160-deadbeef", bytes, 2);
    assert.match(mismatch.find("error").err, /previous version of the world/, "a changed world is refused while others play on the old one");

    again.ws.close();
    b.ws.close();
    await sleep(300);

    // Once nobody is online, an updated client's new world replaces the old one (monsters respawn on it).
    const bytes2 = Buffer.from(bytes);
    bytes2[(80 * W + 90) >> 3] |= 1 << ((80 * W + 90) & 7); // one more blocked cell
    const hash2 = worldHash(bytes2);
    const stale = await connect("Carl", "1234", hash2, bytes2, 1);
    assert.match(stale.find("error").err, /older than this server's world/, "a client with an old layout version can't replace the world");
    const updated = await connect("Carl", "1234", hash2, bytes2, 2);
    assert.ok(updated.find("needworld") && updated.find("welcome"), "the updated client uploads the new world and logs in");
    const old = await connect("Dana", "secret4");
    assert.match(old.find("error").err, /older than this server's world/, "old clients are refused after the world changed");
    updated.ws.close();
    old.ws.close();

    // ---- elites: a second server where every monster is a champion
    server.kill("SIGTERM");
    await sleep(300);
    const elite = spawn(process.execPath, [path.join(__dirname, "..", "server.js")], {
      env: { ...process.env, PORT: String(PORT), DATA_DIR, PUBLIC_DIR: path.join(__dirname, "..", "public"), ELITE_CHANCE: "1" },
      stdio: ["ignore", "pipe", "pipe"],
    });
    elite.stdout.on("data", (d) => (serverLog += d));
    await sleep(700);
    try {
      const e = await connect("Erin", "secret5", hash2, bytes2, 2); // the world was replaced above
      state(e, 80, 113);
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
  } catch (e) {
    console.error("TEST FAILED:", e.stack);
    console.error("--- server log ---\n" + serverLog);
  } finally {
    server.kill("SIGTERM");
    await sleep(200);
    fs.rmSync(DATA_DIR, { recursive: true, force: true });
    process.exit(ok ? 0 : 1);
  }
}

main();
