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
        public const float FeastXp = 1.1f, FeastSeconds = 600f;
        static SiegeAftermath I;

        /// <summary>The Heroes' Feast runs until (Time.time); the hero's experience is 10% more meanwhile.</summary>
        public static float FeastUntil;
        public static bool Feasting => Time.time < FeastUntil;

        class Built { public string Key; public GameObject Root; public float Until; public readonly List<SiegeLife.Extra> People = new List<SiegeLife.Extra>(); }
        readonly Dictionary<string, Built> built = new Dictionary<string, Built>();
        readonly HashSet<string> feastsJoined = new HashSet<string>();
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
        public static float CaptivesUntil;

        public static void Set(NetAfter a)
        {
            Get().last = a;
            Get().nextCheck = 0f;
            Captives = a != null && a.cp != null && a.cp.Length > 0 ? a.cp[0] : null;
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
                if (d < 12f && !feastsJoined.Contains(b.Key))
                {
                    feastsJoined.Add(b.Key);
                    FeastUntil = Time.time + FeastSeconds;
                    GameUI.Banner("Heroes' Feast: +10% experience for 10 minutes", UISkin.Gold);
                    GameUI.Log("The town toasts its defenders. You eat and drink your fill: +10% experience for ten minutes.", UISkin.Gold);
                    Sfx.Play2D("levelup", 0.5f, 1.2f);
                    p.Achievements.Add("feasts");
                }
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
                    if (b.Key.StartsWith("rp|") && b.Root != null && Factory.FlatDistance(p.transform.position, b.Root.transform.position) < 35f)
                        Sfx.Play(rng.NextDouble() < 0.5 ? "chop" : "anvil", b.Root.transform.position + Vector3.up * 1.5f, 0.3f, 0.15f, 35f);
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
            var at = TownLife.Walkable(square + new Vector3(2f, 0f, 2f));
            b.Root = new GameObject("VictoryFeast");
            b.Root.transform.position = at;
            var t = b.Root.transform;
            // the bonfire: a stack of logs, a big fire, its light
            for (int i = 0; i < 6; i++)
                Factory.Prim(PrimitiveType.Cylinder, t, at + Vector3.up * 0.35f, new Vector3(0.22f, 0.9f, 0.22f), new Color(0.35f, 0.24f, 0.14f))
                    .transform.rotation = Quaternion.Euler(60f, i * 60f, 0f);
            PropFire.Add(t, at + Vector3.up * 0.7f, new Color(1f, 0.6f, 0.2f), 1.6f, true);
            var l = new GameObject("FeastLight").AddComponent<Light>();
            l.transform.SetParent(t, false);
            l.transform.position = at + Vector3.up * 2.2f;
            l.type = LightType.Point;
            l.color = new Color(1f, 0.65f, 0.3f);
            l.range = 14f;
            l.intensity = 2f;
            // trestle tables with food and drink either side
            var wood = new Color(0.55f, 0.38f, 0.22f);
            foreach (float side in new[] { -1f, 1f })
            {
                var c = TownLife.Walkable(at + new Vector3(side * 5.5f, 0f, 0f));
                Factory.Prim(PrimitiveType.Cube, t, c + Vector3.up * 0.8f, new Vector3(1f, 0.08f, 3f), wood);
                foreach (float z in new[] { -1.2f, 1.2f }) Factory.Prim(PrimitiveType.Cube, t, c + new Vector3(0f, 0.4f, z), new Vector3(0.9f, 0.8f, 0.1f), wood * 0.8f);
                for (int k = 0; k < 5; k++)
                {
                    var food = Factory.Prim(k % 2 == 0 ? PrimitiveType.Cylinder : PrimitiveType.Sphere, t, c + new Vector3(R(-0.3f, 0.3f), 0.92f, -1.2f + k * 0.6f),
                        k % 2 == 0 ? new Vector3(0.14f, 0.1f, 0.14f) : new Vector3(0.22f, 0.16f, 0.22f), k % 2 == 0 ? new Color(0.7f, 0.6f, 0.4f) : new Color(0.75f, 0.45f, 0.2f));
                    food.name = k % 2 == 0 ? "Tankard" : "Loaf";
                }
            }
            // banners of the town, and the townsfolk round the fire
            var life = SiegeLife.Get();
            string[] models = { "Characters/Keeper", "Characters/RogueHooded", "Characters/Mage", "Characters/Rogue", "Characters/Knight" };
            for (int i = 0; i < 9; i++)
            {
                float ang = i * 40f + R(-8f, 8f);
                var spot = TownLife.Walkable(at + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * R(3f, 4.2f));
                string model = models[i % models.Length];
                var e = life.Person("Villager", model, spot, i == 4 ? 1.2f : R(1.75f, 1.95f));
                e.FaceAt = at;
                e.Party = i % 3 == 0 ? "dance" : i % 3 == 1 ? "cheer" : "clap";
                b.People.Add(e);
            }
            Sfx.Play("bell", at + Vector3.up * 3f, 0.8f, 0.05f, 80f);
            return b;
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
                    Factory.Prim(PrimitiveType.Cube, tr, at + across * s + inward * d + Vector3.up * 2f, new Vector3(0.12f, 4f, 0.12f), wood);
            foreach (float h in new[] { 1.4f, 2.9f })
                Factory.Prim(PrimitiveType.Cube, tr, at + inward * 0.6f + Vector3.up * h, Abs(across * 5f + inward * 1.3f) + Vector3.up * 0.08f, wood * 0.9f);
            // fresh planks stacked by it, a sawhorse
            var pile = at + inward * 3f + across * 3.5f;
            for (int i = 0; i < 6; i++) Factory.Prim(PrimitiveType.Cube, tr, pile + Vector3.up * (0.08f + i * 0.1f), Abs(across * 0.3f + inward * 2.4f) + Vector3.up * 0.08f, new Color(0.78f, 0.62f, 0.4f));
            var life = SiegeLife.Get();
            var c1 = life.Person("Carpenter", "Characters/Barbarian", TownLife.Walkable(at + inward * 1.2f + across * 1.4f), 1.9f);
            var c2 = life.Person("Carpenter", "Characters/Keeper", TownLife.Walkable(at + inward * 1.2f - across * 1.4f), 1.85f);
            c1.FaceAt = c2.FaceAt = gate;
            c1.Tending = c2.Tending = true;
            b.People.Add(c1);
            b.People.Add(c2);
            return b;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        // ------------------------------------------------------------------ graves for the fallen guards

        Built Graves(Vector3 gate, string town, int n, int seed)
        {
            var t = WorldGenerator.TownNamed(town);
            if (t == null || n <= 0) return null;
            var outward = Factory.Flat(gate - t.Center).normalized;
            var along = Vector3.Cross(Vector3.up, outward);
            var r = new System.Random(seed * 7919 + n);
            float Rr(float a, float b2) => a + (float)r.NextDouble() * (b2 - a);
            var b = new Built();
            b.Root = new GameObject("Graves");
            var tr = b.Root.transform;
            var grid = WorldGrid.Instance;
            float side = r.NextDouble() < 0.5 ? -1f : 1f; // to one side of the road out of the gate
            int placed = 0;
            for (int i = 0; placed < n && i < n * 3; i++)
            {
                int row = placed / 4, col = placed % 4;
                var p = gate + outward * (7f + row * 2.4f) + along * side * (6f + col * 1.6f) + new Vector3(Rr(-0.2f, 0.2f), 0f, Rr(-0.2f, 0.2f));
                if (grid != null && !grid.IsWalkable(p)) { placed++; continue; }
                placed++;
                var earth = new Color(0.35f, 0.27f, 0.2f);
                Factory.Prim(PrimitiveType.Cube, tr, p + Vector3.up * 0.12f, Abs(outward * 1.8f + along * 0.8f) + Vector3.up * 0.25f, earth);
                var cross = Factory.Empty("Cross", tr, p - outward * 0.95f);
                cross.rotation = Quaternion.LookRotation(outward) * Quaternion.Euler(Rr(-6f, 6f), 0f, Rr(-6f, 6f));
                var w = new Color(0.5f, 0.38f, 0.25f);
                Factory.Prim(PrimitiveType.Cube, cross, cross.position + Vector3.up * 0.55f, new Vector3(0.09f, 1.1f, 0.09f), w).transform.rotation = cross.rotation;
                Factory.Prim(PrimitiveType.Cube, cross, cross.position + Vector3.up * 0.8f, new Vector3(0.55f, 0.09f, 0.09f), w).transform.rotation = cross.rotation;
                // a helmet hung on some, flowers on others
                if (r.NextDouble() < 0.4) Factory.Prim(PrimitiveType.Sphere, cross, cross.position + Vector3.up * 1.12f, new Vector3(0.24f, 0.18f, 0.24f), new Color(0.55f, 0.56f, 0.6f));
                else Factory.Prim(PrimitiveType.Sphere, tr, p + Vector3.up * 0.28f + outward * 0.3f, new Vector3(0.18f, 0.08f, 0.18f), new Color(0.9f, 0.85f, 0.4f));
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
            Factory.Prim(PrimitiveType.Cylinder, tr, camp + Vector3.up * 1f, new Vector3(0.18f, 1f, 0.18f), new Color(0.35f, 0.25f, 0.15f));
            var life = SiegeLife.Get();
            string[] models = { "Characters/Keeper", "Characters/RogueHooded", "Characters/Rogue", "Characters/Mage" };
            for (int i = 0; i < n; i++)
            {
                var at = camp + Quaternion.Euler(0f, i * (360f / n), 0f) * Vector3.forward * 1.1f;
                var e = life.Person("Captive", models[i % models.Length], at, i == n - 1 ? 1.2f : R(1.7f, 1.9f));
                e.FaceAt = camp + (at - camp) * 3f;
                e.Party = "sit";
                b.People.Add(e);
                // the rope from them to the stake
                var rope = Factory.Prim(PrimitiveType.Cube, tr, Vector3.Lerp(at, camp, 0.5f) + Vector3.up * 0.6f, new Vector3(0.03f, 0.03f, 1.1f), new Color(0.75f, 0.65f, 0.45f));
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
                foreach (float x in new[] { -0.7f, 0.7f }) Factory.Prim(PrimitiveType.Cube, go.transform, at + go.transform.right * x + Vector3.up * 0.95f, new Vector3(0.12f, 1.9f, 0.12f), wood);
                var board = Factory.Prim(PrimitiveType.Cube, go.transform, at + Vector3.up * 1.4f, new Vector3(1.5f, 0.95f, 0.08f), new Color(0.3f, 0.22f, 0.14f));
                board.transform.rotation = go.transform.rotation;
                // pinned notices, a carved shield on top
                for (int i = 0; i < 4; i++)
                {
                    var n = Factory.Prim(PrimitiveType.Cube, go.transform, at + go.transform.right * (-0.45f + i * 0.3f) + Vector3.up * (1.3f + (i % 2) * 0.2f) + go.transform.forward * 0.05f, new Vector3(0.24f, 0.3f, 0.01f), new Color(0.9f, 0.86f, 0.72f));
                    n.transform.rotation = go.transform.rotation * Quaternion.Euler(0f, 0f, (i - 1.5f) * 4f);
                }
                Factory.Prim(PrimitiveType.Cube, go.transform, at + Vector3.up * 2.05f, new Vector3(0.45f, 0.5f, 0.1f), new Color(0.6f, 0.15f, 0.12f)).transform.rotation = go.transform.rotation;
                b.AddClickCollider(0.8f, 2.2f);
            }
        }

        public override void Interact(Player p) => GameUI.I?.OpenChronicle(Town);
    }
}
