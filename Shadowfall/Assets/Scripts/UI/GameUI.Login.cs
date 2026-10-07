using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The login screens over the live hero preview: log in, create an account, forgot password, reset with a code,
    /// then the account's heroes (character select) and creating a new hero.
    /// </summary>
    public partial class GameUI
    {
        static readonly string[] heroRoles = { "Sword & Shield", "Two-handed Fury", "Arcane Fire", "Shadow & Blade" };

        enum LoginScreen { Login, Register, Forgot, Reset, Characters, Create }
        LoginScreen loginScreen = LoginScreen.Login;
        string loginPass2 = "", loginEmail = "", loginCode = "", newHeroName = "", deletePass = "";
        int selectedHero;
        bool confirmDelete;

        void ShowLoginScreen(LoginScreen screen)
        {
            if (screen == loginScreen) return;
            loginScreen = screen;
            loginPass = loginPass2 = deletePass = "";
            confirmDelete = false;
            NetClient.I.ClearMessages();
            GUI.FocusControl(null);
            Sfx.Play2D("ui_click", 0.3f);
        }

        /// <summary>Opens the reset form when the page was opened from a reset email (?reset=CODE&amp;user=NAME).</summary>
        void ReadResetLink()
        {
            string url = Application.absoluteURL;
            int q = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (q < 0) return;
            foreach (var part in url.Substring(q + 1).Split('&'))
            {
                int eq = part.IndexOf('=');
                if (eq < 0) continue;
                string key = part.Substring(0, eq), value = System.Uri.UnescapeDataString(part.Substring(eq + 1).Replace('+', ' '));
                if (key == "reset") { loginCode = value; loginScreen = LoginScreen.Reset; }
                else if (key == "user") loginName = value;
            }
        }

        void DrawLogin()
        {
            var net = NetClient.I;
            bool wide = VW >= 1100f;

            // The screen follows the connection: logged in = your heroes, logged out = the login form.
            if (net.SignedIn && loginScreen != LoginScreen.Characters && loginScreen != LoginScreen.Create)
                loginScreen = net.Characters.Length == 0 ? LoginScreen.Create : LoginScreen.Characters;
            else if (!net.SignedIn && (loginScreen == LoginScreen.Characters || loginScreen == LoginScreen.Create))
                loginScreen = LoginScreen.Login;
            if (loginScreen == LoginScreen.Characters && net.Characters.Length > 0)
            {
                selectedHero = Mathf.Clamp(selectedHero, 0, net.Characters.Length - 1);
                loginLook = net.Characters[selectedHero].look;
            }

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

            int sel = Mathf.Max(0, System.Array.IndexOf(heroNames, loginLook));

            // ---- title (and, on wide screens, the shown hero under it)
            float tx = wide ? VW * 0.06f : 0f, tw = wide ? VW * 0.44f : VW;
            var titleStyle = UISkin.V(UISkin.TitleHuge, fontSize: wide ? 78 : 54, alignment: wide ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            float ty = wide ? VH * 0.07f : 16f;
            UISkin.Shadowed(new Rect(tx, ty, tw, 96), "SHADOWFALL", titleStyle, UISkin.Gold, 2);
            var sub = UISkin.V(UISkin.Label, fontSize: 18, alignment: wide ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, fontStyle: FontStyle.Italic);
            float divW = wide ? 420f : 360f;
            UISkin.Divider(new Rect(wide ? tx : (VW - divW) / 2f, ty + (wide ? 92 : 64), divW, 20));
            UISkin.Shadowed(new Rect(tx + (wide ? 4 : 0), ty + (wide ? 112 : 82), tw, 28), "One world. Shared by all. Darkness stirs beneath Hollowmere.", sub, UISkin.Cream);

            if (wide)
            {
                var nameStyle = UISkin.V(UISkin.Heading, fontSize: 34);
                float hy = VH * 0.78f;
                bool hero = loginScreen == LoginScreen.Characters && net.Characters.Length > 0;
                var shown = hero ? net.Characters[selectedHero] : null;
                UISkin.Shadowed(new Rect(tx, hy, tw, 44), hero ? shown.name : heroNames[sel], nameStyle, UISkin.Gold, 2);
                UISkin.Shadowed(new Rect(tx, hy + 42, tw, 26), (hero ? "Level " + shown.lvl + " " + shown.look : heroRoles[sel]).ToUpper(),
                    UISkin.V(UISkin.Small, fontSize: 14, font: UISkin.Title ?? UISkin.Bold), UISkin.Muted);
                UISkin.Shadowed(new Rect(tx, hy + 66, tw, 28), heroBlurbs[sel], UISkin.V(UISkin.Label, fontSize: 18, fontStyle: FontStyle.Italic), UISkin.Cream);
                UISkin.Shadowed(new Rect(tx, hy + 96, tw, 24), ClassKits.Role(heroNames[sel]), UISkin.V(UISkin.Small, fontSize: 15), UISkin.Muted);
            }

            switch (loginScreen)
            {
                case LoginScreen.Login: DrawLoginForm(wide); break;
                case LoginScreen.Register: DrawRegisterForm(wide); break;
                case LoginScreen.Forgot: DrawForgotForm(wide); break;
                case LoginScreen.Reset: DrawResetForm(wide); break;
                case LoginScreen.Characters: DrawCharacterSelect(wide); break;
                case LoginScreen.Create: DrawCreateHero(wide, sel); break;
            }

            // Footer: build version + art status, handy when checking that a new build is really live.
            if (animStatus == null) animStatus = CharacterView.AnimationsAvailable ? "animations OK" : "<color=#ff8866>animations missing</color>";
            string art = ArtLibrary.Available ? "3D art loaded, " + animStatus : "<color=#ff8866>3D art missing (glTFast?)</color>";
            UISkin.Shadowed(new Rect(8, VH - 26, VW - 16, 22), "Build " + Application.version + "   " + art,
                UISkin.V(UISkin.Small, alignment: TextAnchor.LowerRight), UISkin.Muted);
            if (wide)
                UISkin.Shadowed(new Rect(VW * 0.06f, VH - 26, 600, 22), "Middle mouse rotates the camera in game  -  F1 for all controls",
                    UISkin.V(UISkin.Small, alignment: TextAnchor.LowerLeft), UISkin.Muted);
            DrawRecoveryCode();
            DrawTooltip();
        }

        // ------------------------------------------------------------------ building blocks

#if UNITY_WEBGL && !UNITY_EDITOR
        static bool ShowServerField => false;
#else
        static bool ShowServerField => true;
#endif
        string ServerUrl => ShowServerField ? serverUrl : "";

        /// <summary>The panel on the right (or centered on narrow screens). Returns the content area's x, width and first y.</summary>
        Rect LoginPanel(bool wide, string title, float contentHeight)
        {
            float w = 440f, h = contentHeight + 70f;
            var r = wide ? new Rect(VW - w - VW * 0.06f, Mathf.Max(70f, (VH - h) / 2f + 20f), w, h)
                         : new Rect((VW - w) / 2f, 130f, w, h);
            UISkin.Window(r, title, false);
            Block(r);
            return r;
        }

        static GUIStyle FieldLabel => UISkin.V(UISkin.Small, fontSize: 13, font: UISkin.Title ?? UISkin.Bold);

        string LoginField(float x, ref float y, float w, string label, string value, bool password, int max, string control = null)
        {
            UISkin.Shadowed(new Rect(x, y, w, 20), label, FieldLabel, UISkin.Muted);
            y += 22;
            if (control != null) GUI.SetNextControlName(control);
            value = password ? GUI.PasswordField(new Rect(x, y, w, 40), value ?? "", '•', max, UISkin.Field)
                             : GUI.TextField(new Rect(x, y, w, 40), value ?? "", max, UISkin.Field);
            y += 52;
            return value;
        }

        bool LoginLink(Rect r, string text)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            UISkin.Shadowed(r, text, UISkin.V(UISkin.RichSmall, alignment: TextAnchor.MiddleCenter), hover ? Color.white : UISkin.Gold);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) return true;
            return false;
        }

        static bool EnterPressed => Event.current.type == EventType.KeyDown &&
                                    (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

        bool BigButton(float x, ref float y, float w, string text, bool enabled = true)
        {
            var net = NetClient.I;
            GUI.enabled = enabled && !net.Busy;
            bool go = UISkin.Btn(new Rect(x, y, w, 54), net.Busy ? "Please wait..." : text, UISkin.V(UISkin.Button, fontSize: 20)) ||
                      (EnterPressed && GUI.enabled && !confirmDelete);
            GUI.enabled = true;
            y += 62;
            return go;
        }

        /// <summary>Errors in red, progress in gold, success in green.</summary>
        void LoginStatus(float x, float y, float w, string idle = null)
        {
            var net = NetClient.I;
            var style = UISkin.V(UISkin.SmallCenter, wordWrap: true);
            if (!string.IsNullOrEmpty(net.Notice)) UISkin.Shadowed(new Rect(x - 10, y, w + 20, 44), net.Notice, style, new Color(0.55f, 0.95f, 0.55f));
            else if (!string.IsNullOrEmpty(net.Status))
            {
                bool error = !net.Busy && net.Status != NetClient.LoggedOutMessage;
                UISkin.Shadowed(new Rect(x - 10, y, w + 20, 44), net.Status, style, error ? new Color(1f, 0.55f, 0.45f) : UISkin.Gold);
            }
            else if (idle != null) UISkin.Shadowed(new Rect(x - 10, y, w + 20, 44), idle, style, UISkin.Muted);
        }

        void Remember()
        {
            try
            {
                PlayerPrefs.SetString("sf_name", loginName);
                PlayerPrefs.SetString("sf_server", serverUrl);
            }
            catch (System.Exception) { }
        }

        // ------------------------------------------------------------------ screens

        void DrawLoginForm(bool wide)
        {
            var net = NetClient.I;
            var r = LoginPanel(wide, "Enter the World", (ShowServerField ? 74 : 0) + 380);
            float x = r.x + 34, w = r.width - 68, y = r.y + 50;
            loginName = LoginField(x, ref y, w, "ACCOUNT NAME", loginName, false, 16, "login_name");
            loginPass = LoginField(x, ref y, w, "PASSWORD", loginPass, true, 128);
            if (ShowServerField) serverUrl = LoginField(x, ref y, w, "SERVER", serverUrl, false, 200);
            y += 4;
            if (BigButton(x, ref y, w, "Log In"))
            {
                Remember();
                net.Login(ServerUrl, loginName, loginPass);
            }
            if (LoginLink(new Rect(x, y, w / 2 - 4, 26), "Create an account")) ShowLoginScreen(LoginScreen.Register);
            if (LoginLink(new Rect(x + w / 2 + 4, y, w / 2 - 4, 26), "Forgot password?")) ShowLoginScreen(LoginScreen.Forgot);
            y += 40;
            LoginStatus(x, y, w, "One account holds all your heroes.");
        }

        void DrawRegisterForm(bool wide)
        {
            var net = NetClient.I;
            var r = LoginPanel(wide, "Create an Account", (ShowServerField ? 74 : 0) + 556);
            float x = r.x + 34, w = r.width - 68, y = r.y + 50;
            loginName = LoginField(x, ref y, w, "ACCOUNT NAME  (3-16 LETTERS OR DIGITS)", loginName, false, 16);
            loginPass = LoginField(x, ref y, w, "PASSWORD  (AT LEAST 6 CHARACTERS)", loginPass, true, 128);
            loginPass2 = LoginField(x, ref y, w, "PASSWORD AGAIN", loginPass2, true, 128);
            loginEmail = LoginField(x, ref y, w, "EMAIL  (OPTIONAL, FOR PASSWORD RESETS)", loginEmail, false, 254);
            if (ShowServerField) serverUrl = LoginField(x, ref y, w, "SERVER", serverUrl, false, 200);
            y += 4;
            bool match = loginPass == loginPass2;
            if (BigButton(x, ref y, w, "Create Account", match))
            {
                Remember();
                net.Register(ServerUrl, loginName, loginPass, loginEmail);
            }
            if (LoginLink(new Rect(x, y, w, 26), "I already have an account")) ShowLoginScreen(LoginScreen.Login);
            y += 40;
            if (!match && loginPass2.Length > 0 && string.IsNullOrEmpty(net.Status))
                UISkin.Shadowed(new Rect(x - 10, y, w + 20, 44), "The passwords don't match.", UISkin.V(UISkin.SmallCenter, wordWrap: true), new Color(1f, 0.55f, 0.45f));
            else LoginStatus(x, y, w, "You'll get a recovery code: keep it somewhere safe.");
        }

        void DrawForgotForm(bool wide)
        {
            var net = NetClient.I;
            var r = LoginPanel(wide, "Forgot Your Password?", (ShowServerField ? 74 : 0) + 470);
            float x = r.x + 34, w = r.width - 68, y = r.y + 50;
            var text = UISkin.V(UISkin.Rich, fontSize: 15, wordWrap: true);
            GUI.Label(new Rect(x, y, w, 110),
                "<color=#c8a060>Have your recovery code or a reset code?</color> Use it below to choose a new password.\n" +
                "<color=#c8a060>Gave us an email address?</color> We can email you a reset code.\n" +
                "<color=#c8a060>Neither?</color> Ask an admin for a reset code.", text);
            y += 118;
            if (BigButton(x, ref y, w, "I have a code")) ShowLoginScreen(LoginScreen.Reset);
            UISkin.Divider(new Rect(x, y, w, 18));
            y += 24;
            loginName = LoginField(x, ref y, w, "ACCOUNT NAME OR EMAIL", loginName, false, 254);
            if (ShowServerField) serverUrl = LoginField(x, ref y, w, "SERVER", serverUrl, false, 200);
            if (BigButton(x, ref y, w, "Email Me a Code")) net.ForgotPassword(ServerUrl, loginName);
            if (LoginLink(new Rect(x, y, w, 26), "Back to log in")) ShowLoginScreen(LoginScreen.Login);
            y += 34;
            LoginStatus(x, y, w);
        }

        void DrawResetForm(bool wide)
        {
            var net = NetClient.I;
            var r = LoginPanel(wide, "Choose a New Password", (ShowServerField ? 74 : 0) + 502);
            float x = r.x + 34, w = r.width - 68, y = r.y + 50;
            loginName = LoginField(x, ref y, w, "ACCOUNT NAME", loginName, false, 16);
            loginCode = LoginField(x, ref y, w, "RECOVERY CODE OR RESET CODE", loginCode, false, 40);
            loginPass = LoginField(x, ref y, w, "NEW PASSWORD", loginPass, true, 128);
            loginPass2 = LoginField(x, ref y, w, "NEW PASSWORD AGAIN", loginPass2, true, 128);
            if (ShowServerField) serverUrl = LoginField(x, ref y, w, "SERVER", serverUrl, false, 200);
            bool match = loginPass == loginPass2;
            if (BigButton(x, ref y, w, "Set Password and Log In", match))
            {
                Remember();
                net.ResetPassword(ServerUrl, loginName, loginCode, loginPass);
            }
            if (LoginLink(new Rect(x, y, w, 26), "Back to log in")) ShowLoginScreen(LoginScreen.Login);
            y += 34;
            if (!match && loginPass2.Length > 0 && string.IsNullOrEmpty(net.Status))
                UISkin.Shadowed(new Rect(x - 10, y, w + 20, 44), "The passwords don't match.", UISkin.V(UISkin.SmallCenter, wordWrap: true), new Color(1f, 0.55f, 0.45f));
            else LoginStatus(x, y, w);
        }

        void DrawCharacterSelect(bool wide)
        {
            var net = NetClient.I;
            var heroes = net.Characters;
            const float row = 64f;
            float listH = Mathf.Max(1, heroes.Length) * (row + 6);
            var r = LoginPanel(wide, "Your Heroes", 56 + listH + 70 + (confirmDelete ? 120 : 44) + 90);
            float x = r.x + 30, w = r.width - 60, y = r.y + 50;
            UISkin.Shadowed(new Rect(x, y, w, 22), "ACCOUNT  " + net.AccountName.ToUpper(), UISkin.V(FieldLabel, alignment: TextAnchor.MiddleCenter), UISkin.Muted);
            y += 32;
            for (int i = 0; i < heroes.Length; i++)
            {
                var h = heroes[i];
                var hr = new Rect(x, y, w, row);
                bool selected = i == selectedHero, hover = hr.Contains(Event.current.mousePosition);
                UISkin.Box(hr, selected ? UISkin.InsetLight : UISkin.Inset);
                UISkin.IconInSlot(new Rect(hr.x + 8, hr.y + 6, row - 12, row - 12), UISkin.Icon(h.look.ToLower()), selected || hover ? Color.white : new Color(0.8f, 0.75f, 0.7f, 0.8f), 0);
                UISkin.Shadowed(new Rect(hr.x + row + 6, hr.y + 8, w - row - 12, 26), h.name, UISkin.V(UISkin.Heading, fontSize: 21), selected ? UISkin.Gold : UISkin.Cream);
                UISkin.Shadowed(new Rect(hr.x + row + 6, hr.y + 34, w - row - 12, 22), "Level " + h.lvl + " " + h.look, UISkin.Small, UISkin.Muted);
                if (GUI.Button(hr, GUIContent.none, GUIStyle.none))
                {
                    if (selected && Event.current.clickCount >= 2) net.Play(h.name);
                    else if (!selected) { selectedHero = i; confirmDelete = false; Sfx.Play2D("ui_click", 0.3f); }
                }
                y += row + 6;
            }
            y += 6;
            if (heroes.Length > 0 && BigButton(x, ref y, w, "Enter World")) net.Play(heroes[selectedHero].name);

            float half = (w - 8) / 2f;
            GUI.enabled = !net.Busy;
            if (UISkin.Btn(new Rect(x, y, half, 38), "New Hero", UISkin.Button)) { newHeroName = ""; ShowLoginScreen(LoginScreen.Create); }
            if (heroes.Length > 0 && UISkin.Btn(new Rect(x + half + 8, y, half, 38), "Delete Hero", UISkin.Button)) { confirmDelete = !confirmDelete; deletePass = ""; }
            GUI.enabled = true;
            y += 46;
            if (confirmDelete && heroes.Length > 0)
            {
                UISkin.Shadowed(new Rect(x, y, w, 22), "Delete " + heroes[selectedHero].name + " forever? Enter your password:",
                    UISkin.V(UISkin.Small, wordWrap: true), new Color(1f, 0.6f, 0.5f));
                y += 26;
                deletePass = GUI.PasswordField(new Rect(x, y, w - 130, 38), deletePass, '•', 128, UISkin.Field);
                if (UISkin.Btn(new Rect(x + w - 120, y, 120, 38), "Delete", UISkin.Button))
                {
                    net.DeleteCharacter(heroes[selectedHero].name, deletePass);
                    deletePass = "";
                    confirmDelete = false;
                    selectedHero = 0;
                }
                y += 50;
            }
            if (LoginLink(new Rect(x, y, w, 26), "Log out")) { net.LogOut(); loginPass = ""; }
            y += 34;
            LoginStatus(x, y, w);
        }

        void DrawCreateHero(bool wide, int sel)
        {
            var net = NetClient.I;
            var r = LoginPanel(wide, "Create a Hero", 410);
            float x = r.x + 34, w = r.width - 68, y = r.y + 50;
            newHeroName = LoginField(x, ref y, w, "HERO NAME  (3-16 LETTERS OR DIGITS)", newHeroName, false, 16, "hero_name");

            // Class picker
            UISkin.Shadowed(new Rect(x, y, w, 22), "CHOOSE A CLASS", UISkin.V(FieldLabel, alignment: TextAnchor.MiddleCenter), UISkin.Gold);
            y += 30;
            float gap = 10f, card = (w - gap * 3) / 4f;
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
                    try { PlayerPrefs.SetString("sf_look", loginLook); } catch (System.Exception) { }
                    Sfx.Play2D("ui_click", 0.35f);
                }
                if (hover) tooltip = "<b>" + heroNames[i] + "</b>  -  " + heroRoles[i] + "\n" + heroBlurbs[i] + "\n<color=#c8a060>" + ClassKits.Role(heroNames[i]) + "</color>";
            }
            y += card + 42;
            if (!wide)
            {
                UISkin.Shadowed(new Rect(x, y - 6, w, 22), heroBlurbs[sel], UISkin.V(UISkin.SmallCenter, fontStyle: FontStyle.Italic), UISkin.Cream);
                y += 22;
            }
            if (BigButton(x, ref y, w, "Create and Enter World")) net.CreateCharacter(newHeroName, heroNames[sel]);
            if (net.Characters.Length > 0 && LoginLink(new Rect(x, y, w, 26), "Back to your heroes")) ShowLoginScreen(LoginScreen.Characters);
            else if (net.Characters.Length == 0 && LoginLink(new Rect(x, y, w, 26), "Log out")) net.LogOut();
            y += 34;
            LoginStatus(x, y, w, "A hero's class can't be changed later.");
        }
    }
}
