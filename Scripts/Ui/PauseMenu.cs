using Godot;

public partial class PauseMenu : CanvasLayer
{
	private Control _root;
	private Button _resumeButton;
	private Label _dateLabel;

	public override void _Ready()
	{
		Layer = 100;
		ProcessMode = ProcessModeEnum.Always;
		GameSettings.Apply();

		BuildUi();
		_root.Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel"))
			return;

		if (_root.Visible)
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
		else if (!DialogueController.IsOpen)
		{
			Open();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _ExitTree()
	{
		// Never leave the tree paused when this node goes away.
		if (_root != null && _root.Visible)
			GetTree().Paused = false;
	}

	private void Open()
	{
		_dateLabel.Text = GameSession.DescribeDate();
		_root.Visible = true;
		GetTree().Paused = true;
		_resumeButton.GrabFocus();
	}

	private void Close()
	{
		_root.Visible = false;
		GetTree().Paused = false;
	}

	private void BuildUi()
	{
		_root = new Control { Theme = UiTheme.Get() };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f), MouseFilter = Control.MouseFilterEnum.Stop };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(dim);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(center);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(960, 580) };
		panel.AddThemeStyleboxOverride("panel", UiKit.PanelStyle());
		center.AddChild(panel);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		panel.AddChild(column);

		var title = new Label { Text = "Pause", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 30);
		title.AddThemeColorOverride("font_color", UiKit.Amber);
		column.AddChild(title);

		_dateLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_dateLabel.AddThemeColorOverride("font_color", UiTheme.TextDim);
		column.AddChild(_dateLabel);

		var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		column.AddChild(tabs);

		tabs.AddChild(UiKit.Scroll(new ReputationView()));
		tabs.AddChild(UiKit.Scroll(new SettingsView()));
		tabs.AddChild(UiKit.Scroll(new LauncherPanel()));
		tabs.SetTabTitle(0, "Ansehen");
		tabs.SetTabTitle(1, "Einstellungen");
		tabs.SetTabTitle(2, "KI & Stimme");

		var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		buttons.AddThemeConstantOverride("separation", 16);
		column.AddChild(buttons);

		_resumeButton = new Button { Text = "Weiter", ThemeTypeVariation = UiTheme.PrimaryButton, CustomMinimumSize = new Vector2(160, 38) };
		_resumeButton.Pressed += Close;
		buttons.AddChild(_resumeButton);

		var endShift = new Button { Text = "Schicht beenden", CustomMinimumSize = new Vector2(160, 38) };
		endShift.Pressed += OnEndShiftPressed;
		buttons.AddChild(endShift);

		var toLauncher = new Button { Text = "Zum Launcher", CustomMinimumSize = new Vector2(160, 38) };
		toLauncher.Pressed += OnToLauncherPressed;
		buttons.AddChild(toLauncher);

		var quit = new Button { Text = "Spiel beenden", CustomMinimumSize = new Vector2(160, 38) };
		quit.Pressed += () => GetTree().Quit();
		buttons.AddChild(quit);
	}

	private void OnEndShiftPressed()
	{
		Close();
		GameSession.EndShift();
	}

	private void OnToLauncherPressed()
	{
		TtsService.Stop();
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile(LauncherScreen.ScenePath);
	}
}
