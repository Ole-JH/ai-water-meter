using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// What a siege leaves behind (server/invasion.js, "after"): a victory feast round a bonfire in the town square
    /// (heroes who join it get the Heroes' Feast, +10% experience for ten minutes), carpenters mending the gate behind
    /// scaffolding, graves outside the wall for the guards who fell, and after a lost siege the townsfolk the raiders
    /// dragged to their camp, roped to a stake and calling for help. Built near the hero only.
    /// </summary>
    public class SiegeAftermath : MonoBehaviour
    {
        /// <summary>The Heroes' Feast's experience factor (the server says: 1.25).</summary>
        public static float FeastXp = 1.25f;
        static SiegeAftermath I;

        /// <summary>The Heroes' Feast runs until (Time.time); the hero's experience is 10% more meanwhile.</summary>
        public static float FeastUntil;
        public static bool Feasting
        {
            get
            {
                var p = Player.I;
                if (p != null && loadedFor != p) { loadedFor = p; Load(p); }
                return Time.time < FeastUntil;
            }
        }

        class Built
        {
            public string Key; public GameObject Root; public float Until;
            public readonly List<SiegeLife.Extra> People = new List<SiegeLife.Extra>();
            public readonly List<Vector3> Hammers = new List<Vector3>();     // where hammering is heard (the scaffold, the ruins)
            public readonly List<SiegeLife.Extra> Talkers = new List<SiegeLife.Extra>(); // the foreman, the reeve: they shout
        }
        readonly Dictionary<string, Built> built = new Dictionary<string, Built>();
        NetAfter last;
        float nextCheck, nextCheer, nextHammer;
        readonly System.Random rng = new System.Random();

        static readonly string[] cheers = { "Hail the defenders!", "To the heroes of the wall!", "Another round for the heroes!", "We held! We held!", "Sing, you lot, sing!", "Did you see that warlord run?" };
        static readonly string[] pleas = { "Help us! Please!", "Over here! Cut these ropes!", "They're going to take us away...", "Is anyone coming?" };
        static readonly string[] thanks = { "Bless you, hero!", "Home! We're going home!", "I thought we were done for!", "Thank you, thank you!" };

        static SiegeAftermath Get()
        {
            if (I == null) I = new GameObject("SiegeAftermath").AddComponent<SiegeAftermath>();
            return I;
        }

        /// <summary>Captives waiting to be freed (the tracker shows them), and until when (Time.time).</summary>
        public static NetAfterSpot Captives;
        static readonly Dictionary<string, float> repairsUntil = new Dictionary<string, float>();
        /// <summary>Carpenters are still at work on this town's gate (it stays battered until they're done: Rampart).</summary>
        public static bool Repairing(string town) => town != null && repairsUntil.TryGetValue(town, out var u) && Time.time < u;

        /// <summary>A town feasting its defenders now (the criers cry it), or null.</summary>
        public static string FeastTown;
        public static float CaptivesUntil;

        public static void Set(NetAfter a)
        {
            Get().last = a;
            Get().nextCheck = 0f;
            Captives = a != null && a.cp != null && a.cp.Length > 0 ? a.cp[0] : null;
            FeastTown = a != null && a.fe != null && a.fe.Length > 0 ? a.fe[0].k : null;
            repairsUntil.Clear();
            if (a != null && a.rp != null) foreach (var r in a.rp) if (r != null && r.left > 0) repairsUntil[r.k] = Time.time + r.left;
            if (Captives != null) CaptivesUntil = Time.time + Captives.left;
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        void Update()
        {
            var p = Player.I;
            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + 1f;
                Refresh(p);
            }
            if (p == null) return;
            // the feast: cheers, and the buff for whoever comes and joins it
            foreach (var b in built.Values)
            {
                if (!b.Key.StartsWith("fe|") || b.Root == null) continue;
                var fire = b.Root.transform.position;
                float d = Factory.FlatDistance(p.transform.position, fire);
                if (d < 30f && Time.time >= nextCheer && b.People.Count > 0)
                {
                    nextCheer = Time.time + R(3f, 6f);
                    var who = b.People[rng.Next(b.People.Count)];
                    if (who != null) { Speech.Say(who.transform, 2.4f, cheers[rng.Next(cheers.Length)]); who.Cheer(); }
                }
                if (d < 40f) Walker.Watch(fire, 3f); // Hollowmere's townsfolk gather round and cheer too
            }
            // the carpenters' hammers
            if (Time.time >= nextHammer)
            {
                nextHammer = Time.time + R(0.8f, 1.6f);
                foreach (var b in built.Values)
                {
                    if (!b.Key.StartsWith("rp|") || b.Root == null) continue;
                    foreach (var h in b.Hammers) // one blow from somewhere near, now and then
                        if (rng.NextDouble() < 0.35 && Factory.FlatDistance(p.transform.position, h) < 35f)
                            Sfx.Play(rng.NextDouble() < 0.6 ? "chop" : "anvil", h + Vector3.up * 1.2f, 0.28f, 0.15f, 35f);
                    if (rng.NextDouble() < 0.06)
                        foreach (var t in b.Talkers)
                            if (t != null && Factory.FlatDistance(p.transform.position, t.transform.position) < 30f && rng.NextDouble() < 0.5)
                                Speech.Say(t.transform, 2.5f, t.name.StartsWith("Reeve") ? Pick(reeveLines) : Pick(orders));
                }
            }
        }

        /// <summary>Builds what's near and listed, takes away what's gone.</summary>
        void Refresh(Player p)
        {
            var want = new HashSet<string>();
            var a = last;
            bool near(Vector3 at) => p != null && !Dungeon.Active && Factory.FlatDistance(p.transform.position, at) < 220f;
            if (a != null)
            {
                if (a.fe != null) foreach (var f in a.fe) { var at = new Vector3(f.x, 0f, f.z); if (f.left > 0 && near(at)) Want(want, "fe|" + f.k, () => Feast(at, f.k), f.left); }
                if (a.rp != null) foreach (var r in a.rp) { var at = new Vector3(r.x, 0f, r.z); if (r.left > 0 && near(at)) Want(want, "rp|" + r.k + "|" + r.g, () => Repairs(at, r.k), r.left); }
                if (a.gr != null) foreach (var g in a.gr) { var at = new Vector3(g.x, 0f, g.z); if (near(at)) Want(want, "gr|" + g.k + "|" + g.s, () => Graves(at, g.k, g.n, g.s), 99999f); }
                if (a.cp != null) foreach (var c in a.cp) { var at = new Vector3(c.x, 0f, c.z); if (c.left > 0 && near(at)) Want(want, "cp|" + c.k, () => BuildCaptives(at, c.k, c.n), c.left); }
            }
            var gone = new List<string>();
            foreach (var kv in built) if (!want.Contains(kv.Key) || Time.time > kv.Value.Until) gone.Add(kv.Key);
            foreach (var k in gone) TearDown(k);
        }

        void Want(HashSet<string> want, string key, System.Func<Built> make, float left)
        {
            want.Add(key);
            if (built.ContainsKey(key)) return;
            var b = make();
            if (b == null) return;
            b.Key = key;
            b.Until = Time.time + left + 1f;
            built[key] = b;
        }

        void TearDown(string key)
        {
            var b = built[key];
            built.Remove(key);
            if (key.StartsWith("cp|"))
            {
                // freed (or carried off): either way they're gone from here; if the captors are dead, they run home
                bool freed = b.Root != null && CaptorsGone(b.Root.transform.position);
                foreach (var e in b.People)
                {
                    if (e == null) continue;
                    if (freed)
                    {
                        e.Lying = false;
                        e.Tending = false;
                        e.StandUp();
                        e.Line = thanks[rng.Next(thanks.Length)];
                        var t = WorldGenerator.TownNamed(key.Substring(3));
                        e.Walk(t != null ? TownLife.Walkable(t.Center) : e.transform.position);
                        e.OnArrive = x => x.FadeOut(1f);
                        e.FadeOut(25f);
                    }
                    else e.FadeOut(0f);
                }
            }
            else foreach (var e in b.People) if (e != null) e.FadeOut(0.5f);
            if (b.Root != null) Destroy(b.Root, key.StartsWith("cp|") ? 2f : 0.5f);
        }

        static bool CaptorsGone(Vector3 at)
        {
            foreach (var e in Enemy.ById.Values) if (e != null && !e.IsDead && Factory.FlatDistance(e.transform.position, at) < 10f) return false;
            return true;
        }

        // ------------------------------------------------------------------ the feast

        Built Feast(Vector3 square, string town)
        {
            var b = new Built();
            var grid = WorldGrid.Instance;
            // the banquet table: a long trestle across the square, wherever there's room for it near the middle
            Vector3 at = TownLife.Walkable(square);
            foreach (var o in new[] { new Vector3(0f, 0f, -4f), new Vector3(0f, 0f, 4f), new Vector3(-4f, 0f, 0f), new Vector3(4f, 0f, 0f), new Vector3(3f, 0f, -5f), new Vector3(-3f, 0f, 5f), new Vector3(0f, 0f, -6f) })
            {
                var c = square + o;
                bool free = true;
                for (float x = -3.5f; x <= 3.5f && free; x += 1f)
                    for (float z = -1.5f; z <= 1.5f && free; z += 1f)
                        if (grid != null && !grid.IsWalkable(c + new Vector3(x, 0f, z))) free = false;
                if (free) { at = c; break; }
            }
            b.Root = new GameObject("VictoryFeast");
            b.Root.transform.position = at;
            var t = b.Root.transform;
            var wood = new Color(0.55f, 0.38f, 0.22f);
            FeastArt.Table(t, at, rng);
            var table = b.Root.AddComponent<FeastTable>();
            table.Init(town);
            // the bonfire, a little way off, and its light
            var fire = TownLife.Walkable(at + Vector3.forward * 5.5f);
            for (int i = 0; i < 6; i++)
                Factory.PrimAt(PrimitiveType.Cylinder, t, fire + Vector3.up * 0.35f, new Vector3(0.22f, 0.9f, 0.22f), new Color(0.35f, 0.24f, 0.14f))
                    .transform.rotation = Quaternion.Euler(60f, i * 60f, 0f);
            PropFire.Add(t, fire + Vector3.up * 0.7f, new Color(1f, 0.6f, 0.2f), 1.6f, true);
            var l = new GameObject("FeastLight").AddComponent<Light>();
            l.transform.SetParent(t, false);
            l.transform.position = fire + Vector3.up * 2.2f;
            l.type = LightType.Point;
            l.color = new Color(1f, 0.65f, 0.3f);
            l.range = 14f;
            l.intensity = 2f;
            // a roast turning on a spit over the fire
            var spit = new GameObject("Spit").transform;
            spit.SetParent(t, false);
            spit.position = fire + Vector3.up * 1.15f;
            foreach (float s2 in new[] { -1.1f, 1.1f })
            {
                Factory.PrimAt(PrimitiveType.Cube, t, fire + new Vector3(s2, 0.6f, 0f), new Vector3(0.08f, 1.2f, 0.08f), new Color(0.3f, 0.2f, 0.12f)).transform.rotation = Quaternion.Euler(0f, 0f, s2 > 0 ? -10f : 10f);
            }
            Factory.PrimAt(PrimitiveType.Cylinder, spit, spit.position, new Vector3(0.04f, 1.2f, 0.04f), new Color(0.4f, 0.4f, 0.42f)).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            FeastArt.SpitBoar(spit);
            // bunting on poles round the square, lanterns, a cask of ale with a barkeep
            var flags = new[] { new Color(0.85f, 0.2f, 0.18f), new Color(0.95f, 0.8f, 0.25f), new Color(0.25f, 0.45f, 0.85f), new Color(0.3f, 0.7f, 0.35f) };
            var corners = new Vector3[4];
            for (int k = 0; k < 4; k++)
            {
                var c = TownLife.Walkable(at + new Vector3(k < 2 ? -5.5f : 5.5f, 0f, k % 2 == 0 ? -4f : 5f));
                corners[k] = c;
                Factory.PrimAt(PrimitiveType.Cylinder, t, c + Vector3.up * 2f, new Vector3(0.16f, 2f, 0.16f), wood * 0.8f);
                PropFire.Add(t, c + Vector3.up * 4.1f, new Color(1f, 0.75f, 0.35f), 0.25f, false); // a lantern on top
            }
            foreach (var (p0, p1) in new[] { (corners[0], corners[3]), (corners[1], corners[2]), (corners[0], corners[1]), (corners[2], corners[3]), (corners[0], corners[2]), (corners[1], corners[3]) })
                FeastArt.Bunting(t, p0 + Vector3.up * 3.8f, p1 + Vector3.up * 3.8f, flags, Factory.FlatDistance(p0, p1) > 9.5f);
            // braziers at the table's ends, hay bales by the fire to sit on
            foreach (float x in new[] { -4.5f, 4.5f })
            {
                var br = TownLife.Walkable(at + new Vector3(x, 0f, -1.9f));
                if (ArtLibrary.Spawn("Graveyard/fire-basket", t, br - at, 1.3f, ArtLibrary.Fit.Height, 0f, false) != null)
                    PropFire.Add(t, br + Vector3.up * 1.25f, new Color(1f, 0.6f, 0.25f), 0.45f, false);
            }
            var bales = new List<Vector3>();
            foreach (float a2 in new[] { 130f, 230f })
            {
                var hb = TownLife.Walkable(fire + Quaternion.Euler(0f, a2, 0f) * Vector3.forward * 2.3f);
                if (ArtLibrary.Spawn("Seasonal/hay-bale", t, hb - at, 0.55f, ArtLibrary.Fit.Height, a2, false) != null) bales.Add(hb);
            }
            var cask = TownLife.Walkable(at + new Vector3(5f, 0f, 1.6f));
            ArtLibrary.Spawn("Props/barrel_large", t, cask - at, 1.1f, ArtLibrary.Fit.Height, 0f, false);
            ArtLibrary.Spawn("Props/barrel_small_stack", t, cask - at + new Vector3(0.9f, 0f, 0.8f), 1f, ArtLibrary.Fit.Height, 30f, false);
            // the townsfolk: some sat at the benches, the rest dancing and cheering round the fire
            var life = SiegeLife.Get();
            string[] models = { "Characters/Keeper", "Characters/RogueHooded", "Characters/Mage", "Characters/Rogue", "Characters/Knight" };
            // ten sat along the benches facing each other, two on the bales by the fire, the rest dancing and cheering round it
            foreach (float z in new[] { -1f, 1f })
                foreach (float x in FeastArt.SeatX)
                {
                    var seat = at + new Vector3(x, 0f, z * FeastArt.SeatZ);
                    var e = life.Person("Villager", models[rng.Next(models.Length)], seat, R(1.75f, 1.95f));
                    e.FaceAt = at + new Vector3(x, 0f, 0f);
                    e.Party = "sit";
                    e.SeatY = FeastArt.BenchY;
                    b.People.Add(e);
                }
            foreach (var hb in bales)
            {
                var e = life.Person("Villager", models[rng.Next(models.Length)], hb, R(1.75f, 1.9f));
                e.FaceAt = fire; e.Party = "sit"; e.SeatY = 0.5f;
                b.People.Add(e);
            }
            for (int i = 0; i < 7; i++)
            {
                var spot = TownLife.Walkable(fire + Quaternion.Euler(0f, i * 52f + R(-10f, 10f), 0f) * Vector3.forward * R(3.6f, 4.6f));
                var e = life.Person("Villager", models[(i + rng.Next(3)) % models.Length], spot, i == 3 ? 1.2f : R(1.75f, 1.95f));
                e.FaceAt = fire;
                e.Party = rng.NextDouble() < 0.45 ? "dance" : rng.NextDouble() < 0.5 ? "cheer" : "clap";
                b.People.Add(e);
            }
            // the barkeep at the cask, a minstrel, and a ring of dancers going round the fire
            var keep = life.Person("Barkeep", "Characters/Barbarian", TownLife.Walkable(cask + new Vector3(-0.2f, 0f, -1.1f)), 1.9f);
            keep.Tending = true; keep.FaceAt = cask; b.People.Add(keep);
            var minstrel = life.Person("Minstrel", "Characters/Rogue", TownLife.Walkable(at + new Vector3(-4.8f, 0f, 2.2f)), 1.8f);
            minstrel.FaceAt = at; minstrel.Party = "cheer"; b.People.Add(minstrel);
            for (int i = 0; i < 7; i++)
            {
                var d = life.Person("Dancer", models[(i + 2) % models.Length], TownLife.Walkable(fire + Quaternion.Euler(0f, i * 51f, 0f) * Vector3.forward * 3.2f), i == 3 ? 1.2f : R(1.7f, 1.9f));
                d.OrbitAbout = fire;
                d.OrbitR = R(2.9f, 3.4f);
                d.OrbitSpeed = 0.55f;
                d.OrbitAngle = i * (Mathf.PI * 2f / 7f);
                b.People.Add(d);
            }
            b.Root.AddComponent<FeastLife>().Init(b.People, spit, fire, minstrel, keep);
            Sfx.Play("bell", at + Vector3.up * 3f, 0.8f, 0.05f, 80f);
            return b;
        }

        /// <summary>The feast going on: the spit turning, toasts, the minstrel's songs, the barkeep pouring, fireworks.</summary>
        class FeastLife : MonoBehaviour
        {
            List<SiegeLife.Extra> people;
            Transform spit;
            Vector3 fire;
            SiegeLife.Extra minstrel, keep;
            float nextToast, nextSong, nextFirework, nextPour;
            static readonly string[] toasts = { "To the defenders!", "To the town that held!", "To the fallen!", "To the heroes of the wall!" };
            static readonly string[] songs = { "~ Oh the gate it held, and the raiders fled ~", "~ Fill the cups and raise them high ~", "~ The warlord came, the warlord ran ~", "~ Dance, dance, the walls still stand ~" };
            static readonly Color[] sparks = { new Color(1f, 0.5f, 0.2f), new Color(0.4f, 0.7f, 1f), new Color(1f, 0.9f, 0.3f), new Color(0.8f, 0.4f, 1f), new Color(0.5f, 1f, 0.5f) };

            public void Init(List<SiegeLife.Extra> p, Transform s, Vector3 f, SiegeLife.Extra m, SiegeLife.Extra k)
            {
                people = p; spit = s; fire = f; minstrel = m; keep = k;
                nextToast = Time.time + Random.Range(8f, 14f);
                nextSong = Time.time + Random.Range(3f, 6f);
                nextFirework = Time.time + Random.Range(4f, 8f);
            }

            void Update()
            {
                if (spit != null) spit.Rotate(Vector3.right, 40f * Time.deltaTime, Space.Self);
                var hero = Player.I;
                if (hero == null || Factory.FlatDistance(hero.transform.position, fire) > 60f) return;
                if (Time.time >= nextToast)
                {
                    // one raises a cup, everybody cheers
                    nextToast = Time.time + Random.Range(18f, 30f);
                    var who = people[Random.Range(0, people.Count)];
                    if (who != null) Speech.Say(who.transform, 2.4f, toasts[Random.Range(0, toasts.Length)]);
                    foreach (var e in people) if (e != null && e.SeatY < 0f && Random.value < 0.75f) e.Cheer();
                    Sfx.Play("coins", fire + Vector3.up, 0.25f, 0.2f, 30f); // cups clinking
                }
                if (minstrel != null && Time.time >= nextSong)
                {
                    nextSong = Time.time + Random.Range(9f, 15f);
                    Speech.Say(minstrel.transform, 2.4f, songs[Random.Range(0, songs.Length)]);
                }
                if (keep != null && Time.time >= nextPour)
                {
                    nextPour = Time.time + Random.Range(4f, 8f);
                    if (Random.value < 0.4f) Speech.Say(keep.transform, 2.4f, Random.value < 0.5f ? "On the house tonight!" : "Who's dry? Who's dry?");
                }
                if (Time.time >= nextFirework && SpellFx.Ready)
                {
                    // a rocket goes up from behind the fire and bursts over the square
                    nextFirework = Time.time + Random.Range(3.5f, 7f);
                    var c = sparks[Random.Range(0, sparks.Length)];
                    var at = fire + new Vector3(Random.Range(-6f, 6f), Random.Range(11f, 16f), Random.Range(-6f, 6f));
                    SpellFx.Emit(new SpellFx.P
                    {
                        Burst = 60, Duration = 0.1f, Life = new Vector2(0.9f, 1.6f), Speed = new Vector2(3f, 6f), Size = new Vector2(0.12f, 0.24f),
                        Start = new Color(1f, 1f, 1f, 1f), Mid = c, End = new Color(c.r, c.g, c.b, 0f), Gravity = 0.6f, Drag = 1.5f, Radius = 0.2f, Stretch = true,
                    }, at);
                    SpellFx.Flash(at, c, 18f, 1.6f, 0.5f);
                    Sfx.Play("boom", at, 0.25f, 0.25f, 90f);
                }
            }
        }

        /// <summary>Our plate at a victory feast ("fed"): +25% experience for fifteen minutes, kept over a reload.</summary>
        public static void Fed(NetMsg m)
        {
            var p = Player.I;
            if (p == null) return;
            FeastXp = m.mul > 1f ? m.mul : 1.25f;
            FeastUntil = Time.time + m.s;
            Save(p);
            GameUI.Banner("Heroes' Feast: +" + Mathf.RoundToInt((FeastXp - 1f) * 100f) + "% experience for " + (m.s / 60) + " minutes", UISkin.Gold);
            GameUI.Log("You eat and drink your fill at the victory feast: +" + Mathf.RoundToInt((FeastXp - 1f) * 100f) + "% experience for " + (m.s / 60) + " minutes.", UISkin.Gold);
            Sfx.Play2D("levelup", 0.5f, 1.2f);
            p.Achievements.Add("feasts");
        }

        static string Key(Player p) => "sf_feast_" + p.DisplayName;
        static void Save(Player p)
        {
            try
            {
                double end = (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalSeconds + (FeastUntil - Time.time);
                PlayerPrefs.SetString(Key(p), end.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" + FeastXp.ToString(System.Globalization.CultureInfo.InvariantCulture));
                PlayerPrefs.Save();
            }
            catch (System.Exception) { }
        }

        /// <summary>A Heroes' Feast still running from before a reload (per character).</summary>
        static void Load(Player p)
        {
            try
            {
                var v = PlayerPrefs.GetString(Key(p), "");
                if (string.IsNullOrEmpty(v)) return;
                var parts = v.Split('|');
                double end = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                double left = end - (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalSeconds;
                if (left <= 0) return;
                FeastUntil = Time.time + (float)left;
                if (parts.Length > 1) FeastXp = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (System.Exception) { }
        }

        static Player loadedFor;

        /// <summary>The feast's table: click it to eat (the server lets each hero eat once a feast).</summary>
        public class FeastTable : Interactable
        {
            public override string HoverText => "Victory Feast\n<eat and drink: +25% experience for 15 minutes, once>";
            public override Color LabelColor => UISkin.Gold;
            public override float LabelHeight => 1.8f;
            public void Init(string town) { DisplayName = "Victory Feast"; InteractRange = 2.6f; AddClickCollider(1.6f, 1.2f); }
            public override void Interact(Player p)
            {
                p.FaceTowards(transform.position);
                p.PlayInteract();
                Sfx.Play("potion", transform.position + Vector3.up, 0.5f, 0.1f);
                NetClient.I?.Eat();
            }
        }

        // ------------------------------------------------------------------ the carpenters at the gate

        Built Repairs(Vector3 gate, string town)
        {
            var t = WorldGenerator.TownNamed(town);
            if (t == null) return null;
            var inward = Factory.Flat(t.Center - gate).normalized;
            var across = Vector3.Cross(Vector3.up, inward);
            var b = new Built();
            b.Root = new GameObject("GateRepairs");
            var at = gate + inward * 2.2f;
            b.Root.transform.position = at;
            var tr = b.Root.transform;
            var wood = new Color(0.62f, 0.48f, 0.3f);
            // scaffolding: poles either side, two planked platforms
            foreach (float s in new[] { -2.4f, 2.4f })
                foreach (float d in new[] { 0f, 1.2f })
                    Factory.PrimAt(PrimitiveType.Cube, tr, at + across * s + inward * d + Vector3.up * 2f, new Vector3(0.12f, 4f, 0.12f), wood);
            foreach (float h in new[] { 1.4f, 2.9f })
                Factory.PrimAt(PrimitiveType.Cube, tr, at + inward * 0.6f + Vector3.up * h, Abs(across * 5f + inward * 1.3f) + Vector3.up * 0.08f, wood * 0.9f);
            // fresh planks stacked by it, a sawhorse
            var pile = at + inward * 3f + across * 3.5f;
            for (int i = 0; i < 6; i++) Factory.PrimAt(PrimitiveType.Cube, tr, pile + Vector3.up * (0.08f + i * 0.1f), Abs(across * 0.3f + inward * 2.4f) + Vector3.up * 0.08f, new Color(0.78f, 0.62f, 0.4f));
            var life = SiegeLife.Get();
            string[] models = { "Characters/Barbarian", "Characters/Keeper", "Characters/Rogue", "Characters/RogueHooded", "Characters/Knight" };
            string M() => models[rng.Next(models.Length)];
            // carpenters up on the scaffold (two on the top deck, one below), hammering at the new gate
            float[] decks = { 2.95f, 2.95f, 1.45f };
            float[] along = { -1.4f, 1.3f, 0.2f };
            for (int i = 0; i < 3; i++)
            {
                var foot = TownLife.Walkable(at + inward * 0.7f + across * along[i]);
                var c = life.Person("Carpenter", M(), foot, R(1.8f, 1.95f));
                c.FaceAt = gate;
                c.Tending = true;
                c.RaiseTo = decks[i];
                b.People.Add(c);
                b.Hammers.Add(foot + Vector3.up * decks[i]);
            }
            // two hauling fresh planks from the pile to the foot of the scaffold, and the foreman giving orders
            for (int i = 0; i < 2; i++)
            {
                var hauler = life.Person("Labourer", M(), TownLife.Walkable(pile + across * R(-1f, 1f)), R(1.75f, 1.9f));
                hauler.Shuttle(TownLife.Walkable(pile - across * 0.8f + inward * R(-0.5f, 0.5f)), TownLife.Walkable(at + inward * 2f + across * R(-1.5f, 1.5f)));
                b.People.Add(hauler);
            }
            var boss = life.Person("Foreman", "Characters/Knight", TownLife.Walkable(at + inward * 4.5f - across * 2f), 1.95f);
            boss.FaceAt = at;
            boss.Party = "point";
            boss.Line = Pick(orders);
            b.People.Add(boss);
            b.Talkers.Add(boss);

            // around the town after a sack: crews at the fallen-in houses, timber hauled from a lumber pile
            var ruins = Sack.RuinsOf(town);
            if (ruins != null && ruins.Count > 0)
            {
                var yard = TownLife.Walkable(t.Center + inward * 6f + across * 6f);
                for (int i = 0; i < 8; i++)
                    Factory.PrimAt(PrimitiveType.Cube, tr, yard + new Vector3(R(-0.3f, 0.3f), 0.1f + (i / 2) * 0.16f, (i % 2) * 0.5f), new Vector3(2.4f, 0.14f, 0.3f), new Color(0.72f, 0.56f, 0.36f));
                Factory.PrimAt(PrimitiveType.Cube, tr, yard + new Vector3(0f, 0.25f, 1.8f), new Vector3(1.2f, 0.5f, 0.8f), new Color(0.5f, 0.5f, 0.5f)); // cut stone
                int crews = Mathf.Min(6, ruins.Count);
                for (int k = 0; k < crews; k++)
                {
                    var ruin = ruins[k];
                    var hammerer = life.Person("Builder", M(), TownLife.Walkable(ruin + new Vector3(R(-1.5f, 1.5f), 0f, R(-1.5f, 1.5f))), R(1.75f, 1.95f));
                    hammerer.FaceAt = ruin;
                    hammerer.Tending = true;
                    b.People.Add(hammerer);
                    b.Hammers.Add(ruin);
                    var hauler = life.Person("Labourer", M(), TownLife.Walkable(yard + new Vector3(R(-1f, 1f), 0f, R(-1f, 1f))), R(1.75f, 1.9f));
                    hauler.Shuttle(TownLife.Walkable(yard + new Vector3(R(-1.2f, 1.2f), 0f, -1f)), TownLife.Walkable(ruin + new Vector3(R(-2f, 2f), 0f, R(-2f, 2f))));
                    b.People.Add(hauler);
                    if (k % 2 == 0)
                    {
                        // clearing the rubble: shovelling it into a barrow
                        var clearer = life.Person("Labourer", M(), TownLife.Walkable(ruin + new Vector3(R(-2f, 2f), 0f, R(-2f, 2f))), R(1.75f, 1.9f));
                        clearer.FaceAt = ruin;
                        clearer.Tending = true;
                        b.People.Add(clearer);
                    }
                }
                var reeve = life.Person("Reeve Halden", "Characters/Keeper", TownLife.Walkable(yard + inward * 2f), 1.9f);
                reeve.FaceAt = yard;
                reeve.Party = "point";
                b.People.Add(reeve);
                b.Talkers.Add(reeve);
            }
            return b;
        }

        static readonly string[] reeveLines = { "Timber to the baker's first, then the smithy!", "Clear that rubble before you lay a stone!", "We'll be back on our feet by market day.", "Careful, that wall's still warm!" };
        static readonly string[] orders = { "Mind that beam! Up, up!", "Hammer, not your thumb, Wat!", "Planks to the left, lads!", "This gate'll stand a hundred years!", "Faster! The raiders won't wait for us next time!", "Steady... and drop it in!" };
        static string Pick(string[] a) => a[Random.Range(0, a.Length)];

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        // ------------------------------------------------------------------ graves for the fallen guards

        /// <summary>
        /// The siege's dead in the town's graveyard (TownGraveyard): any the hero didn't see carried there are dug now, and
        /// if so the townsfolk come to mourn them a while.
        /// </summary>
        Built Graves(Vector3 gate, string town, int n, int seed)
        {
            var yard = TownGraveyard.For(town);
            if (yard == null || n <= 0) return null;
            int made = yard.Ensure(n);
            var b = new Built { Root = new GameObject("Graves " + town) };
            b.Root.transform.position = yard.Centre;
            if (made > 0)
            {
                var life = SiegeLife.Get();
                string[] models = { "Characters/Keeper", "Characters/RogueHooded", "Characters/Mage", "Characters/Rogue" };
                int k = Mathf.Min(6, 2 + made);
                for (int i = 0; i < k; i++)
                {
                    var spot = TownLife.Walkable(yard.Centre + Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward * R(2.5f, 4f));
                    var m = life.Person("Mourner", models[rng.Next(models.Length)], spot, R(1.7f, 1.9f));
                    m.FaceAt = yard.Centre;
                    m.Party = rng.NextDouble() < 0.4 ? "sit" : "bow";
                    m.FadeOut(R(150f, 260f));
                }
            }
            return b;
        }

        // ------------------------------------------------------------------ captives at the raiders' camp

        Built BuildCaptives(Vector3 camp, string town, int n)
        {
            var b = new Built();
            b.Root = new GameObject("Captives");
            b.Root.transform.position = camp;
            var tr = b.Root.transform;
            // a stake, and the captives sat round it, roped
            Factory.PrimAt(PrimitiveType.Cylinder, tr, camp + Vector3.up * 1f, new Vector3(0.18f, 1f, 0.18f), new Color(0.35f, 0.25f, 0.15f));
            var life = SiegeLife.Get();
            string[] models = { "Characters/Keeper", "Characters/RogueHooded", "Characters/Rogue", "Characters/Mage" };
            for (int i = 0; i < n; i++)
            {
                var at = camp + Quaternion.Euler(0f, i * (360f / n), 0f) * Vector3.forward * 1.1f;
                var e = life.Person("Captive", models[i % models.Length], at, i == n - 1 ? 1.2f : R(1.7f, 1.9f), false);
                e.FaceAt = camp + (at - camp) * 3f;
                e.Party = "sit";
                b.People.Add(e);
                // the rope from them to the stake
                var rope = Factory.PrimAt(PrimitiveType.Cube, tr, Vector3.Lerp(at, camp, 0.5f) + Vector3.up * 0.6f, new Vector3(0.03f, 0.03f, 1.1f), new Color(0.75f, 0.65f, 0.45f));
                rope.transform.rotation = Quaternion.LookRotation(camp - at);
            }
            b.Root.AddComponent<CaptiveCries>().Init(b.People, pleas);
            return b;
        }

        class CaptiveCries : MonoBehaviour
        {
            List<SiegeLife.Extra> people;
            string[] lines;
            float next;
            public void Init(List<SiegeLife.Extra> p, string[] l) { people = p; lines = l; }
            void Update()
            {
                if (Time.time < next || people == null || people.Count == 0) return;
                next = Time.time + Random.Range(5f, 9f);
                var hero = Player.I;
                if (hero == null || Factory.FlatDistance(hero.transform.position, transform.position) > 35f) return;
                var who = people[Random.Range(0, people.Count)];
                if (who != null) Speech.Say(who.transform, 1.6f, lines[Random.Range(0, lines.Length)]);
            }
        }
    }

    /// <summary>The towns' siege record (server/invasion.js, "chron"): held, fell, spared, the last defenders, prosperity.</summary>
    public static class SiegeChronicle
    {
        public static NetChron[] Records = new NetChron[0];
        public static void Set(NetChron[] list) { if (list != null) Records = list; }

        public static NetChron For(string town)
        {
            foreach (var c in Records) if (c != null && c.k == town) return c;
            return null;
        }

        public static string LastWord(NetChron c)
        {
            if (c == null || c.ago < 0) return "never besieged";
            string what = c.last == "h" ? "held" : c.last == "f" ? "fell" : "was spared";
            return what + " " + Ago(c.ago);
        }

        public static string Ago(int s) => s < 90 ? "just now" : s < 5400 ? (s / 60) + " minutes ago" : s < 172800 ? (s / 3600) + " hours ago" : (s / 86400) + " days ago";

        public static string ProsperityWord(int p) => p >= 3 ? "Thriving" : p == 2 ? "Prosperous" : p == 1 ? "Doing well" : p == 0 ? "Getting by" : p == -1 ? "Struggling" : p == -2 ? "Impoverished" : "Ruined";

        public static Color ProsperityColor(int p) => p > 0 ? new Color(0.55f, 1f, 0.55f) : p < 0 ? new Color(1f, 0.55f, 0.45f) : UISkin.Cream;

        /// <summary>"8% cheaper" / "8% dearer" (the merchants' prices there), or "".</summary>
        public static string PriceWord(int p) => p == 0 ? "" : (Mathf.Abs(p) * 4) + "% " + (p > 0 ? "cheaper" : "dearer");
    }

    /// <summary>The siege record board by a walled town's crier: click it for every town's record.</summary>
    public class MemorialBoard : Interactable
    {
        public string Town;
        public override string HoverText => "Siege Record\n<the towns' defences and their defenders>";
        public override Color LabelColor => new Color(0.85f, 0.8f, 0.65f);
        public override float LabelHeight => 2.6f;

        public static void SpawnAll()
        {
            var grid = WorldGrid.Instance;
            foreach (var c in TownCrier.Criers)
            {
                if (c == null) continue;
                var t = WorldGenerator.TownAt(c.transform.position);
                if (t == null) continue;
                var at = c.transform.position + c.transform.right * 2.6f;
                if (grid != null && !grid.IsWalkable(at)) at = TownLife.Walkable(at);
                var go = new GameObject("SiegeRecord");
                go.transform.position = at;
                go.transform.rotation = c.transform.rotation;
                var b = go.AddComponent<MemorialBoard>();
                b.Town = t.Name;
                b.DisplayName = "Siege Record";
                b.InteractRange = 2.4f;
                var wood = new Color(0.42f, 0.3f, 0.18f);
                foreach (float x in new[] { -0.7f, 0.7f }) Factory.PrimAt(PrimitiveType.Cube, go.transform, at + go.transform.right * x + Vector3.up * 0.95f, new Vector3(0.12f, 1.9f, 0.12f), wood);
                var board = Factory.PrimAt(PrimitiveType.Cube, go.transform, at + Vector3.up * 1.4f, new Vector3(1.5f, 0.95f, 0.08f), new Color(0.3f, 0.22f, 0.14f));
                board.transform.rotation = go.transform.rotation;
                // pinned notices, a carved shield on top
                for (int i = 0; i < 4; i++)
                {
                    var n = Factory.PrimAt(PrimitiveType.Cube, go.transform, at + go.transform.right * (-0.45f + i * 0.3f) + Vector3.up * (1.3f + (i % 2) * 0.2f) + go.transform.forward * 0.05f, new Vector3(0.24f, 0.3f, 0.01f), new Color(0.9f, 0.86f, 0.72f));
                    n.transform.rotation = go.transform.rotation * Quaternion.Euler(0f, 0f, (i - 1.5f) * 4f);
                }
                Factory.PrimAt(PrimitiveType.Cube, go.transform, at + Vector3.up * 2.05f, new Vector3(0.45f, 0.5f, 0.1f), new Color(0.6f, 0.15f, 0.12f)).transform.rotation = go.transform.rotation;
                b.AddClickCollider(0.8f, 2.2f);
            }
        }

        public override void Interact(Player p) => GameUI.I?.OpenChronicle(Town);
    }
}
