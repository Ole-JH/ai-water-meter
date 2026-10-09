using UnityEngine;

namespace Shadowfall
{
    /// <summary>The "What's New" window: the changelog, with entries the hero hasn't read marked NEW.</summary>
    public partial class GameUI
    {
        bool showNews;
        int newsSeenAtOpen;
        float newsScroll;
        Player newsCheckedFor;
        float newsCheckAt;

        public void OpenNews(Player p)
        {
            if (showNews) return;
            showNews = true;
            newsSeenAtOpen = p.NewsSeen;
            newsScroll = 0f;
            Sfx.Play2D("book", 0.5f);
        }

        void CloseNews(Player p)
        {
            showNews = false;
            if (p.NewsSeen < Changelog.Latest)
            {
                p.NewsSeen = Changelog.Latest;
                NetClient.I?.SaveNow();
            }
        }

        /// <summary>Shortly after entering the world, pops the window open if anything is unread.</summary>
        void CheckNews(Player p)
        {
            if (newsCheckedFor == p) return;
            if (newsCheckAt <= 0f) { newsCheckAt = Time.time + 2.5f; return; }
            if (Time.time < newsCheckAt) return;
            newsCheckedFor = p;
            newsCheckAt = 0f;
            if (Changelog.UnreadCount(p.NewsSeen) > 0 && !p.IsDead) OpenNews(p);
        }

        /// <summary>A gold "NEW" chip above the menu buttons while there is unread news.</summary>
        void DrawNewsChip(Player p, Rect menuBar)
        {
            int unread = Changelog.UnreadCount(p.NewsSeen);
            if (unread == 0 || showNews) return;
            var r = new Rect(menuBar.xMax - 176, menuBar.y - 46, 176, 36);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            if (UISkin.Btn(r, GUIContent.none, UISkin.Button)) OpenNews(p);
            UISkin.Shadowed(new Rect(r.x, r.y + 5, r.width, 26), "<b>NEW</b>  What's New (" + unread + ")",
                UISkin.V(UISkin.Rich, alignment: TextAnchor.MiddleCenter), new Color(1f, 0.85f, 0.4f) * pulse + new Color(0, 0, 0, 1f - pulse));
            if (r.Contains(Event.current.mousePosition)) tooltip = "Read about the latest changes";
            Block(r);
        }

        void DrawNews(Player p)
        {
            float w = Mathf.Min(680, VW - 40), h = Mathf.Min(760, VH - 60);
            var r = new Rect((VW - w) / 2, (VH - h) / 2, w, h);
            if (UISkin.Window(r, "What's New", true, true)) { CloseNews(p); return; }
            Block(r);
            var view = new Rect(r.x + 28, r.y + 60, w - 56, h - 130);
            var title = UISkin.V(UISkin.InkRich, fontSize: 19, wordWrap: true);
            var body = UISkin.V(UISkin.InkRich, fontSize: 15, wordWrap: true);
            var date = UISkin.V(UISkin.InkRich, fontSize: 13);

            // Lay out once to measure, then draw inside a clipped, scrolled group.
            float contentH = 0f;
            foreach (var e in Changelog.Entries)
            {
                contentH += title.CalcHeight(new GUIContent(e.Title), view.width - 90) + 6;
                foreach (var it in e.Items) contentH += body.CalcHeight(new GUIContent("•  " + it), view.width - 20) + 4;
                contentH += 22;
            }
            float maxScroll = Mathf.Max(0f, contentH - view.height);
            var ev = Event.current;
            if (ev.type == EventType.ScrollWheel && r.Contains(ev.mousePosition)) { newsScroll += ev.delta.y * 28f; ev.Use(); }
            newsScroll = Mathf.Clamp(newsScroll, 0f, maxScroll);

            GUI.BeginGroup(view);
            float y = -newsScroll;
            foreach (var e in Changelog.Entries)
            {
                bool isNew = e.Id > newsSeenAtOpen;
                float th = title.CalcHeight(new GUIContent(e.Title), view.width - 90);
                if (isNew)
                {
                    var chip = new Rect(0, y + 2, 46, 22);
                    GUI.color = new Color(0.75f, 0.2f, 0.12f);
                    GUI.DrawTexture(chip, UISkin.White);
                    GUI.color = Color.white;
                    GUI.Label(chip, "<b><color=#fff2d0>NEW</color></b>", UISkin.V(UISkin.Rich, fontSize: 13, alignment: TextAnchor.MiddleCenter));
                }
                GUI.Label(new Rect(isNew ? 56 : 0, y, view.width - 150, th), "<b>" + e.Title + "</b>", title);
                GUI.Label(new Rect(view.width - 92, y + 3, 92, 20), "<color=#6a5238>" + e.Date + "</color>", date);
                y += th + 6;
                foreach (var it in e.Items)
                {
                    float ih = body.CalcHeight(new GUIContent("•  " + it), view.width - 20);
                    GUI.Label(new Rect(14, y, view.width - 20, ih), "•  " + it, body);
                    y += ih + 4;
                }
                y += 10;
                GUI.color = new Color(0.35f, 0.25f, 0.15f, 0.35f);
                GUI.DrawTexture(new Rect(0, y, view.width, 1), UISkin.White);
                GUI.color = Color.white;
                y += 12;
            }
            GUI.EndGroup();

            if (maxScroll > 0f)
            {
                // a thin scroll indicator on the right edge
                var track = new Rect(view.xMax + 8, view.y, 4, view.height);
                GUI.color = new Color(0.3f, 0.22f, 0.12f, 0.3f);
                GUI.DrawTexture(track, UISkin.White);
                float thumbH = Mathf.Max(30f, view.height * view.height / contentH);
                GUI.color = new Color(0.45f, 0.32f, 0.15f, 0.85f);
                GUI.DrawTexture(new Rect(track.x, track.y + (view.height - thumbH) * (newsScroll / maxScroll), 4, thumbH), UISkin.White);
                GUI.color = Color.white;
            }
            if (UISkin.Btn(new Rect(r.center.x - 90, r.yMax - 62, 180, 44), "Got it", UISkin.Button)) CloseNews(p);
        }
    }
}
