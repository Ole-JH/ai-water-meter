// The auction house: list an item from your bags at a buyout price at any general merchant in town; anyone can browse
// and buy. The seller gets the price less a 5% cut (gold leaves the economy), by "mail": at once when online, else at
// their next login. Unsold items come back the same way after LISTING_HOURS. Listings and undelivered mail are stored
// with store.setMeta ("auction", "auction_mail").
//
// Item ops (through iop, so the usual checks apply): aubrowse {k: search}, aulist {i: bag slot, n: price},
// aubuy {id}, aucancel {id}. The client gets "auction" {items: [listing JSON], k: search, n: my listings count}.

const HOURS = Number(process.env.AUCTION_HOURS ?? 48);
const CUT = 0.05, PER_SELLER = 10, MAX_LISTINGS = 2000, RESULTS = 60, MAX_PRICE = 10_000_000;

module.exports = function createAuctions(ctx) {
  const { store, I, sessions, safeSend, sys, give, ierr, iok, inTownNow, bagItem, ledgerChanged, log, metrics } = ctx;
  let listings = [];  // { id, seller, item, price, at, until }
  let mail = {};      // lower-case character name -> { gold, items: [] }
  let nextId = 1;

  async function load() {
    try { listings = JSON.parse((await store.getMeta("auction")) || "[]"); } catch { listings = []; }
    try { mail = JSON.parse((await store.getMeta("auction_mail")) || "{}"); } catch { mail = {}; }
    nextId = listings.reduce((m, l) => Math.max(m, l.id), 0) + 1;
    if (listings.length) log(`${listings.length} auction listing(s) loaded`);
  }
  const save = () => store.setMeta("auction", JSON.stringify(listings)).catch((e) => log(`auction save failed: ${e.message}`));
  const saveMail = () => store.setMeta("auction_mail", JSON.stringify(mail)).catch((e) => log(`auction mail save failed: ${e.message}`));
  const key = (n) => String(n || "").toLowerCase();
  const online = (name) => [...sessions.values()].find((o) => o.inWorld && o.ledger && key(o.name) === key(name));

  /** Gold and items for a hero: delivered now if they're online, kept for their next login otherwise. */
  function post(name, gold, items, why) {
    const m = mail[key(name)] || (mail[key(name)] = { gold: 0, items: [], notes: [] });
    m.gold += gold;
    m.items.push(...items);
    m.notes.push(why);
    const o = online(name);
    if (o) deliver(o);
    else saveMail();
  }

  function deliver(s) {
    const m = mail[key(s.name)];
    if (!m || !s.ledger) return;
    delete mail[key(s.name)];
    s.ledger.gold += m.gold;
    const drops = m.items.length ? give(s, m.items) : [];
    if (drops.length) safeSend(s, JSON.stringify({ t: "drops", drops }));
    for (const n of m.notes.slice(0, 10)) sys(s, `[Auction] ${n}`);
    if (m.notes.length > 10) sys(s, `[Auction] ...and ${m.notes.length - 10} more.`);
    ledgerChanged(s);
    saveMail();
  }

  const view = (l) => JSON.stringify({ id: l.id, seller: l.seller, price: l.price, left: Math.max(0, Math.round((l.until - Date.now()) / 60000)), item: l.item });

  function browse(s, q) {
    q = key(q).trim().slice(0, 40);
    const mine = listings.filter((l) => key(l.seller) === key(s.name));
    const hits = listings.filter((l) => key(l.seller) !== key(s.name) && (!q || key(l.item.Name).includes(q) || key(l.item.BaseType).includes(q)))
      .sort((a, b) => b.at - a.at).slice(0, RESULTS);
    safeSend(s, JSON.stringify({ t: "auction", k: q, n: mine.length, items: [...mine, ...hits].map(view) }));
  }

  const ops = {
    aubrowse(s, m) { browse(s, m.k); return false; },

    aulist(s, m) {
      const it = bagItem(s, m.i);
      if (!it) return false;
      if (!inTownNow(s)) return ierr(s, "aulist", "The auction house is at the general merchants in town.");
      const price = Math.floor(Number(m.n));
      if (!(price >= 1 && price <= MAX_PRICE)) return ierr(s, "aulist", "Set a price between 1 and 10,000,000 gold.");
      if (listings.filter((l) => key(l.seller) === key(s.name)).length >= PER_SELLER) return ierr(s, "aulist", `You can have at most ${PER_SELLER} items for sale.`);
      if (listings.length >= MAX_LISTINGS) return ierr(s, "aulist", "The auction house is full. Try again later.");
      s.ledger.bag[m.i] = null;
      const l = { id: nextId++, seller: s.name, item: it, price, at: Date.now(), until: Date.now() + HOURS * 3600000 };
      listings.push(l);
      save();
      metrics.inc({ op: "listed" });
      iok(s, "aulist", { name: it.Name, gold: price });
      browse(s, "");
      return true;
    },

    aubuy(s, m) {
      const l = listings.find((x) => x.id === (m.id | 0));
      if (!l) { browse(s, ""); return ierr(s, "aubuy", "That's already sold."); }
      if (!inTownNow(s)) return ierr(s, "aubuy", "The auction house is at the general merchants in town.");
      if (key(l.seller) === key(s.name)) return ierr(s, "aubuy", "That's your own listing.");
      if (s.ledger.gold < l.price) return ierr(s, "aubuy", "You don't have enough gold.");
      if (!I.fits(s.ledger.bag, [l.item])) return ierr(s, "aubuy", "Your bags are full.");
      listings = listings.filter((x) => x !== l);
      save();
      s.ledger.gold -= l.price;
      I.addItem(s.ledger.bag, l.item);
      const paid = Math.max(1, Math.floor(l.price * (1 - CUT)));
      post(l.seller, paid, [], `${s.name} bought your ${l.item.Name}: ${paid} gold (after the house's 5%).`);
      metrics.inc({ op: "sold" });
      log(`Auction: ${s.name} bought ${l.item.Name} from ${l.seller} for ${l.price}`);
      iok(s, "aubuy", { name: l.item.Name, gold: l.price });
      browse(s, m.k);
      return true;
    },

    aucancel(s, m) {
      const l = listings.find((x) => x.id === (m.id | 0) && key(x.seller) === key(s.name));
      if (!l) return false;
      listings = listings.filter((x) => x !== l);
      save();
      const drops = give(s, [l.item]);
      if (drops.length) safeSend(s, JSON.stringify({ t: "drops", drops }));
      iok(s, "aucancel", { name: l.item.Name });
      browse(s, "");
      return true;
    },
  };

  /** Unsold items go home. */
  function tick() {
    const t = Date.now(), gone = listings.filter((l) => l.until <= t);
    if (!gone.length) return;
    listings = listings.filter((l) => l.until > t);
    save();
    for (const l of gone) { post(l.seller, 0, [l.item], `Your ${l.item.Name} didn't sell; it's back with you.`); metrics.inc({ op: "expired" }); }
  }

  return { load, ops, deliver, tick, count: () => listings.length };
};
