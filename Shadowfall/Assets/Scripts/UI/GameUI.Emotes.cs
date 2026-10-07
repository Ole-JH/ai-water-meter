using UnityEngine;

namespace Shadowfall
{
    /// <summary>The emote menu (G): a grid of emote buttons. Emotes can also be typed in chat (/wave, /dance...).</summary>
    public partial class GameUI
    {
        bool showEmotes;

        void ListEmotes()
        {
            var names = new System.Text.StringBuilder();
            foreach (var e in EmoteDef.All) names.Append(names.Length > 0 ? ", /" : "/").Append(e.Id);
            Log("Emotes: " + names + "  (or press G)", Player.EmoteColor);
        }

        void DrawEmotes(Player p)
        {
            const int cols = 3;
            const float bw = 118, bh = 38, gap = 6;
            int rows = (EmoteDef.All.Length + cols - 1) / cols;
            float w = cols * (bw + gap) - gap + 36, h = rows * (bh + gap) - gap + 92;
            var r = new Rect(VW - w - 20, VH - h - 130, w, h);
            if (UISkin.Window(r, "Emotes")) { showEmotes = false; return; }
            Block(r);
            var style = UISkin.V(UISkin.Button, fontSize: 15);
            for (int i = 0; i < EmoteDef.All.Length; i++)
            {
                var e = EmoteDef.All[i];
                var b = new Rect(r.x + 18 + (i % cols) * (bw + gap), r.y + 58 + (i / cols) * (bh + gap), bw, bh);
                if (UISkin.Btn(b, e.Label, style)) p.DoEmote(e);
                if (b.Contains(Event.current.mousePosition))
                    tooltip = "/" + e.Id + (e.Loop || e.Then != null ? "\nLasts until you move" : "");
            }
            UISkin.Shadowed(new Rect(r.x, r.yMax - 30, r.width, 20), "Type /wave, /dance... in chat", UISkin.SmallCenter, UISkin.Muted);
        }
    }
}
