/// <summary>
/// PLATZHALTER für die Wirtschaft – bewusst noch nicht aktiv, damit nichts vergessen wird.
/// Jede Stelle, an der später Geld fließen soll, ruft schon hier an; die Preise sind 0
/// bzw. die Schalter aus. Zum Aktivieren: Schalter umlegen und Preise setzen.
///
/// Offen (siehe Handlung.md, Kapitel 12):
/// - Einkauf von Zutaten (Preis pro Portion steht schon in Data/Bar/ingredients.json, "price")
/// - Reparatur von zerbrochenen Möbeln (Folgen von Schlägereien, "world"-Props)
/// - Bestechung (Täuschen/Einschüchtern, Phase 3)
/// - Strafen der Sternenwacht (Fahndung, Phase 4)
/// - Miete/Liegegebühr am Kreuzweg, Löhne für Personal (Akt II)
/// - Verkauf von Gegenständen (Wert steht in Data/Items/items.json, "value")
/// </summary>
public static class Economy
{
	/// <summary>Zutaten kosten Geld beim Mixen. Noch aus.</summary>
	public const bool IngredientCostsActive = false;

	/// <summary>Wöchentliche Kosten (Liegegebühr) beim Schichtende. Noch aus.</summary>
	public const bool WeeklyCostsActive = false;

	public const int WeeklyDockingFee = 0;

	/// <summary>Kosten für das Mixen einer Zutat (0, solange nicht aktiv).</summary>
	public static int IngredientCost(IngredientDef ingredient)
	{
		return IngredientCostsActive && ingredient != null ? ingredient.Price : 0;
	}

	/// <summary>Reparaturkosten für einen zerstörten Gegenstand. Platzhalter.</summary>
	public static int RepairCost(string propId)
	{
		return 0;
	}

	/// <summary>Bestechungssumme für eine Fraktion. Platzhalter (Phase 3).</summary>
	public static int BribeCost(Faction faction)
	{
		return 0;
	}

	/// <summary>Strafe der Sternenwacht je Fahndungsstufe. Platzhalter (Phase 4).</summary>
	public static int FineFor(int wantedLevel)
	{
		return 0;
	}

	/// <summary>Wöchentliche Kosten am Schichtende. Platzhalter.</summary>
	public static void ChargeWeekly()
	{
		if (WeeklyCostsActive && WeeklyDockingFee > 0)
		{
			PlayerState.AddCredits(-WeeklyDockingFee, "Liegegebühr am Kreuzweg");
		}
	}
}
