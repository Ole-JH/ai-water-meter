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
const { MONSTERS, SPAWNERS, TOWN, SPAWN, DUNGEONS, BALANCE, DIFFICULTIES, EMOTES, map: designToWorld } = require("./content");
const dungeonGen = require("./dungeon");
const metrics = require("./metrics");

const PORT = parseInt(process.env.PORT || "7341", 10);
const DATA_DIR = path.resolve(process.env.DATA_DIR || path.join(__dirname, "data"));
const PUBLIC_DIR = path.resolve(process.env.PUBLIC_DIR || path.join(__dirname, "public"));
const CHAR_DIR = path.join(DATA_DIR, "characters");
const WORLD_FILE = path.join(DATA_DIR, "world.json");
const PROTOCOL_VERSION = 4;
const TICK = 0.1; // seconds
const HERO_MODELS = ["Knight", "Barbarian", "Mage", "Rogue"]; // must match CharacterLook.HeroModels
const MONSTER_VIEW = 45;
const PLAYER_VIEW = 60;

// In Docker the container starts as root so it can take ownership of the bind-mounted data
// folder (which Docker creates as root), then drops to an unprivileged user before serving.
if (process.env.DROP_PRIVILEGES === "1" && process.getuid && process.getuid() === 0) {
  const uid = parseInt(process.env.APP_UID || "1000", 10), gid = parseInt(process.env.APP_GID || "1000", 10);
  fs.mkdirSync(CHAR_DIR, { recursive: true });
  const chownTree = (p) => {
    fs.chownSync(p, uid, gid);
    if (fs.statSync(p).isDirectory()) for (const f of fs.readdirSync(p)) chownTree(path.join(p, f));
  };
  chownTree(DATA_DIR);
  process.setgid(gid);
  process.setuid(uid);
}

fs.mkdirSync(CHAR_DIR, { recursive: true });

const log = (...a) => console.log(new Date().toISOString(), ...a);
const rand = (a, b) => a + Math.random() * (b - a);
const randInt = (a, b) => Math.floor(rand(a, b + 1));
const dist = (ax, az, bx, bz) => Math.hypot(ax - bx, az - bz);
const r2 = (v) => Math.round(v * 100) / 100;
const inTown = (x, z) => x >= TOWN.x0 && x < TOWN.x1 && z >= TOWN.z0 && z < TOWN.z1;
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

