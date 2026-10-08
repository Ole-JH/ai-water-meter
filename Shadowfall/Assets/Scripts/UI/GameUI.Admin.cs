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
        int adminTab, spawnType, spawnLevel = 5, spawnCount = 1, adminDifficulty, adminGate, adminRiftTier = 1;
        bool adminWarn;
        bool spawnElite;
        string announceText = "";
        static readonly string[] adminTabs = { "Hero", "Dungeons", "World", "Events", "Players" };
        static readonly string[] adminGates = { "Any gate", "North", "South", "East", "West" };
        static readonly string[] monsterTypes =
        {
            "Dire Wolf", "Goblin", "Goblin Shaman", "Bandit", "Skeleton", "Skeleton Archer", "Zombie", "Rock Golem",
            "Goblin Warchief", "Bandit Lord", "Goblin King", "Crypt Lord", "Stone Colossus", "Lich King",
            "Frost Wolf", "Ice Wraith", "Frost Giant", "Desert Raider", "Raider Marksman", "Sand Golem", "Ash Ghoul", "Ember Skeleton",
            "Ash Wraith", "Cinder Golem", "Jarl Frostborn", "Raider Warlord", "The Ashen King", "The Frost Witch", "The Sand Colossus", "The Cinder Lord",
            "Old Bramblehide", "Hrimgar the Mountain", "Gorvash the Dune Reaver", "The Pyre Colossus",
        };

        void AdminKeys()
        {
            if (AdminTools.IsAdmin && GameInput.Down(GKey.F10)) showAdmin = !showAdmin;
            if (!AdminTools.IsAdmin) showAdmin = false;
        }

        void DrawAdmin(Player p)
        {
            var r = new Rect(14, 110, 470, 740);
            if (UISkin.Window(r, "Admin")) { showAdmin = false; return; }
            Block(r);
            float x = r.x + 22, w = r.width - 44, y = r.y + 56;

            // Tabs
            float tw = (w - (adminTabs.Length - 1) * 6) / adminTabs.Length;
            for (int i = 0; i < adminTabs.Length; i++)
                if (UISkin.Btn(new Rect(x + i * (tw + 6), y, tw, 32), i == adminTab ? "> " + adminTabs[i] : adminTabs[i], UISkin.V(UISkin.Button, fontSize: 13)))
                {
                    adminTab = i;
                    if (i == 4) AdminTools.Send(new AdminCmd { c = "who" });
                    if (i == 3) AdminTools.Send(new AdminCmd { c = "status" });
                }
            y += 46;

            switch (adminTab)
            {
                case 0: AdminMapHero(p, x, y, w); break;
                case 1: AdminDungeons(x, y, w); break;
                case 2: AdminWorld(p, x, y, w); break;
                case 3: AdminEvents(p, x, y, w); break;
                default: AdminPlayers(x, y, w); break;
            }

            adminFieldFocused = GUI.GetNameOfFocusedControl().StartsWith("admin_"); // typing: the game keys stay quiet

            // The server's answer to the last command
            if (!string.IsNullOrEmpty(AdminTools.LastResult))
                UISkin.Shadowed(new Rect(x, r.yMax - 64, w, 52), AdminTools.LastResult, UISkin.V(UISkin.Small, wordWrap: true), UISkin.Muted);
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
            y += 32;
            if (Toggle(new Rect(x, y, w, 28), AdminTools.NoCooldowns, "No cooldowns (abilities, potions, recall; no mana cost)"))
                AdminTools.NoCooldowns = !AdminTools.NoCooldowns;
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
            y += 40;
            if (AdminButton(new Rect(x, y, bw, 34), "Forge materials")) Give("materials");
            if (AdminButton(new Rect(x + bw + 6, y, bw, 34), "All mounts")) Give("mounts");
            GUI.enabled = p.Level >= ParagonBoard.MaxLevel;
            if (AdminButton(new Rect(x + 2 * (bw + 6), y, bw, 34), "+1 paragon")) p.AdminParagonLevel();
            GUI.enabled = true;
            y += 40;
            UISkin.Shadowed(new Rect(x, y, w, 40), "Paragon levels start at level " + ParagonBoard.MaxLevel + ". Forge materials: scrap, dust, crystals and souls for reforging.",
                UISkin.V(UISkin.Small, wordWrap: true), UISkin.Muted);
            y += 46;

            // tp x z
            Section(ref y, x, w, "Teleport");
            float tw = (w - 12) / 4f;
            UISkin.Shadowed(new Rect(x, y + 6, 20, 24), "X", UISkin.Label, UISkin.Muted);
            tpX = TextInput(new Rect(x + 20, y, tw - 20, 32), "admin_tpx", tpX, 6);
            UISkin.Shadowed(new Rect(x + tw + 6, y + 6, 20, 24), "Z", UISkin.Label, UISkin.Muted);
            tpZ = TextInput(new Rect(x + tw + 26, y, tw - 20, 32), "admin_tpz", tpZ, 6);
            if (AdminButton(new Rect(x + 2 * (tw + 6), y, tw, 32), "Go") && float.TryParse(tpX, out float gx) && float.TryParse(tpZ, out float gz))
                AdminTools.Send(new AdminCmd { c = "tp", x = gx, z = gz });
            if (AdminButton(new Rect(x + 3 * (tw + 6), y, tw, 32), "Where am I"))
            {
                tpX = p.transform.position.x.ToString("0");
                tpZ = p.transform.position.z.ToString("0");
            }
            y += 38;
            int towns = WorldGenerator.Towns.Length;
            float cw = (w - (towns - 1) * 4) / Mathf.Max(1, towns);
            for (int i = 0; i < towns; i++)
            {
                var t = WorldGenerator.Towns[i];
                if (AdminButton(new Rect(x + i * (cw + 4), y, cw, 28), t.Name.Split(' ')[0]))
                    AdminTools.Send(new AdminCmd { c = "tp", x = t.Center.x, z = t.Center.z });
            }
        }

        string tpX = "", tpZ = "", resetName = "";

        void AdminEvents(Player p, float x, float y, float w)
        {
            if (AdminButton(new Rect(x + w - 150, y - 4, 150, 28), "Server status")) AdminTools.Send(new AdminCmd { c = "status" });
            Section(ref y, x, w, "Town invasion");
            UISkin.Shadowed(new Rect(x, y - 4, w, 20), Invasion.Current != null ? Invasion.Current.town + ": " + Invasion.Status : "None right now",
                UISkin.Small, Invasion.Current != null ? Invasion.Color : UISkin.Muted);
            y += 20;
            float gw = (w - 24) / 5f;
            for (int i = 0; i < adminGates.Length; i++)
                if (AdminButton(new Rect(x + i * (gw + 6), y, gw, 28), (i == adminGate ? "> " : "") + adminGates[i])) adminGate = i;
            y += 34;
            int walled = 0;
            foreach (var t in WorldGenerator.Towns) if (t.Walled) walled++;
            float tw = (w - (walled - 1) * 6) / Mathf.Max(1, walled);
            int k = 0;
            foreach (var t in WorldGenerator.Towns)
            {
                if (!t.Walled) continue;
                if (AdminButton(new Rect(x + k++ * (tw + 6), y, tw, 32), t.Name.Split(' ')[0]))
                    AdminTools.Send(new AdminCmd { c = "invasion", town = t.Name, gate = adminGate == 0 ? "" : adminGates[adminGate].ToLowerInvariant(), warn = adminWarn });
            }
            y += 38;
            float hw = (w - 6) / 2f, qw = (w - 18) / 4f;
            // the town buttons above start it straight away, or (ticked) with the scouts' 90-second warning first
            var wr = new Rect(x, y, qw, 32);
            if (AdminButton(wr, (adminWarn ? "[x]" : "[ ]") + " Scouts first")) adminWarn = !adminWarn;
            if (wr.Contains(Event.current.mousePosition))
                tooltip = "Ticked, the town buttons start an invasion as the server's own do: scouts announce the town and gate and the first wave falls on it 90 seconds later";
            GUI.enabled = Invasion.Current != null;
            if (AdminButton(new Rect(x + qw + 6, y, qw, 32), "Go to the gate") && Invasion.Current != null)
                AdminTools.Send(new AdminCmd { c = "tp", x = Invasion.Current.gx, z = Invasion.Current.gz - 4f });
            if (AdminButton(new Rect(x + 2 * (qw + 6), y, qw, 32), "Fire arrow")) AdminTools.Send(new AdminCmd { c = "invasion", fire = true });
            if (AdminButton(new Rect(x + 3 * (qw + 6), y, qw, 32), "End it")) AdminTools.Send(new AdminCmd { c = "invasion", stop = true });
            GUI.enabled = true;
            y += 38;
            // As a lost siege: the quarter behind the chosen gate burns for twelve minutes (south when "Any gate")
            // which quarter: the gate picked above ("Any gate" burns the south one)
            string burnGate = adminGate == 0 ? "South" : adminGates[adminGate];
            UISkin.Shadowed(new Rect(x, y - 2, 90, 18), "Burn the", UISkin.Small, UISkin.Muted);
            UISkin.Shadowed(new Rect(x, y + 14, 90, 18), burnGate.ToLowerInvariant() + " quarter:", UISkin.Small, UISkin.Muted);
            float sw = (w - 90 - 80 - walled * 4) / Mathf.Max(1, walled);
            k = 0;
            foreach (var t in WorldGenerator.Towns)
            {
                if (!t.Walled) continue;
                var br = new Rect(x + 90 + k++ * (sw + 4), y, sw, 30);
                if (AdminButton(br, t.Name.Split(' ')[0]))
                    AdminTools.Send(new AdminCmd { c = "sack", town = t.Name, gate = burnGate.ToLowerInvariant() });
                if (br.Contains(Event.current.mousePosition))
                    tooltip = "Set fire to the " + burnGate.ToLowerInvariant() + " quarter of " + t.Name + " for twelve minutes, as a lost siege does: its houses burn, " +
                              "its merchants flee and their trade is refused there. Pick the gate in the row above.";
            }
            var pr = new Rect(x + w - 76, y, 76, 30);
            if (AdminButton(pr, "Put out")) AdminTools.Send(new AdminCmd { c = "sack", town = "stop" });
            if (pr.Contains(Event.current.mousePosition)) tooltip = "Put out every burning quarter now; everyone comes back";
            y += 44;

            Section(ref y, x, w, "World boss");
            UISkin.Shadowed(new Rect(x, y - 4, w, 20), WorldBoss.Up ? WorldBoss.Current.name + ": " + WorldBoss.Status : "None up right now",
                UISkin.Small, WorldBoss.Up ? WorldBoss.Color : UISkin.Muted);
            y += 20;
            float bw = (w - 6) / 2f;
            for (int i = 0; i < WorldBoss.Names.Length; i++)
            {
                string label = WorldBoss.Names[i].Replace("The ", "").Replace(" the Mountain", "").Replace(" the Dune Reaver", "");
                if (AdminButton(new Rect(x + (i % 2) * (bw + 6), y + (i / 2) * 38, bw, 32), label))
                    AdminTools.Send(new AdminCmd { c = "worldboss", name = WorldBoss.Names[i] });
            }
            y += ((WorldBoss.Names.Length + 1) / 2) * 38;
            GUI.enabled = WorldBoss.Up;
            if (AdminButton(new Rect(x, y, hw, 32), "Go to it") && WorldBoss.Up)
                AdminTools.Send(new AdminCmd { c = "tp", x = WorldBoss.Position.x, z = WorldBoss.Position.z - 8f });
            if (AdminButton(new Rect(x + hw + 6, y, hw, 32), "Put it to sleep")) AdminTools.Send(new AdminCmd { c = "worldboss", stop = true });
            GUI.enabled = true;
            y += 46;

            Section(ref y, x, w, "Greater rift  -  your best tier " + Rift.Best);
            UISkin.Shadowed(new Rect(x, y + 4, 40, 24), "Tier", UISkin.Label, UISkin.Cream);
            if (AdminButton(new Rect(x + 44, y, 34, 32), "-")) adminRiftTier = Mathf.Max(1, adminRiftTier - (Event.current.shift ? 10 : 1));
            UISkin.Shadowed(new Rect(x + 80, y + 4, 44, 24), adminRiftTier.ToString(), UISkin.LabelCenter, UISkin.Gold);
            if (AdminButton(new Rect(x + 126, y, 34, 32), "+")) adminRiftTier = Mathf.Min(150, adminRiftTier + (Event.current.shift ? 10 : 1));
            float rw = (w - 172 - 6) / 2f;
            if (AdminButton(new Rect(x + 172, y, rw, 32), "Open rift")) AdminTools.Send(new AdminCmd { c = "rift", n = adminRiftTier });
            if (AdminButton(new Rect(x + 172 + rw + 6, y, rw, 32), "Set as best")) AdminTools.Send(new AdminCmd { c = "riftbest", n = adminRiftTier });
            y += 46;

            Section(ref y, x, w, "Daily bounties");
            if (AdminButton(new Rect(x, y, hw, 32), "New bounties now")) AdminTools.Send(new AdminCmd { c = "bounties" });
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

            Section(ref y, x, w, "Season and weather (everyone)  -  now " + Weather.Season + ", " + Weather.Sky);
            string[] seasons = { "spring", "summer", "autumn", "winter" };
            for (int i = 0; i < seasons.Length; i++)
                if (AdminButton(new Rect(x + i * (bw + 6), y, bw, 32), char.ToUpper(seasons[i][0]) + seasons[i].Substring(1)))
                    AdminTools.Send(new AdminCmd { c = "season", kind = seasons[i] });
            y += 38;
            string[] skies = { "clear", "cloudy", "rain", "storm", "fog" };
            float sw = (w - 24) / 5f;
            for (int i = 0; i < skies.Length; i++)
                if (AdminButton(new Rect(x + i * (sw + 6), y, sw, 32), char.ToUpper(skies[i][0]) + skies[i].Substring(1)))
                    AdminTools.Send(new AdminCmd { c = "weather", kind = skies[i], n = 15 });
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
            announceText = TextInput(new Rect(x, y, w - 96, 34), "admin_announce", announceText, 200);
            if (AdminButton(new Rect(x + w - 90, y, 90, 34), "Send") && announceText.Trim().Length > 0)
            {
                AdminTools.Send(new AdminCmd { c = "announce", text = announceText });
                announceText = "";
            }
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
            y += 8;
            // resetpw <account or character>
            Section(ref y, x, w, "Reset a password");
            resetName = TextInput(new Rect(x, y, w - 156, 32), "admin_resetpw", resetName, 40);
            if (AdminButton(new Rect(x + w - 150, y, 150, 32), "Reset password") && resetName.Trim().Length > 0)
            {
                AdminTools.Send(new AdminCmd { c = "resetpw", name = resetName.Trim() });
                resetName = "";
            }
            UISkin.Shadowed(new Rect(x, y + 36, w, 40), "Account or character name: a new password comes back here to hand over. Every admin command also has a chat form: /a help",
                UISkin.V(UISkin.Small, wordWrap: true), UISkin.Muted);
        }
    }
}
