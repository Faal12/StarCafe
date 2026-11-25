using System;
using Godot;

/// <summary>
/// Mini-game "Druckventil": the pressure keeps rising in jolts. Hold Space or the left mouse button
/// to open the valve. Keep the needle inside the (slowly moving) green zone for 7 seconds in total.
/// Warning signs announce pressure surges. Over 100 = steam burst: damage and lost progress.
/// 50 seconds.
/// </summary>
public partial class PressureGame : CanvasLayer
{
	private const double TimeLimit = 50.0;

	private const double Needed = 7.0;

	private const float ZoneSize = 20f;

	private double _time = TimeLimit;

	private double _progress;

	private float _pressure = 40f;

	private float _zone = 45f;

	private double _clock;

	private double _surgeIn = 4.0;

	private double _surgeWarning;

	private bool _venting;

	private bool _done;

	private Action<bool> _callback;

	private PressureGauge _gauge;

	private Label _timer;

	private Label _status;

	private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();

	public static bool IsOpen { get; private set; }

	internal float Pressure => _pressure;

	internal float Zone => _zone;

	internal float ZoneSizeValue => ZoneSize;

	internal float Progress => (float)(_progress / Needed);

	internal bool Venting => _venting;

	internal bool SurgeWarning => _surgeWarning > 0;

	public static void Open(Action<bool> done)
	{
		if (Engine.GetMainLoop() is not SceneTree tree || IsOpen)
		{
			return;
		}
		tree.Root.AddChild(new PressureGame { _callback = done });
	}

	public override void _Ready()
	{
		IsOpen = true;
		Layer = 32;
		ProcessMode = ProcessModeEnum.Always;
		GetTree().Paused = true;
		_rng.Randomize();
		Build();
		// Automated tests only (debug builds): STARCAFE_TEST_AUTOWIN=1 wins after 1.5 s.
		if (OS.IsDebugBuild() && OS.GetEnvironment("STARCAFE_TEST_AUTOWIN") == "1")
		{
			GetTree().CreateTimer(1.5, processAlways: true).Timeout += () => Finish(true, null);
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			GetViewport().SetInputAsHandled();
			Finish(false, null);
		}
	}

	public override void _Process(double delta)
	{
		if (_done)
		{
			return;
		}
		float dt = (float)delta;
		_clock += delta;
		_time -= delta;
		_venting = Input.IsKeyPressed(Key.Space) || Input.IsMouseButtonPressed(MouseButton.Left);
		// The green zone wanders slowly.
		_zone = 42f + 14f * Mathf.Sin((float)_clock * 0.45f);
		// Rising pressure with wobble; the valve lets it out.
		float rise = 15f + 9f * Mathf.Sin((float)_clock * 1.7f) + _rng.RandfRange(-6f, 6f);
		_pressure += (rise - (_venting ? 42f : 0f)) * dt;
		// Surges: a warning, then a jolt.
		_surgeIn -= delta;
		if (_surgeWarning > 0)
		{
			_surgeWarning -= delta;
			if (_surgeWarning <= 0)
			{
				_pressure += 18f;
				_status.Text = "Druckstoß!";
			}
		}
		else if (_surgeIn <= 0)
		{
			_surgeWarning = 0.9;
			_surgeIn = _rng.RandfRange(4f, 7f);
			_status.Text = "⚠ Druckstoß kommt …";
			_status.AddThemeColorOverride("font_color", Holo.Strong);
		}
		_pressure = Mathf.Clamp(_pressure, 0f, 110f);
		bool inZone = _pressure >= _zone && _pressure <= _zone + ZoneSize;
		_progress = inZone ? _progress + delta : Math.Max(0, _progress - delta * 0.25);
		if (_pressure >= 100f)
		{
			PlayerCombat.Instance?.TakeDamage(8f, Vector2.Zero, "Dampfstoß");
			_pressure = 55f;
			_progress = Math.Max(0, _progress - Needed * 0.25);
			_status.Text = "Dampfstoß! Ventil früher öffnen.";
			_status.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.4f));
		}
		_timer.Text = $"{Mathf.Max(0, (int)_time)} s";
		_timer.AddThemeColorOverride("font_color", _time > 12 ? Holo.Accent : new Color(1f, 0.45f, 0.4f));
		_gauge.QueueRedraw();
		if (_progress >= Needed)
		{
			_done = true;
			_status.Text = "Druck stabil. Generator läuft an!";
			_status.AddThemeColorOverride("font_color", Holo.Accent);
			GetTree().CreateTimer(0.9, processAlways: true).Timeout += () => Finish(true, null);
		}
		else if (_time <= 0)
		{
			PlayerCombat.Instance?.TakeDamage(10f, Vector2.Zero, "Dampfstoß");
			Finish(false, "Zu lange – das Ventil ist überhitzt. Gleich nochmal versuchen.");
		}
	}

	private void Finish(bool success, string message)
	{
		if (!IsOpen)
		{
			return;
		}
		IsOpen = false;
		_done = true;
		GetTree().Paused = false;
		if (message != null)
		{
			InventoryHud.Toast(message, new Color(1f, 0.55f, 0.45f));
		}
		Action<bool> callback = _callback;
		QueueFree();
		callback?.Invoke(success);
	}

	private void Build()
	{
		Control root = new Control { Theme = UiTheme.Get() };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);
		ColorRect dim = new ColorRect { Color = new Color(0f, 0.02f, 0.04f, 0.72f) };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(dim);
		CenterContainer center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(center);
		PanelContainer frame = new PanelContainer();
		frame.AddThemeStyleboxOverride("panel", Holo.Panel(16));
		center.AddChild(frame);
		VBoxContainer box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		frame.AddChild(box);
		HBoxContainer header = new HBoxContainer();
		box.AddChild(header);
		Label title = Holo.Caption("Generator  ·  Druckventil", 13);
		title.AddThemeColorOverride("font_color", Holo.Accent);
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(title);
		_timer = Holo.Caption("", 13);
		header.AddChild(_timer);
		box.AddChild(Holo.Text_("Halte Leertaste oder die linke Maustaste, um Druck abzulassen. Bleib im grünen Bereich, bis der Ring voll ist.", 13, Holo.TextDim));
		_gauge = new PressureGauge { Game = this, CustomMinimumSize = new Vector2(420f, 330f) };
		box.AddChild(_gauge);
		_status = Holo.Text_("", 14, Holo.Accent);
		box.AddChild(_status);
		Button cancel = Holo.Button("Abbrechen (Esc)");
		cancel.Pressed += () => Finish(false, null);
		box.AddChild(cancel);
	}
}

