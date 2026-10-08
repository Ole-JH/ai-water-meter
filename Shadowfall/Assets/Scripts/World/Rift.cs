using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The client side of greater rifts (server/rift.js): the Rift Stone in Hollowmere, the window to pick a tier with
    /// the leaderboard (GameUI.Rift.cs), and the progress and timer while inside.
    /// </summary>
    public static class Rift
    {
        public static readonly Color Color = new Color(0.75f, 0.45f, 1f);
        public static readonly Vector3 StonePos = new Vector3(149.5f, 0f, 149.5f); // server/rift.js STONE

        public static int Best { get; private set; }
        public static string[] Board { get; private set; } = new string[0];
        public static int Tier { get; private set; }
        public static int Progress { get; private set; }
        public static string Phase { get; private set; } = "";
        static float leftAt, left;

        public static bool Inside => Dungeon.Active && Tier > 0;
        public static int SecondsLeft => Mathf.Max(0, Mathf.CeilToInt(left - (Time.time - leftAt)));

        public static void OnInfo(NetMsg m)
        {
            int.TryParse(m.k, out var best);
            Best = best;
            Board = m.items ?? new string[0];
        }

        public static void OnState(NetMsg m)
        {
            string was = Phase;
            Tier = m.n;
            Progress = Mathf.RoundToInt(m.i);
            Phase = m.k ?? "";
            left = m.left;
            leftAt = Time.time;
            if (Phase == was) return;
            if (Phase == "guardian") { GameUI.Banner("The Rift Guardian has come!", Color); Sfx.Play2D("roar", 0.7f, 0.8f); }
            else if (Phase == "won")
            {
                GameUI.Banner("Rift cleared! Tier " + (Tier + 1) + " awaits", new Color(0.55f, 1f, 0.55f));
                Sfx.Play2D("quest_done", 0.8f);
                Player.I?.Achievements.Max("rift_tier", Tier);
                if (Tier >= Best) Best = Tier;
            }
            else if (Phase == "late" && was != "") GameUI.Banner("Time's up", new Color(0.8f, 0.7f, 0.6f));
        }

        /// <summary>Out of the rift (any dungeon message for another place): forget it.</summary>
        public static void Left() { Tier = 0; Phase = ""; }

        public static void SpawnStone()
        {
            var go = new GameObject("RiftStone");
            go.transform.position = StonePos;
            go.AddComponent<RiftStone>().Setup();
            var dark = new Color(0.16f, 0.13f, 0.2f);
            Factory.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.12f, 0), new Vector3(1.7f, 0.12f, 1.7f), dark * 1.4f);
            if (ArtLibrary.Spawn("Graveyard/pillar-large", go.transform, new Vector3(0, 0.24f, 0), 2.7f, ArtLibrary.Fit.Height, 45f, true, true, true) == null)
                Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 1.5f, 0), new Vector3(0.5f, 2.6f, 0.5f), dark);
            foreach (float y in new[] { 0.9f, 1.6f, 2.3f })
                Factory.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, y, 0), new Vector3(0.62f, 0.02f, 0.62f), Color, false, Mat.Glow(Color));
            var rift = Factory.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0, 3.35f, 0), new Vector3(0.5f, 0.75f, 0.12f), Color, false, Mat.Glow(Color));
            rift.AddComponent<RiftSpin>();
            var l = new GameObject("RiftLight").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(0, 3.3f, 0);
            l.type = LightType.Point;
            l.color = Color;
            l.range = 7f;
            l.intensity = 1.4f;
            l.shadows = LightShadows.None;
            NightLight.Add(l, 0.7f);
        }
    }

    /// <summary>The stone in Hollowmere's square that opens greater rifts.</summary>
    public class RiftStone : Interactable
    {
        public override Color LabelColor => Rift.Color;
        public override float LabelHeight => 3.8f;
        public override string HoverText => "Rift Stone\n<greater rifts, timed, with a leaderboard>";

        public void Setup()
        {
            DisplayName = "Rift Stone";
            InteractRange = 2.6f;
            AddClickCollider(0.7f, 3f);
        }

        public override void Interact(Player p)
        {
            Sfx.Play("frost_cast", Position, 0.5f, 0.05f);
            NetClient.I?.SendRift("rinfo");
            GameUI.I?.OpenRift();
        }
    }

    /// <summary>The rift's tear above the stone: turns and breathes.</summary>
    public class RiftSpin : MonoBehaviour
    {
        void Update()
        {
            transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
            float s = 1f + Mathf.Sin(Time.time * 2.2f) * 0.08f;
            transform.localScale = new Vector3(0.5f * s, 0.75f * s, 0.12f);
        }
    }
}
