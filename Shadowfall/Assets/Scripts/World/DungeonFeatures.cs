using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// What makes a dungeon level more than rooms and monsters (<see cref="Dungeon"/> builds it, this adds to it):
    /// traps in the corridors (pressure plates that fire spikes up out of the floor, and pendulum blades swinging across
    /// the way: both visible, both avoidable with care), and the boss's room, whose doorways slam shut with iron
    /// portcullises once the fight starts and rise again with a rumble when the boss falls (or the fight is abandoned).
    /// The traps hurt only us (our own health is ours to keep); the bars block only our own way (WorldGrid.SetClosed).
    /// Not in greater rifts.
    /// </summary>
    public class DungeonFeatures : MonoBehaviour
    {
        static DungeonFeatures I;

        WorldGrid grid;
        List<RectInt> rooms;
        RectInt bossRoom;
        bool hasBossRoom, sealedIn;
        float quietSince;
        readonly List<Transform> bars = new List<Transform>();
        readonly List<Vector2Int> doorways = new List<Vector2Int>();
        Enemy boss;

        public static void Build(Transform root, bool[] blocked, int w, int h, List<RectInt> rooms, int seed, NetMsg m)
        {
            var go = new GameObject("DungeonFeatures");
            go.transform.SetParent(root, false);
            I = go.AddComponent<DungeonFeatures>();
            I.grid = WorldGrid.Instance;
            I.rooms = rooms;
            if (m.k != null && m.k.StartsWith("Greater Rift")) return; // a rift is its own thing
            I.PlaceTraps(blocked, w, h, rooms, new System.Random(seed ^ 0x5eed), m);
        }

        // ------------------------------------------------------------------ traps

        void PlaceTraps(bool[] blocked, int w, int h, List<RectInt> rooms, System.Random rng, NetMsg m)
        {
            bool B(int x, int y) => x < 0 || y < 0 || x >= w || y >= h || blocked[y * w + x];
            bool InRoom(int x, int y) { foreach (var r in rooms) if (r.Contains(new Vector2Int(x, y))) return true; return false; }
            var keepClear = new List<Vector3>();
            if (m.exit != null && m.exit.Length == 2) keepClear.Add(Dungeon.ToWorld(m.exit[0], m.exit[1]));
            if (m.stairs != null && m.stairs.Length == 2) keepClear.Add(Dungeon.ToWorld(m.stairs[0], m.stairs[1]));
            if (m.start != null && m.start.Length == 2) keepClear.Add(Dungeon.ToWorld(m.start[0], m.start[1]));

            // corridor cells: floor with wall on two opposite sides, outside the rooms
            var spots = new List<(Vector2Int c, bool alongX)>();
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    if (B(x, y) || InRoom(x, y)) continue;
                    bool ns = B(x, y - 1) && B(x, y + 1) && !B(x - 1, y) && !B(x + 1, y); // runs along X
                    bool ew = B(x - 1, y) && B(x + 1, y) && !B(x, y - 1) && !B(x, y + 1); // runs along Z
                    if (ns || ew) spots.Add((new Vector2Int(x, y), ns));
                }
            int plates = 0, blades = 0, want = 2 + rng.Next(3);
            var placed = new List<Vector3>();
            for (int tries = 0; tries < 200 && plates + blades < want && spots.Count > 0; tries++)
            {
                var (c, alongX) = spots[rng.Next(spots.Count)];
                var at = Dungeon.ToWorld(c.x + 0.5f, c.y + 0.5f);
                bool clear = true;
                foreach (var k in keepClear) if (Factory.FlatDistance(k, at) < 7f) clear = false;
                foreach (var p in placed) if (Factory.FlatDistance(p, at) < 9f) clear = false;
                if (!clear) continue;
                placed.Add(at);
                if (rng.NextDouble() < 0.55) { SpikePlate.Make(transform, at); plates++; }
                else { Pendulum.Make(transform, at, alongX ? Vector3.right : Vector3.forward, (float)rng.NextDouble() * 6f); blades++; }
            }
        }

        // ------------------------------------------------------------------ the boss's room

        void Update()
        {
            var p = Player.I;
            if (p == null || !Dungeon.Active) return;
            if (boss == null || boss.IsDead && !sealedIn) FindBoss();
            if (boss == null) return;

            if (!sealedIn)
            {
                // the fight has started (it's been hurt) and we're in its room with it: the doors slam shut
                if (hasBossRoom && !boss.IsDead && boss.Health < boss.MaxHealth && Inside(p.transform.position) && Inside(boss.transform.position)) Seal();
                return;
            }
            bool fighting = !boss.IsDead && boss.Health < boss.MaxHealth;
            if (fighting) quietSince = Time.time;
            // the boss fell, it was left alone long enough to recover, or we fell: the bars go up
            if (boss.IsDead || p.IsDead || Time.time - quietSince > 15f) Open(boss.IsDead);
        }

        void FindBoss()
        {
            boss = null;
            foreach (var c in Combatant.All)
                if (c is Enemy e && !e.IsDead && e.Def.Boss) { boss = e; break; }
            if (boss == null) return;
            hasBossRoom = false;
            var cell = grid.WorldToCell(boss.transform.position);
            foreach (var r in rooms)
                if (r.Contains(cell)) { bossRoom = r; hasBossRoom = true; break; }
        }

        bool Inside(Vector3 p)
        {
            var c = grid.WorldToCell(p);
            return c.x >= bossRoom.xMin && c.x < bossRoom.xMax && c.y >= bossRoom.yMin && c.y < bossRoom.yMax;
        }

        void Seal()
        {
            sealedIn = true;
            quietSince = Time.time;
            doorways.Clear();
            // every floor cell just outside the room's edge is a way in
            for (int x = bossRoom.xMin - 1; x <= bossRoom.xMax; x++)
                for (int y = bossRoom.yMin - 1; y <= bossRoom.yMax; y++)
                {
                    bool edge = x == bossRoom.xMin - 1 || x == bossRoom.xMax || y == bossRoom.yMin - 1 || y == bossRoom.yMax;
                    if (!edge || grid.IsBlocked(x, y)) continue;
                    bool corner = (x == bossRoom.xMin - 1 || x == bossRoom.xMax) && (y == bossRoom.yMin - 1 || y == bossRoom.yMax);
                    if (corner) continue;
                    doorways.Add(new Vector2Int(x, y));
                }
            foreach (var d in doorways)
            {
                grid.SetClosed(d.x, d.y, true);
                bool acrossX = d.y == bossRoom.yMin - 1 || d.y == bossRoom.yMax; // a doorway in the south or north wall: bars run along X
                bars.Add(Portcullis(grid.CellToWorld(d), acrossX));
            }
            var p = Player.I;
            if (p != null) CameraRig.Shake(0.3f);
            Sfx.Play2D("gong", 0.5f, 0.7f);
            GameUI.Banner("The doors slam shut!", new Color(1f, 0.45f, 0.35f));
        }

        void Open(bool won)
        {
            sealedIn = false;
            foreach (var d in doorways) grid.SetClosed(d.x, d.y, false);
            doorways.Clear();
            foreach (var b in bars) if (b != null) b.gameObject.AddComponent<Raise>();
            bars.Clear();
            Sfx.Play2D("rubble", 0.6f, 0.8f);
            if (won) CameraRig.Shake(0.2f);
        }

        /// <summary>Iron bars dropping into a doorway (they fall, bounce and settle).</summary>
        Transform Portcullis(Vector3 at, bool acrossX)
        {
            var t = new GameObject("Portcullis").transform;
            t.SetParent(transform, false);
            t.position = at + Vector3.up * 2.6f;
            t.rotation = Quaternion.LookRotation(acrossX ? Vector3.forward : Vector3.right);
            var iron = new Color(0.2f, 0.2f, 0.22f);
            for (int i = 0; i < 5; i++)
                Factory.Prim(PrimitiveType.Cube, t, new Vector3(-0.4f + i * 0.2f, 1.2f, 0f), new Vector3(0.07f, 2.4f, 0.07f), iron);
            foreach (float y in new[] { 0.6f, 1.5f, 2.3f })
                Factory.Prim(PrimitiveType.Cube, t, new Vector3(0f, y, 0f), new Vector3(1f, 0.08f, 0.09f), iron * 1.2f);
            for (int i = 0; i < 5; i++) // spikes on the bottom
                Factory.Prim(PrimitiveType.Cube, t, new Vector3(-0.4f + i * 0.2f, -0.06f, 0f), new Vector3(0.05f, 0.14f, 0.05f), iron).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            t.gameObject.AddComponent<Drop>();
            return t;
        }

        class Drop : MonoBehaviour
        {
            float v;
            bool landed;
            void Update()
            {
                if (landed) return;
                v += 30f * Time.deltaTime;
                var p = transform.position;
                p.y = Mathf.Max(0f, p.y - v * Time.deltaTime);
                transform.position = p;
                if (p.y > 0f) return;
                landed = true;
                Sfx.Play("hit_armor", p, 0.9f, 0.1f, 40f);
                Sfx.Play("rubble", p, 0.6f, 0.1f, 40f);
                SpellFx.Dust(p, 0.8f);
            }
        }

        class Raise : MonoBehaviour
        {
            float t;
            void Update()
            {
                t += Time.deltaTime;
                transform.position += Vector3.up * Time.deltaTime * 1.4f; // grinding up
                if (t > 2f) Destroy(gameObject);
            }
        }
    }

    /// <summary>A pressure plate: step on it and, a heartbeat later, spikes shoot up around it. Then they sink and it rearms.</summary>
    public class SpikePlate : MonoBehaviour
    {
        Transform plate, spikes;
        float firedAt = -10f;
        bool armed = true, hit;

        public static void Make(Transform parent, Vector3 at)
        {
            var go = new GameObject("SpikePlate");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var s = go.AddComponent<SpikePlate>();
            var stone = new Color(0.32f, 0.3f, 0.29f);
            s.plate = Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.7f, 0.06f, 0.7f), stone * 1.15f).transform;
            // the holes the spikes come out of give it away, if you look
            for (int i = 0; i < 9; i++)
                Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(-0.3f + (i % 3) * 0.3f, 0.005f, -0.3f + (i / 3) * 0.3f) * 1.4f, new Vector3(0.08f, 0.01f, 0.08f), Color.black);
            s.spikes = new GameObject("Spikes").transform;
            s.spikes.SetParent(go.transform, false);
            var iron = new Color(0.55f, 0.55f, 0.58f);
            for (int i = 0; i < 9; i++)
            {
                var sp = Factory.Prim(PrimitiveType.Cube, s.spikes, new Vector3(-0.3f + (i % 3) * 0.3f, 0.3f, -0.3f + (i / 3) * 0.3f) * 1.4f, new Vector3(0.06f, 0.7f, 0.06f), iron);
                sp.transform.localRotation = Quaternion.Euler(Random.Range(-6f, 6f), 45f, Random.Range(-6f, 6f));
            }
            s.spikes.localPosition = Vector3.down * 0.8f;
        }

        void Update()
        {
            var p = Player.I;
            float since = Time.time - firedAt;
            if (armed && p != null && !p.IsDead && Factory.FlatDistance(p.transform.position, transform.position) < 0.5f)
            {
                armed = false;
                hit = false;
                firedAt = Time.time;
                plate.localPosition = new Vector3(0f, 0.005f, 0f);
                Sfx.Play("hit_stone", transform.position, 0.6f, 0.1f, 20f);
                since = 0f;
            }
            if (since < 0.35f) return; // the click... and a heartbeat
            if (since < 1.6f)
            {
                spikes.localPosition = Vector3.Lerp(spikes.localPosition, Vector3.zero, Time.deltaTime * 30f);
                if (!hit && since < 0.6f)
                {
                    hit = true;
                    Sfx.Play("swing_heavy", transform.position, 0.8f, 0.1f, 25f);
                    if (p != null && !p.IsDead && Factory.FlatDistance(p.transform.position, transform.position) < 1.1f)
                    {
                        p.TakeDamage(Mathf.Max(5f, p.MaxHealth * 0.1f), null);
                        SpellFx.Hit(p.transform.position + Vector3.up * 0.5f, new Color(0.6f, 0.05f, 0.05f), true, 10);
                    }
                }
                return;
            }
            spikes.localPosition = Vector3.MoveTowards(spikes.localPosition, Vector3.down * 0.8f, Time.deltaTime * 1.2f);
            if (since > 4f && !armed) { armed = true; plate.localPosition = new Vector3(0f, 0.03f, 0f); }
        }
    }

    /// <summary>A blade on a chain, swinging across a corridor from the ceiling: time your run.</summary>
    public class Pendulum : MonoBehaviour
    {
        Transform arm;
        Vector3 across;
        float phase, nextHit, lastSwish;

        public static void Make(Transform parent, Vector3 at, Vector3 corridorDir, float phase)
        {
            var go = new GameObject("Pendulum");
            go.transform.SetParent(parent, false);
            go.transform.position = at + Vector3.up * 2.4f;
            // it swings across the corridor's direction of travel
            go.transform.rotation = Quaternion.LookRotation(corridorDir);
            var p = go.AddComponent<Pendulum>();
            p.phase = phase;
            p.across = Vector3.Cross(corridorDir, Vector3.up);
            p.arm = new GameObject("Arm").transform;
            p.arm.SetParent(go.transform, false);
            var iron = new Color(0.3f, 0.3f, 0.32f);
            Factory.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.6f, 0.12f, 0.2f), iron); // the mount in the ceiling
            Factory.Prim(PrimitiveType.Cube, p.arm, new Vector3(0f, -0.9f, 0f), new Vector3(0.05f, 1.8f, 0.05f), iron);
            var blade = Factory.Prim(PrimitiveType.Cube, p.arm, new Vector3(0f, -1.95f, 0f), new Vector3(0.05f, 0.35f, 0.95f), new Color(0.75f, 0.75f, 0.8f));
            blade.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            Factory.Prim(PrimitiveType.Cube, p.arm, new Vector3(0f, -2.13f, 0f), new Vector3(0.02f, 0.06f, 0.9f), new Color(0.6f, 0.08f, 0.08f)); // a rusty edge
        }

        void Update()
        {
            float a = Mathf.Sin((Time.time + phase) * 2.2f) * 62f;
            arm.localRotation = Quaternion.Euler(0f, 0f, a);
            var p = Player.I;
            if (Mathf.Abs(a) < 12f && Time.time - lastSwish > 0.5f)
            {
                lastSwish = Time.time;
                if (p != null && Factory.FlatDistance(p.transform.position, transform.position) < 14f) Sfx.Play("swing_heavy", transform.position + Vector3.down * 2f, 0.35f, 0.1f, 14f);
            }
            if (p == null || p.IsDead || Time.time < nextHit || Mathf.Abs(a) > 18f) return;
            // the blade is at the bottom of its swing: is the hero in its path?
            var off = p.transform.position - new Vector3(transform.position.x, 0f, transform.position.z);
            if (Mathf.Abs(Vector3.Dot(off, transform.forward)) < 0.55f && Mathf.Abs(Vector3.Dot(off, across)) < 0.7f)
            {
                nextHit = Time.time + 1.2f;
                p.TakeDamage(Mathf.Max(6f, p.MaxHealth * 0.12f), null);
                SpellFx.Hit(p.transform.position + Vector3.up * 1.1f, new Color(0.6f, 0.05f, 0.05f), true, 12);
                Sfx.Play("hit_flesh", p.transform.position, 0.8f, 0.1f, 20f);
            }
        }
    }
}
