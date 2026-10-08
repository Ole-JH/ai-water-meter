using UnityEngine;

namespace Shadowfall
{
    /// <summary>The auction house window (from any general merchant in town): browse and buy, or put an item up for sale.</summary>
    public partial class GameUI
    {
        bool auctionOpen, auctionSell, auctionFieldFocused;
        string auctionSearch = "", auctionPrice = "";
        int auctionSlot = -1;
        float auctionScroll;

        public void OpenAuction()
        {
            auctionOpen = true;
            auctionSell = false;
            dialogNpc = null;
            Auction.Browse(auctionSearch);
            Sfx.Play2D("ui_open", 0.4f);
        }

        void DrawAuction(Player p)
        {
            auctionFieldFocused = false;
            if (!WorldGenerator.InTown(p.transform.position) || Dungeon.Active) { auctionOpen = false; return; }
            var r = new Rect(14, 100, 560, Mathf.Min(660, VH - 120));
            if (UISkin.Window(r, "Auction House", true, true)) { auctionOpen = false; return; }
            Block(r);
            float x = r.x + 26, y = r.y + 56, w = r.width - 52;
            if (UISkin.Btn(new Rect(x, y, 150, 36), "Browse", auctionSell ? UISkin.Button : UISkin.ButtonLight)) { auctionSell = false; Auction.Browse(auctionSearch); }
            if (UISkin.Btn(new Rect(x + 158, y, 150, 36), "Sell an Item", auctionSell ? UISkin.ButtonLight : UISkin.Button)) { auctionSell = true; auctionSlot = -1; }
            GUI.Label(new Rect(x + 320, y + 8, w - 320, 24), "You have <color=#a07010><b>" + p.Gold + "</b></color> gold", UISkin.V(UISkin.InkRich, alignment: TextAnchor.UpperRight));
            y += 46;
            if (auctionSell) DrawAuctionSell(p, r, x, y, w);
            else DrawAuctionBrowse(p, r, x, y, w);
        }

        void DrawAuctionBrowse(Player p, Rect r, float x, float y, float w)
        {
            auctionSearch = TextInput(new Rect(x, y, w - 130, 34), "auction_search", auctionSearch, 40);
            auctionFieldFocused |= GUI.GetNameOfFocusedControl() == "auction_search";
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && auctionFieldFocused;
            if (UISkin.Btn(new Rect(x + w - 120, y, 120, 34), "Search", UISkin.Button) || enter) Auction.Browse(auctionSearch);
            y += 44;
            var list = Auction.Listings;
            if (!Auction.Loaded) { GUI.Label(new Rect(x, y, w, 24), "<i>Asking the auctioneer...</i>", UISkin.InkRich); return; }
            if (list.Count == 0) { GUI.Label(new Rect(x, y, w, 24), "<i>Nothing for sale" + (Auction.Query != "" ? " matching \"" + Auction.Query + "\"" : "") + ".</i>", UISkin.InkRich); return; }
            const float rowH = 52;
            var view = new Rect(x, y, w, r.yMax - y - 20);
            auctionScroll = GUI.BeginScrollView(view, new Vector2(0, auctionScroll), new Rect(0, 0, w - 18, list.Count * rowH)).y;
            for (int i = 0; i < list.Count; i++)
            {
                var l = list[i];
                bool mine = i < Auction.Mine;
                float ry = i * rowH;
                var slot = new Rect(0, ry + 2, 44, 44);
                DrawItemSlot(slot, l.item, p);
                if (slot.Contains(Event.current.mousePosition)) ItemTooltip(l.item, p, null);
                GUI.Label(new Rect(52, ry + 2, 280, 24), "<b><color=#" + Item.Hex(l.item.NameColor) + ">" + l.item.Name + (l.item.Count > 1 ? " x" + l.item.Count : "") + "</color></b>", UISkin.InkRich);
                GUI.Label(new Rect(52, ry + 24, 300, 22), (mine ? "<b>yours</b>" : l.seller) + "  -  " + (l.left >= 60 ? l.left / 60 + " h" : l.left + " min") + " left", UISkin.Ink14);
                GUI.Label(new Rect(300, ry + 12, 100, 24), "<color=#a07010><b>" + l.price + "</b></color> gold", UISkin.InkRich);
                var b = new Rect(w - 18 - 110, ry + 6, 110, 38);
                if (mine) { if (UISkin.Btn(b, "Cancel", UISkin.Button)) NetClient.I?.Op("aucancel", id: l.id); }
                else
                {
                    GUI.enabled = p.Gold >= l.price;
                    if (UISkin.Btn(b, "Buy", UISkin.Button)) NetClient.I?.Op("aubuy", id: l.id, k: auctionSearch);
                    GUI.enabled = true;
                }
            }
            GUI.EndScrollView();
        }

        void DrawAuctionSell(Player p, Rect r, float x, float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 24), "Choose an item from your bags, set a price, and list it for 48 hours. The house keeps 5% of the sale.", UISkin.V(UISkin.Ink14, wordWrap: true));
            y += 44;
            const int cols = 8;
            const float cell = 48, gap = 4;
            for (int i = 0; i < p.Inventory.Slots.Length; i++)
            {
                var it = p.Inventory.Slots[i];
                var cr = new Rect(x + (i % cols) * (cell + gap), y + (i / cols) * (cell + gap), cell, cell);
                DrawItemSlot(cr, it, p);
                if (it == null) continue;
                if (i == auctionSlot)
                {
                    GUI.color = UISkin.Gold;
                    GUI.DrawTexture(new Rect(cr.x - 2, cr.yMax, cr.width + 4, 3), UISkin.White);
                    GUI.color = Color.white;
                }
                if (cr.Contains(Event.current.mousePosition)) ItemTooltip(it, p, "Click to sell it");
                if (ClickedIn(cr) >= 0) { auctionSlot = i; auctionPrice = Mathf.Max(1, it.Value * 3 * Mathf.Max(1, it.Count)).ToString(); }
            }
            y += Mathf.CeilToInt(p.Inventory.Slots.Length / (float)cols) * (cell + gap) + 10;
            var sel = auctionSlot >= 0 && auctionSlot < p.Inventory.Slots.Length ? p.Inventory.Slots[auctionSlot] : null;
            if (sel == null) { auctionSlot = -1; return; }
            GUI.Label(new Rect(x, y + 6, 260, 24), "<b><color=#" + Item.Hex(sel.NameColor) + ">" + sel.Name + "</color></b>  for", UISkin.InkRich);
            auctionPrice = TextInput(new Rect(x + 270, y, 120, 34), "auction_price", auctionPrice, 8);
            auctionFieldFocused |= GUI.GetNameOfFocusedControl() == "auction_price";
            if (UISkin.Btn(new Rect(x + w - 110, y, 110, 34), "List It", UISkin.Button) && int.TryParse(auctionPrice, out var price))
            {
                NetClient.I?.Op("aulist", i: auctionSlot, n: price);
                auctionSlot = -1;
                auctionSell = false;
            }
        }
    }
}