/// <summary>Draws the pressure tube, green zone, needle and progress ring.</summary>
public partial class PressureGauge : Control
{
	public PressureGame Game;

	public override void _Draw()
	{
		Rect2 tube = new Rect2(70f, 15f, 70f, 300f);
		DrawRect(tube, new Color(0.04f, 0.08f, 0.11f, 0.95f));
		float Y(float value) => tube.End.Y - tube.Size.Y * Mathf.Clamp(value, 0f, 100f) / 100f;
		// Danger zone.
		DrawRect(new Rect2(tube.Position.X, Y(100f), tube.Size.X, Y(85f) - Y(100f)), new Color(1f, 0.3f, 0.25f, 0.25f));
		// Green zone.
		float zoneTop = Y(Game.Zone + Game.ZoneSizeValue);
		float zoneBottom = Y(Game.Zone);
		DrawRect(new Rect2(tube.Position.X, zoneTop, tube.Size.X, zoneBottom - zoneTop), new Color(0.35f, 1f, 0.55f, 0.28f));
		DrawRect(new Rect2(tube.Position.X, zoneTop, tube.Size.X, zoneBottom - zoneTop), new Color(0.35f, 1f, 0.55f, 0.8f), false, 2f);
		// Pressure fill.
		float level = Y(Game.Pressure);
		bool danger = Game.Pressure > 85f;
		Color fill = danger ? new Color(1f, 0.45f, 0.35f, 0.85f) : new Color(0.35f, 0.85f, 1f, 0.7f);
		DrawRect(new Rect2(tube.Position.X + 8f, level, tube.Size.X - 16f, tube.End.Y - level), fill);
		DrawLine(new Vector2(tube.Position.X - 12f, level), new Vector2(tube.End.X + 12f, level), Colors.White, 3f);
		DrawRect(tube, new Color(0.35f, 0.85f, 0.95f, 0.6f), false, 2f);
		for (int i = 0; i <= 10; i++)
		{
			float y = Y(i * 10f);
			DrawLine(new Vector2(tube.Position.X - 8f, y), new Vector2(tube.Position.X, y), new Color(0.6f, 0.75f, 0.8f, 0.6f), 1f);
		}
		DrawString(ThemeDB.FallbackFont, new Vector2(tube.Position.X - 2f, tube.End.Y + 16f), $"{Mathf.RoundToInt(Game.Pressure)} bar", HorizontalAlignment.Left, -1, 12, Colors.White);
		// Progress ring.
		Vector2 c = new Vector2(300f, 150f);
		DrawArc(c, 80f, 0f, Mathf.Tau, 64, new Color(1f, 1f, 1f, 0.08f), 14f);
		DrawArc(c, 80f, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * Mathf.Clamp(Game.Progress, 0f, 1f), 64, new Color(0.35f, 1f, 0.55f), 14f);
		DrawString(ThemeDB.FallbackFont, c + new Vector2(-34f, 8f), $"{Mathf.RoundToInt(Game.Progress * 100f)} %", HorizontalAlignment.Left, -1, 22, Colors.White);
		// Valve.
		Color valve = Game.Venting ? new Color(0.4f, 0.95f, 1f) : new Color(0.4f, 0.5f, 0.55f);
		DrawCircle(new Vector2(300f, 290f), 22f, new Color(valve, 0.3f));
		DrawArc(new Vector2(300f, 290f), 22f, 0f, Mathf.Tau, 32, valve, 3f);
		DrawString(ThemeDB.FallbackFont, new Vector2(262f, 328f), Game.Venting ? "VENTIL AUF" : "VENTIL ZU", HorizontalAlignment.Left, -1, 12, valve);
		if (Game.SurgeWarning)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(210f, 40f), "⚠ DRUCKSTOSS", HorizontalAlignment.Left, -1, 18, new Color(1f, 0.75f, 0.3f));
		}
	}
}
