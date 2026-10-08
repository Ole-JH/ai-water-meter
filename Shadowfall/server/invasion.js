// Town invasions. Every INVASION_MINUTES or so (default 45, 0 = never), monsters from the surrounding lands gather
// outside a gate of a walled town where players are, and attack it in three waves. They march on the gate and batter
// it; heroes they meet on the way are fought as usual. Heroes inside the walls are out of their reach, but the town's
// militia put up ladders behind the wall during the gathering (the client's Rampart.cs): heroes on the wall can shoot
// down from there, and only the invaders' archers and casters can shoot back. The gate is shut while it lasts. Kill every wave, the last led by a warlord, and the town holds: everyone who hurt an invader gets
// experience and a boss's share of loot. If the gate falls (or the siege drags on for 12 minutes), the invaders
// plunder the town and withdraw, and nobody is rewarded. Worse: they set fire to the quarter behind the broken gate
// (the "sack", SACK_S, 5 minutes). It burns, its people flee, and its merchants, smiths and auctioneers are gone
// until the fires are out: the server refuses their trade to anyone standing in the quarter (sackedAt).
//
// The server calls: tick(t) every simulation tick, idle(m, t) for an invader with nobody to fight, onDamage(m, s) when
// a hero hurts one, sendTo(s) when a hero enters the world, and start()/stop() for the admin command.

const WAVES = 3;
// An admin's invasion without the scouts' warning gathers this long before the first wave; after a warning the first
// wave falls at once (the warning was the time to get there)
const GATHER_S = Number(process.env.INVASION_GATHER_S ?? 60);
const WAVE_GAP_S = 75;      // the next wave comes after this long, or sooner when the last one is nearly dead
const MAX_S = 12 * 60;      // a siege nobody answers ends in a sack
// Gate integrity (of 100) lost per second per invader at the gate (big ones count double)
const SIEGE_RATE = Number(process.env.INVASION_SIEGE_RATE ?? 0.35);
const RESULT_S = 15;        // how long the outcome stays on players' screens
const SACK_S = Number(process.env.SACK_S ?? 720); // how long the quarter behind a broken gate burns (deliveries to the reeve shorten it)
// Scouts see the raiders coming: an invasion the server starts by itself is announced this long before they gather
// (phase "warn"), so heroes further away have time to come and defend
const WARN_S = Number(process.env.INVASION_WARN_S ?? 90);
// Nobody near the town: the warning's countdown and the siege stand still (the raiders wait, the gate isn't battered);
// after this long with nobody about they give up and scatter, and the town isn't sacked
const ABANDON_S = Number(process.env.INVASION_ABANDON_S ?? 180);
const STAGING = 24;         // how far outside the gate the invaders gather
const PLAYERS_NEAR = 110;   // players this close to a town count towards it (choosing the town, scaling the waves)
// The town's guards: archers up on the wall walk beside the gate and soldiers holding a line outside it. They help a
// little (a few percent of an invader's life a blow) so the heroes still do the real work; the fallen are replaced at
// each new wave. Invaders still go for heroes first; with none around they fight the guards, then the gate.
const ARCHERS = 4, SOLDIERS = 6;
const GUARD_HIT = 0.025;    // share of an invader's max life per guard blow
const ARCHER_RANGE = 22, ARCHER_CD = 2.6, SOLDIER_CD = 1.7, SOLDIER_REACH = 7, GUARD_SPEED = 3.2;
// The battering ram: built in the war camp, pushed to the gate by its crew when the first wave charges, and much harder
// on the gate than any raider. Kill its crew (in the camp, or on the way) and it stands where it is.
const RAM = "Battering Ram", RAM_CREW = 2, RAM_SPEED = 1.5, RAM_CD = 2.4, RAM_HIT = 5; // RAM_HIT: as many raiders at the gate
// Beacons: two braziers just inside the wall either side of the gate; lit by a hero, they give the wall's archers fire
// arrows (each lit beacon: half as much damage again)
const BEACON_OFF = 9, BEACON_BONUS = 0.5;
// Fire arrows: the raiders' archers and casters set roofs behind the gate alight now and then. A fire grows; the
// townsfolk's buckets slow it, heroes can put it out (DOUSE a throw); one at full strength spreads to a neighbour.
const FIRE_EVERY = Number(process.env.INVASION_FIRE_S ?? 18), FIRE_MAX = 8, FIRE_GROW = 1.6, FIRE_BUCKETS = 0.8, DOUSE = 35, FIRE_SPREAD_S = 20;
// The banner bearer marches with the warlord; when he falls the raiders lose heart: some flee, the rest hit softer
const ROUT_SHARE = 0.4, ROUT_DMG = 0.75;
const WARLORD_TAUNTS = ["Your gate is kindling, your walls are sand!", "I have burned bigger towns than this before breakfast!",
  "Bring me their champion's head!", "Break it down! Leave nothing standing!", "Is this all the defence they have? Ha!"];

// After a siege. Won: a feast in the town square (FEAST_S; heroes who join it get the Heroes' Feast, see the client) and
// carpenters at the gate. Won or lost: graves outside the wall for the guards who fell. Lost: the raiders drag
// townsfolk off to their camp; free them (kill their captors) within CAPTIVE_S and the fires burn half as long again.
const FEAST_S = Number(process.env.INVASION_FEAST_S ?? 300), REPAIR_S = 180, GRAVES_S = 2 * 3600;
// The feast's table in the square: every hero may eat from it once, for FEAST_BUFF_S of +FEAST_XP experience
const FEAST_BUFF_S = 15 * 60, FEAST_XP = 1.25;
const CAPTIVE_S = Number(process.env.INVASION_CAPTIVE_S ?? 600);
// A town's prosperity (-3..3, remembered with its siege record): defending it raises it, losing it (or its captives,
// or roofs left burning) lowers it, and it drifts back towards 0 one step every PROSPERITY_DRIFT_H hours. Its merchants
// charge 4% less (or more) a step.
const PROSPERITY_DRIFT_H = 6, PRICE_STEP = 0.04;

