using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shadowfall
{
    /// <summary>
    /// Little bits of life around the hero: fireflies in the woods at night, crows by day and bats by
    /// night circling overhead, and dead leaves drifting down in Whisperwood.
    /// Critters live in a pool around the hero and are recycled when they drift too far away.
    /// </summary>
    public class Ambience : MonoBehaviour
    {
        const float Radius = 18f;

        class Critter
        {
            public Transform T, WingL, WingR;
            public Vector3 Anchor, Velocity;
            public float Phase, Speed, Size;
            public int Kind; // 1 firefly, 2 bird, 3 leaf
            public Renderer[] Renderers;
        }

        readonly List<Critter> fireflies = new List<Critter>(), birds = new List<Critter>(), leaves = new List<Critter>();
        Material vcMat;
        readonly System.Random rng = new System.Random(5150);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        void Start()
        {
            var shader = Resources.Load<Shader>("Shaders/ShadowfallGrass");
            if (shader != null)
            {
                vcMat = new Material(shader) { name = "Critters" };
                vcMat.SetFloat("_Wind", 0f);
            }
            for (int i = 0; i < 28; i++) fireflies.Add(MakeFirefly());
            for (int i = 0; i < 5; i++) birds.Add(MakeFlyer(2, new Color(0.08f, 0.08f, 0.1f), 0.55f));
            for (int i = 0; i < 14; i++) leaves.Add(MakeLeaf());
            foreach (var list in new[] { fireflies, birds, leaves })
                foreach (var c in list) Show(c, false);
        }

        // ------------------------------------------------------------------ construction

        Mesh Quad(Color c, float w, float h, Vector3 offset)
        {
            var m = new Mesh { name = "CritterQuad" };
            m.vertices = new[] { offset + new Vector3(0, 0, -h / 2), offset + new Vector3(w, 0, -h / 2), offset + new Vector3(w, 0, h / 2), offset + new Vector3(0, 0, h / 2) };
            var cc = new Color(c.r, c.g, c.b, 0f);
            m.colors = new[] { cc, cc, cc, cc };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        Transform Part(Transform parent, Mesh mesh, Color c)
        {
            var go = new GameObject("part");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = vcMat != null ? vcMat : Mat.Get(c);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        /// <summary>A body with two flapping wings (crows by day, bats by night).</summary>
        Critter MakeFlyer(int kind, Color color, float size)
        {
            var root = new GameObject("Bird").transform;
            root.SetParent(transform, false);
            var c = new Critter { T = root, Kind = kind, Size = size, Phase = R(0, 10), Speed = R(4f, 6f) };
            c.WingL = Part(root, Quad(color, size, size * 0.45f, Vector3.zero), color);
            c.WingR = Part(root, Quad(color, size, size * 0.45f, Vector3.zero), color);
            c.WingR.localScale = new Vector3(-1, 1, 1);
            Part(root, Quad(color * 0.8f, size * 0.25f, size * 0.9f, new Vector3(-size * 0.125f, 0.01f, 0)), color);
            c.Renderers = root.GetComponentsInChildren<Renderer>();
            return c;
        }

        Critter MakeFirefly()
        {
            var glow = new Color(0.55f, 0.75f, 0.3f);
            var go = Factory.Prim(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.07f, glow, false, Mat.Glow(glow));
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return new Critter { T = go.transform, Kind = 1, Phase = R(0, 10), Speed = R(0.3f, 0.7f), Renderers = new[] { go.GetComponent<Renderer>() } };
        }

        Critter MakeLeaf()
        {
            Color[] c = { new Color(0.42f, 0.26f, 0.1f), new Color(0.35f, 0.2f, 0.09f), new Color(0.3f, 0.32f, 0.12f), new Color(0.45f, 0.33f, 0.12f) };
            var col = c[rng.Next(c.Length)];
            var root = new GameObject("Leaf").transform;
            root.SetParent(transform, false);
            Part(root, Quad(col, 0.14f, 0.09f, new Vector3(-0.07f, 0, 0)), col);
            return new Critter { T = root, Kind = 3, Phase = R(0, 10), Speed = R(0.5f, 0.9f), Renderers = root.GetComponentsInChildren<Renderer>() };
        }

        static void Show(Critter c, bool on)
        {
            foreach (var r in c.Renderers) if (r != null && r.enabled != on) r.enabled = on;
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            var p = Player.I;
            var world = GameManager.I != null ? GameManager.I.World : null;
            if (p == null || world == null)
            {
                foreach (var list in new[] { fireflies, birds, leaves }) foreach (var c in list) Show(c, false);
                return;
            }
            var hero = p.transform.position;
            float night = DayNight.Night;
            string zone = WorldGenerator.ZoneAt(hero);
            bool town = WorldGenerator.InTown(hero);
            bool wild = !town && !WorldGenerator.InCrypt(hero);

            int wantFireflies = night > 0.6f && wild && zone == "Whisperwood" ? fireflies.Count : 0;
            int wantBirds = town ? 2 : zone == "Forsaken Graveyard" ? birds.Count : 3;
            int wantLeaves = zone == "Whisperwood" && !town ? leaves.Count : 0;

            float t = Time.time, dt = Time.deltaTime;
            for (int i = 0; i < fireflies.Count; i++) UpdateFirefly(fireflies[i], i < wantFireflies, hero, t, world);
            for (int i = 0; i < birds.Count; i++) UpdateBird(birds[i], i < wantBirds, hero, t, dt, night);
            for (int i = 0; i < leaves.Count; i++) UpdateLeaf(leaves[i], i < wantLeaves, hero, t, dt);
        }

        Vector3 Near(Vector3 hero, float minR, float maxR)
        {
            float a = R(0, Mathf.PI * 2), d = R(minR, maxR);
            return hero + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
        }

        /// <summary>A spot near the hero where something grows (so fireflies don't hover over the road).</summary>
        Vector3 GrassyNear(Vector3 hero, WorldGenerator world)
        {
            for (int i = 0; i < 6; i++)
            {
                var pos = Near(hero, 3f, Radius);
                if (world.GrassAt(pos) > 0.5f) return pos;
            }
            return Near(hero, 3f, Radius);
        }

        bool Recycle(Critter c, bool want, Vector3 hero)
        {
            bool visible = c.Renderers.Length > 0 && c.Renderers[0].enabled;
            if (!want) { if (visible) Show(c, false); return false; }
            if (!visible || Factory.FlatDistance(c.Anchor, hero) > Radius + 6f) { Show(c, true); return true; }
            return false;
        }

        void UpdateFirefly(Critter c, bool want, Vector3 hero, float t, WorldGenerator world)
        {
            if (Recycle(c, want, hero)) c.Anchor = GrassyNear(hero, world);
            if (!want) return;
            float k = t * c.Speed + c.Phase;
            c.T.position = c.Anchor + new Vector3(Mathf.Sin(k) * 1.5f, 0.4f + Mathf.Sin(k * 1.7f) * 0.35f + Mathf.PerlinNoise(k * 0.5f, c.Phase) * 0.8f, Mathf.Cos(k * 0.8f) * 1.5f);
            float blink = Mathf.Clamp01(Mathf.Sin(t * 1.3f + c.Phase * 3f) * 2f);
            c.T.localScale = Vector3.one * (0.02f + 0.06f * blink);
        }

        void UpdateBird(Critter c, bool want, Vector3 hero, float t, float dt, float night)
        {
            if (Recycle(c, want, hero))
            {
                c.Anchor = Near(hero, 0f, 10f);
                c.Size = R(8f, 14f); // reuse as circle radius
                c.Phase = R(0, 10);
            }
            if (!want) return;
            bool bat = night > 0.6f;
            float speed = bat ? 1.4f : 0.45f;
            float k = t * speed * (c.Speed / 5f) + c.Phase;
            float radius = bat ? c.Size * 0.5f : c.Size;
            float height = bat ? 4f + Mathf.Sin(k * 3f) * 1.2f : 10f + Mathf.Sin(k * 0.5f) * 1.5f;
            var pos = c.Anchor + new Vector3(Mathf.Cos(k) * radius, height, Mathf.Sin(k) * radius);
            var vel = pos - c.T.position;
            c.T.position = pos;
            if (vel.sqrMagnitude > 0.00001f) c.T.rotation = Quaternion.LookRotation(vel.normalized);
            float flapSpeed = bat ? 26f : 9f;
            float glide = bat ? 1f : Mathf.Clamp01(Mathf.Sin(t * 0.7f + c.Phase) * 2f + 0.5f); // crows glide between flaps
            float flap = Mathf.Sin(t * flapSpeed + c.Phase) * 40f * glide + 8f;
            c.WingL.localRotation = Quaternion.Euler(0, 0, flap);
            c.WingR.localRotation = Quaternion.Euler(0, 0, -flap);
            c.T.localScale = Vector3.one * (bat ? 0.6f : 1f);
            if (Factory.FlatDistance(c.Anchor, hero) > 14f) c.Anchor = Vector3.Lerp(c.Anchor, hero, dt * 0.2f); // drift along with the hero
        }

        void UpdateLeaf(Critter c, bool want, Vector3 hero, float t, float dt)
        {
            bool respawn = Recycle(c, want, hero) || (want && c.T.position.y < 0.02f && rng.NextDouble() < dt * 0.5);
            if (respawn) { c.Anchor = Near(hero, 0f, Radius); c.T.position = c.Anchor + Vector3.up * R(4f, 8f); }
            if (!want || c.T.position.y < 0.02f) return; // rest on the ground for a moment
            float k = t * 1.7f + c.Phase;
            c.T.position += new Vector3(Mathf.Sin(k) * 0.6f + 0.25f, -c.Speed, Mathf.Cos(k * 0.8f) * 0.4f) * dt;
            c.T.rotation = Quaternion.Euler(Mathf.Sin(k) * 50f, k * 60f, Mathf.Cos(k * 1.3f) * 40f);
            if (c.T.position.y < 0.02f) c.T.position = new Vector3(c.T.position.x, 0.02f, c.T.position.z);
        }
    }
}
