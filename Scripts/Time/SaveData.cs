using System;
using System.Collections.Generic;
using System.Text.Json;

public sealed class EventSnapshot
{
	public string Subject { get; set; }
	public int Delta { get; set; }
	public int NewValue { get; set; }
	public string Reason { get; set; }
}

public sealed class ReputationSnapshot
{
	public Dictionary<string, int> Values { get; set; } = new();
	public List<string> DeadKeyFigures { get; set; } = new();
	public int Gastrecht { get; set; }
	public bool GastrechtLostThisWeek { get; set; }
	public List<EventSnapshot> History { get; set; } = new();
}

/// <summary>Everything that is kept between sessions. Grows with the game; old files stay readable via Version.</summary>
public sealed class SaveData
{
	public const int CurrentVersion = 1;

	public int Version { get; set; } = CurrentVersion;
	public string SavedAt { get; set; }
	public int Day { get; set; } = GameClock.FirstDay;
	public bool ConjunctionShift { get; set; }
	public ReputationSnapshot Reputation { get; set; } = new();
}

public static class SaveSerializer
{
	private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

	public static string ToJson(SaveData data) => JsonSerializer.Serialize(data, Options);

	/// <summary>Returns false (with a readable reason) for broken or too-new files instead of throwing.</summary>
	public static bool TryRead(string json, out SaveData data, out string problem)
	{
		data = null;
		problem = null;

		try
		{
			data = JsonSerializer.Deserialize<SaveData>(json, Options);
		}
		catch (JsonException ex)
		{
			problem = $"Spielstand beschädigt ({ex.Message})";
			return false;
		}

		if (data == null)
		{
			problem = "Spielstand leer";
			return false;
		}

		if (data.Version > SaveData.CurrentVersion)
		{
			problem = $"Spielstand stammt aus einer neueren Version ({data.Version})";
			data = null;
			return false;
		}

		data.Reputation ??= new ReputationSnapshot();
		return true;
	}
}
