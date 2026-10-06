using UnityEngine;

namespace Shadowfall
{
    /// <summary>The login screen: live hero preview on the left, title, and the "Enter the World" panel on the right.</summary>
    public partial class GameUI
    {
        static readonly string[] heroRoles = { "Sword & Shield", "Two-handed Fury", "Arcane Fire", "Shadow & Blade" };

        void DrawLogin()
        {
            var net = NetClient.I;
            bool wide = VW >= 1100f;
            LoginShowcase.Ensure(loginLook, wide ? -0.36f : 0f);

            // Darken the edges so text reads over the live scene.
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            if (UISkin.FadeDown != null)
            {
                GUI.DrawTexture(new Rect(0, 0, VW, VH * 0.38f), UISkin.FadeDown);
                GUI.DrawTextureWithTexCoords(new Rect(0, VH * 0.72f, VW, VH * 0.28f), UISkin.FadeDown, new Rect(0, 1, 1, -1));
            }
            var fadeRight = UISkin.Tex("Gothic/fade_right");
            if (fadeRight != null && wide) GUI.DrawTexture(new Rect(VW * 0.45f, 0, VW * 0.55f, VH), fadeRight);
            GUI.color = Color.white;

#if UNITY_WEBGL && !UNITY_EDITOR
            bool showServer = false;
#else
            bool showServer = true;
#endif
            int sel = Mathf.Max(0, System.Array.IndexOf(heroNames, loginLook));

            // ---- title (and, on wide screens, the selected hero's name under it)
            float tx = wide ? VW * 0.06f : 0f, tw = wide ? VW * 0.44f : VW;
            var titleStyle = new GUIStyle(UISkin.TitleHuge) { fontSize = wide ? 78 : 54, alignment = wide ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter };
            float ty = wide ? VH * 0.07f : 16f;
            UISkin.Shadowed(new Rect(tx, ty, tw, 96), "SHADOWFALL", titleStyle, UISkin.Gold, 2);
            var sub = new GUIStyle(UISkin.Label) { fontSize = 18, alignment = wide ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
            float divW = wide ? 420f : 360f;
            UISkin.Divider(new Rect(wide ? tx : (VW - divW) / 2f, ty + (wide ? 92 : 64), divW, 20));
            UISkin.Shadowed(new Rect(tx + (wide ? 4 : 0), ty + (wide ? 112 : 82), tw, 28), "One world. Shared by all. Darkness stirs beneath Hollowmere.", sub, UISkin.Cream);

            if (wide)
            {
                var nameStyle = new GUIStyle(UISkin.Heading) { fontSize = 34 };
                float hy = VH * 0.78f;
                UISkin.Shadowed(new Rect(tx, hy, tw, 44), heroNames[sel], nameStyle, UISkin.Gold, 2);
                UISkin.Shadowed(new Rect(tx, hy + 42, tw, 26), heroRoles[sel].ToUpper(), new GUIStyle(UISkin.Small) { fontSize = 14, font = UISkin.Title ?? UISkin.Bold }, UISkin.Muted);
                UISkin.Shadowed(new Rect(tx, hy + 66, tw, 28), heroBlurbs[sel], new GUIStyle(UISkin.Label) { fontSize = 18, fontStyle = FontStyle.Italic }, UISkin.Cream);
                UISkin.Shadowed(new Rect(tx, hy + 96, tw, 24), ClassKits.Role(heroNames[sel]), new GUIStyle(UISkin.Small) { fontSize = 15 }, UISkin.Muted);
            }

            // ---- the panel
            float w = 440f, h = showServer ? 590f : 516f;
            var r = wide ? new Rect(VW - w - VW * 0.06f, Mathf.Max(70f, (VH - h) / 2f + 20f), w, h)
                         : new Rect((VW - w) / 2f, 130f, w, h);
            UISkin.Window(r, "Enter the World", false);
            bool busy = net.State == NetClient.ConnState.Connecting || net.State == NetClient.ConnState.LoggingIn;
            var label = new GUIStyle(UISkin.Small) { fontSize = 13, font = UISkin.Title ?? UISkin.Bold };

            float x = r.x + 34, fw = w - 68, y = r.y + 50;
            UISkin.Shadowed(new Rect(x, y, fw, 20), "CHARACTER NAME", label, UISkin.Muted);
            y += 22;
            GUI.SetNextControlName("login_name");
            loginName = GUI.TextField(new Rect(x, y, fw, 40), loginName, 16, UISkin.Field);
            y += 52;
            UISkin.Shadowed(new Rect(x, y, fw, 20), "PASSWORD", label, UISkin.Muted);
            y += 22;
            loginPass = GUI.PasswordField(new Rect(x, y, fw, 40), loginPass, '•', 64, UISkin.Field);
            y += 52;
            if (showServer)
            {
                UISkin.Shadowed(new Rect(x, y, fw, 20), "SERVER", label, UISkin.Muted);
                y += 22;
                serverUrl = GUI.TextField(new Rect(x, y, fw, 40), serverUrl, 200, UISkin.Field);
                y += 52;
            }

            // Hero picker
            UISkin.Divider(new Rect(x, y, fw, 18));
            y += 22;
            UISkin.Shadowed(new Rect(x, y, fw, 22), "CHOOSE YOUR HERO", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter }, UISkin.Gold);
            y += 30;
            float gap = 10f, card = (fw - gap * 3) / 4f;
            for (int i = 0; i < heroNames.Length; i++)
            {
                var cr = new Rect(x + i * (card + gap), y, card, card + 30);
                bool selected = i == sel;
                bool hover = cr.Contains(Event.current.mousePosition);
                UISkin.Box(cr, selected ? UISkin.InsetLight : UISkin.Inset);
                var tint = selected ? Color.white : hover ? new Color(1f, 1f, 1f, 0.85f) : new Color(0.75f, 0.7f, 0.65f, 0.6f);
                UISkin.IconInSlot(new Rect(cr.x + 10, cr.y + 8, card - 20, card - 20), UISkin.Icon(heroNames[i].ToLower()), tint, 0);
                UISkin.Shadowed(new Rect(cr.x, cr.yMax - 30, cr.width, 24), heroNames[i], UISkin.SmallCenter, selected ? UISkin.Gold : hover ? UISkin.Cream : UISkin.Muted);
                if (GUI.Button(cr, GUIContent.none, GUIStyle.none) && !selected)
                {
                    loginLook = heroNames[i];
                    Sfx.Play2D("ui_click", 0.35f);
                }
                if (hover) tooltip = "<b>" + heroNames[i] + "</b>  -  " + heroRoles[i] + "\n" + heroBlurbs[i] + "\n<color=#c8a060>" + ClassKits.Role(heroNames[i]) + "</color>";
            }
            y += card + 42;
            if (!wide)
            {
                UISkin.Shadowed(new Rect(x, y - 6, fw, 22), heroBlurbs[sel], new GUIStyle(UISkin.SmallCenter) { fontStyle = FontStyle.Italic }, UISkin.Cream);
                y += 22;
            }

            // Enter World
            bool enter = Event.current.type == EventType.KeyDown &&
                         (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            GUI.enabled = !busy;
            var bigButton = new GUIStyle(UISkin.Button) { fontSize = 20 };
            if (UISkin.Btn(new Rect(x, y, fw, 54), busy ? "Connecting..." : "Enter World", bigButton) || (enter && !busy))
            {
                try
                {
                    PlayerPrefs.SetString("sf_name", loginName);
                    PlayerPrefs.SetString("sf_server", serverUrl);
                    PlayerPrefs.SetString("sf_look", loginLook);
                }
                catch (System.Exception) { }
                net.Login(showServer ? serverUrl : "", loginName, loginPass, loginLook);
            }
            GUI.enabled = true;
            y += 62;

            bool hasStatus = !string.IsNullOrEmpty(net.Status);
            string status = hasStatus ? net.Status : "New name? Your hero is created with this class when you first log in. Existing heroes keep their class.";
            var statusStyle = new GUIStyle(UISkin.SmallCenter) { wordWrap = true };
            bool error = hasStatus && !busy;
            UISkin.Shadowed(new Rect(x - 10, y, fw + 20, 40), status, statusStyle, error ? new Color(1f, 0.55f, 0.45f) : hasStatus ? UISkin.Gold : UISkin.Muted);

            // Footer: build version + art status, handy when checking that a new build is really live.
            if (animStatus == null) animStatus = CharacterView.AnimationsAvailable ? "animations OK" : "<color=#ff8866>animations missing</color>";
            string art = ArtLibrary.Available ? "3D art loaded, " + animStatus : "<color=#ff8866>3D art missing (glTFast?)</color>";
            UISkin.Shadowed(new Rect(8, VH - 26, VW - 16, 22), "Build " + Application.version + "   " + art,
                new GUIStyle(UISkin.Small) { alignment = TextAnchor.LowerRight }, UISkin.Muted);
            if (wide)
                UISkin.Shadowed(new Rect(VW * 0.06f, VH - 26, 600, 22), "Middle mouse rotates the camera in game  -  F1 for all controls",
                    new GUIStyle(UISkin.Small) { alignment = TextAnchor.LowerLeft }, UISkin.Muted);
            DrawTooltip();
        }
    }
}
