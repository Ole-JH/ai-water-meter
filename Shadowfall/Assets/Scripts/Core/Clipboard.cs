using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Copy and paste with the browser's clipboard (ShadowfallClipboard.jslib and the page's paste listener). In the
    /// editor and standalone builds, Unity's own copy buffer.
    /// </summary>
    public static class Clipboard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern string SF_TakePaste();
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void SF_Copy(string text);
#else
        static string SF_TakePaste() => null;
        static void SF_Copy(string text) => GUIUtility.systemCopyBuffer = text;
#endif

        /// <summary>What was pasted since the last call (the browser's paste), or null.</summary>
        public static string TakePaste()
        {
            try { var t = SF_TakePaste(); return string.IsNullOrEmpty(t) ? null : t; }
            catch (System.Exception) { return null; }
        }

        public static void Copy(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try { SF_Copy(text); } catch (System.Exception) { }
        }
    }
}
