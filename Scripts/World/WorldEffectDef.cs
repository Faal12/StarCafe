using System.Collections.Generic;

/// <summary>
/// One consequence of an event outcome in the tavern (JSON: "world": [ ... ]).
/// Types:
///   "prop"       – an object appears at a spot (e.g. broken chair, candles, a bullet hole)
///   "character"  – a figure appears and stays (e.g. a nomad child, a test robot)
///   "animation"  – a one-off effect right after the event: shake, flash, blackout, confetti
///   "flag"       – sets a story flag; other events can require or forbid it (follow-ups)
///   "remove"     – removes a prop or character by id
///   "removeFlag" – clears a story flag
/// Without a texture, props and characters show as labelled placeholders.
/// </summary>
public sealed class WorldEffectDef
{
	public static readonly HashSet<string> Types = new HashSet<string> { "prop", "character", "animation", "flag", "remove", "removeFlag" };

	public static readonly HashSet<string> Animations = new HashSet<string> { "shake", "flash", "blackout", "confetti" };

	public static readonly HashSet<string> Appears = new HashSet<string> { "fade", "drop", "pop", "none" };

	public string Type { get; set; } = "prop";

	/// <summary>Unique id of the prop/character/flag (e.g. "kaputter_stuhl").</summary>
	public string Id { get; set; } = "";

	/// <summary>Named spot in the tavern (see WorldSpots in Tavern.tscn), e.g. "Tresen", "Tisch3", "Schleuse".</summary>
	public string Spot { get; set; } = "";

	/// <summary>Offset from the spot in pixels, to place several things at one spot.</summary>
	public float OffsetX { get; set; }

	public float OffsetY { get; set; }

	/// <summary>Exact world position instead of a spot (optional; used e.g. for spilled drinks).</summary>
	public float? X { get; set; }

	public float? Y { get; set; }

	/// <summary>Optional image (res://…png). Empty = placeholder.</summary>
	public string Texture { get; set; } = "";

	/// <summary>Name shown on the placeholder and in the event summary.</summary>
	public string Label { get; set; } = "";

	/// <summary>Faction colour for character placeholders (optional).</summary>
	public string Faction { get; set; } = "";

	/// <summary>Placeholder/flash colour as hex, e.g. "#ff4040" (optional).</summary>
	public string Color { get; set; } = "";

	public float Scale { get; set; } = 1f;

	/// <summary>How many weeks it stays (0 = permanent).</summary>
	public int Weeks { get; set; }

	/// <summary>Entrance for props/characters: "fade" (default), "drop", "pop", "none".</summary>
	public string Appear { get; set; } = "fade";

	/// <summary>For type "animation": "shake", "flash", "blackout" or "confetti".</summary>
	public string Animation { get; set; } = "";

	public bool IsThing => Type == "prop" || Type == "character";
}
