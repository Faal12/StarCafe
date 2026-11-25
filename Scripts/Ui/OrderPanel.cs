using System;
using Godot;

/// <summary>
/// Small holo card at a guest's table: take the order, ask for details, see the verdict
/// after serving. Lines are voiced with the guest's own voice. Pauses the game while open.
/// </summary>
public partial class OrderPanel : CanvasLayer
{
	private static OrderPanel _instance;

	private Control _root;

	private PanelContainer _portrait;

	private Label _initial;

	private Label _name;

	private Label _sub;

	private Label _text;

	private Label _hint;

	private Label _extra;

	private HBoxContainer _buttons;

	public static bool IsOpen { get; private set; }

	public override void _Ready()
	{
		_instance = this;
		Layer = 28;
		ProcessMode = ProcessModeEnum.Always;
		Build();
		_root.Visible = false;
	}

	public override void _ExitTree()
	{
		if (_instance == this)
		{
			_instance = null;
		}
		if (IsOpen)
		{
			IsOpen = false;
			GetTree().Paused = false;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (IsOpen && @event.IsActionPressed("ui_cancel"))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	/// <summary>Guest wants to order.</summary>
	public static void ShowOrder(Guest guest, Action onAccept)
	{
		_instance?.OpenOrder(guest, onAccept);
	}

	/// <summary>Guest is still waiting for the drink.</summary>
	public static void ShowWaiting(Guest guest)
	{
		_instance?.OpenWaiting(guest);
	}

	/// <summary>Verdict after serving.</summary>
	public static void ShowRating(Guest guest, string drinkName, DrinkRating rating)
	{
		_instance?.OpenRating(guest, drinkName, rating);
	}

	private void OpenOrder(Guest guest, Action onAccept)
	{
		Fill(guest);
		_text.Text = "„" + guest.Order.Template.Text + "“";
		SetHint(guest);
		_extra.Text = "";
		VoiceQueue.Clear();
		VoiceQueue.Enqueue(guest.DisplayName, guest.Order.Template.Text);
		UiKit.ClearChildren(_buttons);
		AddAskButton(guest);
		Button accept = Holo.Button("Bestellung aufnehmen", primary: true);
		accept.Pressed += () =>
		{
			Close();
			onAccept?.Invoke();
		};
		_buttons.AddChild(accept);
		Button later = Holo.Button("Später");
		later.Pressed += Close;
		_buttons.AddChild(later);
		Open(accept);
	}

	private void OpenWaiting(Guest guest)
	{
		Fill(guest);
		_text.Text = "„Ich warte noch auf meinen Drink, Wirt.“";
		SetHint(guest);
		_extra.Text = "Bestellt: " + guest.Order.Template.Text + "\nAn der Bar (E) mixen, dann hierher bringen. Ein Getränk für diesen Gast wählst du in der Schnellleiste aus.";
		UiKit.ClearChildren(_buttons);
		AddAskButton(guest);
		Button ok = Holo.Button("Bin dran", primary: true);
		ok.Pressed += Close;
		_buttons.AddChild(ok);
		VoiceQueue.Clear();
		VoiceQueue.Enqueue(guest.DisplayName, "Ich warte noch auf meinen Drink, Wirt.");
		Open(ok);
	}

	private void OpenRating(Guest guest, string drinkName, DrinkRating rating)
	{
		Fill(guest);
		int stars = rating.Band switch
		{
			"perfekt" => 3,
			"gut" => 2,
			"mittel" => 1,
			_ => 0,
		};
		_text.Text = "„" + rating.Reaction + "“";
		_hint.Text = drinkName + "   " + new string('★', stars) + new string('☆', 3 - stars);
		_hint.AddThemeColorOverride("font_color", stars >= 2 ? Holo.Accent : (stars == 1 ? Holo.Strong : new Color(1f, 0.55f, 0.5f)));
		_extra.Text = rating.Tip > 0 ? $"Trinkgeld: ₡{rating.Tip}" : "Kein Trinkgeld.";
		UiKit.ClearChildren(_buttons);
		Button ok = Holo.Button("Weiter", primary: true);
		ok.Pressed += Close;
		_buttons.AddChild(ok);
		VoiceQueue.Clear();
		VoiceQueue.Enqueue(guest.DisplayName, rating.Reaction);
		Open(ok);
	}

	private void AddAskButton(Guest guest)
	{
		if (guest.Order.HintRevealed || string.IsNullOrWhiteSpace(guest.Order.Template.Hint))
		{
			return;
		}
		Button ask = Holo.Button("Nachfragen");
		ask.Pressed += () =>
		{
			guest.Order.HintRevealed = true;
			SetHint(guest);
			ask.QueueFree();
			VoiceQueue.Clear();
			VoiceQueue.Enqueue(guest.DisplayName, guest.Order.Template.Hint);
		};
		_buttons.AddChild(ask);
	}

	private void SetHint(Guest guest)
	{
		_hint.AddThemeColorOverride("font_color", Holo.Accent);
		_hint.Text = guest.Order.HintRevealed ? "Nachgefragt: " + guest.Order.Template.Hint : "";
	}

	private void Fill(Guest guest)
	{
		Color color = WorldStage.FactionColor(guest.Faction);
		StyleBoxFlat box = new StyleBoxFlat { BgColor = new Color(color.Darkened(0.4f), 0.9f), BorderColor = color.Lightened(0.3f) };
		box.SetBorderWidthAll(1);
		box.SetCornerRadiusAll(8);
		_portrait.AddThemeStyleboxOverride("panel", box);
		_initial.Text = guest.DisplayName.Substring(0, 1).ToUpperInvariant();
		_name.Text = guest.DisplayName;
		string faction = EventLibrary.TryFaction(guest.Faction, out Faction f) ? FactionInfo.DisplayName(f) : "ohne Fraktion";
		_sub.Text = $"{guest.Species?.Name ?? "Gast"}  ·  {faction}".ToUpperInvariant();
	}

	private void Open(Button focus)
	{
		IsOpen = true;
		_root.Visible = true;
		GetTree().Paused = true;
		focus.CallDeferred(Control.MethodName.GrabFocus);
	}

	private void Close()
	{
		IsOpen = false;
		_root.Visible = false;
		GetTree().Paused = false;
	}

	private void Build()
	{
		_root = new Control { Theme = UiTheme.Get() };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);
		ColorRect dim = new ColorRect { Color = new Color(0f, 0.02f, 0.04f, 0.45f), MouseFilter = Control.MouseFilterEnum.Stop };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(dim);
		CenterContainer center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(center);
		PanelContainer frame = new PanelContainer { CustomMinimumSize = new Vector2(600f, 0f) };
		frame.AddThemeStyleboxOverride("panel", Holo.Panel(14));
		center.AddChild(frame);
		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		frame.AddChild(row);
		_portrait = new PanelContainer { CustomMinimumSize = new Vector2(96f, 120f), SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
		row.AddChild(_portrait);
		_initial = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		_initial.AddThemeFontOverride("font", UiTheme.TitleFont);
		_initial.AddThemeFontSizeOverride("font_size", 54);
		_portrait.AddChild(_initial);
		VBoxContainer body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 6);
		row.AddChild(body);
		_name = Holo.Text_("", 18, Holo.Text);
		_name.AddThemeFontOverride("font", UiTheme.HeadingFont);
		body.AddChild(_name);
		_sub = Holo.Caption("", 10);
		body.AddChild(_sub);
		_text = Holo.Text_("", 16, Holo.Text);
		body.AddChild(_text);
		_hint = Holo.Text_("", 14, Holo.Accent);
		body.AddChild(_hint);
		_extra = Holo.Text_("", 12, Holo.TextDim);
		body.AddChild(_extra);
		_buttons = new HBoxContainer();
		_buttons.AddThemeConstantOverride("separation", 8);
		body.AddChild(_buttons);
	}
}
