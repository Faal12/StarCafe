using System;
using System.Collections.Generic;

public enum ReputationLevel
{
	Feind,
	Misstrauisch,
	Neutral,
	Wohlwollend,
	Verbuendet
}

public enum Ending
{
	RebellenSeite,
	ImperiumSeite,
	NeutralerHafen,
	Soeldnergilde
}

public sealed record ReputationEvent(Faction? Subject, int Delta, int NewValue, string Reason);

public sealed record EndingStatus(Ending Ending, bool Available, string Reason);

public static class ReputationText
{
	public static string LevelName(ReputationLevel level) => level switch
	{
		ReputationLevel.Feind => "Feind",
		ReputationLevel.Misstrauisch => "Misstrauisch",
		ReputationLevel.Neutral => "Neutral",
		ReputationLevel.Wohlwollend => "Wohlwollend",
		ReputationLevel.Verbuendet => "Verbündet",
		_ => level.ToString()
	};

	public static string EndingName(Ending ending) => ending switch
	{
		Ending.RebellenSeite => "An der Seite der Rebellen",
		Ending.ImperiumSeite => "An der Seite des Imperiums",
		Ending.NeutralerHafen => "Neutraler Hafen",
		Ending.Soeldnergilde => "Offene Söldnergilde",
		_ => ending.ToString()
	};

	// Empty while nothing notable has happened, so early prompts stay clean.
	public static string DescribeForPrompt(ReputationState state)
	{
		var lines = new List<string>();

		var standing = new List<string>();
		foreach (Faction faction in FactionInfo.All)
		{
			ReputationLevel level = state.LevelOf(faction);
			if (level != ReputationLevel.Neutral)
				standing.Add($"{FactionInfo.DisplayName(faction)}: {LevelName(level).ToLowerInvariant()}");
		}

		if (standing.Count > 0)
			lines.Add("- Ansehen des Chefs: " + string.Join("; ", standing));

		if (state.Gastrecht < ReputationState.StartingGastrecht)
			lines.Add(state.Gastrecht < ReputationState.NeutralHarborMinGastrecht
				? "- Gastrecht: schwer verletzt, viele Gäste meiden die Taverne"
				: "- Gastrecht: angeschlagen");

		var dead = new List<string>();
		foreach (Faction faction in FactionInfo.All)
		{
			if (!state.IsKeyFigureAlive(faction))
				dead.Add($"{FactionInfo.KeyFigure(faction)} ({FactionInfo.DisplayName(faction)})");
		}

		if (dead.Count > 0)
			lines.Add("- Gestorben: " + string.Join(", ", dead));

		return lines.Count == 0
			? ""
			: "SPIELSTAND (nur zur Orientierung; erwähne nichts davon ungefragt)\n" + string.Join("\n", lines);
	}
}

public sealed class ReputationState
{
	public const int MinValue = -100;
	public const int MaxValue = 100;
	public const int StartingGastrecht = 100;

	public const int HostileThreshold = -60;
	public const int WaryThreshold = -20;
	public const int FriendlyThreshold = 20;
	public const int AlliedThreshold = 60;

	public const int NeutralHarborMinGastrecht = 50;

	// Placeholder tuning for how fast time heals: every week all standing drifts this far towards 0,
	// and Gastrecht recovers this much if nobody lost any in that week.
	public const int WeeklyDecay = 1;
	public const int WeeklyGastrechtRecovery = 2;

	private const int HistoryLimit = 50;

	private readonly Dictionary<Faction, int> _values = new();
	private readonly HashSet<Faction> _deadKeyFigures = new();
	private readonly List<ReputationEvent> _history = new();
	private int _version;
	private bool _gastrechtLostThisWeek;

	public event Action Changed;

	public int Gastrecht { get; private set; } = StartingGastrecht;

	public IReadOnlyList<ReputationEvent> History => _history;

	public ReputationState()
	{
		foreach (Faction faction in FactionInfo.All)
			_values[faction] = 0;
	}

