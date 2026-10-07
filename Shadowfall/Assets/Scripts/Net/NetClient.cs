using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Talks to the Shadowfall server: login, world snapshots (monsters + other players),
    /// combat events, chat, spell effects and character saves.
    /// </summary>
    public partial class NetClient : MonoBehaviour
    {
        public const int ProtocolVersion = 4;
        public static NetClient I;

        public enum ConnState { Offline, Connecting, LoggingIn, InWorld }
        public ConnState State { get; private set; } = ConnState.Offline;
        public string Status { get; private set; } = "";
        public int MyId { get; private set; }
        public int PlayersOnline { get; private set; }

        // ---- party
        public NetPartyMember[] Party { get; private set; } = new NetPartyMember[0];
        public int PartyLeader { get; private set; }
        public bool InParty => Party.Length > 0;

        // ---- dungeons: positions on the wire are instance-local; the client keeps dungeons at Dungeon.Origin
        public int DungeonId { get; private set; }
        Vector3 Offset => DungeonId != 0 ? Dungeon.Origin : Vector3.zero;
        public bool IsLeader => InParty && PartyLeader == MyId;

        /// <summary>A pending party invitation or shared quest, shown as a popup.</summary>
        public class Offer { public int From; public string Name; public QuestDef Quest; public float Time; }
        public Offer PartyInvite { get; private set; }
        public Offer QuestOffer { get; private set; }

        readonly WebSocketConnection socket = new WebSocketConnection();
        string pendingName, pendingPass, pendingLook;
        float nextStateSend, nextSave, lastMessage;
        readonly HashSet<int> seenMonsters = new HashSet<int>();
        readonly List<int> stale = new List<int>();

        void Awake() => I = this;

        // =====================================================================================
        // Connection
        // =====================================================================================

        /// <param name="url">ws:// or wss:// URL. Empty in WebGL = the server that hosts the page.</param>
        public void Login(string url, string name, string password, string look)
        {
            pendingLook = look;
            name = (name ?? "").Trim();
            if (name.Length < 3 || name.Length > 16) { Status = "Name must be 3-16 characters."; return; }
            if (string.IsNullOrEmpty(password) || password.Length < 4) { Status = "Password must be at least 4 characters."; return; }
            pendingName = name;
            pendingPass = password;
            State = ConnState.Connecting;
            Status = "Connecting...";
            socket.Connect(url);
        }

        public const string LoggedOutMessage = "You have logged out. Farewell, hero.";

        /// <summary>Saves, leaves the world and returns to the login screen.</summary>
        public void LogOut()
        {
            if (Trading) CancelTrade();
            Disconnect(LoggedOutMessage);
        }

        public void Disconnect(string reason)
        {
            if (State == ConnState.InWorld) SaveNow();
            socket.Close();
            bool wasInWorld = State == ConnState.InWorld;
            State = ConnState.Offline;
            Party = new NetPartyMember[0];
            PartyInvite = QuestOffer = TradeInvite = null;
            DropTrade(); // the save above already counted anything in the trade window
            AdminTools.Reset();
            DungeonId = 0;
            Dungeon.Exit();
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

        public void SendStun(int monsterId, float duration)
        {
            if (State == ConnState.InWorld) Send(new StunMsg { mid = monsterId, dur = duration });
        }

        /// <summary>Smoke Bomb: monsters lose track of us for a while.</summary>
        public void SendVanish(float duration)
        {
            if (State == ConnState.InWorld) Send(new VanishMsg { dur = duration });
        }

        public void SendChat(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || State != ConnState.InWorld) return;
            if (text.Length > 200) text = text.Substring(0, 200);
            Send(new ChatMsg { msg = text });
        }

        // ---- party

        void PartySend(string t, string name = null, int id = 0, string q = null)
        {
            if (State == ConnState.InWorld) Send(new PartyCmd { t = t, name = name, id = id, q = q });
        }

        public void InviteToParty(string name) => PartySend("pinvite", name);
        public void LeaveParty() => PartySend("pleave");
        public void KickFromParty(int id) => PartySend("pkick", id: id);
        public void ShareQuest(QuestDef q) => PartySend("pshare", q: q.Id);

        public void AnswerPartyInvite(bool accept)
        {
            PartySend(accept ? "paccept" : "pdecline");
            PartyInvite = null;
        }

        public void AnswerQuestOffer(bool accept)
        {
            var offer = QuestOffer;
            QuestOffer = null;
            if (accept && offer != null && Player.I != null) Player.I.Quests.Accept(offer.Quest);
        }

        public bool IsPartyMember(int id)
        {
            foreach (var m in Party) if (m.id == id) return true;
            return false;
        }

        public void SendFx(string kind, Vector3 from, Vector3 to)
        {
            from -= Offset;
            to -= Offset;
            if (State == ConnState.InWorld) Send(new FxMsg { k = kind, x = from.x, z = from.z, tx = to.x, tz = to.z });
        }

        // ---- dungeons

        public void EnterDungeon(int index, int difficulty) { if (State == ConnState.InWorld) Send(new DungeonCmd { t = "denter", d = index, df = difficulty }); }
        public void DescendDungeon() { if (State == ConnState.InWorld) Send(new DungeonCmd { t = "dstairs" }); }

        /// <param name="toTown">True after dying: the hero respawns in Hollowmere (we leave right away).</param>
        public void LeaveDungeon(bool toTown)
        {
            if (DungeonId == 0 || State != ConnState.InWorld) return;
            Send(new DungeonCmd { t = "dleave", town = toTown });
            if (toTown) SwitchSpace(0, null);
        }

        /// <summary>Moves the hero between the overworld and a dungeon level, clearing what belonged to the old space.</summary>
        void SwitchSpace(int dungeonId, NetMsg layout)
        {
            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) Destroy(e.gameObject);
            foreach (var r in FindObjectsByType<RemotePlayer>(FindObjectsSortMode.None)) Destroy(r.gameObject);
            foreach (var l in FindObjectsByType<LootDrop>(FindObjectsSortMode.None)) Destroy(l.gameObject);
            Enemy.ById.Clear();
            RemotePlayer.ById.Clear();
            DungeonId = dungeonId;
            if (layout != null) Dungeon.Enter(layout); else Dungeon.Exit();
        }

        void HandleDungeon(NetMsg m)
        {
            var p = Player.I;
            if (p == null) return;
            if (m.id != 0)
            {
                bool deeper = DungeonId != 0;
                SwitchSpace(m.id, m);
                Exploration.ResetDungeon(m.w, m.h);
                p.TeleportTo(Dungeon.ToWorld(m.start[0], m.start[1]));
                GameUI.Banner(Dungeon.ZoneName, new Color(1f, 0.55f, 0.3f));
                Sfx.Play2D(deeper ? "rubble" : "gong", 0.6f);
                if (m.boss != null && m.boss.Length == 2) GameUI.Log("You sense a terrible presence. " + Dungeon.Def.Boss + " waits on this level.", new Color(1f, 0.45f, 0.35f));
            }
            else
            {
                if (DungeonId != 0) SwitchSpace(0, null);
                p.TeleportTo(new Vector3(m.x, 0f, m.z));
            }
        }

        /// <summary>Players online, from the admin "who" command: "id|name|level|where".</summary>
        public string[] AdminWho = new string[0];

        public void SendAdmin(AdminCmd cmd)
        {
            if (State == ConnState.InWorld) Send(cmd);
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
                x = p.transform.position.x - Offset.x, z = p.transform.position.z - Offset.z, ry = p.transform.eulerAngles.y,
                hp = p.Health, mhp = p.MaxHealth, lvl = p.Level, mv = p.IsMoving, atk = p.IsAttacking, dead = p.IsDead,
                body = p.BodyHex, legs = p.LegsHex, weapon = p.WeaponHex, helm = p.HelmHex, mdl = p.Look, wk = p.WeaponKind ?? "", cp = p.ActiveCompanion ?? "",
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
                Send(new HelloMsg { name = pendingName, pass = pendingPass, hash = GameManager.I.GridHash, ver = ProtocolVersion, wv = WorldGenerator.LayoutVersion });
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
            if (DungeonId != 0) ShiftIn(m);

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
                    DayNight.SyncServerTime(m.now);
                    AdminTools.Reset();
                    AdminTools.IsAdmin = m.admin;
                    if (m.admin) GameUI.Log("You are an admin. Open the admin panel from the game menu (Esc) or type /a in chat.", new Color(1f, 0.5f, 0.9f));
                    State = ConnState.InWorld;
                    Status = "";
                    nextSave = Time.time + 20f;
                    GameManager.I.EnterWorld(pendingName, m.hasSave ? m.save : null, pendingLook);
                    pendingPass = null;
                    break;

                case "snap": HandleSnapshot(m); break;
                case "tp": // admin teleport
                    Player.I?.TeleportTo(new Vector3(m.x, 0f, m.z) + Offset);
                    break;
                case "clock":
                    DayNight.SyncServerTime(m.now);
                    break;
                case "admwho":
                    AdminWho = m.items ?? new string[0];
                    break;
                case "mdie": if (Enemy.ById.TryGetValue(m.mid, out var dying)) dying.NetDie(); break;
                case "kill": HandleKill(m); break;
                case "matk": HandleMonsterAttack(m); break;
                case "fx": HandleFx(m); break;

                case "chat": HandleChat(m); break;
                case "dungeon": HandleDungeon(m); break;
                case "party":
                    bool wasInParty = InParty;
                    Party = m.pm ?? new NetPartyMember[0];
                    PartyLeader = m.id;
                    if (!wasInParty && InParty) GameUI.Log("You joined a party. Type /p to talk to your party.", PartyColor);
                    break;
                case "tinv": case "topen": case "tupd": case "tok": case "tdone": case "tclose":
                    HandleTrade(m);
                    break;
                case "pinv":
                    PartyInvite = new Offer { From = m.id, Name = m.name, Time = Time.time };
                    GameUI.Log(m.name + " invites you to join a party.", PartyColor);
                    break;
                case "qshare": HandleQuestShare(m); break;

                case "sys":
                    GameUI.Log(m.msg, new Color(1f, 0.85f, 0.4f));
                    break;

                case "leave":
                    if (RemotePlayer.ById.TryGetValue(m.id, out var gone)) Destroy(gone.gameObject);
                    break;
            }
        }

        /// <summary>Instance-local dungeon positions to client world positions.</summary>
        void ShiftIn(NetMsg m)
        {
            float ox = Dungeon.Origin.x, oz = Dungeon.Origin.z;
            switch (m.t)
            {
                case "snap":
                    if (m.m != null) foreach (var nm in m.m) { nm.x += ox; nm.z += oz; }
                    if (m.p != null) foreach (var np in m.p) { np.x += ox; np.z += oz; }
                    break;
                case "matk": case "fx":
                    m.x += ox; m.z += oz; m.tx += ox; m.tz += oz;
                    break;
                case "kill":
                    m.x += ox; m.z += oz;
                    break;
                case "party":
                    if (m.pm != null) foreach (var pm in m.pm) if (pm.di == DungeonId) { pm.x += ox; pm.z += oz; }
                    break;
            }
        }

        public static readonly Color PartyColor = new Color(0.55f, 0.75f, 1f);
        public static readonly Color WhisperColor = new Color(1f, 0.55f, 0.9f);

        void HandleChat(NetMsg m)
        {
            switch (m.ch)
            {
                case "p":
                    GameUI.Log("[Party] " + m.name + ": " + m.msg, PartyColor);
                    Bubble(m.id, m.msg);
                    break;
                case "w":
                    GameUI.Log(m.name + " whispers: " + m.msg, WhisperColor);
                    GameUI.I?.SetReplyTarget(m.name);
                    break;
                case "wto":
                    GameUI.Log("To " + m.name + ": " + m.msg, WhisperColor);
                    break;
                default:
                    GameUI.Log("[" + m.name + "]: " + m.msg, m.id == MyId ? new Color(0.85f, 0.85f, 1f) : Color.white);
                    Bubble(m.id, m.msg);
                    break;
            }
        }

        void Bubble(int id, string text)
        {
            if (id == MyId && Player.I != null) Speech.Say(Player.I.transform, 2.6f, text);
            else if (RemotePlayer.ById.TryGetValue(id, out var speaker) && speaker != null) Speech.Say(speaker.transform, 2.6f, text);
        }

        void HandleQuestShare(NetMsg m)
        {
            var p = Player.I;
            var q = QuestDatabase.Find(m.k);
            if (p == null || q == null) return;
            if (p.Quests.IsActive(q.Id)) { GameUI.Log(m.name + " shared \"" + q.Title + "\", which you already have.", PartyColor); return; }
            if (p.Quests.Completed.Contains(q.Id)) { GameUI.Log(m.name + " shared \"" + q.Title + "\", which you have completed.", PartyColor); return; }
            if (p.Level < q.MinLevel) { GameUI.Log(m.name + " shared \"" + q.Title + "\", but you need level " + q.MinLevel + ".", PartyColor); return; }
            QuestOffer = new Offer { From = m.id, Name = m.name, Quest = q, Time = Time.time };
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
                ItemPowers.OnKill(p, pos);
                p.AddXp(m.xp);
                p.Quests.OnKill(def.Name);
                if (!string.IsNullOrEmpty(m.el))
                {
                    Enemy.RollEliteLoot(def, m.l, pos, m.lb);
                    Sfx.Play2D("quest_done", 0.5f, 0.9f);
                    GameUI.Banner(m.el + " slain!", Enemy.ChampionColor);
                }
                else Enemy.RollLoot(def, m.l, pos, m.lb);
            }
            if (def.Boss) GameUI.Banner(def.Name + " has been slain!", new Color(1f, 0.55f, 0.1f));
        }

        void HandleMonsterAttack(NetMsg m)
        {
            var p = Player.I;
            Enemy.ById.TryGetValue(m.mid, out var e);
            var targetPos = new Vector3(m.x, 0, m.z);
            bool targetIsMe = m.tid == MyId;
            if (e != null && m.k != "blink" && m.k != "explode") e.PlayAttack(targetPos);

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
                            boss ? 0.7f : 0.35f, boss ? 1.8f : 0f, 18f).WithTrail(e.Def.Name == "Skeleton Archer" ? SpellFx.Trail.Arrow : SpellFx.Trail.Magic);
                    else
                        Projectile.FireVisual(from, to, 13f, e.Def.ProjectileColor, boss ? 0.7f : 0.35f, 18f)
                            .WithTrail(e.Def.Name == "Skeleton Archer" ? SpellFx.Trail.Arrow : SpellFx.Trail.Magic);
                    break;

                case "nova":
                    Sfx.Play("frost_cast", e != null ? e.transform.position : targetPos, 0.9f, 0.05f, 50f);
                    var c = new Color(0.4f, 0.85f, 1f);
                    var center = e != null ? e.transform.position : targetPos;
                    SpellFx.FrostNova(center, 6f);
                    if (p != null && !p.IsDead && Factory.FlatDistance(p.transform.position, center) < 6.5f) p.TakeDamage(m.dmg, e);
                    break;

                case "blink":
                {
                    var purple = new Color(0.75f, 0.35f, 1f);
                    SpellFx.Explosion(new Vector3(m.tx, 0.8f, m.tz), purple, 0.8f, false);
                    SpellFx.Explosion(new Vector3(m.x, 0.8f, m.z), purple, 0.8f, false);
                    Sfx.Play("frost_cast", new Vector3(m.x, 0, m.z), 0.5f, 0.2f);
                    break;
                }

                case "explode":
                {
                    // Fire Enchanted elites explode when they die.
                    var at = new Vector3(m.x, 0f, m.z);
                    SpellFx.Explosion(at + Vector3.up * 0.6f, new Color(1f, 0.4f, 0.05f), 3.2f, true);
                    Sfx.Play("explosion", at, 0.9f, 0.1f);
                    CameraRig.Shake(0.2f);
                    if (p != null && !p.IsDead && Factory.FlatDistance(p.transform.position, at) < 3.5f) p.TakeDamage(m.dmg, null);
                    break;
                }

                case "summon":
                    Sfx.Play2D("roar", 0.8f, 0.8f);
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
                    Sfx.Play("fire_cast", from, 0.5f, 0.1f);
                    Projectile.FireVisual(from + Vector3.up * 1.2f, to + Vector3.up * 1.2f, 20f, new Color(1f, 0.45f, 0.1f), 0.5f, 22f).WithTrail(SpellFx.Trail.Fire);
                    break;
                case "nova":
                    Sfx.Play("frost_cast", from, 0.7f);
                    SpellFx.FrostNova(from, 6f);
                    break;
                case "heal":
                    Sfx.Play("holy_cast", from, 0.6f, 0.02f);
                    SpellFx.HolyLight(from);
                    break;
                case "meteor":
                    MeteorFx.Cast(null, to, 0f);
                    break;
                case "cleave":
                    Sfx.Play("swing_heavy", from, 0.6f);
                    var dir = Factory.Flat(to - from);
                    SpellFx.Cleave(from, dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : Quaternion.identity, 3.4f, new Color(0.9f, 0.85f, 0.75f));
                    break;
                case "levelup":
                    Sfx.Play("levelup", from, 0.7f, 0f);
                    SpellFx.LevelUp(from);
                    break;
                default:
                    AbilityFx.Remote(m.k, from, to);
                    break;
            }
        }
    }
}
