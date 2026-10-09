using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The raiders' camp outside the gate during the scouts' warning (server/invasion.js, phase "warn"): tents, a fire
    /// and red banners where the first wave masses, war drums beating while they gather, and horns as the countdown
    /// runs down (at 30 and 10 seconds, and the charge). The camp stays, its fire smouldering, until the siege is over.
    /// </summary>
    public class WarCamp : MonoBehaviour
    {
        const float DrumRange = 90f;
        static WarCamp I;

        Transform root;
        string builtFor;
        float nextDrum;
        int lastCall = int.MaxValue; // the last countdown second a horn or call was made at
        readonly System.Random rng = new System.Random(); // never UnityEngine.Random (world generation)

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>Brings the camp in line with the invasion (called whenever its state arrives).</summary>
        public static void Sync()
        {
            if (I == null) I = new GameObject("WarCamp").AddComponent<WarCamp>();
            I.Refresh();
        }

        void Refresh()
        {
            var iv = Invasion.Current;
            bool want = iv != null && !Dungeon.Active && (iv.sx != 0f || iv.sz != 0f) && (iv.phase == "warn" || iv.phase == "gather" || iv.phase == "wave");
            string key = want ? iv.town + "|" + iv.gate : null;
            if (key == builtFor) return;
            if (root != null) Destroy(root.gameObject);
            root = null;
            builtFor = key;
            lastCall = int.MaxValue;
            if (want) Build(new Vector3(iv.sx, 0f, iv.sz), new Vector3(iv.gx, 0f, iv.gz));
        }

        void Build(Vector3 camp, Vector3 gate)
        {
            root = new GameObject("WarCamp").transform;
            var grid = WorldGrid.Instance;
            var toGate = Factory.Flat(gate - camp).normalized;
            if (toGate == Vector3.zero) toGate = Vector3.forward;
            var across = Vector3.Cross(Vector3.up, toGate);
            bool Free(Vector3 p) => grid == null || grid.IsWalkable(p);

            // The fire in the middle, ringed with stones
            ArtLibrary.Spawn("Nature/campfire_stones", root, camp, 1.6f, ArtLibrary.Fit.Width, R(0f, 360f), false);
            PropFire.Add(root, camp + Vector3.up * 0.25f, new Color(1f, 0.55f, 0.2f), 0.9f, true);

            // Tents behind it (away from the gate), opening towards the fire
            string[] tents = { "Nature/tent_detailedOpen", "Nature/tent_detailedClosed", "Nature/tent_smallClosed" };
            for (int i = 0; i < 3; i++)
            {
                var p = camp - toGate * R(5f, 7f) + across * (i - 1) * R(4.5f, 5.5f);
                if (!Free(p)) continue;
                var face = Quaternion.LookRotation(Factory.Flat(camp - p).normalized).eulerAngles.y;
                ArtLibrary.Spawn(tents[i], root, p, 3.2f, ArtLibrary.Fit.Width, face);
            }
            // Red war banners on the side facing the town, and a few more about the camp
            for (int i = 0; i < 5; i++)
            {
                var p = i < 2 ? camp + toGate * 4.5f + across * (i == 0 ? -3.5f : 3.5f) : camp + Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward * R(3f, 6f);
                if (!Free(p)) continue;
                ArtLibrary.Spawn("Props/banner_red", root, p, 2.6f, ArtLibrary.Fit.Height, Quaternion.LookRotation(toGate).eulerAngles.y + R(-15f, 15f));
            }
            // Torches on poles around the camp (they burn day and night; after dark they're what you see of it)
            for (int i = 0; i < 4; i++)
            {
                var p = camp + Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.forward * 6.5f;
                if (!Free(p)) continue;
                Factory.Prim(PrimitiveType.Cylinder, root, p + Vector3.up * 1.1f, new Vector3(0.08f, 1.1f, 0.08f), new Color(0.3f, 0.2f, 0.12f));
                PropFire.Add(root, p + Vector3.up * 2.3f, new Color(1f, 0.6f, 0.25f), 0.35f, false);
            }
            // Supplies: crates and barrels, a weapon rack's worth of spears stuck in the ground
            string[] stores = { "Props/barrel_small_stack", "Props/crates_stacked", "Props/barrel_large" };
            for (int i = 0; i < 4; i++)
            {
                var p = camp + Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward * R(3f, 6f);
                if (Free(p)) ArtLibrary.Spawn(stores[i % stores.Length], root, p, 1f, ArtLibrary.Fit.Height, R(0f, 360f), false);
            }
            for (int i = 0; i < 6; i++)
            {
                var p = camp + toGate * 3f + across * R(-4f, 4f);
                var spear = Factory.Prim(PrimitiveType.Cylinder, root, p + Vector3.up * 0.9f, new Vector3(0.05f, 0.9f, 0.05f), new Color(0.35f, 0.24f, 0.16f));
                spear.transform.rotation = Quaternion.Euler(R(-12f, 12f), 0f, R(-12f, 12f));
            }
        }

        void Update()
        {
            var iv = Invasion.Current;
            if (root == null || iv == null) return;
            var camp = new Vector3(iv.sx, 0f, iv.sz);
            if (iv.phase != "warn") return;

            // The drums, while they mass (heard from the town too, faintly)
            if (Time.time >= nextDrum && !iv.paused)
            {
                nextDrum = Time.time + 2f;
                var hero = Player.I;
                float d = hero != null ? Factory.FlatDistance(hero.transform.position, camp) : 999f;
                if (d < DrumRange) Sfx.Play("war_drum", camp + Vector3.up, Mathf.Lerp(0.85f, 0.35f, d / DrumRange), 0.03f, DrumRange);
            }

            // Horns as the countdown runs down
            int left = Invasion.Countdown;
            if (iv.paused) { lastCall = left; return; }
            if (lastCall > 30 && left <= 30 && left > 10) Horn("The raiders charge in 30 seconds!", false);
            else if (lastCall > 10 && left <= 10 && left > 0) Horn("The raiders charge in 10 seconds!", true);
            lastCall = left;
        }

        void Horn(string banner, bool urgent)
        {
            var hero = Player.I;
            var iv = Invasion.Current;
            bool near = hero != null && iv != null && Factory.FlatDistance(hero.transform.position, Invasion.Gate) < 140f;
            if (!near) return; // the horns are for those who came; everyone else has the tracker
            Sfx.Play2D("war_horn", urgent ? 0.75f : 0.6f, urgent ? 1.04f : 1f);
            GameUI.Banner(banner, Invasion.Color);
        }

        /// <summary>The horns of the charge (the countdown ran out).</summary>
        public static void Charge()
        {
            Sfx.Play2D("war_horn", 0.85f, 0.94f);
            Sfx.Play2D("war_drum", 0.6f);
        }
    }
}
