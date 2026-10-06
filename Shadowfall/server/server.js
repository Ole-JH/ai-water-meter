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
const { MONSTERS, SPAWNERS, TOWN } = require("./content");

const PORT = parseInt(process.env.PORT || "7341", 10);
const DATA_DIR = path.resolve(process.env.DATA_DIR || path.join(__dirname, "data"));
const PUBLIC_DIR = path.resolve(process.env.PUBLIC_DIR || path.join(__dirname, "public"));
const CHAR_DIR = path.join(DATA_DIR, "characters");
const WORLD_FILE = path.join(DATA_DIR, "world.json");
const PROTOCOL_VERSION = 2;
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
    if (world) log(`Loaded world ${world.hash}`);
  } catch { /* no world yet */ }
}

const blockedAt = (x, y) => x < 0 || y < 0 || x >= world.w || y >= world.h || world.blocked[y * world.w + x] === 1;
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
  const W = world.w;
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
    return spawnMonster(type, randInt(sp.minL, sp.maxL), x, z, sp);
  }
  return null;
}

function spawnMonster(type, level, x, z, spawner) {
  const def = MONSTERS[type];
  const hp = Math.round(def.hp * (1 + 0.28 * (level - 1)));
  const m = {
    id: nextMonsterId++, type, def, level, x, z, ry: rand(0, 360), homeX: x, homeZ: z,
    hp, maxHp: hp, armor: def.armor + level * 2, dmg: def.dmg * (1 + 0.16 * (level - 1)),
    state: "idle", target: 0, path: [], repathAt: 0, nextAttack: 0, slowUntil: 0,
    wanderAt: now() + rand(1, 5), threat: new Map(), spawner, summoned: false, novaAt: 0,
    leash: def.boss ? 40 : 28,
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
    if (o !== m && o.state === "idle" && dist(o.x, o.z, m.x, m.z) < 6) aggro(o, sessionId);
}

function validTarget(m, s) {
  return s && s.inWorld && !s.dead && !inTown(s.x, s.z) && dist(m.homeX, m.homeZ, s.x, s.z) < m.leash + 6;
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

function sendNear(x, z, range, msg) {
  const data = JSON.stringify(msg);
  for (const s of sessions.values())
    if (s.inWorld && dist(s.x, s.z, x, z) <= range) safeSend(s, data);
}

function monsterAttack(m, s, kind, dmg) {
  sendNear(m.x, m.z, PLAYER_VIEW, { t: "matk", mid: m.id, tid: s ? s.id : -1, dmg: r2(dmg), k: kind, x: r2(s ? s.x : m.x), z: r2(s ? s.z : m.z) });
}

function updateMonster(m, t) {
  const slowed = t < m.slowUntil;
  switch (m.state) {
    case "idle": {
      let best = null, bestD = m.def.aggro;
      for (const s of sessions.values()) {
        if (!s.inWorld || s.dead || inTown(s.x, s.z)) continue;
        const d = dist(m.x, m.z, s.x, s.z);
        if (d < bestD) { bestD = d; best = s; }
      }
      if (best) { aggro(m, best.id); alertNearby(m, best.id); break; }
      if (t >= m.wanderAt) {
        m.wanderAt = t + rand(3, 7);
        const wx = m.homeX + rand(-4, 4), wz = m.homeZ + rand(-4, 4);
        if (walkable(wx, wz) && lineOfSight(m.x, m.z, wx, wz)) m.path = [[wx, wz]];
      }
      moveAlongPath(m, m.def.speed * 0.35);
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

      const d = dist(m.x, m.z, s.x, s.z);
      if (m.type === "Lich King") lichAbilities(m, s, d, t);

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
      moveAlongPath(m, m.def.speed * (slowed ? 0.5 : 1));
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
      moveAlongPath(m, m.def.speed * 1.3);
      break;
    }
  }
}

function separate(m) {
  let px = 0, pz = 0;
  for (const o of monsters.values()) {
    if (o === m || o.state !== "chase") continue;
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
      const add = spawnMonster(i % 2 ? "Skeleton Archer" : "Skeleton", Math.max(1, m.level - 4), x, z, null);
      add.leash = 40;
      aggro(add, s.id);
    }
  }
}

function damageMonster(m, s, dmg) {
  if (m.hp <= 0) return;
  m.hp -= dmg;
  m.threat.set(s.id, (m.threat.get(s.id) || 0) + dmg);
  if (m.state !== "chase") { aggro(m, s.id); alertNearby(m, s.id); }
  if (m.hp <= 0) killMonster(m);
}

function killMonster(m) {
  monsters.delete(m.id);
  sendNear(m.x, m.z, PLAYER_VIEW + 10, { t: "mdie", mid: m.id });
  // Everyone who fought it gets credit, plus their party members who are nearby (WoW-style shared kills).
  const credited = new Set();
  for (const [sid] of m.threat) {
    const s = sessions.get(sid);
    if (!s || !s.inWorld) continue;
    credited.add(s);
    const p = partyOf(s);
    if (p) for (const o of partyMembers(p)) if (!o.dead && dist(o.x, o.z, m.x, m.z) <= PARTY_RANGE) credited.add(o);
  }
  for (const s of credited) {
    const diff = m.level - s.lvl;
    const mul = diff < -6 ? 0.1 : diff < -3 ? 0.5 : diff > 3 ? 1.3 : 1;
    const xp = Math.max(1, Math.round(m.def.xp * (1 + 0.12 * (m.level - 1)) * mul));
    safeSend(s, JSON.stringify({ t: "kill", mid: m.id, name: m.type, l: m.level, xp, x: r2(m.x), z: r2(m.z) }));
  }
  if (m.def.boss) broadcast({ t: "sys", msg: `${m.type} has been slain!` });
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
    if (err) return log("save failed", acc.name, err.message);
    fs.rename(tmp, file, (e) => e && log("save rename failed", acc.name, e.message));
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
  if (s.ws.readyState === 1) s.ws.send(data);
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
    if (!checkPassword(acc, pass)) return fail(s, "Wrong password for that character.");
  } else {
    const salt = crypto.randomBytes(16).toString("hex");
    acc = { name, salt, hash: hashPassword(pass, salt), created: new Date().toISOString(), save: null };
    saveAccount(acc);
    log(`New character: ${name}`);
  }

  for (const other of sessions.values())
    if (other !== s && other.account && other.account.name.toLowerCase() === name.toLowerCase()) {
      fail(other, "You logged in from another location.");
      other.inWorld = false;
    }

  s.account = acc;
  s.name = acc.name;
  s.inWorld = true;
  s.lvl = acc.save && acc.save.level ? acc.save.level : 1;
  s.x = acc.save && acc.save.x ? acc.save.x : 80.5;
  s.z = acc.save && acc.save.z ? acc.save.z : 77.5;
  acc.lastLogin = new Date().toISOString();
  safeSend(s, JSON.stringify({ t: "welcome", id: s.id, hasSave: !!acc.save, save: acc.save || undefined, now: Date.now() }));
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
    mdl: (o.look && o.look.mdl) || "Knight", x: r2(o.x), z: r2(o.z), dead: !!o.dead,
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

const handlers = {
  ...partyHandlers,

  hello(s, m) {
    if (s.account || s.pendingLogin) return;
    if (m.ver !== PROTOCOL_VERSION) return fail(s, "Your game client is out of date. Refresh the page.");
    const name = String(m.name || "").trim(), pass = String(m.pass || "");
    if (!/^[A-Za-z][A-Za-z0-9_]{2,15}$/.test(name)) return fail(s, "Names must be 3-16 letters, digits or _ and start with a letter.");
    if (pass.length < 4 || pass.length > 64) return fail(s, "Password must be 4-64 characters.");
    s.pendingLogin = { name, pass };
    if (!world) { s.clientHash = String(m.hash || ""); return safeSend(s, JSON.stringify({ t: "needworld" })); }
    if (m.hash !== world.hash) return fail(s, "Your game client doesn't match this server's world. Refresh the page (or delete data/world.json after updating the game).");
    completeLogin(s);
  },

  world(s, m) {
    if (!s.pendingLogin) return;
    if (!world) {
      const w = parseInt(m.w, 10), h = parseInt(m.h, 10);
      const decoded = w > 0 && h > 0 && w * h <= 1 << 20 ? decodeWorld(w, h, String(m.cells || "")) : null;
      if (!decoded || decoded.hash !== m.hash || decoded.hash !== s.clientHash) return fail(s, "World upload was invalid.");
      world = decoded;
      fs.writeFileSync(WORLD_FILE, JSON.stringify({ w, h, cells: m.cells, hash: world.hash }));
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
    if (Number.isFinite(x) && Number.isFinite(z) && world) {
      s.x = Math.min(Math.max(x, 0), world.w);
      s.z = Math.min(Math.max(z, 0), world.h);
    }
    s.ry = Number(m.ry) || 0;
    s.hp = Number(m.hp) || 0;
    s.mhp = Number(m.mhp) || 1;
    s.lvl = Math.max(1, Math.min(100, parseInt(m.lvl, 10) || 1));
    s.mv = !!m.mv; s.atk = !!m.atk; s.dead = !!m.dead;
    s.look = { body: String(m.body || "").slice(0, 6), legs: String(m.legs || "").slice(0, 6), weapon: String(m.weapon || "").slice(0, 6), helm: String(m.helm || "").slice(0, 6),
      mdl: HERO_MODELS.includes(m.mdl) ? m.mdl : "Knight" };
  },

  hit(s, m) {
    if (!s.inWorld || s.dead) return;
    const mon = monsters.get(m.mid);
    if (!mon || dist(mon.x, mon.z, s.x, s.z) > 30) return;
    const cap = 100 + s.lvl * 60; // sanity cap on reported damage
    const dmg = Math.min(Math.max(parseInt(m.dmg, 10) || 0, 0), cap);
    if (dmg > 0) damageMonster(mon, s, dmg);
  },

  slow(s, m) {
    if (!s.inWorld) return;
    const mon = monsters.get(m.mid);
    if (!mon || dist(mon.x, mon.z, s.x, s.z) > 12) return;
    mon.slowUntil = now() + Math.min(Number(m.dur) || 0, 5);
    if (mon.state !== "chase") aggro(mon, s.id);
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

  fx(s, m) {
    if (!s.inWorld) return;
    const t = now();
    if (t - (s.lastFx || 0) < 0.1) return;
    s.lastFx = t;
    const k = String(m.k || "");
    if (!["fireball", "nova", "heal", "meteor", "cleave", "levelup"].includes(k)) return;
    const msg = JSON.stringify({ t: "fx", id: s.id, k, x: r2(+m.x || 0), z: r2(+m.z || 0), tx: r2(+m.tx || 0), tz: r2(+m.tz || 0) });
    for (const o of sessions.values())
      if (o !== s && o.inWorld && dist(o.x, o.z, s.x, s.z) < PLAYER_VIEW) safeSend(o, msg);
  },

  save(s, m) {
    if (!s.inWorld || !s.account || !m.save || typeof m.save !== "object") return;
    const size = JSON.stringify(m.save).length;
    if (size > 256 * 1024) return;
    const lvl = parseInt(m.save.level, 10);
    if (!(lvl >= 1 && lvl <= 100)) return;
    s.account.save = m.save;
    saveAccount(s.account);
  },
};

function onDisconnect(s) {
  if (partyOf(s)) leaveParty(s, "has gone offline.");
  sessions.delete(s.id);
  if (s.account && s.inWorld) {
    if (s.account.save) {
      // remember where they logged out
      s.account.save.x = s.x;
      s.account.save.z = s.z;
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
      for (const s of sessions.values()) if (s.inWorld && dist(s.x, s.z, m.x, m.z) < 60) { awake = true; break; }
      if (!awake) continue;
    }
    updateMonster(m, t);
  }
  updateSpawners(t);

  const online = [...sessions.values()].filter((s) => s.inWorld);
  for (const s of online) {
    const ms = [];
    for (const m of monsters.values()) {
      if (dist(m.x, m.z, s.x, s.z) > MONSTER_VIEW) continue;
      ms.push({ id: m.id, n: m.type, l: m.level, x: r2(m.x), z: r2(m.z), ry: Math.round(m.ry), hp: Math.ceil(m.hp), mhp: m.maxHp, ar: m.armor, sl: t < m.slowUntil });
    }
    const ps = [];
    for (const o of online) {
      if (o === s || dist(o.x, o.z, s.x, s.z) > PLAYER_VIEW) continue;
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
  const s = { id: nextSessionId++, ws, inWorld: false, x: 80.5, z: 77.5, ry: 0, hp: 1, mhp: 1, lvl: 1, alive: true };
  sessions.set(s.id, s);
  ws.on("pong", () => { s.alive = true; });
  ws.on("message", (data, isBinary) => {
    if (isBinary) return;
    let m;
    try { m = JSON.parse(data.toString()); } catch { return; }
    const handler = m && handlers[m.t];
    if (!handler) return;
    try { handler(s, m); } catch (e) { log("handler error", m.t, e.stack); }
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
    s.account.save.x = s.x; s.account.save.z = s.z;
    saveAccount(s.account);
  }
}, 60000);

loadWorld();
if (world) initSpawners();
else log("No world yet - it will be uploaded by the first client that connects.");

setInterval(tick, TICK * 1000);
server.listen(PORT, () => log(`Shadowfall server listening on :${PORT}  (public: ${PUBLIC_DIR}, data: ${DATA_DIR})`));

function shutdown() {
  log("Shutting down, saving characters...");
  for (const s of sessions.values()) if (s.account && s.inWorld) {
    if (s.account.save) { s.account.save.x = s.x; s.account.save.z = s.z; }
    try { fs.writeFileSync(charFile(s.account.name), JSON.stringify(s.account)); } catch { /* ignore */ }
  }
  process.exit(0);
}
process.on("SIGTERM", shutdown);
process.on("SIGINT", shutdown);
