using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// A blacksmith's corner in every walled town: an anvil with a glowing ingot, a hearth of coals with smoke and
    /// embers, a quench bucket and a pile of salvaged materials, next to the town's smith (the weapon and armour
    /// merchants). When you salvage or reforge (GameUI.Forge; the server does the work), the smith does it on the anvil
    /// in front of you: the piece is laid on it and hammered, salvaged gear breaks into shards and its materials fly onto
    /// the pile, a reforged affix burns off in red and the new one is stamped in gold before the piece hisses in the
    /// bucket. Visual only, built after the world (nothing here is in the map).
    /// </summary>
    public class ForgeStation : MonoBehaviour
    {
        public static readonly List<ForgeStation> All = new List<ForgeStation>();

        static readonly string[] Smiths = { "Smith Gorrin", "Runesmith Halvard", "Sandsmith Tariq", "Quartermaster Bryn" };
        static readonly Color Iron = new Color(0.22f, 0.22f, 0.24f), Stone = new Color(0.42f, 0.4f, 0.38f), Coal = new Color(1f, 0.38f, 0.08f),
            Wood = new Color(0.45f, 0.32f, 0.2f), Spark = new Color(1f, 0.65f, 0.25f);
        const int MaxPile = 36;

        Npc smith;
        Vector3 anvilTop, bucket, pile;
        Transform pileRoot, ingot;
        Light hearthLight;
        int pileCount;
        bool busy;

        /// <summary>Builds the forges by the smiths (once, after the world is made).</summary>
        public static void BuildAll()
        {
            if (All.Count > 0) return;
            foreach (var i in Interactable.All)
            {
                if (!(i is Npc n)) continue;
                bool smith = System.Array.IndexOf(Smiths, n.DisplayName) >= 0;
                if (!smith) continue;
                Make(n);
            }
        }

        static void Make(Npc n)
        {
            var grid = WorldGrid.Instance;
            // a free spot beside the smith for the anvil, the hearth beyond it
            Vector3 c = n.transform.position, side = Vector3.zero;
            foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                if (grid.IsWalkable(c + d * 1.4f) && grid.IsWalkable(c + d * 2.6f) && !NearOther(c + d * 1.6f, n)) { side = d; break; }
            if (side == Vector3.zero) return;
            var go = new GameObject("Forge " + n.DisplayName);
            go.transform.position = c + side * 1.4f;
            go.transform.rotation = Quaternion.LookRotation(Vector3.Cross(side, Vector3.up)); // the anvil runs across
            var f = go.AddComponent<ForgeStation>();
            f.smith = n;
            f.Build(side);
            n.WorkSpot = go.transform.position;
            All.Add(f);
        }

        static bool NearOther(Vector3 p, Npc self)
        {
            foreach (var i in Interactable.All)
                if (i != self && i is Npc && Factory.FlatDistance(i.transform.position, p) < 1.3f) return true;
            return false;
        }

        void Build(Vector3 side)
        {
            var t = transform;
            // anvil: block, waist, face and horn
            Factory.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.17f, 0f), new Vector3(0.5f, 0.34f, 0.42f), Wood * 0.8f); // stump
            Factory.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.42f, 0f), new Vector3(0.26f, 0.18f, 0.22f), Iron);
            Factory.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.58f, 0f), new Vector3(0.62f, 0.14f, 0.28f), Iron * 1.2f);
            var horn = Factory.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.6f, 0.4f), new Vector3(0.12f, 0.1f, 0.26f), Iron * 1.2f);
            horn.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            anvilTop = t.position + Vector3.up * 0.68f;
            ingot = Factory.Prim(PrimitiveType.Cube, t, new Vector3(0.05f, 0.68f, -0.05f), new Vector3(0.24f, 0.05f, 0.09f), Coal, false, Mat.Glow(Coal)).transform;
            // tongs and hammer lying by it
            Factory.Prim(PrimitiveType.Cube, t, new Vector3(-0.2f, 0.66f, 0.08f), new Vector3(0.03f, 0.03f, 0.34f), Iron).transform.localRotation = Quaternion.Euler(0f, 20f, 0f);

            // hearth: stone box, glowing coals, light, smoke and embers
            var hearth = hearthAt = t.position + side * 1.2f;
            var h = Factory.Prim(PrimitiveType.Cube, null, hearth + Vector3.up * 0.4f, new Vector3(0.95f, 0.8f, 0.95f), Stone).transform;
            h.SetParent(t, true);
            var coals = Factory.Prim(PrimitiveType.Cube, null, hearth + Vector3.up * 0.82f, new Vector3(0.75f, 0.06f, 0.75f), Coal, false, Mat.Glow(Coal)).transform;
            coals.SetParent(t, true);
            var hood = Factory.Prim(PrimitiveType.Cube, null, hearth + Vector3.up * 1.7f, new Vector3(0.6f, 0.9f, 0.6f), Stone * 0.8f).transform;
            hood.SetParent(t, true);
            hearthLight = new GameObject("HearthLight").AddComponent<Light>();
            hearthLight.transform.SetParent(t, false);
            hearthLight.transform.position = hearth + Vector3.up * 1.1f;
            hearthLight.type = LightType.Point;
            hearthLight.color = new Color(1f, 0.55f, 0.2f);
            hearthLight.range = 5f;
            hearthLight.intensity = 1.3f;
            hearthLight.shadows = LightShadows.None;
            // quench bucket on the other side of the anvil
            bucket = t.position - side * 0.75f + t.forward * 0.35f;
            var b = Factory.Prim(PrimitiveType.Cylinder, null, bucket + Vector3.up * 0.22f, new Vector3(0.38f, 0.22f, 0.38f), Wood).transform;
            b.SetParent(t, true);
            var water = Factory.Prim(PrimitiveType.Cylinder, null, bucket + Vector3.up * 0.42f, new Vector3(0.32f, 0.01f, 0.32f), new Color(0.15f, 0.2f, 0.25f)).transform;
            water.SetParent(t, true);

            // the material pile, behind the anvil
            pile = t.position - t.forward * 0.85f + side * 0.4f;
            pileRoot = new GameObject("MaterialPile").transform;
            pileRoot.SetParent(t, false);
            pileRoot.position = pile;
            for (int i = 0; i < 6; i++) AddToPile(new Color(0.5f, 0.5f, 0.55f)); // a few scraps to start with
        }

        Vector3 hearthAt;
        bool fxStarted;

        /// <summary>Smoke from the hood and embers off the coals (once the effects are loaded).</summary>
        void StartFx()
        {
            fxStarted = true;
            var hearth = hearthAt;
            var t = transform;
            SpellFx.Emit(new SpellFx.P
            {
                Rate = 4, Duration = 1000000f, Life = new Vector2(3f, 5f), Speed = new Vector2(0.2f, 0.4f), Size = new Vector2(0.4f, 0.8f),
                Start = new Color(0.3f, 0.28f, 0.27f, 0.35f), End = new Color(0.3f, 0.28f, 0.27f, 0f), Velocity = new Vector3(0f, 0.8f, 0f), Grow = true, Smoke = true,
            }, hearth + Vector3.up * 2.2f, t);
            SpellFx.Emit(new SpellFx.P
            {
                Rate = 5, Duration = 1000000f, Life = new Vector2(0.8f, 1.6f), Speed = new Vector2(0.1f, 0.4f), Size = new Vector2(0.03f, 0.06f),
                Start = Spark, End = new Color(1f, 0.3f, 0.05f, 0f), Velocity = new Vector3(0f, 1.2f, 0f), Shape = ParticleSystemShapeType.Circle, Radius = 0.3f,
            }, hearth + Vector3.up * 0.9f, t);
        }

        Material ingotMat;

        void Update()
        {
            if (!fxStarted && SpellFx.Ready) StartFx();
            if (hearthLight != null) hearthLight.intensity = 1.2f + Mathf.PerlinNoise(Time.time * 3f, transform.position.x) * 0.5f;
            if (ingot != null)
            {
                if (ingotMat == null) { var r = ingot.GetComponent<Renderer>(); if (r != null) ingotMat = r.material; }
                float glow = 0.75f + Mathf.Sin(Time.time * 2f) * 0.25f;
                if (ingotMat != null) ingotMat.color = Coal * glow;
            }
        }

        /// <summary>The forge nearest the hero, if it's close enough to watch.</summary>
        public static ForgeStation Near(Vector3 p)
        {
            ForgeStation best = null;
            float bestD = 18f;
            foreach (var f in All)
            {
                float d = Factory.FlatDistance(f.transform.position, p);
                if (d < bestD) { bestD = d; best = f; }
            }
            return best;
        }

        // ------------------------------------------------------------------ salvage

        /// <summary>Breaks a piece on the anvil: hammered, shattered, its materials onto the pile.</summary>
        public void Salvage(Color itemColor, bool weapon, string[] materials)
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(SalvageShow(itemColor, weapon, materials ?? new string[0]));
        }

        IEnumerator SalvageShow(Color itemColor, bool weapon, string[] materials)
        {
            while (busy) yield return null;
            busy = true;
            var piece = Piece(itemColor, weapon);
            for (int i = 0; i < 3; i++)
            {
                Strike(1f + i * 0.1f);
                yield return new WaitForSeconds(0.38f);
            }
            // it breaks
            for (int i = 0; i < 9; i++)
            {
                var shard = Factory.Prim(PrimitiveType.Cube, null, anvilTop + Random.insideUnitSphere * 0.15f, Vector3.one * Random.Range(0.05f, 0.12f), itemColor * Random.Range(0.6f, 1f));
                var fall = shard.AddComponent<FallingPiece>();
                fall.Velocity = new Vector3(Random.Range(-2f, 2f), Random.Range(2f, 3.5f), Random.Range(-2f, 2f));
                fall.Spin = Random.insideUnitSphere * 600f;
                shard.AddComponent<FadeAway>().Seconds = 6f;
            }
            Destroy(piece);
            Sfx.Play("shatter", anvilTop, 0.55f, 0.1f, 25f);
            SpellFx.Hit(anvilTop, itemColor, false, 16);
            yield return new WaitForSeconds(0.25f);
            // what came of it flies onto the pile
            foreach (var m in materials) // "3 Scrap Iron"
            {
                int sp = m.IndexOf(' ');
                int n = sp > 0 && int.TryParse(m.Substring(0, sp), out var k) ? k : 1;
                string name = sp > 0 ? m.Substring(sp + 1) : m;
                for (int i = 0; i < Mathf.Min(n, 6); i++)
                {
                    StartCoroutine(Fly(MaterialColor(name)));
                    yield return new WaitForSeconds(0.1f);
                }
            }
            busy = false;
        }

        IEnumerator Fly(Color c)
        {
            var bit = Factory.Prim(PrimitiveType.Cube, null, anvilTop, Vector3.one * 0.1f, c, false, Mat.Glow(c * 0.8f));
            var from = anvilTop;
            var to = pile + Vector3.up * 0.1f + Random.insideUnitSphere * 0.15f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
            {
                var p = Vector3.Lerp(from, to, t);
                p.y += Mathf.Sin(t * Mathf.PI) * 0.7f;
                bit.transform.position = p;
                bit.transform.Rotate(400f * Time.deltaTime, 300f * Time.deltaTime, 0f);
                yield return null;
            }
            Destroy(bit);
            AddToPile(c);
            Sfx.Play("drop", to, 0.25f, 0.2f, 15f);
        }

        void AddToPile(Color c)
        {
            if (pileRoot == null) return;
            if (pileCount >= MaxPile && pileRoot.childCount > 0) Destroy(pileRoot.GetChild(0).gameObject); // the oldest is carted off
            pileCount++;
            float r = 0.32f * Random.value, a = Random.Range(0f, Mathf.PI * 2f), y = 0.05f + Mathf.Min(0.35f, pileRoot.childCount * 0.012f) * (1f - r * 2f);
            var bit = Factory.Prim(PrimitiveType.Cube, pileRoot, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), Vector3.one * Random.Range(0.09f, 0.16f), c);
            bit.transform.localRotation = Random.rotation;
            bit.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Color MaterialColor(string name)
        {
            switch (name)
            {
                case "Arcane Dust": return new Color(0.55f, 0.75f, 1f);
                case "Veiled Crystal": return new Color(1f, 0.85f, 0.3f);
                case "Forgotten Soul": return new Color(1f, 0.5f, 0.15f);
                default: return new Color(0.55f, 0.55f, 0.6f); // scrap, and gems
            }
        }

        // ------------------------------------------------------------------ reforge

        /// <summary>Reforges a piece on the anvil: the old property burns off, the new one is hammered in, then the quench.</summary>
        public void Reforge(Color itemColor, bool weapon, string oldText, string newText)
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(ReforgeShow(itemColor, weapon, oldText, newText));
        }

        IEnumerator ReforgeShow(Color itemColor, bool weapon, string oldText, string newText)
        {
            while (busy) yield return null;
            busy = true;
            var piece = Piece(itemColor, weapon);
            var r = piece.GetComponent<Renderer>();
            // heated: it glows, and the old property burns away
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                if (r != null) r.material.color = Color.Lerp(itemColor, Coal * 1.4f, t / 0.7f);
                yield return null;
            }
            if (!string.IsNullOrEmpty(oldText)) GameUI.Float(anvilTop + Vector3.up * 0.5f, oldText, new Color(1f, 0.35f, 0.25f), 1.4f);
            SpellFx.Hit(anvilTop + Vector3.up * 0.1f, new Color(1f, 0.4f, 0.1f), false, 20);
            Sfx.Play("fire_cast", anvilTop, 0.45f, 0.1f, 25f);
            yield return new WaitForSeconds(0.45f);
            for (int i = 0; i < 3; i++)
            {
                Strike(1.1f + i * 0.08f);
                yield return new WaitForSeconds(0.36f);
            }
            if (!string.IsNullOrEmpty(newText)) GameUI.Float(anvilTop + Vector3.up * 0.7f, newText, new Color(1f, 0.85f, 0.35f), 1.8f);
            Sfx.Play2D("quest_done", 0.35f);
            yield return new WaitForSeconds(0.35f);
            // the quench
            var from = piece.transform.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                var p = Vector3.Lerp(from, bucket + Vector3.up * 0.35f, t);
                p.y += Mathf.Sin(t * Mathf.PI) * 0.4f;
                piece.transform.position = p;
                yield return null;
            }
            SpellFx.Dust(bucket + Vector3.up * 0.4f, 0.5f, new Color(0.9f, 0.9f, 0.92f));
            Sfx.Play("sizzle", bucket, 0.6f, 0.1f, 25f);
            Destroy(piece);
            busy = false;
        }

        // ------------------------------------------------------------------ shared

        /// <summary>The piece of gear lying on the anvil: a blade or a plate, in its rarity's colour.</summary>
        GameObject Piece(Color c, bool weapon)
        {
            var size = weapon ? new Vector3(0.08f, 0.03f, 0.55f) : new Vector3(0.32f, 0.04f, 0.26f);
            var go = Factory.Prim(PrimitiveType.Cube, null, anvilTop + Vector3.up * 0.03f, size, c);
            go.transform.rotation = transform.rotation * Quaternion.Euler(0f, 90f, 0f);
            Sfx.Play("equip", anvilTop, 0.4f, 0.1f, 15f);
            return go;
        }

        /// <summary>A hammer blow: the smith swings, sparks fly, the anvil rings.</summary>
        void Strike(float pitch)
        {
            if (smith != null) smith.Work(transform.position);
            SpellFx.Hit(anvilTop + Vector3.up * 0.05f, Spark, false, 14);
            Sfx.Play("anvil", anvilTop, 0.6f, 0.05f, 30f);
            var p = Player.I;
            if (p != null && Factory.FlatDistance(p.transform.position, transform.position) < 6f) CameraRig.Shake(0.05f);
        }
    }
}
