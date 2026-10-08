"use strict";
// Shadowfall game server.
//  - Serves the Unity WebGL build from ./public over HTTP
//  - Runs the shared world over a WebSocket at /ws: accounts, character saves,
//    server-simulated monsters (AI, pathfinding, respawns), kill credit, chat and spell effects.

const http = require("http");
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");
const { WebSocketServer } = require("ws");
const { MONSTERS, SPAWNERS, TOWNS, OLD_SIZE, SPAWN, DUNGEONS, BALANCE, DIFFICULTIES, EMOTES, map: designToWorld } = require("./content");
const dungeonGen = require("./dungeon");
const metrics = require("./metrics");
const { createStore, Taken } = require("./store");
const I = require("./items");
const { Weather, SEASONS, KINDS: WEATHER_KINDS } = require("./weather");
const A = require("./accounts");
const createInvasions = require("./invasion");
const createWorldBosses = require("./worldboss");
const createDuels = require("./duel");
const createGuilds = require("./guild");
const createRifts = require("./rift");

const PORT = parseInt(process.env.PORT || "7341", 10);
// Behind a reverse proxy, take the client's address from X-Forwarded-For (for login rate limits).
const TRUST_PROXY = process.env.TRUST_PROXY === "1";
const DATA_DIR = path.resolve(process.env.DATA_DIR || path.join(__dirname, "data"));
const PUBLIC_DIR = path.resolve(process.env.PUBLIC_DIR || path.join(__dirname, "public"));
const WORLD_FILE = path.join(DATA_DIR, "world.json");
const PROTOCOL_VERSION = 5;
const TICK = 0.1; // seconds
const HERO_MODELS = ["Knight", "Barbarian", "Mage", "Rogue"]; // must match CharacterLook.HeroModels
const MONSTER_VIEW = 45;
const PLAYER_VIEW = 60;

// In Docker the container starts as root so it can take ownership of the bind-mounted data
// folder (which Docker creates as root), then drops to an unprivileged user before serving.
if (process.env.DROP_PRIVILEGES === "1" && process.getuid && process.getuid() === 0) {
  const uid = parseInt(process.env.APP_UID || "1000", 10), gid = parseInt(process.env.APP_GID || "1000", 10);
  fs.mkdirSync(DATA_DIR, { recursive: true });
  const chownTree = (p) => {
    fs.chownSync(p, uid, gid);
    if (fs.statSync(p).isDirectory()) for (const f of fs.readdirSync(p)) chownTree(path.join(p, f));
  };
  chownTree(DATA_DIR);
  process.setgid(gid);
  process.setuid(uid);
}

fs.mkdirSync(DATA_DIR, { recursive: true });

const log = (...a) => console.log(new Date().toISOString(), ...a);
const rand = (a, b) => a + Math.random() * (b - a);
const randInt = (a, b) => Math.floor(rand(a, b + 1));
const dist = (ax, az, bx, bz) => Math.hypot(ax - bx, az - bz);
const r2 = (v) => Math.round(v * 100) / 100;
// The Crypt of the Lich (world cells, as WorldGenerator.Crypt): its monsters drop like a dungeon's.
const CRYPT = { x0: 132, z0: 9, x1: 157, z1: 28 };
const lootSource = (m) => (m.inst || (m.x >= CRYPT.x0 && m.x < CRYPT.x1 && m.z >= CRYPT.z0 && m.z < CRYPT.z1) || (m.x >= OLD_SIZE && m.z >= OLD_SIZE) ? "deep" : "surface");
const inTown = (x, z) => TOWNS.some((t) => x >= t.x0 && x < t.x1 && z >= t.z0 && z < t.z1);
const now = () => Date.now() / 1000;

// =====================================================================================
// HTTP: static files for the WebGL build
// =====================================================================================

const MIME = {
  ".html": "text/html; charset=utf-8", ".js": "application/javascript", ".wasm": "application/wasm",
  ".data": "application/octet-stream", ".json": "application/json", ".css": "text/css",
  ".png": "image/png", ".jpg": "image/jpeg", ".ico": "image/x-icon", ".svg": "image/svg+xml",
  ".unityweb": "application/octet-stream", ".txt": "text/plain",
};

// Errors from players' browsers and games (index.html's sfReport, ErrorReporter.cs): logged as one line each
// ("client error ..." in Loki) and counted by kind (load, js, exception, error). At most 30 a minute per address.
const CLIENT_ERROR_KINDS = new Set(["load", "js", "exception", "error"]);
const clientErrorBudget = new Map(); // ip -> { n, until }
function clientError(req, res) {
  const ip = String((TRUST_PROXY && req.headers["x-forwarded-for"]) || req.socket.remoteAddress || "").split(",")[0].trim();
  const t = Date.now(), b = clientErrorBudget.get(ip);
  if (!b || t > b.until) clientErrorBudget.set(ip, { n: 1, until: t + 60000 });
  else if (++b.n > 30) { req.resume(); res.writeHead(429); return res.end(); }
  if (clientErrorBudget.size > 5000) clientErrorBudget.clear();
  let body = "";
  req.setEncoding("utf8");
  req.on("data", (d) => { body += d; if (body.length > 8192) req.destroy(); });
  req.on("end", () => {
    res.writeHead(204);
    res.end();
    let e;
    try { e = JSON.parse(body); } catch { return; }
    const kind = CLIENT_ERROR_KINDS.has(e && e.kind) ? e.kind : "other";
    const clean = (v, n) => String(v || "").replace(/[\r\n]+/g, " | ").replace(/[^\x20-\x7e\u00a0-\uffff]/g, "").slice(0, n);
    M.clientErrors.inc({ kind });
    const page = Number.isFinite(e.page) ? `, ${Math.round(e.page)} s after loading` : "";
    log(`client error [${kind}] build ${clean(e.build, 24) || "?"}${page}: ${clean(e.msg, 500)}` +
      (e.stack ? ` || ${clean(e.stack, 1200)}` : "") + ` || ${clean(e.ua, 160)}`);
  });
}

const server = http.createServer((req, res) => {
  const url = new URL(req.url, "http://localhost");
  if (url.pathname === "/client-error" && req.method === "POST") return clientError(req, res);
  if (url.pathname === "/healthz") {
    res.writeHead(200, { "Content-Type": "application/json" });
    let online = 0, dungeons = 0;
    for (const o of sessions.values()) if (o.inWorld) { online++; if (o.inst) dungeons++; }
    // players: open connections; online: heroes in the world (dungeons: of them, underground). Read by the Homepage dashboard.
    return res.end(JSON.stringify({ ok: true, players: sessions.size, online, dungeons, monsters: monsters.size, world: !!world,
      build: latestBuild() || "none", invasion: invasions.active(), worldBoss: worldBosses.active(), uptime: Math.round(process.uptime()) }));
  }

  let rel = decodeURIComponent(url.pathname);
  if (rel.endsWith("/")) rel += "index.html";
  const file = path.normalize(path.join(PUBLIC_DIR, rel));
  if (!file.startsWith(PUBLIC_DIR)) { res.writeHead(403); return res.end(); }

  fs.stat(file, (err, st) => {
    if (err || !st.isFile()) { res.writeHead(404); return res.end("Not found"); }
    // Unity WebGL emits pre-compressed files (*.gz / *.br); serve them with the right headers.
    let name = file, encoding = null;
    if (name.endsWith(".gz")) { encoding = "gzip"; name = name.slice(0, -3); }
    else if (name.endsWith(".br")) { encoding = "br"; name = name.slice(0, -3); }
    const headers = { "Content-Type": MIME[path.extname(name).toLowerCase()] || "application/octet-stream" };
    if (encoding) headers["Content-Encoding"] = encoding;
    // Always revalidate, so a new client build shows up on the next page load (304 if unchanged).
    headers["Cache-Control"] = "no-cache";
    headers["Last-Modified"] = st.mtime.toUTCString();
    const since = Date.parse(req.headers["if-modified-since"] || "");
    if (!Number.isNaN(since) && Math.floor(st.mtimeMs / 1000) <= Math.floor(since / 1000)) {
      res.writeHead(304, { "Cache-Control": "no-cache", "Last-Modified": headers["Last-Modified"] });
      return res.end();
    }
    res.writeHead(200, headers);
    fs.createReadStream(file).pipe(res);
  });
});

// =====================================================================================
// World grid (uploaded once by the first client; identical for every client build)
// =====================================================================================

let world = null; // { hash, w, h, blocked: Uint8Array }

function decodeWorld(w, h, b64) {
  const bytes = Buffer.from(b64, "base64");
  if (bytes.length !== Math.ceil((w * h) / 8)) return null;
  let hash = 2166136261;
  for (const b of bytes) { hash ^= b; hash = Math.imul(hash, 16777619) >>> 0; }
  const blocked = new Uint8Array(w * h);
  for (let i = 0; i < w * h; i++) blocked[i] = (bytes[i >> 3] >> (i & 7)) & 1;
  return { hash: `${w}x${h}-${hash.toString(16).padStart(8, "0")}`, w, h, blocked };
}

function loadWorld() {
  try {
    const j = JSON.parse(fs.readFileSync(WORLD_FILE, "utf8"));
    world = decodeWorld(j.w, j.h, j.cells);
    if (world) { world.cells = j.cells; world.build = String(j.build || ""); }
    if (world) log(`Loaded world ${world.hash} (from game build ${world.build || "unknown"})`);
  } catch { /* no world yet */ }
}

function saveWorld() {
  fs.writeFileSync(WORLD_FILE, JSON.stringify({ w: world.w, h: world.h, cells: world.cells, hash: world.hash, build: world.build }));
}

// =====================================================================================
// Game versions. The map is generated by the game itself, so the server learns it from the players' game:
// a NEWER build (its stamp, "yyyy.MM.dd-HHmmss", sorts by time) replaces the stored map, an OLDER one (a stale
// browser cache) is told to reload, and the same build plays on the stored map. The build being served is read
// from public/build.json (written by the WebGL build), so stale clients reload even before they log in.
// =====================================================================================

const BUILD_FILE = path.join(PUBLIC_DIR, "build.json");
let servedBuild = "", servedBuildStamp = -1;
function latestBuild() {
  try {
    const st = fs.statSync(BUILD_FILE);
    if (st.mtimeMs !== servedBuildStamp) {
      servedBuildStamp = st.mtimeMs;
      servedBuild = String(JSON.parse(fs.readFileSync(BUILD_FILE, "utf8")).build || "");
    }
  } catch { servedBuild = ""; servedBuildStamp = -1; }
  return servedBuild;
}
const newerBuild = (a, b) => String(a || "") > String(b || "");

/** Tells an out-of-date client to reload the page (it does so by itself, bypassing the cache). */
function sendReload(s, build) {
  safeSend(s, JSON.stringify({ t: "error", err: "A new version of Shadowfall is out. Reloading...", reload: build }));
  setTimeout(() => s.ws.close(), 100);
}

// The grid that pathfinding and walkability use right now: the overworld, or a dungeon instance's grid
// while that instance's monsters are updated (see useGrid). Node is single-threaded, so this is safe.
let G = null;
const useGrid = (inst) => { G = inst ? (instances.get(inst) || {}).grid || null : world; return G; };
const blockedAt = (x, y) => !G || x < 0 || y < 0 || x >= G.w || y >= G.h || G.blocked[y * G.w + x] === 1;
const walkable = (x, z) => !blockedAt(Math.floor(x), Math.floor(z));

function lineOfSight(ax, az, bx, bz, clearance = 0.3) {
  const dx = bx - ax, dz = bz - az, d = Math.hypot(dx, dz);
  if (d < 0.001) return walkable(ax, az);
  const nx = dx / d, nz = dz / d, px = -nz * clearance, pz = nx * clearance;
  const steps = Math.ceil(d / 0.25);
  for (let i = 0; i <= steps; i++) {
    const x = ax + nx * (d * i / steps), z = az + nz * (d * i / steps);
    if (!walkable(x, z) || !walkable(x + px, z + pz) || !walkable(x - px, z - pz)) return false;
  }
  return true;
}

// A* on the tile grid (8 directions, no corner cutting) with greedy string pulling.
const DX = [1, -1, 0, 0, 1, 1, -1, -1], DY = [0, 0, 1, -1, 1, -1, 1, -1];
function findPath(sx, sz, gx, gz, maxExpand = 2500) {
  const W = G.w;
  let s = [Math.floor(sx), Math.floor(sz)], g = [Math.floor(gx), Math.floor(gz)];
  if (blockedAt(g[0], g[1])) {
    const n = nearestWalkable(g[0], g[1], 6);
    if (!n) return [];
    g = n; gx = n[0] + 0.5; gz = n[1] + 0.5;
  }
  if (lineOfSight(sx, sz, gx, gz)) return [[gx, gz]];
  const si = s[1] * W + s[0], gi = g[1] * W + g[0];
  const gScore = new Map([[si, 0]]), parent = new Map([[si, -1]]), closed = new Set();
  const heap = [[h(s[0], s[1]), si]];
  function h(x, y) { const ax = Math.abs(x - g[0]), ay = Math.abs(y - g[1]); return ax + ay - 0.586 * Math.min(ax, ay); }
  function push(item) {
    heap.push(item); let i = heap.length - 1;
    while (i > 0) { const p = (i - 1) >> 1; if (heap[p][0] <= item[0]) break; heap[i] = heap[p]; i = p; }
    heap[i] = item;
  }
  function pop() {
    const top = heap[0], last = heap.pop();
    if (heap.length) {
      let i = 0;
      for (;;) {
        const l = 2 * i + 1, r = l + 1; if (l >= heap.length) break;
        const m = r < heap.length && heap[r][0] < heap[l][0] ? r : l;
        if (heap[m][0] >= last[0]) break; heap[i] = heap[m]; i = m;
      }
      heap[i] = last;
    }
    return top;
  }
  let best = si, bestH = Infinity, expanded = 0, reached = false;
  while (heap.length) {
    const [, cur] = pop();
    if (closed.has(cur)) continue;
    closed.add(cur);
    if (cur === gi) { reached = true; break; }
    if (++expanded > maxExpand) break;
    const cx = cur % W, cy = (cur / W) | 0, hh = h(cx, cy);
    if (hh < bestH) { bestH = hh; best = cur; }
    for (let i = 0; i < 8; i++) {
      const nx = cx + DX[i], ny = cy + DY[i];
      if (blockedAt(nx, ny)) continue;
      if (i >= 4 && (blockedAt(cx + DX[i], cy) || blockedAt(cx, cy + DY[i]))) continue;
      const ni = ny * W + nx;
      if (closed.has(ni)) continue;
      const ng = gScore.get(cur) + (i >= 4 ? 1.4142 : 1);
      if (gScore.has(ni) && ng >= gScore.get(ni)) continue;
      gScore.set(ni, ng); parent.set(ni, cur); push([ng + h(nx, ny), ni]);
    }
  }
  const end = reached ? gi : best;
  if (end === si) return [];
  const raw = [];
  for (let c = end; c !== -1 && c !== si; c = parent.get(c)) raw.push([(c % W) + 0.5, ((c / W) | 0) + 0.5]);
  raw.reverse();
  if (reached) raw[raw.length - 1] = [gx, gz];
  const out = [];
  let fx = sx, fz = sz, idx = 0;
  while (idx < raw.length) {
    let far = idx;
    for (let j = idx + 1; j < raw.length && j < idx + 30; j++) {
      if (lineOfSight(fx, fz, raw[j][0], raw[j][1])) far = j; else break;
    }
    out.push(raw[far]); [fx, fz] = raw[far]; idx = far + 1;
  }
  return out;
}

function nearestWalkable(x, y, maxR) {
  if (!blockedAt(x, y)) return [x, y];
  for (let r = 1; r <= maxR; r++)
    for (let j = -r; j <= r; j++)
      for (let i = -r; i <= r; i++)
        if ((Math.abs(i) === r || Math.abs(j) === r) && !blockedAt(x + i, y + j)) return [x + i, y + j];
  return null;
}

// =====================================================================================
// Monsters
// =====================================================================================

const monsters = new Map();
const spawners = [];
let nextMonsterId = 1;

function initSpawners() {
  useGrid(0);
  for (const [x, z, count, minL, maxL, types, radius = 5, respawn = 30] of SPAWNERS) {
    const sp = { x, z, count, minL, maxL, types, radius, respawn, pending: [] };
    spawners.push(sp);
    for (let i = 0; i < count; i++) spawnFrom(sp);
  }
  log(`Spawned ${monsters.size} monsters from ${spawners.length} spawners`);
}

function spawnFrom(sp) {
  for (let attempt = 0; attempt < 25; attempt++) {
    const a = Math.random() * Math.PI * 2, r = Math.sqrt(Math.random()) * sp.radius;
    const x = sp.x + Math.cos(a) * r, z = sp.z + Math.sin(a) * r;
    if (!walkable(x, z)) continue;
    const type = sp.types[Math.floor(Math.random() * sp.types.length)];
    const m = spawnMonster(type, randInt(sp.minL, sp.maxL), x, z, sp);
    if (!m.def.boss && Math.random() < ELITE_CHANCE) makeElite(m);
    return m;
  }
  return null;
}

