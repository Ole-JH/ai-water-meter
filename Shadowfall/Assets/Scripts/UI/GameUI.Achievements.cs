using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>The achievements window (Y), the "Achievement earned" toasts and the title picker.</summary>
    public partial class GameUI
    {
        bool showAchievements;
        AchievementCategory achCategory = AchievementCategory.Combat;
        Vector2 achScroll;

        static readonly List<(AchievementDef def, float at)> toasts = new List<(AchievementDef, float)>();
        const float ToastTime = 5.5f;
        /// <summary>Achievements worth this much are rare (announced to everyone, as the server's RARE_ACH_POINTS).</summary>
        public const int RarePoints = 50;

        /// <summary>Shows the gold "Achievement earned" plate under the banner (several queue up one after another).</summary>
        public static void AchievementToast(AchievementDef a)
        {
            float start = Time.unscaledTime;
            if (toasts.Count > 0) start = Mathf.Max(start, toasts[toasts.Count - 1].at + 2.2f);
            toasts.Add((a, start));
        }

        void DrawAchievementToasts()
        {
            float now = Time.unscaledTime;
            toasts.RemoveAll(t => now > t.at + ToastTime);
            foreach (var (a, at) in toasts)
            {
                float t = now - at;
                if (t < 0f) continue;
                float alpha = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((ToastTime - t) / 0.6f);
                float slide = (1f - Mathf.Clamp01(t / 0.35f)) * 30f;
                bool rare = a.Points >= RarePoints;
                var accent = rare ? new Color(1f, 0.55f, 0.95f) : new Color(1f, 0.8f, 0.3f);
                // it lands with a little bounce
                float pop = t < 0.45f ? 1f + Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI) * 0.06f : 1f;
                var r = new Rect((VW - 420 * pop) / 2, Lane(92f) - slide, 420 * pop, 92 * pop);
                if (Event.current.type == EventType.Repaint)
                {
                    // a glow behind it, breathing (stronger for a rare one)
                    float breathe = 0.5f + 0.5f * Mathf.Sin(t * 3f);
                    GUI.color = new Color(accent.r, accent.g, accent.b, alpha * (rare ? 0.3f + 0.15f * breathe : 0.16f + 0.08f * breathe));
                    GUI.DrawTexture(new Rect(r.x - 40, r.y - 30, r.width + 80, r.height + 60), UISkin.Circle);
                }
                GUI.color = new Color(1f, 1f, 1f, alpha);
                UISkin.Box(r, UISkin.Panel);
                Outline(r, accent, alpha * (0.5f + 0.5f * Mathf.Sin(t * 4f) * 0.5f), 1f);
                if (t > 0.3f && t < 1.3f && Event.current.type == EventType.Repaint)
                {
                    // a shine sweeping across the plate
                    float k = (t - 0.3f) / 1f, sx = r.x - 60f + (r.width + 120f) * k;
                    GUI.BeginGroup(r);
                    GUI.color = new Color(1f, 1f, 1f, alpha * 0.18f);
                    GUI.DrawTexture(new Rect(sx - r.x, 0, 26, r.height), UISkin.White);
                    GUI.DrawTexture(new Rect(sx - r.x + 32, 0, 8, r.height), UISkin.White);
                    GUI.EndGroup();
                    GUI.color = new Color(1f, 1f, 1f, alpha);
                }
                var icon = new Rect(r.x + 14, r.y + 14, 64, 64);
                UISkin.Box(icon, UISkin.Slot);
                UISkin.IconInSlot(icon, UISkin.Icon(a.Icon), new Color(1f, 1f, 1f, alpha), 4);
                // a sweep of light across the icon as it arrives
                if (t < 1.2f)
                {
                    GUI.color = new Color(1f, 0.9f, 0.5f, alpha * (1f - t / 1.2f) * 0.6f);
                    GUI.DrawTexture(new Rect(icon.x - 6, icon.y - 6, icon.width + 12, icon.height + 12), UISkin.Circle);
                }
                GUI.color = new Color(1f, 1f, 1f, alpha);
                UISkin.Shadowed(new Rect(r.x + 92, r.y + 10, 310, 20), rare ? "RARE ACHIEVEMENT" : "ACHIEVEMENT EARNED", UISkin.V(UISkin.Small, fontSize: 13), new Color(accent.r, accent.g, accent.b, alpha));
                UISkin.Shadowed(new Rect(r.x + 92, r.y + 30, 310, 28), a.Name, UISkin.V(UISkin.Heading, fontSize: 21), new Color(1f, 0.95f, 0.8f, alpha));
                int shown = Mathf.RoundToInt(a.Points * Mathf.Clamp01((t - 0.35f) / 0.8f)); // the points count up
                UISkin.Shadowed(new Rect(r.x + 92, r.y + 60, 310, 20), shown + " points" + (a.Title != null ? "   -   new title: " + a.Title : ""),
                    UISkin.V(UISkin.Small, fontSize: 13), new Color(0.85f, 0.78f, 0.65f, alpha));
                GUI.color = Color.white;
                break; // one at a time
            }
        }

        void DrawAchievements(Player p)
        {
            var log = p.Achievements;
            var r = new Rect((VW - 760) / 2, 110, 760, 600);
            if (UISkin.Window(r, "Achievements")) { showAchievements = false; return; }
            Block(r);

            // Summary and title.
            int earned = log.Earned.Count, all = AchievementDatabase.All.Length;
            UISkin.Shadowed(new Rect(r.x + 28, r.y + 56, 340, 24),
                earned + " / " + all + " earned   -   " + log.Points + " / " + AchievementDatabase.TotalPoints + " points", UISkin.Label, UISkin.Gold);
            DrawTitlePicker(p, new Rect(r.x + 380, r.y + 52, r.width - 408, 32));

            // Categories down the left.
            float y = r.y + 96;
            foreach (AchievementCategory c in System.Enum.GetValues(typeof(AchievementCategory)))
            {
                int have = 0, total = 0;
                foreach (var a in AchievementDatabase.All) if (a.Category == c) { total++; if (log.Has(a.Id)) have++; }
                var b = new Rect(r.x + 22, y, 170, 40);
                bool on = achCategory == c;
                if (UISkin.Btn(b, GUIContent.none, on ? UISkin.ButtonLight : UISkin.Button)) { achCategory = c; achScroll = Vector2.zero; }
                UISkin.Shadowed(new Rect(b.x + 12, b.y + 9, 110, 22), c.ToString(), UISkin.Small, on ? UISkin.Gold : UISkin.Cream);
                UISkin.Shadowed(new Rect(b.x, b.y + 9, b.width - 12, 22), have + "/" + total, UISkin.SmallRight, have == total ? new Color(0.5f, 1f, 0.5f) : UISkin.Muted);
                y += 46;
            }

            // The category's achievements, earned first.
            var list = new List<AchievementDef>();
            foreach (var a in AchievementDatabase.All) if (a.Category == achCategory) list.Add(a);
            list.Sort((x, z) => log.Has(z.Id).CompareTo(log.Has(x.Id)));
            var view = new Rect(r.x + 206, r.y + 96, r.width - 228, r.height - 120);
            const float rowH = 76;
            achScroll = GUI.BeginScrollView(view, achScroll, new Rect(0, 0, view.width - 20, list.Count * rowH), false, false);
            for (int i = 0; i < list.Count; i++) DrawAchievementRow(log, list[i], new Rect(0, i * rowH, view.width - 20, rowH - 6));
            GUI.EndScrollView();
        }

        void DrawAchievementRow(AchievementLog log, AchievementDef a, Rect row)
        {
            bool has = log.Has(a.Id);
            UISkin.Box(row, has ? UISkin.InsetLight : UISkin.Inset);
            var icon = new Rect(row.x + 8, row.y + 7, 56, 56);
            UISkin.Box(icon, UISkin.Slot);
            UISkin.IconInSlot(icon, UISkin.Icon(a.Icon), has ? Color.white : new Color(0.35f, 0.33f, 0.32f), 4);
            UISkin.Shadowed(new Rect(row.x + 74, row.y + 6, row.width - 150, 24), a.Name, UISkin.Label, has ? UISkin.Gold : UISkin.Cream);
            GUI.Label(new Rect(row.x + 74, row.y + 30, row.width - 150, 22), a.Description, UISkin.V(UISkin.RichSmall, wordWrap: false));
            // points badge
            var pts = new Rect(row.xMax - 62, row.y + 10, 50, 30);
            UISkin.Box(pts, UISkin.Slot);
            UISkin.Shadowed(pts, a.Points.ToString(), UISkin.LabelCenter, has ? UISkin.Gold : UISkin.Muted);
            if (has)
            {
                string when = log.Earned[a.Id];
                UISkin.Shadowed(new Rect(row.x + 74, row.y + 50, row.width - 150, 18),
                    (string.IsNullOrEmpty(when) ? "Earned" : "Earned " + when) + (a.Title != null ? "   -   title: " + a.Title : ""),
                    UISkin.V(UISkin.Small, fontSize: 12), new Color(0.55f, 0.95f, 0.55f));
            }
            else if (a.Goal > 1)
            {
                int v = Mathf.Min(log.Get(a.Stat), a.Goal);
                UISkin.Bar(new Rect(row.x + 74, row.y + 53, Mathf.Min(260, row.width - 160), 12), (float)v / a.Goal, "Blue",
                    null, new Color(0.3f, 0.45f, 0.9f));
                UISkin.Shadowed(new Rect(row.x + 74 + Mathf.Min(260, row.width - 160) + 10, row.y + 49, 160, 18),
                    v.ToString("N0") + " / " + a.Goal.ToString("N0") + (a.Stat == "explored" ? " %" : ""), UISkin.V(UISkin.Small, fontSize: 12), UISkin.Muted);
            }
            else if (a.Title != null)
                UISkin.Shadowed(new Rect(row.x + 74, row.y + 50, row.width - 150, 18), "Reward: the title " + a.Title, UISkin.V(UISkin.Small, fontSize: 12), UISkin.Muted);
        }

        /// <summary>"Title: none / Lichbane / ..." with arrows to step through the titles you have earned.</summary>
        void DrawTitlePicker(Player p, Rect r)
        {
            var log = p.Achievements;
            var titles = new List<string> { null };
            foreach (var a in AchievementDatabase.All) if (a.Title != null && log.Has(a.Id)) titles.Add(a.Id);
            int cur = Mathf.Max(0, titles.IndexOf(log.Title != null ? log.TitleFrom : null));
            UISkin.Shadowed(new Rect(r.x, r.y + 5, 50, 22), "Title", UISkin.Small, UISkin.Muted);
            var prev = new Rect(r.x + 50, r.y, 32, 32);
            var next = new Rect(r.xMax - 32, r.y, 32, 32);
            string shown = cur == 0 ? (titles.Count > 1 ? "None" : "None yet") : AchievementDatabase.Get(titles[cur]).Title;
            UISkin.Shadowed(new Rect(prev.xMax, r.y + 5, next.x - prev.xMax, 22), shown, UISkin.SmallCenter, cur == 0 ? UISkin.Muted : new Color(0.85f, 0.75f, 1f));
            GUI.enabled = titles.Count > 1;
            int pick = cur;
            if (UISkin.Btn(prev, "<", UISkin.SquareButton)) pick = (cur + titles.Count - 1) % titles.Count;
            if (UISkin.Btn(next, ">", UISkin.SquareButton)) pick = (cur + 1) % titles.Count;
            GUI.enabled = true;
            if (pick != cur)
            {
                log.TitleFrom = titles[pick];
                Sfx.Play2D("ui_click", 0.4f);
                NetClient.I?.SaveSoon();
            }
            if (r.Contains(Event.current.mousePosition))
                tooltip = "Wear a title you have earned under your name. Everyone around you sees it.";
        }
    }
}
