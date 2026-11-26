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

	/// <summary>Fired when the player closes the shift-end screen and the new shift begins.</summary>
	public static event Action ShiftStarted;

	/// <summary>Radio news broadcast at the last shift end (taken before saving, so they never repeat).</summary>
	public static System.Collections.Generic.IReadOnlyList<RadioNews> LastShiftRadio { get; private set; } = new System.Collections.Generic.List<RadioNews>();

	/// <summary>Income and expenses of the last shift (cash book), shown on the shift-end screen.</summary>
	public static System.Collections.Generic.IReadOnlyList<LedgerEntry> LastShiftLedger { get; private set; } = new System.Collections.Generic.List<LedgerEntry>();

	public static bool HasAutosave => FileAccess.FileExists(AutosavePath);

	static GameSession()
	{
		// Memory listens to deeds from the very first one on.
		MemoryService.Init();
	}

	public static void NewGame()
	{
		Clock.Reset();
		ReputationService.State.Reset();
		EventService.State.Reset();
		WorldService.State.Reset();
		PlayerState.Reset();
		MemoryService.Reset();
	}

	public static void BeginShift() => ShiftStarted?.Invoke();

	/// <summary>The week passes: calendar +7 days, standing fades, then the game is saved.</summary>
	public static ShiftSummary EndShift()
	{
		// A running task (e.g. the blackout) fails when the shift ends.
		if (TaskService.IsActive)
			TaskService.Active.Abort();

		// Rumours spread (some reach the radio) and the wanted level fades.
		MemoryService.EndShift(Clock.Week);
		LastShiftRadio = EventService.State.TakePendingRadio();
		Economy.ChargeWeekly();
		LastShiftLedger = PlayerState.TakeLedger();

		ShiftEnd time = Clock.EndShift();
		bool changed = ReputationService.State.ApplyWeeklyDrift();
		// Props and characters from events whose time is up leave before the new week is saved.
		WorldService.State.Expire(Clock.Week);
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
			Reputation = ReputationService.State.Export(),
			Events = EventService.State.Export(),
			World = WorldService.State.Export(),
			Player = PlayerState.Export(),
			Memory = MemoryService.Export()
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
		EventService.State.Import(data.Events);
		WorldService.State.Import(data.World);
		PlayerState.Import(data.Player);
		MemoryService.Import(data.Memory);
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
