using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Hollowmere's daily life, by the (server-synced) clock:
    /// <list type="bullet">
    /// <item>Villagers leave home at dawn and go to work (the farm plot, the market stalls, the well, the windmill, the
    /// church), have lunch sitting outside the tavern at noon, meet friends on the square in the evening and chat, spend an
    /// hour at the tavern, and go home for the night. A couple of night owls stay at the tavern late.</item>
    /// <item>Children play tag on the square by day and go home at seven.</item>
    /// <item>Two shifts of guards: the day watch patrols the gates from six to eight, then walks back to the barracks
    /// and hands over to the night watch, who carry torches.</item>
    /// <item>The village dog tags along with whoever is about, and sleeps by the tavern at night.</item>
    /// </list>
    /// Purely visual and local to each player; nobody here blocks anything.
    /// </summary>
    public class TownLife : MonoBehaviour
    {
        // Places (walkable spots near them are picked at runtime).
        public static readonly Vector3 Barracks = new Vector3(147.5f, 0, 128.5f);
        public static readonly Vector3 TavernDoor = new Vector3(131.5f, 0, 156.5f);
        static readonly Vector3[] TavernSeats =
        {
            new Vector3(131.2f, 0, 154.2f), new Vector3(132.6f, 0, 155.0f), new Vector3(133.2f, 0, 156.6f), new Vector3(132.6f, 0, 158.2f),
            new Vector3(131.2f, 0, 159.0f), new Vector3(133.9f, 0, 153.6f), new Vector3(134.4f, 0, 158.9f), new Vector3(134.8f, 0, 156.2f),
        };
        static readonly Vector3[] Homes =
        {
            new Vector3(134.5f, 0, 161.6f), new Vector3(128.6f, 0, 149.5f), new Vector3(153.5f, 0, 161.6f), new Vector3(161.4f, 0, 151.5f),
            new Vector3(128.8f, 0, 165.5f), new Vector3(128.6f, 0, 134.5f),
        };
        static readonly Vector3[] Meeting = { new Vector3(140.5f, 0, 140.6f), new Vector3(148.4f, 0, 140.6f), new Vector3(147.2f, 0, 149.8f), new Vector3(144.5f, 0, 147.4f) }; // (not on the Rift Stone at 149.5, 149.5)

        public struct Job
        {
            public string Name;
            public Vector3 Spot, FaceTo;
            public string Clip;      // the work motion (null = the model's interact)
            public string Sound;
        }

        static readonly Job[] Jobs =
        {
            new Job { Name = "farm", Spot = new Vector3(124.5f, 0, 122.5f), FaceTo = new Vector3(124.5f, 0, 121f), Sound = "chop" },
            new Job { Name = "farm", Spot = new Vector3(129.5f, 0, 123.5f), FaceTo = new Vector3(129.5f, 0, 122f), Sound = "chop" },
            new Job { Name = "market", Spot = new Vector3(131.5f, 0, 137.4f), FaceTo = new Vector3(131.5f, 0, 138.5f) },
            new Job { Name = "market", Spot = new Vector3(133.5f, 0, 137.4f), FaceTo = new Vector3(133.5f, 0, 138.5f) },
            new Job { Name = "well", Spot = new Vector3(144.5f, 0, 142.4f), FaceTo = new Vector3(144.5f, 0, 144.5f), Sound = "splash" },
            new Job { Name = "windmill", Spot = new Vector3(129.2f, 0, 166.5f), FaceTo = new Vector3(126f, 0, 166.5f) },
            new Job { Name = "church", Spot = new Vector3(161.5f, 0, 157.6f), FaceTo = new Vector3(161.5f, 0, 162f), Clip = "Spellcast_Raise" },
            new Job { Name = "woodpile", Spot = new Vector3(138.5f, 0, 166.5f), FaceTo = new Vector3(138.5f, 0, 168f), Sound = "chop", Clip = "1H_Melee_Attack_Chop" },
        };

        public static void Spawn(Transform parent)
        {
            var root = new GameObject("TownLife").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(77);
            string[] villagerModels = { "Characters/Keeper", "Characters/RogueHooded", "Characters/Mage", "Characters/Rogue" };
            Color[] tints = { new Color(1f, 0.9f, 0.8f), new Color(0.85f, 0.95f, 0.85f), new Color(0.9f, 0.85f, 1f), new Color(1f, 1f, 0.9f) };
            int adult = 0;
            for (int i = 0; i < 10; i++)
            {
                string model = villagerModels[i % villagerModels.Length];
                bool child = i == 5 || i == 9;
                var look = new CharacterLook
                {
                    Model = model, Height = child ? 1.15f : 1.8f + (float)rng.NextDouble() * 0.15f,
                    Anims = model.EndsWith("Keeper") ? AnimSet.Kenney : AnimSet.KayKit, Tint = tints[rng.Next(tints.Length)],
                };
                var home = Homes[i % Homes.Length];
                var w = Walker.Create(root, child ? "Child" : "Villager", look, home, child ? 2.6f : 1.5f, child ? Walker.Kind.Child : Walker.Kind.Villager);
                w.Home = home;
                if (!child)
                {
                    w.Work = Jobs[adult % Jobs.Length];
                    w.Seat = TavernSeats[adult % TavernSeats.Length];
                    w.MeetAt = Meeting[adult % Meeting.Length];
                    w.NightOwl = adult == 1 || adult == 6;
                    adult++;
                }
            }

            var t = WorldGenerator.Town;
            Vector3[] gates = { new Vector3(t.center.x + 0.5f, 0, t.yMin + 2.5f), new Vector3(t.xMax - 2.5f, 0, t.center.y + 0.5f), new Vector3(t.center.x + 0.5f, 0, t.yMax - 2.5f), new Vector3(t.xMin + 2.5f, 0, t.center.y + 0.5f) };
            for (int i = 0; i < 6; i++)
            {
                bool night = i >= 3;
                var guard = Walker.Create(root, "Guard", new CharacterLook { Model = "Characters/Knight", Height = 2f, Tint = night ? new Color(0.75f, 0.78f, 0.9f) : new Color(0.85f, 0.85f, 0.9f), Weapon = "sword" },
                    Barracks, 1.6f, Walker.Kind.Guard);
                guard.Route = gates;
                guard.RouteIndex = i % 3;
                guard.NightWatch = night;
            }
            Walker.Create(root, "Hound", new CharacterLook { Model = "Monsters/Wolf", Height = 0.85f, Anims = AnimSet.Wolf, RunSpeed = 5f, Tint = new Color(0.55f, 0.45f, 0.38f) },
                new Vector3(143.5f, 0, 142.5f), 2.5f, Walker.Kind.Dog);
        }

        static readonly List<Vector3> townPoints = new List<Vector3>(), squarePoints = new List<Vector3>();

        public static Vector3 RandomTownPoint(System.Random rng)
        {
            if (townPoints.Count == 0)
            {
                var grid = WorldGrid.Instance;
                var town = WorldGenerator.Town;
                for (int y = town.yMin + 2; y < town.yMax - 2; y++)
                    for (int x = town.xMin + 2; x < town.xMax - 2; x++)
                        if (!grid.IsBlocked(x, y) && !grid.IsBlocked(x + 1, y) && !grid.IsBlocked(x, y + 1) && !grid.IsBlocked(x - 1, y) && !grid.IsBlocked(x, y - 1))
                        {
                            var p = new Vector3(x + 0.5f, 0, y + 0.5f);
                            townPoints.Add(p);
                            if (x >= 137 && x <= 151 && y >= 137 && y <= 151) squarePoints.Add(p);
                        }
            }
            return townPoints[rng.Next(townPoints.Count)];
        }

        public static Vector3 RandomSquarePoint(System.Random rng)
        {
            RandomTownPoint(rng);
            return squarePoints.Count > 0 ? squarePoints[rng.Next(squarePoints.Count)] : RandomTownPoint(rng);
        }

        /// <summary>The nearest walkable spot.</summary>
        public static Vector3 Walkable(Vector3 p)
        {
            var grid = WorldGrid.Instance;
            for (int r = 0; r < 5; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var q = p + new Vector3(dx, 0, dy);
                        if (grid.IsWalkable(q)) return q;
                    }
            return p;
        }
    }

    /// <summary>A villager, child, guard or the dog, living out the day in Hollowmere.</summary>
    public class Walker : MonoBehaviour
    {
        public enum Kind { Villager, Child, Guard, Dog }
        enum Doing { Nothing, Walking, Working, Sitting, Chatting, Playing, Patrolling, Inside }

        public Vector3[] Route;
        public int RouteIndex;
        public Vector3 Home, Seat, MeetAt;
        public TownLife.Job Work;
        public bool NightOwl, NightWatch;

        static readonly List<Walker> all = new List<Walker>();

        Kind kind;
        string speaker;
        float speed;
        CharacterView view;
        readonly List<Vector3> path = new List<Vector3>();
        int pathIndex;
        float waitUntil, chatterAt, nextTrample, nextAction;
        Light torch;
        Renderer[] renderers;
        bool hidden;
        System.Random rng;
        Doing doing = Doing.Nothing;
        string plan;          // which part of the day we're in (a change means a new destination)
        Vector3 faceTo;
        Walker partner;       // who we're chatting with
        int line;

        public static Walker Create(Transform parent, string speaker, CharacterLook look, Vector3 pos, float speed, Kind kind)
        {
            var go = new GameObject(speaker);
            go.transform.SetParent(parent, false);
            go.transform.position = TownLife.Walkable(pos);
            var w = go.AddComponent<Walker>();
            w.kind = kind;
            w.speaker = kind == Kind.Child ? "Villager" : speaker;
            w.speed = speed;
            w.rng = new System.Random(pos.GetHashCode() ^ all.Count * 7919);
            w.view = CharacterView.Create(go.transform, look);
            if (w.view == null)
                HumanoidModel.Build(go.transform, look.Height / 1.9f, new Color(0.9f, 0.75f, 0.6f), look.Tint ?? Color.gray, Color.gray, Color.gray, false, true);
            w.renderers = go.GetComponentsInChildren<Renderer>();
            w.waitUntil = Time.time + (float)w.rng.NextDouble() * 4f;
            w.chatterAt = Time.time + 10f + (float)w.rng.NextDouble() * 30f;
            if (kind == Kind.Guard)
            {
                var l = new GameObject("GuardTorch").AddComponent<Light>();
                l.transform.SetParent(go.transform, false);
                l.transform.localPosition = new Vector3(0.4f, 2.2f, 0.3f);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.65f, 0.3f);
                l.range = 6f;
                l.intensity = 1.3f;
                w.torch = l;
            }
            all.Add(w);
            return w;
        }

        void OnDestroy() => all.Remove(this);

        static float Hour => DayNight.Hour;

        // ------------------------------------------------------------------ the schedule

        /// <summary>Where this one should be now: a plan name (changes trigger a new destination), the place, what to do there.</summary>
        string Plan(out Vector3 place, out Doing there)
        {
            float h = Hour;
            place = Home;
            there = Doing.Inside;
            switch (kind)
            {
                case Kind.Child:
                    if (h >= 7f && h < 19f) { there = Doing.Playing; place = TownLife.RandomSquarePoint(rng); return "play"; }
                    return "home";

                case Kind.Guard:
                    bool onDuty = NightWatch ? h >= 20f || h < 6f : h >= 6f && h < 20f;
                    if (onDuty) { there = Doing.Patrolling; place = Route[RouteIndex]; return "watch"; }
                    place = TownLife.Barracks;
                    return "barracks";

                case Kind.Dog:
                    if (DayNight.Night > 0.8f) { place = TownLife.Walkable(TownLife.TavernDoor + new Vector3(1.5f, 0, -1.5f)); there = Doing.Sitting; return "sleep"; }
                    there = Doing.Nothing;
                    return "roam";

                default:
                    if (h < 6f || h >= (NightOwl ? 23.2f : 21.5f)) return "home";
                    if (h >= 12f && h < 13f) { place = Seat; there = Doing.Sitting; return "lunch"; }
                    if (h >= 20.5f) { place = Seat; there = Doing.Sitting; return "tavern"; }
                    if (h >= 17f) { place = MeetAt + new Vector3((float)rng.NextDouble() * 1.4f - 0.7f, 0, (float)rng.NextDouble() * 1.4f - 0.7f); there = Doing.Chatting; return "evening"; }
                    if (h >= 7f) { place = Work.Spot; there = Doing.Working; return "work"; }
                    place = Work.Spot;
                    there = Doing.Working;
                    return "work";
            }
        }

        // Something worth watching nearby (a duel): villagers stop, look and cheer (see Watch).
        static Vector3 spectacle;
        static float spectacleUntil;
        static readonly string[] cheers = { "Go on!", "Ooh!", "Get 'em!", "Ha! Did you see that?", "Again! Again!", "Mind the stall!" };
        float nextCheer;

        /// <summary>Villagers within 22 m of <paramref name="at"/> stop to watch for <paramref name="seconds"/> (0: stop watching).</summary>
        public static void Watch(Vector3 at, float seconds)
        {
            spectacle = at;
            spectacleUntil = seconds > 0f ? Time.time + seconds : 0f;
        }

        /// <summary>All the watchers cheer at once (the end of a duel).</summary>
        public static void Applaud()
        {
            foreach (var w in all)
                if (!w.hidden && w.kind != Kind.Dog && Factory.FlatDistance(w.transform.position, spectacle) < 22f) w.view?.Cheer();
        }

        bool Watching()
        {
            if (Time.time >= spectacleUntil || kind == Kind.Dog || Factory.FlatDistance(transform.position, spectacle) > 22f) return false;
            Factory.Face(transform, spectacle, Time.deltaTime * 4f);
            view?.UpdateLocomotion(0f);
            if (Time.time >= nextCheer)
            {
                nextCheer = Time.time + 3f + (float)rng.NextDouble() * 6f;
                view?.Cheer();
                if (rng.NextDouble() < 0.35) Speech.Say(transform, 2.2f, cheers[rng.Next(cheers.Length)]);
            }
            return true;
        }

        void Update()
        {
            if (torch != null) torch.enabled = !hidden && DayNight.Night > 0.35f;
            string now = Plan(out var place, out var there);
            if (now != plan) Begin(now, place, there);
            if (hidden) return;
            if (Watching()) return;

            float moved = Move();
            if (moved > 0f) { view?.UpdateLocomotion(moved); Chatter(); return; }

            // Arrived (or standing about): do the thing.
            switch (doing)
            {
                case Doing.Walking: Arrive(); break;
                case Doing.Working: DoWork(); break;
                case Doing.Chatting: DoChat(); break;
                case Doing.Playing: if (Time.time >= waitUntil) Go(TownLife.RandomSquarePoint(rng), Doing.Playing); break;
                case Doing.Patrolling:
                    // At a gate: wait a little, then on to the next one.
                    if (waitUntil == float.MaxValue) waitUntil = Time.time + 3f + (float)rng.NextDouble() * 4f;
                    else if (Time.time >= waitUntil) { RouteIndex = (RouteIndex + 1) % Route.Length; Go(Route[RouteIndex], Doing.Patrolling); waitUntil = float.MaxValue; }
                    break;
                case Doing.Nothing:
                    if (kind == Kind.Dog && Time.time >= waitUntil) Roam();
                    break;
            }
            if (faceTo != Vector3.zero && doing != Doing.Walking) Factory.Face(transform, faceTo, Time.deltaTime * 4f);
            view?.UpdateLocomotion(0f);
            PassingWave();
            Chatter();
        }

        float waveAgain;
        bool heroWasNear;

        /// <summary>Standing about and the hero walks right by: a turn and a wave, now and then.</summary>
        void PassingWave()
        {
            if (kind == Kind.Dog || view == null || doing == Doing.Sitting) return;
            bool near = HeroWithin(3f);
            if (near && !heroWasNear && Time.time >= waveAgain && Random.value < 0.6f)
            {
                waveAgain = Time.time + Random.Range(50f, 90f);
                var p = Player.I;
                if (p != null) Factory.Face(transform, p.transform.position);
                view.Emote(EmoteDef.Get("wave"));
            }
            heroWasNear = near;
        }

        /// <summary>A new part of the day: set off for where it happens.</summary>
        void Begin(string now, Vector3 place, Doing there)
        {
            string was = plan;
            plan = now;
            partner = null;
            view?.StopEmote();
            if (hidden && there != Doing.Inside)
            {
                // Coming out of the house (or the barracks).
                SetHidden(false);
                transform.position = TownLife.Walkable(kind == Kind.Guard ? TownLife.Barracks : Home);
            }
            if (kind == Kind.Guard && was == "watch" && now == "barracks" && HeroWithin(20f))
                Speech.Say(transform, 2.6f, NightWatch ? "Dawn. The day watch has it. I need my bed." : "Your watch now. Keep the torches lit.");
            arriveDoing = there;
            Go(place, Doing.Walking);
            if (there == Doing.Patrolling) arriveDoing = Doing.Patrolling;
        }

        Doing arriveDoing;

        void Go(Vector3 place, Doing whileThere)
        {
            path.Clear();
            pathIndex = 0;
            if (!WorldGrid.Instance.FindPath(transform.position, TownLife.Walkable(place), path, 4000)) path.Add(place);
            if (whileThere == Doing.Walking) doing = Doing.Walking;
            else { doing = whileThere; waitUntil = Time.time + 1.5f + (float)rng.NextDouble() * 3f; }
        }

        void Arrive()
        {
            doing = arriveDoing;
            faceTo = Vector3.zero;
            switch (doing)
            {
                case Doing.Inside: SetHidden(true); break;
                case Doing.Working: faceTo = Work.FaceTo; nextAction = Time.time + 1f; break;
                case Doing.Sitting:
                    faceTo = kind == Kind.Dog ? Vector3.zero : TownLife.TavernDoor + new Vector3(2.5f, 0, 0);
                    if (view != null) view.Emote(EmoteDef.Get("sit"));
                    break;
                case Doing.Chatting: FindPartner(); break;
                case Doing.Patrolling: waitUntil = Time.time + 3f + (float)rng.NextDouble() * 4f; break;
                case Doing.Playing: waitUntil = Time.time + (float)rng.NextDouble() * 1.5f; break;
            }
        }

        float Move()
        {
            if (pathIndex >= path.Count) return 0f;
            var target = path[pathIndex];
            var to = Factory.Flat(target - transform.position);
            float sp = doing == Doing.Playing ? speed * 1.3f : speed;
            float step = sp * Time.deltaTime;
            if (to.magnitude <= step) { transform.position = target; pathIndex++; }
            else
            {
                transform.position += to.normalized * step;
                Factory.Face(transform, target, Time.deltaTime * 8f);
            }
            if (Time.time >= nextTrample) { nextTrample = Time.time + 0.3f; SnowField.Trample(transform.position); }
            return sp;
        }

        void SetHidden(bool h)
        {
            hidden = h;
            foreach (var r in renderers) if (r != null) r.enabled = !h;
        }

        // ------------------------------------------------------------------ activities

        void DoWork()
        {
            if (Time.time < nextAction) return;
            nextAction = Time.time + 2.6f + (float)rng.NextDouble() * 2.5f;
            if (view != null)
            {
                if (Work.Clip != null) view.Action(Work.Clip, 1.4f);
                else view.Interact();
            }
            if (Work.Sound != null && HeroWithin(20f)) Sfx.Play(Work.Sound, transform.position + Vector3.up, 0.25f, 0.15f, 16f);
        }

        /// <summary>Evening on the square: pair up with someone at the same meeting place and talk.</summary>
        void FindPartner()
        {
            foreach (var o in all)
                if (o != this && o.kind == Kind.Villager && o.doing == Doing.Chatting && o.partner == null && Factory.FlatDistance(o.transform.position, transform.position) < 4f)
                {
                    partner = o;
                    o.partner = this;
                    o.line = 0;
                    line = 0;
                    conversation = Conversations[rng.Next(Conversations.Length)];
                    o.conversation = conversation;
                    nextAction = Time.time + 1.5f;
                    o.nextAction = float.MaxValue; // the one who started speaks first
                    return;
                }
            nextAction = Time.time + 4f; // wait for someone to turn up
        }

        string[] conversation;

        void DoChat()
        {
            if (partner == null)
            {
                if (Time.time >= nextAction) FindPartner();
                return;
            }
            faceTo = partner.transform.position;
            if (Time.time < nextAction || conversation == null) return;
            if (line >= conversation.Length) { partner = null; nextAction = Time.time + 20f + (float)rng.NextDouble() * 20f; return; }
            if (HeroWithin(18f)) Speech.Say(transform, 2.6f, conversation[line]);
            view?.Interact(); // a gesture
            // The other one answers next.
            partner.line = line + 1;
            partner.nextAction = Time.time + 3.2f;
            nextAction = float.MaxValue;
            if (line + 1 >= conversation.Length) { partner.partner = null; partner.nextAction = Time.time + 25f; partner = null; nextAction = Time.time + 25f; }
        }

        static readonly string[][] Conversations =
        {
            new[] { "Did you hear the howling last night?", "Wolves. Closer every week.", "The Captain says not to worry.", "The Captain always says that." },
            new[] { "Rosie's stew today was something else.", "Something else is right. I'm not sure what." },
            new[] { "My boy wants to be an adventurer.", "Tell him to try farming first. Fewer goblins.", "He says farming is for people without swords." },
            new[] { "Have you seen the price of potions?", "Lysa says it's the goblins. Supply problems.", "Lysa says a lot of things." },
            new[] { "Another hero came through today.", "They always do. Few come back from the crypt.", "This one looked sturdier than most." },
            new[] { "Smith Gorrin's been hammering since dawn.", "Big order for the guard, I heard.", "Or he just likes hammering." },
            new[] { "Nice evening.", "It is.", "...", "Well. Good night then." },
        };

        void Roam()
        {
            // The dog: trot after someone who's out and about, or sniff around the square.
            Walker follow = null;
            foreach (var o in all) if (o != this && !o.hidden && o.kind != Kind.Dog && rng.NextDouble() < 0.3) { follow = o; break; }
            var p = Player.I;
            Vector3 goal = p != null && WorldGenerator.InHollowmere(p.transform.position) && rng.NextDouble() < 0.3
                ? p.transform.position + new Vector3((float)rng.NextDouble() * 2f - 1f, 0, (float)rng.NextDouble() * 2f - 1f) * 1.5f // come say hi
                : follow != null ? follow.transform.position : TownLife.RandomSquarePoint(rng);
            Go(goal, Doing.Nothing);
            waitUntil = Time.time + 4f + (float)rng.NextDouble() * 6f;
        }

        bool HeroWithin(float range)
        {
            var p = Player.I;
            return p != null && Factory.FlatDistance(p.transform.position, transform.position) < range;
        }

        void Chatter()
        {
            if (Time.time < chatterAt) return;
            chatterAt = Time.time + 25f + (float)rng.NextDouble() * 40f;
            if (kind == Kind.Dog || doing == Doing.Chatting || !HeroWithin(18f)) return;
            var p = Player.I;
            string line = doing == Doing.Working && rng.NextDouble() < 0.5 ? WorkLine() : NpcChatter.Line(speaker, p.DisplayName);
            if (line != null) Speech.Say(transform, kind == Kind.Child ? 1.6f : 2.6f, line);
        }

        string WorkLine()
        {
            switch (Work.Name)
            {
                case "farm": return Pick("These turnips won't weed themselves.", "Good soil this year.", "My back. My poor back.");
                case "market": return Pick("Fresh bread! Fresh-ish bread!", "Apples, cabbages, turnips!", "Best prices in Hollowmere!");
                case "well": return Pick("Heave... ho.", "Cold water, straight from the deep.", "Third bucket today.");
                case "windmill": return Pick("Grinding away.", "Flour for the whole village, this.", "The sails are turning nicely today.");
                case "church": return Pick("Light preserve us.", "...and protect the heroes in the crypt...", "Amen.");
                case "woodpile": return Pick("Firewood for the winter.", "*thock*", "One more log.");
                default: return null;
            }
        }

        string Pick(params string[] lines) => lines[rng.Next(lines.Length)];
    }
}
