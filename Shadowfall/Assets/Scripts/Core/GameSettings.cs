using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Player options stored in PlayerPrefs: the graphics preset (Low / Medium / High) and the individual graphics
    /// options it sets, the FPS counter and the loot filter. <see cref="Apply"/> pushes the graphics options into Unity.
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

        /// <summary>
        /// The graphics preset (Low / Medium / High). Choosing one sets every option on the Graphics page; changing an
        /// option afterwards makes it "Custom" (<see cref="IsCustom"/>). New players start on Medium.
        /// </summary>
        public static int Quality
        {
            get { if (quality < 0) quality = Mathf.Clamp(Load("sf_quality", 1), 0, 2); return quality; }
            set
            {
                quality = Mathf.Clamp(value, 0, 2);
                Store("sf_quality", quality);
                for (int i = 0; i < Options.Length; i++) Options[i].Set(Options[i].Presets[quality], false);
                Apply();
            }
        }

        /// <summary>True when some option differs from what the preset would set.</summary>
        public static bool IsCustom
        {
            get
            {
                foreach (var o in Options) if (o.Value != o.Presets[Quality]) return true;
                return false;
            }
        }

        // ------------------------------------------------------------------ the options on the Graphics page

        public class Option
        {
            public string Key, Name, Help;
            public string[] Choices;
            public int[] Presets;   // the choice for Low, Medium, High
            int value = -1;

            public int Value
            {
                get { if (value < 0) value = Mathf.Clamp(Load(Key, Presets[Quality]), 0, Choices.Length - 1); return value; }
            }

            public void Set(int v, bool apply = true)
            {
                value = Mathf.Clamp(v, 0, Choices.Length - 1);
                Store(Key, value);
                if (apply) Apply();
            }
        }

        public static readonly Option Resolution = new Option
        {
            Key = "sf_g_resolution", Name = "Resolution", Choices = new[] { "60%", "75%", "100%", "150%", "Native" }, Presets = new[] { 1, 2, 2 },
            Help = "How many pixels the game draws, compared with the page's size. 100% is one game pixel per page pixel; " +
                   "Native uses every pixel of a high-resolution (Retina) screen: sharpest, but up to four times the work. " +
                   "The biggest single setting for speed on laptops.",
        };
        public static readonly Option FrameRate = new Option
        {
            Key = "sf_g_fps", Name = "Frame rate", Choices = new[] { "30", "60", "Unlimited" }, Presets = new[] { 1, 1, 1 },
            Help = "The most frames a second the game draws. 30 halves the work (cooler, quieter, longer battery); Unlimited follows the screen.",
        };
        public static readonly Option Shadows = new Option
        {
            Key = "sf_g_shadows", Name = "Shadows", Choices = new[] { "Off", "Hard", "Soft" }, Presets = new[] { 0, 1, 2 },
            Help = "Shadows from the sun and moon. Soft shadows have blurred edges and cost the most.",
        };
        public static readonly Option ShadowRange = new Option
        {
            Key = "sf_g_shadowrange", Name = "Shadow distance", Choices = new[] { "Short", "Medium", "Far" }, Presets = new[] { 0, 1, 2 },
            Help = "How far from the camera shadows are drawn.",
        };
        public static readonly Option Lights = new Option
        {
            Key = "sf_g_lights", Name = "Lights", Choices = new[] { "Few", "Some", "Many" }, Presets = new[] { 0, 1, 2 },
            Help = "Lanterns, torches, windows and spells: how many light each object at once, and how far away they still shine. " +
                   "Every light on an object draws it once more, so towns at night are where this matters.",
        };
        public static readonly Option Grass = new Option
        {
            Key = "sf_g_grass", Name = "Grass", Choices = new[] { "Off", "Near", "Far" }, Presets = new[] { 0, 1, 2 },
            Help = "Grass blades on the ground, drawn up to 30 (Near) or 60 (Far) paces away.",
        };
        public static readonly Option Details = new Option
        {
            Key = "sf_g_details", Name = "Small details", Choices = new[] { "Off", "On" }, Presets = new[] { 0, 1, 1 },
            Help = "Flowers, ferns, pebbles, mushrooms and grass tufts scattered over the ground.",
        };
        public static readonly Option Effects = new Option
        {
            Key = "sf_g_effects", Name = "Effects", Choices = new[] { "Low", "High" }, Presets = new[] { 0, 1, 1 },
            Help = "Spell and weather particles, blood stains and the colour grade. Low halves the particles and turns the colour grade off.",
        };

        public static readonly Option[] Options = { Resolution, FrameRate, Shadows, ShadowRange, Lights, Grass, Details, Effects };

        /// <summary>The resolution choices as a multiple of the page's (CSS) pixels; Native = the screen's own ratio.</summary>
        static readonly float[] ResolutionScale = { 0.6f, 0.75f, 1f, 1.5f, 99f };
        public static float ResolutionMultiplier => ResolutionScale[Resolution.Value];

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

        // ---- comfort: how hard the game hits your eyes

        static float shake = -1f;

        /// <summary>Multiplier on camera shake (0 = none .. 1 = full).</summary>
        public static float ShakeScale
        {
            get { if (shake < 0f) { try { shake = PlayerPrefs.GetFloat("sf_shake", 1f); } catch (System.Exception) { shake = 1f; } } return shake; }
            set { shake = Mathf.Clamp01(value); try { PlayerPrefs.SetFloat("sf_shake", shake); PlayerPrefs.Save(); } catch (System.Exception) { } }
        }

        static int hitPauses = -1, flashes = -1, damageNumbers = -1, colorBlind = -1;

        /// <summary>The freeze-frame on crits and killing blows, and a boss's slow-motion death.</summary>
        public static bool HitPauses
        {
            get { if (hitPauses < 0) hitPauses = Load("sf_hitpause", 1); return hitPauses == 1; }
            set { hitPauses = value ? 1 : 0; Store("sf_hitpause", hitPauses); }
        }

        /// <summary>Full-screen flashes (dying, lightning, level up...).</summary>
        public static bool Flashes
        {
            get { if (flashes < 0) flashes = Load("sf_flashes", 1); return flashes == 1; }
            set { flashes = value ? 1 : 0; Store("sf_flashes", flashes); }
        }

        /// <summary>0 = all damage numbers, 1 = crits and hits on you only, 2 = none.</summary>
        public static int DamageNumbers
        {
            get { if (damageNumbers < 0) damageNumbers = Mathf.Clamp(Load("sf_dmgnum", 0), 0, 2); return damageNumbers; }
            set { damageNumbers = Mathf.Clamp(value, 0, 2); Store("sf_dmgnum", damageNumbers); }
        }
        public static readonly string[] DamageNumberNames = { "All", "Big only", "Off" };

        /// <summary>Loot colours told apart without red/green: set items turn teal, legendaries magenta.</summary>
        public static bool ColorBlindLoot
        {
            get { if (colorBlind < 0) colorBlind = Load("sf_cbloot", 0); return colorBlind == 1; }
            set { colorBlind = value ? 1 : 0; Store("sf_cbloot", colorBlind); }
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

        public static float ShadowDistanceScale => ShadowRange.Value == 0 ? 0.45f : ShadowRange.Value == 1 ? 0.7f : 1f;
        public static bool ColorGrading => Effects.Value > 0;
        /// <summary>Particle effects are thinned out on Low effects.</summary>
        public static float ParticleScale => Effects.Value == 0 ? 0.5f : 1f;
        /// <summary>Lights further than this from the hero are switched off.</summary>
        public static float LightCullDistance => Lights.Value == 0 ? 25f : Lights.Value == 1 ? 35f : 45f;
        /// <summary>Grass is drawn up to this far from the hero (0 = no grass).</summary>
        public static float GrassDistance => Grass.Value == 0 ? 0f : Grass.Value == 1 ? 30f : 60f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void SF_SetRenderScale(float scale);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void SF_SetFrameCap(int fps);
#else
        static void SF_SetRenderScale(float scale) { }
#endif

        public static void Apply()
        {
            QualitySettings.pixelLightCount = Lights.Value == 0 ? 1 : Lights.Value == 1 ? 2 : 4; // each extra pixel light is one more pass per lit object
            QualitySettings.shadowCascades = Shadows.Value == 2 && ShadowRange.Value == 2 ? 2 : 1;
            QualitySettings.antiAliasing = 0;
            if (Sun != null) Sun.shadows = Shadows.Value == 0 ? LightShadows.None : Shadows.Value == 1 ? LightShadows.Hard : LightShadows.Soft;
            if (GroundSurface.GrassRoot != null) GroundSurface.GrassRoot.SetActive(Grass.Value > 0);
            if (WorldGenerator.DetailRoot != null) WorldGenerator.DetailRoot.gameObject.SetActive(Details.Value > 0);
            int cap = FrameRate.Value == 0 ? 30 : FrameRate.Value == 1 ? 60 : 0;
#if UNITY_WEBGL && !UNITY_EDITOR
            // In the browser Unity's own cap (targetFrameRate) times frames with setTimeout, out of step with the screen:
            // a 60 Hz screen then often shows 30. So the game runs on every animation frame (-1) and the page skips
            // whole screen refreshes to keep under the cap (index.html).
            Application.targetFrameRate = -1;
            SF_SetFrameCap(cap);
#else
            Application.targetFrameRate = cap == 0 ? -1 : cap;
#endif
            SF_SetRenderScale(ResolutionMultiplier); // the page also reads it at the next start (see the WebGL template)
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
