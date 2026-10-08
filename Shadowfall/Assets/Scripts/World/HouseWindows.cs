using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Lit windows. After dark each house's window panes glow with the fire inside (a pane laid over the glass painted
    /// into the model), flickering a little; now and then someone walks past inside and a window dims for a moment.
    /// Each house goes to bed at its own hour and the lights go out until dawn. When the town is attacked the shutters
    /// slam over every window and the lights go out (<see cref="Invasion"/>), and open again when it is over.
    /// </summary>
    public class HouseWindows : MonoBehaviour
    {
        /// <summary>Window panes per model, in its own (glTF) units: centre x, y, z, which way it faces (x, z), width, height.
        /// Measured from the glass in the meshes.</summary>
        static readonly Dictionary<string, float[]> Panes = new Dictionary<string, float[]>
        {
            { "building_home_A_green", new[] { 0.259f, 0.207f, 0.105f, 1f, 0f, 0.074f, 0.106f,
                0.259f, 0.207f, -0.095f, 1f, 0f, 0.074f, 0.106f,
                -0.259f, 0.207f, 0.105f, -1f, 0f, 0.074f, 0.106f,
                -0.259f, 0.207f, -0.095f, -1f, 0f, 0.074f, 0.106f,
                0f, 0.519f, 0.324f, 0f, 1f, 0.074f, 0.079f } },
            { "building_home_A_blue", new[] { 0.259f, 0.207f, 0.105f, 1f, 0f, 0.074f, 0.106f,
                0.259f, 0.207f, -0.095f, 1f, 0f, 0.074f, 0.106f,
                -0.259f, 0.207f, 0.105f, -1f, 0f, 0.074f, 0.106f,
                -0.259f, 0.207f, -0.095f, -1f, 0f, 0.074f, 0.106f,
                0f, 0.519f, 0.324f, 0f, 1f, 0.074f, 0.079f } },
            { "building_home_B_blue", new[] { -0.14f, 0.417f, 0.224f, 0f, 1f, 0.074f, 0.106f,
                -0.14f, 0.417f, -0.364f, 0f, -1f, 0.074f, 0.106f,
                -0.364f, 0.417f, -0.07f, -1f, 0f, 0.074f, 0.106f,
                0f, 0.989f, 0.241f, 0f, 1f, 0.074f, 0.079f } },
            { "building_tavern_blue", new[] { -0.429f, 0.417f, -0.23f, -1f, 0f, 0.074f, 0.106f,
                0.153f, 0.416f, 0.089f, 1f, 0f, 0.074f, 0.079f } },
            { "building_church_blue", new[] { -0.493f, 0.168f, -0.28f, -1f, 0f, 0.074f, 0.079f,
                -0.493f, 0.168f, 0.14f, -1f, 0f, 0.074f, 0.079f,
                0.493f, 0.168f, 0.14f, 1f, 0f, 0.074f, 0.079f,
                0.493f, 0.168f, -0.28f, 1f, 0f, 0.074f, 0.079f,
                -0.14f, 0.193f, -0.503f, 0f, -1f, 0.074f, 0.106f,
                0.14f, 0.193f, -0.503f, 0f, -1f, 0.074f, 0.106f,
                0f, 0.543f, 0.539f, 0f, 1f, 0.074f, 0.106f,
                0f, 0.774f, 0.539f, 0f, 1f, 0.074f, 0.106f } },
            { "building_market_blue", new[] { -0.165f, 0.207f, -0.006f, 0f, 1f, 0.074f, 0.106f } },
            { "building_blacksmith_blue", new[] { -0.35f, 0.207f, -0.364f, 0f, -1f, 0.074f, 0.106f,
                -0.574f, 0.207f, -0.14f, -1f, 0f, 0.074f, 0.106f } },
            { "building_windmill_blue", new[] { 0.276f, 0.917f, 0f, 1f, 0f, 0.074f, 0.106f,
                -0.276f, 0.917f, 0f, -1f, 0f, 0.074f, 0.106f,
                0f, 0.917f, -0.276f, 0f, -1f, 0.074f, 0.106f } },
        };

        static readonly Color Hearth = new Color(1f, 0.66f, 0.32f), Shutter = new Color(0.42f, 0.27f, 0.17f);
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor"), ColorId = Shader.PropertyToID("_Color");

        class House
        {
            public string Town;
            public Vector3 At;
            public float Bed, Wake;          // the hours its lights go out and come on again
            public NightLight Light;
            public float LightBase;
            public readonly List<Renderer> Glass = new List<Renderer>();
            public readonly List<Transform> Shutters = new List<Transform>();
            public readonly List<Quaternion> Faces = new List<Quaternion>();
            public float Shut;                // 0 open .. 1 closed
            public bool ShutWanted;
            public bool Lit;
            public int Passing = -1;          // the pane someone is walking past
            public float PassT, NextPass;
        }

        static readonly List<House> houses = new List<House>();
        static Material[] glow;               // three, flickering out of step
        static HouseWindows runner;
        static MaterialPropertyBlock block;

        /// <summary>Gives a house (what ArtLibrary.Spawn returned) its windows; <paramref name="light"/> is its window light.</summary>
        public static void Add(GameObject house, string model, NightLight light)
        {
            if (house == null || house.transform.childCount == 0) return;
            string name = model.Substring(model.LastIndexOf('/') + 1);
            if (!Panes.TryGetValue(name, out var p)) return;
            if (glow == null)
            {
                glow = new Material[3];
                for (int i = 0; i < 3; i++) { glow[i] = Mat.New(Hearth); glow[i].EnableKeyword("_EMISSION"); }
            }
            var root = house.transform.GetChild(0); // the model itself (scaled); glTFast mirrors x into Unity's space
            var town = WorldGenerator.TownAt(house.transform.position);
            var rng = new System.Random(house.transform.position.GetHashCode());
            var h = new House
            {
                Town = town != null ? town.Name : "",
                At = house.transform.position,
                Bed = 22.5f + (float)rng.NextDouble() * 3f,   // half past ten to half past one
                Wake = 5f + (float)rng.NextDouble() * 1.5f,
                Light = light,
                LightBase = light != null ? light.BaseIntensity : 0f,
                NextPass = Time.time + 5f + (float)rng.NextDouble() * 20f,
            };
            for (int i = 0; i + 6 < p.Length; i += 7)
            {
                var normal = new Vector3(-p[i + 3], 0f, p[i + 4]);
                var at = new Vector3(-p[i], p[i + 1], p[i + 2]) + normal * 0.002f;
                var face = Quaternion.LookRotation(-normal); // a quad shows its face towards -z
                var pane = Factory.Prim(PrimitiveType.Quad, root, at, new Vector3(p[i + 5] * 0.82f, p[i + 6] * 0.86f, 1f), Hearth, false, glow[(i / 7 + rng.Next(3)) % 3]);
                pane.transform.localRotation = face;
                var r = pane.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.enabled = false;
                pane.transform.SetParent(HouseDoors.Movers, true);
                h.Glass.Add(r);
                h.Faces.Add(pane.transform.rotation);
                // Two wooden shutters, hinged at the sides of the window, folded flat against the wall until needed
                for (int side = -1; side <= 1; side += 2)
                {
                    var hinge = Factory.Empty("ShutterHinge", root, at + normal * 0.002f + Vector3.Cross(Vector3.up, normal) * (side * p[i + 5] * 0.5f));
                    hinge.localRotation = face;
                    // (shut, each leaf covers its half of the window)
                    var leaf = Factory.Prim(PrimitiveType.Cube, hinge, new Vector3(side * p[i + 5] * 0.25f, 0f, -0.003f), new Vector3(p[i + 5] * 0.5f, p[i + 6] * 1.04f, 0.006f), Shutter);
                    leaf.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    hinge.gameObject.SetActive(false);
                    hinge.SetParent(HouseDoors.Movers, true);
                    h.Shutters.Add(hinge);
                }
            }
            houses.Add(h);
            if (runner == null) runner = new GameObject("HouseWindows").AddComponent<HouseWindows>();
        }

        /// <summary>A new world: the old houses are gone.</summary>
        public static void Clear() => houses.Clear();

        float nextCheck;

        void Update()
        {
            if (houses.Count == 0 || glow == null) return;
            float night = DayNight.Night, t = Time.time;
            // the fire inside: three flickers out of step
            for (int i = 0; i < glow.Length; i++)
            {
                float f = 0.82f + 0.1f * Mathf.Sin(t * (3.1f + i) + i * 2f) + 0.08f * Mathf.PerlinNoise(t * 2.3f, i * 7f);
                var c = Hearth * (f * Mathf.Lerp(0.3f, 1f, night));
                glow[i].color = c;
                glow[i].SetColor(EmissionId, c * 1.4f);
            }
            var hero = Player.I;
            if (hero == null || Dungeon.Active) return;
            var hp = hero.transform.position;
            bool check = t >= nextCheck;
            if (check) nextCheck = t + 0.5f;
            foreach (var h in houses)
            {
                if (Factory.FlatDistance(h.At, hp) > 90f) continue;
                if (check) Decide(h, night);
                Animate(h, t);
            }
        }

        static void Decide(House h, float night)
        {
            float hour = DayNight.Hour, bed = h.Bed % 24f;
            bool awake = h.Bed < 24f ? hour >= h.Wake && hour < bed : hour >= h.Wake || hour < bed;
            bool besieged = Invasion.Active && Invasion.Current.town == h.Town;
            bool lit = night > 0.35f && awake && !besieged;
            if (lit != h.Lit)
            {
                h.Lit = lit;
                foreach (var r in h.Glass) if (r != null) r.enabled = lit;
                if (h.Light != null) h.Light.BaseIntensity = lit || night <= 0.35f ? h.LightBase : 0f;
            }
            if (besieged != h.ShutWanted)
            {
                h.ShutWanted = besieged;
                if (besieged)
                {
                    foreach (var s in h.Shutters) if (s != null) s.gameObject.SetActive(true);
                    var hero = Player.I;
                    if (hero != null && Factory.FlatDistance(hero.transform.position, h.At) < 30f)
                        Sfx.Play("door_close", h.At + Vector3.up * 2f, 0.3f, 0.2f, 30f);
                }
            }
        }

        static void Animate(House h, float t)
        {
            // Shutters: slammed shut in 0.4 s, opened again more slowly; folded back flat against the wall (hidden) when open
            float target = h.ShutWanted ? 1f : 0f;
            if (h.Shut != target)
            {
                h.Shut = Mathf.MoveTowards(h.Shut, target, Time.deltaTime / (h.ShutWanted ? 0.4f : 0.8f));
                float open = (1f - Mathf.SmoothStep(0f, 1f, h.Shut)) * 170f;
                for (int i = 0; i < h.Shutters.Count; i++)
                {
                    var s = h.Shutters[i];
                    if (s == null) continue;
                    int side = i % 2 == 0 ? -1 : 1;
                    s.rotation = h.Faces[i / 2] * Quaternion.Euler(0f, side * open, 0f);
                    if (h.Shut <= 0f) s.gameObject.SetActive(false);
                }
            }

            // Someone walks past a lit window inside: it dims and comes back
            if (!h.Lit || h.Glass.Count == 0) return;
            if (h.Passing < 0 && t >= h.NextPass)
            {
                h.Passing = Random.Range(0, h.Glass.Count);
                h.PassT = 0f;
                h.NextPass = t + Random.Range(9f, 28f);
            }
            if (h.Passing >= 0)
            {
                var r = h.Glass[h.Passing];
                h.PassT += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(h.PassT / 0.9f) * Mathf.PI);
                if (block == null) block = new MaterialPropertyBlock();
                if (r != null)
                {
                    if (h.PassT >= 0.9f) { r.SetPropertyBlock(null); h.Passing = -1; return; }
                    var c = r.sharedMaterial.color * (1f - 0.75f * k);
                    block.SetColor(ColorId, c);
                    block.SetColor(EmissionId, c * 1.4f);
                    r.SetPropertyBlock(block);
                }
                else h.Passing = -1;
            }
        }
    }
}
