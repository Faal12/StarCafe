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

	/// <summary>Wanted level (Sternenwacht) needed for this event (0 = none). For raids and the licence hearing.</summary>
	public int MinWanted { get; set; }

	/// <summary>0 = no upper limit.</summary>
	public int MaxWanted { get; set; }

	/// <summary>More likely the higher the wanted level (patrols, investigators).</summary>
	public bool WantedBoost { get; set; }

	/// <summary>
	/// Outcomes that can be reached by talking right now: not task results, not action results,
	/// and hidden outcomes only once their requirements are met.
	/// </summary>
	public List<EventOutcomeDef> TalkOutcomes => Outcomes.FindAll(o => !o.TaskOnly && string.IsNullOrWhiteSpace(o.Action) && MemoryService.Meets(o.Requires));

	/// <summary>Hidden outcomes whose requirements are not met yet (shown as "???").</summary>
	public List<EventOutcomeDef> LockedOutcomes => Outcomes.FindAll(o => o.Requires != null && !o.TaskOnly && string.IsNullOrWhiteSpace(o.Action) && !MemoryService.Meets(o.Requires));

	/// <summary>Outcomes this event defines for actions (deceive, threaten, steal, attack).</summary>
	public List<EventOutcomeDef> ActionOutcomes => Outcomes.FindAll(o => !string.IsNullOrWhiteSpace(o.Action));

	/// <summary>The event's own outcome for an action result, e.g. ("angreifen", "sieg"); null = use the general one.</summary>
	public EventOutcomeDef FindActionOutcome(string action, string result)
	{
		return Outcomes.Find(o => string.Equals(o.Action, action, System.StringComparison.OrdinalIgnoreCase)
			&& (string.IsNullOrWhiteSpace(o.ActionResult) || string.Equals(o.ActionResult, result, System.StringComparison.OrdinalIgnoreCase)));
	}
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

	/// <summary>Fighting strength 1–3: health and punches in a brawl, how hard to intimidate. Default 2.</summary>
	public int Strength { get; set; } = 2;

	/// <summary>Wits 1–3: how hard to deceive and to steal from. Default 2.</summary>
	public int Wits { get; set; } = 2;

	/// <summary>Not in the room (radio, holo call): no attacking, no stealing.</summary>
	public bool Remote { get; set; }

	/// <summary>What can be stolen. Empty = a few credits.</summary>
	public EventPockets Pockets { get; set; } = new EventPockets();
}

/// <summary>Contents of a speaker's pockets (JSON "pockets").</summary>
public sealed class EventPockets
{
	public int CreditsMin { get; set; } = 5;

	public int CreditsMax { get; set; } = 25;

	/// <summary>Item ids from Data/Items/items.json, e.g. "datenchip".</summary>
	public List<string> Items { get; set; } = new List<string>();
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

	/// <summary>
	/// Reached by an action instead of talking: "taeuschen", "einschuechtern", "bestehlen" or "angreifen".
	/// Without it, the general outcome for that action applies (see Data/Events/README.md).
	/// </summary>
	public string Action { get; set; } = "";

	/// <summary>
	/// Which result of the action: "erfolg" / "fehlschlag" (deceive, threaten, steal),
	/// "sieg" / "niederlage" / "flucht" (attack). Empty = any result.
	/// </summary>
	public string ActionResult { get; set; } = "";

	/// <summary>
	/// Hidden outcome: only offered (to the player and the AI) once these requirements are met –
	/// an item in the inventory, a story flag, an earlier deed, a wanted level. Until then it shows as "???".
	/// </summary>
	public EventRequirement Requires { get; set; }

	/// <summary>True for hidden outcomes (with <see cref="Requires"/>), whether unlocked or not.</summary>
	public bool IsSecret => Requires != null;

	/// <summary>Change of the wanted level with the Sternenwacht (negative = suspicion falls).</summary>
	public int Wanted { get; set; }
}

/// <summary>What unlocks a hidden outcome (JSON "requires"). All listed conditions must be true.</summary>
public sealed class EventRequirement
{
	/// <summary>Item ids that must be in the inventory, e.g. "dienstchip".</summary>
	public List<string> Items { get; set; } = new List<string>();

	/// <summary>Story flags that must be set.</summary>
	public List<string> Flags { get; set; } = new List<string>();

	/// <summary>Earlier deeds: "kind" or "kind|tag", e.g. "diebstahl|erwischt", "schlaegerei|sieg".</summary>
	public List<string> Deeds { get; set; } = new List<string>();

	public int MinWanted { get; set; }

	/// <summary>0 = no upper limit.</summary>
	public int MaxWanted { get; set; }

	/// <summary>Shown while locked, e.g. "Wenn du etwas gegen ihn in der Hand hättest …".</summary>
	public string Hint { get; set; } = "";

	/// <summary>Shown once unlocked: why the player has this option, e.g. "Du hast seinen Dienstchip.".</summary>
	public string Reason { get; set; } = "";
}
