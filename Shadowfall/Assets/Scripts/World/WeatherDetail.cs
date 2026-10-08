using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The small things the weather does around the hero (<see cref="Weather"/> has the sky): puddles that gather in
    /// the rain and shrink as the ground dries, with raindrop rings on them, and turn to ice when it's freezing; the
    /// hero's breath steaming in the cold; and in a storm, lightning that strikes a tree near by: a bolt from the sky,
    /// the tree burning for a while, and afterwards standing charred and bare for the rest of the session.
    /// </summary>
    public class WeatherDetail : MonoBehaviour
    {
        const float Cell = 4.5f, Reach = 26f;
        const int MaxPuddles = 46, MaxBurnt = 12;

        readonly Dictionary<Vector2Int, Transform> puddles = new Dictionary<Vector2Int, Transform>();
        readonly Stack<Transform> spare = new Stack<Transform>();
        readonly HashSet<GameObject> burnt = new HashSet<GameObject>();
        Material water, ice;
        float nextScan, nextRipple, nextBreath;

        public static void Ensure()
        {
            if (FindObjectOfType<WeatherDetail>() == null) new GameObject("WeatherDetail").AddComponent<WeatherDetail>();
        }

        void Start()
        {
            water = Mat.New(new Color(0.16f, 0.18f, 0.2f));
            water.SetFloat("_Glossiness", 0.95f);
            water.SetFloat("_Smoothness", 0.95f);
            water.SetFloat("_Metallic", 0.3f);
            ice = Mat.New(new Color(0.72f, 0.82f, 0.9f));
            ice.SetFloat("_Glossiness", 0.9f);
            ice.SetFloat("_Smoothness", 0.9f);
        }

        void Update()
        {
            var p = Player.I;
            if (p == null || Dungeon.Active) { HideAll(); return; }
            var at = p.transform.position;
            Puddles(at);
            Breath(p);
        }

        // ------------------------------------------------------------------ puddles

        /// <summary>How big the puddles are now (0 = none): with the wet ground, or ice in a freeze.</summary>
        float PuddleSize(Vector3 at, out bool frozen)
        {
            frozen = Weather.ColdAt(at) && Weather.Season == Season.Winter;
            if (frozen) return 0.8f; // frozen over in winter
            return Mathf.Clamp01((Weather.Wet - 0.15f) / 0.6f);
        }

        void Puddles(Vector3 at)
        {
            float size = PuddleSize(at, out bool frozen);
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.6f;
                Scan(at, size, frozen);
            }
            // rings where raindrops land
            if (!frozen && size > 0f && Weather.RainingAt(at) && Time.time >= nextRipple && puddles.Count > 0)
            {
                nextRipple = Time.time + Mathf.Lerp(0.25f, 0.06f, Weather.Precip);
                int pick = Random.Range(0, puddles.Count), i = 0;
                foreach (var t in puddles.Values)
                    if (i++ == pick)
                    {
                        if (t.gameObject.activeSelf)
                        {
                            var r = t.localScale.x * 0.4f;
                            SpellFx.Ring(t.position + new Vector3(Random.Range(-r, r), 0.03f, Random.Range(-r, r)), new Color(0.8f, 0.85f, 0.9f), 0.25f, 0.45f);
                        }
                        break;
                    }
            }
        }

        /// <summary>Puddles lie in the same hollows every time: picked per cell of ground by a hash, kept near the hero.</summary>
        void Scan(Vector3 at, float size, bool frozen)
        {
            var grid = WorldGrid.Instance;
            var keep = new HashSet<Vector2Int>();
            if (size > 0.02f)
            {
                int cx = Mathf.FloorToInt(at.x / Cell), cz = Mathf.FloorToInt(at.z / Cell), n = Mathf.CeilToInt(Reach / Cell);
                for (int z = cz - n; z <= cz + n && keep.Count < MaxPuddles; z++)
                    for (int x = cx - n; x <= cx + n && keep.Count < MaxPuddles; x++)
                    {
                        uint h = Hash(x, z);
                        if ((h & 1023) > 190) continue; // about one cell in five
                        var c = new Vector2Int(x, z);
                        var pos = new Vector3((x + ((h >> 10) & 255) / 255f) * Cell, 0.012f, (z + ((h >> 18) & 255) / 255f) * Cell);
                        if ((pos - at).sqrMagnitude > Reach * Reach || !grid.IsWalkable(pos)) continue;
                        keep.Add(c);
                        if (!puddles.TryGetValue(c, out var t)) { t = Take(); puddles[c] = t; t.position = pos; t.rotation = Quaternion.Euler(0f, (h >> 4) % 360, 0f); }
                        float baseR = 0.6f + ((h >> 26) & 63) / 63f * 1.1f;
                        t.localScale = new Vector3(baseR * 2f * size, 0.008f, baseR * 1.4f * size);
                        t.GetComponent<Renderer>().sharedMaterial = frozen ? ice : water;
                        t.gameObject.SetActive(true);
                    }
            }
            var drop = new List<Vector2Int>();
            foreach (var kv in puddles) if (!keep.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var c in drop) { var t = puddles[c]; t.gameObject.SetActive(false); spare.Push(t); puddles.Remove(c); }
        }

        Transform Take()
        {
            if (spare.Count > 0) return spare.Pop();
            var go = Factory.Prim(PrimitiveType.Cylinder, transform, Vector3.zero, Vector3.one, Color.white, false, water);
            go.name = "Puddle";
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void HideAll()
        {
            foreach (var t in puddles.Values) { t.gameObject.SetActive(false); spare.Push(t); }
            puddles.Clear();
        }

        static uint Hash(int x, int z)
        {
            uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ 0x9e3779b9u;
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return h;
        }

        // ------------------------------------------------------------------ breath in the cold

        void Breath(Player p)
        {
            if (Time.time < nextBreath || !SpellFx.Ready) return;
            nextBreath = Time.time + Random.Range(1.8f, 2.6f);
            var at = p.transform.position;
            if (!Weather.ColdAt(at) || p.IsDead) return;
            var mouth = at + Vector3.up * 1.62f + p.transform.forward * 0.28f;
            SpellFx.Emit(new SpellFx.P
            {
                Burst = 5, Duration = 0.1f, Life = new Vector2(0.7f, 1.2f), Speed = new Vector2(0.15f, 0.35f), Size = new Vector2(0.08f, 0.16f),
                Start = new Color(0.95f, 0.97f, 1f, 0.45f), End = new Color(0.95f, 0.97f, 1f, 0f), Velocity = p.transform.forward * 0.35f + Vector3.up * 0.15f,
                Smoke = true, Grow = true, Radius = 0.04f,
            }, mouth);
        }

        // ------------------------------------------------------------------ lightning

        /// <summary>A bolt (Weather calls this with each flash): strikes a living tree near <paramref name="near"/>, if there is one.</summary>
        public static void Strike(Vector3 near)
        {
            var d = FindObjectOfType<WeatherDetail>();
            if (d == null || d.burnt.Count >= MaxBurnt) return;
            GameObject best = null;
            float bestD = 38f * 38f;
            foreach (var t in WorldGenerator.Trees)
            {
                if (t == null || d.burnt.Contains(t)) continue;
                float s = (t.transform.position - near).sqrMagnitude;
                if (s < 64f || s > bestD || Random.value < 0.5f) continue; // not right on top of the hero
                bestD = s;
                best = t;
            }
            if (best == null) return;
            d.burnt.Add(best);
            d.StartCoroutine(d.Burn(best));
        }

        System.Collections.IEnumerator Burn(GameObject tree)
        {
            var at = tree.transform.position;
            float h = 6.5f;
            var rs = tree.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); h = Mathf.Max(3f, b.max.y - at.y); }
            var top = at + Vector3.up * h * 0.9f;

            // the bolt: a jagged line of light from the clouds to the treetop
            var bolt = new GameObject("Bolt");
            var white = new Color(0.9f, 0.95f, 1f);
            Vector3 from = top + new Vector3(Random.Range(-4f, 4f), 40f, Random.Range(-4f, 4f));
            for (int i = 0; i < 9; i++)
            {
                var to = i == 8 ? top : Vector3.Lerp(from, top, (i + 1) / 9f) + new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f));
                var seg = Factory.Prim(PrimitiveType.Cube, bolt.transform, (from + to) * 0.5f, new Vector3(0.18f, 0.18f, Vector3.Distance(from, to)), white, false, Mat.Glow(white));
                seg.transform.rotation = Quaternion.LookRotation(to - from);
                seg.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                from = to;
            }
            var flashLight = new GameObject("BoltLight").AddComponent<Light>();
            flashLight.transform.SetParent(bolt.transform, false);
            flashLight.transform.position = top;
            flashLight.type = LightType.Point;
            flashLight.color = white;
            flashLight.range = 30f;
            flashLight.intensity = 4f;
            Sfx.Play("thunder", top, 1f, 0.05f, 120f);
            Sfx.Play("explosion", top, 0.6f, 0.1f, 60f);
            SpellFx.Hit(top, white, false, 30);
            var p = Player.I;
            if (p != null && Factory.FlatDistance(p.transform.position, at) < 30f) CameraRig.Shake(0.3f);
            Destroy(bolt, 0.18f);
            yield return new WaitForSeconds(0.15f);

            // it burns: flames in the crown and down the trunk, smoke, a glow, in spite of the rain for a while
            var fire = new GameObject("TreeFire").transform;
            fire.position = at;
            var glow = new GameObject("FireLight").AddComponent<Light>();
            glow.transform.SetParent(fire, false);
            glow.transform.localPosition = Vector3.up * h * 0.6f;
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.5f, 0.15f);
            glow.range = 9f;
            glow.shadows = LightShadows.None;
            if (SpellFx.Ready)
            {
                SpellFx.Emit(new SpellFx.P
                {
                    Rate = 45, Duration = 22f, Life = new Vector2(0.5f, 1.1f), Speed = new Vector2(0.5f, 1.6f), Size = new Vector2(0.3f, 0.7f),
                    Start = new Color(1f, 0.75f, 0.3f), Mid = new Color(1f, 0.35f, 0.08f), End = new Color(0.3f, 0.1f, 0.05f, 0f),
                    Velocity = Vector3.up * 1.5f, Shape = ParticleSystemShapeType.Circle, Radius = h * 0.18f,
                }, at + Vector3.up * h * 0.55f, fire);
                SpellFx.Emit(new SpellFx.P
                {
                    Rate = 8, Duration = 40f, Life = new Vector2(3f, 5f), Speed = new Vector2(0.3f, 0.6f), Size = new Vector2(1f, 2f),
                    Start = new Color(0.2f, 0.2f, 0.2f, 0.45f), End = new Color(0.25f, 0.25f, 0.25f, 0f), Velocity = Vector3.up * 1.4f, Smoke = true, Grow = true,
                }, at + Vector3.up * h, fire);
            }
            for (float t = 0f; t < 22f; t += Time.deltaTime)
            {
                glow.intensity = 1.8f + Mathf.PerlinNoise(Time.time * 4f, 3f) * 1.2f;
                if (t > 3f && rs.Length > 0 && rs[0] != null && rs[0].enabled)
                {
                    // the living tree is gone: a charred one stands in its place
                    foreach (var r in rs) if (r != null) r.enabled = false;
                    Charred(at, h, tree.transform.rotation);
                    SpellFx.Dust(at, 1.5f, new Color(0.2f, 0.18f, 0.16f));
                    Sfx.Play("rubble", at, 0.6f, 0.1f, 40f);
                }
                yield return null;
            }
            Destroy(glow.gameObject);
            Destroy(fire.gameObject, 20f); // the smoke drifts off
        }

        static void Charred(Vector3 at, float h, Quaternion rot)
        {
            var go = ArtLibrary.Spawn("Trees/Dead_" + Random.Range(1, 4), null, at, h * 0.75f, ArtLibrary.Fit.Height, rot.eulerAngles.y);
            var black = new Color(0.12f, 0.1f, 0.09f);
            if (go == null)
            {
                go = new GameObject("CharredTree");
                go.transform.position = at;
                Factory.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, h * 0.3f, 0f), new Vector3(0.35f, h * 0.3f, 0.35f), black);
                return;
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.material.color = black;
            // embers glowing in the bark for a while
            var ember = new Color(1f, 0.35f, 0.05f);
            for (int i = 0; i < 4; i++)
            {
                var e = Factory.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, Vector3.one * 0.12f, ember, false, Mat.Glow(ember));
                e.transform.position = at + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.4f, h * 0.5f), Random.Range(-0.25f, 0.25f));
                Destroy(e, Random.Range(30f, 60f));
            }
        }
    }
}
