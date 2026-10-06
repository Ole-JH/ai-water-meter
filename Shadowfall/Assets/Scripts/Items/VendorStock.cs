using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    public enum VendorKind { General, Armor, Weapons, Food, Curios }

    /// <summary>
    /// What a vendor sells. Equipment stock is rolled for the hero's level and restocked every
    /// <see cref="RestockMinutes"/> minutes (or when the hero has outgrown it).
    /// </summary>
    public class VendorStock
    {
        public const float RestockMinutes = 10f;
        public readonly VendorKind Kind;
        public readonly List<Item> Items = new List<Item>();
        float restockAt = -1f;
        int stockedForLevel;

        static readonly EquipSlot[] armorSlots = { EquipSlot.Helm, EquipSlot.Chest, EquipSlot.Gloves, EquipSlot.Legs, EquipSlot.Boots };

        public VendorStock(VendorKind kind) { Kind = kind; }

        public float SecondsUntilRestock => Mathf.Max(0f, restockAt - Time.time);
        public bool Rotates => Kind == VendorKind.Armor || Kind == VendorKind.Weapons || Kind == VendorKind.Curios;

        public void Refresh(int playerLevel)
        {
            if (restockAt > 0f && Time.time < restockAt && Mathf.Abs(playerLevel - stockedForLevel) < 2) return;
            restockAt = Time.time + RestockMinutes * 60f;
            stockedForLevel = playerLevel;
            Items.Clear();
            switch (Kind)
            {
                case VendorKind.General:
                    Items.Add(ItemDatabase.HealthPotion());
                    Items.Add(ItemDatabase.ManaPotion());
                    break;
                case VendorKind.Food:
                    Items.Add(ItemDatabase.Provision("Bread"));
                    Items.Add(ItemDatabase.Food("Cooked Trout"));
                    Items.Add(ItemDatabase.Food("Cooked Salmon"));
                    Items.Add(ItemDatabase.Provision("Hearty Stew"));
                    Items.Add(ItemDatabase.Provision("Mulled Wine"));
                    break;
                case VendorKind.Armor:
                    for (int i = 0; i < 6; i++)
                        Items.Add(ItemDatabase.RandomEquipment(playerLevel + Random.Range(-1, 2), 0f,
                            Random.value < 0.35f ? Rarity.Magic : Rarity.Common, armorSlots[Random.Range(0, armorSlots.Length)]));
                    break;
                case VendorKind.Weapons:
                    for (int i = 0; i < 6; i++)
                        Items.Add(ItemDatabase.RandomEquipment(playerLevel + Random.Range(-1, 2), 0f,
                            Random.value < 0.35f ? Rarity.Magic : Rarity.Common, EquipSlot.Weapon));
                    break;
                case VendorKind.Curios:
                    for (int i = 0; i < 5; i++)
                        Items.Add(ItemDatabase.RandomEquipment(playerLevel + Random.Range(0, 2), 0f,
                            Random.value < 0.25f ? Rarity.Rare : Rarity.Magic, Random.value < 0.5f ? EquipSlot.Ring : EquipSlot.Amulet));
                    break;
            }
        }

        /// <summary>Buying costs more than the item sells for.</summary>
        public static int Price(Item it)
        {
            if (it.Kind != ItemKind.Equipment) return it.Name.Contains("Potion") ? 25 : Mathf.Max(3, it.Value * 3);
            int mul = it.Rarity == Rarity.Rare ? 5 : 4;
            return Mathf.Max(10, it.Value * mul);
        }
    }
}
