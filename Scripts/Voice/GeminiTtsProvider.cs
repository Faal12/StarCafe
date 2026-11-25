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

	// The narrator of events (situations, results): calm, warm, a little wink.
	private const string NarratorVoice = "Sadaltager";
	private const string NarratorStyle = "Auf Deutsch gesprochen, mit deutscher Aussprache und ohne fremden Akzent; ruhig, warm und bildhaft erzählend wie ein Hörbuchsprecher, mit einem Augenzwinkern";

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

		if (request.Slot == LauncherConfig.NarratorSlot)
			return NarratorVoice;

		uint hash = 2166136261;
		foreach (char c in request.CharacterName ?? "")
			hash = (hash ^ c) * 16777619;

		// Every other character gets a stable voice of their own, never Floki's or the narrator's.
		var pool = Catalog.Where(v => v.Name != FlokiVoice && v.Name != NarratorVoice).ToArray();
		return pool[(int)(hash % (uint)pool.Length)].Name;
	}

	private static string StyleFor(TtsRequest request) => request.Slot switch
	{
		"Floki" => FlokiStyle,
		LauncherConfig.NarratorSlot => NarratorStyle,
		_ => DefaultStyle,
	};

	private static byte[] ExtractAudio(string json)
	{
		try
		{
			using JsonDocument document = JsonDocument.Parse(json);
			// The response layout has changed before; search the whole document for audio instead of
			// relying on one fixed path ({"type":"audio","data":…} or {"inlineData":{"mimeType":"audio/…","data":…}}).
			string data = FindAudio(document.RootElement, 0);

			if (string.IsNullOrEmpty(data))
				throw new TtsException("Gemini-Stimme: Antwort ohne Audio (Aufbau: " + Shape(document.RootElement, 0) + ")");

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

	/// <summary>Finds base64 audio anywhere in the response.</summary>
	private static string FindAudio(JsonElement element, int depth)
	{
		if (depth > 12)
			return null;
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
			{
				bool isAudio = (element.TryGetProperty("type", out JsonElement type) && type.ValueKind == JsonValueKind.String && type.GetString() == "audio")
					|| IsAudioMime(element, "mimeType") || IsAudioMime(element, "mime_type");
				if (isAudio && element.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.String)
					return data.GetString();
				foreach (JsonProperty property in element.EnumerateObject())
				{
					string found = FindAudio(property.Value, depth + 1);
					if (!string.IsNullOrEmpty(found))
						return found;
				}
				return null;
			}
			case JsonValueKind.Array:
				foreach (JsonElement item in element.EnumerateArray())
				{
					string found = FindAudio(item, depth + 1);
					if (!string.IsNullOrEmpty(found))
						return found;
				}
				return null;
			default:
				return null;
		}
	}

	private static bool IsAudioMime(JsonElement element, string property) =>
		element.TryGetProperty(property, out JsonElement mime) && mime.ValueKind == JsonValueKind.String
		&& (mime.GetString() ?? "").StartsWith("audio", StringComparison.OrdinalIgnoreCase);

	/// <summary>Layout of a response without any values (for the log when no audio is found).</summary>
	private static string Shape(JsonElement element, int depth)
	{
		if (depth > 4)
			return "…";
		return element.ValueKind switch
		{
			JsonValueKind.Object => "{" + string.Join(", ", element.EnumerateObject().Take(8).Select(p => p.Name + ":" + Shape(p.Value, depth + 1))) + "}",
			JsonValueKind.Array => "[" + (element.GetArrayLength() > 0 ? Shape(element[0], depth + 1) : "") + "]",
			JsonValueKind.String => "text",
			JsonValueKind.Number => "zahl",
			_ => element.ValueKind.ToString().ToLowerInvariant(),
		};
	}

	private static string Describe(string body, string key)
	{
		try
		{
			using JsonDocument document = JsonDocument.Parse(body);
			// Errors come as {"error":{…}} or as [{"error":{…}}] (e.g. HTTP 402 "prepayment credits are depleted").
			JsonElement root = document.RootElement;
			if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
				root = root[0];
			if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out JsonElement error)
				&& error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out JsonElement message))
				body = message.GetString();
		}
		catch (JsonException)
		{
			// plain text body
		}
		catch (InvalidOperationException)
		{
			// unexpected layout – keep the raw body
		}

		body = Regex.Replace(body ?? "", @"\s+", " ").Trim().Replace(key, "***");
		return body.Length > 300 ? body[..300] + "…" : body;
	}
}
