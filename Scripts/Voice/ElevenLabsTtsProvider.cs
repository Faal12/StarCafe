using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>
/// Cloud voices from ElevenLabs. There is no fixed voice list: each slot takes a voice ID from the account.
/// Note: written against the public API documentation, not yet tried with a real key.
/// </summary>
public sealed class ElevenLabsTtsProvider : ITtsProvider
{
	private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

	private const int SampleRate = 24000;

	public TtsProviderKind Kind => TtsProviderKind.ElevenLabs;
	public string Name => "ElevenLabs (Cloud)";

	public bool IsReady(out string problem)
	{
		if (string.IsNullOrWhiteSpace(LauncherConfig.ResolveElevenLabsKey()))
		{
			problem = "Kein ElevenLabs-Schlüssel eingetragen.";
			return false;
		}

		problem = null;
		return true;
	}

	// Free text: the launcher shows an input field instead of a list.
	public IReadOnlyList<TtsVoice> GetVoices() => Array.Empty<TtsVoice>();

	public string CacheKey(TtsRequest request) =>
		$"elevenlabs|{LauncherConfig.ElevenLabsModel}|{request.VoiceId}|{request.Rate:0.00}|{request.Text}";

	public async Task<byte[]> SynthesizeAsync(TtsRequest request)
	{
		if (!IsReady(out string problem))
			throw new TtsException(problem);

		if (string.IsNullOrWhiteSpace(request.VoiceId))
			throw new TtsException($"ElevenLabs: für „{request.Slot}“ ist keine Voice-ID eingetragen");

		string key = LauncherConfig.ResolveElevenLabsKey();
		string body = JsonSerializer.Serialize(new
		{
			text = request.Text,
			model_id = LauncherConfig.ElevenLabsModel,
			voice_settings = new { speed = Math.Clamp(request.Rate, 0.7, 1.2) }
		});

		string url = $"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(request.VoiceId.Trim())}?output_format=pcm_{SampleRate}";

		try
		{
			using var message = new HttpRequestMessage(HttpMethod.Post, url)
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json")
			};
			message.Headers.Add("xi-api-key", key);

			using HttpResponseMessage response = await Http.SendAsync(message);
			if (!response.IsSuccessStatusCode)
			{
				string error = await response.Content.ReadAsStringAsync();
				error = Regex.Replace(error ?? "", @"\s+", " ").Replace(key, "***");
				throw new TtsException($"ElevenLabs: HTTP {(int)response.StatusCode}: {(error.Length > 300 ? error[..300] + "…" : error)}");
			}

			byte[] pcm = await response.Content.ReadAsByteArrayAsync();
			return WavFile.FromPcm16Mono(pcm, SampleRate);
		}
		catch (HttpRequestException ex)
		{
			throw new TtsException("ElevenLabs: keine Internetverbindung", ex);
		}
		catch (TaskCanceledException ex)
		{
			throw new TtsException("ElevenLabs: Zeitüberschreitung", ex);
		}
	}
}
