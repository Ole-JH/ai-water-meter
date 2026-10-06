using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The look and sound of each class ability, shared by the local hero (which also applies the gameplay)
    /// and other players' casts relayed by the server (cosmetic only).
    /// </summary>
    public static class AbilityFx
    {
        public static readonly Color Gold = new Color(1f, 0.85f, 0.4f), Storm = new Color(0.55f, 0.75f, 1f), Blood = new Color(0.9f, 0.2f, 0.12f),
            Steel = new Color(0.9f, 0.88f, 0.8f), Smoke = new Color(0.35f, 0.38f, 0.36f), Arcane = new Color(0.75f, 0.5f, 1f);

        static Quaternion Facing(Vector3 from, Vector3 to)
        {
            var d = Factory.Flat(to - from);
            return d.sqrMagnitude > 0.001f ? Quaternion.LookRotation(d) : Quaternion.identity;
        }

        public static void ShieldBash(Vector3 from, Vector3 to)
        {
            Sfx.Play("hit_heavy", from + Vector3.up, 0.7f, 0.1f);
            Sfx.Play("hit_armor", from + Vector3.up, 0.5f, 0.1f);
            var front = from + Facing(from, to) * Vector3.forward * 1.4f;
            SpellFx.Cleave(from, Facing(from, to), 2.6f, Steel);
            SpellFx.Hit(front + Vector3.up, Gold, false, 14);
            SpellFx.Ring(front, Gold, 1.4f, 0.3f);
        }

        public static void HolyBolt(Vector3 from, Vector3 to) => Sfx.Play("holy_cast", from, 0.35f, 0.15f);

        public static void Consecration(Vector3 at)
        {
            SpellFx.Ring(at, Gold, 4f, 0.7f);
            SpellFx.Flash(at + Vector3.up, Gold, 9f, 2.5f, 0.8f);
        }

        public static void DivineShield(Vector3 at)
        {
            Sfx.Play("bell", at, 0.6f, 0.05f);
            Sfx.Play("holy_cast", at, 0.7f, 0.02f);
            SpellFx.Column(at, Gold, 1.3f, 6f, 1f);
            SpellFx.Ring(at, Gold, 2.2f, 0.6f);
            SpellFx.HolyLight(at);
        }

        public static void ThrowingAxe(Vector3 from) => Sfx.Play("swing", from + Vector3.up, 0.6f, 0.15f);

        public static void Whirl(Vector3 at, Quaternion facing)
        {
            SpellFx.Cleave(at, facing, 3f, Blood);
            SpellFx.Cleave(at, facing * Quaternion.Euler(0f, 180f, 0f), 3f, Blood);
        }

        public static void LeapLand(Vector3 at, float radius)
        {
            Sfx.Play("boom", at, 0.8f, 0.1f, 50f);
            Sfx.Play("rubble", at, 0.6f, 0.1f, 40f);
            SpellFx.Dust(at, radius * 0.7f);
            SpellFx.Ring(at, Steel, radius, 0.5f);
            SpellFx.Hit(at + Vector3.up * 0.3f, new Color(0.55f, 0.45f, 0.35f), false, 18);
            CameraRig.Shake(0.25f);
        }

        public static void WarCry(Vector3 at)
        {
            Sfx.Play("roar", at, 0.9f, 0.05f, 40f);
            SpellFx.Ring(at, Blood, 3f, 0.5f);
            SpellFx.Ring(at, Blood, 6f, 0.8f);
            SpellFx.Flash(at + Vector3.up, Blood, 8f, 3f, 0.8f);
            SpellFx.Emit(new SpellFx.P
            {
                Burst = 40, Duration = 0.1f, Life = new Vector2(0.5f, 0.9f), Speed = new Vector2(4f, 8f), Size = new Vector2(0.06f, 0.12f),
                Start = new Color(1f, 0.6f, 0.4f), End = new Color(0.8f, 0.1f, 0.05f, 0f), Shape = ParticleSystemShapeType.Circle, Radius = 0.5f, Stretch = true, Drag = 2f,
            }, at + Vector3.up);
        }

        /// <summary>A crackling bolt between two points (Chain Lightning).</summary>
        public static void Lightning(Vector3 from, Vector3 to)
        {
            LightningBolt.Spawn(from, to, Storm);
            SpellFx.Hit(to, Storm, false, 10);
            SpellFx.Flash(to, Storm, 6f, 2.5f, 0.25f);
        }

        public static void ChainCast(Vector3 from) => Sfx.Play("shatter", from + Vector3.up, 0.45f, 0.2f);

        public static void Teleport(Vector3 from, Vector3 to)
        {
            Sfx.Play("frost_cast", from, 0.5f, 0.2f);
            foreach (var p in new[] { from, to })
            {
                SpellFx.Column(p, Arcane, 0.9f, 4f, 0.4f);
                SpellFx.Ring(p, Arcane, 1.4f, 0.4f);
                SpellFx.Emit(new SpellFx.P
                {
                    Burst = 30, Duration = 0.1f, Life = new Vector2(0.3f, 0.6f), Speed = new Vector2(1f, 3f), Size = new Vector2(0.06f, 0.12f),
                    Start = Color.white, Mid = Arcane, End = new Color(0.4f, 0.2f, 0.8f, 0f), Radius = 0.6f, Velocity = new Vector3(0f, 2f, 0f),
                }, p + Vector3.up);
            }
        }

        public static void TwinStrike(Vector3 from, Vector3 to)
        {
            Sfx.Play("swing", from + Vector3.up, 0.6f, 0.2f);
            SpellFx.Cleave(from, Facing(from, to), 2f, new Color(0.6f, 0.9f, 0.6f));
        }

        /// <summary>Cosmetic arrows (the local hero fires real ones).</summary>
        public static void Multishot(Vector3 from, Vector3 to, int arrows)
        {
            Sfx.Play("swing", from + Vector3.up, 0.5f, 0.25f);
            var dir = Facing(from, to);
            for (int i = 0; i < arrows; i++)
            {
                float a = (i - (arrows - 1) / 2f) * 10f;
                var d = Quaternion.Euler(0f, a, 0f) * dir * Vector3.forward;
                Projectile.FireVisual(from + Vector3.up * 1.2f, from + Vector3.up * 1.2f + d * 10f, 26f, Steel, 0.25f, 16f).WithTrail(SpellFx.Trail.Arrow);
            }
        }

        public static void FanOfKnives(Vector3 at, float radius)
        {
            Sfx.Play("swing_heavy", at + Vector3.up, 0.6f, 0.15f);
            SpellFx.Ring(at, Steel, radius, 0.35f);
            for (int i = 0; i < 16; i++)
            {
                var d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                Projectile.FireVisual(at + Vector3.up, at + Vector3.up + d * 5f, 24f, Steel, 0.18f, radius).WithTrail(SpellFx.Trail.Arrow);
            }
        }

        public static void SmokeBomb(Vector3 at)
        {
            Sfx.Play("explosion", at, 0.45f, 0.2f);
            Sfx.Play("sizzle", at, 0.5f, 0.1f);
            SpellFx.Emit(new SpellFx.P
            {
                Burst = 40, Duration = 0.1f, Life = new Vector2(1.6f, 2.6f), Speed = new Vector2(0.8f, 2.2f), Size = new Vector2(1.2f, 2f),
                Start = new Color(0.4f, 0.42f, 0.4f, 0.7f), End = new Color(0.2f, 0.22f, 0.2f, 0f), Shape = ParticleSystemShapeType.Circle, Radius = 1f,
                Grow = true, Smoke = true, Drag = 1.5f, Velocity = new Vector3(0f, 0.5f, 0f),
            }, at + Vector3.up * 0.4f);
        }

        public static void RainOfArrows(Vector3 at, float radius) => SpellFx.Ring(at, new Color(0.85f, 0.85f, 0.7f), radius, 0.6f);

        /// <summary>Another player's ability, relayed by the server: effects only, no gameplay.</summary>
        public static bool Remote(string kind, Vector3 from, Vector3 to)
        {
            switch (kind)
            {
                case "bash": ShieldBash(from, to); return true;
                case "holybolt":
                    HolyBolt(from, to);
                    Projectile.FireVisual(from + Vector3.up * 1.2f, to + Vector3.up * 1.2f, 22f, Gold, 0.45f, 20f).WithTrail(SpellFx.Trail.Magic);
                    return true;
                case "consecrate": Consecration(from); GroundEffect.Spawn(GroundEffect.Kind.Consecration, null, from, 4f, 6f, 0f); return true;
                case "dshield": DivineShield(from); return true;
                case "judgement": MeteorFx.CastJudgement(null, to, 0f, 3.5f, 0f); return true;
                case "axe":
                    ThrowingAxe(from);
                    Projectile.FireVisual(from + Vector3.up * 1.2f, to + Vector3.up * 1.2f, 22f, Steel, 0.4f, 18f).WithTrail(SpellFx.Trail.Arrow);
                    return true;
                case "whirl": Whirl(from, Facing(from, to)); return true;
                case "leap": LeapLand(to, 3f); return true;
                case "warcry": WarCry(from); return true;
                case "chain": ChainCast(from); Lightning(from + Vector3.up * 1.3f, to + Vector3.up * 1f); return true;
                case "teleport": Teleport(from, to); return true;
                case "twin": TwinStrike(from, to); return true;
                case "multi": Multishot(from, to, 5); return true;
                case "knives": FanOfKnives(from, 5f); return true;
                case "smoke": SmokeBomb(from); return true;
                case "rain": RainOfArrows(to, 3.5f); GroundEffect.Spawn(GroundEffect.Kind.RainOfArrows, null, to, 3.5f, 3f, 0f); return true;
            }
            return false;
        }
    }

    /// <summary>A jagged, flickering line of light that fades out quickly.</summary>
    public class LightningBolt : MonoBehaviour
    {
        LineRenderer line;
        Vector3 from, to;
        float born, nextJitter;
        const float Life = 0.28f;

        public static void Spawn(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("Lightning");
            var b = go.AddComponent<LightningBolt>();
            b.from = from;
            b.to = to;
            b.born = Time.time;
            b.line = go.AddComponent<LineRenderer>();
            b.line.material = Mat.Glow(Color.Lerp(color, Color.white, 0.4f));
            b.line.positionCount = 10;
            b.line.widthMultiplier = 0.14f;
            b.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.line.receiveShadows = false;
            b.Jitter();
        }

        void Jitter()
        {
            var side = Vector3.Cross((to - from).normalized, Vector3.up);
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (line.positionCount - 1f);
                var p = Vector3.Lerp(from, to, t);
                if (i > 0 && i < line.positionCount - 1) p += side * Random.Range(-0.35f, 0.35f) + Vector3.up * Random.Range(-0.3f, 0.3f);
                line.SetPosition(i, p);
            }
        }

        void Update()
        {
            float age = Time.time - born;
            if (age > Life) { Destroy(gameObject); return; }
            line.widthMultiplier = 0.14f * (1f - age / Life) + 0.02f;
            if (Time.time >= nextJitter) { nextJitter = Time.time + 0.05f; Jitter(); }
        }
    }
}
