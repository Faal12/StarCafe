using System.Linq;
using Godot;

/// <summary>
/// "Tablett" mini-game: while drinks are on the tray (in the inventory), every sudden start,
/// stop or turn adds sloshing; walking evenly lets it calm down, Shift (walk_careful) is gentler.
/// Zig-zagging builds it up fast. More drinks = touchier tray. Too much sloshing spills a drink:
/// it is lost, a puddle stays on the floor for a week and the deed is remembered.
/// </summary>
public partial class TrayBalance : Node
{
	/// <summary>Sloshing per px/s of sudden speed change (any direction).</summary>
	[Export]
	public float Impulse { get; set; } = 0.0011f;

	/// <summary>How fast the sloshing calms down when walking evenly (per second).</summary>
	[Export]
	public float Settle { get; set; } = 0.6f;

	/// <summary>Swing frequency of the marker (Hz) – purely visual.</summary>
	[Export]
	public float SwingHz { get; set; } = 1.6f;

	private double _clock;

	/// <summary>Extra wobble per additional drink.</summary>
	[Export]
	public float PerDrink { get; set; } = 0.22f;

	private CharacterBody2D _player;

	private Vector2 _lastVelocity;

	private float _tilt;

	private int _spills;

	private Control _hud;

	private Control _marker;

	private Label _caption;

	private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();

	public override void _Ready()
	{
		_rng.Randomize();
		_player = GetTree().GetFirstNodeInGroup("Player") as CharacterBody2D;
		BuildHud();
	}

	public override void _PhysicsProcess(double delta)
	{
		int drinks = PlayerState.Inventory.All().Count(s => s.Stack.ItemId == "getraenk");
		_hud.Visible = drinks > 0 && !UiBus.IsOpen(UiWindow.Dialogue) && !UiBus.IsOpen(UiWindow.Event);
		if (_player == null || drinks == 0)
		{
			_tilt = 0f;
			_lastVelocity = _player?.Velocity ?? Vector2.Zero;
			return;
		}
		Vector2 velocity = _player.Velocity;
		Vector2 change = velocity - _lastVelocity;
		_lastVelocity = velocity;
		float factor = (1f + PerDrink * (drinks - 1)) * (Input.IsActionPressed("walk_careful") ? 0.5f : 1f);
		if (change.LengthSquared() > 1f)
		{
			_tilt += change.Length() * Impulse * factor;
		}
		_tilt = Mathf.MoveToward(_tilt, 0f, Settle * (float)delta);
		if (_tilt >= 1f)
		{
			Spill();
		}
		_clock += delta;
		float swing = Mathf.Clamp(_tilt, 0f, 1f) * Mathf.Sin((float)(_clock * Mathf.Tau * SwingHz));
		float width = 150f;
		_marker.Position = new Vector2(width / 2f + swing * (width / 2f) - 5f, -1f);
		Color color = Mathf.Abs(_tilt) < 0.5f ? Holo.Accent : (Mathf.Abs(_tilt) < 0.8f ? Holo.Strong : new Color(1f, 0.4f, 0.35f));
		_marker.Modulate = color;
		_caption.Text = drinks == 1 ? "TABLETT · 1 GETRÄNK" : $"TABLETT · {drinks} GETRÄNKE";
	}

	private void Spill()
	{
		_tilt = 0f;
		var drink = PlayerState.Inventory.All().LastOrDefault(s => s.Stack.ItemId == "getraenk");
		if (drink.Stack == null)
		{
			return;
		}
		PlayerState.Inventory.TakeSlot(drink.Slot);
		_spills++;
		Vector2 at = _player.GlobalPosition + new Vector2(_rng.RandfRange(-12f, 12f), 6f);
		WorldService.State.Apply(new WorldEffectDef
		{
			Type = "prop",
			Id = $"pfuetze_{GameSession.Clock.Week}_{_spills}_{_rng.Randi() % 10000}",
			Label = "Pfütze",
			X = at.X,
			Y = at.Y,
			Color = "#7a5a3a",
			Scale = 0.55f,
			Weeks = 1,
			Appear = "pop"
		}, GameSession.Clock.Week);
		PlayerState.Record("verschuettet", $"{drink.Stack.DisplayName} verschüttet.", new[] { "rezept:" + (drink.Stack.Get("recipe") ?? "") }, witnessed: true);
		UiBus.Toast($"Verschüttet: {drink.Stack.DisplayName}!", new Color(1f, 0.55f, 0.45f));
		VoiceQueue.Enqueue(FlokiAdvisor.Name, SpillLines[_rng.RandiRange(0, SpillLines.Length - 1)]);
	}

	private static readonly string[] SpillLines =
	{
		"Und das, Chef, nennt man Schwerkraft.",
		"Ich trage das als Bodenpflege ein.",
		"Langsamer, Chef. Mit Umschalt. Ich sag's nur einmal. Heute.",
		"Das war ein guter Drink. War."
	};

	private void BuildHud()
	{
		CanvasLayer layer = new CanvasLayer { Layer = 15 };
		AddChild(layer);
		_hud = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
		_hud.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		_hud.Position = new Vector2(-75f, -118f);
		layer.AddChild(_hud);
		_caption = Holo.Caption("Tablett", 10);
		_caption.Position = new Vector2(0f, -18f);
		_caption.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
		_caption.AddThemeConstantOverride("outline_size", 4);
		_hud.AddChild(_caption);
		Panel track = new Panel { Size = new Vector2(150f, 8f), MouseFilter = Control.MouseFilterEnum.Ignore };
		StyleBoxFlat style = new StyleBoxFlat { BgColor = new Color(0.03f, 0.06f, 0.09f, 0.8f), BorderColor = Holo.Line };
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(4);
		track.AddThemeStyleboxOverride("panel", style);
		_hud.AddChild(track);
		Panel center = new Panel { Position = new Vector2(74f, 0f), Size = new Vector2(2f, 8f), MouseFilter = Control.MouseFilterEnum.Ignore };
		StyleBoxFlat centerStyle = new StyleBoxFlat { BgColor = new Color(1f, 1f, 1f, 0.25f) };
		center.AddThemeStyleboxOverride("panel", centerStyle);
		_hud.AddChild(center);
		Panel marker = new Panel { Size = new Vector2(10f, 10f), MouseFilter = Control.MouseFilterEnum.Ignore };
		StyleBoxFlat markerStyle = new StyleBoxFlat { BgColor = Colors.White, ShadowColor = new Color(0.4f, 0.95f, 1f, 0.6f), ShadowSize = 5 };
		markerStyle.SetCornerRadiusAll(5);
		marker.AddThemeStyleboxOverride("panel", markerStyle);
		_hud.AddChild(marker);
		_marker = marker;
	}
}
