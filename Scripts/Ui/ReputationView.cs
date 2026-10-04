using Godot;

public partial class ReputationView : VBoxContainer
{
	private const int ChronicleLength = 8;
	private const int DebugStep = 10;

	public override void _EnterTree()
	{
		ReputationService.State.Changed += Rebuild;
	}

	public override void _ExitTree()
	{
		ReputationService.State.Changed -= Rebuild;
	}

	public override void _Ready()
	{
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		AddThemeConstantOverride("separation", 8);
		Rebuild();
	}

	private void Rebuild()
	{
		UiKit.ClearChildren(this);
		ReputationState state = ReputationService.State;

		AddChild(UiKit.Header("Gastrecht"));
		AddChild(BuildGastrechtRow(state));

		AddChild(UiKit.Header("Ansehen"));
		foreach (Faction faction in FactionInfo.All)
			AddChild(BuildFactionRow(state, faction));

		AddChild(UiKit.Header("Chronik"));
		AddChild(BuildChronicle(state));

		if (OS.IsDebugBuild())
			AddDebugTools(state);
	}

	private static HBoxContainer BuildGastrechtRow(ReputationState state)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);

		row.AddChild(NameLabel("Ruf als Gastgeber"));
		row.AddChild(Bar(0, ReputationState.StartingGastrecht, state.Gastrecht,
			state.Gastrecht >= ReputationState.NeutralHarborMinGastrecht ? UiKit.Good : UiKit.Bad));
		row.AddChild(new Label { Text = $"{state.Gastrecht}/{ReputationState.StartingGastrecht}" });
		return row;
	}

	private static HBoxContainer BuildFactionRow(ReputationState state, Faction faction)
	{
		int value = state.Get(faction);
		ReputationLevel level = ReputationState.LevelFor(value);
		Color levelColor = LevelColor(level);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);

		row.AddChild(NameLabel(FactionInfo.DisplayName(faction)));
		row.AddChild(Bar(ReputationState.MinValue, ReputationState.MaxValue, value, levelColor));

		var levelLabel = new Label
		{
			Text = ReputationText.LevelName(level),
			CustomMinimumSize = new Vector2(110, 0)
		};
		levelLabel.AddThemeColorOverride("font_color", levelColor);
		row.AddChild(levelLabel);

		return row;
	}

	private static Control BuildChronicle(ReputationState state)
	{
		var column = new VBoxContainer();

		if (state.History.Count == 0)
		{
			column.AddChild(UiKit.Hint("Noch nichts passiert. Niemand kennt deine Taverne."));
			return column;
		}

		int start = System.Math.Max(0, state.History.Count - ChronicleLength);
		for (int i = state.History.Count - 1; i >= start; i--)
		{
			ReputationEvent entry = state.History[i];
			string subject = entry.Subject.HasValue ? FactionInfo.DisplayName(entry.Subject.Value) : "Gastrecht";
			string change = entry.Delta == 0 ? "" : $" {entry.Delta:+0;-0}";
			column.AddChild(UiKit.Hint($"{subject}{change}: {entry.Reason}"));
		}

		return column;
	}

	private void AddDebugTools(ReputationState state)
	{
		AddChild(new HSeparator());
		AddChild(UiKit.Header("Entwicklerwerkzeuge"));
		AddChild(UiKit.Hint("Nur in Debug-Builds sichtbar. Hier lässt sich das System testen, bis echte Ereignisse es auslösen."));

		foreach (Faction faction in FactionInfo.All)
		{
			Faction current = faction;
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);

			row.AddChild(NameLabel(FactionInfo.DisplayName(current)));
			row.AddChild(SmallButton($"-{DebugStep}", () => state.Change(current, -DebugStep, "Entwicklerwerkzeug")));
			row.AddChild(SmallButton($"+{DebugStep}", () => state.Change(current, DebugStep, "Entwicklerwerkzeug")));

			bool alive = state.IsKeyFigureAlive(current);
			Button kill = SmallButton(alive ? $"{FactionInfo.KeyFigure(current)} töten" : $"{FactionInfo.KeyFigure(current)} ist tot",
				() => state.KillKeyFigure(current));
			kill.Disabled = !alive;
			row.AddChild(kill);

			AddChild(row);
		}

		var gastrechtRow = new HBoxContainer();
		gastrechtRow.AddThemeConstantOverride("separation", 8);
		gastrechtRow.AddChild(NameLabel("Gastrecht"));
		gastrechtRow.AddChild(SmallButton($"-{DebugStep}", () => state.ChangeGastrecht(-DebugStep, "Entwicklerwerkzeug")));
		gastrechtRow.AddChild(SmallButton($"+{DebugStep}", () => state.ChangeGastrecht(DebugStep, "Entwicklerwerkzeug")));
		AddChild(gastrechtRow);

		AddChild(UiKit.Header("Mögliche Enden"));
		foreach (EndingStatus ending in state.EvaluateEndings())
		{
			string marker = ending.Available ? "[x]" : "[ ]";
			string reason = ending.Available ? "" : $" – {ending.Reason}";
			var label = new Label { Text = $"{marker} {ReputationText.EndingName(ending.Ending)}{reason}" };
			label.AddThemeColorOverride("font_color", ending.Available ? UiKit.Good : UiKit.Muted);
			AddChild(label);
		}

		var resetAll = SmallButton("Alles zurücksetzen", state.Reset);
		resetAll.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		AddChild(resetAll);
	}

	private static Label NameLabel(string text) => new Label
	{
		Text = text,
		CustomMinimumSize = new Vector2(210, 0)
	};

	private static ProgressBar Bar(double min, double max, double value, Color fillColor)
	{
		var bar = new ProgressBar
		{
			MinValue = min,
			MaxValue = max,
			Value = value,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(280, 18),
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		bar.AddThemeStyleboxOverride("fill", UiKit.FillStyle(fillColor));
		return bar;
	}

	private static Button SmallButton(string text, System.Action onPressed)
	{
		var button = new Button { Text = text };
		button.Pressed += () => onPressed();
		return button;
	}

	private static Color LevelColor(ReputationLevel level) => level switch
	{
		ReputationLevel.Feind => new Color(0.90f, 0.30f, 0.30f),
		ReputationLevel.Misstrauisch => new Color(0.95f, 0.62f, 0.30f),
		ReputationLevel.Wohlwollend => new Color(0.60f, 0.85f, 0.55f),
		ReputationLevel.Verbuendet => new Color(0.40f, 0.90f, 0.70f),
		_ => new Color(0.70f, 0.72f, 0.75f)
	};
}
