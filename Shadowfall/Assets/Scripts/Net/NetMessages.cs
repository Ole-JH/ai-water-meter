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
        public int lvl;
        public bool mv, atk, dead;
        public string body, legs, weapon, helm, mdl, wk; // wk = weapon model kind
        public string cp;      // companion following them (id, empty = none)
    }

    /// <summary>Union of every server -> client message.</summary>
    [Serializable]
    public class NetMsg
    {
        public string t;
        public int id, mid, tid, l, xp;
        public string name, msg, err, k;
        public float x, z, tx, tz, dmg;
        public bool hasSave;
        public string el;       // "kill": the elite's name when an elite died (better loot)
        public double now;      // server clock (ms since 1970) in "welcome", for the day/night cycle
        public string ch;       // chat channel: "" = everyone, "p" = party, "w" = whisper to you, "wto" = your whisper
        public NetPartyMember[] pm; // "party": members (id = leader)
        // "dungeon": id (0 = back in the overworld at x, z), l = depth, k = name, plus the generated layout
        public int w, h, seed;
        public int d, n, df;           // "dungeon": which dungeon (DungeonDef index), how many levels, difficulty
        public float lb;               // "kill": loot bonus from the dungeon difficulty
        public bool admin;             // "welcome": this account is an admin
        public string cells;
        public int[] rooms;            // x, y, w, h per room
        public float[] start, exit, stairs, boss, chests;
        public SaveData save;
        public string[] items;         // trade offers: items as JSON
        public int gold;
        public NetMonster[] m;
        public NetPlayer[] p;
    }

    // ---------------------------------------------------------------- client -> server

    [Serializable] public class HelloMsg { public string t = "hello"; public string name, pass, hash; public int ver, wv; }
    [Serializable] public class WorldMsg { public string t = "world"; public string hash, cells; public int w, h; }
    [Serializable] public class HitMsg { public string t = "hit"; public int mid, dmg; public bool crit; }
    [Serializable] public class SlowMsg { public string t = "slow"; public int mid; public float dur; }
    [Serializable] public class StunMsg { public string t = "stun"; public int mid; public float dur; }
    [Serializable] public class VanishMsg { public string t = "vanish"; public float dur; }
    [Serializable] public class ChatMsg { public string t = "chat"; public string msg; }
    [Serializable] public class FxMsg { public string t = "fx"; public string k; public float x, z, tx, tz; }
    [Serializable] public class SaveMsg { public string t = "save"; public SaveData save; }

    [Serializable]
    public class StateMsg
    {
        public string t = "state";
        public float x, z, ry, hp, mhp;
        public int lvl;
        public bool mv, atk, dead;
        public string body, legs, weapon, helm, mdl, wk; // wk = weapon model kind
        public string cp;      // active companion id
    }

    [Serializable]
    public class NetPartyMember
    {
        public int id, lvl;
        public string name, mdl;
        public float hp, mhp, x, z;
        public bool dead;
        public int di;          // dungeon instance the member is in (0 = overworld)
    }

    /// <summary>Dungeon commands: denter, dstairs, dleave (town = after dying).</summary>
    [Serializable] public class DungeonCmd { public string t; public bool town; public int d, df; }
    /// <summary>Admin command (the server checks the sender is an admin). Unused fields are ignored.</summary>
    [Serializable] public class AdminCmd { public string t = "adm"; public string c, name, type, text, phase; public float x, z, r, chance; public int d, l, n, df; public bool elite, fresh; }

    /// <summary>Party commands: pinvite (name), paccept, pdecline, pleave, pkick (id), pshare (q).</summary>
    [Serializable] public class PartyCmd { public string t, name, q; public int id; }
    [Serializable] public class TradeCmd { public string t; public int id, gold; public string[] items; }

    // ---------------------------------------------------------------- character save (stored by the server)

    [Serializable] public class QuestSave { public string id; public int kills; }
    [Serializable] public class SlotSave { public int index; public Item item; }

    [Serializable]
    public class SaveData
    {
        public int level, xp, gold, str, dex, intel, vit, statPoints;
        public string look;
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
        public string fog;            // explored overworld tiles, 1 bit each, base64 (see Exploration)
        public int wv;                // WorldGenerator.LayoutVersion when saved (older positions get converted)
    }
}