const server = http.createServer((req, res) => {
  const url = new URL(req.url, "http://localhost");
  if (url.pathname === "/healthz") {
    res.writeHead(200, { "Content-Type": "application/json" });
    return res.end(JSON.stringify({ ok: true, players: sessions.size, monsters: monsters.size, world: !!world }));
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
    if (world) world.version = j.version | 0;
    if (world) log(`Loaded world ${world.hash} (layout version ${world.version})`);
  } catch { /* no world yet */ }
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

let ELITE_CHANCE = Number(process.env.ELITE_CHANCE ?? 0.12); // admins can change it at runtime
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
    if (o !== m && o.inst === m.inst && o.state === "idle" && dist(o.x, o.z, m.x, m.z) < 6) aggro(o, sessionId);
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

function monsterAttack(m, s, kind, dmg) {
  if (s && hasAffix(m, "Vampiric") && dmg > 0) m.hp = Math.min(m.maxHp, m.hp + dmg * 0.6);
  sendNear(m.x, m.z, PLAYER_VIEW, { t: "matk", mid: m.id, tid: s ? s.id : -1, dmg: r2(dmg), k: kind, x: r2(s ? s.x : m.x), z: r2(s ? s.z : m.z) }, m.inst);
}

function updateMonster(m, t) {
  const slowed = t < m.slowUntil;
  switch (m.state) {
    case "idle": {
      let best = null, bestD = m.def.aggro;
      for (const s of sessions.values()) {
        if (!s.inWorld || s.dead || hidden(s) || (s.inst || 0) !== m.inst || (m.inst === 0 && inTown(s.x, s.z))) continue;
        const d = dist(m.x, m.z, s.x, s.z);
        if (d < bestD) { bestD = d; best = s; }
      }
      if (best) { aggro(m, best.id); alertNearby(m, best.id); break; }
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
      m.hp = Math.min(m.maxHp, m.hp + m.maxHp * 0.25 * TICK);
      if (!m.path.length) {
        if (dist(m.x, m.z, m.homeX, m.homeZ) < 1.5) { m.state = "idle"; m.hp = m.maxHp; m.threat.clear(); break; }
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
    safeSend(s, JSON.stringify({ t: "kill", mid: m.id, name: m.type, l: m.level, xp, x: r2(m.x), z: r2(m.z), ...(m.elite ? { el: m.elite.name } : {}), ...(m.lootBonus ? { lb: m.lootBonus } : {}) }));
  }
  if (m.def.boss && m.inst) {
    const inst = instances.get(m.inst);
    sendNear(m.x, m.z, 999, { t: "sys", msg: `${m.type} has been slain! ${inst ? DUNGEONS[inst.dIdx].name : "The dungeon"} falls silent.` }, m.inst);
  }
  else if (m.def.boss) broadcast({ t: "sys", msg: `${m.type} has been slain!` });
  else if (m.elite) sendNear(m.x, m.z, PLAYER_VIEW, { t: "sys", msg: `${m.elite.name} (${m.type}) has been slain!` }, m.inst);
  if (m.spawner) m.spawner.pending.push(now() + m.spawner.respawn);
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

const charFile = (name) => path.join(CHAR_DIR, name.toLowerCase() + ".json");

function loadAccount(name) {
  try { return JSON.parse(fs.readFileSync(charFile(name), "utf8")); } catch { return null; }
}

function saveAccount(acc) {
  const file = charFile(acc.name), tmp = file + ".tmp";
  fs.writeFile(tmp, JSON.stringify(acc), (err) => {
    if (err) { M.saves.inc({ result: "error" }); return log("save failed", acc.name, err.message); }
    fs.rename(tmp, file, (e) => {
      M.saves.inc({ result: e ? "error" : "ok" });
      if (e) log("save rename failed", acc.name, e.message);
    });
  });
}

const hashPassword = (pass, salt) => crypto.scryptSync(pass, salt, 32).toString("hex");

function checkPassword(acc, pass) {
  const a = Buffer.from(hashPassword(pass, acc.salt), "hex"), b = Buffer.from(acc.hash, "hex");
  return a.length === b.length && crypto.timingSafeEqual(a, b);
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

function completeLogin(s) {
  const { name, pass } = s.pendingLogin;
  s.pendingLogin = null;

  let acc = loadAccount(name);
  if (acc) {
    if (!checkPassword(acc, pass)) { M.logins.inc({ result: "bad_password" }); return fail(s, "Wrong password for that character."); }
    M.logins.inc({ result: "ok" });
  } else {
    const salt = crypto.randomBytes(16).toString("hex");
    acc = { name, salt, hash: hashPassword(pass, salt), created: new Date().toISOString(), save: null };
    saveAccount(acc);
    M.logins.inc({ result: "new" });
    log(`New character: ${name}`);
  }

  for (const other of sessions.values())
    if (other !== s && other.account && other.account.name.toLowerCase() === name.toLowerCase()) {
      fail(other, "You logged in from another location.");
      other.inWorld = false;
    }

  s.account = acc;
  s.admin = isAdmin(acc);
  if (s.admin) log(`${acc.name} is an admin`);
  s.name = acc.name;
  s.inWorld = true;
  s.lvl = acc.save && acc.save.level ? acc.save.level : 1;
  s.x = acc.save && acc.save.x ? acc.save.x : SPAWN.x;
  s.z = acc.save && acc.save.z ? acc.save.z : SPAWN.z;
  acc.lastLogin = new Date().toISOString();
  safeSend(s, JSON.stringify({ t: "welcome", id: s.id, hasSave: !!acc.save, save: acc.save || undefined, now: worldClock(), admin: !!s.admin }));
  broadcast({ t: "sys", msg: `${acc.name} has entered the world.` });
  log(`${acc.name} logged in (${sessions.size} connected)`);
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
  handlerErrors: metrics.counter("shadowfall_handler_errors_total", "Exceptions thrown while handling a client message, by type."),
  logins: metrics.counter("shadowfall_logins_total", "Login attempts, by result (ok, new, bad_password, rejected)."),
  kills: metrics.counter("shadowfall_monsters_killed_total", "Monsters killed, by monster type and whether it was an elite."),
  bossKills: metrics.counter("shadowfall_bosses_killed_total", "Bosses killed, by boss."),
  deaths: metrics.counter("shadowfall_player_deaths_total", "Player deaths."),
  trades: metrics.counter("shadowfall_trades_completed_total", "Completed player trades."),
  dungeonEntries: metrics.counter("shadowfall_dungeon_entries_total", "Players entering a dungeon level, by dungeon and difficulty."),
  admin: metrics.counter("shadowfall_admin_commands_total", "Admin commands run, by command."),
  saves: metrics.counter("shadowfall_character_saves_total", "Character files written, by result."),
  tick: metrics.histogram("shadowfall_tick_duration_seconds", "Time spent in one simulation tick.", [0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25]),
};
// Start at zero so dashboards show a flat line rather than "no data" until the first event.
M.deaths.inc(undefined, 0);
M.trades.inc(undefined, 0);
M.bytesIn.inc(undefined, 0);
M.bytesOut.inc(undefined, 0);
M.msgOut.inc(undefined, 0);
for (const result of ["ok", "error"]) M.saves.inc({ result }, 0);
for (const result of ["ok", "new", "bad_password"]) M.logins.inc({ result }, 0);
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
let accountCount = 0, accountCountAt = 0;
metrics.gauge("shadowfall_accounts", "Characters on disk.", () => {
  if (Date.now() - accountCountAt > 60000) {
    accountCountAt = Date.now();
    try { accountCount = fs.readdirSync(CHAR_DIR).filter((f) => f.endsWith(".json")).length; } catch { accountCount = 0; }
  }
  return accountCount;
});
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
  const pm = partyMembers(p).map((o) => ({
    id: o.id, name: o.name, lvl: o.lvl, hp: Math.ceil(o.hp || 0), mhp: Math.ceil(o.mhp || 1),
    mdl: (o.look && o.look.mdl) || "Knight", x: r2(o.x), z: r2(o.z), dead: !!o.dead, di: o.inst || 0,
  }));
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
    const tr = { a: o.id, b: s.id, offers: new Map([[o.id, { items: [], gold: 0 }], [s.id, { items: [], gold: 0 }]]), ok: new Set() };
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

  toffer(s, m) {
    const tr = trades.get(s.id);
    if (!tr) return;
    const items = Array.isArray(m.items) ? m.items.filter((x) => typeof x === "string" && x.length <= 6000).slice(0, TRADE_SLOTS) : [];
    const gold = Math.max(0, Math.min(1e9, parseInt(m.gold, 10) || 0));
    tr.offers.set(s.id, { items, gold });
    tr.ok.clear(); // any change resets both acceptances
    const other = sessions.get(tr.a === s.id ? tr.b : tr.a);
    if (other) safeSend(other, JSON.stringify({ t: "tupd", items, gold }));
  },

  tok(s) {
    const tr = trades.get(s.id);
    if (!tr) return;
    const other = sessions.get(tr.a === s.id ? tr.b : tr.a);
    if (!other || !canTrade(s, other)) return closeTrade(tr, "You moved too far apart to trade.");
    tr.ok.add(s.id);
    safeSend(other, JSON.stringify({ t: "tok", id: s.id }));
    if (tr.ok.size < 2) return;
    trades.delete(tr.a);
    trades.delete(tr.b);
    for (const [me, them] of [[s, other], [other, s]]) {
      const got = tr.offers.get(them.id);
      safeSend(me, JSON.stringify({ t: "tdone", items: got.items, gold: got.gold, name: them.name }));
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
  if (!world) return DUNGEONS.map((d) => [designToWorld(d.at[0]), designToWorld(d.at[1])]);
  if (entrancesFor !== world) {
    useGrid(0);
    for (const d of DUNGEONS) d.entrance = resolveEntrance(designToWorld(d.at[0]), designToWorld(d.at[1]));
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
    t: "dungeon", id: inst.id, l: inst.depth, k: DUNGEONS[inst.dIdx].name, d: inst.dIdx, n: DUNGEONS[inst.dIdx].depths, df: inst.df, seed: inst.seed, w: L.w, h: L.h, cells: inst.cells,
    rooms: L.rooms.flatMap((r) => [r.x, r.y, r.w, r.h]), start: L.start, exit: L.exit,
    stairs: L.stairs || [], boss: L.boss || [], chests: L.chests.flat(),
  }));
}

function leaveInstance(s, toTown) {
  if (trades.has(s.id)) closeTrade(trades.get(s.id), "The trade was cancelled.");
  const inst = instances.get(s.inst);
  s.inst = 0;
  [s.x, s.z] = toTown ? [SPAWN.x, SPAWN.z] : exitOf(inst ? inst.dIdx : 0);
  safeSend(s, JSON.stringify({ t: "dungeon", id: 0, x: s.x, z: s.z }));
}

/** Where a player is in the overworld (for saves): dungeon players are saved at the entrance. */
const overworldPos = (s) => (s.inst ? exitOf((instances.get(s.inst) || { dIdx: 0 }).dIdx) : [s.x, s.z]);

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
const isAdmin = (acc) => !!acc && (acc.admin === true || ADMINS.has(String(acc.name).toLowerCase()));

const CYCLE_MS = 48 * 60000; // must match DayNight.CycleMinutes on the client
let clockOffset = 0;
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
const ADMIN_COMMANDS = new Set(["tp", "tpto", "summon", "dungeon", "regen", "spawn", "killall", "time", "elites", "announce", "kick", "who"]);

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
    case "elites": return runAdmin(s, "elites", { chance: w[0] });
    case "announce": return runAdmin(s, "announce", { text: rest });
    case "kick": return runAdmin(s, "kick", { name: w[0] });
    case "who": return runAdmin(s, "who");
    default: return "Admin commands: tp x z, tpto name, summon name, dungeon <id|0-3> [depth], regen, spawn <type> [level] [count] [elite], killall [radius], time dawn|day|dusk|night, elites <0-1>, announce text, kick name, who";
  }
}

const handlers = {
  ...partyHandlers,
  ...dungeonHandlers,

  adm(s, m) {
    if (!s.inWorld) return;
    if (!s.admin) return sys(s, "You are not an admin.");
    const res = runAdmin(s, String(m.c || ""), m);
    if (res) sys(s, `[admin] ${res}`);
    log(`admin ${s.name}: ${m.c}`);
  },

  hello(s, m) {
    if (s.account || s.pendingLogin) return;
    if (m.ver !== PROTOCOL_VERSION) return fail(s, "Your game client is out of date. Refresh the page.");
    const name = String(m.name || "").trim(), pass = String(m.pass || "");
    if (!/^[A-Za-z][A-Za-z0-9_]{2,15}$/.test(name)) return fail(s, "Names must be 3-16 letters, digits or _ and start with a letter.");
    if (pass.length < 4 || pass.length > 64) return fail(s, "Password must be 4-64 characters.");
    s.pendingLogin = { name, pass };
    s.worldVersion = m.wv | 0;
    if (!world) { s.clientHash = String(m.hash || ""); return safeSend(s, JSON.stringify({ t: "needworld" })); }
    if (m.hash !== world.hash) {
      // The game was updated and its world changed (a higher layout version). Nobody is playing on the old
      // one, so take the new world from this client instead of locking everyone out. An older client
      // (a stale browser cache) must never swap the world back.
      if ((m.wv | 0) <= world.version) return fail(s, "Your game client is older than this server's world. Refresh the page (Ctrl+F5).");
      const others = [...sessions.values()].some((o) => o !== s && o.inWorld);
      if (others) return fail(s, "This server is still running the previous version of the world. Try again once everyone has logged out, or refresh the page.");
      s.clientHash = String(m.hash || "");
      s.replacesWorld = true;
      return safeSend(s, JSON.stringify({ t: "needworld" }));
    }
    completeLogin(s);
  },

  world(s, m) {
    if (!s.pendingLogin) return;
    if (!world || s.replacesWorld) {
      const w = parseInt(m.w, 10), h = parseInt(m.h, 10);
      const decoded = w > 0 && h > 0 && w * h <= 1 << 20 ? decodeWorld(w, h, String(m.cells || "")) : null;
      if (!decoded || decoded.hash !== m.hash || decoded.hash !== s.clientHash) return fail(s, "World upload was invalid.");
      if (world && [...sessions.values()].some((o) => o !== s && o.inWorld)) return fail(s, "Someone joined the old world meanwhile. Try again.");
      if (world) {
        log(`World changed (${world.hash} -> ${decoded.hash}): respawning the overworld's monsters`);
        for (const [id, mon] of monsters) if (mon.inst === 0) monsters.delete(id);
        spawners.length = 0;
      }
      s.replacesWorld = false;
      world = decoded;
      world.version = s.worldVersion | 0;
      fs.writeFileSync(WORLD_FILE, JSON.stringify({ w, h, cells: m.cells, hash: world.hash, version: world.version }));
      log(`World received from ${s.pendingLogin.name}: ${world.hash}`);
      initSpawners();
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
    s.lvl = Math.max(1, Math.min(100, parseInt(m.lvl, 10) || 1));
    if (m.dead && !s.dead) M.deaths.inc();
    s.mv = !!m.mv; s.atk = !!m.atk; s.dead = !!m.dead;
    s.look = { body: String(m.body || "").slice(0, 6), legs: String(m.legs || "").slice(0, 6), weapon: String(m.weapon || "").slice(0, 6), helm: String(m.helm || "").slice(0, 6),
      mdl: HERO_MODELS.includes(m.mdl) ? m.mdl : "Knight",
      wk: ["sword", "axe", "mace", "dagger", "staff"].includes(m.wk) ? m.wk : "",
      cp: COMPANIONS.includes(m.cp) ? m.cp : "" };
  },

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
      const res = adminFromChat(s, arg);
      if (res) sys(s, `[admin] ${res}`);
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
      case "/leave": return partyHandlers.pleave(s);
      default:
        if (cmd.startsWith("/")) return sys(s, "Commands: /p party chat, /w name whisper, /invite name, /leave, /who");
    }
    broadcast({ t: "chat", id: s.id, name: s.name, msg });
  },

  // Emotes (/wave, /dance...): shown to players nearby, who also get the "Alice waves." line.
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
    if (!s.inWorld || !s.account || !m.save || typeof m.save !== "object") return;
    const size = JSON.stringify(m.save).length;
    if (size > 256 * 1024) return;
    const lvl = parseInt(m.save.level, 10);
    if (!(lvl >= 1 && lvl <= 100)) return;
    s.account.save = m.save;
    [s.account.save.x, s.account.save.z] = overworldPos(s);
    saveAccount(s.account);
  },
};

function onDisconnect(s) {
  if (partyOf(s)) leaveParty(s, "has gone offline.");
  if (trades.has(s.id)) closeTrade(trades.get(s.id), `${s.name} has gone offline.`);
  sessions.delete(s.id);
  if (s.account && s.inWorld) {
    if (s.account.save) {
      // remember where they logged out
      [s.account.save.x, s.account.save.z] = overworldPos(s);
    }
    saveAccount(s.account);
    broadcast({ t: "sys", msg: `${s.name} has left the world.` });
    broadcast({ t: "leave", id: s.id });
    log(`${s.name} logged out (${sessions.size} connected)`);
  }
}

// =====================================================================================
// Game loop: monster AI + snapshots
// =====================================================================================

let tickCount = 0;
function tick() {
  if (!world) return;
  const t = now();
  if (++tickCount % 10 === 0) for (const p of parties.values()) sendParty(p); // party frames: 1 Hz
  for (const m of monsters.values()) {
    // Monsters with no player anywhere nearby sleep (unless they need to walk home).
    if (m.state === "idle") {
      let awake = false;
      for (const s of sessions.values()) if (s.inWorld && (s.inst || 0) === m.inst && dist(s.x, s.z, m.x, m.z) < 60) { awake = true; break; }
      if (!awake) continue;
    }
    if (!useGrid(m.inst)) { monsters.delete(m.id); continue; } // its dungeon is gone
    updateMonster(m, t);
  }
  useGrid(0);
  updateSpawners(t);
  if (tickCount % 50 === 0) cleanupInstances(t);

  const online = [...sessions.values()].filter((s) => s.inWorld);
  // Each monster's snapshot entry is built once per tick and shared by every player who can see it.
  const entries = new Map();
  const entry = (m) => {
    let e = entries.get(m.id);
    if (!e) {
      e = { id: m.id, n: m.type, l: m.level, x: r2(m.x), z: r2(m.z), ry: Math.round(m.ry), hp: Math.ceil(m.hp), mhp: m.maxHp, ar: m.armor, sl: t < m.slowUntil, st: t < m.stunUntil,
        ...(m.elite ? { el: m.elite.name, af: m.elite.affixes.join(","), sh: t < m.shieldUntil } : {}) };
      entries.set(m.id, e);
    }
    return e;
  };
  for (const s of online) {
    const ms = [];
    const inst = s.inst || 0;
    for (const m of monsters.values()) {
      if (m.inst !== inst || dist(m.x, m.z, s.x, s.z) > MONSTER_VIEW) continue;
      ms.push(entry(m));
    }
    const ps = [];
    for (const o of online) {
      if (o === s || (o.inst || 0) !== inst || dist(o.x, o.z, s.x, s.z) > PLAYER_VIEW) continue;
      ps.push({ id: o.id, name: o.name, x: r2(o.x), z: r2(o.z), ry: Math.round(o.ry || 0), hp: Math.ceil(o.hp || 0), mhp: Math.ceil(o.mhp || 1),
        lvl: o.lvl, mv: o.mv, atk: o.atk, dead: o.dead, ...(o.look || {}) });
    }
    safeSend(s, JSON.stringify({ t: "snap", l: online.length, m: ms, p: ps }));
  }
}

// =====================================================================================
// Startup
// =====================================================================================

const wss = new WebSocketServer({ server, path: "/ws", maxPayload: 512 * 1024 });

wss.on("connection", (ws, req) => {
  const s = { id: nextSessionId++, ws, inWorld: false, x: SPAWN.x, z: SPAWN.z, ry: 0, hp: 1, mhp: 1, lvl: 1, alive: true };
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
    try { handler(s, m); } catch (e) { M.handlerErrors.inc({ type: m.t }); log("handler error", m.t, e.stack); }
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
  for (const s of sessions.values()) if (s.inWorld && s.account && s.account.save) {
    [s.account.save.x, s.account.save.z] = overworldPos(s);
    saveAccount(s.account);
  }
}, 60000);

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

function shutdown() {
  log("Shutting down, saving characters...");
  for (const s of sessions.values()) if (s.account && s.inWorld) {
    if (s.account.save) [s.account.save.x, s.account.save.z] = overworldPos(s);
    try { fs.writeFileSync(charFile(s.account.name), JSON.stringify(s.account)); } catch { /* ignore */ }
  }
  process.exit(0);
}
process.on("SIGTERM", shutdown);
process.on("SIGINT", shutdown);
