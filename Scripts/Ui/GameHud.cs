using Godot;

/// <summary>Shows the date in the corner and the "Schichtende" screen when a week passes.</summary>
public partial class GameHud : CanvasLayer
{
	private Label _date;
	private Label _conjunction;
	private Control _shiftEndScreen;
	private Label _shiftEndTitle;
	private Label _shiftEndText;
	private Button _shiftEndContinue;

	public bool IsShowingShiftEnd => _shiftEndScreen != null && _shiftEndScreen.Visible;

	public override void _Ready()
	{
		Layer = 20;
		ProcessMode = ProcessModeEnum.Always;
		BuildUi();
		Refresh();
	}

	public override void _EnterTree()
	{
		GameSession.Clock.Changed += Refresh;
		GameSession.ShiftEnded += OnShiftEnded;
	}

	public override void _ExitTree()
	{
		GameSession.Clock.Changed -= Refresh;
		GameSession.ShiftEnded -= OnShiftEnded;

		if (IsShowingShiftEnd)
			GetTree().Paused = false;
	}

	private void BuildUi()
	{
		var panel = new PanelContainer { Position = new Vector2(12, 10), MouseFilter = Control.MouseFilterEnum.Ignore };
		var hudStyle = UiTheme.Box(new Color(0.04f, 0.045f, 0.06f, 0.72f), new Color(0.58f, 0.47f, 0.27f, 0.7f), 1, 8, 12, 7);
		panel.AddThemeStyleboxOverride("panel", hudStyle);
		AddChild(panel);

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		panel.AddChild(column);

		_date = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
		_date.AddThemeColorOverride("font_color", UiKit.Amber);
		column.AddChild(_date);

		_conjunction = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
		_conjunction.AddThemeFontSizeOverride("font_size", 13);
		column.AddChild(_conjunction);

		_shiftEndScreen = new Control { Visible = false, Theme = UiTheme.Get() };
		_shiftEndScreen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_shiftEndScreen);

		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.82f), MouseFilter = Control.MouseFilterEnum.Stop };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_shiftEndScreen.AddChild(dim);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_shiftEndScreen.AddChild(center);

		var box = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
		box.AddThemeConstantOverride("separation", 14);
		center.AddChild(box);

		_shiftEndTitle = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_shiftEndTitle.AddThemeFontSizeOverride("font_size", 30);
		_shiftEndTitle.AddThemeColorOverride("font_color", UiKit.Amber);
		box.AddChild(_shiftEndTitle);

		_shiftEndText = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		box.AddChild(_shiftEndText);

		_shiftEndContinue = new Button { Text = "Nächste Schicht", ThemeTypeVariation = UiTheme.PrimaryButton, CustomMinimumSize = new Vector2(220, 44), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
		_shiftEndContinue.Pressed += CloseShiftEnd;
		box.AddChild(_shiftEndContinue);
	}

	private void Refresh()
	{
		if (_date == null)
			return;

		GameClock clock = GameSession.Clock;
		_date.Text = GameSession.DescribeDate();

		if (clock.ConjunctionShift)
		{
			_conjunction.Text = "Konjunktion: Hochbetrieb!";
			_conjunction.AddThemeColorOverride("font_color", UiKit.Warn);
		}
		else
		{
			_conjunction.Text = clock.DaysUntilConjunction == 0
				? "Konjunktion heute"
				: $"Konjunktion in {clock.DaysUntilConjunction} Tagen";
			_conjunction.AddThemeColorOverride("font_color", UiKit.Muted);
		}
	}

	private void OnShiftEnded(ShiftSummary summary)
	{
		_shiftEndTitle.Text = "Schichtende";

		string text = $"Eine Woche vergeht. Es ist jetzt Woche {summary.Week}, Tag {summary.Time.NewDay}.";
		text += summary.Time.ConjunctionStarts
			? "\n\nDie Welten rücken zusammen: Die Konjunktion beginnt, und alle Gilden ziehen durch."
			: $"\n\nBis zur nächsten Konjunktion: {summary.DaysUntilConjunction} Tage.";
		if (summary.StandingChanged)
			text += "\n\nMit der Zeit verblasst manches Ansehen, und alte Wunden heilen.";
		text += summary.Saved ? "\n\nSpielstand gespeichert." : "\n\nAchtung: Der Spielstand konnte nicht gespeichert werden.";
		_shiftEndText.Text = text;

		_shiftEndScreen.Visible = true;
		GetTree().Paused = true;
		_shiftEndContinue.GrabFocus();
	}

	private void CloseShiftEnd()
	{
		_shiftEndScreen.Visible = false;
		GetTree().Paused = false;
	}
}
