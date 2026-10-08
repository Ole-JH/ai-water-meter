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

        /// <summary>The playtest tour (PlaytestTour) opens and closes windows by name.</summary>
        public static void CheckShow(string window, bool on)
        {
            if (I == null) return;
            switch (window)
            {
                case "bags": I.showBags = on; break;
                case "char": I.showChar = on; break;
                case "talents": I.showTalents = on; break;
                case "map": I.showMap = on; break;
                case "achievements": I.showAchievements = on; break;
                case "comfort": I.menu = on ? MenuPage.Comfort : MenuPage.None; break;
            }
        }

        // ---- state read by gameplay code
        public bool MouseOverUI { get; private set; }
        public bool ChatOpen { get; private set; }
        public bool KeyboardCaptured => ChatOpen || Player.I == null || tradeGoldFocused || (auctionOpen && auctionFieldFocused) || menu != MenuPage.None || (showAdmin && adminFieldFocused) || (showGuild && guildFieldFocused);
        public bool BlocksWorldInput => Player.I == null || Player.I.IsDead || showMap || menu != MenuPage.None;

        // ---- windows
        bool showBags, showChar, showSkills, showQuests, showMap, showHelp, showTalents;
        Npc dialogNpc;
        /// <summary>The NPC whose window is open (a vendor, a quest giver), or null.</summary>
        public static Npc TalkingTo => I != null ? I.dialogNpc : null;
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
        struct FloatText { public Vector3 Pos; public string Text; public Color Color; public float Time, Size, Drift; public bool Shake; }
        struct LogLine { public string Text, Stamp, Who; public Color Color; public float Time; public Item Item; }
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
            // Nothing here uses GUILayout: skip IMGUI's layout pass, which would run all of OnGUI once more per event.
            useGUILayout = false;
            try
            {
                loginName = PlayerPrefs.GetString("sf_name", "");
                serverUrl = PlayerPrefs.GetString("sf_server", "ws://localhost:7341/ws");
                loginLook = PlayerPrefs.GetString("sf_look", "Knight");
            }
            catch (System.Exception) { }
            ReadResetLink();
            cursorDefault = Resources.Load<Texture2D>("UI/Cursors/cursorGauntlet_bronze");
            cursorAttack = Resources.Load<Texture2D>("UI/Cursors/cursorSword_gold");
            cursorInteract = Resources.Load<Texture2D>("UI/Cursors/cursorHand_beige");
        }

        // =====================================================================================
        // Static API used by gameplay code
        // =====================================================================================

        /// <summary>A damage number, if the comfort settings want it (<paramref name="big"/>: a crit or a hit on you).</summary>
        public static void Damage(Vector3 worldPos, string text, Color color, float size, bool shake, bool big)
        {
            int mode = GameSettings.DamageNumbers;
            if (mode == 2 || (mode == 1 && !big)) return;
            Float(worldPos, text, color, size, shake);
        }

        public static void Float(Vector3 worldPos, string text, Color color, float size = 1f) => Float(worldPos, text, color, size, false);

        /// <summary>A floating number or word: pops out, arcs off to one side and fades; <paramref name="shake"/> for crits and hits on us.</summary>
        public static void Float(Vector3 worldPos, string text, Color color, float size, bool shake)
        {
            // several at once from the same spot (xp, gold, a level up...) stack up instead of printing over each other
            int near = 0;
            foreach (var f in floats)
                if (Time.time - f.Time < 0.6f && Factory.FlatDistance(f.Pos, worldPos) < 1.2f && Mathf.Abs(f.Pos.y - worldPos.y) < 2.5f) near++;
            worldPos += Vector3.up * (0.42f * Mathf.Min(near, 6));
            floats.Add(new FloatText { Pos = worldPos + new Vector3(Random.Range(-0.3f, 0.3f), 0, 0), Text = text, Color = color, Time = Time.time, Size = size,
                Drift = Random.Range(-1f, 1f), Shake = shake });
            if (floats.Count > 80) floats.RemoveAt(0);
        }

        public static void Log(string text, Color color) => Log(text, color, null, null);

        /// <summary>A chat line: <paramref name="who"/> said it (their name in it is clickable), <paramref name="item"/> is linked in it.</summary>
        public static void Log(string text, Color color, string who, Item item)
        {
            log.Add(new LogLine { Text = text, Color = color, Time = Time.time, Stamp = System.DateTime.Now.ToString("HH:mm"), Who = who, Item = item });
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
                npc.Shop.Refresh(true);
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

        int windowsOpen = -1;

        /// <summary>A window opening sounds like a page turned, one closing like a book shut.</summary>
        void WindowSounds()
        {
            // (the forge, auction, rift and vendor windows have sounds of their own)
            bool[] open = { showBags, showChar, showSkills, showQuests, showMap, showTalents, showAchievements, showGuild };
            int mask = 0;
            for (int i = 0; i < open.Length; i++) if (open[i]) mask |= 1 << i;
            if (windowsOpen >= 0 && mask != windowsOpen)
            {
                bool opened = (mask & ~windowsOpen) != 0;
                Sfx.Play2D(opened ? "book" : "ui_close", opened ? 0.3f : 0.35f, opened ? 1.15f : 1f);
            }
            windowsOpen = mask;
        }

        void Update()
        {
            scale = UiScaleNow();
            var mp = GameInput.MousePosition;
            var guiMouse = new Vector2(mp.x / scale, (Screen.height - mp.y) / scale);
            bool over = false;
            foreach (var r in blockRects) if (r.Contains(guiMouse)) { over = true; break; }
            MouseOverUI = over;

            var p = Player.I;
            UpdateCursor(p);
            if (p == null) { ChatOpen = false; return; }
            CheckNews(p);
            WindowSounds();

            if (!ChatOpen)
            {
                bool before = showBags | showChar | showSkills | showQuests | showMap | showHelp | showTalents | showAchievements | menu != MenuPage.None;
                bool questsBefore = showQuests;
                WindowKeys();
                AdminKeys();
                bool after = showBags | showChar | showSkills | showQuests | showMap | showHelp | showTalents | showAchievements | menu != MenuPage.None;
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
                if (GameInput.Down(GKey.G)) showEmotes = !showEmotes;
                if (GameInput.Down(GKey.Y)) showAchievements = !showAchievements;
                if (GameInput.Down(GKey.O)) showGuild = !showGuild;
                if (GameInput.Down(GKey.Escape))
                {
                    if (waystoneOpen != null) waystoneOpen = null;
                    else if (chooseDungeon >= 0) chooseDungeon = -1;
                    else if (dialogNpc != null || craftStation != null || forgeOpen || riftOpen || auctionOpen) { dialogNpc = null; craftStation = null; forgeOpen = false; riftOpen = false; auctionOpen = false; }
                    else if (showGuild) showGuild = false;
                    else if (guildBoardOpen) guildBoardOpen = false;
                    else if (tradeOpen) NetClient.I?.CancelTrade();
                    else if (menu != MenuPage.None) menu = menu == MenuPage.Main ? MenuPage.None : MenuPage.Main;
                    else if (showNews) CloseNews(Player.I);
                    else if (showEmotes) showEmotes = false;
                    else if (showBags | showChar | showSkills | showQuests | showMap | showHelp | showTalents | showStash | showAdmin | showAchievements)
                        showBags = showChar = showSkills = showQuests = showMap = showHelp = showTalents = showStash = showAdmin = showAchievements = false;
                    else menu = MenuPage.Main; // nothing to close: open the game menu
                }
            }
        }

        void UpdateCursor(Player p)
        {
            var want = CursorKind.Default;
            if (p != null && !MouseOverUI)
            {
                if (p.HoveredEnemy != null || p.HoveredFoe != null) want = CursorKind.Attack;
                else if (p.HoveredInteractable != null || RemotePlayerUnderMouse() != null) want = CursorKind.Interact;
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

        /// <summary>Phones and tablets (in their browser): the interface is drawn bigger, for fingers.</summary>
        public static bool Touch => Application.isMobilePlatform;

        /// <summary>
        /// How big the interface is drawn: by the screen's height and the player's UI scale (bigger on touch screens),
        /// but never so big that the virtual screen gets narrower or shorter than the windows need: on a small
        /// screen (a phone, a short laptop at UI scale 150%) everything still fits, just smaller.
        /// </summary>
        static float UiScaleNow()
        {
            float user = GameSettings.UiScale * (Touch ? 1.25f : 1f);
            float s = Screen.height / RefHeight * user;
            float minW = Touch ? 1024f : 1150f, minH = Touch ? 800f : 820f;
            s = Mathf.Min(s, Screen.width / minW, Screen.height / minH);
            return Mathf.Max(0.3f, s);
        }

        /// <summary>A phone held upright: the game is a landscape game, say so.</summary>
        void DrawTurnSideways()
        {
            if (!Touch || Screen.height <= Screen.width) return;
            var r = new Rect(20, VH * 0.35f, VW - 40, 120);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(r, UISkin.White);
            GUI.color = Color.white;
            UISkin.Shadowed(new Rect(r.x, r.y + 20, r.width, 40), "Turn your phone sideways", UISkin.V(UISkin.HeadingCenter, fontSize: 30), UISkin.Gold, 2);
            UISkin.Shadowed(new Rect(r.x, r.y + 66, r.width, 30), "Shadowfall is made for a wide screen.", UISkin.V(UISkin.LabelCenter, fontSize: 20), UISkin.Cream, 2);
        }

        void OnGUI()
        {
            UISkin.Init();
            if (Event.current.type == EventType.Repaint) blockRects.Clear(); // collected while painting, read by Update
            scale = UiScaleNow();
            VW = Screen.width / scale;
            VH = Screen.height / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            tooltip = null;
            laneY = VH * 0.13f + (bossShown != null ? 70f : 0f);

            TextInputEvents();
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
            // Holding the button to walk sends a drag event every frame: with nothing of the interface under the mouse or
            // being dragged, there's nothing for it to do, and the whole HUD would be run through a second time a frame.
            if (et == EventType.MouseDrag && !MouseOverUI && GUIUtility.hotControl == 0 && barDrag < 0 && bagDrag < 0 && menu == MenuPage.None) return;
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
            if (showAchievements) DrawAchievements(p);
            if (showGuild) DrawGuild(p);
            if (guildBoardOpen) DrawGuildBoard(p);
            if (riftOpen) DrawRiftWindow(p);
            if (showTalents) DrawTalents(p);
            if (showStash) DrawStash(p);
            if (tradeOpen) DrawTrade(p);
            if (showAdmin) DrawAdmin(p);
            if (chooseDungeon >= 0) DrawDifficultyPicker(p);
            if (waystoneOpen != null) DrawWaystone(p);
            else tradeGoldFocused = false;
            if (dialogNpc != null && dialogNpc.Away) dialogNpc = null; // fled a burning quarter mid-sale
            if (dialogNpc != null) DrawDialog(p);
            if (craftStation != null) DrawCrafting(p);
            if (forgeOpen) DrawForge(p);
            if (auctionOpen) DrawAuction(p);
            if (showHelp) DrawHelp();
            if (showNews) DrawNews(p);
            if (showEmotes) DrawEmotes(p);
            if (showMap) DrawWorldMap(p);
            if (menuPlayer != null) DrawPlayerMenu();
            // the announcements down the middle share one lane, each below the last (never on top of each other)
            DrawTitleCard();
            DrawBanner();
            DrawAchievementToasts();
            DrawOffers();
            if (p.IsDead) DrawDeath(p);
            DrawScreenFlash();
            DrawFlyers();
            DrawTurnSideways();
            if (menu != MenuPage.None) DrawGameMenu(p);

            DrawRecoveryCode();
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
                // kept on screen (a speaker at the edge still gets a whole bubble; its tail keeps pointing at them)
                r.x = Mathf.Clamp(r.x, 6f, VW - r.width - 6f);
                r.y = Mathf.Max(r.y, 6f);
                if (g.x < 0f || g.x > VW) continue; // off screen to the side: don't float a bubble with nobody under it
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

        float partyTop = 120; // under the hero frame (and the attribute button and companion frame, when shown)

        /// <summary>Each other party member's color on the frames and the maps, by their place in the party.</summary>
        static readonly Color[] memberColors =
        {
            new Color(0.4f, 1f, 0.5f), new Color(0.35f, 0.85f, 1f), new Color(1f, 0.7f, 0.3f), new Color(1f, 0.5f, 0.85f),
        };

        public static Color MemberColor(int id)
        {
            var net = NetClient.I;
            int k = 0;
            if (net != null)
                foreach (var m in net.Party)
                {
                    if (m.id == net.MyId) continue;
                    if (m.id == id) return memberColors[k % memberColors.Length];
                    k++;
                }
            return memberColors[0];
        }

        /// <summary>Where a party member is, as seen from here: distance in the same place, else the dungeon or zone.</summary>
        static string MemberWhere(NetPartyMember m, Player p, out bool here)
        {
            var net = NetClient.I;
            here = m.di == net.DungeonId;
            if (here)
            {
                Vector3 at = RemotePlayer.ById.TryGetValue(m.id, out var rp) && rp != null ? rp.transform.position : new Vector3(m.x, 0f, m.z);
                return Mathf.RoundToInt(Factory.FlatDistance(at, p.transform.position)) + " m away";
            }
            if (!string.IsNullOrEmpty(m.dn)) return m.dn;
            return WorldGenerator.ZoneAt(new Vector3(m.x, 0f, m.z));
        }

        /// <summary>One frame per other party member: live portrait, name, level and class, life, mana and where they are.</summary>
        void DrawPartyFrames(Player p)
        {
            var net = NetClient.I;
            if (!net.InParty) return;
            float y = partyTop;
            int k = 0;
            foreach (var m in net.Party)
            {
                if (m.id == net.MyId) continue;
                var col = memberColors[k++ % memberColors.Length];
                float hp = m.hp, mhp = m.mhp;
                bool near = RemotePlayer.ById.TryGetValue(m.id, out var rp) && rp != null;
                if (near) { hp = rp.Health; mhp = rp.MaxHealth; }
                string cls = string.IsNullOrEmpty(m.mdl) ? "Knight" : m.mdl;
                string where = MemberWhere(m, p, out bool here);

                var r = new Rect(12, y, 290, 82);
                UISkin.Box(r, UISkin.PanelPlain);
                Block(r);
                GUI.color = col;
                GUI.DrawTexture(new Rect(r.x + 3, r.y + 8, 3, r.height - 16), UISkin.White);
                GUI.color = Color.white;

                // Portrait: the member's live 3D model (or their class icon until it is ready), with a level badge.
                var pr = new Rect(r.x + 10, r.y + 8, 66, 66);
                UISkin.Box(pr, UISkin.Slot);
                var tex = PartyPortraits.For(m.id);
                var tint = m.dead ? new Color(0.4f, 0.36f, 0.36f) : Color.white;
                if (tex != null)
                {
                    GUI.color = tint;
                    if (Event.current.type == EventType.Repaint)
                        GUI.DrawTextureWithTexCoords(new Rect(pr.x + 4, pr.y + 4, pr.width - 8, pr.height - 8), tex, Avatar.HeadCrop, true);
                    GUI.color = Color.white;
                }
                else UISkin.IconInSlot(pr, UISkin.Icon(cls.ToLower()), tint, 6);
                if (m.dead) UISkin.Shadowed(new Rect(pr.x, pr.y + 22, pr.width, 22), "DEAD", UISkin.V(UISkin.SmallCenter, fontSize: 14), new Color(1f, 0.35f, 0.3f), 2);
                var lv = new Rect(pr.xMax - 22, pr.yMax - 20, 26, 22);
                UISkin.Box(lv, UISkin.Slot);
                UISkin.Shadowed(lv, m.lvl.ToString(), UISkin.SmallCenter, UISkin.Gold);

                // Name, class and where; life and mana.
                bool leader = m.id == net.PartyLeader;
                float tx = pr.xMax + 10, tw = r.xMax - tx - 10;
                UISkin.Shadowed(new Rect(tx, r.y + 6, tw - (net.IsLeader ? 26 : 0), 20), m.name, UISkin.V(UISkin.Label, fontSize: 16), col);
                if (leader) UISkin.Shadowed(new Rect(tx, r.y + 6, tw - (net.IsLeader ? 30 : 4), 20), "Leader", UISkin.V(UISkin.Small, alignment: TextAnchor.MiddleRight), UISkin.Gold);
                UISkin.Shadowed(new Rect(tx, r.y + 25, tw, 16), cls + "  -  " + where, UISkin.V(UISkin.Small, fontSize: 12),
                    here ? UISkin.Muted : new Color(0.75f, 0.68f, 0.9f));
                UISkin.Bar(new Rect(tx, r.y + 45, tw, 15), mhp > 0 ? hp / mhp : 0f, "Red",
                    m.dead ? "Dead" : Mathf.CeilToInt(hp) + " / " + Mathf.CeilToInt(mhp), new Color(0.75f, 0.12f, 0.1f));
                if (m.mmp > 0) UISkin.Bar(new Rect(tx, r.y + 64, tw, 9), m.mp / m.mmp, "Blue", null, new Color(0.2f, 0.35f, 0.9f));
                // the ready check: a mark on the portrait for 40 s (green ready, red not, grey waiting)
                if (Time.time - net.ReadyAt < 40f)
                {
                    bool answered = net.ReadyAnswers.TryGetValue(m.id, out bool ready);
                    var mark = new Rect(pr.x - 4, pr.y - 4, 24, 24);
                    GUI.color = answered ? (ready ? new Color(0.25f, 0.8f, 0.3f) : new Color(0.85f, 0.25f, 0.2f)) : new Color(0.4f, 0.4f, 0.4f);
                    GUI.DrawTexture(mark, UISkin.Circle);
                    GUI.color = Color.white;
                    UISkin.Shadowed(mark, answered ? (ready ? "OK" : "NO") : "?", UISkin.V(UISkin.SmallCenter, fontSize: 11), Color.white, 1);
                }

                if (net.IsLeader)
                {
                    var kick = new Rect(r.xMax - 28, r.y + 5, 22, 22);
                    if (UISkin.Btn(kick, "x", UISkin.SquareButton)) net.KickFromParty(m.id);
                    if (kick.Contains(Event.current.mousePosition)) tooltip = "Remove " + m.name + " from the party";
                }
                else if (r.Contains(Event.current.mousePosition))
                    tooltip = "<b><color=#" + Item.Hex(col) + ">" + m.name + "</color></b>  level " + m.lvl + " " + cls + (leader ? "  (party leader)" : "") +
                              "\nLife " + Mathf.CeilToInt(hp) + " / " + Mathf.CeilToInt(mhp) + (m.mmp > 0 ? ",  mana " + Mathf.FloorToInt(m.mp) + " / " + Mathf.FloorToInt(m.mmp) : "") +
                              "\n" + where + (here ? "" : "\n<color=#998877>Not in the same place as you</color>");
                y += 88;
            }
            var leave = new Rect(12, y, 130, 32);
            Block(leave);
            if (UISkin.Btn(leave, "Leave Party", UISkin.Button)) net.LeaveParty();
            if (net.IsLeader)
            {
                // the leader's tools: a ready check, and the loot rule for gold
                var rcb = new Rect(148, y, 74, 32);
                Block(rcb);
                if (UISkin.Btn(rcb, "Ready?", UISkin.Button)) net.StartReadyCheck();
                if (rcb.Contains(Event.current.mousePosition)) tooltip = "Ready check: ask everyone in the party if they're ready (or type /ready)";
                var gb = new Rect(228, y, 74, 32);
                Block(gb);
                if (UISkin.Btn(gb, net.ShareGold ? "Split" : "Keep", UISkin.Button)) net.SetShareGold(!net.ShareGold);
                if (gb.Contains(Event.current.mousePosition))
                    tooltip = "<b>Loot rule for gold</b>\n" + (net.ShareGold ? "Shared: gold anyone picks up is split with the party nearby." : "Finders keepers: gold goes to whoever picks it up.") +
                              "\n<color=#998877>Click to switch. Items are always each player's own.</color>";
            }
            else UISkin.Shadowed(new Rect(148, y + 6, 160, 20), net.ShareGold ? "Gold is shared" : "Finders keepers", UISkin.V(UISkin.Small, fontSize: 12), UISkin.Muted);
        }

        void DrawPlayerMenu()
        {
            var rp = menuPlayer;
            if (rp == null) { menuPlayer = null; return; }
            var net = NetClient.I;
            bool canInvite = !net.IsPartyMember(rp.Id) && (!net.InParty || net.IsLeader);
            bool canDuel = !Dungeon.Active && !Duel.Active;
            var r = new Rect(menuPos.x - 100, menuPos.y + 10, 200, 60 + (canInvite ? 44 : 0) + (canDuel ? 44 : 0) + 132);
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
            if (canDuel)
            {
                if (UISkin.Btn(new Rect(r.x + 16, y, r.width - 32, 38), "Challenge to Duel", UISkin.Button)) { Duel.Challenge(rp); menuPlayer = null; }
                y += 44;
            }
            if (UISkin.Btn(new Rect(r.x + 16, y, r.width - 32, 38), "Close", UISkin.Button)) menuPlayer = null;
            if (Event.current.type == EventType.MouseDown && !r.Contains(Event.current.mousePosition) && Time.frameCount > menuOpenedFrame + 1) menuPlayer = null;
        }

        int menuOpenedFrame;

        /// <summary>Opens the invite / whisper / trade menu for a player, at the mouse.</summary>
        public void OpenPlayerMenu(RemotePlayer rp)
        {
            var mp = GameInput.MousePosition;
            menuPlayer = rp;
            menuPos = new Vector2(mp.x / scale, (Screen.height - mp.y) / scale);
            menuOpenedFrame = Time.frameCount;
            Sfx.Play2D("ui_click", 0.4f);
        }

        /// <summary>
        /// The other player whose character (not just the name) is under the mouse: their body, from the feet to above
        /// the head, as it appears on screen. The nearest one to the camera wins.
        /// </summary>
        public RemotePlayer RemotePlayerUnderMouse()
        {
            var cam = GameManager.I != null ? GameManager.I.Cam : Camera.main;
            if (cam == null) return null;
            Vector2 m = GameInput.MousePosition;
            RemotePlayer best = null;
            float bestDepth = float.MaxValue;
            plateAlpha = 1f;
            foreach (var rp in RemotePlayer.ById.Values)
            {
                if (rp == null) continue;
                var feet = cam.WorldToScreenPoint(rp.transform.position);
                var head = cam.WorldToScreenPoint(rp.transform.position + Vector3.up * 2.2f);
                if (feet.z <= 0f) continue;
                float h = Mathf.Abs(head.y - feet.y), w = Mathf.Max(24f, h * 0.55f);
                var r = new Rect(feet.x - w / 2f, Mathf.Min(feet.y, head.y) - h * 0.05f, w, h * 1.1f);
                if (r.Contains(m) && feet.z < bestDepth) { best = rp; bestDepth = feet.z; }
            }
            return best;
        }

        /// <summary>Party invitations and shared quests waiting for an answer.</summary>
        void DrawOffers()
        {
            var net = NetClient.I;
            var inv = net.PartyInvite;
            float y = laneY; // below the announcements; each question takes its own place
            if (inv != null)
            {
                if (Time.time - inv.Time > 60f) net.AnswerPartyInvite(false);
                else if (OfferBox(y, "<b>" + inv.Name + "</b> invites you to join a party.", out bool yes)) net.AnswerPartyInvite(yes);
                y += 140;
            }
            var rc = net.ReadyPrompt;
            if (rc != null)
            {
                if (Time.time - rc.Time > 30f) net.AnswerReady(false);
                else if (OfferBox(y, "<b>" + rc.Name + "</b> asks: are you ready?", out bool yes)) net.AnswerReady(yes);
                y += 140;
            }
            var ti = net.TradeInvite;
            if (ti != null)
            {
                if (Time.time - ti.Time > 60f) net.AnswerTradeInvite(false);
                else if (OfferBox(y, "<b>" + ti.Name + "</b> wants to trade with you.", out bool yes)) net.AnswerTradeInvite(yes);
                y += 140;
            }
            if (Guild.InviteFrom != null)
            {
                if (Time.time - Guild.InviteTime > 60f) Guild.Answer(false);
                else if (OfferBox(y, "<b>" + Guild.InviteFrom + "</b> invites you to join\n<b>" + Guild.InviteGuild + "</b>.", out bool yes)) Guild.Answer(yes);
                y += 140;
            }
            if (Duel.ChallengerId != 0)
            {
                if (Time.time - Duel.ChallengeTime > 30f) Duel.Answer(false);
                else if (OfferBox(y, "<b>" + Duel.ChallengerName + "</b> challenges you to a duel.\nNobody dies: the first to drop to their last breath loses.", out bool yes)) Duel.Answer(yes);
                y += 140;
            }
            if (Duel.Phase == "count")
                UISkin.Shadowed(new Rect(0, VH * 0.3f, VW, 80), Duel.Countdown > 0 ? Duel.Countdown.ToString() : "Fight!", UISkin.V(UISkin.HeadingCenter, fontSize: 64), Duel.Color, 3);
            var q = net.QuestOffer;
            if (q != null)
            {
                if (Time.time - q.Time > 60f) net.AnswerQuestOffer(false);
                else if (OfferBox(y, "<b>" + q.Name + "</b> shares a quest:\n<b>" + q.Quest.Title + "</b>  -  " + q.Quest.Objective, out bool yes))
                    net.AnswerQuestOffer(yes);
            }
            laneY = y;
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

        float plateAlpha = 1f;

        void Plate(Rect r, float frac, Color c, float chip = 0f)
        {
            c.a *= plateAlpha;
            GUI.color = new Color(0, 0, 0, 0.8f * plateAlpha);
            GUI.DrawTexture(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), UISkin.White);
            var back = Factory.Shade(c, 0.3f);
            back.a = c.a;
            GUI.color = back;
            GUI.DrawTexture(r, UISkin.White);
            if (chip > frac)
            {
                GUI.color = new Color(1f, 0.95f, 0.85f, 0.9f * plateAlpha); // the chip: what the last hits took
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(chip), r.height), UISkin.White);
            }
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), UISkin.White);
            GUI.color = new Color(1, 1, 1, 0.25f * plateAlpha);
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
                // far ones fade out rather than pop (they vanish at 32 m)
                plateAlpha = Mathf.Clamp01((32f - Factory.FlatDistance(e.transform.position, p.transform.position)) / 8f);
                float bw = e.Def.Boss ? 120 : e.Elite ? 96 : 64;
                Plate(new Rect(g.x - bw / 2, g.y, bw, 7), e.Health / e.MaxHealth, e.Shielded ? new Color(0.4f, 0.8f, 1f) : new Color(0.85f, 0.12f, 0.1f), e.ChipHealth / e.MaxHealth);
                if (focus)
                    UISkin.Shadowed(new Rect(g.x - 160, g.y - 22, 320, 22), e.DisplayName + "  " + e.Level + (e.Slowed ? "  <color=#88ccff>slowed</color>" : ""),
                        UISkin.SmallCenter, e.Elite ? Enemy.ChampionColor : LevelColor(e.Level, p.Level));
                if (e.Elite)
                    UISkin.Shadowed(new Rect(g.x - 160, g.y + 8, 320, 20), string.Join("  \u2022  ", e.Affixes),
                        UISkin.V(UISkin.SmallCenter, fontSize: 12), new Color(0.75f, 0.82f, 1f));
            }

            // the town's guards in an invasion: who they are, and how they're holding up
            foreach (var tg in TownGuards.All.Values)
            {
                if (tg == null || Factory.FlatDistance(tg.transform.position, p.transform.position) > 30f) continue;
                if (!WorldToGui(tg.Head, out var gg)) continue;
                UISkin.Shadowed(new Rect(gg.x - 100, gg.y - 18, 200, 18), tg.Title, UISkin.V(UISkin.SmallCenter, fontSize: 12), new Color(0.55f, 0.75f, 1f));
                Plate(new Rect(gg.x - 24, gg.y + 1, 48, 4), tg.Health / tg.MaxHealth, new Color(0.35f, 0.6f, 1f));
            }

            foreach (var rp in RemotePlayer.ById.Values)
            {
                if (rp == null) continue;
                if (!WorldToGui(rp.transform.position + Vector3.up * 2.45f, out var g)) continue;
                bool mate = NetClient.I.IsPartyMember(rp.Id);
                string plate = (rp.GuildTag != "" ? "<" + rp.GuildTag + "> " : "") + rp.Name + "  " + rp.Level + (rp.Paragon > 0 ? " (" + rp.Paragon + ")" : "") + (rp.Dead ? "  (dead)" : "");
                UISkin.Shadowed(new Rect(g.x - 140, g.y - 22, 280, 22), plate, UISkin.SmallCenter,
                    mate ? new Color(0.45f, 1f, 0.5f) : new Color(0.5f, 0.78f, 1f));
                if (!string.IsNullOrEmpty(rp.Title)) NameTitle(new Vector2(g.x, g.y + 5), rp.Title, rp.Id);
                float pw = UISkin.SmallCenter.CalcSize(new GUIContent(plate)).x + 12f;
                var plateRect = new Rect(g.x - pw / 2, g.y - 22, pw, 22);
                Block(plateRect);
                if (ClickedIn(plateRect) >= 0) { menuPlayer = rp; menuPos = Event.current.mousePosition; }
                Plate(new Rect(g.x - 32, g.y, 64, 5), rp.Health / rp.MaxHealth, new Color(0.25f, 0.85f, 0.25f));
            }

            if (WorldToGui(p.transform.position + Vector3.up * 2.45f, out var pg))
            {
                UISkin.Shadowed(new Rect(pg.x - 140, pg.y - 20, 280, 22), p.DisplayName, UISkin.SmallCenter, new Color(0.65f, 0.9f, 1f));
                if (p.Achievements.Title != null) NameTitle(new Vector2(pg.x, pg.y + 1), p.Achievements.Title, 0);
            }

            foreach (var it in Interactable.All)
            {
                if (it == null) continue;
                float dist = Factory.FlatDistance(it.Position, p.transform.position);
                if (dist > 28f) continue;

                if (it is Npc npc)
                {
                    if (npc.Away) continue; // fled a burning quarter
                    if (!WorldToGui(it.Position + Vector3.up * it.LabelHeight, out var g)) continue;
                    UISkin.Shadowed(new Rect(g.x - 140, g.y - 20, 280, 22), npc.DisplayName, UISkin.SmallCenter, npc.LabelColor);
                    if (!string.IsNullOrEmpty(npc.Title))
                        UISkin.Shadowed(new Rect(g.x - 140, g.y - 2, 280, 20), "<" + npc.Title + ">", UISkin.SmallCenter, new Color(0.75f, 0.9f, 0.7f));
                    var mark = npc.Marker(p, out var mc);
                    if (mark != null) QuestMarker(g, mark, mc, npc.GetInstanceID());
                    continue;
                }

                if (it is AuctionPodium podium)
                {
                    if (podium.Away) continue;
                    // named like the townsfolk (it's a clickable podium, not an Npc, so it had no plate)
                    if (!WorldToGui(it.Position + Vector3.up * it.LabelHeight, out var g)) continue;
                    UISkin.Shadowed(new Rect(g.x - 140, g.y - 20, 280, 22), it.DisplayName, UISkin.SmallCenter, new Color(0.55f, 1f, 0.55f));
                    UISkin.Shadowed(new Rect(g.x - 140, g.y - 2, 280, 20), "<Auction House>", UISkin.SmallCenter, new Color(0.75f, 0.9f, 0.7f));
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
                    // sized to the text (names often have a second line), on a dark plate so it reads over anything
                    var hc = new GUIContent(it.HoverText);
                    var hs = UISkin.SmallCenter;
                    float hw = Mathf.Min(420f, hs.CalcSize(hc).x + 24f), hh = hs.CalcHeight(hc, hw) + 8f;
                    var hr = new Rect(g.x - hw / 2f, g.y - hh / 2f, hw, hh);
                    GUI.color = new Color(0.05f, 0.03f, 0.02f, 0.75f);
                    GUI.DrawTexture(hr, UISkin.White);
                    GUI.color = Color.white;
                    UISkin.Shadowed(hr, it.HoverText, hs, it.LabelColor);
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
                // up fast and slowing, drifting to its side; a pop at the start that settles with an overshoot
                float rise = 1.9f * age - 0.55f * age * age;
                if (!WorldToGui(f.Pos + Vector3.up * rise, out var g)) continue;
                g.x += f.Drift * 34f * Mathf.Sqrt(age);
                float pop = age < 0.18f ? 1f + 0.55f * Mathf.Sin(age / 0.18f * Mathf.PI) : 1f;
                if (f.Shake && age < 0.3f) { g.x += Mathf.Sin(age * 90f) * 3f * (1f - age / 0.3f); g.y += Mathf.Cos(age * 77f) * 2f * (1f - age / 0.3f); }
                // In steps of 3: every new size makes the font draw its letters again into its atlas (a hitch), and the
                // pop and fade used to ask for a new size nearly every frame
                UISkin.FloatText.fontSize = Mathf.RoundToInt(19 * f.Size * pop / 3f) * 3;
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
            partyTop = below + (p.CompanionInstance != null ? 58 : 0);

            var bossNow = DrawBossBar(p);
            Combatant target = p.HoveredEnemy != null ? p.HoveredEnemy : p.AttackTarget;
            if (target != null && !target.IsDead && target != bossNow)
            {
                var t = new Rect(354, bossNow != null ? 96 : 12, 320, 76);
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

        // ---- the boss bar: a boss in the fight gets a wide bar across the top, with its phases marked

        Enemy bossShown;
        float bossTrail = 1f, bossPhaseFlash = -10f, bossLastFrac = 1f;

        /// <summary>Which share of health a boss changes at: world bosses lose a plate at 75/50/25%, the Lich calls his dead at half.</summary>
        static float[] BossPhases(Enemy e)
        {
            if (WorldBoss.Is(e.Def.Name)) return new[] { 0.75f, 0.5f, 0.25f };
            if (e.Def.Name == "Lich King") return new[] { 0.5f };
            return new float[0];
        }

        /// <summary>The nearest living boss that's in the fight (hurt, or on us) within 30 m; draws its bar. Returns it (or null).</summary>
        Enemy DrawBossBar(Player p)
        {
            Enemy boss = null;
            float best = 30f;
            foreach (var e in Enemy.ById.Values)
            {
                if (e == null || e.IsDead || !e.Def.Boss) continue;
                bool fighting = e.Health < e.MaxHealth - 0.5f || p.AttackTarget == e || Time.time - e.LastDamagedTime < 15f;
                float d = Factory.FlatDistance(e.transform.position, p.transform.position);
                if (fighting && d < best) { best = d; boss = e; }
            }
            if (boss != bossShown) { bossShown = boss; bossTrail = boss != null ? boss.Health / boss.MaxHealth : 1f; bossLastFrac = bossTrail; }
            if (boss == null) return null;

            float w = Mathf.Min(640f, VW - 720f), x = (VW - w) / 2f, y = 14f;
            if (w < 360f) { w = Mathf.Min(VW - 40f, 560f); x = (VW - w) / 2f; y = 96f; } // narrow screens: below the hero frame
            float frac = Mathf.Clamp01(boss.Health / boss.MaxHealth);
            var phases = BossPhases(boss);
            foreach (var ph in phases) if (bossLastFrac > ph && frac <= ph) { bossPhaseFlash = Time.unscaledTime; Sfx.Play2D("gong", 0.35f, 0.9f); }
            bossLastFrac = frac;
            // the lost health lingers a moment in a pale chip, then drains
            bossTrail = frac >= bossTrail ? frac : Mathf.MoveTowards(bossTrail, frac, Time.unscaledDeltaTime * 0.25f);

            bool world = WorldBoss.Is(boss.Def.Name);
            var accent = world ? WorldBoss.Color : new Color(1f, 0.45f, 0.2f);
            UISkin.Shadowed(new Rect(x, y, w, 26), boss.DisplayName, UISkin.V(UISkin.HeadingCenter, fontSize: 20), accent, 2);
            UISkin.Shadowed(new Rect(x, y + 2, w, 22), "Level " + boss.Level, UISkin.V(UISkin.Small, alignment: TextAnchor.MiddleRight), UISkin.Cream);
            var bar = new Rect(x, y + 30, w, 18);
            GUI.color = new Color(0.05f, 0.02f, 0.02f, 0.9f);
            GUI.DrawTexture(new Rect(bar.x - 2, bar.y - 2, bar.width + 4, bar.height + 4), UISkin.White);
            GUI.color = new Color(1f, 0.9f, 0.75f, 0.85f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * bossTrail, bar.height), UISkin.White);
            float rage = world && frac < 0.25f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
            GUI.color = Color.Lerp(new Color(0.72f, 0.1f, 0.08f), new Color(1f, 0.25f, 0.1f), rage);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * frac, bar.height), UISkin.White);
            GUI.color = new Color(1f, 1f, 1f, 0.12f); // a little shine on top
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * frac, bar.height * 0.4f), UISkin.White);
            // phase marks: a notch and a diamond above it; passed ones go dark, the one just passed flashes
            float flash = Mathf.Clamp01(1f - (Time.unscaledTime - bossPhaseFlash) / 0.8f);
            foreach (var ph in phases)
            {
                float mx = bar.x + bar.width * ph;
                bool passed = frac <= ph;
                GUI.color = passed ? new Color(0.25f, 0.2f, 0.18f, 0.9f) : new Color(1f, 0.9f, 0.6f, 0.95f);
                GUI.DrawTexture(new Rect(mx - 1, bar.y - 4, 2, bar.height + 8), UISkin.White);
                GUI.DrawTexture(new Rect(mx - 3, bar.y - 8, 6, 6), UISkin.White);
            }
            if (flash > 0f) Outline(bar, accent, flash, 2f + 4f * (1f - flash));
            GUI.color = Color.white;
            UISkin.Shadowed(bar, Mathf.CeilToInt(boss.Health) + " / " + Mathf.CeilToInt(boss.MaxHealth) + "   (" + Mathf.CeilToInt(frac * 100f) + "%)",
                UISkin.V(UISkin.SmallCenter, fontSize: 13), Color.white, 1);
            // under the bar: the world boss's armour plates, or its rage
            if (world && WorldBoss.Current != null)
            {
                string under = frac < 0.25f ? "ENRAGED" : "Armour plates: " + new string('#', Mathf.Max(0, WorldBoss.Current.pl)).Replace("#", "[] ");
                UISkin.Shadowed(new Rect(x, bar.yMax + 4, w, 18), under, UISkin.V(UISkin.SmallCenter, fontSize: 13),
                    frac < 0.25f ? new Color(1f, 0.35f, 0.2f, 0.6f + 0.4f * rage) : UISkin.Cream);
            }
            return boss;
        }

        readonly float[] barLastCd = new float[8], barCasts = { -10f, -10f, -10f, -10f, -10f, -10f, -10f, -10f }, barReady = { -10f, -10f, -10f, -10f, -10f, -10f, -10f, -10f };

        /// <summary>A coloured frame around <paramref name="r"/>, grown outward by <paramref name="grow"/>, at <paramref name="alpha"/>.</summary>
        static void Outline(Rect r, Color c, float alpha, float grow)
        {
            var o = new Rect(r.x - grow, r.y - grow, r.width + grow * 2f, r.height + grow * 2f);
            GUI.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
            GUI.DrawTexture(new Rect(o.x, o.y, o.width, 2), UISkin.White);
            GUI.DrawTexture(new Rect(o.x, o.yMax - 2, o.width, 2), UISkin.White);
            GUI.DrawTexture(new Rect(o.x, o.y, 2, o.height), UISkin.White);
            GUI.DrawTexture(new Rect(o.xMax - 2, o.y, 2, o.height), UISkin.White);
            GUI.color = Color.white;
        }

        int barDrag = -1;
        bool barDragging;
        Vector2 barDragFrom;

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

            var ev = Event.current;
            int hoverSlot = -1;
            for (int i = 0; i < kit.Length; i++)
            {
                int k = i < p.BarOrder.Length && p.BarOrder[i] < kit.Length ? p.BarOrder[i] : i; // the ability in this slot
                var a = kit[k];
                var r = new Rect(x0 + i * (slot + gap), y0, slot, slot);
                if (r.Contains(ev.mousePosition)) hoverSlot = i;
                bool dragged = barDragging && barDrag == i;
                bool locked = p.Level < a.RequiredLevel;
                UISkin.Box(r, UISkin.Slot);
                UISkin.IconInSlot(r, UISkin.Icon(UISkin.AbilityIcon(a.Id)), dragged ? new Color(1f, 1f, 1f, 0.25f) : locked ? new Color(0.35f, 0.35f, 0.35f) : Color.white, 3);
                if (barDragging && barDrag != i && r.Contains(ev.mousePosition)) Outline(r, UISkin.Gold, 1f, 2f); // where it would go

                float cd = p.CooldownEnd[k] - Time.time;
                // a cast pops the button in its colour; a cooldown running out flashes it ready
                if (i < barCasts.Length)
                {
                    if (cd > 0.3f && barLastCd[i] <= 0f) barCasts[i] = Time.unscaledTime;
                    if (cd <= 0f && barLastCd[i] > 0f && a.Cooldown >= 2f) { barReady[i] = Time.unscaledTime; Sfx.Play2D("ui_click", 0.2f, 1.6f); }
                    barLastCd[i] = cd;
                    float pressed = 1f - (Time.unscaledTime - barCasts[i]) / 0.3f, ready = 1f - (Time.unscaledTime - barReady[i]) / 0.55f;
                    if (pressed > 0f) Outline(r, a.Color, pressed, 3f * pressed);
                    if (ready > 0f)
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.35f * ready);
                        GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), UISkin.White);
                        Outline(r, Color.white, ready, 6f * (1f - ready));
                        GUI.color = Color.white;
                    }
                }
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
                UISkin.Shadowed(new Rect(r.x + 5, r.y + 2, r.width, 18), (i + 1).ToString(), UISkin.Small, UISkin.Gold, 2);
                if (r.Contains(ev.mousePosition) && !barDragging)
                    tooltip = "<size=17><b><color=#" + Item.Hex(a.Color) + ">" + a.Name + "</color></b></size>   [" + (i + 1) + (k == 1 ? " / right-click" : "") + "]\n" +
                              "<color=#88aaff>" + a.ManaCost + " mana</color>   " + a.Cooldown + "s cooldown" +
                              (locked ? "\n<color=#ff6666>Requires level " + a.RequiredLevel + "</color>" : "") + "\n\n" + a.Description +
                              "\n\n<color=#998877>Drag to another slot to rearrange the bar.</color>";
                // press on a slot: a click casts it; a drag moves it to another slot
                if (ev.type == EventType.MouseDown && ev.button == 0 && r.Contains(ev.mousePosition)) { barDrag = i; barDragFrom = ev.mousePosition; barDragging = false; ev.Use(); }
            }
            if (barDrag >= 0)
            {
                if (ev.type == EventType.MouseDrag && (ev.mousePosition - barDragFrom).sqrMagnitude > 64f) { barDragging = true; ev.Use(); }
                if (ev.type == EventType.MouseUp && ev.button == 0)
                {
                    if (!barDragging) p.CastAbility(p.BarOrder[barDrag], p.MouseGround);
                    else if (hoverSlot >= 0 && hoverSlot != barDrag) { p.SwapBar(barDrag, hoverSlot); Sfx.Play2D("equip", 0.4f, 1.2f); }
                    barDrag = -1;
                    barDragging = false;
                    ev.Use();
                }
                if (barDragging && barDrag >= 0 && barDrag < kit.Length && ev.type == EventType.Repaint)
                {
                    var a = kit[p.BarOrder[barDrag]];
                    var at = new Rect(ev.mousePosition.x - slot * 0.45f, ev.mousePosition.y - slot * 0.45f, slot * 0.9f, slot * 0.9f);
                    UISkin.IconInSlot(at, UISkin.Icon(UISkin.AbilityIcon(a.Id)), new Color(1f, 1f, 1f, 0.85f), 0);
                }
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
                    tooltip = home ? (p.HasReturnPoint ? "<b>Return</b>  [R]\nStep back to where you recalled from." : "<b>Recall</b>  [R]\nYou are already in town.")
                                   : "<b>Recall to town</b>  [R]\nTo the nearest town whose waystone you know. Channel for " + Player.RecallTime + " seconds (moving or taking damage interrupts). " +
                                     "Press R in town afterwards to return to the same spot (not into a dungeon).";
                if (ClickedIn(r) == 0) p.Recall();
            }

            // Recall channel bar
            if (p.RecallProgress >= 0f)
                UISkin.Bar(new Rect(VW / 2 - 140, y0 - 96, 280, 18), p.RecallProgress, "Blue", "Recalling...", new Color(0.4f, 0.6f, 1f));

            // XP bar inside the action bar frame (a shimmer runs along it whenever experience comes in)
            int xpNow = p.Level >= ParagonBoard.MaxLevel ? p.Paragon.Xp + p.Paragon.Level * 100000 : p.Xp + p.Level * 100000;
            if (xpNow != lastXpSeen) { if (lastXpSeen >= 0) xpShimmerAt = Time.unscaledTime; lastXpSeen = xpNow; }
            var xpRect = new Rect(x0, y0 + slot + 10, barW - 24, 16);
            if (p.Level >= ParagonBoard.MaxLevel)
                UISkin.Bar(new Rect(x0, y0 + slot + 10, barW - 24, 16), (float)p.Paragon.Xp / p.Paragon.XpToNext, "Blue",
                    "Level " + p.Level + "  -  Paragon " + p.Paragon.Level + "   " + p.Paragon.Xp + " / " + p.Paragon.XpToNext + " XP", new Color(0.35f, 0.6f, 0.95f));
            else
                UISkin.Bar(new Rect(x0, y0 + slot + 10, barW - 24, 16), (float)p.Xp / p.XpToNext, "Purple",
                    "Level " + p.Level + "   " + p.Xp + " / " + p.XpToNext + " XP", new Color(0.6f, 0.35f, 0.9f));
            float shimmer = (Time.unscaledTime - xpShimmerAt) / 0.7f;
            if (shimmer < 1f)
            {
                float sx = xpRect.x + (xpRect.width + 60f) * shimmer - 60f;
                for (int i = 0; i < 6; i++)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.07f * (6 - Mathf.Abs(i - 3) * 2) * (1f - shimmer * 0.5f));
                    GUI.DrawTexture(new Rect(Mathf.Clamp(sx + i * 10f, xpRect.x, xpRect.xMax), xpRect.y, Mathf.Max(0f, Mathf.Min(10f, xpRect.xMax - (sx + i * 10f))), xpRect.height), UISkin.White);
                }
                GUI.color = Color.white;
            }

            // Gathering progress
            if (p.GatherNode != null)
            {
                var gr = new Rect((VW - 300) / 2, y0 - 58, 300, 22);
                UISkin.Bar(gr, p.GatherProgress, "Green", SkillSet.Verb(p.GatherNode.Skill) + " " + p.GatherNode.DisplayName + "...", SkillSet.SkillColor(p.GatherNode.Skill));
            }
        }

        // The orbs' liquid: it follows the value on a spring, so a big hit or a potion sloshes it up and down.
        readonly Dictionary<float, Vector2> orbLevel = new Dictionary<float, Vector2>(); // x -> (shown, velocity)

        float Slosh(float key, float frac)
        {
            if (!orbLevel.TryGetValue(key, out var s)) s = new Vector2(frac, 0f);
            if (Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
                s.y += (frac - s.x) * 140f * dt;
                s.y *= Mathf.Exp(-7f * dt);
                s.x += s.y * dt;
                orbLevel[key] = s;
            }
            return Mathf.Clamp01(s.x);
        }

        void DrawOrb(Rect r, float frac, Color color, string text)
        {
            frac = Slosh(r.x, Mathf.Clamp01(frac));
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

        static readonly Color TitleColor = new Color(0.85f, 0.75f, 1f);

        /// <summary>A hero's title under their name, with a slow sheen running over it.</summary>
        static void NameTitle(Vector2 at, string title, int seed)
        {
            float sheen = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * 0.9f + seed * 1.3f)), 12f);
            UISkin.Shadowed(new Rect(at.x - 140, at.y, 280, 18), title, UISkin.V(UISkin.SmallCenter, fontSize: 12, fontStyle: FontStyle.Italic),
                Color.Lerp(TitleColor, Color.white, sheen * 0.8f));
        }

        void DrawMenuButtons()
        {
            string[] icons = { "bags", "character", "talents", "skills", "quests", "achievements", "map", "help", "menu" };
            string[] tips = { "Bags  [I]", "Character  [C]", "Talents  [T]", "Skills  [K]", "Quest Log  [L]", "Achievements  [Y]", "World Map  [M]", "Help  [F1]", "Game Menu  [Esc]\nSettings, log out" };
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
                        case 5: showAchievements = !showAchievements; break;
                        case 6: showMap = !showMap; break;
                        case 7: showHelp = !showHelp; break;
                        default: menu = menu == MenuPage.None ? MenuPage.Main : MenuPage.None; break;
                    }
                }
                if (i == 0)
                {
                    bagButton = b.center;
                    float bump = 1f - (Time.unscaledTime - bagBumpAt) / 0.3f;
                    if (bump > 0f) b = new Rect(b.x - bump * 4f, b.y - bump * 6f, b.width + bump * 8f, b.height + bump * 8f);
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
            UISkin.Shadowed(new Rect(info.x + 14, info.y + 62, info.width - 28, 18), DayNight.Phase + "  " + DayNight.Clock + "  -  " + Weather.Season + ", " + SkyWord(pp),
                UISkin.V(UISkin.Small, alignment: TextAnchor.MiddleCenter), dark ? new Color(0.65f, 0.75f, 1f) : new Color(1f, 0.85f, 0.5f));

            // The map itself, then markers clipped to the circle.
            GUI.color = new Color(0.03f, 0.025f, 0.02f, 1f);
            GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), UISkin.Circle);
            GUI.color = Color.white;
            // Rotating mode: the camera's view direction is up on the map (the map turns as you turn the camera).
            float camYaw = MinimapYaw();
            if (Event.current.type == EventType.Repaint)
            {
                if (camYaw != 0f) RotatedTexture(r, Minimap.Render(pp), -camYaw, Color.white);
                else GUI.DrawTexture(r, Minimap.Render(pp));
            }

            float span = Minimap.Span;
            float ya = camYaw * Mathf.Deg2Rad, sa = Mathf.Sin(ya), ca = Mathf.Cos(ya);
            System.Func<Vector3, Vector2> toMap = w =>
            {
                float dx = w.x - pp.x, dz = w.z - pp.z;
                float mx = dx * ca - dz * sa, my = dx * sa + dz * ca; // along the camera's right and forward
                return new Vector2(r.center.x + mx / span * r.width, r.center.y - my / span * r.height);
            };

            foreach (var it in Interactable.All)
            {
                if (!Exploration.Seen(it.Position) && !(it is DungeonEntrance)) continue; // fog hides what you haven't found
                if (it is Npc npc)
                {
                    if (npc.Away) continue;
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
                if (rp != null && !net.IsPartyMember(rp.Id)) Dot(r, toMap(rp.transform.position), new Color(0.3f, 0.6f, 1f), 7);
            MapPing.Draw(toMap, r, false);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.alt && InCircle(r, Event.current.mousePosition, 0f))
            {
                // back from the (maybe turned) minimap to the world
                var d = Event.current.mousePosition - r.center;
                float mx = d.x / r.width * span, my = -d.y / r.height * span;
                MapPing.Send(new Vector3(pp.x + mx * ca + my * sa, 0f, pp.z - mx * sa + my * ca));
                Event.current.Use();
            }
            // Party members in the same place as us (near or far): an arrow in their color, facing where they face.
            foreach (var m in net.Party)
            {
                if (m.id == net.MyId || m.di != net.DungeonId) continue;
                bool near = RemotePlayer.ById.TryGetValue(m.id, out var mrp) && mrp != null;
                Vector3 at = near ? mrp.transform.position : new Vector3(m.x, 0f, m.z);
                PartyMarker(r, toMap(at), (near ? mrp.transform.eulerAngles.y : m.ry) - camYaw, m, Factory.FlatDistance(at, pp));
            }

            // A world boss: an orange marker where it is (on the rim when it's off the map)
            if (WorldBoss.Up && !Dungeon.Active)
            {
                var bp = toMap(WorldBoss.Position);
                var c = r.center;
                float rad = r.width / 2f - 8f;
                if ((bp - c).magnitude > rad) bp = c + (bp - c).normalized * rad;
                DotAt(bp, WorldBoss.Color, 11f);
            }

            // Legendary and set items lying about: a pulsing ping in their colour (on the rim when off the map)
            foreach (var d in LootDrop.Treasures)
            {
                if (d == null) continue;
                var lp = toMap(d.transform.position);
                var c = r.center;
                float rad = r.width / 2f - 8f;
                if ((lp - c).magnitude > rad) lp = c + (lp - c).normalized * rad;
                DotAt(lp, d.LabelColor, 6f + 3f * Mathf.Abs(Mathf.Sin(Time.time * 4f)));
            }

            // A town under attack: a pulsing marker at the gate (on the rim when it's off the map)
            if (Invasion.Active && !Dungeon.Active)
            {
                var gp = toMap(Invasion.Gate);
                var c = r.center;
                float rad = r.width / 2f - 8f;
                if ((gp - c).magnitude > rad) gp = c + (gp - c).normalized * rad;
                DotAt(gp, Invasion.Color, 9f + 4f * Mathf.Abs(Mathf.Sin(Time.time * 3f)));
            }

            // Hero: an arrow pointing where we face.
            // (Not GUIUtility.RotateAroundPivot: that pivots in unscaled screen space, so with the UI scale the
            // arrow would orbit around the wrong point. Rotate around the map centre as it appears on screen.)
            var saved = GUI.matrix;
            Vector3 pivot = saved.MultiplyPoint3x4(new Vector3(r.center.x, r.center.y, 0f));
            GUI.matrix = Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, p.transform.eulerAngles.y - camYaw), Vector3.one) *
                         Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one) * saved;
            GUI.color = new Color(1f, 0.95f, 0.8f);
            GUI.DrawTexture(new Rect(r.center.x - 8, r.center.y - 9, 16, 18), Minimap.Arrow);
            GUI.matrix = saved;
            GUI.color = Color.white;

            // Bronze ring, north marker and zoom buttons.
            if (UISkin.OrbFrame != null) GUI.DrawTexture(frame, UISkin.OrbFrame);
            // North: at the top, or wherever north is on a rotating map.
            float nr = frame.width / 2f - 12f;
            var npos = new Vector2(frame.center.x - sa * nr, frame.center.y - ca * nr);
            UISkin.Shadowed(new Rect(npos.x - 12, npos.y - 11, 24, 22), "N", UISkin.V(UISkin.HeadingCenter, fontSize: 15), UISkin.Gold, 2);
            // The rotate / north-up toggle, on the frame.
            var rot = new Rect(frame.x + 10, frame.y + 10, 26, 26);
            if (UISkin.Btn(rot, GameSettings.MinimapRotate ? "R" : "N", UISkin.SquareButton)) GameSettings.MinimapRotate = !GameSettings.MinimapRotate;
            if (rot.Contains(Event.current.mousePosition))
                tooltip = GameSettings.MinimapRotate ? "The minimap turns with the camera.\n<color=#998877>Click to keep north up.</color>"
                                                     : "North is up on the minimap.\n<color=#998877>Click to turn it with the camera.</color>";
            var zin = new Rect(frame.xMax - 40, frame.yMax - 44, 26, 26);
            var zout = new Rect(frame.x + 14, frame.yMax - 44, 26, 26);
            if (UISkin.Btn(zin, "+", UISkin.SquareButton)) Minimap.Span = Mathf.Max(Minimap.MinSpan, Minimap.Span / 1.3f);
            if (UISkin.Btn(zout, "-", UISkin.SquareButton)) Minimap.Span = Mathf.Min(Minimap.MaxSpan, Minimap.Span * 1.3f);
            if (zin.Contains(Event.current.mousePosition)) tooltip = "Zoom in";
            if (zout.Contains(Event.current.mousePosition)) tooltip = "Zoom out";
        }

        /// <summary>
        /// A party member on the minimap: an arrow in their color pointing where they face. Out of range, it sits on the
        /// rim pointing toward them, slightly smaller. Hover for their name and distance.
        /// </summary>
        void PartyMarker(Rect circle, Vector2 pos, float facing, NetPartyMember m, float distance)
        {
            float rim = circle.width / 2 - 10;
            var d = pos - circle.center;
            bool pinned = d.magnitude > rim;
            if (pinned)
            {
                pos = circle.center + d.normalized * rim;
                facing = Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg;
            }
            var c = m.dead ? new Color(0.55f, 0.5f, 0.5f) : MemberColor(m.id);
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(pos.x - 8, pos.y - 8, 16, 16), UISkin.Circle);
                RotatedTexture(new Rect(pos.x - 7, pos.y - 8, 14, 16), Minimap.Arrow, facing, c, pinned ? 0.8f : 1f);
            }
            if (new Rect(pos.x - 10, pos.y - 10, 20, 20).Contains(Event.current.mousePosition))
                tooltip = "<b><color=#" + Item.Hex(MemberColor(m.id)) + ">" + m.name + "</color></b>  " + Mathf.RoundToInt(distance) + " m" + (m.dead ? "  (dead)" : "");
        }

        /// <summary>Draws a texture turned <paramref name="degrees"/> clockwise around its center (correct under the UI scale).</summary>
        static void RotatedTexture(Rect rect, Texture tex, float degrees, Color c, float scale = 1f)
        {
            var saved = GUI.matrix;
            Vector3 pivot = saved.MultiplyPoint3x4(new Vector3(rect.center.x, rect.center.y, 0f));
            GUI.matrix = Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, degrees), new Vector3(scale, scale, 1f)) *
                         Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one) * saved;
            GUI.color = c;
            GUI.DrawTexture(rect, tex);
            GUI.matrix = saved;
            GUI.color = Color.white;
        }

        CameraRig rig;

        /// <summary>How far the minimap is turned: the camera's yaw when it rotates, 0 for north up.</summary>
        float MinimapYaw()
        {
            if (!GameSettings.MinimapRotate) return 0f;
            if (rig == null && GameManager.I != null && GameManager.I.Cam != null) rig = GameManager.I.Cam.GetComponent<CameraRig>();
            return rig != null ? rig.Yaw : 0f;
        }

        /// <summary>"clear", "snow", "blizzard", "fog"... for the minimap plate.</summary>
        static string SkyWord(Vector3 at)
        {
            bool cold = Weather.ColdAt(at);
            switch (Weather.Sky)
            {
                case "rain": return cold ? "snow" : "rain";
                case "storm": return cold ? "blizzard" : "storm";
                default: return Weather.Sky;
            }
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

        /// <summary>A town under attack: which, how it goes, and the gate's integrity. Returns the height it took.</summary>
        float DrawInvasionTracker()
        {
            var iv = Invasion.Current;
            if (iv == null || Dungeon.Active) return 0f;
            float x = VW - 330, y = 342;
            float pulse = Invasion.Active ? 0.75f + 0.25f * Mathf.Sin(Time.time * 4f) : 1f;
            UISkin.Shadowed(new Rect(x, y, 300, 26), "Invasion: " + iv.town, UISkin.Heading, Invasion.Color * pulse + new Color(0, 0, 0, 1f - pulse));
            y += 28;
            UISkin.Shadowed(new Rect(x + 12, y, 300, 20), Invasion.Status, UISkin.Small, UISkin.Cream);
            y += 24;
            if (!Invasion.Active) return y - 342 + 10;
            UISkin.Bar(new Rect(x + 12, y + 2, 230, 13), iv.hp / 100f, iv.hp > 50 ? "Yellow" : "Red", "Gate " + iv.hp + "%", new Color(0.85f, 0.6f, 0.2f));
            y += 22;
            return y - 342 + 12;
        }

        /// <summary>The world boss that is up: who, its health, how far. Returns the height it took.</summary>
        float DrawWorldBossTracker(float y0)
        {
            var wb = WorldBoss.Current;
            if (wb == null || Dungeon.Active) return 0f;
            float x = VW - 330, y = y0;
            UISkin.Shadowed(new Rect(x, y, 300, 26), "World boss: " + wb.name, UISkin.Heading, WorldBoss.Color);
            y += 28;
            UISkin.Bar(new Rect(x + 12, y + 2, 230, 13), wb.hp / 100f, "Red", wb.hp + "%", new Color(0.8f, 0.2f, 0.1f));
            y += 20;
            UISkin.Shadowed(new Rect(x + 12, y, 300, 20), WorldBoss.Status, UISkin.Small, UISkin.Cream);
            y += 30;
            return y - y0;
        }

        /// <summary>Today's bounties, while any is left to do. Returns the height it took.</summary>
        float DrawBounties(float y0)
        {
            var list = Bounties.List;
            bool open = false;
            foreach (var b in list) if (!b.Done) open = true;
            if (!open || Dungeon.Active) return 0f;
            float x = VW - 330, y = y0;
            UISkin.Shadowed(new Rect(x, y, 300, 26), "Bounties", UISkin.Heading, Bounties.Color);
            y += 28;
            foreach (var b in list)
            {
                UISkin.Shadowed(new Rect(x + 12, y, 290, 20), b.Done ? "<s>" + b.Text + "</s>  done" : b.Text + ":  " + b.Have + " / " + b.Need,
                    UISkin.Small, b.Done ? new Color(0.55f, 1f, 0.55f) : UISkin.Cream);
                y += 22;
            }
            return y - y0 + 10;
        }

        struct QuestTick { public int Progress; public bool Ready; public float At, ReadyAt; }
        readonly Dictionary<string, QuestTick> questTicks = new Dictionary<string, QuestTick>();
        int lastXpSeen = -1;
        float xpShimmerAt = -10f;

        void DrawQuestTracker(Player p)
        {
            float top = DrawInvasionTracker();
            top += DrawWorldBossTracker(342 + top);
            top += DrawRiftTracker(342 + top);
            top += DrawBounties(342 + top);
            if (p.Quests.Active.Count == 0) return;
            float x = VW - 330, y = 342 + top;
            UISkin.Shadowed(new Rect(x, y, 300, 26), "Quests", UISkin.Heading, UISkin.Gold);
            y += 28;
            foreach (var q in p.Quests.Active)
            {
                bool ready = q.IsReady(p);
                int prog = q.Progress(p);
                // a tick pops the count; finishing it strikes the objective out before "Return to" takes its place
                if (!questTicks.TryGetValue(q.Def.Id, out var tk)) tk = new QuestTick { Progress = prog, Ready = ready, At = -10f, ReadyAt = -10f };
                if (prog != tk.Progress) { tk.Progress = prog; tk.At = Time.unscaledTime; }
                if (ready && !tk.Ready) { tk.ReadyAt = Time.unscaledTime; Sfx.Play2D("ui_confirm", 0.4f, 1.2f); }
                tk.Ready = ready;
                questTicks[q.Def.Id] = tk;
                UISkin.Shadowed(new Rect(x, y, 300, 22), q.Def.Title, UISkin.Label, new Color(1f, 0.85f, 0.3f));
                y += 22;
                string obj = q.Def.Type == QuestType.Kill ? q.Def.Target + " slain" : q.Def.Target;
                float sinceReady = Time.unscaledTime - tk.ReadyAt, sinceTick = Time.unscaledTime - tk.At;
                if (ready && sinceReady < 1.2f)
                {
                    float k = sinceReady / 1.2f;
                    var c = Color.Lerp(new Color(0.55f, 1f, 0.55f), new Color(0.55f, 1f, 0.55f, 0f), Mathf.Clamp01((k - 0.6f) / 0.4f));
                    UISkin.Shadowed(new Rect(x + 12, y, 290, 20), "<s>" + obj + ":  " + prog + " / " + q.Def.Count + "</s>", UISkin.Small, c);
                }
                else
                {
                    float pop = !ready && sinceTick < 0.35f ? 1f - sinceTick / 0.35f : 0f;
                    UISkin.Shadowed(new Rect(x + 12 + pop * 4f, y, 290, 20), ready ? "Return to " + GiverOf(q.Def) : obj + ":  " + prog + " / " + q.Def.Count,
                        UISkin.Small, ready ? new Color(0.55f, 1f, 0.55f) : Color.Lerp(UISkin.Cream, UISkin.Gold, pop));
                }
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

        // ---- text fields: Tab moves between them, Ctrl/Cmd+V pastes (the browser's clipboard: Clipboard), Ctrl/Cmd+C copies
        readonly List<string> tabOrder = new List<string>(), tabOrderShown = new List<string>();
        string pasted;          // waiting for the focused field (or the chat) to take it
        float pastedAt;
        bool copyAsked;

        /// <summary>Tab between the fields shown, pasted text, and copy requests (start of OnGUI).</summary>
        void TextInputEvents()
        {
            var e = Event.current;
            copyAsked = false; // a copy is for this key press only
            if (e.type == EventType.Repaint)
            {
                // the fields drawn last time, in order
                tabOrderShown.Clear();
                tabOrderShown.AddRange(tabOrder);
                tabOrder.Clear();
                if (pasted != null && Time.unscaledTime - pastedAt > 1f) pasted = null; // nobody was typing
            }
            if (pasted == null && (pasted = Clipboard.TakePaste()) != null) pastedAt = Time.unscaledTime;
            if (ChatOpen && pasted != null)
            {
                chatText = OneLine(chatText + pasted, 200);
                pasted = null;
            }
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Tab || e.character == '\t')
            {
                int i = tabOrderShown.IndexOf(GUI.GetNameOfFocusedControl());
                if (i < 0) return;
                if (e.keyCode == KeyCode.Tab && tabOrderShown.Count > 1)
                    GUI.FocusControl(tabOrderShown[(i + (e.shift ? -1 : 1) + tabOrderShown.Count) % tabOrderShown.Count]);
                e.Use();
            }
            else if ((e.control || e.command) && (e.keyCode == KeyCode.C || e.keyCode == KeyCode.X)) copyAsked = true;
        }

        static string OneLine(string s, int max)
        {
            s = s.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
            return s.Length > max ? s.Substring(0, max) : s;
        }

        /// <summary>
        /// A text field that takes part in Tab order, takes pasted text at its cursor and copies its selection (or all of it)
        /// to the browser's clipboard. Every text field in the game goes through here.
        /// </summary>
        public string TextInput(Rect r, string name, string value, int max, bool password = false, GUIStyle style = null)
        {
            var e = Event.current;
            value = value ?? "";
            if (e.type == EventType.Repaint) tabOrder.Add(name);
            if (GUI.GetNameOfFocusedControl() == name)
            {
                var editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                if (pasted != null)
                {
                    string add = OneLine(pasted, max);
                    int at = editor != null ? Mathf.Clamp(Mathf.Min(editor.cursorIndex, editor.selectIndex), 0, value.Length) : value.Length;
                    int end = editor != null ? Mathf.Clamp(Mathf.Max(editor.cursorIndex, editor.selectIndex), at, value.Length) : value.Length;
                    value = value.Substring(0, at) + add + value.Substring(end);
                    if (value.Length > max) value = value.Substring(0, max);
                    if (editor != null) { editor.text = value; editor.cursorIndex = editor.selectIndex = Mathf.Min(value.Length, at + add.Length); }
                    pasted = null;
                }
                if (copyAsked && e.type == EventType.KeyDown && !password)
                {
                    string sel = editor != null ? editor.SelectedText : "";
                    Clipboard.Copy(string.IsNullOrEmpty(sel) ? value : sel);
                    copyAsked = false;
                }
            }
            GUI.SetNextControlName(name);
            return password ? GUI.PasswordField(r, value, '\u2022', max, style ?? UISkin.Field) : GUI.TextField(r, value, max, style ?? UISkin.Field);
        }

        /// <summary>The chat commands as buttons, above the chat when it's open or pointed at.</summary>
        void DrawChatBar(Rect bar)
        {
            Block(bar);
            var net = NetClient.I;
            var buttons = new System.Collections.Generic.List<(string label, string tip, System.Action act)>
            {
                ("Say", "Talk to everyone nearby", () => ChatChannel("")),
            };
            if (net != null && net.InParty) buttons.Add(("Party", "Party chat (/p)", () => ChatChannel("/p ")));
            if (Guild.Current != null) buttons.Add(("Guild", "Guild chat (/g)", () => ChatChannel("/g ")));
            buttons.Add(("Whisper", "Whisper to someone by name (/w name)", () => ChatChannel("/w ")));
            if (!string.IsNullOrEmpty(replyTo)) buttons.Add(("Reply", "Whisper back to " + replyTo + " (/r)", () => ChatChannel("/w " + replyTo + " ")));
            buttons.Add(("Who", "Who's online (/who)", () => net?.SendChat("/who")));
            buttons.Add(("Invite", "Invite someone to your party by name (/invite name)", () => ChatChannel("/invite ")));
            buttons.Add(("Emotes", "Wave, bow, dance... (/e)", () => showEmotes = !showEmotes));
            float bw = (bar.width - (buttons.Count - 1) * 4) / buttons.Count;
            var style = UISkin.V(UISkin.Button, fontSize: 12);
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = new Rect(bar.x + i * (bw + 4), bar.y, bw, bar.height);
                if (UISkin.Btn(b, buttons[i].label, style)) buttons[i].act();
                if (b.Contains(Event.current.mousePosition)) tooltip = buttons[i].tip;
            }
        }

        /// <summary>Opens the chat on a channel, keeping what was already typed.</summary>
        void ChatChannel(string prefix)
        {
            string text = ChatOpen ? chatText : "";
            foreach (var p in new[] { "/p ", "/g ", "/invite " })
                if (text.StartsWith(p)) { text = text.Substring(p.Length); break; }
            if (text.StartsWith("/w "))
            {
                int sp = text.IndexOf(' ', 3);
                text = sp > 0 ? text.Substring(sp + 1) : "";
            }
            OpenChat(prefix + text);
        }

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
                var emote = EmoteDef.FromChat(text);
                if (emote != null) Player.I?.DoEmote(emote);
                else if (text == "/e" || text == "/emote" || text == "/emotes") ListEmotes();
                else
                {
                    // the link goes along only while its [name] is still in the line
                    bool linked = chatLinkName != null && text.Contains("[" + chatLinkName + "]");
                    NetClient.I.SendChat(text, linked ? chatLinkBag : -1, linked ? chatLinkWorn : -1);
                }
                chatLinkBag = chatLinkWorn = -1;
                chatLinkName = null;
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
            else if (e.control || e.command)
            {
                // Ctrl/Cmd+C copies what's typed, +X cuts it (+V pastes: TextInputEvents); no letters from shortcuts
                if (e.keyCode == KeyCode.C || e.keyCode == KeyCode.X) { Clipboard.Copy(chatText); if (e.keyCode == KeyCode.X) chatText = ""; }
                copyAsked = false;
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

        /// <summary>A name clicked in the chat: their player menu if they're near, else a whisper to them.</summary>
        void ChatNameClicked(string who)
        {
            foreach (var rp in RemotePlayer.ById.Values)
                if (rp != null && rp.Name == who) { menuPlayer = rp; menuPos = Event.current.mousePosition; return; }
            ChatOpen = true;
            chatText = "/w " + who + " ";
            Sfx.Play2D("ui_click", 0.3f);
        }

        // ---- an item linked into the chat line being typed (Shift+click it in the bags or on the character)
        int chatLinkBag = -1, chatLinkWorn = -1;
        string chatLinkName;

        /// <summary>Puts [Item Name] into the chat line and remembers which slot it is, for the server to attach.</summary>
        public void LinkItemInChat(Item item, int bagSlot, int wornSlot)
        {
            if (item == null) return;
            if (!ChatOpen) ChatOpen = true;
            chatLinkBag = bagSlot;
            chatLinkWorn = wornSlot;
            chatLinkName = item.Name;
            chatText = (chatText.Length > 0 && !chatText.EndsWith(" ") ? chatText + " " : chatText) + "[" + item.Name + "] ";
            Sfx.Play2D("ui_click", 0.3f, 1.2f);
        }

        void DrawLog(bool inWorld)
        {
            float w = 400, lines = ChatOpen ? 14 : 8, lh = 20;
            float x = 16, bottom = VH - 44 - (inWorld ? 30 : 0);
            if (inWorld && x + w + 8 > OrbsLeft) bottom = VH - 200; // narrow screen: sit above the orbs
            float y = bottom - lines * lh;
            var area = new Rect(x - 8, y - 8, w + 16, lines * lh + 16);
            var bar = new Rect(x - 8, area.y - 30, w + 16, 28); // the chat bar: every chat command as a button
            bool live = ChatOpen || (inWorld && (area.Contains(Event.current.mousePosition) || bar.Contains(Event.current.mousePosition))); // names and links answer the mouse
            if (live && inWorld) DrawChatBar(bar);
            if (live)
            {
                GUI.color = new Color(1, 1, 1, 0.9f);
                UISkin.Box(area, UISkin.Inset);
                GUI.color = Color.white;
                if (inWorld) Block(area);
            }
            var stampStyle = UISkin.V(UISkin.Small, fontSize: 11);
            int start = Mathf.Max(0, log.Count - (int)lines);
            for (int i = start; i < log.Count; i++)
            {
                var l = log[i];
                float age = Time.time - l.Time;
                float alpha = ChatOpen ? 1f : Mathf.Clamp01(1f - (age - 20f) / 5f);
                if (alpha <= 0f) continue;
                var c = l.Color;
                c.a = alpha;
                float ly = y + (i - start) * lh;
                // the time, dim, in front
                float sx = x;
                if (!string.IsNullOrEmpty(l.Stamp))
                {
                    UISkin.Shadowed(new Rect(x, ly + 1, 44, lh), l.Stamp, stampStyle, new Color(0.6f, 0.56f, 0.5f, alpha * 0.8f));
                    sx = x + 40;
                }
                var row = new Rect(sx, ly, w - (sx - x), lh);
                // a linked item: its name in the line gets a band of its colour, and shows the item when pointed at
                if (l.Item != null)
                {
                    string token = "[" + l.Item.Name + "]";
                    int at = l.Text.IndexOf(token, System.StringComparison.Ordinal);
                    float tx = at >= 0 ? UISkin.Small.CalcSize(new GUIContent(l.Text.Substring(0, at))).x : 0f;
                    float tw = UISkin.Small.CalcSize(new GUIContent(at >= 0 ? token : l.Text)).x;
                    var band = new Rect(row.x + tx - 2, ly + 2, tw + 4, lh - 3);
                    var ic = l.Item.NameColor;
                    GUI.color = new Color(ic.r, ic.g, ic.b, 0.28f * alpha);
                    GUI.DrawTexture(band, UISkin.White);
                    GUI.color = Color.white;
                    if (live && band.Contains(Event.current.mousePosition) && Player.I != null) ItemTooltip(l.Item, Player.I, l.Who != null ? "Linked by " + l.Who : "Linked");
                }
                UISkin.Shadowed(row, l.Text, UISkin.Small, c);
                // right-click a line: it goes to the clipboard
                if (live && inWorld && Event.current.type == EventType.MouseDown && Event.current.button == 1 && row.Contains(Event.current.mousePosition))
                {
                    Clipboard.Copy((l.Who != null && !l.Text.Contains(l.Who) ? l.Who + ": " : "") + l.Text);
                    Event.current.Use();
                    Log("Copied to the clipboard.", new Color(0.6f, 0.6f, 0.6f));
                }
                // the speaker's name: click for the player menu (whisper, invite, trade), or a whisper if they're far
                if (live && inWorld && !string.IsNullOrEmpty(l.Who))
                {
                    int at = l.Text.IndexOf(l.Who, System.StringComparison.Ordinal);
                    if (at >= 0)
                    {
                        float nx = UISkin.Small.CalcSize(new GUIContent(l.Text.Substring(0, at))).x;
                        float nw = UISkin.Small.CalcSize(new GUIContent(l.Who)).x;
                        var nr = new Rect(row.x + nx, ly, nw, lh);
                        if (nr.Contains(Event.current.mousePosition))
                        {
                            GUI.color = new Color(c.r, c.g, c.b, 0.9f);
                            GUI.DrawTexture(new Rect(nr.x, nr.yMax - 3, nr.width, 1), UISkin.White);
                            GUI.color = Color.white;
                            tooltip = "<b>" + l.Who + "</b>\nClick: whisper, invite, trade";
                        }
                        if (ClickedIn(nr) >= 0) ChatNameClicked(l.Who);
                    }
                }
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
                    GUI.Label(r, "Say something...   /p party   /w name   /r reply   Ctrl+V pastes", style);
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
            var ev = Event.current;
            int hoverSlot = -1;
            for (int i = 0; i < p.Inventory.Slots.Length; i++)
            {
                var cr = new Rect(r.x + 20 + (i % cols) * (cell + gap), r.y + 58 + (i / cols) * (cell + gap), cell, cell);
                var item = p.Inventory.Slots[i];
                if (cr.Contains(ev.mousePosition)) hoverSlot = i;
                DrawItemSlot(cr, item, p);
                if (bagDragging && bagDrag != i && hoverSlot == i) Outline(cr, UISkin.Gold, 1f, 2f); // where it would go
                if (item == null) continue;
                if (bagDragging && bagDrag == i)
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.55f);
                    GUI.DrawTexture(new Rect(cr.x + 2, cr.y + 2, cr.width - 4, cr.height - 4), UISkin.White);
                    GUI.color = Color.white;
                    continue;
                }
                BetterOrWorse(cr, item, p);
                if (p.NewItems.Count > 0 && p.NewItems.Contains(item.Signature))
                {
                    if (hoverSlot == i) p.NewItems.Remove(item.Signature); // seen
                    else NewSparkle(cr, i);
                }
                if (i == socketGem)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.35f + Mathf.PingPong(Time.time, 0.4f));
                    GUI.DrawTexture(new Rect(cr.x + 2, cr.y + 2, cr.width - 4, 3), UISkin.White);
                    GUI.DrawTexture(new Rect(cr.x + 2, cr.yMax - 5, cr.width - 4, 3), UISkin.White);
                    GUI.color = Color.white;
                }
                if (item.Kind == ItemKind.Equipment && item.Sockets > 0) DrawSocketPips(cr, item);
                bool offered = NetClient.I != null && NetClient.I.IsOffered(i);
                if (offered)
                {
                    // In the trade window: stays in the bags until the trade completes.
                    GUI.color = new Color(0.1f, 0.25f, 0.1f, 0.6f);
                    GUI.DrawTexture(new Rect(cr.x + 2, cr.y + 2, cr.width - 4, cr.height - 4), UISkin.White);
                    GUI.color = Color.white;
                    UISkin.Shadowed(new Rect(cr.x, cr.y + 2, cr.width, 16), "TRADE", UISkin.SmallCenter, new Color(0.6f, 1f, 0.6f));
                }
                if (cr.Contains(Event.current.mousePosition) && !bagDragging)
                    ItemTooltip(item, p, offered ? "In the trade window" : BagHint(item, vendor));
                // press: a click uses the item, a drag moves it to another slot
                if (ev.type == EventType.MouseDown && ev.button == 0 && cr.Contains(ev.mousePosition)) { bagDrag = i; bagDragFrom = ev.mousePosition; bagDragging = false; ev.Use(); }
                int click = ClickedIn(cr);
                if (click == 1)
                {
                    if (socketGem >= 0) socketGem = -1;
                    else if (tradeOpen) { if (!offered) NetClient.I.OfferItem(i); }
                    else if (showStash) StashItem(p, i);
                    else if (vendor) Sell(p, i);
                    else if (Event.current.shift) p.DropItem(i);
                }
            }
            if (bagDrag >= 0)
            {
                var held = bagDrag < p.Inventory.Slots.Length ? p.Inventory.Slots[bagDrag] : null;
                if (held == null) { bagDrag = -1; bagDragging = false; }
                else
                {
                    if (ev.type == EventType.MouseDrag && (ev.mousePosition - bagDragFrom).sqrMagnitude > 64f)
                    {
                        if (!bagDragging) Sfx.Play2D("ui_click", 0.3f, 0.8f);
                        bagDragging = true;
                        ev.Use();
                    }
                    if (ev.type == EventType.MouseUp && ev.button == 0)
                    {
                        int from = bagDrag;
                        bagDrag = -1;
                        if (!bagDragging) LeftClickBag(p, from, held);
                        else if (hoverSlot >= 0 && hoverSlot != from && !tradeOpen)
                        {
                            NetClient.I?.Op("move", i: from, j: hoverSlot);
                            Sfx.Play2D(held.Kind == ItemKind.Equipment ? "equip" : "drop", 0.4f, 1.15f);
                        }
                        bagDragging = false;
                        ev.Use();
                    }
                    if (bagDragging && ev.type == EventType.Repaint)
                    {
                        var at = new Rect(ev.mousePosition.x - cell * 0.5f, ev.mousePosition.y - cell * 0.5f, cell, cell);
                        GUI.color = new Color(1f, 1f, 1f, 0.9f);
                        DrawItemSlot(at, held, p);
                        GUI.color = Color.white;
                    }
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
                NetClient.I?.Op("sort");
                Sfx.Play2D("ui_click", 0.4f);
            }
        }

        int bagDrag = -1;
        bool bagDragging;
        Vector2 bagDragFrom;

        void LeftClickBag(Player p, int i, Item item)
        {
            if (ChatOpen && Event.current.shift) { LinkItemInChat(item, i, -1); return; } // Shift+click with the chat open: link it
            if (socketGem >= 0 && item.Kind == ItemKind.Equipment) { p.SocketGem(socketGem, item); socketGem = -1; }
            else if (item.Kind == ItemKind.Gem)
            {
                socketGem = socketGem == i ? -1 : i;
                if (socketGem >= 0) Log("Click an item (in your bags or worn) with an empty socket. Right-click to cancel.", item.IconColor);
            }
            else p.UseItem(i);
        }

        /// <summary>A little arrow in the corner: better (green, up) or worse (red, down) than what you wear there.</summary>
        static void BetterOrWorse(Rect r, Item item, Player p)
        {
            if (item.Kind != ItemKind.Equipment || item.Slot == EquipSlot.None || item.RequiredLevel > p.Level || Event.current.type != EventType.Repaint) return;
            var worn = p.Inventory.GetEquipped(item.Slot);
            float d = worn == null ? 1f : item.Rating - worn.Rating;
            if (Mathf.Abs(d) < 0.5f) return;
            bool up = d > 0f;
            GUI.color = up ? new Color(0.35f, 1f, 0.35f) : new Color(1f, 0.3f, 0.25f);
            float x = r.x + 6, y = r.yMax - 15;
            for (int k = 0; k < 5; k++) // a small triangle out of lines
            {
                float half = up ? k : 4 - k;
                GUI.DrawTexture(new Rect(x + 5 - half, y + k * 2, half * 2 + 1, 2), UISkin.White);
            }
            GUI.color = Color.white;
        }

        /// <summary>A new item: a gold glint twinkling over its slot.</summary>
        static void NewSparkle(Rect r, int seed)
        {
            if (Event.current.type != EventType.Repaint) return;
            float t = Time.unscaledTime * 2.2f + seed * 0.7f;
            float k = 0.5f + 0.5f * Mathf.Sin(t);
            var gold = new Color(1f, 0.9f, 0.5f);
            GUI.color = new Color(gold.r, gold.g, gold.b, 0.12f + 0.12f * k);
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), UISkin.White);
            // a four-point star in the top-right corner, breathing
            float cx = r.xMax - 11, cy = r.y + 11, len = 4f + 5f * k;
            GUI.color = new Color(1f, 0.97f, 0.8f, 0.6f + 0.4f * k);
            GUI.DrawTexture(new Rect(cx - len, cy - 0.75f, len * 2, 1.5f), UISkin.White);
            GUI.DrawTexture(new Rect(cx - 0.75f, cy - len, 1.5f, len * 2), UISkin.White);
            GUI.DrawTexture(new Rect(cx - 1.5f, cy - 1.5f, 3f, 3f), UISkin.White);
            GUI.color = Color.white;
        }

        void Sell(Player p, int index)
        {
            if (p.Inventory.Slots[index] != null) NetClient.I?.Op("sell", i: index);
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
            bool paragon = p.Level >= ParagonBoard.MaxLevel;
            float ch = Mathf.Min(paragon ? 790 : 600, VH - 16);
            var r = new Rect(14, Mathf.Clamp(paragon ? 100 : 140, 8, VH - ch - 8), 470, ch);
            if (UISkin.Window(r, p.DisplayName + "  -  Level " + p.Level + (paragon ? " (Paragon " + p.Paragon.Level + ")" : "") + " " + p.Look)) showChar = false;
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
            if (paragon) DrawParagon(p, r, y + 104);
        }

        /// <summary>Paragon points (past the level cap): four rows with a + each, and a free reset.</summary>
        void DrawParagon(Player p, Rect r, float y)
        {
            var pb = p.Paragon;
            UISkin.Box(new Rect(r.x + 18, y - 6, r.width - 36, 176), UISkin.Inset);
            UISkin.Shadowed(new Rect(r.x + 32, y, 300, 24), "Paragon " + pb.Level, UISkin.Heading, ParagonBoard.Color);
            UISkin.Shadowed(new Rect(r.x + 180, y + 3, 260, 22), pb.Free > 0 ? "<b>" + pb.Free + "</b> point" + (pb.Free == 1 ? "" : "s") + " to spend" : "",
                UISkin.V(UISkin.Small, alignment: TextAnchor.UpperRight), UISkin.Gold);
            y += 30;
            for (int i = 0; i < pb.Points.Length; i++)
            {
                var row = new Rect(r.x + 24, y, r.width - 48, 30);
                // the bar fills with the colour as points go in
                float fill = pb.Points[i] / (float)ParagonBoard.Cap;
                if (Event.current.type == EventType.Repaint && fill > 0f)
                {
                    var pc = ParagonBoard.Color;
                    GUI.color = new Color(pc.r, pc.g, pc.b, 0.12f + 0.1f * fill);
                    GUI.DrawTexture(new Rect(row.x, row.y + 2, row.width * fill, row.height - 4), UISkin.White);
                    GUI.color = Color.white;
                }
                NodeFlash(row, new Rect(r.xMax - 66, y, 34, 30), "p:" + i, ParagonBoard.Color);
                UISkin.Shadowed(new Rect(r.x + 32, y + 2, 120, 26), ParagonBoard.Names[i], UISkin.Label, UISkin.Cream);
                UISkin.Shadowed(new Rect(r.x + 140, y + 2, 70, 26), "<b>" + pb.Points[i] + "</b> / " + ParagonBoard.Cap, UISkin.Label, UISkin.Gold);
                UISkin.Shadowed(new Rect(r.x + 216, y + 4, 180, 26), ParagonBoard.Effects[i] + " each", UISkin.Small, UISkin.Muted);
                bool can = pb.Free > 0 && pb.Points[i] < ParagonBoard.Cap;
                if (can) NodeGlow(new Rect(r.xMax - 66, y, 34, 30), null, true, 0f);
                if (can && UISkin.Btn(new Rect(r.xMax - 66, y, 34, 30), "+", UISkin.SquareButton))
                {
                    p.SpendParagon(i);
                    LitNode("p:" + i, pb.Points[i] >= ParagonBoard.Cap);
                }
                y += 30;
            }
            if (pb.Spent > 0 && UISkin.Btn(new Rect(r.x + 32, y + 4, 130, 26), "Reset points", UISkin.Button)) p.ResetParagon();
        }

        static EquipSlot glintSlot;
        static float glintAt = -10f;

        /// <summary>Something was just put on in <paramref name="slot"/>: its square in the character window glints.</summary>
        public static void EquipGlint(EquipSlot slot) { glintSlot = slot; glintAt = Time.unscaledTime; }

        void DrawEquipSlot(Player p, Rect r, EquipSlot slot, bool labelLeft)
        {
            var item = p.Inventory.GetEquipped(slot);
            DrawItemSlot(r, item, p, SlotIcon(slot));
            float g = (Time.unscaledTime - glintAt) / 0.6f;
            if (slot == glintSlot && g < 1f && item != null)
            {
                // a bright band sweeping across the square, and a border in the item's colour fading out
                float bx = r.x - 16f + (r.width + 16f) * g;
                GUI.color = new Color(1f, 1f, 1f, 0.45f * (1f - g));
                GUI.DrawTexture(new Rect(Mathf.Max(r.x, bx), r.y, Mathf.Max(0f, Mathf.Min(16f, r.xMax - bx)), r.height), UISkin.White);
                var c = item.NameColor;
                GUI.color = new Color(c.r, c.g, c.b, 1f - g);
                GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, 2), UISkin.White);
                GUI.DrawTexture(new Rect(r.x - 2, r.yMax, r.width + 4, 2), UISkin.White);
                GUI.DrawTexture(new Rect(r.x - 2, r.y, 2, r.height), UISkin.White);
                GUI.DrawTexture(new Rect(r.xMax, r.y, 2, r.height), UISkin.White);
                GUI.color = Color.white;
            }
            if (item != null && r.Contains(Event.current.mousePosition))
                tooltip = item.Tooltip(p) + "\n<color=#998877>Click to unequip</color>";
            if (item == null && r.Contains(Event.current.mousePosition))
                tooltip = "<b>" + Item.SlotName(slot) + "</b>\n<color=#998877>Empty</color>";
            if (item != null && item.Sockets > 0) DrawSocketPips(r, item);
            if (item != null && ChatOpen && Event.current.shift && ClickedIn(r) == 0) { LinkItemInChat(item, -1, (int)slot); }
            else if (item != null && ClickedIn(r) == 0)
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
                string[] zones = { "Whisperwood", "Goblin Encampment", "Forsaken Graveyard", "Ironvein Quarry", "Hollowmere", "Crypt of the Lich",
                                   WorldGenerator.Frostpeak, WorldGenerator.Badlands, WorldGenerator.Ashen };
                Vector3[] centers =
                {
                    WorldGenerator.Map(new Vector3(80, 0, 128)), WorldGenerator.Map(new Vector3(130, 0, 80)), WorldGenerator.Map(new Vector3(80, 0, 42)),
                    WorldGenerator.Map(new Vector3(30, 0, 80)), WorldGenerator.Map(new Vector3(80, 0, 80)), new Vector3(144.5f, 0, 18),
                    new Vector3(70, 0, 420), new Vector3(420, 0, 60), new Vector3(380, 0, 400),
                };
                for (int i = 0; i < zones.Length; i++)
                {
                    // names stand out once you've been nearby (or anywhere in the zone); before that, faint, like rumours on an old chart
                    var c = toMap(centers[i]);
                    if (!ZoneKnown(centers[i], 30f) && (Player.I == null || Player.I.Achievements.Get("zone." + zones[i]) == 0))
                    {
                        UISkin.Shadowed(new Rect(c.x - 140, c.y - 14, 280, 30), zones[i], UISkin.V(UISkin.HeadingCenter, fontStyle: FontStyle.Italic),
                            new Color(0.85f, 0.75f, 0.58f, 0.38f), 1);
                        continue;
                    }
                    string zoneHere = WorldGenerator.ZoneAt(p.transform.position);
                    bool here = zoneHere.StartsWith(zones[i]) || zones[i].StartsWith(zoneHere); // "Hollowmere Village" is Hollowmere
                    UISkin.Shadowed(new Rect(c.x - 140, c.y - 14, 280, 30), zones[i], here ? UISkin.V(UISkin.HeadingCenter, fontSize: UISkin.HeadingCenter.fontSize + 4) : UISkin.HeadingCenter,
                        here ? UISkin.Gold : new Color(1f, 0.92f, 0.75f, 0.85f), 2);
                }
                // The outer towns (Hollowmere is labelled above) and the waystones
                for (int i = 1; i < WorldGenerator.Towns.Length; i++)
                {
                    var t = WorldGenerator.Towns[i];
                    if (!ZoneKnown(t.Center, 20f)) continue;
                    var c = toMap(t.Center);
                    UISkin.Shadowed(new Rect(c.x - 100, c.y - 10, 200, 22), t.Name, UISkin.SmallCenter, new Color(0.75f, 1f, 0.75f), 2);
                }
                foreach (var w in Waystone.Stones)
                    if (Waystone.Known(w.Town)) mark(w.Position, w.LabelColor, 9);
                if (AdminTools.ShowDungeons)
                    foreach (var def in DungeonDef.All)
                    {
                        mark(def.Entrance, new Color(1f, 0.55f, 0.3f), 12);
                        var c = toMap(def.Entrance);
                        UISkin.Shadowed(new Rect(c.x - 100, c.y + 6, 200, 20), def.Name, UISkin.SmallCenter, new Color(1f, 0.7f, 0.45f), 2);
                    }
                // what you've found: dungeon doors each with an icon; a town's shops, healer, stash, auction house and bounty
                // board as one row of small icons under its name (at this scale a whole town is a few dozen pixels)
                var services = new Dictionary<Settlement, List<(string icon, string label)>>();
                foreach (var it in Interactable.All)
                {
                    if (it == null || !Exploration.Seen(it.Position)) continue;
                    if (it is Npc qn && !qn.Away && qn.Marker(p, out _) != null) { mark(qn.Position, new Color(1f, 0.85f, 0.1f), 9); continue; }
                    string icon = MapIcon(it, out string label);
                    if (icon == null) continue;
                    var town = it is DungeonEntrance ? null : WorldGenerator.TownAt(it.Position);
                    if (town != null)
                    {
                        if (!services.TryGetValue(town, out var list)) services[town] = list = new List<(string, string)>();
                        if (!list.Exists(x => x.icon == icon)) list.Add((icon, label));
                        else { int k = list.FindIndex(x => x.icon == icon); list[k] = (icon, list[k].label + "\n" + label); }
                        continue;
                    }
                    var m = toMap(it.Position);
                    if (!r.Contains(m)) continue;
                    var ir = new Rect(m.x - 9, m.y - 9, 18, 18);
                    bool over = ir.Contains(Event.current.mousePosition);
                    GUI.color = new Color(0.05f, 0.03f, 0.02f, over ? 0.9f : 0.65f);
                    GUI.DrawTexture(new Rect(m.x - 12, m.y - 12, 24, 24), UISkin.Circle);
                    GUI.color = Color.white;
                    UISkin.IconInSlot(over ? new Rect(m.x - 12, m.y - 12, 24, 24) : ir, UISkin.Icon(icon), Color.white, 0);
                    if (it is DungeonEntrance de)
                        UISkin.Shadowed(new Rect(m.x - 100, m.y + 10, 200, 20), de.Def.Name, UISkin.SmallCenter, new Color(1f, 0.7f, 0.45f), 2);
                    if (over) tooltip = label;
                }
                foreach (var kv in services)
                {
                    var list = kv.Value;
                    const float sz = 14f, gap = 2f;
                    var c = toMap(kv.Key.Center);
                    var row = new Rect(c.x - (list.Count * (sz + gap) - gap) / 2f, c.y + 12f, list.Count * (sz + gap) - gap, sz);
                    if (!r.Contains(row.center)) continue;
                    GUI.color = new Color(0.05f, 0.03f, 0.02f, 0.7f);
                    GUI.DrawTexture(new Rect(row.x - 3, row.y - 2, row.width + 6, row.height + 4), UISkin.White);
                    GUI.color = Color.white;
                    for (int i = 0; i < list.Count; i++)
                        UISkin.IconInSlot(new Rect(row.x + i * (sz + gap), row.y, sz, sz), UISkin.Icon(list[i].icon), Color.white, 0);
                    if (row.Contains(Event.current.mousePosition))
                        tooltip = "<b>" + kv.Key.Name + "</b>\n" + string.Join("\n", list.ConvertAll(x => x.label.Replace("<b>", "").Replace("</b>", "")));
                }
            }
            if (AdminTools.ShowEnemies)
                foreach (var e in Enemy.ById.Values)
                    if (e != null && !e.IsDead) mark(e.transform.position, e.Def.Boss ? new Color(1f, 0.5f, 0f) : e.Elite ? Enemy.ChampionColor : new Color(0.9f, 0.15f, 0.1f), e.Def.Boss ? 10 : 6);
            var net = NetClient.I;
            foreach (var rp in RemotePlayer.ById.Values) if (rp != null && !net.IsPartyMember(rp.Id)) mark(rp.transform.position, new Color(0.3f, 0.6f, 1f), 9);
            foreach (var m in net.Party) // the party, wherever they are on this map, with their names
            {
                if (m.id == net.MyId || m.di != net.DungeonId) continue;
                Vector3 at = RemotePlayer.ById.TryGetValue(m.id, out var mrp) && mrp != null ? mrp.transform.position : new Vector3(m.x, 0f, m.z);
                var mp = toMap(at);
                if (!r.Contains(mp)) continue;
                var mc = m.dead ? new Color(0.55f, 0.5f, 0.5f) : MemberColor(m.id);
                DotAt(mp, mc, 12);
                UISkin.Shadowed(new Rect(mp.x - 90, mp.y + 7, 180, 20), m.name + (m.dead ? " (dead)" : ""), UISkin.SmallCenter, mc, 2);
            }
            if (WorldBoss.Up && !Dungeon.Active)
            {
                mark(WorldBoss.Position, WorldBoss.Color, 14f);
                var bp = toMap(WorldBoss.Position);
                if (r.Contains(bp)) UISkin.Shadowed(new Rect(bp.x - 130, bp.y + 9, 260, 20), WorldBoss.Current.name, UISkin.SmallCenter, WorldBoss.Color, 2);
            }
            if (Invasion.Active && !Dungeon.Active)
            {
                mark(Invasion.Gate, Invasion.Color, 12f + 5f * Mathf.Abs(Mathf.Sin(Time.time * 3f)));
                var ig = toMap(Invasion.Gate);
                if (r.Contains(ig)) UISkin.Shadowed(new Rect(ig.x - 110, ig.y + 8, 220, 20), "Under attack!", UISkin.SmallCenter, Invasion.Color, 2);
            }
            mark(p.transform.position, Color.white, 11);
            MapPing.Draw(toMap, r, true);

            string hint = (AdminTools.IsAdmin && !underground ? "Click or M to close   -   Admin: right-click to teleport there" : "Click anywhere or press M to close") +
                          (net.InParty ? "   -   Alt+click to ping your party" : "");
            UISkin.Shadowed(new Rect(r.x, r.yMax - 28, r.width, 24), hint, UISkin.SmallCenter, UISkin.Cream);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.alt && r.Contains(Event.current.mousePosition))
            {
                var mp = Event.current.mousePosition;
                MapPing.Send(new Vector3(o.x + (mp.x - r.x) / r.width * mw, 0f, o.z + (1f - (mp.y - r.y) / r.height) * mh));
                Event.current.Use();
            }
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

        /// <summary>The world map's icon for a place you've found (null: not shown), and what hovering it says.</summary>
        static string MapIcon(Interactable it, out string label)
        {
            label = it.DisplayName;
            switch (it)
            {
                case Npc npc when npc.Role == NpcRole.Healer:
                    label = "<b>" + npc.DisplayName + "</b>\nHealer";
                    return "heal";
                case Npc npc when npc.Role == NpcRole.Vendor && npc.Shop != null:
                    label = "<b>" + npc.DisplayName + "</b>\n" + (string.IsNullOrEmpty(npc.Title) ? npc.Shop.Kind + " goods" : npc.Title);
                    switch (npc.Shop.Kind)
                    {
                        case VendorKind.Weapons: return "sword";
                        case VendorKind.Armor: return "helm";
                        case VendorKind.Food: return "cooked_fish";
                        case VendorKind.Curios: return "gem";
                        case VendorKind.Companions: return "companions";
                        default: return "gold";
                    }
                case StashChest _: label = "<b>Stash</b>\nYour own chest, the same in every town"; return "stash";
                case AuctionPodium _: label = "<b>Auction House</b>"; return "trade";
                case BountyBoard _: label = "<b>Bounty Board</b>\nToday's bounties"; return "quests";
                case DungeonEntrance de: label = "<b>" + de.Def.Name + "</b>\nDungeon, level " + de.Def.MinLevel + "+"; return "ach_stairs";
                default: return null;
            }
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

        Vector2 helpScroll;

        void DrawHelp()
        {
            var r = new Rect((VW - 600) / 2, 40, 600, Mathf.Min(720, VH - 50));
            if (UISkin.Window(r, "How to Play", true, true)) showHelp = false;
            Block(r);
            string text =
                "<b>Combat</b>\n" +
                "Left-click the ground to move (hold to keep walking). Left-click a monster to attack it; Shift+click attacks in place.\n" +
                "<b>1-5</b> your class's abilities (right-click casts ability 2),  <b>Q / E</b> health / mana potions,  <b>R</b> recall to town (and back),  <b>V</b> mount / dismount,  " +
                "<b>Alt</b> shows every item on the ground,  mouse wheel zooms.\n\n" +
                "<b>Windows</b>\n" +
                "<b>I</b> bags   <b>C</b> character   <b>T</b> talents   <b>K</b> skills   <b>L</b> quests   <b>Y</b> achievements   <b>M</b> map   <b>Enter</b> chat   <b>Esc</b> close / game menu\n" +
                "<b>Camera:</b> middle-drag or arrow keys rotate and tilt,  <b>Space</b> resets\n\n" +
                "<b>Emotes:</b> <b>G</b> opens the emote menu, or type /wave, /dance, /bow, /sit, /sleep... (/e lists them all)\n\n" +
                "<b>Chat & parties</b>\n" +
                "<b>/p</b> party chat,  <b>/w name</b> whisper,  <b>/r</b> reply,  <b>/invite name</b>,  <b>/leave</b>,  <b>/who</b>. " +
                "Right-click a player (or click their name) to invite them, whisper or trade. Party members nearby share kills; share quests from the quest log.\n\n" +
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
                "Far south: Crypt of the Lich (boss)";
            // scrolls (mouse wheel, or drag the bar) when the window is shorter than the text
            var view = new Rect(r.x + 28, r.y + 58, 552, r.height - 110);
            float textH = UISkin.InkRich.CalcHeight(new GUIContent(text), 530f) + 12f;
            helpScroll = GUI.BeginScrollView(view, helpScroll, new Rect(0, 0, 530, textH));
            GUI.Label(new Rect(0, 0, 530, textH), text, UISkin.InkRich);
            GUI.EndScrollView();
            GUI.Label(new Rect(r.x + 28, r.yMax - 44, 544, 26), "Graphics, sound and other settings: press <b>Esc</b> and choose <b>Settings</b>.", UISkin.InkRich);
        }

        void DrawDeath(Player p)
        {
            // it all fades in after the fall: the dark, then the words, then the way back
            float t = Time.time - p.DiedAt;
            float dark = Mathf.Clamp01(t / 1.4f), words = Mathf.Clamp01((t - 0.8f) / 1f), button = Mathf.Clamp01((t - 1.8f) / 0.6f);
            GUI.color = new Color(0.12f, 0f, 0f, 0.6f * dark);
            GUI.DrawTexture(new Rect(0, 0, VW, VH), UISkin.White);
            GUI.color = Color.white;
            Block(new Rect(0, 0, VW, VH));
            var red = new Color(0.9f, 0.2f, 0.15f, words);
            float drift = (1f - words) * 18f;
            UISkin.Shadowed(new Rect(0, VH * 0.28f + drift, VW, 90), "You Have Died", UISkin.TitleHuge, red, 2);
            if (button <= 0f) return;
            GUI.color = new Color(1f, 1f, 1f, button);
            if (UISkin.Btn(new Rect((VW - 320) / 2, VH * 0.28f + 110, 320, 52), "Release Spirit", UISkin.Button)) p.Respawn();
            UISkin.Shadowed(new Rect(0, VH * 0.28f + 168, VW, 24), "You will wake in the nearest town whose waystone you know, and lose 15% of your gold.", UISkin.SmallCenter, new Color(UISkin.Cream.r, UISkin.Cream.g, UISkin.Cream.b, button));
            GUI.color = Color.white;
        }

        /// <summary>A quest giver's "!" or "?": bobbing over their head with a soft glow; a quest ready to hand in pulses.</summary>
        static void QuestMarker(Vector2 g, string mark, Color mc, int seed)
        {
            bool live = mc.b < 0.5f; // gold: available or ready (grey: not yet)
            bool ready = live && mark == "?";
            float t = Time.time;
            float bob = Mathf.Sin(t * 2.4f + seed * 0.37f) * 5f;
            float pulse = ready ? 1f + 0.13f * Mathf.Sin(t * 5f) : 1f;
            var c = new Vector2(g.x, g.y - 46 + bob);
            if (live && Event.current.type == EventType.Repaint)
            {
                float glow = (ready ? 70f : 56f) * pulse;
                GUI.color = new Color(1f, 0.8f, 0.2f, ready ? 0.22f + 0.1f * Mathf.Sin(t * 5f) : 0.14f);
                GUI.DrawTexture(new Rect(c.x - glow / 2, c.y - glow / 2, glow, glow), UISkin.Circle);
                GUI.color = Color.white;
            }
            var style = UISkin.V(UISkin.TitleHuge, fontSize: Mathf.RoundToInt(UISkin.TitleHuge.fontSize * pulse / 4f) * 4); // (steps of 4: see the damage numbers)
            UISkin.Shadowed(new Rect(c.x - 50, c.y - 40, 100, 80), mark, style, live ? Color.Lerp(mc, Color.white, ready ? 0.25f * (pulse - 0.87f) / 0.26f : 0f) : mc, 2);
        }

        // ---- rewards flying from a quest giver's hands into the bags

        struct Flyer { public Vector2 From; public float Start; public string Icon; public Color Tint; public float Arc; }
        static readonly List<Flyer> flyers = new List<Flyer>();
        static Vector2 bagButton = new Vector2(-1f, -1f);
        static float bagBumpAt = -10f;

        /// <summary>Coins (and the reward item) arc from <paramref name="from"/> (GUI) into the bags button, one after another.</summary>
        public static void RewardsToBags(Vector2 from, int coins, Color? item)
        {
            for (int i = 0; i < coins; i++)
                flyers.Add(new Flyer { From = from + Random.insideUnitCircle * 18f, Start = Time.unscaledTime + 0.15f + i * 0.07f, Icon = "gold", Tint = Color.white, Arc = Random.Range(80f, 160f) });
            if (item.HasValue)
                flyers.Add(new Flyer { From = from, Start = Time.unscaledTime + 0.25f + coins * 0.07f, Icon = "bags", Tint = item.Value, Arc = 190f });
        }

        /// <summary>Where a quest's rewards set off from: the quest giver's window when it's open, else above the hero.</summary>
        public static Vector2 QuestRewardOrigin(Player p)
        {
            if (I != null && I.dialogNpc != null) return new Vector2(14 + 235, 120 + 260);
            if (I != null && p != null && I.WorldToGui(p.transform.position + Vector3.up * 2f, out var g)) return g;
            return I != null ? new Vector2(I.VW / 2f, I.VH / 2f) : Vector2.zero;
        }

        void DrawFlyers()
        {
            if (flyers.Count == 0) return;
            var to = bagButton.x < 0f ? new Vector2(VW - 200, VH - 40) : bagButton;
            const float flight = 0.7f;
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                float k = (Time.unscaledTime - f.Start) / flight;
                if (k < 0f) continue;
                if (k >= 1f)
                {
                    flyers.RemoveAt(i);
                    bagBumpAt = Time.unscaledTime;
                    if (f.Icon == "gold") Sfx.Play2D("coins", 0.12f, Random.Range(1.1f, 1.4f));
                    else Sfx.Play2D("loot", 0.5f);
                    continue;
                }
                if (Event.current.type != EventType.Repaint) continue;
                float e = k * k * (3f - 2f * k);
                var pos = Vector2.Lerp(f.From, to, e) + Vector2.down * Mathf.Sin(k * Mathf.PI) * f.Arc;
                float size = (f.Icon == "gold" ? 26f : 40f) * (1f - 0.35f * k);
                if (f.Icon != "gold")
                {
                    GUI.color = new Color(f.Tint.r, f.Tint.g, f.Tint.b, 0.35f);
                    GUI.DrawTexture(new Rect(pos.x - size, pos.y - size, size * 2, size * 2), UISkin.Circle);
                    GUI.color = Color.white;
                }
                UISkin.IconInSlot(new Rect(pos.x - size / 2, pos.y - size / 2, size, size), UISkin.Icon(f.Icon), f.Tint, 0);
            }
        }

        static Color flashColor;
        static float flashAt = -10f, flashFor = 1f;

        /// <summary>A full-screen flash of <paramref name="color"/> fading out over <paramref name="seconds"/>.</summary>
        public static void ScreenFlash(Color color, float seconds) { if (!GameSettings.Flashes) return; flashColor = color; flashAt = Time.unscaledTime; flashFor = Mathf.Max(0.05f, seconds); }

        void DrawScreenFlash()
        {
            float k = 1f - (Time.unscaledTime - flashAt) / flashFor;
            if (k <= 0f) return;
            GUI.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0.85f * k * k);
            GUI.DrawTexture(new Rect(0, 0, VW, VH), UISkin.White);
            GUI.color = Color.white;
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

        string typedBody;
        float typedAt;

        void DrawDialog(Player p)
        {
            var npc = dialogNpc;
            float height = npc.Role == NpcRole.Vendor && npc.Shop != null && npc.Shop.Kind == VendorKind.Companions ? 290 + CompanionDef.All.Length * 80 + MountDef.All.Length * 62
                : npc.Role == NpcRole.Vendor && npc.Shop != null ? 330 + npc.Shop.Items.Count * 54 + (npc.Shop.Kind == VendorKind.Curios || npc.Shop.Kind == VendorKind.Weapons || npc.Shop.Kind == VendorKind.Armor || npc.Shop.Kind == VendorKind.General ? 54 : 0)
                : npc.Role == NpcRole.QuestGiver ? QuestDialogHeight(npc) : 520;
            var r = new Rect(14, 120, 470, Mathf.Min(height, VH - 140));
            if (UISkin.Window(r, npc.DisplayName, true, true)) { dialogNpc = null; return; }
            Block(r);

            float y = r.y + 58;
            GUI.Label(new Rect(r.x + 26, y, 420, 56), "<i>\"" + (npc.NightGreeting ?? npc.Greeting) + "\"</i>", UISkin.InkRich);
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
                    var bodyRect = new Rect(r.x + 26, y, 420, bodyH);
                    // the giver's words come out a few at a time (a click shows them all)
                    if (body != typedBody) { typedBody = body; typedAt = Time.unscaledTime; }
                    int shown = body.IndexOf('<') >= 0 ? body.Length : Mathf.Min(body.Length, Mathf.FloorToInt((Time.unscaledTime - typedAt) * 70f));
                    if (shown < body.Length && ClickedIn(bodyRect) == 0) { typedAt = -100f; shown = body.Length; }
                    // the rest is there but see-through, so the words don't jump about as they wrap
                    GUI.Label(bodyRect, shown >= body.Length ? body : body.Substring(0, shown) + "<color=#00000000>" + body.Substring(shown) + "</color>", bodyStyle);
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
                    shop.Refresh();
                    UISkin.Shadowed(new Rect(r.x + 26, y, 420, 28), "For Sale", UISkin.Heading, UISkin.Gold);
                    if (shop.Rotates)
                        GUI.Label(new Rect(r.x + 200, y + 4, 244, 24), "New stock in " + Mathf.CeilToInt(shop.SecondsUntilRestock / 60f) + " min",
                            UISkin.V(UISkin.Ink14, alignment: TextAnchor.UpperRight));
                    y += 36;
                    if (!shop.Loaded) { GUI.Label(new Rect(r.x + 26, y, 420, 24), "<i>Unpacking the wares...</i>", UISkin.InkRich); y += 54; }
                    for (int i = 0; i < shop.Items.Count; i++)
                        y = ShopRow(p, r, y, shop, i);
                    y += 14;
                    GUI.Label(new Rect(r.x + 26, y, 420, 50),
                        "Right-click items in your bags to sell them. You have <color=#f0c45a><b>" + p.Gold + " gold</b></color>.", UISkin.Ink14);
                    y += 56;
                    if (UISkin.Btn(new Rect(r.x + (r.width - 340) / 2, y, 340, 46), "Sell Common Items & Materials", UISkin.Button))
                        NetClient.I?.Op("sellcommon");
                    if (shop.Kind == VendorKind.General)
                    {
                        y += 54;
                        if (UISkin.Btn(new Rect(r.x + (r.width - 340) / 2, y, 340, 46), "Auction House", UISkin.Button)) OpenAuction();
                    }
                    if (shop.Kind == VendorKind.Weapons || shop.Kind == VendorKind.Armor)
                    {
                        y += 54;
                        if (UISkin.Btn(new Rect(r.x + (r.width - 340) / 2, y, 340, 46), "Salvage & Reforge Gear", UISkin.Button)) OpenForge();
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
                NetClient.I?.Op("buy", k: shop.Kind.ToString(), i: index, n: n, name: item.Name); // the server sends the new stock
                break;
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
                    for (int i = 0, n = Mathf.Min(50, have / rec.InputCount); i < n; i++)
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

        // ---- the announcement lane: zone names, banners, achievement plates and questions (party invites, ready
        // checks, trades...) stack down the middle of the screen in that order, each taking the next free space

        float laneY;

        /// <summary>The top of the next free space in the lane, <paramref name="height"/> tall (and moves the lane on).</summary>
        float Lane(float height)
        {
            float y = laneY;
            laneY += height + 8f;
            return y;
        }

        static string cardTitle, cardSub;
        static Color cardColor;
        static float cardAt = -10f;

        /// <summary>A place's name in big type with a line under it (entering a zone, a dungeon, arriving by waystone).</summary>
        public static void TitleCard(string title, string subtitle, Color color)
        {
            cardTitle = title;
            cardSub = subtitle;
            cardColor = color;
            cardAt = Time.time;
        }

        void DrawTitleCard()
        {
            float age = Time.time - cardAt;
            if (age > 3.6f || string.IsNullOrEmpty(cardTitle)) return;
            float a = age < 0.5f ? age / 0.5f : age > 2.8f ? 1f - (age - 2.8f) / 0.8f : 1f;
            float y = Lane(string.IsNullOrEmpty(cardSub) ? 76f : 106f);
            var c = cardColor; c.a = a;
            UISkin.Shadowed(new Rect(0, y, VW, 64), cardTitle, UISkin.TitleHuge, c, 3);
            // a rule under the name that draws out from the middle
            float w = Mathf.Lerp(40f, 420f, Mathf.Clamp01(age / 0.8f));
            GUI.color = new Color(c.r, c.g, c.b, a * 0.8f);
            GUI.DrawTexture(new Rect((VW - w) / 2f, y + 70, w, 2), UISkin.White);
            GUI.color = Color.white;
            if (!string.IsNullOrEmpty(cardSub))
                UISkin.Shadowed(new Rect(0, y + 78, VW, 26), cardSub, UISkin.SmallCenter, new Color(0.9f, 0.88f, 0.82f, a * Mathf.Clamp01((age - 0.3f) / 0.5f)), 2);
        }

        void DrawBanner()
        {
            float age = Time.time - bannerTime;
            if (age > 3.5f || string.IsNullOrEmpty(bannerText)) return;
            var c = bannerColor;
            c.a = age < 0.2f ? age / 0.2f : age > 2.5f ? 1f - (age - 2.5f) : 1f;
            UISkin.Shadowed(new Rect(0, Lane(50f), VW, 50), bannerText, UISkin.Banner, c, 2);
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

        void Block(Rect r)
        {
            if (Event.current.type == EventType.Repaint) blockRects.Add(r);
        }

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