// =====================================================================================
// Elite monsters: champions with a name and random affixes (Diablo style).
// =====================================================================================

let ELITE_CHANCE = Number(process.env.ELITE_CHANCE ?? 0.18); // admins can change it at runtime
const AFFIXES = ["Fast", "Vampiric", "Fire Enchanted", "Teleporter", "Shielding", "Mighty", "Extra Health"];
const NAME_A = ["Grim", "Blood", "Rot", "Skull", "Ash", "Gore", "Bone", "Black", "Iron", "Venom", "Dread", "Hollow", "Grave", "Thorn"];
const NAME_B = ["maw", "fang", "hide", "claw", "bane", "heart", "eye", "tooth", "grin", "spine", "shade", "gut", "jaw", "skull"];
const NAME_C = ["the Cruel", "the Hungry", "the Unbroken", "the Vile", "the Ravenous", "the Defiler", "the Cursed", "the Wretched", "the Butcher"];
const pick = (arr) => arr[Math.floor(Math.random() * arr.length)];

function makeElite(m, affixCount) {
  const n = affixCount ?? (m.level >= 9 ? 3 : m.level >= 5 ? randInt(2, 3) : randInt(1, 2));
  const affixes = [];
  while (affixes.length < n) { const a = pick(AFFIXES); if (!affixes.includes(a)) affixes.push(a); }
  const has = (a) => affixes.includes(a);
  m.elite = { name: `${pick(NAME_A)}${pick(NAME_B)} ${pick(NAME_C)}`, affixes };
  m.level += 2;
  m.maxHp = m.hp = Math.round(m.maxHp * (has("Extra Health") ? 4.2 : 2.8));
  m.dmg *= has("Mighty") ? 1.7 : 1.3;
  m.armor += 12;
  m.speedMul = has("Fast") ? 1.5 : 1.1;
  m.leash += 8;
  m.blinkAt = 0;
  m.shieldAt = 0;
  m.shieldUntil = 0;
  return m;
}

const hasAffix = (m, a) => !!(m.elite && m.elite.affixes.includes(a));
const speedOf = (m) => m.def.speed * (m.speedMul || 1);

/** Per-tick elite abilities while chasing. */
function eliteAbilities(m, s, d, t) {
  if (hasAffix(m, "Teleporter") && d > 5 && t >= m.blinkAt) {
    m.blinkAt = t + rand(4, 6);
    for (let i = 0; i < 8; i++) {
      const a = Math.random() * Math.PI * 2, x = s.x + Math.cos(a) * 1.5, z = s.z + Math.sin(a) * 1.5;
      if (!walkable(x, z)) continue;
      const ox = m.x, oz = m.z;
      m.x = x; m.z = z; m.path = [];
      sendNear(m.x, m.z, PLAYER_VIEW, { t: "matk", mid: m.id, tid: -1, dmg: 0, k: "blink", x: r2(x), z: r2(z), tx: r2(ox), tz: r2(oz) }, m.inst);
      break;
    }
  }
  if (hasAffix(m, "Shielding") && t >= m.shieldAt && m.hp < m.maxHp * 0.9) {
    m.shieldAt = t + rand(9, 12);
    m.shieldUntil = t + 3;
  }
}

function spawnMonster(type, level, x, z, spawner, inst = 0) {
  const def = MONSTERS[type];
  const hp = Math.round(def.hp * BALANCE.hp * (1 + BALANCE.hpPerLevel * (level - 1)));
  const m = {
    id: nextMonsterId++, type, def, level, x, z, ry: rand(0, 360), homeX: x, homeZ: z,
    hp, maxHp: hp, armor: def.armor + level * 2, dmg: def.dmg * BALANCE.dmg * (1 + BALANCE.dmgPerLevel * (level - 1)),
    state: "idle", target: 0, path: [], repathAt: 0, nextAttack: 0, slowUntil: 0, stunUntil: 0,
    wanderAt: now() + rand(1, 5), threat: new Map(), spawner, summoned: false, novaAt: 0,
    leash: def.boss ? 40 : 28, inst,
  };
  monsters.set(m.id, m);
  return m;
}

function aggro(m, sessionId) {
  if (m.state === "chase" && m.target) return;
  m.state = "chase";
  m.target = sessionId;
  m.repathAt = 0;
}

function alertNearby(m, sessionId) {
  for (const o of monsters.values())
    if (o !== m && o.inst === m.inst && o.state === "idle" && dist(o.x, o.z, m.x, m.z) < BALANCE.pull) aggro(o, sessionId);
}

/** Smoke Bomb: monsters can't see a vanished hero. */
const hidden = (s) => now() < (s.hiddenUntil || 0);

function validTarget(m, s) {
  return s && s.inWorld && !s.dead && !hidden(s) && (s.inst || 0) === m.inst && !(m.inst === 0 && inTown(s.x, s.z)) && dist(m.homeX, m.homeZ, s.x, s.z) < m.leash + 6;
}

function moveAlongPath(m, speed) {
  if (!m.path.length) return false;
  const [tx, tz] = m.path[0];
  const dx = tx - m.x, dz = tz - m.z, d = Math.hypot(dx, dz), step = speed * TICK;
  if (d <= step + 0.05) { m.x = tx; m.z = tz; m.path.shift(); }
  else { m.x += (dx / d) * step; m.z += (dz / d) * step; }
  if (d > 0.01) m.ry = (Math.atan2(dx, dz) * 180) / Math.PI;
  return true;
}

/** Companions a hero can have following them (shown to other players). */
const COMPANIONS = ["hound", "squire", "witch", "ranger", "acolyte", "golem"];

/** Effects a client may relay to the players around it (ability visuals). */
const FX_KINDS = new Set(["fireball", "nova", "heal", "meteor", "cleave", "levelup",
  "bash", "holybolt", "consecrate", "dshield", "judgement", "axe", "whirl", "leap", "warcry",
  "chain", "teleport", "twin", "multi", "knives", "smoke", "rain"]);

function sendNear(x, z, range, msg, inst = 0) {
  const data = JSON.stringify(msg);
  for (const s of sessions.values())
    if (s.inWorld && (s.inst || 0) === inst && dist(s.x, s.z, x, z) <= range) safeSend(s, data);
}

/** A monster attacks hero s (or, with no hero, swings at tx, tz: an invader at a town gate). */
function monsterAttack(m, s, kind, dmg, tx = m.x, tz = m.z) {
  if (s && hasAffix(m, "Vampiric") && dmg > 0) m.hp = Math.min(m.maxHp, m.hp + dmg * 0.6);
  sendNear(m.x, m.z, PLAYER_VIEW, { t: "matk", mid: m.id, tid: s ? s.id : -1, dmg: r2(dmg), k: kind, x: r2(s ? s.x : tx), z: r2(s ? s.z : tz) }, m.inst);
}

function updateMonster(m, t) {
  const slowed = t < m.slowUntil;
  switch (m.state) {
    case "idle": {
      let best = null, bestD = m.def.aggro * BALANCE.aggro;
      for (const s of sessions.values()) {
        if (!s.inWorld || s.dead || hidden(s) || (s.inst || 0) !== m.inst || (m.inst === 0 && inTown(s.x, s.z))) continue;
        const d = dist(m.x, m.z, s.x, s.z);
        if (d < bestD) { bestD = d; best = s; }
      }
      if (best) { aggro(m, best.id); alertNearby(m, best.id); break; }
      if (m.invasion && invasions.idle(m, t)) break; // marching on a town gate, or battering it
      if (t >= m.wanderAt) {
        m.wanderAt = t + rand(3, 7);
        const wx = m.homeX + rand(-4, 4), wz = m.homeZ + rand(-4, 4);
        if (walkable(wx, wz) && lineOfSight(m.x, m.z, wx, wz)) m.path = [[wx, wz]];
      }
      moveAlongPath(m, speedOf(m) * 0.35);
      break;
    }

    case "chase": {
      let s = sessions.get(m.target);
      if (!validTarget(m, s)) {
        // switch to whoever else has hurt us the most
        s = null;
        let top = 0;
        for (const [sid, amount] of m.threat) {
          const c = sessions.get(sid);
          if (validTarget(m, c) && amount > top) { top = amount; s = c; }
        }
        if (s) m.target = s.id;
      }
      if (!s || dist(m.x, m.z, m.homeX, m.homeZ) > m.leash) {
        m.state = "return"; m.target = 0; m.path = [];
        break;
      }

      if (t < m.stunUntil) { m.path = []; break; } // stunned: no moving, no attacking
      const d = dist(m.x, m.z, s.x, s.z);
      if (m.type === "Lich King" || m.type === "Crypt Lord") lichAbilities(m, s, d, t);
      if (m.elite) eliteAbilities(m, s, d, t);
      if (m.worldBoss && worldBosses.abilities(m, s, d, t)) break; // winding up a slam

      const canHit = d <= m.def.range + 0.45 && (!m.def.ranged || lineOfSight(m.x, m.z, s.x, s.z, 0.1));
      if (canHit) {
        m.path = [];
        m.ry = (Math.atan2(s.x - m.x, s.z - m.z) * 180) / Math.PI;
        if (t >= m.nextAttack) {
          m.nextAttack = t + m.def.cd * (slowed ? 1.5 : 1) * rand(0.9, 1.1);
          monsterAttack(m, s, m.def.ranged ? "shot" : "melee", m.dmg * (m.def.ranged ? rand(0.85, 1.15) : rand(0.8, 1.2)));
        }
        break;
      }
      if (lineOfSight(m.x, m.z, s.x, s.z)) m.path = [[s.x, s.z]];
      else if (t >= m.repathAt || !m.path.length) {
        m.repathAt = t + rand(0.4, 0.7);
        m.path = findPath(m.x, m.z, s.x, s.z);
      }
      moveAlongPath(m, speedOf(m) * (slowed ? 0.5 : 1));
      separate(m);
      break;
    }

    case "return": {
      if (m.invasion) { m.state = "idle"; m.path = []; break; } // back to the siege, without healing
      m.hp = Math.min(m.maxHp, m.hp + m.maxHp * 0.25 * TICK);
      if (!m.path.length) {
        if (dist(m.x, m.z, m.homeX, m.homeZ) < 1.5) { m.state = "idle"; if (m.worldBoss) worldBosses.reset(m); m.hp = m.maxHp; m.threat.clear(); break; }
        m.path = findPath(m.x, m.z, m.homeX, m.homeZ, 6000);
        if (!m.path.length) { m.x = m.homeX; m.z = m.homeZ; }
      }
      moveAlongPath(m, speedOf(m) * 1.3);
      break;
    }
  }
}

function separate(m) {
  let px = 0, pz = 0;
  for (const o of monsters.values()) {
    if (o === m || o.inst !== m.inst || o.state !== "chase") continue;
    const dx = m.x - o.x, dz = m.z - o.z, d = Math.hypot(dx, dz);
    if (d > 0.001 && d < 0.9) { px += (dx / d) * (0.9 - d); pz += (dz / d) * (0.9 - d); }
  }
  const nx = m.x + px * 0.5, nz = m.z + pz * 0.5;
  if ((px || pz) && walkable(nx, nz)) { m.x = nx; m.z = nz; }
}

function lichAbilities(m, s, d, t) {
  if (t >= m.novaAt && d < 6) {
    m.novaAt = t + 7;
    monsterAttack(m, null, "nova", m.dmg * 1.4);
  }
  if (!m.summoned && m.hp < m.maxHp * 0.5) {
    m.summoned = true;
    monsterAttack(m, null, "summon", 0);
    for (let i = 0; i < 4; i++) {
      const a = (i * Math.PI) / 2, x = m.x + Math.cos(a) * 2.5, z = m.z + Math.sin(a) * 2.5;
      if (!walkable(x, z)) continue;
      const add = spawnMonster(i % 2 ? "Skeleton Archer" : "Skeleton", Math.max(1, m.level - 4), x, z, null, m.inst);
      add.leash = 40;
      aggro(add, s.id);
    }
  }
}

function damageMonster(m, s, dmg) {
  if (m.hp <= 0) return;
  if (now() < (m.shieldUntil || 0)) dmg = 0; // Shielding elites are immune for a moment
  m.hp -= dmg;
  m.threat.set(s.id, (m.threat.get(s.id) || 0) + dmg);
  if (m.invasion) invasions.onDamage(m, s);
  if (m.worldBoss) worldBosses.onDamage(m, s);
  if (m.state !== "chase") { aggro(m, s.id); alertNearby(m, s.id); }
  if (m.hp <= 0) killMonster(m);
}

function killMonster(m) {
  monsters.delete(m.id);
  M.kills.inc({ monster: m.type, elite: m.elite ? "yes" : "no" });
  if (m.def.boss) M.bossKills.inc({ boss: m.type });
  sendNear(m.x, m.z, PLAYER_VIEW + 10, { t: "mdie", mid: m.id }, m.inst);
  if (hasAffix(m, "Fire Enchanted")) monsterAttack(m, null, "explode", m.dmg * 1.6);
  // Everyone who fought it gets credit, plus their party members who are nearby (WoW-style shared kills).
  const credited = new Set();
  for (const [sid] of m.threat) {
    const s = sessions.get(sid);
    if (!s || !s.inWorld) continue;
    credited.add(s);
    const p = partyOf(s);
    if (p) for (const o of partyMembers(p)) if (!o.dead && (o.inst || 0) === m.inst && dist(o.x, o.z, m.x, m.z) <= PARTY_RANGE) credited.add(o);
  }
  for (const s of credited) {
    const diff = m.level - s.lvl;
    const mul = diff < -6 ? 0.1 : diff < -3 ? 0.5 : diff > 3 ? 1.3 : 1;
    const eliteMul = m.elite ? 3 + m.elite.affixes.length * 0.5 : 1;
    const xp = Math.max(1, Math.round(m.def.xp * (1 + 0.12 * (m.level - 1)) * mul * eliteMul * (m.xpMul || 1)));
    const loot = s.ledger ? I.rollLoot({ name: m.type, boss: !!m.def.boss }, m.level, m.lootBonus || 0, heroClass(s), !!m.elite, lootSource(m)) : [];
    const drops = s.ledger ? dropFor(s, m.x, m.z, loot, m.inst) : [];
    safeSend(s, JSON.stringify({ t: "kill", mid: m.id, name: m.type, l: m.level, xp, x: r2(m.x), z: r2(m.z), drops, ...(m.elite ? { el: m.elite.name } : {}), ...(m.lootBonus ? { lb: m.lootBonus } : {}) }));
  }
  if (m.def.boss && m.inst) {
    const inst = instances.get(m.inst);
    if (!(inst && inst.rift)) sendNear(m.x, m.z, 999, { t: "sys", msg: `${m.type} has been slain! ${inst ? DUNGEONS[inst.dIdx].name : "The dungeon"} falls silent.` }, m.inst);
  }
  else if (m.def.boss) broadcast({ t: "sys", msg: `${m.type} has been slain!` });
  else if (m.elite) sendNear(m.x, m.z, PLAYER_VIEW, { t: "sys", msg: `${m.elite.name} (${m.type}) has been slain!` }, m.inst);
  if (m.spawner) m.spawner.pending.push(now() + m.spawner.respawn);
  if (m.inst) { // a greater rift counts its kills
    let top = null, most = -1;
    for (const [sid, amount] of m.threat) if (amount > most) { most = amount; top = sessions.get(sid); }
    rifts.onKill(m, top);
  }
}

function updateSpawners(t) {
  for (const sp of spawners) {
    for (let i = sp.pending.length - 1; i >= 0; i--) {
      if (t < sp.pending[i]) continue;
      let near = false;
      for (const s of sessions.values()) if (s.inWorld && dist(s.x, s.z, sp.x, sp.z) < sp.radius + 10) near = true;
      if (near) continue; // don't pop monsters in right next to players
      sp.pending.splice(i, 1);
      spawnFrom(sp);
    }
  }
}

// =====================================================================================
// Accounts & characters
// =====================================================================================

// Accounts and characters live in PostgreSQL (DATABASE_URL) or, without one, in JSON files under DATA_DIR.
const store = createStore({ databaseUrl: process.env.DATABASE_URL || "", dataDir: DATA_DIR, log });
const mailer = A.createMailer({ smtpUrl: process.env.SMTP_URL || "", from: process.env.MAIL_FROM || "", publicUrl: process.env.PUBLIC_URL || "", log });
const MAX_CHARACTERS = 10;
const RESET_MINUTES = 30;
const ADMIN_RESET_HOURS = 24;
// Wrong passwords: 5 per account in 10 minutes locks it for 2 minutes; 25 failures from one address in 15 minutes
// block that address for 15 minutes. Registrations: 10 per address per hour.
const accountLimit = new A.Limiter({ max: 5, windowMs: 10 * 60000, lockMs: 2 * 60000 });
const ipLimit = new A.Limiter({ max: 25, windowMs: 15 * 60000, lockMs: 15 * 60000 });
// New accounts per address per hour (REGISTER_LIMIT; raise it for a local load test).
const registerLimit = new A.Limiter({ max: Number(process.env.REGISTER_LIMIT) || 10, windowMs: 60 * 60000, lockMs: 60 * 60000 });
const forgotLimit = new A.Limiter({ max: 5, windowMs: 60 * 60000, lockMs: 60 * 60000 });
setInterval(() => { for (const l of [accountLimit, ipLimit, registerLimit, forgotLimit]) l.prune(); }, 5 * 60000);

