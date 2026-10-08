using System.Collections;
using System.Text;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The pre-deploy browser check (tools/browser-check): opened with <c>?sfcheck=1</c>, the game plays a short scripted
    /// session by itself against a throwaway server: registers an account, creates a hero, walks to the well and the
    /// waystone. Each step is reported to the page (<c>window.sfCheck</c>, ShadowfallCheck.jslib); at the "shot:" steps
    /// the game waits until the browser has taken its screenshot. Errors and exceptions logged along the way are reported
    /// too: exceptions fail the check, other logged errors are listed as warnings. Without the URL parameter (every normal player) this component is never added.
    /// </summary>
    public class GameCheck : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void SF_CheckReport(string stage, string detail);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int SF_CheckAcked(string stage);
#else
        static void SF_CheckReport(string stage, string detail) => Debug.Log("[check] " + stage + " " + detail);
        static int SF_CheckAcked(string stage) => 1;
#endif

        public static bool Requested =>
            Application.absoluteURL != null && (Application.absoluteURL.Contains("sfcheck=1"));

        int errors, exceptions, missing;
        readonly StringBuilder exceptionText = new StringBuilder(), missingText = new StringBuilder(), errorText = new StringBuilder();

        public static void StartIfRequested(GameObject host)
        {
            if (Requested) host.AddComponent<GameCheck>();
        }

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;

        void OnLog(string message, string stack, LogType type)
        {
            // The check's Chromium has no AAC decoder (Unity's WebGL audio format), so every compressed sound fails to load
            // there ("Loading FSB failed"). Players' browsers decode it: not a problem with the build.
            if (message.Contains("Loading FSB failed") || message.Contains("FMOD")) return;
            if (type == LogType.Warning && message.Contains("Missing model"))
            {
                // Only a warning in the game (it falls back to primitives), but the check reports it.
                if (++missing <= 8) missingText.Append(message.Replace("[Shadowfall] ", "")).Append('\n');
            }
            else if (type == LogType.Exception || type == LogType.Assert)
            {
                if (++exceptions <= 3)
                {
                    var lines = (stack ?? "").Split('\n');
                    exceptionText.Append(message).Append(" @ ").Append(string.Join(" < ", lines, 0, Mathf.Min(4, lines.Length)).Trim()).Append('\n');
                }
            }
            else if (type == LogType.Error)
            {
                if (++errors <= 5) errorText.Append(message).Append('\n');
            }
            else return;
            SF_CheckReport("error", type + ": " + message + (type == LogType.Exception ? "\n" + stack : ""));
        }

        void Start() => StartCoroutine(Run());

        void Report(string stage, string detail = "") => SF_CheckReport(stage, detail ?? "");

        IEnumerator Fail(string why)
        {
            Report("fail", why);
            enabled = false;
            yield break;
        }

        /// <summary>Asks the browser for a screenshot and waits (up to 20 s) until it says it took it.</summary>
        IEnumerator Shot(string name)
        {
            yield return new WaitForSeconds(1.5f); // let effects, labels and the camera settle
            string stage = "shot:" + name;
            Report(stage);
            float until = Time.realtimeSinceStartup + 20f;
            while (SF_CheckAcked(stage) == 0 && Time.realtimeSinceStartup < until) yield return null;
        }

        static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < until) yield return null;
        }

        IEnumerator Run()
        {
            Report("boot", "build " + Application.version);
            yield return Until(() => NetClient.I != null, 30f);
            var net = NetClient.I;
            if (net == null) { yield return Fail("NetClient never started"); yield break; }

            string id = new System.Random().Next(100000, 999999).ToString();
            string user = "check" + id, pass = "pw-" + System.Guid.NewGuid().ToString("N").Substring(0, 16);
            Report("register", user);
            net.Register("", user, pass, "");
            yield return Until(() => net.State == NetClient.ConnState.Account && !net.Busy, 45f);
            if (net.State != NetClient.ConnState.Account) { yield return Fail("register: state " + net.State + ", status \"" + net.Status + "\""); yield break; }
            net.DismissRecoveryCode();

            Report("create", "Chk" + id);
            net.CreateCharacter("Chk" + id, "Knight");
            yield return Until(() => net.State == NetClient.ConnState.InWorld && Player.I != null, 90f);
            if (net.State != NetClient.ConnState.InWorld || Player.I == null)
            { yield return Fail("entering the world: state " + net.State + ", status \"" + net.Status + "\""); yield break; }

            yield return new WaitForSeconds(4f); // first snapshot, monsters, NPCs
            var hero = Player.I;
            Report("in-world", "at " + Fmt(hero.transform.position) + ", " + net.PlayersOnline + " online");
            yield return Shot("spawn");

            // Walk to the well in the middle of the square, then to Hollowmere's waystone; average the frame rate on the way.
            int frames = 0;
            float walkTime = 0f;
            var stops = new[]
            {
                ("well", new Vector3(WorldGenerator.Center + 0.5f, 0f, WorldGenerator.Center - 2.5f)),
                ("waystone", WorldGenerator.Towns[0].Waystone + new Vector3(0f, 0f, -2.5f)),
            };
            foreach (var (name, at) in stops)
            {
                Vector3 from = hero.transform.position;
                hero.MoveTo(at);
                float t0 = Time.realtimeSinceStartup, lastProgress = t0, best = Factory.FlatDistance(from, at);
                while (Time.realtimeSinceStartup - t0 < 40f)
                {
                    frames++;
                    float d = Factory.FlatDistance(hero.transform.position, at);
                    if (d < best - 0.2f) { best = d; lastProgress = Time.realtimeSinceStartup; }
                    if (d < 1.8f || Time.realtimeSinceStartup - lastProgress > 6f) break;
                    yield return null;
                }
                walkTime += Time.realtimeSinceStartup - t0;
                float left = Factory.FlatDistance(hero.transform.position, at);
                if (left > 3f)
                {
                    yield return Shot(name + "-stuck");
                    yield return Fail("walking to the " + name + ": stuck " + left.ToString("0.0") + " m short at " + Fmt(hero.transform.position));
                    yield break;
                }
                Report("walked", name + " in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s");
                yield return Shot(name);
            }

            // Ride a horse for a moment: shows the mount model (or the box it falls back to).
            hero.PreviewMount("horse");
            yield return Shot("mount");
            hero.Dismount();

            if (PlaytestTour.Requested)
            {
                // the longer playtest (windows, a fight, a dungeon) instead of the photos
                Report("tour");
                yield return gameObject.AddComponent<PlaytestTour>().Run(Shot, Report);
            }
            else
            {
                // Every model lined up, and the towns from above, for judging how things look (PhotoTour.cs).
                Report("photos");
                yield return gameObject.AddComponent<PhotoTour>().Run(Shot);
            }

            float fps = walkTime > 0f ? frames / walkTime : 0f;
            if (net.State != NetClient.ConnState.InWorld) { yield return Fail("lost the connection: " + net.Status); yield break; }
            // Exceptions fail the check; other logged errors don't, but the first ones go in the report (and Discord).
            string summary = "fps " + fps.ToString("0") + " while walking (software rendering), " + exceptions + " exception(s), " +
                missing + " missing model(s), " + errors + " other error(s)";
            string details = (exceptions > 0 ? "\nEXCEPTIONS: " + exceptionText : "") + (missing > 0 ? "\nMISSING MODELS: " + missingText : "") +
                             (errors > 0 ? "\nERRORS: " + errorText : "");
            if (exceptions > 0) { yield return Fail(summary + details); yield break; }
            summary += details;
            Report("done", summary);
        }

        static string Fmt(Vector3 p) => "(" + p.x.ToString("0.0") + ", " + p.z.ToString("0.0") + ")";
    }
}
