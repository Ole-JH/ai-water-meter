// World bosses. Every WORLD_BOSS_MINUTES or so (default 90, 0 = never) one of four giants rises at its lair, preferably
// the one whose level suits the heroes online, and everyone is told where. It is a fight for many: it grows tougher
// with every hero who joins in, telegraphs a ground slam (a red ring: step out), calls for help at two thirds and one
// third of its health, and rages at a quarter. It wears three armour plates (the client shows them): one breaks off at
// three quarters, a half and a quarter of its health, each time taking some of its armour and quickening its slams. Everyone who hurt it gets the kill (better loot than a dungeon boss).
// Left alone for WORLD_BOSS_STAY minutes it goes back to sleep.
//
// The server calls: tick(t) every simulation tick, abilities(m, s, d, t) while it fights, onDamage(m, s) when a hero
// hurts it, sendTo(s) when a hero enters the world, and start()/stop() for the admin command.

const STAY_S = Number(process.env.WORLD_BOSS_STAY ?? 25) * 60;
const SLAM_EVERY = Number(process.env.WORLD_BOSS_SLAM_S ?? 10), SLAM_WINDUP = 1.5, SLAM_RADIUS = 6; // WorldBoss.cs has these too
const JOIN_HP = 0.35; // each hero after the first adds this much of its starting health

