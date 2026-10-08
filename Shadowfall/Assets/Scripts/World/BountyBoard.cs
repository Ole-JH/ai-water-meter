using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The bounty boards (one in every walled town) with today's three bounties pinned up as notices (<see cref="Bounties"/>):
    /// a finished bounty's notice is torn off and flutters to the ground. And the Bounty Cache: when all three are done
    /// it falls out of the sky at your feet as a chest, trailing light, lands with a thud and bursts open (the loot itself
    /// is the server's, dropped as usual). Visual only, built after the world.
    /// </summary>
    public class BountyBoard : Interactable
    {
        public static readonly List<BountyBoard> Boards = new List<BountyBoard>();
        static readonly Vector3 HollowmereSpot = new Vector3(136.6f, 0f, 148.6f); // tools/layout/hollowmere_audit.py

        readonly Transform[] notices = new Transform[3];
        readonly bool[] torn = new bool[3];
        bool seen;

        public override string HoverText => "Bounty Board\n<today's bounties: new ones every day>";
        public override Color LabelColor => Bounties.Color;
        public override float LabelHeight => 3f;

        public static void SpawnAll()
        {
            if (Boards.Count > 0) return;
            var grid = WorldGrid.Instance;
            foreach (var t in WorldGenerator.Towns)
            {
                if (!t.Walled) continue;
                var at = t == WorldGenerator.Towns[0] ? HollowmereSpot : t.Center + new Vector3(4.5f, 0f, 4.5f);
                if (!grid.IsWalkable(at)) continue;
                Make(at, t.Center);
            }
        }

        static void Make(Vector3 at, Vector3 faceTo)
        {
            var go = new GameObject("BountyBoard");
            go.transform.position = at;
            go.transform.rotation = Quaternion.LookRotation(Factory.Flat(faceTo - at).normalized + Vector3.forward * 0.001f);
            var b = go.AddComponent<BountyBoard>();
            b.DisplayName = "Bounty Board";
            b.InteractRange = 2.4f;
            var wood = new Color(0.42f, 0.29f, 0.17f);
            foreach (float x in new[] { -0.85f, 0.85f })
                Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(x, 1.1f, 0f), new Vector3(0.12f, 2.2f, 0.12f), wood);
            Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 1.35f, 0.02f), new Vector3(1.6f, 1.1f, 0.06f), wood * 1.25f);
            var roof = Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 2.25f, -0.05f), new Vector3(2f, 0.07f, 0.55f), wood * 0.8f);
            roof.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            // a "WANTED" header plank
            Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 2.0f, 0.05f), new Vector3(1.1f, 0.18f, 0.04f), new Color(0.6f, 0.15f, 0.12f));
            for (int i = 0; i < 3; i++)
            {
                var n = new GameObject("Notice" + i).transform;
                n.SetParent(go.transform, false);
                n.localPosition = new Vector3((i - 1) * 0.5f, 1.38f + (i == 1 ? 0.06f : -0.02f), 0.07f);
                n.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * -4f);
                var paper = new Color(0.9f, 0.84f, 0.66f);
                Factory.Prim(PrimitiveType.Cube, n, Vector3.zero, new Vector3(0.4f, 0.55f, 0.01f), paper);
                Factory.Prim(PrimitiveType.Cube, n, new Vector3(0f, 0.12f, 0.008f), new Vector3(0.24f, 0.2f, 0.004f), new Color(0.35f, 0.28f, 0.22f)); // the sketch
                for (int l = 0; l < 3; l++)
                    Factory.Prim(PrimitiveType.Cube, n, new Vector3(0f, -0.06f - l * 0.06f, 0.008f), new Vector3(0.28f, 0.015f, 0.004f), new Color(0.4f, 0.35f, 0.3f)); // writing
                Factory.Prim(PrimitiveType.Sphere, n, new Vector3(0f, 0.25f, 0.012f), new Vector3(0.05f, 0.05f, 0.02f), new Color(0.7f, 0.1f, 0.1f)); // the pin
                b.notices[i] = n;
            }
            b.AddClickCollider(1f, 2.3f);
            Boards.Add(b);
        }

        void Update()
        {
            var list = Bounties.List;
            if (list.Length == 0) return;
            var p = Player.I;
            bool near = p != null && Factory.FlatDistance(p.transform.position, transform.position) < 40f;
            for (int i = 0; i < 3; i++)
            {
                bool done = i < list.Length && list[i].Done;
                if (done == torn[i] || notices[i] == null) continue;
                torn[i] = done;
                if (!done) { notices[i].gameObject.SetActive(true); continue; } // a new day: fresh notices
                // torn off: in front of us it flutters down; otherwise it's simply gone when we come by
                if (near && seen) TearOff(notices[i]);
                else notices[i].gameObject.SetActive(false);
            }
            seen = true;
        }

        void TearOff(Transform n)
        {
            var paper = Instantiate(n.gameObject, n.position, n.rotation);
            n.gameObject.SetActive(false);
            var fall = paper.AddComponent<FallingPiece>();
            fall.Velocity = transform.forward * 1.2f + Vector3.up * 0.8f;
            fall.Spin = new Vector3(Random.Range(-90f, 90f), Random.Range(-200f, 200f), Random.Range(-90f, 90f));
            paper.AddComponent<FadeAway>().Seconds = 8f;
            Sfx.Play("book", n.position, 0.5f, 0.15f, 25f);
        }

        public override void Interact(Player p)
        {
            Sfx.Play2D("book", 0.5f);
            var list = Bounties.List;
            if (list.Length == 0) { GameUI.Log("No bounties posted today. Try again tomorrow.", Bounties.Color); return; }
            GameUI.Log("Today's bounties:", Bounties.Color);
            foreach (var l in list)
                GameUI.Log((l.Done ? "  [done] " : "  ") + l.Text + (l.Done ? "" : "  (" + l.Have + "/" + l.Need + ")"), l.Done ? new Color(0.6f, 0.9f, 0.6f) : Bounties.Color);
            Speech.Say(transform, 2.6f, "WANTED");
        }

        // ------------------------------------------------------------------ the cache

        /// <summary>All three done: the Bounty Cache drops out of the sky at the hero's feet.</summary>
        public static void CacheFalls(Vector3 at)
        {
            var go = new GameObject("BountyCache");
            go.transform.position = at + Vector3.up * 22f;
            go.AddComponent<CacheDrop>().Ground = at;
        }

        class CacheDrop : MonoBehaviour
        {
            public Vector3 Ground;
            Transform chest, lid;
            float t, landedAt = -1f;
            ParticleSystem trail;

            void Start()
            {
                chest = new GameObject("Chest").transform;
                chest.SetParent(transform, false);
                var gold = new Color(1f, 0.8f, 0.35f);
                if (ArtLibrary.Spawn("Props/chest", chest, Vector3.zero, 0.9f, ArtLibrary.Fit.Height) == null)
                {
                    var wood = new Color(0.45f, 0.3f, 0.15f);
                    Factory.Prim(PrimitiveType.Cube, chest, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.6f), wood);
                    Factory.Prim(PrimitiveType.Cube, chest, new Vector3(0f, 0.3f, 0.31f), new Vector3(0.14f, 0.18f, 0.02f), gold);
                    lid = Factory.Prim(PrimitiveType.Cube, chest, new Vector3(0f, 0.68f, 0f), new Vector3(0.94f, 0.12f, 0.64f), new Color(0.5f, 0.33f, 0.15f)).transform;
                }
                var l = new GameObject("CacheLight").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.type = LightType.Point;
                l.color = gold;
                l.range = 6f;
                l.intensity = 2f;
                l.shadows = LightShadows.None;
                if (SpellFx.Ready)
                    trail = SpellFx.Emit(new SpellFx.P
                    {
                        Rate = 60, Duration = 3f, Life = new Vector2(0.4f, 0.8f), Speed = new Vector2(0.1f, 0.5f), Size = new Vector2(0.1f, 0.25f),
                        Start = gold, End = new Color(1f, 0.6f, 0.2f, 0f), Radius = 0.3f,
                    }, transform.position, transform);
                Sfx.Play2D("levelup", 0.4f, 1.3f);
            }

            void Update()
            {
                t += Time.deltaTime;
                if (landedAt < 0f)
                {
                    // falls faster and faster, turning slowly
                    float y = Mathf.Max(0f, 22f - 9f * t * t);
                    transform.position = new Vector3(Ground.x, Ground.y + y, Ground.z);
                    transform.Rotate(0f, 120f * Time.deltaTime, 0f);
                    if (y > 0f) return;
                    landedAt = Time.time;
                    SpellFx.Dust(Ground, 1.4f);
                    SpellFx.Ring(Ground + Vector3.up * 0.05f, new Color(1f, 0.8f, 0.35f), 2.2f, 0.6f);
                    Sfx.Play("hit_heavy", Ground, 0.9f, 0.05f, 40f);
                    var p = Player.I;
                    if (p != null && Factory.FlatDistance(p.transform.position, Ground) < 15f) CameraRig.Shake(0.25f);
                    return;
                }
                float since = Time.time - landedAt;
                if (since > 0.5f && since < 1.2f)
                {
                    // bursts open (the model chest gives a jolt), coins and light spilling out
                    float k = Mathf.Clamp01((since - 0.5f) / 0.4f);
                    if (lid != null)
                    {
                        lid.localRotation = Quaternion.Euler(-110f * k, 0f, 0f);
                        lid.localPosition = new Vector3(0f, 0.68f + 0.12f * k, -0.25f * k);
                    }
                    else chest.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.15f);
                    if (k >= 1f && since - Time.deltaTime < 0.9f)
                    {
                        SpellFx.Hit(Ground + Vector3.up * 0.8f, new Color(1f, 0.85f, 0.35f), false, 30);
                        Sfx.Play2D("coins", 0.8f);
                    }
                }
                if (since > 25f) transform.position += Vector3.down * Time.deltaTime * 0.4f;
                if (since > 28f) Destroy(gameObject);
            }
        }
    }
}
