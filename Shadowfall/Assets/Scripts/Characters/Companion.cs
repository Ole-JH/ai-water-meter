using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>A companion for hire at Beastmaster Orla's.</summary>
    public class CompanionDef
    {
        public string Id, Name, Role, Description, Icon;
        public int Price, RequiredLevel;
        public CharacterLook Look;
        public float Damage;            // per attack at hero level 1 (grows 12% per level)
        public float Cooldown = 1.2f, Range = 1.8f, Speed = 6.4f;
        public bool Ranged;
        public float Aoe;               // > 0: hits everything within this radius of the target
        public Color Color = Color.white;
        public string Special;          // "slow", "stun", "heal", "volley"

        public static readonly CompanionDef[] All =
        {
            new CompanionDef
            {
                Id = "hound", Name = "War Hound", Role = "Fast melee", Icon = "companion_hound", Price = 300, RequiredLevel = 1,
                Description = "A loyal mastiff that bites hard and hamstrings its prey (slows on hit).",
                Look = new CharacterLook { Model = "Monsters/Wolf", Height = 1.05f, Anims = AnimSet.Wolf, RunSpeed = 6f, Tint = new Color(0.75f, 0.55f, 0.38f) },
                Damage = 5f, Cooldown = 0.9f, Range = 1.5f, Speed = 7.2f, Special = "slow", Color = new Color(0.8f, 0.6f, 0.4f),
            },
            new CompanionDef
            {
                Id = "squire", Name = "Squire Edric", Role = "Sword & shield", Icon = "companion_squire", Price = 800, RequiredLevel = 4,
                Description = "A sworn squire in his father's mail. Every few seconds he bashes a foe senseless (stun).",
                Look = new CharacterLook { Model = "Characters/Knight", Height = 1.75f, Tint = new Color(0.8f, 0.85f, 0.95f), Weapon = "sword" },
                Damage = 8f, Cooldown = 1.2f, Range = 1.8f, Special = "stun", Color = new Color(0.8f, 0.85f, 1f),
            },
            new CompanionDef
            {
                Id = "witch", Name = "Hedge Witch Nell", Role = "Fire magic", Icon = "companion_witch", Price = 1400, RequiredLevel = 7,
                Description = "She hurls fire from a safe distance. Her fireballs burn everything around the target.",
                Look = new CharacterLook { Model = "Characters/Mage", Height = 1.85f, Tint = new Color(0.75f, 1f, 0.7f), Weapon = "staff" },
                Damage = 9f, Cooldown = 1.6f, Range = 9f, Ranged = true, Aoe = 1.8f, Color = new Color(1f, 0.45f, 0.1f),
            },
            new CompanionDef
            {
                Id = "ranger", Name = "Ranger Kestrel", Role = "Archer", Icon = "companion_ranger", Price = 2000, RequiredLevel = 10,
                Description = "A hooded scout who never misses twice. Fires two arrows at a time.",
                Look = new CharacterLook { Model = "Characters/RogueHooded", Height = 1.85f, Tint = new Color(0.8f, 0.95f, 0.8f), Weapon = "dagger" },
                Damage = 7f, Cooldown = 1.1f, Range = 11f, Ranged = true, Special = "volley", Color = new Color(0.9f, 0.9f, 0.8f),
            },
            new CompanionDef
            {
                Id = "acolyte", Name = "Acolyte Mira", Role = "Healer", Icon = "companion_acolyte", Price = 2800, RequiredLevel = 12,
                Description = "A novice of the Light. Heals you for 10% of your life when you are hurt, and smites with holy bolts.",
                Look = new CharacterLook { Model = "Characters/Mage", Height = 1.8f, Tint = new Color(1.2f, 1.15f, 1.05f), Weapon = "staff" },
                Damage = 5f, Cooldown = 1.5f, Range = 9f, Ranged = true, Special = "heal", Color = new Color(1f, 0.88f, 0.45f),
            },
            new CompanionDef
            {
                Id = "golem", Name = "Stone Golem", Role = "Heavy brawler", Icon = "companion_golem", Price = 5000, RequiredLevel = 15,
                Description = "A quarry golem bound with runes. Slow, but every slam shakes the ground around it.",
                Look = new CharacterLook { Model = "Monsters/Golem", Height = 2.3f, Anims = AnimSet.Big, RunSpeed = 4f, Tint = new Color(0.6f, 0.65f, 0.75f) },
                Damage = 16f, Cooldown = 2f, Range = 2.2f, Speed = 5.6f, Aoe = 2.6f, Color = new Color(0.6f, 0.7f, 0.9f),
            },
        };

        public static CompanionDef Get(string id)
        {
            foreach (var d in All) if (d.Id == id) return d;
            return null;
        }

        public float DamageAt(int heroLevel) => Damage * (1f + 0.12f * (heroLevel - 1));
    }

    /// <summary>
    /// A hired companion following a hero. For the local hero it fights (its hits count as the hero's);
    /// following another player it is purely cosmetic.
    /// </summary>
    public class Companion : MonoBehaviour
    {
        public CompanionDef Def { get; private set; }
        Transform owner;
        bool fights;
        CharacterView view;
        readonly List<Vector3> path = new List<Vector3>();
        float repathAt, nextAttack, nextSpecial, nextHeal, speed, nextBark, nextPick;
        Enemy target;
        static readonly List<Combatant> buffer = new List<Combatant>();

        public static Companion Spawn(CompanionDef def, Transform owner, bool fights)
        {
            var go = new GameObject("Companion " + def.Name);
            go.transform.position = owner.position - owner.forward * 1.5f + owner.right * 1.2f;
            var c = go.AddComponent<Companion>();
            c.Def = def;
            c.owner = owner;
            c.fights = fights;
            c.view = CharacterView.Create(go.transform, def.Look);
            if (c.view == null)
                Factory.Prim(PrimitiveType.Capsule, go.transform, new Vector3(0, def.Look.Height * 0.5f, 0), new Vector3(0.6f, def.Look.Height * 0.5f, 0.6f), def.Color);
            SpellFx.Ring(go.transform.position, def.Color, 1.2f, 0.5f);
            return c;
        }

        public void Dismiss()
        {
            SpellFx.Ring(transform.position, Def.Color, 1.2f, 0.4f);
            Destroy(gameObject);
        }

        Vector3 FollowSpot => owner.position - owner.forward * 1.6f + owner.right * 1.3f;

        void Update()
        {
            if (owner == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            var pos = transform.position;
            float toOwner = Factory.FlatDistance(pos, owner.position);
            // Lost behind (dungeon change, teleport, respawn): catch up instantly.
            if (toOwner > 24f)
            {
                transform.position = new Vector3(FollowSpot.x, 0f, FollowSpot.z);
                path.Clear();
                target = null;
                SpellFx.Ring(transform.position, Def.Color, 1f, 0.4f);
                return;
            }

            var hero = Player.I;
            bool combat = fights && hero != null && !hero.IsDead && !WorldGenerator.InTown(owner.position);
            if (!combat) target = null;
            else if (Time.time >= nextPick || (target != null && target.IsDead)) { nextPick = Time.time + 0.25f; PickTarget(hero); }

            Vector3? goal = null;
            float stopAt = 0.4f;
            if (target != null)
            {
                float d = Factory.FlatDistance(pos, target.transform.position) - target.Radius;
                if (d > Def.Range || (Def.Ranged && !WorldGrid.Instance.LineOfSight(pos, target.transform.position)))
                {
                    goal = target.transform.position;
                    stopAt = Def.Range * 0.8f;
                }
                else
                {
                    path.Clear();
                    Factory.Face(transform, target.transform.position, 0.3f);
                    if (Time.time >= nextAttack) Attack(hero);
                }
            }
            else if (toOwner > 3f) goal = FollowSpot;

            if (combat && Def.Special == "heal" && Time.time >= nextHeal && hero.Health < hero.MaxHealth * 0.7f)
            {
                nextHeal = Time.time + 6f;
                hero.Heal(hero.MaxHealth * 0.1f);
                view?.Cast();
                SpellFx.HolyLight(hero.transform.position);
                Sfx.Play("holy_cast", hero.transform.position, 0.5f, 0.05f);
            }

            speed = 0f;
            if (goal.HasValue)
            {
                if (Time.time >= repathAt || path.Count == 0)
                {
                    repathAt = Time.time + 0.4f;
                    WorldGrid.Instance.FindPath(pos, goal.Value, path, 3000);
                }
                float run = toOwner > 8f ? Def.Speed * 1.35f : Def.Speed;
                if (path.Count > 0 && Factory.FlatDistance(pos, goal.Value) > stopAt)
                {
                    var next = path[0];
                    var to = Factory.Flat(next - pos);
                    float step = run * dt;
                    if (to.magnitude <= step) { transform.position = new Vector3(next.x, 0f, next.z); path.RemoveAt(0); }
                    else transform.position += to.normalized * step;
                    if (to.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), dt * 12f);
                    speed = run;
                }
            }
            else if (target == null) Factory.Face(transform, owner.position + owner.forward * 3f, 0.05f);
            view?.UpdateLocomotion(speed);

            if (fights && Time.time >= nextBark && target != null)
            {
                nextBark = Time.time + Random.Range(20f, 40f);
                if (Def.Id == "hound") Sfx.Play("wolf_attack", transform.position, 0.5f, 0.1f);
            }
        }

        void PickTarget(Player hero)
        {
            if (target != null && (target.IsDead || Factory.FlatDistance(target.transform.position, hero.transform.position) > 14f)) target = null;
            if (hero.AttackTarget is Enemy focus && !focus.IsDead && Factory.FlatDistance(focus.transform.position, hero.transform.position) < 14f)
            {
                target = focus;
                return;
            }
            if (target != null) return;
            float best = 9f;
            foreach (var c in Combatant.All)
            {
                if (!(c is Enemy e) || e.IsDead) continue;
                if (Factory.FlatDistance(e.transform.position, hero.transform.position) > 10f) continue;
                float d = Factory.FlatDistance(e.transform.position, transform.position);
                if (d < best) { best = d; target = e; }
            }
        }

        void Attack(Player hero)
        {
            nextAttack = Time.time + Def.Cooldown * Random.Range(0.9f, 1.1f);
            float dmg = Def.DamageAt(hero.Level) * Random.Range(0.85f, 1.15f);
            var from = transform.position + Vector3.up * 1.1f + transform.forward * 0.5f;
            var at = target.transform.position + Vector3.up * 1f;
            if (Def.Ranged)
            {
                if (Def.Id == "witch")
                {
                    view?.Cast();
                    Sfx.Play("fire_cast", transform.position, 0.4f, 0.1f);
                    Projectile.Fire(hero, from, at, 18f, dmg, Def.Color, 0.4f, Def.Aoe, Def.Range + 4f).WithTrail(SpellFx.Trail.Fire);
                }
                else if (Def.Id == "acolyte")
                {
                    view?.Cast();
                    Projectile.Fire(hero, from, at, 20f, dmg, Def.Color, 0.35f, 0f, Def.Range + 4f).WithTrail(SpellFx.Trail.Magic);
                }
                else
                {
                    view?.Shoot();
                    Sfx.Play("bow", transform.position, 0.4f, 0.12f);
                    int arrows = Def.Special == "volley" ? 2 : 1;
                    for (int i = 0; i < arrows; i++)
                    {
                        var side = transform.right * (i - (arrows - 1) * 0.5f) * 0.6f;
                        Projectile.Fire(hero, from + side, at + side, 26f, dmg, Def.Color, 0.22f, 0f, Def.Range + 4f).WithTrail(SpellFx.Trail.Arrow).WithShape(Projectile.Shape.Arrow);
                    }
                }
                return;
            }

            view?.Attack(0.5f);
            Sfx.Play(Def.Id == "hound" ? "wolf_attack" : Def.Id == "golem" ? "boom" : "swing", transform.position + Vector3.up, Def.Id == "golem" ? 0.5f : 0.45f, 0.15f);
            if (Def.Aoe > 0f)
            {
                Combatant.Overlap(target.transform.position, Def.Aoe, Faction.Player, buffer);
                foreach (var c in buffer.ToArray()) c.TakeDamage(dmg, hero, false);
                SpellFx.Dust(target.transform.position, Def.Aoe * 0.6f);
                SpellFx.Ring(target.transform.position, Def.Color, Def.Aoe, 0.4f);
                CameraRig.Shake(0.08f);
            }
            else target.TakeDamage(dmg, hero, false);

            if (Def.Special == "slow" && Random.value < 0.35f) target.Slow(2f);
            if (Def.Special == "stun" && Time.time >= nextSpecial)
            {
                nextSpecial = Time.time + 7f;
                target.Stun(1.2f);
                SpellFx.Hit(target.Center, new Color(1f, 0.9f, 0.5f), false, 12);
                Sfx.Play("hit_armor", target.Center, 0.5f, 0.1f);
            }
        }
    }
}
