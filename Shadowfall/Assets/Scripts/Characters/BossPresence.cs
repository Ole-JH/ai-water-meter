using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// What makes a world boss (server/worldboss.js) more than a big monster: its entrance (it climbs out of the ground
    /// its land is made of: a thicket, frozen earth, sand, a lava crack), three armour plates that break off at three
    /// quarters, a half and a quarter of its health (the server takes some armour each time and quickens its slams), the
    /// glow of its rage once the last is gone, the craters its slams leave, and a corpse that lies where it fell for a
    /// minute and a half. Added to the boss's <see cref="Enemy"/> when it is spawned.
    /// </summary>
    public class BossPresence : MonoBehaviour
    {
        /// <summary>How each boss looks doing it.</summary>
        class Style
        {
            public Color Plate, Glow, Dust, Crack;
            public bool GlowingPlates;
            public string RiseSound;
            public string Banner;      // "{0}'s ... cracks"
        }

        static readonly Dictionary<string, Style> styles = new Dictionary<string, Style>
        {
            { "Old Bramblehide", new Style { Plate = new Color(0.36f, 0.27f, 0.17f), Glow = new Color(0.55f, 0.9f, 0.3f), Dust = new Color(0.35f, 0.42f, 0.22f),
                Crack = new Color(0.16f, 0.12f, 0.08f), RiseSound = "leaves", Banner = "{0}'s bark armour splits!" } },
            { "Hrimgar the Mountain", new Style { Plate = new Color(0.72f, 0.88f, 1f), Glow = new Color(0.45f, 0.8f, 1f), Dust = new Color(0.85f, 0.92f, 1f),
                Crack = new Color(0.35f, 0.5f, 0.65f), GlowingPlates = true, RiseSound = "shatter", Banner = "{0}'s ice armour shatters!" } },
            { "Gorvash the Dune Reaver", new Style { Plate = new Color(0.72f, 0.5f, 0.25f), Glow = new Color(1f, 0.65f, 0.25f), Dust = new Color(0.85f, 0.7f, 0.45f),
                Crack = new Color(0.55f, 0.42f, 0.25f), RiseSound = "rubble", Banner = "{0}'s bronze plate falls away!" } },
            { "The Pyre Colossus", new Style { Plate = new Color(0.12f, 0.1f, 0.1f), Glow = new Color(1f, 0.4f, 0.08f), Dust = new Color(0.3f, 0.22f, 0.2f),
                Crack = new Color(1f, 0.35f, 0.05f), GlowingPlates = false, RiseSound = "explosion", Banner = "{0}'s obsidian shell cracks open!" } },
        };

        const float RiseSeconds = 3.2f, CorpseSeconds = 90f;
        const int MaxCraters = 14;
        static readonly Queue<GameObject> craters = new Queue<GameObject>();

        Enemy enemy;
        Transform model;
        Style style;
        float height, riseT = -1f;
        readonly List<GameObject> plates = new List<GameObject>();
        Light rage;
        bool dead;

        public static void Attach(Enemy e, Transform model, float height)
        {
            if (!styles.TryGetValue(e.Def.Name, out var st)) return;
            var b = e.gameObject.AddComponent<BossPresence>();
            b.enemy = e;
            b.model = model;
            b.style = st;
            b.height = height;
            b.BuildPlates();
            var wb = WorldBoss.Current;
            bool ours = wb != null && wb.name == e.Def.Name;
            if (ours) b.BreakTo(wb.pl, false);
            // A boss that rose a moment ago: watch it climb out (late-comers find it standing).
            if (ours && Time.time - WorldBoss.RoseAt < 4f) b.Entrance();
        }

        // ------------------------------------------------------------------ armour

        void BuildPlates()
        {
            var parent = model != null ? model : transform;
            float h = height;
            bool beast = enemy.Def.Name == "Old Bramblehide"; // on all fours: plates along the back
            var at = beast
                ? new[] { new Vector3(0f, 0.62f, 0.22f), new Vector3(0f, 0.66f, -0.05f), new Vector3(0f, 0.6f, -0.3f) }
                : new[] { new Vector3(0f, 0.6f, 0.13f), new Vector3(-0.2f, 0.78f, 0f), new Vector3(0.2f, 0.78f, 0f) };
            var size = beast
                ? new[] { new Vector3(0.34f, 0.1f, 0.22f), new Vector3(0.38f, 0.12f, 0.24f), new Vector3(0.32f, 0.1f, 0.22f) }
                : new[] { new Vector3(0.3f, 0.26f, 0.06f), new Vector3(0.16f, 0.07f, 0.18f), new Vector3(0.16f, 0.07f, 0.18f) };
            var mat = style.GlowingPlates ? Mat.Glow(style.Plate) : null;
            for (int i = 0; i < 3; i++)
            {
                var go = Factory.Prim(PrimitiveType.Cube, parent, Vector3.zero, size[i] * h, style.Plate, false, mat);
                go.name = "BossPlate";
                // placed in world terms (the model may be scaled), then kept on the model
                go.transform.position = transform.position + transform.rotation * (at[i] * h);
                go.transform.rotation = transform.rotation * Quaternion.Euler(beast ? 0f : (i == 0 ? -8f : 0f), 0f, i == 1 ? 18f : i == 2 ? -18f : 0f);
                go.transform.localScale = Vector3.Scale(size[i] * h, new Vector3(1f / Mathf.Max(0.01f, parent.lossyScale.x), 1f / Mathf.Max(0.01f, parent.lossyScale.y), 1f / Mathf.Max(0.01f, parent.lossyScale.z)));
                // studs: two rivets per plate
                foreach (float sx in new[] { -0.3f, 0.3f })
                    Factory.Prim(PrimitiveType.Sphere, go.transform, new Vector3(sx, 0.5f, 0.5f), Vector3.one * 0.2f, style.Plate * 0.6f);
                plates.Add(go);
            }
        }

        /// <summary>Down to <paramref name="left"/> plates: the rest break off (with a show when <paramref name="loud"/>).</summary>
        public void BreakTo(int left, bool loud = true)
        {
            left = Mathf.Clamp(left, 0, 3);
            while (plates.Count > left)
            {
                var go = plates[plates.Count - 1];
                plates.RemoveAt(plates.Count - 1);
                if (go == null) continue;
                if (!loud) { Destroy(go); continue; }
                Fling(go, 2.5f);
            }
            if (!loud) { if (left == 0) Rage(); return; }
            SpellFx.Hit(enemy.Center, style.Glow, false, 24);
            SpellFx.Dust(transform.position, height * 0.5f, style.Dust);
            Sfx.Play("shatter", enemy.Center, 1f, 0.05f, 70f);
            Sfx.Play("hit_armor", enemy.Center, 1f, 0.1f, 70f);
            var p = Player.I;
            if (p != null && Factory.FlatDistance(p.transform.position, transform.position) < 30f) CameraRig.Shake(0.25f);
            GameUI.Banner(string.Format(style.Banner, enemy.Def.Name), WorldBoss.Color);
            if (left == 0) Rage();
        }

        void Fling(GameObject go, float force)
        {
            go.transform.SetParent(null, true);
            var fall = go.AddComponent<FallingPiece>();
            var away = Factory.Flat(go.transform.position - transform.position);
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            fall.Velocity = Factory.Flat(away).normalized * Random.Range(2f, 4f) * force * 0.5f + Vector3.up * Random.Range(3f, 5.5f);
            fall.Spin = Random.insideUnitSphere * 400f;
        }

        /// <summary>No plates left: it burns with its colour (the server makes it hit harder and move faster).</summary>
        void Rage()
        {
            if (rage != null) return;
            rage = new GameObject("BossRage").AddComponent<Light>();
            rage.transform.SetParent(transform, false);
            rage.transform.localPosition = Vector3.up * height * 0.6f;
            rage.type = LightType.Point;
            rage.color = style.Glow;
            rage.range = height * 2.5f;
            rage.intensity = 2f;
            rage.shadows = LightShadows.None;
            SpellFx.Emit(new SpellFx.P
            {
                Rate = 30, Duration = 100000f, Life = new Vector2(0.6f, 1.2f), Speed = new Vector2(0.2f, 0.8f),
                Size = new Vector2(0.15f, 0.35f), Start = new Color(style.Glow.r, style.Glow.g, style.Glow.b, 0.9f), End = new Color(style.Glow.r, style.Glow.g, style.Glow.b, 0f),
                Shape = ParticleSystemShapeType.Circle, Radius = height * 0.3f, Velocity = new Vector3(0f, 2f, 0f),
            }, transform.position, transform);
        }

        // ------------------------------------------------------------------ entrance

        void Entrance()
        {
            riseT = 0f;
            if (model != null) model.localPosition = new Vector3(model.localPosition.x, -height, model.localPosition.z);
            var at = transform.position;
            Fissure(at, height * 0.55f, 120f);
            SpellFx.Dust(at, height * 0.7f, style.Dust);
            SpellFx.Ring(at + Vector3.up * 0.05f, style.Glow, height * 0.8f, 1.2f);
            Sfx.Play(style.RiseSound, at, 1f, 0.05f, 80f);
            Sfx.Play("roar", at, 1f, 0.05f, 90f);
            var p = Player.I;
            if (p != null && Factory.FlatDistance(p.transform.position, at) < 40f) CameraRig.Shake(0.5f);
            // what its land throws up as it climbs out
            SpellFx.Emit(new SpellFx.P
            {
                Rate = 60, Duration = RiseSeconds, Life = new Vector2(0.8f, 1.6f), Speed = new Vector2(2f, 5f),
                Size = new Vector2(0.12f, 0.3f) * Mathf.Max(1f, height / 3f), Start = new Color(style.Dust.r, style.Dust.g, style.Dust.b, 1f),
                End = new Color(style.Dust.r, style.Dust.g, style.Dust.b, 0f), Gravity = 1.2f, Shape = ParticleSystemShapeType.Circle, Radius = height * 0.4f,
            }, at + Vector3.up * 0.2f);
        }

        void Update()
        {
            if (riseT >= 0f && model != null)
            {
                riseT += Time.deltaTime / RiseSeconds;
                float t = Mathf.Clamp01(riseT);
                // heaves itself up in surges, not a lift
                float y = -height * (1f - Mathf.SmoothStep(0f, 1f, t)) + Mathf.Sin(t * Mathf.PI * 5f) * 0.08f * height * (1f - t);
                model.localPosition = new Vector3(model.localPosition.x, y, model.localPosition.z);
                if (Random.value < 0.15f) SpellFx.Dust(transform.position, height * 0.3f, style.Dust);
                if (t >= 1f) { riseT = -1f; model.localPosition = new Vector3(model.localPosition.x, 0f, model.localPosition.z); }
            }
            if (rage != null && !dead) rage.intensity = 1.6f + Mathf.Sin(Time.time * 6f) * 0.6f;
            if (!dead && enemy != null && enemy.IsDead) OnDeath();
        }

        // ------------------------------------------------------------------ death

        void OnDeath()
        {
            dead = true;
            foreach (var go in plates) if (go != null) Fling(go, 1.5f);
            plates.Clear();
            if (rage != null) Destroy(rage.gameObject);
            Fissure(transform.position, height * 0.7f, CorpseSeconds);
            SpellFx.Dust(transform.position, height * 0.8f, style.Dust);
            var p = Player.I;
            if (p != null && Factory.FlatDistance(p.transform.position, transform.position) < 40f) CameraRig.Shake(0.45f);
            // embers or motes rising off the body while it lies there
            SpellFx.Emit(new SpellFx.P
            {
                Rate = 8, Duration = CorpseSeconds - 10f, Life = new Vector2(1.5f, 3f), Speed = new Vector2(0.1f, 0.4f),
                Size = new Vector2(0.06f, 0.14f), Start = new Color(style.Glow.r, style.Glow.g, style.Glow.b, 0.8f), End = new Color(style.Glow.r, style.Glow.g, style.Glow.b, 0f),
                Shape = ParticleSystemShapeType.Circle, Radius = height * 0.4f, Velocity = new Vector3(0f, 0.6f, 0f),
            }, transform.position + Vector3.up * 0.3f);
        }

        /// <summary>How long a world boss's body lies there before sinking away (other monsters: 7 s).</summary>
        public static float Linger => CorpseSeconds;

        // ------------------------------------------------------------------ ground marks

        /// <summary>A slam's crater: scorched, cracked ground and a few stones; stays a couple of minutes.</summary>
        public static void Crater(Vector3 at, float radius, string boss)
        {
            var st = boss != null && styles.TryGetValue(boss, out var found) ? found : styles["Gorvash the Dune Reaver"];
            Fissure(at, radius * 0.7f, 150f, st);
            for (int i = 0; i < 6; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(radius * 0.4f, radius * 0.8f);
                var stone = Factory.Prim(PrimitiveType.Cube, null, at + new Vector3(Mathf.Cos(a) * r, 0.08f, Mathf.Sin(a) * r),
                    Vector3.one * Random.Range(0.15f, 0.35f), st.Crack * 0.8f + new Color(0.15f, 0.15f, 0.15f));
                stone.transform.rotation = Random.rotation;
                stone.AddComponent<FadeAway>().Seconds = 150f;
                Keep(stone);
            }
        }

        void Fissure(Vector3 at, float radius, float seconds) => Fissure(at, radius, seconds, style);

        static void Fissure(Vector3 at, float radius, float seconds, Style st)
        {
            var root = new GameObject("BossMark").transform;
            root.position = at;
            // a dark disc, and cracks radiating from it (glowing for a lava boss)
            var disc = Factory.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 0.015f, 0f), new Vector3(radius * 1.4f, 0.01f, radius * 1.4f), st.Crack * 0.55f);
            disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bool glow = st.Crack.r > 0.8f && st.Crack.g < 0.5f;
            int n = Random.Range(6, 9);
            for (int i = 0; i < n; i++)
            {
                float ang = i * 360f / n + Random.Range(-15f, 15f), len = radius * Random.Range(0.8f, 1.5f);
                var crack = Factory.Prim(PrimitiveType.Cube, root, Vector3.zero, new Vector3(Random.Range(0.08f, 0.16f), 0.012f, len), st.Crack, false, glow ? Mat.Glow(st.Crack) : null);
                crack.transform.localRotation = Quaternion.Euler(0f, ang, 0f);
                crack.transform.localPosition = crack.transform.localRotation * new Vector3(0f, 0.025f, len * 0.5f + radius * 0.3f);
                crack.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            root.gameObject.AddComponent<FadeAway>().Seconds = seconds;
            Keep(root.gameObject);
        }

        static void Keep(GameObject go)
        {
            craters.Enqueue(go);
            while (craters.Count > MaxCraters * 8)
            {
                var old = craters.Dequeue();
                if (old != null) Destroy(old);
            }
        }
    }

    /// <summary>Stays for <see cref="Seconds"/>, then sinks into the ground and is gone.</summary>
    public class FadeAway : MonoBehaviour
    {
        public float Seconds = 60f;
        float born;
        void Start() => born = Time.time;
        void Update()
        {
            float t = Time.time - born;
            if (t < Seconds) return;
            transform.position += Vector3.down * Time.deltaTime * 0.15f;
            if (t > Seconds + 3f) Destroy(gameObject);
        }
    }
}
