namespace Shadowfall
{
    /// <summary>
    /// Salvaging and reforging at a blacksmith (the server does it: server/items.js salvageYield, reforgeCost, reforge;
    /// these numbers only tell the player what it will take).
    /// </summary>
    public static class Forge
    {
        public static readonly string[] Materials = { "Scrap Iron", "Arcane Dust", "Veiled Crystal", "Forgotten Soul" };

        /// <summary>What salvaging gives, roughly (the counts vary a little).</summary>
        public static string YieldText(Item it)
        {
            switch (it.Rarity)
            {
                case Rarity.Common: return "Scrap Iron";
                case Rarity.Magic: return "Arcane Dust and Scrap Iron";
                case Rarity.Rare: return "a Veiled Crystal and Arcane Dust";
                default: return "a Forgotten Soul and Veiled Crystals";
            }
        }

        public static bool Reforgeable(Item it) => it != null && it.Kind == ItemKind.Equipment && it.Rarity != Rarity.Common && it.Mods.Count > 0;

        /// <summary>Whether affix <paramref name="index"/> may be reforged: once one has been, only that one can again.</summary>
        public static bool CanReforge(Item it, int index) => Reforgeable(it) && (it.Reforged == 0 || it.Reforged == index + 1);

        public static int Gold(Item it) => System.Math.Max(25, (int)System.Math.Round(it.Value * 1.5));

        public static (string name, int n)[] Cost(Item it)
        {
            switch (it.Rarity)
            {
                case Rarity.Magic: return new[] { ("Arcane Dust", 3) };
                case Rarity.Rare: return new[] { ("Veiled Crystal", 2), ("Arcane Dust", 4) };
                default: return new[] { ("Forgotten Soul", 1), ("Veiled Crystal", 2) };
            }
        }

        public static bool CanAfford(Player p, Item it)
        {
            if (p.Gold < Gold(it)) return false;
            foreach (var (name, n) in Cost(it)) if (p.Inventory.CountOf(name) < n) return false;
            return true;
        }

        public static string CostText(Player p, Item it)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (name, n) in Cost(it))
            {
                bool have = p.Inventory.CountOf(name) >= n;
                sb.Append(have ? "" : "<color=#ff7a5c>").Append(n).Append(' ').Append(name).Append(have ? "" : "</color>").Append(",  ");
            }
            bool gold = p.Gold >= Gold(it);
            sb.Append(gold ? "<color=#f0c45a>" : "<color=#ff7a5c>").Append(Gold(it)).Append(" gold</color>");
            return sb.ToString();
        }
    }
}
