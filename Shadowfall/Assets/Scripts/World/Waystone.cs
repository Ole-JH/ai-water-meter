using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// A rune stone in every town's square. Walk up to one and it's attuned for good (saved with the hero's
    /// achievement counters as "waystone.&lt;town&gt;"); use any waystone to travel to any attuned one. Hollowmere's is
    /// known from the start. Spawned after the world hash is computed and never blocks a tile.
    /// </summary>
    public class Waystone : Interactable
    {
        public static readonly List<Waystone> Stones = new List<Waystone>();
        const float AttuneRange = 12f;

        public Settlement Town;
        float nextCheck;

        public override Color LabelColor => new Color(0.55f, 0.8f, 1f);
        public override float LabelHeight => 3.1f;
        public override string HoverText => "Waystone of " + Town.Name + (Known(Town) ? "" : "\n<not attuned>");

        public static bool Known(Settlement t)
        {
            var p = Player.I;
            return t == WorldGenerator.Towns[0] || (p != null && p.Achievements.Get("waystone." + t.Name) > 0);
        }

        public static void SpawnAll()
        {
            foreach (var t in WorldGenerator.Towns) Spawn(t);
        }

        static void Spawn(Settlement t)
        {
            var go = new GameObject("Waystone");
            go.transform.position = t.Waystone;
            var w = go.AddComponent<Waystone>();
            w.Town = t;
            w.DisplayName = "Waystone";
            w.InteractRange = 2.4f;
            w.AddClickCollider(0.6f, 2.6f);
            var rune = new Color(0.45f, 0.75f, 1f);
            if (ArtLibrary.Spawn("Graveyard/stone-wall-column", go.transform, Vector3.zero, 2.6f, ArtLibrary.Fit.Height, 0f) == null)
                Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 1.2f, 0), new Vector3(0.7f, 2.4f, 0.7f), new Color(0.42f, 0.42f, 0.46f));
            var orb = Factory.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0, 2.95f, 0), Vector3.one * 0.38f, rune, false, Mat.Glow(rune));
            orb.AddComponent<WaystoneOrb>();
            var l = new GameObject("WaystoneLight").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(0, 2.9f, 0);
            l.type = LightType.Point;
            l.color = rune;
            l.range = 6f;
            l.intensity = 1.2f;
            l.shadows = LightShadows.None;
            NightLight.Add(l, 0.6f);
            Stones.Add(w);
        }

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.5f;
            var p = Player.I;
            if (p == null || Dungeon.Active || Known(Town) || Factory.FlatDistance(p.transform.position, Position) > AttuneRange) return;
            p.Achievements.Once("waystone", Town.Name);
            SpellFx.Ring(Position, new Color(0.55f, 0.8f, 1f), 2.5f, 0.8f);
            Sfx.Play("holy_cast", Position, 0.6f, 0.05f);
            GameUI.Banner("Waystone attuned: " + Town.Name, new Color(0.55f, 0.8f, 1f));
            GameUI.Log("You attune to the waystone of " + Town.Name + ". Use any waystone to travel here.", new Color(0.55f, 0.8f, 1f));
        }

        public override void Interact(Player p)
        {
            Sfx.Play("holy_cast", Position, 0.4f, 0.05f);
            GameUI.I?.OpenWaystone(this);
        }

        /// <summary>Where you arrive: just south of the stone, on a walkable tile.</summary>
        public Vector3 Arrival
        {
            get
            {
                var grid = WorldGrid.Instance;
                var c = grid.WorldToCell(Position + new Vector3(0f, 0f, -1.6f));
                return grid.NearestWalkable(c, 6, out var free) ? grid.CellToWorld(free) : Position;
            }
        }

        /// <summary>Takes the hero to another waystone (if attuned, and not in the middle of a fight).</summary>
        public static void Travel(Player p, Waystone to)
        {
            if (p == null || p.IsDead || to == null) return;
            if (!Known(to.Town)) { GameUI.Float(p.transform.position + Vector3.up * 2.5f, "You haven't attuned to that waystone", Color.gray, 0.8f); return; }
            if (Time.time - p.LastDamagedTime < 5f) { GameUI.Float(p.transform.position + Vector3.up * 2.5f, "Not while you're fighting", new Color(1f, 0.6f, 0.4f), 0.8f); return; }
            SpellFx.Column(p.transform.position, new Color(0.5f, 0.75f, 1f), 1f, 6f, 0.6f);
            Sfx.Play2D("blink", 0.6f);
            p.TeleportTo(to.Arrival);
            p.Achievements.Add("waystone_trips");
            GameUI.Log("The waystone carries you to " + to.Town.Name + ".", new Color(0.6f, 0.8f, 1f));
        }
    }

    /// <summary>The waystone's rune orb bobs and pulses.</summary>
    public class WaystoneOrb : MonoBehaviour
    {
        Vector3 home;
        float phase;

        void Start()
        {
            home = transform.localPosition;
            phase = transform.position.x * 0.37f;
        }

        void Update()
        {
            float t = Time.time + phase;
            transform.localPosition = home + Vector3.up * Mathf.Sin(t * 1.3f) * 0.08f;
            transform.localScale = Vector3.one * (0.36f + Mathf.Sin(t * 2.1f) * 0.03f);
        }
    }
}
