using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Day/night cycle. The time of day comes from the server's clock (sent at login), so every player
    /// sees the same sunset. One in-game day lasts <see cref="CycleMinutes"/> real minutes.
    /// Drives the sun/moon light, ambient light, fog and every <see cref="NightLight"/>.
    /// </summary>
    public class DayNight : MonoBehaviour
    {
        public static DayNight I;
        public const float CycleMinutes = 48f; // 2 real minutes per in-game hour

        /// <summary>0 in full daylight, 1 at night.</summary>
        public static float Night { get; private set; }
        /// <summary>In-game hour, 0..24.</summary>
        public static float Hour { get; private set; } = 12f;

        static double serverOffsetMs;  // server clock - local clock
        public static void SyncServerTime(double serverNowMs)
        {
            if (serverNowMs > 0) serverOffsetMs = serverNowMs - NowMs();
        }

        static double NowMs() => (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalMilliseconds;

        public static string Clock
        {
            get
            {
                int h = Mathf.FloorToInt(Hour), m = Mathf.FloorToInt((Hour - h) * 60f) / 10 * 10;
                return h.ToString("00") + ":" + m.ToString("00");
            }
        }

        public static string Phase =>
            Hour < 5f || Hour >= 21f ? "Night" : Hour < 8f ? "Dawn" : Hour < 18f ? "Day" : "Dusk";

        Light sun;
        Camera cam;

        // Keyframes: hour, sun color, sun intensity, ambient sky, ambient horizon, ambient ground, fog, night factor
        struct Key
        {
            public float H, Sun, Night;
            public Color SunC, Sky, Eq, Gnd, Fog;
        }

        static Key K(float h, Color sunC, float sun, Color sky, Color eq, Color gnd, Color fog, float night) =>
            new Key { H = h, SunC = sunC, Sun = sun, Sky = sky, Eq = eq, Gnd = gnd, Fog = fog, Night = night };

        static readonly Color MoonC = new Color(0.55f, 0.66f, 1f);
        static readonly Key[] keys =
        {
            K(0f,    MoonC,                         0.42f, new Color(0.13f, 0.16f, 0.27f), new Color(0.09f, 0.1f, 0.15f),  new Color(0.04f, 0.04f, 0.05f), new Color(0.03f, 0.04f, 0.07f), 1f),
            K(4.5f,  MoonC,                         0.42f, new Color(0.13f, 0.16f, 0.27f), new Color(0.09f, 0.1f, 0.15f),  new Color(0.04f, 0.04f, 0.05f), new Color(0.03f, 0.04f, 0.07f), 1f),
            K(6f,    new Color(1f, 0.55f, 0.35f),   0.12f, new Color(0.3f, 0.28f, 0.38f),  new Color(0.3f, 0.22f, 0.22f),  new Color(0.1f, 0.08f, 0.08f),  new Color(0.16f, 0.11f, 0.12f), 0.6f),
            K(7.2f,  new Color(1f, 0.72f, 0.5f),    0.8f,  new Color(0.45f, 0.45f, 0.55f), new Color(0.4f, 0.33f, 0.3f),   new Color(0.16f, 0.14f, 0.12f), new Color(0.24f, 0.2f, 0.2f),   0.15f),
            K(9f,    new Color(1f, 0.9f, 0.76f),    1.1f,  new Color(0.36f, 0.4f, 0.5f),   new Color(0.27f, 0.26f, 0.27f), new Color(0.12f, 0.1f, 0.09f),  new Color(0.12f, 0.12f, 0.14f), 0f),
            K(16f,   new Color(1f, 0.86f, 0.7f),    1.1f,  new Color(0.36f, 0.4f, 0.5f),   new Color(0.27f, 0.26f, 0.27f), new Color(0.12f, 0.1f, 0.09f),  new Color(0.12f, 0.12f, 0.14f), 0f),
            K(18.5f, new Color(1f, 0.55f, 0.3f),    0.8f,  new Color(0.42f, 0.36f, 0.48f), new Color(0.42f, 0.28f, 0.24f), new Color(0.15f, 0.1f, 0.1f),   new Color(0.22f, 0.13f, 0.12f), 0.25f),
            K(20f,   new Color(0.85f, 0.4f, 0.4f),  0.12f, new Color(0.2f, 0.18f, 0.32f),  new Color(0.16f, 0.12f, 0.18f), new Color(0.06f, 0.05f, 0.06f), new Color(0.08f, 0.06f, 0.1f),  0.75f),
            K(21.5f, MoonC,                         0.42f, new Color(0.13f, 0.16f, 0.27f), new Color(0.09f, 0.1f, 0.15f),  new Color(0.04f, 0.04f, 0.05f), new Color(0.03f, 0.04f, 0.07f), 1f),
            K(24f,   MoonC,                         0.42f, new Color(0.13f, 0.16f, 0.27f), new Color(0.09f, 0.1f, 0.15f),  new Color(0.04f, 0.04f, 0.05f), new Color(0.03f, 0.04f, 0.07f), 1f),
        };

        public void Init(Light sunLight, Camera camera)
        {
            I = this;
            sun = sunLight;
            cam = camera;
            Apply();
        }

        void Update() => Apply();

        /// <summary>Underground: no sun or moon, a dim cold ambient, black fog close by. Torches do the work.</summary>
        void ApplyDungeon()
        {
            Night = 1f;
            RenderSettings.ambientSkyColor = new Color(0.09f, 0.085f, 0.11f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.06f, 0.065f);
            RenderSettings.ambientGroundColor = new Color(0.03f, 0.03f, 0.03f);
            RenderSettings.fogColor = new Color(0.01f, 0.008f, 0.01f);
            RenderSettings.fogStartDistance = 12f;
            RenderSettings.fogEndDistance = 42f;
            if (cam != null) cam.backgroundColor = RenderSettings.fogColor;
            if (sun != null) { sun.intensity = 0.04f; sun.color = new Color(0.5f, 0.55f, 0.8f); }
        }

        void Apply()
        {
            double cycleMs = CycleMinutes * 60000.0;
            double t = ((NowMs() + serverOffsetMs) % cycleMs + cycleMs) % cycleMs / cycleMs;
            Hour = (float)(t * 24.0);

            if (Dungeon.Active) { ApplyDungeon(); return; }
            RenderSettings.fogStartDistance = 34f;
            RenderSettings.fogEndDistance = 85f;

            int i = 0;
            while (i < keys.Length - 2 && Hour >= keys[i + 1].H) i++;
            var a = keys[i];
            var b = keys[i + 1];
            float f = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.H, b.H, Hour));

            Night = Mathf.Lerp(a.Night, b.Night, f);
            var fog = Color.Lerp(a.Fog, b.Fog, f);
            RenderSettings.ambientSkyColor = Color.Lerp(a.Sky, b.Sky, f);
            RenderSettings.ambientEquatorColor = Color.Lerp(a.Eq, b.Eq, f);
            RenderSettings.ambientGroundColor = Color.Lerp(a.Gnd, b.Gnd, f);
            RenderSettings.fogColor = fog;
            if (cam != null) cam.backgroundColor = fog;

            if (sun == null) return;
            sun.color = Color.Lerp(a.SunC, b.SunC, f);
            sun.intensity = Mathf.Lerp(a.Sun, b.Sun, f);
            sun.shadowStrength = Mathf.Lerp(0.85f, 0.55f, Night);

            // Sun from 6:00 (east) to 20:00 (west); the moon takes over for the night. The swaps happen while
            // the light is at its dimmest, so the shadow jump is hard to notice.
            float yaw, elev;
            if (Hour >= 6f && Hour < 20f)
            {
                float p = (Hour - 6f) / 14f;
                yaw = -110f + p * 220f;
                elev = 14f + Mathf.Sin(p * Mathf.PI) * 48f;
            }
            else
            {
                float p = ((Hour - 20f + 24f) % 24f) / 10f;
                yaw = 70f + p * 140f;
                elev = 22f + Mathf.Sin(p * Mathf.PI) * 33f;
            }
            sun.transform.rotation = Quaternion.Euler(elev, yaw, 0f);
        }
    }

    /// <summary>
    /// A light that brightens at night: lanterns, braziers, windows. <see cref="DayFactor"/> is how bright
    /// it is during the day (0 = off, 1 = always full).
    /// </summary>
    public class NightLight
    {
        public float BaseIntensity = 1f, DayFactor = 0f, BaseRange = 6f;
        float rangeBoost = 1.3f;
        Light l;

        static readonly List<NightLight> all = new List<NightLight>();
        static int cursor;

        public static NightLight Add(Light light, float dayFactor, float nightRangeBoost = 1.3f)
        {
            var n = new NightLight { l = light, BaseIntensity = light.intensity, BaseRange = light.range, DayFactor = dayFactor, rangeBoost = nightRangeBoost };
            all.Add(n);
            if (updater == null) updater = new GameObject("NightLights").AddComponent<NightLightUpdater>();
            return n;
        }

        static NightLightUpdater updater;

        /// <summary>
        /// Night changes slowly: one shared updater goes through an eighth of the lights each frame (every light about
        /// eight times a second), instead of every lantern running its own Update. Lights further than
        /// <see cref="GameSettings.LightCullDistance"/> from the hero are switched off: nobody sees them, they only cost.
        /// </summary>
        internal static void Step()
        {
            if (all.Count == 0) return;
            var hero = Player.I;
            Vector3 at = hero != null ? hero.transform.position : Vector3.zero;
            float cull = GameSettings.LightCullDistance, cull2 = cull * cull, night = DayNight.Night;
            int n = Mathf.Max(1, all.Count / 8);
            for (int i = 0; i < n; i++)
            {
                if (cursor >= all.Count) cursor = 0;
                var x = all[cursor];
                if (x.l == null) { all.RemoveAt(cursor); continue; }
                cursor++;
                float k = Mathf.Lerp(x.DayFactor, 1f, night);
                bool near = hero == null || (x.l.transform.position - at).sqrMagnitude < cull2;
                x.l.intensity = x.BaseIntensity * k;
                x.l.range = x.BaseRange * Mathf.Lerp(1f, x.rangeBoost, night);
                x.l.enabled = k > 0.02f && near;
            }
        }
    }

    public class NightLightUpdater : MonoBehaviour
    {
        void Update() => NightLight.Step();
    }
}
