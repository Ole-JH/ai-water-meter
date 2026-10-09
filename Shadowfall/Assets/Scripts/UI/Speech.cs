using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Speech bubbles over players and NPCs (chat messages and NPC chatter). Drawn by <see cref="GameUI"/>.
    /// </summary>
    public static class Speech
    {
        public class Bubble
        {
            public Transform Who;
            public float Height, Start, Duration;
            public string Text;
        }

        public static readonly List<Bubble> Active = new List<Bubble>();

        /// <summary>Shows <paramref name="text"/> above <paramref name="who"/> (replacing what they were saying).</summary>
        public static void Say(Transform who, float height, string text)
        {
            if (who == null || string.IsNullOrEmpty(text)) return;
            Active.RemoveAll(b => b.Who == who || b.Who == null);
            Active.Add(new Bubble
            {
                Who = who,
                Height = height,
                Text = text.Length > 160 ? text.Substring(0, 157) + "..." : text,
                Start = Time.time,
                Duration = Mathf.Clamp(3.5f + text.Length * 0.06f, 4f, 10f),
            });
            if (Active.Count > 24) Active.RemoveAt(0);
        }

        public static bool IsTalking(Transform who)
        {
            foreach (var b in Active) if (b.Who == who && Time.time - b.Start < b.Duration) return true;
            return false;
        }
    }
}
