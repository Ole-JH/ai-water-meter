// Greater rifts: a timed, single-level dungeon at a tier the players choose, opened at the Rift Stone in Hollowmere.
// Monsters from three random dungeons, tougher with every tier. Killing them fills the progress bar; at 100% the Rift
// Guardian (a dungeon boss) appears next to whoever got it there. Beat it within RIFT_MINUTES and everyone inside
// unlocks the next tier (ledger.riftBest), gets a treasure chest's worth of loot and goes on the leaderboard (stored
// with store.setMeta, the best run per tier and party). Too slow: the rift still gives its monsters' loot, nothing more.
//
// Messages: rinfo (-> rinfo {k: best, items: leaderboard lines "tier|seconds|names"}), ropen {n: tier}.
// While inside: rift {tier, pct, left (seconds), k: phase run|guardian|won|late}.

const MINUTES = Number(process.env.RIFT_MINUTES ?? 10);
const STONE = { x: 149.5, z: 149.5 }; // WorldGenerator / RiftStone.cs: where the old well stood, north-east of the square
const BOARD_SIZE = 20;
const GUARDIANS = ["Crypt Lord", "Goblin King", "Stone Colossus", "Bandit Lord", "The Frost Witch", "The Sand Colossus", "The Cinder Lord"];

module.exports = function createRifts(ctx) {
  const { store, DUNGEONS, dungeonGen, instances, closeInstance, nextInstanceId, monsters, sessions, spawnMonster, makeElite, walkable, useGrid,
    partyOf, partyMembers, partyKey, enterInstance, safeSend, sys, sendNear, broadcast, dropFor, rollChest, heroClass, ledgerChanged,
    log, now, rand, randInt, dist, metrics } = ctx;
  let board = []; // [{ tier, secs, names, at }]

  async function load() {
    try { board = JSON.parse((await store.getMeta("rift_board")) || "[]"); } catch { board = []; }
  }
  const saveBoard = () => store.setMeta("rift_board", JSON.stringify(board)).catch((e) => log(`rift board save failed: ${e.message}`));

  const send = (s, msg) => safeSend(s, JSON.stringify(msg));
  const inside = (inst) => [...sessions.values()].filter((o) => o.inWorld && o.inst === inst.id);

  function info(s) {
    send(s, { t: "rinfo", k: String(s.ledger ? s.ledger.riftBest : 0), items: board.map((b) => `${b.tier}|${b.secs}|${b.names.join(", ")}`) });
  }

  /** Opens a rift for s and the party members at the stone (or joins the party's rift that is already open). */
  function open(s, tier) {
    if (!s.ledger || s.inst || s.dead) return;
    if (dist(s.x, s.z, STONE.x, STONE.z) > 6) return sys(s, "Stand at the Rift Stone in Hollowmere to open a rift.");
    const key = partyKey(s) + ":rift";
    for (const inst of [...instances.values()]) {
      if (inst.key !== key || !inst.rift) continue;
      if (inst.rift.phase !== "won" && inside(inst).length) return enter(s, inst); // the party is in there: join them
      closeInstance(inst); // finished or empty: a fresh one
    }
    const best = s.ledger.riftBest || 0;
    tier = Math.max(1, Math.min(best + 1, tier | 0));
    const p = partyOf(s);
    const near = (p ? partyMembers(p) : [s]).filter((o) => !o.inst && !o.dead && dist(o.x, o.z, STONE.x, STONE.z) < 15);
    if (!near.includes(s)) near.push(s);
    const level = Math.max(3, Math.round(near.reduce((a, o) => a + (o.lvl || 1), 0) / near.length));
    const themes = [...DUNGEONS.keys()].sort(() => Math.random() - 0.5).slice(0, 3);
    const types = [...new Set(themes.flatMap((d) => DUNGEONS[d].types[DUNGEONS[d].types.length - 1]))];
    const dIdx = themes[0], seed = (Math.random() * 2147483647) | 0;
    const L = DUNGEONS[dIdx].style === "caves" ? dungeonGen.generateCaves(seed, 3, 3) : dungeonGen.generate(seed, 3, 3);
    const inst = {
      id: nextInstanceId(), key, dIdx, depth: 1, depths: 1, df: 0, seed, layout: { ...L, stairs: null }, grid: { w: L.w, h: L.h, blocked: L.blocked },
      cells: dungeonGen.pack(L.blocked), lastActive: now(), level, name: `Greater Rift  -  Tier ${tier}`,
      rift: { tier, began: now(), deadline: now() + MINUTES * 60, progress: 0, weight: 0, phase: "run", guardian: 0, sentAt: 0, players: near.length },
    };
    instances.set(inst.id, inst);
    populate(inst, types);
    log(`Rift ${inst.id} tier ${tier} (level ${level}) opened by ${s.name} for ${near.length}`);
    metrics.inc({ tier: String(Math.min(tier, 20)) });
    for (const o of near) enter(o, inst);
  }

  function enter(s, inst) {
    enterInstance(s, inst);
    sendState(inst, [s]);
  }

  /** Tier scaling: health grows 17% a tier, damage 10%; experience and loot follow. */
  function harden(m, tier) {
    m.maxHp = m.hp = Math.round(m.hp * Math.pow(1.17, tier - 1));
    m.dmg *= Math.pow(1.1, tier - 1);
    m.xpMul = 1 + 0.15 * tier;
    m.lootBonus = Math.min(1.2, 0.08 * tier);
    m.leash = 80;
    return m;
  }

  function populate(inst, types) {
    useGrid(inst.id);
    const r = inst.rift, L = inst.layout;
    for (const pk of L.packs) {
      const n = pk.n + Math.max(0, r.players - 1) + 1;
      for (let i = 0; i < n; i++) {
        for (let a = 0; a < 12; a++) {
          const x = pk.room.x + 1 + Math.random() * (pk.room.w - 2), z = pk.room.y + 1 + Math.random() * (pk.room.h - 2);
          if (!walkable(x, z)) continue;
          const m = spawnMonster(types[randInt(0, types.length - 1)], inst.level + randInt(-1, 1), x, z, null, inst.id);
          if (Math.random() < 0.08 + Math.min(0.2, r.tier * 0.01)) makeElite(m);
          harden(m, r.tier);
          r.weight += m.elite ? 4 : 1;
          break;
        }
      }
    }
    useGrid(0);
    r.per = 100 / Math.max(1, r.weight * 0.7); // about 70% of the monsters fill the bar
  }

  function sendState(inst, to) {
    const r = inst.rift;
    const msg = JSON.stringify({ t: "rift", n: r.tier, i: Math.min(100, Math.floor(r.progress)), left: Math.max(0, Math.round(r.deadline - now())), k: r.phase });
    for (const o of to || inside(inst)) safeSend(o, msg);
    r.sentAt = now();
  }

  /** A monster died in a rift: progress, or the end. */
  function onKill(m, killer) {
    const inst = instances.get(m.inst);
    if (!inst || !inst.rift) return;
    const r = inst.rift;
    if (m.id === r.guardian) return complete(inst);
    if (r.phase !== "run" && r.phase !== "late") return;
    r.progress += (m.elite ? 4 : 1) * r.per;
    if (r.progress >= 100 && !r.guardian) summonGuardian(inst, m, killer);
    sendState(inst);
  }

  function summonGuardian(inst, at, killer) {
    const r = inst.rift;
    useGrid(inst.id);
    let x = at.x, z = at.z;
    if (killer && killer.inst === inst.id) { x = killer.x + rand(-3, 3); z = killer.z + rand(-3, 3); }
    if (!walkable(x, z)) { x = at.x; z = at.z; }
    const g = spawnMonster(GUARDIANS[randInt(0, GUARDIANS.length - 1)], inst.level + 3, x, z, null, inst.id);
    g.maxHp = g.hp = Math.round(g.hp * (0.7 + 0.3 * inside(inst).length) * 1.3);
    harden(g, r.tier);
    useGrid(0);
    r.guardian = g.id;
    if (r.phase === "run") r.phase = "guardian";
    sendNear(g.x, g.z, 999, { t: "sys", msg: `The Rift Guardian, ${g.type}, has come!` }, inst.id);
  }

  function complete(inst) {
    const r = inst.rift, secs = Math.round(now() - r.began), inTime = r.phase === "guardian";
    const heroes = inside(inst);
    r.phase = inTime ? "won" : "late";
    if (inTime) {
      for (const s of heroes) {
        if (!s.ledger) continue;
        const up = r.tier > (s.ledger.riftBest || 0);
        if (up) s.ledger.riftBest = r.tier;
        const drops = dropFor(s, s.x, s.z, rollChest(inst.level + 2, heroClass(s), Math.min(1.5, 0.1 * r.tier)), inst.id);
        safeSend(s, JSON.stringify({ t: "drops", drops }));
        sys(s, `Rift tier ${r.tier} cleared in ${Math.floor(secs / 60)}:${String(secs % 60).padStart(2, "0")}!` + (up ? ` Tier ${r.tier + 1} is open to you.` : ""));
        ledgerChanged(s);
      }
      const names = heroes.map((s) => s.name).sort();
      board = board.filter((b) => !(b.tier === r.tier && b.names.join() === names.join() && b.secs <= secs));
      if (!board.some((b) => b.tier === r.tier && b.names.join() === names.join())) board.push({ tier: r.tier, secs, names, at: new Date().toISOString() });
      board.sort((a, b) => b.tier - a.tier || a.secs - b.secs);
      board = board.slice(0, BOARD_SIZE);
      saveBoard();
      const entry = board.find((b) => b.tier === r.tier && b.names.join() === names.join());
      if (entry && entry.secs === secs && board.indexOf(entry) < 3)
        broadcast({ t: "sys", msg: `${names.join(", ")} cleared a tier ${r.tier} rift in ${Math.floor(secs / 60)}:${String(secs % 60).padStart(2, "0")}, number ${board.indexOf(entry) + 1} on the leaderboard!` });
      log(`Rift ${inst.id} tier ${r.tier} cleared in ${secs}s by ${names.join(", ")}`);
    } else {
      for (const s of heroes) sys(s, "The Rift Guardian falls, but too late: no new tier this time.");
    }
    sendState(inst);
  }

  function tick(t) {
    for (const inst of instances.values()) {
      const r = inst.rift;
      if (!r) continue;
      if ((r.phase === "run" || r.phase === "guardian") && t > r.deadline) {
        r.phase = "late";
        for (const s of inside(inst)) sys(s, "Time's up! The rift still holds its loot, but this run won't unlock a new tier.");
        sendState(inst);
      } else if (t - r.sentAt > 1) sendState(inst);
    }
  }

  return { load, info, open, onKill, tick, STONE, board: () => board };
};
