using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// A walled town under attack (server/invasion.js; <see cref="Invasion"/> has the state): the attacked gate is a real
    /// gate that swings shut when the invasion starts, splinters and loses planks as the invaders batter it, and is
    /// smashed when it falls. Two militia helpers carry ladders to the inside of the wall on either side of it and lay a
    /// walkway on top, so heroes can climb up (<see cref="SiegeLadder"/>), shoot from the wall, and jump down on either
    /// side (<see cref="Player.ClimbWall"/>).
    /// Everything here is the client's own show, rebuilt from the invasion state; nothing of it is in the world map: the
    /// shut gate blocks its cells with <see cref="WorldGrid.SetClosed"/>, which never reaches the server.
    /// </summary>
    public static class Rampart
    {
        /// <summary>The walkway's height on top of the wall (palisades and stone walls are about 2.6-2.8 high).</summary>
        public const float Top = 2.6f;
        /// <summary>How far from the gate's middle the ladders stand, and how far the walkway reaches (wall cells).</summary>
        const int LadderAt = 6, WalkFrom = 4, WalkTo = 12;

        static Transform root;
        static WorldGrid world;          // the overworld's grid (not a dungeon's)
        static TownGate gate;            // the gate of the current (or last) invasion
        static readonly Dictionary<string, TownGate> gates = new Dictionary<string, TownGate>(); // every walled town's gates
        static readonly List<GameObject> helpers = new List<GameObject>();
        /// <summary>The siege ladders up (or being carried) now: the wall's archers wait for theirs to stand before climbing.</summary>
        public static readonly List<SiegeLadder> Standing = new List<SiegeLadder>();

        /// <summary>The ladder whose walkway is nearest <paramref name="p"/> (null: none).</summary>
        public static SiegeLadder LadderNear(Vector3 p)
        {
            SiegeLadder best = null;
            float bd = 40f;
            foreach (var l in Standing)
            {
                if (l == null) continue;
                float d = Factory.FlatDistance(l.Walk.Foot, p);
                if (d < bd) { bd = d; best = l; }
            }
            return best;
        }

        /// <summary>
        /// The front door of a house near <paramref name="p"/> (within <paramref name="r"/>, inside the same town), picked
        /// at random among the nearest few, swung open: where townsfolk and guards come out of instead of appearing.
        /// </summary>
        public static HouseDoors.Door DoorNear(Vector3 p, float r, System.Random rng = null)
        {
            var town = WorldGenerator.TownAt(p);
            var near = new List<HouseDoors.Door>();
            foreach (var d in HouseDoors.All)
            {
                if (Factory.FlatDistance(d.Centre, p) > r) continue;
                var t = WorldGenerator.TownAt(d.Centre);
                if (town != null && t != town) continue;
                near.Add(d);
            }
            if (near.Count == 0) return null;
            near.Sort((a, b) => Factory.FlatDistance(a.Centre, p).CompareTo(Factory.FlatDistance(b.Centre, p)));
            int k = Mathf.Min(near.Count, 4);
            var pick = near[rng != null ? rng.Next(k) : Random.Range(0, k)];
            HouseDoors.Swing(pick, 1.6f);
            return pick;
        }
        static string builtFor;          // "town|gate|started" of the ladders and helpers up now
        static bool runner;

        /// <summary>One side of a walled town's gate: where the wall line runs and which way is out.</summary>
        public class Side
        {
            public Vector3 Axis, Out;    // along the wall; outward (unit X or Z)
            public float Line;           // the wall's centre line (world X or Z across the axis)
            public int Mid;              // the middle cell of the 5-cell gate gap, along the axis
            public int Row;              // the wall's cells, across the axis
            public int Half;             // cells from the middle to the corner
            public Vector3 Point(float a) => Axis * a + Across * Line;
            public Vector3 Across => new Vector3(Mathf.Abs(Out.x), 0f, Mathf.Abs(Out.z));
            public Vector2Int Cell(int a) => Axis.x != 0f ? new Vector2Int(a, Row) : new Vector2Int(Row, a);
            public Vector3 Centre => Point(Mid + 0.5f);
        }

        /// <summary>The gate side <paramref name="name"/> (south, north, west, east) of a walled town, as built by WorldGenerator.</summary>
        public static Side SideOf(Settlement t, string name)
        {
            var r = t.Rect;
            int x0 = r.xMin, x1 = r.xMax - 1, y0 = r.yMin, y1 = r.yMax - 1;
            int cx = r.xMin + r.width / 2, cy = r.yMin + r.height / 2;
            switch (name)
            {
                case "north": return new Side { Axis = Vector3.right, Out = Vector3.forward, Row = y1, Line = y1 + 0.5f, Mid = cx, Half = r.width / 2 };
                case "west": return new Side { Axis = Vector3.forward, Out = Vector3.left, Row = x0, Line = x0 + 0.5f, Mid = cy, Half = r.height / 2 };
                case "east": return new Side { Axis = Vector3.forward, Out = Vector3.right, Row = x1, Line = x1 + 0.5f, Mid = cy, Half = r.height / 2 };
                default: return new Side { Axis = Vector3.right, Out = Vector3.back, Row = y0, Line = y0 + 0.5f, Mid = cx, Half = r.width / 2 };
            }
        }

        static Settlement Town(string name)
        {
            foreach (var t in WorldGenerator.Towns) if (t.Walled && t.Name == name) return t;
            return null;
        }

        /// <summary>Brings the gate, ladders and helpers in line with the invasion state (called when it changes, and twice a second).</summary>
        public static void Sync()
        {
            if (!runner) { runner = true; new GameObject("RampartRunner").AddComponent<RampartRunner>(); }
            if (Dungeon.Active || WorldGrid.Instance == null) return; // the overworld's grid isn't the current one in there
            world = WorldGrid.Instance;
            if (root == null) root = new GameObject("Rampart").transform;
            Gates();

            var iv = Invasion.Current;
            var town = iv != null ? Town(iv.town) : null;
            if (town == null)
            {
                Clear(true);
                return;
            }
            var side = SideOf(town, iv.gate);
            if (gate == null || gate.Town != town.Name || gate.GateName != iv.gate)
            {
                Clear(false);
                if (gate != null) gate.Repair();
                gate = GateOf(town, iv.gate);
            }

            switch (iv.phase)
            {
                case "warn": // the scouts' warning: the militia put the ladders up, the gate stays open until they come
                    Ladders(town, side, iv);
                    break;
                case "gather":
                case "wave":
                    gate.Close();
                    gate.SetHealth(iv.hp);
                    Ladders(town, side, iv);
                    break;
                case "won":
                    gate.SetHealth(iv.hp);
                    gate.Open();
                    break;
                case "lost":
                    gate.Shatter();
                    break;
            }
        }

        /// <summary>
        /// Every walled town has real gates in its four gateways: open by day, pulled shut at night and swung open for
        /// whoever comes along (see TownGate.Update). Only the ones near the hero are switched on.
        /// </summary>
        static void Gates()
        {
            var hero = Player.I;
            foreach (var t in WorldGenerator.Towns)
            {
                if (!t.Walled) continue;
                foreach (var name in new[] { "south", "north", "west", "east" })
                {
                    var g = GateOf(t, name);
                    bool near = hero == null || Factory.FlatDistance(hero.transform.position, g.transform.position) < 110f || g == gate;
                    if (g.gameObject.activeSelf != near) g.gameObject.SetActive(near);
                }
            }
        }

        static TownGate GateOf(Settlement town, string name)
        {
            string key = town.Name + "|" + name;
            if (!gates.TryGetValue(key, out var g) || g == null)
                gates[key] = g = TownGate.Build(root, town.Name, name, SideOf(town, name), world);
            return g;
        }

        /// <summary>Takes the ladders, walkway and helpers away; with <paramref name="repair"/> the gate is mended and opened.</summary>
        static void Clear(bool repair)
        {
            foreach (var h in helpers) if (h != null) Object.Destroy(h);
            helpers.Clear();
            Standing.Clear();
            builtFor = null;
            var p = Player.I;
            if (p != null && p.OnWall != null) p.LeaveWall(); // the walkway is gone
            if (repair && gate != null && !Sack.Burns(gate.Town)) gate.Repair(); // a sacked town's gate lies broken while it burns
        }

        /// <summary>The two ladders behind the gate, once per siege.</summary>
        static void Ladders(Settlement town, Side side, NetInvasion iv)
        {
            string key = iv.town + "|" + iv.gate;
            if (builtFor == key) return;
            Clear(false);
            builtFor = key;
            // Seen from the start (the warning or the gathering has just begun): the helpers carry the ladders out.
            // Otherwise (we logged in later, or came back from a dungeon) they're up already.
            bool fresh = (iv.phase == "warn" || iv.phase == "gather") && iv.left >= 25;
            foreach (int dir in new[] { -1, 1 }) PlaceLadder(town, side, dir, fresh);
        }

        static void PlaceLadder(Settlement town, Side side, int dir, bool walk)
        {
            // A spot along the wall with room at its foot inside (houses and props stand by the walls in places).
            int along = -1;
            foreach (int k in new[] { LadderAt, LadderAt + 1, LadderAt - 1, LadderAt + 2, LadderAt + 3 })
            {
                int a = side.Mid + dir * k;
                var foot = side.Point(a + 0.5f) - side.Out * WallWalk.FootIn;
                if (world.IsWalkable(foot) && world.IsWalkable(foot - side.Out * 0.8f) && world.IsWalkable(foot + side.Out * 0.6f)) { along = a; break; }
            }
            if (along < 0) return;
            int reach = Mathf.Min(WalkTo, side.Half - 3);
            var walkway = new WallWalk
            {
                Side = side,
                Min = dir < 0 ? side.Mid - reach + 0.5f : side.Mid + WalkFrom + 0.5f,
                Max = dir < 0 ? side.Mid - WalkFrom + 0.5f : side.Mid + reach + 0.5f,
                LadderAt = along + 0.5f,
            };

            var ladder = SiegeLadder.Build(root, walkway);
            walkway.Ladder = ladder;
            Standing.Add(ladder);
            helpers.Add(ladder.gameObject);
            var deck = Walkway(walkway);
            helpers.Add(deck);

            var helper = Militia.Create(root, town, side, walkway, ladder, deck, dir, walk);
            if (helper != null) helpers.Add(helper.gameObject);
            else { ladder.Raise(true); deck.SetActive(true); }
        }

        /// <summary>The plank walkway along the inside top of the wall, with a few posts under it.</summary>
        static GameObject Walkway(WallWalk w)
        {
            var go = new GameObject("WallWalk");
            go.transform.SetParent(root, false);
            var s = w.Side;
            var wood = new Color(0.47f, 0.34f, 0.21f);
            float len = w.Max - w.Min + 1f, mid = (w.Min + w.Max) * 0.5f;
            var c = s.Point(mid) - s.Out * 0.3f;
            var size = s.Axis * len + s.Across * 0.95f + Vector3.up * 0.1f;
            Factory.Prim(PrimitiveType.Cube, go.transform, c + Vector3.up * (Top - 0.05f), new Vector3(Mathf.Abs(size.x), size.y, Mathf.Abs(size.z)), wood);
            for (float a = w.Min; a <= w.Max + 0.01f; a += 2f)
                if (Mathf.Abs(a - w.LadderAt) > 0.7f) // no post where the ladder leans
                    Factory.Prim(PrimitiveType.Cube, go.transform, s.Point(a) - s.Out * 0.72f + Vector3.up * (Top * 0.5f), new Vector3(0.14f, Top, 0.14f), wood * 0.8f);
            go.SetActive(false);
            return go;
        }

        /// <summary>
        /// An invader swung at the gate (<paramref name="force"/> 0..1: a raider's blow is small, the battering ram's is 1):
        /// the gate shakes, splinters and dust fly, the thud carries across the town and the ground shakes under you if
        /// you're close.
        /// </summary>
        public static void Struck(Vector3 at, float force = 0.25f)
        {
            if (gate != null && Factory.FlatDistance(at, gate.transform.position) < 6f) gate.Shake();
            Sfx.Play("chop", at + Vector3.up, 0.6f + force * 0.4f, 0.1f, 40f);
            if (force >= 0.5f) Sfx.Play(force >= 1f ? "boom" : "hit_heavy", at + Vector3.up, 0.5f + force * 0.4f, 0.08f, force >= 1f ? 140f : 70f);
            SpellFx.Hit(at + Vector3.up * 1.2f, new Color(0.75f, 0.55f, 0.3f), false, Mathf.RoundToInt(6 + force * 10)); // splinters
            if (force >= 0.5f) SpellFx.Dust(at, 0.6f + force, new Color(0.7f, 0.62f, 0.5f));
            var p = Player.I;
            if (p == null) return;
            float d = Factory.FlatDistance(p.transform.position, at);
            float reach = 12f + force * 28f;
            if (d < reach) CameraRig.Shake(Mathf.Lerp(0.05f + force * 0.25f, 0f, d / reach));
        }

        /// <summary>Remote heroes stand on the walkway when the server says they're on a wall.</summary>
        public static float HeightFor(bool onWall) => onWall ? Top : 0f;

        class RampartRunner : MonoBehaviour
        {
            float next;
            void Update()
            {
                if (Time.time < next) return;
                next = Time.time + 0.5f;
                Sync();
            }
        }
    }

    /// <summary>A stretch of walkway on top of a town wall: where a hero on it can walk, and the ladder up to it.</summary>
    public class WallWalk
    {
        public Rampart.Side Side;
        public float Min, Max;           // along the wall (world X or Z)
        public float LadderAt;
        public SiegeLadder Ladder;

        public float Param(Vector3 p) => Vector3.Dot(p, Side.Axis);
        public float Clamp(float a) => Mathf.Clamp(a, Min, Max);
        /// <summary>Where a hero stands on the walkway at <paramref name="a"/>.</summary>
        public Vector3 At(float a) => Side.Point(Clamp(a)) - Side.Out * 0.3f + Vector3.up * Rampart.Top;
        /// <summary>How far <paramref name="p"/> is outside the wall (negative: inside the town).</summary>
        public float Outside(Vector3 p) => Vector3.Dot(p - Side.Point(Param(p)), Side.Out);
        /// <summary>Where a hero lands jumping down at <paramref name="a"/>, outside or inside.</summary>
        public Vector3 Landing(float a, bool outside) => Side.Point(Clamp(a)) + Side.Out * (outside ? 1.6f : -1.5f);
        /// <summary>How far inside the wall line the ladder's foot stands.</summary>
        public const float FootIn = 1.9f;
        public Vector3 Foot => Side.Point(LadderAt) - Side.Out * FootIn;
        /// <summary>Where the ladder's top rests: against the walkway's inner edge, a little above it (not through it).</summary>
        public Vector3 LadderTop => Side.Point(LadderAt) - Side.Out * 0.86f + Vector3.up * (Rampart.Top + 0.45f);
    }

    /// <summary>The attacked gate: two leaves of planks that close, take damage, lose pieces and break.</summary>
    public class TownGate : MonoBehaviour
    {
        public string Town, GateName;
        Rampart.Side side;
        WorldGrid grid;
        Transform left, right;
        readonly List<Piece> pieces = new List<Piece>();
        float open = 1f, openTarget = 1f; // 1 = swung open (inside), 0 = shut
        float health = 100f, shake;
        bool broken, blocked;
        bool siege;                       // shut (or opened) by the invasion, not by the time of day
        float nextLook;
        static readonly Color Wood = new Color(0.5f, 0.35f, 0.2f), Dark = new Color(0.3f, 0.2f, 0.12f), Iron = new Color(0.25f, 0.25f, 0.27f);

        class Piece { public GameObject Go; public float BreaksAt; public Color Color; }

        public static TownGate Build(Transform parent, string town, string gateName, Rampart.Side side, WorldGrid grid)
        {
            var go = new GameObject("Gate " + town + " " + gateName);
            go.transform.SetParent(parent, false);
            go.transform.position = side.Centre;
            go.transform.rotation = Quaternion.LookRotation(side.Out); // local +z points out of town
            var g = go.AddComponent<TownGate>();
            g.Town = town;
            g.GateName = gateName;
            g.side = side;
            g.grid = grid;
            g.Make();
            return g;
        }

        void Make()
        {
            var rng = new System.Random(Town.GetHashCode() ^ GateName.GetHashCode());
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            // A beam across the top, between the towers
            Factory.Prim(PrimitiveType.Cube, transform, new Vector3(0f, 3.35f, 0f), new Vector3(5.6f, 0.35f, 0.4f), Dark);
            left = Leaf("LeftLeaf", -2.5f, 1f, R);
            right = Leaf("RightLeaf", 2.5f, -1f, R);
            // Which piece goes at what health: planks one by one (in a random order), then the bars and the brace.
            var order = new List<Piece>(pieces);
            for (int i = order.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
            for (int i = 0; i < order.Count; i++) order[i].BreaksAt = Mathf.Lerp(88f, 4f, i / (float)Mathf.Max(1, order.Count - 1));
            Pose();
        }

        /// <summary>A leaf hinged at <paramref name="hinge"/> (local X), its planks running toward the middle (<paramref name="dir"/>).</summary>
        Transform Leaf(string name, float hinge, float dir, System.Func<float, float, float> R)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(hinge, 0f, 0f);
            for (int i = 0; i < 5; i++)
            {
                float x = dir * (0.25f + i * 0.5f), h = R(2.85f, 3.1f);
                var c = Wood * R(0.85f, 1.05f);
                Add(Factory.Prim(PrimitiveType.Cube, pivot, new Vector3(x, h * 0.5f, 0f), new Vector3(0.47f, h, 0.16f), c), c);
            }
            foreach (float y in new[] { 0.7f, 2.3f })
                Add(Factory.Prim(PrimitiveType.Cube, pivot, new Vector3(dir * 1.25f, y, -0.14f), new Vector3(2.45f, 0.24f, 0.1f), Dark), Dark);
            var brace = Factory.Prim(PrimitiveType.Cube, pivot, new Vector3(dir * 1.25f, 1.5f, -0.15f), new Vector3(0.2f, 1.95f, 0.08f), Dark);
            brace.transform.localRotation = Quaternion.Euler(0f, 0f, dir * 38f);
            Add(brace, Dark);
            Add(Factory.Prim(PrimitiveType.Cube, pivot, new Vector3(dir * 1.25f, 1.5f, 0.1f), new Vector3(2.4f, 0.12f, 0.04f), Iron), Iron);
            return pivot;
        }

        void Add(GameObject go, Color c) => pieces.Add(new Piece { Go = go, Color = c });

        public void Close()
        {
            if (broken) return;
            siege = true;
            if (openTarget != 0f) { openTarget = 0f; Sfx.Play("rubble", transform.position + Vector3.up, 0.5f, 0.1f, 50f); }
            Block(true);
        }

        public void Open()
        {
            if (broken || !siege) return;
            siege = false;
            openTarget = 1f;
            Block(false);
            Sfx.Play("rubble", transform.position + Vector3.up, 0.4f, 0.1f, 50f);
        }

        /// <summary>The gate as new and open again (the invasion is over).</summary>
        public void Repair()
        {
            Block(false);
            if (!broken && health >= 100f) { Open(); return; }
            foreach (var p in pieces) if (p.Go != null) Destroy(p.Go);
            pieces.Clear();
            foreach (Transform c in transform) Destroy(c.gameObject);
            broken = false;
            siege = false;
            health = 100f;
            open = openTarget = 1f;
            Make();
        }

        /// <summary>The gate's integrity (0-100): darker and more battered as it drops, pieces break off at their marks.</summary>
        public void SetHealth(float hp)
        {
            if (broken) return;
            if (hp < health - 0.5f)
            {
                Shake();
                // splinters off the outside face where it was hit
                var at = transform.position + transform.right * Random.Range(-2f, 2f) + Vector3.up * Random.Range(0.6f, 2.4f) + side.Out * 0.15f;
                SpellFx.Hit(at, new Color(0.7f, 0.5f, 0.3f), false, 8);
            }
            health = Mathf.Min(health, hp);
            float worn = 1f - health / 100f;
            foreach (var p in pieces)
            {
                if (p.Go == null) continue;
                if (health <= p.BreaksAt) { BreakOff(p, 1f); continue; }
                var r = p.Go.GetComponent<Renderer>();
                if (r != null) r.material.color = Color.Lerp(p.Color, p.Color * 0.45f, worn);
            }
            if (health <= 0f) Shatter();
        }

        /// <summary>The gate gives way: every piece flies inward.</summary>
        public void Shatter()
        {
            if (broken) return;
            broken = true;
            health = 0f;
            foreach (var p in pieces) if (p.Go != null) BreakOff(p, 2.2f);
            SpellFx.Dust(transform.position + Vector3.up * 0.2f, 2.6f, new Color(0.45f, 0.36f, 0.26f));
            Sfx.Play("shatter", transform.position + Vector3.up, 0.9f, 0.05f, 70f);
            Sfx.Play("rubble", transform.position + Vector3.up, 0.9f, 0.05f, 70f);
            Block(false);
        }

        void BreakOff(Piece p, float force)
        {
            var go = p.Go;
            p.Go = null;
            if (go == null) return;
            go.transform.SetParent(transform.parent, true);
            var fall = go.AddComponent<FallingPiece>();
            // Battered from outside: the pieces fall into the town, spinning
            fall.Velocity = -side.Out * Random.Range(1.2f, 3f) * force + Vector3.up * Random.Range(0.8f, 2.4f) + transform.right * Random.Range(-1f, 1f);
            fall.Spin = new Vector3(Random.Range(-260f, 260f), Random.Range(-120f, 120f), Random.Range(-260f, 260f));
            Sfx.Play(Random.value < 0.5f ? "chop" : "hit_heavy", go.transform.position, 0.55f, 0.15f, 45f);
            SpellFx.Hit(go.transform.position, new Color(0.7f, 0.5f, 0.3f), false, 10);
        }

        public void Shake() => shake = 0.35f;

        /// <summary>Shut gates block their five cells (not part of the map: WorldGrid.SetClosed). Heroes in the gap are moved inside.</summary>
        void Block(bool on)
        {
            if (blocked == on || grid == null) return;
            blocked = on;
            for (int a = side.Mid - 2; a <= side.Mid + 2; a++)
            {
                var c = side.Cell(a);
                grid.SetClosed(c.x, c.y, on);
            }
            var p = Player.I;
            if (on && p != null && !Dungeon.Active && p.OnWall == null && !grid.IsWalkable(p.transform.position))
                p.TeleportTo(side.Point(Vector3.Dot(p.transform.position, side.Axis)) - side.Out * 1.5f);
        }

        void Pose()
        {
            float swing = Mathf.SmoothStep(0f, 1f, open) * 92f;
            float wobble = shake > 0f ? Mathf.Sin(Time.time * 60f) * shake * 6f : 0f;
            float sag = broken ? 0f : (1f - health / 100f) * 4f; // a battered gate hangs crooked
            if (left != null) left.localRotation = Quaternion.Euler(wobble * 0.3f, swing + wobble, sag);
            if (right != null) right.localRotation = Quaternion.Euler(-wobble * 0.3f, -swing - wobble, -sag * 0.7f);
        }

        void Update()
        {
            if (!siege && !broken && Time.time >= nextLook) Peacetime();
            if (Mathf.Abs(open - openTarget) > 0.001f) open = Mathf.MoveTowards(open, openTarget, Time.deltaTime / (siege ? 2.5f : 1.3f));
            if (shake > 0f) shake = Mathf.Max(0f, shake - Time.deltaTime);
            Pose();
        }

        /// <summary>
        /// Not under attack: open all day; at night pulled to (not barred: nothing is blocked) and swung open for anyone
        /// who comes up to it, the hero, other heroes or the night watch, then pulled to again behind them.
        /// </summary>
        void Peacetime()
        {
            nextLook = Time.time + 0.25f;
            float want = DayNight.Night > 0.55f && !SomeoneComing() ? 0.06f : 1f;
            if (want == openTarget) return;
            var hero = Player.I;
            if (hero != null && Factory.FlatDistance(hero.transform.position, transform.position) < 30f)
                Sfx.Play(want > openTarget ? "door_open" : "door_close", transform.position + Vector3.up * 1.5f, 0.55f, 0.1f, 30f);
            openTarget = want;
        }

        bool SomeoneComing()
        {
            var at = transform.position;
            const float r = 11f;
            var hero = Player.I;
            if (hero != null && Factory.FlatDistance(hero.transform.position, at) < r) return true;
            foreach (var rp in RemotePlayer.ById.Values)
                if (rp != null && Factory.FlatDistance(rp.transform.position, at) < r) return true;
            return Walker.AnyNear(at, 7f);
        }

        void OnDestroy()
        {
            if (blocked && grid != null)
                for (int a = side.Mid - 2; a <= side.Mid + 2; a++) { var c = side.Cell(a); grid.SetClosed(c.x, c.y, false); }
        }
    }

    /// <summary>A piece knocked off the gate: flies, tumbles, lands, lies there a while and sinks away.</summary>
    public class FallingPiece : MonoBehaviour
    {
        public Vector3 Velocity, Spin;
        float landed = -1f;

        void Update()
        {
            float dt = Time.deltaTime;
            if (landed < 0f)
            {
                Velocity += Physics.gravity * dt;
                transform.position += Velocity * dt;
                transform.Rotate(Spin * dt, Space.World);
                float half = Mathf.Min(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) * 0.5f;
                if (transform.position.y <= half)
                {
                    transform.position = new Vector3(transform.position.x, half, transform.position.z);
                    if (Velocity.y < -3f) { Velocity = new Vector3(Velocity.x * 0.4f, -Velocity.y * 0.25f, Velocity.z * 0.4f); Spin *= 0.3f; return; }
                    landed = Time.time;
                    // lie flat on the ground
                    var e = transform.eulerAngles;
                    transform.rotation = Quaternion.Euler(Mathf.Round(e.x / 90f) * 90f, e.y, Mathf.Round(e.z / 90f) * 90f);
                    SpellFx.Dust(transform.position, 0.6f);
                }
                return;
            }
            float t = Time.time - landed;
            if (t > 20f) transform.position += Vector3.down * dt * 0.3f;
            if (t > 24f) Destroy(gameObject);
        }
    }

    /// <summary>A ladder against the inside of the wall: up to the walkway.</summary>
    public class SiegeLadder : Interactable
    {
        public WallWalk Walk;
        bool raised;
        float raiseT = -1f;
        Quaternion carried, leaning;

        public override bool CanInteract => raised;
        /// <summary>Standing against the wall (the archers climb it then).</summary>
        public bool Raised => raised;
        public override string HoverText => "Ladder\n<climb onto the wall: shoot from up there, or jump down outside>";
        public override Color LabelColor => new Color(1f, 0.85f, 0.55f);
        public override float LabelHeight => 3.2f;
        public override Vector3 Position => Walk.Foot;

        public static SiegeLadder Build(Transform parent, WallWalk walk)
        {
            var go = new GameObject("Ladder");
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<SiegeLadder>();
            l.Walk = walk;
            l.DisplayName = "Ladder";
            l.InteractRange = 1.7f;
            var wood = new Color(0.55f, 0.4f, 0.24f);
            var s = walk.Side;
            var along = walk.LadderTop - walk.Foot;
            float len = along.magnitude;
            foreach (float x in new[] { -0.26f, 0.26f })
                Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(x, len * 0.5f, 0f), new Vector3(0.09f, len, 0.09f), wood * 0.85f);
            for (float y = 0.3f; y < len - 0.1f; y += 0.38f)
                Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, y, 0f), new Vector3(0.52f, 0.06f, 0.07f), wood);
            // the rungs run along the wall (local x = the wall's axis) and the rails climb up to the walkway
            l.leaning = Quaternion.LookRotation(Vector3.Cross(s.Axis, along.normalized), along.normalized);
            l.carried = l.leaning;
            go.transform.position = walk.Foot;
            go.transform.rotation = l.leaning;
            return l;
        }

        /// <summary>Carried on a helper's shoulder (lying along their way).</summary>
        public void Carry(Transform by)
        {
            transform.SetParent(by, false);
            transform.localPosition = new Vector3(0.35f, 1.55f, -1.4f);
            transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        /// <summary>Stands it up against the wall (at once, or swung up over a second).</summary>
        public void Raise(bool now)
        {
            transform.position = Walk.Foot;
            carried = Quaternion.LookRotation(Vector3.Cross(Walk.Side.Axis, -Walk.Side.Out), -Walk.Side.Out); // lying on the ground, pointing into town, rungs along the wall
            if (now) { transform.rotation = leaning; raiseT = -1f; Ready(); return; }
            raiseT = 0f;
            transform.rotation = carried;
            Sfx.Play("swing", transform.position + Vector3.up, 0.4f, 0.1f);
        }

        void Ready()
        {
            if (raised) return;
            raised = true;
            AddClickCollider(0.6f, 3f);
            Sfx.Play("hit_heavy", transform.position + Vector3.up * 2.5f, 0.4f, 0.1f);
        }

        void Update()
        {
            if (raiseT < 0f) return;
            raiseT += Time.deltaTime / 1.1f;
            transform.rotation = Quaternion.Slerp(carried, leaning, Mathf.SmoothStep(0f, 1f, raiseT));
            if (raiseT >= 1f) { raiseT = -1f; Ready(); }
        }

        public override void Interact(Player p) => p.ClimbWall(Walk);
    }

    /// <summary>A militia helper: carries a ladder from the middle of town to the wall, stands it up, lays the walkway, then shouts encouragement.</summary>
    public class Militia : MonoBehaviour
    {
        enum Step { Walking, Raising, Building, Guarding }

        static readonly string[] Shouts =
        {
            "Up the ladders! Shoot them from the wall!",
            "They're battering the gate! Get up there!",
            "Mind the drop on the far side, friend.",
            "Hold the gate! Hold!",
            "Aim for the big ones, they hit the gate hardest!",
        };

        CharacterView view;
        SiegeLadder ladder;
        GameObject deck;
        WallWalk walk;
        readonly List<Vector3> path = new List<Vector3>();
        Step step;
        float until, nextShout, speed, delay;
        Vector3 stand;

        public static Militia Create(Transform parent, Settlement town, Rampart.Side side, WallWalk walk, SiegeLadder ladder,
            GameObject deck, int dir, bool fromTown)
        {
            var look = new CharacterLook { Model = "Characters/Knight", Height = 1.95f, Tint = new Color(0.82f, 0.78f, 0.7f), Weapon = dir < 0 ? "sword" : "axe" };
            var go = new GameObject("Militia");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<Militia>();
            m.walk = walk;
            m.ladder = ladder;
            m.deck = deck;
            m.speed = Random.Range(3.1f, 4.2f);
            m.delay = Time.time + Random.Range(0.3f, 3f); // each sets off in his own time
            m.stand = walk.Foot - side.Out * 0.9f + side.Axis * dir * 1.2f;
            var grid = WorldGrid.Instance;
            if (!grid.IsWalkable(m.stand)) m.stand = walk.Foot - side.Out * 1.2f;
            Vector3 start = m.stand;
            if (fromTown)
            {
                // out of a house's front door (one near the middle of town, on his side), not out of thin air
                var mid = new Vector3(town.Rect.center.x, 0f, town.Rect.center.y) + side.Out * 5f + side.Axis * dir * 3f;
                var door = Rampart.DoorNear(mid, 26f);
                start = door != null ? TownLife.Walkable(door.Step) : TownLife.Walkable(mid);
                if (!grid.FindPath(start, walk.Foot, m.path) || m.path.Count == 0) { start = m.stand; fromTown = false; }
            }
            go.transform.position = start;
            m.view = CharacterView.Create(go.transform, look);
            if (m.view == null)
                HumanoidModel.Build(go.transform, 1f, new Color(0.9f, 0.75f, 0.6f), new Color(0.5f, 0.45f, 0.4f), Color.gray, Color.gray, false, true);
            if (fromTown)
            {
                m.step = Step.Walking;
                ladder.Carry(go.transform);
            }
            else
            {
                ladder.Raise(true);
                deck.SetActive(true);
                m.step = Step.Guarding;
                m.FaceGate();
            }
            m.nextShout = Time.time + Random.Range(6f, 14f);
            return m;
        }

        void FaceGate() => Factory.Face(transform, walk.Side.Centre - walk.Side.Out * 3f);

        void Update()
        {
            float dt = Time.deltaTime, moving = 0f;
            switch (step)
            {
                case Step.Walking:
                    if (Time.time < delay) break;
                    if (path.Count == 0)
                    {
                        step = Step.Raising;
                        until = Time.time + 1.2f;
                        ladder.transform.SetParent(transform.parent, true);
                        ladder.Raise(false);
                        Factory.Face(transform, walk.Foot + walk.Side.Out);
                        view?.Interact();
                        break;
                    }
                    var to = Factory.Flat(path[0] - transform.position);
                    float stepLen = speed * dt;
                    if (to.magnitude <= stepLen) { transform.position = new Vector3(path[0].x, 0f, path[0].z); path.RemoveAt(0); }
                    else transform.position += to.normalized * stepLen;
                    if (to.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), dt * 10f);
                    moving = speed;
                    break;

                case Step.Raising:
                    if (Time.time < until) break;
                    // up the ladder goes the walkway: a few hammer blows
                    step = Step.Building;
                    until = Time.time + 2.4f;
                    break;

                case Step.Building:
                    if (Mathf.Repeat(Time.time, 0.8f) < dt)
                    {
                        view?.Interact();
                        Sfx.Play("chop", walk.Foot + Vector3.up * 2f, 0.5f, 0.15f, 35f);
                    }
                    if (Time.time < until) break;
                    deck.SetActive(true);
                    SpellFx.Dust(walk.At(walk.LadderAt), 0.8f);
                    step = Step.Guarding;
                    path.Clear();
                    WorldGrid.Instance?.FindPath(transform.position, stand, path);
                    break;

                case Step.Guarding:
                    if (path.Count > 0)
                    {
                        var d = Factory.Flat(path[0] - transform.position);
                        float s = 2.5f * dt;
                        if (d.magnitude <= s) { transform.position = new Vector3(path[0].x, 0f, path[0].z); path.RemoveAt(0); if (path.Count == 0) FaceGate(); }
                        else { transform.position += d.normalized * s; transform.rotation = Quaternion.LookRotation(d); }
                        moving = 2.5f;
                        break;
                    }
                    if (Time.time >= nextShout)
                    {
                        nextShout = Time.time + Random.Range(18f, 35f);
                        var p = Player.I;
                        if (p != null && Factory.FlatDistance(p.transform.position, transform.position) < 26f)
                            Speech.Say(transform, 2.3f, Shouts[Random.Range(0, Shouts.Length)]);
                    }
                    break;
            }
            view?.UpdateLocomotion(moving);
        }
    }
}