/** Saves a character's progress (the client sends it; position comes from the server). */
function saveCharacter(s) {
  if (!s.char || !s.char.save) return Promise.resolve();
  if (s.ledger) {
    I.ledgerToSave(s.ledger, s.char.save);
    s.char.save.questsDone = [...s.ledger.questsDone];
  }
  [s.char.save.x, s.char.save.z] = overworldPos(s);
  return store.saveCharacter(s.char.id, s.char.save).then(
    () => M.saves.inc({ result: "ok" }),
    (e) => { M.saves.inc({ result: "error" }); log("save failed", s.char.name, e.message); });
}

// =====================================================================================
// Sessions
// =====================================================================================

const sessions = new Map();
let nextSessionId = 1;

function safeSend(s, data) {
  if (s.ws.readyState !== 1) return;
  s.ws.send(data);
  M.msgOut.inc();
  M.bytesOut.inc(undefined, data.length);
}

function broadcast(msg) {
  const data = JSON.stringify(msg);
  for (const s of sessions.values()) if (s.inWorld) safeSend(s, data);
}

function fail(s, err) {
  safeSend(s, JSON.stringify({ t: "error", err }));
  setTimeout(() => s.ws.close(), 100);
}

/** The character is chosen and the world is in sync: put them in the world. */
function completeLogin(s) {
  const ch = s.char;
  s.pendingPlay = false;
  s.admin = isAdmin(s);
  if (s.admin) log(`${ch.name} (${s.acc.username}) is an admin`);
  s.name = ch.name;
  s.inWorld = true;
  s.dead = false;
  s.inst = 0;
  const isNew = !ch.save;
  openLedger(s);
  s.earned = earnedFrom(ch.save);
  s.lvl = ch.save && ch.save.level ? ch.save.level : 1;
  s.x = ch.save && ch.save.x ? ch.save.x : SPAWN.x;
  s.z = ch.save && ch.save.z ? ch.save.z : SPAWN.z;
  safeSend(s, JSON.stringify({ t: "welcome", id: s.id, name: ch.name, look: ch.look, hasSave: !isNew, save: isNew ? undefined : ch.save, now: worldClock(), admin: !!s.admin }));
  sendInv(s);
  safeSend(s, JSON.stringify(weather.message()));
  invasions.sendTo(s);
  worldBosses.sendTo(s);
  guilds.entered(s);
  broadcast({ t: "sys", msg: `${ch.name} has entered the world.` });
  log(`${ch.name} entered the world (${sessions.size} connected)`);
}

/** Takes a hero out of the world (logging out, or back to character select). */
function leaveWorld(s, why) {
  if (!s.inWorld) return Promise.resolve();
  if (partyOf(s)) leaveParty(s, why === "select" ? "has left the world." : "has gone offline.");
  if (trades.has(s.id)) closeTrade(trades.get(s.id), `${s.name} has left.`);
  duels.left(s);
  guilds.left(s);
  const saved = saveCharacter(s);
  s.inWorld = false;
  s.inst = 0;
  broadcast({ t: "sys", msg: `${s.name} has left the world.` });
  broadcast({ t: "leave", id: s.id });
  log(`${s.name} left the world (${sessions.size} connected)`);
  return saved;
}

// =====================================================================================
// Parties: up to 5 players share kill credit (when nearby), a chat channel and quests.
// =====================================================================================

const parties = new Map();
let nextPartyId = 1;
const MAX_PARTY = 5;
const PARTY_RANGE = 60;
const INVITE_TIMEOUT = 60;

function partyOf(s) { return s.party ? parties.get(s.party) || null : null; }
function partyMembers(p) { return [...p.members].map((id) => sessions.get(id)).filter((o) => o && o.inWorld); }
function sys(s, msg) { safeSend(s, JSON.stringify({ t: "sys", msg })); }

// ---- trading
const trades = new Map(); // session id -> { a, b, offers: Map(id -> { items, gold }), ok: Set }

// =====================================================================================
// Metrics (Prometheus, on METRICS_PORT; see docs/deployment/monitoring.md)
// =====================================================================================

const M = {
  msgIn: metrics.counter("shadowfall_messages_received_total", "Client messages received, by type."),
  bytesIn: metrics.counter("shadowfall_received_bytes_total", "Bytes received from clients."),
  msgOut: metrics.counter("shadowfall_messages_sent_total", "Messages sent to clients."),
  bytesOut: metrics.counter("shadowfall_sent_bytes_total", "Bytes sent to clients."),
  worldMismatch: metrics.counter("shadowfall_world_mismatches_total", "Players whose game built a different map than the server's within the same build (a determinism bug)."),
  handlerErrors: metrics.counter("shadowfall_handler_errors_total", "Exceptions thrown while handling a client message, by type."),
  logins: metrics.counter("shadowfall_logins_total", "Login attempts, by result (ok, new = account created, bad_password, locked)."),
  resets: metrics.counter("shadowfall_password_resets_total", "Password resets by method: recovery (recovery code), code (an email or admin code used), admin (codes issued in game), change (changed in game)."),
  kills: metrics.counter("shadowfall_monsters_killed_total", "Monsters killed, by monster type and whether it was an elite."),
  bossKills: metrics.counter("shadowfall_bosses_killed_total", "Bosses killed, by boss."),
  deaths: metrics.counter("shadowfall_player_deaths_total", "Player deaths."),
  trades: metrics.counter("shadowfall_trades_completed_total", "Completed player trades."),
  itemOps: metrics.counter("shadowfall_item_actions_total", "Item and gold actions players asked for, by action (equip, sell, buy, pickup...)."),
  achievements: metrics.counter("shadowfall_achievements_total", "Achievements earned by players."),
  dungeonEntries: metrics.counter("shadowfall_dungeon_entries_total", "Players entering a dungeon level, by dungeon and difficulty."),
  admin: metrics.counter("shadowfall_admin_commands_total", "Admin commands run, by command."),
  saves: metrics.counter("shadowfall_character_saves_total", "Character files written, by result."),
  clientErrors: metrics.counter("shadowfall_client_errors_total", "Errors reported by players' browsers and games, by kind: load (the page couldn't start the game), js (browser script errors), exception (the game threw), error (the game logged an error)."),
  rifts: metrics.counter("shadowfall_rifts_opened_total", "Greater rifts opened, by tier (20 = 20 and up)."),
  duels: metrics.counter("shadowfall_duels_total", "Duels that ended, by result (won, draw)."),
  invasions: metrics.counter("shadowfall_invasions_total", "Town invasions that ended, by town and result (won = beaten off, lost = the town was sacked)."),
  tick: metrics.histogram("shadowfall_tick_duration_seconds", "Time spent in one simulation tick.", [0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25]),
};
// Start at zero so dashboards show a flat line rather than "no data" until the first event.
M.deaths.inc(undefined, 0);
for (const kind of CLIENT_ERROR_KINDS) M.clientErrors.inc({ kind }, 0);
M.trades.inc(undefined, 0);
M.bytesIn.inc(undefined, 0);
M.bytesOut.inc(undefined, 0);
M.msgOut.inc(undefined, 0);
for (const result of ["ok", "error"]) M.saves.inc({ result }, 0);
for (const result of ["ok", "new", "bad_password", "locked"]) M.logins.inc({ result }, 0);
M.worldMismatch.inc(undefined, 0);
const inWorld = () => [...sessions.values()].filter((o) => o.inWorld);
metrics.gauge("shadowfall_connections", "Open WebSocket connections (including the login screen).", () => sessions.size);
metrics.gauge("shadowfall_players_online", "Players in the world.", () => inWorld().length);
metrics.gauge("shadowfall_players_in_dungeons", "Players inside a dungeon instance.", () => inWorld().filter((o) => o.inst).length);
metrics.gauge("shadowfall_players_by_class", "Players in the world, by class.", () => {
  const by = {};
  for (const o of inWorld()) { const c = (o.look && o.look.mdl) || "Knight"; by[c] = (by[c] || 0) + 1; }
  return Object.entries(by).map(([c, n]) => [{ class: c }, n]);
});
metrics.gauge("shadowfall_players_by_level", "Players in the world, by level band.", () => {
  const by = {};
  for (const o of inWorld()) { const b = o.lvl >= 30 ? "30+" : `${Math.floor(o.lvl / 5) * 5}-${Math.floor(o.lvl / 5) * 5 + 4}`; by[b] = (by[b] || 0) + 1; }
  return Object.entries(by).map(([b, n]) => [{ band: b }, n]);
});
metrics.gauge("shadowfall_monsters_alive", "Monsters alive, overworld vs dungeons.", () => {
  let ow = 0, dg = 0;
  for (const m of monsters.values()) if (m.inst) dg++; else ow++;
  return [[{ zone: "overworld" }, ow], [{ zone: "dungeon" }, dg]];
});
metrics.gauge("shadowfall_elites_alive", "Elite monsters alive.", () => { let n = 0; for (const m of monsters.values()) if (m.elite) n++; return n; });
metrics.gauge("shadowfall_dungeon_instances", "Open dungeon instances, by dungeon.", () => {
  const by = Object.fromEntries(DUNGEONS.map((d) => [d.name, 0]));
  for (const inst of instances.values()) by[DUNGEONS[inst.dIdx].name]++;
  return Object.entries(by).map(([d, n]) => [{ dungeon: d }, n]);
});
metrics.gauge("shadowfall_parties", "Active parties.", () => parties.size);
metrics.gauge("shadowfall_trades_open", "Trades in progress.", () => trades.size / 2);
metrics.gauge("shadowfall_world_loaded", "1 when a world map is loaded.", () => (world ? 1 : 0));
let counts = { accounts: 0, characters: 0 };
const refreshCounts = () => store.counts().then((c) => { counts = c; }, () => {});
metrics.gauge("shadowfall_accounts", "Player accounts.", () => counts.accounts);
metrics.gauge("shadowfall_characters", "Characters (not deleted).", () => counts.characters);
metrics.gauge("shadowfall_storage_info", "Where accounts are stored.", () => [[{ kind: store.kind }, 1]]);
const TRADE_SLOTS = 12, TRADE_RANGE = 10;
const canTrade = (s, o) => !s.dead && !o.dead && (s.inst || 0) === (o.inst || 0) && dist(s.x, s.z, o.x, o.z) <= TRADE_RANGE;

function closeTrade(tr, msg) {
  trades.delete(tr.a);
  trades.delete(tr.b);
  for (const id of [tr.a, tr.b]) {
    const o = sessions.get(id);
    if (o) safeSend(o, JSON.stringify({ t: "tclose", msg }));
  }
}
function partySys(p, msg) { for (const o of partyMembers(p)) sys(o, msg); }

function findOnline(name) {
  const n = String(name || "").trim().toLowerCase();
  if (!n) return null;
  for (const o of sessions.values()) if (o.inWorld && o.name && o.name.toLowerCase() === n) return o;
  return null;
}

function sendParty(p) {
  const pm = partyMembers(p).map((o) => {
    const inst = o.inst && instances.get(o.inst), look = o.look || {};
    return {
      id: o.id, name: o.name, lvl: o.lvl, hp: Math.ceil(o.hp || 0), mhp: Math.ceil(o.mhp || 1), mp: Math.ceil(o.mp || 0), mmp: Math.ceil(o.mmp || 0),
      mdl: look.mdl || "Knight", wk: look.wk || "", helm: look.helm || "", x: r2(o.x), z: r2(o.z), ry: Math.round(o.ry || 0), dead: !!o.dead,
      di: o.inst || 0, dn: inst ? `${DUNGEONS[inst.dIdx].name}, level ${inst.depth}` : "",
    };
  });
  const data = JSON.stringify({ t: "party", id: p.leader, pm });
  for (const o of partyMembers(p)) safeSend(o, data);
}

function leaveParty(s, why) {
  const p = partyOf(s);
  if (!p) return;
  p.members.delete(s.id);
  s.party = 0;
  safeSend(s, JSON.stringify({ t: "party", id: 0, pm: [] }));
  const rest = partyMembers(p);
  if (rest.length <= 1) {
    for (const o of rest) {
      o.party = 0;
      safeSend(o, JSON.stringify({ t: "party", id: 0, pm: [] }));
      sys(o, "Your party has been disbanded.");
    }
    parties.delete(p.id);
    return;
  }
  partySys(p, `${s.name} ${why}`);
  if (p.leader === s.id) {
    p.leader = rest[0].id;
    partySys(p, `${rest[0].name} is now the party leader.`);
  }
  sendParty(p);
}

function invite(s, name) {
  const target = findOnline(name);
  if (!target) return sys(s, `No player named "${String(name || "").slice(0, 16)}" is online.`);
  if (target === s) return sys(s, "You can't invite yourself.");
  const p = partyOf(s);
  if (p && p.leader !== s.id) return sys(s, "Only the party leader can invite.");
  if (p && p.members.size >= MAX_PARTY) return sys(s, "Your party is full.");
  if (partyOf(target)) return sys(s, `${target.name} is already in a party.`);
  target.invite = { from: s.id, at: now() };
  safeSend(target, JSON.stringify({ t: "pinv", id: s.id, name: s.name }));
  sys(s, `You invited ${target.name} to your party.`);
}

const QUEST_ID = /^[a-z0-9_]{1,32}$/;

