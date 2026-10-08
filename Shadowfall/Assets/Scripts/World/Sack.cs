using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// A lost siege (server/invasion.js): the invaders set fire to the quarter behind the gate they broke. For twelve minutes (less as heroes help the reeve)
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
        const int MaxHouses = 12;
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

        /// <summary>Seconds until the fires of a town are out (0: not burning).</summary>
        public static int SecondsLeft(string town) => town != null && burning.TryGetValue(town, out var b) ? Mathf.Max(0, Mathf.CeilToInt(b.Until - Time.time)) : 0;

        /// <summary>The quarter that burns in a town ("west"), or null.</summary>
        public static string GateOf(string town) => town != null && burning.TryGetValue(town, out var b) ? b.Gate : null;

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
            var houses = new List<int>();
            for (int i = 0; i < WorldGenerator.HouseBounds.Count; i++)
            {
                var c = WorldGenerator.HouseBounds[i].center;
                if (Factory.FlatDistance(c, b.At) < b.Radius && TownIs(c, s.k)) houses.Add(i);
            }
            houses.Sort((x, y) => Factory.FlatDistance(WorldGenerator.HouseBounds[x].center, b.At).CompareTo(Factory.FlatDistance(WorldGenerator.HouseBounds[y].center, b.At)));
            var rng = new System.Random(s.k.GetHashCode() ^ s.g.GetHashCode());
            float R(float a, float c) => a + (float)rng.NextDouble() * (c - a);
            for (int n = 0; n < houses.Count && n < MaxHouses; n++)
            {
                var h = WorldGenerator.HouseBounds[houses[n]];
                var spots = houses[n] < WorldGenerator.HouseFireSpots.Count ? WorldGenerator.HouseFireSpots[houses[n]] : new[] { h.center };
                float size = Mathf.Clamp(Mathf.Max(h.size.x, h.size.z) * 0.13f, 0.7f, 1.5f);
                // Fires on the building itself (points taken from its model): the highest is the big blaze with the
                // house's one light; the others along the roof and out of the upper walls
                for (int j = 0; j < spots.Length; j++)
                {
                    var at = spots[j];
                    Light light = null;
                    if (j == 0)
                    {
                        light = new GameObject("FireLight").AddComponent<Light>();
                        light.transform.SetParent(b.Root, false);
                        light.transform.position = at + Vector3.up * 0.5f;
                        light.type = LightType.Point;
                        light.color = Flame;
                        light.range = Mathf.Max(h.size.x, h.size.z) * 1.4f + 4f;
                        light.intensity = 2.4f;
                        light.shadows = LightShadows.None;
                    }
                    float k = j == 0 ? 1.6f : R(0.75f, 1.15f);
                    PropFire.Add(b.Root, at, Flame, size * k, j == 0 || j % 3 == 0, light);
                }
                var roar = Sfx.LoopAt("fire_loop", h.center + Vector3.up * 2f, 0.55f, 32f);
                if (roar != null) roar.transform.SetParent(b.Root, true);
            }
            Debris(b, s, rng);
            // The reeve, on the far side of town from the fire, asking for help with the rebuilding
            var town = WorldGenerator.TownAt(b.At - Factory.Flat(b.At - CenterOf(s.k)).normalized * 6f);
            if (town != null)
            {
                var away = Factory.Flat(CenterOf(s.k) - b.At).normalized;
                var spot = TownLife.Walkable(town.Center + away * Mathf.Min(town.Rect.width, town.Rect.height) * 0.25f);
                RebuildReeve.Create(b.Root, spot, s.k, b.At);
            }
            var hero = Player.I;
            if (hero != null && Factory.FlatDistance(hero.transform.position, b.At) < b.Radius + 40f)
                Sfx.Play("boom", b.At + Vector3.up * 2f, 0.6f, 0.1f, 80f);
        }

        static readonly Color Wood = new Color(0.42f, 0.29f, 0.18f), Charred = new Color(0.12f, 0.1f, 0.09f), Stone = new Color(0.45f, 0.43f, 0.4f);

        /// <summary>
        /// What a siege leaves: the gate's planks thrown inward, toppled barrels and spilled crates, broken and charred
        /// timber, rubble, arrows stuck in the ground, a torn banner, scorch marks and small fires in the street, and
        /// more of the same on the battlefield outside the gate. Only to look at: nothing blocks the way.
        /// </summary>
        static void Debris(Burning b, NetSack s, System.Random rng)
        {
            var grid = WorldGrid.Instance;
            if (grid == null) return;
            float R(float a, float c) => a + (float)rng.NextDouble() * (c - a);
            var outward = Factory.Flat(b.At - CenterOf(s.k)); // from the middle of the town out through the gate
            outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Vector3.back;
            var root = b.Root;

            Vector3 Spot(float minR, float maxR, bool inside)
            {
                for (int tries = 0; tries < 12; tries++)
                {
                    float ang = R(-80f, 80f) * Mathf.Deg2Rad;
                    var dir = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f) * (inside ? -outward : outward);
                    var p = b.At + dir * R(minR, maxR);
                    if (grid.IsWalkable(p) && (TownIs(p, s.k) == inside)) return new Vector3(p.x, 0f, p.z);
                }
                return Vector3.positiveInfinity;
            }

            void Plank(Vector3 p, bool burnt)
            {
                var go = Factory.Prim(PrimitiveType.Cube, root, p + Vector3.up * 0.06f, new Vector3(R(0.12f, 0.22f), 0.08f, R(0.9f, 2.1f)), burnt ? Charred : Wood * R(0.8f, 1.05f));
                go.transform.rotation = Quaternion.Euler(R(-8f, 8f), R(0f, 360f), R(-12f, 12f));
            }

            // Inside the walls: the street behind the gate and the quarter
            for (int i = 0; i < 70; i++)
            {
                var p = Spot(2f, b.Radius * 0.9f, true);
                if (float.IsInfinity(p.x)) continue;
                double roll = rng.NextDouble();
                if (roll < 0.32) Plank(p, rng.NextDouble() < 0.5);
                else if (roll < 0.47)
                {
                    // rubble: a little heap of stones
                    int n = 2 + rng.Next(4);
                    for (int k = 0; k < n; k++)
                    {
                        var stone = Factory.Prim(PrimitiveType.Cube, root, p + new Vector3(R(-0.4f, 0.4f), 0.08f, R(-0.4f, 0.4f)), Vector3.one * R(0.15f, 0.35f), Stone * R(0.75f, 1.05f));
                        stone.transform.rotation = Quaternion.Euler(R(0f, 360f), R(0f, 360f), R(0f, 360f));
                    }
                }
                else if (roll < 0.57)
                {
                    var barrel = ArtLibrary.Spawn("Props/barrel_large", root, p, R(0.8f, 1f), ArtLibrary.Fit.Height, R(0f, 360f), false);
                    if (barrel != null) barrel.transform.rotation = Quaternion.Euler(90f, R(0f, 360f), 0f) * Quaternion.identity; // knocked over
                    if (barrel != null) barrel.transform.position = p + Vector3.up * 0.35f;
                }
                else if (roll < 0.65)
                {
                    var crate = ArtLibrary.Spawn(rng.NextDouble() < 0.5 ? "Props/box_stacked" : "Props/crates_stacked", root, p, R(0.6f, 0.9f), ArtLibrary.Fit.Height, R(0f, 360f), false);
                    if (crate != null) crate.transform.rotation = Quaternion.Euler(R(-25f, 25f), R(0f, 360f), R(-25f, 25f));
                }
                else if (roll < 0.8) Arrow(root, p, rng);
                else if (roll < 0.86)
                {
                    PropFire.Add(root, p + Vector3.up * 0.1f, Flame, R(0.35f, 0.6f), false); // burning wreckage in the street
                    Plank(p, true);
                }
                else if (roll < 0.92) ImpactMarks.Place(p, ImpactMarks.Kind.Scorch, R(0.8f, 1.6f), s.left);
                else Factory.Prim(PrimitiveType.Cube, root, p + Vector3.up * 0.05f, new Vector3(R(0.5f, 1f), 0.1f, R(0.4f, 0.8f)), Charred); // ash and cinders
            }
            // The gate's planks, thrown inward, and a fallen banner
            for (int i = 0; i < 10; i++)
            {
                var p = Spot(1f, 6f, true);
                if (!float.IsInfinity(p.x)) Plank(p, rng.NextDouble() < 0.3);
            }
            var bp = Spot(3f, 8f, true);
            if (!float.IsInfinity(bp.x))
            {
                var banner = ArtLibrary.Spawn("Props/banner_red", root, bp, 2.2f, ArtLibrary.Fit.Height, R(0f, 360f), false);
                if (banner != null) banner.transform.rotation = Quaternion.Euler(84f, R(0f, 360f), 0f);
            }
            // The battlefield outside: arrows, broken weapons' hafts, planks of the ladders and the dead's gear
            for (int i = 0; i < 45; i++)
            {
                var p = Spot(3f, 22f, false);
                if (float.IsInfinity(p.x)) continue;
                double roll = rng.NextDouble();
                if (roll < 0.45) Arrow(root, p, rng);
                else if (roll < 0.75) Plank(p, rng.NextDouble() < 0.25);
                else if (roll < 0.85) ImpactMarks.Place(p, ImpactMarks.Kind.Crack, R(0.6f, 1.2f), s.left);
                else
                {
                    var stone = Factory.Prim(PrimitiveType.Cube, root, p + Vector3.up * 0.1f, Vector3.one * R(0.25f, 0.45f), Stone);
                    stone.transform.rotation = Quaternion.Euler(R(0f, 360f), R(0f, 360f), R(0f, 360f));
                }
            }
        }

        /// <summary>An arrow stuck in the ground at a slant, fletching up.</summary>
        static void Arrow(Transform root, Vector3 p, System.Random rng)
        {
            float R(float a, float c) => a + (float)rng.NextDouble() * (c - a);
            var arrow = new GameObject("Arrow").transform;
            arrow.SetParent(root, false);
            arrow.position = p;
            arrow.rotation = Quaternion.Euler(R(15f, 40f), R(0f, 360f), 0f);
            Factory.Prim(PrimitiveType.Cube, arrow, new Vector3(0f, 0.32f, 0f), new Vector3(0.025f, 0.64f, 0.025f), new Color(0.5f, 0.38f, 0.24f));
            Factory.Prim(PrimitiveType.Cube, arrow, new Vector3(0f, 0.6f, 0f), new Vector3(0.09f, 0.1f, 0.01f), new Color(0.85f, 0.82f, 0.75f));
        }

        static Vector3 CenterOf(string town)
        {
            foreach (var t in WorldGenerator.Towns) if (t.Name == town) return t.Center;
            return Vector3.zero;
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

    /// <summary>
    /// The reeve of a burned town, standing clear of the fire: bring timber (5 logs), stone (5 ore) or coin and the
    /// fires are out sooner (server: rebuild), for experience. Only there while it burns.
    /// </summary>
    public class RebuildReeve : Interactable
    {
        public string Town;
        CharacterView view;
        Vector3 fire;
        float nextCall;
        static readonly string[] Calls = { "Timber! Stone! Anything you can spare!", "Bring logs and ore, and we'll have the fires out sooner!",
            "The masons need paying. Every coin helps!", "Don't stand there gawping, help us rebuild!" };

        public override string HoverText => "Reeve of " + Town + "\n<help put out the fires and rebuild>";
        public override Color LabelColor => new Color(1f, 0.75f, 0.35f);
        public override float LabelHeight => 2.6f;

        public static RebuildReeve Create(Transform parent, Vector3 at, string town, Vector3 fire)
        {
            var go = new GameObject("Reeve");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.LookRotation(Factory.Flat(fire - at).sqrMagnitude > 0.01f ? Factory.Flat(fire - at) : Vector3.forward);
            var r = go.AddComponent<RebuildReeve>();
            r.Town = town;
            r.fire = fire;
            r.DisplayName = "Reeve Halden";
            r.InteractRange = 2.4f;
            r.view = CharacterView.Create(go.transform, new CharacterLook { Model = "Characters/Keeper", Height = 1.9f, Anims = AnimSet.Kenney, Tint = new Color(0.9f, 0.8f, 0.7f) });
            r.AddClickCollider(0.5f, 2f);
            return r;
        }

        void Update()
        {
            view?.UpdateLocomotion(0f);
            if (Time.time < nextCall) return;
            nextCall = Time.time + Random.Range(18f, 30f);
            var p = Player.I;
            if (p == null || Factory.FlatDistance(p.transform.position, transform.position) > 22f) return;
            Speech.Say(transform, 2.5f, Calls[Random.Range(0, Calls.Length)]);
            view?.Interact();
        }

        public override void Interact(Player p) => GameUI.I?.OpenRebuild(this);
    }
}
