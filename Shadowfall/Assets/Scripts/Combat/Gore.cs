using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Diablo-style gore: blood sprays from hits in the direction of the blow, bursts and chunks on death, and splatter
    /// and pools that stay on the ground (drying darker over a few minutes) before fading away.
    /// Skeletons shed bone chips and dust, golems rubble, goblins bleed dark green, zombies something worse.
    /// All ground splats are drawn as one mesh with the Shadowfall/Decal shader; the texture atlas is generated here.
    /// </summary>
    public class Gore : MonoBehaviour
    {
        public enum Kind { Flesh, Ichor, Rot, Bone, Stone }

        static readonly Color[] Fresh =
        {
            new Color(0.5f, 0.02f, 0.02f),    // flesh: red
            new Color(0.2f, 0.34f, 0.03f),    // ichor: goblin green
            new Color(0.3f, 0.12f, 0.04f),    // rot: brown-red
            new Color(0.13f, 0.11f, 0.09f),   // bone: grave dust
            new Color(0.3f, 0.28f, 0.25f),    // stone: rock dust
        };
        static readonly Color[] Spray =
        {
            new Color(0.75f, 0.04f, 0.03f), new Color(0.35f, 0.55f, 0.05f), new Color(0.45f, 0.2f, 0.06f),
            new Color(0.92f, 0.9f, 0.82f), new Color(0.55f, 0.52f, 0.48f),
        };

        /// <summary>The kind of gore a monster makes, by name.</summary>
        public static Kind KindOf(string monster)
        {
            if (monster.StartsWith("Skeleton") || monster == "Lich King" || monster == "Crypt Lord" || monster.EndsWith("Wraith")
                || monster == "Ember Skeleton" || monster == "The Ashen King") return Kind.Bone;
            if (monster.EndsWith("Golem") || monster.EndsWith("Colossus")) return Kind.Stone;
            if (monster.StartsWith("Goblin")) return Kind.Ichor;
            if (monster == "Zombie" || monster == "Ash Ghoul") return Kind.Rot;
            return Kind.Flesh;
        }

        static bool Bleeds(Kind k) => k == Kind.Flesh || k == Kind.Ichor || k == Kind.Rot;

        // ------------------------------------------------------------------ public API

        /// <summary>Blood from a hit, sprayed away from the attacker. Severity 0..1 (a critical hit is 1).</summary>
        public static void Hit(Vector3 center, Vector3 dir, Kind kind, float severity)
        {
            if (GameSettings.Gore == 0 || !SpellFx.Ready) return;
            var g = Instance;
            dir = FlatDir(dir);
            float extra = GameSettings.Gore == 2 ? 1.6f : 1f;
            var c = Spray[(int)kind];
            if (Bleeds(kind))
            {
                SpellFx.Emit(new SpellFx.P
                {
                    Burst = Mathf.RoundToInt((10 + 18 * severity) * extra), Duration = 0.1f, Life = new Vector2(0.35f, 0.75f),
                    Speed = new Vector2(0.8f, 2.4f), Size = new Vector2(0.05f, 0.13f), Start = c, Mid = c * 0.8f, End = new Color(c.r * 0.4f, c.g * 0.3f, c.b * 0.3f, 0f),
                    Gravity = 2.4f, Radius = 0.15f, Velocity = dir * (2.6f + 2f * severity) + Vector3.up * 1.4f, Smoke = true, Max = 60,
                }, center);
                if (severity > 0.5f || Random.value < 0.35f)
                    SpellFx.Emit(new SpellFx.P
                    {
                        Burst = 4, Duration = 0.1f, Life = new Vector2(0.4f, 0.7f), Speed = new Vector2(0.2f, 0.6f), Size = new Vector2(0.35f, 0.6f),
                        Start = new Color(c.r, c.g, c.b, 0.45f), End = new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 0f), Radius = 0.15f,
                        Velocity = dir * 1.2f, Grow = true, Smoke = true, Max = 8,
                    }, center);
                int splats = (severity > 0.5f ? 3 : Random.value < 0.6f ? 1 : 2) + (GameSettings.Gore == 2 ? 1 : 0);
                for (int i = 0; i < splats; i++)
                {
                    float d = Random.Range(0.2f, 1.2f + 1.2f * severity);
                    var at = Ground(center) + Quaternion.Euler(0f, Random.Range(-28f, 28f), 0f) * dir * d;
                    g.Splat(at, Random.Range(0.3f, 0.6f) * (1f + 0.5f * severity), dir, kind, 0.08f + d * 0.12f);
                }
            }
            else
            {
                // bone chips / stone grit, and only a little dust on the ground
                SpellFx.Emit(new SpellFx.P
                {
                    Burst = Mathf.RoundToInt((6 + 10 * severity) * extra), Duration = 0.1f, Life = new Vector2(0.4f, 0.8f), Speed = new Vector2(1.5f, 3.5f),
                    Size = new Vector2(0.04f, 0.1f), Start = c, End = new Color(c.r, c.g, c.b, 0f), Gravity = 2.5f, Radius = 0.12f,
                    Velocity = dir * 2f + Vector3.up * 1.2f, Smoke = true, Max = 40,
                }, center);
                if (Random.value < 0.3f + 0.4f * severity)
                    g.Splat(Ground(center) + dir * Random.Range(0.3f, 1f), Random.Range(0.25f, 0.45f), dir, kind, 0.2f);
            }
        }

        /// <summary>A monster dies: a burst, splatter all around (more in the direction of the killing blow), a pool under the body, chunks on big kills.</summary>
        public static void Death(Vector3 center, Vector3 dir, Kind kind, float bodySize, bool overkill, bool boss)
        {
            if (GameSettings.Gore == 0 || !SpellFx.Ready) return;
            var g = Instance;
            dir = FlatDir(dir);
            bool extra = GameSettings.Gore == 2;
            var c = Spray[(int)kind];
            var ground = Ground(center);
            float scale = Mathf.Clamp(bodySize, 0.6f, 3f) * (boss ? 1.5f : 1f);

            SpellFx.Emit(new SpellFx.P
            {
                Burst = Mathf.RoundToInt((30 + (overkill ? 30 : 0)) * scale * (extra ? 1.5f : 1f)), Duration = 0.1f, Life = new Vector2(0.5f, 1f),
                Speed = new Vector2(1.5f, 4.5f) * Mathf.Sqrt(scale), Size = new Vector2(0.06f, 0.16f), Start = c, Mid = c * 0.8f,
                End = new Color(c.r * 0.4f, c.g * 0.3f, c.b * 0.3f, 0f), Gravity = 2.2f, Radius = 0.3f * scale,
                Velocity = dir * 1.5f + Vector3.up * 2.2f, Smoke = true, Max = 200,
            }, center);
            SpellFx.Emit(new SpellFx.P
            {
                Burst = Mathf.RoundToInt(6 * scale), Duration = 0.1f, Life = new Vector2(0.6f, 1.1f), Speed = new Vector2(0.3f, 1f), Size = new Vector2(0.5f, 0.9f) * Mathf.Sqrt(scale),
                Start = new Color(c.r, c.g, c.b, 0.5f), End = new Color(c.r * 0.4f, c.g * 0.4f, c.b * 0.4f, 0f), Radius = 0.3f * scale, Grow = true, Smoke = true, Max = 30,
            }, center);

            if (Bleeds(kind))
            {
                int splats = Mathf.RoundToInt((5 + (overkill ? 4 : 0)) * Mathf.Sqrt(scale) * (extra ? 1.6f : 1f));
                for (int i = 0; i < splats; i++)
                {
                    bool forward = i % 2 == 0;
                    var d = forward ? Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * dir : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                    float dist = Random.Range(0.3f, forward ? 3.2f : 1.6f) * Mathf.Sqrt(scale);
                    g.Splat(ground + d * dist, Random.Range(0.45f, 0.9f) * Mathf.Sqrt(scale), d, kind, 0.1f + dist * 0.1f);
                }
                // the pool spreads under the body once it has fallen
                g.Splat(ground + dir * 0.2f * scale, Random.Range(1.1f, 1.5f) * scale, dir, kind, 0.7f, 3.5f, true);
            }
            else
            {
                for (int i = 0; i < 3; i++)
                    g.Splat(ground + Random.insideUnitSphere.Flat() * scale, Random.Range(0.5f, 0.9f) * scale, dir, kind, 0.2f);
            }

            if (overkill || boss || extra || kind == Kind.Bone || kind == Kind.Stone)
            {
                int chunks = Mathf.RoundToInt((overkill ? 9 : 5) * Mathf.Sqrt(scale) * (extra ? 1.5f : 1f));
                for (int i = 0; i < chunks; i++) g.Gib(center + Random.insideUnitSphere * 0.3f * scale, dir, kind, scale);
            }
        }

        /// <summary>A badly wounded creature leaves drops behind it.</summary>
        public static void Drip(Vector3 at, Kind kind)
        {
            if (GameSettings.Gore == 0 || !Bleeds(kind) || !SpellFx.Ready) return;
            Instance.Splat(Ground(at) + Random.insideUnitSphere.Flat() * 0.25f, Random.Range(0.15f, 0.3f), Random.insideUnitSphere.Flat(), kind, 0f);
        }

        /// <summary>The hero bleeds a little when hurt.</summary>
        public static void PlayerHit(Vector3 center, Vector3 dir, float fraction)
        {
            if (GameSettings.Gore == 0 || fraction < 0.03f) return;
            Hit(center, dir, Kind.Flesh, Mathf.Clamp01(fraction * 3f) * 0.6f);
        }

        /// <summary>Removes all blood and chunks (leaving the world, changing dungeon level).</summary>
        public static void Clear()
        {
            if (instance == null) return;
            instance.decals.Clear();
            instance.pending.Clear();
            foreach (var gib in instance.gibs) if (gib.T != null) Destroy(gib.T.gameObject);
            instance.gibs.Clear();
            instance.dirty = true;
        }

        // ------------------------------------------------------------------ internals

        static Gore instance;
        static Gore Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("Gore");
                instance = go.AddComponent<Gore>();
                instance.Setup();
                return instance;
            }
        }

        static Vector3 FlatDir(Vector3 d)
        {
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        }

        static Vector3 Ground(Vector3 p) => new Vector3(p.x, 0f, p.z);

        class Decal
        {
            public Vector3 Pos;
            public float Size, Angle, Born, Grow, Life;
            public int Variant;
            public Color Color;
        }

        class PendingDecal { public Decal D; public float At; }

        class GibPiece
        {
            public Transform T;
            public Vector3 Vel, Spin;
            public float Born, RestAt = -1f, Half;
            public Kind Kind;
            public bool Bounced;
        }

        readonly List<Decal> decals = new List<Decal>();
        readonly List<PendingDecal> pending = new List<PendingDecal>();
        readonly List<GibPiece> gibs = new List<GibPiece>();
        Mesh mesh;
        Material material;
        Vector3[] verts;
        Vector2[] uvs;
        Color[] colors;
        int[] tris;
        Vector3[] normals;
        bool dirty;
        float nextAge;

        int MaxDecals => (GameSettings.Effects.Value == 0 ? 160 : 400) * (GameSettings.Gore == 2 ? 3 : 2) / 2;
        float DecalLife => GameSettings.Gore == 2 ? 360f : 180f;
        const float FadeTime = 20f, DryTime = 45f;
        const int MaxGibs = 80;

        void Setup()
        {
            var shader = Resources.Load<Shader>("Shaders/ShadowfallDecal");
            if (shader == null) shader = Shader.Find("Shadowfall/Decal");
            if (shader == null || !shader.isSupported) shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { mainTexture = MakeAtlas(), name = "Gore" };
            mesh = new Mesh { name = "GoreDecals" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
        }

        /// <summary>Adds a splat after <paramref name="delay"/> (spray in flight), growing over <paramref name="grow"/> seconds.</summary>
        void Splat(Vector3 at, float size, Vector3 dir, Kind kind, float delay, float grow = 0.18f, bool pool = false)
        {
            var grid = WorldGrid.Instance;
            if (grid != null && !grid.IsWalkable(at)) return; // not on walls, rocks or water
            var c = Fresh[(int)kind];
            float v = Random.Range(0.85f, 1.15f);
            var d = new Decal
            {
                Pos = at, Size = size, Grow = grow, Life = DecalLife * Random.Range(0.85f, 1.1f),
                // pools are round; small splats are droplets; the rest streak in the spray direction
                Variant = pool ? 0 : size < 0.35f ? 1 : Random.Range(2, 4),
                Angle = pool || size < 0.35f ? Random.Range(0f, 360f) : Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + Random.Range(-12f, 12f),
                Color = new Color(c.r * v, c.g * v, c.b * v, pool ? 0.92f : 0.95f),
            };
            pending.Add(new PendingDecal { D = d, At = Time.time + delay });
        }

        void Gib(Vector3 at, Vector3 dir, Kind kind, float scale)
        {
            var color = kind == Kind.Bone ? new Color(0.85f, 0.82f, 0.72f) : kind == Kind.Stone ? new Color(0.45f, 0.42f, 0.38f)
                      : kind == Kind.Ichor ? new Color(0.25f, 0.3f, 0.08f) : kind == Kind.Rot ? new Color(0.3f, 0.15f, 0.08f) : new Color(0.42f, 0.05f, 0.04f);
            float s = Random.Range(0.07f, 0.16f) * Mathf.Sqrt(scale);
            var size = kind == Kind.Bone ? new Vector3(s * 0.5f, s * 0.5f, s * 2.2f) : new Vector3(s, s * Random.Range(0.6f, 1f), s * Random.Range(0.8f, 1.4f));
            var go = Factory.Prim(kind == Kind.Stone ? PrimitiveType.Cube : kind == Kind.Bone ? PrimitiveType.Capsule : PrimitiveType.Sphere, transform, at, size, color);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var v = (Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * dir) * Random.Range(1.5f, 4.5f) + Vector3.up * Random.Range(2.5f, 5f);
            gibs.Add(new GibPiece { T = go.transform, Vel = v, Spin = Random.insideUnitSphere * 720f, Born = Time.time, Half = size.y * 0.5f, Kind = kind });
            while (gibs.Count > MaxGibs)
            {
                if (gibs[0].T != null) Destroy(gibs[0].T.gameObject);
                gibs.RemoveAt(0);
            }
        }

        void Update()
        {
            float now = Time.time, dt = Time.deltaTime;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].At > now) continue;
                var d = pending[i].D;
                d.Born = now;
                decals.Add(d);
                pending.RemoveAt(i);
                dirty = true;
            }
            int max = MaxDecals;
            if (decals.Count > max) { decals.RemoveRange(0, decals.Count - max); dirty = true; }

            // flying chunks: fall, bounce once (leaving a splat), rest, then sink away
            for (int i = gibs.Count - 1; i >= 0; i--)
            {
                var g = gibs[i];
                if (g.T == null) { gibs.RemoveAt(i); continue; }
                if (g.RestAt < 0f)
                {
                    g.Vel += Vector3.down * 14f * dt;
                    var p = g.T.position + g.Vel * dt;
                    g.T.Rotate(g.Spin * dt, Space.World);
                    if (p.y <= g.Half)
                    {
                        p.y = g.Half;
                        if (!g.Bounced && g.Vel.y < -2f)
                        {
                            g.Bounced = true;
                            g.Vel = new Vector3(g.Vel.x * 0.45f, -g.Vel.y * 0.3f, g.Vel.z * 0.45f);
                            g.Spin *= 0.5f;
                            if (Bleeds(g.Kind)) Splat(p, Random.Range(0.18f, 0.32f), g.Vel, g.Kind, 0f);
                        }
                        else { g.RestAt = now; g.Vel = Vector3.zero; }
                    }
                    g.T.position = p;
                }
                else if (now - g.RestAt > 25f)
                {
                    g.T.position += Vector3.down * 0.05f * dt;
                    if (now - g.RestAt > 30f) { Destroy(g.T.gameObject); gibs.RemoveAt(i); }
                }
            }

            bool growing = false;
            for (int i = decals.Count - 1; i >= 0; i--)
            {
                if (now - decals[i].Born > decals[i].Life + FadeTime) { decals.RemoveAt(i); dirty = true; continue; }
                if (now - decals[i].Born < decals[i].Grow) growing = true;
            }
            // Growing pools are redrawn 15 times a second, not every frame (the whole mesh of up to 600 splats is rebuilt)
            if (dirty || (growing && now >= nextGrow) || now >= nextAge)
            {
                nextAge = now + 1f;
                nextGrow = now + 0.066f;
                dirty = false;
                Rebuild(now);
            }
        }

        float nextGrow;

        void Rebuild(float now)
        {
            int n = decals.Count;
            if (verts == null || verts.Length < n * 4)
            {
                int cap = Mathf.NextPowerOfTwo(Mathf.Max(64, n)) * 4;
                verts = new Vector3[cap];
                uvs = new Vector2[cap];
                colors = new Color[cap];
                tris = new int[cap / 4 * 6];
                normals = new Vector3[cap];
                for (int i = 0; i < cap; i++) normals[i] = Vector3.up;
            }
            mesh.Clear();
            if (n == 0) return;
            for (int i = 0; i < n; i++)
            {
                var d = decals[i];
                float age = now - d.Born;
                float k = d.Grow > 0f ? Mathf.Clamp01(age / d.Grow) : 1f;
                float size = d.Size * (0.35f + 0.65f * (1f - (1f - k) * (1f - k)));
                // fresh and wet, then drying darker and browner, then fading away
                float dry = Mathf.Clamp01(age / DryTime);
                var c = Color.Lerp(d.Color, new Color(d.Color.r * 0.42f + 0.03f, d.Color.g * 0.45f + 0.015f, d.Color.b * 0.5f + 0.01f, d.Color.a), dry);
                c.a *= 1f - Mathf.Clamp01((age - d.Life) / FadeTime);
                var rot = Quaternion.Euler(0f, d.Angle, 0f);
                var right = rot * Vector3.right * size * 0.5f;
                var fwd = rot * Vector3.forward * size * 0.5f;
                var p = d.Pos + Vector3.up * (0.012f + (i % 8) * 0.0005f);
                int v = i * 4;
                verts[v] = p - right - fwd;
                verts[v + 1] = p - right + fwd;
                verts[v + 2] = p + right + fwd;
                verts[v + 3] = p + right - fwd;
                float u0 = (d.Variant % 2) * 0.5f, v0 = (d.Variant / 2) * 0.5f;
                uvs[v] = new Vector2(u0, v0);
                uvs[v + 1] = new Vector2(u0, v0 + 0.5f);
                uvs[v + 2] = new Vector2(u0 + 0.5f, v0 + 0.5f);
                uvs[v + 3] = new Vector2(u0 + 0.5f, v0);
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = c;
                int t = i * 6;
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }
            mesh.SetVertices(verts, 0, n * 4);
            mesh.SetUVs(0, uvs, 0, n * 4);
            mesh.SetColors(colors, 0, n * 4);
            mesh.SetNormals(normals, 0, n * 4);
            mesh.SetTriangles(tris, 0, n * 6, 0, false);
            mesh.RecalculateBounds();
            var b = mesh.bounds;
            b.Expand(new Vector3(0f, 1f, 0f));
            mesh.bounds = b;
        }

        // ------------------------------------------------------------------ the splat texture

        /// <summary>
        /// A 2x2 atlas of splats, made from metaballs: 0 a pool with a ragged edge, 1 a cluster of droplets,
        /// 2 and 3 splashes whose spikes streak along +V (the spray direction), with drops at their tips.
        /// </summary>
        static Texture2D MakeAtlas()
        {
            const int S = 128, N = S * 2;
            var px = new Color32[N * N];
            var rng = new System.Random(1337);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int variant = 0; variant < 4; variant++)
            {
                var balls = new List<Vector3>(); // x, y, radius in 0..1 cell space
                if (variant == 0)
                {
                    // pool: a big irregular blob with a few drops around it
                    balls.Add(new Vector3(0.5f, 0.5f, 0.24f));
                    for (int i = 0; i < 12; i++) { float a = R(0f, 6.283f), d = R(0.1f, 0.22f); balls.Add(new Vector3(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d, R(0.06f, 0.12f))); }
                    for (int i = 0; i < 5; i++) { float a = R(0f, 6.283f), d = R(0.33f, 0.4f); balls.Add(new Vector3(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d, R(0.012f, 0.025f))); }
                }
                else if (variant == 1)
                {
                    for (int i = 0; i < 9; i++) balls.Add(new Vector3(R(0.2f, 0.8f), R(0.2f, 0.8f), R(0.025f, 0.07f)));
                }
                else
                {
                    // splash: a core with tapering spikes, longest toward +V (the spray direction), and drops at their tips
                    float cy = 0.36f, core = R(0.14f, 0.17f);
                    balls.Add(new Vector3(0.5f, cy, core));
                    int spikes = variant == 2 ? 9 : 12;
                    for (int k = 0; k < spikes; k++)
                    {
                        float a = k / (float)spikes * 6.283f + R(-0.15f, 0.15f);
                        float fwd = Mathf.Max(0f, Mathf.Sin(a));
                        float length = R(0.04f, 0.16f) + fwd * R(0.1f, 0.34f);
                        int steps = Mathf.Max(1, (int)(length / 0.025f));
                        float r0 = R(0.03f, 0.06f);
                        for (int st = 1; st <= steps; st++)
                        {
                            float t = st / (float)steps, d = core * 0.6f + t * length;
                            balls.Add(new Vector3(0.5f + Mathf.Cos(a) * d, cy + Mathf.Sin(a) * d, r0 * (1f - 0.6f * t)));
                        }
                        if (rng.NextDouble() < 0.7)
                        {
                            float d = core * 0.6f + length + R(0.03f, 0.08f);
                            balls.Add(new Vector3(0.5f + Mathf.Cos(a) * d, cy + Mathf.Sin(a) * d, R(0.012f, 0.028f)));
                        }
                    }
                }
                int ox = (variant % 2) * S, oy = (variant / 2) * S;
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float fx = (x + 0.5f) / S, fy = (y + 0.5f) / S;
                        // metaballs blended with a squared sum: smooth joins, but spikes stay thin
                        float f = 0f;
                        foreach (var b in balls)
                        {
                            float dx = fx - b.x, dy = fy - b.y;
                            float m = b.z * b.z / (dx * dx + dy * dy + 0.00001f);
                            f += m * m;
                        }
                        f = Mathf.Sqrt(f) * (0.9f + 0.2f * Mathf.PerlinNoise(fx * 12f + variant * 13f, fy * 12f));
                        float edge = Mathf.Clamp01((f - 0.95f) / 0.12f);
                        // keep away from the cell border so atlas neighbours never bleed in
                        float border = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy));
                        edge *= Mathf.Clamp01(border * 25f);
                        float inner = Mathf.Clamp01((f - 1.1f) * 0.6f);
                        float shade = 0.7f + 0.3f * inner + 0.05f * Mathf.PerlinNoise(fx * 23f, fy * 23f + variant);
                        byte s = (byte)(Mathf.Clamp01(shade) * 255f);
                        px[(oy + y) * N + ox + x] = new Color32(s, s, s, (byte)(edge * 255f));
                    }
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "GoreSplats", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }

    static class GoreVectorExt
    {
        /// <summary>The vector with y = 0.</summary>
        public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