	public int Get(Faction faction) => _values[faction];

	public ReputationLevel LevelOf(Faction faction) => LevelFor(_values[faction]);

	public static ReputationLevel LevelFor(int value) =>
		value <= HostileThreshold ? ReputationLevel.Feind :
		value <= WaryThreshold ? ReputationLevel.Misstrauisch :
		value < FriendlyThreshold ? ReputationLevel.Neutral :
		value < AlliedThreshold ? ReputationLevel.Wohlwollend :
		ReputationLevel.Verbuendet;

	public bool IsKeyFigureAlive(Faction faction) => !_deadKeyFigures.Contains(faction);

	// A successor takes over off-screen, so the faction stays in the world but is weaker.
	public bool IsWeakened(Faction faction) => _deadKeyFigures.Contains(faction);

	public int Change(Faction faction, int delta, string reason, bool applyRivalry = true)
	{
		int versionBefore = _version;

		int applied = ApplyOne(faction, delta, reason);
		if (applyRivalry && FactionInfo.Rival(faction) is Faction rival)
			ApplyOne(rival, -(delta / 2), reason);

		if (_version != versionBefore)
			Changed?.Invoke();

		return applied;
	}

	public int ChangeGastrecht(int delta, string reason)
	{
		int updated = Math.Clamp(Gastrecht + delta, 0, StartingGastrecht);
		int applied = updated - Gastrecht;
		if (applied == 0)
			return 0;

		Gastrecht = updated;
		if (applied < 0)
			_gastrechtLostThisWeek = true;

		AddEvent(new ReputationEvent(null, applied, updated, reason));
		Changed?.Invoke();
		return applied;
	}

	public bool KillKeyFigure(Faction faction, string reason = null)
	{
		if (!_deadKeyFigures.Add(faction))
			return false;

		AddEvent(new ReputationEvent(faction, 0, _values[faction],
			reason ?? $"{FactionInfo.KeyFigure(faction)} ist gestorben."));
		Changed?.Invoke();
		return true;
	}

	public void Reset()
	{
		foreach (Faction faction in FactionInfo.All)
			_values[faction] = 0;

		_deadKeyFigures.Clear();
		_history.Clear();
		Gastrecht = StartingGastrecht;
		_gastrechtLostThisWeek = false;
		_version++;
		Changed?.Invoke();
	}

	/// <summary>
	/// A week passes: standing slowly fades towards neutral (frozen for factions whose key figure is dead, their path is closed)
	/// and Gastrecht recovers after a week without violence. Returns whether anything changed.
	/// </summary>
	public bool ApplyWeeklyDrift()
	{
		bool changed = false;

		foreach (Faction faction in FactionInfo.All)
		{
			if (!IsKeyFigureAlive(faction))
				continue;

			int value = _values[faction];
			if (value == 0)
				continue;

			_values[faction] = value > 0 ? Math.Max(0, value - WeeklyDecay) : Math.Min(0, value + WeeklyDecay);
			changed = true;
		}

		if (!_gastrechtLostThisWeek && Gastrecht < StartingGastrecht)
		{
			Gastrecht = Math.Min(StartingGastrecht, Gastrecht + WeeklyGastrechtRecovery);
			changed = true;
		}

		_gastrechtLostThisWeek = false;

		if (changed)
		{
			_version++;
			Changed?.Invoke();
		}

		return changed;
	}

	public ReputationSnapshot Export()
	{
		var snapshot = new ReputationSnapshot { Gastrecht = Gastrecht, GastrechtLostThisWeek = _gastrechtLostThisWeek };

		foreach (Faction faction in FactionInfo.All)
			snapshot.Values[faction.ToString()] = _values[faction];

		foreach (Faction faction in _deadKeyFigures)
			snapshot.DeadKeyFigures.Add(faction.ToString());

		foreach (ReputationEvent entry in _history)
			snapshot.History.Add(new EventSnapshot
			{
				Subject = entry.Subject?.ToString(),
				Delta = entry.Delta,
				NewValue = entry.NewValue,
				Reason = entry.Reason
			});

		return snapshot;
	}