const partyHandlers = {
  pinvite(s, m) { if (s.inWorld) invite(s, m.name); },

  // ---- trading: the server only relays offers and makes sure both sides accepted the same thing.
  treq(s, m) {
    if (!s.inWorld || s.dead) return;
    const o = sessions.get(m.id | 0);
    if (!o || o === s || !o.inWorld) return sys(s, "That player is not here.");
    if (!canTrade(s, o)) return sys(s, `You are too far away from ${o.name} to trade.`);
    if (trades.has(s.id) || trades.has(o.id)) return sys(s, `${o.name} is busy.`);
    o.tradeInvite = { from: s.id, at: now() };
    safeSend(o, JSON.stringify({ t: "tinv", id: s.id, name: s.name }));
    sys(s, `You ask ${o.name} to trade.`);
  },

  tacc(s) {
    const inv = s.tradeInvite;
    s.tradeInvite = null;
    if (!inv || !s.inWorld) return;
    const o = sessions.get(inv.from);
    if (now() - inv.at > INVITE_TIMEOUT || !o || !o.inWorld) return sys(s, "That trade request has expired.");
    if (!canTrade(s, o)) return sys(s, `You are too far away from ${o.name} to trade.`);
    if (trades.has(s.id) || trades.has(o.id)) return sys(s, `${o.name} is busy.`);
    const tr = { a: o.id, b: s.id, offers: new Map([[o.id, { slots: [], items: [], gold: 0 }], [s.id, { slots: [], items: [], gold: 0 }]]), ok: new Set() };
    trades.set(o.id, tr);
    trades.set(s.id, tr);
    safeSend(o, JSON.stringify({ t: "topen", id: s.id, name: s.name }));
    safeSend(s, JSON.stringify({ t: "topen", id: o.id, name: o.name }));
  },

  tdecl(s) {
    const inv = s.tradeInvite;
    s.tradeInvite = null;
    const o = inv && sessions.get(inv.from);
    if (o && o.inWorld) sys(o, `${s.name} declines to trade.`);
  },

  /** An offer is bag slots and gold; the items stay in the bags until the trade completes. */
  toffer(s, m) {
    const tr = trades.get(s.id);
    if (!tr || !s.ledger) return;
    const slots = [...new Set((Array.isArray(m.slots) ? m.slots : []).map((x) => x | 0))]
      .filter((i) => i >= 0 && i < I.BAG_SIZE && s.ledger.bag[i]).slice(0, TRADE_SLOTS);
    const gold = Math.max(0, Math.min(s.ledger.gold, parseInt(m.gold, 10) || 0));
    // Remember exactly what was offered: if the bags change before both accept, the trade is called off.
    const items = slots.map((i) => JSON.stringify(s.ledger.bag[i]));
    tr.offers.set(s.id, { slots, items, gold });
    tr.ok.clear(); // any change resets both acceptances
    const other = sessions.get(tr.a === s.id ? tr.b : tr.a);
    if (other) safeSend(other, JSON.stringify({ t: "tupd", items, gold }));
    safeSend(s, JSON.stringify({ t: "tmine", slots, gold }));
  },

  tok(s) {
    const tr = trades.get(s.id);
    if (!tr) return;
    const other = sessions.get(tr.a === s.id ? tr.b : tr.a);
    if (!other || !canTrade(s, other)) return closeTrade(tr, "You moved too far apart to trade.");
    tr.ok.add(s.id);
    safeSend(other, JSON.stringify({ t: "tok", id: s.id }));
    if (tr.ok.size < 2) return;
    // Both accepted: check that everything offered is still there and fits, then swap it all at once.
    const offerOf = (o) => tr.offers.get(o.id);
    for (const o of [s, other]) {
      const off = offerOf(o);
      if (!o.ledger || off.gold > o.ledger.gold || off.slots.some((i, k) => JSON.stringify(o.ledger.bag[i]) !== off.items[k]))
        return closeTrade(tr, "The trade was called off: something offered is no longer there.");
    }
    for (const [me, them] of [[s, other], [other, s]]) {
      const bag = me.ledger.bag.slice();
      for (const i of offerOf(me).slots) bag[i] = null;
      if (!I.fits(bag, offerOf(them).slots.map((i) => them.ledger.bag[i])))
        return closeTrade(tr, `The trade was called off: ${me.name} has no room for it.`);
    }
    trades.delete(tr.a);
    trades.delete(tr.b);
    const moving = new Map([[s.id, offerOf(s).slots.map((i) => s.ledger.bag[i])], [other.id, offerOf(other).slots.map((i) => other.ledger.bag[i])]]);
    for (const o of [s, other]) {
      for (const i of offerOf(o).slots) o.ledger.bag[i] = null;
      o.ledger.gold -= offerOf(o).gold;
    }
    for (const [me, them] of [[s, other], [other, s]]) {
      for (const it of moving.get(them.id)) I.addItem(me.ledger.bag, it);
      me.ledger.gold += offerOf(them).gold;
      safeSend(me, JSON.stringify({ t: "tdone", items: offerOf(them).items, gold: offerOf(them).gold, name: them.name }));
      ledgerChanged(me);
      saveCharacter(me);
    }
    M.trades.inc();
    log(`trade: ${s.name} <-> ${other.name}`);
  },

  tcancel(s) {
    const tr = trades.get(s.id);
    if (tr) closeTrade(tr, `${s.name} cancelled the trade.`);
  },

  paccept(s) {
    if (!s.inWorld || !s.invite) return;
    const inv = s.invite;
    s.invite = null;
    const from = sessions.get(inv.from);
    if (now() - inv.at > INVITE_TIMEOUT || !from || !from.inWorld) return sys(s, "That invitation has expired.");
    if (partyOf(s)) return sys(s, "You are already in a party.");
    let p = partyOf(from);
    if (!p) {
      p = { id: nextPartyId++, leader: from.id, members: new Set([from.id]) };
      parties.set(p.id, p);
      from.party = p.id;
    }
    if (p.members.size >= MAX_PARTY) return sys(s, "That party is full.");
    p.members.add(s.id);
    s.party = p.id;
    partySys(p, `${s.name} joins the party.`);
    sendParty(p);
  },

  pdecline(s) {
    if (!s.invite) return;
    const from = sessions.get(s.invite.from);
    s.invite = null;
    if (from && from.inWorld) sys(from, `${s.name} declines your invitation.`);
  },

  pleave(s) { if (partyOf(s)) leaveParty(s, "has left the party."); else sys(s, "You are not in a party."); },

  pkick(s, m) {
    const p = partyOf(s);
    if (!p || p.leader !== s.id) return;
    const target = sessions.get(parseInt(m.id, 10));
    if (!target || target === s || target.party !== p.id) return;
    sys(target, "You have been removed from the party.");
    leaveParty(target, "was removed from the party.");
  },

  pshare(s, m) {
    const p = partyOf(s);
    const q = String(m.q || "");
    if (!p || !QUEST_ID.test(q)) return;
    const data = JSON.stringify({ t: "qshare", id: s.id, name: s.name, k: q });
    for (const o of partyMembers(p)) if (o !== s) safeSend(o, data);
    sys(s, "Quest shared with your party.");
  },
};

// =====================================================================================
// Dungeons: the Catacombs, three levels generated per party (or solo player). Each level is an
// instance with its own grid and monsters; players in an instance only see that instance.
// =====================================================================================

const instances = new Map();
let nextInstanceId = 1;
// Dungeon entrances: each DUNGEONS entry's design position, moved to the nearest clear 5x5 spot on the world map.
// The client runs the very same search on the very same map (DungeonDef.ResolveEntrance), so both agree.
function resolveEntrance(x, z) {
  const cx = Math.floor(x), cz = Math.floor(z);
  const clear = (px, pz) => {
    for (let j = -2; j <= 2; j++) for (let i = -2; i <= 2; i++) if (blockedAt(px + i, pz + j)) return false;
    return true;
  };
  for (let r = 0; r <= 30; r++)
    for (let dz = -r; dz <= r; dz++)
      for (let dx = -r; dx <= r; dx++) {
        if (Math.max(Math.abs(dx), Math.abs(dz)) !== r) continue;
        if (clear(cx + dx, cz + dz)) return [cx + dx + 0.5, cz + dz + 0.5];
      }
  return [cx + 0.5, cz + 0.5];
}

let entrancesFor = null;
/** Entrance positions (world coordinates), computed once per world map. */
function entrances() {
  const at = (d) => (d.world ? d.at : [designToWorld(d.at[0]), designToWorld(d.at[1])]);
  if (!world) return DUNGEONS.map(at);
  if (entrancesFor !== world) {
    useGrid(0);
    for (const d of DUNGEONS) d.entrance = resolveEntrance(...at(d));
    entrancesFor = world;
  }
  return DUNGEONS.map((d) => d.entrance);
}
const dungeonIndex = (id) => { const i = DUNGEONS.findIndex((d) => d.id === id); return i < 0 ? 0 : i; };
const exitOf = (dIdx) => { const e = entrances()[dIdx]; return [e[0], e[1] - 2.5]; };

const partyKey = (s) => { const p = partyOf(s); return p ? "p" + p.id : "s" + s.id; };

/** The party's (or solo hero's) instance of a dungeon level, generated on first entry. */
function getInstance(s, depth, dIdx, fresh = false, df = 0) {
  const key = partyKey(s) + ":" + dIdx + ":" + depth;
  for (const inst of instances.values()) if (inst.key === key) {
    if (!fresh) return inst; // the party is already in there: join it, at its difficulty
    df = inst.df;
    closeInstance(inst);
    break;
  }
  df = Math.max(0, Math.min(DIFFICULTIES.length - 1, df | 0));
  const def = DUNGEONS[dIdx];
  const p = partyOf(s);
  const members = p ? partyMembers(p) : [s];
  const level = Math.max(def.minLevel, Math.round(members.reduce((a, o) => a + (o.lvl || 1), 0) / members.length));
  const seed = (Math.random() * 2147483647) | 0;
  const L = def.style === "caves" ? dungeonGen.generateCaves(seed, depth, def.depths) : dungeonGen.generate(seed, depth, def.depths);
  const inst = { id: nextInstanceId++, key, dIdx, depth, df, seed, layout: L, grid: { w: L.w, h: L.h, blocked: L.blocked }, cells: dungeonGen.pack(L.blocked), lastActive: now(), level };
  instances.set(inst.id, inst);
  populate(inst, members.length);
  log(`Dungeon ${inst.id} (${def.name} ${DIFFICULTIES[df].name}, ${key}, depth ${depth}, level ${level}) created`);
  return inst;
}

function closeInstance(inst) {
  for (const m of monsters.values()) if (m.inst === inst.id) monsters.delete(m.id);
  instances.delete(inst.id);
}

function populate(inst, players) {
  useGrid(inst.id);
  const def = DUNGEONS[inst.dIdx];
  const L = inst.layout, types = def.types[Math.min(inst.depth, def.types.length) - 1];
  const diff = DIFFICULTIES[inst.df || 0];
  /** Applies the dungeon difficulty to a monster (after makeElite, which scales from the base stats). */
  const harden = (m) => {
    m.maxHp = m.hp = Math.round(m.hp * diff.hp);
    m.dmg *= diff.dmg;
    m.xpMul = diff.xp;
    m.lootBonus = diff.loot;
    m.leash = 70;
    return m;
  };
  for (const pk of L.packs) {
    const eliteRoom = Math.random() < 0.3;
    const n = pk.n + Math.max(0, players - 1);
    for (let i = 0; i < n; i++) {
      for (let a = 0; a < 12; a++) {
        const x = pk.room.x + 1 + Math.random() * (pk.room.w - 2), z = pk.room.y + 1 + Math.random() * (pk.room.h - 2);
        if (!walkable(x, z)) continue;
        const m = spawnMonster(types[randInt(0, types.length - 1)], Math.max(3, inst.level + inst.depth - 1 + randInt(-1, 1)), x, z, null, inst.id);
        if ((eliteRoom && i === 0) || Math.random() < 0.06 + diff.elite) makeElite(m);
        harden(m);
        break;
      }
    }
  }
  if (L.boss) {
    const b = spawnMonster(def.boss, inst.level + 3, L.boss[0], L.boss[1], null, inst.id);
    b.maxHp = b.hp = Math.round(b.hp * (0.7 + 0.3 * players));
    harden(b);
  }
  useGrid(0);
}

function enterInstance(s, inst) {
  if (trades.has(s.id)) closeTrade(trades.get(s.id), "The trade was cancelled.");
  s.inst = inst.id;
  [s.x, s.z] = inst.layout.start;
  M.dungeonEntries.inc({ dungeon: DUNGEONS[inst.dIdx].name, difficulty: (DIFFICULTIES[inst.df] || DIFFICULTIES[0]).name });
  inst.lastActive = now();
  const L = inst.layout;
  safeSend(s, JSON.stringify({
    t: "dungeon", id: inst.id, l: inst.depth, k: inst.name || DUNGEONS[inst.dIdx].name, d: inst.dIdx, n: inst.depths || DUNGEONS[inst.dIdx].depths, df: inst.df, seed: inst.seed, w: L.w, h: L.h, cells: inst.cells,
    rooms: L.rooms.flatMap((r) => [r.x, r.y, r.w, r.h]), start: L.start, exit: L.exit,
    stairs: L.stairs || [], boss: L.boss || [], chests: L.chests.flat(),
  }));
}

function leaveInstance(s, toTown) {
  if (trades.has(s.id)) closeTrade(trades.get(s.id), "The trade was cancelled.");
  const inst = instances.get(s.inst);
  s.inst = 0;
  [s.x, s.z] = toTown ? [SPAWN.x, SPAWN.z] : inst && inst.rift ? [rifts.STONE.x, rifts.STONE.z + 2] : exitOf(inst ? inst.dIdx : 0);
  safeSend(s, JSON.stringify({ t: "dungeon", id: 0, x: s.x, z: s.z }));
}

/** Where a player is in the overworld (for saves): dungeon players are saved at the entrance. */
const overworldPos = (s) => {
  if (!s.inst) return [s.x, s.z];
  const inst = instances.get(s.inst) || { dIdx: 0 };
  return inst.rift ? [rifts.STONE.x, rifts.STONE.z + 2] : exitOf(inst.dIdx);
};

function cleanupInstances(t) {
  for (const inst of instances.values()) {
    let occupied = false;
    for (const s of sessions.values()) if (s.inWorld && s.inst === inst.id) { occupied = true; break; }
    if (occupied) { inst.lastActive = t; continue; }
    if (t - inst.lastActive < 120) continue;
    closeInstance(inst);
    log(`Dungeon ${inst.id} closed`);
  }
}

const dungeonHandlers = {
  denter(s, m) {
    const dIdx = Math.max(0, Math.min(DUNGEONS.length - 1, m.d | 0));
    const e = entrances()[dIdx];
    if (!s.inWorld || s.dead || s.inst || dist(s.x, s.z, e[0], e[1]) > 6) return;
    enterInstance(s, getInstance(s, 1, dIdx, false, m.df));
  },
  dstairs(s) {
    const cur = s.inst && instances.get(s.inst);
    if (!cur || !cur.layout.stairs || s.dead || dist(s.x, s.z, cur.layout.stairs[0], cur.layout.stairs[1]) > 5) return;
    enterInstance(s, getInstance(s, cur.depth + 1, cur.dIdx, false, cur.df));
  },
  dleave(s, m) {
    if (s.inst) leaveInstance(s, !!m.town);
  },
};

// =====================================================================================
// Admin module: accounts named in ADMINS (comma-separated, case-insensitive) or with "admin": true in their
// character file. Server-side commands are checked here; client-side helpers (map reveal, god mode, gold...)
// are only offered by the client when the server said "admin: true" at login.
// =====================================================================================

const ADMINS = new Set(String(process.env.ADMINS || "").split(",").map((n) => n.trim().toLowerCase()).filter(Boolean));
// An admin account, or ADMINS naming the account. Only account names count: anyone could register an account
// called like someone else's character, so character names must never grant rights.
const isAdmin = (s) => !!(s.acc && (s.acc.admin || ADMINS.has(s.acc.username.toLowerCase())));

const CYCLE_MS = 48 * 60000; // must match DayNight.CycleMinutes on the client
let clockOffset = 0;
const weather = new Weather({ seasonMinutes: Number(process.env.SEASON_MINUTES) || 120 });
setInterval(() => { if (weather.tick()) broadcast(weather.message()); }, 5000);
metrics.gauge("shadowfall_weather_info", "The current season and weather.", () => [[{ season: SEASONS[weather.season()], weather: weather.kind }, 1]]);
const worldClock = () => Date.now() + clockOffset;
const PHASE_HOURS = { dawn: 6, morning: 9, day: 12, noon: 12, dusk: 19, evening: 20, night: 23, midnight: 0 };

function findSession(name) {
  name = String(name || "").toLowerCase();
  for (const o of sessions.values()) if (o.inWorld && o.name.toLowerCase() === name) return o;
  return null;
}

function teleport(s, x, z) {
  [s.x, s.z] = [x, z];
  safeSend(s, JSON.stringify({ t: "tp", x: r2(x), z: r2(z) }));
}

/** Runs one admin command. Returns a short result line for the admin. */
/** A one-time reset code for a locked-out player (by account or character name), valid for a day. */
async function adminResetPassword(s, name) {
  if (!name) return "Usage: resetpw <account or character name>";
  let acc = await store.findAccount(name);
  if (!acc) {
    const ch = await store.findCharacter(name);
    if (ch) acc = await store.findAccountById(ch.accountId);
  }
  if (!acc) return `No account or character called ${name}.`;
  const code = A.randomCode(2);
  await store.createResetToken(acc.id, A.tokenHash(code), "admin", new Date(Date.now() + ADMIN_RESET_HOURS * 3600000));
  store.logEvent(acc.id, acc.username, `admin_reset_code:${s.name || "console"}`, s.ip);
  M.resets.inc({ method: "admin" });
  return `Reset code for account ${acc.username}: ${code}  (works once, for ${ADMIN_RESET_HOURS} hours). ` +
    `They choose "Forgot password?" > "I have a code" and enter it with their account name.`;
}

const ADMIN_COMMANDS = new Set(["tp", "tpto", "summon", "dungeon", "regen", "spawn", "killall", "time", "elites", "announce", "kick", "who", "resetpw", "give", "weather", "season", "invasion", "worldboss"]);

