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
            return p;
        }

        /// <summary>Cosmetic projectile (another player's spell) that never deals damage.</summary>
        public static void FireVisual(Vector3 from, Vector3 toward, float speed, Color color, float size, float range)
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
            p.visualOnly = true;
        }

        void Update()
        {
            float step = speed * Time.deltaTime;
            transform.position += dir * step;
            life -= Time.deltaTime;

            // Trail sparkle
            if (Random.value < 0.5f)
                FxPulse.Spawn(transform.position, color, Vector3.one * 0.18f, Vector3.zero, 0.25f, PrimitiveType.Cube);

            if (visualOnly)
            {
                if (life <= 0f || !WorldGrid.Instance.IsWalkable(transform.position))
                {
                    FxPulse.Burst(transform.position, color, 1.2f, 0.3f);
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
                FxPulse.Burst(transform.position, color, aoe * 0.8f, 0.3f);
            }
            else if (direct != null)
            {
                direct.TakeDamage(damage, owner, crit);
                onHit?.Invoke(direct);
                FxPulse.Burst(transform.position, color, 0.5f, 0.2f);
            }
            FxPulse.Sparks(transform.position, color, 4);
            Destroy(gameObject);
        }
    }
}