	/// <summary>Replaces the whole state. Unknown faction names in the file are ignored, values are clamped.</summary>
	public void Import(ReputationSnapshot snapshot)
	{
		foreach (Faction faction in FactionInfo.All)
			_values[faction] = snapshot.Values.TryGetValue(faction.ToString(), out int value)
				? Math.Clamp(value, MinValue, MaxValue)
				: 0;

		_deadKeyFigures.Clear();
		foreach (string name in snapshot.DeadKeyFigures)
			if (Enum.TryParse(name, out Faction dead))
				_deadKeyFigures.Add(dead);

		_history.Clear();
		foreach (EventSnapshot entry in snapshot.History)
		{
			Faction? subject = Enum.TryParse(entry.Subject, out Faction parsed) ? parsed : null;
			_history.Add(new ReputationEvent(subject, entry.Delta, entry.NewValue, entry.Reason));
		}

		Gastrecht = Math.Clamp(snapshot.Gastrecht, 0, StartingGastrecht);
		_gastrechtLostThisWeek = snapshot.GastrechtLostThisWeek;
		_version++;
		Changed?.Invoke();
	}

	public IReadOnlyList<EndingStatus> EvaluateEndings() => new[]
	{
		EndingBoundToFaction(Ending.RebellenSeite, Faction.Rebellen),
		EndingBoundToFaction(Ending.ImperiumSeite, Faction.Imperium),
		EndingBoundToFaction(Ending.Soeldnergilde, Faction.Soeldner),
		NeutralHarbor()
	};

	private int ApplyOne(Faction faction, int delta, string reason)
	{
		// Once a key figure is dead, the faction's path is closed: no more reputation gains.
		if (delta > 0 && !IsKeyFigureAlive(faction))
			return 0;

		int old = _values[faction];
		int updated = Math.Clamp(old + delta, MinValue, MaxValue);
		int applied = updated - old;
		if (applied == 0)
			return 0;

		_values[faction] = updated;
		AddEvent(new ReputationEvent(faction, applied, updated, reason));
		return applied;
	}

	private void AddEvent(ReputationEvent reputationEvent)
	{
		_history.Add(reputationEvent);
		if (_history.Count > HistoryLimit)
			_history.RemoveAt(0);

		_version++;
	}

	private EndingStatus EndingBoundToFaction(Ending ending, Faction faction)
	{
		if (!IsKeyFigureAlive(faction))
			return new EndingStatus(ending, false, $"{FactionInfo.KeyFigure(faction)} ist tot.");

		int value = _values[faction];
		if (value < AlliedThreshold)
			return new EndingStatus(ending, false,
				$"Ansehen ({FactionInfo.DisplayName(faction)}) zu niedrig: {value}/{AlliedThreshold}.");

		return new EndingStatus(ending, true, "");
	}

	private EndingStatus NeutralHarbor()
	{
		if (Gastrecht < NeutralHarborMinGastrecht)
			return new EndingStatus(Ending.NeutralerHafen, false,
				$"Gastrecht zu niedrig: {Gastrecht}/{NeutralHarborMinGastrecht}.");

		foreach (Faction power in new[] { Faction.Imperium, Faction.Rebellen })
		{
			if (!IsKeyFigureAlive(power))
				return new EndingStatus(Ending.NeutralerHafen, false,
					$"{FactionInfo.KeyFigure(power)} ist tot – {FactionInfo.DisplayName(power)} redet nicht mehr mit dir.");

			if (LevelOf(power) == ReputationLevel.Feind)
				return new EndingStatus(Ending.NeutralerHafen, false,
					$"{FactionInfo.DisplayName(power)} ist dir feindlich gesinnt.");
		}

		return new EndingStatus(Ending.NeutralerHafen, true, "");
	}
}

public static class ReputationService
{
	public static readonly ReputationState State = new();
}
