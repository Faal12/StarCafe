using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

/// <summary>
/// Loads all event files from res://Data/Events/*.json once and validates them.
/// Broken events are skipped and reported in <see cref="Problems"/> and the Godot output.
/// </summary>
public static class EventLibrary
{
	public const string Folder = "res://Data/Events";

	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	private static List<GameEventDef> _events;

	private static readonly List<string> _problems = new List<string>();

	public static IReadOnlyList<GameEventDef> Events
	{
		get
		{
			if (_events == null)
			{
				Reload();
			}
			return _events;
		}
	}

	public static IReadOnlyList<string> Problems => _problems;

	public static void Reload()
	{
		_events = new List<GameEventDef>();
		_problems.Clear();
		HashSet<string> ids = new HashSet<string>();
		using DirAccess dir = DirAccess.Open(Folder);
		if (dir == null)
		{
			Report("Ordner " + Folder + " fehlt.");
			return;
		}
		foreach (string fileName in dir.GetFiles())
		{
			if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string path = Folder + "/" + fileName;
			GameEventFile file;
			try
			{
				file = JsonSerializer.Deserialize<GameEventFile>(FileAccess.GetFileAsString(path), Options);
			}
			catch (JsonException ex)
			{
				Report(fileName + ": JSON-Fehler – " + ex.Message);
				continue;
			}
			if (file?.Events == null)
			{
				continue;
			}
			foreach (GameEventDef def in file.Events)
			{
				string problem = Validate(def);
				if (problem == null && !ids.Add(def.Id))
				{
					problem = "Id doppelt vergeben";
				}
				if (problem != null)
				{
					Report($"{fileName} / {def?.Id ?? "?"}: {problem} – Ereignis wird übersprungen.");
					continue;
				}
				_events.Add(def);
			}
		}
		GD.Print($"Ereignisse geladen: {_events.Count}" + (_problems.Count > 0 ? $", Probleme: {_problems.Count}" : ""));
	}

	public static bool TryFaction(string name, out Faction faction)
	{
		return Enum.TryParse(name, ignoreCase: true, out faction) && Enum.IsDefined(faction);
	}

	private static string Validate(GameEventDef def)
	{
		if (def == null)
		{
			return "leerer Eintrag";
		}
		if (string.IsNullOrWhiteSpace(def.Id))
		{
			return "\"id\" fehlt";
		}
		if (string.IsNullOrWhiteSpace(def.Title) || string.IsNullOrWhiteSpace(def.Text))
		{
			return "\"title\" oder \"text\" fehlt";
		}
		if (def.Outcomes == null || def.Outcomes.Count < 2)
		{
			return "mindestens zwei \"outcomes\" nötig";
		}
		if (def.Outcomes.FindAll(o => o != null && o.Default).Count != 1)
		{
			return "genau ein Ausgang braucht \"default\": true (was passiert, wenn der Wirt sich heraushält)";
		}
		if (def.Outcomes.Exists(o => o != null && o.Default && o.TaskOnly))
		{
			return "der Standard-Ausgang darf nicht \"taskOnly\" sein";
		}
		foreach (EventOutcomeDef withTask in def.Outcomes.FindAll(o => o?.Task != null))
		{
			EventTaskDef task = withTask.Task;
			if (!TaskService.IsKnown(task.Id))
			{
				return $"Ausgang \"{withTask.Id}\": unbekannte Aufgabe \"{task.Id}\" (bekannt: generator)";
			}
			foreach (string target in new[] { task.Success, task.Late, task.Fail })
			{
				if (!string.IsNullOrEmpty(target) && def.FindOutcome(target) == null)
				{
					return $"Ausgang \"{withTask.Id}\": Aufgabe verweist auf unbekannten Ausgang \"{target}\"";
				}
			}
		}
		if (string.IsNullOrWhiteSpace(def.Opening))
		{
			return "\"opening\" (erster Satz der Figur) fehlt";
		}
		if (def.MaxTurns < 1)
		{
			return "\"maxTurns\" muss mindestens 1 sein";
		}
		def.OfflineLines ??= new List<string>();
		if (def.Weight <= 0)
		{
			return "\"weight\" muss größer als 0 sein";
		}
		string conj = def.Conjunction?.ToLowerInvariant() ?? "egal";
		if (conj != "egal" && conj != "nur" && conj != "nie")
		{
			return "\"conjunction\" muss \"egal\", \"nur\" oder \"nie\" sein";
		}
		def.Speaker ??= new EventSpeaker();
		def.Speaker.Strength = Math.Clamp(def.Speaker.Strength, 1, 3);
		def.Speaker.Wits = Math.Clamp(def.Speaker.Wits, 1, 3);
		def.Speaker.Pockets ??= new EventPockets();
		def.Speaker.Pockets.Items ??= new List<string>();
		def.Speaker.Pockets.CreditsMin = Math.Max(0, def.Speaker.Pockets.CreditsMin);
		def.Speaker.Pockets.CreditsMax = Math.Max(def.Speaker.Pockets.CreditsMin, def.Speaker.Pockets.CreditsMax);
		def.MinReputation ??= new Dictionary<string, int>();
		def.MaxReputation ??= new Dictionary<string, int>();
		def.RequiresAlive ??= new List<string>();
		def.RequiresFlags ??= new List<string>();
		def.BlockedByFlags ??= new List<string>();
		string bad = FirstUnknownFaction(def.MinReputation.Keys) ?? FirstUnknownFaction(def.MaxReputation.Keys) ?? FirstUnknownFaction(def.RequiresAlive);
		if (!string.IsNullOrEmpty(def.Speaker.Faction) && !TryFaction(def.Speaker.Faction, out _))
		{
			bad = def.Speaker.Faction;
		}
		HashSet<string> outcomeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (EventOutcomeDef outcome in def.Outcomes)
		{
			if (outcome == null || string.IsNullOrWhiteSpace(outcome.Id) || string.IsNullOrWhiteSpace(outcome.Label))
			{
				return "ein Ausgang hat kein \"id\" oder \"label\"";
			}
			if (!System.Text.RegularExpressions.Regex.IsMatch(outcome.Id, "^[A-Za-z0-9_]+$"))
			{
				return $"Ausgangs-Id \"{outcome.Id}\" darf nur Buchstaben, Ziffern und _ enthalten";
			}
			if (!outcomeIds.Add(outcome.Id))
			{
				return $"Ausgangs-Id \"{outcome.Id}\" doppelt";
			}
			outcome.Action = (outcome.Action ?? "").Trim().ToLowerInvariant();
			outcome.ActionResult = (outcome.ActionResult ?? "").Trim().ToLowerInvariant();
			bool byAction = outcome.Action.Length > 0;
			if (byAction && !ConversationActions.IsKnown(outcome.Action))
			{
				return $"Ausgang \"{outcome.Id}\": unbekannte \"action\" \"{outcome.Action}\" (erlaubt: {string.Join(", ", ConversationActions.Ids)})";
			}
			if (byAction && outcome.ActionResult.Length > 0 && !ConversationActions.IsKnownResult(outcome.Action, outcome.ActionResult))
			{
				return $"Ausgang \"{outcome.Id}\": \"actionResult\" \"{outcome.ActionResult}\" passt nicht zu \"{outcome.Action}\" (erlaubt: {string.Join(", ", ConversationActions.Results(outcome.Action))})";
			}
			if (byAction && outcome.Default)
			{
				return $"Ausgang \"{outcome.Id}\": der Standard-Ausgang kann nicht durch eine \"action\" erreicht werden";
			}
			if (string.IsNullOrWhiteSpace(outcome.Condition) && !outcome.TaskOnly && !byAction)
			{
				return $"Ausgang \"{outcome.Id}\" hat keine \"condition\" (die KI braucht sie)";
			}
			outcome.Reputation ??= new Dictionary<string, int>();
			outcome.Keywords ??= new List<string>();
			outcome.World ??= new List<WorldEffectDef>();
			foreach (WorldEffectDef effect in outcome.World)
			{
				string worldProblem = ValidateWorld(effect);
				if (worldProblem != null)
				{
					return $"Ausgang \"{outcome.Id}\", world: {worldProblem}";
				}
			}
			bad ??= FirstUnknownFaction(outcome.Reputation.Keys);
			if (!string.IsNullOrEmpty(outcome.KillKeyFigure) && !TryFaction(outcome.KillKeyFigure, out _))
			{
				bad ??= outcome.KillKeyFigure;
			}
		}
		if (bad != null)
		{
			return $"unbekannte Fraktion \"{bad}\" (erlaubt: {string.Join(", ", FactionInfo.All)})";
		}
		return null;
	}

