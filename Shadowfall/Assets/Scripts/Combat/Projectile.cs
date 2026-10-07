using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    public class Projectile : MonoBehaviour
    {
        Combatant owner;
        Faction ownerFaction;
        Vector3 dir;
        float speed, life, damage, aoe, hitRadius;
        Color color;
        bool crit, visualOnly;
        System.Action<Combatant> onHit;
        ParticleSystem[] trails;
        SpellFx.Trail trailKind = SpellFx.Trail.Magic;
        float size;
        static readonly List<Combatant> buffer = new List<Combatant>();

        public static Projectile Fire(Combatant owner, Vector3 from, Vector3 toward, float speed, float damage,
            Color color, float size = 0.35f, float aoe = 0f, float range = 18f, bool crit = false,
            System.Action<Combatant> onHit = null)
        {
            var go = Factory.Prim(PrimitiveType.Sphere, null, from, Vector3.one * size, color, false, Mat.Glow(color));
            go.name = "Projectile";
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var p = go.AddComponent<Projectile>();
            p.owner = owner;
            p.ownerFaction = owner.Faction;
            var d = Factory.Flat(toward - from);
            p.dir = d.sqrMagnitude > 0.001f ? d.normalized : owner.transform.forward;
            p.speed = speed;
            p.life = range / speed;
            p.damage = damage;
            p.color = color;
            p.aoe = aoe;
            p.hitRadius = size * 0.5f + 0.2f;
            p.crit = crit;
            p.onHit = onHit;
            p.size = size;
            return p;
        }

        public enum Shape { Orb, Spear, Arrow, Axe }

        /// <summary>
        /// Changes the projectile's body so each class's missiles read differently: a lance of light (Holy Bolt),
        /// a thin arrow (Rogue, ranger) or a real spinning axe (Throwing Axe). Call after <see cref="WithTrail"/>.
        /// </summary>
        public Projectile WithShape(Shape shape)
        {
            if (shape == Shape.Orb) return this;
            transform.rotation = Quaternion.LookRotation(dir);
            var r = GetComponent<Renderer>();
            switch (shape)
            {
                case Shape.Spear:
                    transform.localScale = new Vector3(0.16f, 0.16f, 1.1f);
                    break;
                case Shape.Arrow:
                    transform.localScale = new Vector3(0.06f, 0.06f, 0.75f);
                    break;
                case Shape.Axe:
                    transform.localScale = Vector3.one;
                    var axe = ArtLibrary.Spawn("Weapons/Axe", transform, Vector3.zero, 0.75f, ArtLibrary.Fit.Height, 0f, false, false, true);
                    if (axe != null)
                    {
                        if (r != null) r.enabled = false;
                        axe.AddComponent<Spinner>();
                    }
                    else transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
                    break;
            }
            return this;
        }

        /// <summary>Adds a particle trail (fire, magic or arrow) and the matching impact effect.</summary>
        public Projectile WithTrail(SpellFx.Trail kind)
        {
            trailKind = kind;
            trails = SpellFx.AttachTrail(transform, kind, color, size);
            if (trails != null) transform.localScale *= kind == SpellFx.Trail.Arrow ? 0.4f : 0.6f; // the particles carry the look
            return this;
        }

        void Impact(float radius)
        {
            if (trails != null)
            {
                foreach (var t in trails) SpellFx.Detach(t);
                trails = null;
                if (trailKind == SpellFx.Trail.Arrow) SpellFx.Hit(transform.position, color, false, 10);
                else
                {
                    SpellFx.Explosion(transform.position, color, Mathf.Max(0.6f, radius), trailKind == SpellFx.Trail.Fire);
                    var ground = new Vector3(transform.position.x, 0f, transform.position.z);
                    if (trailKind == SpellFx.Trail.Fire)
                    {
                        // embers that keep burning where it hit, and a ring of flame
                        SpellFx.GroundFire(ground, Mathf.Max(0.6f, radius * 0.45f), 0.9f);
                        SpellFx.Ring(ground, color, Mathf.Max(1f, radius * 1.2f), 0.4f);
                    }
                    else
                    {
                        // a short pillar of light and a sparkle burst (Holy Bolt and other magic)
                        SpellFx.Column(ground, color, 0.5f, 2.5f, 0.35f);
                        SpellFx.Hit(transform.position, color, false, 14);
                    }
                }
                return;
            }
            FxPulse.Burst(transform.position, color, Mathf.Max(0.5f, radius), 0.3f);
            FxPulse.Sparks(transform.position, color, 4);
        }

        /// <summary>Cosmetic projectile (another player's spell) that never deals damage.</summary>
        public static Projectile FireVisual(Vector3 from, Vector3 toward, float speed, Color color, float size, float range)
        {
            var go = Factory.Prim(PrimitiveType.Sphere, null, from, Vector3.one * size, color, false, Mat.Glow(color));
            go.name = "Projectile (visual)";
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var p = go.AddComponent<Projectile>();
            var d = Factory.Flat(toward - from);
            p.dir = d.sqrMagnitude > 0.001f ? d.normalized : Vector3.forward;
            p.speed = speed;
            p.life = Mathf.Min(range, Mathf.Max(0.5f, d.magnitude)) / speed;
            p.color = color;
            p.size = size;
            p.visualOnly = true;
            return p;
        }

        void Update()
        {
            float step = speed * Time.deltaTime;
            transform.position += dir * step;
            life -= Time.deltaTime;

            // Trail sparkle (only without particle trails)
            if (trails == null && Random.value < 0.5f)
                FxPulse.Spawn(transform.position, color, Vector3.one * 0.18f, Vector3.zero, 0.25f, PrimitiveType.Cube);

            if (visualOnly)
            {
                if (life <= 0f || !WorldGrid.Instance.IsWalkable(transform.position))
                {
                    Impact(trailKind == SpellFx.Trail.Fire ? 1.6f : 0.8f);
                    if (trailKind == SpellFx.Trail.Fire) Sfx.Play("explosion", transform.position, 0.35f, 0.15f);
                    else if (trailKind == SpellFx.Trail.Arrow) Sfx.Play("hit_flesh", transform.position, 0.25f, 0.15f);
                    Destroy(gameObject);
                }
                return;
            }

            foreach (var c in Combatant.All)
            {
                if (c.IsDead || c.Faction == ownerFaction) continue;
                if (Factory.FlatDistance(c.transform.position, transform.position) <= c.Radius + hitRadius)
                {
                    Explode(c);
                    return;
                }
            }

            if (life <= 0f || !WorldGrid.Instance.IsWalkable(transform.position)) Explode(null);
        }

        void Explode(Combatant direct)
        {
            if (aoe > 0f)
            {
                Combatant.Overlap(transform.position, aoe, ownerFaction, buffer);
                foreach (var c in buffer)
                {
                    c.TakeDamage(c == direct ? damage : damage * 0.6f, owner, crit);
                    onHit?.Invoke(c);
                }
                Impact(aoe * 0.8f);
                Sfx.Play("explosion", transform.position, 0.5f, 0.15f);
            }
            else
            {
                if (direct != null)
                {
                    direct.TakeDamage(damage, owner, crit);
                    onHit?.Invoke(direct);
                }
                Impact(0.5f);
            }
            Destroy(gameObject);
        }
    }

    /// <summary>Tumbles a thrown weapon end over end.</summary>
    public class Spinner : MonoBehaviour
    {
        void Update() => transform.Rotate(Vector3.right, 1100f * Time.deltaTime, Space.Self);
    }
}
