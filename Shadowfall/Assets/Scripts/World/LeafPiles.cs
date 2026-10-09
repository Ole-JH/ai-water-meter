using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// A heap of autumn leaves in a villager's yard: a mound of individual leaves. Run through it and it bursts, leaves
    /// flying up and fluttering down all around, and the pile is spread over the yard; its <see cref="Raker"/> rakes it
    /// back together, leaf by leaf (after dealing with you).
    /// </summary>
    public class LeafPile : MonoBehaviour
    {
        const int Count = 170;
        public const float Radius = 1.15f;

        public Vector3 Center { get; private set; }
        /// <summary>The local hero ran through it: where from and which way.</summary>
        public event System.Action<Vector3> Kicked;

        readonly Vector3[] home = new Vector3[Count], pos = new Vector3[Count], vel = new Vector3[Count];
        readonly float[] yaw = new float[Count], spin = new float[Count], tilt = new float[Count];
        readonly bool[] flying = new bool[Count];
        Mesh mesh;
        Vector3[] verts;
        bool dirty = true;
        int airborne;
        float cooldown;
        System.Random rng;

        static Material leafMat;

        public static LeafPile Create(Transform parent, Vector3 at, int seed)
        {
            var go = new GameObject("LeafPile");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<LeafPile>();
            p.Center = new Vector3(at.x, 0f, at.z);
            p.rng = new System.Random(seed);
            p.Build();
            return p;
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        void Build()
        {
            var colors = new Color[Count * 4];
            for (int i = 0; i < Count; i++)
            {
                // A mound: denser and higher in the middle.
                float r = Radius * Mathf.Sqrt(R(0f, 1f)), a = R(0f, Mathf.PI * 2f);
                float h = (1f - (r / Radius) * (r / Radius)) * 0.55f + R(0f, 0.08f);
                home[i] = Center + new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r);
                pos[i] = home[i];
                yaw[i] = R(0f, 360f);
                tilt[i] = R(-35f, 35f);
                spin[i] = R(-400f, 400f);
                var c = Color.Lerp(new Color(0.75f, 0.25f, 0.06f), new Color(0.92f, 0.62f, 0.12f), R(0f, 1f)) * R(0.7f, 1.05f);
                if (R(0f, 1f) < 0.15f) c = new Color(0.45f, 0.3f, 0.15f); // a few brown ones
                c.a = 0f; // (the grass shader's sway and tint follow alpha: keep the leaves still)
                for (int k = 0; k < 4; k++) colors[i * 4 + k] = c;
            }
            verts = new Vector3[Count * 4];
            var tris = new int[Count * 6];
            for (int i = 0; i < Count; i++)
            {
                int v = i * 4, t = i * 6;
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2; tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }
            mesh = new Mesh { name = "Leaves" };
            mesh.MarkDynamic();
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = tris;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r2 = gameObject.AddComponent<MeshRenderer>();
            r2.sharedMaterial = LeafMaterial;
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Rebuild();
        }

        static Material LeafMaterial
        {
            get
            {
                if (leafMat != null) return leafMat;
                var shader = Resources.Load<Shader>("Shaders/ShadowfallGrass");
                leafMat = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = "Leaves" };
                leafMat.SetFloat("_Wind", 0f);
                return leafMat;
            }
        }

        /// <summary>How much of the pile is still a pile (0..1).</summary>
        public float Intact
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++) if (!flying[i] && (pos[i] - home[i]).sqrMagnitude < 0.04f) n++;
                return (float)n / Count;
            }
        }

        /// <summary>A scattered leaf to go and rake (null when the pile is whole).</summary>
        public Vector3? Stray()
        {
            float best = -1f;
            Vector3? at = null;
            for (int i = 0; i < Count; i++)
            {
                if (flying[i]) continue;
                float d = Factory.FlatDistance(pos[i], Center);
                if (d > Radius + 0.3f && d > best) { best = d; at = pos[i]; }
            }
            return at;
        }

        /// <summary>The rake sweeps scattered leaves near <paramref name="at"/> a little way back toward the pile.</summary>
        public void Rake(Vector3 at, float strength)
        {
            for (int i = 0; i < Count; i++)
            {
                if (flying[i] || Factory.FlatDistance(pos[i], at) > 1.6f) continue;
                var to = home[i] - pos[i];
                float d = to.magnitude;
                if (d < 0.01f) continue;
                // Along the ground until close to the pile, then up onto it.
                float step = Mathf.Min(d, strength * Random.Range(0.6f, 1.2f));
                var flat = Factory.Flat(to);
                pos[i] += flat.magnitude > 0.3f ? flat.normalized * step : to.normalized * step;
                if (flat.magnitude > 0.3f) pos[i].y = 0.02f;
                dirty = true;
            }
        }

        /// <summary>Something burst through: the leaves fly up and away from it.</summary>
        public void Burst(Vector3 from, Vector3 dir)
        {
            dir = Factory.Flat(dir).normalized;
            for (int i = 0; i < Count; i++)
            {
                var away = Factory.Flat(pos[i] - from);
                if (away.magnitude > Radius + 1.2f) continue;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : Random.insideUnitSphere;
                vel[i] = (away * R(1.5f, 4f) + dir * R(1f, 3.5f)) + Vector3.up * R(2.5f, 6f);
                if (!flying[i]) airborne++;
                flying[i] = true;
            }
            dirty = true;
            Sfx.Play("leaves", Center + Vector3.up * 0.5f, 0.75f, 0.1f, 25f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            cooldown -= dt;
            if (airborne > 0) Fly(dt);
            CheckRunThrough();
            if (dirty) Rebuild();
        }

        void Fly(float dt)
        {
            float t = Time.time;
            for (int i = 0; i < Count; i++)
            {
                if (!flying[i]) continue;
                // Leaves fall slowly, sway side to side and spin.
                vel[i].y -= 7f * dt;
                vel[i] *= 1f - 2.2f * dt;
                vel[i].y = Mathf.Max(vel[i].y, -1.6f);
                var sway = new Vector3(Mathf.Sin(t * 5f + i), 0f, Mathf.Cos(t * 4.3f + i * 1.7f)) * 0.9f;
                pos[i] += (vel[i] + sway * Mathf.Clamp01(-vel[i].y)) * dt;
                yaw[i] += spin[i] * dt;
                tilt[i] = Mathf.Sin(t * 6f + i) * 50f;
                if (pos[i].y <= 0.02f && vel[i].y <= 0f)
                {
                    pos[i].y = 0.02f;
                    vel[i] = Vector3.zero;
                    tilt[i] = Random.Range(-15f, 15f);
                    flying[i] = false;
                    airborne--;
                }
            }
            dirty = true;
        }

        /// <summary>Running (not walking) through a standing pile bursts it.</summary>
        void CheckRunThrough()
        {
            var p = Player.I;
            if (p == null || cooldown > 0f || p.IsDead) return;
            var at = p.transform.position;
            if (Factory.FlatDistance(at, Center) > Radius) return;
            if (p.CurrentSpeed < 2.5f || Intact < 0.5f) return;
            cooldown = 3f;
            Burst(at - p.transform.forward * 0.5f, p.transform.forward);
            Kicked?.Invoke(at);
        }

        void Rebuild()
        {
            dirty = false;
            for (int i = 0; i < Count; i++)
            {
                var rot = Quaternion.Euler(tilt[i], yaw[i], tilt[i] * 0.5f);
                var right = rot * new Vector3(0.09f, 0f, 0f);
                var fwd = rot * new Vector3(0f, 0f, 0.06f);
                var c = pos[i] - transform.position;
                verts[i * 4] = c - right - fwd;
                verts[i * 4 + 1] = c - right * 0.2f + fwd * 1.2f;
                verts[i * 4 + 2] = c + right + fwd * 0.3f;
                verts[i * 4 + 3] = c + right * 0.2f - fwd * 1.1f;
            }
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }

    /// <summary>
    /// A villager raking leaves into a pile. Run through the pile and they get very cross: they drop everything, chase you
    /// down, give you one good whack with the rake (1 life) and a piece of their mind, then go back and rake it all up again.
    /// </summary>
    public class Raker : MonoBehaviour
    {
        enum State { Raking, Chasing, Whacking, Returning, Gathering }
        const float WalkSpeed = 1.6f, ChaseSpeed = 7.4f;

        static readonly string[] angry = { "OI! My leaves!", "HEY! I just raked that!", "You absolute hooligan!", "Three hours! THREE HOURS of raking!", "Come back here!" };
        static readonly string[] scold = { "And STAY out of my leaves!", "That'll teach you.", "Every autumn. Every single autumn.", "Hooligans, the lot of you." };
        static readonly string[] giveUp = { "...and don't come back!", "Too fast. Too young. Too rude.", "I'll remember that face!" };
        static readonly string[] idle = { "Rake, rake, rake...", "Lovely colours this year.", "Don't even think about it.", "Biggest pile in Hollowmere, this.", "Mind the pile, {name}." };
        static readonly string[] models = { "Characters/Keeper", "Characters/Mage", "Characters/Barbarian", "Characters/RogueHooded" };

        LeafPile pile;
        CharacterView view;
        State state = State.Raking;
        Vector3 workSpot;
        Vector3? gatherAt;
        float actionAt, stateUntil, chatterAt, repath;
        readonly List<Vector3> path = new List<Vector3>();
        int pathIndex;
        bool kenney;

        public static Raker Create(Transform parent, LeafPile pile, int seed)
        {
            var go = new GameObject("Raker");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<Raker>();
            r.pile = pile;
            r.workSpot = pile.Center + new Vector3(Mathf.Cos(seed * 2.1f), 0f, Mathf.Sin(seed * 2.1f)) * (LeafPile.Radius + 0.8f);
            go.transform.position = r.workSpot;
            string model = models[seed % models.Length];
            r.kenney = model.EndsWith("Keeper");
            r.view = CharacterView.Create(go.transform, new CharacterLook
            {
                Model = model, Height = 1.8f, Anims = r.kenney ? AnimSet.Kenney : AnimSet.KayKit, Tint = new Color(0.95f, 0.85f, 0.7f),
            });
            if (r.view == null)
                HumanoidModel.Build(go.transform, 0.95f, new Color(0.9f, 0.75f, 0.6f), new Color(0.6f, 0.4f, 0.25f), Color.gray, Color.gray, false, true);
            r.GiveRake();
            r.chatterAt = Time.time + 15f + seed * 7f;
            pile.Kicked += r.OnKicked;
            return r;
        }

        /// <summary>A wooden rake: a long handle with a row of tines.</summary>
        void GiveRake()
        {
            Transform hand = view != null ? view.Hand : transform;
            var rake = new GameObject("Rake").transform;
            rake.SetParent(hand, false);
            float s = 1f / Mathf.Max(0.0001f, hand.lossyScale.y);
            rake.localScale = Vector3.one * s;
            if (view == null) rake.localPosition = new Vector3(0.35f, 0.9f, 0.2f);
            var wood = new Color(0.55f, 0.38f, 0.22f);
            // Held near one end, the head at the far end (along the hand's +Y, like a sword).
            Factory.Prim(PrimitiveType.Cylinder, rake, new Vector3(0f, 0.45f, 0f), new Vector3(0.04f, 0.75f, 0.04f), wood);
            Factory.Prim(PrimitiveType.Cube, rake, new Vector3(0f, 1.2f, 0f), new Vector3(0.45f, 0.05f, 0.05f), wood * 0.8f);
            for (int i = 0; i < 7; i++)
                Factory.Prim(PrimitiveType.Cube, rake, new Vector3(-0.2f + i * 0.067f, 1.27f, 0.03f), new Vector3(0.018f, 0.12f, 0.018f), wood * 0.7f);
        }

        void OnDestroy()
        {
            if (pile != null) pile.Kicked -= OnKicked;
        }

        void OnKicked(Vector3 at)
        {
            if (state == State.Chasing || state == State.Whacking) return;
            state = State.Chasing;
            stateUntil = Time.time + 14f;
            path.Clear();
            repath = 0f;
            Speech.Say(transform, 2.4f, angry[Random.Range(0, angry.Length)]);
            view?.Cheer(); // arms up in outrage
            actionAt = Time.time + 0.6f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var p = Player.I;
            switch (state)
            {
                case State.Raking:
                    Idle(dt);
                    if (pile.Intact < 0.85f) { state = State.Gathering; gatherAt = null; }
                    break;

                case State.Gathering:
                    if (gatherAt == null) gatherAt = pile.Stray();
                    if (gatherAt == null) { state = State.Returning; break; }
                    // Stand just beyond the stray leaves and rake them in toward the pile.
                    var from = gatherAt.Value + Factory.Flat(gatherAt.Value - pile.Center).normalized * 0.9f;
                    if (WalkTo(from, WalkSpeed, dt))
                    {
                        Factory.Face(transform, pile.Center, dt * 6f);
                        if (Time.time >= actionAt)
                        {
                            actionAt = Time.time + 1.4f;
                            RakeMotion();
                            pile.Rake(gatherAt.Value, 0.9f);
                            Sfx.Play("rake", transform.position, 0.35f, 0.1f, 16f);
                            var next = gatherAt.Value + Factory.Flat(pile.Center - gatherAt.Value).normalized * 0.8f;
                            gatherAt = Factory.FlatDistance(next, pile.Center) < LeafPile.Radius ? null : next;
                        }
                    }
                    break;

                case State.Chasing:
                    if (Time.time < actionAt) { view?.UpdateLocomotion(0f); break; } // still shouting
                    if (p == null || p.IsDead || Time.time > stateUntil || Factory.FlatDistance(p.transform.position, transform.position) > 30f
                        || !WorldGenerator.InHollowmere(p.transform.position) || Dungeon.Active)
                    {
                        Speech.Say(transform, 2.4f, giveUp[Random.Range(0, giveUp.Length)]);
                        state = State.Returning;
                        path.Clear();
                        break;
                    }
                    if (Factory.FlatDistance(p.transform.position, transform.position) < 1.4f)
                    {
                        state = State.Whacking;
                        Factory.Face(transform, p.transform.position, 1f);
                        view?.Attack(0.8f);
                        actionAt = Time.time + 0.35f;
                        stateUntil = Time.time + 1.4f;
                        break;
                    }
                    Chase(p.transform.position, dt);
                    break;

                case State.Whacking:
                    view?.UpdateLocomotion(0f);
                    if (actionAt > 0f && Time.time >= actionAt)
                    {
                        actionAt = -1f;
                        if (p != null && Factory.FlatDistance(p.transform.position, transform.position) < 2.2f)
                        {
                            p.Poke(1, transform.position);
                            p.Achievements.Add("raked");
                            Sfx.Play("hit_bone", p.transform.position + Vector3.up, 0.5f, 0.1f);
                            GameUI.Log("A villager whacks you with a rake. That's fair, honestly.", new Color(1f, 0.6f, 0.3f));
                        }
                        Speech.Say(transform, 2.4f, scold[Random.Range(0, scold.Length)]);
                    }
                    if (Time.time >= stateUntil) { state = State.Returning; path.Clear(); }
                    break;

                case State.Returning:
                    if (WalkTo(workSpot, WalkSpeed * 1.4f, dt)) state = pile.Intact < 0.85f ? State.Gathering : State.Raking;
                    break;
            }
            Chatter();
        }

        void Idle(float dt)
        {
            view?.UpdateLocomotion(0f);
            Factory.Face(transform, pile.Center, dt * 4f);
            if (Time.time < actionAt) return;
            actionAt = Time.time + Random.Range(1.6f, 2.6f);
            RakeMotion();
            pile.Rake(pile.Center, 0.4f);
            Sfx.Play("rake", transform.position, 0.25f, 0.1f, 14f);
        }

        void RakeMotion()
        {
            if (view == null) return;
            if (kenney) view.Interact();
            else view.Action("Interact", 1.1f);
        }

        /// <summary>Walks straight there (yards are open ground). True on arrival.</summary>
        bool WalkTo(Vector3 target, float speed, float dt)
        {
            var to = Factory.Flat(target - transform.position);
            if (to.magnitude < 0.15f) { view?.UpdateLocomotion(0f); return true; }
            transform.position += to.normalized * Mathf.Min(to.magnitude, speed * dt);
            Factory.Face(transform, target, dt * 8f);
            view?.UpdateLocomotion(speed);
            return false;
        }

        /// <summary>Runs after the hero around houses and fences (a fresh path every half second).</summary>
        void Chase(Vector3 target, float dt)
        {
            repath -= dt;
            if (repath <= 0f || pathIndex >= path.Count)
            {
                repath = 0.5f;
                path.Clear();
                pathIndex = 0;
                if (!WorldGrid.Instance.FindPath(transform.position, target, path, 2000)) { path.Clear(); path.Add(target); }
            }
            if (pathIndex >= path.Count) return;
            var wp = path[pathIndex];
            var to = Factory.Flat(wp - transform.position);
            float step = ChaseSpeed * dt;
            if (to.magnitude <= step) { transform.position = new Vector3(wp.x, 0f, wp.z); pathIndex++; }
            else transform.position += to.normalized * step;
            Factory.Face(transform, wp, dt * 12f);
            view?.UpdateLocomotion(ChaseSpeed);
        }

        void Chatter()
        {
            if (state != State.Raking || Time.time < chatterAt) return;
            chatterAt = Time.time + 25f + Random.Range(0f, 35f);
            var p = Player.I;
            if (p == null || Factory.FlatDistance(p.transform.position, transform.position) > 14f) return;
            Speech.Say(transform, 2.4f, idle[Random.Range(0, idle.Length)].Replace("{name}", p.DisplayName));
        }
    }
}
