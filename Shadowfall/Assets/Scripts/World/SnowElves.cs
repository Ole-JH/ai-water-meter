using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Winterfest's street crew: five elves, each with a street of Hollowmere (or the square) to keep clear. An elf walks
    /// its lanes, and wherever the snow is deep it stops, digs, and throws the snow onto a pile at the street edge,
    /// leaving a clear path behind it (<see cref="SnowField.Clear"/>). While it keeps snowing, the paths fill in again
    /// and the elves start over, so the streets open up bit by bit.
    /// </summary>
    public static class ElfCrew
    {
        public static void Create(Transform parent)
        {
            var root = new GameObject("Elves").transform;
            root.SetParent(parent, false);
            var piles = root.gameObject.AddComponent<SnowPiles>();
            // Street centerlines (the cross streets run x 142..146 and y 142..146, the square 136..152).
            var routes = new List<Vector3[]>
            {
                new[] { new Vector3(144.5f, 0, 171f), new Vector3(144.5f, 0, 153.5f) },   // north street
                new[] { new Vector3(144.5f, 0, 117.5f), new Vector3(144.5f, 0, 135.5f) }, // south street
                new[] { new Vector3(117.5f, 0, 144.5f), new Vector3(135.5f, 0, 144.5f) }, // west street
                new[] { new Vector3(171f, 0, 144.5f), new Vector3(153.5f, 0, 144.5f) },   // east street
                new[] { new Vector3(137.5f, 0, 137.5f), new Vector3(151.5f, 0, 137.5f), new Vector3(151.5f, 0, 151.5f),
                        new Vector3(137.5f, 0, 151.5f), new Vector3(137.5f, 0, 137.6f) },  // around the square
            };
            Color[] coats = { new Color(0.45f, 0.95f, 0.45f), new Color(1f, 0.5f, 0.45f), new Color(0.5f, 0.95f, 0.6f), new Color(1f, 0.55f, 0.5f), new Color(0.55f, 1f, 0.5f) };
            for (int i = 0; i < routes.Count; i++) SnowElf.Create(root, routes[i], coats[i], piles, i);
        }
    }

    /// <summary>The snow the elves shovel ends up in piles along the street edges, growing as they work.</summary>
    public class SnowPiles : MonoBehaviour
    {
        const int MaxPiles = 60;
        readonly List<Transform> piles = new List<Transform>();
        readonly List<float> sizes = new List<float>();

        public void Add(Vector3 at, float amount)
        {
            for (int i = 0; i < piles.Count; i++)
            {
                if (piles[i] == null || Factory.FlatDistance(piles[i].position, at) > 2.2f) continue;
                sizes[i] = Mathf.Min(1.8f, sizes[i] + amount * 0.12f);
                piles[i].localScale = Vector3.one * sizes[i];
                return;
            }
            if (piles.Count >= MaxPiles) return;
            var go = ArtLibrary.Spawn("Seasonal/snow-pile", transform, at, 1f, ArtLibrary.Fit.Width, Random.Range(0f, 360f));
            if (go == null) return;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var pivot = new GameObject("SnowPile").transform;
            pivot.SetParent(transform, false);
            pivot.position = at;
            go.transform.SetParent(pivot, true);
            piles.Add(pivot);
            sizes.Add(0.35f);
            pivot.localScale = Vector3.one * 0.35f;
        }

        void Update()
        {
            // Without snow on the ground the piles melt away.
            if (Weather.SnowSouth > 0.05f) return;
            for (int i = 0; i < piles.Count; i++)
            {
                if (piles[i] == null) continue;
                sizes[i] = Mathf.Max(0f, sizes[i] - Time.deltaTime * 0.01f);
                piles[i].localScale = Vector3.one * sizes[i];
            }
        }
    }

    public class SnowElf : MonoBehaviour
    {
        const float Speed = 1.4f, Step = 0.8f, LaneOffset = 1.05f;
        static readonly string[] lines =
        {
            "Who ordered all this snow?", "Mind the shovel!", "Left, right, dig. Left, right, dig.", "My toes stopped talking to me an hour ago.",
            "Winterfest! Winterfest! Shovel first, then Winterfest.", "Please stay on the path. We worked very hard on the path.",
            "If you see the reindeer, he owes me a carrot.", "One more street. There's always one more street.",
            "Snow, snow, go away... no, wait, that's our job.", "Hot cocoa at the tavern after this. Hot. Cocoa.",
        };

        Vector3[] route;
        readonly List<Vector3> lane = new List<Vector3>();
        int laneIndex, stepIndex;
        SnowPiles piles;
        CharacterView view;
        float digUntil, digHit = -1f, chatterAt;
        Vector3 side;
        Light lantern;

        public static SnowElf Create(Transform parent, Vector3[] route, Color coat, SnowPiles piles, int seed)
        {
            var go = new GameObject("Elf");
            go.transform.SetParent(parent, false);
            go.transform.position = route[0];
            var e = go.AddComponent<SnowElf>();
            e.route = route;
            e.piles = piles;
            e.laneIndex = seed % 2;
            e.chatterAt = Time.time + 8f + seed * 5f;
            e.view = CharacterView.Create(go.transform, new CharacterLook { Model = "Characters/Rogue", Height = 1.15f, Tint = coat });
            if (e.view == null)
                HumanoidModel.Build(go.transform, 0.6f, new Color(0.95f, 0.8f, 0.65f), coat, coat * 0.7f, coat * 0.6f, false, true);
            e.Dress(coat);
            var l = new GameObject("Lantern").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(0.3f, 1.3f, 0.2f);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.75f, 0.4f);
            l.range = 4.5f;
            l.intensity = 1.1f;
            e.lantern = l;
            e.BuildLane();
            return e;
        }

        /// <summary>A pointy hat with a bobble, and a shovel.</summary>
        void Dress(Color coat)
        {
            Transform head = view != null ? ArtLibrary.FindDeep(view.Root.transform, "head") ?? ArtLibrary.FindDeep(view.Root.transform, "Head") : null;
            var hat = new GameObject("ElfHat").transform;
            if (head != null)
            {
                hat.SetParent(head, false);
                float s = 1f / Mathf.Max(0.0001f, head.lossyScale.y);
                hat.localScale = Vector3.one * s;
                hat.localPosition = new Vector3(0f, 0.22f * s, 0f);
            }
            else
            {
                hat.SetParent(transform, false);
                hat.localPosition = new Vector3(0f, 1.1f, 0f);
            }
            var red = coat.r > coat.g ? new Color(0.2f, 0.6f, 0.25f) : new Color(0.8f, 0.15f, 0.15f);
            var cone = new GameObject("Cone");
            cone.transform.SetParent(hat, false);
            cone.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            cone.AddComponent<MeshFilter>().sharedMesh = ConeMesh;
            cone.AddComponent<MeshRenderer>().sharedMaterial = Mat.Get(red);
            Factory.Prim(PrimitiveType.Sphere, cone.transform, new Vector3(0f, 0.42f, 0f), Vector3.one * 0.1f, Color.white);
            Factory.Prim(PrimitiveType.Cylinder, hat, new Vector3(0f, 0.01f, 0f), new Vector3(0.3f, 0.025f, 0.3f), Color.white);

            if (view == null) return;
            var hand = view.Hand;
            // Held at the top of the handle, the blade at the far end (like a sword, along the hand's +Y).
            float len = 0.95f / Mathf.Max(0.0001f, hand.lossyScale.y);
            var shovel = ArtLibrary.Spawn("Seasonal/shovel", hand, Vector3.zero, len, ArtLibrary.Fit.Height);
            if (shovel != null)
            {
                shovel.transform.localRotation = Quaternion.Euler(180f, 0f, 0f) * shovel.transform.localRotation;
                shovel.transform.localPosition = new Vector3(0f, len * 0.9f, 0f);
                foreach (var c in shovel.GetComponentsInChildren<Collider>()) Destroy(c);
            }
        }

        static Mesh coneMesh;
        static Mesh ConeMesh
        {
            get
            {
                if (coneMesh != null) return coneMesh;
                const int n = 10;
                var v = new List<Vector3> { new Vector3(0f, 0.45f, 0f) };
                var tris = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    v.Add(new Vector3(Mathf.Cos(a) * 0.14f, 0f, Mathf.Sin(a) * 0.14f));
                }
                for (int i = 0; i < n; i++) { tris.Add(0); tris.Add(1 + (i + 1) % n); tris.Add(1 + i); }
                coneMesh = new Mesh { name = "ElfHat" };
                coneMesh.SetVertices(v);
                coneMesh.SetTriangles(tris, 0);
                coneMesh.RecalculateNormals();
                return coneMesh;
            }
        }

        /// <summary>The route, offset to one side of the street (the elf works both sides in turn), sampled every step.</summary>
        void BuildLane()
        {
            lane.Clear();
            float off = laneIndex == 0 ? -LaneOffset : LaneOffset;
            bool back = laneIndex == 1;
            for (int i = 0; i + 1 < route.Length; i++)
            {
                var a = route[back ? route.Length - 1 - i : i];
                var b = route[back ? route.Length - 2 - i : i + 1];
                var dir = Factory.Flat(b - a).normalized;
                var perp = new Vector3(dir.z, 0f, -dir.x) * off;
                int n = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / Step));
                for (int k = 0; k <= n; k++) lane.Add(Vector3.Lerp(a, b, (float)k / n) + perp);
            }
            stepIndex = 0;
        }

        void Update()
        {
            if (lantern != null) lantern.enabled = DayNight.Night > 0.35f;
            float dt = Time.deltaTime;
            if (digHit > 0f && Time.time >= digHit) Throw();
            if (Time.time < digUntil) { view?.UpdateLocomotion(0f); return; }

            if (stepIndex >= lane.Count) { laneIndex = 1 - laneIndex; BuildLane(); }
            var target = lane[stepIndex];
            var to = Factory.Flat(target - transform.position);
            if (to.magnitude > 0.05f)
            {
                float step = Speed * dt;
                transform.position += to.normalized * Mathf.Min(step, to.magnitude);
                Factory.Face(transform, target, dt * 8f);
                view?.UpdateLocomotion(Speed);
            }
            else
            {
                // At a step: dig if the snow is deep here, else move on.
                if (SnowField.DepthAt(target) > 0.12f) Dig(target);
                stepIndex++;
            }
            Chatter();
        }

        void Dig(Vector3 at)
        {
            var next = lane[Mathf.Min(stepIndex + 1, lane.Count - 1)];
            var dir = Factory.Flat(next - at);
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();
            // Throw toward the near edge of the street.
            var perp = new Vector3(dir.z, 0f, -dir.x) * (laneIndex == 0 ? -1f : 1f);
            side = perp;
            Factory.Face(transform, at + perp, 1f);
            view?.Action("1H_Melee_Attack_Chop", 0.8f);
            digHit = Time.time + 0.45f;
            digUntil = Time.time + 1.15f;
        }

        void Throw()
        {
            digHit = -1f;
            var at = transform.position;
            float depth = SnowField.DepthAt(at);
            SnowField.Clear(at, 1.15f, 1f);
            Sfx.Play("shovel", at, 0.4f, 0.12f, 18f);
            var edge = at + side * 2.4f;
            piles.Add(edge, depth);
            if (!SpellFx.Ready) return;
            SpellFx.Emit(new SpellFx.P
            {
                Burst = 14, Duration = 0.1f, Life = new Vector2(0.5f, 0.9f), Speed = new Vector2(2f, 3.5f), Size = new Vector2(0.12f, 0.28f),
                Start = new Color(0.97f, 0.98f, 1f, 0.95f), End = new Color(0.97f, 0.98f, 1f, 0f), Gravity = 1.2f, Smoke = true, Radius = 0.2f,
                Shape = ParticleSystemShapeType.Cone, Arc = 25f,
            }, at + Vector3.up * 0.4f, null, Quaternion.LookRotation(side + Vector3.up * 0.8f));
        }

        void Chatter()
        {
            if (Time.time < chatterAt) return;
            chatterAt = Time.time + 20f + Random.Range(0f, 30f);
            var p = Player.I;
            if (p == null || Factory.FlatDistance(p.transform.position, transform.position) > 16f) return;
            string line = p.SnowDepth > 0.35f && Factory.FlatDistance(p.transform.position, transform.position) < 8f
                ? "Oi, " + p.DisplayName + "! You'll sink in there. Stick to the path, that's what it's for!" : lines[Random.Range(0, lines.Length)];
            Speech.Say(transform, 1.6f, line);
        }
    }
}