module.exports = function createInvasions(ctx) {
  const { TOWNS, SPAWNERS, MONSTERS, monsters, sessions, spawnMonster, makeElite, findPath, nearestWalkable, moveAlongPath,
    speedOf, monsterAttack, broadcast, safeSend, rollLoot, dropFor, heroClass, deep, log, now, rand, dist, r2, metrics,
    walkable, guardHit, store } = ctx;

  const minutes = Number(process.env.INVASION_MINUTES ?? 45);
  let inv = null;
  let nextId = 1;
  let lastTick = 0;
  let nextAt = minutes > 0 ? now() + rand(0.3, 0.6) * minutes * 60 : Infinity;

  const walled = TOWNS.filter((t) => t.walled);

  // ------------------------------------------------------------------ the towns' siege record (persisted)

  let chronicle = {}; // town name -> { h held, f fell, sp spared, last, at (ms), g gate, d defenders' names, p prosperity, pAt }
  const saveChronicle = () => { if (store) store.setMeta("siege_chronicle", JSON.stringify(chronicle)).catch((e) => log(`siege chronicle save failed: ${e.message}`)); };
  const recordOf = (name) => chronicle[name] || (chronicle[name] = { h: 0, f: 0, sp: 0, last: "", at: 0, g: "", d: [], p: 0, pAt: Date.now() });

  /** A town's prosperity now (drifting back towards 0 with time). */
  function prosperity(name) {
    const c = chronicle[name];
    if (!c) return 0;
    const steps = Math.floor((Date.now() - (c.pAt || 0)) / (PROSPERITY_DRIFT_H * 3600 * 1000));
    if (steps > 0) {
      for (let i = 0; i < steps && c.p; i++) c.p -= Math.sign(c.p);
      c.pAt = (c.pAt || Date.now()) + steps * PROSPERITY_DRIFT_H * 3600 * 1000;
    }
    return c.p;
  }
  function shiftProsperity(name, by) {
    const c = recordOf(name);
    prosperity(name);
    c.p = Math.max(-3, Math.min(3, c.p + by));
    c.pAt = Date.now();
  }

  /** How the siege ended for the record: "h" held, "f" fell, "sp" spared (nobody came). */
  function record(name, result, gate, defenders) {
    const c = recordOf(name);
    c[result]++;
    c.last = result;
    c.at = Date.now();
    c.g = gate;
    if (defenders) c.d = defenders.slice(0, 8);
    if (result === "h") shiftProsperity(name, 1);
    if (result === "f") shiftProsperity(name, -2);
    saveChronicle();
    sendChronicle();
  }

  function chronicleState() {
    const t = Date.now();
    return { t: "chron", rec: walled.map((w) => {
      const c = chronicle[w.name] || {};
      return { k: w.name, h: c.h || 0, f: c.f || 0, sp: c.sp || 0, last: c.last || "", ago: c.at ? Math.round((t - c.at) / 1000) : -1, g: c.g || "", d: c.d || [], p: prosperity(w.name) };
    }) };
  }
  function sendChronicle() {
    const data = JSON.stringify(chronicleState());
    for (const s of sessions.values()) if (s.inWorld) safeSend(s, data);
  }

  // ------------------------------------------------------------------ the aftermath: feasts, repairs, graves, captives

  const feasts = new Map();  // town -> { town, until }
  const repairs = new Map(); // town -> { town, gate, until }
  let graves = [];           // { town, gate, n, seed, until }
  let captives = null;       // { town, gate, x, z, n, until, ids, helpers }

  function afterState() {
    const t = now();
    return { t: "after",
      fe: [...feasts.values()].map((f) => ({ k: f.town.name, x: r2(centre(f.town).x), z: r2(centre(f.town).z), left: Math.ceil(f.until - t) })),
      rp: [...repairs.values()].map((r) => ({ k: r.town.name, g: r.gate.name, x: r2(r.gate.x), z: r2(r.gate.z), left: Math.ceil(r.until - t) })),
      gr: graves.map((g) => ({ k: g.town.name, g: g.gate.name, x: r2(g.gate.x), z: r2(g.gate.z), n: g.n, s: g.seed })),
      cp: captives ? [{ k: captives.town.name, x: r2(captives.x), z: r2(captives.z), n: captives.n, left: Math.ceil(captives.until - t), c: captorsLeft() }] : [] };
  }
  function sendAfter() {
    const data = JSON.stringify(afterState());
    for (const s of sessions.values()) if (s.inWorld) safeSend(s, data);
  }
  const captorsLeft = () => { let n = 0; if (captives) for (const id of captives.ids) if (monsters.has(id)) n++; return n; };

  /** The fires of a sack are out: carpenters put up scaffolding at the broken gate and build it anew. */
  function mend(k) {
    repairs.set(k.town.name, { town: k.town, gate: k.gate, until: now() + REPAIR_S });
    sendAfter();
  }

  /** The raiders of a lost siege drag townsfolk off to their camp, guarded. */
  function takeCaptives(i) {
    const n = 3 + Math.floor(Math.random() * 3);
    const ids = new Set();
    const guards = Math.min(8, 3 + i.players);
    const types = i.types.filter((ty) => !MONSTERS[ty].ranged).length ? i.types.filter((ty) => !MONSTERS[ty].ranged) : i.types;
    for (let k = 0; k < guards; k++) {
      const a = (k / guards) * Math.PI * 2;
      const spot = nearestWalkable(Math.floor(i.sx + Math.cos(a) * 3.5), Math.floor(i.sz + Math.sin(a) * 3.5), 3);
      if (!spot) continue;
      const m = spawnMonster(types[k % types.length], i.level, spot[0] + 0.5, spot[1] + 0.5, null);
      m.captor = true;
      m.leash = 22;
      if (k === 0) { makeElite(m, 1); m.elite.name = "Slaver " + m.elite.name.split(" ")[0]; }
      ids.add(m.id);
    }
    captives = { town: i.town, gate: i.gate, x: i.sx, z: i.sz, n, until: now() + CAPTIVE_S, ids, helpers: new Set() };
    broadcast({ t: "sys", msg: `The raiders dragged ${n} townsfolk from ${i.town.name} back to their camp outside the ${i.gate.name} gate! Free them within ${Math.round(CAPTIVE_S / 60)} minutes and the town will be back on its feet sooner.` });
    sendAfter();
  }

  function updateAftermath(t) {
    let changed = false;
    for (const [k, f] of feasts) if (t >= f.until) { feasts.delete(k); changed = true; }
    for (const [k, r] of repairs) if (t >= r.until) { repairs.delete(k); changed = true; }
    const before = graves.length;
    graves = graves.filter((g) => t < g.until);
    if (graves.length !== before) changed = true;
    if (captives) {
      const left = captorsLeft();
      if (left === 0) {
        const c = captives;
        captives = null;
        const k = sacks.get(c.town.name);
        if (k) { k.until -= Math.max(0, (k.until - t) / 2); sendSacks(); }
        shiftProsperity(c.town.name, 1);
        saveChronicle();
        sendChronicle();
        for (const s of sessions.values()) {
          if (!s.inWorld || !c.helpers.has(s.name)) continue;
          safeSend(s, JSON.stringify({ t: "rescued", k: c.town.name, xp: Math.round(10 * Math.pow(s.lvl || 1, 1.5)) }));
        }
        const names = [...c.helpers].slice(0, 5);
        broadcast({ t: "sys", msg: `The captives of ${c.town.name} are free${names.length ? `, thanks to ${names.join(", ")}` : ""}! They hurry home to fight the fires.` });
        log(`Captives of ${c.town.name} freed by ${c.helpers.size}`);
        changed = true;
      } else if (t >= captives.until) {
        const c = captives;
        captives = null;
        for (const id of c.ids) monsters.delete(id);
        shiftProsperity(c.town.name, -1);
        saveChronicle();
        sendChronicle();
        broadcast({ t: "sys", msg: `Nobody came for the captives of ${c.town.name}: the raiders carried them off into the wilds.` });
        changed = true;
      } else if (left !== captives.shown) { captives.shown = left; changed = true; }
    }
    if (changed) sendAfter();
  }
  // Burning quarters: town name -> { town, gate, until }. The quarter is everything inside the walls within
  // sackRadius of the broken gate (most of the town: all but its far side).
  const sacks = new Map();
  const sackRadius = (t) => 0.85 * (t.x1 - t.x0); // most of the town: everything but the far side
  const inside = (t, x, z) => x >= t.x0 && x <= t.x1 && z >= t.z0 && z <= t.z1;

  function sackState() {
    const t = now();
    return { t: "sack", sk: [...sacks.values()].map((k) => ({ k: k.town.name, g: k.gate.name, x: r2(k.gate.x), z: r2(k.gate.z),
      r: r2(sackRadius(k.town)), left: Math.max(0, Math.ceil(k.until - t)) })) };
  }
  function sendSacks() {
    const data = JSON.stringify(sackState());
    for (const s of sessions.values()) if (s.inWorld) safeSend(s, data);
  }
  function sack(town, gate) {
    sacks.set(town.name, { town, gate, until: now() + SACK_S });
    sendSacks();
  }
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

  /** The walled town for the next siege: one with heroes about (more heroes, more likely), not one already burning. */
  function chooseTown() {
    const players = overworld();
    const weighted = walled.filter((w) => !sacks.has(w.name)).map((w) => ({ t: w, n: near(w, players).length })).filter((w) => w.n > 0);
    if (!weighted.length) return null;
    let r = Math.random() * weighted.reduce((a, w) => a + w.n, 0);
    return weighted.find((w) => (r -= w.n) < 0)?.t || weighted[0].t;
  }

  /**
   * Scouts sight the raiders: the town and gate are named now (phase "warn") and for WARN_S seconds the first wave
   * masses in a war camp outside the gate, a few at a time, while the guards take their posts. Heroes can fall on the
   * camp before it's whole (every raider killed there is one fewer in the first wave); then the wave charges.
   */
  function scout(town, gateName) {
    const t = town || chooseTown();
    if (!t) return "nobody is near a walled town";
    return begin(t, gateName, true);
  }

  /** How many invaders a wave has (the warlord comes on top of the last). */
  const waveSize = (w) => Math.min(18, 3 + w * 2 + (inv.players - 1) * 2);

  /** Sets up a siege of town t (or the busiest walled town); warned: with the scouts' warning first. Returns why not, or null. */
  function begin(t, gateName, warned) {
    if (!t) {
      t = chooseTown();
      if (!t) return "nobody is near a walled town";
    }
    const options = gates(t).filter((g) => !gateName || g.name === gateName).sort(() => Math.random() - 0.5);
    for (const g of options) {
      const spot = nearestWalkable(Math.floor(g.x + g.nx * STAGING), Math.floor(g.z + g.nz * STAGING), 8);
      if (!spot) continue;
      const sx = spot[0] + 0.5, sz = spot[1] + 0.5;
      if (!findPath(sx, sz, g.x, g.z, 8000).length) continue;
      const ros = roster(sx, sz);
      if (!ros.types.length) continue;
      const defenders = near(t);
      const avg = defenders.length ? defenders.reduce((a, s) => a + (s.lvl || 1), 0) / defenders.length : ros.level;
      const level = Math.max(1, Math.min(40, Math.round(Math.max(ros.level, avg - 1))));
      inv = {
        id: nextId++, town: t, gate: g, sx, sz, types: ros.types, level, players: Math.max(1, defenders.length),
        phase: warned ? "warn" : "gather", wave: 0, integrity: 100, began: now(), waveAt: now() + (warned ? WARN_S : GATHER_S),
        ids: new Set(), defenders: new Map(), sentAt: 0, endedAt: 0, guards: [], nextGuard: 1, massed: 0, nextMass: now() + Math.min(2, WARN_S * 0.1),
        ram: 0, ramStopped: false, beacons: [], fires: [], nextFire: Infinity, nextFireId: 1, bearer: 0, routed: false, nextTaunt: 0, lostFires: 0,
      };
      buildRam();
      placeBeacons();
      postGuards(); // the guards muster at once and march to their posts, ready well before the first wave
      if (warned) {
        broadcast({ t: "sys", msg: `Scouts sight raiders massing outside the ${g.name} gate of ${t.name}! They attack in ${Math.round(WARN_S)} seconds. Defenders, to the walls, or strike their camp before they're ready!` });
      } else broadcast({ t: "sys", msg: `${t.name} is under attack! Monsters are gathering outside its ${g.name} gate. Defend the town!` });
      log(`${warned ? "Raiders sighted near" : "Invasion of"} ${t.name} (${g.name} gate, level ${level}, ${ros.types.join(", ")})`);
      send();
      return null;
    }
    return `no gate of ${t.name} can be reached from outside`;
  }

  // ------------------------------------------------------------------ the ram, the beacons

  /** The battering ram and its crew, at the edge of the camp nearest the gate. */
  function buildRam() {
    const g = inv.gate, ox = -g.nx, oz = -g.nz; // towards the gate
    const spot = nearestWalkable(Math.floor(inv.sx + ox * 4), Math.floor(inv.sz + oz * 4), 3);
    if (!spot) return;
    const ram = spawnMonster(RAM, inv.level, spot[0] + 0.5, spot[1] + 0.5, null);
    ram.invasion = inv.id;
    ram.homeX = g.x; ram.homeZ = g.z;
    ram.leash = 999;
    ram.state = "idle";
    ram.wanderAt = Infinity;
    ram.ry = (Math.atan2(g.x - ram.x, g.z - ram.z) * 180) / Math.PI;
    inv.ids.add(ram.id);
    inv.ram = ram.id;
    for (let i = 0; i < RAM_CREW; i++) {
      const m = spawnOne(false);
      if (m) { m.crew = true; m.x = ram.x + (i ? 1.2 : -1.2) * Math.abs(g.nz); m.z = ram.z + (i ? 1.2 : -1.2) * Math.abs(g.nx); if (!walkable(m.x, m.z)) { m.x = ram.x; m.z = ram.z; } }
    }
  }

  const ramOf = () => (inv && inv.ram ? monsters.get(inv.ram) : null);
  const crewAlive = () => { for (const id of inv.ids) { const m = monsters.get(id); if (m && m.crew && m.hp > 0) return true; } return false; };

  /** The ram's turn (its own AI: no hero ever draws it off). */
  function ramIdle(m, t) {
    m.sieging = false;
    if (inv.phase !== "wave" || inv.paused) { m.path = []; return true; }
    if (!crewAlive()) {
      if (!inv.ramStopped) {
        inv.ramStopped = true;
        broadcast({ t: "sys", msg: `The battering ram before ${inv.town.name} stands abandoned: its crew lies dead!` });
        sayNear(m, "");
      }
      m.path = [];
      return true;
    }
    const g = inv.gate, d = dist(m.x, m.z, g.x, g.z);
    if (d < 4) {
      m.path = [];
      m.sieging = true;
      m.ry = (Math.atan2(g.x - m.x, g.z - m.z) * 180) / Math.PI;
      if (t >= m.nextAttack) { m.nextAttack = t + RAM_CD; monsterAttack(m, null, "melee", 0, g.x - g.nx * 2, g.z - g.nz * 2); }
      return true;
    }
    if (!m.path.length || t >= m.repathAt) { m.repathAt = t + 3; m.path = findPath(m.x, m.z, g.x + g.nx * 2.5, g.z + g.nz * 2.5, 8000); }
    moveAlongPath(m, RAM_SPEED);
    return true;
  }

  /** The two braziers by the gate, inside the wall. */
  function placeBeacons() {
    const g = inv.gate, ax = Math.abs(g.nz), az = Math.abs(g.nx);
    for (const off of [-BEACON_OFF, BEACON_OFF]) {
      const spot = nearestWalkable(Math.floor(g.x - g.nx * 4 + ax * off), Math.floor(g.z - g.nz * 4 + az * off), 3);
      if (spot && inside(inv.town, spot[0] + 0.5, spot[1] + 0.5)) inv.beacons.push({ x: spot[0] + 0.5, z: spot[1] + 0.5, lit: "" });
    }
  }

  const litBeacons = () => (inv ? inv.beacons.filter((b) => b.lit).length : 0);

  /** A speech bubble over a monster, for those near it (the warlord's taunts). */
  function sayNear(m, text) {
    if (!text) return;
    const data = JSON.stringify({ t: "gev", k: "say", mid: m.id, msg: text });
    for (const s of sessions.values()) if (s.inWorld && !s.inst && dist(s.x, s.z, m.x, m.z) < 60) safeSend(s, data);
  }

  // ------------------------------------------------------------------ fire arrows on the roofs

  /** A raider archer or caster looses a fire arrow over the wall: a roof behind the gate catches. */
  function fireArrow(t) {
    inv.nextFire = t + FIRE_EVERY * rand(0.7, 1.3);
    if (inv.fires.length >= FIRE_MAX) return;
    let shooter = null;
    for (const id of inv.ids) { const m = monsters.get(id); if (m && m.hp > 0 && m.def.ranged && !m.fleeing && dist(m.x, m.z, inv.gate.x, inv.gate.z) < 30) { shooter = m; break; } }
    if (!shooter) return;
    const g = inv.gate;
    for (let tries = 0; tries < 8; tries++) {
      const a = rand(-1.1, 1.1), r = rand(8, 24);
      const ix = -g.nx, iz = -g.nz; // into town
      const x = g.x + (ix * Math.cos(a) - iz * Math.sin(a)) * r, z = g.z + (iz * Math.cos(a) + ix * Math.sin(a)) * r;
      if (!inside(inv.town, x, z)) continue;
      startFire(x, z, 30, shooter);
      return;
    }
  }

  function startFire(x, z, strength, shooter) {
    const f = { id: inv.nextFireId++, x, z, s: strength, fullAt: 0 };
    inv.fires.push(f);
    guardEvent({ k: "fire", id: f.id, x: r2(x), z: r2(z), ...(shooter ? { mid: shooter.id, sx: r2(shooter.x), sz: r2(shooter.z) } : {}) });
    send();
  }

  function updateFires(t, dt) {
    if (inv.phase === "wave" && t >= inv.nextFire) fireArrow(t);
    let changed = false;
    for (const f of inv.fires) {
      const was = f.s;
      f.s = Math.min(100, f.s + (FIRE_GROW - FIRE_BUCKETS) * dt);
      if (f.s >= 100) {
        if (!f.fullAt) f.fullAt = t;
        else if (t - f.fullAt > FIRE_SPREAD_S && inv.fires.length < FIRE_MAX) {
          f.fullAt = t;
          const a = rand(0, Math.PI * 2), x = f.x + Math.cos(a) * 7, z = f.z + Math.sin(a) * 7;
          if (inside(inv.town, x, z)) { startFire(x, z, 25, null); broadcast({ t: "sys", msg: `The fire in ${inv.town.name} is spreading! Douse it before it takes the whole street.` }); }
        }
      } else f.fullAt = 0;
      if (Math.floor(was / 10) !== Math.floor(f.s / 10)) changed = true;
    }
    return changed;
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
      // they muster in the street behind the gate, march out through it and take up their post
      const mx = g.x - g.nx * 9 + ax * (off * 0.4), mz = g.z - g.nz * 9 + az * (off * 0.4);
      const fromIn = inv.phase === "warn" || inv.phase === "gather" ? walkable(mx, mz) : false;
      inv.guards.push({ id: inv.nextGuard++, k: "s", x: fromIn ? mx : g.x, z: fromIn ? mz : g.z, px, pz, hp: 70 + lvl * 26, mhp: 70 + lvl * 26, next: now() + 1,
        ...(fromIn ? { wx: g.x, wz: g.z } : {}) });
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

  /** A soldier on the way to his post: through the gateway first if he mustered inside. True while still marching. */
  function march(o) {
    if (o.wx !== undefined) {
      step(o, o.wx, o.wz, GUARD_SPEED);
      if (dist(o.x, o.z, o.wx, o.wz) < 0.3) { delete o.wx; delete o.wz; }
      return true;
    }
    step(o, o.px, o.pz, GUARD_SPEED);
    return dist(o.x, o.z, o.px, o.pz) > 0.1;
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
        const lit = litBeacons();
        guardEvent({ k: "shot", id: g.id, mid: m.id, x: r2(m.x), z: r2(m.z), ...(lit ? { f: 1 } : {}) }); // f: fire arrows (a beacon is lit)
        guardHit(m, Math.max(1, Math.round(m.maxHp * GUARD_HIT * (1 + lit * BEACON_BONUS))));
        continue;
      }
      // a soldier: steps up to an invader near his post, otherwise holds the line
      const m = nearestInvader(g.px, g.pz, SOLDIER_REACH, foes);
      if (!m || g.wx !== undefined) { march(g); continue; }
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
      if (g.hp <= 0) { g.hp = 0; inv.fallen = (inv.fallen || 0) + 1; guardEvent({ k: "die", id: g.id }); }
      else guardEvent({ k: "hurt", id: g.id });
    }
    return true;
  }

  /** One invader at the war camp outside the gate (the warlord leads the last wave). */
  function spawnOne(warlord) {
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
      return m;
    }
    return null;
  }

  function spawnWave() {
    inv.wave++;
    postGuards();
    // the first wave is whoever massed in the camp during the warning (those killed there don't come), plus the rest
    let count = waveSize(inv.wave);
    if (inv.wave === 1) count = Math.max(0, count - inv.massed);
    for (let i = 0; i < count; i++) spawnOne(false);
    if (inv.wave === WAVES) {
      const lord = spawnOne(true);
      // the banner bearer marches at his side
      const b = spawnOne(false);
      if (b) {
        if (!b.elite) makeElite(b, 1);
        b.elite.name = "Banner Bearer";
        b.bearer = true;
        inv.bearer = b.id;
        if (lord) { b.x = lord.x; b.z = lord.z; }
      }
      if (lord) { inv.warlord = lord.id; inv.nextTaunt = now() + 4; sayNear(lord, WARLORD_TAUNTS[0]); }
    }
    if (inv.wave === 1) inv.nextFire = now() + FIRE_EVERY * rand(0.5, 0.9);
    inv.waveAt = now();
    const what = inv.wave === WAVES ? "The last wave, led by a warlord, charges" : `Wave ${inv.wave} of ${WAVES} marches`;
    broadcast({ t: "sys", msg: `${what} on the ${inv.gate.name} gate of ${inv.town.name}!` });
  }

  // invaders still fighting (the ram isn't one: it never counts towards a wave, and is left behind if the town holds)
  const alive = () => { let n = 0; for (const id of inv.ids) { const m = monsters.get(id); if (!m) inv.ids.delete(id); else if (!m.def.siege) n++; } return n; };

  function state() {
    if (!inv) return { phase: "none" };
    const left = inv.phase === "gather" || inv.phase === "warn" ? Math.max(0, Math.ceil(inv.waveAt - now())) : 0;
    const live = inv.phase === "won" || inv.phase === "lost" ? [] : inv.guards.filter((g) => g.hp > 0);
    return { town: inv.town.name, gate: inv.gate.name, phase: inv.phase, gx: r2(inv.gate.x), gz: r2(inv.gate.z), sx: r2(inv.sx), sz: r2(inv.sz),
      n: inv.phase === "warn" ? alive() : 0, // raiders in the war camp
      wave: inv.wave, waves: WAVES, left: inv.phase === "wave" ? alive() : left, hp: Math.max(0, Math.round(inv.integrity)),
      gd: live.map((g) => ({ i: g.id, k: g.k, x: r2(g.x), z: r2(g.z), hp: Math.ceil(g.hp), mh: g.mhp })), paused: !!inv.paused,
      bc: inv.beacons.map((b) => ({ x: r2(b.x), z: r2(b.z), l: b.lit })),          // the beacons (l: who lit it, "" = unlit)
      fr: inv.phase === "won" || inv.phase === "lost" ? [] : inv.fires.map((f) => ({ i: f.id, x: r2(f.x), z: r2(f.z), s: Math.round(f.s) })), // burning roofs
      ram: inv.ram && monsters.has(inv.ram) ? (inv.ramStopped ? 2 : 1) : 0, // 1: the ram is coming, 2: abandoned
      rt: inv.routed };
  }

  function send() {
    if (inv) { inv.sentAt = now(); inv.sentHp = inv.integrity; }
    const data = JSON.stringify({ t: "invasion", iv: state() });
    for (const s of sessions.values()) if (s.inWorld) safeSend(s, data);
  }

  function end(won) {
    inv.phase = won ? "won" : "lost";
    inv.lostFires = inv.fires.filter((f) => f.s >= 100).length; // roofs left burning (the town's stores went up with them)
    for (const id of inv.ids) { const m = monsters.get(id); if (m && m.def.siege) { monsters.delete(id); inv.ids.delete(id); } } // the ram is hauled off as firewood
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
      broadcast({ t: "sys", msg: `${inv.town.name} holds! ${n} ${n === 1 ? "defender" : "defenders"} drove off the invaders. There's a feast in the square tonight!` });
      log(`Invasion of ${inv.town.name} beaten off by ${n}`);
      const best = [...inv.defenders.entries()].sort((a, b) => b[1] - a[1]).map(([name]) => name);
      record(inv.town.name, "h", inv.gate.name, best);
      if (inv.lostFires >= 2) { shiftProsperity(inv.town.name, -1); saveChronicle(); } // the stores went up with the roofs
      feasts.set(inv.town.name, { town: inv.town, until: now() + FEAST_S, eaten: new Set() });
      repairs.set(inv.town.name, { town: inv.town, gate: inv.gate, until: now() + REPAIR_S });
    } else {
      for (const id of inv.ids) monsters.delete(id); // they withdraw with their spoils
      inv.ids.clear();
      broadcast({ t: "sys", msg: `The invaders broke through the ${inv.gate.name} gate of ${inv.town.name}, plundered the market and set the ${inv.gate.name} quarter on fire! Its merchants have fled until the fires are out.` });
      log(`Invasion of ${inv.town.name} succeeded`);
      sack(inv.town, inv.gate);
      record(inv.town.name, "f", inv.gate.name, null);
      // (the carpenters come to the broken gate once the fires are out: see mend())
      if (CAPTIVE_S > 0) takeCaptives(inv);
    }
    if (inv.fallen) graves.push({ town: inv.town, gate: inv.gate, n: Math.min(12, inv.fallen), seed: inv.id, until: now() + GRAVES_S });
    sendAfter();
    send();
    nextAt = minutes > 0 ? now() + minutes * 60 * rand(0.75, 1.25) : Infinity;
  }

  /**
   * Is anyone about to defend o.town (o = the scouted raid or the siege)? Nobody: it stands still (o.paused, shown to
   * players) and the time is added to its clocks; after ABANDON_S of nobody it's given up. Returns "on", "paused" or "gone".
   */
  function attended(o, dt, shift) {
    const nobody = near(o.town).length === 0;
    if (nobody !== !!o.paused) {
      o.paused = nobody;
      o.empty = 0;
      log(`${nobody ? "Nobody near" : "Heroes back at"} ${o.town.name}: the raid ${nobody ? "waits" : "goes on"}`);
      send();
    }
    if (!nobody) return "on";
    o.empty += dt;
    shift(dt);
    return o.empty >= ABANDON_S ? "gone" : "paused";
  }

  function tick(t) {
    const dt = lastTick ? Math.min(1, t - lastTick) : 0;
    lastTick = t;
    let burntOut = false;
    for (const [name, k] of sacks) if (t >= k.until) { sacks.delete(name); burntOut = true; mend(k); broadcast({ t: "sys", msg: `The fires in ${name} are out. Its merchants are back at their stalls; carpenters set to work on the gate.` }); }
    if (burntOut) sendSacks();
    updateAftermath(t);
    if (!inv) {
      if (t >= nextAt) {
        const why = WARN_S > 0 ? scout() : begin(null);
        if (why) nextAt = t + 5 * 60; // try again in a while
      }
      return;
    }
    if (inv.phase === "won" || inv.phase === "lost") {
      if (t - inv.endedAt > RESULT_S) { inv = null; send(); }
      return;
    }
    const how = attended(inv, dt, (d) => { inv.waveAt += d; inv.began += d; });
    if (how === "gone") {
      for (const id of inv.ids) monsters.delete(id);
      inv.ids.clear();
      const name = inv.town.name;
      broadcast({ t: "sys", msg: inv.phase === "warn" ? `With nobody in ${name} to plunder or fight, the raiders massing outside it break camp and scatter.`
        : `With nobody in ${name} to fight, the raiders outside its ${inv.gate.name} gate lose heart and withdraw. The town is spared.` });
      log(`Invasion of ${name} abandoned: nobody near`);
      metrics.inc({ town: name, result: "abandoned" });
      if (inv.phase !== "warn") record(name, "sp", inv.gate.name, null);
      inv = null;
      send();
      nextAt = minutes > 0 ? t + minutes * 60 * rand(0.75, 1.25) : Infinity;
      return;
    }
    if (how === "paused") return;
    if (inv.phase === "warn") {
      // the camp fills up over the first 60% of the warning, the guards march to their posts
      const want = waveSize(1);
      if (inv.massed < want && t >= inv.nextMass) {
        if (spawnOne(false)) inv.massed++;
        inv.nextMass = t + (WARN_S * 0.6) / want;
      }
      let marching = false;
      for (const g of inv.guards) if (g.k === "s" && march(g)) marching = true;
      if (t >= inv.waveAt) {
        inv.phase = "wave";
        inv.began = t;
        broadcast({ t: "sys", msg: `The war horns sound: the raiders charge the ${inv.gate.name} gate of ${inv.town.name}!` });
        spawnWave();
        send();
      } else if (t - inv.sentAt > (marching ? 0.35 : 2)) send();
      return;
    }
    if (inv.phase === "gather") {
      let marching = false;
      for (const g of inv.guards) if (g.k === "s" && march(g)) marching = true;
      if (t >= inv.waveAt) { inv.phase = "wave"; spawnWave(); send(); }
      else if (t - inv.sentAt > (marching ? 0.35 : 2)) send();
      return;
    }
    updateGuards(t);
    inv.guards = inv.guards.filter((g) => g.hp > 0);
    const firesChanged = updateFires(t, dt);
    if (inv.bearer && !inv.routed && !monsters.has(inv.bearer)) rout();
    if (inv.warlord && t >= inv.nextTaunt) {
      inv.nextTaunt = t + rand(14, 22);
      const lord = monsters.get(inv.warlord);
      if (lord) sayNear(lord, WARLORD_TAUNTS[Math.floor(rand(1, WARLORD_TAUNTS.length))]);
    }
    if (firesChanged) send();
    // Battering the gate
    for (const id of inv.ids) {
      const m = monsters.get(id);
      if (m && m.sieging && m.state === "idle") inv.integrity -= SIEGE_RATE * (m.def.siege ? RAM_HIT : m.warlord ? 4 : m.def.hp >= 200 ? 2 : 1) * 0.1;
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
    if (!inv || m.invasion !== inv.id) return false;
    if (m.def.siege) return ramIdle(m, t);
    if (m.fleeing) { // routed: back to the camp, and away
      if (dist(m.x, m.z, inv.sx, inv.sz) < 4) { monsters.delete(m.id); inv.ids.delete(m.id); return true; }
      if (!m.path.length) m.path = findPath(m.x, m.z, inv.sx, inv.sz, 8000);
      if (!m.path.length) { monsters.delete(m.id); inv.ids.delete(m.id); return true; }
      moveAlongPath(m, speedOf(m) * 1.2);
      return true;
    }
    if (inv.phase !== "wave" && inv.phase !== "warn") return false;
    if (inv.paused) { m.path = []; m.sieging = false; return true; } // nobody about: they wait where they are
    if (m.crew && inv.phase === "wave") { // walk beside the ram, pushing
      const ram = ramOf();
      if (ram && !inv.ramStopped && dist(m.x, m.z, ram.x, ram.z) > 1.8) { m.path = [[ram.x + rand(-1, 1), ram.z + rand(-1, 1)]]; moveAlongPath(m, speedOf(m) * 0.8); return true; }
      if (ram && !inv.ramStopped) { m.path = []; m.ry = ram.ry; return true; }
    }
    if (inv.phase === "warn") { // in the war camp, waiting for the horns
      if (dist(m.x, m.z, inv.sx, inv.sz) > 7) {
        if (!m.path.length) m.path = findPath(m.x, m.z, inv.sx + rand(-3, 3), inv.sz + rand(-3, 3), 4000);
        moveAlongPath(m, speedOf(m) * 0.6);
      } else m.path = [];
      return true;
    }
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

  /** The banner falls: the raiders lose heart. Some run for the camp, the rest fight on, weaker. */
  function rout() {
    inv.routed = true;
    let fled = 0;
    for (const id of inv.ids) {
      const m = monsters.get(id);
      if (!m || m.def.siege || m.warlord || m.hp <= 0) continue;
      if (Math.random() < ROUT_SHARE) {
        m.fleeing = true; m.state = "idle"; m.target = 0; m.threat.clear(); m.path = []; m.sieging = false;
        fled++;
      } else m.dmg *= ROUT_DMG;
    }
    broadcast({ t: "sys", msg: `The raiders' banner falls before ${inv.town.name}! ${fled ? `${fled} of them turn and run; the rest` : "They"} waver.` });
    guardEvent({ k: "rout" });
    log(`Invasion of ${inv.town.name}: the banner fell, ${fled} fled`);
    send();
  }

  function onDamage(m, s) {
    if (m.captor) { if (captives && s && s.name) captives.helpers.add(s.name); return; }
    if (inv && m.invasion === inv.id && s && s.name) inv.defenders.set(s.name, (inv.defenders.get(s.name) || 0) + 1);
    m.sieging = false;
  }

  return {
    tick, idle, onDamage,
    /** At start-up, once the store is open: the towns' siege record. */
    async load() {
      if (!store) return;
      try { chronicle = JSON.parse((await store.getMeta("siege_chronicle")) || "{}") || {}; } catch { chronicle = {}; }
    },
    sendTo(s) {
      if (inv) safeSend(s, JSON.stringify({ t: "invasion", iv: state() }));
      if (sacks.size) safeSend(s, JSON.stringify(sackState()));
      safeSend(s, JSON.stringify(chronicleState()));
      if (feasts.size || repairs.size || graves.length || captives) safeSend(s, JSON.stringify(afterState()));
    },
    /** A hero eats at a victory feast's table (they must be by it, and only once a feast). Returns why not, or "". */
    eat(s) {
      for (const f of feasts.values()) {
        const c = centre(f.town);
        if (dist(s.x, s.z, c.x, c.z) > 14) continue;
        if (f.eaten.has(s.name)) return "You've eaten your fill at this feast already.";
        f.eaten.add(s.name);
        safeSend(s, JSON.stringify({ t: "fed", k: f.town.name, s: FEAST_BUFF_S, mul: FEAST_XP }));
        return "";
      }
      return "There's no feast here.";
    },
    /** The merchants' price factor where a hero stands (the town's prosperity): 1 outside the walled towns. */
    priceMul(x, z) {
      const t = walled.find((w) => inside(w, x, z));
      return t ? Math.round((1 - PRICE_STEP * prosperity(t.name)) * 100) / 100 : 1;
    },
    /** Tests: the chronicle and aftermath as players get them. */
    _chronicle: () => chronicleState(),
    /** In a burning quarter (its merchants, smiths and auctioneers have fled): their trade is refused there. */
    sackedAt(x, z) {
      for (const k of sacks.values())
        if (inside(k.town, x, z) && dist(x, z, k.gate.x, k.gate.z) < sackRadius(k.town)) return k.town.name;
      return "";
    },
    /** A hero lights beacon i by the gate (within reach of it, while the town is threatened). Returns why not, or "". */
    light(s, i) {
      if (!inv || inv.phase === "won" || inv.phase === "lost") return "There's nothing to light a beacon for.";
      const b = inv.beacons[i | 0];
      if (!b) return "";
      if (b.lit) return "That beacon is already burning.";
      if (dist(s.x, s.z, b.x, b.z) > 4.5) return "Get closer to the beacon.";
      b.lit = s.name;
      inv.defenders.set(s.name, inv.defenders.get(s.name) || 0); // a part in the defence
      broadcast({ t: "sys", msg: `${s.name} lit a beacon at the ${inv.gate.name} gate of ${inv.town.name}: the wall's archers loose fire arrows!` });
      guardEvent({ k: "beacon", i: i | 0, by: s.name });
      send();
      return "";
    },
    /** A hero throws water on burning roof `id` (they must be near it). Returns the xp earned (0: nothing done). */
    douseFire(s, id) {
      if (!inv) return 0;
      const f = inv.fires.find((x) => x.id === (id | 0));
      if (!f || dist(s.x, s.z, f.x, f.z) > 14) return 0;
      const t = now();
      if (t - (s.lastDouse || 0) < 1.2) return 0;
      s.lastDouse = t;
      f.s -= DOUSE;
      if (f.s <= 0) { inv.fires = inv.fires.filter((x) => x !== f); guardEvent({ k: "fireout", id: f.id, by: s.name }); }
      send();
      return Math.max(1, Math.round(3 * Math.pow(s.lvl || 1, 1.2)));
    },
    /** The burning town a hero stands in (anywhere inside its walls), or null. */
    sackOf(x, z) {
      for (const k of sacks.values()) if (inside(k.town, x, z)) return k;
      return null;
    },
    /** A delivery for the rebuilding: the fires burn `seconds` shorter; out early, everyone hears who helped. True if out. */
    douse(k, seconds, who) {
      k.until -= seconds;
      k.helpers = k.helpers || new Map();
      k.helpers.set(who, (k.helpers.get(who) || 0) + 1);
      if (k.until > now()) { sendSacks(); return false; }
      sacks.delete(k.town.name);
      mend(k);
      const top = [...k.helpers.entries()].sort((a, b) => b[1] - a[1]).slice(0, 5).map(([n]) => n);
      broadcast({ t: "sys", msg: `The fires in ${k.town.name} are out early, thanks to ${top.join(", ")}${k.helpers.size > top.length ? " and others" : ""}. Its merchants are back at their stalls.` });
      sendSacks();
      log(`The fires in ${k.town.name} were put out early by ${k.helpers.size} helpers`);
      return true;
    },
    /** Admin: set the quarter behind a gate on fire now (the end of a lost siege), or put every fire out. */
    sack(townName, gateName) {
      if (townName === "stop") { const n = sacks.size; sacks.clear(); sendSacks(); return n ? "The fires are out." : "Nothing is burning."; }
      const t = townName ? walled.find((w) => w.name.toLowerCase().startsWith(String(townName).toLowerCase())) : walled[0];
      if (!t) return `No walled town called "${townName}" (${walled.map((w) => w.name).join(", ")}).`;
      const g = gates(t).find((x) => x.name === (gateName || "south")) || gates(t)[0];
      sack(t, g);
      return `The ${g.name} quarter of ${t.name} burns for ${Math.round(SACK_S / 60)} minutes.`;
    },
    active: () => (inv && inv.phase !== "won" && inv.phase !== "lost" ? inv.town.name : ""),
    /** Admin: start one now (at the named town, or the busiest), or end the current one. */
    start(townName, gateName, warn) {
      if (inv && inv.phase !== "won" && inv.phase !== "lost") return `${inv.town.name} is already under attack.`;
      inv = null;
      const t = townName ? walled.find((w) => w.name.toLowerCase().startsWith(String(townName).toLowerCase())) : null;
      if (townName && !t) return `No walled town called "${townName}" (${walled.map((w) => w.name).join(", ")}).`;
      if (warn) { // as the server does by itself: scouts' warning first
        const why = scout(t || walled[0], gateName);
        return why ? `No invasion: ${why}.` : `Scouts sight raiders outside the ${inv.gate.name} gate of ${inv.town.name}; they attack in ${Math.round(WARN_S)} s.`;
      }
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
    /** Admin: a fire arrow onto a roof behind the gate now (as if a raider archer loosed one). */
    fireNow() {
      if (!inv || inv.phase === "won" || inv.phase === "lost") return "There is no siege.";
      const g = inv.gate, x = g.x - g.nx * 12, z = g.z - g.nz * 12;
      startFire(x, z, 50, null);
      return `A roof behind the ${g.name} gate of ${inv.town.name} is on fire.`;
    },
  };
};