	private static string ValidateWorld(WorldEffectDef effect)
	{
		if (effect == null)
		{
			return "leerer Eintrag";
		}
		effect.Type = (effect.Type ?? "").Trim();
		if (!WorldEffectDef.Types.Contains(effect.Type))
		{
			return $"\"type\" \"{effect.Type}\" unbekannt (erlaubt: {string.Join(", ", WorldEffectDef.Types)})";
		}
		if (effect.Type != "animation" && string.IsNullOrWhiteSpace(effect.Id))
		{
			return $"\"{effect.Type}\" braucht eine \"id\"";
		}
		if (effect.Type == "animation" && !WorldEffectDef.Animations.Contains(effect.Animation ?? ""))
		{
			return $"\"animation\" \"{effect.Animation}\" unbekannt (erlaubt: {string.Join(", ", WorldEffectDef.Animations)})";
		}
		if (effect.IsThing && !string.IsNullOrWhiteSpace(effect.Appear) && !WorldEffectDef.Appears.Contains(effect.Appear))
		{
			return $"\"appear\" \"{effect.Appear}\" unbekannt (erlaubt: {string.Join(", ", WorldEffectDef.Appears)})";
		}
		if (!string.IsNullOrEmpty(effect.Faction) && !TryFaction(effect.Faction, out _))
		{
			return $"unbekannte Fraktion \"{effect.Faction}\"";
		}
		if (effect.Weeks < 0)
		{
			return "\"weeks\" darf nicht negativ sein";
		}
		if (!string.IsNullOrWhiteSpace(effect.Texture) && !ResourceLoader.Exists(effect.Texture))
		{
			GD.PushWarning($"Ereignisse: Bild \"{effect.Texture}\" fehlt – \"{effect.Id}\" erscheint als Platzhalter.");
		}
		return null;
	}

	private static string FirstUnknownFaction(IEnumerable<string> names)
	{
		foreach (string name in names)
		{
			if (!TryFaction(name, out _))
			{
				return name;
			}
		}
		return null;
	}

	private static void Report(string problem)
	{
		_problems.Add(problem);
		GD.PushWarning("Ereignisse: " + problem);
	}
}
