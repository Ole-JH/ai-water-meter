using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    public enum VendorKind { General, Armor, Weapons, Food, Curios, Companions }

    /// <summary>
    /// What a vendor sells, as the server has it for this hero (vendor stock in server/items.js): equipment is rolled
    /// for the hero's level and restocked every 10 minutes. Every vendor of a kind shares one stock.
    /// </summary>
    public class VendorStock
    {
        static readonly Dictionary<VendorKind, VendorStock> byKind = new Dictionary<VendorKind, VendorStock>();

        public readonly VendorKind Kind;
        public readonly List<Item> Items = new List<Item>();
        float restockAt = -1f, askedAt = -99f;

        VendorStock(VendorKind kind) { Kind = kind; }

        public static VendorStock For(VendorKind kind)
        {
            if (!byKind.TryGetValue(kind, out var s)) byKind[kind] = s = new VendorStock(kind);
            return s;
        }

        public float SecondsUntilRestock => Mathf.Max(0f, restockAt - Time.time);
        public bool Rotates => Kind == VendorKind.Armor || Kind == VendorKind.Weapons || Kind == VendorKind.Curios;
        public bool Loaded => restockAt > 0f;

        /// <summary>Asks the server for the current stock (when opening the shop, or when it's time to restock).</summary>
        public void Refresh(bool force = false)
        {
            if (Kind == VendorKind.Companions || Time.time - askedAt < 2f) return;
            if (!force && Loaded && Time.time < restockAt) return;
            askedAt = Time.time;
            NetClient.I?.Op("vendor", k: Kind.ToString());
        }

        /// <summary>The server's answer to "vendor" (and after every purchase).</summary>
        public static void OnStock(string kind, Item[] items, int restockSeconds)
        {
            if (!System.Enum.TryParse(kind, out VendorKind k)) return;
            var s = For(k);
            s.Items.Clear();
            if (items != null) foreach (var it in items) if (it != null && !string.IsNullOrEmpty(it.Name)) s.Items.Add(it);
            s.restockAt = Time.time + Mathf.Max(5, restockSeconds);
        }

        /// <summary>The merchants' price factor where we shop: the town's prosperity (server/invasion.js), 1 = normal.</summary>
        public static float PriceMul = 1f;

        /// <summary>What a merchant here charges: the base price times the town's factor (the server charges the same).</summary>
        public static int Price(Item it) => Mathf.Max(1, Mathf.FloorToInt(BasePrice(it) * PriceMul + 0.5f)); // (rounded as the server does)

        /// <summary>Buying costs more than the item sells for (the server charges the same: price() in server/items.js).</summary>
        public static int BasePrice(Item it)
        {
            if (it.Kind != ItemKind.Equipment) return it.Name.Contains("Potion") ? 25 : Mathf.Max(3, it.Value * 3);
            int mul = it.Rarity == Rarity.Rare ? 5 : 4;
            return Mathf.Max(10, it.Value * mul);
        }
    }
}
