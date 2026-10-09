using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// More life in the wilds near the hero (Ambience has the fireflies, flying crows, bats and leaves): fish leaping out
    /// of the lakes with a splash, little waves lapping at the shore, and crows pecking on the ground by day that burst
    /// up and away when the hero comes close.
    /// </summary>
    public class NatureLife : MonoBehaviour
    {
        class Crow { public Transform T, WingL, WingR; public Vector3 Fly; public float Peck, FlyT = -1f; }

        readonly List<Crow> flock = new List<Crow>();
        float nextFish, nextLap, nextFlock;

        public static void Ensure()
        {
            if (FindObjectOfType<NatureLife>() == null) new GameObject("NatureLife").AddComponent<NatureLife>();
        }

        void Update()
        {
            var p = Player.I;
            if (p == null || Dungeon.Active) { Clear(); return; }
            var hero = p.transform.position;
            Lakes(hero);
            Crows(p, hero);
        }

        // ------------------------------------------------------------------ fish and waves

        void Lakes(Vector3 hero)
        {
            if (Time.time < nextFish && Time.time < nextLap) return;
            foreach (var l in WorldGenerator.Lakes)
            {
                var c = new Vector3(l.x, 0f, l.z);
                float d = Factory.FlatDistance(hero, c);
                if (d > l.y + 30f) continue;
                bool frozen = Weather.Season == Season.Winter && Weather.ColdAt(c);
                if (frozen) continue;
                if (Time.time >= nextFish)
                {
                    nextFish = Time.time + Random.Range(3f, 8f);
                    float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(0.2f, 0.65f) * l.y;
                    var at = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    var fish = Factory.Prim(PrimitiveType.Capsule, null, at, new Vector3(0.12f, 0.2f, 0.12f), new Color(0.7f, 0.75f, 0.8f));
                    fish.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    fish.AddComponent<LeapingFish>().Init(at, Random.Range(0f, 360f));
                }
                if (Time.time >= nextLap && d < l.y + 8f)
                {
                    // a small wave at the shore nearest us
                    nextLap = Time.time + Random.Range(1.2f, 2.5f);
                    var dir = Factory.Flat(hero - c).normalized;
                    var at = c + Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * dir * (l.y - 0.6f);
                    SpellFx.Ring(at + Vector3.up * 0.03f, new Color(0.75f, 0.85f, 0.9f), Random.Range(0.4f, 0.8f), 0.9f);
                }
                return;
            }
        }

        // ------------------------------------------------------------------ crows on the ground

        void Crows(Player p, Vector3 hero)
        {
            bool day = DayNight.Night < 0.4f, wild = !WorldGenerator.InTown(hero);
            if (flock.Count == 0)
            {
                // crows out in the wilds; in town, grey pigeons on the cobbles
                if (!day || Time.time < nextFlock || Weather.Precip > 0.4f) return;
                flockInTown = !wild;
                nextFlock = Time.time + Random.Range(25f, 50f);
                // somewhere grassy a little ahead
                var ahead = hero + p.transform.forward * Random.Range(9f, 14f) + p.transform.right * Random.Range(-5f, 5f);
                var grid = WorldGrid.Instance;
                if (!grid.IsWalkable(ahead)) return;
                int n = Random.Range(3, 6);
                for (int i = 0; i < n; i++)
                {
                    var at = ahead + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
                    if (grid.IsWalkable(at)) flock.Add(MakeCrow(at, flockInTown));
                }
                return;
            }
            float dt = Time.deltaTime;
            bool spooked = false;
            foreach (var c in flock)
            {
                if (c.FlyT >= 0f || c.T == null) continue;
                if (Factory.FlatDistance(c.T.position, hero) < (p.IsMoving ? 5.5f : 2.5f)) spooked = true;
                // other heroes and monsters coming by put them up too
                else if (Time.frameCount % 10 == 0) spooked |= Disturbed(c.T.position);
            }
            int gone = 0;
            foreach (var c in flock)
            {
                if (c.T == null) { gone++; continue; }
                if (spooked && c.FlyT < 0f)
                {
                    c.FlyT = 0f;
                    var away = Factory.Flat(c.T.position - hero).normalized;
                    c.Fly = (away + Random.insideUnitSphere * 0.4f).normalized * Random.Range(7f, 10f) + Vector3.up * Random.Range(4f, 6f);
                    c.T.rotation = Quaternion.LookRotation(Factory.Flat(c.Fly));
                    if (Random.value < 0.5f) Sfx.Play("leaves", c.T.position, 0.35f, 0.3f, 25f);
                }
                if (c.FlyT >= 0f)
                {
                    c.FlyT += dt;
                    c.T.position += c.Fly * dt;
                    c.Fly += Vector3.up * dt * 1.5f;
                    float flap = Mathf.Sin(c.FlyT * 30f) * 55f;
                    c.WingL.localRotation = Quaternion.Euler(0f, 0f, flap);
                    c.WingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
                    if (c.FlyT > 5f) { Destroy(c.T.gameObject); c.T = null; }
                    continue;
                }
                // pecking: bob down now and then, turn a little
                c.Peck -= dt;
                if (c.Peck < 0f) { c.Peck = Random.Range(0.6f, 2f); c.T.Rotate(0f, Random.Range(-60f, 60f), 0f); }
                c.T.localRotation = Quaternion.Euler(c.Peck < 0.25f ? 35f : 0f, c.T.localEulerAngles.y, 0f);
            }
            if (gone == flock.Count || (wild == flockInTown && !spooked)) Clear(); // (walked into town, or out of it)
            else if (Factory.FlatDistance(flock[0].T != null ? flock[0].T.position : hero, hero) > 45f) Clear();
        }

        bool flockInTown;

        /// <summary>Anyone else moving close by: another hero, or a monster.</summary>
        static bool Disturbed(Vector3 at)
        {
            foreach (var rp in RemotePlayer.ById.Values)
                if (rp != null && Factory.FlatDistance(rp.transform.position, at) < 4f) return true;
            foreach (var e in Enemy.ById.Values)
                if (e != null && !e.IsDead && Factory.FlatDistance(e.transform.position, at) < 4.5f) return true;
            return false;
        }

        Crow MakeCrow(Vector3 at, bool pigeon = false)
        {
            var black = pigeon ? new Color(0.5f, 0.52f, 0.58f) : new Color(0.07f, 0.07f, 0.09f);
            var root = new GameObject("Crow").transform;
            root.position = at;
            root.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Factory.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.14f, 0f), new Vector3(0.14f, 0.13f, 0.3f), black);
            Factory.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.22f, 0.15f), new Vector3(0.1f, 0.1f, 0.11f), black);
            Factory.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.21f, 0.23f), new Vector3(0.03f, 0.03f, 0.07f), new Color(0.25f, 0.22f, 0.15f)); // beak
            var c = new Crow { T = root, Peck = Random.Range(0f, 2f) };
            c.WingL = new GameObject("WingL").transform; c.WingL.SetParent(root, false); c.WingL.localPosition = new Vector3(-0.07f, 0.17f, 0f);
            c.WingR = new GameObject("WingR").transform; c.WingR.SetParent(root, false); c.WingR.localPosition = new Vector3(0.07f, 0.17f, 0f);
            Factory.Prim(PrimitiveType.Cube, c.WingL, new Vector3(-0.12f, 0f, 0f), new Vector3(0.24f, 0.02f, 0.18f), black);
            Factory.Prim(PrimitiveType.Cube, c.WingR, new Vector3(0.12f, 0f, 0f), new Vector3(0.24f, 0.02f, 0.18f), black);
            c.WingL.localRotation = Quaternion.Euler(0f, 0f, -70f); // folded
            c.WingR.localRotation = Quaternion.Euler(0f, 0f, 70f);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return c;
        }

        void Clear()
        {
            foreach (var c in flock) if (c.T != null) Destroy(c.T.gameObject);
            flock.Clear();
        }
    }

    /// <summary>A fish leaping out of the water and back in, with a splash each way.</summary>
    public class LeapingFish : MonoBehaviour
    {
        Vector3 from;
        float t, yaw;

        public void Init(Vector3 at, float heading)
        {
            from = at;
            yaw = heading;
            Splash(at, 0.35f);
        }

        static void Splash(Vector3 at, float size)
        {
            SpellFx.Ring(at + Vector3.up * 0.03f, new Color(0.85f, 0.92f, 1f), size, 0.6f);
            if (SpellFx.Ready)
                SpellFx.Emit(new SpellFx.P { Burst = 8, Duration = 0.1f, Life = new Vector2(0.3f, 0.6f), Speed = new Vector2(1f, 2f), Size = new Vector2(0.04f, 0.08f),
                    Start = new Color(0.85f, 0.92f, 1f, 0.9f), End = new Color(0.85f, 0.92f, 1f, 0f), Gravity = 1.5f, Velocity = Vector3.up * 1.5f, Radius = 0.1f }, at);
            Sfx.Play("splash", at, 0.3f, 0.2f, 25f);
        }

        void Update()
        {
            t += Time.deltaTime / 0.8f;
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var p = from + dir * 1.2f * t + Vector3.up * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.9f;
            transform.position = p;
            // nose up on the way out, down on the way back in
            transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f - 180f * Mathf.Clamp01(t), 0f, 0f);
            if (t < 1f) return;
            Splash(from + dir * 1.2f, 0.45f);
            Destroy(gameObject);
        }
    }
}
