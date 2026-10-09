using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Player-to-player trading. An offer is bag slots plus gold: the items stay in the bags (marked) until both
    /// accept, then the server checks everything is still there and swaps it all at once.
    /// </summary>
    public partial class NetClient
    {
        public const int TradeSlots = 12;

        public Offer TradeInvite { get; private set; }
        public bool Trading { get; private set; }
        public string TradePartner { get; private set; }
        /// <summary>The bag slots we offer (as the server confirmed them).</summary>
        public readonly List<int> MySlots = new List<int>();
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

        void SendOffer(List<int> slots, int gold)
        {
            MyOk = TheirOk = false;
            if (State == ConnState.InWorld) Send(new TradeCmd { t = "toffer", slots = slots.ToArray(), gold = gold });
        }

        public bool IsOffered(int bagIndex) => Trading && MySlots.Contains(bagIndex);

        /// <summary>What we offer in trade slot <paramref name="i"/> (null = empty).</summary>
        public Item MyOfferItem(int i)
        {
            var p = Player.I;
            return p != null && i < MySlots.Count ? p.Inventory.Slots[MySlots[i]] : null;
        }

        /// <summary>Offers a stack from the bags.</summary>
        public void OfferItem(int bagIndex)
        {
            var p = Player.I;
            if (!Trading || p == null || MySlots.Count >= TradeSlots || MySlots.Contains(bagIndex) || p.Inventory.Slots[bagIndex] == null) return;
            Sfx.Play2D("ui_click", 0.4f);
            SendOffer(new List<int>(MySlots) { bagIndex }, MyGold);
        }

        public void RetractItem(int offerIndex)
        {
            if (!Trading || offerIndex < 0 || offerIndex >= MySlots.Count) return;
            var slots = new List<int>(MySlots);
            slots.RemoveAt(offerIndex);
            SendOffer(slots, MyGold);
        }

        public void SetTradeGold(int amount)
        {
            var p = Player.I;
            if (!Trading || p == null) return;
            SendOffer(MySlots, Mathf.Clamp(amount, 0, p.Gold));
        }

        public void AcceptTrade()
        {
            var p = Player.I;
            if (!Trading || p == null || MyOk) return;
            int need = 0;
            foreach (var it in TheirOffer) if (p.Inventory.IndexOf(it.Name) < 0 || !it.Stackable) need++;
            if (p.Inventory.FreeSlots + MySlots.Count < need) { GameUI.Log("You need " + need + " free bag slots for this trade.", new Color(1f, 0.4f, 0.4f)); return; }
            MyOk = true;
            TradeSend("tok");
            Sfx.Play2D("ui_confirm", 0.5f);
        }

        public void CancelTrade()
        {
            if (!Trading) return;
            TradeSend("tcancel");
            DropTrade();
        }

        void DropTrade()
        {
            MySlots.Clear();
            TheirOffer.Clear();
            MyGold = TheirGold = 0;
            MyOk = TheirOk = false;
            Trading = false;
        }

        void CloseTrade(string msg)
        {
            DropTrade();
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
                case "tmine": // our offer, as the server took it
                    MySlots.Clear();
                    if (m.slots != null) MySlots.AddRange(m.slots);
                    MyGold = Mathf.Max(0, m.gold);
                    MyOk = TheirOk = false;
                    break;
                case "tok":
                    TheirOk = true;
                    break;
                case "tdone": // the server already moved everything (an "inv" follows)
                    CloseTrade("Trade with " + m.name + " complete.");
                    Player.I?.Achievements.Add("trades");
                    Sfx.Play2D("coins", 0.6f);
                    SaveNow();
                    break;
                case "tclose":
                    CloseTrade(m.msg ?? "The trade was cancelled.");
                    break;
            }
        }
    }
}
