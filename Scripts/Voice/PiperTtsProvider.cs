using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Offline neural voices through the Piper command line program.
/// Files live in ./voices (piper/piper.exe and models/*.onnx), fetched by Tools/get_voices.py.
/// </summary>
public sealed class PiperTtsProvider : ITtsProvider
{
	private const int TimeoutSeconds = 40;
	private const string PreferredFlokiModel = "de_DE-thorsten_emotional-medium";
	private const string PreferredFlokiSpeaker = "amused";

	private IReadOnlyList<TtsVoice> _voices;

	public TtsProviderKind Kind => TtsProviderKind.Piper;
	public string Name => "Piper (offline)";

	public static string VoicesRoot
	{
		get
		{
			string project = ProjectSettings.GlobalizePath("res://voices");
			if (Directory.Exists(project))
				return project;

			string beside = Path.Combine(OS.GetExecutablePath().GetBaseDir(), "voices");
			return Directory.Exists(beside) ? beside : project;
		}
	}

	private static string ExePath => Path.Combine(VoicesRoot, "piper", "piper.exe");
	private static string ModelsDir => Path.Combine(VoicesRoot, "models");

	public bool IsReady(out string problem)
	{
		if (!File.Exists(ExePath))
		{
			problem = "Piper fehlt. Voice-Paket laden: python Tools/get_voices.py (Ordner voices/).";
			return false;
		}

		if (GetVoices().Count == 0)
		{
			problem = "Keine Piper-Stimmen gefunden (voices/models/*.onnx).";
			return false;
		}

		problem = null;
		return true;
	}

	public IReadOnlyList<TtsVoice> GetVoices() => _voices ??= LoadVoices();

	public string CacheKey(TtsRequest request) =>
		$"piper|{ResolveVoice(request)}|{Rate(request):0.00}|{request.Text}";

	public async Task<byte[]> SynthesizeAsync(TtsRequest request)
	{
		if (!IsReady(out string problem))
			throw new TtsException(problem);

		(string model, int? speaker) = ParseVoice(ResolveVoice(request));
		string modelPath = Path.Combine(ModelsDir, model + ".onnx");
		string outputPath = Path.Combine(Path.GetTempPath(), $"letzte_runde_tts_{Guid.NewGuid():N}.wav");

		var args = new StringBuilder();
		args.Append($"--model \"{modelPath}\" --output_file \"{outputPath}\" --length_scale {Rate(request).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");
		if (speaker.HasValue)
			args.Append($" --speaker {speaker.Value}");

		var info = new ProcessStartInfo(ExePath, args.ToString())
		{
			WorkingDirectory = Path.GetDirectoryName(ExePath),
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			StandardInputEncoding = new UTF8Encoding(false)
		};

		try
		{
			using Process process = Process.Start(info) ?? throw new TtsException("Piper konnte nicht gestartet werden");

			Task<string> error = process.StandardError.ReadToEndAsync();
			Task<string> output = process.StandardOutput.ReadToEndAsync();
			await process.StandardInput.WriteLineAsync(request.Text.Replace('\n', ' '));
			process.StandardInput.Close();

			using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
			try
			{
				await process.WaitForExitAsync(timeout.Token);
			}
			catch (OperationCanceledException)
			{
				try { process.Kill(true); } catch (Exception) { /* already gone */ }
				throw new TtsException($"Piper antwortete nicht innerhalb von {TimeoutSeconds} s");
			}

			await Task.WhenAll(error, output);

			if (process.ExitCode != 0 || !File.Exists(outputPath))
				throw new TtsException($"Piper-Fehler (Code {process.ExitCode}): {Shorten(error.Result)}");

			return await File.ReadAllBytesAsync(outputPath);
		}
		catch (System.ComponentModel.Win32Exception ex)
		{
			throw new TtsException($"Piper konnte nicht gestartet werden: {ex.Message}", ex);
		}
		finally
		{
			try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch (Exception) { /* temp file, ignore */ }
		}
	}

	// A voice id is "<model>" or "<model>|<speaker>". Empty means: pick one automatically.
	private string ResolveVoice(TtsRequest request)
	{
		var voices = GetVoices();
		if (voices.Count == 0)
			return "";

		if (!string.IsNullOrEmpty(request.VoiceId) && voices.Any(v => v.Id == request.VoiceId))
			return request.VoiceId;

		if (request.Slot == "Floki")
		{
			var floki = voices.FirstOrDefault(v => v.Id.StartsWith(PreferredFlokiModel + "|") && v.Label.EndsWith(PreferredFlokiSpeaker))
				?? voices.FirstOrDefault(v => v.Id.StartsWith(PreferredFlokiModel));
			if (floki != null)
				return floki.Id;
		}

		// Everybody else: a stable voice per character, never the emotional Floki voices.
		var pool = voices.Where(v => !v.Id.StartsWith(PreferredFlokiModel)).ToList();
		if (pool.Count == 0)
			pool = voices.ToList();

		return pool[StableIndex(request.CharacterName, pool.Count)].Id;
	}

	private static int StableIndex(string text, int count)
	{
		uint hash = 2166136261;
		foreach (char c in text ?? "")
			hash = (hash ^ c) * 16777619;
		return (int)(hash % (uint)count);
	}

	private static double Rate(TtsRequest request) => Math.Clamp(1.0 / Math.Max(request.Rate, 0.1), 0.5, 2.0);

	private static (string Model, int? Speaker) ParseVoice(string voiceId)
	{
		int bar = voiceId.IndexOf('|');
		if (bar < 0)
			return (voiceId, null);

		return int.TryParse(voiceId[(bar + 1)..], out int speaker) ? (voiceId[..bar], speaker) : (voiceId[..bar], null);
	}

	private static IReadOnlyList<TtsVoice> LoadVoices()
	{
		var voices = new List<TtsVoice>();
		if (!Directory.Exists(ModelsDir))
			return voices;

		foreach (string modelPath in Directory.GetFiles(ModelsDir, "*.onnx").OrderBy(p => p))
		{
			string model = Path.GetFileNameWithoutExtension(modelPath);
			string label = PrettyModelName(model);
			string configPath = modelPath + ".json";
			if (!File.Exists(configPath))
				continue;

			try
			{
				using JsonDocument config = JsonDocument.Parse(File.ReadAllText(configPath));
				int speakers = config.RootElement.TryGetProperty("num_speakers", out JsonElement count) ? count.GetInt32() : 1;

				if (speakers <= 1)
				{
					voices.Add(new TtsVoice(model, label));
					continue;
				}

				var named = new SortedDictionary<int, string>();
				if (config.RootElement.TryGetProperty("speaker_id_map", out JsonElement map))
					foreach (JsonProperty entry in map.EnumerateObject())
						named[entry.Value.GetInt32()] = entry.Name;

				for (int id = 0; id < speakers; id++)
				{
					string speakerName = named.TryGetValue(id, out string name) && !int.TryParse(name, out _) ? name : $"Sprecher {id + 1}";
					voices.Add(new TtsVoice($"{model}|{id}", $"{label} – {speakerName}"));
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"Piper-Modell {model} konnte nicht gelesen werden: {ex.Message}");
			}
		}

		return voices;
	}

	private static string PrettyModelName(string model)
	{
		string[] parts = model.Split('-');
		string name = parts.Length >= 2 ? parts[1].Replace('_', ' ') : model;
		return char.ToUpperInvariant(name[0]) + name[1..];
	}

	private static string Shorten(string text)
	{
		text = (text ?? "").Trim();
		return text.Length > 300 ? text[..300] + "…" : text;
	}
}
