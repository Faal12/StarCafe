using System.Collections.Generic;

/// <summary>A line for the Galactic Radio.</summary>
public sealed class RadioNews
{
	public int Week { get; set; }

	public string Text { get; set; } = "";
}

/// <summary>Serializable form of <see cref="EventState"/> inside the save file.</summary>
public sealed class EventStateSnapshot
{
	public Dictionary<string, int> TimesHappened { get; set; } = new Dictionary<string, int>();

	public Dictionary<string, int> LastWeek { get; set; } = new Dictionary<string, int>();

	public List<RadioNews> PendingRadio { get; set; } = new List<RadioNews>();

	public List<RadioNews> RadioArchive { get; set; } = new List<RadioNews>();
}

/// <summary>Which events already happened (for "once" and cooldowns) and the radio news.</summary>
public sealed class EventState
{
	private const int ArchiveLimit = 30;

	private readonly Dictionary<string, int> _timesHappened = new Dictionary<string, int>();

	private readonly Dictionary<string, int> _lastWeek = new Dictionary<string, int>();

	private readonly List<RadioNews> _pendingRadio = new List<RadioNews>();

	private readonly List<RadioNews> _radioArchive = new List<RadioNews>();

	/// <summary>News already broadcast, newest last.</summary>
	public IReadOnlyList<RadioNews> RadioArchive => _radioArchive;

	public int TimesHappened(string id)
	{
		return _timesHappened.TryGetValue(id, out int count) ? count : 0;
	}

	/// <summary>Week the event last happened, or null if never.</summary>
	public int? LastWeek(string id)
	{
		return _lastWeek.TryGetValue(id, out int week) ? week : null;
	}

	public void RecordHappened(string id, int week)
	{
		_timesHappened[id] = TimesHappened(id) + 1;
		_lastWeek[id] = week;
	}

	public void AddRadio(int week, string text)
	{
		if (!string.IsNullOrWhiteSpace(text))
		{
			_pendingRadio.Add(new RadioNews { Week = week, Text = text.Trim() });
		}
	}

	/// <summary>Returns the news gathered this shift and moves them to the archive.</summary>
	public List<RadioNews> TakePendingRadio()
	{
		List<RadioNews> news = new List<RadioNews>(_pendingRadio);
		_radioArchive.AddRange(_pendingRadio);
		_pendingRadio.Clear();
		while (_radioArchive.Count > ArchiveLimit)
		{
			_radioArchive.RemoveAt(0);
		}
		return news;
	}

	public void Reset()
	{
		_timesHappened.Clear();
		_lastWeek.Clear();
		_pendingRadio.Clear();
		_radioArchive.Clear();
	}

	public EventStateSnapshot Export()
	{
		return new EventStateSnapshot
		{
			TimesHappened = new Dictionary<string, int>(_timesHappened),
			LastWeek = new Dictionary<string, int>(_lastWeek),
			PendingRadio = new List<RadioNews>(_pendingRadio),
			RadioArchive = new List<RadioNews>(_radioArchive)
		};
	}

	public void Import(EventStateSnapshot snapshot)
	{
		Reset();
		if (snapshot == null)
		{
			return;
		}
		foreach (KeyValuePair<string, int> pair in snapshot.TimesHappened ?? new Dictionary<string, int>())
		{
			_timesHappened[pair.Key] = pair.Value;
		}
		foreach (KeyValuePair<string, int> pair in snapshot.LastWeek ?? new Dictionary<string, int>())
		{
			_lastWeek[pair.Key] = pair.Value;
		}
		_pendingRadio.AddRange(snapshot.PendingRadio ?? new List<RadioNews>());
		_radioArchive.AddRange(snapshot.RadioArchive ?? new List<RadioNews>());
	}
}
