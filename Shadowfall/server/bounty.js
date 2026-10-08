// Daily bounties: three tasks a day per hero, chosen for their level (the same three all day, however often they log
// in: seeded by character and date). Kills are counted here as the server credits them. A finished bounty pays gold,
// experience and an item at once; all three finished pay a Bounty Cache on top. Kept in the ledger (ledger.bounties),
// so they survive logging out; a new day (UTC) brings new ones.
//
// The client gets "bounties" {items: ["text|have|need|done|kind|target"]} at login and on progress, and "bounty" {k: text, xp,
// drops, n: 1 when all three are done} when one is finished.

module.exports = function createBounties(ctx) {
  const { MONSTERS, SPAWNERS, DUNGEONS, safeSend, give, dropFor, rollChest, randomEquipment, heroClass, ledgerChanged, sys, log } = ctx;

  const day = () => new Date().toISOString().slice(0, 10);

  /** A small deterministic generator: the same character on the same day gets the same bounties. */
  function seeded(text) {
    let h = 2166136261;
    for (let i = 0; i < text.length; i++) { h ^= text.charCodeAt(i); h = Math.imul(h, 16777619) >>> 0; }
    return () => { h ^= h << 13; h >>>= 0; h ^= h >> 17; h ^= h << 5; h >>>= 0; return h / 4294967296; };
  }

  /** Monsters that live where a hero of this level hunts (from the spawners), bosses aside. */
  function huntable(level) {
    const out = new Set();
    for (const [, , , minL, maxL, types] of SPAWNERS)
      if (minL <= level + 3 && maxL >= level - 4) for (const t of types) if (MONSTERS[t] && !MONSTERS[t].boss) out.add(t);
    if (!out.size) for (const [, , , , , types] of SPAWNERS) for (const t of types) if (MONSTERS[t] && !MONSTERS[t].boss) out.add(t);
    return [...out];
  }

  function roll(s) {
    const lvl = s.lvl || 1, rnd = seeded(`${s.name.toLowerCase()}|${day()}`);
    const pick = (arr) => arr[Math.floor(rnd() * arr.length)];
    const types = huntable(lvl);
    const list = [];
    const t1 = pick(types);
    list.push({ kind: "kill", target: t1, need: 15 + Math.floor(rnd() * 16), have: 0 });
    const t2 = types.length > 1 ? pick(types.filter((t) => t !== t1)) : t1;
    if (rnd() < 0.5) list.push({ kind: "kill", target: t2, need: 10 + Math.floor(rnd() * 11), have: 0 });
    else list.push({ kind: "elite", target: "", need: 2 + Math.floor(rnd() * 3), have: 0 });
    const dungeons = DUNGEONS.filter((d) => d.minLevel <= lvl + 2);
    if (dungeons.length && rnd() < 0.6) list.push({ kind: "boss", target: pick(dungeons).boss, need: 1, have: 0 });
    else list.push({ kind: "any", target: "", need: 60 + Math.floor(rnd() * 41), have: 0 });
    return { day: day(), level: lvl, list, cache: false };
  }

  const text = (b) => b.kind === "kill" ? `Slay ${b.need} ${b.target}${b.target.endsWith("s") ? "" : "s"}`
    : b.kind === "elite" ? `Slay ${b.need} elite champions`
    : b.kind === "boss" ? `Defeat ${b.target}`
    : `Slay ${b.need} monsters`;

  /** Today's bounties (new ones on a new day). */
  function today(s) {
    if (!s.ledger) return null;
    const b = s.ledger.bounties;
    if (!b || b.day !== day() || !Array.isArray(b.list)) { s.ledger.bounties = roll(s); ledgerChanged(s); }
    return s.ledger.bounties;
  }

  function send(s) {
    const b = today(s);
    if (b) safeSend(s, JSON.stringify({ t: "bounties", items: b.list.map((x) => `${text(x)}|${Math.min(x.have, x.need)}|${x.need}|${x.have >= x.need ? 1 : 0}|${x.kind}|${x.target}`) }));
  }

  function reward(s, b, x) {
    const lvl = s.lvl || 1;
    const gold = 30 + lvl * 25;
    const xp = Math.round(4 * Math.pow(lvl, 1.55) + 40);
    s.ledger.gold += gold;
    const items = [randomEquipment(lvl + 1, 0.6, null, null, heroClass(s))];
    let drops = give(s, items);
    let all = false;
    if (!b.cache && b.list.every((y) => y.have >= y.need)) {
      b.cache = true;
      all = true;
      drops = drops.concat(dropFor(s, s.x, s.z, rollChest(lvl + 2, heroClass(s), 0.5), s.inst || 0));
    }
    safeSend(s, JSON.stringify({ t: "bounty", k: text(x), xp: all ? xp * 2 : xp, gold, drops, n: all ? 1 : 0 }));
    if (all) sys(s, "All of today's bounties done: a Bounty Cache lies at your feet. New bounties tomorrow.");
    ledgerChanged(s);
  }

  /** The server credited s with a kill of m. */
  function onKill(s, m) {
    const b = today(s);
    if (!b) return;
    let changed = false;
    for (const x of b.list) {
      if (x.have >= x.need) continue;
      const counts = (x.kind === "kill" && m.type === x.target) || (x.kind === "elite" && m.elite) ||
        (x.kind === "boss" && m.type === x.target && m.inst) || x.kind === "any";
      if (!counts) continue;
      x.have++;
      changed = true;
      if (x.have >= x.need) reward(s, b, x);
    }
    if (changed) send(s);
  }

  return { send, onKill, roll: (s) => { if (s.ledger) s.ledger.bounties = null; return today(s); } };
};
