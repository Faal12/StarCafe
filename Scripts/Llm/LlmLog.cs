using System;
using System.Collections.Generic;
using Godot;

public enum LlmLogKind
{
	Anfrage,
	Antwort,
	Fehler,
	Info
}

public sealed record LlmLogEntry(DateTime Time, string Provider, LlmLogKind Kind, string Text, long? DurationMs);

public static class LlmLog
{
	private const int Capacity = 300;
	private const int MaxTextLength = 4000;

	private static readonly List<LlmLogEntry> _entries = new();

	public static IReadOnlyList<LlmLogEntry> Entries => _entries;

	public static event Action<LlmLogEntry> EntryAdded;
	public static event Action Cleared;

	public static void Add(string provider, LlmLogKind kind, string text, long? durationMs = null)
	{
		if (text != null && text.Length > MaxTextLength)
			text = text.Substring(0, MaxTextLength) + "…";

		var entry = new LlmLogEntry(DateTime.Now, provider, kind, text ?? "", durationMs);
		_entries.Add(entry);
		if (_entries.Count > Capacity)
			_entries.RemoveAt(0);

		if (kind == LlmLogKind.Fehler)
			GD.PrintErr($"[{provider}] {entry.Text}");

		EntryAdded?.Invoke(entry);
	}

	public static void Clear()
	{
		_entries.Clear();
		Cleared?.Invoke();
	}
}
