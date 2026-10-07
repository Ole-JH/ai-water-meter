using UnityEngine;

namespace Shadowfall
{
    /// <summary>Talents, buffs, gem sockets, the stash and the trade window.</summary>
    public partial class GameUI
    {
        bool showStash;
        int socketGem = -1;             // bag index of the gem being socketed (-1 = none)
        string tradeGoldText = "0";
        bool tradeOpen => NetClient.I != null && NetClient.I.Trading;
        bool tradeGoldFocused;

        public void OpenStash()
        {
            showStash = true;
            showBags = true;
            Sfx.Play2D("ui_open", 0.4f);
        }

        static string BagHint(Item item, bool vendor)
        {
            if (NetClient.I != null && NetClient.I.Trading) return "Right-click to offer in the trade";
            if (I != null && I.showStash) return "Right-click to put in your stash";
            if (vendor) return "Right-click to sell";
            if (item.Kind == ItemKind.Gem) return "Left-click, then click an item with an empty socket";
            return "Left-click to use / equip.  Shift+Right-click to drop";
        }

        /// <summary>Small diamonds along the bottom of a slot: filled gems and empty sockets.</summary>
        void DrawSocketPips(Rect r, Item item)
        {
            for (int s = 0; s < item.Sockets; s++)
            {
                var pip = new Rect(r.x + 6 + s * 11, r.yMax - 14, 8, 8);
                string gem = item.Gems != null && s < item.Gems.Count ? item.Gems[s] : null;
                GUI.color = new Color(0f, 0f, 0f, 0.8f);
                GUI.DrawTexture(new Rect(pip.x - 1, pip.y - 1, pip.width + 2, pip.height + 2), UISkin.White);
                if (gem != null && ItemPowers.ParseGem(gem, out var type, out _)) GUI.color = ItemPowers.GemColor(type);
                else GUI.color = new Color(0.35f, 0.32f, 0.3f);
                GUI.DrawTexture(pip, UISkin.White);
            }
            GUI.color = Color.white;
        }

        // =====================================================================================
        // Talents
        // =====================================================================================

        void DrawTalents(Player p)
        {
            var talents = Shadowfall.Talents.For(p.Look);
            var r = new Rect(498, 120, 520, 140 + talents.Length * 78);
            if (UISkin.Window(r, p.Look + " Talents")) showTalents = false;
            Block(r);
            float y = r.y + 56;
            int points = p.TalentPoints;
            GUI.Label(new Rect(r.x + 26, y, r.width - 52, 24),
                points > 0 ? "<color=#c9a2ff><b>" + points + "</b> talent point" + (points == 1 ? "" : "s") + " to spend</color>  -  you gain one every level"
                           : "<color=#9a8c78>No talent points left. You gain one every level.</color>", UISkin.RichSmall);
            y += 32;
            foreach (var t in talents)
            {
                int rank = p.Tal(t.Id);
                var row = new Rect(r.x + 20, y, r.width - 40, 70);
                bool hover = row.Contains(Event.current.mousePosition);
                UISkin.Box(row, hover ? UISkin.InsetLight : UISkin.Inset);
                var icon = new Rect(row.x + 10, row.y + 9, 52, 52);
                UISkin.Box(icon, UISkin.Slot);
                UISkin.IconInSlot(icon, UISkin.Icon(t.Icon), rank > 0 ? Color.white : new Color(0.5f, 0.5f, 0.5f), 4);
                UISkin.Shadowed(new Rect(row.x + 74, row.y + 8, 300, 24), t.Name, UISkin.Label, rank > 0 ? UISkin.Gold : UISkin.Cream);
                GUI.Label(new Rect(row.x + 74, row.y + 32, row.width - 170, 36), t.Description, UISkin.V(UISkin.RichSmall, wordWrap: true));
                // rank pips
                for (int i = 0; i < t.MaxRank; i++)
                {
                    GUI.color = i < rank ? new Color(0.8f, 0.6f, 1f) : new Color(0.25f, 0.22f, 0.2f);
                    GUI.DrawTexture(new Rect(row.xMax - 86 + i * 14, row.y + 14, 10, 10), UISkin.White);
                }
                GUI.color = Color.white;
                UISkin.Shadowed(new Rect(row.xMax - 90, row.y + 26, 76, 20), rank + " / " + t.MaxRank, UISkin.SmallCenter, UISkin.Muted);
                GUI.enabled = points > 0 && rank < t.MaxRank;
                if (UISkin.Btn(new Rect(row.xMax - 74, row.y + 44, 48, 22), "+", UISkin.Button)) p.LearnTalent(t);
                GUI.enabled = true;
                y += 78;
            }
            y += 6;
            bool inTown = WorldGenerator.InTown(p.transform.position);
            int cost = 25 * p.Level;
            GUI.enabled = inTown && p.TalentPointsSpent > 0 && p.Gold >= cost;
            if (UISkin.Btn(new Rect(r.x + (r.width - 260) / 2, y, 260, 40), "Reset Talents (" + cost + " gold)", UISkin.Button))
            {
                NetClient.I?.Op("respec"); // the talents reset when the server has taken the gold
            }
            GUI.enabled = true;
            if (new Rect(r.x + (r.width - 260) / 2, y, 260, 40).Contains(Event.current.mousePosition))
                tooltip = inTown ? "Refund every talent point." : "You can only reset your talents in Hollowmere.";
        }

