using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

public sealed record VoiceInfo(string Id, string Name, string Language);

public static class TtsService
{
	private static readonly Regex StageDirections = new(@"\*[^*]*\*|\([^)]*\)", RegexOptions.Compiled);
	private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

	public static IReadOnlyList<VoiceInfo> GetVoices()
	{
		var voices = new List<VoiceInfo>();

		try
		{
			foreach (Godot.Collections.Dictionary entry in DisplayServer.TtsGetVoices())
				voices.Add(new VoiceInfo(entry["id"].AsString(), entry["name"].AsString(), entry["language"].AsString()));
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Sprachausgabe: Stimmen konnten nicht gelesen werden: {ex.Message}");
		}

		return voices
			.OrderByDescending(voice => voice.Language.StartsWith("de", StringComparison.OrdinalIgnoreCase))
			.ThenBy(voice => voice.Name)
			.ToList();
	}

	public static string SlotFor(string characterName) =>
		Array.IndexOf(LauncherConfig.VoiceSlots, characterName) >= 0 ? characterName : LauncherConfig.DefaultVoiceSlot;

	// Stage directions like "(seufzt)" or "*seufzt*" are not meant to be read aloud.
	public static string CleanForSpeech(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return "";

		return Whitespace.Replace(StageDirections.Replace(text, " "), " ").Trim();
	}

	public static void Speak(string characterName, string text, bool force = false)
	{
		if (!force && !LauncherConfig.TtsEnabled)
			return;

		string spoken = CleanForSpeech(text);
		if (spoken.Length == 0)
			return;

		string voiceId = ResolveVoiceId(SlotFor(characterName));
		if (voiceId == null)
			return;

		int volume = Mathf.Clamp(Mathf.RoundToInt((float)(LauncherConfig.TtsVolume * GameSettings.MasterVolume)), 0, 100);

		try
		{
			DisplayServer.TtsStop();
			DisplayServer.TtsSpeak(spoken, voiceId, volume, 1.0f, (float)LauncherConfig.TtsRate, 0, true);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Sprachausgabe fehlgeschlagen: {ex.Message}");
		}
	}

	public static void Stop()
	{
		try
		{
			DisplayServer.TtsStop();
		}
		catch (Exception)
		{
			// Nothing to stop on platforms without speech support.
		}
	}

	private static string ResolveVoiceId(string slot)
	{
		IReadOnlyList<VoiceInfo> voices = GetVoices();
		if (voices.Count == 0)
			return null;

		string configured = LauncherConfig.GetVoice(slot);
		if (!string.IsNullOrEmpty(configured) && voices.Any(voice => voice.Id == configured))
			return configured;

		return voices[0].Id;
	}
}