function runAdmin(s, c, a) {
  a = a || {};
  if (ADMIN_COMMANDS.has(c)) M.admin.inc({ cmd: c });
  switch (c) {
    case "tp": {
      const x = Number(a.x), z = Number(a.z);
      if (!Number.isFinite(x) || !Number.isFinite(z) || !world) return "Usage: tp <x> <z>";
      if (s.inst) leaveInstance(s, false);
      teleport(s, Math.min(Math.max(x, 1), world.w - 1), Math.min(Math.max(z, 1), world.h - 1));
      return `Teleported to ${Math.round(x)}, ${Math.round(z)}.`;
    }
    case "tpto": {
      const o = findSession(a.name);
      if (!o || o === s) return "No such player online.";
      if (o.inst && o.inst !== s.inst && instances.get(o.inst)) enterInstance(s, instances.get(o.inst));
      else if (!o.inst && s.inst) leaveInstance(s, false);
      teleport(s, o.x + 1, o.z);
      return `Teleported to ${o.name}.`;
    }
    case "summon": {
      const o = findSession(a.name);
      if (!o || o === s) return "No such player online.";
      if (s.inst && instances.get(s.inst)) enterInstance(o, instances.get(s.inst));
      else if (o.inst) leaveInstance(o, false);
      teleport(o, s.x + 1, s.z);
      sys(o, `${s.name} summoned you.`);
      return `Summoned ${o.name}.`;
    }
    case "dungeon": {
      const dIdx = typeof a.d === "string" && isNaN(a.d) ? dungeonIndex(a.d) : Math.max(0, Math.min(DUNGEONS.length - 1, a.d | 0));
      const def = DUNGEONS[dIdx];
      const depth = Math.max(1, Math.min(def.depths, a.l | 0 || 1));
      s.inst = 0;
      enterInstance(s, getInstance(s, depth, dIdx, !!a.fresh, a.df));
      return `Entered ${def.name}, depth ${depth}.`;
    }
    case "regen": {
      const cur = s.inst && instances.get(s.inst);
      if (!cur) return "You are not in a dungeon.";
      const inside = [...sessions.values()].filter((o) => o.inWorld && o.inst === cur.id);
      const fresh = getInstance(s, cur.depth, cur.dIdx, true);
      for (const o of inside) enterInstance(o, fresh);
      return `Regenerated ${DUNGEONS[fresh.dIdx].name}, depth ${fresh.depth} (seed ${fresh.seed}).`;
    }
    case "spawn": {
      const type = Object.keys(MONSTERS).find((k) => k.toLowerCase() === String(a.type || "").toLowerCase());
      if (!type) return `Unknown monster. Types: ${Object.keys(MONSTERS).join(", ")}`;
      const n = Math.max(1, Math.min(20, a.n | 0 || 1)), level = Math.max(1, Math.min(60, a.l | 0 || s.lvl));
      useGrid(s.inst || 0);
      let made = 0;
      for (let i = 0; i < n; i++)
        for (let k = 0; k < 20; k++) {
          const ang = Math.random() * Math.PI * 2, r = 3 + Math.random() * 3;
          const x = s.x + Math.cos(ang) * r, z = s.z + Math.sin(ang) * r;
          if (!walkable(x, z)) continue;
          const m = spawnMonster(type, level, x, z, null, s.inst || 0);
          m.leash = 60;
          if (a.elite) makeElite(m);
          made++;
          break;
        }
      useGrid(0);
      return `Spawned ${made} ${type} (level ${level}${a.elite ? ", elite" : ""}).`;
    }
    case "killall": {
      const r = Math.max(1, Math.min(200, Number(a.r) || 20));
      let n = 0;
      for (const m of [...monsters.values()])
        if (m.inst === (s.inst || 0) && dist(m.x, m.z, s.x, s.z) <= r) { damageMonster(m, s, m.hp + 1); n++; }
      return `Killed ${n} monsters within ${r} m.`;
    }
    case "time": {
      const want = PHASE_HOURS[String(a.phase || "").toLowerCase()];
      const hour = want !== undefined ? want : Number(a.phase);
      if (!Number.isFinite(hour)) return "Usage: time dawn|day|dusk|night|<hour>";
      const target = ((hour % 24) / 24) * CYCLE_MS;
      const cur = ((Date.now() % CYCLE_MS) + CYCLE_MS) % CYCLE_MS;
      clockOffset = target - cur;
      broadcast({ t: "clock", now: worldClock() });
      return `The time is now ${hour}:00.`;
    }
    case "weather": {
      const kind = String(a.kind || "").toLowerCase();
      if (!WEATHER_KINDS.includes(kind)) return `Usage: weather ${WEATHER_KINDS.join("|")} [minutes]  (now ${weather.kind})`;
      weather.set(kind, Math.max(1, Math.min(120, Number(a.n) || 15)), Number(a.i) || 0.9);
      broadcast(weather.message());
      return `The weather is now ${kind}.`;
    }
    case "season": {
      const i = SEASONS.indexOf(String(a.kind || "").toLowerCase());
      if (i < 0) return `Usage: season ${SEASONS.join("|")}  (now ${SEASONS[weather.season()]})`;
      weather.setSeason(i);
      broadcast(weather.message());
      return `It is now ${SEASONS[i]}.`;
    }
    case "invasion":
      return a.stop ? invasions.stop() : invasions.start(a.town || "", a.gate);
    case "worldboss":
      return a.stop ? worldBosses.stop() : worldBosses.start(a.name || "");
    case "elites": {
      const v = Number(a.chance);
      if (!Number.isFinite(v)) return `Elite chance is ${ELITE_CHANCE}.`;
      ELITE_CHANCE = Math.max(0, Math.min(1, v));
      return `Elite chance set to ${Math.round(ELITE_CHANCE * 100)}% (new spawns).`;
    }
    case "announce": {
      const text = String(a.text || "").replace(/[<>]/g, "").slice(0, 200);
      if (!text) return "Usage: announce <text>";
      broadcast({ t: "sys", msg: `[Announcement] ${text}` });
      return "Announced.";
    }
    case "kick": {
      const o = findSession(a.name);
      if (!o) return "No such player online.";
      if (o === s) return "You can't kick yourself.";
      fail(o, `You were kicked by ${s.name}.`);
      return `Kicked ${o.name}.`;
    }
    case "resetpw": return adminResetPassword(s, String(a.name || ""));
    case "give": {
      if (!s.ledger) return "Not in the world.";
      const what = String(a.what || ""), lvl = s.lvl + 2;
      const items = what === "legendary" ? [I.randomEquipment(lvl, 1, I.Rarity.Legendary, null, heroClass(s))]
        : what === "set" ? [I.randomEquipment(lvl, 1, I.Rarity.Set, null, heroClass(s))]
        : what === "gems" ? Array.from({ length: 5 }, () => I.randomGem(20))
        : what === "potions" ? [{ ...I.healthPotion(), Count: 10 }, { ...I.manaPotion(), Count: 10 }]
        : what === "gold" ? [] : null;
      if (!items) return "Usage: give gold|legendary|set|gems|potions";
      if (what === "gold") s.ledger.gold += Math.max(1, Math.min(1e6, parseInt(a.n, 10) || 1000));
      const drops = give(s, items);
      if (drops.length) safeSend(s, JSON.stringify({ t: "drops", drops }));
      ledgerChanged(s);
      return what === "gold" ? `Gold: ${s.ledger.gold}.` : `Gave ${items.map((x) => x.Name).join(", ")}.`;
    }
    case "who": {
      const list = [...sessions.values()].filter((o) => o.inWorld).map((o) => {
        const inst = o.inst && instances.get(o.inst);
        return `${o.id}|${o.name}|${o.lvl}|${inst ? DUNGEONS[inst.dIdx].name + " " + inst.depth : Math.round(o.x) + "," + Math.round(o.z)}`;
      });
      safeSend(s, JSON.stringify({ t: "admwho", items: list }));
      return null;
    }
  }
  return "Unknown admin command.";
}

/** "/a <command> ..." typed in chat: the same commands with plain arguments. */
function adminFromChat(s, line) {
  const [c, ...w] = line.trim().split(/\s+/);
  const rest = line.trim().slice(c.length).trim();
  switch ((c || "").toLowerCase()) {
    case "tp": return runAdmin(s, "tp", { x: w[0], z: w[1] });
    case "tpto": return runAdmin(s, "tpto", { name: w[0] });
    case "summon": return runAdmin(s, "summon", { name: w[0] });
    case "dungeon": return runAdmin(s, "dungeon", { d: w[0], l: w[1] });
    case "regen": return runAdmin(s, "regen");
    case "spawn": return runAdmin(s, "spawn", { type: w.filter((x) => isNaN(x) && x !== "elite").join(" "), l: w.find((x) => !isNaN(x)), n: w.filter((x) => !isNaN(x))[1], elite: w.includes("elite") });
    case "killall": return runAdmin(s, "killall", { r: w[0] });
    case "time": return runAdmin(s, "time", { phase: w[0] });
    case "weather": return runAdmin(s, "weather", { kind: w[0], n: w[1] });
    case "season": return runAdmin(s, "season", { kind: w[0] });
    case "elites": return runAdmin(s, "elites", { chance: w[0] });
    case "announce": return runAdmin(s, "announce", { text: rest });
    case "kick": return runAdmin(s, "kick", { name: w[0] });
    case "who": return runAdmin(s, "who");
    case "resetpw": return runAdmin(s, "resetpw", { name: w[0] });
    case "give": return runAdmin(s, "give", { what: w[0], n: w[1] });
    case "worldboss": return runAdmin(s, "worldboss", w[0] === "stop" ? { stop: true } : { name: rest });
    case "invasion": {
      const gate = ["north", "south", "east", "west"].includes((w[w.length - 1] || "").toLowerCase()) ? w.pop().toLowerCase() : undefined;
      return runAdmin(s, "invasion", w[0] === "stop" ? { stop: true } : { town: w.join(" "), gate });
    }
    default: return "Admin commands: tp x z, tpto name, summon name, dungeon <id|0-6> [depth], regen, spawn <type> [level] [count] [elite], killall [radius], time dawn|day|dusk|night, elites <0-1>, announce text, kick name, who, resetpw <account or character>, give gold [n]|legendary|set|gems|potions, weather clear|cloudy|rain|storm|fog [minutes], season spring|summer|autumn|winter, invasion [town] [north|south|east|west] | invasion stop, worldboss [name] | worldboss stop";
  }
}

// =====================================================================================
// Accounts: register, log in, recover, characters. Errors are "autherr" (the socket stays open so the
// player can try again); "error" is only used for things that end the connection.
// =====================================================================================

const authErr = (s, err) => safeSend(s, JSON.stringify({ t: "autherr", err }));
const authOk = (s, msg) => safeSend(s, JSON.stringify({ t: "authok", msg }));

async function sendAccount(s, extra) {
  const chars = await store.listCharacters(s.acc.id);
  safeSend(s, JSON.stringify({
    t: "account", user: s.acc.username, email: s.acc.email || "", mail: !!mailer,
    chars: chars.map((c) => ({ name: c.name, look: c.look, lvl: c.level })), ...extra,
  }));
}

/** Logs out every other connection of this account (a new login, or a password change). */
function kickOtherSessions(s, why) {
  for (const o of sessions.values())
    if (o !== s && o.acc && String(o.acc.id) === String(s.acc.id)) {
      leaveWorld(o, "offline");
      o.acc = null;
      fail(o, why);
    }
}

/** A successful login, registration or reset: the session now belongs to the account. */
async function signedIn(s, acc, event, extra = {}) {
  accountLimit.clear(acc.username.toLowerCase());
  s.acc = acc;
  kickOtherSessions(s, "You logged in from another location.");
  store.updateAccount(acc.id, { lastLogin: new Date() }).catch(() => {});
  store.logEvent(acc.id, acc.username, event, s.ip);
  // Accounts from before recovery codes (imported characters) get one now.
  if (!acc.recoveryHash && !extra.rc) {
    const rc = A.randomCode(4);
    const h = await A.hashRecovery(rc);
    await store.updateAccount(acc.id, { recoverySalt: h.salt, recoveryHash: h.hash });
    Object.assign(acc, { recoverySalt: h.salt, recoveryHash: h.hash });
    extra = { ...extra, rc, rcWhy: "new" };
  }
  await sendAccount(s, extra);
}

/** Checks the limits before a password attempt; returns false (and tells the player) when blocked. */
function allowAttempt(s, username) {
  const ipWait = ipLimit.blocked(s.ip), accWait = accountLimit.blocked(username.toLowerCase());
  if (!ipWait && !accWait) return true;
  M.logins.inc({ result: "locked" });
  const secs = Math.max(ipWait, accWait);
  authErr(s, `Too many wrong attempts. Try again in ${secs >= 90 ? Math.ceil(secs / 60) + " minutes" : secs + " seconds"}.`);
  return false;
}

function failedAttempt(s, username) {
  ipLimit.fail(s.ip);
  accountLimit.fail(username.toLowerCase());
}

/** Sets a new password and logs out everywhere else. */
async function setPassword(s, acc, pass) {
  const h = await A.hashPassword(pass);
  const sessionVersion = (acc.sessionVersion | 0) + 1;
  await store.updateAccount(acc.id, { salt: h.salt, hash: h.hash, sessionVersion });
  Object.assign(acc, { salt: h.salt, hash: h.hash, sessionVersion });
}

const authHandlers = {
  async login(s, m) {
    if (!s.hello || s.acc) return;
    const user = String(m.user || "").trim(), pass = String(m.pass || "");
    if (!user || !pass) return authErr(s, "Enter your account name and password.");
    if (!allowAttempt(s, user)) return;
    const acc = await store.findAccount(user);
    if (!acc || !(await A.checkPassword(acc, pass))) {
      failedAttempt(s, user);
      M.logins.inc({ result: "bad_password" });
      store.logEvent(acc ? acc.id : null, user, "login_failed", s.ip);
      if (!acc) {
        const ch = await store.findCharacter(user);
        if (ch) return authErr(s, `"${ch.name}" is a character name. Log in with the name of its account.`);
        return authErr(s, "No account with that name. Check the spelling, or create a new account.");
      }
      return authErr(s, "Wrong password. Forgot it? Use \"Forgot password?\" below.");
    }
    M.logins.inc({ result: "ok" });
    await signedIn(s, acc, "login");
  },

  async register(s, m) {
    if (!s.hello || s.acc) return;
    const user = String(m.user || "").trim(), pass = String(m.pass || ""), email = String(m.email || "").trim();
    if (!A.USERNAME_RE.test(user)) return authErr(s, `Account names must be ${A.NAME_RULE}.`);
    const bad = A.passwordProblem(pass);
    if (bad) return authErr(s, bad);
    if (email && !A.EMAIL_RE.test(email)) return authErr(s, "That email address doesn't look right.");
    if (registerLimit.blocked(s.ip)) return authErr(s, "Too many new accounts from your address. Try again later.");
    const rc = A.randomCode(4);
    const [p, r] = await Promise.all([A.hashPassword(pass), A.hashRecovery(rc)]);
    let acc;
    try {
      acc = await store.createAccount({ username: user, salt: p.salt, hash: p.hash, recoverySalt: r.salt, recoveryHash: r.hash, email: email || null });
    } catch (e) {
      if (e instanceof Taken) return authErr(s, e.what === "email" ? "That email address already belongs to an account." : "That account name is taken.");
      throw e;
    }
    registerLimit.fail(s.ip);
    M.logins.inc({ result: "new" });
    log(`New account: ${user}`);
    await signedIn(s, acc, "register", { rc, rcWhy: "register" });
  },

  async forgot(s, m) {
    if (!s.hello || s.acc) return;
    if (!mailer) return authErr(s, "This server can't send emails. Use your recovery code, or ask an admin for a reset code.");
    if (forgotLimit.blocked(s.ip)) return authErr(s, "Too many reset emails requested. Try again later.");
    forgotLimit.fail(s.ip);
    const who = String(m.user || "").trim();
    const acc = who.includes("@") ? await store.findAccountByEmail(who) : await store.findAccount(who);
    // The same answer either way, so this can't be used to find out which accounts or emails exist.
    authOk(s, "If that account has an email address, a reset code is on its way. Check your inbox (and spam folder).");
    if (!acc || !acc.email) return;
    const code = A.randomCode(2);
    await store.createResetToken(acc.id, A.tokenHash(code), "email", new Date(Date.now() + RESET_MINUTES * 60000));
    store.logEvent(acc.id, acc.username, "reset_email_sent", s.ip);
    mailer.sendReset(acc.email, acc.username, code, RESET_MINUTES).catch((e) => log("reset email failed", acc.username, e.message));
  },

  /** A new password with the account's recovery code, or a one-time code from an email or an admin. */
  async reset(s, m) {
    if (!s.hello || s.acc) return;
    const user = String(m.user || "").trim(), code = String(m.code || ""), pass = String(m.pass || "");
    const bad = A.passwordProblem(pass);
    if (bad) return authErr(s, bad);
    if (!user || !code) return authErr(s, "Enter your account name and the code.");
    if (!allowAttempt(s, user)) return;
    const acc = await store.findAccount(user);
    let method = null;
    if (acc && await A.checkRecovery(acc, code)) method = "recovery";
    else if (acc) {
      const tokenOk = await store.useResetToken(A.tokenHash(code), acc.id);
      if (tokenOk) method = "code";
    }
    if (!method) {
      failedAttempt(s, user);
      store.logEvent(acc ? acc.id : null, user, "reset_failed", s.ip);
      return authErr(s, "That code doesn't match this account (or it was already used or has expired).");
    }
    await setPassword(s, acc, pass);
    M.resets.inc({ method: method === "recovery" ? "recovery" : "code" });
    log(`Password reset for ${acc.username} (${method})`);
    let extra = { msg: "Your password has been changed." };
    if (method === "recovery") {
      // A recovery code works once: hand out a new one.
      const rc = A.randomCode(4);
      const h = await A.hashRecovery(rc);
      await store.updateAccount(acc.id, { recoverySalt: h.salt, recoveryHash: h.hash });
      Object.assign(acc, { recoverySalt: h.salt, recoveryHash: h.hash });
      extra = { ...extra, rc, rcWhy: "used" };
    }
    await signedIn(s, acc, method === "recovery" ? "reset_recovery" : "reset_code", extra);
  },

  async chpass(s, m) {
    if (!s.acc) return;
    const bad = A.passwordProblem(String(m.pass || ""));
    if (bad) return authErr(s, bad);
    if (!allowAttempt(s, s.acc.username)) return;
    if (!(await A.checkPassword(s.acc, String(m.old || "")))) { failedAttempt(s, s.acc.username); return authErr(s, "Your current password is wrong."); }
    await setPassword(s, s.acc, String(m.pass));
    kickOtherSessions(s, "Your password was changed. Log in again.");
    M.resets.inc({ method: "change" });
    store.logEvent(s.acc.id, s.acc.username, "password_changed", s.ip);
    authOk(s, "Password changed. Any other place you were logged in has been logged out.");
  },

  async setemail(s, m) {
    if (!s.acc) return;
    const email = String(m.email || "").trim();
    if (email && !A.EMAIL_RE.test(email)) return authErr(s, "That email address doesn't look right.");
    if (!allowAttempt(s, s.acc.username)) return;
    if (!(await A.checkPassword(s.acc, String(m.pass || "")))) { failedAttempt(s, s.acc.username); return authErr(s, "Wrong password."); }
    try {
      await store.updateAccount(s.acc.id, { email: email || null });
    } catch (e) {
      if (e instanceof Taken) return authErr(s, "That email address already belongs to another account.");
      throw e;
    }
    s.acc.email = email || null;
    store.logEvent(s.acc.id, s.acc.username, email ? "email_set" : "email_removed", s.ip);
    authOk(s, email ? `Email set to ${email}.` + (mailer ? "" : " (This server can't send emails yet, so it's only kept for later.)") : "Email removed.");
  },

  async newcode(s, m) {
    if (!s.acc) return;
    if (!allowAttempt(s, s.acc.username)) return;
    if (!(await A.checkPassword(s.acc, String(m.pass || "")))) { failedAttempt(s, s.acc.username); return authErr(s, "Wrong password."); }
    const rc = A.randomCode(4);
    const h = await A.hashRecovery(rc);
    await store.updateAccount(s.acc.id, { recoverySalt: h.salt, recoveryHash: h.hash });
    Object.assign(s.acc, { recoverySalt: h.salt, recoveryHash: h.hash });
    store.logEvent(s.acc.id, s.acc.username, "recovery_code_changed", s.ip);
    safeSend(s, JSON.stringify({ t: "rcode", rc }));
  },

  // ---- characters

  async play(s, m) {
    if (!s.acc || s.inWorld || s.pendingPlay) return;
    const ch = await store.findCharacter(String(m.name || ""));
    if (!ch || String(ch.accountId) !== String(s.acc.id)) return authErr(s, "That character isn't on your account.");
    beginPlay(s, ch);
  },

  async create(s, m) {
    if (!s.acc || s.inWorld || s.pendingPlay) return;
    const name = String(m.name || "").trim(), look = String(m.look || "");
    if (!A.USERNAME_RE.test(name)) return authErr(s, `Character names must be ${A.NAME_RULE}.`);
    if (!HERO_MODELS.includes(look)) return authErr(s, "Choose a class.");
    const existing = await store.listCharacters(s.acc.id);
    if (existing.length >= MAX_CHARACTERS) return authErr(s, `An account can have at most ${MAX_CHARACTERS} characters.`);
    let ch;
    try {
      ch = await store.createCharacter(s.acc.id, name, look);
    } catch (e) {
      if (e instanceof Taken) return authErr(s, "That character name is taken.");
      throw e;
    }
    store.logEvent(s.acc.id, s.acc.username, `character_created:${name}`, s.ip);
    log(`New character: ${name} (${look}) on ${s.acc.username}`);
    beginPlay(s, ch);
  },

  async delchar(s, m) {
    if (!s.acc || s.inWorld) return;
    if (!allowAttempt(s, s.acc.username)) return;
    if (!(await A.checkPassword(s.acc, String(m.pass || "")))) { failedAttempt(s, s.acc.username); return authErr(s, "Wrong password."); }
    const ch = await store.findCharacter(String(m.name || ""));
    if (!ch || String(ch.accountId) !== String(s.acc.id)) return authErr(s, "That character isn't on your account.");
    await store.deleteCharacter(ch.id);
    guilds.deleted(ch.name);
    store.logEvent(s.acc.id, s.acc.username, `character_deleted:${ch.name}`, s.ip);
    log(`Character deleted: ${ch.name} (${s.acc.username})`);
    await sendAccount(s, { msg: `${ch.name} has been deleted.` });
  },

  /** Back to character select. */
  async leave(s) {
    if (!s.acc || !s.inWorld) return;
    await leaveWorld(s, "select");
    s.char = null;
    await sendAccount(s);
  },
};

