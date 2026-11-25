using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// "Am Tresen" – the bar mini-game. Pick an order, put up to four ingredients into the shaker,
/// choose a method, watch the taste profile and finish the drink onto the tray (inventory).
/// Matching an exact recipe discovers it in the recipe book. Pauses the game while open.
/// </summary>
public partial class BarPanel : CanvasLayer
{
	private static BarPanel _instance;

	private readonly DrinkMixer _mixer = new DrinkMixer();

	private Control _root;

	private Control _mixView;

	private Control _bookView;

	private Button _tabMix;

	private Button _tabBook;

	private VBoxContainer _orders;

	private VBoxContainer _orderCard;

	private GridContainer _shelf;

	private HBoxContainer _shakerSlots;

	private VBoxContainer _profile;

	private HBoxContainer _methods;

	private Label _result;

	private Button _finish;

	private VBoxContainer _book;

	private Label _message;

	private GuestOrder _selectedOrder;

	public static bool IsOpen { get => UiBus.IsOpen(UiWindow.Bar); private set => UiBus.SetOpen(UiWindow.Bar, value); }

	/// <summary>Accepted orders waiting for a drink (logic: <see cref="OrderBook"/>).</summary>
	private static List<GuestOrder> OpenOrders => OrderBook.Open;

	public static void Open()
	{
		_instance?.Show();
	}

