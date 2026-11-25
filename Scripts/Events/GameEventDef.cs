using System.Collections.Generic;

// Data model for events. Filled from res://Data/Events/*.json (see Data/Events/README.md).
// Property names map to camelCase JSON keys (case-insensitive).
//
// An event is a conversation: the player sees the possible outcomes but has to bring one
// about by talking to the speaker. The AI plays the speaker and decides when an outcome
// is reached (offline: keywords decide).

/// <summary>Root of an event file: { "events": [ ... ] }.</summary>
public sealed class GameEventFile
{
	public List<GameEventDef> Events { get; set; } = new List<GameEventDef>();
}

/// <summary>One event (conflict or happening) the player resolves in conversation.</summary>
public sealed class GameEventDef
{
	/// <summary>Unique, never changes once used in saves (e.g. "zwei_agenten").</summary>
	public string Id { get; set; } = "";

	/// <summary>"Konflikt" (between factions) or "Ereignis" (everything else).</summary>
	public string Kind { get; set; } = "Ereignis";

	public string Title { get; set; } = "";

	/// <summary>Who the player talks to. Placeholder until real characters exist.</summary>
	public EventSpeaker Speaker { get; set; } = new EventSpeaker();

	/// <summary>The situation, shown to the player and given to the AI.</summary>
	public string Text { get; set; } = "";

	/// <summary>First line the speaker says to the player.</summary>
	public string Opening { get; set; } = "";

	/// <summary>Optional aside from Floki at the start (shown in *asterisks*).</summary>
	public string Floki { get; set; }

	/// <summary>Player messages before the speaker must decide (last round forces an outcome).</summary>
	public int MaxTurns { get; set; } = 6;

	/// <summary>Speaker lines used in offline mode when no outcome was reached yet.</summary>
	public List<string> OfflineLines { get; set; } = new List<string>();

	/// <summary>Relative chance compared to other eligible events.</summary>
	public int Weight { get; set; } = 1;

	/// <summary>Happens at most once per playthrough.</summary>
	public bool Once { get; set; }

	/// <summary>Weeks that must pass before it can happen again.</summary>
	public int CooldownWeeks { get; set; } = 3;

	public int MinWeek { get; set; } = 1;

	/// <summary>0 = no upper limit.</summary>
	public int MaxWeek { get; set; }

	/// <summary>"egal" (default), "nur" (only during a conjunction) or "nie" (never during one).</summary>
	public string Conjunction { get; set; } = "egal";

	/// <summary>Faction name -> minimum standing, e.g. { "Rebellen": 20 }.</summary>
	public Dictionary<string, int> MinReputation { get; set; } = new Dictionary<string, int>();

	/// <summary>Faction name -> maximum standing.</summary>
	public Dictionary<string, int> MaxReputation { get; set; } = new Dictionary<string, int>();

	public int MinGastrecht { get; set; }

	public int MaxGastrecht { get; set; } = 100;

	/// <summary>Factions whose key figure must still be alive.</summary>
	public List<string> RequiresAlive { get; set; } = new List<string>();

	/// <summary>Story flags that must be set (by an earlier outcome's "world" effects) – for follow-up events.</summary>
	public List<string> RequiresFlags { get; set; } = new List<string>();

	/// <summary>Story flags that prevent this event.</summary>
	public List<string> BlockedByFlags { get; set; } = new List<string>();

	/// <summary>Possible endings. Exactly one has "default": true (walking away / no decision).</summary>
	public List<EventOutcomeDef> Outcomes { get; set; } = new List<EventOutcomeDef>();

	public bool IsConflict => string.Equals(Kind, "Konflikt", System.StringComparison.OrdinalIgnoreCase);

	public EventOutcomeDef DefaultOutcome => Outcomes.Find(o => o.Default) ?? Outcomes[^1];

	public EventOutcomeDef FindOutcome(string id)
	{
		return Outcomes.Find(o => string.Equals(o.Id, id?.Trim(), System.StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>Outcomes that can be reached in the conversation (not the task results).</summary>
	public List<EventOutcomeDef> TalkOutcomes => Outcomes.FindAll(o => !o.TaskOnly);
}

public sealed class EventSpeaker
{
	/// <summary>Display name, e.g. "Imperialer Offizier".</summary>
	public string Name { get; set; } = "Unbekannter Gast";

	/// <summary>Faction name or empty for none. Colours the placeholder portrait.</summary>
	public string Faction { get; set; } = "";

	/// <summary>For the AI: character, goal, what convinces them, what they never accept.</summary>
	public string Persona { get; set; } = "";

	/// <summary>True while no real character/art exists. Shows a placeholder card.</summary>
	public bool Placeholder { get; set; } = true;
}

public sealed class EventOutcomeDef
{
	/// <summary>Short id the AI reports, unique inside the event (e.g. "verraten").</summary>
	public string Id { get; set; } = "";

	/// <summary>Shown to the player under "Mögliche Ausgänge".</summary>
	public string Label { get; set; } = "";

	/// <summary>For the AI: when exactly this outcome happens.</summary>
	public string Condition { get; set; } = "";

	/// <summary>Used when the player walks away or nobody decides in time. Exactly one per event.</summary>
	public bool Default { get; set; }

	/// <summary>Offline mode: word stems in the player's message that lead here (lowercase).</summary>
	public List<string> Keywords { get; set; } = new List<string>();

	/// <summary>Faction name -> standing change, e.g. { "Imperium": 8, "Rebellen": -4 }.</summary>
	public Dictionary<string, int> Reputation { get; set; } = new Dictionary<string, int>();

	/// <summary>If true, a change for Imperium/Rebellen also moves the rival by half the other way.</summary>
	public bool Rivalry { get; set; } = true;

	/// <summary>Change of Gastrecht (0–100). Negative when violence or betrayal happens in the tavern.</summary>
	public int Gastrecht { get; set; }

	/// <summary>Closing narration after the outcome.</summary>
	public string Result { get; set; } = "";

	/// <summary>Optional Floki reaction after the outcome.</summary>
	public string Floki { get; set; }

	/// <summary>Optional news line for the Galactic Radio at the end of the shift.</summary>
	public string Radio { get; set; }

	/// <summary>Optional faction whose key figure dies (permanent, closes that faction's path).</summary>
	public string KillKeyFigure { get; set; }

	/// <summary>What changes in the tavern afterwards: objects, characters, animations, story flags.</summary>
	public List<WorldEffectDef> World { get; set; } = new List<WorldEffectDef>();

	/// <summary>Optional task the player has to do after this outcome (e.g. repair the generator).</summary>
	public EventTaskDef Task { get; set; }

	/// <summary>Only reachable as the result of a task – hidden from the player's list and from the AI.</summary>
	public bool TaskOnly { get; set; }
}
