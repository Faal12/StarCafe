using Godot;

/// <summary>
/// Health and energy bars (bottom right, Stardew-like but holo), red damage vignette, and the
/// task tracker (top left, under the date): title, steps with checkmarks, timer.
/// </summary>
public partial class CombatHud : CanvasLayer
{
	private static CombatHud _instance;

	private Control _bars;

	private Panel _healthFill;

	private Panel _energyFill;

	private Label _healthText;

	private ColorRect _vignette;

	private PanelContainer _taskBox;

	private Label _taskTitle;

	private VBoxContainer _taskSteps;

	private Label _taskTimer;

	private const float BarHeight = 130f;

	private PanelContainer _brawlBanner;

	private Label _brawlLabel;

	public static void FlashDamage()
	{
		if (_instance == null)
		{
			return;
		}
		_instance._vignette.Color = new Color(0.9f, 0.1f, 0.05f, 0.35f);
		_instance._vignette.CreateTween().TweenProperty(_instance._vignette, "color:a", 0f, 0.45);
	}

	public override void _Ready()
	{
		_instance = this;
		Layer = 14;
		ProcessMode = ProcessModeEnum.Always;
		_vignette = new ColorRect { Color = new Color(0.9f, 0.1f, 0.05f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		_vignette.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_vignette);
		BuildBars();
		BuildTask();
		BuildBrawlBanner();
		UiBus.BrawlChanged += OnBrawlChanged;
	}

	/// <summary>"SCHLÄGEREI" banner at the top while a tavern brawl runs (placeholder look).</summary>
	private void BuildBrawlBanner()
	{
		_brawlBanner = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
		_brawlBanner.AddThemeStyleboxOverride("panel", Holo.Panel());
		_brawlBanner.Position = new Vector2(576f - 190f, 14f);
		_brawlBanner.CustomMinimumSize = new Vector2(380f, 0f);
		AddChild(_brawlBanner);
		_brawlLabel = Holo.Text_("", 15, new Color(1f, 0.6f, 0.45f));
		_brawlLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_brawlBanner.AddChild(_brawlLabel);
	}

	private void OnBrawlChanged(bool running, string opponent)
	{
		_brawlBanner.Visible = running;
		_brawlLabel.Text = "SCHLÄGEREI  ·  " + (opponent ?? "").ToUpperInvariant();
	}

	public override void _ExitTree()
	{
		UiBus.BrawlChanged -= OnBrawlChanged;
		if (_instance == this)
		{
			_instance = null;
		}
	}

	public override void _Process(double delta)
	{
		if (!Engine.IsEditorHint())
		{
			TaskService.Update(GetTree().Paused ? 0 : delta);
		}
		PlayerCombat combat = PlayerCombat.Instance;
		bool covered = DialogueController.IsOpen || EventPanel.IsOpen || BarCounterView.IsOpen || OrderPanel.IsOpen;
		if (combat != null)
		{
			bool show = !covered && (DeckManager.Current == DeckManager.Technikdeck || BrawlService.IsActive || combat.Health < PlayerCombat.MaxHealth || combat.Energy < PlayerCombat.MaxEnergy - 1f || TaskService.IsActive);
			_bars.Visible = show;
			SetFill(_healthFill, combat.Health / PlayerCombat.MaxHealth);
			SetFill(_energyFill, combat.Energy / PlayerCombat.MaxEnergy);
			_healthText.Text = Mathf.CeilToInt(combat.Health).ToString();
		}
		GameTask task = TaskService.Active;
		_taskBox.Visible = task != null && !covered;
		if (task != null)
		{
			_taskTitle.Text = task.Title.ToUpperInvariant();
			UiKit.ClearChildren(_taskSteps);
			foreach (TaskStep step in task.Steps)
			{
				Label line = Holo.Text_((step.Done ? "✓ " : "○ ") + step.Text, 13, step.Done ? Holo.TextDim : Holo.Text);
				_taskSteps.AddChild(line);
			}
			if (task.TimeLimit > 0)
			{
				double left = task.TimeLimit - task.Elapsed;
				_taskTimer.Text = left > 0 ? $"Zeit: {(int)left / 60}:{(int)left % 60:00}" : "Zu spät – die Gäste werden unruhig!";
				_taskTimer.AddThemeColorOverride("font_color", left > 30 ? Holo.Accent : (left > 0 ? Holo.Strong : new Color(1f, 0.45f, 0.4f)));
			}
			else
			{
				_taskTimer.Text = "";
			}
		}
	}

	private static void SetFill(Panel fill, float ratio)
	{
		ratio = Mathf.Clamp(ratio, 0f, 1f);
		fill.Size = new Vector2(10f, (BarHeight - 4f) * ratio);
		fill.Position = new Vector2(2f, 2f + (BarHeight - 4f) * (1f - ratio));
	}

	private void BuildBars()
	{
		_bars = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		_bars.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		_bars.Position = new Vector2(-80f, -BarHeight - 40f);
		AddChild(_bars);
		_healthFill = Bar(new Vector2(0f, 0f), new Color(1f, 0.35f, 0.35f), "HP");
		_energyFill = Bar(new Vector2(30f, 0f), new Color(0.75f, 1f, 0.35f), "EN");
		_healthText = Holo.Caption("100", 10);
		_healthText.Position = new Vector2(-6f, -18f);
		_healthText.AddThemeColorOverride("font_color", Holo.Text);
		_bars.AddChild(_healthText);
	}

	private Panel Bar(Vector2 position, Color color, string caption)
	{
		Panel frame = new Panel { Position = position, Size = new Vector2(14f, BarHeight), MouseFilter = Control.MouseFilterEnum.Ignore };
		StyleBoxFlat style = new StyleBoxFlat { BgColor = Holo.Glass, BorderColor = Holo.Line, ShadowColor = new Color(0.3f, 0.9f, 1f, 0.15f), ShadowSize = 6 };
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		frame.AddThemeStyleboxOverride("panel", style);
		_bars.AddChild(frame);
		Panel fill = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
		StyleBoxFlat fillStyle = new StyleBoxFlat { BgColor = color, ShadowColor = new Color(color, 0.5f), ShadowSize = 5 };
		fillStyle.SetCornerRadiusAll(4);
		fill.AddThemeStyleboxOverride("panel", fillStyle);
		frame.AddChild(fill);
		Label label = Holo.Caption(caption, 9);
		label.Position = position + new Vector2(-1f, BarHeight + 2f);
		_bars.AddChild(label);
		return fill;
	}

	private void BuildTask()
	{
		_taskBox = new PanelContainer { Position = new Vector2(12f, 82f), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
		StyleBoxFlat style = Holo.Panel(6);
		style.SetContentMarginAll(10);
		_taskBox.AddThemeStyleboxOverride("panel", style);
		AddChild(_taskBox);
		VBoxContainer box = new VBoxContainer { CustomMinimumSize = new Vector2(250f, 0f) };
		box.AddThemeConstantOverride("separation", 4);
		_taskBox.AddChild(box);
		_taskTitle = Holo.Caption("Aufgabe", 11);
		_taskTitle.AddThemeColorOverride("font_color", Holo.Strong);
		box.AddChild(_taskTitle);
		_taskSteps = new VBoxContainer();
		_taskSteps.AddThemeConstantOverride("separation", 2);
		box.AddChild(_taskSteps);
		_taskTimer = Holo.Caption("", 11);
		box.AddChild(_taskTimer);
	}
}
