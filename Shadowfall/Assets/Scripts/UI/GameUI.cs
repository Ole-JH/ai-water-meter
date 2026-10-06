using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// All UI, drawn with Unity's immediate-mode GUI (no Canvas/TextMeshPro setup needed, works in WebGL).
    /// Login screen, HUD (orbs, action bar, XP bar, unit frames, minimap, quest tracker, chat),
    /// windows (bags, character, skills, quests, world map, help), NPC dialogs, vendor, crafting.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public static GameUI I;

        // ---- state read by gameplay code
        public bool MouseOverUI { get; private set; }
        public bool ChatOpen { get; private set; }
        public bool KeyboardCaptured => ChatOpen || Player.I == null;
        public bool BlocksWorldInput => Player.I == null || Player.I.IsDead || showMap;

        // ---- windows
        bool showBags, showChar, showSkills, showQuests, showMap, showHelp;
        Npc dialogNpc;
        CraftingStation craftStation;

        // ---- login
        string loginName = "", loginPass = "", serverUrl = "";

        // ---- chat
        string chatText = "";
        bool focusChat;

        // ---- layout
        const float RefHeight = 900f;
        float scale = 1f, VW, VH;
        readonly List<Rect> blockRects = new List<Rect>();
        string tooltip;

        // ---- feedback
        struct FloatText { public Vector3 Pos; public string Text; public Color Color; public float Time, Size; }
        struct LogLine { public string Text; public Color Color; public float Time; }
        static readonly List<FloatText> floats = new List<FloatText>();
        static readonly List<LogLine> log = new List<LogLine>();
        static string bannerText;
        static Color bannerColor;
        static float bannerTime = -99f;

        // ---- assets
        Texture2D circle, white;
        GUIStyle label, labelCenter, labelSmall, title, box, button, slotText, rich, floatStyle, bannerStyle, field;
        bool stylesReady;

        void Awake()
        {
            I = this;
            circle = MakeCircle(128);
            white = Texture2D.whiteTexture;
            try
            {
                loginName = PlayerPrefs.GetString("sf_name", "");
                serverUrl = PlayerPrefs.GetString("sf_server", "ws://localhost:7341/ws");
            }
            catch (System.Exception) { }
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
            if (npc.Role == NpcRole.Vendor) showBags = true;
        }

        public void OpenCrafting(CraftingStation s)
        {
            craftStation = s;
            dialogNpc = null;
        }

        // =====================================================================================
        // Update: hotkeys, hover detection, auto-closing dialogs
        // =====================================================================================

        void Update()
        {
            scale = Mathf.Max(0.55f, Screen.height / RefHeight);
            var mp = GameInput.MousePosition;
            var guiMouse = new Vector2(mp.x / scale, (Screen.height - mp.y) / scale);
            bool over = false;
            foreach (var r in blockRects) if (r.Contains(guiMouse)) { over = true; break; }
            MouseOverUI = over;

            var p = Player.I;
            if (p == null) { ChatOpen = false; return; }

            if (!ChatOpen)
            {
                if (GameInput.Down(GKey.I) || GameInput.Down(GKey.B)) showBags = !showBags;
                if (GameInput.Down(GKey.C)) showChar = !showChar;
                if (GameInput.Down(GKey.K)) showSkills = !showSkills;
                if (GameInput.Down(GKey.L)) showQuests = !showQuests;
                if (GameInput.Down(GKey.M)) showMap = !showMap;
                if (GameInput.Down(GKey.F1) || GameInput.Down(GKey.H)) showHelp = !showHelp;
                if (GameInput.Down(GKey.Escape))
                {
                    if (dialogNpc != null || craftStation != null) { dialogNpc = null; craftStation = null; }
                    else showBags = showChar = showSkills = showQuests = showMap = showHelp = false;
                }
            }

            if (dialogNpc != null && Factory.FlatDistance(p.transform.position, dialogNpc.transform.position) > 5f) dialogNpc = null;
            if (craftStation != null && Factory.FlatDistance(p.transform.position, craftStation.transform.position) > 5f) craftStation = null;
        }

        // =====================================================================================
        // OnGUI
        // =====================================================================================

        void OnGUI()
        {
            InitStyles();
            if (Event.current.type == EventType.Layout) blockRects.Clear();
            scale = Mathf.Max(0.55f, Screen.height / RefHeight);
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

            DrawWorldOverlays(p);
            DrawFloatingText();
            DrawUnitFrames(p);
            DrawMinimap(p);
            DrawQuestTracker(p);
            DrawActionBar(p);
            DrawLog(true);

            if (showBags) DrawBags(p);
            if (showChar) DrawCharacter(p);
            if (showSkills) DrawSkills(p);
            if (showQuests) DrawQuestLog(p);
            if (dialogNpc != null) DrawDialog(p);
            if (craftStation != null) DrawCrafting(p);
            if (showHelp) DrawHelp();
            if (showMap) DrawWorldMap(p);
            if (p.IsDead) DrawDeath(p);

            DrawBanner();
            DrawTooltip();
        }

        // =====================================================================================
        // Login
        // =====================================================================================

        void DrawLogin()
        {
            var net = NetClient.I;
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(new Rect(0, 0, VW, VH), white);
            GUI.color = Color.white;

            Shadowed(new Rect(0, VH * 0.12f, VW, 80), "SHADOWFALL", title, new Color(1f, 0.75f, 0.3f));
            Shadowed(new Rect(0, VH * 0.12f + 70, VW, 30), "A world of heroes, monsters and loot", labelCenter, new Color(0.85f, 0.8f, 0.7f));

#if UNITY_WEBGL && !UNITY_EDITOR
            bool showServer = false;
#else
            bool showServer = true;
#endif
            float w = 380, h = showServer ? 310 : 260;
            var r = new Rect((VW - w) / 2, VH * 0.36f, w, h);
            Panel(r, "Log in");
            bool busy = net.State == NetClient.ConnState.Connecting || net.State == NetClient.ConnState.LoggingIn;

            float y = r.y + 44;
            GUI.Label(new Rect(r.x + 20, y, 120, 26), "Character", label);
            GUI.SetNextControlName("login_name");
            loginName = GUI.TextField(new Rect(r.x + 130, y, 230, 28), loginName, 16, field);
            y += 38;
            GUI.Label(new Rect(r.x + 20, y, 120, 26), "Password", label);
            loginPass = GUI.PasswordField(new Rect(r.x + 130, y, 230, 28), loginPass, '*', 64, field);
            y += 38;
            if (showServer)
            {
                GUI.Label(new Rect(r.x + 20, y, 120, 26), "Server", label);
                serverUrl = GUI.TextField(new Rect(r.x + 130, y, 230, 28), serverUrl, 200, field);
                y += 38;
            }
            GUI.Label(new Rect(r.x + 20, y, w - 40, 40), "New name? An account is created automatically.", labelSmall);
            y += 30;

            bool enter = Event.current.type == EventType.KeyDown &&
                         (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            GUI.enabled = !busy;
            if (GUI.Button(new Rect(r.x + 20, y, w - 40, 40), busy ? "Connecting..." : "Enter World", button) || (enter && !busy))
            {
                try
                {
                    PlayerPrefs.SetString("sf_name", loginName);
                    PlayerPrefs.SetString("sf_server", serverUrl);
                }
                catch (System.Exception) { }
                net.Login(showServer ? serverUrl : "", loginName, loginPass);
            }
            GUI.enabled = true;
            y += 50;
            if (!string.IsNullOrEmpty(net.Status))
                GUI.Label(new Rect(r.x + 10, y, w - 20, 40), net.Status, labelCenter);
        }

        // =====================================================================================
        // World-space overlays: nameplates, health bars, loot labels, quest markers
        // =====================================================================================

        bool WorldToGui(Vector3 world, out Vector2 gui)
        {
            var sp = GameManager.I.Cam.WorldToScreenPoint(world);
            gui = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
            return sp.z > 0f;
        }

        void DrawWorldOverlays(Player p)
        {
            var hovered = p.HoveredEnemy;

            foreach (var e in Enemy.ById.Values)
            {
                if (e == null || e.IsDead) continue;
                if (Factory.FlatDistance(e.transform.position, p.transform.position) > 32f) continue;
                if (!WorldToGui(e.transform.position + Vector3.up * (e.Height + 0.35f), out var g)) continue;
                bool focus = e == hovered || e == p.AttackTarget || e.Def.Boss;
                bool hurt = e.Health < e.MaxHealth;
                if (!focus && !hurt) continue;
                float bw = e.Def.Boss ? 110 : 60;
                Bar(new Rect(g.x - bw / 2, g.y, bw, 7), e.Health / e.MaxHealth, new Color(0.8f, 0.1f, 0.1f));
                if (focus)
                    Shadowed(new Rect(g.x - 120, g.y - 20, 240, 20), e.DisplayName + "  (" + e.Level + ")" + (e.Slowed ? "  [Slowed]" : ""),
                        labelSmallCenter, LevelColor(e.Level, p.Level));
            }

            foreach (var rp in RemotePlayer.ById.Values)
            {
                if (rp == null) continue;
                if (!WorldToGui(rp.transform.position + Vector3.up * 2.45f, out var g)) continue;
                Shadowed(new Rect(g.x - 120, g.y - 20, 240, 20), rp.Name + "  (" + rp.Level + ")" + (rp.Dead ? "  [Dead]" : ""),
                    labelSmallCenter, new Color(0.45f, 0.75f, 1f));
                Bar(new Rect(g.x - 30, g.y, 60, 5), rp.Health / rp.MaxHealth, new Color(0.2f, 0.8f, 0.2f));
            }

            // Own nameplate
            if (WorldToGui(p.transform.position + Vector3.up * 2.45f, out var pg))
                Shadowed(new Rect(pg.x - 120, pg.y - 18, 240, 20), p.DisplayName, labelSmallCenter, new Color(0.6f, 0.9f, 1f));

            foreach (var it in Interactable.All)
            {
                if (it == null) continue;
                float dist = Factory.FlatDistance(it.Position, p.transform.position);
                if (dist > 28f) continue;

                if (it is Npc npc)
                {
                    if (!WorldToGui(it.Position + Vector3.up * it.LabelHeight, out var g)) continue;
                    Shadowed(new Rect(g.x - 120, g.y - 18, 240, 20), npc.DisplayName, labelSmallCenter, npc.LabelColor);
                    if (!string.IsNullOrEmpty(npc.Title))
                        Shadowed(new Rect(g.x - 120, g.y - 2, 240, 18), "<" + npc.Title + ">", labelSmallCenter, new Color(0.7f, 0.9f, 0.7f));
                    var mark = npc.Marker(p, out var mc);
                    if (mark != null) Shadowed(new Rect(g.x - 30, g.y - 62, 60, 46), mark, title, mc);
                    continue;
                }

                if (it is LootDrop drop && drop.CanInteract)
                {
                    if (!WorldToGui(it.Position + Vector3.up * it.LabelHeight, out var g)) continue;
                    var text = drop.HoverText;
                    var size = labelSmall.CalcSize(new GUIContent(text));
                    var r = new Rect(g.x - size.x / 2 - 6, g.y - 10, size.x + 12, 20);
                    GUI.color = new Color(0, 0, 0, 0.7f);
                    GUI.DrawTexture(r, white);
                    GUI.color = Color.white;
                    Shadowed(r, text, labelSmallCenter, drop.LabelColor);
                    Block(r);
                    if (ClickedIn(r) == 0) p.SetInteract(drop);
                    continue;
                }

                if (it == p.HoveredInteractable)
                {
                    if (!WorldToGui(it.Position + Vector3.up * it.LabelHeight, out var g)) continue;
                    Shadowed(new Rect(g.x - 160, g.y - 10, 320, 20), it.HoverText, labelSmallCenter, it.LabelColor);
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
                floatStyle.fontSize = Mathf.RoundToInt(17 * f.Size * (age < 0.1f ? 1.3f : 1f));
                var c = f.Color;
                c.a = age > 0.9f ? 1f - (age - 0.9f) / 0.4f : 1f;
                Shadowed(new Rect(g.x - 150, g.y - 15, 300, 30), f.Text, floatStyle, c);
            }
        }

        // =====================================================================================
        // HUD
        // =====================================================================================

        void DrawUnitFrames(Player p)
        {
            var r = new Rect(12, 12, 250, 74);
            Panel(r, null);
            Block(r);
            GUI.Label(new Rect(r.x + 10, r.y + 6, 230, 20), "<b>" + p.DisplayName + "</b>   Level " + p.Level, rich);
            Bar(new Rect(r.x + 10, r.y + 30, 230, 16), p.Health / p.MaxHealth, new Color(0.15f, 0.7f, 0.15f),
                Mathf.CeilToInt(p.Health) + " / " + Mathf.CeilToInt(p.MaxHealth));
            Bar(new Rect(r.x + 10, r.y + 50, 230, 14), p.Mana / p.MaxMana, new Color(0.2f, 0.35f, 0.9f),
                Mathf.FloorToInt(p.Mana) + " / " + Mathf.FloorToInt(p.MaxMana));
            if (p.StatPoints > 0)
            {
                var sr = new Rect(r.x, r.yMax + 4, 250, 22);
                if (GUI.Button(sr, "+" + p.StatPoints + " attribute points  [C]", button)) showChar = true;
                Block(sr);
            }

            Combatant target = p.HoveredEnemy != null ? p.HoveredEnemy : p.AttackTarget;
            if (target != null && !target.IsDead)
            {
                var t = new Rect(272, 12, 250, 56);
                Panel(t, null);
                var e = target as Enemy;
                bool boss = e != null && e.Def.Boss;
                GUI.Label(new Rect(t.x + 10, t.y + 6, 230, 20),
                    "<b><color=#" + Item.Hex(LevelColor(target.Level, p.Level)) + ">" + target.DisplayName + "</color></b>   " +
                    (boss ? "<color=#ff9933>Boss</color> " : "") + "Level " + target.Level, rich);
                Bar(new Rect(t.x + 10, t.y + 30, 230, 16), target.Health / target.MaxHealth, new Color(0.75f, 0.12f, 0.1f),
                    Mathf.CeilToInt(target.Health) + " / " + Mathf.CeilToInt(target.MaxHealth));
            }
        }

        void DrawActionBar(Player p)
        {
            const float slot = 54, gap = 6;
            int count = AbilityDef.All.Length + 2;
            float barW = count * (slot + gap) - gap;
            float x0 = (VW - barW) / 2, y0 = VH - slot - 26;

            // XP bar
            var xr = new Rect(0, VH - 14, VW, 14);
            Bar(xr, (float)p.Xp / p.XpToNext, new Color(0.55f, 0.3f, 0.85f), "XP " + p.Xp + " / " + p.XpToNext);
            Block(xr);

            // Orbs
            float orb = 120;
            DrawOrb(new Rect(x0 - orb - 20, VH - orb - 18, orb, orb), p.Health / p.MaxHealth, new Color(0.75f, 0.08f, 0.08f),
                Mathf.CeilToInt(p.Health).ToString());
            DrawOrb(new Rect(x0 + barW + 20, VH - orb - 18, orb, orb), p.Mana / p.MaxMana, new Color(0.12f, 0.25f, 0.85f),
                Mathf.FloorToInt(p.Mana).ToString());

            var bg = new Rect(x0 - 8, y0 - 8, barW + 16, slot + 16);
            Panel(bg, null);
            Block(bg);

            for (int i = 0; i < AbilityDef.All.Length; i++)
            {
                var a = AbilityDef.All[i];
                var r = new Rect(x0 + i * (slot + gap), y0, slot, slot);
                bool locked = p.Level < a.RequiredLevel;
                GUI.color = locked ? new Color(0.25f, 0.25f, 0.25f) : Factory.Shade(a.Color, 0.75f);
                GUI.DrawTexture(r, white);
                GUI.color = Color.white;
                Shadowed(new Rect(r.x, r.y + 12, r.width, 24), a.Icon, labelCenter, locked ? Color.gray : Color.white);

                float cd = p.CooldownEnd[i] - Time.time;
                if (cd > 0)
                {
                    float frac = Mathf.Clamp01(cd / a.Cooldown);
                    GUI.color = new Color(0, 0, 0, 0.65f);
                    GUI.DrawTexture(new Rect(r.x, r.y + r.height * (1 - frac), r.width, r.height * frac), white);
                    GUI.color = Color.white;
                    Shadowed(new Rect(r.x, r.y + 14, r.width, 24), cd.ToString(cd < 1 ? "0.0" : "0"), labelCenter, Color.white);
                }
                else if (!locked && p.Mana < a.ManaCost)
                {
                    GUI.color = new Color(0.1f, 0.1f, 0.6f, 0.5f);
                    GUI.DrawTexture(r, white);
                    GUI.color = Color.white;
                }
                if (locked) Shadowed(new Rect(r.x, r.y + 32, r.width, 20), "Lv " + a.RequiredLevel, labelSmallCenter, new Color(1f, 0.5f, 0.5f));
                Shadowed(new Rect(r.x + 3, r.y + 1, r.width, 18), a.Key, labelSmall, new Color(1f, 1f, 0.8f));
                if (r.Contains(Event.current.mousePosition))
                    tooltip = "<b><color=#" + Item.Hex(a.Color) + ">" + a.Name + "</color></b>  [" + a.Key + "]\n" +
                              a.ManaCost + " mana   " + a.Cooldown + "s cooldown" + (locked ? "\n<color=#ff6666>Requires level " + a.RequiredLevel + "</color>" : "") +
                              "\n\n" + a.Description;
                if (ClickedIn(r) == 0) p.CastAbility(i, p.MouseGround);
            }

            // Potions
            string[] potions = { "Health Potion", "Mana Potion" };
            string[] keys = { "Q", "E" };
            for (int i = 0; i < 2; i++)
            {
                var r = new Rect(x0 + (AbilityDef.All.Length + i) * (slot + gap), y0, slot, slot);
                int n = p.Inventory.CountOf(potions[i]);
                GUI.color = i == 0 ? new Color(0.55f, 0.08f, 0.08f) : new Color(0.1f, 0.18f, 0.6f);
                GUI.DrawTexture(r, white);
                GUI.color = i == 0 ? new Color(1f, 0.2f, 0.2f) : new Color(0.3f, 0.5f, 1f);
                GUI.DrawTexture(new Rect(r.x + 15, r.y + 12, 24, 30), circle);
                GUI.color = Color.white;
                Shadowed(new Rect(r.x + 3, r.y + 1, r.width, 18), keys[i], labelSmall, new Color(1f, 1f, 0.8f));
                Shadowed(new Rect(r.x, r.y + r.height - 20, r.width - 4, 18), n.ToString(), labelSmallRight, n > 0 ? Color.white : Color.red);
                if (r.Contains(Event.current.mousePosition)) tooltip = potions[i] + "  [" + keys[i] + "]\nYou have " + n + ".";
                if (ClickedIn(r) == 0) p.UseItemByName(potions[i]);
            }

            // Gathering progress
            if (p.GatherNode != null)
            {
                var gr = new Rect((VW - 260) / 2, y0 - 40, 260, 18);
                Bar(gr, p.GatherProgress, SkillSet.SkillColor(p.GatherNode.Skill), SkillSet.Verb(p.GatherNode.Skill) + " " + p.GatherNode.DisplayName + "...");
            }
        }

        void DrawOrb(Rect r, float frac, Color color, string text)
        {
            frac = Mathf.Clamp01(frac);
            GUI.color = new Color(0.08f, 0.06f, 0.05f, 0.95f);
            GUI.DrawTexture(new Rect(r.x - 6, r.y - 6, r.width + 12, r.height + 12), circle);
            GUI.color = Factory.Shade(color, 0.25f);
            GUI.DrawTexture(r, circle);
            GUI.color = color;
            var fill = new Rect(r.x, r.y + r.height * (1 - frac), r.width, r.height * frac);
            GUI.DrawTextureWithTexCoords(fill, circle, new Rect(0, 0, 1, frac));
            GUI.color = new Color(1, 1, 1, 0.18f);
            GUI.DrawTexture(new Rect(r.x + r.width * 0.2f, r.y + r.height * 0.1f, r.width * 0.35f, r.height * 0.25f), circle);
            GUI.color = Color.white;
            Shadowed(new Rect(r.x, r.y + r.height / 2 - 12, r.width, 24), text, labelCenter, Color.white);
            Block(r);
        }

        void DrawMinimap(Player p)
        {
            const float size = 190, span = 60;
            var r = new Rect(VW - size - 14, 14, size, size);
            Panel(new Rect(r.x - 6, r.y - 6, r.width + 12, r.height + 72), null);
            Block(new Rect(r.x - 6, r.y - 6, r.width + 12, r.height + 72));

            var tex = GameManager.I.World.MapTexture;
            Vector3 pp = p.transform.position;
            float u0 = (pp.x - span / 2) / WorldGenerator.W, v0 = (pp.z - span / 2) / WorldGenerator.H;
            GUI.DrawTextureWithTexCoords(r, tex, new Rect(u0, v0, span / WorldGenerator.W, span / WorldGenerator.H));

            System.Func<Vector3, Vector2> toMap = w => new Vector2(
                r.x + (w.x - (pp.x - span / 2)) / span * r.width,
                r.y + (1f - (w.z - (pp.z - span / 2)) / span) * r.height);

            foreach (var it in Interactable.All)
                if (it is Npc npc) Dot(r, toMap(npc.Position), npc.Marker(p, out _) != null ? new Color(1f, 0.85f, 0.1f) : new Color(0.3f, 1f, 0.3f), 6);
            foreach (var e in Enemy.ById.Values)
                if (e != null && !e.IsDead) Dot(r, toMap(e.transform.position), e.Def.Boss ? new Color(1f, 0.5f, 0f) : new Color(0.9f, 0.15f, 0.1f), e.Def.Boss ? 8 : 4);
            foreach (var rp in RemotePlayer.ById.Values)
                if (rp != null) Dot(r, toMap(rp.transform.position), new Color(0.3f, 0.6f, 1f), 6);
            Dot(r, toMap(pp), Color.white, 7);

            var net = NetClient.I;
            Shadowed(new Rect(r.x, r.yMax + 4, r.width, 20), WorldGenerator.ZoneAt(pp), labelSmallCenter,
                WorldGenerator.InTown(pp) ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.85f, 0.6f));
            Shadowed(new Rect(r.x, r.yMax + 22, r.width, 20), "Gold: " + p.Gold + "    Online: " + net.PlayersOnline, labelSmallCenter, new Color(1f, 0.85f, 0.2f));
            Shadowed(new Rect(r.x, r.yMax + 40, r.width, 20), "[F1] Help  [M] Map", labelSmallCenter, new Color(0.7f, 0.7f, 0.7f));
        }

        void Dot(Rect clip, Vector2 pos, Color c, float size)
        {
            if (!clip.Contains(pos)) return;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(pos.x - size / 2 - 1, pos.y - size / 2 - 1, size + 2, size + 2), circle);
            GUI.color = c;
            GUI.DrawTexture(new Rect(pos.x - size / 2, pos.y - size / 2, size, size), circle);
            GUI.color = Color.white;
        }

        void DrawQuestTracker(Player p)
        {
            if (p.Quests.Active.Count == 0) return;
            float y = 300, x = VW - 300;
            foreach (var q in p.Quests.Active)
            {
                bool ready = q.IsReady(p);
                Shadowed(new Rect(x, y, 290, 20), q.Def.Title, label, new Color(1f, 0.82f, 0f));
                y += 20;
                string obj = q.Def.Type == QuestType.Kill ? q.Def.Target + " slain" : q.Def.Target;
                Shadowed(new Rect(x + 10, y, 280, 20), ready ? "Return to quest giver" : "- " + obj + ": " + q.Progress(p) + "/" + q.Def.Count,
                    labelSmall, ready ? new Color(0.5f, 1f, 0.5f) : Color.white);
                y += 24;
            }
        }

        // =====================================================================================
        // Chat
        // =====================================================================================

        void HandleChatKeys()
        {
            var e = Event.current;
            if (Player.I == null || e.type != EventType.KeyDown) return;
            bool enter = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter;
            if (!ChatOpen && enter)
            {
                ChatOpen = true;
                focusChat = true;
                chatText = "";
                e.Use();
            }
            else if (ChatOpen && enter)
            {
                NetClient.I.SendChat(chatText);
                chatText = "";
                ChatOpen = false;
                GUI.FocusControl(null);
                e.Use();
            }
            else if (ChatOpen && e.keyCode == KeyCode.Escape)
            {
                ChatOpen = false;
                GUI.FocusControl(null);
                e.Use();
            }
        }

        void DrawLog(bool inWorld)
        {
            float w = 440, lines = ChatOpen ? 14 : 8, lh = 18;
            float x = 12, y = VH - 40 - lines * lh - (inWorld ? 30 : 0);
            if (ChatOpen || (inWorld && MouseOverRect(new Rect(x, y, w, lines * lh))))
            {
                GUI.color = new Color(0, 0, 0, 0.45f);
                GUI.DrawTexture(new Rect(x - 4, y - 4, w + 8, lines * lh + 8), white);
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
                Shadowed(new Rect(x, y + (i - start) * lh, w, lh), l.Text, labelSmall, c);
            }

            if (inWorld && ChatOpen)
            {
                var r = new Rect(x, y + lines * lh + 6, w, 26);
                Block(r);
                GUI.SetNextControlName("chat");
                chatText = GUI.TextField(r, chatText, 200, field);
                if (focusChat) { GUI.FocusControl("chat"); focusChat = false; }
            }
        }

        bool MouseOverRect(Rect r) => r.Contains(Event.current.mousePosition);

        // =====================================================================================
        // Windows
        // =====================================================================================

        void DrawBags(Player p)
        {
            const int cols = 8, rows = 5;
            const float cell = 46, gap = 4;
            float w = cols * (cell + gap) + 20, h = rows * (cell + gap) + 80;
            var r = new Rect(VW - w - 14, VH - h - 110, w, h);
            Panel(r, "Bags");
            Block(r);
            if (CloseButton(r)) showBags = false;

            bool vendor = dialogNpc != null && dialogNpc.Role == NpcRole.Vendor;
            for (int i = 0; i < p.Inventory.Slots.Length; i++)
            {
                var cr = new Rect(r.x + 10 + (i % cols) * (cell + gap), r.y + 36 + (i / cols) * (cell + gap), cell, cell);
                var item = p.Inventory.Slots[i];
                DrawItemSlot(cr, item, p);
                if (item == null) continue;
                if (cr.Contains(Event.current.mousePosition))
                    tooltip = item.Tooltip(p, item.Kind == ItemKind.Equipment ? p.Inventory.GetEquipped(item.Slot) : null) +
                              "\n<color=#888888>" + (vendor ? "Right-click to sell" : "Left-click to use / equip.  Shift+Right-click to drop") + "</color>";
                int click = ClickedIn(cr);
                if (click == 0) p.UseItem(i);
                else if (click == 1)
                {
                    if (vendor) Sell(p, i);
                    else if (Event.current.shift) p.DropItem(i);
                }
            }
            GUI.Label(new Rect(r.x + 12, r.yMax - 34, w - 24, 24),
                "<color=#ffd700>" + p.Gold + " gold</color>     <color=#aaaaaa>" + p.Inventory.FreeSlots + " free slots</color>", rich);
        }

        void Sell(Player p, int index)
        {
            var item = p.Inventory.Slots[index];
            if (item == null) return;
            int value = item.Value * Mathf.Max(1, item.Count);
            p.Inventory.TakeAll(index);
            p.AddGold(value);
            Log("Sold " + item.Name + (item.Count > 1 ? " x" + item.Count : "") + " for " + value + " gold.", new Color(1f, 0.85f, 0.2f));
        }

        void DrawItemSlot(Rect r, Item item, Player p)
        {
            GUI.color = new Color(0.08f, 0.07f, 0.06f, 0.95f);
            GUI.DrawTexture(r, white);
            if (item != null)
            {
                Color border = item.Kind == ItemKind.Equipment ? Item.RarityColor(item.Rarity) : new Color(0.5f, 0.5f, 0.5f);
                GUI.color = border;
                GUI.DrawTexture(r, white);
                GUI.color = item.IconColor;
                GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), white);
                GUI.color = new Color(0, 0, 0, 0.25f);
                GUI.DrawTexture(new Rect(r.x + 2, r.y + r.height / 2, r.width - 4, r.height / 2 - 2), white);
                GUI.color = Color.white;
                Shadowed(new Rect(r.x, r.y + r.height / 2 - 12, r.width, 24), item.Icon, labelCenter, Color.white);
                if (item.Count > 1) Shadowed(new Rect(r.x, r.yMax - 18, r.width - 3, 18), item.Count.ToString(), labelSmallRight, Color.white);
                if (item.Kind == ItemKind.Equipment && item.RequiredLevel > p.Level)
                {
                    GUI.color = new Color(1f, 0f, 0f, 0.3f);
                    GUI.DrawTexture(r, white);
                }
            }
            GUI.color = Color.white;
        }

        static readonly EquipSlot[] dollLeft = { EquipSlot.Helm, EquipSlot.Chest, EquipSlot.Legs, EquipSlot.Boots };
        static readonly EquipSlot[] dollRight = { EquipSlot.Amulet, EquipSlot.Weapon, EquipSlot.Gloves, EquipSlot.Ring };

        void DrawCharacter(Player p)
        {
            var r = new Rect(14, 120, 420, 520);
            Panel(r, p.DisplayName + " - Level " + p.Level);
            Block(r);
            if (CloseButton(r)) showChar = false;

            const float cell = 50;
            for (int i = 0; i < 4; i++)
            {
                DrawEquipSlot(p, new Rect(r.x + 20, r.y + 44 + i * (cell + 10), cell, cell), dollLeft[i]);
                DrawEquipSlot(p, new Rect(r.x + 220, r.y + 44 + i * (cell + 10), cell, cell), dollRight[i]);
            }
            // little hero silhouette between the columns
            GUI.color = new Color(1, 1, 1, 0.08f);
            GUI.DrawTexture(new Rect(r.x + 100, r.y + 50, 90, 90), circle);
            GUI.DrawTexture(new Rect(r.x + 95, r.y + 140, 100, 120), white);
            GUI.color = Color.white;

            float y = r.y + 300;
            string[] names = { "Strength", "Dexterity", "Intelligence", "Vitality" };
            Stat[] stats = { Stat.Strength, Stat.Dexterity, Stat.Intelligence, Stat.Vitality };
            int[] values = { p.TotStr, p.TotDex, p.TotInt, p.TotVit };
            string[] hints = { "+2% melee damage each", "+0.15% crit, +0.25 armor each", "+2.5% spell damage, +3 mana each", "+6 life each" };
            for (int i = 0; i < 4; i++)
            {
                GUI.Label(new Rect(r.x + 20, y, 120, 22), names[i], label);
                GUI.Label(new Rect(r.x + 130, y, 50, 22), "<b>" + values[i] + "</b>", rich);
                GUI.Label(new Rect(r.x + 170, y + 2, 200, 22), hints[i], labelSmall);
                if (p.StatPoints > 0 && GUI.Button(new Rect(r.x + 370, y, 28, 22), "+", button)) p.SpendStatPoint(stats[i]);
                y += 26;
            }
            if (p.StatPoints > 0) GUI.Label(new Rect(r.x + 20, y, 380, 22), "<color=#ffd700>Unspent attribute points: " + p.StatPoints + "</color>", rich);
            y += 30;

            float dps = (p.MinDamage + p.MaxDamage) * 0.5f * p.MeleeMultiplier * p.AttackSpeed * (1f + p.CritChance / 100f);
            string left = "Damage: " + Mathf.RoundToInt(p.MinDamage * p.MeleeMultiplier) + "-" + Mathf.RoundToInt(p.MaxDamage * p.MeleeMultiplier) +
                          "\nAttacks/sec: " + p.AttackSpeed.ToString("0.00") + "\nDPS: " + dps.ToString("0.0") +
                          "\nSpell power: " + Mathf.RoundToInt(p.SpellMultiplier * 100) + "%";
            string right = "Armor: " + Mathf.RoundToInt(p.ArmorValue) + " (" + Mathf.RoundToInt(100f - 10000f / (100f + p.ArmorValue)) + "% reduction)" +
                           "\nCrit chance: " + p.CritChance.ToString("0.0") + "%" +
                           "\nLife regen: " + p.HealthRegen.ToString("0.0") + "/s   Mana: " + p.ManaRegen.ToString("0.0") + "/s" +
                           "\nMove speed: " + p.MoveSpeed.ToString("0.0");
            GUI.Label(new Rect(r.x + 20, y, 180, 90), left, labelSmall);
            GUI.Label(new Rect(r.x + 200, y, 210, 90), right, labelSmall);
        }

        void DrawEquipSlot(Player p, Rect r, EquipSlot slot)
        {
            var item = p.Inventory.GetEquipped(slot);
            DrawItemSlot(r, item, p);
            if (item == null) Shadowed(new Rect(r.x, r.y + 15, r.width, 20), Item.SlotName(slot), labelSmallCenter, new Color(0.5f, 0.5f, 0.5f));
            GUI.Label(new Rect(r.xMax + 8, r.y + 4, 140, 44),
                item != null ? "<color=#" + Item.Hex(item.NameColor) + ">" + item.Name + "</color>" : "<color=#666666>" + Item.SlotName(slot) + "</color>", richSmall);
            if (item != null && r.Contains(Event.current.mousePosition))
                tooltip = item.Tooltip(p) + "\n<color=#888888>Click to unequip</color>";
            if (item != null && ClickedIn(r) == 0) p.Unequip(slot);
        }

        void DrawSkills(Player p)
        {
            var r = new Rect(450, 120, 380, 470);
            Panel(r, "Skills");
            Block(r);
            if (CloseButton(r)) showSkills = false;
            float y = r.y + 40;
            GUI.Label(new Rect(r.x + 16, y, 340, 22), "<b>Professions</b>   (total level " + p.Skills.TotalLevel + ")", rich);
            y += 28;
            foreach (var s in SkillSet.All)
            {
                int lvl = p.Skills.Level(s), xp = p.Skills.Xp(s);
                int cur = SkillSet.XpForLevel(lvl), next = SkillSet.XpForLevel(lvl + 1);
                GUI.Label(new Rect(r.x + 16, y, 120, 22), s.ToString(), label);
                GUI.Label(new Rect(r.x + 130, y, 60, 22), "<b>" + lvl + "</b>/99", rich);
                Bar(new Rect(r.x + 190, y + 4, 170, 14), lvl >= 99 ? 1f : (float)(xp - cur) / Mathf.Max(1, next - cur), SkillSet.SkillColor(s),
                    xp + " xp");
                y += 26;
            }
            y += 12;
            GUI.Label(new Rect(r.x + 16, y, 340, 22), "<b>Combat abilities</b>", rich);
            y += 26;
            foreach (var a in AbilityDef.All)
            {
                bool locked = p.Level < a.RequiredLevel;
                GUI.Label(new Rect(r.x + 16, y, 350, 40),
                    "<color=#" + Item.Hex(locked ? Color.gray : a.Color) + "><b>" + a.Name + "</b></color> [" + a.Key + "]  " +
                    (locked ? "<color=#ff6666>Level " + a.RequiredLevel + "</color>" : "") + "\n<size=11>" + a.Description + "</size>", richSmall);
                y += 40;
            }
        }

        void DrawQuestLog(Player p)
        {
            var r = new Rect(450, 120, 420, 440);
            Panel(r, "Quest Log");
            Block(r);
            if (CloseButton(r)) showQuests = false;
            float y = r.y + 40;
            if (p.Quests.Active.Count == 0)
                GUI.Label(new Rect(r.x + 16, y, 380, 60), "You have no active quests. Look for villagers with a yellow ! above their heads.", labelSmall);
            foreach (var q in p.Quests.Active)
            {
                bool ready = q.IsReady(p);
                GUI.Label(new Rect(r.x + 16, y, 390, 22), "<b><color=#ffd100>" + q.Def.Title + "</color></b>" + (ready ? "  <color=#66ff66>(Complete)</color>" : ""), rich);
                y += 22;
                GUI.Label(new Rect(r.x + 16, y, 390, 40), q.Def.Objective + "  (" + q.Progress(p) + "/" + q.Def.Count + ")", labelSmall);
                y += 40;
            }
            GUI.Label(new Rect(r.x + 16, r.yMax - 30, 390, 22), "Completed quests: " + p.Quests.Completed.Count, labelSmall);
        }

        void DrawWorldMap(Player p)
        {
            float size = Mathf.Min(VW, VH) - 120;
            var r = new Rect((VW - size) / 2, (VH - size) / 2, size, size);
            Panel(new Rect(r.x - 10, r.y - 40, r.width + 20, r.height + 50), "World Map  [M]");
            Block(new Rect(0, 0, VW, VH));
            GUI.DrawTexture(r, GameManager.I.World.MapTexture);
            System.Func<Vector3, Vector2> toMap = w => new Vector2(r.x + w.x / WorldGenerator.W * r.width, r.y + (1f - w.z / WorldGenerator.H) * r.height);

            string[] zones = { "Whisperwood", "Goblin Encampment", "Forsaken Graveyard", "Ironvein Quarry", "Hollowmere", "Crypt of the Lich" };
            Vector3[] centers = { new Vector3(80, 0, 125), new Vector3(128, 0, 80), new Vector3(80, 0, 40), new Vector3(32, 0, 80), new Vector3(80, 0, 80), new Vector3(80, 0, 15) };
            for (int i = 0; i < zones.Length; i++)
            {
                var c = toMap(centers[i]);
                Shadowed(new Rect(c.x - 100, c.y - 10, 200, 20), zones[i], labelCenter, new Color(1f, 0.9f, 0.7f));
            }
            foreach (var it in Interactable.All)
                if (it is Npc npc && npc.Marker(p, out _) != null) Dot(r, toMap(npc.Position), new Color(1f, 0.85f, 0.1f), 8);
            foreach (var rp in RemotePlayer.ById.Values) if (rp != null) Dot(r, toMap(rp.transform.position), new Color(0.3f, 0.6f, 1f), 8);
            Dot(r, toMap(p.transform.position), Color.white, 10);
            if (ClickedIn(new Rect(0, 0, VW, VH)) >= 0) showMap = false;
        }

        void DrawHelp()
        {
            var r = new Rect((VW - 520) / 2, 90, 520, 480);
            Panel(r, "How to play");
            Block(r);
            if (CloseButton(r)) showHelp = false;
            GUI.Label(new Rect(r.x + 20, r.y + 40, 480, 430),
                "<b>Movement & combat</b>\n" +
                "Left-click ground to move (hold to keep walking)\n" +
                "Left-click a monster to attack it   Shift+click: attack in place\n" +
                "Right-click: Fireball   1-5: abilities   Q / E: health / mana potion\n" +
                "Mouse wheel: zoom\n\n" +
                "<b>Windows</b>\n" +
                "I or B: bags   C: character   K: skills   L: quest log   M: world map\n" +
                "Enter: chat with other players   Esc: close windows\n\n" +
                "<b>World</b>\n" +
                "Talk to villagers with <color=#ffd100>!</color> for quests and turn them in at <color=#ffd100>?</color>\n" +
                "Click trees, rocks and fishing spots to gather (Woodcutting, Mining, Fishing)\n" +
                "Use the anvil (Smithing) and campfire (Cooking) in Hollowmere\n" +
                "Sell loot to Merchant Lysa (right-click items while trading)\n\n" +
                "<b>Zones</b>   North: Whisperwood (Lv 1-7)   East: Goblin Encampment (3-10)\n" +
                "West: Ironvein Quarry (3-15)   South: Forsaken Graveyard (6-11)\n" +
                "Far south: Crypt of the Lich (11-16, boss)", rich);
        }

        void DrawDeath(Player p)
        {
            GUI.color = new Color(0.3f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(0, 0, VW, VH), white);
            GUI.color = Color.white;
            Block(new Rect(0, 0, VW, VH));
            Shadowed(new Rect(0, VH * 0.3f, VW, 70), "You have died.", title, new Color(0.9f, 0.2f, 0.2f));
            if (GUI.Button(new Rect((VW - 260) / 2, VH * 0.3f + 90, 260, 44), "Release Spirit (lose 10% gold)", button))
                p.Respawn();
        }

        // =====================================================================================
        // NPC dialog / vendor / healer / crafting
        // =====================================================================================

        void DrawDialog(Player p)
        {
            var npc = dialogNpc;
            var r = new Rect(14, 120, 420, 430);
            Panel(r, npc.DisplayName);
            Block(r);
            if (CloseButton(r)) { dialogNpc = null; return; }

            float y = r.y + 40;
            GUI.Label(new Rect(r.x + 16, y, 390, 50), "<i>\"" + npc.Greeting + "\"</i>", richSmall);
            y += 54;

            switch (npc.Role)
            {
                case NpcRole.QuestGiver:
                {
                    var q = npc.CurrentQuest(p);
                    if (q == null)
                    {
                        GUI.Label(new Rect(r.x + 16, y, 390, 40), "I have nothing more for you. Thank you, hero.", labelSmall);
                        break;
                    }
                    var state = p.Quests.Get(q.Id);
                    GUI.Label(new Rect(r.x + 16, y, 390, 22), "<b><color=#ffd100>" + q.Title + "</color></b>", rich);
                    y += 26;
                    string body = state != null && state.IsReady(p) ? q.CompletionText : q.Description;
                    GUI.Label(new Rect(r.x + 16, y, 390, 110), body, labelSmall);
                    y += 110;
                    GUI.Label(new Rect(r.x + 16, y, 390, 22), "<b>Objective:</b> " + q.Objective, richSmall);
                    y += 40;
                    string reward = "<b>Rewards:</b> <color=#c080ff>" + q.RewardXp + " xp</color>, <color=#ffd700>" + q.RewardGold + " gold</color>";
                    if (q.RewardItemLevel > 0) reward += ", <color=#" + Item.Hex(Item.RarityColor(q.RewardRarity)) + ">a " + q.RewardRarity + " item</color>";
                    GUI.Label(new Rect(r.x + 16, y, 390, 22), reward, richSmall);
                    y += 34;

                    var br = new Rect(r.x + 16, y, 200, 36);
                    if (state == null)
                    {
                        if (p.Level < q.MinLevel)
                            GUI.Label(new Rect(r.x + 16, y, 390, 36), "<color=#ff6666>Come back when you are level " + q.MinLevel + ".</color>", rich);
                        else if (GUI.Button(br, "Accept Quest", button)) p.Quests.Accept(q);
                    }
                    else if (state.IsReady(p))
                    {
                        if (GUI.Button(br, "Complete Quest", button)) p.Quests.TurnIn(state, p);
                    }
                    else
                        GUI.Label(new Rect(r.x + 16, y, 390, 36), "Progress: " + state.Progress(p) + "/" + q.Count, label);
                    break;
                }

                case NpcRole.Vendor:
                {
                    GUI.Label(new Rect(r.x + 16, y, 390, 22), "<b>For sale</b>", rich);
                    y += 28;
                    y = VendorRow(p, r, y, "Health Potion", 25, ItemDatabase.HealthPotion);
                    y = VendorRow(p, r, y, "Mana Potion", 25, ItemDatabase.ManaPotion);
                    y += 10;
                    GUI.Label(new Rect(r.x + 16, y, 390, 60),
                        "Right-click items in your bags to sell them.\nYou have <color=#ffd700>" + p.Gold + " gold</color>.", richSmall);
                    y += 50;
                    if (GUI.Button(new Rect(r.x + 16, y, 250, 32), "Sell all Common items & materials", button))
                    {
                        for (int i = 0; i < p.Inventory.Slots.Length; i++)
                        {
                            var it = p.Inventory.Slots[i];
                            if (it == null || it.Kind == ItemKind.Consumable) continue;
                            if (it.Kind == ItemKind.Equipment && it.Rarity != Rarity.Common) continue;
                            Sell(p, i);
                        }
                    }
                    break;
                }

                case NpcRole.Healer:
                    if (GUI.Button(new Rect(r.x + 16, y, 220, 36), "Heal me", button))
                    {
                        p.Heal(p.MaxHealth);
                        p.RestoreMana(p.MaxMana);
                        FxPulse.Ring(p.transform.position, new Color(1f, 1f, 0.6f), 2.5f, 0.6f);
                        Log("Sister Mae: \"Go with the Light.\"", new Color(0.4f, 1f, 0.4f));
                    }
                    break;
            }
        }

        float VendorRow(Player p, Rect r, float y, string name, int price, System.Func<Item> make)
        {
            GUI.Label(new Rect(r.x + 16, y + 4, 160, 22), name, label);
            GUI.Label(new Rect(r.x + 170, y + 4, 80, 22), "<color=#ffd700>" + price + "g</color>", rich);
            foreach (int n in new[] { 1, 5 })
            {
                if (!GUI.Button(new Rect(r.x + (n == 1 ? 240 : 310), y, 62, 28), "Buy " + n, button)) continue;
                if (p.Gold < price * n) { Log("You don't have enough gold.", new Color(1f, 0.4f, 0.4f)); continue; }
                var item = make();
                item.Count = n;
                if (!p.Inventory.Add(item)) { Log("Your bags are full.", new Color(1f, 0.4f, 0.4f)); continue; }
                p.Gold -= price * n;
            }
            return y + 34;
        }

        void DrawCrafting(Player p)
        {
            var s = craftStation;
            var r = new Rect(14, 120, 420, 120 + s.Recipes.Length * 70);
            Panel(r, s.DisplayName + " - " + s.Skill + " " + p.Skills.Level(s.Skill));
            Block(r);
            if (CloseButton(r)) { craftStation = null; return; }
            float y = r.y + 44;
            foreach (var rec in s.Recipes)
            {
                bool canLevel = p.Skills.Level(s.Skill) >= rec.LevelRequired;
                int have = p.Inventory.CountOf(rec.Input);
                GUI.Label(new Rect(r.x + 16, y, 250, 22), "<b><color=#" + (canLevel ? "ffffff" : "888888") + ">" + rec.Name + "</color></b>", rich);
                GUI.Label(new Rect(r.x + 16, y + 22, 260, 40),
                    "Needs " + rec.InputCount + " " + rec.Input + " (have " + have + ")\n" +
                    (canLevel ? "+" + rec.Xp + " xp" : "<color=#ff6666>Requires level " + rec.LevelRequired + "</color>"), richSmall);
                GUI.enabled = canLevel && have >= rec.InputCount;
                if (GUI.Button(new Rect(r.x + 280, y + 4, 56, 30), "Make", button)) rec.Craft(p);
                if (GUI.Button(new Rect(r.x + 342, y + 4, 60, 30), "All", button))
                    for (int i = 0; i < 50 && p.Inventory.CountOf(rec.Input) >= rec.InputCount; i++)
                        if (!rec.Craft(p)) break;
                GUI.enabled = true;
                y += 70;
            }
            GUI.Label(new Rect(r.x + 16, r.yMax - 40, 390, 30),
                s.Skill == SkillType.Smithing ? "Higher Smithing levels forge better gear." : "Higher Cooking levels burn less food.", labelSmall);
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
            Shadowed(new Rect(0, VH * 0.18f, VW, 50), bannerText, bannerStyle, c);
        }

        void DrawTooltip()
        {
            if (string.IsNullOrEmpty(tooltip)) return;
            var content = new GUIContent(tooltip);
            float w = 300;
            float h = rich.CalcHeight(content, w - 20) + 18;
            var m = Event.current.mousePosition;
            var r = new Rect(m.x + 18, m.y + 18, w, h);
            if (r.xMax > VW) r.x = m.x - w - 10;
            if (r.yMax > VH) r.y = Mathf.Max(0, VH - h);
            GUI.color = new Color(0.04f, 0.04f, 0.06f, 0.96f);
            GUI.DrawTexture(r, white);
            GUI.color = new Color(0.5f, 0.45f, 0.3f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 10, r.y + 8, w - 20, h - 12), content, rich);
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

        bool CloseButton(Rect r) => GUI.Button(new Rect(r.xMax - 30, r.y + 6, 24, 22), "x", button);

        void Panel(Rect r, string heading)
        {
            GUI.color = new Color(0.07f, 0.06f, 0.05f, 0.92f);
            GUI.DrawTexture(r, white);
            GUI.color = new Color(0.45f, 0.36f, 0.22f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2), white);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2, r.width, 2), white);
            GUI.DrawTexture(new Rect(r.x, r.y, 2, r.height), white);
            GUI.DrawTexture(new Rect(r.xMax - 2, r.y, 2, r.height), white);
            GUI.color = Color.white;
            if (heading != null) Shadowed(new Rect(r.x + 14, r.y + 8, r.width - 50, 24), heading, label, new Color(1f, 0.82f, 0.4f));
        }

        void Bar(Rect r, float frac, Color c, string text = null)
        {
            GUI.color = new Color(0, 0, 0, 0.75f);
            GUI.DrawTexture(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), white);
            GUI.color = Factory.Shade(c, 0.3f);
            GUI.DrawTexture(r, white);
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), white);
            GUI.color = new Color(1, 1, 1, 0.15f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height * 0.4f), white);
            GUI.color = Color.white;
            if (text != null) Shadowed(new Rect(r.x, r.y + r.height / 2 - 9, r.width, 18), text, labelSmallCenter, Color.white);
        }

        void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, color.a * 0.85f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }

        static Color LevelColor(int level, int playerLevel)
        {
            int d = level - playerLevel;
            if (d >= 5) return new Color(1f, 0.15f, 0.15f);
            if (d >= 3) return new Color(1f, 0.5f, 0.15f);
            if (d >= -2) return new Color(1f, 1f, 0.2f);
            if (d >= -6) return new Color(0.3f, 1f, 0.3f);
            return new Color(0.6f, 0.6f, 0.6f);
        }

        GUIStyle labelSmallCenter, labelSmallRight, richSmall;

        void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            label.normal.textColor = Color.white;
            labelCenter = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            labelSmall = new GUIStyle(label) { fontSize = 13, wordWrap = true };
            labelSmallCenter = new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            labelSmallRight = new GUIStyle(labelSmall) { alignment = TextAnchor.LowerRight, wordWrap = false, fontStyle = FontStyle.Bold };
            title = new GUIStyle(label) { fontSize = 52, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            bannerStyle = new GUIStyle(label) { fontSize = 28, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            floatStyle = new GUIStyle(label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            rich = new GUIStyle(label) { fontSize = 14, wordWrap = true, richText = true };
            richSmall = new GUIStyle(rich) { fontSize = 12 };
            slotText = new GUIStyle(labelCenter);
            box = new GUIStyle(GUI.skin.box);
            button = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
            field = new GUIStyle(GUI.skin.textField) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
        }

        static Texture2D MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d));
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