        // =====================================================================================
        // Buffs (above the action bar)
        // =====================================================================================

        void DrawBuffs(Player p)
        {
            if (p.Buffs.Count == 0) return;
            const float s = 40, gap = 6;
            float x = (VW - (p.Buffs.Count * (s + gap) - gap)) / 2f, y = VH - 58 - 46 - 12 - s - 14;
            foreach (var b in p.Buffs)
            {
                float left = b.Until - Time.time;
                if (left <= 0f) continue;
                var r = new Rect(x, y, s, s);
                UISkin.Box(r, UISkin.Slot);
                UISkin.IconInSlot(r, UISkin.Icon(b.Icon), left < 2f && Mathf.PingPong(Time.time * 4f, 1f) > 0.5f ? new Color(1, 1, 1, 0.5f) : Color.white, 3);
                UISkin.Shadowed(new Rect(r.x, r.yMax - 2, r.width, 18), Mathf.CeilToInt(left) + "s", UISkin.SmallCenter, Color.white, 2);
                if (r.Contains(Event.current.mousePosition))
                {
                    string fx = "";
                    if (b.DamageMul != 1f) fx += "\n+" + Mathf.RoundToInt((b.DamageMul - 1f) * 100f) + "% damage";
                    if (b.ArmorMul != 1f) fx += "\n+" + Mathf.RoundToInt((b.ArmorMul - 1f) * 100f) + "% armor";
                    if (b.DamageTakenMul != 1f) fx += "\n" + Mathf.RoundToInt((1f - b.DamageTakenMul) * 100f) + "% less damage taken";
                    if (b.Name == "Vanished") fx += "\nMonsters cannot see you";
                    tooltip = "<b><color=#" + Item.Hex(b.Color) + ">" + b.Name + "</color></b>" + fx;
                }
                x += s + gap;
            }
        }

        // =====================================================================================
        // Stash
        // =====================================================================================

        void StashItem(Player p, int bagIndex)
        {
            if (p.Inventory.Slots[bagIndex] == null) return;
            NetClient.I?.Op("stash", i: bagIndex);
            Sfx.Play2D("drop", 0.4f);
        }

        void DrawStash(Player p)
        {
            var chest = StashChest.I;
            if (chest == null || Factory.FlatDistance(p.transform.position, chest.Position) > 6f) { showStash = false; return; }
            const int cols = 8, rows = 5;
            const float cell = 50, gap = 4;
            float w = cols * (cell + gap) - gap + 40, h = rows * (cell + gap) + 96;
            var r = new Rect(VW - w - 20 - w - 16, VH - h - 82, w, h);
            if (r.x < 10) r.x = 10;
            if (UISkin.Window(r, "Stash")) showStash = false;
            Block(r);
            for (int i = 0; i < p.Stash.Slots.Length; i++)
            {
                var cr = new Rect(r.x + 20 + (i % cols) * (cell + gap), r.y + 58 + (i / cols) * (cell + gap), cell, cell);
                var item = p.Stash.Slots[i];
                DrawItemSlot(cr, item, p);
                if (item == null) continue;
                if (item.Kind == ItemKind.Equipment && item.Sockets > 0) DrawSocketPips(cr, item);
                if (cr.Contains(Event.current.mousePosition)) ItemTooltip(item, p, "Click to take it out");
                if (ClickedIn(cr) >= 0)
                {
                    NetClient.I?.Op("unstash", i: i);
                    Sfx.Play2D("ui_click", 0.4f);
                }
            }
            int used = 0;
            foreach (var it in p.Stash.Slots) if (it != null) used++;
            UISkin.Shadowed(new Rect(r.x + 20, r.yMax - 36, w - 40, 22), used + " / " + p.Stash.Slots.Length + "   -   right-click items in your bags to store them",
                UISkin.Small, UISkin.Muted);
        }

