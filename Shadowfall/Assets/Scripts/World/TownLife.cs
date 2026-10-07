using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Villagers strolling around Hollowmere, guards patrolling between the gates (with torches after dark)
    /// and the village dog. Purely visual and local to each player: they don't block anything.
    /// </summary>
    public class TownLife : MonoBehaviour
    {
        public static void Spawn(Transform parent)
        {
            var root = new GameObject("TownLife").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(77);
            string[] villagerModels = { "Characters/Keeper", "Characters/RogueHooded", "Characters/Mage", "Characters/Rogue" };
            Color[] tints = { new Color(1f, 0.9f, 0.8f), new Color(0.85f, 0.95f, 0.85f), new Color(0.9f, 0.85f, 1f), new Color(1f, 1f, 0.9f) };
            for (int i = 0; i < 10; i++)
            {
                string model = villagerModels[i % villagerModels.Length];
                bool child = i == 5 || i == 9;
                var look = new CharacterLook
                {
                    Model = model, Height = child ? 1.15f : 1.8f + (float)rng.NextDouble() * 0.15f,
                    Anims = model.EndsWith("Keeper") ? AnimSet.Kenney : AnimSet.KayKit, Tint = tints[rng.Next(tints.Length)],
                };
                Walker.Create(root, child ? "Child" : "Villager", look, RandomTownPoint(rng), child ? 2.6f : 1.5f, Walker.Kind.Villager);
            }
            var t = WorldGenerator.Town;
            Vector3[] gates = { new Vector3(t.center.x + 0.5f, 0, t.yMin + 2.5f), new Vector3(t.xMax - 2.5f, 0, t.center.y + 0.5f), new Vector3(t.center.x + 0.5f, 0, t.yMax - 2.5f), new Vector3(t.xMin + 2.5f, 0, t.center.y + 0.5f) };
            for (int i = 0; i < 3; i++)
            {
                var guard = Walker.Create(root, "Guard", new CharacterLook { Model = "Characters/Knight", Height = 2f, Tint = new Color(0.85f, 0.85f, 0.9f), Weapon = "sword" },
                    gates[i], 1.6f, Walker.Kind.Guard);
                guard.Route = gates;
                guard.RouteIndex = i;
            }
            Walker.Create(root, "Hound", new CharacterLook { Model = "Monsters/Wolf", Height = 0.85f, Anims = AnimSet.Wolf, RunSpeed = 5f, Tint = new Color(0.55f, 0.45f, 0.38f) },
                new Vector3(143.5f, 0, 142.5f), 2.5f, Walker.Kind.Dog);
        }

        static readonly List<Vector3> townPoints = new List<Vector3>();

        public static Vector3 RandomTownPoint(System.Random rng)
        {
            if (townPoints.Count == 0)
            {
                var grid = WorldGrid.Instance;
                var town = WorldGenerator.Town;
                for (int y = town.yMin + 2; y < town.yMax - 2; y++)
                    for (int x = town.xMin + 2; x < town.xMax - 2; x++)
                        if (!grid.IsBlocked(x, y) && !grid.IsBlocked(x + 1, y) && !grid.IsBlocked(x, y + 1) && !grid.IsBlocked(x - 1, y) && !grid.IsBlocked(x, y - 1))
                            townPoints.Add(new Vector3(x + 0.5f, 0, y + 0.5f));
            }
            return townPoints[rng.Next(townPoints.Count)];
        }
    }

    /// <summary>A villager, guard or dog that walks between points in town and sometimes says something.</summary>
    public class Walker : MonoBehaviour
    {
        public enum Kind { Villager, Guard, Dog }

        public Vector3[] Route;
        public int RouteIndex;

        Kind kind;
        string speaker;
        float speed;
        CharacterView view;
        readonly List<Vector3> path = new List<Vector3>();
        int pathIndex;
        float waitUntil, chatterAt, nextTrample;
        Light torch;
        Renderer[] renderers;
        bool hidden;
        System.Random rng;

        public static Walker Create(Transform parent, string speaker, CharacterLook look, Vector3 pos, float speed, Kind kind)
        {
            var go = new GameObject(speaker);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var w = go.AddComponent<Walker>();
            w.kind = kind;
            w.speaker = speaker == "Child" ? "Villager" : speaker;
            w.speed = speed;
            w.rng = new System.Random(pos.GetHashCode());
            w.view = CharacterView.Create(go.transform, look);
            if (w.view == null)
                HumanoidModel.Build(go.transform, look.Height / 1.9f, new Color(0.9f, 0.75f, 0.6f), look.Tint ?? Color.gray, Color.gray, Color.gray, false, true);
            w.renderers = go.GetComponentsInChildren<Renderer>();
            w.waitUntil = Time.time + (float)w.rng.NextDouble() * 4f;
            w.chatterAt = Time.time + 10f + (float)w.rng.NextDouble() * 30f;
            if (kind == Kind.Guard)
            {
                var l = new GameObject("GuardTorch").AddComponent<Light>();
                l.transform.SetParent(go.transform, false);
                l.transform.localPosition = new Vector3(0.4f, 2.2f, 0.3f);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.65f, 0.3f);
                l.range = 6f;
                l.intensity = 1.3f;
                w.torch = l;
            }
            return w;
        }

        void Update()
        {
            // Most villagers go home after dark; guards and the dog stay out.
            bool shouldHide = kind == Kind.Villager && DayNight.Night > 0.8f;
            if (shouldHide != hidden && (path.Count == 0 || pathIndex >= path.Count))
            {
                hidden = shouldHide;
                foreach (var r in renderers) if (r != null) r.enabled = !hidden;
            }
            if (torch != null) torch.enabled = DayNight.Night > 0.35f;
            if (hidden) return;

            float moved = 0f;
            if (pathIndex < path.Count)
            {
                var target = path[pathIndex];
                var to = target - transform.position;
                to.y = 0f;
                float step = speed * Time.deltaTime;
                if (to.magnitude <= step) { transform.position = target; pathIndex++; }
                else
                {
                    transform.position += to.normalized * step;
                    Factory.Face(transform, target, Time.deltaTime * 8f);
                }
                moved = speed;
                if (Time.time >= nextTrample) { nextTrample = Time.time + 0.3f; SnowField.Trample(transform.position); }
                if (pathIndex >= path.Count) waitUntil = Time.time + Pause();
            }
            else if (Time.time >= waitUntil) PickDestination();
            view?.UpdateLocomotion(moved);

            Chatter();
        }

        float Pause() => kind == Kind.Guard ? 3f + (float)rng.NextDouble() * 4f : 2f + (float)rng.NextDouble() * 8f;

        void PickDestination()
        {
            Vector3 goal;
            var p = Player.I;
            if (kind == Kind.Guard && Route != null)
            {
                RouteIndex = (RouteIndex + 1) % Route.Length;
                goal = Route[RouteIndex];
            }
            else if (kind == Kind.Dog && p != null && WorldGenerator.InTown(p.transform.position) && rng.NextDouble() < 0.5)
                goal = p.transform.position + new Vector3((float)rng.NextDouble() * 2f - 1f, 0, (float)rng.NextDouble() * 2f - 1f) * 1.5f; // come say hi
            else goal = TownLife.RandomTownPoint(rng);

            path.Clear();
            pathIndex = 0;
            if (!WorldGrid.Instance.FindPath(transform.position, goal, path, 3000)) waitUntil = Time.time + 2f;
        }

        void Chatter()
        {
            if (Time.time < chatterAt) return;
            chatterAt = Time.time + 25f + (float)rng.NextDouble() * 40f;
            var p = Player.I;
            if (p == null || Factory.FlatDistance(p.transform.position, transform.position) > 18f) return;
            if (kind == Kind.Dog) return;
            string line = NpcChatter.Line(speaker, p.DisplayName);
            if (line != null) Speech.Say(transform, kind == Kind.Dog ? 1.2f : 2.6f, line);
        }
    }
}
