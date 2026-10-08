using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The weather on a body: clothes soak dark (and pick up a sheen) after a while in the rain, a dusting of snow
    /// frosts them pale in a snowfall, and both dry off slowly once it stops. People (not skeletons, golems or wolves)
    /// also breathe steam in the cold, nearby ones only. On every CharacterView; the hero's own breath is WeatherDetail's.
    /// </summary>
    public class CharacterWeather : MonoBehaviour
    {
        static readonly int RoughId = Shader.PropertyToID("roughnessFactor"), SmoothId = Shader.PropertyToID("_Smoothness");
        static readonly Color Frost = new Color(0.9f, 0.94f, 1f);
        const float BreathRange = 20f;

        public bool Breathes;
        public float Height = 1.8f;

        float wet, snow, shownWet, shownSnow, nextTick, nextBreath;
        readonly List<Material> mats = new List<Material>();
        readonly List<Color> baseColors = new List<Color>();
        readonly List<float> baseRough = new List<float>();
        static int breathsThisSecond;
        static float breathWindow;

        public static void Add(GameObject root, CharacterLook look)
        {
            var w = root.AddComponent<CharacterWeather>();
            w.Height = look.Height;
            string m = look.Model ?? "";
            w.Breathes = (look.Anims == AnimSet.KayKit || look.Anims == AnimSet.Kenney) && !m.Contains("Skeleton") && !m.Contains("Zombie");
            w.nextTick = Time.time + Random.value * 0.5f;
            w.nextBreath = Time.time + Random.Range(0.5f, 3f);
        }

        /// <summary>Not for a portrait or showcase model (the studio is not out in the weather).</summary>
        public static void Off(CharacterView view)
        {
            if (view == null) return;
            var w = view.Root.GetComponent<CharacterWeather>();
            if (w != null) Destroy(w);
        }

        void Update()
        {
            if (Time.time < nextTick) return;
            float dt = 0.5f + (Time.time - nextTick);
            nextTick = Time.time + 0.5f;

            var at = transform.position;
            bool indoorsOrUnder = Dungeon.Active;
            bool rain = !indoorsOrUnder && Weather.RainingAt(at);
            bool snowing = !indoorsOrUnder && Weather.SnowingAt(at);
            // Soaked through in about half a minute of rain, dry again over two minutes; snow settles a little slower
            wet = Mathf.Clamp01(wet + (rain ? dt * Weather.Precip / 30f : -dt / 120f));
            snow = Mathf.Clamp01(snow + (snowing ? dt * Weather.Precip / 45f : -dt / (Weather.ColdAt(at) ? 150f : 40f)));
            if (Mathf.Abs(wet - shownWet) > 0.03f || Mathf.Abs(snow - shownSnow) > 0.03f || (wet == 0f && shownWet > 0f) || (snow == 0f && shownSnow > 0f))
                Apply();

            if (Breathes && Time.time >= nextBreath) Breath(at);
        }

        void Apply()
        {
            if (mats.Count == 0 || (shownWet == 0f && shownSnow == 0f)) Collect();
            shownWet = wet;
            shownSnow = snow;
            float dark = Mathf.Lerp(1f, 0.66f, wet * (1f - snow * 0.5f));
            for (int i = 0; i < mats.Count; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                var c = baseColors[i];
                var tinted = new Color(c.r * dark, c.g * dark, c.b * dark, c.a);
                m.color = Color.Lerp(tinted, new Color(Frost.r, Frost.g, Frost.b, c.a), snow * 0.32f);
                if (baseRough[i] >= 0f)
                {
                    float r = Mathf.Lerp(baseRough[i], 0.42f, wet);
                    if (m.HasProperty(RoughId)) m.SetFloat(RoughId, r);
                    if (m.HasProperty(SmoothId)) m.SetFloat(SmoothId, 1f - r);
                }
            }
        }

        /// <summary>Takes this body's own copies of its materials (weapons and gear may have changed since last time).</summary>
        void Collect()
        {
            // put back what there was before taking stock again
            for (int i = 0; i < mats.Count; i++) if (mats[i] != null) mats[i].color = baseColors[i];
            mats.Clear();
            baseColors.Clear();
            baseRough.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                foreach (var m in r.materials)
                {
                    if (m == null || !m.HasProperty("_Color") && !m.HasProperty("baseColorFactor")) continue;
                    mats.Add(m);
                    baseColors.Add(m.color);
                    baseRough.Add(m.HasProperty(RoughId) ? m.GetFloat(RoughId) : m.HasProperty(SmoothId) ? 1f - m.GetFloat(SmoothId) : -1f);
                }
            }
        }

        void Breath(Vector3 at)
        {
            nextBreath = Time.time + Random.Range(2f, 3.2f);
            if (!SpellFx.Ready || Dungeon.Active || !Weather.ColdAt(at)) return;
            var hero = Player.I;
            if (hero == null || Factory.FlatDistance(hero.transform.position, at) > BreathRange || transform.IsChildOf(hero.transform)) return;
            if (!IsVisible()) return;
            // a handful of puffs a second at most, however crowded the square
            if (Time.time - breathWindow > 1f) { breathWindow = Time.time; breathsThisSecond = 0; }
            if (++breathsThisSecond > 6) return;
            var fwd = transform.forward;
            SpellFx.Emit(new SpellFx.P
            {
                Burst = 4, Duration = 0.1f, Life = new Vector2(0.6f, 1.1f), Speed = new Vector2(0.15f, 0.3f), Size = new Vector2(0.07f, 0.14f) * (Height / 1.8f),
                Start = new Color(0.95f, 0.97f, 1f, 0.4f), End = new Color(0.95f, 0.97f, 1f, 0f), Velocity = fwd * 0.3f + Vector3.up * 0.15f,
                Smoke = true, Grow = true, Radius = 0.04f,
            }, at + Vector3.up * Height * 0.88f + fwd * 0.15f * Height);
        }

        SkinnedMeshRenderer[] skins;

        bool IsVisible()
        {
            if (skins == null) skins = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var r in skins) if (r != null && r.enabled && r.isVisible) return true;
            return false;
        }
    }
}
