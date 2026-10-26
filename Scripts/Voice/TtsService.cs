using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;

public sealed record VoiceInfo(string Id, string Name, string Language);

/// <summary>A line of speech that is ready to play. Stream is null for the Windows system voice, which speaks by itself.</summary>
public sealed record TtsClip(string Text, AudioStreamWav Stream, double Seconds, string SystemVoiceId, string Source);

public static class TtsService
{
	private const string LogName = "Stimme";

	private static readonly Regex StageDirections = new(@"\*[^*]*\*|\([^)]*\)", RegexOptions.Compiled);
	private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

	private static readonly Dictionary<TtsProviderKind, ITtsProvider> Providers = new()
	{
		[TtsProviderKind.Piper] = new PiperTtsProvider(),
		[TtsProviderKind.Gemini] = new GeminiTtsProvider(),
		[TtsProviderKind.ElevenLabs] = new ElevenLabsTtsProvider(),
	};

	private static AudioStreamPlayer _player;

	public static ITtsProvider ProviderFor(TtsProviderKind kind) =>
		Providers.TryGetValue(kind, out ITtsProvider provider) ? provider : null;

	// ----- Windows system voices (also the fallback when a neural voice is not available) -----

	public static IReadOnlyList<VoiceInfo> GetSystemVoices()
	{
		var voices = new List<VoiceInfo>();

		try
		{
			foreach (Godot.Collections.Dictionary entry in DisplayServer.TtsGetVoices())
				voices.Add(new VoiceInfo(entry["id"].AsString(), entry["name"].AsString(), entry["language"].AsString()));
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Sprachausgabe: Systemstimmen konnten nicht gelesen werden: {ex.Message}");
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

	// ----- Preparing and playing -----

	/// <summary>
	/// Makes the audio for a line (from the cache when possible). Returns null when speech is off or nothing could be made;
	/// the reason is in the live log. Never throws.
	/// </summary>
	public static async Task<TtsClip> PrepareAsync(string characterName, string text, bool force = false)
	{
		if (!force && !LauncherConfig.TtsEnabled)
			return null;

		string spoken = CleanForSpeech(text);
		if (spoken.Length == 0)
			return null;

		string slot = SlotFor(characterName);
		TtsProviderKind kind = LauncherConfig.TtsProvider;

		if (kind != TtsProviderKind.System)
		{
			ITtsProvider provider = ProviderFor(kind);
			TtsClip clip = await TrySynthesizeAsync(provider, characterName, slot, spoken);
			if (clip != null)
				return clip;
		}

		return SystemClip(slot, spoken, kind == TtsProviderKind.System ? null : $"{ProviderFor(kind)?.Name} nicht verfügbar, Windows-Stimme eingesprungen");
	}

	public static async void Speak(string characterName, string text, bool force = false)
	{
		TtsClip clip = await PrepareAsync(characterName, text, force);
		if (clip != null)
			Play(clip);
	}

	public static void Play(TtsClip clip)
	{
		Stop();
		if (clip == null)
			return;

		float volume = (float)(LauncherConfig.TtsVolume / 100.0 * GameSettings.MasterVolume);

		if (clip.Stream != null)
		{
			AudioStreamPlayer player = EnsurePlayer();
			if (player == null)
				return;

			player.Stream = clip.Stream;
			player.VolumeDb = volume <= 0.001f ? -80f : Mathf.LinearToDb(volume);
			player.Play();
			return;
		}

		try
		{
			DisplayServer.TtsSpeak(clip.Text, clip.SystemVoiceId ?? "", Mathf.Clamp(Mathf.RoundToInt(volume * 100f), 0, 100),
				1.0f, (float)LauncherConfig.TtsRate, 0, true);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Sprachausgabe fehlgeschlagen: {ex.Message}");
		}
	}

	public static void Stop()
	{
		if (_player != null && GodotObject.IsInstanceValid(_player))
			_player.Stop();

		try
		{
			DisplayServer.TtsStop();
		}
		catch (Exception)
		{
			// Nothing to stop on platforms without speech support.
		}
	}

	public static bool IsSpeaking => _player != null && GodotObject.IsInstanceValid(_player) && _player.Playing;

	// ----- Cache -----

	public static string CacheDirectory => ProjectSettings.GlobalizePath("user://tts_cache");

	public static (int Files, long Bytes) CacheStats()
	{
		if (!Directory.Exists(CacheDirectory))
			return (0, 0);

		var files = new DirectoryInfo(CacheDirectory).GetFiles("*.wav");
		return (files.Length, files.Sum(file => file.Length));
	}

	public static void ClearCache()
	{
		if (!Directory.Exists(CacheDirectory))
			return;

		foreach (string file in Directory.GetFiles(CacheDirectory, "*.wav"))
		{
			try { File.Delete(file); } catch (Exception) { /* in use or already gone */ }
		}
	}

	// ----- Internals -----

	private static async Task<TtsClip> TrySynthesizeAsync(ITtsProvider provider, string characterName, string slot, string spoken)
	{
		if (!provider.IsReady(out string problem))
		{
			LlmLog.Add(LogName, LlmLogKind.Fehler, $"{provider.Name}: {problem}");
			return null;
		}

		var request = new TtsRequest(characterName, slot, spoken, LauncherConfig.GetVoice(provider.Kind, slot), LauncherConfig.TtsRate);
		string cachePath = Path.Combine(CacheDirectory, Hash(provider.CacheKey(request)) + ".wav");

		try
		{
			byte[] wav;
			string source;
			var watch = System.Diagnostics.Stopwatch.StartNew();

			if (File.Exists(cachePath))
			{
				wav = await File.ReadAllBytesAsync(cachePath);
				source = "Cache";
			}
			else
			{
				LlmLog.Add(LogName, LlmLogKind.Anfrage, $"{provider.Name} · {slot} · „{Shorten(spoken)}“");
				wav = await provider.SynthesizeAsync(request);
				source = provider.Name;

				Directory.CreateDirectory(CacheDirectory);
				await File.WriteAllBytesAsync(cachePath, wav);
			}

			AudioStreamWav stream = AudioStreamWav.LoadFromBuffer(wav);
			if (stream == null)
				throw new TtsException("Audiodaten konnten nicht gelesen werden");

			double seconds = stream.GetLength();
			LlmLog.Add(LogName, LlmLogKind.Antwort, $"{source}: {seconds:0.0} s Audio", watch.ElapsedMilliseconds);
			return new TtsClip(spoken, stream, seconds, null, source);
		}
		catch (TtsException ex)
		{
			LlmLog.Add(LogName, LlmLogKind.Fehler, ex.Message);
		}
		catch (Exception ex)
		{
			LlmLog.Add(LogName, LlmLogKind.Fehler, $"{provider.Name}: unerwarteter Fehler: {ex.Message}");
			TryDelete(cachePath);
		}

		return null;
	}

	private static TtsClip SystemClip(string slot, string spoken, string fallbackNote)
	{
		IReadOnlyList<VoiceInfo> voices = GetSystemVoices();
		if (voices.Count == 0)
		{
			LlmLog.Add(LogName, LlmLogKind.Fehler, "Keine Windows-Stimme verfügbar.");
			return null;
		}

		string configured = LauncherConfig.GetVoice(TtsProviderKind.System, slot);
		string voiceId = voices.Any(voice => voice.Id == configured) ? configured : voices[0].Id;

		if (fallbackNote != null)
			LlmLog.Add(LogName, LlmLogKind.Info, fallbackNote);

		// The system voice has no length we could read; estimate it so the text can still type along.
		double seconds = Math.Max(1.0, spoken.Length / (14.0 * LauncherConfig.TtsRate));
		return new TtsClip(spoken, null, seconds, voiceId, "Windows");
	}

	private static AudioStreamPlayer EnsurePlayer()
	{
		if (_player != null && GodotObject.IsInstanceValid(_player))
			return _player;

		if (Engine.GetMainLoop() is not SceneTree tree)
			return null;

		_player = new AudioStreamPlayer { Name = "TtsPlayer" };
		tree.Root.AddChild(_player);
		return _player;
	}

	private static void TryDelete(string path)
	{
		try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { /* ignore */ }
	}

	private static string Hash(string text) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32].ToLowerInvariant();

	private static string Shorten(string text) => text.Length > 60 ? text[..60] + "…" : text;
}
