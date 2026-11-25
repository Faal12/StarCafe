using System;
using System.Collections.Generic;
using Godot;

/// <summary>Names of the windows that block the game while open (see <see cref="UiBus.IsOpen"/>).</summary>
public static class UiWindow
{
	public const string Dialogue = "dialogue";
	public const string Event = "event";
	public const string Bar = "bar";
	public const string Order = "order";
	public const string Inventory = "inventory";
	public const string Circuit = "circuit";
	public const string Pressure = "pressure";
}

/// <summary>Mini-games that game logic can ask the UI to run (see <see cref="UiBus.RunMinigame"/>).</summary>
public static class MinigameId
{
	public const string Circuit = "circuit";
	public const string Pressure = "pressure";
}

/// <summary>
/// The contract between game logic (back end) and the interface (front end).
///
/// Logic never touches a window directly. It calls the methods here ("show this", "something happened"),
/// and the UI side (<c>UiHost</c> in <c>Scenes/Ui/TavernUi.tscn</c>) listens to the events and decides
/// how to show it. Windows report back which of them are open, so logic can wait (no events during a dialogue …).
///
/// Rule for the team: a new kind of message goes in here first (one method + one event), then each side does its part.
/// Renaming or removing something here needs both sides to agree.
/// </summary>
public static class UiBus
{
	// ───────────── Logic → UI ─────────────

	/// <summary>Short note at the bottom of the screen ("+₡12", "Kein Platz im Inventar." …).</summary>
	public static event Action<string, Color?> ToastRequested;

	public static void Toast(string text, Color? color = null)
	{
		if (ToastRequested == null)
		{
			GD.Print("[Hinweis] " + text);
			return;
		}
		ToastRequested(text, color);
	}

	/// <summary>Open the free conversation with an NPC (Floki).</summary>
	public static event Action<NpcPersona> DialogueRequested;

	public static void OpenDialogue(NpcPersona persona) => DialogueRequested?.Invoke(persona);

	/// <summary>Open the mixing screen at the bar.</summary>
	public static event Action BarRequested;

	public static void OpenBar() => BarRequested?.Invoke();

	/// <summary>A guest wants to order; <c>onAccept</c> is called when the player takes the order.</summary>
	public static event Action<Guest, Action> OrderRequested;

	public static void ShowOrder(Guest guest, Action onAccept) => OrderRequested?.Invoke(guest, onAccept);

	/// <summary>The player talks to a guest who is still waiting for the drink.</summary>
	public static event Action<Guest> WaitingRequested;

	public static void ShowWaiting(Guest guest) => WaitingRequested?.Invoke(guest);

	/// <summary>A drink was served and rated.</summary>
	public static event Action<Guest, string, DrinkRating> RatingRequested;

	public static void ShowRating(Guest guest, string drinkName, DrinkRating rating) => RatingRequested?.Invoke(guest, drinkName, rating);

	/// <summary>Start an event as a conversation.</summary>
	public static event Action<GameEventDef> EventRequested;

	public static void ShowEvent(GameEventDef def) => EventRequested?.Invoke(def);

	/// <summary>Show the result of an event after its task (no conversation).</summary>
	public static event Action<GameEventDef, EventOutcomeDef, EventResolution> AftermathRequested;

	public static void ShowAftermath(GameEventDef def, EventOutcomeDef outcome, EventResolution resolution) =>
		AftermathRequested?.Invoke(def, outcome, resolution);

	/// <summary>The player took damage (red flash, shake …).</summary>
	public static event Action PlayerHurt;

	public static void NotifyPlayerHurt() => PlayerHurt?.Invoke();

	/// <summary>Run a mini-game (<see cref="MinigameId"/>); <c>done(true)</c> = solved.</summary>
	public static event Action<string, Action<bool>> MinigameRequested;

	public static void RunMinigame(string id, Action<bool> done)
	{
		if (MinigameRequested == null)
		{
			GD.PushWarning($"UiBus: no UI for mini-game '{id}' – counted as failed.");
			done?.Invoke(false);
			return;
		}
		MinigameRequested(id, done);
	}

	// ───────────── UI → Logic ─────────────

	private static readonly HashSet<string> _open = new HashSet<string>();

	/// <summary>Raised when a window opens or closes (<see cref="UiWindow"/> name, open?).</summary>
	public static event Action<string, bool> WindowChanged;

	/// <summary>Windows call this when they open or close.</summary>
	public static void SetOpen(string window, bool open)
	{
		bool changed = open ? _open.Add(window) : _open.Remove(window);
		if (changed)
		{
			WindowChanged?.Invoke(window, open);
		}
	}

	public static bool IsOpen(string window) => _open.Contains(window);

	/// <summary>True while any blocking window is open.</summary>
	public static bool AnyOpen => _open.Count > 0;

	/// <summary>Forget all open windows (the UI scene calls this when it leaves the tree).</summary>
	public static void ResetOpen() => _open.Clear();
}
