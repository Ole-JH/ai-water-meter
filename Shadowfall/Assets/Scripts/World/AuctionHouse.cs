using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The auction house in the world (the window is GameUI.Auction, the trade server/auction.js): an auctioneer at a
    /// podium with a bell beside every town's general merchant, who calls out lots, bangs the gavel and rings the bell
    /// when something sells near by ("ausold"); and the courier who runs up to you with what the auction house owes you
    /// ("aumail": gold from a sale, an unsold item coming back). Built after the world: visual only, never in the map.
    /// </summary>
    public class AuctionPodium : Interactable
    {
        public static readonly List<AuctionPodium> Podiums = new List<AuctionPodium>();

        static readonly string[] Calls =
        {
            "Lot the next! Fine goods from the far wilds!",
            "Do I hear a bid? Anyone? Gold speaks louder than silence!",
            "Going once... going twice...",
            "Everything sells in the end, friend. Everything.",
            "Put it up for sale: the house only takes five in the hundred!",
        };

        CharacterView view;
        Transform bell;
        float nextCall, ringT = -1f;

        public override string HoverText => "Auctioneer\n<the auction house: buy, and sell your finds>";
        public override Color LabelColor => new Color(1f, 0.85f, 0.4f);
        public override float LabelHeight => 2.9f;

        public static void BuildAll()
        {
            if (Podiums.Count > 0) return;
            var grid = WorldGrid.Instance;
            foreach (var i in new List<Interactable>(Interactable.All))
            {
                if (!(i is Npc n) || n.Shop == null || n.Shop.Kind != VendorKind.General) continue;
                var c = n.transform.position;
                foreach (var d in new[] { Vector3.left, Vector3.right, Vector3.back, Vector3.forward })
                {
                    var at = c + d * 2.2f;
                    if (!grid.IsWalkable(at) || !grid.IsWalkable(at + d * 0.8f) || Crowded(at, n)) continue;
                    Make(at, d);
                    break;
                }
            }
        }

        static bool Crowded(Vector3 p, Npc self)
        {
            foreach (var i in Interactable.All)
                if (i != self && Factory.FlatDistance(i.Position, p) < 1.6f) return true;
            foreach (var f in ForgeStation.All) if (Factory.FlatDistance(f.transform.position, p) < 2.5f) return true;
            return false;
        }

        static void Make(Vector3 at, Vector3 away)
        {
            var go = new GameObject("Auctioneer");
            go.transform.position = at;
            go.transform.rotation = Quaternion.LookRotation(-away); // the podium faces back toward the merchant's stall front
            var a = go.AddComponent<AuctionPodium>();
            a.DisplayName = "Auctioneer";
            a.InteractRange = 2.4f;
            var wood = new Color(0.45f, 0.3f, 0.18f);
            // the podium, in front of the auctioneer
            var front = go.transform.forward * 0.6f;
            var pod = Factory.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.7f, 1.05f, 0.45f), wood);
            pod.transform.position = at + front + Vector3.up * 0.52f;
            var top = Factory.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.8f, 0.06f, 0.55f), wood * 0.8f);
            top.transform.position = at + front + Vector3.up * 1.08f;
            top.transform.rotation = go.transform.rotation * Quaternion.Euler(-12f, 0f, 0f);
            var cloth = Factory.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.5f, 0.6f, 0.02f), new Color(0.62f, 0.12f, 0.12f));
            cloth.transform.position = at + front * 1.39f + Vector3.up * 0.6f;
            cloth.transform.rotation = go.transform.rotation;
            // the bell on a post beside it
            var side = go.transform.right * 0.85f;
            var post = Factory.Prim(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(0.07f, 0.9f, 0.07f), wood * 0.8f);
            post.transform.position = at + side + Vector3.up * 0.9f;
            var arm = Factory.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.05f, 0.05f, 0.4f), wood * 0.8f);
            arm.transform.position = at + side + Vector3.up * 1.78f + go.transform.forward * 0.15f;
            arm.transform.rotation = go.transform.rotation;
            a.bell = new GameObject("Bell").transform;
            a.bell.SetParent(go.transform, false);
            a.bell.position = at + side + Vector3.up * 1.75f + go.transform.forward * 0.32f;
            var brass = new Color(0.85f, 0.65f, 0.25f);
            Factory.Prim(PrimitiveType.Cylinder, a.bell, new Vector3(0f, -0.12f, 0f), new Vector3(0.2f, 0.1f, 0.2f), brass, false, Mat.Glow(brass * 0.35f));
            // the auctioneer: a man in a merchant's red, with a gavel
            a.view = CharacterView.Create(go.transform, new CharacterLook { Model = "Characters/Rogue", Height = 1.9f, Tint = new Color(1.1f, 0.8f, 0.75f), Weapon = "mace" });
            if (a.view == null)
                HumanoidModel.Build(go.transform, 1f, new Color(0.9f, 0.75f, 0.6f), new Color(0.6f, 0.15f, 0.15f), Color.gray, Color.gray, true, false);
            a.AddClickCollider(0.6f, 2.2f);
            a.nextCall = Time.time + Random.Range(10f, 25f);
            Podiums.Add(a);
        }

        /// <summary>The podium nearest <paramref name="p"/> within <paramref name="range"/>.</summary>
        public static AuctionPodium Near(Vector3 p, float range = 40f)
        {
            AuctionPodium best = null;
            foreach (var a in Podiums)
            {
                float d = Factory.FlatDistance(a.transform.position, p);
                if (d < range) { range = d; best = a; }
            }
            return best;
        }

        /// <summary>A sale: the gavel, the bell, and the lot called out.</summary>
        public void Sold(string item, int gold)
        {
            view?.Action("1H_Melee_Attack_Chop", 1f);
            ringT = 0f;
            Sfx.Play("bell", bell.position, 0.8f, 0.05f, 45f);
            Sfx.Play("hit_heavy", transform.position + Vector3.up, 0.3f, 0.1f, 20f);
            Speech.Say(transform, 2.4f, "Sold! " + item + ", for " + gold + " gold!");
            SpellFx.Hit(bell.position, new Color(1f, 0.85f, 0.4f), false, 10);
        }

        void Update()
        {
            view?.UpdateLocomotion(0f);
            if (ringT >= 0f && bell != null)
            {
                ringT += Time.deltaTime;
                bell.localRotation = Quaternion.Euler(Mathf.Sin(ringT * 22f) * 25f * Mathf.Max(0f, 1f - ringT / 1.4f), 0f, 0f);
                if (ringT > 1.4f) { ringT = -1f; bell.localRotation = Quaternion.identity; }
            }
            if (Time.time < nextCall) return;
            nextCall = Time.time + Random.Range(25f, 50f);
            var p = Player.I;
            if (p == null || Factory.FlatDistance(p.transform.position, transform.position) > 18f) return;
            Speech.Say(transform, 2.4f, Calls[Random.Range(0, Calls.Length)]);
            view?.Action("1H_Melee_Attack_Chop", 1f);
            Sfx.Play("hit_heavy", transform.position + Vector3.up, 0.2f, 0.1f, 18f);
        }

        public override void Interact(Player p)
        {
            Speech.Say(transform, 2.4f, "Welcome, welcome! Buying or selling?");
            view?.Interact();
            GameUI.I?.OpenAuction();
        }
    }

    /// <summary>The auction house's courier: runs up with gold or an unsold item, hands it over, and runs off.</summary>
    public class AuctionCourier : MonoBehaviour
    {
        enum Step { Running, Handing, Leaving }

        CharacterView view;
        Step step;
        float until, speed = 6.5f;
        Vector3 leaveTo;
        string note;
        int gold, items;

        /// <summary>The server delivered (gold and items are already ours): the courier shows it.</summary>
        public static void Deliver(NetMsg m)
        {
            var p = Player.I;
            int gold = m.gold, items = m.items != null ? m.items.Length : 0;
            string note = string.IsNullOrEmpty(m.k) ? "Post from the auction house!" : m.k;
            if (p == null || Dungeon.Active || p.IsDead) { Sfx.Play2D("coins", 0.5f); return; } // no courier goes in there
            // he comes from somewhere a little way off that's open ground
            var grid = WorldGrid.Instance;
            Vector3 from = p.transform.position;
            for (int i = 0; i < 12; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                var c = p.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 13f;
                if (grid.IsWalkable(c) && grid.LineOfSight(c, p.transform.position)) { from = c; break; }
            }
            var go = new GameObject("AuctionCourier");
            go.transform.position = from;
            var cr = go.AddComponent<AuctionCourier>();
            cr.view = CharacterView.Create(go.transform, new CharacterLook { Model = "Characters/Rogue", Height = 1.75f, Tint = new Color(0.9f, 1f, 0.9f) });
            if (cr.view == null) HumanoidModel.Build(go.transform, 0.9f, new Color(0.9f, 0.75f, 0.6f), new Color(0.3f, 0.5f, 0.3f), Color.gray, Color.gray, false, false);
            // his satchel
            var bag = Factory.Prim(PrimitiveType.Cube, go.transform, new Vector3(0.28f, 0.95f, -0.05f), new Vector3(0.14f, 0.32f, 0.3f), new Color(0.45f, 0.3f, 0.18f));
            bag.name = "Satchel";
            cr.note = note;
            cr.gold = gold;
            cr.items = items;
            cr.leaveTo = from;
            Sfx.Play("step_grass", from, 0.4f, 0.1f, 25f);
        }

        void Update()
        {
            var p = Player.I;
            float dt = Time.deltaTime;
            if (p == null) { Destroy(gameObject); return; }
            switch (step)
            {
                case Step.Running:
                {
                    var to = Factory.Flat(p.transform.position - transform.position);
                    if (to.magnitude <= 1.3f)
                    {
                        step = Step.Handing;
                        until = Time.time + 1.6f;
                        Factory.Face(transform, p.transform.position);
                        view?.Interact();
                        Speech.Say(transform, 2.2f, note);
                        Sfx.Play2D(gold > 0 ? "coins" : "drop", 0.7f);
                        var hand = transform.position + transform.forward * 0.5f + Vector3.up * 1.1f;
                        SpellFx.Hit(hand, new Color(1f, 0.85f, 0.35f), false, 12);
                        if (gold > 0) GameUI.Float(p.transform.position + Vector3.up * 2.4f, "+" + gold + " gold", new Color(1f, 0.85f, 0.3f), 1.3f);
                        if (items > 0) GameUI.Float(p.transform.position + Vector3.up * 2.9f, items == 1 ? "An item comes back" : items + " items come back", new Color(0.8f, 0.9f, 1f), 1f);
                        break;
                    }
                    transform.position += to.normalized * speed * dt;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), dt * 10f);
                    view?.UpdateLocomotion(speed);
                    if (Time.time > until + 12f && until > 0f) Destroy(gameObject); // couldn't catch them
                    if (until == 0f) until = Time.time;
                    break;
                }
                case Step.Handing:
                    view?.UpdateLocomotion(0f);
                    if (Time.time < until) break;
                    step = Step.Leaving;
                    until = Time.time + 2.5f;
                    view?.Cheer();
                    break;
                case Step.Leaving:
                {
                    var to = Factory.Flat(leaveTo - transform.position);
                    if (to.sqrMagnitude > 0.04f)
                    {
                        transform.position += to.normalized * speed * dt;
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), dt * 10f);
                        view?.UpdateLocomotion(speed);
                    }
                    float left = until - Time.time;
                    if (left < 0.6f) transform.localScale = Vector3.one * Mathf.Max(0.01f, left / 0.6f);
                    if (left <= 0f) Destroy(gameObject);
                    break;
                }
            }
        }
    }
}