        // =====================================================================================
        // Trading
        // =====================================================================================

        void DrawTrade(Player p)
        {
            var net = NetClient.I;
            tradeGoldFocused = GUI.GetNameOfFocusedControl() == "trade_gold";
            const float cell = 50, gap = 4;
            const int cols = 4;
            float colW = cols * (cell + gap) - gap;
            var r = new Rect((VW - (colW * 2 + 100)) / 2f, 130, colW * 2 + 100, 470);
            if (UISkin.Window(r, "Trade with " + net.TradePartner)) { net.CancelTrade(); return; }
            Block(r);

            float lx = r.x + 30, rx = r.x + r.width - 30 - colW, y = r.y + 58;
            UISkin.Shadowed(new Rect(lx, y, colW, 22), "Your offer", UISkin.HeadingCenter, net.MyOk ? new Color(0.5f, 1f, 0.5f) : UISkin.Gold);
            UISkin.Shadowed(new Rect(rx, y, colW, 22), net.TradePartner, UISkin.HeadingCenter, net.TheirOk ? new Color(0.5f, 1f, 0.5f) : UISkin.Gold);
            y += 32;
            for (int i = 0; i < NetClient.TradeSlots; i++)
            {
                var a = new Rect(lx + (i % cols) * (cell + gap), y + (i / cols) * (cell + gap), cell, cell);
                var mine = net.MyOfferItem(i);
                DrawItemSlot(a, mine, p);
                if (mine != null)
                {
                    if (a.Contains(Event.current.mousePosition)) tooltip = mine.Tooltip(p) + "\n<color=#998877>Click to take it back</color>";
                    if (ClickedIn(a) >= 0) net.RetractItem(i);
                }
                var b = new Rect(rx + (i % cols) * (cell + gap), y + (i / cols) * (cell + gap), cell, cell);
                var theirs = i < net.TheirOffer.Count ? net.TheirOffer[i] : null;
                DrawItemSlot(b, theirs, p);
                if (theirs != null && b.Contains(Event.current.mousePosition))
                    ItemTooltip(theirs, p, null);
            }
            y += 3 * (cell + gap) + 12;

            // Gold
            UISkin.IconInSlot(new Rect(lx, y + 4, 24, 24), UISkin.Icon("gold"), Color.white, 0);
            GUI.SetNextControlName("trade_gold");
            tradeGoldText = GUI.TextField(new Rect(lx + 30, y, 110, 32), tradeGoldText, 9, UISkin.Field);
            if (UISkin.Btn(new Rect(lx + 146, y, colW - 146, 32), "Set", UISkin.Button))
            {
                int.TryParse(tradeGoldText, out int g);
                net.SetTradeGold(g);
                tradeGoldText = net.MyGold.ToString();
            }
            UISkin.IconInSlot(new Rect(rx, y + 4, 24, 24), UISkin.Icon("gold"), Color.white, 0);
            UISkin.Shadowed(new Rect(rx + 30, y + 4, colW - 30, 24), net.TheirGold + " gold", UISkin.Label, new Color(1f, 0.85f, 0.3f));
            y += 40;
            UISkin.Shadowed(new Rect(lx, y, colW, 20), "Offering " + net.MyGold + " gold", UISkin.Small, UISkin.Muted);
            y += 30;

            string state = net.MyOk && net.TheirOk ? "Completing..." : net.MyOk ? "Waiting for " + net.TradePartner + "..." :
                net.TheirOk ? net.TradePartner + " has accepted." : "Right-click items in your bags to offer them.";
            UISkin.Shadowed(new Rect(r.x, y, r.width, 22), state, UISkin.SmallCenter, net.TheirOk ? new Color(0.5f, 1f, 0.5f) : UISkin.Cream);
            y += 30;
            GUI.enabled = !net.MyOk;
            if (UISkin.Btn(new Rect(r.x + r.width / 2 - 170, y, 160, 44), "Accept", UISkin.Button)) net.AcceptTrade();
            GUI.enabled = true;
            if (UISkin.Btn(new Rect(r.x + r.width / 2 + 10, y, 160, 44), "Cancel", UISkin.Button)) net.CancelTrade();
            if (!showBags) showBags = true;
        }
    }
}
