using System;

/// <summary>What happened when a shift ended.</summary>
public sealed record ShiftEnd(int OldDay, int NewDay, bool ConjunctionStarts);

/// <summary>
/// The calendar. Every shift ends with a jump of one week; the conjunction follows the calendar
/// (every 40 days) and is started by the first shift whose week contains that day.
/// Pure logic, no Godot types.
/// </summary>
public sealed class GameClock
{
	public const int FirstDay = 1;
	public const int DaysPerWeek = 7;
	public const int ConjunctionInterval = 40;

	public event Action Changed;

	public int Day { get; private set; } = FirstDay;

	/// <summary>True while the current shift is a conjunction shift (high season).</summary>
	public bool ConjunctionShift { get; private set; }

	public int Week => (Day - FirstDay) / DaysPerWeek + 1;

	public int DayOfWeek => (Day - FirstDay) % DaysPerWeek + 1;

	public int NextConjunctionDay => (Day + ConjunctionInterval - 1) / ConjunctionInterval * ConjunctionInterval;

	public int DaysUntilConjunction => NextConjunctionDay - Day;

	public ShiftEnd EndShift()
	{
		int oldDay = Day;
		Day += DaysPerWeek;
		ConjunctionShift = ContainsConjunction(oldDay, Day);
		Changed?.Invoke();
		return new ShiftEnd(oldDay, Day, ConjunctionShift);
	}

	public void Reset()
	{
		Restore(FirstDay, false);
	}

	public void Restore(int day, bool conjunctionShift)
	{
		Day = Math.Max(FirstDay, day);
		ConjunctionShift = conjunctionShift;
		Changed?.Invoke();
	}

	/// <summary>Is there a conjunction day in the days after <paramref name="fromDay"/> up to and including <paramref name="toDay"/>?</summary>
	public static bool ContainsConjunction(int fromDay, int toDay) =>
		toDay / ConjunctionInterval > fromDay / ConjunctionInterval;
}
