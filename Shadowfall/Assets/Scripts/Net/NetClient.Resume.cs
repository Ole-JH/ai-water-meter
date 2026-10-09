using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Back into the world by itself: when the server restarts (every deploy), the connection drops or the page reloads
    /// into a new build, the client signs back in with the one-time resume token the server gave it on entering the
    /// world, and plays the same hero, without the login screen. Only within a few minutes of having been in the world
    /// (a closed tab opened tomorrow logs in by hand), never after logging out or being logged in from elsewhere.
    /// </summary>
    public partial class NetClient
    {
        const string ResumeKey = "sf_resume";
        const float ResumeWindowMinutes = 10f, ResumeGiveUpSeconds = 150f;

        /// <summary>Signing back in on our own (the login screen says so instead of asking).</summary>
        public bool Resuming { get; private set; }
        float resumeNextTry = -1f, resumeGiveUpAt;
        float nextAlive;
        bool resumeTriedAtStart;

        struct ResumeInfo { public string user, name, code; public long alive; }

        static long Now => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        static ResumeInfo? LoadResume()
        {
            try
            {
                var parts = PlayerPrefs.GetString(ResumeKey, "").Split('\n');
                if (parts.Length != 4 || !long.TryParse(parts[3], out var alive)) return null;
                return new ResumeInfo { user = parts[0], name = parts[1], code = parts[2], alive = alive };
            }
            catch (System.Exception) { return null; }
        }

        static void StoreResume(ResumeInfo r)
        {
            try { PlayerPrefs.SetString(ResumeKey, r.user + "\n" + r.name + "\n" + r.code + "\n" + r.alive); PlayerPrefs.Save(); }
            catch (System.Exception) { }
        }

        /// <summary>Forgets the token (logging out, or it was refused).</summary>
        public static void ForgetResume()
        {
            try { PlayerPrefs.DeleteKey(ResumeKey); PlayerPrefs.Save(); } catch (System.Exception) { }
        }

        /// <summary>The server's "resume" message: the token for next time.</summary>
        void GotResume(NetMsg m)
        {
            if (string.IsNullOrEmpty(m.k)) return;
            StoreResume(new ResumeInfo { user = m.user, name = m.name, code = m.k, alive = Now });
        }

        /// <summary>After an unexpected disconnect from the world: start trying to get back in.</summary>
        void ResumeSoon()
        {
            if (GameCheck.Requested && !PlaytestTour.Requested) return; // the deploy check reports a drop as it is
            var r = LoadResume();
            if (!r.HasValue) return;
            Resuming = true;
            resumeNextTry = Time.unscaledTime + 2f;
            resumeGiveUpAt = Time.unscaledTime + ResumeGiveUpSeconds;
        }

        void ResumeTick()
        {
            // while in the world, the token stays fresh ("we were just playing")
            if (State == ConnState.InWorld)
            {
                Resuming = false;
                if (Time.unscaledTime >= nextAlive)
                {
                    nextAlive = Time.unscaledTime + 30f;
                    var r = LoadResume();
                    if (r.HasValue) { var v = r.Value; v.alive = Now; StoreResume(v); }
                }
                return;
            }
            // at start (a reload into a new build): straight back in
            if (!resumeTriedAtStart && State == ConnState.Offline)
            {
                resumeTriedAtStart = true;
                var r = LoadResume();
                if (r.HasValue && Now - r.Value.alive < ResumeWindowMinutes * 60f && !GameCheck.Requested)
                {
                    Resuming = true;
                    resumeNextTry = Time.unscaledTime;
                    resumeGiveUpAt = Time.unscaledTime + ResumeGiveUpSeconds;
                }
            }
            if (!Resuming || State != ConnState.Offline || Time.unscaledTime < resumeNextTry) return;
            if (Time.unscaledTime > resumeGiveUpAt) { Resuming = false; Status = "Couldn't get back in. Log in again."; return; }
            var info = LoadResume();
            if (!info.HasValue) { Resuming = false; return; }
            resumeNextTry = Time.unscaledTime + 5f; // the server may still be starting: try again in a bit
            Request("", new AuthMsg { t = "resume", user = info.Value.user, code = info.Value.code, name = info.Value.name });
            Status = "Rejoining the world as " + info.Value.name + "...";
        }

        /// <summary>The server refused the token (used, too old, password changed): back to the login screen.</summary>
        void ResumeRefused()
        {
            ForgetResume();
            Resuming = false;
            Status = "";
        }
    }
}
