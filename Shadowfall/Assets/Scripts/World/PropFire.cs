using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// A living fire on a prop (campfire, torch, brazier): looping flame tongues, rising embers and a thin smoke
    /// plume, a glow core that wobbles, and a light that flickers on top of whatever else drives it (day/night).
    /// Emission pauses when the hero is far away so dozens of torches cost nothing off screen.
    /// </summary>
    public class PropFire : MonoBehaviour
    {
        const float ActiveRange = 45f;

        ParticleSystem[] systems;
        Light fireLight;
        Transform core;
        Vector3 coreScale;
        float seed, nextCheck, lastBase;
        bool active = true;

        /// <param name="size">1 = a campfire, 0.3 = a wall torch.</param>
        /// <param name="glowCore">An existing glowing mesh to wobble (optional).</param>
        public static PropFire Add(Transform parent, Vector3 pos, Color color, float size, bool smoke = true, Light light = null, Transform glowCore = null)
        {
            var go = new GameObject("Fire");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var f = go.AddComponent<PropFire>();
            f.seed = Random.value * 100f;
            f.fireLight = light;
            f.core = glowCore;
            if (glowCore != null) f.coreScale = glowCore.localScale;
            if (!SpellFx.Ready) { f.systems = new ParticleSystem[0]; return f; }

            var hot = Color.Lerp(color, Color.white, 0.55f);
            var flames = SpellFx.Emit(new SpellFx.P
            {
                Rate = 26f * Mathf.Sqrt(size), Life = new Vector2(0.35f, 0.7f), Speed = new Vector2(0.05f, 0.25f),
                Size = new Vector2(0.35f, 0.6f) * size, Start = hot, Mid = color, End = new Color(color.r * 0.6f, color.g * 0.2f, color.b * 0.1f, 0f),
                Radius = 0.18f * size, Velocity = new Vector3(0f, 1.3f * Mathf.Sqrt(size), 0f), Max = 60,
            }, pos, go.transform);
            var embers = SpellFx.Emit(new SpellFx.P
            {
                Rate = 5f * Mathf.Sqrt(size), Life = new Vector2(0.9f, 1.8f), Speed = new Vector2(0.2f, 0.6f),
                Size = new Vector2(0.03f, 0.07f) * Mathf.Max(0.6f, size), Start = new Color(1f, 0.9f, 0.6f), Mid = color, End = new Color(color.r, color.g * 0.3f, 0f, 0f),
                Radius = 0.2f * size, Velocity = new Vector3(0f, 1.6f * Mathf.Sqrt(size), 0f), Stretch = true, Max = 30,
            }, pos + Vector3.up * 0.1f * size, go.transform);
            ParticleSystem plume = null;
            if (smoke)
                plume = SpellFx.Emit(new SpellFx.P
                {
                    Rate = 4f * Mathf.Sqrt(size), Life = new Vector2(1.6f, 2.6f), Speed = new Vector2(0.05f, 0.2f),
                    Size = new Vector2(0.4f, 0.7f) * size, Start = new Color(0.25f, 0.23f, 0.22f, 0f), Mid = new Color(0.22f, 0.2f, 0.2f, 0.28f), End = new Color(0.2f, 0.2f, 0.2f, 0f),
                    Radius = 0.12f * size, Velocity = new Vector3(0.15f, 1.1f * Mathf.Sqrt(size), 0.05f), Smoke = true, Grow = true, Max = 30,
                }, pos + Vector3.up * 0.5f * size, go.transform);
            f.systems = new[] { flames, embers, plume };
            foreach (var ps in f.systems)
            {
                if (ps == null) continue;
                // Emit() builds one-shot effects; these burn forever.
                var main = ps.main;
                main.loop = true;
                main.prewarm = true;
                main.stopAction = ParticleSystemStopAction.None;
                ps.transform.localPosition = ps == embers ? Vector3.up * 0.1f * size : ps == plume ? Vector3.up * 0.5f * size : Vector3.zero;
                ps.Play();
            }
            return f;
        }

        void Update()
        {
            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + 0.5f + Random.value * 0.2f;
                var p = Player.I;
                bool want = p == null || (p.transform.position - transform.position).sqrMagnitude < ActiveRange * ActiveRange;
                if (want != active)
                {
                    active = want;
                    foreach (var ps in systems)
                    {
                        if (ps == null) continue;
                        if (want) ps.Play(); else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }
            if (!active) return;
            float t = Time.time * 7f;
            if (core != null)
            {
                float n = Mathf.PerlinNoise(t, seed), m = Mathf.PerlinNoise(seed, t * 1.3f);
                core.localScale = new Vector3(coreScale.x * (0.85f + m * 0.3f), coreScale.y * (0.8f + n * 0.45f), coreScale.z * (0.85f + m * 0.3f));
            }
        }

        // After NightLight/Flicker set the base brightness, add the fire's own flicker on top.
        void LateUpdate()
        {
            if (fireLight == null || !active) return;
            float n = Mathf.PerlinNoise(Time.time * 9f, seed) * 0.6f + Mathf.PerlinNoise(Time.time * 23f, seed + 5f) * 0.4f;
            // Only scale what this frame's owner set; remember the unflickered value when nobody else drives it.
            float baseI = Mathf.Approximately(fireLight.intensity, lastBase * lastK) ? lastBase : fireLight.intensity;
            lastBase = baseI;
            lastK = 0.75f + n * 0.45f;
            fireLight.intensity = baseI * lastK;
        }

        float lastK = 1f;
    }
}
