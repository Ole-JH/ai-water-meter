using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The admin panel (F10, or Admin in the game menu; only for accounts the server marks as admin).
    /// Map overrides and hero cheats are local; dungeon, world and player commands go to the server, which
    /// checks the admin flag again.
    /// </summary>
    public partial class GameUI
    {
        bool showAdmin;
        int adminTab, spawnType, spawnLevel = 5, spawnCount = 1, adminDifficulty;
        bool spawnElite;
        string announceText = "";
        static readonly string[] adminTabs = { "Map & Hero", "Dungeons", "World", "Players" };
        static readonly string[] monsterTypes =
        {
            "Dire Wolf", "Goblin", "Goblin Shaman", "Bandit", "Skeleton", "Skeleton Archer", "Zombie", "Rock Golem",
            "Goblin Warchief", "Bandit Lord", "Goblin King", "Crypt Lord", "Stone Colossus", "Lich King",
        };

        void AdminKeys()
        {
            if (AdminTools.IsAdmin && GameInput.Down(GKey.F10)) showAdmin = !showAdmin;
            if (!AdminTools.IsAdmin) showAdmin = false;
        }

        void DrawAdmin(Player p)
        {
            var r = new Rect(14, 120, 470, 560);
            if (UISkin.Window(r, "Admin")) { showAdmin = false; return; }
            Block(r);
            float x = r.x + 22, w = r.width - 44, y = r.y + 56;

            // Tabs
            float tw = (w - 3 * 6) / 4f;
            for (int i = 0; i < adminTabs.Length; i++)
                if (UISkin.Btn(new Rect(x + i * (tw + 6), y, tw, 32), i == adminTab ? "> " + adminTabs[i] : adminTabs[i], UISkin.V(UISkin.Button, fontSize: 13)))
                {
                    adminTab = i;
                    if (i == 3) AdminTools.Send(new AdminCmd { c = "who" });
                }
            y += 46;

            switch (adminTab)
            {
                case 0: AdminMapHero(p, x, y, w); break;
                case 1: AdminDungeons(x, y, w); break;
                case 2: AdminWorld(p, x, y, w); break;
                default: AdminPlayers(x, y, w); break;
            }
        }

        void Section(ref float y, float x, float w, string title)
        {
            UISkin.Shadowed(new Rect(x, y, w, 22), title, UISkin.V(UISkin.Heading, fontSize: 16), UISkin.Gold);
            y += 28;
        }

        bool AdminButton(Rect r, string text) => UISkin.Btn(r, text, UISkin.V(UISkin.Button, fontSize: 14));

        void AdminMapHero(Player p, float x, float y, float w)
        {
            Section(ref y, x, w, "Map overrides");
            if (Toggle(new Rect(x, y, w, 28), AdminTools.RevealMap, "Reveal the whole map (no fog)")) AdminTools.RevealMap = !AdminTools.RevealMap;
            y += 32;
            if (Toggle(new Rect(x, y, w, 28), AdminTools.ShowEnemies, "Show enemies on the maps")) AdminTools.ShowEnemies = !AdminTools.ShowEnemies;
            y += 32;
            if (Toggle(new Rect(x, y, w, 28), AdminTools.ShowDungeons, "Show dungeon entrances on the maps")) AdminTools.ShowDungeons = !AdminTools.ShowDungeons;
            y += 42;

            Section(ref y, x, w, "Hero");
            if (Toggle(new Rect(x, y, w / 2, 28), AdminTools.God, "God mode")) AdminTools.God = !AdminTools.God;
            if (Toggle(new Rect(x + w / 2, y, w / 2, 28), AdminTools.Fast, "Run fast")) AdminTools.Fast = !AdminTools.Fast;
            y += 38;
            float bw = (w - 12) / 3f;
            if (AdminButton(new Rect(x, y, bw, 34), "+1000 gold")) Give("gold");
            if (AdminButton(new Rect(x + bw + 6, y, bw, 34), "+1 level")) p.AddXp(p.XpToNext - p.Xp);
            if (AdminButton(new Rect(x + 2 * (bw + 6), y, bw, 34), "Full heal"))
            {
                p.Heal(p.MaxHealth);
                p.RestoreMana(p.MaxMana);
                for (int i = 0; i < p.CooldownEnd.Length; i++) p.CooldownEnd[i] = 0f;
            }
            y += 40;
            if (AdminButton(new Rect(x, y, bw, 34), "Legendary")) Give("legendary");
            if (AdminButton(new Rect(x + bw + 6, y, bw, 34), "Set piece")) Give("set");
            if (AdminButton(new Rect(x + 2 * (bw + 6), y, bw, 34), "5 gems")) Give("gems");
            y += 40;
            if (AdminButton(new Rect(x, y, bw, 34), "10 potions")) Give("potions");
            if (AdminButton(new Rect(x + bw + 6, y, bw, 34), "Recall ready")) p.ResetRecallCooldown();
            if (AdminButton(new Rect(x + 2 * (bw + 6), y, bw, 34), "Save now")) NetClient.I?.SaveNow();
        }

        /// <summary>Items and gold come from the server (admin command "give").</summary>
        static void Give(string what) => AdminTools.Send(new AdminCmd { c = "give", what = what, n = 1000 });

        void AdminDungeons(float x, float y, float w)
        {
            Section(ref y, x, w, "Enter any dungeon at any depth");
            float dw = (w - 18) / 4f;
            for (int i = 0; i < Difficulties.Names.Length; i++)
                if (AdminButton(new Rect(x + i * (dw + 6), y, dw, 28), (i == adminDifficulty ? "> " : "") + Difficulties.Names[i])) adminDifficulty = i;
            y += 38;
            for (int d = 0; d < DungeonDef.All.Length; d++)
            {
                var def = DungeonDef.All[d];
                UISkin.Shadowed(new Rect(x, y + 6, 180, 22), def.Name, UISkin.Label, UISkin.Cream);
                for (int depth = 1; depth <= def.Depths; depth++)
                    if (AdminButton(new Rect(x + 190 + (depth - 1) * 74, y, 68, 32), "Depth " + depth))
                        AdminTools.Send(new AdminCmd { c = "dungeon", d = d, l = depth, df = adminDifficulty });
                y += 40;
            }
            y += 10;
            GUI.enabled = Dungeon.Active;
            if (AdminButton(new Rect(x, y, w, 36), "Regenerate this level (new layout, new monsters)")) AdminTools.Send(new AdminCmd { c = "regen" });
            y += 42;
            if (AdminButton(new Rect(x, y, w, 36), "Fresh copy of this level (only me)"))
                AdminTools.Send(new AdminCmd { c = "dungeon", d = Dungeon.Index, l = Dungeon.Depth, fresh = true, df = adminDifficulty });
            GUI.enabled = true;
            y += 48;
            UISkin.Shadowed(new Rect(x, y, w, 60), "Dungeons are generated per party: everyone in your party\nwho enters gets the same layout.", UISkin.V(UISkin.Small, wordWrap: true), UISkin.Muted);
        }

        void AdminWorld(Player p, float x, float y, float w)
        {
            Section(ref y, x, w, "Time of day (everyone)");
            string[] phases = { "dawn", "day", "dusk", "night" };
            float bw = (w - 18) / 4f;
            for (int i = 0; i < phases.Length; i++)
                if (AdminButton(new Rect(x + i * (bw + 6), y, bw, 32), char.ToUpper(phases[i][0]) + phases[i].Substring(1)))
                    AdminTools.Send(new AdminCmd { c = "time", phase = phases[i] });
            y += 44;

            Section(ref y, x, w, "Elite chance for new spawns");
            float[] chances = { 0f, 0.08f, 0.5f, 1f };
            for (int i = 0; i < chances.Length; i++)
                if (AdminButton(new Rect(x + i * (bw + 6), y, bw, 32), Mathf.RoundToInt(chances[i] * 100) + "%"))
                    AdminTools.Send(new AdminCmd { c = "elites", chance = chances[i] });
            y += 44;

            Section(ref y, x, w, "Spawn monsters next to you");
            if (AdminButton(new Rect(x, y, 34, 32), "<")) spawnType = (spawnType + monsterTypes.Length - 1) % monsterTypes.Length;
            UISkin.Shadowed(new Rect(x + 40, y + 4, 190, 24), monsterTypes[spawnType], UISkin.LabelCenter, UISkin.Cream);
            if (AdminButton(new Rect(x + 236, y, 34, 32), ">")) spawnType = (spawnType + 1) % monsterTypes.Length;
            if (Toggle(new Rect(x + 290, y + 2, 140, 28), spawnElite, "Elite")) spawnElite = !spawnElite;
            y += 40;
            UISkin.Shadowed(new Rect(x, y + 4, 60, 24), "Level", UISkin.Label, UISkin.Cream);
            if (AdminButton(new Rect(x + 60, y, 34, 32), "-")) spawnLevel = Mathf.Max(1, spawnLevel - 1);
            UISkin.Shadowed(new Rect(x + 96, y + 4, 40, 24), spawnLevel.ToString(), UISkin.LabelCenter, UISkin.Gold);
            if (AdminButton(new Rect(x + 138, y, 34, 32), "+")) spawnLevel = Mathf.Min(60, spawnLevel + 1);
            UISkin.Shadowed(new Rect(x + 190, y + 4, 60, 24), "Count", UISkin.Label, UISkin.Cream);
            if (AdminButton(new Rect(x + 250, y, 34, 32), "-")) spawnCount = Mathf.Max(1, spawnCount - 1);
            UISkin.Shadowed(new Rect(x + 286, y + 4, 40, 24), spawnCount.ToString(), UISkin.LabelCenter, UISkin.Gold);
            if (AdminButton(new Rect(x + 328, y, 34, 32), "+")) spawnCount = Mathf.Min(20, spawnCount + 1);
            y += 42;
            if (AdminButton(new Rect(x, y, w / 2 - 3, 36), "Spawn"))
                AdminTools.Send(new AdminCmd { c = "spawn", type = monsterTypes[spawnType], l = spawnLevel, n = spawnCount, elite = spawnElite });
            if (AdminButton(new Rect(x + w / 2 + 3, y, w / 2 - 3, 36), "Kill everything nearby")) AdminTools.Send(new AdminCmd { c = "killall", r = 25f });
            y += 50;

            Section(ref y, x, w, "Announcement");
            GUI.SetNextControlName("admin_announce");
            announceText = GUI.TextField(new Rect(x, y, w - 96, 34), announceText, 200, UISkin.Field);
            if (AdminButton(new Rect(x + w - 90, y, 90, 34), "Send") && announceText.Trim().Length > 0)
            {
                AdminTools.Send(new AdminCmd { c = "announce", text = announceText });
                announceText = "";
            }
            adminFieldFocused = GUI.GetNameOfFocusedControl() == "admin_announce";
        }

        bool adminFieldFocused;

        void AdminPlayers(float x, float y, float w)
        {
            Section(ref y, x, w, "Players online");
            if (AdminButton(new Rect(x + w - 90, y - 32, 90, 28), "Refresh")) AdminTools.Send(new AdminCmd { c = "who" });
            foreach (var row in NetClient.I.AdminWho)
            {
                var f = row.Split('|');
                if (f.Length < 4) continue;
                bool me = f[1] == Player.I.DisplayName || int.TryParse(f[0], out int id) && id == NetClient.I.MyId;
                UISkin.Shadowed(new Rect(x, y + 2, 150, 22), f[1] + "  " + f[2], UISkin.Label, me ? UISkin.Gold : UISkin.Cream);
                UISkin.Shadowed(new Rect(x, y + 22, 170, 18), f[3], UISkin.Small, UISkin.Muted);
                if (!me)
                {
                    if (AdminButton(new Rect(x + 176, y + 4, 70, 32), "Go to")) AdminTools.Send(new AdminCmd { c = "tpto", name = f[1] });
                    if (AdminButton(new Rect(x + 252, y + 4, 80, 32), "Summon")) AdminTools.Send(new AdminCmd { c = "summon", name = f[1] });
                    if (AdminButton(new Rect(x + 338, y + 4, 88, 32), "Kick")) { AdminTools.Send(new AdminCmd { c = "kick", name = f[1] }); AdminTools.Send(new AdminCmd { c = "who" }); }
                }
                y += 46;
                if (y > Screen.height) break;
            }
            UISkin.Shadowed(new Rect(x, y + 10, w, 40), "Chat commands: /a help", UISkin.Small, UISkin.Muted);
        }
    }
}
