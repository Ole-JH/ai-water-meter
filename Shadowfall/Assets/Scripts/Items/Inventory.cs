using System.Collections.Generic;

namespace Shadowfall
{
    public class Inventory
    {
        public readonly Item[] Slots;
        public readonly Dictionary<EquipSlot, Item> Equipped = new Dictionary<EquipSlot, Item>();
        public event System.Action Changed;

        public Inventory(int size) { Slots = new Item[size]; }

        public void NotifyChanged() => Changed?.Invoke();

        public int FreeSlots
        {
            get
            {
                int n = 0;
                foreach (var s in Slots) if (s == null) n++;
                return n;
            }
        }

        /// <summary>Adds an item, stacking when possible. Returns false if there was no room.</summary>
        public bool Add(Item item)
        {
            if (item.Stackable)
            {
                for (int i = 0; i < Slots.Length && item.Count > 0; i++)
                {
                    var s = Slots[i];
                    if (s == null || s.Name != item.Name || s.Count >= s.MaxStack) continue;
                    int move = System.Math.Min(item.Count, s.MaxStack - s.Count);
                    s.Count += move;
                    item.Count -= move;
                }
                if (item.Count <= 0) { NotifyChanged(); return true; }
            }
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] != null) continue;
                Slots[i] = item;
                NotifyChanged();
                return true;
            }
            NotifyChanged();
            return false;
        }

        /// <summary>
        /// Tidies the bags: merges stacks, then orders equipment (best rarity first, by slot), gems, potions and
        /// food, then materials. Empty slots end up at the back.
        /// </summary>
        public void Sort()
        {
            var items = new List<Item>();
            foreach (var s in Slots) if (s != null) items.Add(s);
            for (int i = 0; i < Slots.Length; i++) Slots[i] = null;
            // merge stacks
            var merged = new List<Item>();
            foreach (var it in items)
            {
                if (it.Stackable)
                    foreach (var m in merged)
                    {
                        if (m.Name != it.Name || m.Count >= m.MaxStack) continue;
                        int move = System.Math.Min(it.Count, m.MaxStack - m.Count);
                        m.Count += move;
                        it.Count -= move;
                        if (it.Count <= 0) break;
                    }
                if (it.Count > 0) merged.Add(it);
            }
            int Group(Item it) => it.Kind == ItemKind.Equipment ? 0 : it.Kind == ItemKind.Gem ? 1 : it.Kind == ItemKind.Consumable ? 2 : 3;
            merged.Sort((a, b) =>
            {
                int c = Group(a).CompareTo(Group(b));
                if (c != 0) return c;
                if (a.Kind == ItemKind.Equipment)
                {
                    c = b.Rarity.CompareTo(a.Rarity);
                    if (c != 0) return c;
                    c = a.Slot.CompareTo(b.Slot);
                    if (c != 0) return c;
                    c = b.ItemLevel.CompareTo(a.ItemLevel);
                    if (c != 0) return c;
                }
                return string.CompareOrdinal(a.Name, b.Name);
            });
            for (int i = 0; i < merged.Count && i < Slots.Length; i++) Slots[i] = merged[i];
            NotifyChanged();
        }

        public int CountOf(string name)
        {
            int n = 0;
            foreach (var s in Slots) if (s != null && s.Name == name) n += s.Count;
            return n;
        }

        public bool Remove(string name, int count)
        {
            if (CountOf(name) < count) return false;
            for (int i = Slots.Length - 1; i >= 0 && count > 0; i--)
            {
                var s = Slots[i];
                if (s == null || s.Name != name) continue;
                int take = System.Math.Min(count, s.Count);
                s.Count -= take;
                count -= take;
                if (s.Count <= 0) Slots[i] = null;
            }
            NotifyChanged();
            return true;
        }

        /// <summary>Removes one unit from the stack at index and returns it.</summary>
        public Item TakeOne(int index)
        {
            var s = Slots[index];
            if (s == null) return null;
            Item one;
            if (s.Count > 1)
            {
                s.Count--;
                one = s.CloneSingle();
            }
            else
            {
                Slots[index] = null;
                one = s;
            }
            NotifyChanged();
            return one;
        }

        public Item TakeAll(int index)
        {
            var s = Slots[index];
            Slots[index] = null;
            NotifyChanged();
            return s;
        }

        public int IndexOf(string name)
        {
            for (int i = 0; i < Slots.Length; i++) if (Slots[i] != null && Slots[i].Name == name) return i;
            return -1;
        }

        public Item GetEquipped(EquipSlot slot) => Equipped.TryGetValue(slot, out var it) ? it : null;
    }
}
