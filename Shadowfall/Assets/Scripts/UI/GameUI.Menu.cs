using UnityEngine;

namespace Shadowfall
{
    /// <summary>The Esc game menu: resume, settings, help and logging out.</summary>
    public partial class GameUI
    {
        enum MenuPage { None, Main, Settings, Graphics, ConfirmLogout, Account }
        MenuPage menu = MenuPage.None;

        bool logoutToSelect;

        /// <summary>Seconds after taking damage during which you can't log out (no escaping a fight by logging off).</summary>
        const float CombatLogoutLock = 6f;

        void DrawGameMenu(Player p)
        {
            // Dim the game behind the menu.
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, VW, VH), UISkin.White);
            GUI.color = Color.white;
            Block(new Rect(0, 0, VW, VH));

            switch (menu)
            {
                case MenuPage.Main: DrawMenuMain(p); break;
                case MenuPage.Settings: DrawMenuSettings(); break;
                case MenuPage.Graphics: DrawMenuGraphics(); break;
                case MenuPage.ConfirmLogout: DrawMenuLogout(p); break;
                case MenuPage.Account: DrawMenuAccount(); break;
            }
        }

        void DrawMenuMain(Player p)
        {
            const float w = 340, bh = 50, gap = 12;
            int unread = Changelog.UnreadCount(p.NewsSeen);
            string news = unread > 0 ? "What's New  (" + unread + ")" : "What's New";
            var items = new System.Collections.Generic.List<string> { "Resume", "Settings", "How to Play", news, "Account" };
            if (AdminTools.IsAdmin) items.Add("Admin  (F10)");
            items.Add("Character Select");
            items.Add("Log Out");
            float h = 80 + items.Count * (bh + gap) + 30;
            var r = new Rect((VW - w) / 2, (VH - h) / 2, w, h);
            if (UISkin.Window(r, "Game Menu")) { menu = MenuPage.None; return; }
            float y = r.y + 64;
            var big = UISkin.V(UISkin.Button, fontSize: 19);
            for (int i = 0; i < items.Count; i++)
            {
                if (UISkin.Btn(new Rect(r.x + 34, y, w - 68, bh), items[i], big))
                {
                    switch (i == 3 ? "news" : items[i])
                    {
                        case "Resume": menu = MenuPage.None; break;
                        case "Settings": menu = MenuPage.Settings; break;
                        case "How to Play": menu = MenuPage.None; showHelp = true; break;
                        case "news": menu = MenuPage.None; OpenNews(p); break;
                        case "Account": menu = MenuPage.Account; NetClient.I.ClearMessages(); break;
                        case "Character Select": menu = MenuPage.ConfirmLogout; logoutToSelect = true; break;
                        case "Log Out": menu = MenuPage.ConfirmLogout; logoutToSelect = false; break;
                        default: menu = MenuPage.None; showAdmin = true; break;
                    }
                }
                y += bh + gap;
            }
            UISkin.Shadowed(new Rect(r.x, r.yMax - 34, r.width, 20), p.DisplayName + "  -  level " + p.Level + " " + p.Look,
                UISkin.SmallCenter, UISkin.Muted);
        }

        void DrawMenuLogout(Player p)
        {
            const float w = 420, h = 210;
            var r = new Rect((VW - w) / 2, (VH - h) / 2, w, h);
            if (UISkin.Window(r, logoutToSelect ? "Character Select" : "Log Out")) { menu = MenuPage.Main; return; }
            float sinceHit = Time.time - p.LastDamagedTime;
            bool inCombat = sinceHit < CombatLogoutLock && !p.IsDead;
            string text = inCombat
                ? "You can't log out in the middle of a fight.\n<color=#ff8866>Wait " + Mathf.CeilToInt(CombatLogoutLock - sinceHit) + "s without taking damage.</color>"
                : logoutToSelect ? "Your character is saved on the server.\nLeave the world and pick another hero?"
                : "Your character is saved on the server.\nLog out and return to the login screen?";
            GUI.Label(new Rect(r.x + 24, r.y + 56, w - 48, 64), text, UISkin.V(UISkin.Rich, alignment: TextAnchor.MiddleCenter, wordWrap: true));
            GUI.enabled = !inCombat;
            if (UISkin.Btn(new Rect(r.x + 36, r.yMax - 70, 160, 46), logoutToSelect ? "Leave" : "Log Out", UISkin.Button))
            {
                menu = MenuPage.None;
                CloseAllWindows();
                if (logoutToSelect) NetClient.I.BackToCharacterSelect();
                else NetClient.I.LogOut();
            }
            GUI.enabled = true;
            if (UISkin.Btn(new Rect(r.xMax - 196, r.yMax - 70, 160, 46), "Cancel", UISkin.Button)) menu = MenuPage.Main;
        }

        void CloseAllWindows()
        {
            showBags = showChar = showSkills = showQuests = showMap = showHelp = showTalents = showStash = showAdmin = showNews = showEmotes = showAchievements = showGuild = false;
            dialogNpc = null;
            craftStation = null;
            forgeOpen = false;
            menuPlayer = null;
            socketGem = -1;
            ChatOpen = false;
        }

        void DrawMenuSettings()
        {
            const float w = 560, h = 550;
            var r = new Rect((VW - w) / 2, (VH - h) / 2, w, h);
            if (UISkin.Window(r, "Settings")) { menu = MenuPage.Main; return; }
            float x = r.x + 34, y = r.y + 66;
            var label = UISkin.V(UISkin.Label, fontSize: 17);

            // Graphics preset, and the page with every option
            UISkin.Shadowed(new Rect(x, y + 6, 120, 26), "Graphics", label, UISkin.Cream);
            bool custom = GameSettings.IsCustom;
            for (int i = 0; i < GameSettings.QualityNames.Length; i++)
            {
                var br = new Rect(x + 130 + i * 92, y, 86, 38);
                bool on = !custom && GameSettings.Quality == i;
                if (UISkin.Btn(br, on ? "> " + GameSettings.QualityNames[i] + " <" : GameSettings.QualityNames[i], UISkin.Button) && !on)
                    GameSettings.Quality = i;
            }
            if (UISkin.Btn(new Rect(x + 130 + 3 * 92, y, 110, 38), custom ? "> Custom <" : "More...", UISkin.Button)) menu = MenuPage.Graphics;
            if (new Rect(x + 130, y, 386, 38).Contains(Event.current.mousePosition))
                tooltip = "<b>Low</b>: lower resolution, no shadows, grass or small details, few lights, fewer particles.\n" +
                          "<b>Medium</b>: hard shadows, near grass, some lights.\n<b>High</b>: soft far shadows, far grass, many lights.\n" +
                          "<b>More...</b>: every option on its own (resolution, frame rate, shadows, lights, grass...).";
            y += 58;

            // Gore
            UISkin.Shadowed(new Rect(x, y + 6, 120, 26), "Gore", label, UISkin.Cream);
            for (int i = 0; i < GameSettings.GoreNames.Length; i++)
            {
                var br = new Rect(x + 130 + i * 118, y, 110, 38);
                bool on = GameSettings.Gore == i;
                if (UISkin.Btn(br, on ? "> " + GameSettings.GoreNames[i] + " <" : GameSettings.GoreNames[i], UISkin.Button) && !on)
                    GameSettings.Gore = i;
            }
            if (new Rect(x + 130, y, 346, 38).Contains(Event.current.mousePosition))
                tooltip = "<b>Off</b>: no blood.\n<b>Normal</b>: blood sprays and stains the ground for a few minutes.\n<b>Extra</b>: more of it, chunks on every kill, stains last twice as long.";
            y += 58;

            // Volume
            UISkin.Shadowed(new Rect(x, y, 120, 26), "Volume", label, UISkin.Cream);
            float v = GUI.HorizontalSlider(new Rect(x + 130, y + 8, 300, 20), Sfx.Volume, 0f, 1f);
            if (Mathf.Abs(v - Sfx.Volume) > 0.001f) Sfx.Volume = v;
            UISkin.Shadowed(new Rect(x + 444, y, 60, 26), Mathf.RoundToInt(Sfx.Volume * 100) + "%", label, UISkin.Gold);
            y += 50;

            // Music (on top of the master volume)
            UISkin.Shadowed(new Rect(x, y, 120, 26), "Music", label, UISkin.Cream);
            float mv = GUI.HorizontalSlider(new Rect(x + 130, y + 8, 300, 20), Music.Volume, 0f, 1f);
            if (Mathf.Abs(mv - Music.Volume) > 0.001f) Music.Volume = mv;
            UISkin.Shadowed(new Rect(x + 444, y, 60, 26), Music.Volume < 0.005f ? "Off" : Mathf.RoundToInt(Music.Volume * 100) + "%", label, UISkin.Gold);
            if (new Rect(x, y, w - 68, 30).Contains(Event.current.mousePosition))
                tooltip = "Music for Hollowmere, the wilds, the graveyard and the dungeons; fights with elites, crowds and bosses have their own.";
            y += 50;

            // Interface size: applied when the mouse button is released, so the slider doesn't jump under the cursor.
            UISkin.Shadowed(new Rect(x, y, 120, 26), "UI scale", label, UISkin.Cream);
            uiScaleDraft = uiScaleDraft < 0f ? GameSettings.UiScale : uiScaleDraft;
            uiScaleDraft = GUI.HorizontalSlider(new Rect(x + 130, y + 8, 300, 20), uiScaleDraft, 0.7f, 1.5f);
            UISkin.Shadowed(new Rect(x + 444, y, 70, 26), Mathf.RoundToInt(uiScaleDraft * 100) + "%", label, UISkin.Gold);
            if (!GameInput.LeftHeld && Mathf.Abs(uiScaleDraft - GameSettings.UiScale) > 0.001f) // (the slider uses up the MouseUp event)
                GameSettings.UiScale = Mathf.Round(uiScaleDraft * 20f) / 20f;
            y += 50;

            // Toggles
            if (Toggle(new Rect(x, y, w - 68, 30), GameSettings.ShowFps, "Show frames per second")) GameSettings.ShowFps = !GameSettings.ShowFps;
            y += 40;
            if (Toggle(new Rect(x, y, w - 68, 30), GameSettings.ShowCommonLoot, "Label common (white) items on the ground  -  otherwise hold Alt"))
                GameSettings.ShowCommonLoot = !GameSettings.ShowCommonLoot;
            y += 52;

            if (UISkin.Btn(new Rect(r.x + (w - 200) / 2, r.yMax - 70, 200, 46), "Back", UISkin.Button)) menu = MenuPage.Main;
        }

        float uiScaleDraft = -1f;

        /// <summary>Settings > Graphics: every option with its choices; hovering a row explains it.</summary>
        void DrawMenuGraphics()
        {
            var options = GameSettings.Options;
            const float w = 660;
            float h = 190 + options.Length * 50;
            var r = new Rect((VW - w) / 2, (VH - h) / 2, w, h);
            if (UISkin.Window(r, "Graphics")) { menu = MenuPage.Settings; return; }
            Block(r);
            float x = r.x + 30, y = r.y + 62;
            var label = UISkin.V(UISkin.Label, fontSize: 16);
            string preset = GameSettings.IsCustom ? "Custom" : GameSettings.QualityNames[GameSettings.Quality];
            GUI.Label(new Rect(x, y, w - 60, 22), "<color=#b8a88c>Preset:</color> <color=#f0c45a>" + preset + "</color><color=#b8a88c>  -  pick one in Settings, or change anything here.</color>",
                UISkin.V(UISkin.RichSmall, fontSize: 14));
            y += 32;
            foreach (var o in options)
            {
                UISkin.Shadowed(new Rect(x, y + 7, 160, 24), o.Name, label, UISkin.Cream);
                float bw = Mathf.Min(96f, (w - 60 - 170) / o.Choices.Length - 6f);
                for (int i = 0; i < o.Choices.Length; i++)
                {
                    bool on = o.Value == i;
                    if (UISkin.Btn(new Rect(x + 170 + i * (bw + 6), y, bw, 38), on ? "> " + o.Choices[i] + " <" : o.Choices[i], UISkin.V(UISkin.Button, fontSize: 14)) && !on)
                        o.Set(i);
                }
                if (new Rect(x, y, w - 60, 40).Contains(Event.current.mousePosition)) tooltip = "<b>" + o.Name + "</b>\n" + o.Help;
                y += 50;
            }
            GUI.Label(new Rect(x, y + 2, w - 60, 40),
                "<color=#b8a88c>Slow on a laptop? Lower <b>Resolution</b> first, then Shadows and Lights. Frame rate 30 saves battery. " +
                "If a resolution change doesn't show, reload the page.</color>", UISkin.V(UISkin.RichSmall, fontSize: 13, wordWrap: true));
            if (UISkin.Btn(new Rect(r.x + (w - 200) / 2, r.yMax - 64, 200, 44), "Back", UISkin.Button)) menu = MenuPage.Settings;
        }

        // ---- dungeon difficulty picker (shown when clicking a dungeon entrance)

        int chooseDungeon = -1;

        public void ChooseDifficulty(int dungeonIndex)
        {
            chooseDungeon = dungeonIndex;
            Sfx.Play2D("ui_open", 0.4f);
        }

        void DrawDifficultyPicker(Player p)
        {
            var def = DungeonDef.Get(chooseDungeon);
            if (Factory.FlatDistance(p.transform.position, def.Entrance) > 7f || p.IsDead) { chooseDungeon = -1; return; }
            const float w = 520;
            float h = 150 + Difficulties.Names.Length * 66 + 70;
            var r = new Rect((VW - w) / 2, (VH - h) / 2, w, h);
            if (UISkin.Window(r, def.Name)) { chooseDungeon = -1; return; }
            Block(r);
            float x = r.x + 26, y = r.y + 56;
            GUI.Label(new Rect(x, y, w - 52, 44), def.Blurb + "  <color=#c8a060>Recommended level " + def.MinLevel + "+, " + def.Depths + " levels.</color>",
                UISkin.V(UISkin.Rich, wordWrap: true));
            y += 50;
            string party = NetClient.I.InParty ? "If your party is already inside, you join them at their difficulty." : "Choose a difficulty:";
            UISkin.Shadowed(new Rect(x, y, w - 52, 22), party, UISkin.Small, UISkin.Muted);
            y += 30;
            for (int i = 0; i < Difficulties.Names.Length; i++)
            {
                var row = new Rect(x, y, w - 52, 58);
                if (UISkin.Btn(new Rect(row.x, row.y, 150, 46), Difficulties.Names[i], UISkin.V(UISkin.Button, fontSize: 17)))
                {
                    NetClient.I.EnterDungeon(chooseDungeon, i);
                    chooseDungeon = -1;
                    return;
                }
                GUI.Label(new Rect(row.x + 162, row.y, row.width - 162, 50),
                    "<color=#" + Item.Hex(Difficulties.Colors[i]) + ">" + Difficulties.Blurbs[i] + "</color>", UISkin.V(UISkin.RichSmall, wordWrap: true));
                y += 66;
            }
            if (UISkin.Btn(new Rect(r.center.x - 80, r.yMax - 62, 160, 42), "Cancel", UISkin.Button)) chooseDungeon = -1;
        }

        // ---- waystones

        Waystone waystoneOpen;

        public void OpenWaystone(Waystone w) => waystoneOpen = w;

        /// <summary>Every town's waystone: attuned ones take you there, the rest say where to find them.</summary>
        void DrawWaystone(Player p)
        {
            var here = waystoneOpen;
            if (here == null || p.IsDead || Dungeon.Active || Factory.FlatDistance(p.transform.position, here.Position) > 5f) { waystoneOpen = null; return; }
            const float w = 460;
            float h = 128 + Waystone.Stones.Count * 58;
            var r = new Rect((VW - w) / 2, (VH - h) / 2, w, h);
            if (UISkin.Window(r, "Waystone of " + here.Town.Name)) { waystoneOpen = null; return; }
            Block(r);
            float x = r.x + 26, y = r.y + 56;
            UISkin.Shadowed(new Rect(x, y, w - 52, 22), "Travel to any waystone you have attuned to.", UISkin.Small, UISkin.Muted);
            y += 30;
            foreach (var s in Waystone.Stones)
            {
                bool known = Waystone.Known(s.Town), current = s == here;
                GUI.enabled = known && !current;
                if (UISkin.Btn(new Rect(x, y, 200, 46), s.Town.Name, UISkin.V(UISkin.Button, fontSize: 16)))
                {
                    waystoneOpen = null;
                    GUI.enabled = true;
                    Waystone.Travel(p, s);
                    return;
                }
                GUI.enabled = true;
                string note = current ? "You are here." : known ? Mathf.RoundToInt(Factory.FlatDistance(p.transform.position, s.Position)) + " paces away"
                    : "Not attuned yet: walk up to it first. " + Compass(s.Position - p.transform.position) + ".";
                GUI.Label(new Rect(x + 212, y + 2, w - 52 - 212, 44), note, UISkin.V(UISkin.RichSmall, wordWrap: true));
                y += 58;
            }
        }

        static string Compass(Vector3 d)
        {
            float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            return "Far to the " + names[Mathf.RoundToInt(((a % 360f) + 360f) % 360f / 45f) % 8];
        }

        /// <summary>A checkbox drawn from the slot and gold textures; returns true when clicked.</summary>
        bool Toggle(Rect r, bool on, string text)
        {
            var box = new Rect(r.x, r.y + 2, 26, 26);
            UISkin.Box(box, UISkin.Slot);
            if (on)
            {
                GUI.color = UISkin.Gold;
                GUI.DrawTexture(new Rect(box.x + 7, box.y + 7, 12, 12), UISkin.White);
                GUI.color = Color.white;
            }
            bool hover = r.Contains(Event.current.mousePosition);
            UISkin.Shadowed(new Rect(r.x + 38, r.y + 2, r.width - 38, 26), text, UISkin.V(UISkin.Label, fontSize: 16), hover ? Color.white : UISkin.Cream);
            if (ClickedIn(r) == 0) { Sfx.Play2D("ui_click", 0.35f); return true; }
            return false;
        }
    }
}
