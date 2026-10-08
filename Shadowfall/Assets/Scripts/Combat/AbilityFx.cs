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
            SpellFx.CrossSlash(from, Facing(from, to), 2.6f, Steel);
            SpellFx.Hit(front + Vector3.up, Gold, false, 24);
            SpellFx.Shockwave(front, Gold, 1.6f);
            CameraRig.Shake(0.12f);
        }

        public static void HolyBolt(Vector3 from, Vector3 to)
        {
            Sfx.Play("holy_bolt", from + Vector3.up, 0.6f, 0.1f);
            SpellFx.CastCircle(from, Gold, 1f, 0.45f);
        }

        public static void Consecration(Vector3 at)
        {
            SpellFx.CastCircle(at, Gold, 4f, 1.2f);
            SpellFx.Shockwave(at, Gold, 4f);
            ImpactMarks.Place(at, ImpactMarks.Kind.Holy, 3.4f, 14f);
            SpellFx.Column(at, Gold, 1.2f, 5f, 0.8f);
            SpellFx.Flash(at + Vector3.up, Gold, 9f, 2.5f, 0.8f);
        }

        public static void DivineShield(Vector3 at)
        {
            Sfx.Play("holy_cast", at, 0.7f, 0.02f);
            SpellFx.Column(at, Gold, 1.3f, 6f, 1f);
            SpellFx.CastCircle(at, Gold, 2.2f, 1f);
            SpellFx.HolyLight(at);
            SpellFx.Emit(new SpellFx.P
            {
                Burst = 60, Duration = 0.1f, Life = new Vector2(0.6f, 1f), Speed = new Vector2(2f, 4f), Size = new Vector2(0.06f, 0.12f),
                Start = Color.white, Mid = Gold, End = new Color(1f, 0.6f, 0.2f, 0f), Radius = 1f, Drag = 2f, Velocity = new Vector3(0f, 1.5f, 0f),
            }, at + Vector3.up * 1.1f);
        }

        public static void ThrowingAxe(Vector3 from)
        {
            Sfx.Play("throw", from + Vector3.up, 0.7f, 0.12f);
            SpellFx.Hit(from + Vector3.up * 1.3f, Steel, false, 8);
        }

        public static void Whirl(Vector3 at, Quaternion facing)
        {
            SpellFx.Cleave(at, facing, 3f, Blood);
            SpellFx.Cleave(at, facing * Quaternion.Euler(0f, 180f, 0f), 3f, Blood);
            SpellFx.Emit(new SpellFx.P
            {
                Burst = 10, Duration = 0.1f, Life = new Vector2(0.4f, 0.7f), Speed = new Vector2(1.5f, 3f), Size = new Vector2(0.4f, 0.7f),
                Start = new Color(0.45f, 0.38f, 0.3f, 0.4f), End = new Color(0.35f, 0.3f, 0.25f, 0f), Shape = ParticleSystemShapeType.Circle, Radius = 1.4f,
                Grow = true, Smoke = true, Drag = 2f, Orbital = 6f,
            }, at + Vector3.up * 0.2f);
        }

        public static void LeapLand(Vector3 at, float radius)
        {
            Sfx.Play("boom", at, 0.8f, 0.1f, 50f);
            Sfx.Play("rubble", at, 0.6f, 0.1f, 40f);
            SpellFx.Dust(at, radius * 0.7f);
            ImpactMarks.Place(at, ImpactMarks.Kind.Crack, radius * 0.6f);
            SpellFx.Shockwave(at, new Color(1f, 0.6f, 0.3f), radius);
            SpellFx.Hit(at + Vector3.up * 0.3f, new Color(0.55f, 0.45f, 0.35f), false, 18);
            for (int i = 0; i < 8; i++) // rocks thrown up by the slam
                FxPulse.Spawn(at + Vector3.up * 0.3f, new Color(0.35f, 0.3f, 0.25f), Vector3.one * Random.Range(0.12f, 0.28f), Vector3.one * 0.05f, Random.Range(0.5f, 0.9f), PrimitiveType.Cube)
                    .WithVelocity(new Vector3(Random.Range(-5f, 5f), Random.Range(4f, 7f), Random.Range(-5f, 5f)));
            CameraRig.Shake(0.3f);
        }

        public static void WarCry(Vector3 at)
        {
            Sfx.Play("roar", at, 0.9f, 0.05f, 40f);
            SpellFx.Shockwave(at, Blood, 3f);
            SpellFx.Ring(at, Blood, 6f, 0.8f);
            SpellFx.Ring(at, new Color(1f, 0.5f, 0.3f), 9f, 1.1f);
            CameraRig.Shake(0.2f);
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
            SpellFx.Hit(to, Storm, false, 16);
            SpellFx.Ring(new Vector3(to.x, 0f, to.z), Storm, 1f, 0.3f);
            SpellFx.Flash(to, Storm, 7f, 3f, 0.25f);
        }

        public static void ChainCast(Vector3 from)
        {
            Sfx.Play("zap", from + Vector3.up, 0.6f, 0.12f);
            SpellFx.CastCircle(from, Storm, 1.1f, 0.4f);
        }

        public static void Teleport(Vector3 from, Vector3 to)
        {
            Sfx.Play("blink", from, 0.6f, 0.1f);
            foreach (var p in new[] { from, to })
            {
                SpellFx.Column(p, Arcane, 0.9f, 4f, 0.4f);
                SpellFx.CastCircle(p, Arcane, 1.4f, 0.5f);
                SpellFx.Emit(new SpellFx.P
                {
                    Burst = 20, Duration = 0.1f, Life = new Vector2(0.3f, 0.5f), Speed = new Vector2(0f, 0.2f), Size = new Vector2(0.05f, 0.08f),
                    Start = Color.white, Mid = Arcane, End = new Color(0.4f, 0.2f, 0.8f, 0f), Shape = ParticleSystemShapeType.Circle, Radius = 0.5f,
                    Velocity = new Vector3(0f, 9f, 0f), Stretch = true,
                }, p);
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
            SpellFx.CrossSlash(from, Facing(from, to), 2.2f, new Color(0.6f, 0.95f, 0.6f));
            SpellFx.Hit(from + Facing(from, to) * Vector3.forward * 1.3f + Vector3.up, new Color(0.7f, 1f, 0.7f), false, 12);
        }

        /// <summary>Cosmetic arrows (the local hero fires real ones).</summary>
        public static void Multishot(Vector3 from, Vector3 to, int arrows)
        {
            Sfx.Play("bow", from + Vector3.up, 0.6f, 0.1f);
            var dir = Facing(from, to);
            for (int i = 0; i < arrows; i++)
            {
                float a = (i - (arrows - 1) / 2f) * 10f;
                var d = Quaternion.Euler(0f, a, 0f) * dir * Vector3.forward;
                Projectile.FireVisual(from + Vector3.up * 1.2f, from + Vector3.up * 1.2f + d * 10f, 26f, Steel, 0.25f, 16f).WithTrail(SpellFx.Trail.Arrow).WithShape(Projectile.Shape.Arrow);
            }
        }

        public static void FanOfKnives(Vector3 at, float radius)
        {
            Sfx.Play("swing_heavy", at + Vector3.up, 0.6f, 0.15f);
            SpellFx.Shockwave(at, Steel, radius * 0.8f);
            SpellFx.Swirl(at + Vector3.up * 0.8f, null, Steel, 0.8f, 0.25f, 120f, false);
            for (int i = 0; i < 16; i++)
            {
                var d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                Projectile.FireVisual(at + Vector3.up, at + Vector3.up + d * 5f, 24f, Steel, 0.18f, radius).WithTrail(SpellFx.Trail.Arrow);
            }
        }

        public static void SmokeBomb(Vector3 at)
        {
            Sfx.Play("poof", at, 0.7f, 0.1f);
            SpellFx.Emit(new SpellFx.P
            {
                Burst = 40, Duration = 0.1f, Life = new Vector2(1.6f, 2.6f), Speed = new Vector2(0.8f, 2.2f), Size = new Vector2(1.2f, 2f),
                Start = new Color(0.4f, 0.42f, 0.4f, 0.7f), End = new Color(0.2f, 0.22f, 0.2f, 0f), Shape = ParticleSystemShapeType.Circle, Radius = 1f,
                Grow = true, Smoke = true, Drag = 1.5f, Velocity = new Vector3(0f, 0.5f, 0f),
            }, at + Vector3.up * 0.4f);
            // the cloud keeps billowing for a few seconds
            SpellFx.Emit(new SpellFx.P
            {
                Rate = 14, Duration = 3f, Life = new Vector2(1.5f, 2.5f), Speed = new Vector2(0.2f, 0.6f), Size = new Vector2(1.2f, 2.2f),
                Start = new Color(0.38f, 0.4f, 0.38f, 0.5f), End = new Color(0.2f, 0.22f, 0.2f, 0f), Shape = ParticleSystemShapeType.Circle, Radius = 1.8f,
                Grow = true, Smoke = true, Velocity = new Vector3(0f, 0.3f, 0f), Orbital = 1.2f,
            }, at + Vector3.up * 0.3f);
            SpellFx.Flash(at + Vector3.up, new Color(0.6f, 0.7f, 0.6f), 5f, 1.5f, 0.3f);
        }

        public static void RainOfArrows(Vector3 at, float radius)
        {
            SpellFx.CastCircle(at, new Color(0.85f, 0.85f, 0.7f), radius, 0.8f);
            Sfx.Play("bow", at, 0.5f, 0.1f);
        }

        /// <summary>Another player's ability, relayed by the server: effects only, no gameplay.</summary>
        public static bool Remote(string kind, Vector3 from, Vector3 to)
        {
            switch (kind)
            {
                case "bash": ShieldBash(from, to); return true;
                case "holybolt":
                    HolyBolt(from, to);
                    Projectile.FireVisual(from + Vector3.up * 1.2f, to + Vector3.up * 1.2f, 22f, Gold, 0.45f, 20f).WithTrail(SpellFx.Trail.Magic).WithShape(Projectile.Shape.Spear);
                    return true;
                case "consecrate": Consecration(from); GroundEffect.Spawn(GroundEffect.Kind.Consecration, null, from, 4f, 6f, 0f); return true;
                case "dshield": DivineShield(from); return true;
                case "judgement": MeteorFx.CastJudgement(null, to, 0f, 3.5f, 0f); return true;
                case "axe":
                    ThrowingAxe(from);
                    Projectile.FireVisual(from + Vector3.up * 1.2f, to + Vector3.up * 1.2f, 22f, Steel, 0.4f, 18f).WithTrail(SpellFx.Trail.Arrow).WithShape(Projectile.Shape.Axe);
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
        float born, nextJitter, width = 0.14f;
        const float Life = 0.28f;

        public static void Spawn(Vector3 from, Vector3 to, Color color) => Spawn(from, to, color, 0.14f, true);

        static void Spawn(Vector3 from, Vector3 to, Color color, float width, bool forks)
        {
            if (forks)
            {
                // A couple of thinner side branches that break off the main bolt and fizzle out.
                for (int f = 0; f < 2; f++)
                {
                    var start = Vector3.Lerp(from, to, Random.Range(0.25f, 0.7f));
                    var dir = (to - from).normalized;
                    var side = Vector3.Cross(dir, Vector3.up).normalized * Random.Range(-1f, 1f);
                    Spawn(start, start + (dir * 0.6f + side + Vector3.up * Random.Range(-0.4f, 0.4f)) * Random.Range(1f, 2f), color, width * 0.45f, false);
                }
                SpellFx.Emit(new SpellFx.P
                {
                    Burst = 14, Duration = 0.1f, Life = new Vector2(0.15f, 0.35f), Speed = new Vector2(2f, 6f), Size = new Vector2(0.03f, 0.07f),
                    Start = Color.white, Mid = color, End = new Color(color.r, color.g, color.b, 0f), Stretch = true, Gravity = 0.8f,
                }, from);
            }
            var go = new GameObject("Lightning");
            var b = go.AddComponent<LightningBolt>();
            b.from = from;
            b.to = to;
            b.born = Time.time;
            b.line = go.AddComponent<LineRenderer>();
            b.line.material = Mat.Glow(Color.Lerp(color, Color.white, 0.4f));
            b.line.positionCount = 10;
            b.line.widthMultiplier = width;
            b.width = width;
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
            line.widthMultiplier = width * (1f - age / Life) + 0.02f;
            if (Time.time >= nextJitter) { nextJitter = Time.time + 0.05f; Jitter(); }
        }
    }

    /// <summary>
    /// A buff's look on the hero while it lasts: War Cry burns with rising red embers, Divine Shield wraps the
    /// hero in a golden bubble, Smoke Bomb trails wisps of smoke.
    /// </summary>
    public class BuffAura : MonoBehaviour
    {
        string kind;
        float until, nextPulse;
        ParticleSystem ps;
        GameObject bubble;

        public static void Attach(Transform hero, string kind, float duration)
        {
            foreach (var old in hero.GetComponentsInChildren<BuffAura>()) if (old.kind == kind) Destroy(old.gameObject);
            var go = new GameObject("Aura " + kind);
            go.transform.SetParent(hero, false);
            var a = go.AddComponent<BuffAura>();
            a.kind = kind;
            a.until = Time.time + duration;
            switch (kind)
            {
                case "War Cry":
                    a.ps = SpellFx.Emit(new SpellFx.P
                    {
                        Rate = 30, Duration = duration, Life = new Vector2(0.6f, 1.1f), Speed = new Vector2(0.1f, 0.3f), Size = new Vector2(0.06f, 0.12f),
                        Start = new Color(1f, 0.7f, 0.4f), Mid = AbilityFx.Blood, End = new Color(0.6f, 0.05f, 0.02f, 0f), Shape = ParticleSystemShapeType.Circle,
                        Radius = 0.6f, Velocity = new Vector3(0f, 1.8f, 0f), Max = 150,
                    }, hero.position, go.transform);
                    break;
                case "Divine Shield":
                    var gold = AbilityFx.Gold;
                    a.bubble = Factory.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0f, 1.1f, 0f), Vector3.one * 2.6f, gold, false, Mat.Glow(gold * 0.35f));
                    var r = a.bubble.GetComponent<Renderer>();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (SpellFx.Ready) { Object.Destroy(a.bubble); a.bubble = null; }
                    a.ps = SpellFx.Swirl(hero.position + Vector3.up * 0.2f, go.transform, gold, 1.1f, duration, 60f, false);
                    break;
                case "Vanished":
                    a.ps = SpellFx.Swirl(hero.position + Vector3.up * 0.3f, go.transform, new Color(0.45f, 0.48f, 0.45f), 0.7f, duration, 14f, true);
                    break;
            }
        }

        void Update()
        {
            if (Time.time >= until)
            {
                if (ps != null) SpellFx.Detach(ps);
                Destroy(gameObject);
                return;
            }
            if (Time.time < nextPulse) return;
            nextPulse = Time.time + 0.9f;
            var pos = transform.position;
            if (kind == "Divine Shield") { SpellFx.Ring(pos, AbilityFx.Gold, 1.4f, 0.8f); SpellFx.Column(pos, AbilityFx.Gold, 0.9f, 3f, 0.6f); }
            else if (kind == "War Cry") SpellFx.Ring(pos, AbilityFx.Blood, 1.2f, 0.6f);
        }
    }

    /// <summary>Little stars circling the head of a stunned monster.</summary>
    public class StunStars : MonoBehaviour
    {
        Transform[] stars;
        Enemy enemy;

        public static void Show(Enemy e)
        {
            if (e.Stars != null) return;
            var go = new GameObject("StunStars");
            go.transform.SetParent(e.transform, false);
            go.transform.localPosition = Vector3.up * (e.Height + 0.25f);
            var s = go.AddComponent<StunStars>();
            s.enemy = e;
            e.Stars = s;
            var c = new Color(1f, 0.9f, 0.4f);
            s.stars = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var star = Factory.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, Vector3.one * 0.14f, c, false, Mat.Glow(c));
                star.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Object.Destroy(star.GetComponent<Collider>());
                s.stars[i] = star.transform;
            }
        }

        void Update()
        {
            if (enemy == null || enemy.IsDead || !enemy.Stunned) { Destroy(gameObject); return; }
            for (int i = 0; i < stars.Length; i++)
            {
                float a = Time.time * 5f + i * Mathf.PI * 2f / stars.Length;
                stars[i].localPosition = new Vector3(Mathf.Cos(a) * 0.45f, Mathf.Sin(Time.time * 7f + i) * 0.06f, Mathf.Sin(a) * 0.45f);
                stars[i].localRotation = Quaternion.Euler(45f, Time.time * 360f, 45f);
            }
        }
    }
}
