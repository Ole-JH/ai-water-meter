using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Hollowmere through the year. Each season dresses the village for its festival and changes the trees:
    /// <list type="bullet">
    /// <item>Spring, the Bloom Festival: flower garlands across the streets, flowers by the houses, drifting petals.</item>
    /// <item>Summer, the Midsummer Fair: a maypole with turning ribbons, bunting, a bonfire on the square at night.</item>
    /// <item>Autumn, the Harvest Festival: golden and orange trees, pumpkins (lit at night), hay bales, and villagers
    ///       raking leaves into piles you really shouldn't run through (<see cref="LeafPile"/>, <see cref="Raker"/>).</item>
    /// <item>Winter, Winterfest: a decorated tree, presents, snowmen, a sled and a reindeer, coloured lights over the
    ///       market, frozen lakes, and elves shoveling the streets clear (<see cref="SnowElf"/>).</item>
    /// </list>
    /// Purely visual and local to each player; nothing here blocks the walkable map.
    /// </summary>
    public class SeasonalTown : MonoBehaviour
    {
        public static SeasonalTown I;
        public static readonly string[] Festivals = { "the Bloom Festival", "the Midsummer Fair", "the Harvest Festival", "Winterfest" };
        public static string Festival => Festivals[(int)Weather.Season];

        Transform root;
        Season built = (Season)(-1);
        bool announced;
        static Material vcMat;

        public static void Ensure()
        {
            if (I == null) new GameObject("SeasonalTown").AddComponent<SeasonalTown>();
        }

        void Awake()
        {
            I = this;
            Weather.SeasonChanged += Rebuild;
        }

        void OnDestroy()
        {
            Weather.SeasonChanged -= Rebuild;
            if (I == this) I = null;
        }

        void Update()
        {
            var p = Player.I;
            if (p == null || built != Weather.Season) return;
            // Announce the festival the first time the hero is in town for it.
            if (!announced && WorldGenerator.InHollowmere(p.transform.position) && !Dungeon.Active)
            {
                announced = true;
                p.Achievements.Once("season", Weather.Season.ToString());
                var c = SeasonColor(Weather.Season);
                GameUI.Banner(Capitalize(Festival) + " in Hollowmere", c);
                GameUI.Log("It's " + Weather.Season.ToString().ToLower() + ", and Hollowmere is celebrating " + Festival + ".", c);
            }
        }

        static string Capitalize(string s) => char.ToUpper(s[0]) + s.Substring(1);

        public static Color SeasonColor(Season s) =>
            s == Season.Spring ? new Color(1f, 0.7f, 0.85f) : s == Season.Summer ? new Color(1f, 0.85f, 0.35f) :
            s == Season.Autumn ? new Color(1f, 0.6f, 0.25f) : new Color(0.7f, 0.85f, 1f);

        void Rebuild()
        {
            if (built == Weather.Season) return;
            bool first = (int)built < 0;
            built = Weather.Season;
            announced = false;
            if (root != null) Destroy(root.gameObject);
            root = new GameObject("Season " + built).transform;
            root.SetParent(transform, false);
            Foliage.Apply(built);
            switch (built)
            {
                case Season.Spring: BuildSpring(); break;
                case Season.Summer: BuildSummer(); break;
                case Season.Autumn: BuildAutumn(); break;
                case Season.Winter: BuildWinter(); break;
            }
            if (!first && Player.I != null)
            {
                var c = SeasonColor(built);
                GameUI.Banner(built + " has come", c);
                GameUI.Log(built + " has come to Hollowmere. The village is getting ready for " + Festival + ".", c);
            }
        }

        // ------------------------------------------------------------------ helpers

        GameObject Prop(string path, float x, float z, float size, float yaw = 0f, ArtLibrary.Fit fit = ArtLibrary.Fit.Height, float y = 0f)
        {
            var go = ArtLibrary.Spawn("Seasonal/" + path, root, new Vector3(x, y, z), size, fit, yaw);
            if (go == null)
            {
                // Fallback when the art is missing: a simple marker of the right size.
                go = Factory.Prim(PrimitiveType.Cube, root, new Vector3(x, y + size * 0.5f, z), Vector3.one * size * 0.6f, new Color(0.8f, 0.8f, 0.8f));
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            return go;
        }

        Light Glow(Vector3 at, Color c, float range, float intensity, float dayFactor = 0f)
        {
            var l = new GameObject("FestiveLight").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.position = at;
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            NightLight.Add(l, dayFactor);
            return l;
        }

        /// <summary>Vertex-coloured, wind-swayed material (the grass shader): bunting, ribbons.</summary>
        public static Material VertexColor
        {
            get
            {
                if (vcMat != null) return vcMat;
                var shader = Resources.Load<Shader>("Shaders/ShadowfallGrass");
                vcMat = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = "Festive" };
                vcMat.SetFloat("_Wind", 0.35f);
                vcMat.SetFloat("_WindSpeed", 2.4f);
                return vcMat;
            }
        }

        /// <summary>A string of little triangular flags sagging between two points, fluttering in the wind.</summary>
        void Bunting(Vector3 a, Vector3 b, float sag, Color[] colors)
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(3, Mathf.RoundToInt(len / 0.55f));
            var side = Vector3.Cross((b - a).normalized, Vector3.up).normalized * 0.01f;
            for (int i = 0; i < n; i++)
            {
                float t0 = (i + 0.1f) / n, t1 = (i + 0.9f) / n, tm = (i + 0.5f) / n;
                Vector3 P(float t) => Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t);
                var c = colors[i % colors.Length];
                int v = verts.Count;
                verts.Add(P(t0)); verts.Add(P(t1)); verts.Add(P(tm) + Vector3.down * 0.42f + side);
                cols.Add(new Color(c.r, c.g, c.b, 0f)); cols.Add(new Color(c.r, c.g, c.b, 0f)); cols.Add(new Color(c.r, c.g, c.b, 0.6f));
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
            }
            // the cord
            for (int i = 0; i < n * 2; i++)
            {
                float t0 = (float)i / (n * 2), t1 = (float)(i + 1) / (n * 2);
                Vector3 P(float t) => Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t);
                int v = verts.Count;
                var cc = new Color(0.25f, 0.2f, 0.15f, 0f);
                verts.Add(P(t0)); verts.Add(P(t1)); verts.Add(P(t1) + Vector3.down * 0.03f); verts.Add(P(t0) + Vector3.down * 0.03f);
                cols.Add(cc); cols.Add(cc); cols.Add(cc); cols.Add(cc);
                tris.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
            }
            var mesh = new Mesh { name = "Bunting" };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Bunting");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = VertexColor;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Pole(Vector3 at, float height, Color c) =>
            Factory.Prim(PrimitiveType.Cylinder, root, at + Vector3.up * height * 0.5f, new Vector3(0.12f, height * 0.5f, 0.12f), c, false);

        static readonly Color[] SpringFlags = { new Color(1f, 0.6f, 0.75f), new Color(1f, 0.95f, 0.6f), new Color(0.65f, 0.85f, 1f), new Color(0.75f, 1f, 0.7f) };
        static readonly Color[] SummerFlags = { new Color(0.95f, 0.25f, 0.2f), new Color(1f, 0.85f, 0.2f), new Color(0.2f, 0.45f, 0.9f), new Color(0.95f, 0.95f, 0.9f) };
        static readonly Color Wood = new Color(0.45f, 0.32f, 0.2f);

        /// <summary>Flags across the cross streets, between poles at the street edges.</summary>
        void StreetBunting(Color[] colors)
        {
            foreach (float y in new[] { 125.5f, 133.5f, 156.5f, 165.5f }) // across the north-south street
            {
                Pole(new Vector3(140.7f, 0, y), 3.6f, Wood);
                Pole(new Vector3(148.3f, 0, y), 3.6f, Wood);
                Bunting(new Vector3(140.7f, 3.5f, y), new Vector3(148.3f, 3.5f, y), 0.35f, colors);
            }
            foreach (float x in new[] { 129.5f, 134.5f, 155.5f, 160.5f }) // across the east-west street
            {
                Pole(new Vector3(x, 0, 140.7f), 3.6f, Wood);
                Pole(new Vector3(x, 0, 148.3f), 3.6f, Wood);
                Bunting(new Vector3(x, 3.5f, 140.7f), new Vector3(x, 3.5f, 148.3f), 0.35f, colors);
            }
        }

        // ------------------------------------------------------------------ the seasons

        void BuildSpring()
        {
            StreetBunting(SpringFlags);
            string[] flowers = { "Flowers_Yellow", "Flowers_Purple", "Bush_Flowers" };
            var rng = new System.Random(3);
            foreach (var at in new[] { new Vector2(129.5f, 152.5f), new Vector2(130.5f, 146.6f), new Vector2(137.5f, 162.3f), new Vector2(149.5f, 162.3f),
                                       new Vector2(155.5f, 158.2f), new Vector2(162.3f, 147.3f), new Vector2(128.4f, 139.5f), new Vector2(156.3f, 137.4f),
                                       new Vector2(135.2f, 135.2f), new Vector2(153.6f, 153.6f), new Vector2(139.6f, 170.2f), new Vector2(148.4f, 118.4f) })
            {
                var go = ArtLibrary.Spawn("Plants/" + flowers[rng.Next(flowers.Length)], root, new Vector3(at.x, 0, at.y), 0.9f, ArtLibrary.Fit.Height, rng.Next(360));
                if (go != null) foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            root.gameObject.AddComponent<TownDrift>().Petals();
        }

        void BuildSummer()
        {
            StreetBunting(SummerFlags);
            root.gameObject.AddComponent<Maypole>().Build(new Vector3(139.5f, 0, 149.5f), SummerFlags);
            // A bonfire on the square, lit at dusk.
            var fireAt = new Vector3(139.5f, 0, 138.8f); // south-west of the well (the stash chest stands south-east)
            var logs = ArtLibrary.Spawn("Nature/campfire_logs", root, fireAt, 1.4f, ArtLibrary.Fit.Width);
            if (logs != null) foreach (var c in logs.GetComponentsInChildren<Collider>()) Destroy(c);
            var light = Glow(fireAt + Vector3.up * 1.2f, new Color(1f, 0.6f, 0.25f), 12f, 2.2f);
            root.gameObject.AddComponent<NightFire>().Init(fireAt, light);
            foreach (var at in new[] { new Vector2(137.6f, 137.6f), new Vector2(150.4f, 150.4f), new Vector2(137.6f, 150.4f) })
            {
                Prop("lantern-candle", at.x, at.y, 0.7f);
                Glow(new Vector3(at.x, 0.6f, at.y), new Color(1f, 0.75f, 0.4f), 4f, 1f);
            }
        }

        void BuildAutumn()
        {
            var rng = new System.Random(10);
            string[] pumpkins = { "pumpkin", "pumpkin-carved", "pumpkin-tall-carved" };
            foreach (var at in new[] { new Vector2(129.4f, 151.6f), new Vector2(130.3f, 151.2f), new Vector2(136.6f, 161.6f), new Vector2(149.4f, 161.7f),
                                       new Vector2(155.4f, 154.6f), new Vector2(162.4f, 146.6f), new Vector2(128.4f, 139.4f), new Vector2(151.5f, 133.4f),
                                       new Vector2(138.6f, 131.6f), new Vector2(141.2f, 170.4f), new Vector2(147.8f, 170.4f), new Vector2(141.2f, 118.6f),
                                       new Vector2(147.8f, 118.6f), new Vector2(166.5f, 141.2f), new Vector2(121.5f, 147.8f) })
            {
                string kind = pumpkins[rng.Next(pumpkins.Length)];
                Prop(kind, at.x, at.y, 0.55f + (float)rng.NextDouble() * 0.25f, rng.Next(360));
                if (kind != "pumpkin") Glow(new Vector3(at.x, 0.35f, at.y), new Color(1f, 0.55f, 0.15f), 2.8f, 1.2f);
            }
            Prop("hay-bale", 137.4f, 137.6f, 0.9f, 20f, ArtLibrary.Fit.Height);
            Prop("hay-bale-bundled", 150.6f, 137.4f, 0.9f, -15f, ArtLibrary.Fit.Height);
            Prop("hay-bale", 137.6f, 150.5f, 0.9f, 80f, ArtLibrary.Fit.Height);
            Prop("hay-bale-bundled", 131.5f, 127.6f, 0.9f, 5f, ArtLibrary.Fit.Height);
            Prop("lantern-candle", 150.6f, 137.4f, 0.55f, 0f, ArtLibrary.Fit.Height, 0.9f);
            Glow(new Vector3(150.6f, 1.3f, 137.4f), new Color(1f, 0.7f, 0.35f), 4f, 1f);

            // Leaf piles and the villagers who rake them.
            var yards = new[] { new Vector3(132.5f, 0, 152.5f), new Vector3(158.5f, 0, 150.5f), new Vector3(134.5f, 0, 129.5f), new Vector3(156.5f, 0, 127.6f) };
            for (int i = 0; i < yards.Length; i++)
            {
                var pile = LeafPile.Create(root, Walkable(yards[i]), 100 + i);
                Raker.Create(root, pile, i);
            }
        }

        void BuildWinter()
        {
            // The Winterfest tree on the square, with presents.
            Prop("tree-decorated-snow", 139.5f, 149.5f, 5f);
            Glow(new Vector3(139.5f, 3f, 149.5f), new Color(1f, 0.85f, 0.5f), 9f, 1.6f, 0.3f);
            Prop("present-a-cube", 138.6f, 148.4f, 0.55f, 20f);
            Prop("present-b-round", 140.6f, 148.7f, 0.5f, -30f);
            Prop("present-a-rectangle", 140.2f, 150.6f, 0.45f, 70f);
            // Snowmen around town.
            var rng = new System.Random(5);
            foreach (var at in new[] { new Vector2(150.5f, 138.5f), new Vector2(133.5f, 149.5f), new Vector2(159.6f, 150.6f), new Vector2(127.6f, 141.2f),
                                       new Vector2(147.5f, 128.5f), new Vector2(139.4f, 157.5f) })
                Prop(rng.Next(2) == 0 ? "snowman" : "snowman-hat", at.x, at.y, 1.5f, rng.Next(360));
            Prop("sled", 153.2f, 157.8f, 1.4f, 30f, ArtLibrary.Fit.Width);
            Prop("reindeer", 165.5f, 127.5f, 1.6f, 200f);
            foreach (var at in new[] { new Vector2(141.2f, 170.5f), new Vector2(147.8f, 170.5f), new Vector2(141.2f, 118.5f), new Vector2(147.8f, 118.5f) })
                Prop("candy-cane-red", at.x, at.y, 1.6f, 0f);
            // Coloured lights over the market stalls, and lanterns at the gates.
            for (int i = 0; i < 3; i++)
            {
                Prop("lights-colored", 130.5f + i * 2f, 137.6f, 1.9f, 0f, ArtLibrary.Fit.Width, 2.2f);
                Glow(new Vector3(130.5f + i * 2f, 2.2f, 137.8f), i % 2 == 0 ? new Color(1f, 0.4f, 0.35f) : new Color(0.4f, 1f, 0.5f), 3f, 0.9f, 0.2f);
            }
            Prop("wreath-decorated", 144.5f, 144.5f, 0.9f, 0f, ArtLibrary.Fit.Height, 1.2f);
            // The elves who keep the streets clear.
            ElfCrew.Create(root);
        }

        /// <summary>The nearest walkable spot (decorations and piles go on open ground).</summary>
        static Vector3 Walkable(Vector3 p)
        {
            var grid = WorldGrid.Instance;
            for (int r = 0; r < 6; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var q = p + new Vector3(dx, 0, dy);
                        if (grid.IsWalkable(q)) return q;
                    }
            return p;
        }
    }

    /// <summary>Season colours for the trees: golden oaks and orange willows in autumn, cold dark needles in winter.</summary>
    public static class Foliage
    {
        static readonly Dictionary<Material, Color> original = new Dictionary<Material, Color>();
        static readonly string[] colorProps = { "baseColorFactor", "_BaseColor", "_Color" };

        public static void Apply(Season s)
        {
            if (original.Count == 0)
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null || original.ContainsKey(m) || !m.name.StartsWith("Leaves_")) continue;
                        string prop = ColorProp(m);
                        if (prop != null) original[m] = m.GetColor(prop);
                    }
            foreach (var kv in original)
            {
                var m = kv.Key;
                if (m == null) continue;
                m.SetColor(ColorProp(m), kv.Value * Tint(m.name, s));
            }
        }

        static string ColorProp(Material m)
        {
            foreach (var p in colorProps) if (m.HasProperty(p)) return p;
            return null;
        }

        static Color Tint(string material, Season s)
        {
            bool pine = material.Contains("Pine"), willow = material.Contains("Twisted");
            switch (s)
            {
                case Season.Spring: return pine ? new Color(1f, 1.05f, 1f) : new Color(1.12f, 1.2f, 0.95f);
                case Season.Autumn: return pine ? new Color(0.95f, 0.95f, 0.85f) : willow ? new Color(2.5f, 0.85f, 0.3f) : new Color(2.3f, 1.15f, 0.35f);
                case Season.Winter: return pine ? new Color(0.75f, 0.85f, 0.95f) : new Color(1.25f, 1.05f, 0.95f);
                default: return Color.white;
            }
        }
    }

    /// <summary>Petals drifting through Hollowmere in spring (around the hero while in town).</summary>
    public class TownDrift : MonoBehaviour
    {
        ParticleSystem ps;

        public void Petals()
        {
            ps = SpellFx.Emit(new SpellFx.P
            {
                Rate = 14, Duration = 10f, Life = new Vector2(5f, 8f), Speed = new Vector2(0.1f, 0.4f), Size = new Vector2(0.06f, 0.11f),
                Start = new Color(1f, 0.75f, 0.85f, 0.9f), Mid = new Color(1f, 0.85f, 0.92f, 0.9f), End = new Color(1f, 0.9f, 0.95f, 0f),
                Shape = ParticleSystemShapeType.Circle, Radius = 14f, Velocity = new Vector3(0.6f, -0.35f, 0.25f), Max = 200, Smoke = true,
            }, Vector3.zero);
            if (ps == null) return;
            ps.transform.SetParent(transform, false);
            var main = ps.main;
            main.loop = true;
            main.stopAction = ParticleSystemStopAction.None;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.3f;
            ps.Play();
        }

        void Update()
        {
            if (ps == null) return;
            var p = Player.I;
            bool show = p != null && WorldGenerator.InHollowmere(p.transform.position) && !Dungeon.Active;
            if (show) ps.transform.position = p.transform.position + Vector3.up * 6f;
            var em = ps.emission;
            em.enabled = show;
        }
    }

    /// <summary>The Midsummer maypole: a tall pole with ribbons that turn slowly around it.</summary>
    public class Maypole : MonoBehaviour
    {
        Transform crown;

        public void Build(Vector3 at, Color[] colors)
        {
            var root = new GameObject("Maypole").transform;
            root.SetParent(transform, false);
            Factory.Prim(PrimitiveType.Cylinder, root, at + Vector3.up * 3f, new Vector3(0.22f, 3f, 0.22f), new Color(0.95f, 0.92f, 0.85f), false);
            crown = new GameObject("Ribbons").transform;
            crown.SetParent(root, false);
            crown.position = at;
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            int n = 8;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var top = new Vector3(0f, 5.8f, 0f);
                var bottom = new Vector3(Mathf.Cos(a) * 2.6f, 0.9f, Mathf.Sin(a) * 2.6f);
                var side = Vector3.Cross((bottom - top).normalized, Vector3.up).normalized * 0.09f;
                var c = colors[i % colors.Length];
                int v = verts.Count;
                verts.Add(top - side); verts.Add(top + side); verts.Add(bottom + side); verts.Add(bottom - side);
                cols.Add(new Color(c.r, c.g, c.b, 0f)); cols.Add(new Color(c.r, c.g, c.b, 0f)); cols.Add(new Color(c.r, c.g, c.b, 0.5f)); cols.Add(new Color(c.r, c.g, c.b, 0.5f));
                tris.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
            }
            var mesh = new Mesh { name = "Ribbons" };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            crown.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            crown.gameObject.AddComponent<MeshRenderer>().sharedMaterial = SeasonalTown.VertexColor;
            Factory.Prim(PrimitiveType.Sphere, root, at + Vector3.up * 6.05f, Vector3.one * 0.5f, new Color(1f, 0.85f, 0.3f), false);
        }

        void Update()
        {
            if (crown != null) crown.Rotate(0f, 12f * Time.deltaTime, 0f);
        }
    }

    /// <summary>A bonfire that burns from dusk to dawn.</summary>
    public class NightFire : MonoBehaviour
    {
        Vector3 at;
        Light light;
        PropFire fire;

        public void Init(Vector3 pos, Light l)
        {
            at = pos;
            light = l;
        }

        void Update()
        {
            bool lit = DayNight.Night > 0.3f;
            if (lit && fire == null) fire = PropFire.Add(transform, at + Vector3.up * 0.2f, new Color(1f, 0.55f, 0.15f), 1.4f, true, light);
            else if (!lit && fire != null) { Destroy(fire.gameObject); fire = null; }
        }
    }
}
