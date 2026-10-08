using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Doors that open. The house models have their front door painted into the mesh, so each house gets a wooden
    /// leaf laid over it and a doorway behind: while a door stands open the doorway shows (dark by day, a warm lit
    /// room at night, with light spilling out onto the step) and the leaf swings out on its hinge, creaking.
    /// Townsfolk going home or setting off in the morning open them (see TownLife.Walker).
    /// </summary>
    public class HouseDoors : MonoBehaviour
    {
        /// <summary>Where the door is on each model, in the model's own (glTF) units: centre x, sill height, wall plane z
        /// (the door faces +z). Measured from the meshes; every door panel is 0.154 wide and 0.294 tall.</summary>
        static readonly Dictionary<string, Vector3> Spots = new Dictionary<string, Vector3>
        {
            { "building_home_A_green", new Vector3(0f, 0f, 0.322f) },
            { "building_home_A_blue", new Vector3(0f, 0f, 0.322f) },
            { "building_home_B_blue", new Vector3(0.14f, 0.21f, 0.217f) },
            { "building_tavern_blue", new Vector3(-0.14f, 0.21f, 0.287f) },
            { "building_church_blue", new Vector3(0f, 0f, 0.532f) },
            { "building_market_blue", new Vector3(0.14f, 0f, -0.013f) },
            { "building_blacksmith_blue", new Vector3(-0.35f, 0f, 0.077f) },
            { "building_windmill_blue", new Vector3(0f, 0f, 0.387f) },
        };
        const float PanelW = 0.154f, PanelH = 0.294f;
        static readonly Color Wood = new Color(0.6f, 0.36f, 0.27f), Gap = new Color(0.07f, 0.05f, 0.04f), Hearth = new Color(1f, 0.62f, 0.3f);

        public class Door
        {
            public Vector3 Centre;   // middle of the doorway, at the sill
            public Vector3 Out;      // the way the door faces (flat)
            public Vector3 Step;     // on the ground just outside
            public Transform Leaf, Hole;
            public Quaternion Shut;  // the leaf's rotation when closed
            public Light Spill;
            public float OpenUntil, Open;
        }

        static readonly List<Door> doors = new List<Door>();
        static HouseDoors runner;
        static Transform movers;

        /// <summary>
        /// Where the parts that move or change live: the town's decoration is static-batched once the world is built
        /// (a batched mesh never moves again), so doors and shutters are made on the house and then moved out here.
        /// </summary>
        public static Transform Movers
        {
            get
            {
                if (movers == null) movers = new GameObject("HouseMovers").transform;
                return movers;
            }
        }

        /// <summary>Gives a house (the object ArtLibrary.Spawn returned) a door that can open, if its model has one.</summary>
        public static void Add(GameObject house, string model)
        {
            if (house == null || house.transform.childCount == 0) return;
            string name = model.Substring(model.LastIndexOf('/') + 1);
            if (!Spots.TryGetValue(name, out var s)) return;
            var root = house.transform.GetChild(0); // the model itself (scaled); glTFast mirrors x into Unity's space
            var hinge = new Vector3(-s.x - PanelW * 0.5f, s.y, s.z + 0.004f);

            var d = new Door();
            d.Centre = root.TransformPoint(new Vector3(-s.x, s.y, s.z));
            d.Out = Factory.Flat(root.TransformDirection(Vector3.forward)).normalized;
            d.Step = new Vector3(d.Centre.x, 0f, d.Centre.z) + d.Out * 0.9f;

            var hole = Factory.Prim(PrimitiveType.Quad, root, new Vector3(-s.x, s.y + PanelH * 0.5f, s.z + 0.003f), new Vector3(PanelW, PanelH, 1f), Gap);
            hole.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // quads face -z; turn it to face out
            hole.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hole.SetActive(false);
            hole.transform.SetParent(Movers, true);
            d.Hole = hole.transform;

            var leaf = Factory.Empty("DoorHinge", root, hinge);
            var plank = Factory.Prim(PrimitiveType.Cube, leaf, new Vector3(PanelW * 0.5f, PanelH * 0.5f, 0.008f), new Vector3(PanelW, PanelH, 0.014f), Wood);
            // a couple of dark iron bands across it
            Factory.Prim(PrimitiveType.Cube, plank.transform, new Vector3(0f, 0.25f, 0.6f), new Vector3(0.96f, 0.05f, 0.4f), new Color(0.2f, 0.2f, 0.22f));
            Factory.Prim(PrimitiveType.Cube, plank.transform, new Vector3(0f, -0.25f, 0.6f), new Vector3(0.96f, 0.05f, 0.4f), new Color(0.2f, 0.2f, 0.22f));
            leaf.gameObject.SetActive(false);
            leaf.SetParent(Movers, true);
            d.Leaf = leaf;
            d.Shut = leaf.rotation;
            doors.Add(d);
        }

        /// <summary>The door nearest <paramref name="p"/> within <paramref name="max"/> metres (or null).</summary>
        public static Door Near(Vector3 p, float max)
        {
            Door best = null;
            float bd = max;
            foreach (var d in doors)
            {
                float dist = Factory.FlatDistance(d.Centre, p);
                if (dist < bd) { bd = dist; best = d; }
            }
            return best;
        }

        /// <summary>Swings the door open for a moment (someone going in or out); it shuts by itself.</summary>
        public static void Swing(Door d, float hold = 1.4f)
        {
            if (d == null) return;
            bool wasShut = d.Open <= 0.01f && Time.time >= d.OpenUntil;
            d.OpenUntil = Mathf.Max(d.OpenUntil, Time.time + hold);
            if (wasShut && HeroNear(d.Centre, 24f)) Sfx.Play("door_open", d.Centre + Vector3.up, 0.32f, 0.12f, 18f);
            if (runner == null)
            {
                runner = new GameObject("HouseDoors").AddComponent<HouseDoors>();
                DontDestroyOnLoad(runner.gameObject);
            }
            runner.enabled = true;
        }

        static bool HeroNear(Vector3 p, float r)
        {
            var hero = Player.I;
            return hero != null && Factory.FlatDistance(hero.transform.position, p) < r;
        }

        void Update()
        {
            bool any = false;
            foreach (var d in doors)
            {
                if (d.Leaf == null) continue;
                bool opening = Time.time < d.OpenUntil;
                if (!opening && d.Open <= 0f) continue;
                any = true;
                float was = d.Open;
                d.Open = Mathf.MoveTowards(d.Open, opening ? 1f : 0f, Time.deltaTime / (opening ? 0.45f : 0.6f));
                if (was > 0f && d.Open <= 0f && HeroNear(d.Centre, 24f)) Sfx.Play("door_close", d.Centre + Vector3.up, 0.35f, 0.12f, 18f);
                Pose(d);
            }
            if (!any) enabled = false;
        }

        static void Pose(Door d)
        {
            bool shown = d.Open > 0f;
            if (d.Leaf.gameObject.activeSelf != shown)
            {
                d.Leaf.gameObject.SetActive(shown);
                d.Hole.gameObject.SetActive(shown);
                if (shown)
                {
                    // A room lit by the hearth after dark, just shadow by day
                    bool night = DayNight.Night > 0.4f;
                    d.Hole.GetComponent<Renderer>().sharedMaterial = night ? Mat.Glow(Hearth * 0.8f) : Mat.Get(Gap);
                    if (night && d.Spill == null)
                    {
                        d.Spill = new GameObject("DoorLight").AddComponent<Light>();
                        d.Spill.transform.position = d.Centre + Vector3.up * 1.1f + d.Out * 0.6f;
                        d.Spill.type = LightType.Point;
                        d.Spill.color = Hearth;
                        d.Spill.range = 5f;
                    }
                }
            }
            // Out on the hinge with a little overshoot, then back
            float swing = Mathf.SmoothStep(0f, 1f, d.Open) * 105f;
            d.Leaf.rotation = d.Shut * Quaternion.Euler(0f, -swing, 0f);
            if (d.Spill != null)
            {
                d.Spill.intensity = d.Open * 1.6f;
                if (d.Open <= 0f) { Destroy(d.Spill.gameObject); d.Spill = null; }
            }
        }

        /// <summary>A new world (another region or a reload): the old houses are gone.</summary>
        public static void Clear()
        {
            doors.Clear();
            if (movers != null) Destroy(movers.gameObject);
            movers = null;
        }
    }
}
