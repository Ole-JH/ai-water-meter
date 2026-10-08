using System;
using System.Collections.Generic;

namespace Shadowfall
{
    // All messages are JSON text frames over a WebSocket. Field names are kept short because
    // snapshots are sent 10 times per second. The server side lives in /server/server.js.

    [Serializable]
    public class NetMonster
    {
        public int id;
        public string n;       // type name, matches EnemyDef.Name
        public int l;          // level
        public float x, z, ry, hp, mhp, ar;
        public bool sl;        // slowed
        public bool st;        // stunned
        public string el, af;  // elite name and comma-separated affixes (empty for normal monsters)
        public bool sh;        // elite shield up (immune)
    }

    [Serializable]
    public class NetPlayer
    {
        public int id;
        public string name;
        public float x, z, ry, hp, mhp;
        public int lvl, pl;    // pl: paragon level
        public bool mv, atk, dead;
        public bool w;         // up on a town wall (Rampart)
        public string body, legs, weapon, helm, mdl, wk; // wk = weapon model kind
        public string gt;      // guild tag (empty = none)
        public string cp;      // companion following them (id, empty = none)
        public string mt;      // mount they ride (id, empty = on foot)
        public string ti;      // title worn under their name (empty = none)
    }

    /// <summary>Union of every server -> client message.</summary>
    [Serializable]
    public class NetMsg
    {
        public string t;
        public int id, mid, tid, l, xp;
        public string name, msg, err, k;
        public string e;        // "emote": the emote id
        // accounts: "account" (user, email, mail, chars, rc + rcWhy when a recovery code must be shown), "rcode" (rc),
        // "welcome" (name, look of the character entering the world), "hi" (mail = the server can send reset emails)
        public string user, email, look, rc, rcWhy;
        public string reload;   // "error": a newer game build is out, reload the page for it
        public string hash;     // "grid": the server's map hash
        public bool mail;
        public NetCharacter[] chars;
        public float x, z, tx, tz, dmg;
        public bool hasSave;
        public string el;       // "kill": the elite's name when an elite died (better loot)
        public double now;      // server clock (ms since 1970) in "welcome", for the day/night cycle
        public string ch;       // chat channel: "" = everyone, "p" = party, "w" = whisper to you, "wto" = your whisper
        public NetPartyMember[] pm; // "party": members (id = leader)
        // "dungeon": id (0 = back in the overworld at x, z), l = depth, k = name, plus the generated layout
        public int w, h, seed;
        public int d, n, df;           // "dungeon": which dungeon (DungeonDef index), how many levels, difficulty
        public int j;                  // "iok" reforge: which property was reforged
        public float lb;               // "kill": loot bonus from the dungeon difficulty
        public bool admin;             // "welcome": this account is an admin
        public string cells;
        public int[] rooms;            // x, y, w, h per room
        public float[] start, exit, stairs, boss, chests;
        public SaveData save;
        public string[] items;         // trade offers: items as JSON
        public int gold;
        // items and gold live on the server: "inv" (gold, bag, stash, eq, comp), "drops" (loot only we see),
        // "stock" (k = vendor, stock, restock seconds), "iok"/"ierr" (op answered: msg = why not, plus what happened)
        public Item[] bag, stash, eq, stock;
        public string[] comp;
        public NetDrop[] drops;
        public string op, item, target;
        public int rarity, restock;
        public bool burnt;
        public int[] slots;            // "tmine": the bag slots of our trade offer
        // "weather": s = season (0 spring, 1 summer, 2 autumn, 3 winter), sky = clear|cloudy|rain|storm|fog,
        // i = intensity 0..1, left = seconds until the next season
        public int s, left;
        public int c;                  // "rift": seconds until a cleared rift collapses (0: it isn't)
        public string sky;
        public float i;
        public NetMonster[] m;
        public NetPlayer[] p;
        public NetGuild g;             // "guild": our guild (null = none); "ginv": name invites us to k
        public int win;                // "duel" (end): the winner's id (0 = a draw)
        public NetWorldBoss wb;        // "wboss": the world boss that is up (see WorldBoss.cs)
        public NetInvasion iv;         // "invasion": a town under attack (see Invasion.cs); "invwin": k = town, xp, drops
    }

    /// <summary>Our guild (server/guild.js): ranks are member, officer, leader.</summary>
    [Serializable]
    public class NetGuild
    {
        public string name, tag, motd, rank;
        public NetGuildMember[] members;
    }
    [Serializable] public class NetGuildMember { public string name, rank; public bool on; public int lvl; }
    /// <summary>Greater rifts: rinfo (best tier and leaderboard, please), ropen (open tier n).</summary>
    [Serializable] public class RiftMsg { public string t; public int n; }
    [Serializable] public class GuildAnswerMsg { public string t = "ganswer"; public bool yes; }

    /// <summary>A world boss (server/worldboss.js): phase none | up; hp in percent; n = heroes fighting it.</summary>
    [Serializable]
    public class NetWorldBoss
    {
        public string name, region, phase;
        public float x, z;
        public int l, hp, n;
        public int age, pl;            // seconds since it rose; armour plates left (BossPresence)
    }

    /// <summary>A town invasion (server/invasion.js): phase none | gather | wave | won | lost.</summary>
    [Serializable]
    public class NetInvasion
    {
        public string town, gate, phase;
        public float gx, gz;           // where the invaders batter the gate
        public int wave, waves, left;  // left: invaders alive (wave), or seconds until the first wave (gather)
        public int hp;                 // the gate's integrity, 0..100
    }

    // ---------------------------------------------------------------- client -> server

