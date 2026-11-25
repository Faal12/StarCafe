using System.Collections.Generic;
using Godot;

/// <summary>
/// The ship has three decks, connected by the elevator: Schankraum (tavern), Wohndeck, Technikdeck.
/// All decks live in the same scene, far apart, so time, guests and events keep running while the
/// player is elsewhere. This node handles the elevator menu, fade transitions, camera limits,
/// depth sorting per deck and the lighting (power on/off, emergency lights, flashlight).
/// </summary>
public partial class DeckManager : Node
{
	public sealed record Deck(string Id, string Name, Rect2 Bounds, Vector2 Arrival);

	public const string Schankraum = "schankraum";

	public const string Wohndeck = "wohndeck";

	public const string Technikdeck = "technikdeck";

	public static readonly Deck[] Decks =
	{
		new Deck(Wohndeck, "Deck 2 · Wohndeck", new Rect2(0f, 2000f, 1400f, 640f), new Vector2(170f, 2470f)),
		new Deck(Schankraum, "Deck 1 · Schankraum", new Rect2(0f, 0f, 1872f, 1152f), new Vector2(1650f, 1040f)),
		new Deck(Technikdeck, "Unterdeck · Technik", new Rect2(0f, 3000f, 1800f, 1100f), new Vector2(230f, 3990f)),
	};

	private static DeckManager _instance;

	/// <summary>Deck the player is on.</summary>
	public static string Current { get; private set; } = Schankraum;

	/// <summary>Ship power. Off during a blackout (Stromausfall): everything dark, flashlight on.</summary>
	public static bool PowerOn { get; private set; } = true;

	public static bool InTavern => Current == Schankraum;

	public static bool IsTraveling { get; private set; }

	private CanvasModulate _modulate;

	private PointLight2D _flashlight;

	private CanvasLayer _ui;

	private ColorRect _fade;

	private Control _menu;

	private VBoxContainer _menuButtons;

	public static DeckManager Instance => _instance;

	/// <summary>Z index sorted by the height inside the deck (decks are stacked far apart).</summary>
	public static int SortZ(float globalY)
	{
		float origin = globalY >= 2900f ? 3000f : (globalY >= 1900f ? 2000f : 0f);
		return Mathf.Clamp(Mathf.RoundToInt(globalY - origin), -3999, 4000);
	}

	public static Deck DeckById(string id) => System.Array.Find(Decks, d => d.Id == id);

	public static Deck DeckAt(Vector2 position)
	{
		foreach (Deck deck in Decks)
		{
			if (deck.Bounds.Grow(200f).HasPoint(position))
			{
				return deck;
			}
		}
		return Decks[1];
	}

	public override void _Ready()
	{
		_instance = this;
		ProcessMode = ProcessModeEnum.Always;
		Current = Schankraum;
		_modulate = new CanvasModulate { Color = Colors.White };
		AddChild(_modulate);
		Player player = PlayerNode;
		if (player != null)
		{
			_flashlight = new PointLight2D
			{
				Texture = FlashlightTexture(),
				TextureScale = 2.2f,
				Energy = 1.25f,
				Color = new Color(1f, 0.95f, 0.85f),
				Enabled = false,
				Position = new Vector2(0f, -30f)
			};
			player.AddChild(_flashlight);
		}
		BuildUi();
		ApplyCamera(DeckById(Current));
		ApplyLighting();
		// Elevator in the tavern (next to the elevator door in the corner) – deferred, the scene is still loading.
		Callable.From(AddTavernElevator).CallDeferred();
	}

	private void AddTavernElevator()
	{
		if (GetParent() is Node2D tavern)
		{
			Interactable elevator = DeckBuilder.Use(tavern, new Vector2(1650f, 1035f), "[E] Aufzug", 85f, new Vector2(0f, -110f));
			elevator.Used += OpenElevator;
		}
	}

	public override void _ExitTree()
	{
		if (_instance == this)
		{
			_instance = null;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_menu.Visible && @event.IsActionPressed("ui_cancel"))
		{
			CloseMenu();
			GetViewport().SetInputAsHandled();
		}
	}

	public static bool MenuOpen => _instance != null && _instance._menu.Visible;

	private Player PlayerNode => GetTree().GetFirstNodeInGroup("Player") as Player;

	public static void SetPower(bool on)
	{
		PowerOn = on;
		_instance?.ApplyLighting();
	}

	/// <summary>Opens the elevator menu.</summary>
	public static void OpenElevator()
	{
		_instance?.ShowMenu();
	}

	/// <summary>Moves the player to a deck with a short fade. Optional exact target position.</summary>
	public static void Travel(string deckId, Vector2? target = null, System.Action arrived = null)
	{
		_instance?.DoTravel(deckId, target, arrived);
	}

