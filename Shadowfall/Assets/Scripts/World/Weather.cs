using UnityEngine;

namespace Shadowfall
{
    public enum Season { Spring, Summer, Autumn, Winter }

    /// <summary>
    /// Seasons and weather, as the server says (weather.js): clouds that dim the sun, rain with splashes and wet ground,
    /// snow where it's cold (everywhere in winter, the north in spring and autumn) that piles up over time, fog that closes
    /// in, and storms with lightning and thunder. Drives the terrain, grass and water shaders through globals
    /// (_SfSnow, _SfSnowMask, _SfWet, _SfLeaves, _SfFrost) and layers its light and fog on top of <see cref="DayNight"/>.
    /// </summary>
    public class Weather : MonoBehaviour
    {
        public static Weather I;

        public static Season Season { get; private set; } = Season.Summer;
        public static string Sky { get; private set; } = "clear";
        public static float Intensity { get; private set; } = 0.5f;
        /// <summary>Raised when the season changes (and once when the first weather message arrives).</summary>
        public static event System.Action SeasonChanged;
        static bool known;
        static float seasonEndsAt;

        /// <summary>Smoothed: how hard it rains or snows (0..1), how overcast, how foggy, how wet the ground is.</summary>
        public static float Precip { get; private set; }
        public static float Cloud { get; private set; }
        public static float Fog { get; private set; }
        public static float Wet { get; private set; }
        /// <summary>Snow cover 0..1 in the north (Whisperwood) and everywhere else.</summary>
        public static float SnowNorth { get; private set; }
        public static float SnowSouth { get; private set; }

        /// <summary>Where "the north" begins (world z): north of Hollowmere, Whisperwood.</summary>
        public const float NorthLine = 190f, NorthBlend = 24f;

        public static float SecondsUntilNextSeason => Mathf.Max(0f, seasonEndsAt - Time.time);
        /// <summary>
        /// Frostpeak, north of <see cref="PermLine"/> and west of <see cref="EastEdge"/>, keeps snow all year (<see cref="SnowPerm"/>).
        /// The Sunscar Badlands (east of EastEdge, south of <see cref="BadlandsNorth"/>) only get snow in winter, like the south.
        /// </summary>
        public const float PermLine = 400f, EastEdge = 300f, BadlandsNorth = WorldGenerator.OldSize;
        public static float SnowPerm { get; private set; } = 0.75f;

        static bool InBadlands(Vector3 p) => p.x > EastEdge && p.z < BadlandsNorth;
        static bool InFrostpeak(Vector3 p) => p.z > PermLine && p.x < EastEdge;
        public static bool ColdAt(Vector3 p) => Season == Season.Winter || InFrostpeak(p) || (Season != Season.Summer && p.z > NorthLine && !InBadlands(p));
        public static float SnowCoverAt(Vector3 p)
        {
            if (Dungeon.Contains(p)) return 0f;
            float region = Mathf.Clamp01((p.z - NorthLine) / NorthBlend + 0.5f)
                         * (1f - Mathf.Clamp01((p.x - EastEdge) / 24f + 0.5f) * Mathf.Clamp01((BadlandsNorth - p.z) / 24f + 0.5f));
            float perm = SnowPerm * Mathf.Clamp01((p.z - PermLine) / 30f + 0.5f) * Mathf.Clamp01((EastEdge - p.x) / 30f + 0.5f);
            return Mathf.Max(Mathf.Lerp(SnowSouth, SnowNorth, region), perm);
        }
        public static bool SnowingAt(Vector3 p) => Precip > 0.15f && ColdAt(p);
        public static bool RainingAt(Vector3 p) => Precip > 0.15f && !ColdAt(p);
        public static string SeasonName => Season.ToString();

        static readonly int SnowId = Shader.PropertyToID("_SfSnow"), WetId = Shader.PropertyToID("_SfWet"), LeavesId = Shader.PropertyToID("_SfLeaves"),
            FrostId = Shader.PropertyToID("_SfFrost"), WorldId = Shader.PropertyToID("_SfWorld"), Snow2Id = Shader.PropertyToID("_SfSnow2");

