using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Background music. Picks a context from where the hero is and what they are doing (the login screen, Hollowmere,
    /// the wilds, the graveyard, a dungeon, a fight with elites or a crowd, a boss), and crossfades to a track of that
    /// context. Exploration music plays a piece, then leaves a quiet gap before the next one; fight music loops until the
    /// fight is over. Tracks live in Assets/Resources/Music with a playlist, music.json (tools/audio/build_music.py),
    /// and are loaded one at a time so only the playing ones take memory.
    /// </summary>
    public class Music : MonoBehaviour
    {
        [System.Serializable] class Track { public string clip, intro; public bool loop; }
        [System.Serializable] class Context { public string name; public Track[] tracks; }
        [System.Serializable] class Playlist { public Context[] contexts; }

        const float Fade = 2.5f, FightFade = 1.2f;

        static Music I;
        readonly Dictionary<string, Track[]> playlist = new Dictionary<string, Track[]>();
        readonly Dictionary<string, int> nextIndex = new Dictionary<string, int>();
        AudioSource a, b;         // b fades out while a fades in
        AudioClip queuedLoop;     // after a boss intro: the loop that follows
        string context;
        readonly System.Random rng = new System.Random(); // not UnityEngine.Random: leave the game's random sequence alone
        float fadeT = 1f, fadeLen = Fade, gapUntil, nextCheck, fightUntil;
        bool fightIsBoss;

        /// <summary>Music volume 0..1 (on top of the master volume), saved between sessions.</summary>
        public static float Volume
        {
            get { if (volume < 0f) { try { volume = PlayerPrefs.GetFloat("sf_music", 0.55f); } catch (System.Exception) { volume = 0.55f; } } return volume; }
            set { volume = Mathf.Clamp01(value); try { PlayerPrefs.SetFloat("sf_music", volume); PlayerPrefs.Save(); } catch (System.Exception) { } }
        }
        static float volume = -1f;

        /// <summary>The context playing now (login, town, wilds, graveyard, dungeon, combat, boss), for the settings screen.</summary>
        public static string Now => I != null ? I.context : null;

        public static void Ensure()
        {
            if (I == null) new GameObject("Music").AddComponent<Music>();
        }

        void Awake()
        {
            I = this;
            DontDestroyOnLoad(gameObject);
            a = Source();
            b = Source();
            var json = Resources.Load<TextAsset>("Music/music");
            if (json == null) { Debug.LogWarning("[Shadowfall] No music (run task music:build)."); return; }
            var list = JsonUtility.FromJson<Playlist>(json.text);
            foreach (var c in list.contexts)
            {
                // Start each context at a random track, then go round in order.
                playlist[c.name] = c.tracks;
                nextIndex[c.name] = rng.Next(c.tracks.Length);
            }
        }

        AudioSource Source()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.priority = 0;
            s.volume = 0f;
            s.ignoreListenerPause = true;
            return s;
        }

        void Update()
        {
            if (playlist.Count == 0) return;
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 0.5f;
                string want = Want();
                if (want != context)
                {
                    bool fight = want == "combat" || want == "boss";
                    context = want;
                    Play(fight ? FightFade : Fade);
                }
            }

            // Crossfade.
            fadeT = Mathf.Min(1f, fadeT + Time.unscaledDeltaTime / fadeLen);
            float target = Volume * (context == "dungeon" || context == "login" ? 0.85f : 0.7f);
            a.volume = target * Mathf.SmoothStep(0f, 1f, fadeT);
            b.volume = target * (1f - Mathf.SmoothStep(0f, 1f, fadeT)) * (b.isPlaying ? 1f : 0f);
            if (fadeT >= 1f && b.isPlaying) Stop(b);

            // A boss intro hands over to its loop; a finished piece leaves a gap, then the next one.
            if (!a.isPlaying && a.clip != null && Application.isFocused)
            {
                if (queuedLoop != null)
                {
                    a.clip = queuedLoop;
                    a.loop = true;
                    queuedLoop = null;
                    a.Play();
                }
                else
                {
                    Stop(a);
                    gapUntil = Time.unscaledTime + 20f + (float)rng.NextDouble() * 30f;
                }
            }
            if (a.clip == null && Time.unscaledTime >= gapUntil && context != null) Play(Fade);
        }

        /// <summary>Where we are and what we're doing.</summary>
        string Want()
        {
            var p = Player.I;
            if (p == null) return "login";
            UpdateFight(p);
            if (Time.time < fightUntil) return fightIsBoss ? "boss" : "combat";
            if (Dungeon.Active) return "dungeon";
            if (WorldGenerator.InTown(p.transform.position)) return "town";
            string zone = WorldGenerator.ZoneAt(p.transform.position);
            if (zone.Contains("Graveyard") || zone.Contains("Crypt")) return "graveyard";
            return "wilds";
        }

        /// <summary>
        /// A fight worth music: a boss or an elite we're trading blows with, or a crowd (four or more monsters hit lately).
        /// Ordinary skirmishes keep the zone's music. The music holds for a while after the last blow.
        /// </summary>
        void UpdateFight(Player p)
        {
            if (p.IsDead) { fightUntil = 0f; return; }
            bool hurt = Time.time - p.LastDamagedTime < 6f;
            bool boss = false, elite = false;
            int engaged = 0;
            foreach (var e in Enemy.ById.Values)
            {
                if (e == null || e.IsDead) continue;
                if (Factory.FlatDistance(e.transform.position, p.transform.position) > (e.Def.Boss ? 32f : 22f)) continue;
                bool hit = Time.time - e.LastDamagedTime < 8f;
                if (!hit && !(hurt && (e.Def.Boss || e.Elite))) continue;
                engaged++;
                if (e.Def.Boss) boss = true;
                else if (e.Elite) elite = true;
            }
            if (boss) { fightIsBoss = true; fightUntil = Time.time + 8f; }
            else if (elite || engaged >= 4)
            {
                if (Time.time >= fightUntil) fightIsBoss = false; // a boss fight stays a boss fight until it ends
                fightUntil = Mathf.Max(fightUntil, Time.time + 6f);
            }
        }

        /// <summary>Crossfades to the next track of the current context.</summary>
        void Play(float fade)
        {
            if (context == null || !playlist.TryGetValue(context, out var tracks) || tracks.Length == 0) return;
            int i = nextIndex[context] % tracks.Length;
            nextIndex[context] = i + 1;
            var t = tracks[i];
            var clip = Resources.Load<AudioClip>("Music/" + (string.IsNullOrEmpty(t.intro) ? t.clip : t.intro));
            if (clip == null) return;
            queuedLoop = string.IsNullOrEmpty(t.intro) ? null : Resources.Load<AudioClip>("Music/" + t.clip);

            // The old track fades out on b; the new one fades in on a.
            if (b.isPlaying) Stop(b);
            (a, b) = (b, a);
            fadeT = 0f;
            fadeLen = fade;
            a.clip = clip;
            a.loop = t.loop && queuedLoop == null;
            a.volume = 0f;
            a.Play();
            gapUntil = 0f;
        }

        void Stop(AudioSource s)
        {
            s.Stop();
            var clip = s.clip;
            s.clip = null;
            // Free the decoded audio unless the other source still plays it.
            if (clip != null && clip != a.clip && clip != b.clip && clip != queuedLoop) clip.UnloadAudioData();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }
    }
}
