using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Guild heraldry (server/guild.js "hb": field colour, second colour, emblem): the flag drawn as a small texture,
    /// the banner members wear on their backs (<see cref="GuildBanner"/>), and the guild board in Hollowmere that hangs
    /// the biggest guilds' banners (<see cref="GuildBoard"/>).
    /// </summary>
    public static class GuildHeraldry
    {
        public static readonly Color[] Colours =
        {
            new Color(0.72f, 0.1f, 0.1f), new Color(0.12f, 0.25f, 0.65f), new Color(0.12f, 0.45f, 0.2f), new Color(0.95f, 0.78f, 0.25f),
            new Color(0.92f, 0.9f, 0.85f), new Color(0.1f, 0.1f, 0.12f), new Color(0.45f, 0.18f, 0.55f), new Color(0.9f, 0.45f, 0.12f),
            new Color(0.25f, 0.62f, 0.75f), new Color(0.45f, 0.3f, 0.18f),
        };
        public static readonly string[] ColourNames = { "Crimson", "Azure", "Forest", "Gold", "Silver", "Sable", "Purple", "Orange", "Sky", "Umber" };
        public static readonly string[] Emblems = { "Cross", "Chevron", "Star", "Sun", "Diamond", "Saltire", "Crescent", "Tower" };

        static readonly Dictionary<string, Texture2D> flags = new Dictionary<string, Texture2D>();

        public static bool Parse(string hb, out int c1, out int c2, out int emblem)
        {
            c1 = c2 = emblem = 0;
            if (string.IsNullOrEmpty(hb)) return false;
            var p = hb.Split(',');
            return p.Length == 3 && int.TryParse(p[0], out c1) && int.TryParse(p[1], out c2) && int.TryParse(p[2], out emblem)
                   && c1 >= 0 && c1 < Colours.Length && c2 >= 0 && c2 < Colours.Length && emblem >= 0 && emblem < Emblems.Length;
        }

        /// <summary>The guild's flag (32x48, a swallow-tail notch at the bottom), made once per heraldry.</summary>
        public static Texture2D Flag(string hb)
        {
            if (flags.TryGetValue(hb ?? "", out var t) && t != null) return t;
            Parse(hb, out int c1, out int c2, out int e);
            const int W = 32, H = 48;
            t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "GuildFlag" };
            var px = new Color32[W * H];
            Color field = Colours[c1], charge = Colours[c2], edge = Color.Lerp(charge, Color.black, 0.35f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, v = (y + 0.5f) / H; // v: 0 bottom, 1 top
                    // a swallow-tail notch at the bottom, drawn as a darker fold (alpha cut-outs may not survive a WebGL build's shader stripping)
                    if (v < 0.16f && Mathf.Abs(u - 0.5f) < (0.16f - v) * 2.4f) { px[y * W + x] = Color.Lerp(field, Color.black, 0.55f); continue; }
                    bool border = x < 2 || x >= W - 2 || y >= H - 2;
                    Color c = border ? edge : field;
                    if (!border && Emblem(e, u, v)) c = charge;
                    px[y * W + x] = c;
                }
            t.SetPixels32(px);
            t.Apply();
            flags[hb ?? ""] = t;
            return t;
        }

        /// <summary>Whether (u, v) on the flag is part of the emblem.</summary>
        static bool Emblem(int e, float u, float v)
        {
            float x = u - 0.5f, y = v - 0.58f; // centred a little above the middle
            switch (e)
            {
                case 0: return Mathf.Abs(x) < 0.08f && Mathf.Abs(y) < 0.3f || Mathf.Abs(y - 0.06f) < 0.06f && Mathf.Abs(x) < 0.3f; // cross
                case 1: return Mathf.Abs(y - (-Mathf.Abs(x) * 1.1f + 0.12f)) < 0.07f && Mathf.Abs(x) < 0.42f; // chevron
                case 2: // star (five points)
                {
                    float a = Mathf.Atan2(y, x), r = Mathf.Sqrt(x * x + y * y);
                    float k = Mathf.Cos(5f * (a - Mathf.PI / 2f)) * 0.5f + 0.5f;
                    return r < 0.1f + 0.16f * k;
                }
                case 3: // sun: a disc with rays
                {
                    float a = Mathf.Atan2(y, x), r = Mathf.Sqrt(x * x + y * y);
                    return r < 0.14f || r < 0.27f && Mathf.Cos(a * 8f) > 0.6f;
                }
                case 4: return Mathf.Abs(x) * 1.2f + Mathf.Abs(y) < 0.24f; // diamond (lozenge)
                case 5: return (Mathf.Abs(x - y * 0.66f) < 0.06f || Mathf.Abs(x + y * 0.66f) < 0.06f) && Mathf.Abs(y) < 0.36f; // saltire
                case 6: // crescent
                {
                    float r1 = Mathf.Sqrt(x * x + y * y), r2 = Mathf.Sqrt((x - 0.08f) * (x - 0.08f) + (y - 0.04f) * (y - 0.04f));
                    return r1 < 0.24f && r2 > 0.2f;
                }
                default: // tower: a block with three merlons and a door
                    if (Mathf.Abs(x) < 0.16f && y > -0.25f && y < 0.14f) return !(Mathf.Abs(x) < 0.05f && y < -0.1f);
                    return y >= 0.14f && y < 0.22f && (Mathf.Abs(x) > 0.11f || Mathf.Abs(x) < 0.03f) && Mathf.Abs(x) < 0.16f;
            }
        }

        /// <summary>A flag in the world: a thin board with the guild's flag on both faces.</summary>
        public static GameObject Cloth(Transform parent, string hb, Vector3 localPos, float w, float h)
        {
            var go = Factory.Prim(PrimitiveType.Cube, parent, localPos, new Vector3(w, h, 0.02f), Color.white);
            var r = go.GetComponent<Renderer>();
            var m = Mat.New(Color.white);
            m.mainTexture = Flag(hb);
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }

    /// <summary>The guild banner a member carries on their back: a pole with the guild's flag, swaying as they move.</summary>
    public class GuildBanner : MonoBehaviour
    {
        string hb;
        Transform flag;
        Vector3 lastPos;
        float sway;

        /// <summary>Puts the right banner on <paramref name="body"/> (none for "": no guild). Cheap to call often.</summary>
        public static void Sync(Transform body, string heraldry)
        {
            if (body == null) return;
            var have = body.GetComponentInChildren<GuildBanner>(true);
            if (string.IsNullOrEmpty(heraldry) || !GuildHeraldry.Parse(heraldry, out _, out _, out _))
            {
                if (have != null) Destroy(have.gameObject);
                return;
            }
            if (have != null && have.hb == heraldry) return;
            if (have != null) Destroy(have.gameObject);
            var go = new GameObject("GuildBanner");
            go.transform.SetParent(body, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.24f);
            var b = go.AddComponent<GuildBanner>();
            b.hb = heraldry;
            var wood = new Color(0.35f, 0.25f, 0.15f);
            Factory.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 1.7f, 0f), new Vector3(0.035f, 0.85f, 0.035f), wood);
            Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 2.5f, 0f), new Vector3(0.42f, 0.03f, 0.03f), wood); // cross-bar
            var pivot = new GameObject("Flag").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(0f, 2.5f, 0f);
            GuildHeraldry.Cloth(pivot, heraldry, new Vector3(0f, -0.3f, 0f), 0.4f, 0.6f);
            b.flag = pivot;
            b.lastPos = body.position;
        }

        void Update()
        {
            // swings back as its wearer runs, and settles
            float speed = (transform.position - lastPos).magnitude / Mathf.Max(0.0001f, Time.deltaTime);
            lastPos = transform.position;
            sway = Mathf.Lerp(sway, Mathf.Clamp(speed * 3f, 0f, 30f), Time.deltaTime * 4f);
            if (flag != null) flag.localRotation = Quaternion.Euler(sway + Mathf.Sin(Time.time * 3f) * 3f, 0f, Mathf.Sin(Time.time * 1.7f) * 4f);
        }
    }

    /// <summary>The guild board on Hollowmere's square: the biggest guilds' banners hanging from a beam; click it for them all.</summary>
    public class GuildBoard : Interactable
    {
        public static readonly Vector3 Spot = new Vector3(152.6f, 0f, 148.4f); // tools/layout/hollowmere_audit.py has it
        Transform hangers;
        string shown = "";
        string[] shownBoard;
        float nextAsk;

        public override string HoverText => "Guild Board\n<every guild in the realm, and their banners>";
        public override Color LabelColor => Guild.Color;
        public override float LabelHeight => 3.4f;

        public static void Spawn()
        {
            var go = new GameObject("GuildBoard");
            go.transform.position = Spot;
            go.transform.rotation = Quaternion.LookRotation(Vector3.left); // faces the square
            var b = go.AddComponent<GuildBoard>();
            b.DisplayName = "Guild Board";
            b.InteractRange = 2.6f;
            var wood = new Color(0.4f, 0.28f, 0.17f);
            foreach (float x in new[] { -1.55f, 1.55f })
                Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(x, 1.4f, 0f), new Vector3(0.16f, 2.8f, 0.16f), wood);
            Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 2.75f, 0f), new Vector3(3.4f, 0.16f, 0.2f), wood * 0.9f);
            var roof = Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 3.0f, 0f), new Vector3(3.7f, 0.08f, 0.7f), new Color(0.32f, 0.22f, 0.14f));
            roof.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.9f, 0.04f), new Vector3(2.9f, 0.9f, 0.06f), new Color(0.55f, 0.42f, 0.28f)); // notice board
            b.hangers = new GameObject("Banners").transform;
            b.hangers.SetParent(go.transform, false);
            b.AddClickCollider(1.4f, 3f);
        }

        void Update()
        {
            var p = Player.I;
            if (p == null || Dungeon.Active) return;
            bool near = Factory.FlatDistance(p.transform.position, transform.position) < 35f;
            if (near && Time.time >= nextAsk) { nextAsk = Time.time + 60f; NetClient.I?.RequestGuildList(); }
            // A new list arrives as a new array: only then compare what's on it (joining it every frame made garbage)
            if (ReferenceEquals(Guild.Board, shownBoard)) return;
            shownBoard = Guild.Board;
            string now = string.Join(";", Guild.Board, 0, Mathf.Min(5, Guild.Board.Length));
            if (now == shown) return;
            shown = now;
            foreach (Transform c in hangers) Destroy(c.gameObject);
            int n = Mathf.Min(5, Guild.Board.Length);
            for (int i = 0; i < n; i++)
            {
                var f = Guild.Board[i].Split('|');
                if (f.Length < 4) continue;
                float x = (i - (n - 1) / 2f) * 0.62f;
                GuildHeraldry.Cloth(hangers, f[3], new Vector3(x, 2.22f, -0.06f), 0.5f, 0.85f);
            }
        }

        public override void Interact(Player p)
        {
            NetClient.I?.RequestGuildList();
            GameUI.I?.OpenGuildBoard();
            Sfx.Play2D("book", 0.5f);
        }
    }
}
