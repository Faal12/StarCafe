using Godot;

public partial class LauncherScreen : Control
{
	public const string ScenePath = "res://Launcher.tscn";
	public const string GameScenePath = "res://Tavern.tscn";

	public override void _Ready()
	{
		GameSettings.Apply();
		SetAnchorsPreset(LayoutPreset.FullRect);
		BuildUi();
	}

	private void BuildUi()
	{
		var background = new ColorRect { Color = new Color(0.04f, 0.045f, 0.06f) };
		background.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(background);

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		foreach (string side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride($"margin_{side}", 32);
		AddChild(margin);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		margin.AddChild(column);

		var title = new Label { Text = "DIE LETZTE RUNDE", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 34);
		title.AddThemeColorOverride("font_color", UiKit.Amber);
		column.AddChild(title);

		var subtitle = new Label { Text = "Launcher – KI & Sprachausgabe", HorizontalAlignment = HorizontalAlignment.Center };
		subtitle.AddThemeColorOverride("font_color", UiKit.Muted);
		column.AddChild(subtitle);

		column.AddChild(UiKit.Scroll(new LauncherPanel()));

		var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		buttons.AddThemeConstantOverride("separation", 16);
		column.AddChild(buttons);

		var start = new Button { Text = "Spiel starten", CustomMinimumSize = new Vector2(200, 42) };
		start.Pressed += OnStartPressed;
		buttons.AddChild(start);

		var quit = new Button { Text = "Beenden", CustomMinimumSize = new Vector2(200, 42) };
		quit.Pressed += () => GetTree().Quit();
		buttons.AddChild(quit);

		start.GrabFocus();
	}

	private void OnStartPressed()
	{
		// There is no save system yet, so every start is a new game.
		ReputationService.State.Reset();
		GetTree().ChangeSceneToFile(GameScenePath);
	}
}
