#!/usr/bin/env node
// Load test: N fake players log in, run around the village and the wilds, fight monsters and chat, while we watch the
// server's tick time (from its Prometheus metrics) and how evenly snapshots arrive.
//
//   node tools/loadtest/bots.js [--url ws://localhost:8080/ws] [--bots 100] [--seconds 60] [--metrics http://localhost:9464/metrics]
//
// Against a fresh server the first bot uploads a flat test world. Against a real server the bots take the server's map.
// Bot accounts are called lb<run><n>; delete them afterwards on a real server (task accounts).
"use strict";
const WebSocket = require(require.resolve("ws", { paths: [require("path").join(__dirname, "..", "..", "server")] }));

const args = Object.fromEntries(process.argv.slice(2).reduce((a, v, i, all) => (v.startsWith("--") ? [...a, [v.slice(2), all[i + 1]]] : a), []));
const URL = args.url || "ws://localhost:8080/ws";
const BOTS = Number(args.bots) || 100;
const SECONDS = Number(args.seconds) || 60;
const METRICS = args.metrics || "";
const RUN = Math.random().toString(36).slice(2, 7);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// A flat 288x288 test world with a wall round the edge (only used if the server has no map yet).
const W = 288, H = 288;
const cells = Buffer.alloc((W * H) / 8);
for (let y = 0; y < H; y++)
  for (let x = 0; x < W; x++)
    if (Math.min(x, y, W - 1 - x, H - 1 - y) < 4) { const i = y * W + x; cells[i >> 3] |= 1 << (i & 7); }
let h = 2166136261;
for (const b of cells) { h ^= b; h = Math.imul(h, 16777619) >>> 0; }
const HASH = `${W}x${H}-${h.toString(16).padStart(8, "0")}`;

const stats = { inWorld: 0, snaps: 0, gaps: [], kills: 0, errors: 0, bytes: 0 };

async function buildStamp() {
  try {
    const base = URL.replace(/^ws/, "http").replace(/\/ws$/, "");
    const r = await fetch(base + "/build.json");
    if (r.ok) return (await r.json()).build || "";
  } catch (e) { /* no build.json: a bare test server */ }
  return "";
}

function bot(n, build) {
  return new Promise((resolve) => {
    const ws = new WebSocket(URL);
    const name = `lb${RUN}${n}`;
    const me = { x: 144.5, z: 141.5, goal: null, lastSnap: 0, monsters: [], ws };
    ws.on("error", () => { stats.errors++; resolve(me); });
    ws.on("open", () => ws.send(JSON.stringify({ t: "hello", hash: HASH, ver: 5, wv: 4, build })));
    ws.on("message", (d) => {
      stats.bytes += d.length;
      const m = JSON.parse(d);
      switch (m.t) {
        case "hi": ws.send(JSON.stringify({ t: "register", user: name, pass: "loadtest" })); break;
        case "account": ws.send(JSON.stringify({ t: "create", name, look: ["Knight", "Barbarian", "Mage", "Rogue"][n % 4] })); break;
        case "needworld": ws.send(JSON.stringify({ t: "world", hash: HASH, w: W, h: H, cells: cells.toString("base64") })); break;
        case "grid": ws.send(JSON.stringify({ t: "world", hash: m.hash, w: m.w, h: m.h, cells: "" })); break;
        case "welcome": stats.inWorld++; me.inWorld = true; resolve(me); break;
        case "snap": {
          const t = Date.now();
          if (me.lastSnap) stats.gaps.push(t - me.lastSnap);
          me.lastSnap = t;
          stats.snaps++;
          me.monsters = m.m || [];
          break;
        }
        case "kill": stats.kills++; break;
        case "error": case "autherr": stats.errors++; resolve(me); break;
      }
    });
  });
}

