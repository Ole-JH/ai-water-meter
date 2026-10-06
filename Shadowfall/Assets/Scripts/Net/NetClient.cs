using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Talks to the Shadowfall server: login, world snapshots (monsters + other players),
    /// combat events, chat, spell effects and character saves.
    /// </summary>
    public class NetClient : MonoBehaviour
    {
        public const int ProtocolVersion = 1;
        public static NetClient I;

        public enum ConnState { Offline, Connecting, LoggingIn, InWorld }
        public ConnState State { get; private set; } = ConnState.Offline;
        public string Status { get; private set; } = "";
        public int MyId { get; private set; }
        public int PlayersOnline { get; private set; }

        readonly WebSocketConnection socket = new WebSocketConnection();
        string pendingName, pendingPass;
        float nextStateSend, nextSave, lastMessage;
        readonly HashSet<int> seenMonsters = new HashSet<int>();
        readonly List<int> stale = new List<int>();

        void Awake() => I = this;

        // =====================================================================================
        // Connection
        // =====================================================================================

        /// <param name="url">ws:// or wss:// URL. Empty in WebGL = the server that hosts the page.</param>
        public void Login(string url, string name, string password)
        {
            name = (name ?? "").Trim();
            if (name.Length < 3 || name.Length > 16) { Status = "Name must be 3-16 characters."; return; }
            if (string.IsNullOrEmpty(password) || password.Length < 4) { Status = "Password must be at least 4 characters."; return; }
            pendingName = name;
            pendingPass = password;
            State = ConnState.Connecting;
            Status = "Connecting...";
            socket.Connect(url);
        }

        public void Disconnect(string reason)
        {
            if (State == ConnState.InWorld) SaveNow();
            socket.Close();
            bool wasInWorld = State == ConnState.InWorld;
            State = ConnState.Offline;
            Status = reason;
            if (wasInWorld) GameManager.I.LeaveWorld();
        }

        void OnApplicationQuit()
        {
            if (State == ConnState.InWorld) SaveNow();
            socket.Close();
        }

        void Send(object msg) => socket.Send(JsonUtility.ToJson(msg));

        // =====================================================================================
        // Outgoing gameplay messages
        // =====================================================================================

        public void SendHit(int monsterId, int dmg, bool crit)
        {
            if (State == ConnState.InWorld) Send(new HitMsg { mid = monsterId, dmg = dmg, crit = crit });
        }

        public void SendSlow(int monsterId, float duration)
        {
            if (State == ConnState.InWorld) Send(new SlowMsg { mid = monsterId, dur = duration });
        }

        public void SendChat(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || State != ConnState.InWorld) return;
            if (text.Length > 200) text = text.Substring(0, 200);
            Send(new ChatMsg { msg = text });
        }

        public void SendFx(string kind, Vector3 from, Vector3 to)
        {
            if (State == ConnState.InWorld) Send(new FxMsg { k = kind, x = from.x, z = from.z, tx = to.x, tz = to.z });
        }

        public void SaveNow()
        {
            if (State != ConnState.InWorld || Player.I == null) return;
            Send(new SaveMsg { save = Player.I.ToSave() });
            nextSave = Time.time + 20f;
        }

        void SendState()
        {
            var p = Player.I;
            if (p == null) return;
            Send(new StateMsg
            {
                x = p.transform.position.x, z = p.transform.position.z, ry = p.transform.eulerAngles.y,
                hp = p.Health, mhp = p.MaxHealth, lvl = p.Level, mv = p.IsMoving, atk = p.IsAttacking, dead = p.IsDead,
                body = p.BodyHex, legs = p.LegsHex, weapon = p.WeaponHex, helm = p.HelmHex,
            });
        }

        // =====================================================================================
        // Main loop
        // =====================================================================================

        void Update()
        {
            int budget = 200;
            while (budget-- > 0 && socket.TryReceive(out var raw)) Handle(raw);

            if (State == ConnState.InWorld)
            {
                if (Time.time >= nextStateSend) { nextStateSend = Time.time + 0.1f; SendState(); }
                if (Time.time >= nextSave) SaveNow();
                if (Time.time - lastMessage > 15f) Disconnect("Lost connection to the server (timeout).");
            }
        }

        void Handle(string raw)
        {
            lastMessage = Time.time;
            if (raw == "__open")
            {
                State = ConnState.LoggingIn;
                Status = "Logging in...";
                Send(new HelloMsg { name = pendingName, pass = pendingPass, hash = GameManager.I.GridHash, ver = ProtocolVersion });
                return;
            }
            if (raw.StartsWith("__close:") || raw.StartsWith("__error:"))
            {
                string why = raw.Substring(raw.IndexOf(':') + 1);
                if (State != ConnState.Offline) Disconnect(State == ConnState.InWorld ? "Disconnected: " + why : "Could not connect: " + why);
                return;
            }

            NetMsg m;
            try { m = JsonUtility.FromJson<NetMsg>(raw); }
            catch (System.Exception e) { Debug.LogWarning("Bad message: " + e.Message); return; }
            if (m == null || m.t == null) return;

            switch (m.t)
            {
                case "needworld":
                    // First client to connect to a fresh server teaches it the world layout.
                    var grid = WorldGrid.Instance;
                    Send(new WorldMsg { hash = GameManager.I.GridHash, w = grid.Width, h = grid.Height, cells = System.Convert.ToBase64String(grid.Pack()) });
                    Status = "Uploading world map to server...";
                    break;

                case "error":
                    Disconnect(m.err);
                    break;

                case "welcome":
                    MyId = m.id;
                    State = ConnState.InWorld;
                    Status = "";
                    nextSave = Time.time + 20f;
                    GameManager.I.EnterWorld(pendingName, m.hasSave ? m.save : null);
                    pendingPass = null;
                    break;

                case "snap": HandleSnapshot(m); break;
                case "mdie": if (Enemy.ById.TryGetValue(m.mid, out var dying)) dying.NetDie(); break;
                case "kill": HandleKill(m); break;
                case "matk": HandleMonsterAttack(m); break;
                case "fx": HandleFx(m); break;

                case "chat":
                    GameUI.Log("[" + m.name + "]: " + m.msg, m.id == MyId ? new Color(0.85f, 0.85f, 1f) : Color.white);
                    if (RemotePlayer.ById.TryGetValue(m.id, out var speaker)) GameUI.Float(speaker.transform.position + Vector3.up * 3f, m.msg, Color.white, 0.9f);
                    break;

                case "sys":
                    GameUI.Log(m.msg, new Color(1f, 0.85f, 0.4f));
                    break;

                case "leave":
                    if (RemotePlayer.ById.TryGetValue(m.id, out var gone)) Destroy(gone.gameObject);
                    break;
            }
        }

        void HandleSnapshot(NetMsg m)
        {
            if (Player.I == null) return;
            PlayersOnline = m.l;

            seenMonsters.Clear();
            if (m.m != null)
                foreach (var nm in m.m)
                {
                    seenMonsters.Add(nm.id);
                    if (Enemy.ById.TryGetValue(nm.id, out var e) && e != null) { if (!e.IsDead) e.ApplySnapshot(nm); }
                    else Enemy.Spawn(nm);
                }
            // Remove monsters that left our area of interest (but let dying ones finish their animation).
            stale.Clear();
            foreach (var kv in Enemy.ById)
                if (!seenMonsters.Contains(kv.Key) && !kv.Value.IsDead && Time.time - kv.Value.LastSeen > 1f) stale.Add(kv.Key);
            foreach (var id in stale) { Destroy(Enemy.ById[id].gameObject); Enemy.ById.Remove(id); }

            if (m.p != null)
                foreach (var np in m.p)
                    if (np.id != MyId) RemotePlayer.Get(np).Apply(np);
        }

        void HandleKill(NetMsg m)
        {
            var p = Player.I;
            if (p == null) return;
            var def = EnemyDef.ByName(m.name);
            var pos = new Vector3(m.x, 0, m.z);
            if (Enemy.ById.TryGetValue(m.mid, out var e)) e.NetDie();
            if (!p.IsDead)
            {
                p.AddXp(m.xp);
                p.Quests.OnKill(def.Name);
                Enemy.RollLoot(def, m.l, pos);
            }
            if (def.Boss) GameUI.Banner(def.Name + " has been slain!", new Color(1f, 0.55f, 0.1f));
        }

        void HandleMonsterAttack(NetMsg m)
        {
            var p = Player.I;
            Enemy.ById.TryGetValue(m.mid, out var e);
            var targetPos = new Vector3(m.x, 0, m.z);
            bool targetIsMe = m.tid == MyId;
            if (e != null) e.PlayAttack(targetPos);

            switch (m.k)
            {
                case "melee":
                    if (targetIsMe && p != null && !p.IsDead) p.TakeDamage(m.dmg, e);
                    break;

                case "shot":
                    if (e == null) break;
                    var from = e.transform.position + Vector3.up * e.Height * 0.6f + e.transform.forward * 0.5f;
                    var to = targetPos + Vector3.up;
                    bool boss = e.Def.Boss;
                    if (targetIsMe && p != null && !p.IsDead)
                        Projectile.Fire(e, from, p.transform.position + Vector3.up, 13f, m.dmg, e.Def.ProjectileColor,
                            boss ? 0.7f : 0.35f, boss ? 1.8f : 0f, 18f);
                    else
                        Projectile.FireVisual(from, to, 13f, e.Def.ProjectileColor, boss ? 0.7f : 0.35f, 18f);
                    break;

                case "nova":
                    var c = new Color(0.4f, 0.85f, 1f);
                    var center = e != null ? e.transform.position : targetPos;
                    FxPulse.Ring(center, c, 6f, 0.6f);
                    FxPulse.Burst(center + Vector3.up, c, 2f, 0.4f);
                    if (p != null && !p.IsDead && Factory.FlatDistance(p.transform.position, center) < 6.5f) p.TakeDamage(m.dmg, e);
                    break;

                case "summon":
                    GameUI.Banner("Lich King: \"Rise, my servants!\"", new Color(0.6f, 0.85f, 1f));
                    break;
            }
        }

        void HandleFx(NetMsg m)
        {
            if (m.id == MyId) return;
            var from = new Vector3(m.x, 0, m.z);
            var to = new Vector3(m.tx, 0, m.tz);
            switch (m.k)
            {
                case "fireball":
                    Projectile.FireVisual(from + Vector3.up * 1.2f, to + Vector3.up * 1.2f, 20f, new Color(1f, 0.45f, 0.1f), 0.5f, 22f);
                    break;
                case "nova":
                    FxPulse.Ring(from, new Color(0.45f, 0.8f, 1f), 6f, 0.45f);
                    break;
                case "heal":
                    FxPulse.Spawn(from + Vector3.up, new Color(1f, 0.95f, 0.5f), new Vector3(2f, 0.05f, 2f), new Vector3(0.2f, 5f, 0.2f), 0.7f, PrimitiveType.Cylinder);
                    break;
                case "meteor":
                    MeteorFx.Cast(null, to, 0f);
                    break;
                case "cleave":
                    FxPulse.Ring(from, new Color(0.9f, 0.85f, 0.75f), 2.5f, 0.25f);
                    break;
                case "levelup":
                    FxPulse.Ring(from, new Color(1f, 0.85f, 0.2f), 4f, 0.8f);
                    FxPulse.Spawn(from + Vector3.up, new Color(1f, 0.9f, 0.4f), new Vector3(1.5f, 0.1f, 1.5f), new Vector3(0.2f, 8f, 0.2f), 0.9f, PrimitiveType.Cylinder);
                    break;
            }
        }
    }
}
