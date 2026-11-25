using System.Collections.Generic;

/// <summary>
/// Accepted orders that are waiting for a drink. Guests add and remove their orders,
/// the bar screen reads the list. Logic only – no UI.
/// </summary>
public static class OrderBook
{
	public static readonly List<GuestOrder> Open = new List<GuestOrder>();
}
