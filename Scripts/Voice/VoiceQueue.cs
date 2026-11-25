using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Plays voice lines one after another (narrator, speaker, Floki …) instead of cutting each other off.
/// Lines are synthesized as soon as they are queued, so the next one is usually ready when the
/// previous one ends. <see cref="Clear"/> stops everything (e.g. when the player answers).
/// </summary>
public static class VoiceQueue
{
	private const int PollMs = 120;

	private static readonly Queue<Task<TtsClip>> Pending = new Queue<Task<TtsClip>>();

	private static int _generation;

	private static bool _running;

	public static bool IsBusy => _running || TtsService.IsSpeakingAny;

	/// <summary>Queues a line. characterName picks the voice: "Floki", <see cref="LauncherConfig.NarratorSlot"/> or any figure name.</summary>
	public static void Enqueue(string characterName, string text)
	{
		if (!LauncherConfig.TtsEnabled || TtsService.CleanForSpeech(text).Length == 0)
		{
			return;
		}
		Pending.Enqueue(TtsService.PrepareAsync(characterName, text));
		if (!_running)
		{
			Run(_generation);
		}
	}

	/// <summary>Stops the current line and drops everything queued.</summary>
	public static void Clear()
	{
		_generation++;
		Pending.Clear();
		_running = false;
		TtsService.Stop();
	}

	private static async void Run(int generation)
	{
		_running = true;
		while (generation == _generation && Pending.Count > 0)
		{
			TtsClip clip;
			try
			{
				clip = await Pending.Dequeue();
			}
			catch (System.Exception ex)
			{
				LlmLog.Add("Stimme", LlmLogKind.Fehler, "Vorlesen fehlgeschlagen: " + ex.Message);
				clip = null;
			}
			if (generation != _generation)
			{
				return;
			}
			if (clip == null)
			{
				continue;
			}
			TtsService.Play(clip);
			await WaitUntilDone(generation, clip);
		}
		if (generation == _generation)
		{
			_running = false;
		}
	}

	private static async Task WaitUntilDone(int generation, TtsClip clip)
	{
		// The Windows voice needs a moment before it reports "speaking".
		await Task.Delay(400);
		int limitMs = (int)((clip.Seconds + 6.0) * 1000.0);
		int waited = 400;
		while (generation == _generation && TtsService.IsSpeakingAny && waited < limitMs)
		{
			await Task.Delay(PollMs);
			waited += PollMs;
		}
	}
}
