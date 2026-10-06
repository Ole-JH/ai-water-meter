using UnityEngine;

namespace Shadowfall
{
    public enum AbilityId { Cleave, Fireball, FrostNova, Heal, Meteor }

    public class AbilityDef
    {
        public AbilityId Id;
        public string Name, Description, Key, Icon;
        public float ManaCost, Cooldown;
        public int RequiredLevel;
        public Color Color;

        public static readonly AbilityDef[] All =
        {
            new AbilityDef
            {
                Id = AbilityId.Cleave, Name = "Cleave", Key = "1", Icon = "Cl", ManaCost = 8, Cooldown = 2.5f, RequiredLevel = 1,
                Color = new Color(0.9f, 0.85f, 0.75f),
                Description = "Sweep your weapon in a wide arc, dealing 170% weapon damage to all enemies in front of you."
            },
            new AbilityDef
            {
                Id = AbilityId.Fireball, Name = "Fireball", Key = "2/RMB", Icon = "Fb", ManaCost = 6, Cooldown = 0.55f, RequiredLevel = 1,
                Color = new Color(1f, 0.45f, 0.1f),
                Description = "Hurl a ball of fire that explodes on impact, burning nearby enemies. Scales with Intelligence."
            },
            new AbilityDef
            {
                Id = AbilityId.FrostNova, Name = "Frost Nova", Key = "3", Icon = "FN", ManaCost = 20, Cooldown = 8f, RequiredLevel = 3,
                Color = new Color(0.45f, 0.8f, 1f),
                Description = "Blast of frost damages all nearby enemies and slows them by 50% for 4 seconds."
            },
            new AbilityDef
            {
                Id = AbilityId.Heal, Name = "Holy Light", Key = "4", Icon = "HL", ManaCost = 25, Cooldown = 14f, RequiredLevel = 5,
                Color = new Color(1f, 0.95f, 0.5f),
                Description = "Heal yourself for 35% of maximum life plus 3x Intelligence."
            },
            new AbilityDef
            {
                Id = AbilityId.Meteor, Name = "Meteor", Key = "5", Icon = "Mt", ManaCost = 40, Cooldown = 12f, RequiredLevel = 8,
                Color = new Color(1f, 0.25f, 0.05f),
                Description = "Call down a meteor at the target location, crushing enemies in a large area after a short delay."
            },
        };
    }

    /// <summary>A meteor falling from the sky toward a target point.</summary>
    public class MeteorFx : MonoBehaviour
    {
        Vector3 target;
        float delay, t, damage;
        Combatant owner;
        GameObject marker;
        static readonly System.Collections.Generic.List<Combatant> buffer = new System.Collections.Generic.List<Combatant>();

        public static void Cast(Combatant owner, Vector3 target, float damage, float delay = 0.8f)
        {
            var c = new Color(1f, 0.3f, 0.05f);
            var go = Factory.Prim(PrimitiveType.Sphere, null, target + new Vector3(-4f, 14f, -4f), Vector3.one * 1.3f, c, false, Mat.Glow(c));
            go.name = "Meteor";
            var m = go.AddComponent<MeteorFx>();
            m.target = target;
            m.delay = delay;
            m.damage = damage;
            m.owner = owner;
            Sfx.Play("meteor_fall", target, 0.8f, 0.05f, 50f);
            m.marker = Factory.Prim(PrimitiveType.Cylinder, null, new Vector3(target.x, 0.03f, target.z), new Vector3(8f, 0.01f, 8f),
                new Color(0.6f, 0.1f, 0.05f), false, Mat.Glow(new Color(0.5f, 0.08f, 0.02f)));
        }

        void Update()
        {
            t += Time.deltaTime;
            var start = target + new Vector3(-4f, 14f, -4f);
            transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(t / delay));
            if (t < delay) return;

            if (owner != null) // null = another player's meteor (cosmetic only)
            {
                Combatant.Overlap(target, 4f, owner.Faction, buffer);
                foreach (var c in buffer) c.TakeDamage(damage * Random.Range(0.9f, 1.1f), owner, false);
            }
            FxPulse.Burst(target, new Color(1f, 0.45f, 0.1f), 4f, 0.45f);
            FxPulse.Ring(target, new Color(1f, 0.25f, 0.05f), 4.5f, 0.5f);
            FxPulse.Sparks(target + Vector3.up * 0.5f, new Color(1f, 0.6f, 0.1f), 14);
            CameraRig.Shake(0.35f);
            Sfx.Play("boom", target, 1f, 0.08f, 60f);
            Sfx.Play("rubble", target, 0.6f, 0.1f, 40f);
            Destroy(marker);
            Destroy(gameObject);
        }
    }
}
