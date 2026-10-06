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
    }

    [Serializable]
    public class NetPlayer
    {
        public int id;
        public string name;
        public float x, z, ry, hp, mhp;
        public int lvl;
        public bool mv, atk, dead;
        public string body, legs, weapon, helm;
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
        public SaveData save;
        public NetMonster[] m;
        public NetPlayer[] p;
    }

    // ---------------------------------------------------------------- client -> server

    [Serializable] public class HelloMsg { public string t = "hello"; public string name, pass, hash; public int ver; }
    [Serializable] public class WorldMsg { public string t = "world"; public string hash, cells; public int w, h; }
    [Serializable] public class HitMsg { public string t = "hit"; public int mid, dmg; public bool crit; }
    [Serializable] public class SlowMsg { public string t = "slow"; public int mid; public float dur; }
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
        public string body, legs, weapon, helm;
    }

    // ---------------------------------------------------------------- character save (stored by the server)

    [Serializable] public class QuestSave { public string id; public int kills; }
    [Serializable] public class SlotSave { public int index; public Item item; }

    [Serializable]
    public class SaveData
    {
        public int level, xp, gold, str, dex, intel, vit, statPoints;
        public float x, z, hp, mana;
        public int[] skillXp;
        public string[] completedQuests;
        public QuestSave[] activeQuests;
        public SlotSave[] inventory;
        public Item[] equipped;
    }
}
