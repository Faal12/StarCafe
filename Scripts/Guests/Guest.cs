using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// A guest in the tavern (placeholder figure). Walks in, sits down, wants to order (!),
/// waits for the drink (…), drinks and leaves. Interact with E.
/// Waiting too long makes them leave annoyed. Everything they experience is recorded as a deed.
/// </summary>
public partial class Guest : Node2D
{
	public enum State { Arriving, WantsToOrder, WaitingForDrink, Drinking, Leaving }

	private const float WalkSpeed = 120f;

	private const float InteractRadius = 62f;

	private const double OrderPatience = 75.0;

	private const double DrinkPatience = 160.0;

	private const double DrinkingTime = 8.0;

	public string Id { get; private set; } = System.Guid.NewGuid().ToString("N").Substring(0, 8);

	public string DisplayName { get; private set; } = "Gast";

	public SpeciesDef Species { get; private set; }

	public string Faction { get; private set; } = "";

	public GuestOrder Order { get; private set; }

	public State Current { get; private set; } = State.Arriving;

	public int SeatIndex { get; private set; } = -1;

	private Label _bubble;

	private Control _patienceBar;

	private Panel _patienceFill;

	private Label _prompt;

	private double _patience;

	private double _patienceMax;

	private bool _playerInRange;

	public System.Action<Guest> Left;

	public void Setup(string name, SpeciesDef species, string faction, GuestOrder order, int seat)
	{
		DisplayName = name;
		Species = species;
		Faction = faction;
		Order = order;
		Order.GuestId = Id;
		Order.GuestName = name;
		Order.Species = species;
		Order.Faction = faction;
		SeatIndex = seat;
	}

