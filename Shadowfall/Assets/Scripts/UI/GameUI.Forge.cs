using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The blacksmiths' forge: salvage gear in the bags into materials, and reforge one affix of a piece for materials and
    /// gold (Forge.cs has the costs; the server does the work). Opened from a weapon or armor merchant in town.
    /// </summary>
    public partial class GameUI
    {
        bool forgeOpen;
        int forgeSelected = -1;

        /// <summary>The piece just sent to the smith (salvage or reforge): what the forge's show needs once the server answers.</summary>
        public static (Color color, bool weapon, string oldText) ForgePending;
        static int forgeFlashRow = -1;
        static float forgeFlashAt;

        /// <summary>The server reforged property <paramref name="row"/>: its row glows gold for a moment.</summary>
        public static void ForgeFlash(int row) { forgeFlashRow = row; forgeFlashAt = Time.time; }

        public void OpenForge() { forgeOpen = true; forgeSelected = -1; dialogNpc = null; Sfx.Play2D("anvil", 0.5f); }

        void DrawForge(Player p)
        {
            if (!WorldGenerator.InTown(p.transform.position) || Dungeon.Active) { forgeOpen = false; return; }
            const int cols = 8;
            const float cell = 48, gap = 4;
            var r = new Rect(14, 110, 520, Mathf.Min(640, VH - 130));
            if (UISkin.Window(r, "Salvage & Reforge", true, true)) { forgeOpen = false; return; }
            Block(r);
            float x = r.x + 26, y = r.y + 56, w = r.width - 52;

            // Materials on hand
            string mats = "";
            foreach (var name in Forge.Materials) mats += "<b>" + p.Inventory.CountOf(name) + "</b> " + name + "     ";
            GUI.Label(new Rect(x, y, w, 24), mats, UISkin.Ink14);
            y += 30;

            // Gear in the bags
            GUI.Label(new Rect(x, y, w, 22), "<b>Your gear</b>  (click a piece)", UISkin.InkRich);
            y += 26;
            int shown = 0;
            for (int i = 0; i < p.Inventory.Slots.Length; i++)
            {
                var it = p.Inventory.Slots[i];
                if (it == null || it.Kind != ItemKind.Equipment) continue;
                var cr = new Rect(x + (shown % cols) * (cell + gap), y + (shown / cols) * (cell + gap), cell, cell);
                shown++;
                DrawItemSlot(cr, it, p);
                if (i == forgeSelected)
                {
                    GUI.color = UISkin.Gold;
                    GUI.DrawTexture(new Rect(cr.x - 2, cr.y - 2, cr.width + 4, 2), UISkin.White);
                    GUI.DrawTexture(new Rect(cr.x - 2, cr.yMax, cr.width + 4, 2), UISkin.White);
                    GUI.DrawTexture(new Rect(cr.x - 2, cr.y, 2, cr.height), UISkin.White);
                    GUI.DrawTexture(new Rect(cr.xMax, cr.y, 2, cr.height), UISkin.White);
                    GUI.color = Color.white;
                }
                if (cr.Contains(Event.current.mousePosition)) ItemTooltip(it, p, "Click to choose it");
                if (ClickedIn(cr) >= 0) { forgeSelected = i; Sfx.Play2D("ui_click", 0.4f); }
            }
            if (shown == 0) GUI.Label(new Rect(x, y, w, 24), "<i>No gear in your bags.</i>", UISkin.InkRich);
            y += Mathf.Max(1, Mathf.CeilToInt(shown / (float)cols)) * (cell + gap) + 10;

            var sel = forgeSelected >= 0 && forgeSelected < p.Inventory.Slots.Length ? p.Inventory.Slots[forgeSelected] : null;
            if (sel == null || sel.Kind != ItemKind.Equipment)
            {
                forgeSelected = -1;
                GUI.Label(new Rect(x, y, w, 44), "Salvaging breaks gear into materials (gems in it come back). Reforging rerolls one property of a magic, rare, legendary or set piece.", UISkin.V(UISkin.Ink14, wordWrap: true));
                y += 52;
            }
            else
            {
                GUI.Label(new Rect(x, y, w, 24), "<b><color=#" + Item.Hex(sel.NameColor) + ">" + sel.Name + "</color></b>", UISkin.InkRich);
                y += 28;
                if (Forge.Reforgeable(sel))
                {
                    for (int m = 0; m < sel.Mods.Count; m++)
                    {
                        var mod = sel.Mods[m];
                        bool can = Forge.CanReforge(sel, m);
                        float flash = m == forgeFlashRow ? 1f - (Time.time - forgeFlashAt) / 2.2f : 0f;
                        if (flash > 0f)
                        {
                            GUI.color = new Color(1f, 0.82f, 0.3f, 0.45f * flash);
                            GUI.DrawTexture(new Rect(x, y, w - 124, 34), UISkin.White);
                            GUI.color = Color.white;
                        }
                        GUI.Label(new Rect(x + 6, y + 6, w - 130, 24), "<color=#" + (can ? "3d5aa8" : "8a8070") + ">" + Item.StatText(mod.Stat, mod.Value) + "</color>" +
                            (sel.Reforged == m + 1 ? "  <i>(reforged)</i>" : ""), UISkin.InkRich);
                        var b = new Rect(x + w - 118, y, 118, 34);
                        GUI.enabled = can && Forge.CanAfford(p, sel);
                        if (UISkin.Btn(b, "Reforge", UISkin.Button))
                        {
                            ForgePending = (sel.NameColor, sel.Slot == EquipSlot.Weapon, Item.StatText(mod.Stat, mod.Value));
                            NetClient.I?.Op("reforge", i: forgeSelected, j: m);
                        }
                        GUI.enabled = true;
                        if (!can && b.Contains(Event.current.mousePosition)) tooltip = "This piece has been reforged before: only the same property can be reforged again.";
                        y += 38;
                    }
                    GUI.Label(new Rect(x, y, w, 24), "Each reforge costs  " + Forge.CostText(p, sel), UISkin.InkRich);
                    y += 30;
                }
                else
                {
                    GUI.Label(new Rect(x, y, w, 24), "<i>Common gear has nothing to reforge.</i>", UISkin.InkRich);
                    y += 30;
                }
                if (UISkin.Btn(new Rect(x, y, 240, 40), "Salvage It", UISkin.Button))
                {
                    ForgePending = (sel.NameColor, sel.Slot == EquipSlot.Weapon, null);
                    NetClient.I?.Op("salvage", i: forgeSelected);
                    forgeSelected = -1;
                }
                GUI.Label(new Rect(x + 252, y + 8, w - 252, 24), "for " + Forge.YieldText(sel), UISkin.Ink14);
                y += 50;
            }

            if (UISkin.Btn(new Rect(x, r.yMax - 64, w, 42), "Salvage All Common & Magic Gear", UISkin.Button))
            {
                ForgePending = (new Color(0.55f, 0.55f, 0.6f), false, null);
                NetClient.I?.Op("salvagejunk");
            }
        }
    }
}
