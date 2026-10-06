using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shadowfall
{
    public enum Rarity { Common, Magic, Rare, Legendary }
    public enum ItemKind { Equipment, Consumable, Material }
    public enum EquipSlot { None, Weapon, Helm, Chest, Gloves, Legs, Boots, Ring, Amulet }

    public enum Stat
    {
        Strength, Dexterity, Intelligence, Vitality, Health, Mana, Armor,
        CritChance, AttackSpeed, LifeOnHit, MoveSpeed, HealthRegen, ManaRegen
    }

    [System.Serializable]
    public struct StatMod
    {
        public Stat Stat;
        public int Value;
        public StatMod(Stat s, int v) { Stat = s; Value = v; }
    }

    [System.Serializable]
    public class Item
    {
        public string Name;
        public string BaseType;
        public ItemKind Kind;
        public EquipSlot Slot;
        public Rarity Rarity;
        public int ItemLevel = 1;
        public int RequiredLevel = 1;
        public int MinDamage, MaxDamage;
        public float AttacksPerSecond;
        public int Armor;
        public List<StatMod> Mods = new List<StatMod>();
        public int Value;
        public int Count = 1;
        public int MaxStack = 1;
        public Color IconColor = Color.gray;
        public string Icon = "?";
        public float HealAmount, ManaAmount;
        public string Flavor;

        public bool Stackable => MaxStack > 1;

        public int GetStat(Stat s)
        {
            int total = 0;
            for (int i = 0; i < Mods.Count; i++) if (Mods[i].Stat == s) total += Mods[i].Value;
            return total;
        }

        public Item CloneSingle()
        {
            var c = (Item)MemberwiseClone();
            c.Mods = new List<StatMod>(Mods);
            c.Count = 1;
            return c;
        }

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Magic: return new Color(0.45f, 0.55f, 1f);
                case Rarity.Rare: return new Color(1f, 0.9f, 0.3f);
                case Rarity.Legendary: return new Color(1f, 0.55f, 0.1f);
                default: return new Color(0.92f, 0.92f, 0.92f);
            }
        }

        public Color NameColor => Kind == ItemKind.Equipment ? RarityColor(Rarity) : new Color(0.92f, 0.92f, 0.92f);

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static string StatText(Stat s, int v)
        {
            switch (s)
            {
                case Stat.Strength: return "+" + v + " Strength";
                case Stat.Dexterity: return "+" + v + " Dexterity";
                case Stat.Intelligence: return "+" + v + " Intelligence";
                case Stat.Vitality: return "+" + v + " Vitality";
                case Stat.Health: return "+" + v + " Maximum Life";
                case Stat.Mana: return "+" + v + " Maximum Mana";
                case Stat.Armor: return "+" + v + " Armor";
                case Stat.CritChance: return "+" + v + "% Critical Hit Chance";
                case Stat.AttackSpeed: return "+" + v + "% Attack Speed";
                case Stat.LifeOnHit: return "+" + v + " Life per Hit";
                case Stat.MoveSpeed: return "+" + v + "% Movement Speed";
                case Stat.HealthRegen: return "+" + v + " Life Regeneration per Second";
                case Stat.ManaRegen: return "+" + v + " Mana Regeneration per Second";
                default: return "+" + v + " " + s;
            }
        }

        /// <summary>Rich-text tooltip (IMGUI supports &lt;color&gt; and &lt;b&gt; tags).</summary>
        public string Tooltip(Player player, Item compareTo = null)
        {
            var sb = new StringBuilder();
            sb.Append("<b><color=#").Append(Hex(NameColor)).Append(">").Append(Name).Append("</color></b>");
            if (Count > 1) sb.Append(" x").Append(Count);
            sb.Append('\n');

            if (Kind == ItemKind.Equipment)
            {
                sb.Append("<color=#").Append(Hex(NameColor)).Append(">").Append(Rarity).Append(' ').Append(BaseType).Append("</color>\n");
                sb.Append("<color=#999999>").Append(SlotName(Slot)).Append("   Item Level ").Append(ItemLevel).Append("</color>\n");
                if (Slot == EquipSlot.Weapon)
                {
                    sb.Append("<size=16><b>").Append(MinDamage).Append(" - ").Append(MaxDamage).Append("</b></size> Damage\n");
                    sb.Append(AttacksPerSecond.ToString("0.00")).Append(" Attacks per Second\n");
                    if (compareTo != null && compareTo.Slot == EquipSlot.Weapon)
                        sb.Append(Delta((MinDamage + MaxDamage) * AttacksPerSecond * 0.5f -
                                        (compareTo.MinDamage + compareTo.MaxDamage) * compareTo.AttacksPerSecond * 0.5f, "DPS")).Append('\n');
                }
                if (Armor > 0)
                {
                    sb.Append("<size=16><b>").Append(Armor).Append("</b></size> Armor\n");
                    if (compareTo != null) sb.Append(Delta(Armor - compareTo.Armor, "Armor")).Append('\n');
                }
                foreach (var m in Mods)
                    sb.Append("<color=#7f9fff>").Append(StatText(m.Stat, m.Value)).Append("</color>\n");
                if (player != null && RequiredLevel > player.Level)
                    sb.Append("<color=#ff4444>Requires Level ").Append(RequiredLevel).Append("</color>\n");
                else
                    sb.Append("<color=#999999>Requires Level ").Append(RequiredLevel).Append("</color>\n");
            }
            else if (Kind == ItemKind.Consumable)
            {
                if (HealAmount > 0) sb.Append("<color=#66ff66>Use: Restores ").Append(Mathf.RoundToInt(HealAmount)).Append(" Life</color>\n");
                if (ManaAmount > 0) sb.Append("<color=#66aaff>Use: Restores ").Append(Mathf.RoundToInt(ManaAmount)).Append(" Mana</color>\n");
            }
            else
            {
                sb.Append("<color=#999999>Crafting Material</color>\n");
            }

            if (!string.IsNullOrEmpty(Flavor)) sb.Append("<i><color=#c8a060>\"").Append(Flavor).Append("\"</color></i>\n");
            sb.Append("<color=#ffd700>Sell value: ").Append(Value * Mathf.Max(1, Count)).Append(" gold</color>");
            return sb.ToString();
        }

        static string Delta(float d, string label)
        {
            if (Mathf.Abs(d) < 0.05f) return "<color=#999999>No change in " + label + "</color>";
            return d > 0
                ? "<color=#44ff44>+" + d.ToString("0.#") + " " + label + "</color>"
                : "<color=#ff4444>" + d.ToString("0.#") + " " + label + "</color>";
        }

        public static string SlotName(EquipSlot s)
        {
            switch (s)
            {
                case EquipSlot.Weapon: return "Main Hand";
                case EquipSlot.Helm: return "Head";
                case EquipSlot.Chest: return "Chest";
                case EquipSlot.Gloves: return "Hands";
                case EquipSlot.Legs: return "Legs";
                case EquipSlot.Boots: return "Feet";
                case EquipSlot.Ring: return "Finger";
                case EquipSlot.Amulet: return "Neck";
                default: return "";
            }
        }
    }
}
