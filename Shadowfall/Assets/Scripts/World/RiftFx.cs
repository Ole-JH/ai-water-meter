using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The look of a greater rift (<see cref="Rift"/>), on top of the dungeon it's built from (the light and fog in its
    /// tier's colour are DayNight's): an orb of the rift's energy that floats by the hero and fills as the bar does, a
    /// mote of essence flying into it from every kill, the Rift Guardian stepping out of a tear in the air, and, once the
    /// rift is cleared, its collapse: rocks raining down, the ground shaking harder, the dark closing in, until the server
    /// throws everyone back to the Rift Stone.
    /// </summary>
    public class RiftFx : MonoBehaviour
    {
        static RiftFx I;

        Transform orb, core;
        Light orbLight;
        float shown, nextRumble, nextRock;
        readonly List<Transform> motes = new List<Transform>();

        /// <summary>Makes sure the effects run while we're in a rift.</summary>
        public static void Ensure()
        {
            if (I != null || !Rift.Inside) return;
            I = new GameObject("RiftFx").AddComponent<RiftFx>();
            I.Build();
        }

        public static void Clear()
        {
            if (I != null) Destroy(I.gameObject);
            I = null;
        }

        void Build()
        {
            var c = Rift.TierColor(Rift.Tier);
            orb = new GameObject("RiftOrb").transform;
            orb.SetParent(transform, false);
            // a dark shell with the energy glowing inside
            var shell = Factory.Prim(PrimitiveType.Sphere, orb, Vector3.zero, Vector3.one * 0.55f, new Color(0.08f, 0.05f, 0.1f));
            shell.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            core = Factory.Prim(PrimitiveType.Sphere, orb, Vector3.zero, Vector3.one * 0.1f, c, false, Mat.Glow(c)).transform;
            orbLight = new GameObject("OrbLight").AddComponent<Light>();
            orbLight.transform.SetParent(orb, false);
            orbLight.type = LightType.Point;
            orbLight.color = c;
            orbLight.range = 4f;
            orbLight.intensity = 0.6f;
            orbLight.shadows = LightShadows.None;
        }

        /// <summary>A kill: its essence flies from <paramref name="from"/> into the orb (more motes for an elite).</summary>
        public static void Mote(Vector3 from, int n)
        {
            Ensure();
            if (I == null || I.orb == null) return;
            for (int i = 0; i < n; i++) I.StartCoroutine(I.Fly(from + Random.insideUnitSphere * 0.3f, i * 0.08f));
        }

        IEnumerator Fly(Vector3 from, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var c = Rift.TierColor(Rift.Tier);
            var m = Factory.Prim(PrimitiveType.Sphere, transform, from, Vector3.one * 0.14f, c, false, Mat.Glow(c)).transform;
            var bend = Random.insideUnitSphere * 2f + Vector3.up * 1.5f;
            for (float t = 0f; t < 1f && orb != null; t += Time.deltaTime / 0.7f)
            {
                var to = orb.position;
                var mid = (from + to) * 0.5f + bend;
                // a curve, quicker at the end
                float e = t * t;
                m.position = Vector3.Lerp(Vector3.Lerp(from, mid, e), Vector3.Lerp(mid, to, e), e);
                if (Random.value < 0.4f) FxPulse.Spawn(m.position, c, Vector3.one * 0.07f, Vector3.zero, 0.3f, PrimitiveType.Sphere);
                yield return null;
            }
            Destroy(m.gameObject);
            if (core != null) { SpellFx.Hit(orb.position, c, false, 5); shown = Mathf.Min(1f, shown + 0.02f); }
            Sfx.Play("zap", orb != null ? orb.position : from, 0.15f, 0.3f, 15f);
        }

        void Update()
        {
            if (!Rift.Inside) { Clear(); return; }
            var p = Player.I;
            if (p == null) return;
            var c = Rift.TierColor(Rift.Tier);

            // the orb floats by the hero's shoulder
            var want = p.transform.position + Vector3.up * 2.9f - p.transform.right * 0.9f;
            orb.position = Vector3.Lerp(orb.position == Vector3.zero ? want : orb.position, want, Time.deltaTime * 4f) + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.003f;
            float target = Rift.Progress / 100f;
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * 0.6f);
            if (shown > target + 0.05f) shown = target;
            float full = Mathf.Clamp01(shown), pulse = Rift.Pulse;
            core.localScale = Vector3.one * Mathf.Lerp(0.08f, 0.5f, full) * (1f + pulse * 0.08f);
            orbLight.intensity = 0.4f + full * 1.4f + pulse * 0.3f;
            orbLight.range = 3f + full * 4f;
            orb.Rotate(0f, 60f * Time.deltaTime, 0f);
            // full: it bursts when the guardian comes, and is spent once the rift is cleared
            bool spent = Rift.Phase == "won" || Rift.Phase == "late";
            orb.gameObject.SetActive(!spent);

            if (Rift.CollapseIn > 0f) Collapse(p, c);
        }

        /// <summary>The cleared rift caving in: rocks fall, the ground rumbles, harder the closer the end.</summary>
        void Collapse(Player p, Color c)
        {
            float left = Rift.CollapseIn, near = 1f - Mathf.Clamp01(left / 30f);
            if (Time.time >= nextRumble)
            {
                nextRumble = Time.time + Mathf.Lerp(3f, 0.9f, near);
                CameraRig.Shake(Mathf.Lerp(0.08f, 0.3f, near));
                Sfx.Play("rubble", p.transform.position + Random.insideUnitSphere * 6f, Mathf.Lerp(0.35f, 0.8f, near), 0.15f, 30f);
            }
            if (Time.time >= nextRock)
            {
                nextRock = Time.time + Mathf.Lerp(0.5f, 0.12f, near);
                var at = p.transform.position + new Vector3(Random.Range(-9f, 9f), 0f, Random.Range(-9f, 9f));
                if (WorldGrid.Instance != null && !WorldGrid.Instance.IsWalkable(at)) return;
                var rock = Factory.Prim(PrimitiveType.Cube, null, at + Vector3.up * Random.Range(5f, 8f), Vector3.one * Random.Range(0.15f, 0.4f), new Color(0.3f, 0.27f, 0.3f));
                rock.transform.rotation = Random.rotation;
                var fall = rock.AddComponent<FallingPiece>();
                fall.Velocity = Vector3.down * 2f;
                fall.Spin = Random.insideUnitSphere * 200f;
                rock.AddComponent<FadeAway>().Seconds = 4f;
                if (Random.value < 0.4f) SpellFx.Ring(at + Vector3.up * 0.05f, c, 0.6f, 0.5f);
            }
        }

        // ------------------------------------------------------------------ the guardian

        /// <summary>The Rift Guardian steps out of a tear in the air.</summary>
        public static void GuardianArrives(Enemy e, Transform model)
        {
            Ensure();
            var c = Rift.TierColor(Rift.Tier);
            var tear = new GameObject("RiftTear").AddComponent<RiftTear>();
            tear.transform.position = e.transform.position + Vector3.up * e.Height * 0.55f;
            var p = Player.I;
            if (p != null) tear.transform.rotation = Quaternion.LookRotation(Factory.Flat(p.transform.position - e.transform.position).normalized + Vector3.forward * 0.001f);
            tear.Open(c, e.Height, model);
            // the orb bursts: its energy tore the rift open
            if (I != null && I.orb != null)
            {
                SpellFx.Explosion(I.orb.position, c, 1.2f, false);
                I.shown = 0f;
            }
            Sfx.Play("blink", e.transform.position, 1f, 0.05f, 60f);
            Sfx.Play("roar", e.transform.position, 0.9f, 0.05f, 60f);
            if (p != null && Factory.FlatDistance(p.transform.position, e.transform.position) < 25f) CameraRig.Shake(0.3f);
        }
    }

    /// <summary>A rip in the air, edged in the rift's colour: opens, lets the guardian through (it grows out of it), closes.</summary>
    public class RiftTear : MonoBehaviour
    {
        Transform rim, hole, guardian;
        float t, height;
        Vector3 guardianScale;

        public void Open(Color c, float h, Transform model)
        {
            height = h;
            rim = Factory.Prim(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.zero, c, false, Mat.Glow(c)).transform;
            hole = Factory.Prim(PrimitiveType.Sphere, transform, new Vector3(0f, 0f, -0.02f), Vector3.zero, new Color(0.02f, 0f, 0.04f)).transform;
            guardian = model;
            if (guardian != null) { guardianScale = guardian.localScale; guardian.localScale = guardianScale * 0.05f; }
            SpellFx.Emit(new SpellFx.P
            {
                Rate = 50, Duration = 2.2f, Life = new Vector2(0.4f, 0.9f), Speed = new Vector2(1f, 3f), Size = new Vector2(0.06f, 0.14f),
                Start = c, End = new Color(c.r, c.g, c.b, 0f), Shape = ParticleSystemShapeType.Circle, Radius = h * 0.4f,
            }, transform.position);
        }

        void Update()
        {
            t += Time.deltaTime;
            // open (0.5 s), hold while it steps through (1.4 s), close (0.6 s)
            float open = t < 0.5f ? t / 0.5f : t < 1.9f ? 1f : Mathf.Clamp01(1f - (t - 1.9f) / 0.6f);
            float w = height * 0.55f * open, h = height * 1.1f * Mathf.Sqrt(open);
            rim.localScale = new Vector3(w * 1.15f, h * 1.08f, 0.06f);
            hole.localScale = new Vector3(w, h, 0.08f);
            transform.Rotate(0f, 0f, 25f * Time.deltaTime);
            if (guardian != null)
            {
                float g = Mathf.Clamp01((t - 0.3f) / 1.3f);
                guardian.localScale = guardianScale * Mathf.Lerp(0.05f, 1f, Mathf.SmoothStep(0f, 1f, g));
                if (g >= 1f) guardian = null;
            }
            if (t > 2.6f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (guardian != null) guardian.localScale = guardianScale;
        }
    }
}
