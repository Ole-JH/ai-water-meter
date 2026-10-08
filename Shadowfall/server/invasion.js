// Town invasions. Every INVASION_MINUTES or so (default 45, 0 = never), monsters from the surrounding lands gather
// outside a gate of a walled town where players are, and attack it in three waves. They march on the gate and batter
// it; heroes they meet on the way are fought as usual. Heroes inside the walls are out of their reach, but the town's
// militia put up ladders behind the wall during the gathering (the client's Rampart.cs): heroes on the wall can shoot
// down from there, and only the invaders' archers and casters can shoot back. The gate is shut while it lasts. Kill every wave, the last led by a warlord, and the town holds: everyone who hurt an invader gets
// experience and a boss's share of loot. If the gate falls (or the siege drags on for 12 minutes), the invaders
// plunder the town and withdraw, and nobody is rewarded.
//
// The server calls: tick(t) every simulation tick, idle(m, t) for an invader with nobody to fight, onDamage(m, s) when
// a hero hurts one, sendTo(s) when a hero enters the world, and start()/stop() for the admin command.

const WAVES = 3;
const GATHER_S = Number(process.env.INVASION_GATHER_S ?? 60); // between the warning and the first wave
const WAVE_GAP_S = 75;      // the next wave comes after this long, or sooner when the last one is nearly dead
const MAX_S = 12 * 60;      // a siege nobody answers ends in a sack
// Gate integrity (of 100) lost per second per invader at the gate (big ones count double)
const SIEGE_RATE = Number(process.env.INVASION_SIEGE_RATE ?? 0.35);
const RESULT_S = 15;        // how long the outcome stays on players' screens
const STAGING = 24;         // how far outside the gate the invaders gather
const PLAYERS_NEAR = 110;   // players this close to a town count towards it (choosing the town, scaling the waves)
// The town's guards: archers up on the wall walk beside the gate and soldiers holding a line outside it. They help a
// little (a few percent of an invader's life a blow) so the heroes still do the real work; the fallen are replaced at
// each new wave. Invaders still go for heroes first; with none around they fight the guards, then the gate.
const ARCHERS = 3, SOLDIERS = 4;
const GUARD_HIT = 0.025;    // share of an invader's max life per guard blow
const ARCHER_RANGE = 22, ARCHER_CD = 2.6, SOLDIER_CD = 1.7, SOLDIER_REACH = 7, GUARD_SPEED = 3.2;

