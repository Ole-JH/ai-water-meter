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
                    if (!tg.Archer) SiegeLife.GuardDown(tg.transform.position, tg.look); // carried back to the healer
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
            if (view != null) view.Die();
            Sfx.Play("death", transform.position + Vector3.up, 0.3f, 0.1f, 25f);
            Destroy(gameObject, 6f);
        }

        void Update()
        {
            if (dieAt >= 0f) return;
            float dt = Time.deltaTime;
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
            if (dist > 6f) transform.position = target;
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
