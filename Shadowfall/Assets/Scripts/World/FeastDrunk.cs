using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// A reveller who has had far too much at the Heroes' Feast (SiegeAftermath.Feast): reels about the square in long
    /// weaving curves, swaying and lurching, slurs at people, hiccups, now and then doubles over and is sick (leaving a
    /// puddle), and sometimes goes down flat for a while before getting up and carrying on. Only to look at.
    /// </summary>
    public class FeastDrunk : MonoBehaviour
    {
        CharacterView view;
        Vector3 centre, target;
        float roam, speed, phase, sway;
        float nextSay, nextPuke, nextFall, nextLurch;
        float pukeUntil, downUntil, lurchUntil;
        Vector3 lurch;
        bool puking, down;
        static readonly string[] lines =
        {
            "*hic*", "Anuzzer round! For the... *hic* ...wall!", "I love you. I love all of you.", "Who moved the ground?",
            "I held the gate! Me! With... with my hands!", "~ Fill the cups and... and... ~", "Shhhh. Shhh. The raiders are shleeping.",
            "Thish ale ish... ish *hic* ...good ale.", "Barkeep! BARKEEP! Oh, there you are. Two of you.", "I'm fine. I'm fine! ...I'm not fine.",
            "Whose round izzit?", "Wait wait wait. Lissen. LISSEN.",
        };
        static readonly string[] after = { "Better out than in...", "Ugh. Not the turkey...", "Nobody saw that.", "...still worth it." };

        public static FeastDrunk Create(Transform parent, Vector3 at, string model, Vector3 centre, float roam, int seed)
        {
            var go = new GameObject("Drunk");
            go.transform.SetParent(parent, true);
            go.transform.position = at;
            var d = go.AddComponent<FeastDrunk>();
            d.centre = centre;
            d.roam = roam;
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            d.speed = R(0.8f, 1.2f);
            d.phase = R(0f, 10f);
            d.nextSay = Time.time + R(2f, 8f);
            d.nextPuke = Time.time + R(12f, 30f);
            d.nextFall = Time.time + R(30f, 70f);
            d.nextLurch = Time.time + R(2f, 5f);
            d.view = CharacterView.Create(go.transform, new CharacterLook
            {
                Model = model, Height = R(1.75f, 1.92f), Anims = model.EndsWith("Keeper") ? AnimSet.Kenney : AnimSet.KayKit,
                Tint = new Color(R(0.95f, 1f), R(0.72f, 0.82f), R(0.7f, 0.8f)), // red in the face
            });
            d.target = d.NewTarget();
            return d;
        }

        Vector3 NewTarget()
        {
            for (int i = 0; i < 10; i++)
            {
                var p = centre + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(2f, roam);
                if (WorldGrid.Instance == null || WorldGrid.Instance.IsWalkable(p)) return new Vector3(p.x, 0f, p.z);
            }
            return centre;
        }

        void Update()
        {
            if (view == null) return;
            float t = Time.time, dt = Time.deltaTime;
            var hero = Player.I;
            bool seen = hero != null && Factory.FlatDistance(hero.transform.position, transform.position) < 45f;

            // flat out on the cobbles for a while, then up again
            if (down)
            {
                if (t < downUntil) { Lean(0f, 0f, dt); return; }
                down = false;
                view.StopEmote();
                if (seen) Speech.Say(transform, 2.3f, Random.value < 0.5f ? "Wha'... where'd the floor go?" : "I wazh just resting my eyes.");
            }
            // doubled over, being sick
            if (puking)
            {
                Lean(40f, Mathf.Sin(t * 9f) * 4f, dt);
                view.UpdateLocomotion(0f);
                if (t >= pukeUntil)
                {
                    puking = false;
                    if (seen) Speech.Say(transform, 2.3f, after[Random.Range(0, after.Length)]);
                }
                return;
            }
            if (t >= nextPuke && seen) { Puke(); return; }
            if (t >= nextFall && seen && Random.value < 0.5f)
            {
                nextFall = t + Random.Range(40f, 90f);
                down = true;
                downUntil = t + Random.Range(5f, 10f);
                view.Emote(EmoteDef.Get("sleep"));
                Sfx.Play("hit_flesh", transform.position, 0.3f, 0.2f, 20f);
                return;
            }
            if (t >= nextSay && seen)
            {
                nextSay = t + Random.Range(5f, 11f);
                Speech.Say(transform, 2.3f, lines[Random.Range(0, lines.Length)]);
            }

            // reeling towards somewhere, never in a straight line: the heading swings to and fro, the pace comes and goes,
            // and now and then a lurch sideways
            var to = Factory.Flat(target - transform.position);
            if (to.magnitude < 0.6f) { target = NewTarget(); return; }
            var dir = Quaternion.Euler(0f, Mathf.Sin(t * 1.3f + phase) * 45f + Mathf.Sin(t * 3.1f + phase) * 15f, 0f) * to.normalized;
            float pace = speed * (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 0.9f + phase)));
            if (t >= nextLurch)
            {
                nextLurch = t + Random.Range(3f, 7f);
                lurchUntil = t + 0.45f;
                lurch = Quaternion.Euler(0f, Random.value < 0.5f ? 90f : -90f, 0f) * dir * 1.6f;
            }
            var step = dir * pace + (t < lurchUntil ? lurch : Vector3.zero);
            var next = transform.position + step * dt;
            if (WorldGrid.Instance != null && !WorldGrid.Instance.IsWalkable(next)) { target = NewTarget(); return; }
            transform.position = new Vector3(next.x, 0f, next.z);
            if (step.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Factory.Flat(step)), dt * 3f);
            sway = Mathf.Sin(t * 2.2f + phase) * 13f + (t < lurchUntil ? 10f : 0f);
            Lean(8f + Mathf.Sin(t * 1.7f + phase) * 5f, sway, dt);
            view.UpdateLocomotion(step.magnitude);
        }

        /// <summary>Tilts the whole body: forwards (bent over) and side to side (swaying).</summary>
        void Lean(float forward, float side, float dt)
        {
            var r = view.Root.transform;
            r.localRotation = Quaternion.Slerp(r.localRotation, Quaternion.Euler(forward, 0f, side), dt * 6f);
        }

        void Puke()
        {
            puking = true;
            pukeUntil = Time.time + 2.2f;
            nextPuke = Time.time + Random.Range(25f, 50f);
            view.UpdateLocomotion(0f);
            var head = transform.position + Vector3.up * 1.15f + transform.forward * 0.45f;
            if (SpellFx.Ready)
                SpellFx.Emit(new SpellFx.P
                {
                    Rate = 70, Duration = 1.3f, Life = new Vector2(0.4f, 0.7f), Speed = new Vector2(0.2f, 0.6f), Size = new Vector2(0.05f, 0.11f),
                    Start = new Color(0.62f, 0.68f, 0.22f, 1f), End = new Color(0.5f, 0.45f, 0.2f, 0.6f), Gravity = 1.4f,
                    Velocity = transform.forward * 1.1f + Vector3.down * 0.4f, Radius = 0.05f, Max = 120,
                }, head);
            Sfx.Play("splash", head, 0.45f, 0.1f, 22f);
            StartCoroutine(Puddle(transform.position + transform.forward * 0.85f));
        }

        System.Collections.IEnumerator Puddle(Vector3 at)
        {
            yield return new WaitForSeconds(0.7f);
            if (this == null) yield break;
            var p = Factory.PrimAt(PrimitiveType.Cylinder, transform.parent, at + Vector3.up * 0.012f, new Vector3(Random.Range(0.5f, 0.75f), 0.006f, Random.Range(0.4f, 0.6f)), new Color(0.55f, 0.58f, 0.2f));
            p.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Destroy(p, 90f);
        }
    }
}
