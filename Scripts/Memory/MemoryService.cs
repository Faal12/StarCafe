using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

/// <summary>Something a faction knows about the Wirt – from experience or from rumours.</summary>
public sealed class Rumor
{
	public int Week { get; set; }

	/// <summary>Faction that knows it (JSON name, e.g. "Imperium").</summary>
	public string Faction { get; set; } = "";

	public string Text { get; set; } = "";

	/// <summary>Deed kind it came from ("diebstahl", "schlaegerei" …).</summary>
	public string Kind { get; set; } = "";

	/// <summary>True = heard from others, false = experienced themselves.</summary>
	public bool Hearsay { get; set; }
}

public sealed class MemorySnapshot
{
	public int Wanted { get; set; }

	public List<Rumor> Known { get; set; } = new List<Rumor>();

	/// <summary>Witnessed deeds of the running shift that have not spread yet.</summary>
	public List<Deed> Unspread { get; set; } = new List<Deed>();
}

/// <summary>
/// "Die Welt erinnert sich" (phase 4) – logic only.
/// <list type="bullet">
/// <item><b>Memory:</b> victims and witnesses remember the Wirt's deeds; the AI gets what a speaker's faction knows.</item>
/// <item><b>Rumours:</b> witnessed deeds spread to other factions at the end of the shift, big ones reach the radio.</item>
/// <item><b>Wanted level</b> with the Sternenwacht (0–100): theft, brawls and threats raise it, it fades every shift.
///   From 30 more patrols, from 60 a search, from 90 the licence hearing.</item>
/// <item><b>Requirements</b> for hidden outcomes: items, story flags, deeds, wanted level.</item>
/// </list>
/// </summary>
public static class MemoryService
{
	public const int MaxWanted = 100;

	/// <summary>From here on the Sternenwacht checks more often.</summary>
	public const int Patrols = 30;

	/// <summary>From here on a search of the tavern can happen.</summary>
	public const int Search = 60;

	/// <summary>From here on the licence hearing can happen.</summary>
	public const int Hearing = 90;

	/// <summary>Fades by this much at every shift end.</summary>
	public const int WeeklyDecay = 8;

	private const int KnownLimit = 120;

	private static readonly List<Rumor> _known = new List<Rumor>();

	private static readonly List<Deed> _unspread = new List<Deed>();

	private static readonly RandomNumberGenerator Rng = CreateRng();

	/// <summary>Wanted level with the Sternenwacht, 0–100.</summary>
	public static int Wanted { get; private set; }

	/// <summary>(old, new, reason) – for the HUD and Floki.</summary>
	public static event Action<int, int, string> WantedChanged;

	/// <summary>Rumours that spread at the last shift end (shown on the shift-end screen).</summary>
	public static IReadOnlyList<string> LastShiftRumors { get; private set; } = new List<string>();

	/// <summary>Change of the wanted level over the last shift (deeds and decay).</summary>
	public static int LastShiftWantedChange { get; private set; }

	private static int _wantedAtShiftStart;

	static MemoryService()
	{
		PlayerState.DeedRecorded += OnDeed;
	}

	/// <summary>Touch the class so it starts listening to deeds (called at game start).</summary>
	public static void Init()
	{
	}

	public static IReadOnlyList<Rumor> Known => _known;

	// ───────────── Wanted level ─────────────

	public static void ChangeWanted(int delta, string reason)
	{
		if (delta == 0)
		{
			return;
		}
		int old = Wanted;
		Wanted = Math.Clamp(Wanted + delta, 0, MaxWanted);
		if (Wanted == old)
		{
			return;
		}
		WantedChanged?.Invoke(old, Wanted, reason);
		AnnounceThreshold(old, Wanted);
	}

	/// <summary>Short word for the current level ("unauffällig", "beobachtet" …).</summary>
	public static string WantedStage(int level) => level switch
	{
		>= Hearing => "Lizenz in Gefahr",
		>= Search => "Durchsuchung droht",
		>= Patrols => "unter Beobachtung",
		> 0 => "aufgefallen",
		_ => "unauffällig",
	};

	private static void AnnounceThreshold(int old, int now)
	{
		string line = null;
		if (old < Hearing && now >= Hearing)
		{
			line = "Chef … die Sternenwacht hat eine Anhörung zu unserer Lizenz angesetzt. Das ist das Gegenteil von gut.";
		}
		else if (old < Search && now >= Search)
		{
			line = "Chef, auf dem Sternenwacht-Kanal fällt unser Name. Oft. Rechne mit einer Durchsuchung.";
		}
		else if (old < Patrols && now >= Patrols)
		{
			line = "Die Sternenwacht hat uns auf dem Schirm. Mehr Streifen, mehr Fragen. Vielleicht weniger … Zwischenfälle?";
		}
		if (line != null)
		{
			UiBus.Toast("Fahndung: " + WantedStage(now) + " (" + now + ")", new Color(0.55f, 0.7f, 1f));
			VoiceQueue.Enqueue(FlokiAdvisor.Name, line);
		}
	}

