using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// How a hit feels: a freeze-frame on the big ones (<see cref="Stop"/>: crits, killing blows; a boss's death in slow
    /// motion), the struck body flashing white and rocking back from the blow (<see cref="HitFlash"/>), and the frost
    /// that creeps over a slowed monster (<see cref="FrostBite"/>).
    /// </summary>
    public static class HitFx
    {
        static Runner runner;
        static float stopUntil, stopScale = 1f;

        /// <summary>Freezes (or slows) the game for a moment of real time; never stacks up.</summary>
        public static void Stop(float seconds, float scale = 0.02f)
        {
            if (GameCheck.Requested) return; // the browser check measures the frame rate: no freezes there
            if (runner == null) runner = new GameObject("HitFx").AddComponent<Runner>();
            float until = Time.unscaledTime + Mathf.Min(seconds, 1.2f);
            // one at a time: the one that lasts longer wins (a crit's freeze must not stretch over a boss's slow death)
            if (stopScale < 1f && Time.unscaledTime < stopUntil && until <= stopUntil) return;
            stopUntil = until;
            stopScale = Mathf.Clamp(scale, 0.01f, 1f);
            Time.timeScale = stopScale;
        }

        class Runner : MonoBehaviour
        {
            void Update()
            {
                if (stopScale >= 1f || Time.unscaledTime < stopUntil) return;
                stopScale = 1f;
                Time.timeScale = 1f;
            }
        }
    }

    /// <summary>A white flash over a body when it's struck, and a jolt back along the blow (springs back at once).</summary>
    public class HitFlash : MonoBehaviour
    {
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor"), GltfEmissionId = Shader.PropertyToID("emissiveFactor");
        Renderer[] renderers;
        MaterialPropertyBlock block;
        Transform body;
        Vector3 home, push;
        float flash, jolt;

        public static HitFlash On(GameObject go, Transform body)
        {
            var f = go.GetComponent<HitFlash>();
            if (f == null) f = go.AddComponent<HitFlash>();
            if (f.body != body) { f.body = body; if (body != null) f.home = body.localPosition; }
            return f;
        }

        /// <summary>Struck from <paramref name="dir"/> (world, away from the attacker), <paramref name="strength"/> 0..1.</summary>
        public void Hit(Vector3 dir, float strength)
        {
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
            flash = Mathf.Clamp01(0.6f + strength * 0.4f);
            jolt = 1f;
            dir.y = 0f;
            push = (dir.sqrMagnitude > 0.0001f ? dir.normalized : -transform.forward) * Mathf.Lerp(0.12f, 0.35f, strength);
            Apply();
        }

        void Apply()
        {
            if (renderers == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            var c = new Color(1f, 0.95f, 0.85f) * (flash * 0.9f);
            foreach (var r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                if (flash <= 0f) { r.SetPropertyBlock(null); continue; }
                r.GetPropertyBlock(block);
                block.SetColor(EmissionId, c);
                block.SetColor(GltfEmissionId, c);
                r.SetPropertyBlock(block);
            }
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - dt / 0.11f);
                Apply();
            }
            if (body != null && jolt > 0f)
            {
                jolt = Mathf.Max(0f, jolt - dt / 0.18f);
                // out fast, back with a little overshoot
                float k = Mathf.Sin(jolt * Mathf.PI) * jolt;
                var local = body.parent != null ? body.parent.InverseTransformDirection(push) : push;
                body.localPosition = new Vector3(home.x + local.x * k, body.localPosition.y, home.z + local.z * k);
            }
        }
    }

    /// <summary>A slowed monster: rime on the ground around its feet and cold mist curling off it while the slow lasts.</summary>
    public class FrostBite : MonoBehaviour
    {
        Transform rime;
        float nextMist, radius = 0.6f;
        public bool Active;

        public static FrostBite On(GameObject go, float radius)
        {
            var f = go.GetComponent<FrostBite>();
            if (f == null) { f = go.AddComponent<FrostBite>(); f.radius = radius; }
            return f;
        }

        void Update()
        {
            if (!Active)
            {
                if (rime != null) { Destroy(rime.gameObject); rime = null; }
                return;
            }
            if (rime == null)
            {
                rime = new GameObject("Rime").transform;
                rime.SetParent(transform, false);
                var ice = new Color(0.7f, 0.88f, 1f);
                Factory.Prim(PrimitiveType.Cylinder, rime, new Vector3(0f, 0.01f, 0f), new Vector3(radius * 2.2f, 0.005f, radius * 2.2f), ice * 0.8f)
                    .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f + Random.Range(-0.3f, 0.3f);
                    var shard = Factory.Prim(PrimitiveType.Cube, rime, new Vector3(Mathf.Cos(a), 0.12f, Mathf.Sin(a)) * radius, new Vector3(0.07f, 0.28f, 0.07f), ice, false, Mat.Glow(ice * 0.6f));
                    shard.transform.localRotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 90f), Random.Range(-25f, 25f));
                }
                Sfx.Play("frost_cast", transform.position, 0.25f, 0.2f, 15f);
            }
            if (Time.time >= nextMist && SpellFx.Ready)
            {
                nextMist = Time.time + 0.4f;
                SpellFx.Emit(new SpellFx.P
                {
                    Burst = 3, Duration = 0.1f, Life = new Vector2(0.6f, 1f), Speed = new Vector2(0.1f, 0.3f), Size = new Vector2(0.15f, 0.3f),
                    Start = new Color(0.8f, 0.92f, 1f, 0.35f), End = new Color(0.8f, 0.92f, 1f, 0f), Velocity = Vector3.up * 0.4f, Smoke = true, Radius = radius * 0.6f,
                }, transform.position + Vector3.up * 0.5f);
            }
        }
    }
}
