using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Player options stored in PlayerPrefs: graphics quality (Low / Medium / High), the FPS counter and the
    /// loot filter. <see cref="Apply"/> pushes the graphics level into Unity's quality settings.
    /// </summary>
    public static class GameSettings
    {
        public static readonly string[] QualityNames = { "Low", "Medium", "High" };
        public static Light Sun;

        static int quality = -1;
        static int showFps = -1, showCommon = -1;

        static int Load(string key, int fallback)
        {
            try { return PlayerPrefs.GetInt(key, fallback); } catch (System.Exception) { return fallback; }
        }

        static void Store(string key, int v)
        {
            try { PlayerPrefs.SetInt(key, v); PlayerPrefs.Save(); } catch (System.Exception) { }
        }

        public static int Quality
        {
            get { if (quality < 0) quality = Mathf.Clamp(Load("sf_quality", 2), 0, 2); return quality; }
            set { quality = Mathf.Clamp(value, 0, 2); Store("sf_quality", quality); Apply(); }
        }

        static int gore = -1;

        /// <summary>0 = off (no blood), 1 = normal, 2 = extra (more of everything, chunks on every kill, longer-lasting stains).</summary>
        public static int Gore
        {
            get { if (gore < 0) gore = Mathf.Clamp(Load("sf_gore", 1), 0, 2); return gore; }
            set { gore = Mathf.Clamp(value, 0, 2); Store("sf_gore", gore); if (gore == 0) Shadowfall.Gore.Clear(); }
        }
        public static readonly string[] GoreNames = { "Off", "Normal", "Extra" };

        public static bool ShowFps
        {
            get { if (showFps < 0) showFps = Load("sf_fps", 0); return showFps == 1; }
            set { showFps = value ? 1 : 0; Store("sf_fps", showFps); }
        }

        /// <summary>Show labels for common (white) equipment on the ground. Off: hold Alt to see them.</summary>
        public static bool ShowCommonLoot
        {
            get { if (showCommon < 0) showCommon = Load("sf_common_loot", 1); return showCommon == 1; }
            set { showCommon = value ? 1 : 0; Store("sf_common_loot", showCommon); }
        }

        static int minimapRotate = -1;

        /// <summary>The minimap turns with the camera (true), or keeps north up. Toggled on the minimap itself.</summary>
        public static bool MinimapRotate
        {
            get { if (minimapRotate < 0) minimapRotate = Load("sf_minimap_rotate", 1); return minimapRotate == 1; }
            set { minimapRotate = value ? 1 : 0; Store("sf_minimap_rotate", minimapRotate); }
        }

        static float uiScale = -1f;

        /// <summary>Multiplier on the size of the whole interface (0.7 .. 1.5).</summary>
        public static float UiScale
        {
            get
            {
                if (uiScale < 0f)
                {
                    try { uiScale = PlayerPrefs.GetFloat("sf_ui_scale", 1f); } catch (System.Exception) { uiScale = 1f; }
                    uiScale = Mathf.Clamp(uiScale, 0.7f, 1.5f);
                }
                return uiScale;
            }
            set
            {
                uiScale = Mathf.Clamp(value, 0.7f, 1.5f);
                try { PlayerPrefs.SetFloat("sf_ui_scale", uiScale); PlayerPrefs.Save(); } catch (System.Exception) { }
            }
        }

        public static float ShadowDistanceScale => Quality == 0 ? 0.5f : Quality == 1 ? 0.75f : 1f;
        public static bool ColorGrading => Quality > 0;
        /// <summary>Particle effects are thinned out on Low.</summary>
        public static float ParticleScale => Quality == 0 ? 0.5f : 1f;

        public static void Apply()
        {
            int q = Quality;
            QualitySettings.pixelLightCount = q == 0 ? 1 : q == 1 ? 3 : 4; // each extra pixel light is one more pass per lit object
            QualitySettings.shadowCascades = q == 2 ? 2 : 1;
            QualitySettings.antiAliasing = 0;
            if (Sun != null) Sun.shadows = q == 0 ? LightShadows.None : q == 1 ? LightShadows.Hard : LightShadows.Soft;
            if (GroundSurface.GrassRoot != null) GroundSurface.GrassRoot.SetActive(q > 0);
        }
    }

    /// <summary>Smoothed frames-per-second counter for the HUD.</summary>
    public static class FpsMeter
    {
        static float smoothed = 60f;

        public static float Fps
        {
            get
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > 0f) smoothed = Mathf.Lerp(smoothed, 1f / dt, 0.05f);
                return smoothed;
            }
        }
    }
}
