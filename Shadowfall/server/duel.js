// Duels: one hero challenges another (the player menu), and if they accept, the two fight each other, and only each
// other, after a three-second countdown, around a flag planted between them. Nobody dies: a duel ends when one of them
// is about to fall (their game yields at the last hit point), dies to something else, leaves, or runs more than
// FLEE metres from the flag; after MAX_S it is a draw. Not in dungeons.
//
// Messages: dreq {id} (challenge), dans {yes} (answer), dhit {id, dmg} (a hit on the opponent, forwarded to them),
// dyield (out of health: we lost). The server sends dreq {id, name}, duel {k: count|fight|end, id, name, x, z,
// win}, dhit {id, dmg}.

const RANGE = 20, FLEE = 45, COUNT_S = 3, MAX_S = 180, INVITE_S = 30;

module.exports = function createDuels(ctx) {
  const { sessions, safeSend, sendNear, sys, dist, now, log, metrics } = ctx;
  const duels = new Map();   // session id -> duel { a, b, x, z, fightAt, began }
  const invites = new Map(); // challenged id -> { from, at }

  const send = (s, msg) => safeSend(s, JSON.stringify(msg));
  const other = (d, s) => (d.a === s ? d.b : d.a);
  const free = (s) => s && s.inWorld && !s.dead && !s.inst && !duels.has(s.id);

  function challenge(s, id) {
    const o = sessions.get(id | 0);
    if (!o || o === s) return;
    if (!free(s)) return sys(s, "You can't start a duel right now.");
    if (!free(o)) return sys(s, `${o.name} can't duel right now.`);
    if (dist(s.x, s.z, o.x, o.z) > RANGE) return sys(s, `${o.name} is too far away to challenge.`);
    invites.set(o.id, { from: s.id, at: now() });
    send(o, { t: "dreq", id: s.id, name: s.name });
    sys(s, `You challenge ${o.name} to a duel.`);
  }

  function answer(o, yes) {
    const inv = invites.get(o.id);
    invites.delete(o.id);
    if (!inv || now() - inv.at > INVITE_S) return;
    const s = sessions.get(inv.from);
    if (!s) return;
    if (!yes) return sys(s, `${o.name} declines your duel.`);
    if (!free(s) || !free(o) || dist(s.x, s.z, o.x, o.z) > RANGE) return sys(o, "The duel can't start: one of you is busy or too far away.");
    const d = { a: s, b: o, x: (s.x + o.x) / 2, z: (s.z + o.z) / 2, fightAt: now() + COUNT_S, began: now(), phase: "count" };
    duels.set(s.id, d);
    duels.set(o.id, d);
    for (const p of [s, o]) send(p, { t: "duel", k: "count", id: other(d, p).id, name: other(d, p).name, x: d.x, z: d.z, left: COUNT_S });
    log(`Duel: ${s.name} vs ${o.name}`);
  }

  function end(d, winner, why) {
    duels.delete(d.a.id);
    duels.delete(d.b.id);
    for (const p of [d.a, d.b]) send(p, { t: "duel", k: "end", id: other(d, p).id, name: other(d, p).name, win: winner ? winner.id : 0 });
    const loser = winner ? other(d, winner) : null;
    const msg = winner ? `${winner.name} has defeated ${loser.name} in a duel${why ? ` (${why})` : ""}!` : `The duel between ${d.a.name} and ${d.b.name} ends in a draw.`;
    sendNear(d.x, d.z, 60, { t: "sys", msg });
    metrics.inc({ result: winner ? "won" : "draw" });
  }

  /** A hit on the opponent: forwarded to them (their game applies it, and yields at the last hit point). */
  function hit(s, id, dmg, cap) {
    const d = duels.get(s.id);
    if (!d || d.phase !== "fight") return;
    const o = other(d, s);
    if (o.id !== (id | 0) || dist(s.x, s.z, o.x, o.z) > 30) return;
    const n = Math.min(Math.max(parseInt(dmg, 10) || 0, 0), cap);
    if (n > 0) send(o, { t: "dhit", id: s.id, dmg: n });
  }

  function yieldDuel(s) {
    const d = duels.get(s.id);
    if (d && d.phase === "fight") end(d, other(d, s), "");
  }

  /** Someone left the world (logout, character select, disconnect): they lose. */
  function left(s) {
    invites.delete(s.id);
    const d = duels.get(s.id);
    if (d) end(d, other(d, s), "they left");
  }

  function tick(t) {
    for (const [id, inv] of invites) if (t - inv.at > INVITE_S) invites.delete(id);
    const seen = new Set();
    for (const d of duels.values()) {
      if (seen.has(d)) continue;
      seen.add(d);
      if (d.phase === "count" && t >= d.fightAt) {
        d.phase = "fight";
        for (const p of [d.a, d.b]) send(p, { t: "duel", k: "fight", id: other(d, p).id, name: other(d, p).name, x: d.x, z: d.z });
        continue;
      }
      for (const p of [d.a, d.b]) {
        if (!p.inWorld || p.inst) { end(d, other(d, p), "they left"); break; }
        if (p.dead) { end(d, other(d, p), "they fell"); break; }
        if (dist(p.x, p.z, d.x, d.z) > FLEE) { end(d, other(d, p), `${p.name} fled`); break; }
      }
      if (duels.get(d.a.id) === d && t - d.began > MAX_S) end(d, null, "");
    }
  }

  return {
    challenge, answer, hit, yieldDuel, left, tick,
    inDuel: (s) => duels.has(s.id),
  };
};