	/// <summary>How much a deed raises the wanted level.</summary>
	private static int WantedFor(Deed deed)
	{
		bool againstWatch = deed.Tags.Contains("fraktion:Sternenwacht");
		int value = deed.Kind switch
		{
			"diebstahl" when deed.Tags.Contains("erwischt") => 15,
			"diebstahl" => againstWatch ? 6 : 0,
			"schlaegerei" => 10 + (deed.Tags.Contains("angefangen:wirt") ? 5 : 0),
			"drohung" => deed.Witnessed ? 3 : 0,
			"luege" when deed.Tags.Contains("durchschaut") => againstWatch ? 6 : 1,
			_ => 0,
		};
		return againstWatch && deed.Kind != "diebstahl" ? value * 2 : value;
	}

	// ───────────── Memory and rumours ─────────────

	private static void OnDeed(Deed deed)
	{
		int wanted = WantedFor(deed);
		if (wanted > 0)
		{
			ChangeWanted(wanted, deed.Text);
		}
		string faction = Tag(deed, "fraktion:");
		// The victim's faction knows what happened to one of them – if they noticed.
		if (deed.Witnessed && !string.IsNullOrEmpty(faction) && IsRemembered(deed.Kind))
		{
			Learn(new Rumor { Week = deed.Week, Faction = faction, Text = deed.Text, Kind = deed.Kind, Hearsay = false });
		}
		if (deed.Witnessed && IsRemembered(deed.Kind))
		{
			_unspread.Add(deed);
		}
	}

	/// <summary>Deeds worth talking about.</summary>
	private static bool IsRemembered(string kind) =>
		kind is "diebstahl" or "schlaegerei" or "drohung" or "luege" or "warten_lassen" or "verschuettet" or "ohnmacht";

	private static void Learn(Rumor rumor)
	{
		if (_known.Any(r => r.Faction == rumor.Faction && r.Text == rumor.Text))
		{
			return;
		}
		_known.Add(rumor);
		if (_known.Count > KnownLimit)
		{
			_known.RemoveAt(0);
		}
	}

	/// <summary>
	/// Shift end: witnessed deeds spread to other factions, big ones become radio news, the wanted level fades.
	/// Called by <see cref="GameSession.EndShift"/> before the radio is collected.
	/// </summary>
	public static void EndShift(int week)
	{
		// Same kind of rumour = one line ("… (3×)") with everybody who heard it.
		Dictionary<string, (int Count, HashSet<Faction> Heard, bool Big)> grouped = new Dictionary<string, (int, HashSet<Faction>, bool)>();
		foreach (Deed deed in _unspread)
		{
			string victim = Tag(deed, "fraktion:");
			List<Faction> listeners = FactionInfo.All.Where(f => f.ToString() != victim && Rng.Randf() < 0.45f).Take(3).ToList();
			foreach (Faction f in listeners)
			{
				Learn(new Rumor { Week = deed.Week, Faction = f.ToString(), Text = deed.Text, Kind = deed.Kind, Hearsay = true });
			}
			string line = RumorLine(deed);
			bool big = deed.Kind == "schlaegerei" || (deed.Kind == "diebstahl" && deed.Tags.Contains("erwischt"));
			(int count, HashSet<Faction> heard, bool wasBig) = grouped.TryGetValue(line, out var entry) ? entry : (0, new HashSet<Faction>(), false);
			heard.UnionWith(listeners);
			grouped[line] = (count + 1, heard, wasBig || big);
		}
		List<string> lines = new List<string>();
		foreach (KeyValuePair<string, (int Count, HashSet<Faction> Heard, bool Big)> pair in grouped)
		{
			if (pair.Value.Heard.Count > 0 && lines.Count < 4)
			{
				string times = pair.Value.Count > 1 ? $" ({pair.Value.Count}×)" : "";
				lines.Add(pair.Key + times + "  – gehört von: " + string.Join(", ", pair.Value.Heard.Select(FactionInfo.DisplayName)));
			}
			// Big stories reach the radio – more likely the more often they happened.
			if (pair.Value.Big && Rng.Randf() < Math.Min(0.9f, 0.4f + 0.15f * pair.Value.Count))
			{
				EventService.State.AddRadio(week, "Gerüchteküche: " + pair.Key);
			}
		}
		_unspread.Clear();
		LastShiftRumors = lines;
		ChangeWanted(-WeeklyDecay, "Die Zeit vergeht");
		LastShiftWantedChange = Wanted - _wantedAtShiftStart;
		_wantedAtShiftStart = Wanted;
	}

	private static string RumorLine(Deed deed) => deed.Kind switch
	{
		"schlaegerei" => "Man erzählt sich von einer Schlägerei in der „Letzten Runde“.",
		"diebstahl" => "Der Wirt der „Letzten Runde“ soll einem Gast in die Tasche gegriffen haben.",
		"drohung" => "Der Wirt der „Letzten Runde“ soll Gästen drohen.",
		"luege" => "Dem Wirt der „Letzten Runde“ glaubt man besser nicht alles.",
		"warten_lassen" => "In der „Letzten Runde“ wartet man ewig auf sein Getränk.",
		"verschuettet" => "In der „Letzten Runde“ landen die Drinks auf dem Boden.",
		"ohnmacht" => "Der Wirt der „Letzten Runde“ lag bewusstlos auf dem Boden.",
		_ => deed.Text,
	};