module.exports = function createWorldBosses(ctx) {
  const { map, monsters, sessions, spawnMonster, nearestWalkable, walkable, aggro, monsterAttack, sendNear, broadcast, safeSend,
    log, now, rand, dist, r2 } = ctx;

  const LAIRS = [
    { name: "Old Bramblehide", region: "Whisperwood", x: map(70), z: map(140), level: 10, adds: ["Dire Wolf"] },
    { name: "Hrimgar the Mountain", region: "the Frostpeak Wilds", x: 235, z: 450, level: 19, adds: ["Frost Wolf", "Ice Wraith"] },
    { name: "Gorvash the Dune Reaver", region: "the Sunscar Badlands", x: 380, z: 262, level: 20, adds: ["Desert Raider", "Raider Marksman"] },
    { name: "The Pyre Colossus", region: "the Ashen Reach", x: 420, z: 345, level: 24, adds: ["Ember Skeleton", "Ash Wraith"] },
  ];

  const minutes = Number(process.env.WORLD_BOSS_MINUTES ?? 90);
  let boss = null; // { lair, id, began, sentAt }
  let nextAt = minutes > 0 ? now() + rand(0.25, 0.5) * minutes * 60 : Infinity;

  const overworld = () => [...sessions.values()].filter((s) => s.inWorld && !s.inst);

  /** The lair whose boss suits the heroes online best (or a random one). */
  function pickLair() {
    const players = overworld();
    if (!players.length) return null;
    const avg = players.reduce((a, s) => a + (s.lvl || 1), 0) / players.length;
    const fitting = LAIRS.filter((l) => Math.abs(l.level - avg) <= 7);
    const pool = fitting.length ? fitting : LAIRS;
    return pool[Math.floor(Math.random() * pool.length)];
  }

  function rise(lair) {
    const spot = nearestWalkable(Math.floor(lair.x), Math.floor(lair.z), 8);
    if (!spot) return `no room at ${lair.name}'s lair`;
    const m = spawnMonster(lair.name, lair.level, spot[0] + 0.5, spot[1] + 0.5, null);
    m.worldBoss = lair;
    m.leash = 45;
    m.lootBonus = 0.6;
    m.baseHp = m.maxHp;
    m.baseDmg = m.dmg;
    m.attackers = new Set();
    m.baseArmor = m.armor;
    m.slamAt = 0; m.nextSlam = 0; m.addsCalled = 0; m.enraged = false;
    m.plates = 3; m.slamEvery = SLAM_EVERY;
    boss = { lair, id: m.id, began: now(), sentAt: 0 };
    broadcast({ t: "sys", msg: `${lair.name} has risen in ${lair.region}! Gather your allies: it is marked on your map.` });
    log(`World boss ${lair.name} rose at ${Math.round(m.x)}, ${Math.round(m.z)}`);
    send();
    return null;
  }

  function state() {
    const m = boss && monsters.get(boss.id);
    if (!m) return { phase: "none" };
    return { phase: "up", name: boss.lair.name, region: boss.lair.region, x: r2(m.x), z: r2(m.z), l: m.level,
      hp: Math.max(1, Math.round((m.hp / m.maxHp) * 100)), n: m.attackers.size,
      age: Math.round(now() - boss.began), pl: m.plates }; // age: seconds since it rose (the client plays its entrance when new)
  }

  function send() {
    if (boss) boss.sentAt = now();
    const data = JSON.stringify({ t: "wboss", wb: state() });
    for (const s of sessions.values()) if (s.inWorld) safeSend(s, data);
  }

  function end(why) {
    boss = null;
    if (why) broadcast({ t: "sys", msg: why });
    send();
    nextAt = minutes > 0 ? now() + minutes * 60 * rand(0.75, 1.25) : Infinity;
  }

  function tick(t) {
    if (!boss) {
      if (t >= nextAt) {
        const lair = pickLair();
        if (!lair || rise(lair)) nextAt = t + 5 * 60;
      }
      return;
    }
    const m = monsters.get(boss.id);
    if (!m) return end(null); // slain: killMonster announced it
    if (m.state === "idle" && m.attackers.size === 0 && t - boss.began > STAY_S) {
      monsters.delete(m.id);
      return end(`${boss.lair.name} returns to its slumber, unchallenged.`);
    }
    const frac = m.hp / m.maxHp;
    const plates = frac < 0.25 ? 0 : frac < 0.5 ? 1 : frac < 0.75 ? 2 : 3;
    if (plates < m.plates) { // an armour plate breaks off
      m.plates = plates;
      m.armor = Math.round(m.armor * 0.7);
      m.slamEvery *= 0.85;
      monsterAttack(m, null, "phase", plates); // dmg carries the plates left
      return send();
    }
    if (t - boss.sentAt > 3) send();
  }

  /** While it fights: the telegraphed slam, the adds, the rage. */
  function abilities(m, s, d, t) {
    if (m.slamAt) {
      m.path = [];
      if (t < m.slamAt) return true; // winding up: stands still, doesn't swing
      monsterAttack(m, null, "slam", m.dmg * 1.8, m.slamX, m.slamZ);
      m.slamAt = 0;
      m.nextAttack = t + 0.6;
      return true;
    }
    if (!m.nextSlam) m.nextSlam = t + m.slamEvery * 0.6;
    if (t >= m.nextSlam && d < SLAM_RADIUS + 2) {
      m.nextSlam = t + m.slamEvery * rand(0.85, 1.15);
      m.slamAt = t + SLAM_WINDUP;
      m.slamX = m.x; m.slamZ = m.z;
      monsterAttack(m, null, "warn", 0, m.x, m.z); // the red ring
      return true;
    }
    const frac = m.hp / m.maxHp;
    if ((m.addsCalled === 0 && frac < 0.66) || (m.addsCalled === 1 && frac < 0.33)) {
      m.addsCalled++;
      monsterAttack(m, null, "summon", 0);
      for (let i = 0; i < 3 + m.attackers.size; i++) {
        const a = rand(0, Math.PI * 2), x = m.x + Math.cos(a) * 3, z = m.z + Math.sin(a) * 3;
        if (!walkable(x, z)) continue;
        const types = m.worldBoss.adds;
        const add = spawnMonster(types[i % types.length], Math.max(1, m.level - 2), x, z, null);
        add.leash = 40;
        aggro(add, s.id);
      }
    }
    if (!m.enraged && frac < 0.25) {
      m.enraged = true;
      m.dmg *= 1.4;
      m.speedMul = (m.speedMul || 1) * 1.25;
      sendNear(m.x, m.z, 60, { t: "sys", msg: `${m.type} becomes enraged!` });
    }
    return false;
  }

  /** Back at its lair with nobody left to fight: as it rose. */
  function reset(m) {
    m.maxHp = m.hp = m.baseHp;
    m.dmg = m.baseDmg;
    m.speedMul = 1;
    m.attackers.clear();
    m.addsCalled = 0; m.enraged = false; m.slamAt = 0; m.nextSlam = 0;
    m.plates = 3; m.slamEvery = SLAM_EVERY; m.armor = m.baseArmor;
  }

  function onDamage(m, s) {
    if (!m.attackers || !s || m.attackers.has(s.id)) return;
    m.attackers.add(s.id);
    if (m.attackers.size > 1) { // tougher with every hero who joins
      const more = Math.round(m.baseHp * JOIN_HP);
      m.maxHp += more;
      m.hp += more;
    }
    send(); // the trackers show who joined
  }

  return {
    tick, abilities, onDamage, reset, LAIRS,
    sendTo(s) { if (boss) safeSend(s, JSON.stringify({ t: "wboss", wb: state() })); },
    active: () => (boss ? boss.lair.name : ""),
    /** Admin: raise one now (by the start of its name, or the best fit), or put the current one back to sleep. */
    start(name) {
      if (boss) return `${boss.lair.name} is already up.`;
      const lair = name ? LAIRS.find((l) => l.name.toLowerCase().includes(String(name).toLowerCase())) : pickLair() || LAIRS[0];
      if (!lair) return `No world boss called "${name}" (${LAIRS.map((l) => l.name).join(", ")}).`;
      const why = rise(lair);
      return why ? `No world boss: ${why}.` : `${lair.name} rises in ${lair.region}.`;
    },
    stop() {
      if (!boss) return "There is no world boss up.";
      const name = boss.lair.name;
      monsters.delete(boss.id);
      end(`${name} returns to its slumber.`);
      return `${name} is gone.`;
    },
  };
};
