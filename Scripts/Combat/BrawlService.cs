using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Tavern brawls that come out of event conversations ("Angreifen", or an angry speaker attacking).
/// The fight starts as soon as the event window is closed and runs in real time with the normal combat
/// system. Its result – <c>sieg</c>, <c>niederlage</c> or <c>flucht</c> – becomes the event's outcome.
/// Guests of the same faction may join in. Nobody dies: guests go down k.o., the player faints.
/// </summary>
public static class BrawlService
{
	private sealed record Pending(GameEventDef Def, bool StartedByPlayer);

	private static Pending _pending;

	private static BrawlFight _fight;

	static BrawlService()
	{
		UiBus.WindowChanged += OnWindowChanged;
	}

	/// <summary>A brawl is waiting or running (no new events, no elevator rides meanwhile).</summary>
	public static bool IsActive => _pending != null || (_fight != null && GodotObject.IsInstanceValid(_fight));

	/// <summary>Opponents still standing (for UI and tests).</summary>
	public static IReadOnlyList<Brawler> Opponents => IsActive && _fight != null ? _fight.Opponents : new List<Brawler>();

	/// <summary>Prepares a brawl; it starts when the event window closes.</summary>
	public static void Queue(GameEventDef def, bool startedByPlayer)
	{
		_pending = new Pending(def, startedByPlayer);
		if (!UiBus.IsOpen(UiWindow.Event))
		{
			Callable.From(StartPending).CallDeferred();
		}
	}

	private static void OnWindowChanged(string window, bool open)
	{
		if (window == UiWindow.Event && !open && _pending != null)
		{
			// Deferred: the window unpauses the game right after reporting "closed".
			Callable.From(StartPending).CallDeferred();
		}
	}

	private static void StartPending()
	{
		Pending pending = _pending;
		_pending = null;
		if (pending == null || Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		if (tree.GetFirstNodeInGroup("Player") is not Player player || player.GetParent() == null)
		{
			return;
		}
		_fight = new BrawlFight(pending.Def, pending.StartedByPlayer);
		player.GetParent().AddChild(_fight);
	}

	internal static void Finished(BrawlFight fight)
	{
		if (_fight == fight)
		{
			_fight = null;
		}
	}
}

/// <summary>One running brawl: spawns the opponents, watches who is still standing, ends with an outcome.</summary>
public partial class BrawlFight : Node2D
{
	private readonly GameEventDef _def;

	private readonly bool _startedByPlayer;

	private readonly List<Brawler> _opponents = new List<Brawler>();

	private Brawler _main;

	private int _witnesses;

	private bool _done;

	public IReadOnlyList<Brawler> Opponents => _opponents.Where(b => GodotObject.IsInstanceValid(b) && !b.OutOfFight).ToList();

	public BrawlFight()
	{
	}

	public BrawlFight(GameEventDef def, bool startedByPlayer)
	{
		_def = def;
		_startedByPlayer = startedByPlayer;
	}