// =====================================================================================
// Items and gold (server-owned). Every character's bags, equipment, stash, gold and hired companions live in
// s.ledger; the client asks for changes ("iop") and gets the whole ledger back ("inv"). See items.js.
// =====================================================================================

const DROP_RANGE = 7;         // how close you must be to pick something up (latency slack included)
const DROP_LIFETIME = 5 * 60; // seconds before unclaimed loot disappears
let nextDropId = 1;

/** Loads the ledger when a character enters the world; new characters get the starter kit. */
function openLedger(s) {
  const save = s.char.save;
  const L = I.ledgerFromSave(save);
  if (!save) {
    L.eq[I.Slot.Weapon] = I.starterWeapon();
    L.eq[I.Slot.Chest] = I.starterChest();
    const hp = I.healthPotion(); hp.Count = 5; I.addItem(L.bag, hp);
    const mp = I.manaPotion(); mp.Count = 3; I.addItem(L.bag, mp);
    s.char.save = I.ledgerToSave(L, { level: 1, look: s.char.look });
  }
  // Quests whose rewards were paid (from older saves: the client's own list, once).
  L.questsDone = new Set(Array.isArray(save && save.questsDone) ? save.questsDone : (save && save.completedQuests) || []);
  s.ledger = L;
  s.drops = new Map();
  s.vendors = {};
  s.openedChests = new Set();
  s.lastGather = 0;
}

function sendInv(s) {
  const L = s.ledger;
  if (!L) return;
  safeSend(s, JSON.stringify({
    t: "inv", gold: L.gold, bag: L.bag.map((it) => it || {}), stash: L.stash.map((it) => it || {}),
    eq: Object.values(L.eq).filter(Boolean), comp: L.companions,
  }));
}

/** Changes went through: persist soon (the periodic save and saves on logout also write it) and tell the client. */
function ledgerChanged(s) {
  sendInv(s);
  if (!s.ledgerSaveTimer) s.ledgerSaveTimer = setTimeout(() => { s.ledgerSaveTimer = null; if (s.inWorld) saveCharacter(s); }, 2000);
}

/** An item action was refused (msg = why, shown to the player; empty = say nothing). Returns false: nothing changed. */
const ierr = (s, op, msg, extra = {}) => { safeSend(s, JSON.stringify({ t: "ierr", op, msg, ...extra })); return false; };
const iok = (s, op, extra = {}) => safeSend(s, JSON.stringify({ t: "iok", op, ...extra }));

/** Puts loot on the ground for one player (only they see and can take it). */
function dropFor(s, x, z, loot, inst) {
  const out = [];
  for (const l of loot) {
    const id = nextDropId++;
    const d = { id, x: r2(x), z: r2(z), inst: inst || 0, at: now(), gold: l.gold || 0, item: l.item || null };
    s.drops.set(id, d);
    out.push({ id, x: d.x, z: d.z, gold: d.gold, item: d.item || {} });
  }
  return out;
}

setInterval(() => {
  const t = now();
  for (const s of sessions.values()) if (s.drops) for (const [id, d] of s.drops) if (t - d.at > DROP_LIFETIME) s.drops.delete(id);
}, 30000);

const heroClass = (s) => (s.char && s.char.look) || "Knight";
const inTownNow = (s) => !s.inst && inTown(s.x, s.z);
const bagItem = (s, i) => (Number.isInteger(i) && i >= 0 && i < I.BAG_SIZE ? s.ledger.bag[i] : null);

/** Gives items (to the bags, or the ground at the player's feet when full). Returns drops for the client to show. */
function give(s, items) {
  const overflow = [];
  for (const it of items) {
    const left = I.addItem(s.ledger.bag, it);
    if (left > 0) overflow.push({ item: { ...it, Count: left } });
  }
  if (!overflow.length) return [];
  sys(s, "Your bags are full: the rest is on the ground.");
  return dropFor(s, s.x, s.z, overflow, s.inst);
}

/** Salvages gear: materials and any socketed gems into the bags (or at the hero's feet). */
function salvageInto(s, gear) {
  const totals = new Map();
  const items = [];
  for (const it of gear) {
    for (const [name, n] of I.salvageYield(it)) totals.set(name, (totals.get(name) || 0) + n);
    for (const g of it.Gems || []) items.push(I.byName(g));
  }
  for (const [name, n] of totals) items.push({ ...I.material(name), Count: n });
  return { drops: give(s, items), names: [...totals].map(([name, n]) => `${n} ${name}`) };
}

const VENDOR_RESTOCK = I.RESTOCK_MS / 1000;
function vendorFor(s, kind) {
  let v = s.vendors[kind];
  const t = now();
  if (!v || t >= v.restockAt || Math.abs(s.lvl - v.level) >= 2) {
    v = s.vendors[kind] = { items: I.vendorStock(kind, s.lvl, heroClass(s)), restockAt: t + VENDOR_RESTOCK, level: s.lvl };
  }
  return v;
}

const itemOps = {
  equip(s, m) {
    const it = bagItem(s, m.i);
    if (!it || it.Kind !== I.Kind.Equipment || !(it.Slot > 0)) return ierr(s, "equip", "That can't be worn.");
    if (it.RequiredLevel > s.lvl) return ierr(s, "equip", `You must be level ${it.RequiredLevel} to equip ${it.Name}.`);
    const old = s.ledger.eq[it.Slot] || null;
    s.ledger.eq[it.Slot] = it;
    s.ledger.bag[m.i] = old;
    return true;
  },
  unequip(s, m) {
    const slot = m.slot | 0, it = s.ledger.eq[slot];
    if (!it) return false;
    if (I.freeSlots(s.ledger.bag) === 0) return ierr(s, "unequip", "Your inventory is full.");
    delete s.ledger.eq[slot];
    I.addItem(s.ledger.bag, it);
    return true;
  },
  /** Drinks or eats one (the client applies the effect). */
  use(s, m) {
    const it = bagItem(s, m.i);
    if (!it || it.Kind !== I.Kind.Consumable) return false;
    if (--it.Count <= 0) s.ledger.bag[m.i] = null;
    return true;
  },
  drop(s, m) {
    const it = bagItem(s, m.i);
    if (!it) return false;
    s.ledger.bag[m.i] = null;
    safeSend(s, JSON.stringify({ t: "drops", drops: dropFor(s, s.x, s.z, [{ item: it }], s.inst) }));
    return true;
  },
  pickup(s, m) {
    const d = s.drops.get(m.id | 0);
    if (!d) return ierr(s, "pickup", "", { id: m.id | 0 });
    if ((d.inst || 0) !== (s.inst || 0) || dist(d.x, d.z, s.x, s.z) > DROP_RANGE) return ierr(s, "pickup", "Too far away.", { id: d.id });
    if (d.item) {
      const left = I.addItem(s.ledger.bag, d.item);
      if (left === d.item.Count) return ierr(s, "pickup", "Your bags are full.", { id: d.id });
      if (left > 0) { d.item.Count = left; ierr(s, "pickup", "Your bags are full.", { id: d.id, n: left }); return true; }
    }
    s.ledger.gold += d.gold;
    s.drops.delete(d.id);
    iok(s, "pickup", { id: d.id });
    return true;
  },
  sort(s) { I.sortSlots(s.ledger.bag); return true; },
  stash(s, m) {
    const it = bagItem(s, m.i);
    if (!it) return false;
    if (!inTownNow(s)) return ierr(s, "stash", "Your stash is in town.");
    if (I.addItem(s.ledger.stash, it) > 0) return ierr(s, "stash", "Your stash is full.");
    s.ledger.bag[m.i] = null;
    return true;
  },
  unstash(s, m) {
    const i = m.i | 0, it = s.ledger.stash[i];
    if (!it) return false;
    if (!inTownNow(s)) return ierr(s, "unstash", "Your stash is in town.");
    if (I.addItem(s.ledger.bag, it) > 0) return ierr(s, "unstash", "Your bags are full.");
    s.ledger.stash[i] = null;
    return true;
  },
  /** A gem from the bags into an item in the bags (to = "bag", j) or worn (to = "eq", slot). */
  socket(s, m) {
    const g = bagItem(s, m.i);
    if (!g || g.Kind !== I.Kind.Gem) return false;
    const target = m.to === "eq" ? s.ledger.eq[m.slot | 0] : bagItem(s, m.j);
    if (!target || target.Kind !== I.Kind.Equipment) return false;
    target.Gems = target.Gems || [];
    if (target.Gems.length >= (target.Sockets | 0)) return ierr(s, "socket", target.Sockets ? `${target.Name} has no empty sockets.` : `${target.Name} has no sockets.`);
    target.Gems.push(g.Name);
    if (--g.Count <= 0) s.ledger.bag[m.i] = null;
    iok(s, "socket", { name: g.Name, target: target.Name });
    return true;
  },
  /** Breaks a piece of gear in the bags into materials (and gives back its gems). At a blacksmith in town. */
  salvage(s, m) {
    const it = bagItem(s, m.i);
    if (!it || it.Kind !== I.Kind.Equipment) return false;
    if (!inTownNow(s)) return ierr(s, "salvage", "Find a blacksmith in town to salvage gear.");
    s.ledger.bag[m.i] = null;
    const got = salvageInto(s, [it]);
    iok(s, "salvage", { name: it.Name, n: 1, items: got.names, drops: got.drops });
    return true;
  },
  /** Salvages every common and magic piece of gear in the bags. */
  salvagejunk(s) {
    if (!inTownNow(s)) return ierr(s, "salvage", "Find a blacksmith in town to salvage gear.");
    const junk = [];
    s.ledger.bag.forEach((it, i) => {
      if (it && it.Kind === I.Kind.Equipment && it.Rarity <= I.Rarity.Magic) { junk.push(it); s.ledger.bag[i] = null; }
    });
    if (!junk.length) return ierr(s, "salvagejunk", "You have no common or magic gear to salvage.");
    const got = salvageInto(s, junk);
    iok(s, "salvagejunk", { n: junk.length, items: got.names, drops: got.drops });
    return true;
  },
  /** Rerolls one affix of a piece of gear in the bags for materials and gold (see I.reforge). */
  reforge(s, m) {
    const it = bagItem(s, m.i), idx = m.j | 0;
    if (!it || it.Kind !== I.Kind.Equipment || !it.Mods || idx < 0 || idx >= it.Mods.length) return false;
    if (!inTownNow(s)) return ierr(s, "reforge", "Find a blacksmith in town to reforge gear.");
    const cost = I.reforgeCost(it);
    if (!cost) return ierr(s, "reforge", "Only magic gear and better can be reforged.");
    if (it.Reforged && it.Reforged !== idx + 1) return ierr(s, "reforge", "This item has been reforged before: only that same property can be reforged again.");
    if (s.ledger.gold < cost.gold) return ierr(s, "reforge", `Reforging this costs ${cost.gold} gold.`);
    for (const [name, n] of cost.mats) if (I.countOf(s.ledger.bag, name) < n) return ierr(s, "reforge", `Reforging this needs ${n} ${name}. Salvage gear for more.`);
    for (const [name, n] of cost.mats) I.removeByName(s.ledger.bag, name, n);
    s.ledger.gold -= cost.gold;
    const mod = I.reforge(it, idx);
    iok(s, "reforge", { name: it.Name, j: idx, n: mod.Stat, gold: cost.gold, k: String(mod.Value) });
    return true;
  },
  /** Vex fuses three gems of a kind into one of the next quality. */
  fuse(s) {
    if (!inTownNow(s)) return ierr(s, "fuse", "Find a curio dealer in town.");
    for (let tier = 0; tier < 2; tier++)
      for (const type of I.GEM_TYPES) {
        const name = `${I.GEM_TIERS[tier]} ${type}`;
        if (I.countOf(s.ledger.bag, name) < 3) continue;
        const cost = tier === 0 ? 50 : 250;
        if (s.ledger.gold < cost) return ierr(s, "fuse", `Vex wants ${cost} gold to fuse ${name}s.`);
        I.removeByName(s.ledger.bag, name, 3);
        s.ledger.gold -= cost;
        const better = I.gem(type, tier + 1);
        const drops = give(s, [better]);
        iok(s, "fuse", { name: better.Name, gold: cost, drops });
        return true;
      }
    return ierr(s, "fuse", "You need three gems of the same kind and quality (Chipped or Flawless).");
  },
  sell(s, m) {
    const it = bagItem(s, m.i);
    if (!it) return false;
    if (!inTownNow(s)) return ierr(s, "sell", "Find a merchant in town to sell.");
    const value = it.Value * Math.max(1, it.Count);
    s.ledger.bag[m.i] = null;
    s.ledger.gold += value;
    iok(s, "sell", { name: it.Name, n: it.Count, gold: value });
    return true;
  },
  /** Sells every common item and material in the bags. */
  sellcommon(s) {
    if (!inTownNow(s)) return ierr(s, "sell", "Find a merchant in town to sell.");
    let gold = 0, n = 0;
    s.ledger.bag.forEach((it, i) => {
      if (!it || it.Kind === I.Kind.Consumable || it.Kind === I.Kind.Gem) return;
      if (it.Kind === I.Kind.Equipment && it.Rarity !== I.Rarity.Common) return;
      gold += it.Value * Math.max(1, it.Count);
      n++;
      s.ledger.bag[i] = null;
    });
    s.ledger.gold += gold;
    iok(s, "sellcommon", { n, gold });
    return true;
  },
  /** The vendor's current stock (rotating equipment is rolled per player). */
  vendor(s, m) {
    const kind = String(m.k || "");
    if (!I.VENDOR_KINDS.includes(kind)) return false;
    const v = vendorFor(s, kind);
    safeSend(s, JSON.stringify({ t: "stock", k: kind, stock: v.items, restock: Math.max(0, Math.round(v.restockAt - now())) }));
    return false;
  },
  buy(s, m) {
    const kind = String(m.k || "");
    if (!I.VENDOR_KINDS.includes(kind)) return false;
    if (!inTownNow(s)) return ierr(s, "buy", "The merchants are in town.");
    const v = vendorFor(s, kind), it = v.items[m.i | 0];
    if (!it || (m.name && it.Name !== m.name)) {
      safeSend(s, JSON.stringify({ t: "stock", k: kind, stock: v.items, restock: Math.max(0, Math.round(v.restockAt - now())) }));
      return ierr(s, "buy", "That's no longer for sale.");
    }
    const n = I.isStackable(it) ? Math.max(1, Math.min(20, m.n | 0 || 1)) : 1;
    const cost = I.price(it) * n;
    if (s.ledger.gold < cost) return ierr(s, "buy", "You don't have enough gold.");
    const bought = I.isStackable(it) ? { ...I.byName(it.Name), Count: n } : it;
    if (!I.fits(s.ledger.bag, [bought])) return ierr(s, "buy", "Your bags are full.");
    I.addItem(s.ledger.bag, bought);
    s.ledger.gold -= cost;
    if (!I.isStackable(it)) v.items.splice(m.i | 0, 1);
    iok(s, "buy", { name: it.Name, n, gold: cost });
    safeSend(s, JSON.stringify({ t: "stock", k: kind, stock: v.items, restock: Math.max(0, Math.round(v.restockAt - now())) }));
    return true;
  },
  craft(s, m) {
    const r = I.RECIPES[String(m.name || "")];
    if (!r) return false;
    const lvl = I.skillLevel(s.char.save, r.skill);
    if (lvl < r.level) return ierr(s, "craft", `You need ${r.skill} level ${r.level} to do that.`);
    if (!I.removeByName(s.ledger.bag, r.input, r.count)) return ierr(s, "craft", `You need ${r.count} ${r.input}.`);
    if (r.fail && Math.random() < Math.max(0.03, Math.min(0.45, 0.45 - (lvl - r.level) * 0.04))) {
      give(s, [I.material(r.fail)]);
      iok(s, "craft", { k: m.name, name: r.fail, burnt: true });
      return true;
    }
    const made = r.make(lvl, heroClass(s));
    const drops = give(s, [made]);
    iok(s, "craft", { k: m.name, name: made.Name, rarity: made.Rarity, drops });
    return true;
  },
  /** A log, ore or fish from a resource node (the node and the success roll are the client's; the rate and level are checked here). */
  gather(s, m) {
    const name = String(m.name || ""), g = I.GATHER[name];
    if (!g) return false;
    const t = now();
    if (t - s.lastGather < 1.2) return ierr(s, "gather", "");
    s.lastGather = t;
    if (I.skillLevel(s.char.save, g[0]) < g[1]) return ierr(s, "gather", `You need ${g[0]} level ${g[1]}.`);
    if (!I.fits(s.ledger.bag, [I.material(name)])) return ierr(s, "gather", "Your inventory is full.");
    I.addItem(s.ledger.bag, I.material(name));
    return true;
  },
  /** Turning in a quest: collect items are taken, the gold and item reward paid, once per character. */
  quest(s, m) {
    const id = String(m.k || ""), q = I.GAMEDATA.quests[id];
    if (!q) return false;
    if (s.ledger.questsDone.has(id)) return ierr(s, "quest", "You already finished that quest.", { k: id });
    if (q.type === "Collect" && !I.removeByName(s.ledger.bag, q.target, q.count)) return ierr(s, "quest", `You need ${q.count} ${q.target}.`);
    s.ledger.questsDone.add(id);
    s.ledger.gold += q.gold;
    const reward = q.itemLevel > 0 ? I.randomEquipment(q.itemLevel, 0, q.rarity, null, heroClass(s)) : null;
    const drops = reward ? give(s, [reward]) : [];
    iok(s, "quest", { k: id, gold: q.gold, item: reward ? reward.Name : "", rarity: reward ? reward.Rarity : 0, drops });
    return true;
  },
  hire(s, m) {
    const id = String(m.k || ""), c = I.GAMEDATA.companions[id];
    if (!c || s.ledger.companions.includes(id)) return false;
    const mount = id.startsWith("mount:");
    if (!inTownNow(s)) return ierr(s, "hire", "Beastmaster Orla is in Hollowmere.");
    if (s.lvl < c.level) return ierr(s, "hire", mount ? `You need to be level ${c.level} to ride that.` : `They won't follow anyone below level ${c.level}.`);
    if (s.ledger.gold < c.price) return ierr(s, "hire", `You need ${c.price} gold.`);
    s.ledger.gold -= c.price;
    s.ledger.companions.push(id);
    iok(s, "hire", { k: id });
    return true;
  },
  /** Resetting talents costs 25 gold per level (the talents themselves are the client's). */
  respec(s) {
    if (!inTownNow(s)) return ierr(s, "respec", "You can only reset your talents in town.");
    const cost = 25 * s.lvl;
    if (s.ledger.gold < cost) return ierr(s, "respec", `Resetting your talents costs ${cost} gold.`);
    s.ledger.gold -= cost;
    iok(s, "respec", { gold: cost });
    return true;
  },
  /** A treasure chest in a dungeon level: once per player. */
  chest(s, m) {
    const inst = s.inst && instances.get(s.inst);
    const i = m.i | 0, c = inst && inst.layout.chests[i];
    if (!c) return false;
    if (dist(c[0], c[1], s.x, s.z) > DROP_RANGE) return ierr(s, "chest", "Too far away.");
    const key = `${inst.id}:${i}`;
    if (s.openedChests.has(key)) return ierr(s, "chest", "");
    s.openedChests.add(key);
    const level = Math.max(1, inst.depth * 3 + DUNGEONS[inst.dIdx].minLevel);
    safeSend(s, JSON.stringify({ t: "drops", chest: i, drops: dropFor(s, c[0], c[1], I.rollChest(level, heroClass(s), DIFFICULTIES[inst.df || 0].loot), s.inst) }));
    return false;
  },
};

