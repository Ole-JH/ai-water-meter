using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    public enum EnemyShape { Humanoid, Beast, Golem }

    /// <summary>
    /// Client-side look of a monster type. Gameplay stats (health, damage, AI) live on the server
    /// in server/content.js; the two are matched by <see cref="Name"/>.
    /// </summary>
    public class EnemyDef
    {
        public string Name;
        public EnemyShape Shape;
        public Color Color, Secondary;
        public float Scale = 1f;
        public bool Ranged, Boss, Robe, Weapon = true;
        public Color ProjectileColor = new Color(0.4f, 1f, 0.3f);

        public static readonly EnemyDef[] All =
        {
            new EnemyDef { Name = "Dire Wolf", Shape = EnemyShape.Beast, Color = new Color(0.42f, 0.42f, 0.46f), Secondary = new Color(0.3f, 0.3f, 0.33f), Scale = 0.9f },
            new EnemyDef { Name = "Goblin", Color = new Color(0.4f, 0.65f, 0.25f), Secondary = new Color(0.45f, 0.3f, 0.15f), Scale = 0.75f },
            new EnemyDef { Name = "Goblin Shaman", Color = new Color(0.4f, 0.65f, 0.25f), Secondary = new Color(0.45f, 0.2f, 0.5f), Scale = 0.75f,
                Ranged = true, Robe = true, Weapon = false, ProjectileColor = new Color(0.5f, 1f, 0.2f) },
            new EnemyDef { Name = "Goblin Warchief", Color = new Color(0.35f, 0.55f, 0.2f), Secondary = new Color(0.55f, 0.15f, 0.1f), Scale = 1.35f, Boss = true },
            new EnemyDef { Name = "Bandit", Color = new Color(0.85f, 0.7f, 0.55f), Secondary = new Color(0.55f, 0.15f, 0.12f) },
            new EnemyDef { Name = "Skeleton", Color = new Color(0.9f, 0.88f, 0.8f), Secondary = new Color(0.75f, 0.73f, 0.66f) },
            new EnemyDef { Name = "Skeleton Archer", Color = new Color(0.9f, 0.88f, 0.8f), Secondary = new Color(0.45f, 0.4f, 0.35f),
                Ranged = true, Weapon = false, ProjectileColor = new Color(0.9f, 0.9f, 0.7f) },
            new EnemyDef { Name = "Zombie", Color = new Color(0.45f, 0.55f, 0.4f), Secondary = new Color(0.3f, 0.28f, 0.25f), Scale = 1.05f, Weapon = false },
            new EnemyDef { Name = "Crypt Lord", Color = new Color(0.8f, 0.75f, 0.68f), Secondary = new Color(0.45f, 0.08f, 0.08f), Scale = 1.8f, Boss = true },
            new EnemyDef { Name = "Rock Golem", Shape = EnemyShape.Golem, Color = new Color(0.5f, 0.48f, 0.45f), Secondary = new Color(0.35f, 0.33f, 0.3f), Scale = 1.3f },
            // dungeon bosses
            new EnemyDef { Name = "Bandit Lord", Color = new Color(0.6f, 0.2f, 0.18f), Secondary = new Color(0.25f, 0.2f, 0.18f), Scale = 1.4f, Boss = true },
            new EnemyDef { Name = "Goblin King", Color = new Color(0.4f, 0.6f, 0.2f), Secondary = new Color(0.85f, 0.7f, 0.2f), Scale = 1.6f, Boss = true },
            new EnemyDef { Name = "Stone Colossus", Shape = EnemyShape.Golem, Color = new Color(0.45f, 0.5f, 0.62f), Secondary = new Color(0.3f, 0.35f, 0.45f), Scale = 2.1f, Boss = true },
            new EnemyDef { Name = "Lich King", Color = new Color(0.55f, 0.75f, 0.85f), Secondary = new Color(0.3f, 0.12f, 0.45f), Scale = 1.8f,
                Ranged = true, Boss = true, Robe = true, ProjectileColor = new Color(0.4f, 0.9f, 1f) },
            // Frostpeak Wilds
            new EnemyDef { Name = "Frost Wolf", Shape = EnemyShape.Beast, Color = new Color(0.85f, 0.88f, 0.95f), Secondary = new Color(0.65f, 0.7f, 0.8f), Scale = 0.95f },
            new EnemyDef { Name = "Ice Wraith", Color = new Color(0.65f, 0.85f, 1f), Secondary = new Color(0.3f, 0.45f, 0.7f), Ranged = true, Robe = true, Weapon = false,
                ProjectileColor = new Color(0.6f, 0.9f, 1f) },
            new EnemyDef { Name = "Frost Giant", Color = new Color(0.6f, 0.7f, 0.85f), Secondary = new Color(0.4f, 0.45f, 0.6f), Scale = 1.5f },
            new EnemyDef { Name = "Jarl Frostborn", Color = new Color(0.65f, 0.8f, 1f), Secondary = new Color(0.3f, 0.4f, 0.7f), Scale = 1.9f, Boss = true },
            // Sunscar Badlands
            new EnemyDef { Name = "Desert Raider", Color = new Color(0.9f, 0.75f, 0.55f), Secondary = new Color(0.75f, 0.45f, 0.2f) },
            new EnemyDef { Name = "Raider Marksman", Color = new Color(0.9f, 0.75f, 0.55f), Secondary = new Color(0.6f, 0.4f, 0.2f), Ranged = true, Weapon = false,
                ProjectileColor = new Color(0.95f, 0.85f, 0.6f) },
            new EnemyDef { Name = "Sand Golem", Shape = EnemyShape.Golem, Color = new Color(0.78f, 0.66f, 0.45f), Secondary = new Color(0.6f, 0.5f, 0.32f), Scale = 1.3f },
            new EnemyDef { Name = "Raider Warlord", Color = new Color(0.85f, 0.6f, 0.4f), Secondary = new Color(0.6f, 0.2f, 0.1f), Scale = 1.5f, Boss = true },
            // Ashen Reach
            new EnemyDef { Name = "Ash Ghoul", Color = new Color(0.45f, 0.43f, 0.42f), Secondary = new Color(0.28f, 0.25f, 0.24f), Scale = 1.05f, Weapon = false },
            new EnemyDef { Name = "Ember Skeleton", Color = new Color(0.8f, 0.5f, 0.38f), Secondary = new Color(0.4f, 0.15f, 0.08f) },
            new EnemyDef { Name = "Ash Wraith", Color = new Color(0.85f, 0.55f, 0.4f), Secondary = new Color(0.35f, 0.15f, 0.1f), Ranged = true, Robe = true, Weapon = false,
                ProjectileColor = new Color(1f, 0.55f, 0.2f) },
            new EnemyDef { Name = "Cinder Golem", Shape = EnemyShape.Golem, Color = new Color(0.35f, 0.25f, 0.22f), Secondary = new Color(0.7f, 0.3f, 0.1f), Scale = 1.35f },
            new EnemyDef { Name = "The Ashen King", Color = new Color(0.75f, 0.45f, 0.35f), Secondary = new Color(0.5f, 0.12f, 0.05f), Scale = 2f, Boss = true },
            // the outer lands' dungeon bosses
            new EnemyDef { Name = "The Frost Witch", Color = new Color(0.75f, 0.88f, 1f), Secondary = new Color(0.3f, 0.45f, 0.75f), Scale = 1.7f, Boss = true,
                Ranged = true, Robe = true, Weapon = false, ProjectileColor = new Color(0.55f, 0.9f, 1f) },
            new EnemyDef { Name = "The Sand Colossus", Shape = EnemyShape.Golem, Color = new Color(0.82f, 0.7f, 0.48f), Secondary = new Color(0.65f, 0.52f, 0.32f), Scale = 2.1f, Boss = true },
            new EnemyDef { Name = "The Cinder Lord", Color = new Color(0.55f, 0.3f, 0.22f), Secondary = new Color(0.8f, 0.3f, 0.1f), Scale = 1.9f, Boss = true },
            // world bosses (WorldBoss.cs)
            new EnemyDef { Name = "Old Bramblehide", Shape = EnemyShape.Beast, Color = new Color(0.35f, 0.3f, 0.22f), Secondary = new Color(0.25f, 0.4f, 0.15f), Scale = 2.4f, Boss = true },
            new EnemyDef { Name = "Hrimgar the Mountain", Color = new Color(0.7f, 0.85f, 1f), Secondary = new Color(0.3f, 0.4f, 0.65f), Scale = 2.8f, Boss = true },
            new EnemyDef { Name = "Gorvash the Dune Reaver", Color = new Color(0.9f, 0.7f, 0.45f), Secondary = new Color(0.55f, 0.2f, 0.1f), Scale = 2.4f, Boss = true },
            new EnemyDef { Name = "The Pyre Colossus", Shape = EnemyShape.Golem, Color = new Color(0.4f, 0.22f, 0.18f), Secondary = new Color(1f, 0.4f, 0.1f), Scale = 2.9f, Boss = true },
        };

        public static EnemyDef ByName(string name)
        {
            foreach (var d in All) if (d.Name == name) return d;
            return All[0];
        }
    }

    /// <summary>
    /// Network proxy for a server-simulated monster. Position, health and state come from server
    /// snapshots; local hits are predicted and reported to the server, which decides deaths.
    /// </summary>
    public class Enemy : Combatant
    {
        public static readonly Dictionary<int, Enemy> ById = new Dictionary<int, Enemy>();

        public int NetId;
        public EnemyDef Def;
        public float LastSeen;
        public bool Slowed, Stunned;
        public bool Elite { get; private set; }
        public string[] Affixes { get; private set; } = new string[0];
        public bool Shielded { get; private set; }
        Light eliteLight;
        float nextShieldPulse;

        /// <summary>Aura color for an elite, from its first affix.</summary>
        public static Color AffixColor(string affix)
        {
            switch (affix)
            {
                case "Fast": return new Color(1f, 0.9f, 0.35f);
                case "Vampiric": return new Color(0.9f, 0.1f, 0.15f);
                case "Fire Enchanted": return new Color(1f, 0.45f, 0.1f);
                case "Teleporter": return new Color(0.75f, 0.35f, 1f);
                case "Shielding": return new Color(0.4f, 0.85f, 1f);
                case "Mighty": return new Color(1f, 0.3f, 0.1f);
                default: return new Color(0.4f, 1f, 0.5f); // Extra Health
            }
        }

        public static readonly Color ChampionColor = new Color(0.45f, 0.65f, 1f);

        void MakeElite(NetMonster m)
        {
            Elite = true;
            DisplayName = m.el;
            Affixes = string.IsNullOrEmpty(m.af) ? new string[0] : m.af.Split(',');
            var c = Affixes.Length > 0 ? AffixColor(Affixes[0]) : ChampionColor;
            if (model != null) model.localScale *= 1.25f;
            Height *= 1.25f;
            Radius *= 1.2f;
            var lightGo = new GameObject("EliteAura");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            eliteLight = lightGo.AddComponent<Light>();
            eliteLight.type = LightType.Point;
            eliteLight.color = c;
            eliteLight.range = 5f;
            eliteLight.intensity = 1.6f;
            SpellFx.Emit(new SpellFx.P
            {
                Rate = 22, Duration = 100000f, Life = new Vector2(0.8f, 1.4f), Speed = new Vector2(0.05f, 0.3f),
                Size = new Vector2(0.08f, 0.18f), Start = new Color(c.r, c.g, c.b, 0.9f), End = new Color(c.r, c.g, c.b, 0f),
                Shape = ParticleSystemShapeType.Circle, Radius = 0.9f, Velocity = new Vector3(0f, 1.1f, 0f),
            }, transform.position, transform);
        }

        Vector3 netPos;
        float armor, attackAnim = -1f, deathTime, curSpeed, walkPhase;
        bool dying;
        Vector3 lastHitDir, fling;
        bool lastHitBig;
        float lastHitMine = -10f;
        FrostBite frost;
        float nextDrip;
        HumanoidModel humanoid;
        CharacterView view;
        Transform model;
        Vector3 lastPos;
        float moveSpeed;
        Transform[] legs;

        public override float Armor => armor;

        public static Enemy Spawn(NetMonster m)
        {
            var def = EnemyDef.ByName(m.n);
            var go = new GameObject(def.Name + " #" + m.id);
            go.transform.position = new Vector3(m.x, 0, m.z);
            go.transform.rotation = Quaternion.Euler(0, m.ry, 0);
            var e = go.AddComponent<Enemy>();
            e.NetId = m.id;
            e.Def = def;
            e.DisplayName = def.Name;
            e.Faction = Faction.Enemy;
            e.Radius = 0.45f * def.Scale * (def.Shape == EnemyShape.Golem ? 1.3f : 1f);
            e.Height = 2f * def.Scale;
            e.BuildModel();
            if (!string.IsNullOrEmpty(m.el)) e.MakeElite(m);
            if (WorldBoss.Is(def.Name)) BossPresence.Attach(e, e.model, e.Height);
            if (Rift.IsGuardian(def.Name)) RiftFx.GuardianArrives(e, e.model);
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, e.Height * 0.5f, 0);
            col.height = e.Height;
            col.radius = Mathf.Max(0.45f, e.Radius);
            e.ApplySnapshot(m, true);
            ById[m.id] = e;
            return e;
        }

        public void ApplySnapshot(NetMonster m, bool snap = false)
        {
            LastSeen = Time.time;
            if (!string.IsNullOrEmpty(m.n)) // full entry (partial ones only carry what changes: position, health, flags)
            {
                Level = m.l;
                armor = m.ar;
                MaxHealth = m.mhp;
            }
            // Keep our locally-predicted damage for a moment so health bars don't flicker back up.
            Health = Time.time - LastDamagedTime < 0.4f ? Mathf.Min(Health, m.hp) : m.hp;
            Slowed = m.sl;
            if (m.st && !Stunned && !IsDead) GameUI.Float(transform.position + Vector3.up * (Height + 0.6f), "Stunned", new Color(1f, 0.9f, 0.4f), 0.8f);
            Stunned = m.st;
            if (Stunned && !IsDead) StunStars.Show(this);
            if (Elite && m.sh && !Shielded) Sfx.Play("frost_cast", transform.position, 0.5f, 0.1f);
            Shielded = m.sh;
            netPos = new Vector3(m.x, 0, m.z);
            if (snap) transform.position = netPos;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (ById.TryGetValue(NetId, out var e) && e == this) ById.Remove(NetId);
        }

        void BuildModel()
        {
            view = CharacterView.Create(transform, CharacterLook.ForMonster(Def.Name));
            if (view != null)
            {
                model = view.Root.transform;
                Height = view.Height;
                return;
            }
            switch (Def.Shape)
            {
                case EnemyShape.Humanoid:
                    humanoid = HumanoidModel.Build(transform, Def.Scale, Def.Color, Def.Secondary,
                        Def.Robe ? Def.Secondary : Factory.Shade(Def.Secondary, 0.7f),
                        Def.Boss ? new Color(0.6f, 0.9f, 1f) : new Color(0.55f, 0.5f, 0.45f),
                        Def.Weapon, Def.Robe, true,
                        Def.Name.Contains("Skeleton") || Def.Name.Contains("Lich") ? new Color(0.3f, 0.9f, 1f) : (Color?)null);
                    model = humanoid.Root;
                    if (Def.Boss)
                    {
                        humanoid.Helm.gameObject.SetActive(true);
                        humanoid.HelmRenderer.sharedMaterial = Def.Name == "Lich King"
                            ? Mat.Glow(new Color(0.3f, 0.8f, 1f))
                            : Mat.Get(new Color(0.5f, 0.3f, 0.15f));
                    }
                    break;

                case EnemyShape.Beast:
                {
                    model = Factory.Empty("Model", transform, Vector3.zero);
                    model.localScale = Vector3.one * Def.Scale;
                    Factory.Prim(PrimitiveType.Cube, model, new Vector3(0, 0.75f, 0), new Vector3(0.6f, 0.55f, 1.3f), Def.Color);
                    var head = Factory.Prim(PrimitiveType.Cube, model, new Vector3(0, 0.95f, 0.8f), new Vector3(0.42f, 0.42f, 0.5f), Def.Color).transform;
                    Factory.Prim(PrimitiveType.Cube, head, new Vector3(0, -0.2f, 0.7f), new Vector3(0.6f, 0.45f, 0.7f), Def.Secondary);
                    Factory.Prim(PrimitiveType.Cube, head, new Vector3(-0.3f, 0.6f, -0.1f), new Vector3(0.2f, 0.5f, 0.2f), Def.Secondary);
                    Factory.Prim(PrimitiveType.Cube, head, new Vector3(0.3f, 0.6f, -0.1f), new Vector3(0.2f, 0.5f, 0.2f), Def.Secondary);
                    var eye = new Color(1f, 0.8f, 0.1f);
                    Factory.Prim(PrimitiveType.Sphere, head, new Vector3(-0.28f, 0.15f, 0.48f), Vector3.one * 0.2f, eye, false, Mat.Glow(eye));
                    Factory.Prim(PrimitiveType.Sphere, head, new Vector3(0.28f, 0.15f, 0.48f), Vector3.one * 0.2f, eye, false, Mat.Glow(eye));
                    Factory.Prim(PrimitiveType.Cube, model, new Vector3(0, 0.95f, -0.8f), new Vector3(0.14f, 0.14f, 0.6f), Def.Secondary)
                        .transform.localRotation = Quaternion.Euler(-30, 0, 0);
                    legs = new Transform[4];
                    Vector3[] lp = { new Vector3(-0.2f, 0.55f, 0.45f), new Vector3(0.2f, 0.55f, 0.45f), new Vector3(-0.2f, 0.55f, -0.45f), new Vector3(0.2f, 0.55f, -0.45f) };
                    for (int i = 0; i < 4; i++)
                    {
                        legs[i] = Factory.Empty("Leg", model, lp[i]);
                        Factory.Prim(PrimitiveType.Cube, legs[i], new Vector3(0, -0.28f, 0), new Vector3(0.15f, 0.56f, 0.15f), Def.Secondary);
                    }
                    Height = 1.3f * Def.Scale;
                    break;
                }

                case EnemyShape.Golem:
                {
                    model = Factory.Empty("Model", transform, Vector3.zero);
                    model.localScale = Vector3.one * Def.Scale;
                    Factory.Prim(PrimitiveType.Cube, model, new Vector3(0, 1.2f, 0), new Vector3(1.1f, 1.0f, 0.8f), Def.Color)
                        .transform.localRotation = Quaternion.Euler(0, 0, 5);
                    var head = Factory.Prim(PrimitiveType.Cube, model, new Vector3(0, 1.95f, 0.1f), new Vector3(0.5f, 0.45f, 0.5f), Def.Secondary).transform;
                    var eye = new Color(1f, 0.5f, 0.1f);
                    Factory.Prim(PrimitiveType.Cube, head, new Vector3(0, 0.05f, 0.5f), new Vector3(0.7f, 0.15f, 0.1f), eye, false, Mat.Glow(eye));
                    legs = new Transform[4];
                    legs[0] = Factory.Empty("LArm", model, new Vector3(-0.75f, 1.55f, 0));
                    legs[1] = Factory.Empty("RArm", model, new Vector3(0.75f, 1.55f, 0));
                    Factory.Prim(PrimitiveType.Cube, legs[0], new Vector3(0, -0.5f, 0), new Vector3(0.4f, 1.1f, 0.4f), Def.Secondary);
                    Factory.Prim(PrimitiveType.Cube, legs[1], new Vector3(0, -0.5f, 0), new Vector3(0.4f, 1.1f, 0.4f), Def.Secondary);
                    legs[2] = Factory.Empty("LLeg", model, new Vector3(-0.3f, 0.7f, 0));
                    legs[3] = Factory.Empty("RLeg", model, new Vector3(0.3f, 0.7f, 0));
                    Factory.Prim(PrimitiveType.Cube, legs[2], new Vector3(0, -0.35f, 0), new Vector3(0.38f, 0.7f, 0.38f), Def.Color);
                    Factory.Prim(PrimitiveType.Cube, legs[3], new Vector3(0, -0.35f, 0), new Vector3(0.38f, 0.7f, 0.38f), Def.Color);
                    var crystal = new Color(0.4f, 0.7f, 1f);
                    Factory.Prim(PrimitiveType.Cube, model, new Vector3(0.3f, 1.8f, -0.35f), new Vector3(0.2f, 0.45f, 0.2f), crystal, false, Mat.Glow(crystal))
                        .transform.localRotation = Quaternion.Euler(20, 0, 25);
                    Factory.Prim(PrimitiveType.Cube, model, new Vector3(-0.35f, 1.75f, -0.3f), new Vector3(0.15f, 0.35f, 0.15f), crystal, false, Mat.Glow(crystal))
                        .transform.localRotation = Quaternion.Euler(-15, 0, -30);
                    Height = 2.3f * Def.Scale;
                    break;
                }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dying)
            {
                if (fling.sqrMagnitude > 0.01f)
                {
                    var next = transform.position + fling * dt;
                    if (WorldGrid.Instance == null || WorldGrid.Instance.IsWalkable(next)) transform.position = next;
                    else fling = Vector3.zero;
                    fling = Vector3.MoveTowards(fling, Vector3.zero, 18f * dt);
                }
                if (frost != null) frost.Active = false;
                // corpses lie in their blood for a while before sinking away
                float linger = WorldBoss.Is(Def.Name) ? BossPresence.Linger : 7f; // a world boss lies where it fell a while
                if (Time.time - deathTime > linger) transform.position += Vector3.down * dt * 0.8f;
                if (Time.time - deathTime > linger + 3f) Destroy(gameObject);
                return;
            }

            if (Slowed || frost != null)
            {
                if (frost == null) frost = FrostBite.On(gameObject, Radius + 0.25f);
                frost.Active = Slowed;
            }
            if (Shielded && Time.time >= nextShieldPulse)
            {
                nextShieldPulse = Time.time + 0.45f;
                SpellFx.Ring(transform.position, new Color(0.45f, 0.85f, 1f), 1.6f, 0.45f);
            }
            if (eliteLight != null) eliteLight.intensity = Shielded ? 3f : 1.4f + Mathf.Sin(Time.time * 3f) * 0.3f;

            // Badly wounded and on the move: a trail of drops.
            if (Health < MaxHealth * 0.35f && Health > 0f && Time.time >= nextDrip && Factory.FlatDistance(netPos, transform.position) > 0.05f)
            {
                nextDrip = Time.time + Random.Range(0.35f, 0.9f);
                Gore.Drip(transform.position, Gore.KindOf(Def.Name));
            }

            // Smoothly chase the latest server position.
            Vector3 to = Factory.Flat(netPos - transform.position);
            float dist = to.magnitude;
            if (dist > 6f) transform.position = netPos; // teleport / respawn
            else if (dist > 0.02f)
            {
                float step = Mathf.Max(dist * 8f, 2f) * dt;
                transform.position = Vector3.MoveTowards(transform.position, netPos, step);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), dt * 10f);
            }
            curSpeed = Mathf.Lerp(curSpeed, dist > 0.05f ? 1f : 0f, dt * 8f);
            float moved = Factory.FlatDistance(transform.position, lastPos);
            if (dt > 0f && moved < 3f) moveSpeed = Mathf.Lerp(moveSpeed, moved / dt, dt * 10f); // ignore teleports
            lastPos = transform.position;

            // Face whoever we are hitting
            if (attackAnim >= 0f && Player.I != null && FacingTarget != null)
                Factory.Face(transform, FacingTarget.Value, dt * 10f);

            Animate(dt);
        }

        Vector3? FacingTarget;
        float limpPhase, spotT = -1f, nextTrail;
        bool wasLimping;

        /// <summary>It has seen someone (server "spot"): a "!" over it, a start, and its cry.</summary>
        public void Spotted()
        {
            if (IsDead) return;
            if (!Def.Boss && view != null) spotT = 0f; // (bosses climb, rise and tear in through their model: leave it be)
            GameUI.Float(transform.position + Vector3.up * (Height + 0.5f), "!", Elite || Def.Boss ? new Color(1f, 0.5f, 0.2f) : new Color(1f, 0.85f, 0.25f), 1.7f, true);
            var cry = Voice(Def, "attack") ?? "swing";
            Sfx.Play(Def.Boss || Height > 2.6f ? "roar" : cry, Center, 0.45f, 0.15f, 30f);
        }

        public bool HasAffix(string a) => System.Array.IndexOf(Affixes, a) >= 0;

        float stride;

        /// <summary>The big ones (golems, giants, bosses) are heard walking: a thud with each stride, the ground shaking under a giant.</summary>
        void HeavySteps(float dt)
        {
            if (Height < 2.6f || moveSpeed < 0.6f) { stride = 0f; return; }
            stride += moveSpeed * dt;
            float every = Mathf.Max(1.4f, Height * 0.55f);
            if (stride < every) return;
            stride = 0f;
            var p = Player.I;
            float d = p != null ? Factory.FlatDistance(p.transform.position, transform.position) : 99f;
            if (d > 30f) return;
            Sfx.Play(Height > 4f ? "boom" : "hit_stone", transform.position, Mathf.Clamp01(Height / 6f) * 0.5f, 0.15f, 30f);
            if (Height > 4f && d < 14f) CameraRig.Shake(0.04f * (1f - d / 14f) * Height / 4f);
            if (SpellFx.Ready && Height > 3.2f) SpellFx.Dust(transform.position, Height * 0.12f);
        }

        float printStride;
        bool printLeft;
        int ghost = -1;        // a wraith (no prints): worked out once
        internal StunStars Stars;

        /// <summary>Prints in sand, snow and mud (paws for beasts, broad feet for golems and giants), and a puff of
        /// dust on dry ground now and then. Ghosts and wraiths leave nothing.</summary>
        void Prints(float dt)
        {
            if (ghost < 0) ghost = Def.Name.Contains("Wraith") ? 1 : 0;
            if (moveSpeed < 0.5f || ghost == 1) { printStride = 0f; return; }
            float stepLen = Mathf.Clamp(Height * 0.55f, 0.7f, 2.4f);
            printStride += moveSpeed * dt;
            if (printStride < stepLen) return;
            printStride = 0f;
            printLeft = !printLeft;
            float scale = Mathf.Clamp(Height / 1.8f, 0.6f, 3f);
            Footprints.Step(transform.position, transform.forward, printLeft, Def.Shape == EnemyShape.Beast, scale);
            if (!printLeft || !SpellFx.Ready) return; // dust every other step
            var p = Player.I;
            if (p == null || Factory.FlatDistance(p.transform.position, transform.position) > 22f) return;
            var dust = Footprints.DustAt(transform.position);
            if (dust.HasValue)
            {
                var c = dust.Value;
                SpellFx.Emit(new SpellFx.P { Burst = 2 + Mathf.RoundToInt(scale), Duration = 0.1f, Life = new Vector2(0.4f, 0.7f), Speed = new Vector2(0.2f, 0.6f),
                    Size = new Vector2(0.12f, 0.22f) * scale, Start = new Color(c.r, c.g, c.b, 0.3f), End = new Color(c.r, c.g, c.b, 0f), Smoke = true, Grow = true,
                    Radius = 0.12f * scale, Max = 8 }, transform.position + Vector3.up * 0.05f);
            }
        }

        /// <summary>What an elite's affixes leave behind as it moves: fire underfoot, dust in a fast one's wake.</summary>
        void AffixTrails(float dt)
        {
            if (!Elite || moveSpeed < 0.5f || Time.time < nextTrail) return;
            if (HasAffix("Fire Enchanted"))
            {
                nextTrail = Time.time + 0.7f;
                FirePatch.Drop(transform.position, 0.55f, 4f);
            }
            else if (HasAffix("Fast"))
            {
                nextTrail = Time.time + 0.35f;
                if (SpellFx.Ready) SpellFx.Dust(transform.position, 0.45f);
            }
        }

        /// <summary>Which sound a monster makes: when attacking, getting hit or dying.</summary>
        static string Voice(EnemyDef d, string what)
        {
            switch (d.Name)
            {
                case "Dire Wolf": return what == "attack" ? "wolf_attack" : what == "die" ? "goblin_die" : "hit_flesh";
                case "Goblin":
                case "Goblin Shaman": return what == "attack" ? "goblin" : what == "die" ? "goblin_die" : "hit_flesh";
                case "Goblin Warchief": return what == "attack" ? "brute" : what == "die" ? "roar" : "hit_flesh";
                case "Bandit": return what == "attack" ? null : what == "die" ? "goblin_die" : "hit_flesh";
                case "Skeleton":
                case "Skeleton Archer": return what == "attack" ? null : what == "die" ? "rubble" : "hit_bone";
                case "Zombie": return what == "attack" ? "undead" : what == "die" ? "undead_die" : "hit_flesh";
                case "Rock Golem": return what == "attack" ? "roar" : what == "die" ? "rubble" : "hit_stone";
                case "Lich King": return what == "attack" ? "undead" : what == "die" ? "scream" : "hit_bone";
                case "Crypt Lord": return what == "attack" ? "roar" : what == "die" ? "scream" : "hit_bone";
                case "Bandit Lord": return what == "attack" ? "swing_heavy" : what == "die" ? "scream" : "hit_flesh";
                case "Goblin King": return what == "attack" ? "brute" : what == "die" ? "roar" : "hit_flesh";
                case "Stone Colossus": return what == "attack" ? "boom" : what == "die" ? "rubble" : "hit_stone";
                case "Frost Wolf": return Voice(EnemyDef.ByName("Dire Wolf"), what);
                case "Ice Wraith": case "Ash Wraith": return what == "attack" ? "undead" : what == "die" ? "rubble" : "hit_bone";
                case "Ember Skeleton": return Voice(EnemyDef.ByName("Skeleton"), what);
                case "Ash Ghoul": return Voice(EnemyDef.ByName("Zombie"), what);
                case "Desert Raider": case "Raider Marksman": return Voice(EnemyDef.ByName("Bandit"), what);
                case "Sand Golem": case "Cinder Golem": return Voice(EnemyDef.ByName("Rock Golem"), what);
                case "Frost Giant": case "Jarl Frostborn": return what == "attack" ? "brute" : what == "die" ? "roar" : "hit_flesh";
                case "Raider Warlord": return Voice(EnemyDef.ByName("Bandit Lord"), what);
                case "The Ashen King": return Voice(EnemyDef.ByName("Crypt Lord"), what);
                case "The Frost Witch": return Voice(EnemyDef.ByName("Lich King"), what);
                case "The Sand Colossus": return Voice(EnemyDef.ByName("Stone Colossus"), what);
                case "The Cinder Lord": return Voice(EnemyDef.ByName("Goblin King"), what);
                case "Old Bramblehide": return what == "attack" ? "wolf_attack" : what == "die" ? "wolf_howl" : "hit_flesh";
                case "Hrimgar the Mountain": return what == "attack" ? "brute" : what == "die" ? "roar" : "hit_flesh";
                case "Gorvash the Dune Reaver": return what == "attack" ? "swing_heavy" : what == "die" ? "roar" : "hit_armor";
                case "The Pyre Colossus": return what == "attack" ? "boom" : what == "die" ? "rubble" : "hit_stone";
                default: return what == "hit" ? "hit_flesh" : null;
            }
        }

        float nextVoice;

        public void PlayAttack(Vector3 targetPos)
        {
            attackAnim = 0f;
            FacingTarget = targetPos;
            var voice = Voice(Def, "attack");
            if (voice != null && Time.time > nextVoice && Random.value < 0.4f)
            {
                nextVoice = Time.time + 2.5f;
                Sfx.Play(voice, transform.position + Vector3.up, Def.Boss ? 0.9f : 0.5f, 0.12f);
            }
            if (!Def.Ranged) Sfx.Play("swing", transform.position + Vector3.up, 0.35f, 0.15f);
            else if (Def.Name == "Skeleton Archer") Sfx.Play("swing", transform.position + Vector3.up, 0.3f, 0.05f);
            else Sfx.Play("fire_cast", transform.position + Vector3.up, 0.4f, 0.2f);
            if (view != null)
            {
                if (!Def.Ranged) view.Attack(0.9f);
                else if (Def.Name == "Skeleton Archer") view.Shoot();
                else view.Cast();
            }
        }

        void Animate(float dt)
        {
            float speed01 = curSpeed;
            float atk = -1f;
            if (attackAnim >= 0f)
            {
                attackAnim += dt * 2.8f;
                atk = attackAnim;
                if (attackAnim >= 1f) { attackAnim = -1f; FacingTarget = null; }
            }

            if (view != null)
            {
                bool limping = Health < MaxHealth * 0.3f && moveSpeed > 0.5f && !Def.Boss;
                view.UpdateLocomotion(limping ? moveSpeed * 0.85f : moveSpeed);
                // badly hurt: it limps, lurching to one side with every other step; a fresh alarm makes it start
                limpPhase += dt * moveSpeed * 2.2f;
                if (limping || wasLimping)
                {
                    model.localRotation = Quaternion.Euler(0f, 0f, limping ? Mathf.Max(0f, Mathf.Sin(limpPhase)) * 8f : 0f);
                    wasLimping = limping;
                }
                if (spotT >= 0f) // (only ever started for monsters that don't move their model themselves: see Spotted)
                {
                    spotT += dt / 0.35f;
                    float hop = spotT < 1f ? Mathf.Sin(spotT * Mathf.PI) * 0.25f : 0f;
                    model.localPosition = new Vector3(model.localPosition.x, hop, model.localPosition.z);
                    if (spotT >= 1f) spotT = -1f;
                }
                AffixTrails(dt);
                HeavySteps(dt);
                Prints(dt);
                return;
            }

            if (humanoid != null)
            {
                if (Def.Ranged && atk >= 0f) { humanoid.Animate(speed01, -1f, dt); humanoid.CastPose(atk); }
                else humanoid.Animate(speed01, atk, dt);
                return;
            }

            walkPhase += dt * 12f * Mathf.Max(0.1f, speed01);
            float s = Mathf.Sin(walkPhase) * 35f * speed01;
            if (Def.Shape == EnemyShape.Beast && legs != null)
            {
                legs[0].localRotation = legs[3].localRotation = Quaternion.Euler(s, 0, 0);
                legs[1].localRotation = legs[2].localRotation = Quaternion.Euler(-s, 0, 0);
                model.localPosition = atk >= 0f ? new Vector3(0, Mathf.Sin(atk * Mathf.PI) * 0.3f, Mathf.Sin(atk * Mathf.PI) * 0.6f) : Vector3.zero;
            }
            else if (Def.Shape == EnemyShape.Golem && legs != null)
            {
                legs[2].localRotation = Quaternion.Euler(s * 0.6f, 0, 0);
                legs[3].localRotation = Quaternion.Euler(-s * 0.6f, 0, 0);
                float arm = atk >= 0f ? Mathf.Sin(atk * Mathf.PI) * -140f : -s * 0.4f;
                legs[0].localRotation = Quaternion.Euler(arm, 0, 0);
                legs[1].localRotation = Quaternion.Euler(arm, 0, 0);
            }
        }

        /// <summary>Predict the hit locally and report it to the server (which owns the real health).</summary>
        public override void TakeDamage(float amount, Combatant source, bool crit = false)
        {
            if (IsDead || dying) return;
            if (Shielded)
            {
                GameUI.Float(transform.position + Vector3.up * (Height + 0.2f), "Immune", new Color(0.6f, 0.9f, 1f), 0.9f);
                return;
            }
            float mitigated = amount * 100f / (100f + Mathf.Max(0f, Armor));
            int dmg = Mathf.Max(1, Mathf.RoundToInt(mitigated));
            NoteHit();
            Health = Mathf.Max(0f, Health - dmg);
            LastDamagedTime = Time.time;
            GameUI.Damage(transform.position + Vector3.up * (Height + 0.2f), crit ? dmg + "!" : dmg.ToString(),
                crit ? new Color(1f, 0.85f, 0.2f) : Color.white, DamageSize(dmg, crit), crit, crit);
            var gore = Gore.KindOf(Def.Name);
            bool bones = gore == Gore.Kind.Bone, stone = gore == Gore.Kind.Stone;
            SpellFx.Hit(Center, bones ? new Color(0.9f, 0.88f, 0.8f) : stone ? new Color(0.6f, 0.55f, 0.5f) : new Color(0.55f, 0.03f, 0.03f), !bones && !stone, crit ? 16 : 9);
            // blood flies away from whoever struck the blow
            lastHitDir = source != null ? transform.position - source.transform.position : transform.forward * -1f;
            lastHitBig = crit || dmg > MaxHealth * 0.35f;
            Gore.Hit(Center, lastHitDir, gore, crit ? 1f : Mathf.Clamp01(dmg / Mathf.Max(1f, MaxHealth) * 3f));
            Sfx.Play(crit ? "hit_heavy" : Voice(Def, "hit"), Center, crit ? 0.7f : 0.5f, 0.12f);
            view?.Hit();
            // the body flashes and rocks back; a crit of ours freezes the moment and kicks the camera
            HitFlash.On(gameObject, model).Hit(lastHitDir, crit ? 1f : Mathf.Clamp01(dmg / Mathf.Max(1f, MaxHealth) * 4f));
            if (source is Player)
            {
                lastHitMine = Time.time;
                if (crit) { HitFx.Stop(0.05f); CameraRig.Shake(0.07f); }
            }
            if (source is Player) NetClient.I?.SendHit(NetId, dmg, crit);
        }

        public void Slow(float duration)
        {
            if (!IsDead) NetClient.I?.SendSlow(NetId, duration);
        }

        public void Stun(float duration)
        {
            if (!IsDead) NetClient.I?.SendStun(NetId, duration);
        }

        /// <summary>Server says this monster died.</summary>
        public void NetDie()
        {
            if (dying) return;
            IsDead = true;
            dying = true;
            deathTime = Time.time;
            Health = 0;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            if (view != null) view.Die();
            else if (model != null) model.localRotation = Quaternion.Euler(-80f, 0, 0);
            var deathVoice = Voice(Def, "die");
            if (deathVoice != null) Sfx.Play(deathVoice, Center, Def.Boss ? 1f : 0.6f, 0.1f, Def.Boss ? 80f : 40f);
            if (Def.Boss) Sfx.Play2D("gong", 0.7f);
            bool mine = Time.time - lastHitMine < 0.8f;
            if (Def.Boss) HitFx.Stop(0.9f, 0.25f);          // a boss falls in slow motion
            else if (mine) HitFx.Stop(lastHitBig ? 0.07f : 0.035f); // a killing blow of ours lands with a jolt
            // a big killing blow throws the body back
            if (lastHitBig && !Def.Boss && Time.time - LastDamagedTime < 0.6f)
            {
                var d = Factory.Flat(lastHitDir);
                fling = (d.sqrMagnitude > 0.001f ? d.normalized : -transform.forward) * Mathf.Lerp(7f, 4f, Mathf.Clamp01(Height / 4f));
            }
            // an elite goes out in a burst of its aura's colour
            if (Elite && eliteLight != null)
            {
                SpellFx.Explosion(Center, eliteLight.color, 1.6f, false);
                Sfx.Play("explosion", Center, 0.5f, 0.1f, 40f);
            }
            if (Rift.Inside) RiftFx.Mote(Center, Elite ? 4 : 1); // its essence flies to the rift's orb
            if (SpellFx.Ready) SpellFx.Dust(transform.position, Def.Boss ? 2.5f : 1.2f);
            else FxPulse.Burst(Center, Factory.Shade(Def.Color, 0.6f), 0.8f, 0.3f);
            // A big killing blow (a crit, a heavy hit) also throws chunks.
            bool overkill = lastHitBig && Time.time - LastDamagedTime < 0.6f;
            Gore.Death(Center, lastHitDir, Gore.KindOf(Def.Name), Height / 1.8f, overkill, Def.Boss);
            if (!WorldBoss.Is(Def.Name) && model != null) StartCoroutine(DeathStyle());
        }

        /// <summary>
        /// How the body goes, by what it's made of: a skeleton falls apart into a heap of bones, a golem crumbles into
        /// rocks, a wraith dissolves into mist. Flesh stays and bleeds (the default).
        /// </summary>
        System.Collections.IEnumerator DeathStyle()
        {
            var kind = Gore.KindOf(Def.Name);
            bool wraith = Def.Name.EndsWith("Wraith");
            if (!wraith && kind != Gore.Kind.Bone && kind != Gore.Kind.Stone) yield break;
            float scale = Mathf.Clamp(Height / 2f, 0.6f, 3f);
            if (wraith)
            {
                // it thins and rises into the air, trailing mist
                var mist = new Color(0.75f, 0.85f, 0.95f);
                if (SpellFx.Ready)
                    SpellFx.Emit(new SpellFx.P { Rate = 40, Duration = 1.2f, Life = new Vector2(0.8f, 1.5f), Speed = new Vector2(0.2f, 0.6f), Size = new Vector2(0.3f, 0.6f) * scale,
                        Start = new Color(mist.r, mist.g, mist.b, 0.5f), End = new Color(mist.r, mist.g, mist.b, 0f), Velocity = Vector3.up * 1.2f, Smoke = true, Grow = true, Radius = 0.4f * scale }, Center);
                Sfx.Play("undead_die", Center, 0.5f, 0.1f, 30f);
                var start = model.localScale;
                for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                {
                    float k = t / 1.2f;
                    model.localScale = new Vector3(start.x * (1f - k * 0.7f), start.y * (1f + k * 0.3f), start.z * (1f - k * 0.7f));
                    model.localPosition += Vector3.up * Time.deltaTime * 0.8f;
                    yield return null;
                }
                model.gameObject.SetActive(false);
                yield break;
            }
            yield return new WaitForSeconds(kind == Gore.Kind.Bone ? 0.35f : 0.2f); // the start of the fall, then it goes to pieces
            if (this == null || model == null) yield break;
            model.gameObject.SetActive(false);
            var at = transform.position;
            if (kind == Gore.Kind.Bone)
            {
                var bone = new Color(0.86f, 0.83f, 0.74f);
                Sfx.Play("hit_bone", at + Vector3.up, 0.7f, 0.15f, 30f);
                Sfx.Play("rubble", at, 0.35f, 0.2f, 25f);
                // the skull rolls, the long bones scatter and settle in a heap
                Piece(PrimitiveType.Sphere, at + Vector3.up * 1.4f * scale, Vector3.one * 0.26f * scale, bone, 1.6f);
                for (int i = 0; i < 9; i++)
                    Piece(PrimitiveType.Cylinder, at + Vector3.up * Random.Range(0.3f, 1.3f) * scale, new Vector3(0.06f, Random.Range(0.18f, 0.32f), 0.06f) * scale, bone * Random.Range(0.85f, 1f), 1.1f);
                Piece(PrimitiveType.Cube, at + Vector3.up * 0.9f * scale, new Vector3(0.3f, 0.12f, 0.18f) * scale, bone * 0.9f, 0.8f); // ribs
            }
            else
            {
                var rock = Def.Color.maxColorComponent > 0.05f ? Color.Lerp(Def.Color, new Color(0.4f, 0.37f, 0.33f), 0.5f) : new Color(0.4f, 0.37f, 0.33f);
                Sfx.Play("rubble", at, 0.9f, 0.1f, 40f);
                Sfx.Play("hit_stone", at + Vector3.up, 0.6f, 0.15f, 30f);
                SpellFx.Dust(at, 1.2f * scale, new Color(0.45f, 0.4f, 0.35f));
                int n = Mathf.RoundToInt(10 * Mathf.Sqrt(scale));
                for (int i = 0; i < n; i++)
                    Piece(PrimitiveType.Cube, at + Vector3.up * Random.Range(0.3f, 2f) * scale, Vector3.one * Random.Range(0.18f, 0.42f) * scale, rock * Random.Range(0.75f, 1.1f), 2f);
                if (Height > 2.5f) CameraRig.Shake(0.12f);
            }
        }

        /// <summary>A piece of a body flung out a little and left lying (for as long as the corpse would).</summary>
        void Piece(PrimitiveType shape, Vector3 at, Vector3 size, Color c, float force)
        {
            var go = Factory.Prim(shape, null, at, size, c);
            go.transform.rotation = Random.rotation;
            var fall = go.AddComponent<FallingPiece>();
            var away = lastHitDir.sqrMagnitude > 0.01f ? Factory.Flat(lastHitDir).normalized : Vector3.zero;
            fall.Velocity = (away * 1.5f + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f))) * force + Vector3.up * Random.Range(1f, 3f);
            fall.Spin = Random.insideUnitSphere * 500f;
            go.AddComponent<FadeAway>().Seconds = 9f;
        }

        protected override void Die(Combatant killer) { /* deaths are decided by the server */ }
    }
}