	public override void _Ready()
	{
		Name = "Gast_" + Id;
		WorldStage.BuildCharacterPlaceholder(this, new WorldItem { Label = DisplayName, Faction = Faction, Scale = Species?.Id == "vhorren" ? 1.15f : (Species?.Id == "quirren" ? 0.75f : 1f) });
		// Above the name label: patience bar, then the bubble (! / …), then the key prompt.
		_patienceBar = new Panel { Position = new Vector2(-20f, -116f), Size = new Vector2(40f, 4f), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
		StyleBoxFlat bg = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.55f) };
		bg.SetCornerRadiusAll(2);
		_patienceBar.AddThemeStyleboxOverride("panel", bg);
		AddChild(_patienceBar);
		_patienceFill = new Panel { Size = new Vector2(40f, 4f), MouseFilter = Control.MouseFilterEnum.Ignore };
		_patienceBar.AddChild(_patienceFill);
		_bubble = new Label { Position = new Vector2(-20f, -146f), Size = new Vector2(40f, 26f), HorizontalAlignment = HorizontalAlignment.Center };
		_bubble.AddThemeFontSizeOverride("font_size", 22);
		_bubble.AddThemeColorOverride("font_outline_color", Colors.Black);
		_bubble.AddThemeConstantOverride("outline_size", 6);
		AddChild(_bubble);
		_prompt = new Label { Position = new Vector2(-70f, -168f), Size = new Vector2(140f, 20f), HorizontalAlignment = HorizontalAlignment.Center, Visible = false };
		_prompt.AddThemeFontSizeOverride("font_size", 12);
		_prompt.AddThemeColorOverride("font_color", Holo.Accent);
		_prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
		_prompt.AddThemeConstantOverride("outline_size", 4);
		AddChild(_prompt);
		Area2D area = new Area2D { CollisionMask = 1, Monitorable = false };
		area.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = InteractRadius }, Position = new Vector2(0f, -10f) });
		area.BodyEntered += body => { if (body.IsInGroup("Player")) { _playerInRange = true; UpdatePrompt(); } };
		area.BodyExited += body => { if (body.IsInGroup("Player")) { _playerInRange = false; UpdatePrompt(); } };
		AddChild(area);
		UpdateBubble();
	}

	/// <summary>Walks along the given points, then sits and wants to order.</summary>
	public void WalkIn(IReadOnlyList<Vector2> path)
	{
		Walk(path, () => SetState(State.WantsToOrder));
	}

	public override void _Process(double delta)
	{
		ZIndex = Mathf.Clamp(Mathf.RoundToInt(Position.Y), -3999, 4000);
		if (Current is State.WantsToOrder or State.WaitingForDrink)
		{
			_patience -= delta;
			float left = (float)Mathf.Clamp(_patience / _patienceMax, 0, 1);
			_patienceFill.Size = new Vector2(40f * left, 4f);
			Color color = left > 0.5f ? Holo.Accent : (left > 0.25f ? Holo.Strong : new Color(1f, 0.4f, 0.35f));
			StyleBoxFlat fill = new StyleBoxFlat { BgColor = color };
			fill.SetCornerRadiusAll(2);
			_patienceFill.AddThemeStyleboxOverride("panel", fill);
			if (_patience <= 0)
			{
				LeaveAnnoyed();
			}
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_playerInRange || !@event.IsActionPressed("interact") || @event.IsEcho())
		{
			return;
		}
		if (Current == State.WantsToOrder)
		{
			GetViewport().SetInputAsHandled();
			UiBus.ShowOrder(this, AcceptOrder);
		}
		else if (Current == State.WaitingForDrink)
		{
			GetViewport().SetInputAsHandled();
			TryServe();
		}
	}

	private void AcceptOrder()
	{
		if (Current != State.WantsToOrder)
		{
			return;
		}
		OrderBook.Open.Add(Order);
		SetState(State.WaitingForDrink);
		PlayerState.Record("bestellung_aufgenommen", $"Bestellung von {DisplayName} aufgenommen.", Tags());
	}

	/// <summary>Serves the selected drink (or one made for this guest, or any drink).</summary>
	private void TryServe()
	{
		Inventory inventory = PlayerState.Inventory;
		int slot = -1;
		if (inventory.SelectedStack?.ItemId == "getraenk")
		{
			slot = inventory.Selected;
		}
		else
		{
			var drinks = inventory.All().Where(s => s.Stack.ItemId == "getraenk").ToList();
			var mine = drinks.FirstOrDefault(s => s.Stack.Get("fuer") == Id);
			slot = mine.Stack != null ? mine.Slot : (drinks.Count > 0 ? drinks[0].Slot : -1);
		}
		if (slot < 0)
		{
			UiBus.ShowWaiting(this);
			return;
		}
		ItemStack drink = inventory.TakeSlot(slot);
		DrinkRating rating = DrinkMixer.Rate(Order, drink);
		OrderBook.Open.Remove(Order);
		if (rating.Tip > 0)
		{
			PlayerState.AddCredits(rating.Tip, "Trinkgeld von " + DisplayName);
		}
		if (EventLibrary.TryFaction(Faction, out Faction faction))
		{
			if (rating.Score >= 90)
			{
				ReputationService.State.Change(faction, 1, $"{DisplayName} war begeistert ({drink.DisplayName})", applyRivalry: false);
			}
			else if (rating.Score < 40)
			{
				ReputationService.State.Change(faction, -1, $"{DisplayName} war enttäuscht ({drink.DisplayName})", applyRivalry: false);
			}
		}
		List<string> tags = Tags();
		tags.Add("rezept:" + (drink.Get("recipe") is { Length: > 0 } r ? r : "eigenkreation"));
		tags.Add("bewertung:" + rating.Band);
		foreach (string effect in (drink.Get("effekte") ?? "").Split(',', System.StringSplitOptions.RemoveEmptyEntries))
		{
			tags.Add("wirkung:" + effect);
		}
		PlayerState.Record("serviert", $"{drink.DisplayName} an {DisplayName} serviert ({rating.Band}).", tags);
		UiBus.ShowRating(this, drink.DisplayName, rating);
		SetState(State.Drinking);
		_bubble.Text = rating.Band switch
		{
			"perfekt" => "♥",
			"gut" => "☺",
			"mittel" => "~",
			_ => "✗",
		};
		GetTree().CreateTimer(DrinkingTime, processAlways: false).Timeout += Leave;
	}

	private void LeaveAnnoyed()
	{
		OrderBook.Open.Remove(Order);
		if (EventLibrary.TryFaction(Faction, out Faction faction))
		{
			ReputationService.State.Change(faction, -1, $"{DisplayName} hat zu lange gewartet", applyRivalry: false);
		}
		PlayerState.Record("warten_lassen", $"{DisplayName} ist nach langem Warten gegangen.", Tags());
		UiBus.Toast($"{DisplayName} ist verärgert gegangen.", new Color(1f, 0.6f, 0.5f));
		Leave();
	}

	/// <summary>Leaves through the airlock.</summary>
	public void Leave()
	{
		if (Current == State.Leaving)
		{
			return;
		}
		OrderBook.Open.Remove(Order);
		SetState(State.Leaving);
		Left?.Invoke(this);
		Walk(GuestDirector.ExitPath(Position), () =>
		{
			Tween fade = CreateTween();
			fade.TweenProperty(this, "modulate:a", 0f, 0.4);
			fade.TweenCallback(Callable.From(QueueFree));
		});
	}

	private void Walk(IReadOnlyList<Vector2> path, System.Action done)
	{
		Tween tween = CreateTween();
		Vector2 from = Position;
		foreach (Vector2 point in path)
		{
			tween.TweenProperty(this, "position", point, Mathf.Max(0.05f, from.DistanceTo(point) / WalkSpeed));
			from = point;
		}
		tween.TweenCallback(Callable.From(done));
	}

	private void SetState(State state)
	{
		Current = state;
		_patienceMax = state == State.WantsToOrder ? OrderPatience : DrinkPatience;
		_patience = _patienceMax;
		UpdateBubble();
		UpdatePrompt();
	}

	private void UpdateBubble()
	{
		_patienceBar.Visible = Current is State.WantsToOrder or State.WaitingForDrink;
		switch (Current)
		{
		case State.WantsToOrder:
			_bubble.Text = "!";
			_bubble.AddThemeColorOverride("font_color", Holo.Strong);
			break;
		case State.WaitingForDrink:
			_bubble.Text = "…";
			_bubble.AddThemeColorOverride("font_color", Holo.Accent);
			break;
		case State.Arriving:
		case State.Leaving:
			_bubble.Text = "";
			break;
		}
	}

	private void UpdatePrompt()
	{
		_prompt.Visible = _playerInRange && (Current == State.WantsToOrder || Current == State.WaitingForDrink);
		_prompt.Text = Current == State.WantsToOrder ? "[E] Bestellung" : "[E] Servieren";
	}

	private List<string> Tags()
	{
		List<string> tags = new List<string> { "gast:" + DisplayName, "spezies:" + (Species?.Id ?? "?") };
		if (!string.IsNullOrEmpty(Faction))
		{
			tags.Add("fraktion:" + Faction);
		}
		return tags;
	}
}
