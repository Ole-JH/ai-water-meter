"use strict";
// End-to-end smoke test: starts the server on a temp data dir, connects two fake clients and
// exercises login, world upload, monsters, kill credit, chat, fx, saves and error paths.
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

function connect(name, pass, hash = HASH) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(URL);
    const c = { ws, msgs: [], find: (t) => c.msgs.find((m) => m.t === t), all: (t) => c.msgs.filter((m) => m.t === t) };
    ws.on("open", () => ws.send(JSON.stringify({ t: "hello", name, pass, hash, ver: 1 })));
    ws.on("error", reject);
    ws.on("message", (d) => {
      const m = JSON.parse(d);
      c.msgs.push(m);
      if (m.t === "needworld") ws.send(JSON.stringify({ t: "world", hash, w: W, h: H, cells: bytes.toString("base64") }));
      if (m.t === "welcome" || m.t === "error") resolve(c);
    });
  });
}

const state = (c, x, z) => c.ws.send(JSON.stringify({ t: "state", x, z, ry: 0, hp: 100, mhp: 100, lvl: 1, mv: false, atk: false, dead: false }));

async function main() {
  const server = spawn(process.execPath, [path.join(__dirname, "..", "server.js")], {
    env: { ...process.env, PORT: String(PORT), DATA_DIR, PUBLIC_DIR: path.join(__dirname, "..", "public") },
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
    state(a, 80, 104);
    state(b, 81, 104);
    await sleep(1500);
    const snap = a.all("snap").at(-1);
    assert.ok(snap.m.length > 0, "snapshot contains monsters");
    assert.ok(snap.p.some((p) => p.name === "Bob"), "snapshot contains the other player");
    assert.ok(a.all("matk").length > 0, "monsters attack players");

    const wolf = snap.m.slice().sort((p, q) => Math.hypot(p.x - 80, p.z - 104) - Math.hypot(q.x - 80, q.z - 104))[0];
    a.ws.send(JSON.stringify({ t: "hit", mid: wolf.id, dmg: 5 }));
    b.ws.send(JSON.stringify({ t: "hit", mid: wolf.id, dmg: 999999 })); // capped, but enough to kill a wolf
    await sleep(300);
    assert.strictEqual(a.find("kill")?.mid, wolf.id, "Alice gets kill credit");
    assert.strictEqual(b.find("kill")?.mid, wolf.id, "Bob gets kill credit");
    assert.ok(a.find("kill").xp > 0, "kill grants xp");

    a.ws.send(JSON.stringify({ t: "chat", msg: "hello <b>there</b>" }));
    a.ws.send(JSON.stringify({ t: "fx", k: "fireball", x: 80, z: 104, tx: 85, tz: 110 }));
    a.ws.send(JSON.stringify({ t: "save", save: { level: 3, xp: 10, gold: 55, x: 80, z: 104 } }));
    await sleep(300);
    assert.strictEqual(b.find("chat")?.msg, "hello bthere/b", "chat is relayed and sanitised");
    assert.ok(b.find("fx"), "spell effects are relayed");

    a.ws.close();
    await sleep(300);
    assert.ok(b.find("leave"), "others see a logout");

    const wrong = await connect("alice", "wrong");
    assert.match(wrong.find("error").err, /Wrong password/, "wrong password rejected");

    const again = await connect("Alice", "secret1");
    const w = again.find("welcome");
    assert.strictEqual(w.hasSave, true, "character was saved");
    assert.strictEqual(w.save.gold, 55, "save data round-trips");

    const mismatch = await connect("Carl", "1234", "160x160-deadbeef");
    assert.match(mismatch.find("error").err, /doesn't match/, "mismatched client build rejected");

    again.ws.close();
    b.ws.close();
    ok = true;
    console.log("All smoke tests passed.");
  } catch (e) {
    console.error("TEST FAILED:", e.message);
    console.error("--- server log ---\n" + serverLog);
  } finally {
    server.kill("SIGTERM");
    await sleep(200);
    fs.rmSync(DATA_DIR, { recursive: true, force: true });
    process.exit(ok ? 0 : 1);
  }
}

main();
