using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Sound effects. Clips live in Assets/Resources/Audio as key_N.ogg (built by tools/audio/build_audio.py);
    /// Play("swing", pos) picks a random variant with a little pitch variation. Sounds are positional, heard
    /// from the hero's position (the camera floats too far above for natural falloff), plus ambience loops.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        static Sfx I;
        static Dictionary<string, List<AudioClip>> clips;
        static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        // Own RNG: sounds are placed during world generation, which must not consume UnityEngine.Random
        // (that would change the world layout and its hash).
        static readonly System.Random rng = new System.Random();
        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        const int PoolSize = 24;
        readonly AudioSource[] pool = new AudioSource[PoolSize];
        int next;
        Transform ears;
        AudioSource wind, crickets;
        float nextHowl;

        /// <summary>Master volume 0..1, saved between sessions.</summary>
        public static float Volume
        {
            get => AudioListener.volume;
            set
            {
                AudioListener.volume = Mathf.Clamp01(value);
                try { PlayerPrefs.SetFloat("sf_volume", AudioListener.volume); } catch (System.Exception) { }
            }
        }

        static Sfx Instance
        {
            get
            {
                if (I == null) I = new GameObject("Sfx").AddComponent<Sfx>();
                return I;
            }
        }

        void Awake()
        {
            I = this;
            DontDestroyOnLoad(gameObject);
            LoadClips();
            for (int i = 0; i < PoolSize; i++) pool[i] = MakeSource(transform);
            try { AudioListener.volume = PlayerPrefs.GetFloat("sf_volume", 0.8f); } catch (System.Exception) { }

            // Listen from the hero, not from the camera high above.
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) l.enabled = false;
            ears = new GameObject("Ears").transform;
            ears.SetParent(transform, false);
            ears.gameObject.AddComponent<AudioListener>();

            wind = Loop2D("wind_loop", 0.22f);
            crickets = Loop2D("crickets_loop", 0f);
            nextHowl = Time.time + 30f;
        }

        static void LoadClips()
        {
            if (clips != null) return;
            clips = new Dictionary<string, List<AudioClip>>();
            foreach (var c in Resources.LoadAll<AudioClip>("Audio"))
            {
                int us = c.name.LastIndexOf('_');
                string key = us > 0 && int.TryParse(c.name.Substring(us + 1), out _) ? c.name.Substring(0, us) : c.name;
                if (!clips.TryGetValue(key, out var list)) clips[key] = list = new List<AudioClip>();
                list.Add(c);
            }
            if (clips.Count == 0) Debug.LogWarning("[Shadowfall] No sounds found in Resources/Audio (run task audio:build).");
        }

        static AudioSource MakeSource(Transform parent)
        {
            var go = new GameObject("Voice");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 4f;
            s.maxDistance = 40f;
            s.dopplerLevel = 0f;
            return s;
        }

        static AudioClip Pick(string key)
        {
            LoadClips();
            if (!clips.TryGetValue(key, out var list) || list.Count == 0) return null;
            return list[rng.Next(list.Count)];
        }

        /// <summary>Plays a sound at a world position (3D). Returns false if it was skipped.</summary>
        public static bool Play(string key, Vector3 pos, float volume = 1f, float pitchVariation = 0.08f, float maxDistance = 40f)
        {
            if (!Throttle(key)) return false;
            var clip = Pick(key);
            if (clip == null) return false;
            var s = Instance.NextSource();
            s.transform.position = pos;
            s.spatialBlend = 1f;
            s.maxDistance = maxDistance;
            s.clip = clip;
            s.volume = volume;
            s.pitch = 1f + R(-pitchVariation, pitchVariation);
            s.loop = false;
            s.Play();
            return true;
        }

        /// <summary>Plays a non-positional sound (UI, level-up, the hero's own death).</summary>
        public static void Play2D(string key, float volume = 1f, float pitch = 1f)
        {
            if (!Throttle(key)) return;
            var clip = Pick(key);
            if (clip == null) return;
            var s = Instance.NextSource();
            s.spatialBlend = 0f;
            s.clip = clip;
            s.volume = volume;
            s.pitch = pitch;
            s.loop = false;
            s.Play();
        }

        /// <summary>A positional loop (campfires, braziers, lakes) that keeps playing at that spot.</summary>
        public static AudioSource LoopAt(string key, Vector3 pos, float volume, float maxDistance = 14f)
        {
            var clip = Pick(key);
            if (clip == null) return null;
            _ = Instance; // make sure the listener exists
            var s = MakeSource(null);
            s.name = "Loop " + key;
            s.transform.position = pos;
            s.spatialBlend = 1f;
            s.minDistance = 2f;
            s.maxDistance = maxDistance;
            s.clip = clip;
            s.volume = volume;
            s.loop = true;
            s.time = R(0f, clip.length * 0.9f);
            s.Play();
            return s;
        }

        /// <summary>Extra wind on top of the ambience (storms and blizzards), set by the weather.</summary>
        public static float WindBoost;

        /// <summary>A looping 2D sound the caller controls (volume, stop), e.g. rain.</summary>
        public static AudioSource Loop(string key, float volume) => Instance.Loop2D(key, volume);

        AudioSource Loop2D(string key, float volume)
        {
            var clip = Pick(key);
            if (clip == null) return null;
            var s = MakeSource(transform);
            s.name = "Ambience " + key;
            s.spatialBlend = 0f;
            s.clip = clip;
            s.volume = volume;
            s.loop = true;
            s.Play();
            return s;
        }

        /// <summary>The same sound at most every 50 ms (e.g. many hits in one frame).</summary>
        static bool Throttle(string key)
        {
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(key, out var t) && now - t < 0.05f) return false;
            lastPlayed[key] = now;
            return true;
        }

        AudioSource NextSource()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var s = pool[(next + i) % PoolSize];
                if (!s.isPlaying) { next = (next + i + 1) % PoolSize; return s; }
            }
            var oldest = pool[next];
            next = (next + 1) % PoolSize;
            return oldest;
        }

        void LateUpdate()
        {
            var p = Player.I;
            var cam = GameManager.I != null ? GameManager.I.Cam : null;
            var pos = p != null ? p.transform.position : new Vector3(WorldGenerator.Center, 0f, WorldGenerator.Center);
            ears.position = pos + Vector3.up * 1.6f;
            if (cam != null) ears.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);

            // Ambience: wind everywhere, crickets outside the walls at night, the odd distant howl.
            float night = DayNight.Night;
            bool town = WorldGenerator.InTown(pos);
            bool inWorld = p != null;
            bool underground = Dungeon.Active;
            if (wind != null) wind.volume = Mathf.MoveTowards(wind.volume, underground ? 0.06f : inWorld ? Mathf.Lerp(0.2f, 0.14f, night) + WindBoost : 0.12f, Time.deltaTime * 0.2f);
            if (crickets != null) crickets.volume = Mathf.MoveTowards(crickets.volume, underground ? 0f : inWorld && !town ? night * 0.3f : night * 0.08f, Time.deltaTime * 0.1f);
            if (inWorld && !underground && night > 0.7f && !town && Time.time > nextHowl)
            {
                nextHowl = Time.time + R(40f, 90f);
                var dir = Quaternion.Euler(0, R(0f, 360f), 0) * Vector3.forward;
                Play("wolf_howl", pos + dir * 30f, 0.5f, 0.1f, 60f);
            }
        }
    }
}
