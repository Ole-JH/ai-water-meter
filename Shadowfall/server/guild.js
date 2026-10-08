// Guilds: a name, a tag shown before members' names, a guild chat (/g), a message of the day and three ranks
// (leader, officer, member). Stored as one document per guild (store.saveGuild); members are character names.
//
// Chat commands (all through chat, so they work from anywhere):
//   /guild                       who's in it, and the commands
//   /guild create <Name> <TAG>   found one (GUILD_COST gold)
//   /ginvite <name>              officers and the leader; the hero answers with the popup (ganswer)
//   /gleave  /gkick <name>  /gpromote <name>  /gdemote <name>  /gleader <name>  /gmotd <text>  /g <text>
// The client gets "guild" {g: {name, tag, rank, motd, members: [{name, rank, on, lvl}]}} (g null = no guild)
// and "ginv" {name, k: guild} for an invitation.

const COST = Number(process.env.GUILD_COST ?? 1000);
const MAX_MEMBERS = 100;
const RANKS = ["member", "officer", "leader"];
const NAME = /^[A-Za-z][A-Za-z ']{2,23}$/, TAG = /^[A-Za-z]{2,4}$/;

module.exports = function createGuilds(ctx) {
  const { store, sessions, safeSend, sys, findOnline, log, now } = ctx;
  const guilds = new Map();  // lower-case name -> guild
  const byMember = new Map(); // lower-case character name -> guild
  const invites = new Map();  // session id -> { guild, from, at }

  const send = (s, msg) => safeSend(s, JSON.stringify(msg));
  const key = (n) => String(n || "").toLowerCase();
  const member = (g, name) => g.members.find((m) => key(m.name) === key(name));
  const rankOf = (g, name) => RANKS.indexOf((member(g, name) || {}).rank);
  const online = (g) => [...sessions.values()].filter((o) => o.inWorld && byMember.get(key(o.name)) === g);

  async function load() {
    for (const g of await store.loadGuilds()) {
      if (!g || !g.name || !Array.isArray(g.members)) continue;
      guilds.set(key(g.name), g);
      for (const m of g.members) byMember.set(key(m.name), g);
    }
    if (guilds.size) log(`${guilds.size} guild(s) loaded`);
  }

  const save = (g) => store.saveGuild(g).catch((e) => log(`guild save failed: ${e.message}`));

  function info(g, forName) {
    const on = new Map(online(g).map((o) => [key(o.name), o]));
    return { name: g.name, tag: g.tag, motd: g.motd || "", rank: (member(g, forName) || {}).rank || "member",
      members: g.members.map((m) => ({ name: m.name, rank: m.rank, on: on.has(key(m.name)), lvl: on.get(key(m.name))?.lvl || 0 })) };
  }

  /** Tells everyone in the guild who is online the new state (and those who just left: no guild). */
  function refresh(g, also = []) {
    for (const o of online(g)) send(o, { t: "guild", g: info(g, o.name) });
    for (const o of also) send(o, { t: "guild", g: null });
  }

  function tell(g, msg) { for (const o of online(g)) sys(o, `[${g.tag}] ${msg}`); }

  /** The guild tag shown before a hero's name ("" without one). */
  const tagOf = (s) => (byMember.get(key(s.name)) || {}).tag || "";

  function create(s, args) {
    if (byMember.has(key(s.name))) return sys(s, "You are already in a guild: /gleave first.");
    const tag = (args.pop() || "").toUpperCase(), name = args.join(" ").trim().replace(/\s+/g, " ");
    if (!NAME.test(name) || !TAG.test(tag)) return sys(s, "Usage: /guild create <Name> <TAG>  (a name of 3-24 letters, a tag of 2-4 letters)");
    if (guilds.has(key(name))) return sys(s, `There is already a guild called ${name}.`);
    if ([...guilds.values()].some((g) => g.tag === tag)) return sys(s, `The tag ${tag} is taken.`);
    if (!s.ledger || s.ledger.gold < COST) return sys(s, `Founding a guild costs ${COST} gold.`);
    s.ledger.gold -= COST;
    ctx.ledgerChanged(s);
    const g = { name, tag, motd: "", created: new Date().toISOString(), members: [{ name: s.name, rank: "leader", joined: new Date().toISOString() }] };
    guilds.set(key(name), g);
    byMember.set(key(s.name), g);
    save(g);
    sys(s, `You founded ${name} <${tag}> (${COST} gold). Invite heroes with /ginvite name; talk with /g.`);
    log(`Guild ${name} <${tag}> founded by ${s.name}`);
    refresh(g);
  }

  function invite(s, name) {
    const g = byMember.get(key(s.name));
    if (!g) return sys(s, "You are not in a guild.");
    if (rankOf(g, s.name) < 1) return sys(s, "Only officers and the leader can invite.");
    const o = findOnline(name);
    if (!o) return sys(s, `No player named "${String(name || "").slice(0, 16)}" is online.`);
    if (byMember.has(key(o.name))) return sys(s, `${o.name} is already in a guild.`);
    if (g.members.length >= MAX_MEMBERS) return sys(s, `${g.name} is full.`);
    invites.set(o.id, { guild: g, from: s.name, at: now() });
    send(o, { t: "ginv", name: s.name, k: `${g.name} <${g.tag}>` });
    sys(s, `You invite ${o.name} to ${g.name}.`);
  }

  function answer(s, yes) {
    const inv = invites.get(s.id);
    invites.delete(s.id);
    if (!inv || now() - inv.at > 60 || !guilds.has(key(inv.guild.name))) return;
    const g = inv.guild;
    if (!yes) { const f = findOnline(inv.from); if (f) sys(f, `${s.name} declines to join ${g.name}.`); return; }
    if (byMember.has(key(s.name))) return;
    g.members.push({ name: s.name, rank: "member", joined: new Date().toISOString() });
    byMember.set(key(s.name), g);
    save(g);
    tell(g, `${s.name} has joined the guild.`);
    refresh(g);
  }

  function removeMember(g, name) {
    g.members = g.members.filter((m) => key(m.name) !== key(name));
    byMember.delete(key(name));
    if (!g.members.length) {
      guilds.delete(key(g.name));
      store.deleteGuild(g.name).catch(() => {});
      log(`Guild ${g.name} disbanded`);
      return false;
    }
    if (!g.members.some((m) => m.rank === "leader")) { // the next in line leads: an officer, else the longest member
      const next = g.members.find((m) => m.rank === "officer") || g.members[0];
      next.rank = "leader";
      tell(g, `${next.name} now leads the guild.`);
    }
    save(g);
    return true;
  }

  function leave(s) {
    const g = byMember.get(key(s.name));
    if (!g) return sys(s, "You are not in a guild.");
    const still = removeMember(g, s.name);
    sys(s, still ? `You left ${g.name}.` : `You left ${g.name}, and with nobody left it is no more.`);
    if (still) { tell(g, `${s.name} has left the guild.`); refresh(g, [s]); } else send(s, { t: "guild", g: null });
  }

  function kick(s, name) {
    const g = byMember.get(key(s.name));
    if (!g) return sys(s, "You are not in a guild.");
    const m = member(g, name);
    if (!m) return sys(s, `${String(name || "").slice(0, 16)} is not in your guild.`);
    if (rankOf(g, s.name) < 1 || rankOf(g, m.name) >= rankOf(g, s.name)) return sys(s, "You can only remove members of a lower rank.");
    removeMember(g, m.name);
    tell(g, `${m.name} was removed from the guild by ${s.name}.`);
    const o = findOnline(m.name);
    if (o) sys(o, `You were removed from ${g.name}.`);
    refresh(g, o ? [o] : []);
  }

  function setRank(s, name, rank) {
    const g = byMember.get(key(s.name));
    if (!g) return sys(s, "You are not in a guild.");
    if (rankOf(g, s.name) < 2) return sys(s, "Only the leader can change ranks.");
    const m = member(g, name);
    if (!m || key(m.name) === key(s.name)) return sys(s, `${String(name || "").slice(0, 16)} is not another member of your guild.`);
    if (rank === "leader") { member(g, s.name).rank = "officer"; tell(g, `${m.name} now leads the guild.`); }
    else tell(g, `${m.name} is now ${rank === "officer" ? "an officer" : "a member"}.`);
    m.rank = rank;
    save(g);
    refresh(g);
  }

  function motd(s, text) {
    const g = byMember.get(key(s.name));
    if (!g) return sys(s, "You are not in a guild.");
    if (rankOf(g, s.name) < 1) return sys(s, "Only officers and the leader can set the message of the day.");
    g.motd = String(text || "").slice(0, 160);
    save(g);
    tell(g, `Message of the day: ${g.motd || "(none)"}`);
    refresh(g);
  }

  function chat(s, text) {
    const g = byMember.get(key(s.name));
    if (!g) return sys(s, "You are not in a guild.");
    if (!text) return;
    const data = JSON.stringify({ t: "chat", ch: "g", id: s.id, name: s.name, msg: text });
    for (const o of online(g)) safeSend(o, data);
  }

  function roster(s) {
    const g = byMember.get(key(s.name));
    if (!g) return sys(s, `You are not in a guild. Found one with /guild create <Name> <TAG> (${COST} gold), or ask a guild's officer to /ginvite you.`);
    const on = online(g).map((o) => o.name);
    sys(s, `${g.name} <${g.tag}>: ${g.members.length} member(s), ${on.length} online (${on.join(", ")}). ` +
      `/g chat, /ginvite, /gleave, /gkick, /gpromote, /gdemote, /gleader, /gmotd`);
    send(s, { t: "guild", g: info(g, s.name) });
  }

  /** Handles a chat command; returns false when it isn't a guild command. */
  function command(s, cmd, rest) {
    const arg = rest.join(" ").trim();
    switch (cmd) {
      case "/guild": return rest[0] && rest[0].toLowerCase() === "create" ? create(s, rest.slice(1)) : roster(s), true;
      case "/g": case "/gchat": return chat(s, arg), true;
      case "/ginvite": return invite(s, rest[0]), true;
      case "/gleave": return leave(s), true;
      case "/gkick": return kick(s, rest[0]), true;
      case "/gpromote": return setRank(s, rest[0], "officer"), true;
      case "/gdemote": return setRank(s, rest[0], "member"), true;
      case "/gleader": return setRank(s, rest[0], "leader"), true;
      case "/gmotd": return motd(s, arg), true;
      default: return false;
    }
  }

  return {
    load, command, answer, tagOf,
    /** At login: the guild, the message of the day, and the members hear about it. */
    entered(s) {
      const g = byMember.get(key(s.name));
      if (!g) return;
      send(s, { t: "guild", g: info(g, s.name) });
      if (g.motd) sys(s, `[${g.tag}] ${g.motd}`);
      refresh(g);
    },
    left(s) { invites.delete(s.id); const g = byMember.get(key(s.name)); if (g) setTimeout(() => refresh(g), 100); },
    /** A character was deleted: out of the guild. */
    deleted(name) { const g = byMember.get(key(name)); if (g) { removeMember(g, name); refresh(g); } },
    count: () => guilds.size,
  };
};