/** Wander between random points (village and wilds), hit the nearest monster now and then, chat rarely. */
function act(me, dt) {
  if (!me.inWorld || me.ws.readyState !== WebSocket.OPEN) return;
  if (!me.goal || Math.hypot(me.goal.x - me.x, me.goal.z - me.z) < 1) me.goal = { x: 100 + Math.random() * 90, z: 100 + Math.random() * 130 };
  const dx = me.goal.x - me.x, dz = me.goal.z - me.z, d = Math.hypot(dx, dz), step = Math.min(d, 6.2 * dt);
  me.x += (dx / d) * step;
  me.z += (dz / d) * step;
  me.ws.send(JSON.stringify({ t: "state", x: me.x, z: me.z, ry: 0, hp: 500, mhp: 500, mp: 100, mmp: 100, lvl: 10, mv: true, atk: false, dead: false, mdl: "Knight", wk: "sword" }));
  if (Math.random() < 0.05 && me.monsters.length) {
    const near = me.monsters.reduce((a, b) => (Math.hypot(a.x - me.x, a.z - me.z) < Math.hypot(b.x - me.x, b.z - me.z) ? a : b));
    me.ws.send(JSON.stringify({ t: "hit", mid: near.id, dmg: 120 }));
  }
  if (Math.random() < 0.002) me.ws.send(JSON.stringify({ t: "chat", msg: "loadtest says hi" }));
}

async function metrics() {
  if (!METRICS) return null;
  try {
    const text = await (await fetch(METRICS)).text();
    const get = (re) => { const m = text.match(re); return m ? Number(m[1]) : NaN; };
    const buckets = [...text.matchAll(/^shadowfall_tick_duration_seconds_bucket\{le="([^"]+)"\} (\S+)$/gm)].map((m) => [m[1], Number(m[2])]);
    return {
      sum: get(/^shadowfall_tick_duration_seconds_sum (\S+)$/m), count: get(/^shadowfall_tick_duration_seconds_count (\S+)$/m),
      lag: get(/^nodejs_eventloop_lag_p99_seconds (\S+)$/m), rss: get(/^process_resident_memory_bytes (\S+)$/m), buckets,
    };
  } catch (e) { return null; }
}

const pct = (arr, p) => { if (!arr.length) return 0; const s = [...arr].sort((a, b) => a - b); return s[Math.min(s.length - 1, Math.floor(p * s.length))]; };

(async () => {
  const build = await buildStamp();
  console.log(`Connecting ${BOTS} bots to ${URL} (build ${build || "none"})...`);
  const bots = [];
  // the first one alone (it may have to upload the world), then the rest in waves
  bots.push(await bot(0, build));
  for (let i = 1; i < BOTS; i += 20) bots.push(...(await Promise.all(Array.from({ length: Math.min(20, BOTS - i) }, (_, k) => bot(i + k, build)))));
  console.log(`${stats.inWorld} in the world, ${stats.errors} errors`);
  const m0 = await metrics();
  stats.gaps = [];
  stats.bytes = 0;
  const t0 = Date.now();
  while (Date.now() - t0 < SECONDS * 1000) {
    for (const b of bots) act(b, 0.1);
    await sleep(100);
  }
  const m1 = await metrics();
  const secs = (Date.now() - t0) / 1000;
  console.log(`\n${BOTS} bots for ${secs.toFixed(0)} s:`);
  console.log(`  snapshots: ${(stats.gaps.length / secs / Math.max(1, stats.inWorld)).toFixed(1)}/s per bot, gap p50 ${pct(stats.gaps, 0.5)} ms, p99 ${pct(stats.gaps, 0.99)} ms`);
  console.log(`  downstream: ${(stats.bytes / secs / 1024).toFixed(0)} KB/s total, ${(stats.bytes / secs / 1024 / Math.max(1, stats.inWorld)).toFixed(1)} KB/s per bot`);
  console.log(`  kills: ${stats.kills}`);
  if (m0 && m1) {
    const n = m1.count - m0.count;
    console.log(`  server tick: mean ${(((m1.sum - m0.sum) / n) * 1000).toFixed(2)} ms over ${n} ticks, event loop lag p99 ${(m1.lag * 1000).toFixed(1)} ms, RSS ${(m1.rss / 1e6).toFixed(0)} MB`);
    const over = m1.buckets.map(([le, c], i) => [le, c - (m0.buckets[i] ? m0.buckets[i][1] : 0)]);
    console.log(`  tick histogram: ${over.map(([le, c]) => `<=${le}s: ${c}`).join("  ")}`);
  }
  for (const b of bots) b.ws.close();
  process.exit(0);
})();
