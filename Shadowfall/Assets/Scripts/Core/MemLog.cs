using UnityEngine;
using UnityEngine.Profiling;

namespace Shadowfall
{
    /// <summary>
    /// With <c>?memlog=1</c>: how much memory each step of starting up takes, in the browser console. Phones (iOS Safari
    /// above all) can't grow the game's memory as far as a desktop browser, so this is how to find what to slim down.
    /// </summary>
    public static class MemLog
    {
        static int on = -1;
        static long last;

        public static bool On
        {
            get
            {
                if (on < 0) on = Application.absoluteURL.Contains("memlog=1") ? 1 : 0;
                return on == 1;
            }
        }

        public static void Note(string step)
        {
            if (!On) return;
            long now = Profiler.GetTotalAllocatedMemoryLong();
            Debug.Log("[mem] " + step + ": +" + Mb(now - last) + " -> allocated " + Mb(now) + ", reserved " + Mb(Profiler.GetTotalReservedMemoryLong()) +
                      ", managed " + Mb(Profiler.GetMonoUsedSizeLong()) + " of " + Mb(Profiler.GetMonoHeapSizeLong()) + ", gfx " + Mb(Profiler.GetAllocatedMemoryForGraphicsDriver()));
            last = now;
        }

        static string Mb(long b) => (b / 1048576f).ToString("0") + " MB";
    }
}
