using Godot;

/// <summary>Shows the date in the corner and the "Schichtende" screen when a week passes.</summary>
public partial class GameHud : CanvasLayer
{
	private Label _date;
	private Label _conjunction;
	private Label _wanted;
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
		MemoryService.WantedChanged += OnWantedChanged;
	}

	public override void _ExitTree()
	{
		GameSession.Clock.Changed -= Refresh;
		GameSession.ShiftEnded -= OnShiftEnded;
		MemoryService.WantedChanged -= OnWantedChanged;

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

		// Wanted level with the Sternenwacht – only shown when it is above 0 (placeholder look).
		_wanted = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		_wanted.AddThemeFontSizeOverride("font_size", 13);
		column.AddChild(_wanted);

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
		RefreshWanted();
	}

	private void OnWantedChanged(int old, int now, string reason) => RefreshWanted();

	/// <summary>"Fahndung ▮▮▮▯▯▯▯▯▯▯ 35 · unter Beobachtung"</summary>
	private void RefreshWanted()
	{
		int level = MemoryService.Wanted;
		_wanted.Visible = level > 0;
		if (level <= 0)
			return;
		int filled = Mathf.Clamp(Mathf.CeilToInt(level / 10f), 1, 10);
		_wanted.Text = "Fahndung " + new string('▮', filled) + new string('▯', 10 - filled) + $" {level} · {MemoryService.WantedStage(level)}";
		Color color = level >= MemoryService.Search ? UiKit.Bad : (level >= MemoryService.Patrols ? UiKit.Warn : new Color(0.55f, 0.7f, 1f));
		_wanted.AddThemeColorOverride("font_color", color);
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
		var ledger = GameSession.LastShiftLedger;
		if (ledger.Count > 0)
		{
			int income = 0, expenses = 0;
			foreach (LedgerEntry entry in ledger)
			{
				if (entry.Amount > 0)
					income += entry.Amount;
				else
					expenses -= entry.Amount;
			}
			text += $"\n\nKASSE: Einnahmen ₡{income}" + (expenses > 0 ? $", Ausgaben ₡{expenses}" : "") + $"  ·  Stand ₡{PlayerState.Credits}";
		}

		var news = GameSession.LastShiftRadio;
		if (GameSettings.RadioEnabled && news.Count > 0)
		{
			text += "\n\nGALAKTISCHES RADIO";
			foreach (RadioNews item in news)
				text += "\n• " + item.Text;
		}

		var rumors = MemoryService.LastShiftRumors;
		if (rumors.Count > 0)
		{
			text += "\n\nGERÜCHTE";
			foreach (string rumor in rumors)
				text += "\n• " + rumor;
		}
		int wantedChange = MemoryService.LastShiftWantedChange;
		if (MemoryService.Wanted > 0 || wantedChange != 0)
		{
			string change = wantedChange == 0 ? "" : (wantedChange > 0 ? $" (+{wantedChange})" : $" (−{-wantedChange})");
			text += $"\n\nFAHNDUNG: {MemoryService.Wanted}{change} · {MemoryService.WantedStage(MemoryService.Wanted)}";
		}

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
		GameSession.BeginShift();
	}
}
