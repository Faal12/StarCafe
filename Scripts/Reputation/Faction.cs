using System;

public enum Faction
{
	Imperium,
	Rebellen,
	Soeldner,
	Silberkiel,
	GraueHaende,
	Sternenwacht,
	Funkenwerkstatt,
	Weitwanderer
}

public static class FactionInfo
{
	public static readonly Faction[] All = (Faction[])Enum.GetValues(typeof(Faction));

	public static string DisplayName(Faction faction) => faction switch
	{
		Faction.Imperium => "Das Imperium",
		Faction.Rebellen => "Die Rebellen",
		Faction.Soeldner => "Die Söldner",
		Faction.Silberkiel => "Silberkiel-Gilde",
		Faction.GraueHaende => "Die Grauen Hände",
		Faction.Sternenwacht => "Die Sternenwacht",
		Faction.Funkenwerkstatt => "Die Funkenwerkstatt",
		Faction.Weitwanderer => "Die Weitwanderer",
		_ => faction.ToString()
	};

	public static string KeyFigure(Faction faction) => faction switch
	{
		Faction.Imperium => "Vesna Arkan",
		Faction.Rebellen => "Tarn Weller",
		Faction.Soeldner => "Rorik Dahl",
		Faction.Silberkiel => "Odile Kraal",
		Faction.GraueHaende => "Madame Quill",
		Faction.Sternenwacht => "Gerd Holm",
		Faction.Funkenwerkstatt => "Pixi Osk",
		Faction.Weitwanderer => "Nahra Toll",
		_ => faction.ToString()
	};

	public static bool IsGreatPower(Faction faction) =>
		faction == Faction.Imperium || faction == Faction.Rebellen;

	public static Faction? Rival(Faction faction) => faction switch
	{
		Faction.Imperium => Faction.Rebellen,
		Faction.Rebellen => Faction.Imperium,
		_ => null
	};
}
