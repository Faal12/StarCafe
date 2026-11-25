using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Brings guests into the tavern during a shift: they enter through the airlock, walk along
/// the middle aisle and take a free seat (Marker2D children of "GuestSeats").
/// More guests during a conjunction. At the end of a shift everyone leaves.
/// </summary>
public partial class GuestDirector : Node2D
{
	[Export]
	public NodePath SeatsPath { get; set; } = new NodePath("../GuestSeats");

	[Export]
	public int MaxGuests { get; set; } = 4;

	[Export]
	public int MaxGuestsConjunction { get; set; } = 6;

	[Export]
	public float FirstGuestDelay { get; set; } = 4f;

	[Export]
	public float SpawnDelayMin { get; set; } = 18f;

	[Export]
	public float SpawnDelayMax { get; set; } = 36f;

	/// <summary>Where guests enter and leave.</summary>
	public static Vector2 Entrance = new Vector2(1060f, 1060f);

	/// <summary>The free aisle between the table rows.</summary>
	public const float AisleY = 640f;

	private static readonly string[] FactionPool =
	{
		"Silberkiel", "Silberkiel", "GraueHaende", "GraueHaende", "Sternenwacht", "Funkenwerkstatt", "Funkenwerkstatt",
		"Weitwanderer", "Weitwanderer", "Soeldner", "Soeldner", "Imperium", "Rebellen", ""
	};

	private readonly List<Vector2> _seats = new List<Vector2>();

	private readonly Dictionary<int, Guest> _occupied = new Dictionary<int, Guest>();

	private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();

	private double _timer;

	public IEnumerable<Guest> Guests => _occupied.Values;

	public override void _Ready()
	{
		_rng.Randomize();
		BarLibrary.EnsureLoaded();
		Node seats = GetNodeOrNull(SeatsPath);
		if (seats != null)
		{
			foreach (Node child in seats.GetChildren())
			{
				if (child is Node2D marker)
				{
					_seats.Add(marker.GlobalPosition);
				}
			}
		}
		if (_seats.Count == 0)
		{
			GD.PushWarning("Gäste: keine Sitzplätze (Knoten GuestSeats) gefunden.");
		}
		_timer = FirstGuestDelay;
	}

	public override void _EnterTree()
	{
		GameSession.ShiftEnded += OnShiftEnded;
		GameSession.ShiftStarted += OnShiftStarted;
	}

	public override void _ExitTree()
	{
		GameSession.ShiftEnded -= OnShiftEnded;
		GameSession.ShiftStarted -= OnShiftStarted;
		BarPanel.OpenOrders.Clear();
	}

	public override void _Process(double delta)
	{
		_timer -= delta;
		if (_timer > 0)
		{
			return;
		}
		_timer = _rng.RandfRange(SpawnDelayMin, SpawnDelayMax) * (GameSession.Clock.ConjunctionShift ? 0.6f : 1f);
		int limit = GameSession.Clock.ConjunctionShift ? MaxGuestsConjunction : MaxGuests;
		// Nobody comes into a dark tavern.
		if (_occupied.Count < limit && DeckManager.PowerOn)
		{
			SpawnGuest();
		}
	}

	/// <summary>Spawns one guest right away (also used for testing).</summary>
	public Guest SpawnGuest()
	{
		List<int> free = Enumerable.Range(0, _seats.Count).Where(i => !_occupied.ContainsKey(i)).ToList();
		if (free.Count == 0 || BarLibrary.Species.Count == 0 || BarLibrary.Orders.Count == 0)
		{
			return null;
		}
		int seat = free[_rng.RandiRange(0, free.Count - 1)];
		SpeciesDef species = PickSpecies();
		string role = species.Roles.Count > 0 ? species.Roles[_rng.RandiRange(0, species.Roles.Count - 1)] : "Gast";
		string name = UniqueName(role + " (" + species.Name + ")");
		string faction = FactionPool[_rng.RandiRange(0, FactionPool.Length - 1)];
		GuestOrder order = new GuestOrder { Template = PickOrder(species) };
		Guest guest = new Guest { Position = Entrance };
		guest.Setup(name, species, faction, order, seat);
		guest.Left += g => _occupied.Remove(g.SeatIndex);
		_occupied[seat] = guest;
		AddChild(guest);
		Vector2 target = _seats[seat];
		guest.WalkIn(new[] { new Vector2(Entrance.X, AisleY), new Vector2(target.X, AisleY), target });
		return guest;
	}

	/// <summary>Path from a seat back to the airlock.</summary>
	public static IReadOnlyList<Vector2> ExitPath(Vector2 from)
	{
		return new[] { new Vector2(from.X, AisleY), new Vector2(Entrance.X, AisleY), Entrance };
	}

	private void OnShiftEnded(ShiftSummary summary)
	{
		foreach (Guest guest in _occupied.Values.ToList())
		{
			guest.QueueFree();
		}
		_occupied.Clear();
		BarPanel.OpenOrders.Clear();
	}

	private void OnShiftStarted()
	{
		_timer = FirstGuestDelay;
	}

	private SpeciesDef PickSpecies()
	{
		int total = BarLibrary.Species.Sum(s => System.Math.Max(1, s.Weight));
		int roll = _rng.RandiRange(1, total);
		foreach (SpeciesDef species in BarLibrary.Species)
		{
			roll -= System.Math.Max(1, species.Weight);
			if (roll <= 0)
			{
				return species;
			}
		}
		return BarLibrary.Species[0];
	}

	/// <summary>An order this species can actually be satisfied with.</summary>
	private OrderTemplate PickOrder(SpeciesDef species)
	{
		List<OrderTemplate> fitting = BarLibrary.Orders.Where(o => DrinkMixer.Compatible(o, species)).ToList();
		if (fitting.Count == 0)
		{
			fitting = BarLibrary.Orders;
		}
		return fitting[_rng.RandiRange(0, fitting.Count - 1)];
	}

	private string UniqueName(string name)
	{
		string candidate = name;
		int n = 2;
		while (_occupied.Values.Any(g => g.DisplayName == candidate))
		{
			candidate = $"{name} {n++}";
		}
		return candidate;
	}
}
