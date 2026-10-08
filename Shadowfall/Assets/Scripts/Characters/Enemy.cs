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
        Vector3 lastHitDir;
        bool lastHitBig;
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
                // corpses lie in their blood for a while before sinking away
                float linger = WorldBoss.Is(Def.Name) ? BossPresence.Linger : 7f; // a world boss lies where it fell a while
                if (Time.time - deathTime > linger) transform.position += Vector3.down * dt * 0.8f;
                if (Time.time - deathTime > linger + 3f) Destroy(gameObject);
                return;
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
                view.UpdateLocomotion(moveSpeed);
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
            Health = Mathf.Max(0f, Health - dmg);
            LastDamagedTime = Time.time;
            GameUI.Float(transform.position + Vector3.up * (Height + 0.2f), crit ? dmg + "!" : dmg.ToString(),
                crit ? new Color(1f, 0.85f, 0.2f) : Color.white, crit ? 1.5f : 1f);
            var gore = Gore.KindOf(Def.Name);
            bool bones = gore == Gore.Kind.Bone, stone = gore == Gore.Kind.Stone;
            SpellFx.Hit(Center, bones ? new Color(0.9f, 0.88f, 0.8f) : stone ? new Color(0.6f, 0.55f, 0.5f) : new Color(0.55f, 0.03f, 0.03f), !bones && !stone, crit ? 16 : 9);
            // blood flies away from whoever struck the blow
            lastHitDir = source != null ? transform.position - source.transform.position : transform.forward * -1f;
            lastHitBig = crit || dmg > MaxHealth * 0.35f;
            Gore.Hit(Center, lastHitDir, gore, crit ? 1f : Mathf.Clamp01(dmg / Mathf.Max(1f, MaxHealth) * 3f));
            Sfx.Play(crit ? "hit_heavy" : Voice(Def, "hit"), Center, crit ? 0.7f : 0.5f, 0.12f);
            view?.Hit();
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
            if (SpellFx.Ready) SpellFx.Dust(transform.position, Def.Boss ? 2.5f : 1.2f);
            else FxPulse.Burst(Center, Factory.Shade(Def.Color, 0.6f), 0.8f, 0.3f);
            // A big killing blow (a crit, a heavy hit) also throws chunks.
            bool overkill = lastHitBig && Time.time - LastDamagedTime < 0.6f;
            Gore.Death(Center, lastHitDir, Gore.KindOf(Def.Name), Height / 1.8f, overkill, Def.Boss);
        }

        protected override void Die(Combatant killer) { /* deaths are decided by the server */ }
    }
}