const ECONOMY_OPS = new Set(Object.keys(itemOps));

/** Gold lost on death (15%), taken by the server when the client reports dying. */
function deathPenalty(s) {
  if (!s.ledger) return;
  const lost = Math.floor(s.ledger.gold * 0.15);
  if (lost <= 0) return;
  s.ledger.gold -= lost;
  iok(s, "death", { gold: lost });
  ledgerChanged(s);
}

/** The achievement ids a save says were earned ("id@date"). */
function earnedFrom(save) {
  const ids = new Set();
  for (const e of (save && Array.isArray(save.ach) ? save.ach : [])) {
    const id = String(e).split("@")[0];
    if (I.GAMEDATA.achievements[id]) ids.add(id);
  }
  return ids;
}

/** The title a player may wear: from an achievement they have earned that gives one, else none. */
function titleOf(s, id) {
  const a = I.GAMEDATA.achievements[String(id || "")];
  return a && a.title && s.earned && s.earned.has(String(id)) ? a.title : "";
}

/** Character chosen: sync the world map if needed, then enter. */
function beginPlay(s, ch) {
  for (const o of sessions.values())
    if (o !== s && o.inWorld && o.char && String(o.char.id) === String(ch.id)) { leaveWorld(o, "offline"); fail(o, "You logged in from another location."); }
  s.char = ch;
  s.pendingPlay = true;
  if (!world) return safeSend(s, JSON.stringify({ t: "needworld" }));
  if (s.clientHash === world.hash) {
    if (newerBuild(s.build, world.build)) { world.build = s.build; saveWorld(); } // a new build, same map
    return completeLogin(s);
  }
  if (newerBuild(s.build, world.build)) {
    // A newer game with a changed map: it replaces the stored map (see the world handler).
    s.replacesWorld = true;
    return safeSend(s, JSON.stringify({ t: "needworld" }));
  }
  if (newerBuild(world.build, s.build)) { s.pendingPlay = false; return sendReload(s, latestBuild() || world.build); }
  // Same build, different map: generation should be deterministic, so this is a bug. Don't lock the player out:
  // they play on the server's map (walls and monsters follow it).
  M.worldMismatch.inc();
  log(`World mismatch within build ${s.build || "?"}: client ${s.clientHash}, server ${world.hash} (${s.char.name} uses the server's map)`);
  safeSend(s, JSON.stringify({ t: "grid", w: world.w, h: world.h, cells: world.cells, hash: world.hash }));
}

const handlers = {
  ...authHandlers,

  iop(s, m) {
    if (!s.inWorld || !s.ledger) return;
    const op = String(m.op || "");
    if (!ECONOMY_OPS.has(op)) return;
    // Drinking a potion is fine mid-trade (if it was offered, the trade is called off when accepted).
    if (trades.has(s.id) && op !== "vendor" && op !== "use") return ierr(s, op, "Finish or cancel your trade first.");
    M.itemOps.inc({ op });
    if (s.dead && op !== "vendor") { ierr(s, op, ""); if (op === "use") sendInv(s); return; }
    if (itemOps[op](s, m) === true) ledgerChanged(s);
    else if (op === "use") sendInv(s); // the client already took one out of the stack: put it right
  },
  ...partyHandlers,
  ...dungeonHandlers,

  adm(s, m) {
    if (!s.inWorld) return;
    if (!s.admin) return sys(s, "You are not an admin.");
    Promise.resolve(runAdmin(s, String(m.c || ""), m)).then((res) => res && sys(s, `[admin] ${res}`));
    log(`admin ${s.name}: ${m.c}`);
  },

  // ---- connecting: version check, then the account messages in authHandlers, then play/create

  hello(s, m) {
    if (s.hello) return;
    if (m.ver !== PROTOCOL_VERSION) return fail(s, "Your game client is out of date. Refresh the page (Ctrl+F5).");
    s.hello = true;
    s.clientHash = String(m.hash || "");
    s.build = String(m.build || "").slice(0, 40);
    const latest = latestBuild();
    if (latest && newerBuild(latest, s.build)) return sendReload(s, latest);
    safeSend(s, JSON.stringify({ t: "hi", mail: !!mailer }));
  },

  world(s, m) {
    if (!s.pendingPlay) return;
    if (!world || s.replacesWorld) {
      const w = parseInt(m.w, 10), h = parseInt(m.h, 10);
      const decoded = w > 0 && h > 0 && w * h <= 1 << 20 ? decodeWorld(w, h, String(m.cells || "")) : null;
      if (!decoded || decoded.hash !== m.hash || decoded.hash !== s.clientHash) return fail(s, "World upload was invalid.");
      s.replacesWorld = false;
      if (!world || decoded.hash !== world.hash) {
        if (world) {
          log(`World changed with game build ${s.build || "?"} (${world.hash} -> ${decoded.hash}): respawning the overworld's monsters`);
          // Everyone still playing is on the old game: save them and have them reload into the new one.
          for (const o of sessions.values())
            if (o !== s && o.inWorld) { leaveWorld(o, "offline"); sendReload(o, s.build); }
          for (const [id, mon] of monsters) if (mon.inst === 0) monsters.delete(id);
          spawners.length = 0;
        }
        world = decoded;
        world.cells = String(m.cells);
        world.build = s.build;
        saveWorld();
        log(`World received from ${s.char.name}: ${world.hash}`);
        initSpawners();
      }
    } else if (m.hash !== world.hash) {
      return fail(s, "Your game client doesn't match this server's world.");
    }
    completeLogin(s);
  },

  state(s, m) {
    if (!s.inWorld) return;
    const x = Number(m.x), z = Number(m.z);
    const grid = s.inst ? (instances.get(s.inst) || {}).grid : world;
    if (Number.isFinite(x) && Number.isFinite(z) && grid) {
      s.x = Math.min(Math.max(x, 0), grid.w);
      s.z = Math.min(Math.max(z, 0), grid.h);
    }
    s.ry = Number(m.ry) || 0;
    s.hp = Number(m.hp) || 0;
    s.mhp = Number(m.mhp) || 1;
    s.mp = Math.max(0, Number(m.mp) || 0);
    s.mmp = Math.max(0, Number(m.mmp) || 0);
    s.lvl = Math.max(1, Math.min(100, parseInt(m.lvl, 10) || 1));
    s.pl = Math.max(0, Math.min(100000, parseInt(m.pl, 10) || 0)); // paragon level
    if (m.dead && !s.dead) { M.deaths.inc(); deathPenalty(s); }
    s.mv = !!m.mv; s.atk = !!m.atk; s.dead = !!m.dead;
    s.look = { body: String(m.body || "").slice(0, 6), legs: String(m.legs || "").slice(0, 6), weapon: String(m.weapon || "").slice(0, 6), helm: String(m.helm || "").slice(0, 6),
      mdl: HERO_MODELS.includes(m.mdl) ? m.mdl : "Knight",
      wk: ["sword", "axe", "mace", "dagger", "staff"].includes(m.wk) ? m.wk : "",
      cp: COMPANIONS.includes(m.cp) ? m.cp : "",
      mt: !s.inst && typeof m.mt === "string" && m.mt && s.ledger && s.ledger.companions.includes("mount:" + m.mt) ? m.mt : "", // only mounts they own
      ti: titleOf(s, m.ti) };
  },

  // Duels (duel.js)
  dreq(s, m) { if (s.inWorld) duels.challenge(s, m.id); },
  dans(s, m) { if (s.inWorld) duels.answer(s, !!m.yes); },
  dhit(s, m) { if (s.inWorld && !s.dead) duels.hit(s, m.id, m.dmg, 100 + s.lvl * 60); },
  dyield(s) { if (s.inWorld) duels.yieldDuel(s); },
  rinfo(s) { if (s.inWorld) rifts.info(s); },
  ropen(s, m) { if (s.inWorld) rifts.open(s, m.n); },
  ganswer(s, m) { if (s.inWorld) guilds.answer(s, !!m.yes); },

  hit(s, m) {
    if (!s.inWorld || s.dead) return;
    const mon = monsters.get(m.mid);
    if (!mon || mon.inst !== (s.inst || 0) || dist(mon.x, mon.z, s.x, s.z) > 30) return;
    const cap = 100 + s.lvl * 60; // sanity cap on reported damage
    const dmg = Math.min(Math.max(parseInt(m.dmg, 10) || 0, 0), cap);
    if (dmg > 0) damageMonster(mon, s, dmg);
  },

  slow(s, m) {
    if (!s.inWorld) return;
    const mon = monsters.get(m.mid);
    if (!mon || mon.inst !== (s.inst || 0) || dist(mon.x, mon.z, s.x, s.z) > 12) return;
    mon.slowUntil = now() + Math.min(Number(m.dur) || 0, 5);
    if (mon.state !== "chase") aggro(mon, s.id);
  },

  stun(s, m) {
    if (!s.inWorld) return;
    const mon = monsters.get(m.mid);
    if (!mon || mon.inst !== (s.inst || 0) || dist(mon.x, mon.z, s.x, s.z) > 16) return;
    let dur = Math.min(Number(m.dur) || 0, 3);
    if (mon.def.boss) dur *= 0.4; // bosses shrug it off quickly
    mon.stunUntil = Math.max(mon.stunUntil, now() + dur);
    if (mon.state !== "chase") aggro(mon, s.id);
  },

  vanish(s, m) {
    if (!s.inWorld || s.dead) return;
    s.hiddenUntil = now() + Math.min(Number(m.dur) || 0, 6);
  },

  chat(s, m) {
    if (!s.inWorld) return;
    const t = now();
    if (t - (s.lastChat || 0) < 0.5) return;
    s.lastChat = t;
    const msg = String(m.msg || "").replace(/[<>]/g, "").trim().slice(0, 200);
    if (!msg) return;
    if (msg === "/who") {
      const names = [...sessions.values()].filter((o) => o.inWorld).map((o) => `${o.name} (${o.lvl})`);
      return safeSend(s, JSON.stringify({ t: "sys", msg: `${names.length} online: ${names.join(", ")}` }));
    }
    const [cmd, ...rest] = msg.split(" ");
    const arg = rest.join(" ").trim();
    if (cmd.toLowerCase() === "/a" || cmd.toLowerCase() === "/admin") {
      if (!s.admin) return sys(s, "You are not an admin.");
      Promise.resolve(adminFromChat(s, arg)).then((res) => res && sys(s, `[admin] ${res}`));
      log(`admin ${s.name}: ${arg}`);
      return;
    }
    switch (cmd.toLowerCase()) {
      case "/p": case "/party": {
        const p = partyOf(s);
        if (!p) return sys(s, "You are not in a party.");
        if (!arg) return;
        const data = JSON.stringify({ t: "chat", ch: "p", id: s.id, name: s.name, msg: arg });
        for (const o of partyMembers(p)) safeSend(o, data);
        return;
      }
      case "/w": case "/whisper": case "/tell": {
        const [to, ...words] = rest;
        const target = findOnline(to);
        const text = words.join(" ").trim();
        if (!target) return sys(s, `No player named "${String(to || "").slice(0, 16)}" is online.`);
        if (!text) return;
        safeSend(target, JSON.stringify({ t: "chat", ch: "w", id: s.id, name: s.name, msg: text }));
        safeSend(s, JSON.stringify({ t: "chat", ch: "wto", id: s.id, name: target.name, msg: text }));
        return;
      }
      case "/invite": case "/inv": return invite(s, arg);
      case "/guild": case "/g": case "/gchat": case "/ginvite": case "/gleave": case "/gkick": case "/gpromote": case "/gdemote": case "/gleader": case "/gmotd":
        return guilds.command(s, cmd.toLowerCase(), rest);
      case "/leave": return partyHandlers.pleave(s);
      default:
        if (cmd.startsWith("/")) return sys(s, "Commands: /p party chat, /g guild chat, /w name whisper, /invite name, /leave, /guild, /who");
    }
    broadcast({ t: "chat", id: s.id, name: s.name, msg });
  },

  // Emotes (/wave, /dance...): shown to players nearby, who also get the "Alice waves." line.
  /** An achievement earned: tell the party (wherever they are) and the players nearby, once per achievement. */
  ach(s, m) {
    if (!s.inWorld) return;
    const id = String(m.id || ""), a = I.GAMEDATA.achievements[id];
    const t = now();
    if (!a || s.earned.has(id) || t - (s.lastAch || 0) < 0.3) return;
    s.lastAch = t;
    s.earned.add(id);
    M.achievements.inc();
    const msg = JSON.stringify({ t: "ach", id: s.id, name: s.name, k: a.name });
    const p = partyOf(s);
    for (const o of sessions.values()) {
      if (o === s || !o.inWorld) continue;
      const near = (o.inst || 0) === (s.inst || 0) && dist(o.x, o.z, s.x, s.z) < PLAYER_VIEW;
      if (near || (p && p.members.has(o.id))) safeSend(o, msg);
    }
  },

  emote(s, m) {
    if (!s.inWorld || s.dead) return;
    const t = now();
    if (t - (s.lastEmote || 0) < 0.8) return;
    s.lastEmote = t;
    const e = String(m.e || "");
    if (!EMOTES.includes(e)) return;
    const msg = JSON.stringify({ t: "emote", id: s.id, name: s.name, e });
    for (const o of sessions.values())
      if (o !== s && o.inWorld && (o.inst || 0) === (s.inst || 0) && dist(o.x, o.z, s.x, s.z) < PLAYER_VIEW) safeSend(o, msg);
  },

  fx(s, m) {
    if (!s.inWorld) return;
    const t = now();
    if (t - (s.lastFx || 0) < 0.1) return;
    s.lastFx = t;
    const k = String(m.k || "");
    if (!FX_KINDS.has(k)) return;
    const msg = JSON.stringify({ t: "fx", id: s.id, k, x: r2(+m.x || 0), z: r2(+m.z || 0), tx: r2(+m.tx || 0), tz: r2(+m.tz || 0) });
    for (const o of sessions.values())
      if (o !== s && o.inWorld && (o.inst || 0) === (s.inst || 0) && dist(o.x, o.z, s.x, s.z) < PLAYER_VIEW) safeSend(o, msg);
  },

  save(s, m) {
    if (!s.inWorld || !s.char || !m.save || typeof m.save !== "object") return;
    const size = JSON.stringify(m.save).length;
    if (size > 256 * 1024) return;
    const lvl = parseInt(m.save.level, 10);
    if (!(lvl >= 1 && lvl <= 100)) return;
    s.char.save = m.save;   // items, gold and companions are the server's: saveCharacter writes the ledger over them
    for (const id of earnedFrom(m.save)) s.earned.add(id);
    saveCharacter(s);
  },
};

