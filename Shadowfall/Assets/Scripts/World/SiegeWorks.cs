using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The things a hero can do in a siege besides fighting (server/invasion.js): light the two beacons by the gate (the
    /// wall's archers then loose fire arrows), and put out roofs the raiders' fire arrows set alight (the townsfolk
    /// form a bucket line, and a hero's bucket counts for more). Also routes the siege's events ("gev"): the guards'
    /// deeds, fire arrows, the warlord's taunts, the banner falling.
    /// </summary>
    public class SiegeWorks : MonoBehaviour
    {
        static SiegeWorks I;
        static readonly Color FireArrow = new Color(1f, 0.55f, 0.15f);

        readonly List<Beacon> beacons = new List<Beacon>();
        readonly Dictionary<int, RoofFire> fires = new Dictionary<int, RoofFire>();
        string beaconsFor;

        static SiegeWorks Get()
        {
            if (I == null) I = new GameObject("SiegeWorks").AddComponent<SiegeWorks>();
            return I;
        }

        /// <summary>The siege's state arrived: beacons and fires in line with it.</summary>
        public static void Sync()
        {
            var me = Get();
            var iv = Invasion.Current;
            bool on = iv != null && !Dungeon.Active && iv.phase != "won" && iv.phase != "lost";
            string key = on ? iv.town + "|" + iv.gate : null;
            if (key != me.beaconsFor)
            {
                foreach (var b in me.beacons) if (b != null) Destroy(b.gameObject);
                me.beacons.Clear();
                me.beaconsFor = key;
                if (on && iv.bc != null)
                    for (int i = 0; i < iv.bc.Length; i++) me.beacons.Add(Beacon.Create(i, new Vector3(iv.bc[i].x, 0f, iv.bc[i].z)));
            }
            if (on && iv.bc != null)
                for (int i = 0; i < iv.bc.Length && i < me.beacons.Count; i++) me.beacons[i].SetLit(iv.bc[i].l);

            // fires: new ones catch, changed ones grow or shrink, gone ones go out
            var seen = new HashSet<int>();
            if (on && iv.fr != null)
                foreach (var f in iv.fr)
                {
                    seen.Add(f.i);
                    if (!me.fires.TryGetValue(f.i, out var rf) || rf == null) me.fires[f.i] = rf = RoofFire.Create(f.i, new Vector3(f.x, 0f, f.z), iv.town);
                    rf.SetStrength(f.s);
                }
            var gone = new List<int>();
            foreach (var kv in me.fires) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone) { var rf = me.fires[id]; me.fires.Remove(id); if (rf != null) rf.GoOut(); }
        }

        /// <summary>A siege event ("gev").</summary>
        public static void Event(NetMsg m)
        {
            var hero = Player.I;
            switch (m.k)
            {
                case "say":
                    if (Enemy.ById.TryGetValue(m.mid, out var e) && e != null && !e.IsDead)
                    {
                        Speech.Say(e.transform, e.Height + 0.6f, m.msg);
                        e.Cheer();
                        if (hero != null && Factory.FlatDistance(hero.transform.position, e.transform.position) < 40f) Sfx.Play("roar", e.transform.position + Vector3.up, 0.6f, 0.1f, 50f);
                    }
                    break;
                case "fire":
                {
                    var to = Get().FireSpot(m.id, new Vector3(m.x, 0f, m.z));
                    if (m.sx != 0f || m.sz != 0f)
                    {
                        var from = new Vector3(m.sx, 1.5f, m.sz);
                        if (Enemy.ById.TryGetValue(m.mid, out var shooter) && shooter != null) shooter.PlayAttack(to);
                        Projectile.FireVisual(from, to, 18f, FireArrow, 0.3f, 80f).WithTrail(SpellFx.Trail.Fire).WithShape(Projectile.Shape.Arrow).OverWalls();
                    }
                    if (hero != null && Factory.FlatDistance(hero.transform.position, to) < 50f)
                    {
                        Sfx.Play("fire_cast", to, 0.6f, 0.1f, 50f);
                        GameUI.Log("Fire arrows! A roof is burning - douse it before it spreads.", FireArrow);
                    }
                    break;
                }
                case "fireout":
                    if (hero != null && m.by == hero.DisplayName) break; // we saw our own bucket do it
                    if (Get().fires.TryGetValue(m.id, out var rf) && rf != null) Splash(rf.transform.position + Vector3.up * 2f);
                    break;
                case "beacon":
                    if (m.i >= 0 && m.i < Get().beacons.Count)
                    {
                        var b = Get().beacons[(int)m.i];
                        Sfx.Play("fire_cast", b.transform.position + Vector3.up * 2f, 0.9f, 0.05f, 60f);
                        SpellFx.Flash(b.transform.position + Vector3.up * 2.4f, FireArrow, 9f, 3f, 0.6f);
                    }
                    break;
                case "rout":
                    if (Invasion.Current != null && hero != null && Factory.FlatDistance(hero.transform.position, Invasion.Gate) < 150f)
                    {
                        GameUI.Banner("The raiders' banner falls!", new Color(0.55f, 1f, 0.55f));
                        Sfx.Play2D("war_horn", 0.6f, 0.8f); // a broken, sagging call
                    }
                    break;
                default:
                    TownGuards.Event(m);
                    break;
            }
        }

        /// <summary>Our bucket of water did something ("doused"): experience, and the splash.</summary>
        public static void Doused(NetMsg m)
        {
            var p = Player.I;
            if (p == null) return;
            p.AddXp(m.xp);
            GameUI.Float(p.transform.position + Vector3.up * 2.6f, "+" + m.xp + " xp", new Color(0.6f, 0.85f, 1f), 1f);
        }

        /// <summary>Where a roof fire shows: the nearest burning-spot of a house (on its roof) within reach, else where the server put it.</summary>
        Vector3 FireSpot(int id, Vector3 at)
        {
            if (fires.TryGetValue(id, out var rf) && rf != null) return rf.Flame;
            return RoofFire.Snap(at);
        }

        public static void Splash(Vector3 at)
        {
            SpellFx.Emit(new SpellFx.P
            {
                Burst = 18, Duration = 0.1f, Life = new Vector2(0.4f, 0.8f), Speed = new Vector2(1.5f, 3.5f), Size = new Vector2(0.08f, 0.18f),
                Start = new Color(0.65f, 0.82f, 1f, 0.85f), End = new Color(0.65f, 0.82f, 1f, 0f), Gravity = 1.2f, Radius = 0.4f,
            }, at);
            SpellFx.Emit(new SpellFx.P
            {
                Burst = 6, Duration = 0.1f, Life = new Vector2(0.8f, 1.6f), Speed = new Vector2(0.3f, 0.8f), Size = new Vector2(0.4f, 0.8f),
                Start = new Color(0.85f, 0.85f, 0.85f, 0.5f), End = new Color(0.85f, 0.85f, 0.85f, 0f), Velocity = Vector3.up * 0.8f, Smoke = true, Grow = true, Radius = 0.5f,
            }, at); // the hiss of steam
            Sfx.Play("splash", at, 0.6f, 0.15f, 30f);
            Sfx.Play("sizzle", at, 0.5f, 0.15f, 30f);
        }

        // ------------------------------------------------------------------ the beacons

        public class Beacon : Interactable
        {
            int index;
            string litBy = null;
            PropFire flame;
            Light glow;

            public override string HoverText => string.IsNullOrEmpty(litBy) ? "Beacon\n<light it: the archers loose fire arrows>" : "Beacon\n<lit by " + litBy + ">";
            public override Color LabelColor => new Color(1f, 0.7f, 0.35f);
            public override float LabelHeight => 3.2f;
            public override bool CanInteract => string.IsNullOrEmpty(litBy);

            public static Beacon Create(int i, Vector3 at)
            {
                var go = new GameObject("Beacon");
                go.transform.position = at;
                var b = go.AddComponent<Beacon>();
                b.index = i;
                b.DisplayName = "Beacon";
                b.InteractRange = 3.5f;
                var iron = new Color(0.25f, 0.24f, 0.24f);
                var wood = new Color(0.4f, 0.28f, 0.16f);
                // a tripod of poles with an iron fire basket on top, piled with pitch-soaked wood
                for (int k = 0; k < 3; k++)
                {
                    var leg = Factory.Prim(PrimitiveType.Cylinder, go.transform, Vector3.up * 1.1f, new Vector3(0.1f, 1.15f, 0.1f), wood);
                    leg.transform.localRotation = Quaternion.Euler(12f, k * 120f, 0f);
                    leg.transform.localPosition = Quaternion.Euler(0f, k * 120f, 0f) * new Vector3(0f, 1.1f, -0.22f);
                }
                Factory.Prim(PrimitiveType.Cylinder, go.transform, Vector3.up * 2.3f, new Vector3(0.9f, 0.18f, 0.9f), iron);
                for (int k = 0; k < 4; k++)
                    Factory.Prim(PrimitiveType.Cube, go.transform, Vector3.up * 2.55f + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * 0.05f, new Vector3(0.7f, 0.1f, 0.1f), wood * 0.8f)
                        .transform.localRotation = Quaternion.Euler(0f, k * 45f, 0f);
                b.AddClickCollider(0.7f, 3f);
                return b;
            }

            public void SetLit(string by)
            {
                if (string.IsNullOrEmpty(by) || !string.IsNullOrEmpty(litBy)) { if (string.IsNullOrEmpty(litBy)) litBy = by; return; }
                litBy = by;
                flame = PropFire.Add(transform, transform.position + Vector3.up * 2.7f, new Color(1f, 0.55f, 0.18f), 1.1f, true);
                glow = new GameObject("BeaconLight").AddComponent<Light>();
                glow.transform.SetParent(transform, false);
                glow.transform.localPosition = Vector3.up * 3.2f;
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.6f, 0.25f);
                glow.range = 14f;
                glow.intensity = 2.2f;
            }

            void Update()
            {
                if (glow != null) glow.intensity = 1.9f + Mathf.PerlinNoise(Time.time * 4f, index) * 0.8f;
            }

            public override void Interact(Player p)
            {
                if (!string.IsNullOrEmpty(litBy)) return;
                p.FaceTowards(transform.position);
                NetClient.I?.LightBeacon(index);
                p.PlayInteract();
                p.Achievements.Add("beacons");
            }
        }

        // ------------------------------------------------------------------ a burning roof

        public class RoofFire : Interactable
        {
            int id, band = -1;
            float strength;
            PropFire flame;
            public Vector3 Flame { get; private set; }
            readonly List<SiegeLife.Extra> buckets = new List<SiegeLife.Extra>();
            float nextBucket;

            public override string HoverText => "Burning roof\n<throw water on it>";
            public override Color LabelColor => new Color(1f, 0.55f, 0.25f);
            public override float LabelHeight => 1.2f;

            /// <summary>The nearest house fire-spot to <paramref name="at"/> (on a roof), or a point above it.</summary>
            public static Vector3 Snap(Vector3 at)
            {
                Vector3 best = at + Vector3.up * 2.5f;
                float bd = 12f;
                foreach (var spots in WorldGenerator.HouseFireSpots)
                    foreach (var s in spots)
                    {
                        float d = Factory.FlatDistance(s, at);
                        if (d < bd) { bd = d; best = s; }
                    }
                return best;
            }

            public static RoofFire Create(int id, Vector3 at, string town)
            {
                var flameAt = Snap(at);
                var go = new GameObject("RoofFire");
                // where you stand to throw: walkable ground right by the house
                go.transform.position = TownLife.Walkable(new Vector3(flameAt.x, 0f, flameAt.z));
                var f = go.AddComponent<RoofFire>();
                f.id = id;
                f.Flame = flameAt;
                f.DisplayName = "Burning roof";
                f.InteractRange = 4.5f;
                f.AddClickCollider(1.4f, Mathf.Max(3f, flameAt.y + 1f));
                f.Helpers(town);
                return f;
            }

            /// <summary>Two of the townsfolk with buckets (if the hero is near enough to see them).</summary>
            void Helpers(string town)
            {
                var p = Player.I;
                if (p == null || Factory.FlatDistance(p.transform.position, transform.position) > 60f) return;
                var life = SiegeLife.Get();
                for (int k = 0; k < 2; k++)
                {
                    var stand = TownLife.Walkable(transform.position + Quaternion.Euler(0f, 60f + k * 120f, 0f) * Vector3.forward * 2.5f);
                    var from = TownLife.Walkable(stand + (stand - transform.position).normalized * 10f);
                    var b = life.Bucket(from, stand, Flame);
                    if (b != null) buckets.Add(b);
                }
            }

            public void SetStrength(int s)
            {
                strength = s;
                int nb = s >= 70 ? 2 : s >= 35 ? 1 : 0;
                if (nb == band) return;
                band = nb;
                if (flame != null) Destroy(flame.gameObject);
                flame = PropFire.Add(transform, Flame, new Color(1f, 0.5f, 0.15f), 0.8f + nb * 0.6f, true);
            }

            void Update()
            {
                // the bucket line's splashes
                if (buckets.Count > 0 && Time.time >= nextBucket)
                {
                    nextBucket = Time.time + Random.Range(1.6f, 2.6f);
                    var p = Player.I;
                    if (p != null && Factory.FlatDistance(p.transform.position, transform.position) < 40f)
                        SpellFx.Emit(new SpellFx.P
                        {
                            Burst = 10, Duration = 0.1f, Life = new Vector2(0.3f, 0.6f), Speed = new Vector2(1f, 2.5f), Size = new Vector2(0.06f, 0.14f),
                            Start = new Color(0.65f, 0.82f, 1f, 0.8f), End = new Color(0.65f, 0.82f, 1f, 0f), Gravity = 1.2f, Radius = 0.3f,
                        }, Flame + Vector3.down * 0.5f);
                }
            }

            public override void Interact(Player p)
            {
                p.FaceTowards(Flame);
                p.PlayInteract();
                NetClient.I?.Douse(id);
                Splash(Flame);
                p.Achievements.Add("buckets");
            }

            public void GoOut()
            {
                foreach (var b in buckets) if (b != null) b.FadeOut(3f);
                if (flame != null) Destroy(flame.gameObject);
                // a last plume of steam where it was
                SpellFx.Emit(new SpellFx.P
                {
                    Burst = 10, Duration = 0.1f, Life = new Vector2(1.5f, 2.5f), Speed = new Vector2(0.3f, 0.7f), Size = new Vector2(0.6f, 1.2f),
                    Start = new Color(0.6f, 0.6f, 0.6f, 0.5f), End = new Color(0.6f, 0.6f, 0.6f, 0f), Velocity = Vector3.up, Smoke = true, Grow = true, Radius = 0.8f,
                }, Flame);
                Destroy(gameObject, 0.1f);
            }
        }
    }
}