	/// <summary>
	/// For the AI prompt of an event speaker: what this person and their faction know about the Wirt.
	/// Empty if nothing.
	/// </summary>
	public static string DescribeForSpeaker(EventSpeaker speaker)
	{
		StringBuilder sb = new StringBuilder();
		List<Deed> personal = PlayerState.Deeds
			.Where(d => d.Witnessed && d.Tags.Contains("opfer:" + speaker.Name) || d.Tags.Contains("gegner:" + speaker.Name))
			.TakeLast(3).ToList();
		List<Rumor> faction = string.IsNullOrEmpty(speaker.Faction)
			? new List<Rumor>()
			: _known.Where(r => r.Faction == speaker.Faction).TakeLast(4).ToList();
		if (personal.Count > 0 || faction.Count > 0)
		{
			sb.Append("WAS DU ÜBER DEN WIRT WEISST (nutze es, wenn es passt – erwähne nicht alles auf einmal):\n");
			foreach (Deed d in personal)
			{
				sb.Append("- Selbst erlebt: ").Append(d.Text).Append('\n');
			}
			foreach (Rumor r in faction.Where(r => personal.All(d => d.Text != r.Text)))
			{
				sb.Append(r.Hearsay ? "- Gerücht in deiner Fraktion: " : "- Deine Leute haben erlebt: ").Append(r.Text).Append('\n');
			}
		}
		if (speaker.Faction == "Sternenwacht" && Wanted >= Patrols)
		{
			sb.Append($"- Dienstlich: Der Wirt steht bei der Sternenwacht unter Verdacht ({WantedStage(Wanted)}, Fahndungsstufe {Wanted} von 100).\n");
		}
		return sb.ToString().TrimEnd();
	}

	/// <summary>For Floki's prompt: he sees everything that happens on his ship.</summary>
	public static string DescribeForFloki()
	{
		List<Deed> recent = PlayerState.Deeds.Where(d => IsRemembered(d.Kind) || d.Kind == "serviert").TakeLast(6).ToList();
		if (recent.Count == 0 && Wanted == 0)
		{
			return "";
		}
		StringBuilder sb = new StringBuilder("WAS FLOKI ÜBER DEN WIRT WEISS (er sieht alles an Bord und erinnert sich – gern im unpassendsten Moment):\n");
		foreach (Deed d in recent)
		{
			sb.Append("- Woche ").Append(d.Week).Append(": ").Append(d.Text).Append('\n');
		}
		if (Wanted > 0)
		{
			sb.Append($"- Fahndungsstufe bei der Sternenwacht: {Wanted} von 100 ({WantedStage(Wanted)}).\n");
		}
		return sb.ToString().TrimEnd();
	}

	// ───────────── Requirements for hidden outcomes ─────────────

	/// <summary>True if the player fulfils everything (all listed items, flags and deeds; wanted range).</summary>
	public static bool Meets(EventRequirement req)
	{
		if (req == null)
		{
			return true;
		}
		if (req.Items.Any(id => PlayerState.Inventory.Count(id) <= 0))
		{
			return false;
		}
		if (req.Flags.Any(flag => !WorldService.State.HasFlag(flag)))
		{
			return false;
		}
		foreach (string deed in req.Deeds)
		{
			string[] parts = deed.Split('|', 2);
			if (!PlayerState.HasDeed(parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : null))
			{
				return false;
			}
		}
		return Wanted >= req.MinWanted && (req.MaxWanted <= 0 || Wanted <= req.MaxWanted);
	}

	// ───────────── Save / load ─────────────

	public static void Reset()
	{
		_known.Clear();
		_unspread.Clear();
		Wanted = 0;
		_wantedAtShiftStart = 0;
		LastShiftRumors = new List<string>();
		LastShiftWantedChange = 0;
	}

	public static MemorySnapshot Export() => new MemorySnapshot
	{
		Wanted = Wanted,
		Known = _known.ToList(),
		Unspread = _unspread.ToList(),
	};

	public static void Import(MemorySnapshot snapshot)
	{
		Reset();
		if (snapshot == null)
		{
			return;
		}
		Wanted = Math.Clamp(snapshot.Wanted, 0, MaxWanted);
		_wantedAtShiftStart = Wanted;
		_known.AddRange(snapshot.Known ?? new List<Rumor>());
		_unspread.AddRange(snapshot.Unspread ?? new List<Deed>());
	}

	private static string Tag(Deed deed, string prefix)
	{
		string tag = deed.Tags.FirstOrDefault(t => t.StartsWith(prefix, StringComparison.Ordinal));
		return tag?.Substring(prefix.Length) ?? "";
	}

	private static RandomNumberGenerator CreateRng()
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();
		return rng;
	}
}
