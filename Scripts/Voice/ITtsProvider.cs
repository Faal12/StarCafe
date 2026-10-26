using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public enum TtsProviderKind
{
	Piper,
	Gemini,
	ElevenLabs,
	System
}

public sealed record TtsVoice(string Id, string Label);

/// <param name="CharacterName">Who speaks; lets a provider pick a stable voice per character.</param>
/// <param name="Slot">Voice slot from the launcher ("Floki", "Standard").</param>
/// <param name="VoiceId">Chosen voice, empty = automatic.</param>
/// <param name="Rate">Speaking rate, 1.0 = normal.</param>
public sealed record TtsRequest(string CharacterName, string Slot, string Text, string VoiceId, double Rate);

public sealed class TtsException : Exception
{
	public TtsException(string message) : base(message) { }
	public TtsException(string message, Exception inner) : base(message, inner) { }
}

public interface ITtsProvider
{
	TtsProviderKind Kind { get; }
	string Name { get; }

	/// <summary>False with a readable reason when the provider cannot speak right now (missing files, key, voices).</summary>
	bool IsReady(out string problem);

	IReadOnlyList<TtsVoice> GetVoices();

	/// <summary>Everything that changes the audio, so identical lines can come from the cache.</summary>
	string CacheKey(TtsRequest request);

	/// <summary>Returns a complete RIFF/WAV file (16-bit PCM). Throws TtsException with a readable message.</summary>
	Task<byte[]> SynthesizeAsync(TtsRequest request);
}

public static class WavFile
{
	/// <summary>Wraps headerless 16-bit mono PCM into a WAV container.</summary>
	public static byte[] FromPcm16Mono(byte[] pcm, int sampleRate)
	{
		using var stream = new System.IO.MemoryStream(44 + pcm.Length);
		using var writer = new System.IO.BinaryWriter(stream);
		writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
		writer.Write(36 + pcm.Length);
		writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
		writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
		writer.Write(16);
		writer.Write((short)1);
		writer.Write((short)1);
		writer.Write(sampleRate);
		writer.Write(sampleRate * 2);
		writer.Write((short)2);
		writer.Write((short)16);
		writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
		writer.Write(pcm.Length);
		writer.Write(pcm);
		writer.Flush();
		return stream.ToArray();
	}
}
