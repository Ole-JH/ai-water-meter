using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The town getting ready in the scouts' warning (the 90 s before the charge), so the wait is never dead time. Beats
    /// on the countdown, each once a siege, only seen near the town:
    /// <list type="bullet">
    /// <item>90-70 s: the alarm bell rings; doors slam up and down the streets near the gate, mothers call children in.</item>
    /// <item>from 84 s: labourers haul crates, barrels and a cart into the street behind the gate, a barricade growing
    /// piece by piece; boys run bundles of arrows to the foot of the archers' ladder.</item>
    /// <item>62 s: a raider herald walks up under a banner and demands the town's surrender; an archer answers with an
    /// arrow at his feet, and he runs back to the jeering camp.</item>
    /// <item>48 s: the captain walks out before the soldiers and gives a rallying speech; they roar after each line.</item>
    /// <item>34 s: a priest goes down the line blessing each soldier.</item>
    /// <item>22 s: ranging shots from the camp thud into the wall and gate.</item>
    /// <item>14 s: the gate is barred: a great beam drops into its brackets.</item>
    /// <item>6 s: "Shields! Brace!"</item>
    /// </list>
    /// Everything here is visual and local (like SiegeLife); the people go when the charge comes, the barricade and the
    /// beam when the siege ends.
    /// </summary>
    public class SiegePrep : MonoBehaviour
    {
        static SiegePrep I;
        string key;
        readonly HashSet<string> done = new HashSet<string>();
        GameObject root;                         // the barricade, the arrow bundles, the beam
        readonly List<SiegeLife.Extra> crew = new List<SiegeLife.Extra>();
        SiegeLife.Extra captain;
        float nextBell, bellsUntil;
        int pieces;
        Vector3 gate, inward, across, barricade;
        Settlement town;
        readonly System.Random rng = new System.Random();
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        T Pick<T>(T[] a) => a[rng.Next(a.Length)];

        static readonly string[] homeCalls = { "Inside, children! Now!", "Bar the shutters!", "Get the little ones in!", "Bring the goat in, quick!", "Lock the door behind you!" };
        static readonly string[] haulers = { "Heave!", "Over here! Pile it up!", "Another barrel!", "Wedge it in tight!", "Mind your back!" };
        static readonly string[] boys = { "Arrows! More arrows!", "Coming through!", "Here, take these!" };

        public static void Ensure() { if (I == null) I = new GameObject("SiegePrep").AddComponent<SiegePrep>(); }

        void Update()
        {
            var iv = Invasion.Current;
            if (iv == null || iv.phase == "won" || iv.phase == "lost") { Reset(); return; }
            if (iv.phase != "warn") { Charge(); return; }
            var hero = Player.I;
            var t = WorldGenerator.TownNamed(iv.town);
            if (t == null || hero == null || Dungeon.Active || iv.paused || Factory.FlatDistance(hero.transform.position, t.Center) > 130f) return;
            string k = iv.town + "|" + iv.gate;
            if (k != key) Begin(iv, t, k);
            int left = Invasion.Countdown;

            if (Time.time < bellsUntil && Time.time >= nextBell)
            {
                nextBell = Time.time + 2.4f;
                Sfx.Play("bell", town.Center + Vector3.up * 8f, 0.55f, 0.04f, 140f);
            }
            Beat("bells", left, 90, 72, () => { bellsUntil = Time.time + Mathf.Min(20f, left - 66); StartCoroutine(Doors()); });
            Beat("barricade", left, 86, 30, Barricade);
            Beat("arrows", left, 82, 30, Arrows);
            Beat("herald", left, 64, 54, () => StartCoroutine(Herald(iv)));
            Beat("speech", left, 50, 40, () => StartCoroutine(Speech_()));
            Beat("priest", left, 36, 26, () => StartCoroutine(Priest()));
            Beat("ranging", left, 23, 16, () => StartCoroutine(Ranging(iv)));
            Beat("beam", left, 14, 7, () => StartCoroutine(Bar()));
            Beat("brace", left, 6, 2, Brace);
        }

        /// <summary>Runs a beat once a siege, when the countdown is between <paramref name="from"/> and <paramref name="until"/>.</summary>
        void Beat(string name, int left, int from, int until, System.Action run)
        {
            if (left > from || left < until || done.Contains(name)) return;
            done.Add(name);
            run();
        }

        void Begin(NetInvasion iv, Settlement t, string k)
        {
            Reset();
            key = k;
            town = t;
            var side = Rampart.SideOf(t, iv.gate);
            gate = side.Centre;
            inward = -side.Out;
            across = side.Axis;
            barricade = gate + inward * 7f;
            root = new GameObject("SiegePrep " + k);
        }

        /// <summary>The charge: the helpers clear off the streets (the barricade and the beam stay till it's over).</summary>
        void Charge()
        {
            foreach (var e in crew) if (e != null) { e.Tending = false; e.Party = null; e.FadeOut(R(0.5f, 2.5f)); }
            crew.Clear();
            captain = null;
        }

        void Reset()
        {
            Charge();
            if (root != null) Destroy(root);
            root = null;
            key = null;
            done.Clear();
            pieces = 0;
            bellsUntil = 0f;
            StopAllCoroutines();
        }

        SiegeLife.Extra Person(string name, string model, Vector3 at, float height, bool fromDoor = true)
        {
            var e = SiegeLife.Get().Person(name, model, at, height, fromDoor);
            e.SiegeOnly = true;
            crew.Add(e);
            return e;
        }

        static Vector3 W(Vector3 p) => TownLife.Walkable(p);
        bool Near(Vector3 p, float r = 45f) => Player.I != null && Factory.FlatDistance(Player.I.transform.position, p) < r;

        // ------------------------------------------------------------------ doors slam, children called in

        IEnumerator Doors()
        {
            var list = new List<HouseDoors.Door>();
            foreach (var d in HouseDoors.All)
                if (d != null && town.Contains(d.Centre.x, d.Centre.z) && Factory.FlatDistance(d.Centre, gate) < 45f) list.Add(d);
            list.Sort((a, b) => Factory.FlatDistance(a.Centre, gate).CompareTo(Factory.FlatDistance(b.Centre, gate)));
            for (int i = 0; i < list.Count && i < 12; i++)
            {
                yield return new WaitForSeconds(R(0.6f, 1.6f));
                var d = list[i];
                HouseDoors.Swing(d, 0.4f);
                if (rng.NextDouble() < 0.6) Sfx.Play("door_close", d.Centre + Vector3.up, 0.7f, 0.15f, 30f);
                if (i % 3 == 0 && Near(d.Centre))
                {
                    var voice = new GameObject("Voice").transform;
                    voice.position = d.Step;
                    Speech.Say(voice, 2.4f, Pick(homeCalls));
                    Destroy(voice.gameObject, 4f);
                }
            }
        }

        // ------------------------------------------------------------------ the barricade and the arrows

        void Barricade()
        {
            string[] models = { "Characters/Barbarian", "Characters/Keeper", "Characters/Rogue" };
            var pile = W(barricade + inward * 9f + across * 3f);
            for (int i = 0; i < 3; i++)
            {
                var e = Person("Labourer", models[i], W(pile + across * R(-1f, 1f)), R(1.8f, 1.95f));
                e.Delay += i * 1.5f;
                e.Shuttle(W(pile + across * R(-1f, 1f)), W(barricade + inward * 0.8f + across * R(-2f, 2f)));
                e.Delivered = x =>
                {
                    AddPiece();
                    if (Near(x.transform.position, 30f) && rng.NextDouble() < 0.35) Speech.Say(x.transform, 2.4f, Pick(haulers));
                };
            }
            // a couple of pieces there already (it was started the moment the bell went)
            AddPiece(); AddPiece();
        }

        /// <summary>The next piece of the barricade across the street: crates, barrels, a cart on its side, planks.</summary>
        void AddPiece()
        {
            if (root == null || pieces >= 11) return;
            int i = pieces++;
            float along = (i % 2 == 0 ? 1 : -1) * (i / 2) * 0.95f + R(-0.15f, 0.15f);
            var p = barricade + across * along;
            var local = p;
            float yaw = Mathf.Atan2(across.x, across.z) * Mathf.Rad2Deg + R(-20f, 20f);
            GameObject go = null;
            switch (i % 5)
            {
                case 0: go = ArtLibrary.Spawn("Props/crates_stacked", root.transform, local, R(0.9f, 1.15f), ArtLibrary.Fit.Height, yaw, true); break;
                case 1: go = ArtLibrary.Spawn("Props/barrel_large", root.transform, local, 1f, ArtLibrary.Fit.Height, yaw, true); break;
                case 2:
                    go = ArtLibrary.Spawn("Town/cart", root.transform, local, 2.2f, ArtLibrary.Fit.Width, yaw, true);
                    if (go != null) go.transform.rotation = Quaternion.Euler(0f, yaw, 70f); // tipped on its side
                    break;
                case 3: go = ArtLibrary.Spawn("Props/box_stacked", root.transform, local, R(0.8f, 1f), ArtLibrary.Fit.Height, yaw, true); break;
                default:
                    for (int k = 0; k < 4; k++)
                        Factory.PrimAt(PrimitiveType.Cube, root.transform, p + Vector3.up * (0.12f + k * 0.2f) + inward * R(-0.2f, 0.2f), new Vector3(0.2f, 0.18f, 2.2f), new Color(0.55f, 0.4f, 0.25f) * R(0.85f, 1.05f))
                            .transform.rotation = Quaternion.Euler(0f, yaw + 90f + R(-8f, 8f), R(-6f, 6f));
                    break;
            }
            SpellFx.Dust(p, 0.8f);
            if (Near(p, 35f)) Sfx.Play("hit_heavy", p + Vector3.up * 0.5f, 0.35f, 0.15f, 30f);
        }

        void Arrows()
        {
            var ladder = Rampart.LadderNear(gate);
            if (ladder == null) return;
            var foot = W(ladder.Walk.Foot + inward * 1.2f);
            var from = W(gate + inward * 16f - across * 5f);
            for (int i = 0; i < 2; i++)
            {
                var e = Person("Boy", i == 0 ? "Characters/Rogue" : "Characters/RogueHooded", from, 1.2f);
                e.Delay += i * 3f;
                e.Speed = R(3.6f, 4.2f);
                e.Shuttle(W(from + across * R(-1f, 1f)), W(foot + across * R(-1f, 1f)));
                e.Delivered = x =>
                {
                    if (root == null) return;
                    // a bundle of arrows stood against the wall's foot, fletchings up
                    var b = foot + across * R(-1.2f, 1.2f);
                    Factory.PrimAt(PrimitiveType.Cylinder, root.transform, b + Vector3.up * 0.4f, new Vector3(0.18f, 0.4f, 0.18f), new Color(0.6f, 0.45f, 0.28f));
                    Factory.PrimAt(PrimitiveType.Cylinder, root.transform, b + Vector3.up * 0.85f, new Vector3(0.22f, 0.06f, 0.22f), new Color(0.92f, 0.9f, 0.85f));
                    if (Near(x.transform.position, 30f) && rng.NextDouble() < 0.4) Speech.Say(x.transform, 1.8f, Pick(boys));
                };
            }
        }

        // ------------------------------------------------------------------ the herald

        IEnumerator Herald(NetInvasion iv)
        {
            var camp = new Vector3(iv.sx, 0f, iv.sz);
            if (camp == Vector3.zero) yield break;
            CharacterLook look = null;
            foreach (var en in Enemy.ById.Values)
                if (en != null && !en.IsDead && en.Def != null && Factory.FlatDistance(en.transform.position, camp) < 12f) { look = CharacterLook.ForMonster(en.Def.Name); if (look != null) break; }
            if (look == null) look = new CharacterLook { Model = "Characters/Barbarian", Height = 2f, Tint = new Color(0.55f, 0.45f, 0.4f) };
            var stand = W(gate - inward * 17f); // well out beyond the soldiers before the gate, in bowshot of the wall
            var h = SiegeLife.Extra.Make(SiegeLife.Get(), "Raider Herald", look, W(camp + (stand - camp).normalized * 4f), 2.2f);
            h.SiegeOnly = true;
            crew.Add(h);
            // his banner: a pole with a black flag and a skull
            var pole = Factory.Prim(PrimitiveType.Cube, h.transform, new Vector3(0.45f, 1.6f, 0f), new Vector3(0.07f, 3.2f, 0.07f), new Color(0.25f, 0.18f, 0.12f));
            Factory.Prim(PrimitiveType.Cube, h.transform, new Vector3(0.85f, 2.75f, 0f), new Vector3(0.8f, 0.7f, 0.03f), new Color(0.12f, 0.1f, 0.1f));
            Factory.Prim(PrimitiveType.Sphere, h.transform, new Vector3(0.85f, 2.8f, -0.03f), new Vector3(0.25f, 0.25f, 0.05f), new Color(0.85f, 0.82f, 0.75f));
            h.Walk(stand);
            bool there = false;
            h.OnArrive = x => there = true;
            float until = Time.time + 25f;
            while (!there && Time.time < until && h != null) yield return null;
            if (h == null) yield break;
            h.FaceAt = gate;
            string name = town.Name.Split(' ')[0];
            string[] demands =
            {
                "Hear me, people of " + name + "!",
                "My chief offers you this: open your gate, and only your gold burns.",
                "Refuse... and we take everything. Your stores. Your homes. You.",
                "What is your answer?!",
            };
            foreach (var line in demands)
            {
                if (h == null) yield break;
                Speech.Say(h.transform, 2.9f, line);
                yield return new WaitForSeconds(3.4f);
            }
            // the answer, from the wall
            TownGuards archer = null;
            foreach (var g in TownGuards.All.Values) if (g != null && g.Archer && (archer == null || Factory.FlatDistance(g.transform.position, stand) < Factory.FlatDistance(archer.transform.position, stand))) archer = g;
            if (archer != null) Speech.Say(archer.transform, 2.6f, "Here's our answer!");
            yield return new WaitForSeconds(0.8f);
            if (h == null) yield break;
            var at = h.transform.position + (gate - h.transform.position).normalized * 0.8f;
            if (archer != null)
                Projectile.FireVisual(archer.Head, at + Vector3.up * 0.1f, 22f, new Color(0.85f, 0.75f, 0.55f), 0.25f, 40f).WithTrail(SpellFx.Trail.Arrow).WithShape(Projectile.Shape.Arrow).OverWalls();
            yield return new WaitForSeconds(0.6f);
            if (h == null) yield break;
            var stuck = Factory.PrimAt(PrimitiveType.Cube, root != null ? root.transform : null, at + Vector3.up * 0.3f, new Vector3(0.03f, 0.03f, 0.8f), new Color(0.6f, 0.48f, 0.3f));
            stuck.transform.rotation = Quaternion.LookRotation(Vector3.down + (h.transform.position - gate).normalized * 0.6f);
            SpellFx.Dust(at, 0.4f);
            Sfx.Play("hit_stone", at, 0.6f, 0.1f, 40f);
            Speech.Say(h.transform, 2.9f, "You'll burn for that! ALL OF YOU!");
            TownGuards.Rally(gate, 30f);
            h.Speed = 5.5f;
            h.FaceAt = null;
            h.Walk(W(camp));
            h.OnArrive = x => x.FadeOut(1f);
            foreach (var e in Enemy.ById.Values) if (e != null && !e.IsDead && Factory.FlatDistance(e.transform.position, camp) < 12f && rng.NextDouble() < 0.7) e.Cheer();
        }

        // ------------------------------------------------------------------ the captain's speech, the blessing

        IEnumerator Speech_()
        {
            var line = TownGuards.SoldiersNear(gate, 25f);
            var mid = gate;
            if (line.Count > 0) { mid = Vector3.zero; foreach (var g in line) mid += g.transform.position; mid /= line.Count; }
            var outward = -inward;
            var spot = W(mid + outward * 2.8f);
            captain = Person("Captain Aldric", "Characters/Knight", W(gate + inward * 6f), 2.05f, false);
            captain.Speed = 3.2f;
            captain.Walk(spot);
            bool there = false;
            captain.OnArrive = x => there = true;
            float until = Time.time + 12f;
            while (!there && Time.time < until && captain != null) yield return null;
            if (captain == null) yield break;
            captain.FaceAt = mid;
            string name = town.Name.Split(' ')[0];
            string[] speech =
            {
                "Men of " + name + "! Eyes on me!",
                "Out there are raiders who think this town is easy pickings.",
                "Behind you: your homes. Your families. Your ale!",
                "Not one of them gets through this gate. NOT ONE!",
                "FOR " + name.ToUpperInvariant() + "!",
            };
            for (int i = 0; i < speech.Length; i++)
            {
                if (captain == null) yield break;
                Speech.Say(captain.transform, 3.1f, speech[i]);
                captain.Cheer();
                yield return new WaitForSeconds(i == speech.Length - 1 ? 0.6f : 3.4f);
                if (i >= 2)
                {
                    TownGuards.Rally(mid, 14f);
                    if (Near(mid)) Sfx.Play("swing_heavy", mid + Vector3.up, 0.4f, 0.2f, 40f);
                }
            }
            // the line roars back
            foreach (var g in line)
                if (g != null && rng.NextDouble() < 0.5) Speech.Say(g.transform, 2.5f, rng.NextDouble() < 0.5 ? "FOR " + name.ToUpperInvariant() + "!" : "HOO-RAH!");
            TownGuards.Rally(mid, 14f);
            if (captain != null) { captain.Party = "guard"; captain.Planted = true; }
        }

        IEnumerator Priest()
        {
            var line = TownGuards.SoldiersNear(gate, 25f);
            if (line.Count == 0) yield break;
            var p = Person("Brother Anselm", "Characters/Mage", W(gate + inward * 8f), 1.85f, false);
            p.Speed = 2.4f;
            line.Sort((a, b) => Vector3.Dot(a.transform.position - gate, across).CompareTo(Vector3.Dot(b.transform.position - gate, across)));
            string[] blessings = { "The light keep you.", "Stand firm, my son.", "May your arm not tire.", "Go with the light." };
            foreach (var g in line)
            {
                if (p == null || g == null) continue;
                bool there = false;
                p.Walk(W(g.transform.position - inward * 1.1f));
                p.OnArrive = x => there = true;
                float until = Time.time + 8f;
                while (!there && Time.time < until && p != null) yield return null;
                if (p == null || g == null) continue;
                p.FaceAt = g.transform.position;
                p.View?.Cast();
                SpellFx.Flash(g.transform.position + Vector3.up * 1.2f, new Color(1f, 0.9f, 0.55f), 4f, 2.5f, 0.6f);
                SpellFx.Hit(g.Head, new Color(1f, 0.9f, 0.5f), false, 12);
                if (Near(g.transform.position, 35f)) Sfx.Play("holy_cast", g.transform.position + Vector3.up, 0.35f, 0.1f, 30f);
                if (rng.NextDouble() < 0.5) Speech.Say(p.transform, 2.4f, Pick(blessings));
                yield return new WaitForSeconds(0.9f);
                p.FaceAt = null;
            }
            if (p != null) { p.Walk(W(gate + inward * 10f)); p.OnArrive = x => x.FadeOut(1f); }
        }

        // ------------------------------------------------------------------ ranging shots, the gate barred, brace

        IEnumerator Ranging(NetInvasion iv)
        {
            var camp = new Vector3(iv.sx, 0f, iv.sz);
            if (camp == Vector3.zero) yield break;
            var side = Rampart.SideOf(town, iv.gate);
            for (int i = 0; i < 4; i++)
            {
                var from = camp + across * R(-4f, 4f) + Vector3.up * 1.6f;
                var to = side.Point(side.Mid + R(-7f, 7f)) + Vector3.up * R(0.8f, 2.4f);
                float speed = 20f, dist = Vector3.Distance(from, to);
                Projectile.FireVisual(from, to, speed, new Color(0.85f, 0.75f, 0.55f), 0.25f, dist + 1f).WithTrail(SpellFx.Trail.Arrow).WithShape(Projectile.Shape.Arrow);
                StartCoroutine(Thunk(to, dist / speed, from));
                if (i == 0) Call("Ranging shots! Heads down!");
                yield return new WaitForSeconds(R(0.8f, 1.8f));
            }
        }

        IEnumerator Thunk(Vector3 at, float after, Vector3 from)
        {
            yield return new WaitForSeconds(after);
            if (root == null) yield break;
            var dir = (at - from).normalized;
            var a = Factory.PrimAt(PrimitiveType.Cube, root.transform, at - dir * 0.35f, new Vector3(0.07f, 0.07f, 1.1f), new Color(0.72f, 0.58f, 0.36f));
            a.transform.rotation = Quaternion.LookRotation(dir); // half sunk in the timber
            var fl = Factory.PrimAt(PrimitiveType.Cube, root.transform, at - dir * 0.85f, new Vector3(0.18f, 0.18f, 0.22f), new Color(0.95f, 0.93f, 0.88f));
            fl.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, 45f);
            if (Near(at, 40f)) Sfx.Play("hit_heavy", at, 0.3f, 0.2f, 35f);
            SpellFx.Hit(at, new Color(0.6f, 0.5f, 0.35f), false, 6);
        }

        /// <summary>An order that must be heard: the captain gives it if he's out, else the guard nearest the gate.</summary>
        void Call(string line)
        {
            TownGuards.Hush(4.5f);
            if (captain != null) { Speech.Say(captain.transform, 3.1f, line); captain.Cheer(); return; }
            var g = Nearest(gate);
            if (g != null) Speech.Say(g.transform, 2.6f, line);
        }

        TownGuards Nearest(Vector3 p)
        {
            TownGuards best = null;
            foreach (var g in TownGuards.All.Values)
                if (g != null && (best == null || Factory.FlatDistance(g.transform.position, p) < Factory.FlatDistance(best.transform.position, p))) best = g;
            return best;
        }

        IEnumerator Bar()
        {
            if (root == null) yield break;
            Call("Bar the gate!");
            yield return new WaitForSeconds(1.2f);
            if (root == null) yield break;
            // the brackets either side of the gateway, inside, and the beam that drops into them
            var c = gate + inward * 0.9f;
            foreach (float s in new[] { -2.7f, 2.7f })
                Factory.PrimAt(PrimitiveType.Cube, root.transform, c + across * s + Vector3.up * 1.5f, new Vector3(0.25f, 0.5f, 0.25f), new Color(0.25f, 0.25f, 0.28f));
            var beam = Factory.PrimAt(PrimitiveType.Cube, root.transform, c + Vector3.up * 4f, Vector3.one, new Color(0.62f, 0.45f, 0.27f));
            beam.transform.rotation = Quaternion.LookRotation(across);
            beam.transform.localScale = new Vector3(0.5f, 0.5f, 6.2f);
            foreach (float s in new[] { -2f, 0f, 2f }) // iron bands
                Factory.Prim(PrimitiveType.Cube, beam.transform, new Vector3(0f, 0f, s / 6.2f), new Vector3(1.08f, 1.08f, 0.04f), new Color(0.2f, 0.2f, 0.22f));
            float k = 0f;
            while (k < 1f && beam != null)
            {
                k += Time.deltaTime / 0.35f;
                beam.transform.position = c + Vector3.up * Mathf.Lerp(4f, 1.6f, k * k);
                yield return null;
            }
            if (beam == null) yield break;
            if (Near(c, 50f))
            {
                Sfx.Play("hit_heavy", c + Vector3.up * 1.6f, 0.9f, 0.05f, 60f);
                Sfx.Play("rubble", c + Vector3.up, 0.4f, 0.1f, 40f);
                CameraRig.Shake(0.25f);
            }
            SpellFx.Dust(c + Vector3.up * 1.6f, 1.6f);
        }

        void Brace()
        {
            Call("SHIELDS! BRACE!");
            TownGuards.Rally(gate, 25f);
        }
    }
}
