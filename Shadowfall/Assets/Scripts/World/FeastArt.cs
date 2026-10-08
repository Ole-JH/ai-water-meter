using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The look of the Heroes' Feast (SiegeAftermath.Feast): a long clothed table laden end to end (a roast boar with an
    /// apple in its mouth, chickens, loaves, cheeses, pies, fruit, candelabras, a plate and a tankard at every seat),
    /// benches either side, and strings of pennants and hanging lanterns across the square. All primitives and a few art
    /// props: there are no food models.
    /// </summary>
    public static class FeastArt
    {
        /// <summary>How high the benches' seats are (the diners sit on them).</summary>
        public const float BenchY = 0.5f;
        /// <summary>Where the diners sit, along x either side of the table (z = ±SeatZ).</summary>
        public static readonly float[] SeatX = { -2.8f, -1.4f, 0f, 1.4f, 2.8f };
        public const float SeatZ = 1.32f;

        const float Top = 0.865f;   // the top of the cloth: everything stands on this
        static Mesh pennant;
        static System.Random rng;

        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static GameObject P(PrimitiveType type, Transform t, Vector3 at, Vector3 scale, Color c) => Factory.PrimAt(type, t, at, scale, c);

        static readonly Color Wood = new Color(0.5f, 0.34f, 0.2f), Linen = new Color(0.93f, 0.9f, 0.82f), Red = new Color(0.62f, 0.1f, 0.1f),
            Gold = new Color(0.9f, 0.72f, 0.3f), Pewter = new Color(0.72f, 0.72f, 0.76f), Roast = new Color(0.6f, 0.3f, 0.12f),
            Crust = new Color(0.84f, 0.58f, 0.27f), Green = new Color(0.35f, 0.62f, 0.25f);

        /// <summary>The table and benches at <paramref name="at"/>, running along x, with the food on it.</summary>
        public static void Table(Transform t, Vector3 at, System.Random random)
        {
            rng = random;
            // the board on three trestles, a linen cloth hanging over its sides, a red runner with gold edges down the middle
            P(PrimitiveType.Cube, t, at + Vector3.up * 0.78f, new Vector3(7.4f, 0.1f, 1.5f), Wood);
            foreach (float x in new[] { -3.2f, 0f, 3.2f })
            {
                P(PrimitiveType.Cube, t, at + new Vector3(x, 0.37f, 0f), new Vector3(0.12f, 0.74f, 1.1f), Wood * 0.75f);
                P(PrimitiveType.Cube, t, at + new Vector3(x, 0.04f, 0f), new Vector3(0.22f, 0.08f, 1.4f), Wood * 0.65f);
            }
            P(PrimitiveType.Cube, t, at + Vector3.up * 0.85f, new Vector3(7.5f, 0.02f, 1.62f), Linen);
            foreach (float z in new[] { -0.81f, 0.81f }) P(PrimitiveType.Cube, t, at + new Vector3(0f, 0.68f, z), new Vector3(7.5f, 0.34f, 0.02f), Linen * 0.96f);
            foreach (float x in new[] { -3.75f, 3.75f }) P(PrimitiveType.Cube, t, at + new Vector3(x, 0.68f, 0f), new Vector3(0.02f, 0.34f, 1.62f), Linen * 0.96f);
            P(PrimitiveType.Cube, t, at + Vector3.up * 0.858f, new Vector3(7.56f, 0.02f, 0.56f), Red);
            foreach (float z in new[] { -0.27f, 0.27f }) P(PrimitiveType.Cube, t, at + new Vector3(0f, 0.861f, z), new Vector3(7.56f, 0.02f, 0.05f), Gold);
            foreach (float x in new[] { -3.78f, 3.78f }) P(PrimitiveType.Cube, t, at + new Vector3(x, 0.62f, 0f), new Vector3(0.02f, 0.48f, 0.56f), Red); // the runner's ends hang down
            // the benches
            foreach (float z in new[] { -SeatZ + 0.05f, SeatZ - 0.05f })
            {
                P(PrimitiveType.Cube, t, at + new Vector3(0f, BenchY - 0.04f, z), new Vector3(7f, 0.08f, 0.44f), Wood * 0.9f);
                foreach (float x in new[] { -3.1f, -1f, 1f, 3.1f }) P(PrimitiveType.Cube, t, at + new Vector3(x, (BenchY - 0.08f) / 2f, z), new Vector3(0.1f, BenchY - 0.08f, 0.34f), Wood * 0.7f);
            }

            // a place laid at every seat: a pewter plate with meat and greens on it, a tankard of ale (or a goblet of wine)
            foreach (float z in new[] { -1f, 1f })
                foreach (float x in SeatX)
                {
                    var plate = at + new Vector3(x + R(-0.06f, 0.06f), Top, z * 0.53f);
                    P(PrimitiveType.Cylinder, t, plate + Vector3.up * 0.01f, new Vector3(0.4f, 0.01f, 0.4f), Pewter);
                    P(PrimitiveType.Cylinder, t, plate + Vector3.up * 0.022f, new Vector3(0.3f, 0.004f, 0.3f), Pewter * 0.9f);
                    P(PrimitiveType.Sphere, t, plate + new Vector3(-0.04f, 0.06f, 0f), new Vector3(0.2f, 0.08f, 0.14f), Roast);
                    P(PrimitiveType.Sphere, t, plate + new Vector3(0.08f, 0.05f, 0.06f * z), new Vector3(0.08f, 0.07f, 0.08f), new Color(0.9f, 0.78f, 0.4f));
                    P(PrimitiveType.Sphere, t, plate + new Vector3(0.1f, 0.05f, -0.05f * z), new Vector3(0.08f, 0.06f, 0.08f), Green);
                    var cup = plate + new Vector3(0.32f, 0f, -0.14f * z);
                    if (rng.NextDouble() < 0.7) Tankard(t, cup);
                    else Goblet(t, cup);
                }

            // down the middle: the boar in the place of honour, the rest along either side of it
            Boar(t, at + new Vector3(0f, Top, 0f));
            Candelabra(t, at + new Vector3(-3.3f, Top, 0f));
            Bread(t, at + new Vector3(-2.55f, Top, R(-0.05f, 0.05f)));
            Chickens(t, at + new Vector3(-1.75f, Top, R(-0.05f, 0.05f)));
            Fruit(t, at + new Vector3(-1.05f, Top, R(-0.05f, 0.05f)));
            Pie(t, at + new Vector3(1.15f, Top, R(-0.05f, 0.05f)));
            Cheese(t, at + new Vector3(1.85f, Top, R(-0.05f, 0.05f)));
            Bread(t, at + new Vector3(2.6f, Top, R(-0.05f, 0.05f)));
            Candelabra(t, at + new Vector3(3.3f, Top, 0f));
            // and between the plates, jugs of wine and stray apples
            foreach (float x in new[] { -2.1f, 0.7f, 2.1f })
            {
                Jug(t, at + new Vector3(x, Top, 0.36f * (rng.NextDouble() < 0.5 ? 1f : -1f)));
                P(PrimitiveType.Sphere, t, at + new Vector3(x - 0.55f, Top + 0.05f, R(-0.4f, 0.4f)), Vector3.one * 0.1f, new Color(0.8f, 0.15f, 0.12f));
            }
        }

        static void Boar(Transform t, Vector3 p)
        {
            P(PrimitiveType.Cylinder, t, p + Vector3.up * 0.01f, new Vector3(1.45f, 0.012f, 0.6f), Pewter);
            P(PrimitiveType.Sphere, t, p + new Vector3(-0.08f, 0.22f, 0f), new Vector3(0.95f, 0.42f, 0.52f), Roast);
            P(PrimitiveType.Sphere, t, p + new Vector3(-0.1f, 0.3f, 0f), new Vector3(0.7f, 0.3f, 0.36f), Roast * 0.85f); // the crackling
            P(PrimitiveType.Sphere, t, p + new Vector3(0.45f, 0.24f, 0f), new Vector3(0.36f, 0.32f, 0.34f), Roast * 1.05f);
            var snout = P(PrimitiveType.Cylinder, t, p + new Vector3(0.64f, 0.22f, 0f), new Vector3(0.15f, 0.05f, 0.15f), new Color(0.66f, 0.4f, 0.28f));
            snout.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            P(PrimitiveType.Sphere, t, p + new Vector3(0.72f, 0.2f, 0f), Vector3.one * 0.15f, new Color(0.85f, 0.12f, 0.1f)); // the apple
            foreach (float z in new[] { -0.1f, 0.1f })
                P(PrimitiveType.Cube, t, p + new Vector3(0.42f, 0.42f, z), new Vector3(0.08f, 0.1f, 0.03f), Roast * 0.7f).transform.rotation = Quaternion.Euler(z * 150f, 0f, 20f); // ears
            foreach (float x in new[] { -0.36f, 0.2f })
                foreach (float z in new[] { -0.2f, 0.2f })
                    P(PrimitiveType.Capsule, t, p + new Vector3(x, 0.09f, z), new Vector3(0.09f, 0.11f, 0.09f), Roast * 0.9f).transform.rotation = Quaternion.Euler(z > 0 ? 60f : -60f, 0f, 0f);
            // greens and roast roots round the platter
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                var q = p + new Vector3(Mathf.Cos(a) * 0.64f, 0.04f, Mathf.Sin(a) * 0.25f);
                P(PrimitiveType.Sphere, t, q, Vector3.one * R(0.07f, 0.1f), i % 3 == 0 ? new Color(0.92f, 0.5f, 0.15f) : i % 3 == 1 ? Green : new Color(0.9f, 0.8f, 0.45f));
            }
        }

        static void Tankard(Transform t, Vector3 p)
        {
            var wood = new Color(0.45f, 0.3f, 0.17f);
            P(PrimitiveType.Cylinder, t, p + Vector3.up * 0.11f, new Vector3(0.14f, 0.11f, 0.14f), wood);
            foreach (float y in new[] { 0.05f, 0.17f }) P(PrimitiveType.Cylinder, t, p + Vector3.up * y, new Vector3(0.148f, 0.01f, 0.148f), new Color(0.35f, 0.35f, 0.38f));
            P(PrimitiveType.Cylinder, t, p + Vector3.up * 0.225f, new Vector3(0.13f, 0.012f, 0.13f), new Color(0.97f, 0.95f, 0.85f)); // the froth
            P(PrimitiveType.Cube, t, p + new Vector3(0.09f, 0.12f, 0f), new Vector3(0.03f, 0.13f, 0.035f), wood * 0.8f);
        }

        static void Goblet(Transform t, Vector3 p)
        {
            var c = new Color(0.85f, 0.75f, 0.45f);
            P(PrimitiveType.Cylinder, t, p + Vector3.up * 0.008f, new Vector3(0.1f, 0.008f, 0.1f), c);
            P(PrimitiveType.Cylinder, t, p + Vector3.up * 0.08f, new Vector3(0.025f, 0.07f, 0.025f), c);
            P(PrimitiveType.Sphere, t, p + Vector3.up * 0.18f, new Vector3(0.13f, 0.12f, 0.13f), c);
            P(PrimitiveType.Cylinder, t, p + Vector3.up * 0.22f, new Vector3(0.1f, 0.006f, 0.1f), new Color(0.45f, 0.05f, 0.12f)); // the wine
        }

        static void Jug(Transform t, Vector3 p)
        {
            var clay = new Color(0.72f, 0.4f, 0.25f);
            P(PrimitiveType.Sphere, t, p + Vector3.up * 0.13f, new Vector3(0.2f, 0.26f, 0.2f), clay);
            P(PrimitiveType.Cylinder, t, p + Vector3.up * 0.29f, new Vector3(0.09f, 0.05f, 0.09f), clay * 0.9f);
            P(PrimitiveType.Cube, t, p + new Vector3(0.11f, 0.18f, 0f), new Vector3(0.03f, 0.14f, 0.03f), clay * 0.85f);
        }

        static void Candelabra(Transform t, Vector3 p)
        {
            P(PrimitiveType.Cylinder, t, p + Vector3.up * 0.015f, new Vector3(0.2f, 0.015f, 0.2f), Gold);
            P(PrimitiveType.Cylinder, t, p + Vector3.up * 0.2f, new Vector3(0.04f, 0.2f, 0.04f), Gold);
            P(PrimitiveType.Cube, t, p + Vector3.up * 0.4f, new Vector3(0.44f, 0.03f, 0.04f), Gold);
            foreach (float x in new[] { -0.2f, 0f, 0.2f })
            {
                float h = x == 0f ? 0.12f : 0.09f;
                var c = p + new Vector3(x, 0.42f + h, 0f);
                P(PrimitiveType.Cylinder, t, c, new Vector3(0.05f, h, 0.05f), new Color(0.96f, 0.93f, 0.82f));
                PropFire.Add(t, c + Vector3.up * (h + 0.05f), new Color(1f, 0.78f, 0.38f), 0.07f, false);
            }
        }

        static void Bread(Transform t, Vector3 p)
        {
            P(PrimitiveType.Cylinder, t, p + Vector3.up * 0.05f, new Vector3(0.5f, 0.05f, 0.36f), new Color(0.62f, 0.46f, 0.25f)); // the basket
            for (int i = 0; i < 3; i++)
            {
                var loaf = P(PrimitiveType.Capsule, t, p + new Vector3(-0.12f + i * 0.12f, 0.13f + (i == 1 ? 0.05f : 0f), (i - 1) * 0.04f), new Vector3(0.13f, 0.13f, 0.13f), Crust * R(0.92f, 1.05f));
                loaf.transform.rotation = Quaternion.Euler(0f, R(-20f, 20f), 90f);
            }
        }

        static void Chickens(Transform t, Vector3 p)
        {
            P(PrimitiveType.Cylinder, t, p + Vector3.up * 0.01f, new Vector3(0.6f, 0.01f, 0.44f), Pewter);
            foreach (float x in new[] { -0.13f, 0.13f })
            {
                var c = p + new Vector3(x, 0.09f, R(-0.04f, 0.04f));
                P(PrimitiveType.Sphere, t, c, new Vector3(0.24f, 0.16f, 0.19f), new Color(0.8f, 0.5f, 0.2f));
                foreach (float z in new[] { -0.06f, 0.06f })
                {
                    P(PrimitiveType.Capsule, t, c + new Vector3(0.1f, 0.04f, z), new Vector3(0.05f, 0.06f, 0.05f), new Color(0.74f, 0.44f, 0.18f)).transform.rotation = Quaternion.Euler(0f, 0f, -55f);
                    P(PrimitiveType.Sphere, t, c + new Vector3(0.16f, 0.09f, z), Vector3.one * 0.035f, new Color(0.95f, 0.92f, 0.85f)); // the bone end
                }
            }
        }

        static void Fruit(Transform t, Vector3 p)
        {
            P(PrimitiveType.Sphere, t, p + Vector3.up * 0.04f, new Vector3(0.46f, 0.16f, 0.46f), new Color(0.55f, 0.38f, 0.22f)); // the bowl
            var colors = new[] { new Color(0.85f, 0.15f, 0.12f), new Color(0.55f, 0.8f, 0.25f), new Color(0.95f, 0.55f, 0.1f), new Color(0.95f, 0.85f, 0.25f) };
            for (int i = 0; i < 8; i++)
            {
                float a = i * 2.4f;
                float r = i == 0 ? 0f : 0.12f;
                P(PrimitiveType.Sphere, t, p + new Vector3(Mathf.Cos(a) * r, i == 0 ? 0.2f : 0.13f, Mathf.Sin(a) * r), Vector3.one * 0.12f, colors[i % colors.Length]);
            }
            for (int i = 0; i < 6; i++) // grapes over the side
                P(PrimitiveType.Sphere, t, p + new Vector3(0.2f + (i % 2) * 0.04f, 0.12f - i * 0.025f, -0.06f + (i % 3) * 0.04f), Vector3.one * 0.06f, new Color(0.42f, 0.16f, 0.45f));
        }

        static void Pie(Transform t, Vector3 p)
        {
            P(PrimitiveType.Cylinder, t, p + Vector3.up * 0.04f, new Vector3(0.42f, 0.04f, 0.42f), Crust * 0.85f);
            P(PrimitiveType.Cylinder, t, p + Vector3.up * 0.07f, new Vector3(0.36f, 0.015f, 0.36f), Crust);
            for (int i = -1; i <= 1; i++)
            {
                P(PrimitiveType.Cube, t, p + new Vector3(i * 0.1f, 0.088f, 0f), new Vector3(0.03f, 0.012f, 0.32f), Crust * 1.1f);
                P(PrimitiveType.Cube, t, p + new Vector3(0f, 0.09f, i * 0.1f), new Vector3(0.32f, 0.012f, 0.03f), Crust * 1.1f);
            }
        }

        static void Cheese(Transform t, Vector3 p)
        {
            P(PrimitiveType.Cube, t, p + Vector3.up * 0.015f, new Vector3(0.56f, 0.03f, 0.4f), Wood * 1.2f); // the board
            P(PrimitiveType.Cylinder, t, p + new Vector3(-0.06f, 0.1f, 0f), new Vector3(0.34f, 0.07f, 0.34f), new Color(0.96f, 0.8f, 0.32f));
            P(PrimitiveType.Cylinder, t, p + new Vector3(-0.06f, 0.171f, 0f), new Vector3(0.3f, 0.002f, 0.3f), new Color(0.98f, 0.88f, 0.5f));
            P(PrimitiveType.Cube, t, p + new Vector3(0.18f, 0.07f, 0.06f), new Vector3(0.14f, 0.08f, 0.09f), new Color(0.98f, 0.86f, 0.42f)).transform.rotation = Quaternion.Euler(0f, 30f, 0f);
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
