using System.Collections.Generic;
using Godot;

/// <summary>
/// Lives in the tavern scene. At the start of every shift it plans a few events
/// (see <see cref="EventService.EventsPerShift"/>) and triggers them after a delay,
/// whenever no dialogue, menu or other event is open.
/// Debug builds: Ctrl+Shift+E (action "debug_event") triggers a random event immediately (ignores cooldowns).
/// </summary>
public partial class EventDirector : Node
{
	[Export]
	public float FirstDelayMin { get; set; } = 6f;

	[Export]
	public float FirstDelayMax { get; set; } = 12f;

	[Export]
	public float BetweenDelayMin { get; set; } = 25f;

	[Export]
	public float BetweenDelayMax { get; set; } = 45f;

	private int _remaining;

	private double _timer;

	private readonly HashSet<string> _usedThisShift = new HashSet<string>();

	public override void _Ready()
	{
		StartShift();
	}

	public override void _EnterTree()
	{
		GameSession.ShiftStarted += StartShift;
	}

	public override void _ExitTree()
	{
		GameSession.ShiftStarted -= StartShift;
	}

	private void StartShift()
	{
		_usedThisShift.Clear();
		_remaining = EventService.EventsPerShift(GameSession.Clock.ConjunctionShift);
		_timer = GD.RandRange(FirstDelayMin, FirstDelayMax);
	}

	public override void _Process(double delta)
	{
		// STARCAFE_NO_EVENTS=1 switches automatic events off (for testing other systems).
		if (_remaining <= 0 || OS.GetEnvironment("STARCAFE_NO_EVENTS") == "1")
		{
			return;
		}
		_timer -= delta;
		if (_timer > 0)
		{
			return;
		}
		if (IsBusy())
		{
			_timer = 1.0;
			return;
		}
		_remaining--;
		_timer = GD.RandRange(BetweenDelayMin, BetweenDelayMax);
		Trigger(ignoreCooldown: false);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Not F8: in the Godot editor F8 stops the running game.
		if (OS.IsDebugBuild() && @event.IsActionPressed("debug_event") && !@event.IsEcho() && !IsBusy())
		{
			GetViewport().SetInputAsHandled();
			if (!Trigger(ignoreCooldown: true))
			{
				GD.Print("Ereignisse: kein passendes Ereignis gefunden.");
			}
		}
	}

	private bool Trigger(bool ignoreCooldown)
	{
		// Debug: STARCAFE_EVENT=<id> makes Ctrl+Shift+E open exactly that event (ignores all conditions).
		string forced = ignoreCooldown ? OS.GetEnvironment("STARCAFE_EVENT") : "";
		GameEventDef def = !string.IsNullOrWhiteSpace(forced)
			? System.Linq.Enumerable.FirstOrDefault(EventLibrary.Events, e => e.Id == forced.Trim())
			: EventService.Pick(ignoreCooldown, ignoreCooldown ? null : _usedThisShift);
		if (def == null)
		{
			return false;
		}
		_usedThisShift.Add(def.Id);
		UiBus.ShowEvent(def);
		return true;
	}

	private bool IsBusy()
	{
		// No events while a task or brawl runs, behind the bar, below deck, or during the elevator ride.
		// (The bar view no longer pauses the game: guests keep waiting while you mix.)
		return UiBus.IsOpen(UiWindow.Event) || UiBus.IsOpen(UiWindow.Dialogue) || UiBus.IsOpen(UiWindow.Bar) || GetTree().Paused || TaskService.IsActive || BrawlService.IsActive || !DeckManager.InTavern || DeckManager.IsTraveling;
	}
}
