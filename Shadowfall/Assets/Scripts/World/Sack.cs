using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// A lost siege (server/invasion.js): the invaders set fire to the quarter behind the gate they broke. For five minutes
    /// the houses there burn (flames on the roofs, smoke, the roar of it), the people flee (see Npc: the merchants,
    /// smiths and auctioneers of the quarter are gone, and the server refuses their trade there) and the townsfolk keep
    /// indoors. When the fires are out, everyone comes back.
    /// </summary>
    public class Sack : MonoBehaviour
    {
        class Burning
        {
            public string Town, Gate;
            public Vector3 At;
            public float Radius, Until;
            public Transform Root;   // the fires, their lights and the roar (all gone with it)
            public bool Told;
        }

        static readonly Dictionary<string, Burning> burning = new Dictionary<string, Burning>();
        static Sack runner;
        const int MaxHouses = 9;
        static readonly Color Flame = new Color(1f, 0.5f, 0.15f);

        /// <summary>The server's list of burning quarters (sent when one starts or ends, and at login).</summary>
        public static void Set(NetSack[] list)
        {
            if (runner == null) runner = new GameObject("Sack").AddComponent<Sack>();
            var keep = new HashSet<string>();
            if (list != null)
                foreach (var s in list)
                {
                    if (s == null || string.IsNullOrEmpty(s.k)) continue;
                    keep.Add(s.k);
                    if (burning.TryGetValue(s.k, out var b)) { b.Until = Time.time + s.left; continue; }
                    Ignite(s);
                }
            var gone = new List<string>();
            foreach (var kv in burning) if (!keep.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var k in gone) Extinguish(k);
        }

        /// <summary>In a burning quarter: its people have fled.</summary>
        public static bool At(Vector3 p)
        {
            foreach (var b in burning.Values)
                if (Factory.FlatDistance(p, b.At) < b.Radius && TownIs(p, b.Town)) return true;
            return false;
        }

        /// <summary>Some quarter of this town is burning (the townsfolk keep indoors).</summary>
        public static bool Burns(string town) => town != null && burning.ContainsKey(town);

        static bool TownIs(Vector3 p, string town)
        {
            var t = WorldGenerator.TownAt(p);
            return t != null && t.Name == town;
        }

        static void Ignite(NetSack s)
        {
            var b = new Burning
            {
                Town = s.k, Gate = s.g, At = new Vector3(s.x, 0f, s.z), Radius = s.r, Until = Time.time + s.left,
                Root = new GameObject("Burning " + s.k).transform,
            };
            burning[s.k] = b;
            // The houses of the quarter, nearest the gate first
            var houses = new List<Bounds>();
            foreach (var h in WorldGenerator.HouseBounds)
                if (Factory.FlatDistance(h.center, b.At) < b.Radius && TownIs(h.center, s.k)) houses.Add(h);
            houses.Sort((x, y) => Factory.FlatDistance(x.center, b.At).CompareTo(Factory.FlatDistance(y.center, b.At)));
            var rng = new System.Random(s.k.GetHashCode() ^ s.g.GetHashCode());
            float R(float a, float c) => a + (float)rng.NextDouble() * (c - a);
            for (int i = 0; i < houses.Count && i < MaxHouses; i++)
            {
                var h = houses[i];
                float top = h.max.y, size = Mathf.Clamp(Mathf.Max(h.size.x, h.size.z) * 0.22f, 0.9f, 2.2f);
                // the roof ablaze, with one light for the house
                var light = new GameObject("FireLight").AddComponent<Light>();
                light.transform.SetParent(b.Root, false);
                light.transform.position = new Vector3(h.center.x, top * 0.75f, h.center.z);
                light.type = LightType.Point;
                light.color = Flame;
                light.range = Mathf.Max(h.size.x, h.size.z) * 1.4f + 4f;
                light.intensity = 2.2f;
                light.shadows = LightShadows.None;
                PropFire.Add(b.Root, new Vector3(h.center.x, top * 0.72f, h.center.z), Flame, size * 1.4f, true, light);
                // and flames along the eaves
                int more = 2 + rng.Next(2);
                for (int j = 0; j < more; j++)
                {
                    var at = new Vector3(R(h.min.x + 0.4f, h.max.x - 0.4f), top * R(0.4f, 0.6f), R(h.min.z + 0.4f, h.max.z - 0.4f));
                    PropFire.Add(b.Root, at, Flame, size * R(0.7f, 1f), j == 0);
                }
                var roar = Sfx.LoopAt("fire_loop", h.center + Vector3.up * 2f, 0.55f, 32f);
                if (roar != null) { roar.transform.SetParent(b.Root, true); }
            }
            var hero = Player.I;
            if (hero != null && Factory.FlatDistance(hero.transform.position, b.At) < b.Radius + 40f)
                Sfx.Play("boom", b.At + Vector3.up * 2f, 0.6f, 0.1f, 80f);
        }

        static void Extinguish(string town)
        {
            if (!burning.TryGetValue(town, out var b)) return;
            burning.Remove(town);
            if (b.Root == null) return;
            // The flames die down; a last puff of smoke where each one was
            foreach (var f in b.Root.GetComponentsInChildren<PropFire>())
                if (f != null && SpellFx.Ready) SpellFx.Dust(f.transform.position, 1.2f, new Color(0.3f, 0.3f, 0.32f));
            Destroy(b.Root.gameObject);
        }

        float nextLook;

        void Update()
        {
            if (Time.time < nextLook || burning.Count == 0) return;
            nextLook = Time.time + 1f;
            var hero = Player.I;
            if (hero == null) return;
            foreach (var b in burning.Values)
            {
                if (b.Told || Dungeon.Active) continue;
                if (Factory.FlatDistance(hero.transform.position, b.At) < b.Radius && TownIs(hero.transform.position, b.Town))
                {
                    b.Told = true;
                    int left = Mathf.Max(0, Mathf.CeilToInt(b.Until - Time.time));
                    GameUI.Log("The " + b.Gate + " quarter of " + b.Town + " is burning. Its merchants have fled; they'll be back when the fires are out (" +
                               (left / 60) + ":" + (left % 60).ToString("00") + ").", new Color(1f, 0.55f, 0.3f));
                }
            }
        }
    }
}
