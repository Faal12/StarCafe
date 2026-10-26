using System;
using Godot;

/// <summary>What the player sees after ending a shift.</summary>
public sealed record ShiftSummary(ShiftEnd Time, int Week, int DaysUntilConjunction, bool StandingChanged, bool Saved);

/// <summary>
/// The running game: the calendar, the reputation, ending a shift and the autosave.
/// Static like the other services, so scenes can come and go.
/// </summary>
public static class GameSession
{
	private const string AutosavePath = "user://autosave.json";

	public static readonly GameClock Clock = new();

	public static event Action<ShiftSummary> ShiftEnded;

	public static bool HasAutosave => FileAccess.FileExists(AutosavePath);

	public static void NewGame()
	{
		Clock.Reset();
		ReputationService.State.Reset();
	}

	/// <summary>The week passes: calendar +7 days, standing fades, then the game is saved.</summary>
	public static ShiftSummary EndShift()
	{
		ShiftEnd time = Clock.EndShift();
		bool changed = ReputationService.State.ApplyWeeklyDrift();
		bool saved = Save();

		var summary = new ShiftSummary(time, Clock.Week, Clock.DaysUntilConjunction, changed, saved);
		ShiftEnded?.Invoke(summary);
		return summary;
	}

	public static bool Save()
	{
		var data = new SaveData
		{
			SavedAt = DateTime.Now.ToString("s"),
			Day = Clock.Day,
			ConjunctionShift = Clock.ConjunctionShift,
			Reputation = ReputationService.State.Export()
		};

		using FileAccess file = FileAccess.Open(AutosavePath, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			GD.PrintErr($"Spielstand konnte nicht gespeichert werden: {FileAccess.GetOpenError()}");
			return false;
		}

		file.StoreString(SaveSerializer.ToJson(data));
		return true;
	}

	/// <summary>Loads the autosave. On failure the game stays as it is and the reason is returned.</summary>
	public static bool LoadAutosave(out string problem)
	{
		problem = null;
		if (!HasAutosave)
		{
			problem = "Kein Spielstand vorhanden";
			return false;
		}

		using FileAccess file = FileAccess.Open(AutosavePath, FileAccess.ModeFlags.Read);
		if (file == null || !SaveSerializer.TryRead(file.GetAsText(), out SaveData data, out problem))
		{
			problem ??= "Spielstand nicht lesbar";
			return false;
		}

		ReputationService.State.Import(data.Reputation);
		Clock.Restore(data.Day, data.ConjunctionShift);
		return true;
	}

	/// <summary>Date of the autosave ("Woche 3 · Tag 15") for the Continue button, or null if there is none or it is unreadable.</summary>
	public static string DescribeAutosave()
	{
		if (!HasAutosave)
			return null;

		using FileAccess file = FileAccess.Open(AutosavePath, FileAccess.ModeFlags.Read);
		if (file == null || !SaveSerializer.TryRead(file.GetAsText(), out SaveData data, out _))
			return null;

		int week = (data.Day - GameClock.FirstDay) / GameClock.DaysPerWeek + 1;
		return $"Woche {week} · Tag {data.Day}";
	}

	/// <summary>"Woche 3 · Tag 15" for the HUD and the launcher button.</summary>
	public static string DescribeDate() => $"Woche {Clock.Week} · Tag {Clock.Day}";
}
