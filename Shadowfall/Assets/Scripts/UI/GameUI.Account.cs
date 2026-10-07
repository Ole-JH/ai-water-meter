using UnityEngine;

namespace Shadowfall
{
    /// <summary>Account settings (Esc → Account): password, email, recovery code; and the recovery code popup.</summary>
    public partial class GameUI
    {
        string accPass = "", accNew = "", accNew2 = "", accEmail;

        void DrawMenuAccount()
        {
            var net = NetClient.I;
            const float w = 560, h = 640;
            var r = new Rect((VW - w) / 2, (VH - h) / 2, w, h);
            if (UISkin.Window(r, "Account")) { menu = MenuPage.Main; return; }
            if (accEmail == null) accEmail = net.Email;
            float x = r.x + 40, fw = w - 80, y = r.y + 56;
            UISkin.Shadowed(new Rect(x, y, fw, 24), "Account  <color=#ffd070>" + net.AccountName + "</color>" +
                (string.IsNullOrEmpty(net.Email) ? "   <color=#a89880>(no email)</color>" : "   <color=#a89880>" + net.Email + "</color>"),
                UISkin.V(UISkin.Rich, fontSize: 17), Color.white);
            y += 36;
            accPass = LoginField(x, ref y, fw, "CURRENT PASSWORD  (NEEDED FOR EVERY CHANGE BELOW)", accPass, true, 128);
            UISkin.Divider(new Rect(x, y - 6, fw, 18));
            y += 14;

            float half = (fw - 10) / 2f;
            float yy = y;
            accNew = LoginField(x, ref yy, half, "NEW PASSWORD", accNew, true, 128);
            yy = y;
            accNew2 = LoginField(x + half + 10, ref yy, half, "NEW PASSWORD AGAIN", accNew2, true, 128);
            y = yy;
            GUI.enabled = !net.Busy && accNew.Length > 0 && accNew == accNew2;
            if (UISkin.Btn(new Rect(x, y, fw, 40), "Change Password", UISkin.Button)) { net.ChangePassword(accPass, accNew); accNew = accNew2 = ""; }
            GUI.enabled = true;
            y += 54;

            accEmail = LoginField(x, ref y, fw - 150, "EMAIL  (FOR RESET CODES" + (net.MailEnabled ? ")" : "; THIS SERVER CAN'T SEND EMAIL YET)"), accEmail, false, 254);
            GUI.enabled = !net.Busy;
            if (UISkin.Btn(new Rect(x + fw - 140, y - 52, 140, 40), "Save Email", UISkin.Button)) net.SetEmail(accPass, accEmail);
            if (UISkin.Btn(new Rect(x, y, fw, 40), "Get a New Recovery Code", UISkin.Button)) net.NewRecoveryCode(accPass);
            GUI.enabled = true;
            y += 50;
            UISkin.Shadowed(new Rect(x, y, fw, 40), "A new recovery code replaces the old one. Changing your password logs you out everywhere else.",
                UISkin.V(UISkin.SmallCenter, wordWrap: true), UISkin.Muted);
            y += 44;
            LoginStatus(x, y, fw);
            if (UISkin.Btn(new Rect(r.center.x - 100, r.yMax - 64, 200, 44), "Back", UISkin.Button)) { menu = MenuPage.Main; accPass = ""; accEmail = null; net.ClearMessages(); }
        }

        /// <summary>Shows a new recovery code until the player confirms they saved it.</summary>
        void DrawRecoveryCode()
        {
            var net = NetClient.I;
            if (net == null || string.IsNullOrEmpty(net.RecoveryCode)) return;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, VW, VH), UISkin.White);
            GUI.color = Color.white;
            const float w = 560, h = 380;
            var r = new Rect((VW - w) / 2, (VH - h) / 2, w, h);
            UISkin.Window(r, "Your Recovery Code", false);
            Block(new Rect(0, 0, VW, VH));
            float x = r.x + 36, fw = w - 72, y = r.y + 56;
            string why = net.RecoveryWhy == "used" ? "Your password is changed, and the recovery code you used is gone. Here is your new one."
                       : net.RecoveryWhy == "new" ? "Your account now has a recovery code."
                       : net.RecoveryWhy == "changed" ? "Your new recovery code (the old one no longer works)."
                       : "Welcome! Your account is ready.";
            GUI.Label(new Rect(x, y, fw, 44), why, UISkin.V(UISkin.Rich, fontSize: 16, wordWrap: true, alignment: TextAnchor.MiddleCenter));
            y += 54;
            var codeBox = new Rect(x + 20, y, fw - 40, 60);
            UISkin.Box(codeBox, UISkin.Inset);
            UISkin.Shadowed(codeBox, net.RecoveryCode, UISkin.V(UISkin.Heading, fontSize: 30, alignment: TextAnchor.MiddleCenter), UISkin.Gold, 2);
            y += 72;
            GUI.Label(new Rect(x, y, fw, 70),
                "Write it down or keep it in a password manager. If you forget your password, it lets you choose a new one " +
                "(<color=#c8a060>Forgot password?</color> on the login screen). It is shown only now.",
                UISkin.V(UISkin.Rich, fontSize: 14, wordWrap: true, alignment: TextAnchor.MiddleCenter));
            y += 80;
            float half = (fw - 10) / 2f;
            if (UISkin.Btn(new Rect(x, y, half, 46), "Copy", UISkin.Button))
            {
                GUIUtility.systemCopyBuffer = net.RecoveryCode;
                Sfx.Play2D("ui_confirm", 0.4f);
            }
            if (UISkin.Btn(new Rect(x + half + 10, y, half, 46), "I've Saved It", UISkin.Button)) net.DismissRecoveryCode();
        }
    }
}
