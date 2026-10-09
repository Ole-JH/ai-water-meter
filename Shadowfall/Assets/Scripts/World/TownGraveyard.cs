using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// A small graveyard inside every walled town: an iron fence round a plot, a few old headstones and a lamp. Guards who
    /// fall in a siege are carried here (SiegeLife), laid in a fresh grave, and the townsfolk come out of their houses to
    /// mourn them a few minutes before going home. The graves of a siege stay for the session.
    /// </summary>
    public class TownGraveyard
    {
        const int Rows = 3;
        float W = 10f, D = 8.5f;       // the plot, along x and z (smaller beside a church if that's all the room there is)
        int Cols => Mathf.Clamp(Mathf.FloorToInt((W - 2.4f) / 1.5f) + 1, 3, 5);

        static readonly Dictionary<string, TownGraveyard> byTown = new Dictionary<string, TownGraveyard>();

        public string Town;
        public Vector3 Centre;
        Transform root;
        int next;
        readonly System.Random rng;

        TownGraveyard(string town, int seed) { Town = town; rng = new System.Random(seed); }

        /// <summary>Lays out a graveyard in each walled town (once the world is built).</summary>
        public static void SpawnAll()
        {
            byTown.Clear();
            foreach (var t in WorldGenerator.Towns)
            {
                if (!t.Walled) continue;
                var g = new TownGraveyard(t.Name, t.Name.GetHashCode());
                if (g.Place(t)) byTown[t.Name] = g;
            }
        }

        public static TownGraveyard For(string town) => town != null && byTown.TryGetValue(town, out var g) ? g : null;

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>
        /// Finds a quiet plot inside the walls: walkable, clear of houses, the gateways, the square and every street (no
        /// cobbles, gravel or road on it or a step round it), as far towards the walls as can be. Builds it there.
        /// </summary>
        bool Place(Settlement t)
        {
            var grid = WorldGrid.Instance;
            if (grid == null) return false;
            var r = t.Rect;
            var tries = new List<Vector3>();
            for (float x = r.xMin + 7f; x <= r.xMax - 7f; x += 2f)
                for (float z = r.yMin + 6f; z <= r.yMax - 6f; z += 2f)
                    tries.Add(new Vector3(x, 0f, z));
            // the edges of town first: a graveyard keeps out of the way
            tries.Sort((a, b) => Factory.FlatDistance(b, t.Center).CompareTo(Factory.FlatDistance(a, t.Center)));
            // The churchyards, laid out by hand beside each town's church (the towns are fixed: WorldGenerator)
            if (Churchyard(t.Name, out var spot, out var w, out var d))
            {
                W = w; D = d;
                Centre = spot;
                Build(t);
                return true;
            }
            W = 10f; D = 8.5f;
            for (int pass = 0; pass < 2; pass++)
                foreach (var c in tries)
                {
                    if (Factory.FlatDistance(c, t.Center) < 10f) continue; // not on the square
                    if (!Clear(c, grid, t, pass == 0 ? 1.5f : 0.6f)) continue;
                    Centre = c;
                    Build(t);
                    return true;
                }
            return false;
        }

        bool Clear(Vector3 c, WorldGrid grid, Settlement t, float houseGap)
        {
            var ground = GroundSurface.Current;
            for (float x = -W / 2 - 1f; x <= W / 2 + 1f; x += 1f)
                for (float z = -D / 2 - 1f; z <= D / 2 + 1f; z += 1f)
                {
                    var p = c + new Vector3(x, 0f, z);
                    if (!t.Contains(p.x, p.z) || !grid.IsWalkable(p)) return false;
                    // no street, path or square under it (or right beside it)
                    if (ground != null && ground.RoadWeight(p.x, p.z) + ground.Weight(p.x, p.z, GroundSurface.Cobble) + ground.Weight(p.x, p.z, GroundSurface.Gravel) > 0.25f) return false;
                }
            foreach (var h in WorldGenerator.HouseBounds)
            {
                var hb = h;
                hb.Expand(new Vector3(houseGap * 2f, 10f, houseGap * 2f));
                if (hb.Intersects(new Bounds(c, new Vector3(W, 4f, D)))) return false;
            }
            foreach (var g in new[] { "north", "south", "east", "west" })
                if (Factory.FlatDistance(Rampart.SideOf(t, g).Centre, c) < 12f) return false;
            return true;
        }

        /// <summary>
        /// Where each town's churchyard is (centre, width along x, depth along z), next to its church, out of the streets:
        /// Hollowmere's church stands at cells 156-165 x 159-166 (east of it: open ground to the east wall); in the small
        /// towns (37 cells square, see WorldGenerator.SmallTown) the church fills a corner block 8 cells square, 3 in from
        /// the walls, and the cross streets run 2 cells either side of the middle (offset 18).
        /// </summary>
        static bool Churchyard(string town, out Vector3 centre, out float w, out float d)
        {
            var t = WorldGenerator.TownNamed(town);
            var r = t != null ? t.Rect : new RectInt();
            switch (town)
            {
                case "Hollowmere Village": centre = new Vector3(169f, 0f, 163f); w = 5f; d = 8f; return true;      // east of the church
                case "Frosthaven": centre = new Vector3(r.xMin + 23.3f, 0f, r.yMin + 30f); w = 4.6f; d = 8f; return true; // west of the church (north-east block)
                case "Emberwatch": centre = new Vector3(r.xMin + 13.3f, 0f, r.yMin + 30f); w = 4.6f; d = 8f; return true; // before the church (north-west block)
            }
            centre = Vector3.zero; w = d = 0f;
            return false; // no church (Saltreach): a quiet plot near the walls
        }

        void Build(Settlement t)
        {
            root = new GameObject("Graveyard " + t.Name).transform;
            root.position = Centre;
            // the fence: iron railings round it, a gap facing the middle of town
            var toSquare = Factory.Flat(t.Center - Centre);
            bool gapX = Mathf.Abs(toSquare.x) > Mathf.Abs(toSquare.z);
            for (float x = -W / 2; x <= W / 2 + 0.01f; x += 2f)
                foreach (float z in new[] { -D / 2, D / 2 })
                {
                    bool gap = !gapX && Mathf.Sign(toSquare.z) == Mathf.Sign(z) && Mathf.Abs(x) < 1.1f;
                    if (!gap) ArtLibrary.Spawn("Graveyard/iron-fence", root, new Vector3(x, 0f, z), 2f, ArtLibrary.Fit.Width, 0f, false);
                }
            for (float z = -D / 2 + 1f; z <= D / 2 - 0.99f; z += 2f)
                foreach (float x in new[] { -W / 2, W / 2 })
                {
                    bool gap = gapX && Mathf.Sign(toSquare.x) == Mathf.Sign(x) && Mathf.Abs(z) < 1.1f;
                    if (!gap) ArtLibrary.Spawn("Graveyard/iron-fence", root, new Vector3(x, 0f, z), 2f, ArtLibrary.Fit.Width, 90f, false);
                }
            // the old graves of the back row, a lamp by the way in
            string[] stones = { "Graveyard/gravestone-round", "Graveyard/gravestone-cross", "Graveyard/gravestone-bevel", "Graveyard/gravestone-broken", "Graveyard/gravestone-decorative" };
            for (int i = 0; i < Cols; i++)
            {
                if (rng.NextDouble() < 0.35) continue;
                ArtLibrary.Spawn(stones[rng.Next(stones.Length)], root, Slot(i, Rows) + new Vector3(0f, 0f, 0f) - Centre, R(0.8f, 1.1f), ArtLibrary.Fit.Height, R(-8f, 8f), false);
            }
            ArtLibrary.Spawn("Graveyard/lightpost-single", root, (gapX ? new Vector3(Mathf.Sign(toSquare.x) * (W / 2 + 0.8f), 0f, 1.6f) : new Vector3(1.6f, 0f, Mathf.Sign(toSquare.z) * (D / 2 + 0.8f))), 2.6f, ArtLibrary.Fit.Height, 0f, false);
        }

        /// <summary>Where grave k goes (row by row; the last row holds the old stones).</summary>
        Vector3 Slot(int col, int row) => Centre + new Vector3(-W / 2 + 1.2f + col * ((W - 2.4f) / (Cols - 1)), 0f, -D / 2 + 1.3f + row * ((D - 2.6f) / Rows));

        /// <summary>The next free grave (and its headstone's spot); wraps round when the plot is full.</summary>
        public Vector3 NextGrave()
        {
            int k = next++ % (Cols * Rows);
            return Slot(k % Cols, k / Cols);
        }

        /// <summary>A fresh grave at <paramref name="at"/>: a mound of new earth, a wooden cross, sometimes the guard's helmet on it.</summary>
        public void Dig(Vector3 at)
        {
            if (root == null) return;
            foreach (var d in mounds) if (Factory.FlatDistance(d, at) < 0.3f) return; // (a full graveyard: that one's dug already)
            mounds.Add(at);
            var earth = new Color(0.36f, 0.27f, 0.19f);
            Factory.PrimAt(PrimitiveType.Cube, root, at + Vector3.up * 0.12f, new Vector3(0.9f, 0.25f, 1.7f), earth);
            var w = new Color(0.5f, 0.38f, 0.25f);
            var c = at + Vector3.forward * 0.95f;
            Factory.PrimAt(PrimitiveType.Cube, root, c + Vector3.up * 0.55f, new Vector3(0.09f, 1.1f, 0.09f), w).transform.rotation = Quaternion.Euler(R(-5f, 5f), 0f, R(-5f, 5f));
            Factory.PrimAt(PrimitiveType.Cube, root, c + Vector3.up * 0.8f, new Vector3(0.55f, 0.09f, 0.09f), w);
            if (rng.NextDouble() < 0.5) Factory.PrimAt(PrimitiveType.Sphere, root, c + Vector3.up * 1.12f, new Vector3(0.24f, 0.18f, 0.24f), new Color(0.55f, 0.56f, 0.6f));
            Factory.PrimAt(PrimitiveType.Sphere, root, at + Vector3.up * 0.28f - Vector3.forward * 0.4f, new Vector3(0.18f, 0.08f, 0.18f), new Color(0.9f, 0.85f, 0.4f)); // flowers
            SpellFx.Dust(at, 0.6f, earth);
        }

        readonly List<Vector3> mounds = new List<Vector3>();
        int dug;
        /// <summary>At least <paramref name="n"/> fresh graves of this session (the server's count, for those who weren't here).</summary>
        public int Ensure(int n)
        {
            int made = 0;
            while (dug < n) { Dig(NextGrave()); dug++; made++; }
            return made;
        }

        /// <summary>A grave dug for a body carried here.</summary>
        public void Dug() => dug++;
    }
}
