using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Fantasy UI skin for the IMGUI interface: Kenney RPG panels/buttons/bars (CC0), game-icons.net icons
    /// (CC BY 3.0), Cinzel + Alegreya Sans fonts (SIL OFL). Everything degrades to flat colors if missing.
    /// </summary>
    public static class UISkin
    {
        public static readonly Color Gold = new Color(1f, 0.82f, 0.42f);
        public static readonly Color Cream = new Color(0.96f, 0.9f, 0.78f);
        public static readonly Color Ink = new Color(0.24f, 0.15f, 0.08f);
        public static readonly Color Muted = new Color(0.72f, 0.66f, 0.56f);

        public static Font Title, Body, Bold;
        public static GUIStyle Panel, Parchment, Inset, InsetLight, Button, ButtonLight, SquareButton, Field, Tooltip;
        public static GUIStyle Label, LabelCenter, Small, SmallCenter, SmallRight, Heading, HeadingCenter, TitleHuge, Banner, FloatText,
            Rich, RichSmall, Ink14, InkRich;
        public static Texture2D White, Circle, BarBackL, BarBackM, BarBackR, Close;
        static readonly Dictionary<string, Texture2D[]> bars = new Dictionary<string, Texture2D[]>();
        static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        static bool ready;

        static Texture2D Tex(string path) => Resources.Load<Texture2D>("UI/" + path);

        /// <summary>Call from OnGUI (GUI.skin is only valid there).</summary>
        public static void Init()
        {
            if (ready) return;
            ready = true;
            White = Texture2D.whiteTexture;
            Circle = MakeCircle(128);
            Title = Resources.Load<Font>("UI/Fonts/Cinzel");
            Body = Resources.Load<Font>("UI/Fonts/AlegreyaSans-Regular");
            Bold = Resources.Load<Font>("UI/Fonts/AlegreyaSans-Bold") ?? Body;

            Panel = Box("Skin/panel_brown", 14, new Color(0.13f, 0.09f, 0.06f, 0.95f));
            Parchment = Box("Skin/panel_beige", 14, new Color(0.86f, 0.78f, 0.62f, 0.97f));
            Inset = Box("Skin/panelInset_brown", 12, new Color(0.08f, 0.06f, 0.04f, 0.9f));
            InsetLight = Box("Skin/panelInset_beige", 12, new Color(0.78f, 0.7f, 0.55f, 0.95f));

            Button = Btn("Skin/buttonLong_brown", "Skin/buttonLong_brown_pressed", Cream, 16);
            ButtonLight = Btn("Skin/buttonLong_beige", "Skin/buttonLong_beige_pressed", Ink, 16);
            SquareButton = Btn("Skin/buttonSquare_brown", "Skin/buttonSquare_brown_pressed", Cream, 18);
            SquareButton.padding = new RectOffset(4, 4, 4, 6);

            Field = new GUIStyle(GUI.skin.textField)
            {
                font = Body, fontSize = 17, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 4, 4), border = new RectOffset(12, 12, 12, 12)
            };
            var fieldTex = Tex("Skin/panelInset_beige");
            if (fieldTex != null) Field.normal.background = Field.focused.background = Field.hover.background = Field.active.background = fieldTex;
            Field.normal.textColor = Field.focused.textColor = Field.hover.textColor = Ink;

            Label = Text(Body, 16, Cream);
            LabelCenter = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter };
            Small = Text(Body, 14, Cream);
            Small.wordWrap = true;
            SmallCenter = new GUIStyle(Small) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            SmallRight = new GUIStyle(Small) { alignment = TextAnchor.LowerRight, wordWrap = false, font = Bold };
            Heading = Text(Title ?? Bold, 19, Gold);
            Heading.fontStyle = Title != null ? FontStyle.Normal : FontStyle.Bold;
            HeadingCenter = new GUIStyle(Heading) { alignment = TextAnchor.MiddleCenter };
            TitleHuge = new GUIStyle(HeadingCenter) { fontSize = 72 };
            Banner = new GUIStyle(HeadingCenter) { fontSize = 30 };
            FloatText = Text(Bold, 18, Color.white);
            FloatText.alignment = TextAnchor.MiddleCenter;
            Rich = Text(Body, 15, Cream);
            Rich.wordWrap = true;
            RichSmall = new GUIStyle(Rich) { fontSize = 13 };
            Ink14 = Text(Body, 15, Ink);
            Ink14.wordWrap = true;
            InkRich = new GUIStyle(Ink14) { fontSize = 16 };

            Tooltip = new GUIStyle(Panel) { padding = new RectOffset(14, 14, 12, 12) };

            BarBackL = Tex("Skin/barBack_horizontalLeft");
            BarBackM = Tex("Skin/barBack_horizontalMid");
            BarBackR = Tex("Skin/barBack_horizontalRight");
            Close = Tex("Skin/iconCross_brown");
            foreach (var c in new[] { "Red", "Green", "Blue", "Yellow" })
                bars[c] = new[] { Tex($"Skin/bar{c}_horizontalLeft"), Tex($"Skin/bar{c}_horizontalMid"), Tex($"Skin/bar{c}_horizontalRight") };
        }

        static GUIStyle Box(string tex, int border, Color fallback)
        {
            var t = Tex(tex);
            var s = new GUIStyle { border = new RectOffset(border, border, border, border), padding = new RectOffset(border, border, border, border) };
            s.normal.background = t != null ? t : Solid(fallback);
            return s;
        }

        static GUIStyle Btn(string tex, string pressed, Color text, int size)
        {
            var s = new GUIStyle(GUI.skin.button)
            {
                font = Bold, fontSize = size, alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(12, 12, 12, 14), padding = new RectOffset(8, 8, 4, 8)
            };
            var t = Tex(tex);
            var p = Tex(pressed);
            if (t != null)
            {
                s.normal.background = s.hover.background = s.focused.background = t;
                s.active.background = p != null ? p : t;
            }
            s.normal.textColor = s.focused.textColor = text;
            s.hover.textColor = Color.Lerp(text, Color.white, 0.4f);
            s.active.textColor = text;
            return s;
        }

        static GUIStyle Text(Font f, int size, Color c)
        {
            var s = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, richText = true, wordWrap = false };
            s.normal.textColor = c;
            return s;
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        static Texture2D MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d));
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ icons

        public static Texture2D Icon(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (!icons.TryGetValue(key, out var t))
            {
                t = Tex("Icons/" + key);
                icons[key] = t;
            }
            return t;
        }

        public static string IconKey(Item it)
        {
            if (it == null) return null;
            if (it.Kind == ItemKind.Equipment)
            {
                switch (it.Slot)
                {
                    case EquipSlot.Weapon:
                        string b = (it.BaseType ?? it.Name).ToLower();
                        if (b.Contains("axe")) return "axe";
                        if (b.Contains("mace") || b.Contains("hammer") || b.Contains("star") || b.Contains("club")) return "mace";
                        if (b.Contains("dagger") || b.Contains("kris") || b.Contains("stiletto") || b.Contains("dirk")) return "dagger";
                        return "sword";
                    case EquipSlot.Helm: return "helm";
                    case EquipSlot.Chest: return "chest";
                    case EquipSlot.Gloves: return "gloves";
                    case EquipSlot.Legs: return "legs";
                    case EquipSlot.Boots: return "boots";
                    case EquipSlot.Ring: return "ring";
                    case EquipSlot.Amulet: return "amulet";
                }
            }
            string n = it.Name ?? "";
            if (n == "Health Potion") return "health_potion";
            if (n == "Mana Potion") return "mana_potion";
            if (n.StartsWith("Cooked")) return "cooked_fish";
            if (n == "Bread" || n == "Hearty Stew") return "cooking";
            if (n == "Mulled Wine") return "mana_potion";
            if (n.StartsWith("Burnt")) return "burnt_fish";
            if (n.StartsWith("Raw")) return "raw_fish";
            if (n.Contains("Logs")) return "logs";
            if (n.Contains("Ore")) return "ore";
            return null;
        }

        /// <summary>Materials share one icon and are tinted by their item color (copper, iron, mithril...).</summary>
        public static Color IconTint(Item it) =>
            it != null && ((it.Kind == ItemKind.Material && (it.Name.Contains("Ore") || it.Name.Contains("Logs"))) || it.Name == "Mulled Wine")
                ? Color.Lerp(Color.white, it.IconColor, 0.65f) : Color.white;

        public static string AbilityIcon(AbilityId id)
        {
            switch (id)
            {
                case AbilityId.Cleave: return "cleave";
                case AbilityId.Fireball: return "fireball";
                case AbilityId.FrostNova: return "frostnova";
                case AbilityId.Heal: return "heal";
                default: return "meteor";
            }
        }

        public static string SkillIcon(SkillType s) => s.ToString().ToLower();

        // ------------------------------------------------------------------ drawing helpers

        public static void Shadowed(Rect r, string text, GUIStyle style, Color color, int outline = 1)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, color.a * 0.85f);
            if (outline > 1)
            {
                GUI.Label(new Rect(r.x - 1, r.y, r.width, r.height), text, style);
                GUI.Label(new Rect(r.x + 1, r.y, r.width, r.height), text, style);
                GUI.Label(new Rect(r.x, r.y - 1, r.width, r.height), text, style);
            }
            GUI.Label(new Rect(r.x + 1, r.y + 1.5f, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }

        /// <summary>Kenney 3-piece horizontal bar (back + colored fill), with optional centered text.</summary>
        public static void Bar(Rect r, float frac, string color, string text = null, Color? fallback = null)
        {
            frac = Mathf.Clamp01(frac);
            if (BarBackM == null || !bars.TryGetValue(color, out var fill) || fill[1] == null)
            {
                var c = fallback ?? Color.red;
                GUI.color = new Color(0, 0, 0, 0.75f);
                GUI.DrawTexture(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), White);
                GUI.color = Factory.Shade(c, 0.3f);
                GUI.DrawTexture(r, White);
                GUI.color = c;
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * frac, r.height), White);
                GUI.color = Color.white;
            }
            else
            {
                Pieces(r, BarBackL, BarBackM, BarBackR);
                if (frac > 0.001f)
                {
                    GUI.BeginGroup(new Rect(r.x, r.y, r.width * frac, r.height));
                    Pieces(new Rect(0, 0, r.width, r.height), fill[0], fill[1], fill[2]);
                    GUI.EndGroup();
                }
            }
            if (text != null) Shadowed(new Rect(r.x, r.y, r.width, r.height), text, SmallCenter, Color.white);
        }

        static void Pieces(Rect r, Texture2D left, Texture2D mid, Texture2D right)
        {
            float cap = r.height * 0.5f;
            GUI.DrawTexture(new Rect(r.x, r.y, cap, r.height), left);
            GUI.DrawTexture(new Rect(r.x + cap, r.y, Mathf.Max(0, r.width - cap * 2), r.height), mid);
            GUI.DrawTexture(new Rect(r.xMax - cap, r.y, cap, r.height), right);
        }

        /// <summary>A window: leather panel, gold title, close button. Returns true if closed.</summary>
        public static bool Window(Rect r, string title, bool closable = true, bool parchment = false)
        {
            Box(r, parchment ? Parchment : Panel);
            if (title != null)
            {
                Shadowed(new Rect(r.x + 18, r.y + 12, r.width - 60, 28), title, Heading, parchment ? new Color(0.45f, 0.22f, 0.08f) : Gold);
                GUI.color = new Color(0, 0, 0, 0.25f);
                GUI.DrawTexture(new Rect(r.x + 16, r.y + 44, r.width - 32, 2), White);
                GUI.color = Color.white;
            }
            if (!closable) return false;
            var cr = new Rect(r.xMax - 40, r.y + 12, 26, 26);
            if (Close != null)
            {
                GUI.DrawTexture(new Rect(cr.x + 5, cr.y + 5, 16, 15), Close);
                return GUI.Button(cr, GUIContent.none, GUIStyle.none);
            }
            return Btn(cr, "x", SquareButton);
        }

        /// <summary>Draws a skinned box; leather panels and slots are tinted dark for contrast with light text.</summary>
        public static void Box(Rect r, GUIStyle style)
        {
            var old = GUI.backgroundColor;
            if (style == Panel || style == Tooltip) GUI.backgroundColor = new Color(0.46f, 0.38f, 0.31f);
            else if (style == Inset) GUI.backgroundColor = new Color(0.3f, 0.25f, 0.2f);
            GUI.Box(r, GUIContent.none, style);
            GUI.backgroundColor = old;
        }

        /// <summary>A leather button, darkened so its light label reads well.</summary>
        public static bool Btn(Rect r, string text, GUIStyle style = null) => Btn(r, new GUIContent(text), style);

        public static bool Btn(Rect r, GUIContent content, GUIStyle style = null)
        {
            var old = GUI.backgroundColor;
            if (style == null || style == Button || style == SquareButton) GUI.backgroundColor = new Color(0.62f, 0.5f, 0.4f);
            bool clicked = GUI.Button(r, content, style ?? Button);
            GUI.backgroundColor = old;
            if (clicked) Sfx.Play2D("ui_click", 0.35f);
            return clicked;
        }

        public static void IconInSlot(Rect r, Texture2D icon, Color tint, float pad = 4f)
        {
            if (icon == null) return;
            var old = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(new Rect(r.x + pad, r.y + pad, r.width - pad * 2, r.height - pad * 2), icon, ScaleMode.ScaleToFit);
            GUI.color = old;
        }
    }
}