    [Serializable] public class HelloMsg { public string t = "hello"; public string hash, build; public int ver, wv; }
    /// <summary>Every account request (login, register, forgot, reset, chpass, setemail, newcode, play, create, delchar, leave).</summary>
    [Serializable] public class AuthMsg { public string t, user, pass, email, code, old, name, look; }
    [Serializable] public class NetCharacter { public string name, look; public int lvl; }
    [Serializable] public class WorldMsg { public string t = "world"; public string hash, cells; public int w, h; }
    [Serializable] public class HitMsg { public string t = "hit"; public int mid, dmg; public bool crit; }
    /// <summary>Duels: dreq (challenge id), dans (answer: yes), dhit (a hit on opponent id), dyield (we lost).</summary>
    [Serializable] public class DuelMsg { public string t; public int id, dmg; public bool yes; }
    [Serializable] public class SlowMsg { public string t = "slow"; public int mid; public float dur; }
    [Serializable] public class StunMsg { public string t = "stun"; public int mid; public float dur; }
    [Serializable] public class VanishMsg { public string t = "vanish"; public float dur; }
    [Serializable] public class ChatMsg { public string t = "chat"; public string msg; }
    [Serializable] public class EmoteMsg { public string t = "emote"; public string e; }
    /// <summary>We earned achievement <c>id</c>: the server tells the party and players nearby.</summary>
    [Serializable] public class AchMsg { public string t = "ach"; public string id; }
    [Serializable] public class FxMsg { public string t = "fx"; public string k; public float x, z, tx, tz; }
    [Serializable] public class SaveMsg { public string t = "save"; public SaveData save; }

    [Serializable]
    public class StateMsg
    {
        public string t = "state";
        public float x, z, ry, hp, mhp, mp, mmp;
        public int lvl, pl;    // pl: paragon level
        public bool mv, atk, dead;
        public bool w;         // up on a town wall: ranged invaders can shoot us there
        public string body, legs, weapon, helm, mdl, wk; // wk = weapon model kind
        public string cp;      // active companion id
        public string mt;      // mount ridden (id, empty = on foot; the server checks we own it)
        public string ti;      // the achievement whose title we wear (the server checks we earned it)
    }

    [Serializable]
    public class NetPartyMember
    {
        public int id, lvl;
        public string name, mdl;    // mdl = hero model, which is also the class (Knight, Mage, ...)
        public string wk, helm;     // weapon in hand, helm color (empty = no helm), for the portrait
        public float hp, mhp, mp, mmp, x, z, ry;
        public bool dead;
        public int di;          // dungeon instance the member is in (0 = overworld)
        public string dn;       // where in a dungeon ("Catacombs, level 2"); empty in the overworld
    }

    /// <summary>Dungeon commands: denter, dstairs, dleave (town = after dying).</summary>
    [Serializable] public class DungeonCmd { public string t; public bool town; public int d, df; }
    /// <summary>Admin command (the server checks the sender is an admin). Unused fields are ignored.</summary>
    [Serializable] public class AdminCmd { public string t = "adm"; public string c, name, type, text, phase, what, kind, town, gate; public float x, z, r, chance; public int d, l, n, df; public bool elite, fresh, stop; }

    /// <summary>Party commands: pinvite (name), paccept, pdecline, pleave, pkick (id), pshare (q).</summary>
    [Serializable] public class PartyCmd { public string t, name, q; public int id; }
    [Serializable] public class TradeCmd { public string t; public int id, gold; public int[] slots; }
    /// <summary>Loot on the ground that only we can see (gold or an item; id = what to send to pick it up).</summary>
    [Serializable] public class NetDrop { public int id, gold; public float x, z; public Item item; }
    /// <summary>
    /// An item action, checked and carried out by the server (see itemOps in server.js): equip i, unequip slot, use i,
    /// drop i, pickup id, sort, stash i, unstash i, socket i (to "eq" slot or "bag" j), fuse, sell i, sellcommon, vendor k,
    /// buy k i n, craft name, gather name, quest k, hire k, respec, chest i.
    /// </summary>
    [Serializable] public class IopMsg { public string t = "iop"; public string op, k, name, to; public int i, j, slot, n, id; }

    // ---------------------------------------------------------------- character save (stored by the server)

    [Serializable] public class QuestSave { public string id; public int kills; }
    [Serializable] public class SlotSave { public int index; public Item item; }

    [Serializable]
    public class SaveData
    {
        public int level, xp, gold, str, dex, intel, vit, statPoints;
        public int paragon, paragonXp;  // paragon level and its experience (past ParagonBoard.MaxLevel)
        public int[] paragonPts;        // points in Might, Toughness, Precision, Swiftness
        public string look;
        public string mount;       // the mount V calls
        public string[] talents;   // "id:rank"
        public float x, z, hp, mana;
        public int[] skillXp;
        public string[] completedQuests;
        public QuestSave[] activeQuests;
        public SlotSave[] inventory;
        public Item[] equipped;
        public SlotSave[] stash;
        public string[] companions;   // hired companion ids
        public string companion;      // the one following (empty = none)
        public string fog;            // explored overworld tiles, run-length coded (see Exploration.Save)
        public int news;              // the newest Changelog entry this hero has read (0 = saved before the changelog)
        public int wv;                // WorldGenerator.LayoutVersion when saved (older positions get converted)
        public string[] stats;        // achievement counters, "name=value"
        public string[] ach;          // achievements earned, "id@yyyy-MM-dd"
        public string title;          // the achievement whose title is worn (empty = none)
    }
}
