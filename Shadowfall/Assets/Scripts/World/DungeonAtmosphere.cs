using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The air down there: dust drifting through the torchlight (snow in the Barrow, sand in the Temple, embers in the
    /// Citadel), water dripping from the ceiling into puddles, cobwebs across the room corners, old bones on the floor,
    /// and each dungeon's own touch: frost on the Barrow floor, sand drifts in the Temple, glowing cracks in the Citadel.
    /// Visual only, with its own random numbers (the layout's aren't touched).
    /// </summary>
    public class DungeonAtmosphere : MonoBehaviour
    {
        string id;
        float nextAir;
        readonly List<Vector3> drips = new List<Vector3>();
        readonly List<float> dripAt = new List<float>();

        public static void Build(Transform root, bool[] blocked, int w, int h, List<RectInt> rooms, int seed, string id)
        {
            var a = root.gameObject.AddComponent<DungeonAtmosphere>();
            a.id = id;
            var rng = new System.Random(seed ^ 0x5bd1e995);
            float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            bool Free(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && !blocked[y * w + x];

            bool dusty = id == "catacombs" || id == "hideout" || id == "warrens" || id == "temple";
            bool detail = GameSettings.Details.Value > 0; // small things (webs, bones) only with Details on
            int webs = 0;
            foreach (var r in rooms)
            {
                // cobwebs strung across the corners where two walls meet
                if (dusty && detail)
                    foreach (var c in new[] { new Vector2Int(r.x, r.y), new Vector2Int(r.xMax - 1, r.y), new Vector2Int(r.x, r.yMax - 1), new Vector2Int(r.xMax - 1, r.yMax - 1) })
                    {
                        if (rng.NextDouble() > 0.45 || !Free(c.x, c.y) || webs >= 8) continue;
                        // which way the two walls are (needs one on an x side and one on a z side)
                        int wx = !Free(c.x - 1, c.y) ? -1 : !Free(c.x + 1, c.y) ? 1 : 0, wy = !Free(c.x, c.y - 1) ? -1 : !Free(c.x, c.y + 1) ? 1 : 0;
                        if (wx == 0 || wy == 0) continue;
                        var corner = Dungeon.ToWorld(c.x + (wx > 0 ? 1f : 0f), c.y + (wy > 0 ? 1f : 0f));
                        Cobweb(root, corner, new Vector3(-wx, 0f, 0f), new Vector3(0f, 0f, -wy), R(0.6f, 1.1f));
                        webs++;
                    }
                // bones lying about
                int bones = id == "catacombs" ? rng.Next(2, 5) : id == "warrens" || id == "barrow" ? rng.Next(0, 3) : rng.Next(0, 2);
                for (int i = 0; i < (detail ? bones : 0); i++)
                {
                    int x = r.x + rng.Next(1, Mathf.Max(2, r.width - 1)), y = r.y + rng.Next(1, Mathf.Max(2, r.height - 1));
                    if (Free(x, y)) Bones(root, Dungeon.ToWorld(x + R(0.2f, 0.8f), y + R(0.2f, 0.8f)), rng);
                }
                // each dungeon's own floor
                int n = rng.Next(1, 4);
                for (int i = 0; i < n; i++)
                {
                    int x = r.x + rng.Next(0, r.width), y = r.y + rng.Next(0, r.height);
                    if (!Free(x, y)) continue;
                    var at = Dungeon.ToWorld(x + R(0.2f, 0.8f), y + R(0.2f, 0.8f));
                    switch (id)
                    {
                        case "barrow": Flat(root, at, R(0.8f, 1.6f), new Color(0.82f, 0.9f, 1f), true); break;          // frost
                        case "temple": SandDrift(root, at, R(0.7f, 1.3f), R(0f, 360f)); break;
                        case "citadel": Crack(root, at, R(0.8f, 1.8f), R(0f, 180f), rng); break;
                        case "mine": case "hideout": case "warrens": Flat(root, at, R(0.4f, 0.9f), new Color(0.06f, 0.07f, 0.08f), false); break; // puddles
                    }
                }
            }
            // drips: from the ceiling over open floor (not in the burning Citadel or dry Temple)
            if (id != "citadel" && id != "temple")
                for (int i = 0; i < 40 && a.drips.Count < 10; i++)
                {
                    int x = rng.Next(1, w - 1), y = rng.Next(1, h - 1);
                    if (!Free(x, y)) continue;
                    var at = Dungeon.ToWorld(x + 0.5f, y + 0.5f);
                    a.drips.Add(at);
                    a.dripAt.Add(Time.time + R(0f, 4f));
                    Flat(root, at, 0.35f, new Color(0.05f, 0.06f, 0.07f), false);
                    if (id == "barrow") // an icicle it drips from
                    {
                        var ice = new Color(0.75f, 0.9f, 1f);
                        var cone = Factory.Prim(PrimitiveType.Cylinder, root, at + Vector3.up * 2.15f, new Vector3(0.06f, 0.22f, 0.06f), ice, false, Mat.Glow(ice * 0.4f));
                        cone.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
        }

        static void NoShadow(GameObject go) => go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        /// <summary>A web in a corner: threads fanning out from it at the top of the walls and rings across them.</summary>
        static void Cobweb(Transform root, Vector3 corner, Vector3 alongA, Vector3 alongB, float size)
        {
            var silk = new Color(0.85f, 0.85f, 0.82f);
            var top = corner + Vector3.up * 2.35f;
            for (int i = 0; i <= 4; i++)
            {
                // spokes, drooping down from the corner
                var dir = Vector3.Slerp(alongA, alongB, i / 4f).normalized + Vector3.down * 0.55f;
                var mid = top + dir * size * 0.5f;
                var s = Factory.Prim(PrimitiveType.Cube, root, mid, new Vector3(0.01f, 0.01f, size), silk);
                s.transform.rotation = Quaternion.LookRotation(dir);
                NoShadow(s);
            }
            for (int ring = 1; ring <= 2; ring++)
            {
                float d = size * ring / 2.6f;
                for (int i = 0; i < 4; i++)
                {
                    var p0 = top + (Vector3.Slerp(alongA, alongB, i / 4f).normalized + Vector3.down * 0.55f) * d;
                    var p1 = top + (Vector3.Slerp(alongA, alongB, (i + 1) / 4f).normalized + Vector3.down * 0.55f) * d;
                    var s = Factory.Prim(PrimitiveType.Cube, root, (p0 + p1) / 2f + Vector3.down * 0.02f, new Vector3(0.008f, 0.008f, Vector3.Distance(p0, p1)), silk);
                    s.transform.rotation = Quaternion.LookRotation(p1 - p0);
                    NoShadow(s);
                }
            }
        }

        static void Bones(Transform root, Vector3 at, System.Random rng)
        {
            var bone = new Color(0.86f, 0.82f, 0.7f);
            int n = rng.Next(2, 5);
            for (int i = 0; i < n; i++)
            {
                var b = Factory.Prim(PrimitiveType.Cylinder, root, at + new Vector3((float)rng.NextDouble() - 0.5f, 0.03f, (float)rng.NextDouble() - 0.5f) * 0.6f,
                    new Vector3(0.05f, 0.2f + (float)rng.NextDouble() * 0.12f, 0.05f), bone);
                b.transform.rotation = Quaternion.Euler(90f, (float)rng.NextDouble() * 360f, 0f);
            }
            if (rng.NextDouble() < 0.5) // and a skull
                Factory.Prim(PrimitiveType.Sphere, root, at + new Vector3(0.15f, 0.1f, -0.1f), new Vector3(0.2f, 0.18f, 0.24f), bone)
                    .transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 20f);
        }

        /// <summary>A thin disc on the floor: a puddle (dark and glossy-looking) or frost (pale, faintly glowing).</summary>
        static void Flat(Transform root, Vector3 at, float size, Color c, bool glow)
        {
            var d = Factory.Prim(PrimitiveType.Cylinder, root, at + Vector3.up * 0.012f, new Vector3(size, 0.004f, size * 0.8f), c, false, glow ? Mat.Glow(c * 0.25f) : null);
            d.transform.rotation = Quaternion.Euler(0f, at.x * 37f % 360f, 0f);
            NoShadow(d);
        }

        static void SandDrift(Transform root, Vector3 at, float size, float yaw)
        {
            var sand = new Color(0.82f, 0.68f, 0.45f);
            var d = Factory.Prim(PrimitiveType.Sphere, root, at, new Vector3(size * 1.6f, 0.22f, size), sand);
            d.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            NoShadow(d);
        }

        /// <summary>A crack in the Citadel floor with fire showing through.</summary>
        static void Crack(Transform root, Vector3 at, float len, float yaw, System.Random rng)
        {
            var fire = new Color(1f, 0.4f, 0.08f);
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var p = at - dir * len / 2f;
            for (int i = 0; i < 3; i++)
            {
                var turn = Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 60f, 0f) * dir;
                float l = len / 3f;
                var s = Factory.Prim(PrimitiveType.Cube, root, p + turn * l / 2f + Vector3.up * 0.01f, new Vector3(0.05f, 0.01f, l), fire, false, Mat.Glow(fire));
                s.transform.rotation = Quaternion.LookRotation(turn);
                NoShadow(s);
                p += turn * l;
            }
        }

        void Update()
        {
            var p = Player.I;
            if (p == null || !SpellFx.Ready) return;
            var me = p.transform.position;
            if (Time.time >= nextAir)
            {
                nextAir = Time.time + 0.45f;
                var at = me + new Vector3(Random.Range(-6f, 6f), Random.Range(0.6f, 2.2f), Random.Range(-6f, 6f));
                switch (id)
                {
                    case "barrow": // snow sifting down from the ice
                        SpellFx.Emit(new SpellFx.P { Burst = 3, Duration = 0.1f, Life = new Vector2(3f, 5f), Speed = new Vector2(0f, 0.05f), Size = new Vector2(0.03f, 0.06f),
                            Start = new Color(0.9f, 0.95f, 1f, 0.8f), End = new Color(0.9f, 0.95f, 1f, 0f), Velocity = Vector3.down * 0.35f, Radius = 2f, Max = 10 }, at + Vector3.up);
                        break;
                    case "citadel": // embers rising, and ash
                        SpellFx.Emit(new SpellFx.P { Burst = 2, Duration = 0.1f, Life = new Vector2(2f, 3.5f), Speed = new Vector2(0.05f, 0.2f), Size = new Vector2(0.03f, 0.07f),
                            Start = new Color(1f, 0.6f, 0.2f), End = new Color(1f, 0.25f, 0.05f, 0f), Velocity = Vector3.up * 0.5f, Radius = 2f, Max = 10 }, new Vector3(at.x, me.y + 0.2f, at.z));
                        break;
                    case "temple": // sand trickling from the cracks above
                        SpellFx.Emit(new SpellFx.P { Burst = 6, Duration = 0.1f, Life = new Vector2(0.8f, 1.2f), Speed = new Vector2(0f, 0.1f), Size = new Vector2(0.02f, 0.04f),
                            Start = new Color(0.85f, 0.72f, 0.5f), End = new Color(0.85f, 0.72f, 0.5f, 0f), Gravity = 0.5f, Radius = 0.08f, Max = 12 }, new Vector3(at.x, me.y + 2.3f, at.z));
                        goto default;
                    default: // dust motes turning slowly in the torchlight
                        SpellFx.Emit(new SpellFx.P { Burst = 3, Duration = 0.1f, Life = new Vector2(4f, 6f), Speed = new Vector2(0.02f, 0.08f), Size = new Vector2(0.025f, 0.05f),
                            Start = new Color(1f, 0.9f, 0.7f, 0.35f), Mid = new Color(1f, 0.9f, 0.7f, 0.5f), End = new Color(1f, 0.9f, 0.7f, 0f), Radius = 2.5f, Max = 10 }, at);
                        break;
                }
            }
            // drips, each in its own time: a drop falls and the puddle splashes (a plink if you're close)
            for (int i = 0; i < drips.Count; i++)
            {
                if (Time.time < dripAt[i]) continue;
                dripAt[i] = Time.time + Random.Range(2.5f, 5f);
                var d = drips[i];
                if (Factory.FlatDistance(d, me) > 22f) continue;
                SpellFx.Emit(new SpellFx.P { Burst = 1, Duration = 0.05f, Life = new Vector2(0.5f, 0.5f), Speed = Vector2.zero, Size = new Vector2(0.05f, 0.05f),
                    Start = new Color(0.7f, 0.85f, 1f, 0.9f), End = new Color(0.7f, 0.85f, 1f, 0.6f), Gravity = 1.9f, Radius = 0.001f, Stretch = true, Max = 2 }, d + Vector3.up * 2.3f);
                StartCoroutine(Splash(d));
            }
        }

        System.Collections.IEnumerator Splash(Vector3 at)
        {
            yield return new WaitForSeconds(0.48f);
            SpellFx.Emit(new SpellFx.P { Burst = 5, Duration = 0.05f, Life = new Vector2(0.15f, 0.3f), Speed = new Vector2(0.6f, 1.2f), Size = new Vector2(0.02f, 0.04f),
                Start = new Color(0.75f, 0.88f, 1f, 0.8f), End = new Color(0.75f, 0.88f, 1f, 0f), Gravity = 1f, Shape = ParticleSystemShapeType.Hemisphere, Radius = 0.05f, Max = 6 }, at + Vector3.up * 0.03f);
            var p = Player.I;
            if (p != null && Factory.FlatDistance(at, p.transform.position) < 8f) Sfx.Play("splash", at, 0.08f, 0.3f, 10f);
        }
    }
}