        ParticleSystem rain, splash, snow;
        AudioSource rainLoop;
        float leaves, frost, nextBolt = 20f, flash, thunderAt = -1f;
        bool inDungeon;

        public static void Ensure()
        {
            if (I == null) new GameObject("Weather").AddComponent<Weather>();
        }

        void Awake()
        {
            I = this;
            Shader.SetGlobalVector(WorldId, new Vector4(WorldGenerator.W, WorldGenerator.H, 0, 0));
            Push();
        }

        /// <summary>The server's "weather" message.</summary>
        public static void Apply(NetMsg m)
        {
            var season = (Season)Mathf.Clamp(m.s, 0, 3);
            string old = Sky;
            Sky = string.IsNullOrEmpty(m.sky) ? "clear" : m.sky;
            Intensity = Mathf.Clamp01(m.i);
            seasonEndsAt = Time.time + m.left;
            bool seasonChanged = !known || season != Season;
            Season = season;
            if (!known)
            {
                // Coming in: the ground already looks like the season (snow in winter, a little in the north otherwise).
                SnowSouth = season == Season.Winter ? 0.75f : 0f;
                SnowNorth = season == Season.Winter ? 0.9f : season == Season.Summer ? 0f : 0.45f;
                Wet = Sky == "rain" || Sky == "storm" ? 0.7f : 0f;
                Precip = Sky == "rain" ? 0.6f * Intensity : Sky == "storm" ? Intensity : 0f;
                Cloud = CloudFor(Sky);
                Fog = FogFor(Sky);
                if (I != null) { I.leaves = season == Season.Autumn ? 1f : 0f; I.frost = season == Season.Winter ? 1f : 0f; }
            }
            known = true;
            if (seasonChanged) SeasonChanged?.Invoke();
            else if (old != Sky && Player.I != null) AnnounceSky();
        }

        static float CloudFor(string sky) => sky == "cloudy" ? 0.5f : sky == "rain" ? 0.7f : sky == "storm" ? 0.95f : sky == "fog" ? 0.45f : 0f;
        static float FogFor(string sky) => sky == "fog" ? 0.45f + 0.55f * Intensity : sky == "storm" ? 0.35f : sky == "rain" ? 0.2f : 0f;

        static void AnnounceSky()
        {
            var p = Player.I.transform.position;
            bool cold = ColdAt(p);
            string line = Sky == "rain" ? (cold ? "Snow begins to fall." : "It starts to rain.") :
                          Sky == "storm" ? (cold ? "A blizzard howls in." : "Thunder rolls in from the hills. A storm is coming.") :
                          Sky == "fog" ? "A thick fog creeps in." : Sky == "clear" ? "The sky clears." : "Clouds gather overhead.";
            GameUI.Log(line, new Color(0.7f, 0.8f, 0.95f));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var p = Player.I;
            inDungeon = Dungeon.Active;

            float precipTarget = Sky == "rain" ? 0.35f + 0.5f * Intensity : Sky == "storm" ? 0.7f + 0.3f * Intensity : 0f;
            Precip = Mathf.MoveTowards(Precip, precipTarget, dt / 20f);
            Cloud = Mathf.MoveTowards(Cloud, CloudFor(Sky), dt / 30f);
            Fog = Mathf.MoveTowards(Fog, FogFor(Sky) + (Sky == "storm" && Season == Season.Winter ? 0.3f : 0f), dt / 30f);
            leaves = Mathf.MoveTowards(leaves, Season == Season.Autumn ? 1f : 0f, dt / 120f);
            frost = Mathf.MoveTowards(frost, Season == Season.Winter ? 1f : 0f, dt / 90f);

            // Snow piles up while it snows and melts slowly when it doesn't (never quite away in winter).
            bool coldNorth = Season != Season.Summer, coldSouth = Season == Season.Winter;
            SnowNorth = Accumulate(SnowNorth, coldNorth, Season == Season.Winter ? 0.6f : 0f, dt);
            SnowSouth = Accumulate(SnowSouth, coldSouth, Season == Season.Winter ? 0.45f : 0f, dt);
            float permTarget = Season == Season.Winter ? 1f : Season == Season.Autumn ? 0.8f : Season == Season.Spring ? 0.7f : 0.5f;
            SnowPerm = Mathf.MoveTowards(SnowPerm, permTarget, dt / 120f);

            // Rain soaks the ground; it dries out slowly afterwards.
            bool raining = p != null ? RainingAt(p.transform.position) : Precip > 0.15f && Season != Season.Winter;
            Wet = Mathf.Clamp01(Wet + (raining ? Precip * dt / 45f : -dt / 240f));

            Push();
            SnowField.Tick();
            UpdateParticles(p, dt);
            UpdateStorm(p, dt);
        }

