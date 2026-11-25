using Godot;

public partial class LauncherScreen : Control
{
	public const string ScenePath = "res://Launcher.tscn";
	public const string GameScenePath = "res://Tavern.tscn";

	private const string MascotPath = "res://Assets/UI/kruemel.png";
	private const string VersionText = "Arbeitsstand v0.12";

	private Button _newGameButton;
	private Button _continueButton;
	private Control _settingsOverlay;
	private Button _settingsButton;
	// The mascot only floats up and down a little. No scaling: that resamples the picture every frame, which looked jerky.
	private const double FloatSeconds = 6.0;
	private const float FloatPixels = 2.5f;
	private const float MascotHeadroom = 6f;
	private const float MascotHeight = 250f;

	private TextureRect _mascot;
	private double _floatClock;

	// Launcher art: a soft pastel haze over the stars and a spaceship that flies in and bobs.
	private const string BackgroundPath = "res://Assets/Art/pastel_background.png";
	private const string ShipPath = "res://Assets/Art/spaceship.png";
	private const double ShipEntranceSeconds = 1.6;
	private const float ShipBobPixels = 6f;
	private const double ShipBobSeconds = 4.0;
	private TextureRect _ship;

	public override void _Ready()
	{
		GameSettings.Apply();
		GetWindow().Title = "StarCafe";
		Theme = UiTheme.Get();

		// Menus move in _process, not in physics ticks; physics interpolation would only make them stutter.
		PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
		SetAnchorsPreset(LayoutPreset.FullRect);

		BuildBackground();
		BuildMainMenu();
		BuildSettingsOverlay();
		PlayEntrance();
		PlayShipEntrance();

		(_continueButton.Disabled ? _newGameButton : _continueButton).GrabFocus();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_settingsOverlay.Visible && @event.IsActionPressed("ui_cancel"))
		{
			CloseSettings();
			GetViewport().SetInputAsHandled();
		}
	}

	// ---------- background

	private void BuildBackground()
	{
		AddChild(new SpaceBackground());

		// Pastel art: a mostly transparent haze, laid over the stars as a soft nebula.
		var pastel = new TextureRect
		{
			Texture = GD.Load<Texture2D>(BackgroundPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = MouseFilterEnum.Ignore
		};
		pastel.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(pastel);

		// Lamplight of the tavern against the cold space: a warm glow behind the mascot.
		var glow = new TextureRect
		{
			Texture = RadialGradient(new Color(0.95f, 0.62f, 0.25f, 0.34f), new Color(0.95f, 0.62f, 0.25f, 0f), 512),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore
		};
		glow.SetAnchorsPreset(LayoutPreset.FullRect);
		glow.AnchorLeft = -0.06f;
		glow.AnchorRight = 0.58f;
		glow.AnchorTop = 0.04f;
		glow.AnchorBottom = 1.02f;
		AddChild(glow);

		var vignette = new TextureRect
		{
			Texture = RadialGradient(new Color(0, 0, 0, 0f), new Color(0, 0, 0, 0.70f), 512, 0.5f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore
		};
		vignette.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(vignette);

		// Spaceship floating across the top right, behind the menu.
		_ship = new TextureRect
		{
			Texture = GD.Load<Texture2D>(ShipPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = TextureFilterEnum.LinearWithMipmapsAnisotropic,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_ship.SetAnchorsPreset(LayoutPreset.FullRect);
		_ship.AnchorLeft = 0.44f;
		_ship.AnchorRight = 1.0f;
		_ship.AnchorTop = -0.02f;
		_ship.AnchorBottom = 0.40f;
		AddChild(_ship);
	}

	private void PlayShipEntrance()
	{
		// Fly in from the right edge.
		float slide = GetViewportRect().Size.X * 0.6f;
		_ship.OffsetLeft = slide;
		_ship.OffsetRight = slide;
		Tween tween = CreateTween().SetParallel();
		tween.TweenProperty(_ship, "offset_left", 0f, ShipEntranceSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(_ship, "offset_right", 0f, ShipEntranceSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	// ---------- main menu

	private void BuildMainMenu()
	{
		var margin = new MarginContainer();
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 64);
		margin.AddThemeConstantOverride("margin_right", 64);
		margin.AddThemeConstantOverride("margin_top", 40);
		margin.AddThemeConstantOverride("margin_bottom", 40);
		AddChild(margin);

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 40);
		margin.AddChild(columns);

		columns.AddChild(BuildBrand());
		columns.AddChild(BuildMenu());

		var version = new Label { Text = VersionText, HorizontalAlignment = HorizontalAlignment.Right };
		version.AddThemeFontSizeOverride("font_size", 12);
		version.AddThemeColorOverride("font_color", UiTheme.TextDim);
		version.SetAnchorsPreset(LayoutPreset.BottomRight);
		version.OffsetLeft = -260;
		version.OffsetTop = -34;
		version.OffsetRight = -22;
		version.OffsetBottom = -12;
		AddChild(version);
	}

	private Control BuildBrand()
	{
		var brand = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.25f,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		brand.AddThemeConstantOverride("separation", 6);

		// The picture is drawn at exactly half its pixel size (500 px tall for 250 px), the cleanest case for the filter.
		var mascotFrame = new Control { CustomMinimumSize = new Vector2(0, MascotHeight + MascotHeadroom * 2) };
		brand.AddChild(mascotFrame);

		_mascot = new TextureRect
		{
			Texture = GD.Load<Texture2D>(MascotPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = TextureFilterEnum.LinearWithMipmapsAnisotropic
		};
		mascotFrame.AddChild(_mascot);
		_mascot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_mascot.OffsetTop = MascotHeadroom;
		_mascot.OffsetBottom = -MascotHeadroom;

		var title = new Label { Text = "STARCAFE", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontOverride("font", UiTheme.TitleFont);
		title.AddThemeFontSizeOverride("font_size", 62);
		title.AddThemeColorOverride("font_color", UiKit.Amber);
		title.AddThemeColorOverride("font_outline_color", new Color(0.35f, 0.18f, 0.04f, 0.9f));
		title.AddThemeConstantOverride("outline_size", 5);
		title.AddThemeColorOverride("font_shadow_color", new Color(0.95f, 0.55f, 0.15f, 0.30f));
		title.AddThemeConstantOverride("shadow_offset_x", 0);
		title.AddThemeConstantOverride("shadow_offset_y", 0);
		title.AddThemeConstantOverride("shadow_outline_size", 16);
		brand.AddChild(title);

		var line = new TextureRect
		{
			Texture = new GradientTexture1D
			{
				Gradient = new Gradient
				{
					Offsets = new[] { 0f, 0.5f, 1f },
					Colors = new[] { new Color(UiKit.Amber, 0f), UiKit.Amber, new Color(UiKit.Amber, 0f) }
				},
				Width = 320
			},
			CustomMinimumSize = new Vector2(320, 2),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		brand.AddChild(line);

		var tagline = new Label { Text = "Simulator einer Raumtaverne", HorizontalAlignment = HorizontalAlignment.Center };
		tagline.AddThemeFontOverride("font", UiTheme.HeadingFont);
		tagline.AddThemeFontSizeOverride("font_size", 19);
		tagline.AddThemeColorOverride("font_color", new Color(0.78f, 0.80f, 0.86f));
		brand.AddChild(tagline);
		return brand;
	}

	private Control BuildMenu()
	{
		var menu = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
			CustomMinimumSize = new Vector2(340, 0)
		};
		menu.AddThemeConstantOverride("separation", 12);

		_newGameButton = MenuButton("Neues Spiel");
		_newGameButton.Pressed += OnStartPressed;
		menu.AddChild(_newGameButton);

		string saved = GameSession.DescribeAutosave();
		_continueButton = MenuButton(saved == null ? "Weiter" : $"Weiter  ·  {saved}");
		_continueButton.Disabled = saved == null;
		_continueButton.Pressed += OnContinuePressed;
		menu.AddChild(_continueButton);

		_settingsButton = MenuButton("KI & Stimme");
		_settingsButton.Pressed += OpenSettings;
		menu.AddChild(_settingsButton);

		var quit = MenuButton("Beenden");
		quit.Pressed += () => GetTree().Quit();
		menu.AddChild(quit);

		return menu;
	}

	private static Button MenuButton(string text) => new()
	{
		Text = text,
		ThemeTypeVariation = UiTheme.BigButton,
		Alignment = HorizontalAlignment.Left,
		SizeFlagsHorizontal = SizeFlags.ExpandFill
	};

	// ---------- settings card

	private void BuildSettingsOverlay()
	{
		_settingsOverlay = new Control { Visible = false };
		_settingsOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_settingsOverlay);

		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.78f), MouseFilter = MouseFilterEnum.Stop };
		dim.SetAnchorsPreset(LayoutPreset.FullRect);
		_settingsOverlay.AddChild(dim);

		var center = new CenterContainer();
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		_settingsOverlay.AddChild(center);

		var card = new PanelContainer { CustomMinimumSize = new Vector2(980, 560) };
		center.AddChild(card);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 12);
		card.AddChild(column);

		var header = new HBoxContainer();
		column.AddChild(header);

		var title = new Label { Text = "KI & Stimme", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		title.AddThemeFontSizeOverride("font_size", 28);
		title.AddThemeColorOverride("font_color", UiKit.Amber);
		header.AddChild(title);

		var back = new Button { Text = "Zurück", ThemeTypeVariation = UiTheme.PrimaryButton, CustomMinimumSize = new Vector2(140, 0) };
		back.Pressed += CloseSettings;
		header.AddChild(back);

		column.AddChild(UiKit.Scroll(new LauncherPanel()));
	}

	private void OpenSettings()
	{
		_settingsOverlay.Visible = true;
	}

	private void CloseSettings()
	{
		_settingsOverlay.Visible = false;
		_settingsButton.GrabFocus();
	}

	// ---------- motion

	private void PlayEntrance()
	{
		Modulate = new Color(1, 1, 1, 0);
		CreateTween().TweenProperty(this, "modulate:a", 1.0f, 0.7f);

	}

	// A slow sine: the mascot drifts a couple of pixels up and down. Weak, and free of corners, so it never looks jerky.
	public override void _Process(double delta)
	{
		if (_mascot == null)
			return;

		_floatClock += delta;
		float offset = Mathf.Sin((float)(_floatClock * Mathf.Tau / FloatSeconds)) * FloatPixels;
		_mascot.OffsetTop = MascotHeadroom + offset;
		_mascot.OffsetBottom = -MascotHeadroom + offset;

		if (_ship != null)
		{
			float bob = Mathf.Sin((float)(_floatClock * Mathf.Tau / ShipBobSeconds)) * ShipBobPixels;
			_ship.OffsetTop = bob;
			_ship.OffsetBottom = bob;
		}
	}

	// ---------- actions

	private void OnStartPressed()
	{
		GameSession.NewGame();
		GetTree().ChangeSceneToFile(GameScenePath);
	}

	private void OnContinuePressed()
	{
		if (!GameSession.LoadAutosave(out string problem))
		{
			_continueButton.Text = problem;
			_continueButton.Disabled = true;
			return;
		}

		GetTree().ChangeSceneToFile(GameScenePath);
	}

	private static GradientTexture2D RadialGradient(Color center, Color edge, int size, float innerRadius = 0f)
	{
		return new GradientTexture2D
		{
			Gradient = new Gradient
			{
				Offsets = new[] { 0f, innerRadius, 1f },
				Colors = new[] { center, center, edge }
			},
			Width = size,
			Height = size,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 0.5f)
		};
	}
}
