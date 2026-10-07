using UnityEngine;
using UnityEngine.Rendering;

namespace Shadowfall
{
    /// <summary>
    /// Spell and combat effects built from Unity particle systems and a few procedural meshes (rings, arcs,
    /// light columns) using the soft Shadowfall/Fx shader. Falls back to the old FxPulse blobs if the shader
    /// is missing. Everything cleans itself up.
    /// </summary>
    public static class SpellFx
    {
        public enum Trail { Fire, Magic, Arrow }

        static Material additive, smoke, band, column;
        static bool loaded, ok;
        static Mesh ringMesh, arcMesh, columnMesh;

        public static bool Ready
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    var shader = Resources.Load<Shader>("Shaders/ShadowfallFx");
                    if (shader == null) shader = Shader.Find("Shadowfall/Fx");
                    ok = shader != null && shader.isSupported;
                    if (ok)
                    {
                        additive = Make(shader, 0, BlendMode.One, BlendMode.One);
                        smoke = Make(shader, 0, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);
                        band = Make(shader, 1, BlendMode.One, BlendMode.One);
                        column = Make(shader, 2, BlendMode.One, BlendMode.One);
                        ringMesh = RingMesh(360f, 0.55f);
                        arcMesh = RingMesh(150f, 0.35f);
                        columnMesh = ColumnMesh();
                    }
                }
                return ok;
            }
        }

        static Material Make(Shader s, float mode, BlendMode src, BlendMode dst)
        {
            var m = new Material(s);
            m.SetFloat("_Mode", mode);
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
            return m;
        }

        // =====================================================================================
        // Particle emitter builder
        // =====================================================================================

        public class P
        {
            public int Burst;
            public float Rate, Duration = 1f;
            public Vector2 Life = new Vector2(0.3f, 0.6f), Speed = new Vector2(1f, 3f), Size = new Vector2(0.2f, 0.4f);
            public Color Start = Color.white, End = new Color(1, 1, 1, 0);
            public Color? Mid;
            public float Gravity, Drag, Radius = 0.1f, Arc = 360f;
            public ParticleSystemShapeType Shape = ParticleSystemShapeType.Sphere;
            public bool Grow, Stretch, Smoke, Follow;
            public Vector3 Velocity;
            public float Orbital;      // degrees-ish per second around the emitter's Y axis (swirls)
            public int Max = 200;
        }

        public static ParticleSystem Emit(P p, Vector3 pos, Transform follow = null, Quaternion? rotation = null)
        {
            if (!Ready) return null;
            var go = new GameObject("FX");
            go.SetActive(false);
            if (follow != null) go.transform.SetParent(follow, false);
            else go.transform.position = pos;
            if (rotation.HasValue) go.transform.rotation = rotation.Value;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = false;
            main.duration = Mathf.Max(0.05f, p.Duration);
            main.startLifetime = new ParticleSystem.MinMaxCurve(p.Life.x, p.Life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(p.Speed.x, p.Speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(p.Size.x, p.Size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.gravityModifier = p.Gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = p.Max;
            main.stopAction = follow == null ? ParticleSystemStopAction.Destroy : ParticleSystemStopAction.None;

            var em = ps.emission;
            float scale = GameSettings.ParticleScale;
            em.rateOverTime = p.Rate * scale;
            if (p.Burst > 0) em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, Mathf.RoundToInt(p.Burst * scale))) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = p.Shape;
            shape.radius = Mathf.Max(0.001f, p.Radius);
            shape.arc = p.Arc;
            if (p.Shape == ParticleSystemShapeType.Circle) shape.rotation = new Vector3(90f, 0f, 0f); // flat on the ground

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            var mid = p.Mid ?? Color.Lerp(p.Start, p.End, 0.5f);
            g.SetKeys(
                new[] { new GradientColorKey(p.Start, 0f), new GradientColorKey(mid, 0.45f), new GradientColorKey(p.End, 1f) },
                new[] { new GradientAlphaKey(p.Start.a, 0f), new GradientAlphaKey(mid.a, 0.45f), new GradientAlphaKey(p.End.a, 1f) });
            col.color = g;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = p.Grow ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f))
                               : new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 0.7f), new Keyframe(1f, 0f)));

            if (p.Drag > 0f)
            {
                var lim = ps.limitVelocityOverLifetime;
                lim.enabled = true;
                lim.drag = p.Drag;
                lim.multiplyDragByParticleSize = false;
            }
            if (p.Orbital != 0f)
            {
                var orb = ps.velocityOverLifetime;
                orb.enabled = true;
                orb.space = ParticleSystemSimulationSpace.Local;
                orb.orbitalY = p.Orbital;
            }
            if (p.Velocity != Vector3.zero)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = p.Velocity.x;
                vel.y = p.Velocity.y;
                vel.z = p.Velocity.z;
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = p.Smoke ? smoke : additive;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (p.Stretch)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.06f;
                r.lengthScale = 2f;
            }
            go.SetActive(true);
            return ps;
        }

        /// <summary>A looping emitter attached to <paramref name="follow"/> (auras, idle glows); lives as long as its parent.</summary>
        public static ParticleSystem Loop(P p, Transform follow, Vector3 local)
        {
            var ps = Emit(p, follow.position, follow);
            if (ps == null) return null;
            var main = ps.main;
            main.loop = true;
            main.stopAction = ParticleSystemStopAction.None;
            ps.transform.localPosition = local;
            ps.Play();
            return ps;
        }

        /// <summary>Stops a (following) emitter, lets its particles finish, then removes it.</summary>
        public static void Detach(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.transform.SetParent(null, true);
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // =====================================================================================
        // Meshes: rings, arcs, light columns
        // =====================================================================================

        static Mesh RingMesh(float degrees, float inner)
        {
            int seg = Mathf.Max(8, Mathf.RoundToInt(degrees / 6f));
            var v = new Vector3[(seg + 1) * 2];
            var uv = new Vector2[v.Length];
            var tris = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Deg2Rad * (-degrees / 2f + degrees * i / seg);
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                v[i * 2] = d * inner;
                v[i * 2 + 1] = d;
                float u = (float)i / seg;
                uv[i * 2] = new Vector2(u, 0f);
                uv[i * 2 + 1] = new Vector2(u, 1f);
                if (i == seg) break;
                int k = i * 6, b = i * 2;
                tris[k] = b; tris[k + 1] = b + 1; tris[k + 2] = b + 3;
                tris[k + 3] = b; tris[k + 4] = b + 3; tris[k + 5] = b + 2;
            }
            var m = new Mesh { name = "FxRing", vertices = v, uv = uv, triangles = tris };
            m.RecalculateBounds();
            return m;
        }

        static Mesh ColumnMesh()
        {
            const int seg = 24;
            var v = new Vector3[(seg + 1) * 2];
            var uv = new Vector2[v.Length];
            var tris = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 2] = d;
                v[i * 2 + 1] = d + Vector3.up;
                uv[i * 2] = new Vector2((float)i / seg, 0f);
                uv[i * 2 + 1] = new Vector2((float)i / seg, 1f);
                if (i == seg) break;
                int k = i * 6, b = i * 2;
                tris[k] = b; tris[k + 1] = b + 1; tris[k + 2] = b + 3;
                tris[k + 3] = b; tris[k + 4] = b + 3; tris[k + 5] = b + 2;
            }
            var m = new Mesh { name = "FxColumn", vertices = v, uv = uv, triangles = tris };
            m.RecalculateBounds();
            return m;
        }

        static FxMesh Shape(Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Color color, Vector3 from, Vector3 to, float duration, float spin = 0f)
        {
            var go = new GameObject("FX mesh");
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = from;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            var fx = go.AddComponent<FxMesh>();
            fx.Init(r, color, from, to, duration, spin);
            return fx;
        }

        public static void Ring(Vector3 pos, Color color, float radius, float duration = 0.45f)
        {
            if (!Ready) { FxPulse.Ring(pos, color, radius, duration); return; }
            Shape(ringMesh, band, new Vector3(pos.x, 0.06f, pos.z), Quaternion.identity, color,
                Vector3.one * 0.3f, new Vector3(radius, 1f, radius), duration);
        }

        public static void Column(Vector3 pos, Color color, float radius, float height, float duration)
        {
            if (!Ready) { FxPulse.Spawn(pos + Vector3.up, color, new Vector3(radius * 2f, 0.05f, radius * 2f), new Vector3(0.2f, height, 0.2f), duration, PrimitiveType.Cylinder); return; }
            Shape(columnMesh, column, new Vector3(pos.x, 0.02f, pos.z), Quaternion.identity, color,
                new Vector3(radius, height * 0.3f, radius), new Vector3(radius * 0.25f, height, radius * 0.25f), duration);
        }

        // =====================================================================================
        // Effects
        // =====================================================================================

        static readonly Color FireA = new Color(1f, 0.85f, 0.45f, 1f), FireB = new Color(1f, 0.38f, 0.05f, 0.9f), FireC = new Color(0.5f, 0.05f, 0.02f, 0f);
        static readonly Color SmokeA = new Color(0.12f, 0.1f, 0.09f, 0.45f), SmokeB = new Color(0.05f, 0.05f, 0.05f, 0f);

        /// <summary>Particle trail for a projectile. Call <see cref="Detach"/> on the returned systems when it ends.</summary>
        public static ParticleSystem[] AttachTrail(Transform t, Trail kind, Color color, float size)
        {
            if (!Ready) return null;
            switch (kind)
            {
                case Trail.Fire:
                    AddLight(t, new Color(1f, 0.55f, 0.15f), 6f, 2.2f);
                    return new[]
                    {
                        Emit(new P { Rate = 90, Duration = 10, Life = new Vector2(0.2f, 0.4f), Speed = new Vector2(0.1f, 0.6f), Size = new Vector2(size * 0.8f, size * 1.3f), Start = FireA, Mid = FireB, End = FireC, Radius = size * 0.2f }, t.position, t),
                        Emit(new P { Rate = 30, Duration = 10, Life = new Vector2(0.4f, 0.8f), Speed = new Vector2(0.4f, 1.4f), Size = new Vector2(0.04f, 0.08f), Start = FireA, End = new Color(1f, 0.3f, 0f, 0f), Gravity = -0.25f, Stretch = true }, t.position, t),
                        Emit(new P { Rate = 18, Duration = 10, Life = new Vector2(0.6f, 1f), Speed = new Vector2(0.2f, 0.6f), Size = new Vector2(size * 0.6f, size * 1.1f), Start = SmokeA, End = SmokeB, Grow = true, Smoke = true, Gravity = -0.05f }, t.position, t),
                    };
                case Trail.Arrow:
                    return new[]
                    {
                        Emit(new P { Rate = 60, Duration = 10, Life = new Vector2(0.15f, 0.25f), Speed = Vector2.zero, Size = new Vector2(0.08f, 0.12f), Start = new Color(color.r, color.g, color.b, 0.6f), End = new Color(color.r, color.g, color.b, 0f) }, t.position, t),
                    };
                default:
                    AddLight(t, color, 5f, 1.6f);
                    var bright = Color.Lerp(color, Color.white, 0.5f);
                    return new[]
                    {
                        Emit(new P { Rate = 80, Duration = 10, Life = new Vector2(0.2f, 0.45f), Speed = new Vector2(0.1f, 0.5f), Size = new Vector2(size * 0.7f, size * 1.2f), Start = bright, Mid = color, End = new Color(color.r, color.g, color.b, 0f), Radius = size * 0.2f }, t.position, t),
                        Emit(new P { Rate = 25, Duration = 10, Life = new Vector2(0.4f, 0.7f), Speed = new Vector2(0.5f, 1.5f), Size = new Vector2(0.04f, 0.08f), Start = bright, End = new Color(color.r, color.g, color.b, 0f), Stretch = true }, t.position, t),
                    };
            }
        }

        public static void Explosion(Vector3 pos, Color color, float radius, bool fire)
        {
            if (!Ready) { FxPulse.Burst(pos, color, radius, 0.3f); return; }
            var a = fire ? FireA : Color.Lerp(color, Color.white, 0.5f);
            var b = fire ? FireB : color;
            var c = fire ? FireC : new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 0f);
            Emit(new P { Burst = 1, Duration = 0.1f, Life = new Vector2(0.12f, 0.15f), Speed = Vector2.zero, Size = Vector2.one * radius * 2.6f, Start = a, End = new Color(b.r, b.g, b.b, 0f) }, pos);
            Emit(new P { Burst = Mathf.RoundToInt(18 + radius * 10), Duration = 0.1f, Life = new Vector2(0.25f, 0.55f), Speed = new Vector2(radius * 1.5f, radius * 3.5f), Size = new Vector2(0.5f, 1f) * Mathf.Max(0.6f, radius * 0.5f), Start = a, Mid = b, End = c, Drag = 4f }, pos);
            Emit(new P { Burst = 20, Duration = 0.1f, Life = new Vector2(0.3f, 0.7f), Speed = new Vector2(5f, 11f), Size = new Vector2(0.05f, 0.1f), Start = a, End = new Color(b.r, b.g, b.b, 0f), Gravity = 1.4f, Stretch = true }, pos);
            if (fire)
                Emit(new P { Burst = 10, Duration = 0.1f, Life = new Vector2(0.9f, 1.5f), Speed = new Vector2(0.6f, 1.6f), Size = new Vector2(0.6f, 1.1f) * Mathf.Max(0.7f, radius * 0.5f), Start = SmokeA, End = SmokeB, Grow = true, Smoke = true, Gravity = -0.08f }, pos + Vector3.up * 0.3f);
            Ring(pos, b, radius * 1.1f, 0.35f);
            Flash(pos + Vector3.up, b, radius * 3f + 3f, 4f, 0.35f);
        }

        public static void FrostNova(Vector3 pos, float radius)
        {
            var ice = new Color(0.55f, 0.85f, 1f);
            if (!Ready) { FxPulse.Ring(pos, ice, radius, 0.45f); return; }
            var up = pos + Vector3.up * 0.4f;
            Emit(new P { Burst = 80, Duration = 0.1f, Life = new Vector2(0.35f, 0.5f), Speed = new Vector2(radius * 2.2f, radius * 2.8f), Size = new Vector2(0.08f, 0.18f), Start = new Color(0.9f, 0.97f, 1f), Mid = ice, End = new Color(0.3f, 0.6f, 1f, 0f), Shape = ParticleSystemShapeType.Circle, Radius = 0.4f, Stretch = true, Drag = 2f }, up);
            Emit(new P { Burst = 26, Duration = 0.1f, Life = new Vector2(1f, 1.6f), Speed = new Vector2(radius * 0.6f, radius * 1.1f), Size = new Vector2(0.9f, 1.5f), Start = new Color(0.75f, 0.9f, 1f, 0.35f), End = new Color(0.75f, 0.9f, 1f, 0f), Shape = ParticleSystemShapeType.Circle, Radius = 0.5f, Grow = true, Smoke = true, Drag = 1.5f }, up);
            Ring(pos, ice, radius, 0.45f);
            Ring(pos, new Color(0.8f, 0.95f, 1f), radius * 0.7f, 0.6f);
            for (int i = 0; i < 12; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f), d = Random.Range(radius * 0.35f, radius * 0.95f);
                IceSpike.Spawn(pos + new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d), Random.Range(0.6f, 1.3f), i * 0.012f);
            }
            Flash(pos + Vector3.up, ice, radius * 2f, 3f, 0.5f);
        }

        public static void HolyLight(Vector3 pos)
        {
            var gold = new Color(1f, 0.88f, 0.45f);
            if (!Ready) { FxPulse.Spawn(pos + Vector3.up, gold, new Vector3(2f, 0.05f, 2f), new Vector3(0.2f, 5f, 0.2f), 0.7f, PrimitiveType.Cylinder); return; }
            Column(pos, gold, 1.3f, 7f, 1.1f);
            Ring(pos, gold, 2.2f, 0.6f);
            Emit(new P { Burst = 50, Rate = 30, Duration = 0.8f, Life = new Vector2(0.8f, 1.4f), Speed = new Vector2(0.2f, 0.6f), Size = new Vector2(0.06f, 0.14f), Start = new Color(1f, 1f, 0.85f), Mid = gold, End = new Color(1f, 0.7f, 0.2f, 0f), Shape = ParticleSystemShapeType.Circle, Radius = 1.1f, Velocity = new Vector3(0f, 2.5f, 0f) }, pos + Vector3.up * 0.1f);
            Flash(pos + Vector3.up * 2f, gold, 8f, 3f, 0.9f);
        }

        public static void LevelUp(Vector3 pos)
        {
            var gold = new Color(1f, 0.8f, 0.3f);
            if (!Ready) { FxPulse.Ring(pos, gold, 4f, 0.8f); return; }
            Column(pos, gold, 1.6f, 10f, 1.4f);
            Ring(pos, gold, 4f, 0.8f);
            Emit(new P { Burst = 80, Duration = 0.1f, Life = new Vector2(1f, 1.8f), Speed = new Vector2(0.5f, 1.5f), Size = new Vector2(0.08f, 0.16f), Start = Color.white, Mid = gold, End = new Color(1f, 0.5f, 0.1f, 0f), Shape = ParticleSystemShapeType.Circle, Radius = 1.5f, Velocity = new Vector3(0f, 3.5f, 0f) }, pos);
            Flash(pos + Vector3.up * 2f, gold, 10f, 3.5f, 1.2f);
        }

        // =====================================================================================
        // Building blocks for the class abilities
        // =====================================================================================

        /// <summary>
        /// A glowing rune circle that spins up under a caster while a spell is cast: an outer ring turning one way,
        /// an inner one the other, and motes rising from it.
        /// </summary>
        public static void CastCircle(Vector3 pos, Color color, float radius = 1.3f, float duration = 0.6f)
        {
            if (!Ready) { FxPulse.Ring(pos, color, radius, duration); return; }
            var p = new Vector3(pos.x, 0.05f, pos.z);
            Shape(ringMesh, band, p, Quaternion.identity, color, new Vector3(radius * 0.6f, 1f, radius * 0.6f), new Vector3(radius, 1f, radius), duration, 220f);
            Shape(ringMesh, band, p, Quaternion.Euler(0f, 45f, 0f), Color.Lerp(color, Color.white, 0.3f), new Vector3(radius * 0.9f, 1f, radius * 0.9f), new Vector3(radius * 0.55f, 1f, radius * 0.55f), duration, -300f);
            Emit(new P { Rate = 70, Duration = duration * 0.8f, Life = new Vector2(0.4f, 0.8f), Speed = new Vector2(0.05f, 0.2f), Size = new Vector2(0.05f, 0.11f),
                Start = Color.white, Mid = color, End = new Color(color.r, color.g, color.b, 0f), Shape = ParticleSystemShapeType.Circle, Radius = radius * 0.85f,
                Velocity = new Vector3(0f, 2.2f, 0f), Orbital = 2.5f, Max = 120 }, p);
        }

        /// <summary>A ground shockwave: two expanding rings, a ring of dust thrown outward, sparks and a flash.</summary>
        public static void Shockwave(Vector3 pos, Color color, float radius)
        {
            if (!Ready) { FxPulse.Ring(pos, color, radius, 0.5f); return; }
            Ring(pos, color, radius, 0.4f);
            Ring(pos, Color.Lerp(color, Color.white, 0.4f), radius * 1.35f, 0.65f);
            Emit(new P { Burst = 36, Duration = 0.1f, Life = new Vector2(0.5f, 0.9f), Speed = new Vector2(radius * 1.6f, radius * 2.4f), Size = new Vector2(0.5f, 0.9f),
                Start = new Color(0.45f, 0.4f, 0.34f, 0.5f), End = new Color(0.35f, 0.3f, 0.25f, 0f), Shape = ParticleSystemShapeType.Circle, Radius = 0.4f,
                Grow = true, Smoke = true, Drag = 3f }, pos + Vector3.up * 0.25f);
            Emit(new P { Burst = 40, Duration = 0.1f, Life = new Vector2(0.25f, 0.5f), Speed = new Vector2(radius * 2.5f, radius * 4f), Size = new Vector2(0.04f, 0.09f),
                Start = Color.white, Mid = color, End = new Color(color.r, color.g, color.b, 0f), Shape = ParticleSystemShapeType.Circle, Radius = 0.3f, Stretch = true, Drag = 2f, Gravity = 0.6f },
                pos + Vector3.up * 0.4f);
            Flash(pos + Vector3.up * 1.2f, color, radius * 2.5f + 3f, 3f, 0.4f);
        }

        /// <summary>Two crossed slashes (Twin Strike, Shield Bash's sweep).</summary>
        public static void CrossSlash(Vector3 pos, Quaternion facing, float radius, Color color)
        {
            Cleave(pos, facing * Quaternion.Euler(0f, -25f, 0f), radius, color);
            Cleave(pos + Vector3.up * 0.3f, facing * Quaternion.Euler(0f, 25f, 0f), radius * 0.9f, Color.Lerp(color, Color.white, 0.3f));
        }

        /// <summary>A swirl of particles spinning around a point (whirlwind, smoke, auras).</summary>
        public static ParticleSystem Swirl(Vector3 pos, Transform follow, Color color, float radius, float duration, float rate, bool smoky)
        {
            return Emit(new P { Rate = rate, Duration = duration, Life = new Vector2(0.35f, 0.7f), Speed = new Vector2(0f, 0.1f),
                Size = smoky ? new Vector2(0.5f, 0.9f) : new Vector2(0.06f, 0.14f), Start = smoky ? new Color(color.r, color.g, color.b, 0.4f) : Color.white, Mid = color,
                End = new Color(color.r, color.g, color.b, 0f), Shape = ParticleSystemShapeType.Circle, Radius = radius, Orbital = 7f,
                Velocity = new Vector3(0f, smoky ? 0.4f : 1.2f, 0f), Smoke = smoky, Grow = smoky, Max = 300 }, pos, follow);
        }

        /// <summary>A sweeping arc in front of the attacker (Cleave).</summary>
        public static void Cleave(Vector3 pos, Quaternion facing, float radius, Color color)
        {
            if (!Ready)
            {
                var arc = FxPulse.Spawn(pos + facing * Vector3.forward * 1.5f + Vector3.up * 0.9f, color, new Vector3(0.5f, 0.06f, 0.5f), new Vector3(5.5f, 0.06f, 3f), 0.22f, PrimitiveType.Cylinder);
                arc.transform.rotation = facing;
                return;
            }
            Shape(arcMesh, band, pos + Vector3.up * 1f, facing * Quaternion.Euler(0f, 60f, 0f), color,
                new Vector3(radius * 0.6f, 1f, radius * 0.6f), new Vector3(radius, 1f, radius), 0.25f, -480f);
            Emit(new P { Burst = 24, Duration = 0.1f, Life = new Vector2(0.2f, 0.4f), Speed = new Vector2(3f, 7f), Size = new Vector2(0.04f, 0.09f), Start = Color.white, End = new Color(color.r, color.g, color.b, 0f), Shape = ParticleSystemShapeType.Circle, Arc = 150f, Radius = radius * 0.8f, Stretch = true, Drag = 3f }, pos + Vector3.up, null, facing * Quaternion.Euler(0f, -75f, 0f));
        }

        /// <summary>Glowing motes gathering in a caster's hand.</summary>
        public static void CastGlow(Transform hand, Color color, float duration = 0.4f)
        {
            if (!Ready || hand == null) return;
            var ps = Emit(new P { Rate = 70, Duration = duration, Life = new Vector2(0.2f, 0.35f), Speed = new Vector2(-1.2f, -0.6f), Size = new Vector2(0.08f, 0.16f), Start = Color.Lerp(color, Color.white, 0.5f), End = new Color(color.r, color.g, color.b, 0f), Radius = 0.45f }, hand.position, hand);
            if (ps != null) { var main = ps.main; main.stopAction = ParticleSystemStopAction.Destroy; main.simulationSpace = ParticleSystemSimulationSpace.Local; }
            Flash(hand.position, color, 3f, 1.5f, duration);
        }

        /// <summary>Impact sparks or blood.</summary>
        public static void Hit(Vector3 pos, Color color, bool blood, int count = 10)
        {
            if (!Ready) { FxPulse.Sparks(pos, color, Mathf.Min(count, 4)); return; }
            if (blood)
                Emit(new P { Burst = count, Duration = 0.1f, Life = new Vector2(0.3f, 0.6f), Speed = new Vector2(1.5f, 3.5f), Size = new Vector2(0.06f, 0.14f), Start = new Color(color.r, color.g, color.b, 0.9f), End = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 0f), Gravity = 1.5f, Smoke = true }, pos);
            else
                Emit(new P { Burst = count, Duration = 0.1f, Life = new Vector2(0.15f, 0.35f), Speed = new Vector2(3f, 7f), Size = new Vector2(0.04f, 0.08f), Start = Color.white, Mid = color, End = new Color(color.r, color.g, color.b, 0f), Gravity = 1f, Stretch = true }, pos);
        }

        /// <summary>A puff of dust (deaths, heavy landings).</summary>
        public static void Dust(Vector3 pos, float radius, Color? tint = null)
        {
            if (!Ready) return;
            var c = tint ?? new Color(0.45f, 0.38f, 0.3f);
            Emit(new P { Burst = 14, Duration = 0.1f, Life = new Vector2(0.8f, 1.4f), Speed = new Vector2(radius * 0.6f, radius * 1.4f), Size = new Vector2(0.5f, 0.9f) * radius, Start = new Color(c.r, c.g, c.b, 0.4f), End = new Color(c.r, c.g, c.b, 0f), Shape = ParticleSystemShapeType.Circle, Radius = radius * 0.4f, Grow = true, Smoke = true, Drag = 2f }, pos + Vector3.up * 0.2f);
        }

        /// <summary>Lingering fire on the ground (meteor impacts).</summary>
        public static void GroundFire(Vector3 pos, float radius, float duration)
        {
            if (!Ready) return;
            Emit(new P { Rate = 45 * radius, Duration = duration, Life = new Vector2(0.4f, 0.8f), Speed = new Vector2(0.3f, 1f), Size = new Vector2(0.4f, 0.9f), Start = FireA, Mid = FireB, End = FireC, Shape = ParticleSystemShapeType.Circle, Radius = radius, Velocity = new Vector3(0f, 1.2f, 0f), Max = 400 }, pos);
            Emit(new P { Rate = 10 * radius, Duration = duration, Life = new Vector2(1.2f, 2f), Speed = new Vector2(0.2f, 0.6f), Size = new Vector2(0.8f, 1.4f), Start = SmokeA, End = SmokeB, Shape = ParticleSystemShapeType.Circle, Radius = radius, Velocity = new Vector3(0f, 1.2f, 0f), Grow = true, Smoke = true }, pos + Vector3.up * 0.5f);
        }

        // =====================================================================================
        // Lights
        // =====================================================================================

        public static void Flash(Vector3 pos, Color color, float range, float intensity, float duration)
        {
            var go = new GameObject("FX light");
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            go.AddComponent<FxLight>().Init(l, intensity, duration);
        }

        static void AddLight(Transform t, Color color, float range, float intensity)
        {
            var go = new GameObject("FX glow");
            go.transform.SetParent(t, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }
    }

    /// <summary>Scales and fades an effect mesh (ring, arc, column), then removes it.</summary>
    public class FxMesh : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        MeshRenderer r;
        MaterialPropertyBlock block;
        Color color;
        Vector3 from, to;
        float duration, t, spin;

        public void Init(MeshRenderer renderer, Color c, Vector3 fromScale, Vector3 toScale, float time, float spinDegPerSec)
        {
            r = renderer;
            color = c;
            from = fromScale;
            to = toScale;
            duration = time;
            spin = spinDegPerSec;
            block = new MaterialPropertyBlock();
            Apply(0f);
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            Apply(k);
            if (spin != 0f) transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.Self);
            if (t >= duration) Destroy(gameObject);
        }

        void Apply(float k)
        {
            float ease = 1f - (1f - k) * (1f - k);
            transform.localScale = Vector3.Lerp(from, to, ease);
            float alpha = Mathf.Clamp01(k * 8f) * (1f - k * k);
            block.SetColor(ColorId, new Color(color.r, color.g, color.b, alpha));
            r.SetPropertyBlock(block);
        }
    }

    /// <summary>A point light that fades out and removes itself.</summary>
    public class FxLight : MonoBehaviour
    {
        Light l;
        float start, duration, t;

        public void Init(Light light, float intensity, float time)
        {
            l = light;
            start = intensity;
            duration = time;
        }

        void Update()
        {
            t += Time.deltaTime;
            l.intensity = start * (1f - Mathf.Clamp01(t / duration));
            if (t >= duration) Destroy(gameObject);
        }
    }

    /// <summary>An ice crystal that bursts out of the ground, lingers, and sinks back (Frost Nova).</summary>
    public class IceSpike : MonoBehaviour
    {
        float height, delay, t;

        public static void Spawn(Vector3 pos, float height, float delay)
        {
            var ice = new Color(0.6f, 0.85f, 1f);
            var go = Factory.Prim(PrimitiveType.Cube, null, pos, Vector3.zero, ice, false, Mat.Glow(ice * 0.6f));
            go.name = "Ice spike";
            go.transform.rotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f)) * Quaternion.Euler(0f, 45f, 0f);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var s = go.AddComponent<IceSpike>();
            s.height = height;
            s.delay = delay;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = t - delay;
            if (k < 0f) return;
            float grow = Mathf.Clamp01(k / 0.08f);
            float sink = Mathf.Clamp01((k - 1.4f) / 0.5f);
            float w = 0.22f * height * (1f - sink);
            transform.localScale = new Vector3(w, height * grow * (1f - sink * 0.6f), w);
            if (sink >= 1f) Destroy(gameObject);
        }
    }
}
