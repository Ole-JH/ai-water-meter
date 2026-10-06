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
            new EnemyDef { Name = "Rock Golem", Shape = EnemyShape.Golem, Color = new Color(0.5f, 0.48f, 0.45f), Secondary = new Color(0.35f, 0.33f, 0.3f), Scale = 1.3f },
            new EnemyDef { Name = "Lich King", Color = new Color(0.55f, 0.75f, 0.85f), Secondary = new Color(0.3f, 0.12f, 0.45f), Scale = 1.8f,
                Ranged = true, Boss = true, Robe = true, ProjectileColor = new Color(0.4f, 0.9f, 1f) },
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
        public bool Slowed;

        Vector3 netPos;
        float armor, attackAnim = -1f, deathTime, curSpeed, walkPhase;
        bool dying;
        HumanoidModel humanoid;
        Transform model;
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
            Level = m.l;
            armor = m.ar;
            MaxHealth = m.mhp;
            // Keep our locally-predicted damage for a moment so health bars don't flicker back up.
            Health = Time.time - LastDamagedTime < 0.4f ? Mathf.Min(Health, m.hp) : m.hp;
            Slowed = m.sl;
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
                if (Time.time - deathTime > 1.2f) transform.position += Vector3.down * dt * 1.2f;
                if (Time.time - deathTime > 3f) Destroy(gameObject);
                return;
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

            // Face whoever we are hitting
            if (attackAnim >= 0f && Player.I != null && FacingTarget != null)
                Factory.Face(transform, FacingTarget.Value, dt * 10f);

            Animate(dt);
        }

        Vector3? FacingTarget;

        public void PlayAttack(Vector3 targetPos)
        {
            attackAnim = 0f;
            FacingTarget = targetPos;
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
            float mitigated = amount * 100f / (100f + Mathf.Max(0f, Armor));
            int dmg = Mathf.Max(1, Mathf.RoundToInt(mitigated));
            Health = Mathf.Max(0f, Health - dmg);
            LastDamagedTime = Time.time;
            GameUI.Float(transform.position + Vector3.up * (Height + 0.2f), crit ? dmg + "!" : dmg.ToString(),
                crit ? new Color(1f, 0.85f, 0.2f) : Color.white, crit ? 1.5f : 1f);
            FxPulse.Sparks(Center, new Color(0.7f, 0.05f, 0.05f), 3);
            if (source is Player) NetClient.I?.SendHit(NetId, dmg, crit);
        }

        public void Slow(float duration)
        {
            if (!IsDead) NetClient.I?.SendSlow(NetId, duration);
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
            if (model != null) model.localRotation = Quaternion.Euler(-80f, 0, 0);
            FxPulse.Burst(Center, Factory.Shade(Def.Color, 0.6f), 0.8f, 0.3f);
        }

        protected override void Die(Combatant killer) { /* deaths are decided by the server */ }

        /// <summary>Personal loot, rolled locally when the server credits us with a kill.</summary>
        public static void RollLoot(EnemyDef def, int level, Vector3 pos)
        {
            if (Random.value < (def.Boss ? 1f : 0.55f))
                LootDrop.Spawn(pos, null, Mathf.Max(1, Mathf.RoundToInt(level * Random.Range(2f, 6f) * (def.Boss ? 8f : 1f))));
            if (def.Boss)
            {
                int n = def.Name == "Lich King" ? 4 : 2;
                for (int i = 0; i < n; i++) LootDrop.Spawn(pos, ItemDatabase.RandomEquipment(level + 1, 1f, i == 0 ? Rarity.Rare : (Rarity?)null), 0);
                if (def.Name == "Lich King" && Random.value < 0.5f)
                    LootDrop.Spawn(pos, ItemDatabase.RandomEquipment(level + 2, 1f, Rarity.Legendary), 0);
            }
            else if (Random.value < 0.22f) LootDrop.Spawn(pos, ItemDatabase.RandomEquipment(level), 0);
            if (Random.value < 0.12f) LootDrop.Spawn(pos, Random.value < 0.6f ? ItemDatabase.HealthPotion() : ItemDatabase.ManaPotion(), 0);
        }
    }
}