	public override void _Ready()
	{
		Name = "Schlaegerei";
		if (_def == null || GetTree().GetFirstNodeInGroup("Player") is not Player player)
		{
			QueueFree();
			return;
		}
		EventSpeaker speaker = _def.Speaker;
		Vector2 at = player.GlobalPosition + new Vector2(player.GlobalPosition.X > 936f ? -120f : 120f, -10f);
		_main = Spawn(speaker.Name, speaker.Faction, speaker.Strength, ally: false, at);

		// Friends of the same faction jump in: strong speakers have one, mercenaries always bring one more.
		GuestDirector director = GetParent().GetNodeOrNull<GuestDirector>("Guests");
		List<Guest> guests = director?.Guests.Where(g => GodotObject.IsInstanceValid(g) && g.Current != Guest.State.Leaving).ToList() ?? new List<Guest>();
		int allies = (speaker.Strength >= 2 ? 1 : 0) + (speaker.Faction == "Soeldner" ? 1 : 0);
		foreach (Guest friend in guests.Where(g => !string.IsNullOrEmpty(speaker.Faction) && g.Faction == speaker.Faction).Take(allies).ToList())
		{
			Vector2 position = friend.GlobalPosition;
			string name = friend.DisplayName;
			guests.Remove(friend);
			friend.Leave();
			friend.QueueFree();
			Spawn(name, speaker.Faction, 1, ally: true, position);
		}
		_witnesses = guests.Count;

		if (PlayerCombat.Instance != null)
		{
			PlayerCombat.Instance.Fainted += OnPlayerFainted;
		}
		CombatFx.Shake(player.Camera, 5f, 6);
		UiBus.NotifyBrawl(true, speaker.Name);
		UiBus.Toast(_startedByPlayer ? $"Schlägerei mit {speaker.Name}! (Linksklick/Leertaste)" : $"{speaker.Name} geht auf dich los! (Linksklick/Leertaste)", new Color(1f, 0.6f, 0.4f));
		VoiceQueue.Enqueue(FlokiAdvisor.Name, _startedByPlayer ? "Chef! Nicht die guten Stühle!" : "Chef, Deckung! Und dann – na ja – zurückhauen.");
	}

	private Brawler Spawn(string name, string faction, int strength, bool ally, Vector2 at)
	{
		Brawler brawler = Brawler.Create(name, faction, strength, ally);
		brawler.GlobalPosition = at;
		GetParent().AddChild(brawler);
		_opponents.Add(brawler);
		return brawler;
	}

	public override void _Process(double delta)
	{
		if (_done || _main == null)
		{
			return;
		}
		bool mainGone = !GodotObject.IsInstanceValid(_main);
		if (_main.KnockedOut || (mainGone && !_main.Fled))
		{
			Finish(ConversationActions.Sieg);
		}
		else if (_main.Fled)
		{
			Finish(ConversationActions.Flucht);
		}
	}

	private void OnPlayerFainted()
	{
		Finish(ConversationActions.Niederlage);
	}

	private void Finish(string result)
	{
		if (_done)
		{
			return;
		}
		_done = true;
		if (PlayerCombat.Instance != null)
		{
			PlayerCombat.Instance.Fainted -= OnPlayerFainted;
		}
		// Without their leader the others give up.
		foreach (Brawler other in _opponents.Where(b => GodotObject.IsInstanceValid(b) && !b.OutOfFight))
		{
			other.Escape();
		}
		EventSpeaker speaker = _def.Speaker;
		List<string> tags = new List<string> { result, "gegner:" + speaker.Name, "ereignis:" + _def.Id, "angefangen:" + (_startedByPlayer ? "wirt" : "gast") };
		if (!string.IsNullOrEmpty(speaker.Faction))
		{
			tags.Add("fraktion:" + speaker.Faction);
		}
		string how = result switch
		{
			ConversationActions.Sieg => $"{speaker.Name} k.o. geschlagen",
			ConversationActions.Flucht => $"{speaker.Name} in die Flucht geschlagen",
			_ => $"gegen {speaker.Name} k.o. gegangen",
		};
		PlayerState.Record("schlaegerei", "Schlägerei im Schankraum: " + how + ".", tags, witnessed: _witnesses > 0);
		UiBus.NotifyBrawl(false, speaker.Name);

		EventOutcomeDef outcome = ConversationActions.BrawlOutcome(_def, result);
		// After fainting the player is carried to bed first.
		double wait = result == ConversationActions.Niederlage ? 3.4 : 1.6;
		GetTree().CreateTimer(wait).Timeout += () =>
		{
			EventResolution resolution = EventService.Resolve(_def, outcome);
			UiBus.ShowAftermath(_def, outcome, resolution);
			BrawlService.Finished(this);
			QueueFree();
		};
	}

	public override void _ExitTree()
	{
		if (PlayerCombat.Instance != null)
		{
			PlayerCombat.Instance.Fainted -= OnPlayerFainted;
		}
		BrawlService.Finished(this);
	}
}
