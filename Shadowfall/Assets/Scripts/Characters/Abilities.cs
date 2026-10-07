using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    public enum AbilityId
    {
        Cleave, Fireball, FrostNova, Heal, Meteor,
        ShieldBash, HolyBolt, Consecration, DivineShield, Judgement,
        ThrowingAxe, Whirlwind, Leap, WarCry,
        ChainLightning, Teleport,
        TwinStrike, Multishot, FanOfKnives, SmokeBomb, RainOfArrows,
    }

    public class AbilityDef
    {
        public AbilityId Id;
        public string Name, Description, Key, Icon;
        public float ManaCost, Cooldown;
        public int RequiredLevel;
        public Color Color;

        static AbilityDef A(AbilityId id, string name, string icon, float mana, float cd, int level, Color color, string desc) =>
            new AbilityDef { Id = id, Name = name, Icon = icon, ManaCost = mana, Cooldown = cd, RequiredLevel = level, Color = color, Description = desc };

        static readonly Color Holy = new Color(1f, 0.88f, 0.45f), Fire = new Color(1f, 0.45f, 0.1f), Frost = new Color(0.45f, 0.8f, 1f),
            Storm = new Color(0.55f, 0.7f, 1f), Steel = new Color(0.9f, 0.85f, 0.75f), Blood = new Color(0.9f, 0.25f, 0.15f),
            Shadow = new Color(0.6f, 0.75f, 0.6f), Arcane = new Color(0.75f, 0.5f, 1f);

        /// <summary>Every ability in the game (old saves and other players' effects may refer to any of them).</summary>
        public static readonly AbilityDef[] All =
        {
            A(AbilityId.Cleave, "Cleave", "cleave", 8, 2.5f, 1, Steel, "Sweep your weapon in a wide arc, dealing 170% weapon damage to all enemies in front of you."),
            A(AbilityId.Fireball, "Fireball", "fireball", 6, 0.55f, 1, Fire, "Hurl a ball of fire that explodes on impact, burning nearby enemies. Scales with Intelligence."),
            A(AbilityId.FrostNova, "Frost Nova", "frostnova", 20, 8f, 3, Frost, "A blast of frost damages all nearby enemies and slows them by 50% for 4 seconds."),
            A(AbilityId.Heal, "Holy Light", "heal", 25, 14f, 5, Holy, "Heal yourself for 35% of maximum life plus 3x Intelligence."),
            A(AbilityId.Meteor, "Meteor", "meteor", 40, 12f, 8, Fire, "Call down a meteor at the target location, crushing enemies in a large area after a short delay."),

            A(AbilityId.ShieldBash, "Shield Bash", "shield_bash", 6, 4f, 1, Steel, "Slam your shield into enemies in front of you for 140% weapon damage, stunning them for 1.5 seconds."),
            A(AbilityId.HolyBolt, "Holy Bolt", "holy_bolt", 5, 0.6f, 1, Holy, "Hurl a bolt of holy light. Each hit heals you for 3% of your maximum life. Scales with Strength and Intelligence."),
            A(AbilityId.Consecration, "Consecration", "consecration", 20, 10f, 3, Holy, "Sanctify the ground around you for 6 seconds: enemies inside burn, and you heal while you stand in it."),
            A(AbilityId.DivineShield, "Divine Shield", "divine_shield", 25, 18f, 5, Holy, "Take 50% less damage for 6 seconds and heal 20% of your maximum life."),
            A(AbilityId.Judgement, "Judgement", "judgement", 35, 12f, 8, Holy, "Call down a hammer of light on the target area, smiting and stunning everything there."),

            A(AbilityId.ThrowingAxe, "Throwing Axe", "throwing_axe", 4, 0.6f, 1, Steel, "Hurl a spinning axe for 110% weapon damage."),
            A(AbilityId.Whirlwind, "Whirlwind", "whirlwind", 15, 9f, 3, Blood, "Spin for 2.5 seconds, hitting everything around you for 60% weapon damage several times a second. You can keep moving."),
            A(AbilityId.Leap, "Leap", "leap", 20, 8f, 5, Steel, "Leap to the target location and slam the ground: 200% weapon damage around you and slows enemies."),
            A(AbilityId.WarCry, "War Cry", "war_cry", 30, 20f, 8, Blood, "A terrifying roar: +40% damage and +30% armor for 10 seconds, and heal 15% of your maximum life."),

            A(AbilityId.ChainLightning, "Chain Lightning", "chain_lightning", 8, 1.4f, 1, Storm, "Lightning arcs from the target to up to 4 nearby enemies. Scales with Intelligence."),
            A(AbilityId.Teleport, "Teleport", "teleport", 15, 6f, 5, Arcane, "Instantly teleport to the target location (up to 12 m)."),

            A(AbilityId.TwinStrike, "Twin Strike", "twin_strike", 5, 1.2f, 1, Shadow, "Two quick strikes for 90% weapon damage each, with +15% critical chance. Scales with Dexterity."),
            A(AbilityId.Multishot, "Multishot", "multishot", 6, 0.7f, 1, Shadow, "Fire a fan of 5 arrows for 75% weapon damage each."),
            A(AbilityId.FanOfKnives, "Fan of Knives", "fan_of_knives", 15, 7f, 3, Steel, "Throw knives in every direction: 120% weapon damage and slows enemies for 3 seconds."),
            A(AbilityId.SmokeBomb, "Smoke Bomb", "smoke_bomb", 20, 16f, 5, Shadow, "Vanish in a cloud of smoke: enemies lose track of you for 4 seconds, and you heal 10% of your maximum life."),
            A(AbilityId.RainOfArrows, "Rain of Arrows", "rain_of_arrows", 35, 12f, 8, Shadow, "Arrows rain on the target area for 3 seconds, hitting everything there many times."),
        };

        public static AbilityDef Get(AbilityId id)
        {
            foreach (var a in All) if (a.Id == id) return a;
            return All[0];
        }
    }

    /// <summary>The five abilities each hero class uses (keys 1-5; ability 2 is also on the right mouse button).</summary>
    public static class ClassKits
    {
        static readonly Dictionary<string, AbilityId[]> kits = new Dictionary<string, AbilityId[]>
        {
            { "Knight", new[] { AbilityId.ShieldBash, AbilityId.HolyBolt, AbilityId.Consecration, AbilityId.DivineShield, AbilityId.Judgement } },
            { "Barbarian", new[] { AbilityId.Cleave, AbilityId.ThrowingAxe, AbilityId.Whirlwind, AbilityId.Leap, AbilityId.WarCry } },
            { "Mage", new[] { AbilityId.ChainLightning, AbilityId.Fireball, AbilityId.FrostNova, AbilityId.Teleport, AbilityId.Meteor } },
            { "Rogue", new[] { AbilityId.TwinStrike, AbilityId.Multishot, AbilityId.FanOfKnives, AbilityId.SmokeBomb, AbilityId.RainOfArrows } },
        };

        static readonly string[] keys = { "1", "2/RMB", "3", "4", "5" };

        public static AbilityDef[] For(string heroClass)
        {
            if (!kits.TryGetValue(heroClass ?? "", out var ids)) ids = kits["Knight"];
            var list = new AbilityDef[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                var src = AbilityDef.Get(ids[i]);
                list[i] = new AbilityDef
                {
                    Id = src.Id, Name = src.Name, Description = src.Description, Icon = src.Icon, ManaCost = src.ManaCost,
                    Cooldown = src.Cooldown, RequiredLevel = i < 2 ? 1 : i == 2 ? 3 : i == 3 ? 5 : 8, Color = src.Color, Key = keys[i],
                };
            }
            return list;
        }

        /// <summary>Starting attributes: Str, Dex, Int, Vit.</summary>
        public static int[] StartingStats(string heroClass)
        {
            switch (heroClass)
            {
                case "Barbarian": return new[] { 15, 10, 6, 13 };
                case "Mage": return new[] { 7, 9, 16, 10 };
                case "Rogue": return new[] { 9, 16, 8, 11 };
                default: return new[] { 12, 9, 9, 14 };
            }
        }

        public static string Role(string heroClass)
        {
            switch (heroClass)
            {
                case "Barbarian": return "Two-handed fury. Strength. Whirlwind, Leap, War Cry.";
                case "Mage": return "Arcane fire and storm. Intelligence. Chain Lightning, Teleport, Meteor.";
                case "Rogue": return "Arrows and shadows. Dexterity. Multishot, Smoke Bomb, Rain of Arrows.";
                default: return "Sword, shield and holy light. Strength and Vitality. Shield Bash, Consecration, Judgement.";
            }
        }
    }

    // =====================================================================================
    // Talents
    // =====================================================================================

    public class TalentDef
    {
        public string Id, Name, Description, Icon;
        public int MaxRank;
    }

    /// <summary>Six talents per class: three general passives and three that improve specific abilities.</summary>
    public static class Talents
    {
        static TalentDef T(string id, string name, string icon, int max, string desc) => new TalentDef { Id = id, Name = name, Icon = icon, MaxRank = max, Description = desc };

        public static readonly Dictionary<string, TalentDef[]> ByClass = new Dictionary<string, TalentDef[]>
        {
            { "Knight", new[]
            {
                T("toughness", "Toughness", "knight", 5, "+5% maximum life per rank."),
                T("bulwark", "Bulwark", "divine_shield", 5, "+8% armor per rank."),
                T("righteous", "Righteous Fury", "holy_bolt", 5, "+6% holy damage per rank (Holy Bolt, Consecration, Judgement)."),
                T("bash", "Shield Mastery", "shield_bash", 3, "Shield Bash stuns 0.4 s longer and deals 15% more damage per rank."),
                T("consecrate", "Lingering Light", "consecration", 3, "Consecration lasts 1.5 s longer and its radius grows by 0.5 m per rank."),
                T("layonhands", "Lay on Hands", "heal", 3, "Divine Shield heals an extra 7% of your maximum life per rank."),
            } },
            { "Barbarian", new[]
            {
                T("brute", "Brute Force", "barbarian", 5, "+6% melee damage per rank."),
                T("thickskin", "Thick Skin", "chest", 5, "+5% maximum life per rank."),
                T("bloodlust", "Bloodlust", "war_cry", 5, "Heal 2 life per rank every time you hit."),
                T("cyclone", "Cyclone", "whirlwind", 3, "Whirlwind lasts 0.7 s longer and hits 10% harder per rank."),
                T("earthshaker", "Earthshaker", "leap", 3, "Leap's slam radius grows by 0.6 m and deals 15% more damage per rank."),
                T("battlerage", "Battle Rage", "war_cry", 3, "War Cry lasts 2 s longer and adds another 10% damage per rank."),
            } },
            { "Mage", new[]
            {
                T("arcane", "Arcane Power", "mage", 5, "+6% spell damage per rank."),
                T("manafont", "Mana Font", "mana_potion", 5, "+12% mana regeneration per rank."),
                T("focus", "Focus", "chain_lightning", 5, "+2% critical chance per rank."),
                T("pyromancy", "Pyromancy", "fireball", 3, "Fireball's explosion grows by 0.4 m and deals 10% more damage per rank."),
                T("deepfreeze", "Deep Freeze", "frostnova", 3, "Frost Nova slows 1 s longer and deals 15% more damage per rank."),
                T("blink", "Blink Mastery", "teleport", 3, "Teleport's cooldown is 1.2 s shorter per rank."),
            } },
            { "Rogue", new[]
            {
                T("precision", "Precision", "rogue", 5, "+2% critical chance per rank."),
                T("swiftness", "Swiftness", "boots", 5, "+4% movement speed per rank."),
                T("lethality", "Lethality", "dagger", 5, "Critical hits deal +12% damage per rank."),
                T("volley", "Volley", "multishot", 3, "Multishot fires one more arrow per rank."),
                T("venom", "Venom", "fan_of_knives", 3, "Fan of Knives slows 1 s longer and deals 15% more damage per rank."),
                T("shadowstep", "Shadow Step", "smoke_bomb", 3, "Smoke Bomb hides you 1 s longer per rank."),
            } },
        };

        public static TalentDef[] For(string heroClass) => ByClass.TryGetValue(heroClass ?? "", out var t) ? t : ByClass["Knight"];
    }

    /// <summary>A timed buff on the hero (Divine Shield, War Cry, Smoke Bomb...).</summary>
    public class Buff
    {
        public string Name, Icon;
        public float Until, DamageMul = 1f, DamageTakenMul = 1f, ArmorMul = 1f;
        public Color Color;
    }

    // =====================================================================================
    // Effects that live in the world for a while
    // =====================================================================================

    /// <summary>A meteor (or a hammer of light) falling from the sky toward a target point.</summary>
    public class MeteorFx : MonoBehaviour
    {
        Vector3 target;
        float delay, t, damage, radius = 4f, stun;
        Color color = new Color(1f, 0.3f, 0.05f);
        bool holy;
        Combatant owner;
        GameObject marker;
        ParticleSystem[] trails;
        float nextPulse;
        static readonly List<Combatant> buffer = new List<Combatant>();

        public static void Cast(Combatant owner, Vector3 target, float damage, float delay = 0.8f) => Spawn(owner, target, damage, delay, false, 4f, 0f);

        /// <summary>The Knight's Judgement: a golden hammer of light that stuns.</summary>
        public static void CastJudgement(Combatant owner, Vector3 target, float damage, float radius, float stun) => Spawn(owner, target, damage, 0.6f, true, radius, stun);

        static void Spawn(Combatant owner, Vector3 target, float damage, float delay, bool holy, float radius, float stun)
        {
            var c = holy ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.3f, 0.05f);
            var start = target + (holy ? new Vector3(0f, 14f, 0f) : new Vector3(-4f, 14f, -4f));
            var go = Factory.Prim(holy ? PrimitiveType.Cube : PrimitiveType.Sphere, null, start, holy ? new Vector3(0.9f, 1.6f, 0.9f) : Vector3.one * 1.3f, c, false, Mat.Glow(c));
            go.name = holy ? "Judgement" : "Meteor";
            var m = go.AddComponent<MeteorFx>();
            m.target = target;
            m.delay = delay;
            m.damage = damage;
            m.owner = owner;
            m.holy = holy;
            m.color = c;
            m.radius = radius;
            m.stun = stun;
            Sfx.Play(holy ? "holy_cast" : "meteor_fall", target, 0.8f, 0.05f, 50f);
            m.trails = SpellFx.AttachTrail(go.transform, holy ? SpellFx.Trail.Magic : SpellFx.Trail.Fire, c, 1.6f);
            if (m.trails != null) go.transform.localScale *= 0.7f;
            m.marker = Factory.Prim(PrimitiveType.Cylinder, null, new Vector3(target.x, 0.03f, target.z), new Vector3(radius * 2f, 0.01f, radius * 2f),
                c * 0.6f, false, Mat.Glow(c * 0.4f));
        }

        void Update()
        {
            t += Time.deltaTime;
            var start = target + (holy ? new Vector3(0f, 14f, 0f) : new Vector3(-4f, 14f, -4f));
            transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(t / delay));
            if (t < delay)
            {
                if (Time.time >= nextPulse) { nextPulse = Time.time + 0.25f; SpellFx.Ring(target, color, radius, 0.3f); }
                return;
            }
            if (trails != null) foreach (var tr in trails) SpellFx.Detach(tr);

            if (owner != null) // null = another player's (cosmetic only)
            {
                Combatant.Overlap(target, radius, owner.Faction, buffer);
                foreach (var c in buffer)
                {
                    c.TakeDamage(damage * Random.Range(0.9f, 1.1f), owner, false);
                    if (stun > 0f && c is Enemy e) e.Stun(stun);
                }
            }
            if (holy)
            {
                SpellFx.Explosion(target + Vector3.up * 0.5f, color, radius, false);
                SpellFx.Column(target, color, radius * 0.6f, 9f, 0.9f);
                SpellFx.Shockwave(target, color, radius);
                Sfx.Play("boom", target, 0.8f, 0.08f, 60f);
                Sfx.Play("holy_bolt", target, 0.8f, 0.05f, 50f);
            }
            else if (SpellFx.Ready)
            {
                SpellFx.Explosion(target + Vector3.up * 0.5f, new Color(1f, 0.4f, 0.05f), radius, true);
                SpellFx.Shockwave(target, new Color(1f, 0.45f, 0.1f), radius);
                SpellFx.GroundFire(target, radius * 0.55f, 1.5f);
                SpellFx.Dust(target, radius * 0.75f, new Color(0.3f, 0.25f, 0.2f));
                for (int i = 0; i < 10; i++) // flying rocks
                    FxPulse.Spawn(target + Vector3.up * 0.5f, new Color(0.25f, 0.18f, 0.12f), Vector3.one * Random.Range(0.15f, 0.35f), Vector3.one * 0.05f, Random.Range(0.6f, 1f), PrimitiveType.Cube)
                        .WithVelocity(new Vector3(Random.Range(-6f, 6f), Random.Range(5f, 9f), Random.Range(-6f, 6f)));
                Sfx.Play("boom", target, 1f, 0.08f, 60f);
                Sfx.Play("rubble", target, 0.6f, 0.1f, 40f);
            }
            else
            {
                FxPulse.Burst(target, color, radius, 0.45f);
                FxPulse.Ring(target, color, radius + 0.5f, 0.5f);
            }
            CameraRig.Shake(0.35f);
            Destroy(marker);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// An area that hurts enemies over time: Consecration (holy ground, heals the owner standing in it)
    /// or Rain of Arrows. A null owner makes it cosmetic (another player's).
    /// </summary>
    public class GroundEffect : MonoBehaviour
    {
        public enum Kind { Consecration, RainOfArrows }

        Kind kind;
        Combatant owner;
        float radius, damagePerTick, until, nextTick, healPerTick;
        static readonly List<Combatant> buffer = new List<Combatant>();

        public static void Spawn(Kind kind, Combatant owner, Vector3 pos, float radius, float duration, float damagePerTick, float healPerTick = 0f)
        {
            var go = new GameObject(kind.ToString());
            go.transform.position = pos;
            var g = go.AddComponent<GroundEffect>();
            g.kind = kind;
            g.owner = owner;
            g.radius = radius;
            g.damagePerTick = damagePerTick;
            g.healPerTick = healPerTick;
            g.until = Time.time + duration;
            if (kind == Kind.Consecration)
            {
                var gold = new Color(1f, 0.85f, 0.4f);
                SpellFx.Emit(new SpellFx.P
                {
                    Rate = 50 * radius, Duration = duration, Life = new Vector2(0.8f, 1.4f), Speed = new Vector2(0.05f, 0.2f),
                    Size = new Vector2(0.08f, 0.16f), Start = Color.white, Mid = gold, End = new Color(1f, 0.6f, 0.2f, 0f),
                    Shape = ParticleSystemShapeType.Circle, Radius = radius, Velocity = new Vector3(0f, 1.6f, 0f), Max = 600,
                }, pos + Vector3.up * 0.05f);
                Sfx.Play("holy_cast", pos, 0.6f, 0.03f);
            }
            else
            {
                Sfx.Play("bow", pos, 0.6f, 0.1f);
            }
        }

        void Update()
        {
            if (Time.time >= until) { Destroy(gameObject); return; }
            if (Time.time < nextTick) return;
            bool holy = kind == Kind.Consecration;
            nextTick = Time.time + (holy ? 0.5f : 0.25f);
            var c = holy ? new Color(1f, 0.85f, 0.4f) : new Color(0.85f, 0.85f, 0.75f);
            if (holy)
            {
                SpellFx.CastCircle(transform.position, c, radius, 0.55f);
                SpellFx.Emit(new SpellFx.P
                {
                    Burst = 12, Duration = 0.1f, Life = new Vector2(0.5f, 0.9f), Speed = new Vector2(0f, 0.2f), Size = new Vector2(0.08f, 0.16f),
                    Start = Color.white, Mid = c, End = new Color(c.r, c.g, c.b, 0f), Shape = ParticleSystemShapeType.Circle, Radius = radius * 0.8f,
                    Velocity = new Vector3(0f, 2.5f, 0f),
                }, transform.position + Vector3.up * 0.05f);
            }
            else
                for (int i = 0; i < 4; i++) // arrows streaking down
                {
                    var p = transform.position + new Vector3(Random.Range(-radius, radius), 0f, Random.Range(-radius, radius)) * 0.8f;
                    SpellFx.Emit(new SpellFx.P
                    {
                        Burst = 1, Duration = 0.1f, Life = new Vector2(0.25f, 0.25f), Speed = new Vector2(0f, 0f), Size = new Vector2(0.08f, 0.08f),
                        Start = new Color(1f, 1f, 0.9f), End = new Color(1f, 0.9f, 0.6f, 0f), Velocity = new Vector3(0f, -36f, 0f), Stretch = true,
                    }, p + Vector3.up * 8f);
                    SpellFx.Hit(p, new Color(0.8f, 0.75f, 0.6f), false, 4);
                    if (Random.value < 0.4f) SpellFx.Dust(p, 0.4f);
                }
            if (owner == null) return;
            Combatant.Overlap(transform.position, radius, owner.Faction, buffer);
            foreach (var e in buffer) e.TakeDamage(damagePerTick * Random.Range(0.9f, 1.1f), owner, false);
            if (healPerTick > 0f && Factory.FlatDistance(owner.transform.position, transform.position) <= radius) owner.Heal(healPerTick, false);
        }
    }
}
