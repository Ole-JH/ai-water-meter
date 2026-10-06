using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>A unique effect carried by a legendary item.</summary>
    public class PowerDef
    {
        public string Id, Text, Class; // Class = null: anyone benefits
    }

    /// <summary>A four-piece class set with a 2-piece and a 4-piece bonus.</summary>
    public class SetDef
    {
        public string Id, Name, Class, Bonus2, Bonus4;
        public Dictionary<EquipSlot, string> Pieces;
    }

    /// <summary>Legendary powers, item sets and gems, and how they change the hero.</summary>
    public static class ItemPowers
    {
        public static readonly PowerDef[] Powers =
        {
            new PowerDef { Id = "fireball_split", Class = "Mage", Text = "Fireball splits into three fireballs." },
            new PowerDef { Id = "whirl_slow", Class = "Barbarian", Text = "Whirlwind slows everything it hits." },
            new PowerDef { Id = "multishot_plus", Class = "Rogue", Text = "Multishot fires 3 additional arrows." },
            new PowerDef { Id = "holy_stun", Class = "Knight", Text = "Divine Shield stuns all nearby enemies for 2 seconds." },
            new PowerDef { Id = "explode_kill", Text = "Enemies you kill explode, dealing 60% weapon damage to everything nearby." },
            new PowerDef { Id = "heal_kill", Text = "Killing an enemy heals you for 5% of your maximum life." },
            new PowerDef { Id = "chain_proc", Text = "Every 5th hit unleashes a bolt of chain lightning." },
            new PowerDef { Id = "elitebane", Text = "+25% damage against elites and bosses." },
        };

        public static PowerDef Power(string id)
        {
            foreach (var p in Powers) if (p.Id == id) return p;
            return null;
        }

        public static readonly SetDef[] Sets =
        {
            new SetDef
            {
                Id = "lightbringer", Name = "Lightbringer's Oath", Class = "Knight",
                Bonus2 = "+15% armor and +10% maximum life", Bonus4 = "Holy Bolt, Consecration and Judgement deal 40% more damage",
                Pieces = new Dictionary<EquipSlot, string>
                {
                    { EquipSlot.Helm, "Lightbringer's Visage" }, { EquipSlot.Chest, "Lightbringer's Cuirass" },
                    { EquipSlot.Gloves, "Lightbringer's Gauntlets" }, { EquipSlot.Boots, "Lightbringer's Greaves" },
                },
            },
            new SetDef
            {
                Id = "ancients", Name = "Wrath of the Ancients", Class = "Barbarian",
                Bonus2 = "+12% attack speed", Bonus4 = "Whirlwind deals 60% more damage and lasts 1 second longer",
                Pieces = new Dictionary<EquipSlot, string>
                {
                    { EquipSlot.Helm, "Ancient's Horned Helm" }, { EquipSlot.Chest, "Ancient's Hauberk" },
                    { EquipSlot.Gloves, "Ancient's Fists" }, { EquipSlot.Boots, "Ancient's Stride" },
                },
            },
            new SetDef
            {
                Id = "tempest", Name = "Regalia of the Tempest", Class = "Mage",
                Bonus2 = "+30% mana regeneration", Bonus4 = "Chain Lightning jumps 3 more times and deals 30% more damage",
                Pieces = new Dictionary<EquipSlot, string>
                {
                    { EquipSlot.Helm, "Tempest Cowl" }, { EquipSlot.Chest, "Tempest Robes" },
                    { EquipSlot.Gloves, "Tempest Wraps" }, { EquipSlot.Boots, "Tempest Slippers" },
                },
            },
            new SetDef
            {
                Id = "nightstalker", Name = "Nightstalker's Garb", Class = "Rogue",
                Bonus2 = "+8% critical hit chance", Bonus4 = "Multishot and Rain of Arrows deal 50% more damage",
                Pieces = new Dictionary<EquipSlot, string>
                {
                    { EquipSlot.Helm, "Nightstalker's Hood" }, { EquipSlot.Chest, "Nightstalker's Jerkin" },
                    { EquipSlot.Gloves, "Nightstalker's Grips" }, { EquipSlot.Boots, "Nightstalker's Treads" },
                },
            },
        };

        public static SetDef Set(string id)
        {
            foreach (var s in Sets) if (s.Id == id) return s;
            return null;
        }

        public static int SetCount(Player p, string setId)
        {
            int n = 0;
            foreach (var kv in p.Inventory.Equipped) if (kv.Value != null && kv.Value.Set == setId) n++;
            return n;
        }

        /// <summary>True when an equipped legendary carries the power, or a full set grants it ("set_&lt;id&gt;").</summary>
        public static bool Has(Player p, string id)
        {
            if (id.StartsWith("set_")) return SetCount(p, id.Substring(4)) >= 4;
            foreach (var kv in p.Inventory.Equipped) if (kv.Value != null && kv.Value.Power == id) return true;
            return false;
        }

        public static float Value(Player p, string id)
        {
            int n = 0;
            foreach (var kv in p.Inventory.Equipped) if (kv.Value != null && kv.Value.Power == id) n++;
            return id == "elitebane" ? 0.25f * n : n;
        }

        /// <summary>2-piece set bonuses, applied at the end of <see cref="Player.RecalculateStats"/>.</summary>
        public static void ApplySetBonuses(Player p)
        {
            if (SetCount(p, "lightbringer") >= 2) { p.ArmorValue *= 1.15f; p.MaxHealth *= 1.1f; }
            if (SetCount(p, "ancients") >= 2) p.AttackSpeed *= 1.12f;
            if (SetCount(p, "tempest") >= 2) p.ManaRegen *= 1.3f;
            if (SetCount(p, "nightstalker") >= 2) p.CritChance = Mathf.Min(75f, p.CritChance + 8f);
        }

        // ---- procs

        static int hitCounter;
        static bool inProc;
        static readonly List<Combatant> buffer = new List<Combatant>();

        public static void OnHit(Player p, Combatant target)
        {
            if (inProc || !Has(p, "chain_proc") || ++hitCounter % 5 != 0) return;
            inProc = true;
            try
            {
                var hit = new List<Combatant> { target };
                var from = target.Center;
                float dmg = (p.MinDamage + p.MaxDamage) * 0.5f * p.MeleeMultiplier * 0.8f + 6f * p.SpellMultiplier;
                for (int i = 0; i < 3; i++)
                {
                    Combatant next = null;
                    float best = 7f;
                    foreach (var c in Combatant.All)
                    {
                        if (c.IsDead || c.Faction == p.Faction || hit.Contains(c)) continue;
                        float d = Factory.FlatDistance(c.transform.position, target.transform.position);
                        if (d < best) { best = d; next = c; }
                    }
                    if (next == null) break;
                    AbilityFx.Lightning(from, next.Center);
                    p.DealDamage(next, dmg, false);
                    hit.Add(next);
                    from = next.Center;
                }
            }
            finally { inProc = false; }
        }

        public static void OnCast(Player p, AbilityId id, Vector3 aim)
        {
            if (id == AbilityId.DivineShield && Has(p, "holy_stun"))
            {
                Combatant.Overlap(p.transform.position, 6f, p.Faction, buffer);
                foreach (var c in buffer) if (c is Enemy e) e.Stun(2f);
                SpellFx.Ring(p.transform.position, AbilityFx.Gold, 6f, 0.6f);
            }
        }

        /// <summary>A monster we helped kill died at <paramref name="pos"/>.</summary>
        public static void OnKill(Player p, Vector3 pos)
        {
            if (p == null || p.IsDead || Factory.FlatDistance(p.transform.position, pos) > 20f) return;
            if (Has(p, "heal_kill")) p.Heal(p.MaxHealth * 0.05f);
            if (Has(p, "explode_kill"))
            {
                SpellFx.Explosion(pos + Vector3.up * 0.5f, new Color(0.9f, 0.3f, 0.6f), 2.5f, false);
                Sfx.Play("explosion", pos, 0.5f, 0.15f);
                Combatant.Overlap(pos, 3f, p.Faction, buffer);
                float dmg = (p.MinDamage + p.MaxDamage) * 0.5f * p.MeleeMultiplier * 0.6f;
                foreach (var c in buffer.ToArray()) p.DealDamage(c, dmg, false);
            }
        }

        // ---- loot

        /// <summary>Turns a freshly rolled legendary into one with a power (favouring the hero's class).</summary>
        public static void GiveLegendaryPower(Item item, string heroClass)
        {
            var pool = new List<PowerDef>();
            foreach (var pw in Powers) if (pw.Class == null || pw.Class == heroClass) pool.Add(pw);
            item.Power = pool[Random.Range(0, pool.Count)].Id;
        }

        /// <summary>Turns a freshly rolled armor piece into a set piece (usually of the hero's class).</summary>
        public static bool MakeSetPiece(Item item, string heroClass)
        {
            SetDef set = null;
            if (Random.value < 0.75f) set = System.Array.Find(Sets, s => s.Class == heroClass);
            if (set == null) set = Sets[Random.Range(0, Sets.Length)];
            if (!set.Pieces.TryGetValue(item.Slot, out var name)) return false;
            item.Set = set.Id;
            item.Name = name;
            item.Rarity = Rarity.Set;
            return true;
        }

        // ---- gems

        public static readonly string[] GemTypes = { "Ruby", "Emerald", "Sapphire", "Topaz", "Diamond" };
        public static readonly string[] GemTiers = { "Chipped", "Flawless", "Perfect" };
        static readonly int[] tierValue = { 3, 6, 10 };

        public static Stat GemStat(string type)
        {
            switch (type)
            {
                case "Ruby": return Stat.Strength;
                case "Emerald": return Stat.Dexterity;
                case "Sapphire": return Stat.Intelligence;
                case "Topaz": return Stat.Vitality;
                default: return Stat.Health;
            }
        }

        public static Color GemColor(string type)
        {
            switch (type)
            {
                case "Ruby": return new Color(1f, 0.25f, 0.25f);
                case "Emerald": return new Color(0.3f, 1f, 0.4f);
                case "Sapphire": return new Color(0.35f, 0.55f, 1f);
                case "Topaz": return new Color(1f, 0.8f, 0.25f);
                default: return new Color(0.95f, 0.95f, 1f);
            }
        }

        /// <summary>Parses "Flawless Ruby" into type and tier (0-2). False if the name is not a gem.</summary>
        public static bool ParseGem(string name, out string type, out int tier)
        {
            type = null;
            tier = -1;
            if (string.IsNullOrEmpty(name)) return false;
            var parts = name.Split(' ');
            if (parts.Length != 2) return false;
            tier = System.Array.IndexOf(GemTiers, parts[0]);
            type = parts[1];
            return tier >= 0 && System.Array.IndexOf(GemTypes, type) >= 0;
        }

        public static StatMod GemMod(string gemName)
        {
            if (!ParseGem(gemName, out var type, out int tier)) return new StatMod(Stat.Armor, 0);
            int v = tierValue[tier];
            return new StatMod(GemStat(type), type == "Diamond" ? v * 4 : v);
        }

        public static Item Gem(string type, int tier)
        {
            tier = Mathf.Clamp(tier, 0, 2);
            var mod = GemMod(GemTiers[tier] + " " + type);
            return new Item
            {
                Name = GemTiers[tier] + " " + type, BaseType = "Gem", Kind = ItemKind.Gem, MaxStack = 50,
                Value = tier == 0 ? 15 : tier == 1 ? 60 : 220, IconColor = GemColor(type), Icon = "gem",
                Flavor = "Socket into an item: " + Item.StatText(mod.Stat, mod.Value) + ".",
            };
        }

        public static Item RandomGem(int monsterLevel)
        {
            int tier = monsterLevel >= 14 && Random.value < 0.3f ? 1 : 0;
            return Gem(GemTypes[Random.Range(0, GemTypes.Length)], tier);
        }
    }
}
