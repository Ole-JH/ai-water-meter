using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Dark gothic UI skin for the IMGUI interface: procedural panels, title plates, buttons, slots, bars and
    /// orbs (tools/ui/make_skin.py, CC0), game-icons.net icons (CC BY 3.0), Cinzel + Alegreya Sans fonts
    /// (SIL OFL). Everything degrades to flat colors if a texture is missing.
    /// </summary>
    public static class UISkin
    {
        public static readonly Color Gold = new Color(1f, 0.82f, 0.42f);
        public static readonly Color Cream = new Color(0.96f, 0.9f, 0.78f);
        public static readonly Color Ink = new Color(0.9f, 0.84f, 0.72f); // body text in windows (light on the dark skin)
        public static readonly Color Muted = new Color(0.72f, 0.66f, 0.56f);

        public static Font Title, Body, Bold;
        public static GUIStyle Panel, PanelPlain, Parchment, Inset, InsetLight, Slot, Button, ButtonLight, SquareButton, Field, Tooltip, TitlePlate, BarFrame;
        public static GUIStyle Label, LabelCenter, Small, SmallCenter, SmallRight, Heading, HeadingCenter, TitleHuge, Banner, FloatText,
            Rich, RichSmall, Ink14, InkRich;
        public static Texture2D White, Circle, BarBackL, BarBackM, BarBackR, Close, CloseHover, DividerTex, FadeDown,
            OrbLiquid, OrbGlass, OrbFrame;
        static readonly Dictionary<string, Texture2D[]> bars = new Dictionary<string, Texture2D[]>();
        static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        static bool ready;

        static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();

        public static Texture2D Tex(string path)
        {
            if (!texCache.TryGetValue(path, out var t)) texCache[path] = t = Resources.Load<Texture2D>("UI/" + path);
            return t;
        }

        static readonly Dictionary<(GUIStyle, int, int, int, int, Font), GUIStyle> variants = new Dictionary<(GUIStyle, int, int, int, int, Font), GUIStyle>();

        /// <summary>
        /// A cached copy of <paramref name="baseStyle"/> with a few properties changed. Use this instead of
        /// <c>new GUIStyle(...)</c> inside OnGUI, which runs several times per frame and would allocate every time.
        /// </summary>
        public static GUIStyle V(GUIStyle baseStyle, int fontSize = 0, TextAnchor? alignment = null, bool? wordWrap = null,
            FontStyle? fontStyle = null, Font font = null)
        {
            var key = (baseStyle, fontSize, alignment.HasValue ? (int)alignment.Value : -1, wordWrap.HasValue ? (wordWrap.Value ? 1 : 0) : -1,
                fontStyle.HasValue ? (int)fontStyle.Value : -1, font);
            if (variants.TryGetValue(key, out var s)) return s;
            s = new GUIStyle(baseStyle);
            if (fontSize > 0) s.fontSize = fontSize;
            if (alignment.HasValue) s.alignment = alignment.Value;
            if (wordWrap.HasValue) s.wordWrap = wordWrap.Value;
            if (fontStyle.HasValue) s.fontStyle = fontStyle.Value;
            if (font != null) s.font = font;
            variants[key] = s;
            return s;
        }

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

            Panel = Box("Gothic/panel", 34, new Color(0.08f, 0.06f, 0.05f, 0.95f), 22);
            PanelPlain = Box("Gothic/panel_plain", 14, new Color(0.08f, 0.06f, 0.05f, 0.95f), 14);
            Parchment = Panel; // windows and dialogs share the dark skin
            Inset = Box("Gothic/inset", 8, new Color(0.05f, 0.04f, 0.03f, 0.9f));
            InsetLight = Box("Gothic/inset_selected", 8, new Color(0.2f, 0.15f, 0.08f, 0.95f));
            Slot = Box("Gothic/slot", 6, new Color(0.04f, 0.03f, 0.03f, 0.95f));
            TitlePlate = Box("Gothic/title_plate", 26, new Color(0.25f, 0.08f, 0.05f, 0.95f));
            TitlePlate.border = new RectOffset(26, 26, 0, 0);
            BarFrame = Box("Gothic/bar_frame", 8, new Color(0f, 0f, 0f, 0.8f));

            Button = Btn("Gothic/button", "Gothic/button_hover", "Gothic/button_pressed", Cream, 15, 14);
            ButtonLight = Button;
            SquareButton = Btn("Gothic/button_square", "Gothic/button_square_hover", "Gothic/button_square_pressed", Cream, 17, 12);
            SquareButton.padding = new RectOffset(4, 4, 4, 4);

            Field = new GUIStyle(GUI.skin.textField)
            {
                font = Body, fontSize = 17, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 12, 4, 4), border = new RectOffset(8, 8, 8, 8)
            };
            var fieldTex = Tex("Gothic/field");
            var fieldFocus = Tex("Gothic/field_focus");
            if (fieldTex != null) Field.normal.background = Field.hover.background = fieldTex;
            if (fieldFocus != null) Field.focused.background = Field.active.background = fieldFocus;
            Field.normal.textColor = Field.focused.textColor = Field.hover.textColor = Field.active.textColor = Cream;
            GUI.skin.settings.cursorColor = Gold;
            GUI.skin.settings.selectionColor = new Color(0.6f, 0.4f, 0.15f, 0.6f);

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

            Tooltip = Box("Gothic/tooltip", 14, new Color(0.05f, 0.04f, 0.03f, 0.97f));
            Tooltip.padding = new RectOffset(14, 14, 12, 12);
            Close = Tex("Gothic/close");
            CloseHover = Tex("Gothic/close_hover");
            DividerTex = Tex("Gothic/divider");
            FadeDown = Tex("Gothic/fade_down");
            OrbLiquid = Tex("Gothic/orb_liquid");
            OrbGlass = Tex("Gothic/orb_glass");
            OrbFrame = Tex("Gothic/orb_frame");

            BarBackL = Tex("Skin/barBack_horizontalLeft");
            BarBackM = Tex("Skin/barBack_horizontalMid");
            BarBackR = Tex("Skin/barBack_horizontalRight");
            foreach (var c in new[] { "Red", "Green", "Blue", "Yellow", "Purple" })
                barFills[c] = Tex("Gothic/bar_" + c.ToLower());
        }

        static readonly Dictionary<string, Texture2D> barFills = new Dictionary<string, Texture2D>();

        static GUIStyle Box(string tex, int border, Color fallback, int padding = -1)
        {
            var t = Tex(tex);
            if (padding < 0) padding = border;
            var s = new GUIStyle { border = new RectOffset(border, border, border, border), padding = new RectOffset(padding, padding, padding, padding) };
            s.normal.background = t != null ? t : Solid(fallback);
            return s;
        }

        static GUIStyle Btn(string tex, string hover, string pressed, Color text, int size, int border)
        {
            var s = new GUIStyle(GUI.skin.button)
            {
                font = Title ?? Bold, fontSize = size, alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(border, border, border, border), padding = new RectOffset(10, 10, 4, 4)
            };
            var t = Tex(tex);
            if (t != null)
            {
                s.normal.background = s.focused.background = t;
                s.hover.background = Tex(hover) ?? t;
                s.active.background = Tex(pressed) ?? t;
            }
            s.normal.textColor = s.focused.textColor = text;
            s.hover.textColor = Color.Lerp(text, new Color(1f, 0.9f, 0.6f), 0.6f);
            s.active.textColor = Color.Lerp(text, Color.gray, 0.3f);
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
            if (it.Kind == ItemKind.Gem) return "gem";
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
            it != null && it.Kind == ItemKind.Gem ? it.IconColor :
            it != null && ((it.Kind == ItemKind.Material && (it.Name.Contains("Ore") || it.Name.Contains("Logs"))) || it.Name == "Mulled Wine")
                ? Color.Lerp(Color.white, it.IconColor, 0.65f) : Color.white;

        public static string AbilityIcon(AbilityId id) => AbilityDef.Get(id).Icon;

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

        /// <summary>A framed bar with a glossy colored fill ("Red", "Blue", "Green", "Yellow", "Purple") and optional centered text.</summary>
        public static void Bar(Rect r, float frac, string color, string text = null, Color? fallback = null)
        {
            frac = Mathf.Clamp01(frac);
            if (barFills.TryGetValue(color, out var fillTex) && fillTex != null && BarFrame.normal.background != null)
            {
                GUI.Box(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), GUIContent.none, BarFrame);
                var inner = new Rect(r.x + 1, r.y + 1, (r.width - 2) * frac, r.height - 2);
                if (frac > 0.001f) GUI.DrawTexture(inner, fillTex, ScaleMode.StretchToFill);
            }
            else if (BarBackM == null || !bars.TryGetValue(color, out var fill) || fill[1] == null)
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

        /// <summary>A window: ornate dark panel with a title plate on its top edge and a close button. Returns true if closed.</summary>
        public static bool Window(Rect r, string title, bool closable = true, bool parchment = false)
        {
            Box(r, Panel);
            if (title != null)
            {
                float tw = Heading.CalcSize(new GUIContent(title)).x;
                float pw = Mathf.Min(r.width - 90f, tw + 96f);
                var plate = new Rect(r.x + (r.width - pw) / 2f, r.y - 16f, pw, 46f);
                GUI.Box(plate, GUIContent.none, TitlePlate);
                Shadowed(new Rect(plate.x, plate.y + 1, plate.width, plate.height), title, HeadingCenter, Gold, 2);
            }
            if (!closable) return false;
            var cr = new Rect(r.xMax - 44, r.y + 12, 30, 30);
            if (Close != null)
            {
                GUI.DrawTexture(cr, cr.Contains(Event.current.mousePosition) && CloseHover != null ? CloseHover : Close);
                bool hit = GUI.Button(cr, GUIContent.none, GUIStyle.none);
                if (hit) Sfx.Play2D("ui_close", 0.4f);
                return hit;
            }
            return Btn(cr, "x", SquareButton);
        }

        /// <summary>Draws a skinned box.</summary>
        public static void Box(Rect r, GUIStyle style) => GUI.Box(r, GUIContent.none, style);

        /// <summary>An ornamental gold divider line, centered in <paramref name="r"/>.</summary>
        public static void Divider(Rect r)
        {
            if (DividerTex != null) GUI.DrawTexture(new Rect(r.x, r.y + r.height / 2f - 9f, r.width, 18f), DividerTex, ScaleMode.StretchToFill);
            else
            {
                GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.5f);
                GUI.DrawTexture(new Rect(r.x, r.y + r.height / 2f, r.width, 1f), White);
                GUI.color = Color.white;
            }
        }

        /// <summary>A Diablo-style health/mana orb: tinted liquid filled to <paramref name="frac"/>, glass and a bronze frame.</summary>
        public static bool Orb(Rect r, float frac, Color color)
        {
            if (OrbLiquid == null || OrbFrame == null) return false;
            frac = Mathf.Clamp01(frac);
            var old = GUI.color;
            GUI.color = Factory.Shade(color, 0.2f);
            GUI.DrawTexture(r, OrbLiquid);
            GUI.color = color;
            // the liquid occupies the inner 84% of the texture: map the fill height onto that
            float inner = 0.84f, lo = (1f - inner) / 2f;
            float v = lo + inner * frac;
            if (frac > 0.001f) GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y + r.height * (1f - v), r.width, r.height * v), OrbLiquid, new Rect(0, 0, 1, v));
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            if (OrbGlass != null) GUI.DrawTexture(r, OrbGlass);
            GUI.color = Color.white;
            GUI.DrawTexture(r, OrbFrame);
            GUI.color = old;
            return true;
        }

        public static bool Btn(Rect r, string text, GUIStyle style = null) => Btn(r, new GUIContent(text), style);

        public static bool Btn(Rect r, GUIContent content, GUIStyle style = null)
        {
            bool clicked = GUI.Button(r, content, style ?? Button);
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