	public override void _Ready()
	{
		_instance = this;
		Layer = 28;
		ProcessMode = ProcessModeEnum.Always;
		BarLibrary.EnsureLoaded();
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

	private void Show()
	{
		IsOpen = true;
		_root.Visible = true;
		GetTree().Paused = true;
		if (_selectedOrder == null || !OpenOrders.Contains(_selectedOrder))
		{
			_selectedOrder = OpenOrders.FirstOrDefault(o => !HasDrinkFor(o));
		}
		_message.Text = "";
		ShowMix();
		RefreshAll();
	}

	private void Close()
	{
		IsOpen = false;
		_root.Visible = false;
		GetTree().Paused = false;
	}

	private static bool HasDrinkFor(GuestOrder order)
	{
		return PlayerState.Inventory.All().Any(s => s.Stack.ItemId == "getraenk" && s.Stack.Get("fuer") == order.GuestId);
	}

	private void ShowMix()
	{
		_mixView.Visible = true;
		_bookView.Visible = false;
		_tabMix.AddThemeStyleboxOverride("normal", Holo.Slot(true));
		_tabBook.AddThemeStyleboxOverride("normal", Holo.Slot(false));
	}

	private void ShowBook()
	{
		_mixView.Visible = false;
		_bookView.Visible = true;
		_tabMix.AddThemeStyleboxOverride("normal", Holo.Slot(false));
		_tabBook.AddThemeStyleboxOverride("normal", Holo.Slot(true));
		RefreshBook();
	}

	private void RefreshAll()
	{
		RefreshOrders();
		RefreshShelf();
		RefreshShaker();
	}

	private void RefreshOrders()
	{
		UiKit.ClearChildren(_orders);
		if (OpenOrders.Count == 0)
		{
			_orders.AddChild(Holo.Text_("Keine offenen Bestellungen. Geh zu den Gästen (E) und nimm Bestellungen auf – oder mix einfach etwas.", 13, Holo.TextDim));
		}
		foreach (GuestOrder order in OpenOrders)
		{
			bool ready = HasDrinkFor(order);
			Button button = Holo.Button((ready ? "✓ " : "") + order.GuestName);
			button.Alignment = HorizontalAlignment.Left;
			button.TooltipText = order.Template.Text;
			if (order == _selectedOrder)
			{
				button.AddThemeStyleboxOverride("normal", Holo.Slot(true));
			}
			GuestOrder picked = order;
			button.Pressed += () =>
			{
				_selectedOrder = picked;
				RefreshOrders();
			};
			_orders.AddChild(button);
		}
		UiKit.ClearChildren(_orderCard);
		if (_selectedOrder == null)
		{
			_orderCard.AddChild(Holo.Caption("Für niemanden", 11));
			_orderCard.AddChild(Holo.Text_("Experimentieren ist erlaubt: Neue Rezepte landen im Rezeptbuch.", 13, Holo.TextDim));
			return;
		}
		_orderCard.AddChild(Holo.Caption(_selectedOrder.Species?.Name ?? "Gast", 11));
		_orderCard.AddChild(Holo.Text_("„" + _selectedOrder.Template.Text + "“", 15, Holo.Text));
		if (_selectedOrder.HintRevealed && !string.IsNullOrWhiteSpace(_selectedOrder.Template.Hint))
		{
			_orderCard.AddChild(Holo.Text_("Nachgefragt: " + _selectedOrder.Template.Hint, 13, Holo.Accent));
		}
		else
		{
			_orderCard.AddChild(Holo.Text_("Unklar? Am Tisch nachfragen (E).", 12, Holo.TextDim));
		}
		if (!string.IsNullOrWhiteSpace(_selectedOrder.Species?.Description))
		{
			_orderCard.AddChild(Holo.Text_(_selectedOrder.Species.Description, 12, Holo.TextDim));
		}
		Button none = Holo.Button("Für niemanden mixen");
		none.Pressed += () =>
		{
			_selectedOrder = null;
			RefreshOrders();
		};
		_orderCard.AddChild(none);
	}

	private void RefreshShelf()
	{
		UiKit.ClearChildren(_shelf);
		foreach (IngredientDef ingredient in BarLibrary.Ingredients)
		{
			bool limited = !string.IsNullOrEmpty(ingredient.RequiresItem);
			int have = limited ? PlayerState.Inventory.Count(ingredient.RequiresItem) - _mixer.Ingredients.Count(i => i == ingredient.Id) : int.MaxValue;
			Button tile = Holo.Button("");
			tile.CustomMinimumSize = new Vector2(140f, 62f);
			tile.TooltipText = ingredient.Description + (ingredient.Alcohol ? "\n(alkoholisch)" : "");
			tile.Disabled = _mixer.IsFull || have <= 0;
			VBoxContainer content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			content.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			content.OffsetLeft = 8;
			content.OffsetTop = 5;
			content.AddThemeConstantOverride("separation", 2);
			HBoxContainer top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			top.AddThemeConstantOverride("separation", 6);
			Control dot = Holo.Icon(ingredient.Color, ingredient.Alcohol ? "‰" : "", 16f);
			dot.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			top.AddChild(dot);
			Label name = Holo.Text_(ingredient.Name, 13, tile.Disabled ? Holo.TextDim : Holo.Text);
			name.AutowrapMode = TextServer.AutowrapMode.Off;
			name.MouseFilter = Control.MouseFilterEnum.Ignore;
			top.AddChild(name);
			content.AddChild(top);
			string axes = $"W {Signed(ingredient.Waerme)}   S {Signed(ingredient.Suesse)}   St {Signed(ingredient.Staerke)}";
			Label axisLabel = Holo.Caption(axes, 10);
			axisLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			content.AddChild(axisLabel);
			if (limited)
			{
				Label count = Holo.Caption($"Vorrat: {System.Math.Max(0, have)}", 9);
				count.MouseFilter = Control.MouseFilterEnum.Ignore;
				content.AddChild(count);
			}
			tile.AddChild(content);
			string id = ingredient.Id;
			tile.Pressed += () =>
			{
				if (_mixer.Add(id))
				{
					RefreshShelf();
					RefreshShaker();
				}
			};
			_shelf.AddChild(tile);
		}
	}

	private void RefreshShaker()
	{
		UiKit.ClearChildren(_shakerSlots);
		for (int i = 0; i < DrinkMixer.MaxIngredients; i++)
		{
			Button slot = Holo.Button("");
			slot.CustomMinimumSize = new Vector2(70f, 70f);
			if (i < _mixer.Ingredients.Count)
			{
				IngredientDef ingredient = BarLibrary.Ingredient(_mixer.Ingredients[i]);
				Control icon = Holo.Icon(ingredient?.Color, Initials(ingredient?.Name), 40f);
				icon.Position = new Vector2(15f, 8f);
				slot.AddChild(icon);
				Label label = Holo.Caption(ShortName(ingredient?.Name), 8);
				label.Position = new Vector2(4f, 52f);
				label.Size = new Vector2(62f, 14f);
				label.HorizontalAlignment = HorizontalAlignment.Center;
				label.ClipText = true;
				slot.AddChild(label);
				slot.TooltipText = (ingredient?.Name ?? "") + " – klicken zum Entfernen";
				int index = i;
				slot.Pressed += () =>
				{
					_mixer.RemoveAt(index);
					RefreshShelf();
					RefreshShaker();
				};
			}
			else
			{
				slot.Disabled = true;
				slot.Text = "+";
			}
			_shakerSlots.AddChild(slot);
		}
		UiKit.ClearChildren(_profile);
		Taste taste = _mixer.Profile;
		_profile.AddChild(Holo.AxisBar("Wärme", taste.Waerme, DrinkMixer.AxisMax, Holo.Warm, 170f));
		_profile.AddChild(Holo.AxisBar("Süße", taste.Suesse, DrinkMixer.AxisMax, Holo.Sweet, 170f));
		_profile.AddChild(Holo.AxisBar("Stärke", taste.Staerke, DrinkMixer.AxisMax, Holo.Strong, 170f));
		if (_mixer.HasAlcohol)
		{
			_profile.AddChild(Holo.Caption("‰ alkoholisch", 10));
		}
		UiKit.ClearChildren(_methods);
		foreach (MethodDef method in BarLibrary.Methods)
		{
			Button button = Holo.Button(method.Name);
			button.TooltipText = method.Description + "\n" + method.Taste;
			button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			if (_mixer.Method == method.Id)
			{
				button.AddThemeStyleboxOverride("normal", Holo.Slot(true));
				button.AddThemeColorOverride("font_color", Holo.LineStrong);
			}
			string id = method.Id;
			button.Pressed += () =>
			{
				_mixer.Method = _mixer.Method == id ? null : id;
				RefreshShaker();
			};
			_methods.AddChild(button);
		}
		RecipeDef recipe = _mixer.Recipe;
		if (_mixer.IsEmpty)
		{
			_result.Text = "Wähle Zutaten aus dem Regal.";
			_result.AddThemeColorOverride("font_color", Holo.TextDim);
		}
		else if (string.IsNullOrEmpty(_mixer.Method))
		{
			_result.Text = "Wie zubereiten? Schütteln, Rühren, Erhitzen oder auf Eis.";
			_result.AddThemeColorOverride("font_color", Holo.TextDim);
		}
		else if (recipe == null)
		{
			_result.Text = "Eigenkreation";
			_result.AddThemeColorOverride("font_color", Holo.Text);
		}
		else if (PlayerState.KnowsRecipe(recipe.Id))
		{
			_result.Text = "✓ " + recipe.Name;
			_result.AddThemeColorOverride("font_color", Holo.Accent);
		}
		else
		{
			_result.Text = "??? – das ergibt etwas Bekanntes …";
			_result.AddThemeColorOverride("font_color", Holo.Strong);
		}
		_finish.Disabled = !_mixer.CanFinish;
	}

	private void RefreshBook()
	{
		UiKit.ClearChildren(_book);
		int known = BarLibrary.Recipes.Count(r => PlayerState.KnowsRecipe(r.Id));
		_book.AddChild(Holo.Caption($"{known} von {BarLibrary.Recipes.Count} Rezepten entdeckt", 11));
		foreach (RecipeDef recipe in BarLibrary.Recipes)
		{
			PanelContainer card = new PanelContainer();
			StyleBoxFlat style = Holo.Slot(false);
			style.SetContentMarginAll(10);
			card.AddThemeStyleboxOverride("panel", style);
			VBoxContainer box = new VBoxContainer();
			box.AddThemeConstantOverride("separation", 3);
			card.AddChild(box);
			if (PlayerState.KnowsRecipe(recipe.Id))
			{
				HBoxContainer head = new HBoxContainer();
				Label name = Holo.Text_(recipe.Name, 16, Holo.Accent);
				name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				head.AddChild(name);
				head.AddChild(Holo.Caption($"₡{recipe.Price}", 11));
				box.AddChild(head);
				string ingredients = string.Join(" + ", recipe.Ingredients.Select(i => BarLibrary.Ingredient(i)?.Name ?? i));
				box.AddChild(Holo.Text_(ingredients + "  ·  " + (BarLibrary.Method(recipe.Method)?.Name ?? recipe.Method), 13, Holo.Text));
				box.AddChild(Holo.Caption(DrinkMixer.RecipeProfile(recipe).ToString(), 10));
				box.AddChild(Holo.Text_(recipe.Description, 12, Holo.TextDim));
				if (recipe.Effects.Count > 0)
				{
					box.AddChild(Holo.Text_("Wirkung: " + string.Join(", ", recipe.Effects), 12, Holo.Strong));
				}
			}
			else
			{
				box.AddChild(Holo.Text_("??? – noch nicht entdeckt", 15, Holo.TextDim));
				box.AddChild(Holo.Text_($"{recipe.Ingredients.Count} Zutaten. Gäste verraten manchmal, was hineingehört.", 12, Holo.TextDim));
			}
			_book.AddChild(card);
		}
	}

	private void Finish()
	{
		if (!_mixer.CanFinish)
		{
			return;
		}
		if (!PlayerState.Inventory.HasFreeSlot)
		{
			_message.Text = "Kein Platz auf dem Tablett (Inventar voll).";
			return;
		}
		foreach (string id in _mixer.Ingredients)
		{
			IngredientDef ingredient = BarLibrary.Ingredient(id);
			if (!string.IsNullOrEmpty(ingredient?.RequiresItem))
			{
				PlayerState.Inventory.TryRemove(ingredient.RequiresItem);
			}
			int cost = Economy.IngredientCost(ingredient);
			if (cost > 0)
			{
				PlayerState.AddCredits(-cost, "Einkauf: " + ingredient.Name);
			}
		}
		RecipeDef recipe = _mixer.Recipe;
		bool isNew = recipe != null && PlayerState.LearnRecipe(recipe);
		ItemStack drink = _mixer.MakeDrink(_selectedOrder);
		PlayerState.Inventory.TryAdd(drink);
		_message.Text = (isNew ? "Neues Rezept: " + recipe.Name + "! " : "") + drink.DisplayName + " steht auf dem Tablett" + (_selectedOrder != null ? " – für " + _selectedOrder.GuestName + "." : ".");
		if (isNew)
		{
			VoiceQueue.Enqueue(FlokiAdvisor.Name, "Ein neues Rezept. Ich trage es ein. Widerwillig. " + recipe.Name + ".");
		}
		_mixer.Clear();
		_selectedOrder = OpenOrders.FirstOrDefault(o => !HasDrinkFor(o));
		RefreshAll();
	}

	private void Build()
	{
		_root = new Control { Theme = UiTheme.Get() };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);
		ColorRect dim = new ColorRect { Color = new Color(0f, 0.02f, 0.04f, 0.7f), MouseFilter = Control.MouseFilterEnum.Stop };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(dim);
		CenterContainer center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(center);
		PanelContainer frame = new PanelContainer { CustomMinimumSize = new Vector2(1110f, 610f) };
		frame.AddThemeStyleboxOverride("panel", Holo.Panel(18));
		center.AddChild(frame);
		VBoxContainer column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		frame.AddChild(column);

		HBoxContainer header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);
		column.AddChild(header);
		Label title = Holo.Caption("Am Tresen", 14);
		title.AddThemeColorOverride("font_color", Holo.Accent);
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(title);
		_tabMix = Holo.Button("Mixen");
		_tabMix.Pressed += ShowMix;
		header.AddChild(_tabMix);
		_tabBook = Holo.Button("Rezeptbuch");
		_tabBook.Pressed += ShowBook;
		header.AddChild(_tabBook);
		Button close = Holo.Button("Schließen  (Esc)");
		close.Pressed += Close;
		header.AddChild(close);

