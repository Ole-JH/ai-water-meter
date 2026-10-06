using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>Fixed items (potions, materials, food) and the Diablo-style random equipment generator.</summary>
    public static class ItemDatabase
    {
        // ---------------------------------------------------------------- consumables & materials

        public static Item HealthPotion() => new Item
        {
            Name = "Health Potion", Kind = ItemKind.Consumable, MaxStack = 20, Value = 8,
            HealAmount = 80, IconColor = new Color(0.85f, 0.1f, 0.1f), Icon = "HP",
            Flavor = "Tastes faintly of cherries and iron."
        };

        public static Item ManaPotion() => new Item
        {
            Name = "Mana Potion", Kind = ItemKind.Consumable, MaxStack = 20, Value = 8,
            ManaAmount = 60, IconColor = new Color(0.15f, 0.3f, 0.95f), Icon = "MP",
            Flavor = "It glows. That's probably fine."
        };

        static readonly Dictionary<string, (Color color, string icon, int value)> materials =
            new Dictionary<string, (Color, string, int)>
            {
                { "Oak Logs", (new Color(0.55f, 0.38f, 0.2f), "Lg", 4) },
                { "Willow Logs", (new Color(0.6f, 0.55f, 0.3f), "Lg", 9) },
                { "Yew Logs", (new Color(0.4f, 0.22f, 0.12f), "Lg", 20) },
                { "Copper Ore", (new Color(0.85f, 0.5f, 0.25f), "Or", 5) },
                { "Iron Ore", (new Color(0.55f, 0.35f, 0.3f), "Or", 11) },
                { "Mithril Ore", (new Color(0.35f, 0.5f, 0.95f), "Or", 24) },
                { "Raw Trout", (new Color(0.6f, 0.65f, 0.7f), "Fi", 3) },
                { "Raw Salmon", (new Color(0.95f, 0.55f, 0.45f), "Fi", 8) },
                { "Burnt Fish", (new Color(0.15f, 0.12f, 0.1f), "Fi", 1) },
            };

        public static Item Material(string name)
        {
            var m = materials.TryGetValue(name, out var d) ? d : (Color.gray, "?", 1);
            return new Item
            {
                Name = name, Kind = ItemKind.Material, MaxStack = 50, Value = m.Item3,
                IconColor = m.Item1, Icon = m.Item2
            };
        }

        public static Item Food(string name)
        {
            bool salmon = name.Contains("Salmon");
            return new Item
            {
                Name = name, Kind = ItemKind.Consumable, MaxStack = 50,
                Value = salmon ? 14 : 6, HealAmount = salmon ? 160 : 70,
                IconColor = salmon ? new Color(0.9f, 0.45f, 0.3f) : new Color(0.75f, 0.6f, 0.4f), Icon = "Fd",
                Flavor = salmon ? "Perfectly flaky." : "Smells like home."
            };
        }

        /// <summary>Food and drink sold at the inn.</summary>
        public static Item Provision(string name)
        {
            switch (name)
            {
                case "Bread": return new Item { Name = name, Kind = ItemKind.Consumable, MaxStack = 50, Value = 2, HealAmount = 45,
                    IconColor = new Color(0.8f, 0.6f, 0.35f), Icon = "Fd", Flavor = "Baked this morning. Probably." };
                case "Hearty Stew": return new Item { Name = name, Kind = ItemKind.Consumable, MaxStack = 20, Value = 12, HealAmount = 240,
                    IconColor = new Color(0.6f, 0.35f, 0.2f), Icon = "Fd", Flavor = "Rosie won't say what's in it." };
                case "Mulled Wine": return new Item { Name = name, Kind = ItemKind.Consumable, MaxStack = 20, Value = 6, ManaAmount = 50,
                    IconColor = new Color(0.55f, 0.1f, 0.25f), Icon = "Dr", Flavor = "Warms the soul and the spellbook." };
                default: return null;
            }
        }

        public static Item ByName(string name)
        {
            var provision = Provision(name);
            if (provision != null) return provision;
            if (name == "Health Potion") return HealthPotion();
            if (name == "Mana Potion") return ManaPotion();
            if (name.StartsWith("Cooked")) return Food(name);
            if (ItemPowers.ParseGem(name, out var gemType, out int gemTier)) return ItemPowers.Gem(gemType, gemTier);
            return Material(name);
        }

        public static Item StarterWeapon() => new Item
        {
            Name = "Worn Shortsword", BaseType = "Sword", Kind = ItemKind.Equipment, Slot = EquipSlot.Weapon,
            Rarity = Rarity.Common, ItemLevel = 1, RequiredLevel = 1, MinDamage = 4, MaxDamage = 8,
            AttacksPerSecond = 1.3f, Value = 3, IconColor = new Color(0.6f, 0.6f, 0.65f), Icon = "Sw",
            Flavor = "It has seen better days. So have you."
        };

        public static Item StarterChest() => new Item
        {
            Name = "Padded Tunic", BaseType = "Tunic", Kind = ItemKind.Equipment, Slot = EquipSlot.Chest,
            Rarity = Rarity.Common, ItemLevel = 1, RequiredLevel = 1, Armor = 6, Value = 2,
            IconColor = new Color(0.55f, 0.45f, 0.35f), Icon = "Ch"
        };

        // ---------------------------------------------------------------- random equipment

        static readonly EquipSlot[] slots =
        {
            EquipSlot.Weapon, EquipSlot.Weapon, EquipSlot.Helm, EquipSlot.Chest, EquipSlot.Gloves,
            EquipSlot.Legs, EquipSlot.Boots, EquipSlot.Ring, EquipSlot.Amulet
        };

        static readonly EquipSlot[] setSlots = { EquipSlot.Helm, EquipSlot.Chest, EquipSlot.Gloves, EquipSlot.Boots };

        static readonly string[][] weaponBases =
        {
            new[] { "Short Sword", "Hand Axe", "Club", "Dagger" },
            new[] { "Broadsword", "Battle Axe", "Flanged Mace", "Kris" },
            new[] { "Bastard Sword", "War Axe", "Morning Star", "Stiletto" },
            new[] { "Runeblade", "Executioner Axe", "Warhammer", "Soulreaver Dirk" },
        };

        static readonly Dictionary<EquipSlot, string[]> armorBases = new Dictionary<EquipSlot, string[]>
        {
            { EquipSlot.Helm, new[] { "Leather Cap", "Iron Helm", "Great Helm", "Runic Crown" } },
            { EquipSlot.Chest, new[] { "Leather Armor", "Chainmail", "Plate Mail", "Runic Plate" } },
            { EquipSlot.Gloves, new[] { "Leather Gloves", "Chain Gloves", "Gauntlets", "Runic Grips" } },
            { EquipSlot.Legs, new[] { "Leather Pants", "Chain Leggings", "Plate Greaves", "Runic Legplates" } },
            { EquipSlot.Boots, new[] { "Leather Boots", "Chain Boots", "Plate Sabatons", "Runic Treads" } },
            { EquipSlot.Ring, new[] { "Copper Ring", "Silver Ring", "Gold Ring", "Starmetal Ring" } },
            { EquipSlot.Amulet, new[] { "Bone Charm", "Silver Amulet", "Gold Amulet", "Starmetal Amulet" } },
        };

        static readonly Dictionary<Stat, string> prefixes = new Dictionary<Stat, string>
        {
            { Stat.Strength, "Mighty" }, { Stat.Dexterity, "Nimble" }, { Stat.Intelligence, "Arcane" },
            { Stat.Vitality, "Sturdy" }, { Stat.Health, "Vital" }, { Stat.Mana, "Mystic" }, { Stat.Armor, "Reinforced" },
            { Stat.CritChance, "Keen" }, { Stat.AttackSpeed, "Swift" }, { Stat.LifeOnHit, "Vampiric" },
            { Stat.MoveSpeed, "Fleet" }, { Stat.HealthRegen, "Rejuvenating" }, { Stat.ManaRegen, "Focused" },
        };

        static readonly Dictionary<Stat, string> suffixes = new Dictionary<Stat, string>
        {
            { Stat.Strength, "of the Bear" }, { Stat.Dexterity, "of the Fox" }, { Stat.Intelligence, "of the Owl" },
            { Stat.Vitality, "of the Ox" }, { Stat.Health, "of Life" }, { Stat.Mana, "of the Magi" }, { Stat.Armor, "of Warding" },
            { Stat.CritChance, "of Precision" }, { Stat.AttackSpeed, "of Haste" }, { Stat.LifeOnHit, "of the Leech" },
            { Stat.MoveSpeed, "of the Wind" }, { Stat.HealthRegen, "of the Troll" }, { Stat.ManaRegen, "of Clarity" },
        };

        static readonly string[] rareA = { "Grim", "Doom", "Storm", "Blood", "Dread", "Soul", "Shadow", "Bone", "Ash", "Raven", "Wraith", "Gloom" };
        static readonly string[] rareB = { "Bite", "Song", "Ward", "Grasp", "Fang", "Veil", "Crown", "Reaver", "Shell", "Mark", "Coil", "Spire" };

        static readonly Dictionary<EquipSlot, string[]> legendaryNames = new Dictionary<EquipSlot, string[]>
        {
            { EquipSlot.Weapon, new[] { "Dawnbreaker", "The Grave Whisper", "Emberheart", "Kingsbane" } },
            { EquipSlot.Helm, new[] { "Crown of the Fallen", "Visage of Night" } },
            { EquipSlot.Chest, new[] { "Aegis of Eternity", "Heart of the Mountain" } },
            { EquipSlot.Gloves, new[] { "Gauntlets of the Titan", "Hands of Ruin" } },
            { EquipSlot.Legs, new[] { "Legplates of the Abyss", "Stormstriders" } },
            { EquipSlot.Boots, new[] { "Windwalkers", "Boots of the Lost Road" } },
            { EquipSlot.Ring, new[] { "Band of Endless Night", "Ouroboros" } },
            { EquipSlot.Amulet, new[] { "Eye of the Storm", "Tear of the Moon" } },
        };

        static readonly string[] legendaryFlavor =
        {
            "Forged when the world was young.", "It whispers your name at night.",
            "Many have carried it. None have kept it.", "The light bends around it.",
        };

        public static Rarity RollRarity(float bonus)
        {
            float r = Random.value;
            if (r < 0.01f + bonus * 0.04f) return Rarity.Legendary;
            if (r < 0.02f + bonus * 0.08f) return Rarity.Set;
            if (r < 0.10f + bonus * 0.25f) return Rarity.Rare;
            if (r < 0.40f + bonus * 0.3f) return Rarity.Magic;
            return Rarity.Common;
        }

        /// <param name="rarityBonus">0 = normal drop, 1 = boss-quality.</param>
        public static Item RandomEquipment(int itemLevel, float rarityBonus = 0f, Rarity? forced = null, EquipSlot? forcedSlot = null)
        {
            itemLevel = Mathf.Max(1, itemLevel);
            var slot = forcedSlot ?? slots[Random.Range(0, slots.Length)];
            var rarity = forced ?? RollRarity(rarityBonus);
            if (rarity == Rarity.Set && forcedSlot == null) slot = setSlots[Random.Range(0, setSlots.Length)];
            if (rarity == Rarity.Set && System.Array.IndexOf(setSlots, slot) < 0) rarity = Rarity.Legendary;
            int tier = Mathf.Clamp(itemLevel / 6, 0, 3);

            var item = new Item
            {
                Kind = ItemKind.Equipment, Slot = slot, Rarity = rarity, ItemLevel = itemLevel,
                RequiredLevel = Mathf.Max(1, itemLevel - 2),
            };

            float rarityMul = rarity >= Rarity.Legendary ? 1.35f : rarity == Rarity.Rare ? 1.15f : 1f;

            if (slot == EquipSlot.Weapon)
            {
                int type = Random.Range(0, 4);
                item.BaseType = weaponBases[tier][type];
                float aps = type == 0 ? 1.3f : type == 1 ? 1.1f : type == 2 ? 1.0f : 1.6f;
                float dmgMul = type == 0 ? 1f : type == 1 ? 1.2f : type == 2 ? 1.35f : 0.75f;
                int min = Mathf.RoundToInt((3f + itemLevel * 1.3f) * dmgMul * rarityMul * Random.Range(0.85f, 1.15f));
                item.MinDamage = Mathf.Max(1, min);
                item.MaxDamage = Mathf.RoundToInt(item.MinDamage * Random.Range(1.5f, 2.0f)) + 2;
                item.AttacksPerSecond = aps;
                item.Icon = type == 0 ? "Sw" : type == 1 ? "Ax" : type == 2 ? "Mc" : "Dg";
            }
            else
            {
                item.BaseType = armorBases[slot][tier];
                float slotMul = slot == EquipSlot.Chest ? 1f : slot == EquipSlot.Legs ? 0.8f :
                    slot == EquipSlot.Helm ? 0.6f : slot == EquipSlot.Ring || slot == EquipSlot.Amulet ? 0f : 0.4f;
                item.Armor = Mathf.RoundToInt((4f + itemLevel * 1.6f) * slotMul * rarityMul * Random.Range(0.85f, 1.15f));
                item.Icon = SlotIcon(slot);
            }

            int affixes = rarity == Rarity.Common ? 0 : rarity == Rarity.Magic ? Random.Range(1, 3) :
                rarity == Rarity.Rare ? Random.Range(3, 5) : rarity == Rarity.Set ? 4 : 5;
            if ((slot == EquipSlot.Ring || slot == EquipSlot.Amulet) && affixes < 5) affixes++;

            var pool = new List<Stat>((Stat[])System.Enum.GetValues(typeof(Stat)));
            for (int i = 0; i < affixes && pool.Count > 0; i++)
            {
                int pick = Random.Range(0, pool.Count);
                var stat = pool[pick];
                pool.RemoveAt(pick);
                item.Mods.Add(new StatMod(stat, Mathf.Max(1, Mathf.RoundToInt(RollStat(stat, itemLevel) * rarityMul))));
            }

            // Name
            switch (rarity)
            {
                case Rarity.Common:
                    item.Name = item.BaseType;
                    break;
                case Rarity.Magic:
                    item.Name = prefixes[item.Mods[0].Stat] + " " + item.BaseType +
                                (item.Mods.Count > 1 ? " " + suffixes[item.Mods[1].Stat] : "");
                    break;
                case Rarity.Rare:
                    item.Name = rareA[Random.Range(0, rareA.Length)] + " " + rareB[Random.Range(0, rareB.Length)];
                    break;
                case Rarity.Legendary:
                    var names = legendaryNames[slot];
                    item.Name = names[Random.Range(0, names.Length)];
                    item.Flavor = legendaryFlavor[Random.Range(0, legendaryFlavor.Length)];
                    ItemPowers.GiveLegendaryPower(item, Player.I != null ? Player.I.Look : null);
                    break;
                case Rarity.Set:
                    ItemPowers.MakeSetPiece(item, Player.I != null ? Player.I.Look : null);
                    break;
            }

            // Sockets: weapons, helms, chests and legs can roll up to two.
            if (slot == EquipSlot.Weapon || slot == EquipSlot.Helm || slot == EquipSlot.Chest || slot == EquipSlot.Legs)
            {
                float r = Random.value, bonus = rarity >= Rarity.Rare ? 0.15f : 0f;
                item.Sockets = r < 0.06f + bonus ? 2 : r < 0.3f + bonus ? 1 : 0;
            }

            int rarityValue = rarity == Rarity.Common ? 1 : rarity == Rarity.Magic ? 3 : rarity == Rarity.Rare ? 7 : 20;
            item.Value = Mathf.Max(1, (3 + itemLevel * 2) * rarityValue);
            item.IconColor = IconTint(slot, tier);
            return item;
        }

        static float RollStat(Stat s, int ilvl)
        {
            switch (s)
            {
                case Stat.Strength:
                case Stat.Dexterity:
                case Stat.Intelligence:
                case Stat.Vitality: return Random.Range(1f, 3f + ilvl * 0.5f);
                case Stat.Health: return Random.Range(5f, 12f + ilvl * 3f);
                case Stat.Mana: return Random.Range(5f, 10f + ilvl * 2f);
                case Stat.Armor: return Random.Range(2f, 5f + ilvl);
                case Stat.CritChance: return Random.Range(1f, 3f + ilvl / 8f);
                case Stat.AttackSpeed: return Random.Range(3f, 8f + ilvl / 4f);
                case Stat.LifeOnHit: return Random.Range(1f, 2f + ilvl / 4f);
                case Stat.MoveSpeed: return Random.Range(3f, 9f);
                case Stat.HealthRegen: return Random.Range(1f, 2f + ilvl / 5f);
                case Stat.ManaRegen: return Random.Range(1f, 2f + ilvl / 6f);
                default: return 1f;
            }
        }

        static string SlotIcon(EquipSlot s)
        {
            switch (s)
            {
                case EquipSlot.Helm: return "He";
                case EquipSlot.Chest: return "Ch";
                case EquipSlot.Gloves: return "Gl";
                case EquipSlot.Legs: return "Lg";
                case EquipSlot.Boots: return "Bt";
                case EquipSlot.Ring: return "Rg";
                case EquipSlot.Amulet: return "Am";
                default: return "?";
            }
        }

        static Color IconTint(EquipSlot slot, int tier)
        {
            if (slot == EquipSlot.Ring || slot == EquipSlot.Amulet)
                return tier == 0 ? new Color(0.8f, 0.5f, 0.3f) : tier == 1 ? new Color(0.8f, 0.8f, 0.85f) :
                    tier == 2 ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.5f, 0.7f, 1f);
            return tier == 0 ? new Color(0.55f, 0.42f, 0.3f) : tier == 1 ? new Color(0.6f, 0.62f, 0.65f) :
                tier == 2 ? new Color(0.78f, 0.8f, 0.85f) : new Color(0.45f, 0.6f, 0.9f);
        }
    }
}
