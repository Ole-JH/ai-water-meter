using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The look of the Heroes' Feast (SiegeAftermath.Feast): two long clothed tavern tables (KayKit Dungeon) laden with
    /// the Kenney Food Kit - a turkey, a ham, ribs, bread, cheese, fruit, pies, wine, candles and a plate and mug at every
    /// seat - stools down both sides, and strings of pennants and hanging lanterns across the square.
    /// </summary>
    public static class FeastArt
    {
        /// <summary>How high the stools' seats are (the diners sit on them).</summary>
        public const float BenchY = 0.42f;
        /// <summary>Where the diners sit, along x either side of the table (z = ±SeatZ).</summary>
        public static readonly float[] SeatX = { -2.6f, -1.3f, 0f, 1.3f, 2.6f };
        public const float SeatZ = 1.12f;

        const float Top = 0.8f;   // the tabletop: everything stands on this
        static Mesh pennant;
        static System.Random rng;
        static Transform root;

        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static GameObject P(PrimitiveType type, Transform t, Vector3 at, Vector3 scale, Color c) => Factory.PrimAt(type, t, at, scale, c);

        static readonly Color Roast = new Color(0.6f, 0.3f, 0.12f);

        /// <summary>A model from Art/Feast at a spot relative to the table's middle.</summary>
        static GameObject A(string name, float x, float y, float z, float size, ArtLibrary.Fit fit = ArtLibrary.Fit.Width, float yaw = 0f, bool shadows = false)
            => ArtLibrary.Spawn("Feast/" + name, root, new Vector3(x, y, z), size, fit, yaw, shadows);

        /// <summary>
        /// Two long clothed tavern tables end to end at <paramref name="at"/> (the root's position), running along x, with
        /// stools down both sides, a place laid at every seat and the dishes down the middle.
        /// </summary>
        public static void Table(Transform t, Vector3 at, System.Random random)
        {
            rng = random; root = t;
            foreach (float x in new[] { -1.6f, 1.6f }) A("table_long_tablecloth", x, 0f, 0f, Top, ArtLibrary.Fit.Height, 90f, true);
            foreach (float z in new[] { -SeatZ, SeatZ })
                foreach (float x in SeatX) A("stool", x + R(-0.05f, 0.05f), 0f, z, BenchY, ArtLibrary.Fit.Height, R(0f, 90f), true);

            // a place at every seat: a plate with a helping on it, a mug of ale or a glass of wine
            string[] helpings = { "meat-cooked", "meat-sausage", "skewer", "meat-ribs" };
            foreach (float side in new[] { -1f, 1f })
                foreach (float x in SeatX)
                {
                    float px = x + R(-0.05f, 0.05f), pz = side * 0.47f;
                    A("plate-dinner", px, Top, pz, 0.36f);
                    string h = helpings[rng.Next(helpings.Length)];
                    A(h, px, Top + 0.02f, pz, h == "meat-sausage" ? 0.16f : 0.2f, ArtLibrary.Fit.Width, R(0f, 360f));
                    if (rng.NextDouble() < 0.5) A("loaf-baguette", px - 0.05f, Top + 0.03f, pz - side * 0.06f, 0.16f, ArtLibrary.Fit.Width, 70f);
                    if (rng.NextDouble() < 0.65) A("mug", px + 0.3f, Top, pz - side * 0.08f, 0.17f, ArtLibrary.Fit.Height, side > 0 ? 180f : 0f);
                    else A("glass-wine", px + 0.3f, Top, pz - side * 0.08f, 0.22f, ArtLibrary.Fit.Height);
                }

            // down the middle: the turkey in the place of honour, hams, ribs, bread, cheese, fruit, pies, wine and candles
            A("plate", 0f, Top, 0f, 0.9f);
            A("turkey", 0f, Top + 0.04f, 0f, 0.7f, ArtLibrary.Fit.Width, 90f, true);
            A("cutting-board", -1.25f, Top, 0f, 0.55f, ArtLibrary.Fit.Width, 90f);
            A("whole-ham", -1.25f, Top + 0.03f, 0f, 0.42f, ArtLibrary.Fit.Width, 90f + R(-15f, 15f));
            A("cutting-board", 1.25f, Top, 0f, 0.55f, ArtLibrary.Fit.Width, 90f);
            A("meat-ribs", 1.25f, Top + 0.03f, 0f, 0.42f, ArtLibrary.Fit.Width, R(-15f, 15f));
            A("cheese", -0.68f, Top, 0.05f, 0.3f);
            A("cheese-cut", -0.62f, Top, -0.16f, 0.16f, ArtLibrary.Fit.Width, R(0f, 360f));
            A("grapes", 0.66f, Top, -0.06f, 0.22f, ArtLibrary.Fit.Height, R(0f, 360f));
            A("apple", 0.6f, Top, 0.15f, 0.1f); A("pear", 0.76f, Top, 0.12f, 0.13f, ArtLibrary.Fit.Height); A("orange", 0.82f, Top, -0.16f, 0.09f);
            A("loaf-round", -1.95f, Top, 0.05f, 0.4f, ArtLibrary.Fit.Width, R(0f, 360f));
            A("loaf", -2.0f, Top, -0.18f, 0.24f, ArtLibrary.Fit.Width, 90f);
            A("pie", 1.95f, Top, 0f, 0.42f);
            A("watermelon", 2.05f, Top, -0.24f, 0.16f, ArtLibrary.Fit.Width);
            A("pot-stew", -2.95f, Top, 0f, 0.4f, ArtLibrary.Fit.Width, 90f);
            A("cake", 2.95f, Top, 0f, 0.36f);
            foreach (var (x, z) in new[] { (-1.62f, 0.2f), (1.62f, -0.2f), (-0.3f, -0.26f), (0.35f, 0.26f) })
                A(rng.NextDouble() < 0.5 ? "wine-red" : "wine-white", x, Top, z, 0.3f, ArtLibrary.Fit.Height);
            foreach (float x in new[] { -2.4f, 2.4f })
            {
                if (A("candle_lit", x, Top, 0f, 0.28f, ArtLibrary.Fit.Height) != null)
                    PropFire.Add(t, at + new Vector3(x, Top + 0.3f, 0f), new Color(1f, 0.78f, 0.38f), 0.06f, false);
            }
        }

        /// <summary>A roast boar on the spit over the bonfire, turning (rotate the returned transform about its x).</summary>
        public static void SpitBoar(Transform spit)
        {
            var p = spit.position;
            P(PrimitiveType.Sphere, spit, p, new Vector3(0.95f, 0.5f, 0.55f), Roast);
            P(PrimitiveType.Sphere, spit, p + new Vector3(0.5f, 0.02f, 0f), new Vector3(0.36f, 0.34f, 0.34f), Roast * 1.05f);
            P(PrimitiveType.Sphere, spit, p + new Vector3(0.74f, 0f, 0f), Vector3.one * 0.15f, new Color(0.85f, 0.12f, 0.1f));
        }

        /// <summary>A string of pennants between two posts (sagging in the middle), a lantern hung at its lowest point.</summary>
        public static void Bunting(Transform t, Vector3 a0, Vector3 a1, Color[] colors, bool lantern)
        {
            if (pennant == null) pennant = MakePennant();
            var dir = a1 - a0;
            float len = dir.magnitude;
            if (len < 0.5f) return;
            float sag = Mathf.Min(1f, len * 0.08f);
            System.Func<float, Vector3> at = u => Vector3.Lerp(a0, a1, u) + Vector3.down * Mathf.Sin(u * Mathf.PI) * sag;
            // the rope, in short straight pieces
            int segs = Mathf.Max(6, Mathf.RoundToInt(len / 0.8f));
            var rope = new Color(0.32f, 0.24f, 0.16f);
            for (int k = 0; k < segs; k++)
            {
                var q0 = at(k / (float)segs); var q1 = at((k + 1) / (float)segs);
                var seg = P(PrimitiveType.Cylinder, t, (q0 + q1) / 2f, new Vector3(0.025f, Vector3.Distance(q0, q1) / 2f, 0.025f), rope);
                seg.transform.rotation = Quaternion.FromToRotation(Vector3.up, q1 - q0);
            }
            var face = Quaternion.FromToRotation(Vector3.right, Factory.Flat(dir).normalized);
            int n = Mathf.Max(5, Mathf.RoundToInt(len / 0.42f));
            for (int k = 1; k < n; k++)
            {
                var go = new GameObject("Pennant");
                go.transform.SetParent(t, false);
                go.transform.position = at(k / (float)n);
                go.transform.rotation = face * Quaternion.Euler(Random.Range(-12f, 12f), 0f, 0f);
                go.transform.localScale = new Vector3(0.34f, 0.44f, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = pennant;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = Mat.Get(colors[k % colors.Length]);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (lantern)
            {
                var low = at(0.5f);
                var lamp = ArtLibrary.Spawn("Seasonal/lantern-hanging", t, t.InverseTransformPoint(low + Vector3.down * 0.5f), 0.5f, ArtLibrary.Fit.Height, 0f, false);
                if (lamp == null) P(PrimitiveType.Cube, t, low + Vector3.down * 0.25f, new Vector3(0.18f, 0.26f, 0.18f), new Color(0.25f, 0.2f, 0.15f));
                PropFire.Add(t, low + Vector3.down * 0.28f, new Color(1f, 0.78f, 0.4f), 0.1f, false);
            }
        }

        /// <summary>A pennant: a triangle hanging point down from its top edge (both faces, so it shows from either side).</summary>
        static Mesh MakePennant()
        {
            var m = new Mesh { name = "Pennant" };
            var a = new Vector3(-0.5f, 0f, 0f); var b = new Vector3(0.5f, 0f, 0f); var c = new Vector3(0f, -1f, 0f);
            m.vertices = new[] { a, b, c, a, b, c };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.forward, Vector3.forward, Vector3.forward };
            m.triangles = new[] { 0, 1, 2, 3, 5, 4 };
            m.RecalculateBounds();
            return m;
        }
    }
}
