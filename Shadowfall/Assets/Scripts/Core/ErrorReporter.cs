using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Sends exceptions and logged errors from players' games to the server (index.html's window.sfReport, which posts
    /// them to /client-error), where they are logged and counted: Grafana's Shadowfall dashboard shows them by kind,
    /// and the ShadowfallClientExceptions alert fires when many players hit them. The page reports each message once.
    /// </summary>
    public static class ErrorReporter
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void SF_ReportError(string kind, string msg, string stack);
#else
        static void SF_ReportError(string kind, string msg, string stack) { }
#endif
        static bool installed;
        static int sent;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            Application.logMessageReceived += OnLog;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (++sent > 40) return; // the page caps it too; this saves crossing into JS for a flood
            string kind = type == LogType.Exception ? "exception" : "error";
            // Where the hero was goes with the stack, so the same error from different places still counts once.
            string where = Player.I != null ? "at " + Mathf.RoundToInt(Player.I.transform.position.x) + "," + Mathf.RoundToInt(Player.I.transform.position.z) + (Dungeon.Active ? ", in a dungeon" : "") + "\n" : "";
            try { SF_ReportError(kind, message, where + stack); } catch (System.Exception) { }
        }
    }
}
