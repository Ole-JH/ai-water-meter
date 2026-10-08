using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// A duel as everyone around sees it (server/duel.js "duelring"): a ring of pennant posts with a rope goes up between
    /// the two, a 3-2-1 countdown in big numbers with a gong at each, "Fight!", villagers nearby stop to watch and cheer;
    /// at the end the loser sinks to the ground, the winner cheers under a banner for a while, the crowd applauds and the
    /// ring comes down. (The duellists' own side, the fight itself, is <see cref="Duel"/>.)
    /// </summary>
    public class DuelRing : MonoBehaviour
    {
        const float Radius = 6.5f;
        static DuelRing current;

        Vector3 center;
        int a, b;
        string nameA, nameB;
        float countFrom = -1f, downAt = -1f;
        int lastCount = -1;
        readonly List<Transform> posts = new List<Transform>();

        public static void OnRing(NetMsg m)
        {
            var names = (m.name ?? "|").Split('|');
            var at = new Vector3(m.x, 0f, m.z);
            switch (m.k)
            {
                case "count":
                    if (current != null) Destroy(current.gameObject);
                    current = new GameObject("DuelRing").AddComponent<DuelRing>();
                    current.Setup(at, m.id, m.tid, names[0], names.Length > 1 ? names[1] : "");
                    break;
                case "fight":
                    if (current == null) { current = new GameObject("DuelRing").AddComponent<DuelRing>(); current.Setup(at, m.id, m.tid, names[0], names.Length > 1 ? names[1] : ""); }
                    current.Fight();
                    break;
                case "end":
                    if (current != null) current.End(m.win);
                    else Finish(m.win, m.win == m.id ? m.tid : m.win == m.tid ? m.id : 0); // we came by late: just the end
                    break;
            }
        }

        void Setup(Vector3 at, int idA, int idB, string na, string nb)
        {
            center = at;
            a = idA;
            b = idB;
            nameA = na;
            nameB = nb;
            transform.position = at;
            var grid = WorldGrid.Instance;
            var red = new Color(0.75f, 0.12f, 0.1f);
            var wood = new Color(0.42f, 0.3f, 0.18f);
            const int n = 10;
            Vector3? last = null, first = null;
            for (int i = 0; i < n; i++)
            {
                float ang = i * Mathf.PI * 2f / n;
                var p = at + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Radius;
                if (grid != null && !grid.IsWalkable(p)) { last = null; continue; }
                var post = new GameObject("RingPost").transform;
                post.SetParent(transform, false);
                post.position = p;
                Factory.Prim(PrimitiveType.Cylinder, post, new Vector3(0f, 0.75f, 0f), new Vector3(0.09f, 0.75f, 0.09f), wood);
                var pennant = Factory.Prim(PrimitiveType.Cube, post, new Vector3(0.18f, 1.35f, 0f), new Vector3(0.36f, 0.22f, 0.02f), i % 2 == 0 ? red : new Color(0.95f, 0.85f, 0.5f));
                pennant.AddComponent<Flutter>();
                post.localScale = new Vector3(1f, 0.01f, 1f); // they spring up
                posts.Add(post);
                if (last.HasValue) Rope(last.Value, p);
                if (!first.HasValue) first = p;
                last = p;
            }
            if (last.HasValue && first.HasValue && posts.Count == n) Rope(last.Value, first.Value);
            SpellFx.Ring(at + Vector3.up * 0.05f, Duel.Color, Radius, 1.2f);
            Sfx.Play("hit_heavy", at, 0.5f, 0.1f, 40f);
            countFrom = Time.time;
            TownWalkerWatch(200f);
            if (!Involved) GameUI.Log(nameA + " and " + nameB + " square up for a duel!", Duel.Color);
        }

        void Rope(Vector3 from, Vector3 to)
        {
            var mid = (from + to) * 0.5f + Vector3.up * 0.95f;
            var rope = Factory.Prim(PrimitiveType.Cube, transform, Vector3.zero, new Vector3(0.03f, 0.03f, Vector3.Distance(from, to)), new Color(0.8f, 0.72f, 0.55f));
            rope.transform.position = mid;
            rope.transform.rotation = Quaternion.LookRotation(to - from);
            rope.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        bool Involved => NetClient.I != null && (NetClient.I.MyId == a || NetClient.I.MyId == b);

        static void TownWalkerWatch(float seconds) { if (current != null) Walker.Watch(current.center, seconds); }

        void Fight()
        {
            countFrom = -1f;
            GameUI.Float(center + Vector3.up * 3.2f, "FIGHT!", Duel.Color, 2.4f);
            Sfx.Play("roar", center, 0.6f, 0.1f, 50f);
            SpellFx.Ring(center + Vector3.up * 0.05f, new Color(1f, 0.9f, 0.5f), Radius, 0.6f);
        }

        void End(int win)
        {
            int loser = win == a ? b : win == b ? a : 0;
            Finish(win, loser);
            downAt = Time.time + 8f;
            Walker.Applaud();
            Walker.Watch(center, 8f);
        }

        /// <summary>The result as everyone sees it: a banner over the winner. Our own hero kneels or cheers (seen by all).</summary>
        static void Finish(int win, int loser)
        {
            var me = Player.I;
            int myId = NetClient.I != null ? NetClient.I.MyId : -1;
            if (win != 0)
            {
                var who = win == myId ? (me != null ? me.transform : null) : RemotePlayer.ById.TryGetValue(win, out var rp) && rp != null ? rp.transform : null;
                if (who != null) WinnerBanner.Raise(who);
            }
            if (me == null) return;
            if (win == myId) me.DoEmote(EmoteDef.Get("cheer"));
            else if (loser == myId) me.DoEmote(EmoteDef.Get("sit")); // down on one's backside: beaten
        }

        void Update()
        {
            // posts spring up, and go down at the end
            float grow = downAt > 0f && Time.time > downAt ? Mathf.Clamp01(1f - (Time.time - downAt) / 0.6f) : 1f;
            foreach (var p in posts)
                if (p != null) p.localScale = new Vector3(1f, Mathf.MoveTowards(p.localScale.y, grow, Time.deltaTime * 3f), 1f);
            if (downAt > 0f && Time.time > downAt + 0.8f) { Destroy(gameObject); return; }

            if (countFrom < 0f) return;
            int n = 3 - Mathf.FloorToInt(Time.time - countFrom);
            if (n != lastCount && n >= 1 && n <= 3)
            {
                lastCount = n;
                GameUI.Float(center + Vector3.up * 3.2f, n.ToString(), new Color(1f, 0.9f, 0.6f), 2.6f);
                Sfx.Play("anvil", center, 0.6f, 0f, 50f);
                Sfx.Play2D("gong", 0.25f, 1.2f + (3 - n) * 0.12f);
            }
        }

        void OnDestroy() { if (current == this) current = null; }
    }

    /// <summary>A pennant moving in the wind.</summary>
    public class Flutter : MonoBehaviour
    {
        float seed;
        void Start() => seed = Random.value * 10f;
        void Update() => transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 4f + seed) * 18f, 0f);
    }

    /// <summary>The duel winner's banner: a pennant on a pole over their head, for a few seconds.</summary>
    public class WinnerBanner : MonoBehaviour
    {
        float until;

        public static void Raise(Transform who)
        {
            var old = who.GetComponentInChildren<WinnerBanner>();
            if (old != null) Destroy(old.gameObject);
            var go = new GameObject("WinnerBanner");
            go.transform.SetParent(who, false);
            go.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            var b = go.AddComponent<WinnerBanner>();
            b.until = Time.time + 12f;
            var gold = new Color(1f, 0.8f, 0.3f);
            Factory.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.04f, 0.5f, 0.04f), new Color(0.35f, 0.25f, 0.15f));
            var flag = Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0.3f, 0.82f, 0f), new Vector3(0.56f, 0.34f, 0.02f), gold, false, Mat.Glow(gold * 0.8f));
            flag.AddComponent<Flutter>();
            SpellFx.Hit(go.transform.position + Vector3.up, gold, false, 20);
        }

        void Update()
        {
            transform.Rotate(0f, 40f * Time.deltaTime, 0f);
            if (Time.time > until) Destroy(gameObject);
        }
    }
}