function onDisconnect(s) {
  sessions.delete(s.id);
  leaveWorld(s, "offline");
}

// =====================================================================================
// Game loop: monster AI + snapshots
// =====================================================================================

let tickCount = 0;
// Town invasions (invasion.js): monsters attack a walled town's gate in waves now and then.
const invasions = createInvasions({
  TOWNS, SPAWNERS, MONSTERS, monsters, sessions, spawnMonster, makeElite, findPath, nearestWalkable, moveAlongPath, speedOf,
  monsterAttack, broadcast, safeSend, rollLoot: I.rollLoot, dropFor, heroClass, log, now, rand, dist, r2, metrics: M.invasions,
  deep: (x, z) => x >= OLD_SIZE && z >= OLD_SIZE,
});

// Guilds (guild.js): a name, a tag before members' names, guild chat and ranks; stored with the accounts.
const guilds = createGuilds({ store, sessions, safeSend, sys, findOnline, log, now, ledgerChanged: (s) => ledgerChanged(s) });
metrics.gauge("shadowfall_guilds", "Guilds.", () => guilds.count());

// Greater rifts (rift.js): timed tiers opened at the Rift Stone, with a leaderboard.
const rifts = createRifts({
  store, DUNGEONS, dungeonGen, instances, closeInstance, nextInstanceId: () => nextInstanceId++, monsters, sessions, spawnMonster, makeElite, walkable, useGrid,
  partyOf, partyMembers, partyKey, enterInstance, safeSend, sys, sendNear, broadcast, dropFor, rollChest: I.rollChest, heroClass, ledgerChanged,
  log, now, rand, randInt, dist, metrics: M.rifts,
});

// Duels (duel.js): two heroes fight each other, and only each other, until one yields.
const duels = createDuels({ sessions, safeSend, sendNear, sys, dist, now, log, metrics: M.duels });

// World bosses (worldboss.js): a giant rises at its lair now and then, for everyone to fight together.
const worldBosses = createWorldBosses({
  map: designToWorld, monsters, sessions, spawnMonster, nearestWalkable, walkable, aggro, monsterAttack, sendNear, broadcast, safeSend,
  log, now, rand, dist, r2,
});

function tick() {
  if (!world) return;
  const t = now();
  if (++tickCount % 10 === 0) for (const p of parties.values()) sendParty(p); // party frames: 1 Hz
  for (const m of monsters.values()) {
    // Monsters with no player anywhere nearby sleep (unless they need to walk home).
    if (m.state === "idle" && !m.invasion) { // invaders march on even with nobody watching
      let awake = false;
      for (const s of sessions.values()) if (s.inWorld && (s.inst || 0) === m.inst && dist(s.x, s.z, m.x, m.z) < 60) { awake = true; break; }
      if (!awake) continue;
    }
    if (!useGrid(m.inst)) { monsters.delete(m.id); continue; } // its dungeon is gone
    updateMonster(m, t);
  }
  useGrid(0);
  updateSpawners(t);
  invasions.tick(t);
  worldBosses.tick(t);
  if (tickCount % 5 === 0) { duels.tick(t); rifts.tick(t); }
  if (tickCount % 50 === 0) cleanupInstances(t);

  sendSnapshots(t);
}

// Snapshots, 10 a second, are most of the server's work and traffic. To keep them small:
//  - every entry is built once per tick as a JSON string and shared by everyone who can see it;
//  - what rarely changes (a monster's type, level and armour; a player's name, level and looks) is only sent when it
//    changed or when the viewer hasn't had that entry for FULL_EVERY ticks (it may have forgotten it), as a "full" entry;
//  - things further away (FAR_MONSTER, FAR_PLAYER) are updated every other tick.
// Clients keep the last full details and ignore partial entries for things they don't know yet.
const FULL_EVERY = 8, FAR_MONSTER = 25, FAR_PLAYER = 30;
const jstr = JSON.stringify;
const flags = (o) => (o.sl ? ',"sl":true' : "") + (o.st ? ',"st":true' : "") + (o.sh ? ',"sh":true' : "") + (o.mv ? ',"mv":true' : "") +
  (o.atk ? ',"atk":true' : "") + (o.dead ? ',"dead":true' : "");

function sendSnapshots(t) {
  const online = [];
  for (const s of sessions.values()) if (s.inWorld) online.push(s);
  const tk = tickCount;
  const mon = new Map(); // id -> { part, full } JSON fragments
  const monEntry = (m) => {
    let e = mon.get(m.id);
    if (!e) {
      const part = `{"id":${m.id},"x":${r2(m.x)},"z":${r2(m.z)},"ry":${Math.round(m.ry)},"hp":${Math.ceil(m.hp)}` +
        flags({ sl: t < m.slowUntil, st: t < m.stunUntil, sh: m.elite && t < m.shieldUntil });
      const stat = `"n":${jstr(m.type)},"l":${m.level},"mhp":${m.maxHp},"ar":${m.armor}` +
        (m.elite ? `,"el":${jstr(m.elite.name)},"af":${jstr(m.elite.affixes.join(","))}` : "");
      // Details that change (a world boss growing with every hero who joins) get a new version: viewers get them again.
      if (stat !== m.snapStat) { m.snapStat = stat; m.snapVer = (m.snapVer || 0) + 1; }
      e = { part: part + "}", full: part + "," + stat + "}" };
      mon.set(m.id, e);
    }
    return e;
  };
  // Players: the rarely-changing part gets a version; viewers get it again when it changes.
  for (const o of online) {
    const look = o.look || {};
    const stat = `"name":${jstr(o.name)},"lvl":${o.lvl},"pl":${o.pl || 0},"gt":${jstr(guilds.tagOf(o))},"mhp":${Math.ceil(o.mhp || 1)},"body":${jstr(look.body || "")},"legs":${jstr(look.legs || "")},` +
      `"weapon":${jstr(look.weapon || "")},"helm":${jstr(look.helm || "")},"mdl":${jstr(look.mdl || "Knight")},"wk":${jstr(look.wk || "")},` +
      `"cp":${jstr(look.cp || "")},"mt":${jstr(look.mt || "")},"ti":${jstr(look.ti || "")}`;
    if (stat !== o.snapStat) { o.snapStat = stat; o.snapVer = (o.snapVer || 0) + 1; }
    const part = `{"id":${o.id},"x":${r2(o.x)},"z":${r2(o.z)},"ry":${Math.round(o.ry || 0)},"hp":${Math.ceil(o.hp || 0)}` + flags(o);
    o.snapPart = part + "}";
    o.snapFull = part + "," + stat + "}";
  }

  for (const s of online) {
    const inst = s.inst || 0;
    if (s.snapInst !== inst || !s.sentMon) { s.snapInst = inst; s.sentMon = new Map(); s.sentPly = new Map(); } // a new space: everything anew
    const sentMon = s.sentMon, sentPly = s.sentPly; // id -> tick last sent (players: and the details version)
    const ms = [];
    for (const m of monsters.values()) {
      if (m.inst !== inst) continue;
      const d = dist(m.x, m.z, s.x, s.z);
      if (d > MONSTER_VIEW) continue;
      const last = sentMon.get(m.id);
      const e = monEntry(m);
      const stale = !last || last.ver !== m.snapVer || tk - last.tick >= FULL_EVERY;
      if (!stale && d > FAR_MONSTER && (tk + m.id) % 2) continue;
      ms.push(stale ? e.full : e.part);
      if (stale) sentMon.set(m.id, { ver: m.snapVer, tick: tk });
      else last.tick = tk;
    }
    const ps = [];
    for (const o of online) {
      if (o === s || (o.inst || 0) !== inst) continue;
      const d = dist(o.x, o.z, s.x, s.z);
      if (d > PLAYER_VIEW) continue;
      const last = sentPly.get(o.id);
      const stale = !last || last.ver !== o.snapVer || tk - last.tick >= FULL_EVERY;
      if (!stale && d > FAR_PLAYER && (tk + o.id) % 2) continue;
      ps.push(stale ? o.snapFull : o.snapPart);
      if (stale) sentPly.set(o.id, { ver: o.snapVer, tick: tk });
      else last.tick = tk;
    }
    safeSend(s, `{"t":"snap","l":${online.length},"m":[${ms.join(",")}],"p":[${ps.join(",")}]}`);
    // Forget what's long gone (monsters that died, players who left).
    if (tk % 100 === 0) {
      for (const [id, last] of sentMon) if (tk - last.tick > 200) sentMon.delete(id);
      for (const [id, last] of sentPly) if (tk - last.tick > 600 || !sessions.has(id)) sentPly.delete(id);
    }
  }
}

// =====================================================================================
// Startup
// =====================================================================================

const wss = new WebSocketServer({ server, path: "/ws", maxPayload: 512 * 1024 });

wss.on("connection", (ws, req) => {
  const s = { id: nextSessionId++, ws, inWorld: false, x: SPAWN.x, z: SPAWN.z, ry: 0, hp: 1, mhp: 1, lvl: 1, alive: true,
    ip: String((TRUST_PROXY && req.headers["x-forwarded-for"]) || req.socket.remoteAddress || "").split(",")[0].trim() };
  // Connections that never log in are closed after 15 minutes.
  setTimeout(() => { if (!s.acc && sessions.has(s.id)) s.ws.close(); }, 15 * 60000);
  sessions.set(s.id, s);
  ws.on("pong", () => { s.alive = true; });
  ws.on("message", (data, isBinary) => {
    if (isBinary) return;
    let m;
    try { m = JSON.parse(data.toString()); } catch { return; }
    const handler = m && handlers[m.t];
    if (!handler) return;
    M.msgIn.inc({ type: m.t });
    M.bytesIn.inc(undefined, data.length);
    const onError = (e) => {
      M.handlerErrors.inc({ type: m.t });
      log("handler error", m.t, e.stack);
      if (authHandlers[m.t]) authErr(s, "Something went wrong on the server. Try again.");
    };
    try { Promise.resolve(handler(s, m)).catch(onError); } catch (e) { onError(e); }
  });
  ws.on("close", () => onDisconnect(s));
  ws.on("error", () => {});
});

// Drop connections that stop answering pings.
setInterval(() => {
  for (const s of sessions.values()) {
    if (!s.alive) { s.ws.terminate(); continue; }
    s.alive = false;
    try { s.ws.ping(); } catch { /* ignore */ }
  }
}, 20000);

// Periodically flush characters to disk (in addition to client save messages).
setInterval(() => {
  for (const s of sessions.values()) if (s.inWorld) saveCharacter(s);
}, 60000);

async function start() {
  try {
    await store.init();
  } catch (e) {
    log(`Could not open the ${store.kind === "postgres" ? "database" : "data folder"}: ${e.message}`);
    process.exit(1);
  }
  log(`Accounts are stored in ${store.kind === "postgres" ? "PostgreSQL" : DATA_DIR}${mailer ? ", password reset emails are on" : ""}`);
  refreshCounts();
  setInterval(refreshCounts, 60000);

  await guilds.load();
  await rifts.load();
  loadWorld();
  if (world) initSpawners();
  else log("No world yet - it will be uploaded by the first client that connects.");

  setInterval(() => {
    const t0 = process.hrtime.bigint();
    tick();
    M.tick.observe(Number(process.hrtime.bigint() - t0) / 1e9);
  }, TICK * 1000);
  metrics.serve(parseInt(process.env.METRICS_PORT || "0", 10), log);
  server.listen(PORT, () => log(`Shadowfall server listening on :${PORT}  (public: ${PUBLIC_DIR}, data: ${DATA_DIR})`));
}
start();

let shuttingDown = false;
async function shutdown() {
  if (shuttingDown) return;
  shuttingDown = true;
  log("Shutting down, saving characters...");
  const saves = [...sessions.values()].filter((s) => s.inWorld).map(saveCharacter);
  const timeout = new Promise((r) => setTimeout(r, 8000));
  await Promise.race([Promise.all(saves).then(() => store.close()), timeout]);
  process.exit(0);
}
process.on("SIGTERM", shutdown);
process.on("SIGINT", shutdown);