	private void ShowMenu()
	{
		UiKit.ClearChildren(_menuButtons);
		foreach (Deck deck in Decks)
		{
			Button button = Holo.Button(deck.Name + (deck.Id == Current ? "   (hier)" : ""));
			button.Disabled = deck.Id == Current;
			button.CustomMinimumSize = new Vector2(300f, 44f);
			string id = deck.Id;
			button.Pressed += () =>
			{
				CloseMenu();
				DoTravel(id, null, null);
			};
			_menuButtons.AddChild(button);
		}
		Button cancel = Holo.Button("Abbrechen");
		cancel.Pressed += CloseMenu;
		_menuButtons.AddChild(cancel);
		_menu.Visible = true;
		GetTree().Paused = true;
		foreach (Node child in _menuButtons.GetChildren())
		{
			if (child is Button { Disabled: false } first)
			{
				first.CallDeferred(Control.MethodName.GrabFocus);
				break;
			}
		}
	}

	private void CloseMenu()
	{
		_menu.Visible = false;
		GetTree().Paused = false;
	}

	private void DoTravel(string deckId, Vector2? target, System.Action arrived)
	{
		Deck deck = DeckById(deckId);
		Player player = PlayerNode;
		if (deck == null || player == null || IsTraveling)
		{
			return;
		}
		IsTraveling = true;
		player.CanMove = false;
		Tween tween = CreateTween();
		tween.SetPauseMode(Tween.TweenPauseMode.Process);
		tween.TweenProperty(_fade, "color:a", 1f, 0.35);
		tween.TweenCallback(Callable.From(() =>
		{
			Current = deck.Id;
			player.Teleport(target ?? deck.Arrival);
			ApplyCamera(deck);
			ApplyLighting();
		}));
		tween.TweenInterval(0.25);
		tween.TweenProperty(_fade, "color:a", 0f, 0.35);
		tween.TweenCallback(Callable.From(() =>
		{
			IsTraveling = false;
			player.CanMove = !UiBus.IsOpen(UiWindow.Dialogue);
			arrived?.Invoke();
		}));
	}

	private void ApplyCamera(Deck deck)
	{
		Camera2D camera = PlayerNode?.Camera;
		if (camera == null || deck == null)
		{
			return;
		}
		camera.LimitLeft = (int)deck.Bounds.Position.X;
		camera.LimitTop = (int)deck.Bounds.Position.Y;
		camera.LimitRight = (int)deck.Bounds.End.X;
		camera.LimitBottom = (int)deck.Bounds.End.Y;
		camera.ResetSmoothing();
	}

	private void ApplyLighting()
	{
		Color color;
		if (Current == Technikdeck)
		{
			// Emergency lighting below deck; nearly black without power.
			color = PowerOn ? new Color(0.62f, 0.66f, 0.74f) : new Color(0.14f, 0.15f, 0.2f);
		}
		else
		{
			color = PowerOn ? Colors.White : new Color(0.2f, 0.21f, 0.27f);
		}
		_modulate.Color = color;
		if (_flashlight != null)
		{
			_flashlight.Enabled = color.V < 0.5f;
		}
	}

	private static GradientTexture2D FlashlightTexture()
	{
		Gradient gradient = new Gradient();
		gradient.Offsets = new float[] { 0f, 0.45f, 1f };
		gradient.Colors = new Color[] { Colors.White, new Color(1f, 1f, 1f, 0.45f), new Color(1f, 1f, 1f, 0f) };
		return new GradientTexture2D
		{
			Gradient = gradient,
			Width = 256,
			Height = 256,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 0.5f)
		};
	}

	private void BuildUi()
	{
		_ui = new CanvasLayer { Layer = 40 };
		AddChild(_ui);
		_fade = new ColorRect { Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		_fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_ui.AddChild(_fade);
		_menu = new Control { Visible = false };
		_menu.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_ui.AddChild(_menu);
		ColorRect dim = new ColorRect { Color = new Color(0f, 0.02f, 0.04f, 0.5f) };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_menu.AddChild(dim);
		CenterContainer center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_menu.AddChild(center);
		PanelContainer panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", Holo.Panel(14));
		center.AddChild(panel);
		VBoxContainer box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		panel.AddChild(box);
		Label title = Holo.Caption("Aufzug  ·  Deck wählen", 13);
		title.AddThemeColorOverride("font_color", Holo.Accent);
		box.AddChild(title);
		_menuButtons = new VBoxContainer();
		_menuButtons.AddThemeConstantOverride("separation", 6);
		box.AddChild(_menuButtons);
	}
}
