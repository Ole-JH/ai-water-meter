using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The town's guards in an invasion (server/invasion.js runs them): archers who climb up onto the wall walk by the
    /// gate and shoot down at the invaders, and soldiers who march out and hold a line in front of it. They do a little
    /// (the heroes do the real work), they can be hurt and killed, and fresh ones come out with each new wave.
    /// </summary>
    public class TownGuards : MonoBehaviour
    {
        public static readonly Dictionary<int, TownGuards> All = new Dictionary<int, TownGuards>();
        static readonly Color Livery = new Color(0.55f, 0.75f, 1f);

        public int Id;
        public bool Archer;
        public float Health = 1f, MaxHealth = 1f;
        public string Title => Archer ? "Wall Archer" : "Town Guard";
        public Vector3 Head => transform.position + Vector3.up * 2.25f;

        CharacterView view;
        CharacterLook look;
        // Mustering (seen from the start of a siege): out of a house's door, through the streets to the gate; an archer
        // to the foot of a siege ladder, where he waits until the militia have it standing, then up it and along the walk.
        enum Way { None, Waiting, Walking, AtLadder, Climbing, OnWalk }
        float waitedAt; // how long an archer has stood at the foot of his ladder
        Way way;
        readonly List<Vector3> approach = new List<Vector3>();
        SiegeLadder ladder;
        float startAt, pace, climbK;
        HouseDoors.Door door;
        float nextReady;
        static readonly string[] musterCalls = { "To the walls!", "Form up! Shields!", "Look sharp, they're coming!", "Hold here. Nobody passes.", "Archers, find your marks!" };
        Vector3 target;
        // what they shout (one voice at a time across the line, so it doesn't turn into a babble)
        static float nextShout;
        static readonly string[] archerCalls = { "Loose!", "Mark your targets!", "Nock! Draw! Loose!", "Keep them off the gate!", "Another one, left side!" };
        static readonly string[] soldierCalls = { "Hold the line!", "Shields up!", "For the town!", "Push them back!", "Stand fast, lads!", "Not one step back!" };
        static readonly string[] hurtCalls = { "Argh!", "I'm hit!", "Still standing!", "Is that all you've got?" };
        static readonly string[] downCalls = { "Man down!", "We lost one! Close the gap!", "Medic! Get him out of here!" };

        static void Shout(TownGuards who, string[] lines, float chance)
        {
            if (who == null || Time.time < nextShout || Random.value > chance) return;
            var p = Player.I;
            if (p == null || Factory.FlatDistance(p.transform.position, who.transform.position) > 35f) return;
            nextShout = Time.time + Random.Range(3f, 6f);
            Speech.Say(who.transform, 2.5f, lines[Random.Range(0, lines.Length)]);
        }

        /// <summary>No battle shouts for a while (a call that matters is being given: it mustn't be talked over).</summary>
        public static void Hush(float seconds) => nextShout = Mathf.Max(nextShout, Time.time + seconds);

        /// <summary>The guards near <paramref name="at"/> raise their weapons and roar (a rallying speech, a blessing).</summary>
        public static void Rally(Vector3 at, float r)
        {
            foreach (var g in All.Values)
                if (g != null && g.dieAt < 0f && g.view != null && Factory.FlatDistance(g.transform.position, at) < r) g.view.Cheer();
        }

        /// <summary>The standing soldiers (not the archers) near <paramref name="at"/>.</summary>
        public static List<TownGuards> SoldiersNear(Vector3 at, float r)
        {
            var list = new List<TownGuards>();
            foreach (var g in All.Values)
                if (g != null && !g.Archer && g.dieAt < 0f && Factory.FlatDistance(g.transform.position, at) < r) list.Add(g);
            return list;
        }

        /// <summary>A guard near <paramref name="at"/> that's still standing (to call out a fall).</summary>
        static TownGuards Nearest(Vector3 at)
        {
            TownGuards best = null;
            float bd = 15f;
            foreach (var g in All.Values) if (g != null && g.dieAt < 0f && Factory.FlatDistance(g.transform.position, at) < bd) { bd = Factory.FlatDistance(g.transform.position, at); best = g; }
            return best;
        }
        float climbT = -1f, dieAt = -1f, speed;
        Vector3 lastPos;

        /// <summary>The guards as the server has them now (null: none): new ones appear, gone ones fall.</summary>
        public static void Sync(NetGuard[] list, bool over = false)
        {
            var seen = new HashSet<int>();
            if (list != null && !Dungeon.Active)
                foreach (var g in list)
                {
                    seen.Add(g.i);
                    if (!All.TryGetValue(g.i, out var tg) || tg == null) tg = Spawn(g);
                    tg.MaxHealth = Mathf.Max(1, g.mh);
                    tg.Health = g.hp;
                    tg.target = new Vector3(g.x, tg.Archer ? Rampart.Top : 0f, g.z);
                }
            var gone = new List<int>();
            foreach (var kv in All) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            // fallen ones fall; when it's all over (won, lost) they simply go back to their posts in town
            foreach (var id in gone) { var tg = All[id]; All.Remove(id); if (tg != null) { if (over) Destroy(tg.gameObject); else tg.Fall(); } }
        }

        static TownGuards Spawn(NetGuard g)
        {
            var go = new GameObject(g.k == "a" ? "WallArcher" : "TownGuard");
            var tg = go.AddComponent<TownGuards>();
            tg.Id = g.i;
            tg.Archer = g.k == "a";
            var look = tg.Archer
                ? new CharacterLook { Model = "Characters/RogueHooded", Height = 1.85f, Tint = new Color(0.85f, 0.95f, 1.1f) }
                : new CharacterLook { Model = "Characters/Knight", Height = 1.95f, Tint = new Color(0.85f, 0.9f, 1.1f), Weapon = "sword" };
            tg.view = CharacterView.Create(go.transform, look);
            tg.look = look;
            var ivm = Invasion.Current;
            var hero = Player.I;
            bool muster = ivm != null && (ivm.phase == "warn" || ivm.phase == "gather") && hero != null && WorldGrid.Instance != null
                && Factory.FlatDistance(hero.transform.position, Invasion.Gate) < 140f;
            if (muster && tg.Muster(g, ivm))
            {
                tg.target = new Vector3(g.x, tg.Archer ? Rampart.Top : 0f, g.z);
                tg.lastPos = go.transform.position;
                tg.nextReady = Time.time + Random.Range(2f, 5f);
                All[g.i] = tg;
                return tg;
            }
            if (tg.Archer)
            {
                // climbs up the inside of the wall onto the walkway, from just behind it
                var at = new Vector3(g.x, 0f, g.z);
                var inv = Invasion.Current;
                Vector3 back = inv != null ? Factory.Flat(new Vector3(g.x - inv.gx, 0f, g.z - inv.gz)).normalized : Vector3.zero;
                go.transform.position = at + back * 1.2f;
                tg.climbT = 0f;
            }
            else go.transform.position = new Vector3(g.x, 0f, g.z);
            tg.target = new Vector3(g.x, tg.Archer ? Rampart.Top : 0f, g.z);
            tg.lastPos = go.transform.position;
            tg.nextReady = Time.time + Random.Range(2f, 5f);
            var iv0 = Invasion.Current;
            if (iv0 != null && (iv0.phase == "warn" || iv0.phase == "gather")) Shout(tg, musterCalls, 0.5f); // mustering
            All[g.i] = tg;
            return tg;
        }

        /// <summary>Sets off from a house near the gate (false: no door or way found: appear at the post as before).</summary>
        bool Muster(NetGuard g, NetInvasion iv)
        {
            var gate = Invasion.Gate;
            var town = WorldGenerator.TownNamed(iv.town);
            if (town == null) return false;
            var inward = Factory.Flat(town.Center - gate).normalized;
            door = Rampart.DoorNear(gate + inward * 12f + Vector3.Cross(Vector3.up, inward) * Random.Range(-8f, 8f), 32f);
            if (door == null) return false;
            var start = TownLife.Walkable(door.Step);
            var post = new Vector3(g.x, 0f, g.z);
            Vector3 goal = post;
            if (Archer)
            {
                ladder = Rampart.LadderNear(post);
                if (ladder == null) return false;
                goal = ladder.Walk.Foot - ladder.Walk.Side.Out * Random.Range(0.6f, 1.4f) + ladder.Walk.Side.Axis * Random.Range(-0.8f, 0.8f);
            }
            approach.Clear();
            if (!WorldGrid.Instance.FindPath(start, goal, approach, 6000) || approach.Count == 0) return false;
            transform.position = start;
            way = Way.Waiting;
            startAt = Time.time + Random.Range(0.2f, Archer ? 3f : 5f); // they come out one by one, not all at once
            pace = Archer ? Random.Range(2.7f, 3.5f) : Random.Range(3f, 4f);
            if (view != null) view.Root.SetActive(false);
            return true;
        }

        /// <summary>The muster walk; true while it's still going (the server's position takes over after).</summary>
        bool Approach(float dt)
        {
            switch (way)
            {
                case Way.Waiting:
                    if (Time.time < startAt) return true;
                    if (view != null) view.Root.SetActive(true);
                    if (door != null) HouseDoors.Swing(door, 1.4f);
                    way = Way.Walking;
                    return true;
                case Way.Walking:
                {
                    if (approach.Count == 0) { way = Archer ? Way.AtLadder : Way.None; return way != Way.None; }
                    var to = Factory.Flat(approach[0] - transform.position);
                    float step = pace * dt;
                    if (to.magnitude <= step) { transform.position = new Vector3(approach[0].x, 0f, approach[0].z); approach.RemoveAt(0); }
                    else transform.position += to.normalized * step;
                    if (to.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), dt * 8f);
                    view?.UpdateLocomotion(pace);
                    return true;
                }
                case Way.AtLadder:
                    // waiting for the ladder to stand: looking up at the wall, shifting about
                    if (ladder == null) { way = Way.None; return false; }
                    Face(ladder.Walk.Foot);
                    view?.UpdateLocomotion(0f);
                    waitedAt += dt;
                    if (!ladder.Raised && waitedAt < 25f) return true; // (a ladder nobody puts up: up it anyway in the end)
                    transform.position = ladder.Walk.Foot;
                    climbK = -Random.Range(0f, 1.2f); // not all up the rungs at once
                    way = Way.Climbing;
                    return true;
                case Way.Climbing:
                {
                    climbK += dt / 1.8f;
                    if (climbK < 0f) { view?.UpdateLocomotion(0f); return true; }
                    var w = ladder.Walk;
                    transform.position = Vector3.Lerp(w.Foot, w.LadderTop, Mathf.Clamp01(climbK));
                    transform.rotation = Quaternion.LookRotation(w.Side.Out);
                    view?.UpdateLocomotion(1.2f);
                    if (climbK >= 1f) { transform.position = w.At(w.LadderAt); way = Way.OnWalk; }
                    return true;
                }
                case Way.OnWalk:
                {
                    var to = target - transform.position;
                    to.y = 0f;
                    float step = pace * 0.8f * dt;
                    if (to.magnitude <= step) { transform.position = target; way = Way.None; return false; }
                    transform.position += to.normalized * step;
                    transform.rotation = Quaternion.LookRotation(to);
                    view?.UpdateLocomotion(pace * 0.8f);
                    return true;
                }
            }
            return false;
        }

        /// <summary>One of their deeds (server "gev"): an arrow loosed, a swing, a blow taken, a fall.</summary>
        public static void Event(NetMsg m)
        {
            if (!All.TryGetValue(m.id, out var tg) || tg == null) return;
            var at = new Vector3(m.x, 0f, m.z);
            switch (m.k)
            {
                case "shot":
                    tg.Face(at);
                    if (tg.view != null) tg.view.Shoot();
                    var from = tg.transform.position + Vector3.up * 1.4f + tg.transform.forward * 0.4f;
                    bool flaming = m.f == 1; // a beacon is lit: fire arrows
                    Projectile.FireVisual(from, at + Vector3.up, 16f, flaming ? new Color(1f, 0.55f, 0.15f) : new Color(0.85f, 0.75f, 0.55f), 0.3f, 30f)
                        .WithTrail(flaming ? SpellFx.Trail.Fire : SpellFx.Trail.Arrow).WithShape(Projectile.Shape.Arrow).OverWalls(); // down from the wall walk
                    Sfx.Play("bow", from, 0.35f, 0.1f, 30f);
                    Shout(tg, archerCalls, 0.3f);
                    break;
                case "swing":
                    tg.Face(at);
                    if (tg.view != null) tg.view.Attack(0.8f);
                    Sfx.Play("swing", tg.transform.position + Vector3.up, 0.35f, 0.12f, 25f);
                    Shout(tg, soldierCalls, 0.3f);
                    break;
                case "hurt":
                    if (tg.view != null) tg.view.Hit();
                    Sfx.Play("hit_armor", tg.transform.position + Vector3.up, 0.3f, 0.15f, 25f);
                    Shout(tg, hurtCalls, 0.2f);
                    break;
                case "die":
                    All.Remove(tg.Id);
                    tg.Fall();
                    nextShout = 0f;
                    Shout(Nearest(tg.transform.position), downCalls, 1f);

                    break;
            }
        }

        void Face(Vector3 at)
        {
            var d = Factory.Flat(at - transform.position);
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }

        void Fall()
        {
            if (dieAt >= 0f) return;
            dieAt = Time.time;
            // his comrades carry him to the town's graveyard (SiegeLife); this body sinks away as that one takes its place
            if (view != null && view.Root.activeSelf)
            {
                view.Die();
                Sfx.Play("death", transform.position + Vector3.up, 0.3f, 0.1f, 25f);
                StartCoroutine(HandOver());
                return;
            }
            if (view != null) view.Die();
            Sfx.Play("death", transform.position + Vector3.up, 0.3f, 0.1f, 25f);
            Destroy(gameObject, 6f);
        }

        System.Collections.IEnumerator HandOver()
        {
            yield return new WaitForSeconds(2.2f); // he falls; then he lies there until they come for him
            SiegeLife.GuardDown(transform.position, look);
            Destroy(gameObject);
        }

        void Update()
        {
            if (dieAt >= 0f) return;
            float dt = Time.deltaTime;
            if (way != Way.None && Approach(dt)) { lastPos = transform.position; return; }
            if (climbT >= 0f)
            {
                // up the ladder-side of the wall, then a step onto the walkway
                climbT += dt / 1.6f;
                float k = Mathf.Clamp01(climbT);
                var p = transform.position;
                float y = Mathf.Lerp(0f, Rampart.Top, Mathf.Clamp01(k * 1.25f));
                var flat = Vector3.Lerp(new Vector3(p.x, 0f, p.z), new Vector3(target.x, 0f, target.z), Mathf.Clamp01(k * 1.25f - 0.25f));
                transform.position = new Vector3(flat.x, y, flat.z);
                if (k >= 1f) climbT = -1f;
                return;
            }
            var to = target - transform.position;
            float dist = Factory.Flat(to).magnitude;
            if (dist > 40f) transform.position = target;
            else if (dist > 6f) transform.position = Vector3.MoveTowards(transform.position, target, 6.5f * dt); // catching up at a run
            else if (dist > 0.03f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, Mathf.Max(3.2f, dist * 4f) * dt);
                if (Factory.Flat(to).sqrMagnitude > 0.01f && view != null && !Archer) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Factory.Flat(to)), dt * 8f);
            }
            float moved = Factory.FlatDistance(transform.position, lastPos);
            lastPos = transform.position;
            speed = Mathf.Lerp(speed, dt > 0f ? moved / dt : 0f, dt * 10f);
            // At their posts before the attack: soldiers brace behind their shields, archers point out the raiders' camp
            var iv = Invasion.Current;
            if (view != null && iv != null && (iv.phase == "warn" || iv.phase == "gather") && speed < 0.2f && dist < 0.3f && Time.time >= nextReady && !view.Emoting)
            {
                nextReady = Time.time + Random.Range(4f, 9f);
                var camp = new Vector3(iv.sx, 0f, iv.sz);
                if (iv.sx != 0f || iv.sz != 0f) Face(camp);
                view.Emote(EmoteDef.Get(Archer ? "point" : "guard"));
                return;
            }
            if (view != null && !view.Emoting) view.UpdateLocomotion(speed);
        }

        void OnDestroy() { if (All.TryGetValue(Id, out var tg) && tg == this) All.Remove(Id); }
    }
}
