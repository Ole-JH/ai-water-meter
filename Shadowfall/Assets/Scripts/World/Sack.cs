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
            public readonly List<Vector3> Ruins = new List<Vector3>(); // houses partly fallen in (rebuilt after the fire)
            public float KeepUntil;  // rebuilding: the ruins stand at least until then (and while the carpenters work)
            // the houses blackened by the fire (their own materials swapped for charred copies) and the ones fallen in
            // (sunk and leaning): all put back when the town is rebuilt
            public readonly List<(Renderer r, Material[] orig, Material[] made)> Charred = new List<(Renderer, Material[], Material[])>();
            public readonly List<(Transform t, Vector3 pos, Quaternion rot)> Slumped = new List<(Transform, Vector3, Quaternion)>();
        }

        static readonly Dictionary<string, Burning> burning = new Dictionary<string, Burning>();
        // fires out, ruins still standing while the town is rebuilt (SiegeAftermath's workers), then cleared away
        static readonly Dictionary<string, Burning> rebuilding = new Dictionary<string, Burning>();

        /// <summary>The fallen-in houses of a town that burns or is being rebuilt (where the workers go).</summary>
        public static List<Vector3> RuinsOf(string town)
        {
            if (town != null && burning.TryGetValue(town, out var b)) return b.Ruins;
            if (town != null && rebuilding.TryGetValue(town, out var r)) return r.Ruins;
            return null;
        }
        static Sack runner;
        const int MaxHouses = 36; // the whole quarter, most of the town
        static readonly Color Flame = new Color(1f, 0.5f, 0.15f);

        /// <summary>The server's list of burning quarters (sent when one starts or ends, and at login).</summary>
        static NetSack[] lastList;

        /// <summary>Lays out again what the server last said (once back from a dungeon: nothing is built down there).</summary>
        public static void Resync() => Set(lastList);

        public static void Set(NetSack[] list)
        {
            lastList = list;
            if (Dungeon.Active) return; // laid out on the way back up (the town's grid isn't the one loaded now)
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

        /// <summary>A town that burns now (and its gate), or null.</summary>
        public static string AnyBurning(out string gate)
        {
            foreach (var b in burning.Values) { gate = b.Gate; return b.Town; }
            gate = null;
            return null;
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
            if (rebuilding.TryGetValue(s.k, out var old)) { rebuilding.Remove(s.k); Restore(old); if (old.Root != null) Destroy(old.Root.gameObject); }
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
            // The nearest houses to the broken gate are ablaze, roofs and all, a black column of smoke over each that
            // is seen from far across the land, every third fallen in; the rest of the quarter is scorched and smoulders.
            // All of it charred black (put back as it was when the town is rebuilt).
            int blazing = Lite ? 7 : 14, lights = 0;
            for (int n = 0; n < houses.Count && n < MaxHouses; n++)
            {
                int hi = houses[n];
                var h = WorldGenerator.HouseBounds[hi];
                var spots = hi < WorldGenerator.HouseFireSpots.Count ? WorldGenerator.HouseFireSpots[hi] : new[] { h.center };
                var model = hi < WorldGenerator.HouseObjects.Count ? WorldGenerator.HouseObjects[hi] : null;
                bool blaze = n < blazing, fell = blaze && n % 3 == 1;
                float size = Mathf.Max(h.size.x, h.size.z);
                var top = spots.Length > 0 ? spots[0] : h.center + Vector3.up * h.extents.y;
                Char(b, model, blaze ? 0.2f : 0.42f);
                if (fell) Slump(b, model, rng);
                if (blaze)
                {
                    Light light = null;
                    if (lights < (Lite ? 4 : 8))
                    {
                        lights++;
                        light = new GameObject("FireLight").AddComponent<Light>();
                        light.transform.SetParent(b.Root, false);
                        light.transform.position = top + Vector3.up * 0.5f;
                        light.type = LightType.Point;
                        light.color = Flame;
                        light.range = size * 1.6f + 6f;
                        light.intensity = 3f;
                        light.shadows = LightShadows.None;
                    }
                    Blaze(b.Root, h, top, size, fell);
                    // flames bursting out of the upper walls and windows (the light flickers with the first)
                    int side = 0;
                    for (int j = 1; j < spots.Length && side < (Lite ? 1 : 3); j++, side++)
                        PropFire.Add(b.Root, spots[j], Flame, Mathf.Clamp(size * 0.3f, 1.6f, 3.2f) * R(0.85f, 1.15f), side == 0, side == 0 ? light : null);
                    if (side == 0) PropFire.Add(b.Root, top, Flame, 2.6f, true, light);
                    // and licking up the walls from the ground floor
                    for (int w = 0; w < 4; w++)
                    {
                        if (rng.NextDouble() < (Lite ? 0.75 : 0.45)) continue;
                        var face = w == 0 ? Vector3.right : w == 1 ? Vector3.left : w == 2 ? Vector3.forward : Vector3.back;
                        float half = Vector3.Dot(h.extents, new Vector3(Mathf.Abs(face.x), 0f, Mathf.Abs(face.z)));
                        var wall = new Vector3(h.center.x, 0f, h.center.z) + face * (half + 0.15f) + Vector3.Cross(Vector3.up, face) * R(-half * 0.6f, half * 0.6f);
                        PropFire.Add(b.Root, wall + Vector3.up * R(0.6f, Mathf.Max(1f, h.size.y * 0.4f)), Flame, Mathf.Clamp(size * 0.22f, 1.2f, 2.4f), false);
                    }
                    if (n % 3 == 0)
                    {
                        var roar = Sfx.LoopAt("fire_loop", h.center + Vector3.up * 2f, 0.6f, 34f);
                        if (roar != null) roar.transform.SetParent(b.Root, true);
                    }
                }
                else
                {
                    // burnt out already: a last low fire in the roof and grey smoke
                    PropFire.Add(b.Root, top, Flame, R(0.7f, 1.1f), true);
                }
                var ruinAt = Ruin(b, h, spots, fell, blaze, rng);
                if (ruinAt.HasValue) b.Ruins.Add(ruinAt.Value);
            }
            Pall(b);
            WallFires(b, s, rng);
            Debris(b, s, rng);
            // The reeve, on the far side of town from the fire, asking for help with the rebuilding
            var town = WorldGenerator.TownAt(b.At - Factory.Flat(b.At - CenterOf(s.k)).normalized * 6f);
            if (town != null)
            {
                // at the town's square (always open ground, and where people look for news), on its far side from the fire
                var away = Factory.Flat(CenterOf(s.k) - b.At).normalized;
                var spot = OpenNear(town.Center + away * 4f + Vector3.Cross(Vector3.up, away) * 3f);
                RebuildReeve.Create(b.Root, spot, s.k, b.At);
            }
            var hero = Player.I;
            if (hero != null && Factory.FlatDistance(hero.transform.position, b.At) < b.Radius + 40f)
                Sfx.Play("boom", b.At + Vector3.up * 2f, 0.6f, 0.1f, 80f);
        }

        /// <summary>The nearest walkable spot with walkable ground all round it (somewhere a hero can walk up to).</summary>
        static Vector3 OpenNear(Vector3 p)
        {
            var grid = WorldGrid.Instance;
            if (grid == null) return p;
            for (float r = 0f; r <= 8f; r += 1f)
                for (int a = 0; a < (r == 0f ? 1 : 12); a++)
                {
                    var c = p + Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward * r;
                    bool open = true;
                    for (int dx = -1; dx <= 1 && open; dx++)
                        for (int dz = -1; dz <= 1 && open; dz++)
                            if (!grid.IsWalkable(c + new Vector3(dx, 0f, dz))) open = false;
                    if (open) return new Vector3(c.x, 0f, c.z);
                }
            return TownLife.Walkable(p);
        }

        /// <summary>
        /// A house ablaze: a sheet of big flames over the whole roof, sparks streaming up off it, and a thick black column
        /// of smoke rising high above the town (seen from far off). A fallen-in house burns lower and hotter.
        /// </summary>
        static void Blaze(Transform root, Bounds h, Vector3 top, float size, bool fell)
        {
            if (!SpellFx.Ready) return;
            float k = Mathf.Clamp(size / 7f, 0.7f, 1.5f);
            var roof = new Vector3(h.center.x, fell ? top.y * 0.6f : top.y - 0.4f, h.center.z);
            SpellFx.Loop(new SpellFx.P
            {
                Rate = (Lite ? 22 : 40) * k, Duration = 1f, Life = new Vector2(0.7f, 1.3f), Speed = new Vector2(0.2f, 0.6f), Size = new Vector2(1.3f, 2.6f) * k,
                Start = new Color(1f, 0.85f, 0.45f, 1f), Mid = new Color(1f, 0.45f, 0.1f, 0.9f), End = new Color(0.45f, 0.08f, 0.02f, 0f),
                Velocity = new Vector3(0f, 2.6f, 0f), Radius = size * 0.3f, Max = Lite ? 60 : 110,
            }, root, root.InverseTransformPoint(roof));
            SpellFx.Loop(new SpellFx.P
            {
                Rate = 10, Duration = 1f, Life = new Vector2(2f, 3.5f), Speed = new Vector2(0.5f, 1.5f), Size = new Vector2(0.06f, 0.14f),
                Start = new Color(1f, 0.85f, 0.4f, 1f), End = new Color(1f, 0.3f, 0.05f, 0f), Velocity = new Vector3(0.4f, 3.2f, 0.2f), Stretch = true, Radius = size * 0.3f, Max = 50,
            }, root, root.InverseTransformPoint(roof + Vector3.up));
            SpellFx.Loop(new SpellFx.P
            {
                Rate = Lite ? 2.5f : 4.5f, Duration = 1f, Life = new Vector2(10f, 15f), Speed = new Vector2(0.2f, 0.5f), Size = new Vector2(3f, 5.5f) * k,
                Start = new Color(0.1f, 0.09f, 0.08f, 0f), Mid = new Color(0.12f, 0.11f, 0.1f, 0.62f), End = new Color(0.3f, 0.29f, 0.28f, 0f),
                Velocity = new Vector3(0.7f, 3.2f, 0.25f), Smoke = true, Grow = true, Radius = size * 0.2f, Max = Lite ? 40 : 70,
            }, root, root.InverseTransformPoint(top + Vector3.up * 1.5f));
        }

        /// <summary>Blackens a house: each of its materials swapped for a darkened copy (kept to be put back).</summary>
        static void Char(Burning b, GameObject model, float k)
        {
            if (model == null) return;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                var orig = r.sharedMaterials;
                var made = new Material[orig.Length];
                for (int i = 0; i < orig.Length; i++)
                {
                    if (orig[i] == null) continue;
                    made[i] = new Material(orig[i]);
                    var c = orig[i].color;
                    made[i].color = new Color(c.r * k, c.g * k * 0.95f, c.b * k * 0.9f, c.a);
                }
                r.sharedMaterials = made;
                b.Charred.Add((r, orig, made));
            }
        }

        /// <summary>A house fallen in: sunk into its own rubble and leaning over.</summary>
        static void Slump(Burning b, GameObject model, System.Random rng)
        {
            if (model == null) return;
            var t = model.transform;
            b.Slumped.Add((t, t.position, t.rotation));
            var axis = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * Vector3.right;
            t.rotation = Quaternion.AngleAxis(6f + (float)rng.NextDouble() * 5f, axis) * t.rotation;
            t.position += Vector3.down * 0.7f;
        }

        /// <summary>The town rebuilt: the houses as they were.</summary>
        static void Restore(Burning b)
        {
            foreach (var (r, orig, made) in b.Charred)
            {
                if (r != null) r.sharedMaterials = orig;
                foreach (var m in made) if (m != null) Destroy(m);
            }
            b.Charred.Clear();
            foreach (var (t, pos, rot) in b.Slumped) if (t != null) t.SetPositionAndRotation(pos, rot);
            b.Slumped.Clear();
        }

        /// <summary>
        /// What the fire does to a house: soot scorched on the ground round it, embers drifting up off it, a column of
        /// black smoke from every third (seen from far off), and every third one partly fallen in: charred beams and a
        /// slab of roof down against its side, a heap of rubble and burning timbers at its foot.
        /// </summary>
        static Vector3? Ruin(Burning b, Bounds h, Vector3[] spots, bool fell, bool blaze, System.Random rng)
        {
            float R(float a, float c) => a + (float)rng.NextDouble() * (c - a);
            var root = b.Root;
            float size = Mathf.Max(h.size.x, h.size.z);
            var foot = new Vector3(h.center.x, 0f, h.center.z);
            ImpactMarks.Place(foot, ImpactMarks.Kind.Scorch, size * 0.55f, 3600f);
            var top = spots.Length > 0 ? spots[0] : h.center + Vector3.up * h.size.y;
            if (!fell) return null;
            // fallen in on one side
            var side = (rng.NextDouble() < 0.5 ? Vector3.right : Vector3.forward) * (rng.NextDouble() < 0.5 ? -1f : 1f);
            var at = foot + side * (size * 0.5f + 0.6f);
            if (WorldGrid.Instance != null && !WorldGrid.Instance.IsWalkable(at)) at = foot + side * (size * 0.5f + 1.6f);
            var slab = Factory.Prim(PrimitiveType.Cube, root, at + Vector3.up * 1.1f, new Vector3(size * 0.7f, 0.18f, 2.6f), Charred);
            slab.transform.rotation = Quaternion.LookRotation(side) * Quaternion.Euler(R(35f, 55f), 90f, R(-8f, 8f));
            for (int k = 0; k < 5; k++)
            {
                var beam = Factory.Prim(PrimitiveType.Cube, root, at + new Vector3(R(-1.2f, 1.2f), R(0.3f, 1.2f), R(-1.2f, 1.2f)), new Vector3(0.22f, 0.22f, R(1.6f, 3f)), k % 2 == 0 ? Charred : Wood * 0.5f);
                beam.transform.rotation = Quaternion.Euler(R(-40f, 40f), R(0f, 360f), R(-30f, 30f));
            }
            for (int k = 0; k < 8; k++)
            {
                var stone = Factory.Prim(PrimitiveType.Cube, root, at + new Vector3(R(-1.5f, 1.5f), 0.1f, R(-1.5f, 1.5f)), Vector3.one * R(0.2f, 0.5f), Stone * R(0.5f, 0.8f));
                stone.transform.rotation = Quaternion.Euler(R(0f, 360f), R(0f, 360f), R(0f, 360f));
            }
            PropFire.Add(root, at + Vector3.up * 0.3f, Flame, R(0.6f, 0.9f), true);
            return at;
        }

        /// <summary>
        /// The town wall on either side of the broken gate burns along its length: big flames on top of it and up its face,
        /// soot on the ground at its foot, and a few breaches where it has partly come down (a heap of burning timber).
        /// </summary>
        static void WallFires(Burning b, NetSack s, System.Random rng)
        {
            float R(float a, float c) => a + (float)rng.NextDouble() * (c - a);
            var town = WorldGenerator.TownNamed(s.k);
            if (town == null) return;
            var side = Rampart.SideOf(town, s.g);
            var root = b.Root;
            int reach = Mathf.Min(22, side.Half - 1);
            for (int a = side.Mid - reach; a <= side.Mid + reach; a += 2)
            {
                if (Mathf.Abs(a - side.Mid) <= 3) continue; // the gateway itself has its own heap
                if (rng.NextDouble() < (Lite ? 0.55 : 0.2)) continue;
                var p = side.Point(a + 0.5f);
                bool big = rng.NextDouble() < 0.45;
                PropFire.Add(root, p + Vector3.up * (Rampart.Top + R(-0.2f, 0.4f)), Flame, big ? R(1.8f, 2.8f) : R(1f, 1.6f), big);
                if (rng.NextDouble() < 0.5) PropFire.Add(root, p - side.Out * 0.7f + Vector3.up * R(0.5f, 1.6f), Flame, R(0.9f, 1.5f), false); // up the inside face
                ImpactMarks.Place(p - side.Out * 1.2f, ImpactMarks.Kind.Scorch, R(1.2f, 2f), s.left);
                if (rng.NextDouble() < 0.15)
                {
                    // a breach: the palisade's timbers down in a burning heap at its foot
                    for (int k = 0; k < 6; k++)
                    {
                        var beam = Factory.Prim(PrimitiveType.Cube, root, p - side.Out * R(0.8f, 2.2f) + side.Axis * R(-1f, 1f) + Vector3.up * R(0.2f, 0.6f), new Vector3(0.28f, 0.28f, R(1.8f, 2.8f)), Charred);
                        beam.transform.rotation = Quaternion.Euler(R(-30f, 30f), R(0f, 360f), R(-30f, 30f));
                    }
                    PropFire.Add(root, p - side.Out * 1.4f + Vector3.up * 0.4f, Flame, R(1.4f, 2.2f), true);
                }
            }
        }

        /// <summary>Ash drifting down over the whole quarter.</summary>
        static void Pall(Burning b)
        {
            if (!SpellFx.Ready) return;
            SpellFx.Loop(new SpellFx.P
            {
                Rate = 18, Duration = 1f, Life = new Vector2(5f, 8f), Speed = new Vector2(0.05f, 0.2f), Size = new Vector2(0.05f, 0.11f),
                Start = new Color(0.55f, 0.53f, 0.5f, 0.8f), End = new Color(0.4f, 0.38f, 0.36f, 0f), Gravity = 0.05f, Velocity = new Vector3(0.3f, -0.4f, 0.1f),
                Shape = ParticleSystemShapeType.Circle, Radius = b.Radius * 0.8f, Max = 150,
            }, b.Root, b.At + Vector3.up * 12f);
        }

        /// <summary>Phones and low particle settings: a lighter fire (it's the heaviest scene in the game).</summary>
        static bool Lite => GameSettings.Phone || GameSettings.ParticleScale < 0.75f;

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
            for (int i = 0; i < 340; i++)
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
                else if (roll < 0.89)
                {
                    if (!Lite || rng.NextDouble() < 0.4) PropFire.Add(root, p + Vector3.up * 0.1f, Flame, R(0.35f, 0.75f), rng.NextDouble() < 0.3); // burning wreckage in the street
                    Plank(p, true);
                }
                else if (roll < 0.95) ImpactMarks.Place(p, ImpactMarks.Kind.Scorch, R(0.8f, 2.2f), s.left);
                else Factory.Prim(PrimitiveType.Cube, root, p + Vector3.up * 0.05f, new Vector3(R(0.5f, 1f), 0.1f, R(0.4f, 0.8f)), Charred); // ash and cinders
            }
            // The broken gateway: a heap of stone and the gate's charred timbers across it
            for (int i = 0; i < 18; i++)
            {
                var p = b.At + Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward * R(0.5f, 3f);
                var stone = Factory.Prim(PrimitiveType.Cube, root, new Vector3(p.x, R(0.1f, 0.4f), p.z), Vector3.one * R(0.25f, 0.6f), Stone * R(0.6f, 0.95f));
                stone.transform.rotation = Quaternion.Euler(R(0f, 360f), R(0f, 360f), R(0f, 360f));
            }
            for (int i = 0; i < 3; i++) PropFire.Add(root, b.At + Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward * R(1f, 3f) + Vector3.up * 0.2f, Flame, R(0.5f, 0.8f), i == 0);
            // The gate's planks, thrown inward, and a fallen banner
            for (int i = 0; i < 18; i++)
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
            for (int i = 0; i < 160; i++)
            {
                var p = Spot(3f, 26f, false);
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
            // The fire goes, the wreck stays: the ruins and wreckage are cleared while the town is rebuilt
            foreach (var f in b.Root.GetComponentsInChildren<PropFire>(true)) if (f != null) Destroy(f.gameObject);
            foreach (var l in b.Root.GetComponentsInChildren<Light>(true)) if (l != null) Destroy(l.gameObject);
            foreach (var ps in b.Root.GetComponentsInChildren<ParticleSystem>(true)) if (ps != null) Destroy(ps.gameObject);
            foreach (var a in b.Root.GetComponentsInChildren<AudioSource>(true)) if (a != null) Destroy(a.gameObject);
            foreach (var reeve in b.Root.GetComponentsInChildren<RebuildReeve>(true)) if (reeve != null) Destroy(reeve.gameObject);
            b.KeepUntil = Time.time + 8f;
            rebuilding[town] = b;
        }

        /// <summary>The rebuilding is done (or never came): the ruins and wreckage are cleared away.</summary>
        static void ClearRebuilt()
        {
            List<string> done = null;
            foreach (var kv in rebuilding)
                if (Time.time > kv.Value.KeepUntil && !SiegeAftermath.Repairing(kv.Key)) (done ??= new List<string>()).Add(kv.Key);
            if (done == null) return;
            foreach (var k in done)
            {
                var b = rebuilding[k];
                rebuilding.Remove(k);
                Restore(b);
                if (b.Root != null) Destroy(b.Root.gameObject);
            }
        }

        float nextLook;

        void Update()
        {
            if (Time.time < nextLook || (burning.Count == 0 && rebuilding.Count == 0)) return;
            nextLook = Time.time + 1f;
            ClearRebuilt();
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
        static readonly string[] Calls = { "The raiders broke through and burned us out! Help us rebuild!", "We lost the gate, and half the town with it. Timber! Stone!", "Timber! Stone! Anything you can spare!", "Bring logs and ore, and we'll have the fires out sooner!",
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
