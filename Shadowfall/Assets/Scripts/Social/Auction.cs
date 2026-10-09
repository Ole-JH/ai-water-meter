using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>One item for sale at the auction house (server/auction.js); left = minutes until it goes back.</summary>
    [System.Serializable]
    public class AuctionListing { public int id, price, left; public string seller; public Item item; }

    /// <summary>The auction house as the server last showed it: our listings first (Mine of them), then the search hits.</summary>
    public static class Auction
    {
        public static readonly List<AuctionListing> Listings = new List<AuctionListing>();
        public static int Mine { get; private set; }
        public static string Query { get; private set; } = "";
        public static bool Loaded { get; private set; }

        public static void Set(NetMsg m)
        {
            Listings.Clear();
            foreach (var json in m.items ?? new string[0])
            {
                try { var l = JsonUtility.FromJson<AuctionListing>(json); if (l != null && l.item != null && !string.IsNullOrEmpty(l.item.Name)) Listings.Add(l); }
                catch (System.Exception) { }
            }
            Mine = Mathf.Min(m.n, Listings.Count);
            Query = m.k ?? "";
            Loaded = true;
        }

        public static void Browse(string q) => NetClient.I?.Op("aubrowse", k: q ?? "");
    }
}
