using UnityEngine;

namespace Shadowfall
{
    /// <summary>The Esc game menu: resume, settings, help and logging out.</summary>
    public partial class GameUI
    {
        enum MenuPage { None, Main, Settings, ConfirmLogout }
        MenuPage menu = MenuPage.None;

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
                case MenuPage.ConfirmLogout: DrawMenuLogout(p); break;
            }
        }

        void DrawMenuMain(Player p)
        {
            const float w = 340, bh = 50, gap = 12;
            string[] items = { "Resume", "Settings", "How to Play", "Log Out" };
            float h = 80 + items.Length * (bh + gap) + 30;
            var r = new Rect((VW - w) / 2, (VH - h) / 2, w, h);
            if (UISkin.Window(r, "Game Menu")) { menu = MenuPage.None; return; }
            float y = r.y + 64;
            var big = UISkin.V(UISkin.Button, fontSize: 19);
            for (int i = 0; i < items.Length; i++)
            {
                if (UISkin.Btn(new Rect(r.x + 34, y, w - 68, bh), items[i], big))
                {
                    switch (i)
                    {
                        case 0: menu = MenuPage.None; break;
                        case 1: menu = MenuPage.Settings; break;
                        case 2: menu = MenuPage.None; showHelp = true; break;
                        case 3: menu = MenuPage.ConfirmLogout; break;
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
            if (UISkin.Window(r, "Log Out")) { menu = MenuPage.Main; return; }
            float sinceHit = Time.time - p.LastDamagedTime;
            bool inCombat = sinceHit < CombatLogoutLock && !p.IsDead;
            string text = inCombat
                ? "You can't log out in the middle of a fight.\n<color=#ff8866>Wait " + Mathf.CeilToInt(CombatLogoutLock - sinceHit) + "s without taking damage.</color>"
                : "Your character is saved on the server.\nLog out and return to the login screen?";
            GUI.Label(new Rect(r.x + 24, r.y + 56, w - 48, 64), text, UISkin.V(UISkin.Rich, alignment: TextAnchor.MiddleCenter, wordWrap: true));
            GUI.enabled = !inCombat;
            if (UISkin.Btn(new Rect(r.x + 36, r.yMax - 70, 160, 46), "Log Out", UISkin.Button))
            {
                menu = MenuPage.None;
                CloseAllWindows();
                NetClient.I.LogOut();
            }
            GUI.enabled = true;
            if (UISkin.Btn(new Rect(r.xMax - 196, r.yMax - 70, 160, 46), "Cancel", UISkin.Button)) menu = MenuPage.Main;
        }

        void CloseAllWindows()
        {
            showBags = showChar = showSkills = showQuests = showMap = showHelp = showTalents = showStash = false;
            dialogNpc = null;
            craftStation = null;
            menuPlayer = null;
            socketGem = -1;
            ChatOpen = false;
        }

        void DrawMenuSettings()
        {
            const float w = 560, h = 380;
            var r = new Rect((VW - w) / 2, (VH - h) / 2, w, h);
            if (UISkin.Window(r, "Settings")) { menu = MenuPage.Main; return; }
            float x = r.x + 34, y = r.y + 66;
            var label = UISkin.V(UISkin.Label, fontSize: 17);

            // Graphics quality
            UISkin.Shadowed(new Rect(x, y + 6, 120, 26), "Graphics", label, UISkin.Cream);
            for (int i = 0; i < GameSettings.QualityNames.Length; i++)
            {
                var br = new Rect(x + 130 + i * 118, y, 110, 38);
                bool on = GameSettings.Quality == i;
                if (UISkin.Btn(br, on ? "> " + GameSettings.QualityNames[i] + " <" : GameSettings.QualityNames[i], UISkin.Button) && !on)
                    GameSettings.Quality = i;
            }
            if (new Rect(x + 130, y, 346, 38).Contains(Event.current.mousePosition))
                tooltip = "<b>Low</b>: no shadows or grass, fewer lights and particles, no color grade.\n<b>Medium</b>: hard shadows, fewer lights.\n<b>High</b>: everything.";
            y += 58;

            // Volume
            UISkin.Shadowed(new Rect(x, y, 120, 26), "Volume", label, UISkin.Cream);
            float v = GUI.HorizontalSlider(new Rect(x + 130, y + 8, 300, 20), Sfx.Volume, 0f, 1f);
            if (Mathf.Abs(v - Sfx.Volume) > 0.001f) Sfx.Volume = v;
            UISkin.Shadowed(new Rect(x + 444, y, 60, 26), Mathf.RoundToInt(Sfx.Volume * 100) + "%", label, UISkin.Gold);
            y += 50;

            // Toggles
            if (Toggle(new Rect(x, y, w - 68, 30), GameSettings.ShowFps, "Show frames per second")) GameSettings.ShowFps = !GameSettings.ShowFps;
            y += 40;
            if (Toggle(new Rect(x, y, w - 68, 30), GameSettings.ShowCommonLoot, "Label common (white) items on the ground  -  otherwise hold Alt"))
                GameSettings.ShowCommonLoot = !GameSettings.ShowCommonLoot;
            y += 52;

            if (UISkin.Btn(new Rect(r.x + (w - 200) / 2, r.yMax - 70, 200, 46), "Back", UISkin.Button)) menu = MenuPage.Main;
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
