using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>Gemini neural voices (same API key as the dialogue AI). Uses the Interactions endpoint of the TTS models.</summary>
public sealed class GeminiTtsProvider : ITtsProvider
{
	private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

	private const int MaxAttempts = 3;
	private const int RetryDelayMs = 1500;

	// The model picks the language from the text, but an English style makes it drift into an English accent,
	// so the style is written in German and states the language and pronunciation explicitly.
	private const string FlokiStyle = "Auf Deutsch gesprochen, mit deutscher Aussprache und ohne fremden Akzent; trocken, sarkastisch und leicht selbstgefällig, wie eine gelangweilte Besserwisser-Maschine; schnell und knapp";
	private const string DefaultStyle = "Auf Deutsch gesprochen, mit deutscher Aussprache und ohne fremden Akzent; natürlich, entspannt und im Plauderton";

	// The 30 prebuilt voices of the Gemini TTS models (name, character).
	private static readonly (string Name, string Trait)[] Catalog =
	{
		("Zephyr", "hell"), ("Puck", "munter"), ("Charon", "sachlich"), ("Kore", "bestimmt"), ("Fenrir", "aufgeregt"), ("Leda", "jugendlich"),
		("Orus", "bestimmt"), ("Aoede", "luftig"), ("Callirrhoe", "gelassen"), ("Autonoe", "hell"), ("Enceladus", "hauchig"), ("Iapetus", "klar"),
		("Umbriel", "gelassen"), ("Algieba", "weich"), ("Despina", "weich"), ("Erinome", "klar"), ("Algenib", "rau"), ("Rasalgethi", "sachlich"),
		("Laomedeia", "munter"), ("Achernar", "sanft"), ("Alnilam", "bestimmt"), ("Schedar", "ausgeglichen"), ("Gacrux", "reif"), ("Pulcherrima", "direkt"),
		("Achird", "freundlich"), ("Zubenelgenubi", "locker"), ("Vindemiatrix", "mild"), ("Sadachbia", "lebhaft"), ("Sadaltager", "kenntnisreich"), ("Sulafat", "warm"),
	};

	private const string FlokiVoice = "Algenib";

	public TtsProviderKind Kind => TtsProviderKind.Gemini;
	public string Name => "Gemini (Cloud)";

	public bool IsReady(out string problem)
	{
		if (!LauncherConfig.HasGeminiKey)
		{
			problem = "Kein Gemini-Schlüssel (siehe Abschnitt Dialog-KI).";
			return false;
		}

		problem = null;
		return true;
	}

	public IReadOnlyList<TtsVoice> GetVoices() =>
		Catalog.Select(v => new TtsVoice(v.Name, $"{v.Name} ({v.Trait})")).ToList();

	public string CacheKey(TtsRequest request) =>
		$"gemini|{LauncherConfig.GeminiTtsModel}|{ResolveVoice(request)}|{StyleFor(request)}|{request.Text}";

	public async Task<byte[]> SynthesizeAsync(TtsRequest request)
	{
		if (!IsReady(out string problem))
			throw new TtsException(problem);

		string apiKey = LauncherConfig.ResolveGeminiKey();
		string body = JsonSerializer.Serialize(new
		{
			model = LauncherConfig.GeminiTtsModel,
			input = new object[]
			{
				new
				{
					type = "user_input",
					content = new object[]
					{
						new
						{
							type = "text",
							text = request.Text,
							annotations = new object[] { new { type = "speech_metadata", style = StyleFor(request) } }
						}
					}
				}
			},
			response_format = new { type = "audio" },
			generation_config = new { speech_config = new object[] { new { voice = ResolveVoice(request) } } }
		});

		for (int attempt = 1; attempt <= MaxAttempts; attempt++)
		{
			bool last = attempt == MaxAttempts;
			try
			{
				using var message = new HttpRequestMessage(HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/interactions")
				{
					Content = new StringContent(body, Encoding.UTF8, "application/json")
				};
				message.Headers.Add("x-goog-api-key", apiKey);

				using HttpResponseMessage response = await Http.SendAsync(message);
				string text = await response.Content.ReadAsStringAsync();

				if (response.IsSuccessStatusCode)
					return ExtractAudio(text);

				int code = (int)response.StatusCode;
				bool temporary = code == 429 || code == 503;
				if (!temporary || last)
					throw new TtsException($"Gemini-Stimme: HTTP {code}: {Describe(text, apiKey)}");
			}
			catch (HttpRequestException ex)
			{
				if (last)
					throw new TtsException("Gemini-Stimme: keine Internetverbindung", ex);
			}
			catch (TaskCanceledException ex)
			{
				if (last)
					throw new TtsException("Gemini-Stimme: Zeitüberschreitung", ex);
			}

			await Task.Delay(RetryDelayMs);
		}

		throw new TtsException("Gemini-Stimme: keine Antwort");
	}

	private static string ResolveVoice(TtsRequest request)
	{
		if (!string.IsNullOrEmpty(request.VoiceId) && Catalog.Any(v => v.Name == request.VoiceId))
			return request.VoiceId;

		if (request.Slot == "Floki")
			return FlokiVoice;

		uint hash = 2166136261;
		foreach (char c in request.CharacterName ?? "")
			hash = (hash ^ c) * 16777619;

		var pool = Catalog.Where(v => v.Name != FlokiVoice).ToArray();
		return pool[(int)(hash % (uint)pool.Length)].Name;
	}

	private static string StyleFor(TtsRequest request) => request.Slot == "Floki" ? FlokiStyle : DefaultStyle;

	private static byte[] ExtractAudio(string json)
	{
		try
		{
			using JsonDocument document = JsonDocument.Parse(json);
			string data = null;

			if (document.RootElement.TryGetProperty("steps", out JsonElement steps))
				foreach (JsonElement step in steps.EnumerateArray())
				{
					if (!step.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.Array)
						continue;

					foreach (JsonElement part in content.EnumerateArray())
						if (part.TryGetProperty("type", out JsonElement type) && type.GetString() == "audio"
							&& part.TryGetProperty("data", out JsonElement payload))
							data = payload.GetString();
				}

			if (string.IsNullOrEmpty(data))
				throw new TtsException("Gemini-Stimme: Antwort ohne Audio");

			byte[] bytes = Convert.FromBase64String(data);

			// Unary requests return a WAV file; be tolerant if a model answers with headerless PCM.
			bool isWav = bytes.Length > 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F';
			return isWav ? bytes : WavFile.FromPcm16Mono(bytes, 24000);
		}
		catch (JsonException ex)
		{
			throw new TtsException("Gemini-Stimme: ungültige Antwort des Servers", ex);
		}
		catch (FormatException ex)
		{
			throw new TtsException("Gemini-Stimme: Audiodaten nicht lesbar", ex);
		}
	}

	private static string Describe(string body, string key)
	{
		try
		{
			using JsonDocument document = JsonDocument.Parse(body);
			if (document.RootElement.TryGetProperty("error", out JsonElement error)
				&& error.TryGetProperty("message", out JsonElement message))
				body = message.GetString();
		}
		catch (JsonException)
		{
			// plain text body
		}

		body = Regex.Replace(body ?? "", @"\s+", " ").Trim().Replace(key, "***");
		return body.Length > 300 ? body[..300] + "…" : body;
	}
}
