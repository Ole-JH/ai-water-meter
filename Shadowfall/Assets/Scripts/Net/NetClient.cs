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
        public const int ProtocolVersion = 5;
        public static NetClient I;

        /// <summary>Offline → Connecting → SignedOut (connected) → Account (character select) → Entering → InWorld.</summary>
        public enum ConnState { Offline, Connecting, SignedOut, Account, Entering, InWorld }
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
        // ---- account
        public string AccountName { get; private set; }
        public string Email { get; private set; } = "";
        public bool MailEnabled { get; private set; }
        public NetCharacter[] Characters { get; private set; } = new NetCharacter[0];
        /// <summary>A recovery code to show the player once (new account, used code, or a new one requested).</summary>
        public string RecoveryCode { get; private set; }
        public string RecoveryWhy { get; private set; }
        /// <summary>Waiting for the server to answer an account request.</summary>
        public bool Busy { get; private set; }
        /// <summary>A success message from the server (password changed, email sent...).</summary>
        public string Notice { get; private set; } = "";
        AuthMsg queued;
        public bool SignedIn => State == ConnState.Account || State == ConnState.Entering || State == ConnState.InWorld;
        float nextStateSend, nextSave, lastMessage;
        readonly HashSet<int> seenMonsters = new HashSet<int>();
        readonly List<int> stale = new List<int>();

        void Awake() => I = this;

        // =====================================================================================
        // Connection
        // =====================================================================================

        /// <summary>Sends an account request, connecting first if needed (it goes out once the server says hi).</summary>
        /// <param name="url">ws:// or wss:// URL. Empty in WebGL = the server that hosts the page.</param>
        void Request(string url, AuthMsg msg)
        {
            Status = "";
            Notice = "";
            Busy = true;
            if (State == ConnState.Offline)
            {
                queued = msg;
                State = ConnState.Connecting;
                Status = "Connecting...";
                socket.Connect(url);
            }
            else if (State == ConnState.Connecting) queued = msg;
            else Send(msg);
        }

        bool Need(string value, string what)
        {
            if (!string.IsNullOrEmpty(value)) return true;
            Status = what;
            Notice = "";
            return false;
        }

        public void Login(string url, string user, string pass)
        {
            if (Need(user, "Enter your account name.") && Need(pass, "Enter your password."))
                Request(url, new AuthMsg { t = "login", user = user.Trim(), pass = pass });
        }

        public void Register(string url, string user, string pass, string email)
        {
            if (Need(user, "Choose an account name.") && Need(pass, "Choose a password."))
                Request(url, new AuthMsg { t = "register", user = user.Trim(), pass = pass, email = (email ?? "").Trim() });
        }

        /// <summary>Asks for a reset code by email (by account name or email address).</summary>
        public void ForgotPassword(string url, string who)
        {
            if (Need(who, "Enter your account name or email address.")) Request(url, new AuthMsg { t = "forgot", user = who.Trim() });
        }

        /// <summary>A new password with the recovery code, or a code from an email or an admin.</summary>
        public void ResetPassword(string url, string user, string code, string pass)
        {
            if (Need(user, "Enter your account name.") && Need(code, "Enter your code.") && Need(pass, "Choose a new password."))
                Request(url, new AuthMsg { t = "reset", user = user.Trim(), code = code.Trim(), pass = pass });
        }

        public void Play(string character)
        {
            if (State != ConnState.Account || Busy) return;
            Request(null, new AuthMsg { t = "play", name = character });
            State = ConnState.Entering;
            Status = "Entering the world...";
        }

        public void CreateCharacter(string character, string look)
        {
            if (State != ConnState.Account || Busy || !Need(character, "Name your hero.")) return;
            Request(null, new AuthMsg { t = "create", name = character.Trim(), look = look });
            State = ConnState.Entering;
            Status = "Creating your hero...";
        }

        public void DeleteCharacter(string character, string pass)
        {
            if (State == ConnState.Account && Need(pass, "Enter your password to delete a hero."))
                Request(null, new AuthMsg { t = "delchar", name = character, pass = pass });
        }

        public void ChangePassword(string oldPass, string newPass)
        {
            if (SignedIn && Need(oldPass, "Enter your current password.") && Need(newPass, "Enter a new password."))
                Request(null, new AuthMsg { t = "chpass", old = oldPass, pass = newPass });
        }

        public void SetEmail(string pass, string email)
        {
            if (SignedIn && Need(pass, "Enter your current password.")) Request(null, new AuthMsg { t = "setemail", pass = pass, email = (email ?? "").Trim() });
        }

        public void NewRecoveryCode(string pass)
        {
            if (SignedIn && Need(pass, "Enter your current password.")) Request(null, new AuthMsg { t = "newcode", pass = pass });
        }

        /// <summary>Saves and leaves the world, back to the character list (still logged in).</summary>
        public void BackToCharacterSelect()
        {
            if (State != ConnState.InWorld) return;
            if (Trading) CancelTrade();
            SaveNow();
            Request(null, new AuthMsg { t = "leave" });
        }

        public void DismissRecoveryCode() { RecoveryCode = null; RecoveryWhy = null; }
        public void ClearMessages() { if (!Busy) { Status = ""; Notice = ""; } }

        public const string LoggedOutMessage = "You have logged out. Farewell, hero.";

        /// <summary>Saves, leaves the world and returns to the login screen.</summary>
        public void LogOut()
        {
            if (Trading) CancelTrade();
            Disconnect(LoggedOutMessage);
        }

        public void Disconnect(string reason)
        {
            Duel.Reset();
            Guild.Reset();
            if (State == ConnState.InWorld) SaveNow();
            socket.Close();
            bool wasInWorld = State == ConnState.InWorld;
            State = ConnState.Offline;
            Busy = false;
            queued = null;
            Notice = "";
            AccountName = null;
            Characters = new NetCharacter[0];
            ResetWorldState();
            Status = reason;
            if (wasInWorld) GameManager.I.LeaveWorld();
        }

        /// <summary>Forgets everything about the world session (parties, trades, dungeon, admin tools).</summary>
        void ResetWorldState()
        {
            Party = new NetPartyMember[0];
            PartyInvite = QuestOffer = TradeInvite = null;
            DropTrade(); // the save above already counted anything in the trade window
            AdminTools.Reset();
            DungeonId = 0;
            Dungeon.Exit();
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

        public void SendDuel(string t, int id = 0, bool yes = false) { if (State == ConnState.InWorld) Send(new DuelMsg { t = t, id = id, yes = yes }); }
        public void SendRift(string t, int tier = 0) { if (State == ConnState.InWorld) Send(new RiftMsg { t = t, n = tier }); }
        public void AnswerGuildInvite(bool yes) { if (State == ConnState.InWorld) Send(new GuildAnswerMsg { yes = yes }); }
        public void SendDuelHit(int id, int dmg) { if (State == ConnState.InWorld && id != 0) Send(new DuelMsg { t = "dhit", id = id, dmg = dmg }); }

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

        public void SendEmote(string id)
        {
            if (State == ConnState.InWorld) Send(new EmoteMsg { e = id });
        }

        void HandleEmote(NetMsg m)
        {
            if (m.id == MyId) return;
            var e = EmoteDef.Get(m.e);
            if (e == null || !RemotePlayer.ById.TryGetValue(m.id, out var rp) || rp == null) return;
            rp.Emote(e);
            GameUI.Log(string.Format(e.Other, m.name), Player.EmoteColor);
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
            if (m.id == 0 || m.k == null || !m.k.StartsWith("Greater Rift")) Rift.Left();
            var p = Player.I;
            if (p == null) return;
            if (m.id != 0)
            {
                bool deeper = DungeonId != 0;
                SwitchSpace(m.id, m);
                Exploration.ResetDungeon(m.w, m.h);
                p.TeleportTo(Dungeon.ToWorld(m.start[0], m.start[1]));
                p.Achievements.Once("dungeon", Dungeon.Index.ToString());
                if (Dungeon.Depth >= Dungeon.Depths) p.Achievements.Once("bottom", Dungeon.Index.ToString());
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

        /// <summary>Saves within a couple of seconds (several changes in a row become one save).</summary>
        public void SaveSoon() => nextSave = Mathf.Min(nextSave, Time.time + 2f);

        public void SendAchievement(string id)
        {
            if (State == ConnState.InWorld) Send(new AchMsg { id = id });
        }

        public void SaveNow()
        {
            if (State != ConnState.InWorld || Player.I == null) return;
            Send(new SaveMsg { save = Player.I.ToSave() });
            nextSave = Time.time + 20f;
        }

        /// <summary>Sends our state right away (mounting shows to others without waiting for the next tick).</summary>
        public void SendStateNow() { if (State == ConnState.InWorld) SendState(); }

        void SendState()
        {
            var p = Player.I;
            if (p == null) return;
            Send(new StateMsg
            {
                x = p.transform.position.x - Offset.x, z = p.transform.position.z - Offset.z, ry = p.transform.eulerAngles.y,
                hp = p.Health, mhp = p.MaxHealth, mp = p.Mana, mmp = p.MaxMana, lvl = p.Level, pl = p.Paragon.Level, mv = p.IsMoving, atk = p.IsAttacking, dead = p.IsDead, w = p.OnWall != null,
                body = p.BodyHex, legs = p.LegsHex, weapon = p.WeaponHex, helm = p.HelmHex, mdl = p.Look, wk = p.WeaponKind ?? "", cp = p.ActiveCompanion ?? "", mt = p.Riding != null ? p.Riding.Id : "", ti = p.Achievements.Title != null ? p.Achievements.TitleFrom : "",
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
                Status = "Logging in...";
                Send(new HelloMsg { hash = GameManager.I.GridHash, build = Application.version, ver = ProtocolVersion, wv = WorldGenerator.LayoutVersion });
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
                    if (!string.IsNullOrEmpty(m.reload))
                    {
                        // A newer game is out: reload into it (the browser fetches the new files).
                        if (WebSocketConnection.ReloadPage(m.reload)) { Disconnect("A new version of Shadowfall is out. Loading it..."); break; }
                        Disconnect(Application.platform == RuntimePlatform.WebGLPlayer
                            ? "A new version of Shadowfall is out, but your browser keeps loading the old one. Clear the cache for this site and reload."
                            : "The server runs a newer build (" + m.reload + ") than this client (" + Application.version + "). Rebuild or update the client.");
                        break;
                    }
                    Disconnect(m.err);
                    break;

                case "grid":
                    // The server's map differs from ours within the same build (it shouldn't): play on the server's.
                    GameManager.I.AdoptServerGrid(m.w, m.h, m.cells, m.hash);
                    Send(new WorldMsg { hash = m.hash, w = m.w, h = m.h, cells = "" });
                    break;

                // ---- accounts
                case "hi":
                    MailEnabled = m.mail;
                    State = ConnState.SignedOut;
                    Status = "";
                    if (queued != null) { Send(queued); queued = null; }
                    break;

                case "account":
                    if (State == ConnState.InWorld)
                    {
                        // back from the world to character select
                        ResetWorldState();
                        GameManager.I.LeaveWorld();
                    }
                    State = ConnState.Account;
                    Busy = false;
                    Status = "";
                    Notice = m.msg ?? "";
                    AccountName = m.user;
                    Email = m.email ?? "";
                    MailEnabled = m.mail;
                    Characters = m.chars ?? new NetCharacter[0];
                    if (!string.IsNullOrEmpty(m.rc)) { RecoveryCode = m.rc; RecoveryWhy = m.rcWhy; }
                    break;

                case "autherr":
                    Busy = false;
                    Notice = "";
                    Status = m.err;
                    if (State == ConnState.Entering) State = ConnState.Account;
                    break;

                case "authok":
                    Busy = false;
                    Status = "";
                    Notice = m.msg;
                    break;

                case "rcode":
                    Busy = false;
                    RecoveryCode = m.rc;
                    RecoveryWhy = "changed";
                    break;

                case "welcome":
                    MyId = m.id;
                    DayNight.SyncServerTime(m.now);
                    AdminTools.Reset();
                    AdminTools.IsAdmin = m.admin;
                    if (m.admin) GameUI.Log("You are an admin. Open the admin panel from the game menu (Esc) or type /a in chat.", new Color(1f, 0.5f, 0.9f));
                    State = ConnState.InWorld;
                    Busy = false;
                    Status = "";
                    Notice = "";
                    nextSave = Time.time + 20f;
                    GameManager.I.EnterWorld(m.name, m.hasSave ? m.save : null, m.look);
                    break;

                case "snap": HandleSnapshot(m); break;
                case "tp": // admin teleport
                    Player.I?.TeleportTo(new Vector3(m.x, 0f, m.z) + Offset);
                    break;
                case "clock":
                    DayNight.SyncServerTime(m.now);
                    break;
                case "weather":
                    Weather.Apply(m);
                    break;
                case "admwho":
                    AdminWho = m.items ?? new string[0];
                    break;
                case "mdie": if (Enemy.ById.TryGetValue(m.mid, out var dying)) dying.NetDie(); break;
                case "kill": HandleKill(m); break;
                case "matk": HandleMonsterAttack(m); break;
                case "fx": HandleFx(m); break;
                case "emote": HandleEmote(m); break;
                case "ach": // someone in the party or nearby earned an achievement
                    if (m.id != MyId) GameUI.Log(m.name + " has earned the achievement [" + m.k + "]!", AchievementLog.AchievementColor);
                    break;

                case "chat": HandleChat(m); break;
                case "dungeon": HandleDungeon(m); break;
                case "party":
                    bool wasInParty = InParty;
                    Party = m.pm ?? new NetPartyMember[0];
                    PartyLeader = m.id;
                    if (!wasInParty && InParty)
                    {
                        GameUI.Log("You joined a party. Type /p to talk to your party.", PartyColor);
                        Player.I?.Achievements.Add("parties");
                    }
                    break;
                case "inv": case "drops": case "stock": case "iok": case "ierr":
                    HandleItems(m);
                    break;
                case "tinv": case "topen": case "tupd": case "tmine": case "tok": case "tdone": case "tclose":
                    HandleTrade(m);
                    break;
                case "pinv":
                    PartyInvite = new Offer { From = m.id, Name = m.name, Time = Time.time };
                    GameUI.Log(m.name + " invites you to join a party.", PartyColor);
                    break;
                case "qshare": HandleQuestShare(m); break;

                case "sys":
                    if (m.msg != null && m.msg.StartsWith("[admin] ")) AdminTools.LastResult = m.msg.Substring(8);
                    GameUI.Log(m.msg, new Color(1f, 0.85f, 0.4f));
                    break;
                case "invasion": Invasion.Set(m.iv); break;
                case "wboss": WorldBoss.Set(m.wb); break;
                case "dreq": Duel.Challenged(m.id, m.name); break;
                case "duelring": DuelRing.OnRing(m); break;
                case "guild": Guild.Set(m.g); break;
                case "rinfo": Rift.OnInfo(m); break;
                case "bounties": Bounties.Set(m.items); break;
                case "auction": Auction.Set(m); break;
                case "bounty":
                    Bounties.Finished(m);
                    SpawnDrops(m.drops);
                    break;
                case "rift": Rift.OnState(m); break;
                case "ginv": Guild.Invited(m.name, m.k); break;
                case "duel": Duel.OnState(m); break;
                case "dhit": Duel.OnHit(m.id, Mathf.RoundToInt(m.dmg)); break;
                case "invwin":
                    Invasion.Won(m.k, m.xp);
                    SpawnDrops(m.drops);
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
                    if (m.drops != null) foreach (var d in m.drops) { d.x += ox; d.z += oz; }
                    break;
                case "drops":
                    if (m.drops != null) foreach (var d in m.drops) { d.x += ox; d.z += oz; }
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
                case "g":
                    GameUI.Log("[" + (Guild.Current != null ? Guild.Current.tag : "Guild") + "] " + m.name + ": " + m.msg, Guild.Color);
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
                    else if (!string.IsNullOrEmpty(nm.n)) Enemy.Spawn(nm); // (a partial entry for one we don't know: wait for the full one)
                }
            // Remove monsters that left our area of interest (but let dying ones finish their animation).
            stale.Clear();
            foreach (var kv in Enemy.ById)
                if (!seenMonsters.Contains(kv.Key) && !kv.Value.IsDead && Time.time - kv.Value.LastSeen > 1f) stale.Add(kv.Key);
            foreach (var id in stale) { Destroy(Enemy.ById[id].gameObject); Enemy.ById.Remove(id); }

            if (m.p != null)
                foreach (var np in m.p)
                {
                    if (np.id == MyId) continue;
                    if (RemotePlayer.ById.TryGetValue(np.id, out var known) && known != null) known.Apply(np);
                    else if (!string.IsNullOrEmpty(np.name)) RemotePlayer.Get(np).Apply(np);
                }
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
                var ach = p.Achievements;
                ach.Add("kills");
                ach.Add(AchievementDatabase.KillGroup(def.Name));
                if (!string.IsNullOrEmpty(m.el)) ach.Add("elites");
                if (def.Boss)
                {
                    ach.Once("boss", def.Name);
                    if (WorldBoss.Is(def.Name)) ach.Once("world_boss", def.Name);
                    if (Dungeon.Active) ach.Max("hardest_boss", Dungeon.Difficulty);
                }
                if (!string.IsNullOrEmpty(m.el))
                {
                    Sfx.Play2D("quest_done", 0.5f, 0.9f);
                    GameUI.Banner(m.el + " slain!", Enemy.ChampionColor);
                }
            }
            SpawnDrops(m.drops); // our own loot, rolled by the server
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
                    else if (m.tid < 0 && e != null && Invasion.Active) // an invader battering the town gate
                    {
                        Rampart.Struck(targetPos);
                        Sfx.Play("chop", targetPos + Vector3.up, 0.8f, 0.1f, 40f);
                        SpellFx.Hit(targetPos + Vector3.up * 1.2f, new Color(0.75f, 0.55f, 0.3f), false, 6); // splinters
                    }
                    break;

                case "shot":
                    if (e == null) break;
                    var from = e.transform.position + Vector3.up * e.Height * 0.6f + e.transform.forward * 0.5f;
                    var to = targetPos + Vector3.up;
                    bool boss = e.Def.Boss;
                    if (targetIsMe && p != null && !p.IsDead)
                        Projectile.Fire(e, from, p.transform.position + Vector3.up, 13f, m.dmg, e.Def.ProjectileColor,
                            boss ? 0.7f : 0.35f, boss ? 1.8f : 0f, 18f).WithTrail(e.Def.Name == "Skeleton Archer" ? SpellFx.Trail.Arrow : SpellFx.Trail.Magic)
                            .OverWalls(p.OnWall != null); // shot up at us on a town wall
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
                    if (e != null && WorldBoss.Is(e.Def.Name)) GameUI.Banner(e.Def.Name + " calls for aid!", WorldBoss.Color);
                    else GameUI.Banner("Lich King: \"Rise, my servants!\"", new Color(0.6f, 0.85f, 1f));
                    break;

                case "phase": // a world boss's armour plate breaks off (dmg: plates left)
                    {
                        var presence = e != null ? e.GetComponent<BossPresence>() : null;
                        if (presence != null) presence.BreakTo(Mathf.RoundToInt(m.dmg));
                    }
                    break;

                case "warn": // a world boss winds up a slam: a red ring on the ground, get out of it
                {
                    var at = new Vector3(m.x, 0.05f, m.z);
                    SpellFx.Ring(at, new Color(1f, 0.15f, 0.05f), WorldBoss.SlamRadius, WorldBoss.SlamWindup);
                    SpellFx.Ring(at, new Color(1f, 0.45f, 0.1f), WorldBoss.SlamRadius * 0.6f, WorldBoss.SlamWindup);
                    Sfx.Play("roar", at, 1f, 0.05f, 60f);
                    break;
                }

                case "slam":
                {
                    var at = new Vector3(m.x, 0f, m.z);
                    SpellFx.Shockwave(at, new Color(1f, 0.6f, 0.3f), WorldBoss.SlamRadius);
                    SpellFx.Dust(at, WorldBoss.SlamRadius);
                    BossPresence.Crater(at, WorldBoss.SlamRadius, e != null ? e.Def.Name : null);
                    Sfx.Play("boom", at, 1f, 0.05f, 60f);
                    if (p != null && Factory.FlatDistance(p.transform.position, at) < 25f) CameraRig.Shake(0.35f);
                    if (p != null && !p.IsDead && Factory.FlatDistance(p.transform.position, at) < WorldBoss.SlamRadius + 0.3f) p.TakeDamage(m.dmg, e);
                    break;
                }
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