		// Mix view.
		HBoxContainer mix = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		mix.AddThemeConstantOverride("separation", 16);
		_mixView = mix;
		column.AddChild(mix);

		VBoxContainer left = new VBoxContainer { CustomMinimumSize = new Vector2(250f, 0f) };
		left.AddThemeConstantOverride("separation", 6);
		mix.AddChild(left);
		left.AddChild(Holo.Caption("Bestellungen", 11));
		_orders = new VBoxContainer();
		_orders.AddThemeConstantOverride("separation", 4);
		left.AddChild(_orders);
		left.AddChild(new HSeparator());
		_orderCard = new VBoxContainer();
		_orderCard.AddThemeConstantOverride("separation", 6);
		left.AddChild(_orderCard);

		VBoxContainer middle = new VBoxContainer();
		middle.AddThemeConstantOverride("separation", 6);
		mix.AddChild(middle);
		middle.AddChild(Holo.Caption("Regal  ·  W = Wärme, S = Süße, St = Stärke", 11));
		_shelf = new GridContainer { Columns = 3 };
		_shelf.AddThemeConstantOverride("h_separation", 6);
		_shelf.AddThemeConstantOverride("v_separation", 6);
		middle.AddChild(_shelf);

		VBoxContainer right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		right.AddThemeConstantOverride("separation", 8);
		mix.AddChild(right);
		right.AddChild(Holo.Caption("Shaker", 11));
		_shakerSlots = new HBoxContainer();
		_shakerSlots.AddThemeConstantOverride("separation", 6);
		right.AddChild(_shakerSlots);
		right.AddChild(Holo.Caption("Geschmack", 11));
		_profile = new VBoxContainer();
		_profile.AddThemeConstantOverride("separation", 4);
		right.AddChild(_profile);
		right.AddChild(Holo.Caption("Zubereitung", 11));
		_methods = new HBoxContainer();
		_methods.AddThemeConstantOverride("separation", 4);
		right.AddChild(_methods);
		_result = Holo.Text_("", 18, Holo.Text);
		_result.AddThemeFontOverride("font", UiTheme.HeadingFont);
		right.AddChild(_result);
		HBoxContainer actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 8);
		right.AddChild(actions);
		Button empty = Holo.Button("Ausleeren");
		empty.Pressed += () =>
		{
			_mixer.Clear();
			RefreshShelf();
			RefreshShaker();
		};
		actions.AddChild(empty);
		_finish = Holo.Button("Fertig – aufs Tablett", primary: true);
		_finish.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_finish.Pressed += Finish;
		actions.AddChild(_finish);
		_message = Holo.Text_("", 13, Holo.Accent);
		right.AddChild(_message);

		// Recipe book view.
		ScrollContainer scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, Visible = false, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		_bookView = scroll;
		column.AddChild(scroll);
		_book = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_book.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(_book);
	}

	private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

	private static string Initials(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "?";
		}
		string[] parts = name.Split(new[] { '-', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
		return parts.Length >= 2 ? (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant() : name.Substring(0, System.Math.Min(2, name.Length));
	}

	private static string ShortName(string name) => name ?? "";
}
