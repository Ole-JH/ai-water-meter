using UnityEngine;

namespace Shadowfall
{
    /// <summary>Beastmaster Orla's companion shop and the companion frame under the hero's unit frame.</summary>
    public partial class GameUI
    {
        void DrawCompanionShop(Player p, Rect r, float y)
        {
            UISkin.Shadowed(new Rect(r.x + 26, y, 420, 28), "Companions for Hire", UISkin.Heading, UISkin.Gold);
            y += 36;
            foreach (var def in CompanionDef.All)
            {
                bool owned = p.OwnsCompanion(def.Id), active = p.ActiveCompanion == def.Id, locked = p.Level < def.RequiredLevel;
                var row = new Rect(r.x + 18, y, r.width - 36, 74);
                if (active)
                {
                    GUI.color = new Color(def.Color.r, def.Color.g, def.Color.b, 0.18f);
                    GUI.DrawTexture(row, UISkin.White);
                    GUI.color = Color.white;
                }
                var icon = new Rect(row.x + 6, row.y + 8, 58, 58);
                UISkin.Box(icon, UISkin.Slot);
                UISkin.IconInSlot(icon, UISkin.Icon(def.Icon), locked ? new Color(0.45f, 0.45f, 0.45f) : Color.white, 4);
                GUI.Label(new Rect(row.x + 74, row.y + 4, 250, 24),
                    "<b>" + def.Name + "</b>  <size=13><color=#b8a88c>" + def.Role + "</color></size>", UISkin.InkRich);
                string sub = owned ? (active ? "<color=#7fe07a>Following you</color>" : "<color=#8fb8ff>Hired</color>")
                    : locked ? "<color=#ff7a5c>Requires level " + def.RequiredLevel + "</color>"
                    : "<color=#f0c45a><b>" + def.Price + "</b> gold</color>";
                GUI.Label(new Rect(row.x + 74, row.y + 28, 250, 22), sub, UISkin.Ink14);
                GUI.Label(new Rect(row.x + 74, row.y + 48, 260, 22),
                    "<size=12>" + Mathf.RoundToInt(def.DamageAt(p.Level)) + " damage, " + (def.Ranged ? "ranged" : "melee") + (def.Aoe > 0f ? ", area" : "") + "</size>", UISkin.InkRich);

                var b = new Rect(row.xMax - 100, row.y + 18, 96, 38);
                if (active)
                {
                    if (UISkin.Btn(b, "Dismiss", UISkin.Button)) p.DismissCompanion();
                }
                else if (owned)
                {
                    if (UISkin.Btn(b, "Summon", UISkin.Button)) p.SummonCompanion(def.Id);
                }
                else
                {
                    GUI.enabled = !locked && p.Gold >= def.Price;
                    if (UISkin.Btn(b, "Hire", UISkin.Button)) p.HireCompanion(def);
                    GUI.enabled = true;
                }
                if (row.Contains(Event.current.mousePosition))
                    tooltip = "<b><color=#" + Item.Hex(def.Color) + ">" + def.Name + "</color></b>  -  " + def.Role + "\n" + def.Description +
                              "\n\n<color=#999999>Companions follow you everywhere, fight what you fight and grow stronger as you level. " +
                              "Hire once, then summon any of yours here for free. One at a time.</color>";
                y += 80;
            }
            GUI.Label(new Rect(r.x + 26, y + 4, 420, 24), "You have <color=#f0c45a><b>" + p.Gold + " gold</b></color>.", UISkin.Ink14);
        }

        /// <summary>A small portrait of the companion with its current target, under the hero frame.</summary>
        void DrawCompanionFrame(Player p, float y)
        {
            var c = p.CompanionInstance;
            if (c == null) return;
            var r = new Rect(12, y, 230, 52);
            UISkin.Box(r, UISkin.Panel);
            Block(r);
            var icon = new Rect(r.x + 8, r.y + 6, 40, 40);
            UISkin.Box(icon, UISkin.Slot);
            UISkin.IconInSlot(icon, UISkin.Icon(c.Def.Icon), Color.white, 3);
            UISkin.Shadowed(new Rect(r.x + 56, r.y + 6, 170, 22), c.Def.Name, UISkin.Label, UISkin.Cream);
            UISkin.Shadowed(new Rect(r.x + 56, r.y + 26, 170, 20), c.Def.Role + "  -  level " + p.Level, UISkin.Small, UISkin.Muted);
            if (r.Contains(Event.current.mousePosition))
                tooltip = "<b>" + c.Def.Name + "</b>\n" + c.Def.Description + "\n<color=#998877>Visit Beastmaster Orla in Hollowmere to swap or dismiss companions.</color>";
        }
    }
}
