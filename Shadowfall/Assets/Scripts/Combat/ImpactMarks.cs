using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// What spells leave on the ground for a while after the flash: scorch with embers where fire landed, rime and ice
    /// splinters after frost, cracked earth where something heavy came down, and glowing runes after holy power. They
    /// fade and sink away; only the newest few dozen are kept.
    /// </summary>
    public static class ImpactMarks
    {
        public enum Kind { Scorch, Frost, Crack, Holy }
        const int Max = 40;
        static readonly Queue<GameObject> marks = new Queue<GameObject>();

        public static void Place(Vector3 at, Kind kind, float radius, float seconds = 18f)
        {
            if (WorldGrid.Instance != null && !WorldGrid.Instance.IsWalkable(at)) return;
            var root = new GameObject("Mark " + kind).transform;
            root.position = new Vector3(at.x, 0f, at.z);
            root.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            switch (kind)
            {
                case Kind.Scorch:
                    Disc(root, radius, new Color(0.08f, 0.06f, 0.05f));
                    Disc(root, radius * 0.6f, new Color(0.04f, 0.03f, 0.03f), 0.016f);
                    for (int i = 0; i < 6; i++) // embers still glowing in it
                    {
                        var e = Bit(root, radius * 0.7f, new Vector3(0.08f, 0.03f, 0.08f), new Color(1f, 0.4f, 0.08f), true);
                        Object.Destroy(e, Random.Range(3f, 7f));
                    }
                    break;
                case Kind.Frost:
                    Disc(root, radius, new Color(0.75f, 0.88f, 1f));
                    for (int i = 0; i < 10; i++)
                    {
                        var s = Bit(root, radius * 0.9f, new Vector3(0.06f, Random.Range(0.15f, 0.35f), 0.06f), new Color(0.7f, 0.9f, 1f), true);
                        s.transform.localRotation = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 90f), Random.Range(-30f, 30f));
                        Object.Destroy(s, Random.Range(4f, 8f)); // the splinters melt first
                    }
                    break;
                case Kind.Crack:
                    Disc(root, radius * 0.5f, new Color(0.2f, 0.17f, 0.14f));
                    int n = Random.Range(6, 9);
                    for (int i = 0; i < n; i++)
                    {
                        float ang = i * 360f / n + Random.Range(-14f, 14f), len = radius * Random.Range(0.7f, 1.3f);
                        var c = Factory.Prim(PrimitiveType.Cube, root, Vector3.zero, new Vector3(Random.Range(0.06f, 0.12f), 0.012f, len), new Color(0.1f, 0.08f, 0.07f));
                        c.transform.localRotation = Quaternion.Euler(0f, ang, 0f);
                        c.transform.localPosition = c.transform.localRotation * new Vector3(0f, 0.022f, len * 0.5f + radius * 0.2f);
                        NoShadow(c);
                    }
                    for (int i = 0; i < 5; i++) Bit(root, radius, Vector3.one * Random.Range(0.12f, 0.25f), new Color(0.35f, 0.3f, 0.26f), false); // stones thrown up
                    break;
                case Kind.Holy:
                {
                    var gold = new Color(1f, 0.85f, 0.4f);
                    var ring = Factory.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 0.015f, 0f), new Vector3(radius * 2f, 0.004f, radius * 2f), gold * 0.5f, false, Mat.Glow(gold * 0.45f));
                    NoShadow(ring);
                    Disc(root, radius * 0.9f, new Color(0.3f, 0.26f, 0.18f), 0.017f);
                    for (int i = 0; i < 8; i++) // runes round the edge
                    {
                        float a = i * Mathf.PI / 4f;
                        var r = Factory.Prim(PrimitiveType.Cube, root, new Vector3(Mathf.Cos(a), 0.022f, Mathf.Sin(a)) * radius * 0.75f, new Vector3(0.14f, 0.01f, 0.22f), gold, false, Mat.Glow(gold));
                        r.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                        NoShadow(r);
                        Object.Destroy(r, seconds * 0.5f); // the runes go out before the burn fades
                    }
                    break;
                }
            }
            root.gameObject.AddComponent<FadeAway>().Seconds = seconds;
            marks.Enqueue(root.gameObject);
            while (marks.Count > Max) { var old = marks.Dequeue(); if (old != null) Object.Destroy(old); }
        }

        static void Disc(Transform root, float radius, Color c, float y = 0.012f)
        {
            var d = Factory.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, y, 0f), new Vector3(radius * 2f, 0.004f, radius * 2f * Random.Range(0.8f, 1f)), c);
            NoShadow(d);
        }

        static GameObject Bit(Transform root, float spread, Vector3 size, Color c, bool glow)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(0.1f, spread);
            var b = Factory.Prim(PrimitiveType.Cube, root, new Vector3(Mathf.Cos(a) * r, size.y * 0.5f, Mathf.Sin(a) * r), size, c, false, glow ? Mat.Glow(c) : null);
            b.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            NoShadow(b);
            return b;
        }

        static void NoShadow(GameObject go) => go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
