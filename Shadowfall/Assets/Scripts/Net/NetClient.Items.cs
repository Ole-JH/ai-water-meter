using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Items and gold live on the server. The client asks (<see cref="Op"/>), the server checks, carries it out and
    /// answers with the whole inventory ("inv"), plus "iok" / "ierr" for the messages and effects. Loot is rolled by
    /// the server for each player ("drops", and the drops in "kill") and only that player sees it.
    /// </summary>
    public partial class NetClient
    {
        static readonly Color ItemErrorColor = new Color(1f, 0.4f, 0.4f);
        static readonly Color GoldColor = new Color(1f, 0.85f, 0.2f);

        /// <summary>Sends an item action. Unused arguments are ignored by the server.</summary>
        public void Op(string op, int i = 0, int j = 0, int slot = 0, int n = 0, int id = 0, string k = null, string name = null, string to = null)
        {
            if (State == ConnState.InWorld) Send(new IopMsg { op = op, i = i, j = j, slot = slot, n = n, id = id, k = k, name = name, to = to });
        }

        void HandleItems(NetMsg m)
        {
            var p = Player.I;
            switch (m.t)
            {
                case "inv":
                    p?.ApplyLedger(m.gold, m.bag, m.stash, m.eq, m.comp);
                    break;
                case "drops":
                    SpawnDrops(m.drops);
                    break;
                case "stock":
                    VendorStock.OnStock(m.k, m.stock, m.restock);
                    break;
                case "iok":
                    SpawnDrops(m.drops); // what didn't fit in the bags
                    if (p != null) ItemOk(p, m);
                    break;
                case "ierr":
                    if (m.op == "pickup") LootDrop.Refused(m.id, m.msg, m.n);
                    else if (m.op == "quest" && !string.IsNullOrEmpty(m.k) && p != null) p.Quests.MarkDone(m.k);
                    if (!string.IsNullOrEmpty(m.msg) && m.op != "pickup") GameUI.Log(m.msg, ItemErrorColor);
                    break;
            }
        }

        /// <summary>Loot the server dropped for us (positions are already in client space).</summary>
        void SpawnDrops(NetDrop[] drops)
        {
            if (drops == null || Player.I == null) return;
            foreach (var d in drops)
            {
                var item = d.item != null && !string.IsNullOrEmpty(d.item.Name) ? d.item : null;
                if (item == null && d.gold <= 0) continue;
                LootDrop.Spawn(new Vector3(d.x, 0f, d.z), item, d.gold, d.id);
            }
        }

        void ItemOk(Player p, NetMsg m)
        {
            switch (m.op)
            {
                case "pickup":
                    LootDrop.PickedUp(m.id);
                    break;
                case "sell":
                    Sfx.Play2D("coins", 0.5f);
                    GameUI.Log("Sold " + m.name + (m.n > 1 ? " x" + m.n : "") + " for " + m.gold + " gold.", GoldColor);
                    break;
                case "sellcommon":
                    if (m.n == 0) { GameUI.Log("You have nothing common to sell.", Color.gray); break; }
                    Sfx.Play2D("coins", 0.5f);
                    GameUI.Log("Sold " + m.n + " item" + (m.n == 1 ? "" : "s") + " for " + m.gold + " gold.", GoldColor);
                    break;
                case "buy":
                    Sfx.Play2D("coins", 0.5f);
                    GameUI.Log("Bought " + m.name + (m.n > 1 ? " x" + m.n : "") + " for " + m.gold + " gold.", GoldColor);
                    break;
                case "fuse":
                    Sfx.Play2D("anvil", 0.6f);
                    GameUI.Log("Vex fuses three gems into a " + m.name + " (" + m.gold + " gold).", GoldColor);
                    break;
                case "socket":
                    p.Achievements.Add("sockets");
                    Sfx.Play2D("anvil", 0.5f, 1.3f);
                    GameUI.Log("You socket the " + m.name + " into " + m.target + ".", new Color(0.7f, 0.85f, 1f));
                    break;
                case "craft":
                    Recipe.Find(m.k)?.Crafted(p, m.name, (Rarity)m.rarity, m.burnt);
                    break;
                case "quest":
                    p.Quests.TurnedIn(m.k, p, m.item, (Rarity)m.rarity);
                    break;
                case "hire":
                {
                    var def = CompanionDef.Get(m.k);
                    if (def == null) break;
                    if (!p.OwnedCompanions.Contains(def.Id)) p.OwnedCompanions.Add(def.Id); // the inventory update follows
                    Sfx.Play2D("coins", 0.6f);
                    GameUI.Log(def.Name + " joins you!", def.Color);
                    p.SummonCompanion(def.Id);
                    SaveNow();
                    break;
                }
                case "respec":
                    p.ResetTalents();
                    GameUI.Log("Your talents have been reset (" + m.gold + " gold).", new Color(0.8f, 0.6f, 1f));
                    break;
                case "death":
                    GameUI.Log("You lost " + m.gold + " gold.", new Color(1f, 0.6f, 0.3f));
                    break;
            }
        }
    }
}
