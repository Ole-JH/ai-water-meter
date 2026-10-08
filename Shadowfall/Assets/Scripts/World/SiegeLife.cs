using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The people of a town invasion, around what the server runs (Invasion, TownGuards, WarCamp): purely visual and
    /// local to each player.
    /// <list type="bullet">
    /// <item>The scouts' warning: farmers and a cart flee from the raiders' side into town, and a wounded scout gallops in
    /// to the town crier with the news.</item>
    /// <item>The war camp's raiders jeer, dance and sharpen their blades while they mass; their chief shouts taunts.</item>
    /// <item>A guard who falls is carried back into town by two militiamen and laid down by the healer in the square.</item>
    /// </list>
    /// Only built near the hero (the town under attack within ~200 m).
    /// </summary>
    public class SiegeLife : MonoBehaviour
    {
        static SiegeLife I;
        const float NearTown = 200f;

        static readonly string[] fleeing = { "They burned our farm!", "Open the gate! Let us in!", "Run! They're right behind us!", "The raiders! Hundreds of them!", "My cows... leave them, run!" };
        static readonly string[] taunts = { "Your walls won't save you!", "We'll burn it all!", "Hide, little townsfolk!", "Sharpen your blades, lads!", "Tonight we feast in their halls!", "Bring out your gold!" };
        static readonly string[] chiefTaunts = { "When the horns sound, leave nothing standing!", "Their gate is rotten wood. Break it!", "Whoever brings me the bell gets double shares!" };
        static readonly string[] carriers = { "Easy with him!", "Make way! Wounded!", "Hold on, friend, nearly there.", "Get him to the healer!" };

        readonly System.Random rng = new System.Random();
        readonly List<Extra> extras = new List<Extra>();
        string warnedFor;  // the siege we ran the warning's scenes for
        float nextTaunt, nextChief;
        Extra healer;
        int wounded;

        public static SiegeLife Get()
        {
            if (I == null) I = new GameObject("SiegeLife").AddComponent<SiegeLife>();
            return I;
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        T Pick<T>(T[] a) => a[rng.Next(a.Length)];

        static Settlement Town(NetInvasion iv) => iv != null ? WorldGenerator.TownNamed(iv.town) : null;

        static bool HeroNear(Settlement t, float r = NearTown)
        {
            var p = Player.I;
            if (p == null || t == null || Dungeon.Active) return false;
            return Factory.FlatDistance(p.transform.position, t.Center) < r;
        }

        /// <summary>Called whenever the invasion's state arrives.</summary>
        public static void Sync()
        {
            var me = Get();
            var iv = Invasion.Current;
            if (iv == null) { me.Clear(); return; }
            string key = iv.town + "|" + iv.gate;
            if (iv.phase == "warn" && me.warnedFor != key)
            {
                me.warnedFor = key;
                if (HeroNear(Town(iv)) && iv.left > 20) me.Warning(iv);
            }
        }

        void Clear()
        {
            foreach (var e in extras) if (e != null) Destroy(e.gameObject);
            extras.Clear();
            healer = null;
            wounded = 0;
            warnedFor = null;
        }

        // ------------------------------------------------------------------ the warning: refugees and the scout

        void Warning(NetInvasion iv)
        {
            var town = Town(iv);
            var gate = new Vector3(iv.gx, 0f, iv.gz);
            var camp = new Vector3(iv.sx, 0f, iv.sz);
            var outward = Factory.Flat(camp - gate).normalized;
            if (outward == Vector3.zero) outward = Vector3.back;
            var across = Vector3.Cross(Vector3.up, outward);
            var inside = TownLife.Walkable(gate - outward * 14f);
            var grid = WorldGrid.Instance;

            // Farmers running in from the raiders' side, one or two at a time, and a cart
            string[] models = { "Characters/Keeper", "Characters/RogueHooded", "Characters/Rogue", "Characters/Mage" };
            for (int i = 0; i < 5; i++)
            {
                var from = gate + outward * R(16f, 22f) + across * R(-12f, 12f);
                if (grid != null && !grid.IsWalkable(from)) continue;
                string model = models[i % models.Length];
                var look = new CharacterLook { Model = model, Height = i == 3 ? 1.2f : R(1.75f, 1.9f), Anims = model.EndsWith("Keeper") ? AnimSet.Kenney : AnimSet.KayKit, Tint = new Color(R(0.8f, 1f), R(0.75f, 0.9f), R(0.6f, 0.8f)) };
                var e = Extra.Make(this, "Refugee", look, from, R(4.2f, 5.4f));
                e.Delay = 2f + i * R(1.5f, 3f);
                e.Walk(inside + across * R(-3f, 3f));
                e.Line = Pick(fleeing);
                e.OnArrive = x => x.FadeOut(2.5f);
            }
            var cartFrom = gate + outward * 20f;
            if (grid == null || grid.IsWalkable(cartFrom))
            {
                var cart = Extra.Make(this, "Cart", null, cartFrom, 3.2f);
                BuildCart(cart.transform);
                cart.Delay = 6f;
                cart.Walk(inside);
                cart.OnArrive = x => x.FadeOut(3f);
                var driver = Extra.Make(this, "Carter", new CharacterLook { Model = "Characters/Keeper", Height = 1.8f, Anims = AnimSet.Kenney, Tint = new Color(0.9f, 0.8f, 0.65f) }, cartFrom - outward * 1.6f, 3.2f);
                driver.Delay = 6f;
                driver.Walk(inside - outward * 1.6f);
                driver.Line = "Whoa! Make room, make room!";
                driver.OnArrive = x => x.FadeOut(3f);
            }

            // The scout: wounded, riding hard for the crier
            TownCrier crier = null;
            foreach (var c in TownCrier.Criers) if (c != null && town != null && town.Contains(c.transform.position.x, c.transform.position.z)) crier = c;
            var scoutFrom = gate + outward * 28f + across * R(-4f, 4f);
            if (crier != null && (grid == null || grid.IsWalkable(scoutFrom)))
            {
                var scout = Extra.Make(this, "Scout", new CharacterLook { Model = "Characters/Rogue", Height = 1.85f, Tint = new Color(1f, 0.72f, 0.68f) }, scoutFrom, 9f, MountDef.Get("horse"));
                scout.Delay = 1f;
                scout.Walk(TownLife.Walkable(crier.transform.position + crier.transform.forward * 2.5f));
                scout.Line = "Raiders! Raiders at the " + iv.gate + " gate!";
                scout.OnArrive = x =>
                {
                    Speech.Say(x.transform, 3.2f, "They're making camp outside the " + iv.gate + " gate... dozens of them... the drums...");
                    x.FadeOut(12f);
                };
            }
        }

        static void BuildCart(Transform t)
        {
            var wood = new Color(0.5f, 0.34f, 0.2f);
            Factory.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.75f, 0f), new Vector3(1.3f, 0.12f, 2.2f), wood);
            Factory.Prim(PrimitiveType.Cube, t, new Vector3(-0.62f, 1f, 0f), new Vector3(0.06f, 0.45f, 2.2f), wood * 0.9f);
            Factory.Prim(PrimitiveType.Cube, t, new Vector3(0.62f, 1f, 0f), new Vector3(0.06f, 0.45f, 2.2f), wood * 0.9f);
            foreach (float x in new[] { -0.72f, 0.72f })
            {
                var w = Factory.Prim(PrimitiveType.Cylinder, t, new Vector3(x, 0.45f, 0.2f), new Vector3(0.9f, 0.04f, 0.9f), wood * 0.7f);
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            // household goods piled on it
            Factory.Prim(PrimitiveType.Cube, t, new Vector3(0.2f, 1.05f, 0.4f), new Vector3(0.5f, 0.45f, 0.5f), new Color(0.55f, 0.42f, 0.28f));
            Factory.Prim(PrimitiveType.Cylinder, t, new Vector3(-0.25f, 1.1f, -0.4f), new Vector3(0.45f, 0.3f, 0.45f), new Color(0.45f, 0.3f, 0.2f));
            Factory.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.0f, -0.1f), new Vector3(0.9f, 0.2f, 0.6f), new Color(0.75f, 0.7f, 0.55f)); // a bundle of linen
            Factory.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.8f, 1.5f), new Vector3(0.08f, 0.08f, 1.2f), wood); // shafts
        }

        // ------------------------------------------------------------------ the camp: jeers, dances, the chief's taunts

        void Update()
        {
            var iv = Invasion.Current;
            if (iv == null || iv.phase != "warn" || iv.paused) return;
            var hero = Player.I;
            var camp = new Vector3(iv.sx, 0f, iv.sz);
            if (hero == null || Factory.FlatDistance(hero.transform.position, camp) > 60f) return;
            if (Time.time >= nextTaunt)
            {
                nextTaunt = Time.time + R(2.5f, 5f);
                var e = CampRaider(camp, false);
                if (e != null)
                {
                    float roll = (float)rng.NextDouble();
                    if (roll < 0.4f) Speech.Say(e.transform, e.Height + 0.4f, Pick(taunts));
                    if (roll < 0.7f) e.Cheer(); else e.Busy();
                }
            }
            if (Time.time >= nextChief)
            {
                nextChief = Time.time + R(12f, 18f);
                var chief = CampRaider(camp, true);
                if (chief != null)
                {
                    Speech.Say(chief.transform, chief.Height + 0.5f, Pick(chiefTaunts));
                    chief.Cheer();
                    foreach (var e in Enemy.ById.Values)
                        if (e != null && e != chief && !e.IsDead && Factory.FlatDistance(e.transform.position, camp) < 9f && rng.NextDouble() < 0.6) e.Cheer();
                }
            }
        }

        /// <summary>A raider in the camp (the chief: the biggest of them, an elite if there is one).</summary>
        Enemy CampRaider(Vector3 camp, bool chief)
        {
            Enemy pick = null;
            int n = 0;
            float best = -1f;
            foreach (var e in Enemy.ById.Values)
            {
                if (e == null || e.IsDead || Factory.FlatDistance(e.transform.position, camp) > 9f) continue;
                if (chief)
                {
                    float score = e.MaxHealth * (e.Elite ? 3f : 1f);
                    if (score > best) { best = score; pick = e; }
                }
                else if (rng.Next(++n) == 0) pick = e;
            }
            return pick;
        }

        // ------------------------------------------------------------------ a fallen guard carried to the healer

        /// <summary>A guard fell at <paramref name="at"/> (TownGuards): two militiamen carry him to the healer.</summary>
        public static void GuardDown(Vector3 at, CharacterLook look)
        {
            var iv = Invasion.Current;
            var town = Town(iv);
            if (town == null || !HeroNear(town, 120f)) return;
            Get().Carry(at, look, town);
        }

        void Carry(Vector3 at, CharacterLook look, Settlement town)
        {
            if (wounded >= 6) return; // the infirmary is full; the rest are seen to off-screen
            var square = TownLife.Walkable(town.Center + new Vector3(3f + (wounded % 3) * 1.6f, 0f, -3f - (wounded / 3) * 2f));
            if (healer == null)
            {
                healer = Person("Healer", "Characters/Mage", TownLife.Walkable(town.Center + new Vector3(5f, 0f, -1.5f)), 1.8f);
                healer.Line = "Bring them here! Gently!";
                healer.Tending = true;
                healer.FaceAt = square;
            }
            var bed = square;
            wounded++;
            var militiaLook = new CharacterLook { Model = "Characters/RogueHooded", Height = 1.8f, Tint = new Color(0.75f, 0.85f, 1f) };
            // two men run out of a house near the gate for him
            var near = Vector3.MoveTowards(at, town.Center, 12f);
            var door = Rampart.DoorNear(near, 25f, rng);
            var start = TownLife.Walkable(door != null ? door.Step : near);
            var a = Extra.Make(this, "Militia", militiaLook, start, R(3.6f, 4.6f));
            var b = Extra.Make(this, "Militia", militiaLook, TownLife.Walkable(start + Vector3.right), R(3.6f, 4.6f));
            var body = Extra.Make(this, "Wounded guard", look, at, 0f);
            body.Lying = true;
            a.Delay = R(0.8f, 2.5f);
            b.Delay = a.Delay + R(0.2f, 1.2f);
            a.Walk(at + Vector3.left * 0.7f);
            b.Walk(at + Vector3.right * 0.7f);
            a.Line = Pick(carriers);
            a.OnArrive = x =>
            {
                // pick him up between them and go
                body.CarriedBy = x;
                x.Walk(bed + Vector3.left * 0.7f);
                b.Walk(bed + Vector3.right * 0.7f);
                x.OnArrive = y =>
                {
                    body.CarriedBy = null;
                    body.transform.position = bed;
                    y.Delay = 3f;
                    y.FadeOut(4f);
                    b.FadeOut(4f);
                    body.FadeOut(120f); // he lies there being tended a while, then he's on his feet (out of sight)
                };
            };
            b.OnArrive = null;
        }

        // ------------------------------------------------------------------ the bucket line at a burning roof

        /// <summary>One of the townsfolk running with a bucket from <paramref name="from"/> to <paramref name="stand"/>, throwing at <paramref name="flame"/>.</summary>
        public Extra Bucket(Vector3 from, Vector3 stand, Vector3 flame)
        {
            string[] models = { "Characters/Keeper", "Characters/RogueHooded", "Characters/Rogue" };
            string model = Pick(models);
            var door = Rampart.DoorNear(stand, 30f, rng); // out of a neighbour's house with a bucket
            if (door != null) from = TownLife.Walkable(door.Step);
            var e = Extra.Make(this, "Bucket carrier", new CharacterLook { Model = model, Height = R(1.75f, 1.9f), Anims = model.EndsWith("Keeper") ? AnimSet.Kenney : AnimSet.KayKit, Tint = new Color(R(0.85f, 1f), R(0.8f, 0.95f), R(0.7f, 0.9f)) }, from, R(3.8f, 4.6f));
            e.Delay = R(0.5f, 3f);
            e.Walk(stand);
            if (rng.NextDouble() < 0.5) e.Line = Pick(new[] { "Water! More water!", "Keep the buckets coming!", "Fire! Fire on the roof!", "Form a line!" });
            e.OnArrive = x => { x.Tending = true; x.FaceAt = flame; };
            return e;
        }

        /// <summary>One of the townsfolk standing at <paramref name="at"/> (a feast, a carpenter, a captive).</summary>
        /// <summary>
        /// One of the townsfolk who comes to stand at <paramref name="at"/> (a feast, a carpenter, the healer): out of a
        /// house's front door nearby, in their own time and at their own pace. <paramref name="fromDoor"/> false: already
        /// there (a captive at the raiders' camp).
        /// </summary>
        public Extra Person(string name, string model, Vector3 at, float height, bool fromDoor = true)
        {
            var look = new CharacterLook { Model = model, Height = height, Anims = model.EndsWith("Keeper") ? AnimSet.Kenney : AnimSet.KayKit, Tint = new Color(R(0.85f, 1f), R(0.8f, 0.95f), R(0.7f, 0.9f)) };
            var door = fromDoor ? Rampart.DoorNear(at, 40f, rng) : null;
            if (door == null) return Extra.Make(this, name, look, at, R(1.4f, 2f));
            var e = Extra.Make(this, name, look, TownLife.Walkable(door.Step), R(1.5f, 2.6f));
            e.Delay = R(0.3f, 6f);
            e.Hidden = true; // indoors until they step out
            e.Door = door;
            e.Walk(at);
            return e;
        }

        // ------------------------------------------------------------------ the extras

        /// <summary>One of the townsfolk (or a cart) walking a path, saying a line, maybe riding; fades away when told.</summary>
        public class Extra : MonoBehaviour
        {
            public float Speed, Delay;
            public string Line;
            public System.Action<Extra> OnArrive;
            public bool Lying, Tending;
            public bool Hidden;            // still indoors: shown (and the door swung) when Delay runs out
            public HouseDoors.Door Door;
            float nextFidget;
            public Vector3? FaceAt;
            public string Party;   // an emote kept up while standing about: dance, cheer, clap, sit
            float nextParty;

            public void Cheer() => view?.Cheer();
            /// <summary>Up from sitting or dancing (a captive set free).</summary>
            public void StandUp() { Party = null; view?.StopEmote(); }
            public Extra CarriedBy;
            CharacterView view;
            MountRig mount;
            readonly List<Vector3> path = new List<Vector3>();
            int at;
            float fadeAt = -1f, fadeLen, lastMove, nextTend;
            bool said, lay;

            public static Extra Make(SiegeLife owner, string name, CharacterLook look, Vector3 pos, float speed, MountDef ride = null)
            {
                var go = new GameObject(name);
                go.transform.position = pos;
                var e = go.AddComponent<Extra>();
                e.Speed = speed;
                if (look != null) e.view = CharacterView.Create(go.transform, look);
                if (ride != null) e.mount = new MountRig(go.transform, e.view, ride);
                owner.extras.Add(e);
                return e;
            }

            public void Walk(Vector3 to)
            {
                path.Clear();
                at = 0;
                var grid = WorldGrid.Instance;
                if (grid == null || !grid.FindPath(transform.position, to, path, 6000)) { path.Clear(); path.Add(to); }
            }

            public void FadeOut(float after) { fadeAt = Time.time + after; fadeLen = 1.5f; }

            /// <summary>Standing about: now and then a glance aside, a shuffle of the feet, so nobody stands like a post.</summary>
            void Fidget()
            {
                if (Time.time < nextFidget) return;
                nextFidget = Time.time + Random.Range(4f, 11f);
                if (FaceAt.HasValue && Random.value < 0.6f) return;
                transform.rotation *= Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f);
                if (Random.value < 0.3f)
                {
                    var p = transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));
                    if (WorldGrid.Instance == null || WorldGrid.Instance.IsWalkable(p)) { path.Clear(); at = 0; path.Add(p); }
                }
            }

            void Update()
            {
                float dt = Time.deltaTime;
                if (fadeAt >= 0f && Time.time >= fadeAt)
                {
                    float k = (Time.time - fadeAt) / fadeLen;
                    transform.position += Vector3.down * dt * 0.8f;
                    if (k >= 1f) { Destroy(gameObject); return; }
                }
                if (Lying)
                {
                    if (!lay && view != null) { lay = true; view.Emote(EmoteDef.Get("sleep")); }
                    if (CarriedBy != null)
                    {
                        transform.position = CarriedBy.transform.position + CarriedBy.transform.right * 0.7f + Vector3.up * 0.7f;
                        transform.rotation = CarriedBy.transform.rotation;
                    }
                    return;
                }
                if (Tending && at >= path.Count && Delay <= 0f)
                {
                    if (FaceAt.HasValue) Factory.Face(transform, FaceAt.Value, dt * 6f);
                    if (Time.time >= nextTend) { nextTend = Time.time + Random.Range(FaceAt.HasValue ? 1.6f : 3f, FaceAt.HasValue ? 2.8f : 6f); view?.Interact(); }
                    view?.UpdateLocomotion(0f);
                    return;
                }
                if (Hidden && view != null && view.Root.activeSelf) view.Root.SetActive(false);
                if (Delay > 0f) { Delay -= dt; view?.UpdateLocomotion(0f); mount?.Tick(0f); return; }
                if (Hidden)
                {
                    Hidden = false;
                    if (view != null) view.Root.SetActive(true);
                    if (Door != null) HouseDoors.Swing(Door, 1.3f);
                }
                if (!string.IsNullOrEmpty(Party) && at >= path.Count)
                {
                    if (FaceAt.HasValue) Factory.Face(transform, FaceAt.Value, dt * 4f);
                    if (view != null && !view.Emoting && Time.time >= nextParty)
                    {
                        nextParty = Time.time + Random.Range(0.5f, 3f);
                        view.Emote(EmoteDef.Get(Party));
                    }
                    if (view != null && !view.Emoting) view.UpdateLocomotion(0f);
                    Fidget();
                    return;
                }
                float moved = 0f;
                if (at < path.Count)
                {
                    if (!said && !string.IsNullOrEmpty(Line)) { said = true; Speech.Say(transform, mount != null ? 3.2f : 2.4f, Line); }
                    var to = path[at];
                    var d = Factory.Flat(to - transform.position);
                    float step = Speed * dt;
                    if (d.magnitude <= step) { transform.position = new Vector3(to.x, 0f, to.z); at++; moved = step; }
                    else { transform.position += d.normalized * step; moved = step; }
                    if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), dt * 8f);
                    if (at >= path.Count)
                    {
                        var cb = OnArrive;
                        OnArrive = null;
                        cb?.Invoke(this);
                    }
                }
                float speed = dt > 0f ? moved / dt : 0f;
                lastMove = Mathf.Lerp(lastMove, speed, dt * 10f);
                if (mount != null) mount.Tick(lastMove); else view?.UpdateLocomotion(lastMove);
            }
        }
    }
}
