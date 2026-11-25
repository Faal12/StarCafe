using System.Collections.Generic;
using Godot;

/// <summary>One inventory slot; supports drag &amp; drop between slots.</summary>
public partial class SlotControl : PanelContainer
{
	public int SlotIndex { get; set; }

	public System.Action<int> Clicked;

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (PlayerState.Inventory[SlotIndex] == null)
		{
			return default;
		}
		Control preview = Holo.ItemIcon(PlayerState.Inventory[SlotIndex], 40f);
		preview.Modulate = new Color(1f, 1f, 1f, 0.8f);
		SetDragPreview(preview);
		return SlotIndex;
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return data.VariantType == Variant.Type.Int;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		PlayerState.Inventory.Swap(data.AsInt32(), SlotIndex);
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			Clicked?.Invoke(SlotIndex);
		}
	}
}

/// <summary>
/// Hotbar (8 slots, keys 1–8), credits, toasts and the inventory panel (Tab).
/// Minimal holo look. Hidden while dialogues/events/bar are open.
/// </summary>
public partial class InventoryHud : CanvasLayer
{
	private const float SlotSize = 54f;

	private static InventoryHud _instance;

	private Control _hud;

	private HBoxContainer _hotbar;

	private Label _selectedName;

	private Label _credits;

	private VBoxContainer _toasts;

	private Control _panel;

	private GridContainer _grid;

	private VBoxContainer _details;

	private Label _panelCredits;

	private int _detailSlot = -1;

	public static bool IsPanelOpen { get; private set; }

	/// <summary>Short message above the hotbar ("+₡12 Trinkgeld", "Neues Rezept …").</summary>
	public static void Toast(string text, Color? color = null)
	{
		_instance?.ShowToast(text, color ?? Holo.Text);
	}

	public override void _Ready()
	{
		_instance = this;
		Layer = 15;
		ProcessMode = ProcessModeEnum.Always;
		BuildHud();
		BuildPanel();
		Refresh();
	}

	public override void _EnterTree()
	{
		PlayerState.Inventory.Changed += Refresh;
		PlayerState.CreditsChanged += OnCredits;
		PlayerState.RecipeDiscovered += OnRecipe;
	}

	public override void _ExitTree()
	{
		PlayerState.Inventory.Changed -= Refresh;
		PlayerState.CreditsChanged -= OnCredits;
		PlayerState.RecipeDiscovered -= OnRecipe;
		if (_instance == this)
		{
			_instance = null;
		}
		if (IsPanelOpen)
		{
			IsPanelOpen = false;
			GetTree().Paused = false;
		}
	}

	public override void _Process(double delta)
	{
		bool covered = DialogueController.IsOpen || EventPanel.IsOpen || BarPanel.IsOpen || OrderPanel.IsOpen;
		_hud.Visible = !covered || IsPanelOpen;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (DialogueController.IsOpen || EventPanel.IsOpen || BarPanel.IsOpen || OrderPanel.IsOpen)
		{
			return;
		}
		if (@event is InputEventKey { Pressed: true, Echo: false } key)
		{
			if (key.Keycode == Key.Tab)
			{
				TogglePanel();
				GetViewport().SetInputAsHandled();
				return;
			}
			if (IsPanelOpen && key.Keycode == Key.Escape)
			{
				TogglePanel();
				GetViewport().SetInputAsHandled();
				return;
			}
			int number = (int)key.PhysicalKeycode - (int)Key.Key1;
			if (!key.CtrlPressed && number >= 0 && number < Inventory.HotbarSize)
			{
				PlayerState.Inventory.Selected = number;
				GetViewport().SetInputAsHandled();
			}
		}
		else if (@event is InputEventMouseButton { Pressed: true } wheel && !IsPanelOpen)
		{
			if (wheel.ButtonIndex == MouseButton.WheelDown)
			{
				PlayerState.Inventory.Selected = (PlayerState.Inventory.Selected + 1) % Inventory.HotbarSize;
			}
			else if (wheel.ButtonIndex == MouseButton.WheelUp)
			{
				PlayerState.Inventory.Selected = (PlayerState.Inventory.Selected + Inventory.HotbarSize - 1) % Inventory.HotbarSize;
			}
		}
	}

	private void TogglePanel()
	{
		IsPanelOpen = !IsPanelOpen;
		_panel.Visible = IsPanelOpen;
		GetTree().Paused = IsPanelOpen;
		if (IsPanelOpen)
		{
			_detailSlot = PlayerState.Inventory.Selected;
			Refresh();
		}
	}

	private void OnCredits(int amount, string reason)
	{
		Refresh();
		if (amount > 0)
		{
			ShowToast($"+₡{amount}  {reason}", new Color(0.55f, 1f, 0.7f));
		}
		else if (amount < 0)
		{
			ShowToast($"−₡{-amount}  {reason}", new Color(1f, 0.6f, 0.5f));
		}
	}

