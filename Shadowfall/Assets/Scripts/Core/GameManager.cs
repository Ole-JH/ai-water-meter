using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Creates the game when any scene loads: no prefabs or scene setup required.
    /// Open an empty scene and press Play (or build it for WebGL).
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindAnyObjectByType<GameManager>() != null) return;
            new GameObject("GameManager").AddComponent<GameManager>();
        }
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager I;
        public Camera Cam { get; private set; }
        public WorldGenerator World { get; private set; }
        public Vector3 SpawnPoint => World.SpawnPoint;
        public string GridHash { get; private set; }

        /// <summary>Uses the server's walkability map instead of the one generated here (see NetClient "grid").</summary>
        public void AdoptServerGrid(int w, int h, string cells, string hash)
        {
            var grid = WorldGrid.Instance;
            if (grid == null || grid.Width != w || grid.Height != h || string.IsNullOrEmpty(cells)) return;
            grid.Unpack(System.Convert.FromBase64String(cells));
            GridHash = hash;
            Debug.LogWarning("[Shadowfall] This client generated a different world map than the server's; using the server's (" + hash + ").");
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Application.runInBackground = true; // keep the connection alive in a background tab/window

            SetupCamera();
            SetupLighting();

            World = new WorldGenerator();
            World.Generate();
            GridHash = WorldGrid.Instance.Hash();
            TownLife.Spawn(null);                  // visual only: after the hash, so it can never affect it
            DungeonEntrance.SpawnAll();
            StashChest.Spawn();
            gameObject.AddComponent<Ambience>();
            Music.Ensure();
            Weather.Ensure();
            SeasonalTown.Ensure();

            gameObject.AddComponent<NetClient>();
            gameObject.AddComponent<GameUI>();
        }

        void SetupCamera()
        {
            Cam = Camera.main;
            if (Cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                Cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            Cam.fieldOfView = 40f;
            Cam.nearClipPlane = 0.5f;
            Cam.farClipPlane = 200f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            if (Cam.GetComponent<CameraRig>() == null) Cam.gameObject.AddComponent<CameraRig>();
            if (Cam.GetComponent<ColorGrade>() == null) Cam.gameObject.AddComponent<ColorGrade>();
        }

        void SetupLighting()
        {
            Light sun = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            sun.color = new Color(1f, 0.92f, 0.8f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            QualitySettings.shadowDistance = 70f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.pixelLightCount = 6; // lanterns and torches at night

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.55f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.38f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.07f);
            RenderSettings.fogStartDistance = 34f;
            RenderSettings.fogEndDistance = 85f;

            gameObject.AddComponent<DayNight>().Init(sun, Cam);
            GameSettings.Sun = sun;
            GameSettings.Apply();
        }

        /// <summary>Called when the server accepts our login.</summary>
        public void EnterWorld(string characterName, SaveData save, string look)
        {
            LeaveWorld();
            LoginShowcase.Hide();
            Avatar.Ensure();
            var player = Player.Create(SpawnPoint, look);
            player.DisplayName = characterName;
            if (save != null) player.LoadSave(save);
            Cam.GetComponent<CameraRig>().Target = player.transform;

            GameUI.Log("Welcome to Shadowfall, " + characterName + "!", new Color(1f, 0.85f, 0.4f));
            if (save == null || save.level <= 1)
                GameUI.Log("Talk to the villagers with a yellow '!' above their heads. Press F1 for controls, Enter to chat.", new Color(1f, 0.85f, 0.4f));
        }

        /// <summary>Tear down everything that belongs to a play session (on logout / disconnect).</summary>
        public void LeaveWorld()
        {
            Gore.Clear();
            if (Player.I != null) { Destroy(Player.I.gameObject); Player.I = null; }
            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) Destroy(e.gameObject);
            foreach (var r in FindObjectsByType<RemotePlayer>(FindObjectsSortMode.None)) Destroy(r.gameObject);
            foreach (var l in FindObjectsByType<LootDrop>(FindObjectsSortMode.None)) Destroy(l.gameObject);
            Enemy.ById.Clear();
            RemotePlayer.ById.Clear();
            var rig = Cam != null ? Cam.GetComponent<CameraRig>() : null;
            if (rig != null) rig.Target = null;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }
    }
}