        /// <summary>Snow piles up while it snows and melts when it doesn't; in winter it never melts below the floor.</summary>
        float Accumulate(float level, bool cold, float floor, float dt)
        {
            if (cold && Precip > 0.15f) return Mathf.Clamp01(level + Precip * dt / 150f);
            if (cold && level <= floor) return level;
            float melt = Season == Season.Summer ? 1f / 90f : Season == Season.Spring ? 1f / 300f : 1f / 600f;
            if (Precip > 0.15f) melt *= 2.5f; // rain washes it away
            float v = level - melt * dt;
            if (cold) v = Mathf.Max(v, floor);
            return Mathf.Max(0f, v);
        }

        void Push()
        {
            Shader.SetGlobalVector(SnowId, new Vector4(SnowNorth, SnowSouth, NorthLine, NorthBlend));
            Shader.SetGlobalVector(Snow2Id, new Vector4(SnowPerm, PermLine, EastEdge, BadlandsNorth));
            Shader.SetGlobalFloat(WetId, inDungeon ? 0f : Wet);
            Shader.SetGlobalFloat(LeavesId, leaves);
            Shader.SetGlobalFloat(FrostId, frost);
        }

        // ------------------------------------------------------------------ rain, snow, splashes

        void UpdateParticles(Player p, float dt)
        {
            if (rain == null) Build();
            if (rain == null) return;
            bool show = p != null && !inDungeon;
            var at = show ? p.transform.position : Vector3.zero;
            transform.position = at;
            bool cold = show && ColdAt(at);
            float scale = GameSettings.ParticleScale;
            float wind = Sky == "storm" ? 1f : 0.3f;
            SetRate(rain, show && !cold ? Precip * 1400f * scale : 0f);
            SetRate(splash, show && !cold ? Precip * 260f * scale : 0f);
            SetRate(snow, show && cold ? Precip * 520f * scale : 0f);
            var v = snow.velocityOverLifetime;
            v.x = new ParticleSystem.MinMaxCurve(0.4f + wind * 4f, 1.2f + wind * 6f);
            var rv = rain.velocityOverLifetime;
            rv.x = new ParticleSystem.MinMaxCurve(wind * 2f, wind * 4f);

            if (rainLoop == null) rainLoop = Sfx.Loop("rain_loop", 0f);
            if (rainLoop != null) rainLoop.volume = Mathf.MoveTowards(rainLoop.volume, show && !cold ? Precip * 0.55f : 0f, dt * 0.3f);
            Sfx.WindBoost = show ? (Sky == "storm" ? 0.25f : Sky == "rain" && cold ? 0.08f : 0f) * Precip : 0f;
        }

        static void SetRate(ParticleSystem ps, float rate)
        {
            var em = ps.emission;
            em.rateOverTime = rate;
        }