	private void OnRecipe(RecipeDef recipe)
	{
		ShowToast("Neues Rezept entdeckt: " + recipe.Name, Holo.Strong);
	}

	private void ShowToast(string text, Color color)
	{
		Label label = Holo.Text_(text, 15, color);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
		label.AddThemeConstantOverride("outline_size", 4);
		_toasts.AddChild(label);
		Tween tween = label.CreateTween();
		tween.TweenInterval(2.6);
		tween.TweenProperty(label, "modulate:a", 0f, 0.6);
		tween.TweenCallback(Callable.From(label.QueueFree));
	}

	private void BuildHud()
	{
		_hud = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		_hud.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_hud);

		// Credits, top right.
		PanelContainer creditBox = new PanelContainer();
		StyleBoxFlat creditStyle = Holo.Panel(6);
		creditStyle.SetContentMarginAll(8);
		creditStyle.ContentMarginLeft = 14;
		creditStyle.ContentMarginRight = 14;
		creditBox.AddThemeStyleboxOverride("panel", creditStyle);
		creditBox.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		creditBox.Position = new Vector2(-150f, 12f);
		creditBox.GrowHorizontal = Control.GrowDirection.Begin;
		_hud.AddChild(creditBox);
		_credits = new Label();
		_credits.AddThemeFontOverride("font", UiTheme.HeadingFont);
		_credits.AddThemeFontSizeOverride("font_size", 18);
		_credits.AddThemeColorOverride("font_color", Holo.Accent);
		creditBox.AddChild(_credits);

