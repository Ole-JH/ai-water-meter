using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Player-to-player trading. Offered items leave the bags into an escrow while the window is open
    /// (they come back on cancel); the server relays both offers and finishes the trade when both accept.
    /// </summary>
    public partial class NetClient
    {
        public const int TradeSlots = 12;

        public Offer TradeInvite { get; private set; }
        public bool Trading { get; private set; }
        public string TradePartner { get; private set; }
        public readonly List<Item> MyOffer = new List<Item>();
        public readonly List<Item> TheirOffer = new List<Item>();
        public int MyGold { get; private set; }
        public int TheirGold { get; private set; }
        public bool MyOk { get; private set; }
        public bool TheirOk { get; private set; }

        void TradeSend(string t, int id = 0)
        {
            if (State == ConnState.InWorld) Send(new TradeCmd { t = t, id = id });
        }

        public void RequestTrade(int playerId) => TradeSend("treq", playerId);

        public void AnswerTradeInvite(bool accept)
        {
            TradeSend(accept ? "tacc" : "tdecl");
            TradeInvite = null;
        }

        void SendOffer()
        {
            MyOk = TheirOk = false;
            var items = new string[MyOffer.Count];
            for (int i = 0; i < items.Length; i++) items[i] = JsonUtility.ToJson(MyOffer[i]);
            if (State == ConnState.InWorld) Send(new TradeCmd { t = "toffer", items = items, gold = MyGold });
        }

        /// <summary>Moves a stack from the bags into the trade.</summary>
        public void OfferItem(int bagIndex)
        {
            var p = Player.I;
            if (!Trading || p == null || MyOffer.Count >= TradeSlots) return;
            var item = p.Inventory.TakeAll(bagIndex);
            if (item == null) return;
            MyOffer.Add(item);
            Sfx.Play2D("ui_click", 0.4f);
            SendOffer();
        }

        public void RetractItem(int offerIndex)
        {
            var p = Player.I;
            if (!Trading || p == null || offerIndex < 0 || offerIndex >= MyOffer.Count) return;
            if (!p.Inventory.Add(MyOffer[offerIndex])) { GameUI.Log("Your bags are full.", new Color(1f, 0.4f, 0.4f)); return; }
            MyOffer.RemoveAt(offerIndex);
            SendOffer();
        }

        public void SetTradeGold(int amount)
        {
            var p = Player.I;
            if (!Trading || p == null) return;
            amount = Mathf.Clamp(amount, 0, p.Gold + MyGold);
            p.Gold += MyGold - amount;
            MyGold = amount;
            SendOffer();
        }

        public void AcceptTrade()
        {
            var p = Player.I;
            if (!Trading || p == null || MyOk) return;
            int need = 0;
            foreach (var it in TheirOffer) if (p.Inventory.IndexOf(it.Name) < 0 || !it.Stackable) need++;
            if (p.Inventory.FreeSlots < need) { GameUI.Log("You need " + need + " free bag slots for this trade.", new Color(1f, 0.4f, 0.4f)); return; }
            MyOk = true;
            TradeSend("tok");
            Sfx.Play2D("ui_confirm", 0.5f);
        }

        public void CancelTrade()
        {
            if (!Trading) return;
            TradeSend("tcancel");
            CloseTrade(null);
        }

        /// <summary>Items and gold sitting in the trade window (still ours, counted in saves).</summary>
        public int EscrowGold => Trading ? MyGold : 0;
        public IList<Item> EscrowItems => MyOffer;

        void DropTrade()
        {
            MyOffer.Clear();
            TheirOffer.Clear();
            MyGold = TheirGold = 0;
            MyOk = TheirOk = false;
            Trading = false;
        }

        void CloseTrade(string msg)
        {
            var p = Player.I;
            if (p != null)
            {
                foreach (var it in MyOffer)
                    if (!p.Inventory.Add(it)) LootDrop.Spawn(p.transform.position, it, 0); // bags filled up meanwhile
                p.Gold += MyGold;
            }
            MyOffer.Clear();
            TheirOffer.Clear();
            MyGold = TheirGold = 0;
            MyOk = TheirOk = false;
            Trading = false;
            if (msg != null) GameUI.Log(msg, Color.gray);
        }

        void HandleTrade(NetMsg m)
        {
            switch (m.t)
            {
                case "tinv":
                    TradeInvite = new Offer { From = m.id, Name = m.name, Time = Time.time };
                    GameUI.Log(m.name + " wants to trade with you.", PartyColor);
                    Sfx.Play2D("ui_open", 0.4f);
                    break;
                case "topen":
                    CloseTrade(null);
                    Trading = true;
                    TradePartner = m.name;
                    GameUI.Log("Trading with " + m.name + ". Right-click items in your bags to offer them.", PartyColor);
                    Sfx.Play2D("ui_open", 0.5f);
                    break;
                case "tupd":
                    TheirOffer.Clear();
                    if (m.items != null)
                        foreach (var json in m.items)
                        {
                            try
                            {
                                var it = JsonUtility.FromJson<Item>(json);
                                if (it != null && !string.IsNullOrEmpty(it.Name)) TheirOffer.Add(it);
                            }
                            catch (System.Exception) { /* ignore a malformed item */ }
                        }
                    TheirGold = Mathf.Max(0, m.gold);
                    MyOk = TheirOk = false;
                    break;
                case "tok":
                    TheirOk = true;
                    break;
                case "tdone":
                {
                    var p = Player.I;
                    MyOffer.Clear(); // given away
                    MyGold = 0;
                    if (p != null)
                    {
                        if (m.items != null)
                            foreach (var json in m.items)
                            {
                                Item it = null;
                                try { it = JsonUtility.FromJson<Item>(json); } catch (System.Exception) { }
                                if (it == null || string.IsNullOrEmpty(it.Name)) continue;
                                if (!p.Inventory.Add(it)) LootDrop.Spawn(p.transform.position, it, 0);
                            }
                        p.Gold += Mathf.Max(0, m.gold);
                    }
                    CloseTrade("Trade with " + m.name + " complete.");
                    Sfx.Play2D("coins", 0.6f);
                    SaveNow();
                    break;
                }
                case "tclose":
                    CloseTrade(m.msg ?? "The trade was cancelled.");
                    break;
            }
        }
    }
}
