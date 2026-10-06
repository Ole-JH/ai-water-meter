using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The hero's personal stash in Hollowmere: a big iron-bound chest by the square that keeps 40 items safe.
    /// Spawned after the world hash is computed, never blocks a tile and uses no UnityEngine.Random.
    /// </summary>
    public class StashChest : Interactable
    {
        public static StashChest I { get; private set; }

        public override Color LabelColor => new Color(1f, 0.85f, 0.4f);
        public override float LabelHeight => 1.6f;

        static readonly Vector3[] candidates =
        {
            new Vector3(149.5f, 0f, 139.5f), new Vector3(139.5f, 0f, 139.5f), new Vector3(149.5f, 0f, 149.5f),
            new Vector3(148.5f, 0f, 137.5f), new Vector3(140.5f, 0f, 137.5f), new Vector3(138.5f, 0f, 141.5f),
        };

        public static void Spawn()
        {
            var pos = candidates[0];
            foreach (var c in candidates)
                if (Clear(c)) { pos = c; break; }
            var go = new GameObject("Stash");
            go.transform.position = pos;
            var s = go.AddComponent<StashChest>();
            s.DisplayName = "Stash";
            s.InteractRange = 2f;
            s.AddClickCollider(0.7f, 1.1f);
            float face = Mathf.Atan2(144.5f - pos.x, 141.5f - pos.z) * Mathf.Rad2Deg; // toward the square
            if (ArtLibrary.Spawn("Props/chest", go.transform, Vector3.zero, 1.25f, ArtLibrary.Fit.Width, face) == null)
                Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0.45f, 0), new Vector3(1.2f, 0.9f, 0.8f), new Color(0.4f, 0.28f, 0.14f));
            var l = new GameObject("StashLight").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.75f, 0.4f);
            l.range = 4f;
            l.intensity = 1.2f;
            l.shadows = LightShadows.None;
            I = s;
        }

        static bool Clear(Vector3 p)
        {
            var grid = WorldGrid.Instance;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (!grid.IsWalkable(p + new Vector3(dx, 0f, dz))) return false;
            foreach (var it in All)
                if (it != null && Factory.FlatDistance(it.Position, p) < 2.5f) return false;
            return true;
        }

        public override void Interact(Player p)
        {
            Sfx.Play("loot", transform.position, 0.6f, 0.05f);
            GameUI.I?.OpenStash();
        }
    }
}