        void Build()
        {
            var mat = SpellFx.SoftMaterial;
            if (mat == null) return;
            rain = System("Rain", mat, 900, new Vector3(36f, 1f, 36f), 16f, new Vector2(0.55f, 0.75f), new Vector2(0.04f, 0.06f),
                new Color(0.75f, 0.82f, 0.95f, 0.28f), new Vector3(0f, -26f, 0f), true);
            snow = System("Snow", mat, 1400, new Vector3(40f, 1f, 40f), 14f, new Vector2(5f, 8f), new Vector2(0.06f, 0.14f),
                new Color(1f, 1f, 1f, 0.85f), new Vector3(0f, -2.2f, 0f), false);
            var sv = snow.velocityOverLifetime;
            sv.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var noise = snow.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.4f;
            splash = System("Splash", mat, 300, new Vector3(28f, 0.1f, 28f), 0.05f, new Vector2(0.15f, 0.25f), new Vector2(0.06f, 0.12f),
                new Color(0.8f, 0.85f, 0.95f, 0.45f), new Vector3(0f, 1.2f, 0f), false);
            var sm = splash.main;
            sm.gravityModifier = 0.8f;
            var size = splash.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));
        }

        ParticleSystem System(string name, Material mat, int max, Vector3 box, float height, Vector2 life, Vector2 size, Color color, Vector3 velocity, bool stretch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(velocity.x, velocity.x);
            vel.y = new ParticleSystem.MinMaxCurve(velocity.y, velocity.y);
            vel.z = new ParticleSystem.MinMaxCurve(velocity.z, velocity.z);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (stretch)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.035f;
                r.lengthScale = 1f;
            }
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ lightning and thunder

        void UpdateStorm(Player p, float dt)
        {
            flash = Mathf.MoveTowards(flash, 0f, dt * 5f);
            if (thunderAt > 0f && Time.time >= thunderAt)
            {
                thunderAt = -1f;
                Sfx.Play2D("thunder", 0.7f, Random.Range(0.85f, 1.05f));
                CameraRig.Shake(0.05f);
            }
            if (p == null || inDungeon || Sky != "storm" || ColdAt(p.transform.position) || Precip < 0.4f) return;
            nextBolt -= dt;
            if (nextBolt > 0f) return;
            nextBolt = Random.Range(7f, 22f) / Mathf.Max(0.5f, Intensity);
            flash = Random.Range(0.8f, 1.4f);
            thunderAt = Time.time + Random.Range(0.4f, 2.5f);
            if (Random.value < 0.45f) WeatherDetail.Strike(p.transform.position); // now and then a tree near by is hit
        }

        /// <summary>After DayNight has set the light and fog for the hour: clouds, fog, snow glare and lightning on top.</summary>
        void LateUpdate()
        {
            var cam0 = GameManager.I != null ? GameManager.I.Cam : null;
            if (inDungeon)
            {
                if (cam0 != null) cam0.farClipPlane = Mathf.Clamp(RenderSettings.fogEndDistance + 15f, 50f, 200f);
                return;
            }
            float dim = 1f - 0.5f * Cloud;
            var sun = GameSettings.Sun;
            if (sun != null) sun.intensity = sun.intensity * dim + flash * 1.6f;
            float amb = 1f - 0.3f * Cloud;
            var p = Player.I;
            float cover = p != null ? SnowCoverAt(p.transform.position) : 0f;
            RenderSettings.ambientSkyColor = RenderSettings.ambientSkyColor * amb + Color.white * flash * 0.5f;
            RenderSettings.ambientEquatorColor = RenderSettings.ambientEquatorColor * amb + Color.white * flash * 0.35f;
            // snow throws light back up
            RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, RenderSettings.ambientSkyColor * 0.8f, cover * 0.6f);

            var fog = RenderSettings.fogColor;
            float grey = fog.r * 0.3f + fog.g * 0.59f + fog.b * 0.11f;
            var murk = new Color(grey, grey * 1.02f, grey * 1.06f) * (1f + Fog * 0.8f);
            RenderSettings.fogColor = Color.Lerp(fog, murk, Mathf.Max(Fog, Cloud * 0.5f));
            RenderSettings.fogStartDistance = Mathf.Lerp(RenderSettings.fogStartDistance, 4f, Fog);
            RenderSettings.fogEndDistance = Mathf.Lerp(RenderSettings.fogEndDistance, 34f, Fog);
            var cam = GameManager.I != null ? GameManager.I.Cam : null;
            if (cam != null)
            {
                cam.backgroundColor = RenderSettings.fogColor;
                // Nothing beyond the fog can be seen: don't draw it.
                cam.farClipPlane = Mathf.Clamp(RenderSettings.fogEndDistance + 15f, 50f, 200f);
            }
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }
    }
}