		// Hotbar, bottom centre.
		VBoxContainer bottom = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = Control.MouseFilterEnum.Ignore };
		bottom.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		bottom.GrowHorizontal = Control.GrowDirection.Both;
		bottom.GrowVertical = Control.GrowDirection.Begin;
		bottom.Position = new Vector2(0f, -12f);
		bottom.AddThemeConstantOverride("separation", 4);
		_hud.AddChild(bottom);
		_toasts = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
		bottom.AddChild(_toasts);
		_selectedName = Holo.Caption("", 11);
		_selectedName.HorizontalAlignment = HorizontalAlignment.Center;
		_selectedName.AddThemeColorOverride("font_color", Holo.Text);
		_selectedName.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
		_selectedName.AddThemeConstantOverride("outline_size", 4);
		bottom.AddChild(_selectedName);
		_hotbar = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		_hotbar.AddThemeConstantOverride("separation", 6);
		bottom.AddChild(_hotbar);
		for (int i = 0; i < Inventory.HotbarSize; i++)
		{
			_hotbar.AddChild(MakeSlot(i, showNumber: true));
		}
	}

	private void BuildPanel()
	{
		_panel = new Control { Visible = false };
		_panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_panel);
		ColorRect dim = new ColorRect { Color = new Color(0f, 0.02f, 0.04f, 0.55f) };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_panel.AddChild(dim);
		CenterContainer center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_panel.AddChild(center);
		PanelContainer box = new PanelContainer { CustomMinimumSize = new Vector2(760f, 0f) };
		box.AddThemeStyleboxOverride("panel", Holo.Panel(16));
		center.AddChild(box);
		VBoxContainer column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		box.AddChild(column);
		HBoxContainer header = new HBoxContainer();
		column.AddChild(header);
		Label title = Holo.Caption("Inventar", 13);
		title.AddThemeColorOverride("font_color", Holo.Accent);
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(title);
		_panelCredits = Holo.Caption("", 13);
		_panelCredits.AddThemeColorOverride("font_color", Holo.Accent);
		header.AddChild(_panelCredits);
		HBoxContainer body = new HBoxContainer();
		body.AddThemeConstantOverride("separation", 18);
		column.AddChild(body);
		VBoxContainer left = new VBoxContainer();
		left.AddThemeConstantOverride("separation", 6);
		body.AddChild(left);
		left.AddChild(Holo.Caption("Schnellleiste 1–8  ·  Rucksack", 10));
		_grid = new GridContainer { Columns = 6 };
		_grid.AddThemeConstantOverride("h_separation", 6);
		_grid.AddThemeConstantOverride("v_separation", 6);
		left.AddChild(_grid);
		for (int i = 0; i < Inventory.Size; i++)
		{
			_grid.AddChild(MakeSlot(i, showNumber: i < Inventory.HotbarSize));
		}
		Label hint = Holo.Caption("Ziehen zum Umsortieren  ·  Tab schließt", 10);
		left.AddChild(hint);
		_details = new VBoxContainer { CustomMinimumSize = new Vector2(330f, 0f) };
		_details.AddThemeConstantOverride("separation", 6);
		body.AddChild(_details);
	}

	private SlotControl MakeSlot(int index, bool showNumber)
	{
		SlotControl slot = new SlotControl { SlotIndex = index, CustomMinimumSize = new Vector2(SlotSize, SlotSize) };
		slot.Clicked = i =>
		{
			if (i < Inventory.HotbarSize)
			{
				PlayerState.Inventory.Selected = i;
			}
			_detailSlot = i;
			Refresh();
		};
		return slot;
	}

	private void Refresh()
	{
		if (_hotbar == null)
		{
			return;
		}
		_credits.Text = $"₡ {PlayerState.Credits}";
		_panelCredits.Text = $"₡ {PlayerState.Credits}";
		foreach (Node child in _hotbar.GetChildren())
		{
			FillSlot((SlotControl)child, highlight: ((SlotControl)child).SlotIndex == PlayerState.Inventory.Selected, showNumber: true);
		}
		foreach (Node child in _grid.GetChildren())
		{
			SlotControl slot = (SlotControl)child;
			FillSlot(slot, highlight: slot.SlotIndex == _detailSlot, showNumber: slot.SlotIndex < Inventory.HotbarSize);
		}
		ItemStack selected = PlayerState.Inventory.SelectedStack;
		_selectedName.Text = selected?.DisplayName.ToUpperInvariant() ?? "";
		FillDetails();
	}

	private static void FillSlot(SlotControl slot, bool highlight, bool showNumber)
	{
		foreach (Node child in slot.GetChildren())
		{
			child.QueueFree();
		}
		slot.AddThemeStyleboxOverride("panel", Holo.Slot(highlight));
		Control layer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		slot.AddChild(layer);
		ItemStack stack = PlayerState.Inventory[slot.SlotIndex];
		if (stack != null)
		{
			Control icon = Holo.ItemIcon(stack, 36f);
			icon.Position = new Vector2((SlotSize - 8f - 36f) / 2f, (SlotSize - 8f - 36f) / 2f);
			layer.AddChild(icon);
			if (stack.Count > 1)
			{
				Label count = Holo.Caption(stack.Count.ToString(), 11);
				count.AddThemeColorOverride("font_color", Holo.Text);
				count.Position = new Vector2(SlotSize - 26f, SlotSize - 24f);
				layer.AddChild(count);
			}
			slot.TooltipText = stack.DisplayName;
		}
		else
		{
			slot.TooltipText = "";
		}
		if (showNumber)
		{
			Label number = Holo.Caption((slot.SlotIndex + 1).ToString(), 9);
			number.Position = new Vector2(1f, -2f);
			layer.AddChild(number);
		}
	}

	private void FillDetails()
	{
		UiKit.ClearChildren(_details);
		ItemStack stack = _detailSlot >= 0 ? PlayerState.Inventory[_detailSlot] : null;
		if (stack == null)
		{
			_details.AddChild(Holo.Caption("Leerer Platz", 12));
			_details.AddChild(Holo.Text_("Wähle einen Gegenstand, um Details zu sehen.", 13, Holo.TextDim));
			return;
		}
		ItemDef def = stack.Def;
		HBoxContainer head = new HBoxContainer();
		head.AddThemeConstantOverride("separation", 10);
		head.AddChild(Holo.ItemIcon(stack, 44f));
		VBoxContainer names = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		Label name = Holo.Text_(stack.DisplayName, 18, Holo.Text);
		name.AutowrapMode = TextServer.AutowrapMode.Off;
		name.AddThemeFontOverride("font", UiTheme.HeadingFont);
		names.AddChild(name);
		names.AddChild(Holo.Caption(def?.Category == "Getraenk" ? "Getränk" : (def?.Category ?? "?"), 10));
		head.AddChild(names);
		_details.AddChild(head);
		if (stack.ItemId == "getraenk")
		{
			Taste taste = DrinkMixer.TasteOf(stack);
			_details.AddChild(Holo.AxisBar("Wärme", taste.Waerme, DrinkMixer.AxisMax, Holo.Warm));
			_details.AddChild(Holo.AxisBar("Süße", taste.Suesse, DrinkMixer.AxisMax, Holo.Sweet));
			_details.AddChild(Holo.AxisBar("Stärke", taste.Staerke, DrinkMixer.AxisMax, Holo.Strong));
			string forName = stack.Get("fuerName");
			if (!string.IsNullOrEmpty(forName))
			{
				_details.AddChild(Holo.Text_("Für: " + forName, 13, Holo.TextDim));
			}
			string effects = stack.Get("effekte");
			if (!string.IsNullOrEmpty(effects))
			{
				_details.AddChild(Holo.Text_("Wirkung: " + effects.Replace(",", ", "), 13, Holo.Strong));
			}
			_details.AddChild(Holo.Text_("Vorsichtig tragen: Mit Umschalt gehst du langsam und ruhig.", 12, Holo.TextDim));
		}
		else
		{
			_details.AddChild(Holo.Text_(def?.Description ?? "", 13, Holo.TextDim));
			if (def?.Value > 0)
			{
				_details.AddChild(Holo.Caption($"Wert ₡{def.Value}", 10));
			}
		}
	}
}
