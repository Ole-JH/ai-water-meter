using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// All UI, drawn with Unity's immediate-mode GUI (no Canvas setup needed, works in WebGL) using the
    /// fantasy skin in <see cref="UISkin"/>. Login, HUD (orbs, action bar, unit frames, minimap, quest
    /// tracker, chat, menu), windows (bags, character, skills, quests, map, help), NPC dialogs, vendor, crafting.
    /// </summary>
    public partial class GameUI : MonoBehaviour
    {
        public static GameUI I;

        // ---- state read by gameplay code
        public bool MouseOverUI { get; private set; }
        public bool ChatOpen { get; private set; }
        public bool KeyboardCaptured => ChatOpen || Player.I == null || tradeGoldFocused || menu != MenuPage.None || (showAdmin && adminFieldFocused);
        public bool BlocksWorldInput => Player.I == null || Player.I.IsDead || showMap || menu != MenuPage.None;

        // ---- windows
        bool showBags, showChar, showSkills, showQuests, showMap, showHelp, showTalents;
        Npc dialogNpc;
        GUIStyle chatFieldStyle;
        CraftingStation craftStation;

        // ---- login
        string loginName = "", loginPass = "", serverUrl = "", loginLook = "Knight";

        // ---- chat
        string chatText = "";
        string replyTo;

        // ---- social
        RemotePlayer menuPlayer;
        Vector2 menuPos;

        // ---- layout
        const float RefHeight = 900f;
        float scale = 1f, VW, VH;
        readonly List<Rect> blockRects = new List<Rect>();
        string tooltip;

        // ---- cursor
        enum CursorKind { None, Default, Attack, Interact }
        CursorKind cursor = CursorKind.None;
        Texture2D cursorDefault, cursorAttack, cursorInteract;

        // ---- feedback
        struct FloatText { public Vector3 Pos; public string Text; public Color Color; public float Time, Size; }
        struct LogLine { public string Text; public Color Color; public float Time; }
        static readonly List<FloatText> floats = new List<FloatText>();
        static readonly List<LogLine> log = new List<LogLine>();
        static string bannerText;
        static Color bannerColor;
        static float bannerTime = -99f;

        static readonly string[] heroNames = { "Knight", "Barbarian", "Mage", "Rogue" };
        static readonly string[] heroBlurbs =
        {
            "Plate and steel. Holds the line.",
            "Fury and an axe. Asks questions later.",
            "Robes and arcane fire.",
            "Quick blades, quicker exits."
        };

        void Awake()
        {
            I = this;
            try
            {
                loginName = PlayerPrefs.GetString("sf_name", "");
                serverUrl = PlayerPrefs.GetString("sf_server", "ws://localhost:7341/ws");
                loginLook = PlayerPrefs.GetString("sf_look", "Knight");
            }
            catch (System.Exception) { }
            cursorDefault = Resources.Load<Texture2D>("UI/Cursors/cursorGauntlet_bronze");
            cursorAttack = Resources.Load<Texture2D>("UI/Cursors/cursorSword_gold");
            cursorInteract = Resources.Load<Texture2D>("UI/Cursors/cursorHand_beige");
        }

        // =====================================================================================
        // Static API used by gameplay code
        // =====================================================================================

        public static void Float(Vector3 worldPos, string text, Color color, float size = 1f)
        {
            floats.Add(new FloatText { Pos = worldPos + new Vector3(Random.Range(-0.3f, 0.3f), 0, 0), Text = text, Color = color, Time = Time.time, Size = size });
            if (floats.Count > 80) floats.RemoveAt(0);
        }

        public static void Log(string text, Color color)
        {
            log.Add(new LogLine { Text = text, Color = color, Time = Time.time });
            if (log.Count > 60) log.RemoveAt(0);
        }

        public static void Banner(string text, Color color)
        {
            bannerText = text;
            bannerColor = color;
            bannerTime = Time.time;
        }

        public void OpenDialog(Npc npc)
        {
            dialogNpc = npc;
            craftStation = null;
            if (npc.Role == NpcRole.Vendor)
            {
                showBags = true;
                if (npc.Shop == null) npc.SellsAs(VendorKind.General);
                if (Player.I != null) npc.Shop.Refresh(Player.I.Level);
            }
        }

        public bool IsTalkingTo(Npc npc) => dialogNpc == npc;

        public void OpenCrafting(CraftingStation s)
        {
            craftStation = s;
            dialogNpc = null;
        }

        // =====================================================================================
        // Update: hotkeys, hover detection, cursor, auto-closing dialogs
        // =====================================================================================

        void Update()
        {
            scale = Mathf.Max(0.4f, Screen.height / RefHeight * GameSettings.UiScale);
            var mp = GameInput.MousePosition;
            var guiMouse = new Vector2(mp.x / scale, (Screen.height - mp.y) / scale);
            bool over = false;
            foreach (var r in blockRects) if (r.Contains(guiMouse)) { over = true; break; }
            MouseOverUI = over;

            var p = Player.I;
            UpdateCursor(p);
            if (p == null) { ChatOpen = false; return; }
            CheckNews(p);

            if (!ChatOpen)
            {
                bool before = showBags | showChar | showSkills | showQuests | showMap | showHelp | showTalents | menu != MenuPage.None;
                bool questsBefore = showQuests;
                WindowKeys();
                AdminKeys();
                bool after = showBags | showChar | showSkills | showQuests | showMap | showHelp | showTalents | menu != MenuPage.None;
                if (showQuests && !questsBefore) Sfx.Play2D("book", 0.5f);
                else if (after != before) Sfx.Play2D(after ? "ui_open" : "ui_close", 0.4f);
            }

            if (dialogNpc != null && Factory.FlatDistance(p.transform.position, dialogNpc.transform.position) > 5f) dialogNpc = null;
            if (craftStation != null && Factory.FlatDistance(p.transform.position, craftStation.transform.position) > 5f) craftStation = null;
        }

        void WindowKeys()
        {
            {
                if (GameInput.Down(GKey.I) || GameInput.Down(GKey.B)) showBags = !showBags;
                if (GameInput.Down(GKey.C)) showChar = !showChar;
                if (GameInput.Down(GKey.K)) showSkills = !showSkills;
                if (GameInput.Down(GKey.L)) showQuests = !showQuests;
                if (GameInput.Down(GKey.T)) showTalents = !showTalents;
                if (GameInput.Down(GKey.M)) showMap = !showMap;
                if (GameInput.Down(GKey.F1) || GameInput.Down(GKey.H)) showHelp = !showHelp;
                if (GameInput.Down(GKey.Escape))
                {
                    if (chooseDungeon >= 0) chooseDungeon = -1;
                    else if (dialogNpc != null || craftStation != null) { dialogNpc = null; craftStation = null; }
                    else if (tradeOpen) NetClient.I?.CancelTrade();
                    else if (menu != MenuPage.None) menu = menu == MenuPage.Main ? MenuPage.None : MenuPage.Main;
                    else if (showNews) CloseNews(Player.I);
                    else if (showBags | showChar | showSkills | showQuests | showMap | showHelp | showTalents | showStash | showAdmin)
                        showBags = showChar = showSkills = showQuests = showMap = showHelp = showTalents = showStash = showAdmin = false;
                    else menu = MenuPage.Main; // nothing to close: open the game menu
                }
            }
        }

        void UpdateCursor(Player p)
        {
            var want = CursorKind.Default;
            if (p != null && !MouseOverUI)
            {
                if (p.HoveredEnemy != null) want = CursorKind.Attack;
                else if (p.HoveredInteractable != null) want = CursorKind.Interact;
            }
            if (want == cursor) return;
            cursor = want;
            var tex = want == CursorKind.Attack ? cursorAttack : want == CursorKind.Interact ? cursorInteract : cursorDefault;
            try { Cursor.SetCursor(tex, Vector2.zero, CursorMode.Auto); }
            catch (System.Exception) { /* texture not imported as a cursor: keep the OS cursor */ }
        }

        // =====================================================================================
        // OnGUI
        // =====================================================================================

        void OnGUI()
        {
            UISkin.Init();
            if (Event.current.type == EventType.Layout) blockRects.Clear();
            scale = Mathf.Max(0.4f, Screen.height / RefHeight * GameSettings.UiScale);
            VW = Screen.width / scale;
            VH = Screen.height / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            tooltip = null;

            HandleChatKeys();

            var p = Player.I;
            if (p == null)
            {
                DrawLogin();
                DrawLog(false);
                return;
            }

            // World-space labels only matter when painting or clicking; OnGUI also runs for layout, key and
            // mouse-move events, and projecting every nameplate for those is wasted work.
            var et = Event.current.type;
            bool paintOrClick = et == EventType.Repaint || et == EventType.MouseDown || et == EventType.MouseUp;
            DrawVignette();
            if (paintOrClick)
            {
                DrawWorldOverlays(p);
                DrawBubbles(p);
                DrawFloatingText();
            }
            DrawUnitFrames(p);
            DrawPartyFrames(p);
            DrawMinimap(p);
            DrawQuestTracker(p);
            DrawActionBar(p);
            DrawBuffs(p);
            DrawMenuButtons();
            DrawLog(true);

            if (showBags) DrawBags(p);
            if (showChar) DrawCharacter(p);
            if (showSkills) DrawSkills(p);
            if (showQuests) DrawQuestLog(p);
            if (showTalents) DrawTalents(p);
            if (showStash) DrawStash(p);
            if (tradeOpen) DrawTrade(p);
            if (showAdmin) DrawAdmin(p);
            if (chooseDungeon >= 0) DrawDifficultyPicker(p);
            else tradeGoldFocused = false;
            if (dialogNpc != null) DrawDialog(p);
            if (craftStation != null) DrawCrafting(p);
            if (showHelp) DrawHelp();
            if (showNews) DrawNews(p);
            if (showMap) DrawWorldMap(p);
            if (menuPlayer != null) DrawPlayerMenu();
            DrawOffers();
            if (p.IsDead) DrawDeath(p);
            if (menu != MenuPage.None) DrawGameMenu(p);

            DrawBanner();
            if (GameSettings.ShowFps && Event.current.type == EventType.Repaint)
            {
                float f = FpsMeter.Fps;
                UISkin.Shadowed(new Rect(VW / 2 - 60, 4, 120, 20), Mathf.RoundToInt(f) + " fps", UISkin.SmallCenter,
                    f >= 50f ? new Color(0.5f, 1f, 0.5f) : f >= 30f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.3f));
            }
            DrawTooltip();
        }

        // =====================================================================================
        // Login
        // =====================================================================================


        // =====================================================================================
        // World-space overlays: nameplates, health bars, loot labels, quest markers
        // =====================================================================================

        bool WorldToGui(Vector3 world, out Vector2 gui)
        {
            var sp = GameManager.I.Cam.WorldToScreenPoint(world);
            gui = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
            return sp.z > 0f;
        }

        static Texture2D vignette;
        static string animStatus;

        /// <summary>Darkened screen edges, Diablo style. Stronger at night.</summary>
        void DrawVignette()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (vignette == null)
            {
                const int n = 128;
                vignette = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Vignette" };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                        float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, d));
                        px[y * n + x] = new Color(0f, 0f, 0.02f, a);
                    }
                vignette.SetPixels32(px);
                vignette.Apply();
            }
            GUI.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 0.85f, DayNight.Night));
            GUI.DrawTexture(new Rect(0, 0, VW, VH), vignette, ScaleMode.StretchToFill);
            // Low life: a red pulse at the screen edges (faster the closer to death).
            var hero = Player.I;
            if (hero != null && !hero.IsDead && hero.Health < hero.MaxHealth * 0.3f)
            {
                float danger = 1f - hero.Health / (hero.MaxHealth * 0.3f);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(4f, 9f, danger));
                GUI.color = new Color(0.9f, 0.05f, 0.02f, (0.35f + 0.45f * danger) * (0.6f + 0.4f * pulse));
                GUI.DrawTexture(new Rect(0, 0, VW, VH), vignette, ScaleMode.StretchToFill);
            }
            GUI.color = Color.white;
        }

        // =====================================================================================
        // Speech bubbles, party frames, social popups
        // =====================================================================================

        static GUIStyle bubbleStyle;

        void DrawBubbles(Player p)
        {
            if (bubbleStyle == null) bubbleStyle = new GUIStyle(UISkin.Ink14) { wordWrap = true, alignment = TextAnchor.MiddleCenter, richText = false };
            var list = Speech.Active;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].Who == null || Time.time - list[i].Start > list[i].Duration) list.RemoveAt(i);

            foreach (var b in list)
            {
                if (Factory.FlatDistance(b.Who.position, p.transform.position) > 35f) continue;
                if (!WorldToGui(b.Who.position + Vector3.up * b.Height, out var g)) continue;
                float age = Time.time - b.Start;
                float a = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((b.Duration - age) / 0.5f);
                var content = new GUIContent(b.Text);
                float w = Mathf.Min(240f, bubbleStyle.CalcSize(content).x + 6f);
                float h = bubbleStyle.CalcHeight(content, w);
                var r = new Rect(g.x - w / 2 - 14, g.y - h - 46, w + 28, h + 20);
                GUI.color = new Color(1, 1, 1, a);
                UISkin.Box(r, UISkin.Tooltip);
                GUI.color = new Color(0.05f, 0.04f, 0.03f, a * 0.95f); // a little tail pointing at the speaker
                GUI.DrawTexture(new Rect(g.x - 5, r.yMax - 4, 10, 6), UISkin.White);
                GUI.DrawTexture(new Rect(g.x - 2.5f, r.yMax + 2, 5, 5), UISkin.White);
                GUI.color = new Color(1, 1, 1, a);
                GUI.Label(new Rect(r.x + 14, r.y + 10, w, h), content, bubbleStyle);
                GUI.color = Color.white;
            }
        }

        void DrawPartyFrames(Player p)
        {
            var net = NetClient.I;
            if (!net.InParty) return;
            float y = 12 + 96 + 10 + (p.StatPoints > 0 ? 46 : 0);
            foreach (var m in net.Party)
            {
                if (m.id == net.MyId) continue;
                float hp = m.hp, mhp = m.mhp;
                bool near = RemotePlayer.ById.TryGetValue(m.id, out var rp) && rp != null;
                if (near) { hp = rp.Health; mhp = rp.MaxHealth; }
                var r = new Rect(12, y, 250, 58);
                UISkin.Box(r, UISkin.PanelPlain);
                Block(r);
                var icon = new Rect(r.x + 9, r.y + 9, 40, 40);
                UISkin.Box(icon, UISkin.Slot);
                UISkin.IconInSlot(icon, UISkin.Icon((m.mdl ?? "knight").ToLower()), m.dead ? new Color(0.5f, 0.5f, 0.5f) : Color.white, 3);
                bool leader = m.id == net.PartyLeader;
                UISkin.Shadowed(new Rect(r.x + 58, r.y + 7, 160, 20), m.name + "  " + m.lvl + (leader ? "  (Leader)" : ""),
                    UISkin.Small, leader ? UISkin.Gold : near ? new Color(0.45f, 1f, 0.5f) : UISkin.Muted);
                UISkin.Bar(new Rect(r.x + 58, r.y + 30, 180, 16), mhp > 0 ? hp / mhp : 0f, "Red",
                    m.dead ? "Dead" : Mathf.CeilToInt(hp) + " / " + Mathf.CeilToInt(mhp), new Color(0.75f, 0.12f, 0.1f));
                if (net.IsLeader)
                {
                    var kick = new Rect(r.xMax - 30, r.y + 6, 22, 22);
                    if (UISkin.Btn(kick, "x", UISkin.SquareButton)) net.KickFromParty(m.id);
                    if (kick.Contains(Event.current.mousePosition)) tooltip = "Remove " + m.name + " from the party";
                }
                y += 62;
            }
            var leave = new Rect(12, y, 130, 32);
            Block(leave);
            if (UISkin.Btn(leave, "Leave Party", UISkin.Button)) net.LeaveParty();
        }

        void DrawPlayerMenu()
        {
            var rp = menuPlayer;
            if (rp == null) { menuPlayer = null; return; }
            var net = NetClient.I;
            bool canInvite = !net.IsPartyMember(rp.Id) && (!net.InParty || net.IsLeader);
            var r = new Rect(menuPos.x - 100, menuPos.y + 10, 200, 60 + (canInvite ? 44 : 0) + 132);
            UISkin.Box(r, UISkin.PanelPlain);
            Block(r);
            UISkin.Shadowed(new Rect(r.x, r.y + 12, r.width, 24), rp.Name, UISkin.HeadingCenter, UISkin.Gold);
            float y = r.y + 48;
            if (canInvite)
            {
                if (UISkin.Btn(new Rect(r.x + 16, y, r.width - 32, 38), "Invite to Party", UISkin.Button)) { net.InviteToParty(rp.Name); menuPlayer = null; }
                y += 44;
            }
            if (UISkin.Btn(new Rect(r.x + 16, y, r.width - 32, 38), "Whisper", UISkin.Button)) { OpenChat("/w " + rp.Name + " "); menuPlayer = null; }
            y += 44;
            if (UISkin.Btn(new Rect(r.x + 16, y, r.width - 32, 38), "Trade", UISkin.Button)) { net.RequestTrade(rp.Id); menuPlayer = null; }
            y += 44;
            if (UISkin.Btn(new Rect(r.x + 16, y, r.width - 32, 38), "Close", UISkin.Button)) menuPlayer = null;
            if (Event.current.type == EventType.MouseDown && !r.Contains(Event.current.mousePosition)) menuPlayer = null;
        }

        /// <summary>Party invitations and shared quests waiting for an answer.</summary>
        void DrawOffers()
        {
            var net = NetClient.I;
            var inv = net.PartyInvite;
            float y = 150;
            if (inv != null)
            {
                if (Time.time - inv.Time > 60f) net.AnswerPartyInvite(false);
                else if (OfferBox(y, "<b>" + inv.Name + "</b> invites you to join a party.", out bool yes)) net.AnswerPartyInvite(yes);
                y += 140;
            }
            var ti = net.TradeInvite;
            if (ti != null)
            {
                if (Time.time - ti.Time > 60f) net.AnswerTradeInvite(false);
                else if (OfferBox(y, "<b>" + ti.Name + "</b> wants to trade with you.", out bool yes)) net.AnswerTradeInvite(yes);
                y += 140;
            }
            var q = net.QuestOffer;
            if (q != null)
            {
                if (Time.time - q.Time > 60f) net.AnswerQuestOffer(false);
                else if (OfferBox(y, "<b>" + q.Name + "</b> shares a quest:\n<b>" + q.Quest.Title + "</b>  -  " + q.Quest.Objective, out bool yes))
                    net.AnswerQuestOffer(yes);
            }
        }

        bool OfferBox(float y, string text, out bool accepted)
        {
            accepted = false;
            var r = new Rect((VW - 440) / 2, y, 440, 128);
            UISkin.Box(r, UISkin.Parchment);
            Block(r);
            GUI.Label(new Rect(r.x + 24, r.y + 14, r.width - 48, 54), text, UISkin.V(UISkin.InkRich, wordWrap: true, alignment: TextAnchor.MiddleCenter));
            if (UISkin.Btn(new Rect(r.x + 50, r.y + 74, 150, 40), "Accept", UISkin.Button)) { accepted = true; return true; }
            if (UISkin.Btn(new Rect(r.xMax - 200, r.y + 74, 150, 40), "Decline", UISkin.Button)) return true;
            return false;
        }

        void Plate(Rect r, float frac, Color c)
        {
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.DrawTexture(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), UISkin.White);
            GUI.color = Factory.Shade(c, 0.3f);
            GUI.DrawTexture(r, UISkin.White);
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), UISkin.White);
            GUI.color = new Color(1, 1, 1, 0.25f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height * 0.4f), UISkin.White);
            GUI.color = Color.white;
        }

        void DrawWorldOverlays(Player p)
        {
            var hovered = p.HoveredEnemy;

            foreach (var e in Enemy.ById.Values)
            {
                if (e == null || e.IsDead) continue;
                if (Factory.FlatDistance(e.transform.position, p.transform.position) > 32f) continue;
                if (!WorldToGui(e.transform.position + Vector3.up * (e.Height + 0.35f), out var g)) continue;
                bool focus = e == hovered || e == p.AttackTarget || e.Def.Boss || e.Elite;
                bool hurt = e.Health < e.MaxHealth;
                if (!focus && !hurt) continue;
                float bw = e.Def.Boss ? 120 : e.Elite ? 96 : 64;
                Plate(new Rect(g.x - bw / 2, g.y, bw, 7), e.Health / e.MaxHealth, e.Shielded ? new Color(0.4f, 0.8f, 1f) : new Color(0.85f, 0.12f, 0.1f));
                if (focus)
                    UISkin.Shadowed(new Rect(g.x - 160, g.y - 22, 320, 22), e.DisplayName + "  " + e.Level + (e.Slowed ? "  <color=#88ccff>slowed</color>" : ""),
                        UISkin.SmallCenter, e.Elite ? Enemy.ChampionColor : LevelColor(e.Level, p.Level));
                if (e.Elite)
                    UISkin.Shadowed(new Rect(g.x - 160, g.y + 8, 320, 20), string.Join("  \u2022  ", e.Affixes),
                        UISkin.V(UISkin.SmallCenter, fontSize: 12), new Color(0.75f, 0.82f, 1f));
            }

            foreach (var rp in RemotePlayer.ById.Values)
            {
                if (rp == null) continue;
                if (!WorldToGui(rp.transform.position + Vector3.up * 2.45f, out var g)) continue;
                bool mate = NetClient.I.IsPartyMember(rp.Id);
                string plate = rp.Name + "  " + rp.Level + (rp.Dead ? "  (dead)" : "");
                UISkin.Shadowed(new Rect(g.x - 140, g.y - 22, 280, 22), plate, UISkin.SmallCenter,
                    mate ? new Color(0.45f, 1f, 0.5f) : new Color(0.5f, 0.78f, 1f));
                float pw = UISkin.SmallCenter.CalcSize(new GUIContent(plate)).x + 12f;
                var plateRect = new Rect(g.x - pw / 2, g.y - 22, pw, 22);
                Block(plateRect);
                if (ClickedIn(plateRect) >= 0) { menuPlayer = rp; menuPos = Event.current.mousePosition; }
                Plate(new Rect(g.x - 32, g.y, 64, 5), rp.Health / rp.MaxHealth, new Color(0.25f, 0.85f, 0.25f));
            }

            if (WorldToGui(p.transform.position + Vector3.up * 2.45f, out var pg))
                UISkin.Shadowed(new Rect(pg.x - 140, pg.y - 20, 280, 22), p.DisplayName, UISkin.SmallCenter, new Color(0.65f, 0.9f, 1f));

            foreach (var it in Interactable.All)
            {
                if (it == null) continue;
                float dist = Factory.FlatDistance(it.Position, p.transform.position);
                if (dist > 28f) continue;

                if (it is Npc npc)
                {
                    if (!WorldToGui(it.Position + Vector3.up * it.LabelHeight, out var g)) continue;
                    UISkin.Shadowed(new Rect(g.x - 140, g.y - 20, 280, 22), npc.DisplayName, UISkin.SmallCenter, npc.LabelColor);
                    if (!string.IsNullOrEmpty(npc.Title))
                        UISkin.Shadowed(new Rect(g.x - 140, g.y - 2, 280, 20), "<" + npc.Title + ">", UISkin.SmallCenter, new Color(0.75f, 0.9f, 0.7f));
                    var mark = npc.Marker(p, out var mc);
                    if (mark != null) UISkin.Shadowed(new Rect(g.x - 40, g.y - 74, 80, 56), mark, UISkin.TitleHuge, mc, 2);
                    continue;
                }

                if (it is LootDrop drop && drop.CanInteract)
                {
                    // Loot filter: plain white gear only gets a label while Alt is held (unless the option is on).
                    if (drop.Item != null && drop.Item.Kind == ItemKind.Equipment && drop.Item.Rarity == Rarity.Common &&
                        !GameSettings.ShowCommonLoot && !GameInput.Held(GKey.Alt) && it != p.HoveredInteractable) continue;
                    if (!WorldToGui(it.Position + Vector3.up * it.LabelHeight, out var g)) continue;
                    var text = drop.HoverText;
                    var size = UISkin.Small.CalcSize(new GUIContent(text));
                    bool hasIcon = drop.Item != null;
                    var r = new Rect(g.x - size.x / 2 - (hasIcon ? 18 : 8), g.y - 12, size.x + (hasIcon ? 30 : 16), 24);
                    GUI.color = new Color(0.05f, 0.03f, 0.02f, 0.82f);
                    GUI.DrawTexture(r, UISkin.White);
                    GUI.color = drop.LabelColor * new Color(1, 1, 1, 0.8f);
                    GUI.DrawTexture(new Rect(r.x, r.yMax - 2, r.width, 2), UISkin.White);
                    GUI.color = Color.white;
                    if (hasIcon) UISkin.IconInSlot(new Rect(r.x + 3, r.y + 2, 20, 20), UISkin.Icon(UISkin.IconKey(drop.Item)), UISkin.IconTint(drop.Item), 0);
                    else UISkin.IconInSlot(new Rect(r.x + 3, r.y + 2, 20, 20), UISkin.Icon("gold"), Color.white, 0);
                    UISkin.Shadowed(new Rect(r.x + (hasIcon ? 22 : 18), r.y, r.width - 24, r.height), text, UISkin.Small, drop.LabelColor);
                    Block(r);
                    if (ClickedIn(r) == 0) p.SetInteract(drop);
                    continue;
                }

                if (it is DungeonPortal || it is DungeonEntrance)
                {
                    if (!WorldToGui(it.Position + Vector3.up * it.LabelHeight, out var g)) continue;
                    UISkin.Shadowed(new Rect(g.x - 180, g.y - 11, 360, 22), it.HoverText.Split('\n')[0], UISkin.HeadingCenter, it.LabelColor, 2);
                    continue;
                }

                if (it == p.HoveredInteractable)
                {
                    if (!WorldToGui(it.Position + Vector3.up * it.LabelHeight, out var g)) continue;
                    UISkin.Shadowed(new Rect(g.x - 180, g.y - 11, 360, 22), it.HoverText, UISkin.SmallCenter, it.LabelColor);
                }
            }
        }

        void DrawFloatingText()
        {
            for (int i = floats.Count - 1; i >= 0; i--)
            {
                var f = floats[i];
                float age = Time.time - f.Time;
                if (age > 1.3f) { floats.RemoveAt(i); continue; }
                if (!WorldToGui(f.Pos + Vector3.up * age * 1.4f, out var g)) continue;
                UISkin.FloatText.fontSize = Mathf.RoundToInt(19 * f.Size * (age < 0.12f ? 1.35f : 1f));
                var c = f.Color;
                c.a = age > 0.9f ? 1f - (age - 0.9f) / 0.4f : 1f;
                UISkin.Shadowed(new Rect(g.x - 160, g.y - 16, 320, 32), f.Text, UISkin.FloatText, c, 2);
            }
        }

        // =====================================================================================
        // HUD
        // =====================================================================================

        void DrawUnitFrames(Player p)
        {
            var r = new Rect(12, 12, 330, 96);
            UISkin.Box(r, UISkin.Panel);
            Block(r);
            var portrait = new Rect(r.x + 12, r.y + 12, 72, 72);
            UISkin.Box(portrait, UISkin.Slot);
            var avatar = Avatar.Texture;
            if (avatar != null)
            {
                // head and shoulders of the live avatar (keeps the crop's aspect inside the square slot)
                var inner = new Rect(portrait.x + 4, portrait.y + 4, portrait.width - 8, portrait.height - 8);
                if (Event.current.type == EventType.Repaint)
                    GUI.DrawTextureWithTexCoords(inner, avatar, Avatar.HeadCrop, true);
            }
            else UISkin.IconInSlot(portrait, UISkin.Icon(p.Look.ToLower()), Color.white, 6);
            var lv = new Rect(portrait.xMax - 26, portrait.yMax - 24, 30, 26);
            UISkin.Box(lv, UISkin.Slot);
            UISkin.Shadowed(lv, p.Level.ToString(), UISkin.SmallCenter, UISkin.Gold);

            UISkin.Shadowed(new Rect(r.x + 96, r.y + 12, 220, 24), p.DisplayName, UISkin.Heading, UISkin.Gold);
            UISkin.Bar(new Rect(r.x + 96, r.y + 42, 220, 20), p.Health / p.MaxHealth, "Red",
                Mathf.CeilToInt(p.Health) + " / " + Mathf.CeilToInt(p.MaxHealth), new Color(0.75f, 0.12f, 0.1f));
            UISkin.Bar(new Rect(r.x + 96, r.y + 66, 220, 18), p.Mana / p.MaxMana, "Blue",
                Mathf.FloorToInt(p.Mana) + " / " + Mathf.FloorToInt(p.MaxMana), new Color(0.2f, 0.35f, 0.9f));

            float below = r.yMax + 6;
            if (p.StatPoints > 0)
            {
                var sr = new Rect(r.x, below, 330, 40);
                if (UISkin.Btn(sr, "+" + p.StatPoints + " attribute points  [C]", UISkin.Button)) showChar = true;
                Block(sr);
                below += 46;
            }
            DrawCompanionFrame(p, below);

            Combatant target = p.HoveredEnemy != null ? p.HoveredEnemy : p.AttackTarget;
            if (target != null && !target.IsDead)
            {
                var t = new Rect(354, 12, 320, 76);
                UISkin.Box(t, UISkin.PanelPlain);
                var e = target as Enemy;
                bool boss = e != null && e.Def.Boss;
                bool elite = e != null && e.Elite;
                UISkin.Shadowed(new Rect(t.x + 16, t.y + 12, 290, 24), target.DisplayName, UISkin.Heading, elite ? Enemy.ChampionColor : LevelColor(target.Level, p.Level));
                UISkin.Shadowed(new Rect(t.x + 16, t.y + 12, 288, 24), (boss ? "<color=#ff9a3c>Boss</color>  " : elite ? "<color=#7fa6ff>Champion</color>  " : "") + "Level " + target.Level,
                    UISkin.V(UISkin.Small, alignment: TextAnchor.UpperRight), UISkin.Cream);
                if (elite)
                    UISkin.Shadowed(new Rect(t.x + 16, t.yMax + 4, 288, 20), e.Def.Name + "  -  " + string.Join(", ", e.Affixes), UISkin.Small, new Color(0.75f, 0.82f, 1f));
                UISkin.Bar(new Rect(t.x + 16, t.y + 44, 288, 20), target.Health / target.MaxHealth, "Red",
                    Mathf.CeilToInt(target.Health) + " / " + Mathf.CeilToInt(target.MaxHealth), new Color(0.75f, 0.12f, 0.1f));
            }
        }

        void DrawActionBar(Player p)
        {
            const float slot = 58, gap = 8;
            var kit = p.Kit;
            int count = kit.Length + 3;
            float barW = count * (slot + gap) - gap + 24;
            float x0 = (VW - barW) / 2 + 12, y0 = VH - slot - 46;

            // Orbs
            float orb = 132;
            DrawOrb(new Rect(x0 - orb - 34, VH - orb - 22, orb, orb), p.Health / p.MaxHealth, new Color(0.78f, 0.08f, 0.08f), Mathf.CeilToInt(p.Health).ToString());
            DrawOrb(new Rect(x0 + barW + 10, VH - orb - 22, orb, orb), p.Mana / p.MaxMana, new Color(0.12f, 0.28f, 0.9f), Mathf.FloorToInt(p.Mana).ToString());

            var bg = new Rect(x0 - 12, y0 - 12, barW, slot + 50);
            UISkin.Box(bg, UISkin.Panel);
            Block(bg);

            for (int i = 0; i < kit.Length; i++)
            {
                var a = kit[i];
                var r = new Rect(x0 + i * (slot + gap), y0, slot, slot);
                bool locked = p.Level < a.RequiredLevel;
                UISkin.Box(r, UISkin.Slot);
                UISkin.IconInSlot(r, UISkin.Icon(UISkin.AbilityIcon(a.Id)), locked ? new Color(0.35f, 0.35f, 0.35f) : Color.white, 3);

                float cd = p.CooldownEnd[i] - Time.time;
                if (cd > 0)
                {
                    float frac = Mathf.Clamp01(cd / a.Cooldown);
                    GUI.color = new Color(0, 0, 0, 0.65f);
                    GUI.DrawTexture(new Rect(r.x + 3, r.y + 3 + (r.height - 6) * (1 - frac), r.width - 6, (r.height - 6) * frac), UISkin.White);
                    GUI.color = Color.white;
                    UISkin.Shadowed(new Rect(r.x, r.y + 16, r.width, 26), cd.ToString(cd < 1 ? "0.0" : "0"), UISkin.LabelCenter, Color.white, 2);
                }
                else if (!locked && p.Mana < a.ManaCost)
                {
                    GUI.color = new Color(0.1f, 0.15f, 0.7f, 0.45f);
                    GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), UISkin.White);
                    GUI.color = Color.white;
                }
                if (locked) UISkin.Shadowed(new Rect(r.x, r.y + 18, r.width, 22), "Lv " + a.RequiredLevel, UISkin.SmallCenter, new Color(1f, 0.6f, 0.5f), 2);
                UISkin.Shadowed(new Rect(r.x + 5, r.y + 2, r.width, 18), a.Key.Split('/')[0], UISkin.Small, UISkin.Gold, 2);
                if (r.Contains(Event.current.mousePosition))
                    tooltip = "<size=17><b><color=#" + Item.Hex(a.Color) + ">" + a.Name + "</color></b></size>   [" + a.Key + "]\n" +
                              "<color=#88aaff>" + a.ManaCost + " mana</color>   " + a.Cooldown + "s cooldown" +
                              (locked ? "\n<color=#ff6666>Requires level " + a.RequiredLevel + "</color>" : "") + "\n\n" + a.Description;
                if (ClickedIn(r) == 0) p.CastAbility(i, p.MouseGround);
            }

            // Potions
            string[] potions = { "Health Potion", "Mana Potion" };
            string[] keys = { "Q", "E" };
            string[] icons = { "health_potion", "mana_potion" };
            for (int i = 0; i < 2; i++)
            {
                var r = new Rect(x0 + (kit.Length + i) * (slot + gap) + 4, y0, slot, slot);
                int n = p.Inventory.CountOf(potions[i]);
                UISkin.Box(r, UISkin.Slot);
                UISkin.IconInSlot(r, UISkin.Icon(icons[i]), n > 0 ? Color.white : new Color(0.4f, 0.4f, 0.4f), 6);
                UISkin.Shadowed(new Rect(r.x + 5, r.y + 2, r.width, 18), keys[i], UISkin.Small, UISkin.Gold, 2);
                UISkin.Shadowed(new Rect(r.x, r.y + r.height - 22, r.width - 6, 20), n.ToString(), UISkin.SmallRight, n > 0 ? Color.white : new Color(1f, 0.4f, 0.4f), 2);
                float pcd = p.PotionCooldownLeft;
                if (pcd > 0f)
                {
                    float frac = Mathf.Clamp01(pcd / Player.PotionCooldown);
                    GUI.color = new Color(0, 0, 0, 0.6f);
                    GUI.DrawTexture(new Rect(r.x + 3, r.y + 3 + (r.height - 6) * (1 - frac), r.width - 6, (r.height - 6) * frac), UISkin.White);
                    GUI.color = Color.white;
                }
                if (r.Contains(Event.current.mousePosition)) tooltip = "<b>" + potions[i] + "</b>  [" + keys[i] + "]\nYou have " + n + ".\n<color=#999999>Potions and food share a " + Player.PotionCooldown + " s cooldown.</color>";
                if (ClickedIn(r) == 0) p.UseItemByName(potions[i]);
            }

            // Recall
            {
                var r = new Rect(x0 + (kit.Length + 2) * (slot + gap) + 8, y0, slot, slot);
                UISkin.Box(r, UISkin.Slot);
                bool home = WorldGenerator.InTown(p.transform.position) && !Dungeon.Active;
                bool usable = home ? p.HasReturnPoint : p.RecallReadyIn <= 0f;
                UISkin.IconInSlot(r, UISkin.Icon("teleport"), usable ? Color.white : new Color(0.4f, 0.4f, 0.4f), 6);
                UISkin.Shadowed(new Rect(r.x + 5, r.y + 2, r.width, 18), "R", UISkin.Small, UISkin.Gold, 2);
                if (!home && p.RecallReadyIn > 0f)
                    UISkin.Shadowed(new Rect(r.x, r.y + 16, r.width, 26), Mathf.CeilToInt(p.RecallReadyIn).ToString(), UISkin.LabelCenter, Color.white, 2);
                if (r.Contains(Event.current.mousePosition))
                    tooltip = home ? (p.HasReturnPoint ? "<b>Return</b>  [R]\nStep back to where you recalled from." : "<b>Recall</b>  [R]\nYou are already in Hollowmere.")
                                   : "<b>Recall to Hollowmere</b>  [R]\nChannel for " + Player.RecallTime + " seconds (moving or taking damage interrupts). " +
                                     "Press R in town afterwards to return to the same spot (not into the Catacombs).";
                if (ClickedIn(r) == 0) p.Recall();
            }

            // Recall channel bar
            if (p.RecallProgress >= 0f)
                UISkin.Bar(new Rect(VW / 2 - 140, y0 - 96, 280, 18), p.RecallProgress, "Blue", "Recalling...", new Color(0.4f, 0.6f, 1f));

            // XP bar inside the action bar frame
            UISkin.Bar(new Rect(x0, y0 + slot + 10, barW - 24, 16), (float)p.Xp / p.XpToNext, "Purple",
                "Level " + p.Level + "   " + p.Xp + " / " + p.XpToNext + " XP", new Color(0.6f, 0.35f, 0.9f));

            // Gathering progress
            if (p.GatherNode != null)
            {
                var gr = new Rect((VW - 300) / 2, y0 - 58, 300, 22);
                UISkin.Bar(gr, p.GatherProgress, "Green", SkillSet.Verb(p.GatherNode.Skill) + " " + p.GatherNode.DisplayName + "...", SkillSet.SkillColor(p.GatherNode.Skill));
            }
        }

        void DrawOrb(Rect r, float frac, Color color, string text)
        {
            frac = Mathf.Clamp01(frac);
            var big = new Rect(r.x - 14, r.y - 14, r.width + 28, r.height + 28);
            if (UISkin.Orb(big, frac, color))
            {
                UISkin.Shadowed(new Rect(r.x, r.y + r.height / 2 - 14, r.width, 28), text, UISkin.LabelCenter, Color.white, 2);
                Block(r);
                return;
            }
            var c = UISkin.Circle;
            GUI.color = new Color(0.36f, 0.24f, 0.14f);
            GUI.DrawTexture(new Rect(r.x - 10, r.y - 10, r.width + 20, r.height + 20), c);
            GUI.color = new Color(0.05f, 0.03f, 0.02f);
            GUI.DrawTexture(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), c);
            GUI.color = Factory.Shade(color, 0.22f);
            GUI.DrawTexture(r, c);
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y + r.height * (1 - frac), r.width, r.height * frac), c, new Rect(0, 0, 1, frac));
            GUI.color = Color.Lerp(color, Color.white, 0.35f);
            if (frac > 0.02f && frac < 0.99f)
                GUI.DrawTexture(new Rect(r.x + r.width * 0.12f, r.y + r.height * (1 - frac) - 1, r.width * 0.76f, 2), UISkin.White);
            GUI.color = new Color(1, 1, 1, 0.16f);
            GUI.DrawTexture(new Rect(r.x + r.width * 0.2f, r.y + r.height * 0.1f, r.width * 0.35f, r.height * 0.25f), c);
            GUI.color = Color.white;
            UISkin.Shadowed(new Rect(r.x, r.y + r.height / 2 - 14, r.width, 28), text, UISkin.LabelCenter, Color.white, 2);
            Block(r);
        }

        void DrawMenuButtons()
        {
            string[] icons = { "bags", "character", "talents", "skills", "quests", "map", "help", "menu" };
            string[] tips = { "Bags  [I]", "Character  [C]", "Talents  [T]", "Skills  [K]", "Quest Log  [L]", "World Map  [M]", "Help  [F1]", "Game Menu  [Esc]\nSettings, log out" };
            const float s = 44, gap = 6;
            float w = icons.Length * (s + gap) - gap;
            var r = new Rect(VW - w - 20, VH - s - 18, w, s);
            if (r.x - 8 < OrbsRight) r.y = VH - s - 190; // narrow screen: sit above the orbs
            Block(new Rect(r.x - 8, r.y - 8, r.width + 16, r.height + 16));
            for (int i = 0; i < icons.Length; i++)
            {
                var b = new Rect(r.x + i * (s + gap), r.y, s, s);
                if (UISkin.Btn(b, GUIContent.none, UISkin.SquareButton))
                {
                    switch (i)
                    {
                        case 0: showBags = !showBags; break;
                        case 1: showChar = !showChar; break;
                        case 2: showTalents = !showTalents; break;
                        case 3: showSkills = !showSkills; break;
                        case 4: showQuests = !showQuests; break;
                        case 5: showMap = !showMap; break;
                        case 6: showHelp = !showHelp; break;
                        default: menu = menu == MenuPage.None ? MenuPage.Main : MenuPage.None; break;
                    }
                }
                UISkin.IconInSlot(b, UISkin.Icon(icons[i]), Color.white, 5);
                if (i == 2 && Player.I != null && Player.I.TalentPoints > 0)
                    UISkin.Shadowed(new Rect(b.xMax - 16, b.y - 4, 20, 20), Player.I.TalentPoints.ToString(), UISkin.SmallCenter, new Color(0.8f, 0.6f, 1f), 2);
                if (b.Contains(Event.current.mousePosition)) tooltip = tips[i];
            }
            if (Player.I != null) DrawNewsChip(Player.I, r);
        }

        void DrawMinimap(Player p)
        {
            const float D = 196f;                            // map diameter
            float frameSize = D / 0.83f;                     // the orb frame's inner ring is ~83% of its size
            var frame = new Rect(VW - frameSize - 12, 6, frameSize, frameSize);
            var r = new Rect(frame.center.x - D / 2, frame.center.y - D / 2, D, D);
            var info = new Rect(frame.x + 6, frame.yMax - 10, frameSize - 12, 86);
            Block(frame);
            Block(info);
            Vector3 pp = p.transform.position;
            var net = NetClient.I;

            // Info plate under the ring: zone name (shrunk to fit), gold / players online, time of day.
            UISkin.Box(info, UISkin.Panel);
            string zone = WorldGenerator.ZoneAt(pp);
            int fs = 20;
            while (fs > 12 && UISkin.V(UISkin.HeadingCenter, fontSize: fs).CalcSize(new GUIContent(zone)).x > info.width - 20) fs--;
            UISkin.Shadowed(new Rect(info.x + 8, info.y + 14, info.width - 16, 26), zone, UISkin.V(UISkin.HeadingCenter, fontSize: fs),
                WorldGenerator.InTown(pp) ? new Color(0.6f, 1f, 0.6f) : UISkin.Gold);
            UISkin.IconInSlot(new Rect(info.x + 14, info.y + 42, 20, 20), UISkin.Icon("gold"), Color.white, 0);
            UISkin.Shadowed(new Rect(info.x + 38, info.y + 42, 90, 20), p.Gold.ToString(), UISkin.Small, new Color(1f, 0.85f, 0.3f));
            UISkin.Shadowed(new Rect(info.x + 14, info.y + 42, info.width - 28, 20), net.PlayersOnline + " online",
                UISkin.V(UISkin.Small, alignment: TextAnchor.MiddleRight), UISkin.Muted);
            bool dark = DayNight.Night > 0.5f;
            UISkin.Shadowed(new Rect(info.x + 14, info.y + 62, info.width - 28, 18), DayNight.Phase + "  " + DayNight.Clock,
                UISkin.V(UISkin.Small, alignment: TextAnchor.MiddleCenter), dark ? new Color(0.65f, 0.75f, 1f) : new Color(1f, 0.85f, 0.5f));

            // The map itself, then markers clipped to the circle.
            GUI.color = new Color(0.03f, 0.025f, 0.02f, 1f);
            GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), UISkin.Circle);
            GUI.color = Color.white;
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(r, Minimap.Render(pp));

            float span = Minimap.Span;
            System.Func<Vector3, Vector2> toMap = w => new Vector2(
                r.center.x + (w.x - pp.x) / span * r.width,
                r.center.y - (w.z - pp.z) / span * r.height);

            foreach (var it in Interactable.All)
            {
                if (!Exploration.Seen(it.Position) && !(it is DungeonEntrance)) continue; // fog hides what you haven't found
                if (it is Npc npc)
                {
                    var pos = toMap(npc.Position);
                    var mark = npc.Marker(p, out var mc);
                    if (mark != null && InCircle(r, pos, 6f))
                        UISkin.Shadowed(new Rect(pos.x - 10, pos.y - 13, 20, 24), mark, UISkin.V(UISkin.HeadingCenter, fontSize: 18), mc, 1);
                    else Dot(r, pos, npc.Role == NpcRole.Vendor ? new Color(1f, 0.8f, 0.35f) : npc.Role == NpcRole.Healer ? new Color(0.5f, 1f, 0.7f) : new Color(0.75f, 0.9f, 0.6f), 6);
                }
                else if (it is DungeonPortal) Dot(r, toMap(it.Position), it.LabelColor, 10);
                else if (it is DungeonEntrance && AdminTools.ShowDungeons) Dot(r, toMap(it.Position), it.LabelColor, 10);
                else if (it is StashChest) Dot(r, toMap(it.Position), new Color(0.9f, 0.7f, 0.4f), 6);
            }
            if (AdminTools.ShowEnemies) // enemies are not on the map (admins can turn them on)
                foreach (var e in Enemy.ById.Values)
                    if (e != null && !e.IsDead) Dot(r, toMap(e.transform.position), e.Def.Boss ? new Color(1f, 0.5f, 0f) : e.Elite ? Enemy.ChampionColor : new Color(0.9f, 0.15f, 0.1f), e.Def.Boss ? 10 : e.Elite ? 8 : 5);
            foreach (var rp in RemotePlayer.ById.Values)
                if (rp != null) Dot(r, toMap(rp.transform.position), net.IsPartyMember(rp.Id) ? new Color(0.35f, 1f, 0.45f) : new Color(0.3f, 0.6f, 1f), 7);
            foreach (var m in net.Party) // party members out of view range, in the same place as us
                if (m.id != net.MyId && m.di == net.DungeonId && !RemotePlayer.ById.ContainsKey(m.id)) Dot(r, toMap(new Vector3(m.x, 0, m.z)), new Color(0.35f, 1f, 0.45f), 6);

            // Hero: an arrow pointing where we face.
            // (Not GUIUtility.RotateAroundPivot: that pivots in unscaled screen space, so with the UI scale the
            // arrow would orbit around the wrong point. Rotate around the map centre as it appears on screen.)
            var saved = GUI.matrix;
            Vector3 pivot = saved.MultiplyPoint3x4(new Vector3(r.center.x, r.center.y, 0f));
            GUI.matrix = Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, p.transform.eulerAngles.y), Vector3.one) *
                         Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one) * saved;
            GUI.color = new Color(1f, 0.95f, 0.8f);
            GUI.DrawTexture(new Rect(r.center.x - 8, r.center.y - 9, 16, 18), Minimap.Arrow);
            GUI.matrix = saved;
            GUI.color = Color.white;

            // Bronze ring, north marker and zoom buttons.
            if (UISkin.OrbFrame != null) GUI.DrawTexture(frame, UISkin.OrbFrame);
            UISkin.Shadowed(new Rect(frame.center.x - 12, frame.y + 2, 24, 22), "N", UISkin.V(UISkin.HeadingCenter, fontSize: 15), UISkin.Gold, 2);
            var zin = new Rect(frame.xMax - 40, frame.yMax - 44, 26, 26);
            var zout = new Rect(frame.x + 14, frame.yMax - 44, 26, 26);
            if (UISkin.Btn(zin, "+", UISkin.SquareButton)) Minimap.Span = Mathf.Max(Minimap.MinSpan, Minimap.Span / 1.3f);
            if (UISkin.Btn(zout, "-", UISkin.SquareButton)) Minimap.Span = Mathf.Min(Minimap.MaxSpan, Minimap.Span * 1.3f);
            if (zin.Contains(Event.current.mousePosition)) tooltip = "Zoom in";
            if (zout.Contains(Event.current.mousePosition)) tooltip = "Zoom out";
        }

        static bool InCircle(Rect circle, Vector2 pos, float margin) =>
            (pos - circle.center).sqrMagnitude <= (circle.width / 2 - margin) * (circle.width / 2 - margin);

        void Dot(Rect clip, Vector2 pos, Color c, float size)
        {
            if (!InCircle(clip, pos, size / 2 + 2)) return;
            DotAt(pos, c, size);
        }

        void DotAt(Vector2 pos, Color c, float size)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(pos.x - size / 2 - 1, pos.y - size / 2 - 1, size + 2, size + 2), UISkin.Circle);
            GUI.color = c;
            GUI.DrawTexture(new Rect(pos.x - size / 2, pos.y - size / 2, size, size), UISkin.Circle);
            GUI.color = Color.white;
        }

        void DrawQuestTracker(Player p)
        {
            if (p.Quests.Active.Count == 0) return;
            float x = VW - 330, y = 342;
            UISkin.Shadowed(new Rect(x, y, 300, 26), "Quests", UISkin.Heading, UISkin.Gold);
            y += 28;
            foreach (var q in p.Quests.Active)
            {
                bool ready = q.IsReady(p);
                UISkin.Shadowed(new Rect(x, y, 300, 22), q.Def.Title, UISkin.Label, new Color(1f, 0.85f, 0.3f));
                y += 22;
                string obj = q.Def.Type == QuestType.Kill ? q.Def.Target + " slain" : q.Def.Target;
                UISkin.Shadowed(new Rect(x + 12, y, 290, 20), ready ? "Return to " + GiverOf(q.Def) : obj + ":  " + q.Progress(p) + " / " + q.Def.Count,
                    UISkin.Small, ready ? new Color(0.55f, 1f, 0.55f) : UISkin.Cream);
                y += 26;
            }
        }

        static string GiverOf(QuestDef q)
        {
            foreach (var kv in QuestDatabase.Chains)
                if (System.Array.IndexOf(kv.Value, q) >= 0) return kv.Key;
            return "the quest giver";
        }

        // =====================================================================================
        // Chat
        // =====================================================================================

        /// <summary>Opens the chat box with some text already typed (e.g. "/w Name ").</summary>
        public void OpenChat(string prefill = "")
        {
            ChatOpen = true;
            chatText = prefill;
        }

        public void SetReplyTarget(string name) => replyTo = name;

        /// <summary>
        /// The chat box reads key events itself instead of using a focused GUI.TextField, whose focus handling
        /// is unreliable (in WebGL the box would open but never receive the typed characters).
        /// </summary>
        readonly System.Collections.Generic.List<string> chatHistory = new System.Collections.Generic.List<string>();
        int historyIndex = -1;

        void HandleChatKeys()
        {
            var e = Event.current;
            if (Player.I == null || e.type != EventType.KeyDown) return;
            bool enter = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter;
            if (!ChatOpen)
            {
                if (enter) { OpenChat(); e.Use(); }
                else if (e.keyCode == KeyCode.Slash) { OpenChat(); e.Use(); } // the '/' itself arrives as the next (character) event
                return;
            }

            if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow)
            {
                // Recall what you said before (newest first).
                if (chatHistory.Count > 0)
                {
                    historyIndex = Mathf.Clamp(historyIndex + (e.keyCode == KeyCode.UpArrow ? 1 : -1), -1, chatHistory.Count - 1);
                    chatText = historyIndex < 0 ? "" : chatHistory[chatHistory.Count - 1 - historyIndex];
                }
            }
            else if (enter)
            {
                string text = chatText.Trim();
                if (text.Length > 0 && (chatHistory.Count == 0 || chatHistory[chatHistory.Count - 1] != text))
                {
                    chatHistory.Add(text);
                    if (chatHistory.Count > 30) chatHistory.RemoveAt(0);
                }
                historyIndex = -1;
                if (text.StartsWith("/r ") && !string.IsNullOrEmpty(replyTo)) text = "/w " + replyTo + " " + text.Substring(3);
                NetClient.I.SendChat(text);
                chatText = "";
                ChatOpen = false;
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                chatText = "";
                ChatOpen = false;
            }
            else if (e.keyCode == KeyCode.Backspace)
            {
                if (chatText.Length > 0) chatText = chatText.Substring(0, chatText.Length - 1);
            }
            else
            {
                char c = e.character;
                if (c != '\0' && !char.IsControl(c) && chatText.Length < 200) chatText += c;
            }
            e.Use(); // keep typed keys away from hotkeys
        }

        /// <summary>Left edge of the health orb (the HUD's widest element), used to keep side panels clear of it.</summary>
        float OrbsLeft => VW / 2f - 405f;
        float OrbsRight => VW / 2f + 405f;

        void DrawLog(bool inWorld)
        {
            float w = 400, lines = ChatOpen ? 14 : 8, lh = 20;
            float x = 16, bottom = VH - 44 - (inWorld ? 30 : 0);
            if (inWorld && x + w + 8 > OrbsLeft) bottom = VH - 200; // narrow screen: sit above the orbs
            float y = bottom - lines * lh;
            var area = new Rect(x - 8, y - 8, w + 16, lines * lh + 16);
            if (ChatOpen || (inWorld && area.Contains(Event.current.mousePosition)))
            {
                GUI.color = new Color(1, 1, 1, 0.9f);
                UISkin.Box(area, UISkin.Inset);
                GUI.color = Color.white;
            }
            int start = Mathf.Max(0, log.Count - (int)lines);
            for (int i = start; i < log.Count; i++)
            {
                var l = log[i];
                float age = Time.time - l.Time;
                float alpha = ChatOpen ? 1f : Mathf.Clamp01(1f - (age - 20f) / 5f);
                if (alpha <= 0f) continue;
                var c = l.Color;
                c.a = alpha;
                UISkin.Shadowed(new Rect(x, y + (i - start) * lh, w, lh), l.Text, UISkin.Small, c);
            }

            if (inWorld && ChatOpen)
            {
                var r = new Rect(x - 8, y + lines * lh + 12, w + 16, 34);
                Block(r);
                UISkin.Box(r, UISkin.Field);
                var style = chatFieldStyle ?? (chatFieldStyle = new GUIStyle(UISkin.Field) { normal = { background = null }, clipping = TextClipping.Clip });
                bool empty = chatText.Length == 0;
                string caret = Time.unscaledTime % 1f < 0.55f ? "|" : " ";
                string shown = chatText;
                float room = r.width - style.padding.horizontal - 12;
                while (shown.Length > 0 && style.CalcSize(new GUIContent(shown + "|")).x > room) shown = shown.Substring(1); // keep the end visible
                if (empty)
                {
                    GUI.color = new Color(1, 1, 1, 0.45f);
                    GUI.Label(r, "Say something...   /p party   /w name   /r reply   /invite name", style);
                    GUI.color = Color.white;
                }
                else GUI.Label(r, shown + caret, style);
            }
        }

        // =====================================================================================
        // Windows
        // =====================================================================================

        void DrawBags(Player p)
        {
            const int cols = 8, rows = 5;
            const float cell = 50, gap = 4;
            float w = cols * (cell + gap) - gap + 40, h = rows * (cell + gap) + 112;
            var r = new Rect(VW - w - 20, VH - h - 82, w, h);
            if (UISkin.Window(r, "Bags")) { showBags = false; socketGem = -1; }
            Block(r);

            bool vendor = dialogNpc != null && dialogNpc.Role == NpcRole.Vendor;
            for (int i = 0; i < p.Inventory.Slots.Length; i++)
            {
                var cr = new Rect(r.x + 20 + (i % cols) * (cell + gap), r.y + 58 + (i / cols) * (cell + gap), cell, cell);
                var item = p.Inventory.Slots[i];
                DrawItemSlot(cr, item, p);
                if (item == null) continue;
                if (i == socketGem)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.35f + Mathf.PingPong(Time.time, 0.4f));
                    GUI.DrawTexture(new Rect(cr.x + 2, cr.y + 2, cr.width - 4, 3), UISkin.White);
                    GUI.DrawTexture(new Rect(cr.x + 2, cr.yMax - 5, cr.width - 4, 3), UISkin.White);
                    GUI.color = Color.white;
                }
                if (item.Kind == ItemKind.Equipment && item.Sockets > 0) DrawSocketPips(cr, item);
                if (cr.Contains(Event.current.mousePosition))
                    ItemTooltip(item, p, BagHint(item, vendor));
                int click = ClickedIn(cr);
                if (click == 0)
                {
                    if (socketGem >= 0 && item.Kind == ItemKind.Equipment) { p.SocketGem(socketGem, item); socketGem = -1; }
                    else if (item.Kind == ItemKind.Gem)
                    {
                        socketGem = socketGem == i ? -1 : i;
                        if (socketGem >= 0) Log("Click an item (in your bags or worn) with an empty socket. Right-click to cancel.", item.IconColor);
                    }
                    else p.UseItem(i);
                }
                else if (click == 1)
                {
                    if (socketGem >= 0) socketGem = -1;
                    else if (tradeOpen) NetClient.I.OfferItem(i);
                    else if (showStash) StashItem(p, i);
                    else if (vendor) Sell(p, i);
                    else if (Event.current.shift) p.DropItem(i);
                }
            }
            float fy = r.yMax - 44;
            UISkin.IconInSlot(new Rect(r.x + 20, fy, 26, 26), UISkin.Icon("gold"), Color.white, 0);
            UISkin.Shadowed(new Rect(r.x + 50, fy, 200, 26), p.Gold + " gold", UISkin.Label, new Color(1f, 0.85f, 0.3f));
            UISkin.Shadowed(new Rect(r.x + 20, fy, w - 40 - 86, 26), p.Inventory.FreeSlots + " free slots",
                UISkin.V(UISkin.Small, alignment: TextAnchor.MiddleRight), UISkin.Muted);
            if (UISkin.Btn(new Rect(r.xMax - 20 - 76, fy - 4, 76, 32), "Sort", UISkin.Button))
            {
                socketGem = -1;
                p.Inventory.Sort();
                Sfx.Play2D("ui_click", 0.4f);
            }
        }

        void Sell(Player p, int index)
        {
            var item = p.Inventory.Slots[index];
            if (item == null) return;
            int value = item.Value * Mathf.Max(1, item.Count);
            p.Inventory.TakeAll(index);
            p.AddGold(value);
            Sfx.Play2D("coins", 0.5f);
            Log("Sold " + item.Name + (item.Count > 1 ? " x" + item.Count : "") + " for " + value + " gold.", new Color(1f, 0.85f, 0.2f));
        }

        void DrawItemSlot(Rect r, Item item, Player p, string emptyIcon = null)
        {
            UISkin.Box(r, UISkin.Slot);
            if (item == null)
            {
                if (emptyIcon != null) UISkin.IconInSlot(r, UISkin.Icon(emptyIcon), new Color(1, 1, 1, 0.18f), 9);
                return;
            }
            Color border = item.Kind == ItemKind.Equipment ? Item.RarityColor(item.Rarity) : new Color(0.55f, 0.5f, 0.45f);
            if (item.Kind == ItemKind.Equipment && item.Rarity > Rarity.Common)
            {
                GUI.color = new Color(border.r, border.g, border.b, 0.22f);
                GUI.DrawTexture(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), UISkin.White);
            }
            GUI.color = new Color(border.r, border.g, border.b, 0.9f);
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, 2), UISkin.White);
            GUI.DrawTexture(new Rect(r.x + 3, r.yMax - 5, r.width - 6, 2), UISkin.White);
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, 2, r.height - 6), UISkin.White);
            GUI.DrawTexture(new Rect(r.xMax - 5, r.y + 3, 2, r.height - 6), UISkin.White);
            GUI.color = Color.white;

            var icon = UISkin.Icon(UISkin.IconKey(item));
            if (icon != null) UISkin.IconInSlot(r, icon, UISkin.IconTint(item), 6);
            else UISkin.Shadowed(new Rect(r.x, r.y + r.height / 2 - 12, r.width, 24), item.Icon, UISkin.LabelCenter, Color.white);
            if (item.Count > 1) UISkin.Shadowed(new Rect(r.x, r.yMax - 22, r.width - 6, 20), item.Count.ToString(), UISkin.SmallRight, Color.white, 2);
            if (item.Kind == ItemKind.Equipment && item.RequiredLevel > p.Level)
            {
                GUI.color = new Color(1f, 0f, 0f, 0.28f);
                GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), UISkin.White);
                GUI.color = Color.white;
            }
        }

        static readonly EquipSlot[] dollLeft = { EquipSlot.Helm, EquipSlot.Chest, EquipSlot.Legs, EquipSlot.Boots };
        static readonly EquipSlot[] dollRight = { EquipSlot.Amulet, EquipSlot.Weapon, EquipSlot.Gloves, EquipSlot.Ring };

        static string SlotIcon(EquipSlot s)
        {
            switch (s)
            {
                case EquipSlot.Weapon: return "sword";
                case EquipSlot.Helm: return "helm";
                case EquipSlot.Chest: return "chest";
                case EquipSlot.Gloves: return "gloves";
                case EquipSlot.Legs: return "legs";
                case EquipSlot.Boots: return "boots";
                case EquipSlot.Ring: return "ring";
                default: return "amulet";
            }
        }

        void DrawCharacter(Player p)
        {
            var r = new Rect(14, 140, 470, 600);
            if (UISkin.Window(r, p.DisplayName + "  -  Level " + p.Level + " " + p.Look)) showChar = false;
            Block(r);

            const float cell = 58;
            for (int i = 0; i < 4; i++)
            {
                DrawEquipSlot(p, new Rect(r.x + 22, r.y + 62 + i * (cell + 10), cell, cell), dollLeft[i], false);
                DrawEquipSlot(p, new Rect(r.xMax - 22 - cell, r.y + 62 + i * (cell + 10), cell, cell), dollRight[i], true);
            }
            var portrait = new Rect(r.x + r.width / 2 - 110, r.y + 58, 220, 262);
            UISkin.Box(portrait, UISkin.Slot);
            var avatarTex = Avatar.Texture;
            if (avatarTex != null)
            {
                // full body, wearing what's equipped right now (fit 2:3 into the frame)
                float ah = portrait.height - 36, aw = ah * 2f / 3f;
                if (Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(new Rect(portrait.center.x - aw / 2, portrait.y + 6, aw, ah), avatarTex, ScaleMode.StretchToFill, true);
            }
            else UISkin.IconInSlot(new Rect(portrait.x + 15, portrait.y + 40, 120, 120), UISkin.Icon(p.Look.ToLower()), new Color(1, 1, 1, 0.9f), 0);
            UISkin.Shadowed(new Rect(portrait.x, portrait.yMax - 34, portrait.width, 26), p.Look, UISkin.HeadingCenter, UISkin.Gold);

            float y = r.y + 346;
            var section = new Rect(r.x + 18, y - 8, r.width - 36, 136);
            UISkin.Box(section, UISkin.Inset);
            string[] names = { "Strength", "Dexterity", "Intelligence", "Vitality" };
            Stat[] stats = { Stat.Strength, Stat.Dexterity, Stat.Intelligence, Stat.Vitality };
            int[] values = { p.TotStr, p.TotDex, p.TotInt, p.TotVit };
            string[] hints = { "+2% melee damage", "+crit, +armor", "+2.5% spell damage", "+6 life" };
            for (int i = 0; i < 4; i++)
            {
                UISkin.Shadowed(new Rect(r.x + 32, y + 2, 130, 26), names[i], UISkin.Label, UISkin.Cream);
                UISkin.Shadowed(new Rect(r.x + 150, y + 2, 50, 26), "<b>" + values[i] + "</b>", UISkin.Label, UISkin.Gold);
                UISkin.Shadowed(new Rect(r.x + 200, y + 4, 180, 26), hints[i], UISkin.Small, UISkin.Muted);
                if (p.StatPoints > 0 && UISkin.Btn(new Rect(r.xMax - 66, y, 34, 30), "+", UISkin.SquareButton)) p.SpendStatPoint(stats[i]);
                y += 30;
            }
            y += 16;
            if (p.StatPoints > 0)
                UISkin.Shadowed(new Rect(r.x + 24, y - 6, 420, 24), "Unspent attribute points: " + p.StatPoints, UISkin.Label, UISkin.Gold);
            y += 22;

            float dps = (p.MinDamage + p.MaxDamage) * 0.5f * p.MeleeMultiplier * p.AttackSpeed * (1f + p.CritChance / 100f);
            string left = "Damage  <b>" + Mathf.RoundToInt(p.MinDamage * p.MeleeMultiplier) + "-" + Mathf.RoundToInt(p.MaxDamage * p.MeleeMultiplier) + "</b>" +
                          "\nAttack speed  <b>" + p.AttackSpeed.ToString("0.00") + "</b>\nDPS  <b>" + dps.ToString("0.0") + "</b>" +
                          "\nSpell power  <b>" + Mathf.RoundToInt(p.SpellMultiplier * 100) + "%</b>";
            string right = "Armor  <b>" + Mathf.RoundToInt(p.ArmorValue) + "</b>  (" + Mathf.RoundToInt(100f - 10000f / (100f + p.ArmorValue)) + "%)" +
                           "\nCrit chance  <b>" + p.CritChance.ToString("0.0") + "%</b>" +
                           "\nRegen  <b>" + p.HealthRegen.ToString("0.0") + "</b> life, <b>" + p.ManaRegen.ToString("0.0") + "</b> mana" +
                           "\nMove speed  <b>" + p.MoveSpeed.ToString("0.0") + "</b>";
            GUI.Label(new Rect(r.x + 28, y, 200, 100), left, UISkin.RichSmall);
            GUI.Label(new Rect(r.x + 240, y, 210, 100), right, UISkin.RichSmall);
        }

        void DrawEquipSlot(Player p, Rect r, EquipSlot slot, bool labelLeft)
        {
            var item = p.Inventory.GetEquipped(slot);
            DrawItemSlot(r, item, p, SlotIcon(slot));
            if (item != null && r.Contains(Event.current.mousePosition))
                tooltip = item.Tooltip(p) + "\n<color=#998877>Click to unequip</color>";
            if (item == null && r.Contains(Event.current.mousePosition))
                tooltip = "<b>" + Item.SlotName(slot) + "</b>\n<color=#998877>Empty</color>";
            if (item != null && item.Sockets > 0) DrawSocketPips(r, item);
            if (item != null && ClickedIn(r) == 0)
            {
                if (socketGem >= 0) { p.SocketGem(socketGem, item); socketGem = -1; p.RecalculateStats(); }
                else p.Unequip(slot);
            }
        }

        void DrawSkills(Player p)
        {
            var r = new Rect(498, 140, 430, 560);
            if (UISkin.Window(r, "Skills")) showSkills = false;
            Block(r);
            float y = r.y + 58;
            UISkin.Shadowed(new Rect(r.x + 22, y, 380, 24), "Professions   <size=14><color=#b8a88c>total level " + p.Skills.TotalLevel + "</color></size>", UISkin.Heading, UISkin.Gold);
            y += 32;
            foreach (var s in SkillSet.All)
            {
                int lvl = p.Skills.Level(s), xp = p.Skills.Xp(s);
                int cur = SkillSet.XpForLevel(lvl), next = SkillSet.XpForLevel(lvl + 1);
                var ir = new Rect(r.x + 22, y, 40, 40);
                UISkin.IconInSlot(ir, UISkin.Icon(UISkin.SkillIcon(s)), Color.white, 0);
                UISkin.Shadowed(new Rect(r.x + 72, y, 160, 22), s.ToString(), UISkin.Label, UISkin.Cream);
                UISkin.Shadowed(new Rect(r.x + 72, y, 330, 22), "<b>" + lvl + "</b> / 99", UISkin.V(UISkin.Label, alignment: TextAnchor.UpperRight), UISkin.Gold);
                UISkin.Bar(new Rect(r.x + 72, y + 24, 330, 14), lvl >= 99 ? 1f : (float)(xp - cur) / Mathf.Max(1, next - cur), "Green", null, SkillSet.SkillColor(s));
                y += 48;
            }
            y += 6;
            UISkin.Shadowed(new Rect(r.x + 22, y, 380, 24), "Abilities", UISkin.Heading, UISkin.Gold);
            y += 30;
            foreach (var a in p.Kit)
            {
                bool locked = p.Level < a.RequiredLevel;
                UISkin.IconInSlot(new Rect(r.x + 22, y, 30, 30), UISkin.Icon(UISkin.AbilityIcon(a.Id)), locked ? new Color(0.4f, 0.4f, 0.4f) : Color.white, 0);
                GUI.Label(new Rect(r.x + 62, y - 2, 350, 22),
                    "<b><color=#" + Item.Hex(locked ? Color.gray : a.Color) + ">" + a.Name + "</color></b>  [" + a.Key + "]  " +
                    (locked ? "<color=#ff7766>level " + a.RequiredLevel + "</color>" : ""), UISkin.RichSmall);
                y += 34;
            }
        }

        void DrawQuestLog(Player p)
        {
            var r = new Rect(498, 140, 470, 500);
            if (UISkin.Window(r, "Quest Log", true, true)) showQuests = false;
            Block(r);
            float y = r.y + 60;
            if (p.Quests.Active.Count == 0)
                GUI.Label(new Rect(r.x + 26, y, 420, 60), "You have no active quests. Look for villagers with a <b>!</b> above their heads.", UISkin.InkRich);
            foreach (var q in p.Quests.Active)
            {
                bool ready = q.IsReady(p);
                GUI.Label(new Rect(r.x + 26, y, 420, 26), "<b>" + q.Def.Title + "</b>" + (ready ? "  <color=#7fe07a>(complete)</color>" : ""), UISkin.InkRich);
                if (NetClient.I.InParty && UISkin.Btn(new Rect(r.xMax - 112, y - 4, 86, 32), "Share", UISkin.Button))
                    NetClient.I.ShareQuest(q.Def);
                y += 26;
                GUI.Label(new Rect(r.x + 26, y, 420, 44), q.Def.Objective + "  <b>" + q.Progress(p) + "/" + q.Def.Count + "</b>", UISkin.Ink14);
                y += 46;
            }
            GUI.Label(new Rect(r.x + 26, r.yMax - 46, 420, 24), "Completed quests: " + p.Quests.Completed.Count, UISkin.Ink14);
        }

        void DrawWorldMap(Player p)
        {
            float size = Mathf.Min(VW, VH) - 130;
            var r = new Rect((VW - size) / 2, (VH - size) / 2 + 16, size, size);
            Block(new Rect(0, 0, VW, VH));
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, VW, VH), UISkin.White);
            GUI.color = Color.white;
            bool underground = Dungeon.Active;
            UISkin.Window(new Rect(r.x - 18, r.y - 60, r.width + 36, r.height + 78), underground ? Dungeon.ZoneName : "World Map", false);
            Vector3 o = underground ? Dungeon.Origin : Vector3.zero;
            float mw = underground ? Dungeon.Width : WorldGenerator.W, mh = underground ? Dungeon.Height : WorldGenerator.H;
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(r, Minimap.FoggedMap(underground));
            System.Func<Vector3, Vector2> toMap = w => new Vector2(r.x + (w.x - o.x) / mw * r.width, r.y + (1f - (w.z - o.z) / mh) * r.height);
            System.Action<Vector3, Color, float> mark = (w, c, s) => { var m = toMap(w); if (r.Contains(m)) DotAt(m, c, s); };

            if (underground)
            {
                foreach (var it in Interactable.All)
                    if (it is DungeonPortal portal && Exploration.Seen(portal.Position)) mark(portal.Position, portal.LabelColor, 11);
            }
            else
            {
                string[] zones = { "Whisperwood", "Goblin Encampment", "Forsaken Graveyard", "Ironvein Quarry", "Hollowmere", "Crypt of the Lich" };
                Vector3[] centers =
                {
                    WorldGenerator.Map(new Vector3(80, 0, 128)), WorldGenerator.Map(new Vector3(130, 0, 80)), WorldGenerator.Map(new Vector3(80, 0, 42)),
                    WorldGenerator.Map(new Vector3(30, 0, 80)), WorldGenerator.Map(new Vector3(80, 0, 80)), new Vector3(144.5f, 0, 18),
                };
                for (int i = 0; i < zones.Length; i++)
                {
                    if (!ZoneKnown(centers[i], 30f)) continue; // names appear once you've been nearby
                    var c = toMap(centers[i]);
                    UISkin.Shadowed(new Rect(c.x - 120, c.y - 12, 240, 26), zones[i], UISkin.HeadingCenter, new Color(1f, 0.92f, 0.75f), 2);
                }
                if (AdminTools.ShowDungeons)
                    foreach (var def in DungeonDef.All)
                    {
                        mark(def.Entrance, new Color(1f, 0.55f, 0.3f), 12);
                        var c = toMap(def.Entrance);
                        UISkin.Shadowed(new Rect(c.x - 100, c.y + 6, 200, 20), def.Name, UISkin.SmallCenter, new Color(1f, 0.7f, 0.45f), 2);
                    }
                foreach (var it in Interactable.All)
                    if (it is Npc npc && npc.Marker(p, out _) != null && Exploration.Seen(npc.Position)) mark(npc.Position, new Color(1f, 0.85f, 0.1f), 9);
            }
            if (AdminTools.ShowEnemies)
                foreach (var e in Enemy.ById.Values)
                    if (e != null && !e.IsDead) mark(e.transform.position, e.Def.Boss ? new Color(1f, 0.5f, 0f) : e.Elite ? Enemy.ChampionColor : new Color(0.9f, 0.15f, 0.1f), e.Def.Boss ? 10 : 6);
            foreach (var rp in RemotePlayer.ById.Values) if (rp != null) mark(rp.transform.position, NetClient.I.IsPartyMember(rp.Id) ? new Color(0.35f, 1f, 0.45f) : new Color(0.3f, 0.6f, 1f), 9);
            mark(p.transform.position, Color.white, 11);

            string hint = AdminTools.IsAdmin && !underground ? "Click or M to close   -   Admin: right-click to teleport there" : "Click anywhere or press M to close";
            UISkin.Shadowed(new Rect(r.x, r.yMax - 28, r.width, 24), hint, UISkin.SmallCenter, UISkin.Cream);
            int click = ClickedIn(new Rect(0, 0, VW, VH));
            if (click == 1 && AdminTools.IsAdmin && !underground && r.Contains(Event.current.mousePosition))
            {
                var mp = Event.current.mousePosition;
                float wx = (mp.x - r.x) / r.width * mw, wz = (1f - (mp.y - r.y) / r.height) * mh;
                AdminTools.Send(new AdminCmd { c = "tp", x = wx, z = wz });
                showMap = false;
            }
            else if (click >= 0) showMap = false;
        }

        /// <summary>True when any of the area around a point has been explored.</summary>
        static bool ZoneKnown(Vector3 center, float radius)
        {
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                if (Exploration.Seen(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * (i % 2 == 0 ? 0.4f : 1f))) return true;
            }
            return Exploration.Seen(center);
        }

        void DrawHelp()
        {
            var r = new Rect((VW - 600) / 2, 40, 600, Mathf.Min(720, VH - 50));
            if (UISkin.Window(r, "How to Play", true, true)) showHelp = false;
            Block(r);
            GUI.Label(new Rect(r.x + 28, r.y + 58, 544, r.height - 110),
                "<b>Combat</b>\n" +
                "Left-click the ground to move (hold to keep walking). Left-click a monster to attack it; Shift+click attacks in place.\n" +
                "<b>1-5</b> your class's abilities (right-click casts ability 2),  <b>Q / E</b> health / mana potions,  <b>R</b> recall to town (and back),  " +
                "<b>Alt</b> shows every item on the ground,  mouse wheel zooms.\n\n" +
                "<b>Windows</b>\n" +
                "<b>I</b> bags   <b>C</b> character   <b>T</b> talents   <b>K</b> skills   <b>L</b> quests   <b>M</b> map   <b>Enter</b> chat   <b>Esc</b> close / game menu\n" +
                "<b>Camera:</b> middle-drag or arrow keys rotate and tilt,  <b>Space</b> resets\n\n" +
                "<b>Chat & parties</b>\n" +
                "<b>/p</b> party chat,  <b>/w name</b> whisper,  <b>/r</b> reply,  <b>/invite name</b>,  <b>/leave</b>,  <b>/who</b>. " +
                "Click a player's name to invite them or trade. Party members nearby share kills; share quests from the quest log.\n\n" +
                "<b>Loot</b>\n" +
                "Legendaries (orange) carry unique powers; set pieces (green) grant bonuses at 2 and 4 pieces. Click a gem, then an item with a socket. " +
                "Vex fuses three gems into a better one. Keep spare loot in the stash chest in the square. " +
                "Beastmaster Orla, by the east road, hires out companions that fight at your side.\n\n" +
                "<b>The world</b>\n" +
                "Villagers with a <b>!</b> have quests; return to them when you see a <b>?</b>. Click trees, rocks and fishing spots to gather. " +
                "Smith at the anvil and cook at campfires. Sell loot to Merchant Lysa.\n\n" +
                "<b>Zones</b>\n" +
                "North: Whisperwood (1-7)    East: Goblin Encampment (3-10)\n" +
                "West: Ironvein Quarry (3-15)    South: Forsaken Graveyard (6-11)\n" +
                "Far south: Crypt of the Lich (boss)", UISkin.InkRich);
            GUI.Label(new Rect(r.x + 28, r.yMax - 44, 544, 26), "Graphics, sound and other settings: press <b>Esc</b> and choose <b>Settings</b>.", UISkin.InkRich);
        }

        void DrawDeath(Player p)
        {
            GUI.color = new Color(0.25f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, VW, VH), UISkin.White);
            GUI.color = Color.white;
            Block(new Rect(0, 0, VW, VH));
            UISkin.Shadowed(new Rect(0, VH * 0.28f, VW, 90), "You Have Died", UISkin.TitleHuge, new Color(0.9f, 0.2f, 0.15f), 2);
            if (UISkin.Btn(new Rect((VW - 320) / 2, VH * 0.28f + 110, 320, 52), "Release Spirit", UISkin.Button)) p.Respawn();
            UISkin.Shadowed(new Rect(0, VH * 0.28f + 168, VW, 24), "You will return to Hollowmere and lose 10% of your gold.", UISkin.SmallCenter, UISkin.Cream);
        }

        // =====================================================================================
        // NPC dialog / vendor / healer / crafting
        // =====================================================================================

        /// <summary>Tall enough for the quest's text (descriptions vary a lot in length).</summary>
        float QuestDialogHeight(Npc npc)
        {
            var q = npc.CurrentQuest(Player.I);
            if (q == null) return 300;
            var st = Player.I.Quests.Get(q.Id);
            string body = st != null && st.IsReady(Player.I) ? q.CompletionText : q.Description;
            float bodyH = Mathf.Max(60f, UISkin.V(UISkin.Ink14, wordWrap: true).CalcHeight(new GUIContent(body), 420));
            return 122 + 32 + bodyH + 10 + 46 + 40 + 46 + 40;
        }

        void DrawDialog(Player p)
        {
            var npc = dialogNpc;
            float height = npc.Role == NpcRole.Vendor && npc.Shop != null && npc.Shop.Kind == VendorKind.Companions ? 250 + CompanionDef.All.Length * 80
                : npc.Role == NpcRole.Vendor && npc.Shop != null ? 330 + npc.Shop.Items.Count * 54 + (npc.Shop.Kind == VendorKind.Curios ? 54 : 0)
                : npc.Role == NpcRole.QuestGiver ? QuestDialogHeight(npc) : 520;
            var r = new Rect(14, 120, 470, Mathf.Min(height, VH - 140));
            if (UISkin.Window(r, npc.DisplayName, true, true)) { dialogNpc = null; return; }
            Block(r);

            float y = r.y + 58;
            GUI.Label(new Rect(r.x + 26, y, 420, 56), "<i>\"" + npc.Greeting + "\"</i>", UISkin.InkRich);
            y += 64;

            switch (npc.Role)
            {
                case NpcRole.QuestGiver:
                {
                    var q = npc.CurrentQuest(p);
                    if (q == null)
                    {
                        GUI.Label(new Rect(r.x + 26, y, 420, 40), "I have nothing more for you. Thank you, hero.", UISkin.InkRich);
                        break;
                    }
                    var state = p.Quests.Get(q.Id);
                    UISkin.Shadowed(new Rect(r.x + 26, y, 420, 28), q.Title, UISkin.Heading, UISkin.Gold);
                    y += 32;
                    string body = state != null && state.IsReady(p) ? q.CompletionText : q.Description;
                    var bodyStyle = UISkin.V(UISkin.Ink14, wordWrap: true);
                    float bodyH = Mathf.Max(60f, bodyStyle.CalcHeight(new GUIContent(body), 420));
                    GUI.Label(new Rect(r.x + 26, y, 420, bodyH), body, bodyStyle);
                    y += bodyH + 10;
                    GUI.Label(new Rect(r.x + 26, y, 420, 44), "<b>Objective:</b> " + q.Objective, UISkin.Ink14);
                    y += 46;
                    string reward = "<b>Rewards:</b>  <color=#c49cff>" + q.RewardXp + " xp</color>,  <color=#f0c45a>" + q.RewardGold + " gold</color>";
                    if (q.RewardItemLevel > 0) reward += ",  <color=#" + Item.Hex(Item.RarityColor(q.RewardRarity)) + ">a " + q.RewardRarity + " item</color>";
                    GUI.Label(new Rect(r.x + 26, y, 420, 26), reward, UISkin.Ink14);
                    y += 40;

                    var br = new Rect(r.x + (r.width - 240) / 2, y, 240, 46);
                    if (state == null)
                    {
                        if (p.Level < q.MinLevel)
                            GUI.Label(new Rect(r.x + 26, y, 420, 30), "<color=#ff7a5c>Come back when you are level " + q.MinLevel + ".</color>", UISkin.InkRich);
                        else if (UISkin.Btn(br, "Accept Quest", UISkin.Button)) p.Quests.Accept(q);
                    }
                    else if (state.IsReady(p))
                    {
                        if (UISkin.Btn(br, "Complete Quest", UISkin.Button)) p.Quests.TurnIn(state, p);
                    }
                    else
                        GUI.Label(new Rect(r.x + 26, y, 420, 30), "Progress:  <b>" + state.Progress(p) + " / " + q.Count + "</b>", UISkin.InkRich);
                    break;
                }

                case NpcRole.Vendor:
                {
                    var shop = npc.Shop;
                    if (shop.Kind == VendorKind.Companions) { DrawCompanionShop(p, r, y); break; }
                    shop.Refresh(p.Level);
                    UISkin.Shadowed(new Rect(r.x + 26, y, 420, 28), "For Sale", UISkin.Heading, UISkin.Gold);
                    if (shop.Rotates)
                        GUI.Label(new Rect(r.x + 200, y + 4, 244, 24), "New stock in " + Mathf.CeilToInt(shop.SecondsUntilRestock / 60f) + " min",
                            UISkin.V(UISkin.Ink14, alignment: TextAnchor.UpperRight));
                    y += 36;
                    for (int i = 0; i < shop.Items.Count; i++)
                        y = ShopRow(p, r, y, shop, i);
                    y += 14;
                    GUI.Label(new Rect(r.x + 26, y, 420, 50),
                        "Right-click items in your bags to sell them. You have <color=#f0c45a><b>" + p.Gold + " gold</b></color>.", UISkin.Ink14);
                    y += 56;
                    if (UISkin.Btn(new Rect(r.x + (r.width - 340) / 2, y, 340, 46), "Sell Common Items & Materials", UISkin.Button))
                    {
                        for (int i = 0; i < p.Inventory.Slots.Length; i++)
                        {
                            var it = p.Inventory.Slots[i];
                            if (it == null || it.Kind == ItemKind.Consumable || it.Kind == ItemKind.Gem) continue;
                            if (it.Kind == ItemKind.Equipment && it.Rarity != Rarity.Common) continue;
                            Sell(p, i);
                        }
                    }
                    if (shop.Kind == VendorKind.Curios)
                    {
                        y += 54;
                        if (UISkin.Btn(new Rect(r.x + (r.width - 340) / 2, y, 340, 46), "Fuse Three Gems", UISkin.Button)) p.CombineGems();
                        if (new Rect(r.x + (r.width - 340) / 2, y, 340, 46).Contains(Event.current.mousePosition))
                            tooltip = "Three Chipped gems of a kind become one Flawless gem (50 gold).\nThree Flawless become one Perfect (250 gold).";
                    }
                    break;
                }

                case NpcRole.Healer:
                    if (UISkin.Btn(new Rect(r.x + (r.width - 240) / 2, y, 240, 46), "Heal Me", UISkin.Button))
                    {
                        p.Heal(p.MaxHealth);
                        p.RestoreMana(p.MaxMana);
                        FxPulse.Ring(p.transform.position, new Color(1f, 1f, 0.6f), 2.5f, 0.6f);
                        Log("Sister Mae: \"Go with the Light.\"", new Color(0.4f, 1f, 0.4f));
                    }
                    break;
            }
        }

        /// <summary>One item for sale. Stackables can be bought 1 or 5 at a time; equipment is unique and leaves the shop.</summary>
        float ShopRow(Player p, Rect r, float y, VendorStock shop, int index)
        {
            var item = shop.Items[index];
            int price = VendorStock.Price(item);
            var slot = new Rect(r.x + 26, y, 44, 44);
            DrawItemSlot(slot, item, p);
            if (slot.Contains(Event.current.mousePosition))
                ItemTooltip(item, p, null);
            var nameColor = item.Kind == ItemKind.Equipment ? Item.RarityColor(item.Rarity) : UISkin.Cream;
            GUI.Label(new Rect(r.x + 80, y + 2, 190, 24), "<b><color=#" + Item.Hex(nameColor) + ">" + item.Name + "</color></b>", UISkin.InkRich);
            GUI.Label(new Rect(r.x + 80, y + 23, 190, 22), "<color=#f0c45a>" + price + " gold</color>" +
                (item.Kind == ItemKind.Equipment && item.RequiredLevel > p.Level ? "   <color=#ff7a5c>level " + item.RequiredLevel + "</color>" : ""), UISkin.Ink14);

            int[] amounts = item.Stackable ? new[] { 1, 5 } : new[] { 1 };
            foreach (int n in amounts)
            {
                var b = item.Stackable ? new Rect(r.x + (n == 1 ? 280 : 366), y + 2, 80, 40) : new Rect(r.x + 336, y + 2, 110, 40);
                if (!UISkin.Btn(b, item.Stackable ? "Buy " + n : "Buy", UISkin.Button)) continue;
                if (p.Gold < price * n) { Log("You don't have enough gold.", new Color(1f, 0.4f, 0.4f)); continue; }
                var bought = item.Stackable ? ItemDatabase.ByName(item.Name) : item;
                bought.Count = n;
                if (!p.Inventory.Add(bought)) { Log("Your bags are full.", new Color(1f, 0.4f, 0.4f)); continue; }
                p.Gold -= price * n;
                Sfx.Play2D("coins", 0.5f);
                Log("Bought " + item.Name + (n > 1 ? " x" + n : "") + " for " + price * n + " gold.", new Color(1f, 0.85f, 0.2f));
                if (!item.Stackable) { shop.Items.RemoveAt(index); NetClient.I?.SaveNow(); break; }
            }
            return y + 54;
        }

        void DrawCrafting(Player p)
        {
            var s = craftStation;
            var r = new Rect(14, 140, 470, 150 + s.Recipes.Length * 78);
            if (UISkin.Window(r, s.DisplayName + "  -  " + s.Skill + " " + p.Skills.Level(s.Skill))) { craftStation = null; return; }
            Block(r);
            float y = r.y + 60;
            foreach (var rec in s.Recipes)
            {
                bool canLevel = p.Skills.Level(s.Skill) >= rec.LevelRequired;
                int have = p.Inventory.CountOf(rec.Input);
                var row = new Rect(r.x + 18, y - 6, r.width - 36, 70);
                UISkin.Box(row, UISkin.Inset);
                var inputItem = ItemDatabase.ByName(rec.Input);
                UISkin.IconInSlot(new Rect(r.x + 28, y + 4, 46, 46), UISkin.Icon(UISkin.IconKey(inputItem)), UISkin.IconTint(inputItem), 0);
                UISkin.Shadowed(new Rect(r.x + 84, y, 250, 24), rec.Name, UISkin.Label, canLevel ? UISkin.Cream : UISkin.Muted);
                GUI.Label(new Rect(r.x + 84, y + 24, 230, 40),
                    rec.InputCount + " " + rec.Input + "  (have " + have + ")\n" +
                    (canLevel ? "<color=#9fe08a>+" + rec.Xp + " xp</color>" : "<color=#ff7766>Requires level " + rec.LevelRequired + "</color>"), UISkin.RichSmall);
                GUI.enabled = canLevel && have >= rec.InputCount;
                if (UISkin.Btn(new Rect(r.xMax - 168, y + 6, 70, 42), "Make", UISkin.Button)) rec.Craft(p);
                if (UISkin.Btn(new Rect(r.xMax - 92, y + 6, 66, 42), "All", UISkin.Button))
                    for (int i = 0; i < 50 && p.Inventory.CountOf(rec.Input) >= rec.InputCount; i++)
                        if (!rec.Craft(p)) break;
                GUI.enabled = true;
                y += 78;
            }
            UISkin.Shadowed(new Rect(r.x + 24, r.yMax - 44, 420, 26),
                s.Skill == SkillType.Smithing ? "Higher Smithing levels forge better gear." : "Higher Cooking levels burn less food.", UISkin.Small, UISkin.Muted);
        }

        // =====================================================================================
        // Banner & tooltip
        // =====================================================================================

        void DrawBanner()
        {
            float age = Time.time - bannerTime;
            if (age > 3.5f || string.IsNullOrEmpty(bannerText)) return;
            var c = bannerColor;
            c.a = age < 0.2f ? age / 0.2f : age > 2.5f ? 1f - (age - 2.5f) : 1f;
            UISkin.Shadowed(new Rect(0, VH * 0.17f, VW, 50), bannerText, UISkin.Banner, c, 2);
        }

        /// <summary>Second tooltip shown next to the main one (Shift: the item you have equipped in that slot).</summary>
        string tooltipCompare;

        /// <summary>
        /// An item's tooltip with its stat differences to what's equipped. Holding Shift also shows the equipped
        /// item itself, side by side.
        /// </summary>
        void ItemTooltip(Item item, Player p, string hint)
        {
            var equipped = item.Kind == ItemKind.Equipment ? p.Inventory.GetEquipped(item.Slot) : null;
            bool shift = Event.current.shift;
            tooltip = item.Tooltip(p, equipped) + (hint != null ? "\n<color=#998877>" + hint + "</color>" : "") +
                      (equipped != null && !shift ? "\n<color=#7f9fff>Hold Shift to compare with your equipped " + Item.SlotName(item.Slot).ToLower() + " item</color>" : "");
            tooltipCompare = equipped != null && shift ? "<color=#c8a060><b>Currently equipped</b></color>\n" + equipped.Tooltip(p) : null;
        }

        void DrawTooltip()
        {
            if (string.IsNullOrEmpty(tooltip)) { tooltipCompare = null; return; }
            var content = new GUIContent(tooltip);
            float w = 330;
            float h = UISkin.Rich.CalcHeight(content, w - 28) + 26;
            var m = Event.current.mousePosition;
            var r = new Rect(m.x + 20, m.y + 20, w, h);
            if (r.xMax > VW) r.x = m.x - w - 12;
            if (r.yMax > VH) r.y = Mathf.Max(0, VH - h);
            if (!string.IsNullOrEmpty(tooltipCompare))
            {
                var cc = new GUIContent(tooltipCompare);
                float ch = UISkin.Rich.CalcHeight(cc, w - 28) + 26;
                // beside the main tooltip: to its left if there is room, else to its right
                var cr = new Rect(r.x - w - 8, r.y, w, ch);
                if (cr.x < 0) { cr.x = r.xMax + 8; if (cr.xMax > VW) { r.x = Mathf.Max(0, VW - 2 * w - 8); cr.x = r.xMax + 8; } }
                if (cr.yMax > VH) cr.y = Mathf.Max(0, VH - ch);
                UISkin.Box(cr, UISkin.Tooltip);
                GUI.Label(new Rect(cr.x + 14, cr.y + 13, w - 28, ch - 20), cc, UISkin.Rich);
            }
            UISkin.Box(r, UISkin.Tooltip);
            GUI.Label(new Rect(r.x + 14, r.y + 13, w - 28, h - 20), content, UISkin.Rich);
            tooltip = null;
            tooltipCompare = null;
        }

        // =====================================================================================
        // Helpers
        // =====================================================================================

        void Block(Rect r) => blockRects.Add(r);

        /// <summary>Returns the mouse button (0 = left, 1 = right) clicked inside r this event, or -1.</summary>
        int ClickedIn(Rect r)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || !r.Contains(e.mousePosition)) return -1;
            int b = e.button;
            e.Use();
            return b;
        }

        static Color LevelColor(int level, int playerLevel)
        {
            int d = level - playerLevel;
            if (d >= 5) return new Color(1f, 0.2f, 0.15f);
            if (d >= 3) return new Color(1f, 0.55f, 0.2f);
            if (d >= -2) return new Color(1f, 0.92f, 0.3f);
            if (d >= -6) return new Color(0.4f, 1f, 0.35f);
            return new Color(0.65f, 0.65f, 0.65f);
        }
    }
}
