using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// A town crier on every walled town's square: rings a hand bell and cries the news. An invasion, a world boss
    /// risen, a new rift record are cried at once by every crier; between times, the day's ordinary news. Visual
    /// only (built after the world); clicking one asks for the news again.
    /// </summary>
    public class TownCrier : Interactable
    {
        public static readonly List<TownCrier> Criers = new List<TownCrier>();
        static readonly Vector3 HollowmereSpot = new Vector3(151.9f, 0f, 137.6f); // tools/layout/hollowmere_audit.py

        static readonly string[] Ordinary =
        {
            "Hear ye! The Rift Stone awaits the brave, and the leaderboard the bold!",
            "Hear ye! Bounties posted on the board, fresh every day!",
            "Hear ye! The auction house is open: sell your finds, buy your fortune!",
            "Hear ye! Mind the roads after dark, travellers!",
            "Hear ye! Beastmaster Orla has horses for sale, for those with the coin!",
            "Hear ye! Guilds may raise their banners on the board by the square!",
        };

        CharacterView view;
        Transform bell;
        float nextCry, ringT = -1f;
        string lastNews = "";
        float lastNewsAt = -999f;

        public override string HoverText => "Town Crier\n<the news of the realm>";
        public override Color LabelColor => new Color(1f, 0.9f, 0.6f);
        public override float LabelHeight => 2.6f;

        public static void SpawnAll()
        {
            if (Criers.Count > 0) return;
            var grid = WorldGrid.Instance;
            foreach (var t in WorldGenerator.Towns)
            {
                if (!t.Walled) continue;
                var at = t == WorldGenerator.Towns[0] ? HollowmereSpot : t.Center + new Vector3(-4.5f, 0f, 4.5f);
                if (!grid.IsWalkable(at)) continue;
                var go = new GameObject("TownCrier");
                go.transform.position = at;
                go.transform.rotation = Quaternion.LookRotation(Factory.Flat(t.Center - at).normalized + Vector3.forward * 0.001f);
                var c = go.AddComponent<TownCrier>();
                c.DisplayName = "Town Crier";
                c.InteractRange = 2.4f;
                c.view = CharacterView.Create(go.transform, new CharacterLook { Model = "Characters/Knight", Height = 1.9f, Tint = new Color(1.1f, 0.85f, 0.6f) });
                if (c.view == null) HumanoidModel.Build(go.transform, 1f, new Color(0.9f, 0.75f, 0.6f), new Color(0.6f, 0.35f, 0.15f), Color.gray, Color.gray, false, true);
                // the hand bell
                c.bell = new GameObject("HandBell").transform;
                c.bell.SetParent(go.transform, false);
                c.bell.localPosition = new Vector3(0.38f, 1.25f, 0.2f);
                var brass = new Color(0.9f, 0.7f, 0.3f);
                Factory.Prim(PrimitiveType.Cylinder, c.bell, new Vector3(0f, -0.08f, 0f), new Vector3(0.14f, 0.08f, 0.14f), brass, false, Mat.Glow(brass * 0.3f));
                Factory.Prim(PrimitiveType.Cylinder, c.bell, new Vector3(0f, 0.06f, 0f), new Vector3(0.04f, 0.08f, 0.04f), new Color(0.35f, 0.22f, 0.12f));
                c.AddClickCollider(0.5f, 2.1f);
                c.nextCry = Time.time + Random.Range(15f, 40f);
                Criers.Add(c);
            }
        }

        /// <summary>News for every crier at once (an invasion, a world boss): they ring and cry it.</summary>
        public static void Announce(string news)
        {
            foreach (var c in Criers) if (c != null) c.Cry(news, true);
        }

        void Cry(string text, bool loud)
        {
            if (string.IsNullOrEmpty(text)) return;
            lastNews = loud ? text : lastNews;
            if (loud) lastNewsAt = Time.time;
            ringT = 0f;
            view?.Action("Cheer", 1.4f);
            var p = Player.I;
            if (p == null || Factory.FlatDistance(p.transform.position, transform.position) > 30f) return;
            Speech.Say(transform, 2.3f, text);
            Sfx.Play("bell", bell.position, loud ? 0.7f : 0.45f, 0.1f, 35f);
        }

        void Update()
        {
            view?.UpdateLocomotion(0f);
            if (ringT >= 0f)
            {
                ringT += Time.deltaTime;
                bell.localRotation = Quaternion.Euler(Mathf.Sin(ringT * 24f) * 35f * Mathf.Max(0f, 1f - ringT / 1.2f), 0f, 0f);
                if (ringT > 1.2f) { ringT = -1f; bell.localRotation = Quaternion.identity; }
            }
            if (Time.time < nextCry) return;
            nextCry = Time.time + Random.Range(40f, 75f);
            Cry(Current() ?? Ordinary[Random.Range(0, Ordinary.Length)], false);
        }

        /// <summary>What's going on right now that's worth crying.</summary>
        static string Current()
        {
            var iv = Invasion.Current;
            if (Invasion.Warned) return "Hear ye! Raiders are massing outside the " + iv.gate + " gate of " + iv.town + "! Defenders, to the walls!";
            if (Invasion.Active && iv != null) return "Hear ye! " + iv.town + " is under attack at its " + iv.gate + " gate! To arms!";
            if (iv != null && iv.phase == "lost") return "Hear ye! " + iv.town + " has fallen! The raiders broke the " + iv.gate + " gate!";
            string burning = Sack.AnyBurning(out string g);
            if (burning != null) return "Hear ye! " + burning + " has been sacked and its " + g + " quarter burns! The reeve needs timber, stone and coin!";
            if (SiegeAftermath.Captives != null) return "Hear ye! The raiders hold townsfolk of " + SiegeAftermath.Captives.k + " at their camp! Who will free them?";
            if (SiegeAftermath.FeastTown != null) return "Hear ye! " + SiegeAftermath.FeastTown + " held! There's a feast in the square for its defenders!";
            if (iv != null && iv.phase == "won") return "Hear ye! " + iv.town + " holds! The raiders are beaten!";
            if (WorldBoss.Up && WorldBoss.Current != null) return "Hear ye! " + WorldBoss.Current.name + " walks in " + WorldBoss.Current.region + "! Gather your allies!";
            return null;
        }

        public override void Interact(Player p)
        {
            // the last loud news only while it's fresh: an old "under attack" after the siege is over would be a lie
            bool fresh = !string.IsNullOrEmpty(lastNews) && Time.time - lastNewsAt < 90f;
            Cry(Current() ?? (fresh ? lastNews : Ordinary[Random.Range(0, Ordinary.Length)]), false);
        }
    }
}