module.exports = function createInvasions(ctx) {
  const { TOWNS, SPAWNERS, MONSTERS, monsters, sessions, spawnMonster, makeElite, findPath, nearestWalkable, moveAlongPath,
    speedOf, monsterAttack, broadcast, safeSend, rollLoot, dropFor, heroClass, deep, log, now, rand, dist, r2, metrics,
    walkable, guardHit } = ctx;

  const minutes = Number(process.env.INVASION_MINUTES ?? 45);
  let inv = null;
  let nextId = 1;
  let nextAt = minutes > 0 ? now() + rand(0.3, 0.6) * minutes * 60 : Infinity;

  const walled = TOWNS.filter((t) => t.walled);
  const centre = (t) => ({ x: (t.x0 + t.x1) / 2, z: (t.z0 + t.z1) / 2 });
  const overworld = () => [...sessions.values()].filter((s) => s.inWorld && !s.inst);
  const near = (t, list = overworld()) => { const c = centre(t); return list.filter((s) => dist(s.x, s.z, c.x, c.z) < PLAYERS_NEAR); };

  /** The four gates of a walled town: where the invaders batter (just outside the gap) and which way is out. */
  function gates(t) {
    const cx = t.x0 + Math.floor((t.x1 - t.x0) / 2) + 0.5, cz = t.z0 + Math.floor((t.z1 - t.z0) / 2) + 0.5;
    return [
      { name: "south", x: cx, z: t.z0 - 1.5, nx: 0, nz: -1 },
      { name: "north", x: cx, z: t.z1 + 1.5, nx: 0, nz: 1 },
      { name: "west", x: t.x0 - 1.5, z: cz, nx: -1, nz: 0 },
      { name: "east", x: t.x1 + 1.5, z: cz, nx: 1, nz: 0 },
    ];
  }

  /** The monsters that live out there (from the spawners around the staging point) and their usual level. */
  function roster(x, z) {
    let near = SPAWNERS.filter(([sx, sz]) => dist(sx, sz, x, z) < 80);
    if (!near.length) near = [...SPAWNERS].sort((a, b) => dist(a[0], a[1], x, z) - dist(b[0], b[1], x, z)).slice(0, 3);
    const types = new Set();
    let lv = 0;
    for (const [, , , minL, maxL, ts] of near) {
      for (const ty of ts) if (MONSTERS[ty] && !MONSTERS[ty].boss) types.add(ty);
      lv += (minL + maxL) / 2;
    }
    return { types: [...types], level: Math.round(lv / Math.max(1, near.length)) };
  }

  /** Sets up a siege of town t (or the busiest walled town). Returns why not, or null. */
  function begin(t, gateName) {
    const players = overworld();
    if (!t) {
      const weighted = walled.map((w) => ({ t: w, n: near(w, players).length })).filter((w) => w.n > 0);
      if (!weighted.length) return "nobody is near a walled town";
      let r = Math.random() * weighted.reduce((a, w) => a + w.n, 0);
      t = weighted.find((w) => (r -= w.n) < 0)?.t || weighted[0].t;
    }
    const options = gates(t).filter((g) => !gateName || g.name === gateName).sort(() => Math.random() - 0.5);
    for (const g of options) {
      const spot = nearestWalkable(Math.floor(g.x + g.nx * STAGING), Math.floor(g.z + g.nz * STAGING), 8);
      if (!spot) continue;
      const sx = spot[0] + 0.5, sz = spot[1] + 0.5;
      if (!findPath(sx, sz, g.x, g.z, 8000).length) continue;
      const ros = roster(sx, sz);
      if (!ros.types.length) continue;
      const defenders = near(t, players);
      const avg = defenders.length ? defenders.reduce((a, s) => a + (s.lvl || 1), 0) / defenders.length : ros.level;
      const level = Math.max(1, Math.min(40, Math.round(Math.max(ros.level, avg - 1))));
      inv = {
        id: nextId++, town: t, gate: g, sx, sz, types: ros.types, level, players: Math.max(1, defenders.length),
        phase: "gather", wave: 0, integrity: 100, began: now(), waveAt: now() + GATHER_S, ids: new Set(),
        defenders: new Map(), sentAt: 0, endedAt: 0, guards: [], nextGuard: 1,
      };
      broadcast({ t: "sys", msg: `${t.name} is under attack! Monsters are gathering outside its ${g.name} gate. Defend the town!` });
      log(`Invasion of ${t.name} (${g.name} gate, level ${level}, ${ros.types.join(", ")})`);
      send();
      return null;
    }
    return `no gate of ${t.name} can be reached from outside`;
  }

  // ------------------------------------------------------------------ the town's guards

  /** Tops the guards up to strength (each wave): archers onto the wall by the gate, soldiers out in front of it. */
  function postGuards() {
    const g = inv.gate, ax = Math.abs(g.nz), az = Math.abs(g.nx); // along the wall
    const have = (k) => inv.guards.filter((x) => x.k === k).length;
    const lvl = inv.level;
    const archerSpots = [-5, 5, -9, 9];
    for (let i = have("a"); i < ARCHERS; i++) {
      const off = archerSpots[i % archerSpots.length];
      const x = g.x - g.nx * 2.6 + ax * off, z = g.z - g.nz * 2.6 + az * off; // just inside the wall line: the walkway
      inv.guards.push({ id: inv.nextGuard++, k: "a", x, z, px: x, pz: z, hp: 40 + lvl * 14, mhp: 40 + lvl * 14, next: now() + rand(1, 3) });
    }
    const soldierSpots = [-3, 3, -1, 1, -5, 5];
    for (let i = have("s"); i < SOLDIERS; i++) {
      const off = soldierSpots[i % soldierSpots.length];
      const px = g.x + g.nx * 3.5 + ax * off, pz = g.z + g.nz * 3.5 + az * off;
      if (!walkable(px, pz)) continue;
      // they come out through the gate and take up their post
      inv.guards.push({ id: inv.nextGuard++, k: "s", x: g.x, z: g.z, px, pz, hp: 70 + lvl * 26, mhp: 70 + lvl * 26, next: now() + 1 });
    }
  }

  /** What the players near the town see of the guards, and their deeds. */
  function guardEvent(ev) {
    const c = centre(inv.town), data = JSON.stringify({ t: "gev", ...ev });
    for (const s of sessions.values()) if (s.inWorld && !s.inst && dist(s.x, s.z, c.x, c.z) < PLAYERS_NEAR) safeSend(s, data);
  }

  const invaders = () => { const out = []; for (const id of inv.ids) { const m = monsters.get(id); if (m && m.hp > 0) out.push(m); } return out; };

  function nearestInvader(x, z, range, list) {
    let best = null, bd = range;
    for (const m of list) { const d = dist(m.x, m.z, x, z); if (d < bd) { bd = d; best = m; } }
    return best;
  }

  function step(o, tx, tz, speed) {
    const dx = tx - o.x, dz = tz - o.z, d = Math.hypot(dx, dz), s = speed * 0.1;
    if (d < 0.05) return;
    const nx = d <= s ? tx : o.x + (dx / d) * s, nz = d <= s ? tz : o.z + (dz / d) * s;
    if (walkable(nx, nz)) { o.x = nx; o.z = nz; }
  }

  function updateGuards(t) {
    const foes = invaders();
    for (const g of inv.guards) {
      if (g.hp <= 0) continue;
      if (g.k === "a") {
        if (t < g.next) continue;
        const m = nearestInvader(g.x, g.z, ARCHER_RANGE, foes);
        if (!m) continue;
        g.next = t + ARCHER_CD * rand(0.85, 1.15);
        guardEvent({ k: "shot", id: g.id, mid: m.id, x: r2(m.x), z: r2(m.z) });
        guardHit(m, Math.max(1, Math.round(m.maxHp * GUARD_HIT)));
        continue;
      }
      // a soldier: steps up to an invader near his post, otherwise holds the line
      const m = nearestInvader(g.px, g.pz, SOLDIER_REACH, foes);
      if (!m) { step(g, g.px, g.pz, GUARD_SPEED); continue; }
      if (dist(g.x, g.z, m.x, m.z) > 1.7) { step(g, m.x, m.z, GUARD_SPEED); continue; }
      if (t < g.next) continue;
      g.next = t + SOLDIER_CD * rand(0.85, 1.15);
      guardEvent({ k: "swing", id: g.id, mid: m.id, x: r2(m.x), z: r2(m.z) });
      guardHit(m, Math.max(1, Math.round(m.maxHp * GUARD_HIT)));
    }
  }

  /** An invader with no hero to fight turns on a guard near by (soldiers; their archers and casters shoot up at the
   * wall's archers too). Returns true while it's fighting one. */
  function fightGuard(m, t) {
    const ranged = !!m.def.ranged, reach = ranged ? Math.max(8, m.def.range) : 9;
    let g = null, bd = reach + 4;
    for (const o of inv.guards) {
      if (o.hp <= 0 || (o.k === "a" && !ranged)) continue;
      const d = dist(o.x, o.z, m.x, m.z);
      if (d < bd) { bd = d; g = o; }
    }
    if (!g) return false;
    m.sieging = false;
    const range = ranged ? m.def.range : m.def.range + 0.6;
    m.ry = (Math.atan2(g.x - m.x, g.z - m.z) * 180) / Math.PI;
    if (bd > range) {
      m.path = [[g.x, g.z]];
      moveAlongPath(m, speedOf(m) * 0.9);
      return true;
    }
    m.path = [];
    if (t >= m.nextAttack) {
      m.nextAttack = t + m.def.cd * rand(0.9, 1.2);
      monsterAttack(m, null, ranged ? "shot" : "melee", 0, g.x, g.z);
      g.hp -= m.dmg * rand(0.8, 1.2);
      if (g.hp <= 0) { g.hp = 0; guardEvent({ k: "die", id: g.id }); }
      else guardEvent({ k: "hurt", id: g.id });
    }
    return true;
  }

  function spawnWave() {
    inv.wave++;
    postGuards();
    const count = Math.min(18, 3 + inv.wave * 2 + (inv.players - 1) * 2);
    for (let i = 0; i < count + (inv.wave === WAVES ? 1 : 0); i++) {
      const warlord = inv.wave === WAVES && i === count;
      for (let attempt = 0; attempt < 20; attempt++) {
        const a = Math.random() * Math.PI * 2, r = Math.sqrt(Math.random()) * 5;
        const spot = nearestWalkable(Math.floor(inv.sx + Math.cos(a) * r), Math.floor(inv.sz + Math.sin(a) * r), 2);
        if (!spot) continue;
        const type = warlord ? [...inv.types].sort((x, y) => MONSTERS[y].hp - MONSTERS[x].hp)[0] : inv.types[Math.floor(Math.random() * inv.types.length)];
        const m = spawnMonster(type, inv.level + (warlord ? 2 : 0), spot[0] + 0.5, spot[1] + 0.5, null);
        m.invasion = inv.id;
        m.homeX = inv.gate.x; m.homeZ = inv.gate.z; // they "return" to the gate, not to where they appeared
        m.leash = 40;
        m.xpMul = 1.3;
        m.state = "idle";
        m.wanderAt = Infinity;
        if (warlord) {
          makeElite(m, 3);
          m.elite.name = `Warlord ${m.elite.name.split(" ")[0]}`;
          m.maxHp = m.hp = Math.round(m.maxHp * 1.8);
          m.warlord = true;
        } else if (Math.random() < 0.1) makeElite(m, 1);
        inv.ids.add(m.id);
        break;
      }
    }
    inv.waveAt = now();
    const what = inv.wave === WAVES ? "The last wave, led by a warlord, charges" : `Wave ${inv.wave} of ${WAVES} marches`;
    broadcast({ t: "sys", msg: `${what} on the ${inv.gate.name} gate of ${inv.town.name}!` });
  }

  const alive = () => { let n = 0; for (const id of inv.ids) if (monsters.has(id)) n++; else inv.ids.delete(id); return n; };

  function state() {
    if (!inv) return { phase: "none" };
    const left = inv.phase === "gather" ? Math.max(0, Math.ceil(inv.waveAt - now())) : 0;
    const live = inv.phase === "won" || inv.phase === "lost" ? [] : inv.guards.filter((g) => g.hp > 0);
    return { town: inv.town.name, gate: inv.gate.name, phase: inv.phase, gx: r2(inv.gate.x), gz: r2(inv.gate.z),
      wave: inv.wave, waves: WAVES, left: inv.phase === "wave" ? alive() : left, hp: Math.max(0, Math.round(inv.integrity)),
      gd: live.map((g) => ({ i: g.id, k: g.k, x: r2(g.x), z: r2(g.z), hp: Math.ceil(g.hp), mh: g.mhp })) };
  }

  function send() {
    if (inv) { inv.sentAt = now(); inv.sentHp = inv.integrity; }
    const data = JSON.stringify({ t: "invasion", iv: state() });
    for (const s of sessions.values()) if (s.inWorld) safeSend(s, data);
  }

  function end(won) {
    inv.phase = won ? "won" : "lost";
    inv.endedAt = now();
    metrics.inc({ town: inv.town.name, result: won ? "won" : "lost" });
    if (won) {
      let n = 0;
      for (const s of sessions.values()) {
        if (!s.inWorld || s.inst || !inv.defenders.has(s.name)) continue;
        n++;
        const lvl = s.lvl || 1;
        const xp = Math.round(15 * Math.pow(lvl, 1.55));
        const loot = s.ledger ? rollLoot({ name: "Invasion", boss: true }, inv.level, 0, heroClass(s), false, deep(inv.gate.x, inv.gate.z) ? "deep" : "surface") : [];
        const drops = s.ledger ? dropFor(s, s.x, s.z, loot, 0) : [];
        safeSend(s, JSON.stringify({ t: "invwin", k: inv.town.name, xp, drops }));
      }
      broadcast({ t: "sys", msg: `${inv.town.name} holds! ${n} ${n === 1 ? "defender" : "defenders"} drove off the invaders.` });
      log(`Invasion of ${inv.town.name} beaten off by ${n}`);
    } else {
      for (const id of inv.ids) monsters.delete(id); // they withdraw with their spoils
      inv.ids.clear();
      broadcast({ t: "sys", msg: `The invaders broke through the ${inv.gate.name} gate of ${inv.town.name}, plundered the market and withdrew.` });
      log(`Invasion of ${inv.town.name} succeeded`);
    }
    send();
    nextAt = minutes > 0 ? now() + minutes * 60 * rand(0.75, 1.25) : Infinity;
  }

  function tick(t) {
    if (!inv) {
      if (t >= nextAt) {
        const why = begin(null);
        if (why) nextAt = t + 5 * 60; // try again in a while
      }
      return;
    }
    if (inv.phase === "won" || inv.phase === "lost") {
      if (t - inv.endedAt > RESULT_S) { inv = null; send(); }
      return;
    }
    if (inv.phase === "gather") {
      if (t >= inv.waveAt) { inv.phase = "wave"; spawnWave(); send(); }
      else if (t - inv.sentAt > 2) send();
      return;
    }
    updateGuards(t);
    inv.guards = inv.guards.filter((g) => g.hp > 0);
    // Battering the gate
    for (const id of inv.ids) {
      const m = monsters.get(id);
      if (m && m.sieging && m.state === "idle") inv.integrity -= SIEGE_RATE * (m.warlord ? 4 : m.def.hp >= 200 ? 2 : 1) * 0.1;
    }
    const n = alive();
    if (inv.integrity <= 0 || t - inv.began > MAX_S) return end(false);
    if (inv.wave < WAVES && (n <= 2 || t - inv.waveAt > WAVE_GAP_S)) { spawnWave(); send(); return; }
    if (inv.wave >= WAVES && n === 0) return end(true);
    // Every few points off the gate goes out at once (the gate shows its damage), the rest every two seconds; while
    // guards are out it goes a few times a second, so their walking looks smooth.
    if (t - inv.sentAt > (inv.guards.length ? 0.35 : 2) || (inv.sentHp - inv.integrity >= 2 && t - inv.sentAt > 0.4)) send();
  }

  /** An invader with nobody to fight: march on the gate, then batter it. */
  function idle(m, t) {
    if (!inv || m.invasion !== inv.id || inv.phase !== "wave") return false;
    if (fightGuard(m, t)) return true; // no hero near: the guards before the gate
    const g = inv.gate, d = dist(m.x, m.z, g.x, g.z);
    if (d < 3.5) {
      m.path = [];
      m.sieging = true;
      m.ry = (Math.atan2(g.x - m.x, g.z - m.z) * 180) / Math.PI;
      if (t >= m.nextAttack) {
        m.nextAttack = t + m.def.cd * rand(1.2, 1.8);
        monsterAttack(m, null, "melee", 0, g.x - g.nx * 2, g.z - g.nz * 2); // the swing, at the gate
      }
      return true;
    }
    m.sieging = false;
    if (!m.path.length || t >= m.repathAt) {
      m.repathAt = t + rand(1.5, 2.5);
      m.path = findPath(m.x, m.z, g.x + rand(-1.5, 1.5) * Math.abs(g.nz), g.z + rand(-1.5, 1.5) * Math.abs(g.nx), 8000);
    }
    moveAlongPath(m, speedOf(m) * 0.8);
    return true;
  }

  function onDamage(m, s) {
    if (inv && m.invasion === inv.id && s && s.name) inv.defenders.set(s.name, (inv.defenders.get(s.name) || 0) + 1);
    m.sieging = false;
  }

  return {
    tick, idle, onDamage,
    sendTo(s) { if (inv) safeSend(s, JSON.stringify({ t: "invasion", iv: state() })); },
    active: () => (inv && inv.phase !== "won" && inv.phase !== "lost" ? inv.town.name : ""),
    /** Admin: start one now (at the named town, or the busiest), or end the current one. */
    start(townName, gateName) {
      if (inv && inv.phase !== "won" && inv.phase !== "lost") return `${inv.town.name} is already under attack.`;
      inv = null;
      const t = townName ? walled.find((w) => w.name.toLowerCase().startsWith(String(townName).toLowerCase())) : null;
      if (townName && !t) return `No walled town called "${townName}" (${walled.map((w) => w.name).join(", ")}).`;
      const why = begin(t, gateName);
      return why ? `No invasion: ${why}.` : `Invasion of ${inv.town.name} begins at the ${inv.gate.name} gate.`;
    },
    stop() {
      if (!inv) return "There is no invasion.";
      for (const id of inv.ids) monsters.delete(id);
      inv.ids.clear();
      const name = inv.town.name;
      inv = null;
      send();
      nextAt = minutes > 0 ? now() + minutes * 60 : Infinity;
      return `The invasion of ${name} is called off.`;
    },
    /** Tests: skip the gathering, or the gate's remaining integrity. */
    _debug: () => inv,
  };
};
