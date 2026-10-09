using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// "Over here!": Alt+click the world map or the minimap and your party sees the spot ripple on their maps, a pillar
    /// of light stands there in the world for a few seconds, and a bell rings. Sent through the server to the party.
    /// </summary>
    public static class MapPing
    {
        public struct Ping { public Vector3 Pos; public Color Color; public string Name; public float Time; }
        public static readonly List<Ping> Pings = new List<Ping>();
        const float Life = 6f;
        static float nextSend;

        /// <summary>We ping <paramref name="world"/> (client space): shown here at once, and sent to the party.</summary>
        public static void Send(Vector3 world)
        {
            var net = NetClient.I;
            if (net == null || Time.unscaledTime < nextSend) return;
            if (!net.InParty) { GameUI.Log("Pings go to your party: invite someone first (right-click a player).", Color.gray); return; }
            nextSend = Time.unscaledTime + 0.8f;
            net.Ping(world);
            Add(world, GameUI.MemberColor(net.MyId), "You");
        }

        public static void Add(Vector3 world, Color c, string name)
        {
            world.y = 0f;
            Pings.Add(new Ping { Pos = world, Color = c, Name = name, Time = Time.unscaledTime });
            if (Pings.Count > 8) Pings.RemoveAt(0);
            Sfx.Play2D("bell", 0.35f, 1.5f);
            Beacon.Raise(world, c);
        }

        /// <summary>Draws the pings on a map: rings rippling out from the spot, the pinger's name under it.</summary>
        public static void Draw(System.Func<Vector3, Vector2> toMap, Rect clip, bool names)
        {
            for (int i = Pings.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - Pings[i].Time;
                if (age > Life) { Pings.RemoveAt(i); continue; }
                var at = toMap(Pings[i].Pos);
                if (!clip.Contains(at)) continue;
                var c = Pings[i].Color;
                if (Event.current.type == EventType.Repaint)
                    for (int k = 0; k < 2; k++)
                    {
                        float t = Mathf.Repeat(age * 1.1f + k * 0.5f, 1f);
                        float s = 8f + 34f * t;
                        GUI.color = new Color(c.r, c.g, c.b, (1f - t) * (age > Life - 1f ? Life - age : 1f));
                        Ring(at, s);
                    }
                GUI.color = new Color(c.r, c.g, c.b, 1f);
                GUI.DrawTexture(new Rect(at.x - 5, at.y - 5, 10, 10), UISkin.Circle);
                GUI.color = Color.white;
                if (names) UISkin.Shadowed(new Rect(at.x - 80, at.y + 10, 160, 20), Pings[i].Name, UISkin.SmallCenter, c, 2);
            }
        }

        static void Ring(Vector2 c, float size)
        {
            // a ring out of short dashes (IMGUI has no circles but textures)
            const int n = 16;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size * 0.5f;
                GUI.DrawTexture(new Rect(p.x - 1.5f, p.y - 1.5f, 3f, 3f), UISkin.White);
            }
        }

        /// <summary>The pillar of light standing on the pinged spot for a few seconds.</summary>
        class Beacon : MonoBehaviour
        {
            float born;
            Transform beam;
            Color color;

            public static void Raise(Vector3 at, Color c)
            {
                var go = new GameObject("PingBeacon");
                go.transform.position = at;
                var b = go.AddComponent<Beacon>();
                b.color = c;
                b.born = Time.time;
                b.beam = Factory.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 6f, 0f), new Vector3(0.35f, 6f, 0.35f), c, false, Mat.Glow(c)).transform;
                b.beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                SpellFx.Ring(at + Vector3.up * 0.05f, c, 2.5f, 0.8f);
            }

            void Update()
            {
                float age = Time.time - born;
                if (age > Life) { Destroy(gameObject); return; }
                float w = 0.35f * (1f + 0.2f * Mathf.Sin(age * 8f)) * Mathf.Clamp01((Life - age) / 1.2f) * Mathf.Clamp01(age * 6f);
                beam.localScale = new Vector3(w, 6f, w);
                if (Mathf.Repeat(age, 1.2f) < Time.deltaTime) SpellFx.Ring(transform.position + Vector3.up * 0.05f, color, 2f, 0.6f);
            }
        }
    }
}
