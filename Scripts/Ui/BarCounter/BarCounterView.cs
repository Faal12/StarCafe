using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// "Am Tresen" – the bar as its own view (Papers-Please style): the camera switches to the counter.
/// Bottles stand on the shelves, the shaker, the ice bucket, the bar spoon, the order slips and the recipe
/// book lie on the counter – everything has physics and is moved with the mouse.
///
/// How to mix (all by hand):
///   pour   – hold a bottle over the shaker: it tilts and pours (one portion per moment of pouring, max 4)
///   shake  – grab the shaker and shake it up and down
///   stir   – move the spoon back and forth inside the shaker
///   heat   – put the shaker on the burner
///   ice    – hold the ice bucket over the shaker
///   serve  – pour the shaker into the glass on the right (drink goes on the tray for the selected order slip)
///   empty  – pour the shaker out over the sink
/// Click an order slip to choose the guest, click the book to read recipes. Esc leaves the counter.
///
/// The game is NOT paused here: guests keep waiting while you mix. Logic lives in <see cref="BarService"/>
/// and <see cref="DrinkMixer"/>; this class only shows and handles input (front end).
/// Physics runs in its own SubViewport, so nothing here collides with the tavern.
/// </summary>
public partial class BarCounterView : CanvasLayer
{
	public static readonly float[] ShelfTops = { 200f, 360f };
	public const float ShelfLeft = 40f;
	public const float ShelfRight = 740f;
	public const float CounterTop = 540f;
	public static readonly Rect2 SinkRect = new Rect2(950f, 500f, 90f, 40f);
	public static readonly Vector2 BurnerPos = new Vector2(720f, CounterTop);
	public static readonly Vector2 GlassPos = new Vector2(1090f, CounterTop - 36f);

	private const float PourTime = 0.8f;
	private const float HeatTime = 1.6f;
	private const int ShakeFlips = 6;
	private const int StirFlips = 5;

	private static BarCounterView _instance;

	public static bool IsOpen { get => UiBus.IsOpen(UiWindow.Bar); private set => UiBus.SetOpen(UiWindow.Bar, value); }

	public static void Open() => _instance?.Present();

	/// <summary>The drink being mixed (stays in the shaker when you leave the counter).</summary>
	public DrinkMixer Mixer { get; } = new DrinkMixer();

	public GuestOrder SelectedOrder { get; private set; }

	/// <summary>Shaker sits on the burner and heats up (for the backdrop flames).</summary>
	public bool Heating { get; private set; }

	/// <summary>Colour in the serving glass right after serving.</summary>
	public Color GlassFill { get; private set; } = new Color(0f, 0f, 0f, 0f);

	private Control _root;
	private SubViewportContainer _container;
	private SubViewport _world;
	private Node2D _stage;
	private PourFx _fx;
	private ShakerProp _shaker;

	private Label _orderLabel;
	private Label _mixLabel;
	private Label _methodLabel;
	private Label _tasteLabel;
	private Label _resultLabel;
	private Label _messageLabel;
	private Control _book;
	private Label _bookLeft;
	private Label _bookRight;
	private Label _bookPage;
	private int _bookIndex;

	private BarProp _held;
	private Vector2 _mouse;
	private Vector2 _grabOffset;
	private Vector2 _pressPos;
	private ulong _pressMs;
	private bool _pickPending;
	private int _z = 10;

	private float _pourTimer;
	private float _heatTimer;
	private bool _heatedThisVisit;
	private int _flips;
	private float _lastSign;
	private ulong _lastFlipMs;
	private string _blockedMessage;

	public override void _Ready()
	{
		_instance = this;
		Layer = 28;
		ProcessMode = ProcessModeEnum.Always;
		BarLibrary.EnsureLoaded();
		BuildOverlay();
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
			SetPlayerFree(true);
		}
	}

	// ───────────── open / close ─────────────

	private void Present()
	{
		if (IsOpen)
		{
			return;
		}
		IsOpen = true;
		SelectedOrder = BarService.NextOrder();
		BuildWorld();
		_root.Visible = true;
		_book.Visible = false;
		SetPlayerFree(false);
		_messageLabel.Text = "Zieh die Flaschen mit der Maus. Esc: zurück in den Schankraum.";
		_root.Modulate = new Color(1f, 1f, 1f, 0f);
		_container.Position = new Vector2(0f, 60f);
		Tween tween = CreateTween().SetParallel();
		tween.TweenProperty(_root, "modulate:a", 1f, 0.25);
		tween.TweenProperty(_container, "position", Vector2.Zero, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	private void Close()
	{
		if (!IsOpen)
		{
			return;
		}
		IsOpen = false;
		_held = null;
		Heating = false;
		_root.Visible = false;
		_world?.QueueFree();
		_world = null;
		SetPlayerFree(true);
	}

	private void SetPlayerFree(bool free)
	{
		if (GetTree()?.GetFirstNodeInGroup("Player") is Player player)
		{
			player.CanMove = free && !UiBus.IsOpen(UiWindow.Dialogue);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsOpen || !@event.IsActionPressed("ui_cancel"))
		{
			return;
		}
		if (_book.Visible)
		{
			_book.Visible = false;
		}
		else
		{
			Close();
		}
		GetViewport().SetInputAsHandled();
	}

	// ───────────── world with physics ─────────────

	private void BuildWorld()
	{
		_world?.QueueFree();
		_world = new SubViewport { Size = new Vector2I(1152, 648), TransparentBg = true, HandleInputLocally = false, PhysicsObjectPicking = false };
		_container.AddChild(_world);
		_stage = new Node2D();
		_world.AddChild(_stage);
		_stage.AddChild(new BarBackdrop { View = this, ZIndex = -10 });

		// shelves, counter, walls
		foreach (float top in ShelfTops)
		{
			Solid(new Vector2((ShelfLeft + ShelfRight) / 2f, top + 7f), new Vector2(ShelfRight - ShelfLeft, 14f));
		}
		Solid(new Vector2(576f, CounterTop + 30f), new Vector2(1152f, 60f));
		Solid(new Vector2(-10f, 324f), new Vector2(20f, 1400f));
		Solid(new Vector2(1162f, 324f), new Vector2(20f, 1400f));
		Solid(new Vector2(576f, -10f), new Vector2(1152f, 20f));
		Solid(new Vector2(BurnerPos.X, CounterTop - 5f), new Vector2(72f, 10f));
		Solid(GlassPos, new Vector2(44f, 72f));

		// bottles on the shelves (6 per shelf)
		List<IngredientDef> ingredients = BarLibrary.Ingredients.ToList();
		for (int i = 0; i < ingredients.Count; i++)
		{
			int shelf = Mathf.Min(i / 6, ShelfTops.Length - 1);
			float x = 90f + (i % 6) * 120f;
			Add(BottleProp.Create(ingredients[i]), new Vector2(x, ShelfTops[shelf] - 44f));
		}

		// things on the counter
		_shaker = ShakerProp.Create();
		Add(_shaker, new Vector2(860f, CounterTop - 54f));
		Add(IceBucketProp.Create(), new Vector2(570f, CounterTop - 28f));
		Add(SpoonProp.Create(), new Vector2(640f, CounterTop - 47f));
		Add(RecipeBookProp.Create(), new Vector2(410f, CounterTop - 18f));
		List<GuestOrder> orders = OrderBook.Open.ToList();
		orders.Insert(0, null);
		for (int i = 0; i < orders.Count; i++)
		{
			Add(OrderSlipProp.Create(orders[i]), new Vector2(95f + i * 32f, CounterTop - 44f - i * 30f));
		}
		_fx = new PourFx { ZIndex = 50 };
		_stage.AddChild(_fx);
	}

	private void Solid(Vector2 center, Vector2 size)
	{
		StaticBody2D body = new StaticBody2D { Position = center };
		body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size } });
		body.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.9f, Bounce = 0.05f };
		_stage.AddChild(body);
	}

	private void Add(BarProp prop, Vector2 at)
	{
		prop.Setup(this);
		prop.Position = at;
		prop.ZIndex = _z++;
		_stage.AddChild(prop);
	}

	// ───────────── mouse ─────────────

	private void OnContainerInput(InputEvent @event)
	{
		if (!IsOpen || _book.Visible)
		{
			return;
		}
		if (@event is InputEventMouseMotion motion)
		{
			_mouse = motion.Position;
		}
		else if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left)
		{
			_mouse = button.Position;
			if (button.Pressed)
			{
				_pickPending = true;
				_pressPos = button.Position;
				_pressMs = Time.GetTicksMsec();
			}
			else
			{
				Release();
			}
			_container.AcceptEvent();
		}
	}

	private void Pick()
	{
		_pickPending = false;
		if (_world == null)
		{
			return;
		}
		PhysicsPointQueryParameters2D query = new PhysicsPointQueryParameters2D { Position = _mouse, CollideWithBodies = true, CollideWithAreas = false };
		BarProp best = null;
		foreach (Godot.Collections.Dictionary hit in _world.World2D.DirectSpaceState.IntersectPoint(query, 16))
		{
			if (hit["collider"].AsGodotObject() is BarProp prop && (best == null || prop.ZIndex > best.ZIndex))
			{
				best = prop;
			}
		}
		if (best == null)
		{
			return;
		}
		_held = best;
		_held.Held = true;
		_held.ZIndex = _z++;
		_held.Sleeping = false;
		_grabOffset = _held.GlobalPosition - _mouse;
		_flips = 0;
		_pourTimer = 0f;
		_blockedMessage = null;
	}

	private void Release()
	{
		if (_held == null)
		{
			_pickPending = false;
			return;
		}
		BarProp prop = _held;
		_held.Held = false;
		_held.TiltTarget = 0f;
		_held = null;
		bool click = _pressPos.DistanceTo(_mouse) < 8f && Time.GetTicksMsec() - _pressMs < 350;
		if (click)
		{
			prop.OnClick();
		}
	}

	// ───────────── physics and mixing ─────────────

	public override void _PhysicsProcess(double delta)
	{
		if (!IsOpen || _world == null)
		{
			return;
		}
		float dt = (float)delta;
		if (_pickPending)
		{
			Pick();
		}
		if (_held != null && GodotObject.IsInstanceValid(_held))
		{
			Vector2 target = _mouse + _grabOffset;
			_held.LinearVelocity = ((target - _held.GlobalPosition) * 18f).LimitLength(2400f);
			_held.TiltTarget = 0f;
			switch (_held)
			{
			case BottleProp bottle:
				HoldPourer(bottle, dt);
				break;
			case IceBucketProp ice:
				HoldPourer(ice, dt);
				break;
			case SpoonProp spoon:
				HoldSpoon(spoon);
				break;
			case ShakerProp shaker:
				HoldShaker(shaker, dt);
				break;
			}
			float error = Mathf.AngleDifference(_held.Rotation, _held.TiltTarget);
			_held.AngularVelocity = error * 12f;
		}
		UpdateBurner(dt);
	}

	private bool ShakerReady => _shaker != null && GodotObject.IsInstanceValid(_shaker) && !_shaker.Held
		&& Mathf.Abs(Mathf.AngleDifference(_shaker.Rotation, 0f)) < 0.6f;

	/// <summary>Bottle or ice bucket over the shaker: tilt and pour.</summary>
	private void HoldPourer(BarProp held, float dt)
	{
		if (!ShakerReady)
		{
			_pourTimer = 0f;
			return;
		}
		Vector2 mouth = _shaker.Mouth;
		float dx = held.GlobalPosition.X - mouth.X;
		bool inZone = Mathf.Abs(dx) < 95f && held.GlobalPosition.Y < mouth.Y - 10f && held.GlobalPosition.Y > mouth.Y - 270f;
		if (!inZone)
		{
			_pourTimer = 0f;
			_blockedMessage = null;
			return;
		}
		held.TiltTarget = dx < 0f ? 1.9f : -1.9f;
		bool tilted = Mathf.Abs(Mathf.AngleDifference(held.Rotation, held.TiltTarget)) < 0.5f;
		if (!tilted)
		{
			return;
		}
		if (held is BottleProp bottle)
		{
			if (_blockedMessage != null)
			{
				return;
			}
			_fx.Spawn(bottle.Spout, mouth, new Color(bottle.Liquid, 0.9f));
			_pourTimer += dt;
			if (_pourTimer >= PourTime)
			{
				_pourTimer = 0f;
				if (BarService.TryAdd(Mixer, bottle.Ingredient.Id, out string reason))
				{
					FloatText("+ " + bottle.Ingredient.Name, mouth + new Vector2(0f, -30f), bottle.Liquid.Lightened(0.3f));
					_messageLabel.Text = bottle.Ingredient.Description;
				}
				else
				{
					_blockedMessage = reason;
					_messageLabel.Text = reason;
				}
			}
		}
		else
		{
			_fx.Spawn(held.Spout, mouth, new Color(0.85f, 0.95f, 1f, 0.95f));
			_pourTimer += dt;
			if (_pourTimer >= PourTime)
			{
				_pourTimer = -10f;
				SetMethod("auf_eis", mouth);
			}
		}
	}

	/// <summary>Spoon inside the shaker, moved back and forth: stirring.</summary>
	private void HoldSpoon(SpoonProp spoon)
	{
		if (!ShakerReady)
		{
			return;
		}
		Vector2 mouth = _shaker.Mouth;
		bool inZone = Mathf.Abs(spoon.GlobalPosition.X - mouth.X) < 45f && spoon.GlobalPosition.Y > mouth.Y - 110f && spoon.GlobalPosition.Y < mouth.Y + 40f;
		if (!inZone)
		{
			_flips = 0;
			return;
		}
		if (CountFlip(spoon.LinearVelocity.X, 120f) >= StirFlips)
		{
			_flips = 0;
			SetMethod("ruehren", mouth);
		}
	}

	/// <summary>Shaker in hand: shake it, pour it into the glass, or out over the sink.</summary>
	private void HoldShaker(ShakerProp shaker, float dt)
	{
		Vector2 pos = shaker.GlobalPosition;
		float dxGlass = pos.X - GlassPos.X;
		// The zones must not overlap: sink up to x ≈ 1050, glass to the right of it.
		float split = SinkRect.End.X + 10f;
		bool overGlass = pos.X > split && pos.Y < GlassPos.Y - 40f && pos.Y > GlassPos.Y - 320f;
		bool overSink = pos.X > SinkRect.Position.X - 20f && pos.X <= split && pos.Y < CounterTop - 70f;
		if (overGlass || overSink)
		{
			shaker.TiltTarget = overGlass ? (dxGlass < 0f ? 1.9f : -1.9f) : 2.3f;
			bool tilted = Mathf.Abs(Mathf.AngleDifference(shaker.Rotation, shaker.TiltTarget)) < 0.5f;
			if (!tilted || Mixer.IsEmpty)
			{
				if (tilted && _blockedMessage == null)
				{
					_blockedMessage = "Der Shaker ist leer.";
					_messageLabel.Text = _blockedMessage;
				}
				return;
			}
			if (overGlass && string.IsNullOrEmpty(Mixer.Method))
			{
				if (_blockedMessage == null)
				{
					_blockedMessage = "Erst zubereiten: schütteln, rühren, Brenner oder Eis.";
					_messageLabel.Text = _blockedMessage;
				}
				return;
			}
			_fx.Spawn(shaker.Spout, overGlass ? GlassPos : new Vector2(pos.X, CounterTop), MixColor());
			_pourTimer += dt;
			if (_pourTimer >= PourTime + 0.2f)
			{
				_pourTimer = 0f;
				if (overGlass)
				{
					Serve();
				}
				else
				{
					Mixer.Clear();
					_messageLabel.Text = "Ausgegossen. Der Shaker ist wieder leer.";
				}
			}
			return;
		}
		_pourTimer = 0f;
		_blockedMessage = null;
		if (CountFlip(shaker.LinearVelocity.Y, 450f) >= ShakeFlips)
		{
			_flips = 0;
			SetMethod("schuetteln", shaker.GlobalPosition + new Vector2(0f, -80f));
		}
	}

	/// <summary>Counts direction changes of a fast back-and-forth movement.</summary>
	private int CountFlip(float velocity, float minSpeed)
	{
		ulong now = Time.GetTicksMsec();
		if (now - _lastFlipMs > 700)
		{
			_flips = 0;
		}
		if (Mathf.Abs(velocity) < minSpeed)
		{
			return _flips;
		}
		float sign = Mathf.Sign(velocity);
		if (sign != _lastSign)
		{
			_lastSign = sign;
			_flips++;
			_lastFlipMs = now;
		}
		return _flips;
	}

	private void UpdateBurner(float dt)
	{
		bool onBurner = _shaker != null && GodotObject.IsInstanceValid(_shaker) && !_shaker.Held
			&& Mathf.Abs(_shaker.GlobalPosition.X - BurnerPos.X) < 40f
			&& _shaker.GlobalPosition.Y > CounterTop - 10f - _shaker.Size.Y / 2f - 14f;
		Heating = onBurner;
		if (!onBurner)
		{
			_heatTimer = 0f;
			_heatedThisVisit = false;
			return;
		}
		_heatTimer += dt;
		if (_heatTimer >= HeatTime && !_heatedThisVisit)
		{
			_heatedThisVisit = true;
			SetMethod("erhitzen", _shaker.GlobalPosition + new Vector2(0f, -80f));
		}
	}

	private void SetMethod(string method, Vector2 at)
	{
		if (Mixer.IsEmpty)
		{
			_messageLabel.Text = "Erst Zutaten in den Shaker gießen.";
			return;
		}
		Mixer.Method = method;
		MethodDef def = BarLibrary.Method(method);
		FloatText((def?.Name ?? method) + "!", at, Holo.Accent);
		_messageLabel.Text = def?.Description ?? "";
	}

	private void Serve()
	{
		GuestOrder order = SelectedOrder != null && OrderBook.Open.Contains(SelectedOrder) ? SelectedOrder : null;
		Color color = MixColor();
		ServeResult result = BarService.Finish(Mixer, order);
		_messageLabel.Text = result.Message;
		if (!result.Ok)
		{
			return;
		}
		GlassFill = color;
		FloatText(result.Drink.DisplayName, GlassPos + new Vector2(0f, -70f), color.Lightened(0.4f));
		UiBus.Toast(result.Message, Holo.Accent);
		CreateTween().TweenMethod(Callable.From<float>(a => GlassFill = new Color(color, a)), 0.85f, 0f, 1.6).SetDelay(1.2);
		SelectedOrder = BarService.NextOrder();
	}

	public void SelectOrder(GuestOrder order)
	{
		SelectedOrder = order;
		_messageLabel.Text = order == null ? "Der nächste Drink ist für niemanden – zum Ausprobieren." : "Der nächste Drink ist für " + order.GuestName + ".";
	}

	private Color MixColor()
	{
		if (Mixer.IsEmpty)
		{
			return new Color(0.7f, 0.8f, 0.9f, 0.6f);
		}
		Color sum = new Color(0f, 0f, 0f, 0f);
		foreach (string id in Mixer.Ingredients)
		{
			Color c = Color.FromHtml(BarLibrary.Ingredient(id)?.Color ?? "#888888");
			sum = new Color(sum.R + c.R, sum.G + c.G, sum.B + c.B, 1f);
		}
		int n = Mixer.Ingredients.Count;
		return new Color(sum.R / n, sum.G / n, sum.B / n, 0.9f);
	}

	// ───────────── overlay (holo display, book) ─────────────

	public override void _Process(double delta)
	{
		if (!IsOpen)
		{
			return;
		}
		RefreshDisplay();
	}

	private void RefreshDisplay()
	{
		if (SelectedOrder == null)
		{
			_orderLabel.Text = "FÜR NIEMANDEN\nFrei mixen. Neue Rezepte landen im Buch.";
		}
		else
		{
			bool gone = !OrderBook.Open.Contains(SelectedOrder);
			string hint = SelectedOrder.HintRevealed && !string.IsNullOrWhiteSpace(SelectedOrder.Template.Hint) ? "\nNachgefragt: " + SelectedOrder.Template.Hint : "";
			_orderLabel.Text = (SelectedOrder.GuestName + (gone ? "  (gegangen)" : "")).ToUpperInvariant() + "\n„" + SelectedOrder.Template.Text + "“" + hint;
		}
		_mixLabel.Text = Mixer.IsEmpty
			? "Shaker: leer"
			: "Shaker: " + string.Join(" + ", Mixer.Ingredients.Select(i => BarLibrary.Ingredient(i)?.Name ?? i)) + $"  ({Mixer.Ingredients.Count}/{DrinkMixer.MaxIngredients})";
		_methodLabel.Text = "Zubereitung: " + (string.IsNullOrEmpty(Mixer.Method) ? "—" : BarLibrary.Method(Mixer.Method)?.Name);
		Taste taste = Mixer.Profile;
		_tasteLabel.Text = Bar("Wärme ", taste.Waerme) + "\n" + Bar("Süße  ", taste.Suesse) + "\n" + Bar("Stärke", taste.Staerke) + (Mixer.HasAlcohol ? "\nmit Alkohol" : "");
		RecipeDef recipe = Mixer.Recipe;
		_resultLabel.Text = Mixer.IsEmpty ? "" :
			string.IsNullOrEmpty(Mixer.Method) ? "Noch nicht zubereitet." :
			recipe == null ? "Eigenkreation." :
			PlayerState.KnowsRecipe(recipe.Id) ? "✓ " + recipe.Name : "Das kommt dir bekannt vor … ein Rezept?";
	}

	private static string Bar(string name, int value)
	{
		int v = Mathf.Clamp(value, 0, DrinkMixer.AxisMax);
		return name + " " + new string('▮', v) + new string('▯', DrinkMixer.AxisMax - v) + " " + v;
	}

	public void OpenBook()
	{
		_book.Visible = true;
		_bookIndex = 0;
		ShowBookPages();
	}

	private void ShowBookPages()
	{
		List<RecipeDef> recipes = BarLibrary.Recipes;
		_bookLeft.Text = RecipePage(_bookIndex < recipes.Count ? recipes[_bookIndex] : null);
		_bookRight.Text = RecipePage(_bookIndex + 1 < recipes.Count ? recipes[_bookIndex + 1] : null);
		int known = recipes.Count(r => PlayerState.KnowsRecipe(r.Id));
		_bookPage.Text = $"Seite {_bookIndex / 2 + 1} von {(recipes.Count + 1) / 2}  ·  {known} von {recipes.Count} Rezepten entdeckt";
	}

	private static string RecipePage(RecipeDef recipe)
	{
		if (recipe == null)
		{
			return "";
		}
		if (!PlayerState.KnowsRecipe(recipe.Id))
		{
			return "???\n\nNoch nicht entdeckt.\n" + recipe.Ingredients.Count + " Zutaten.\n\nGäste verraten manchmal, was hineingehört.";
		}
		string ingredients = string.Join("\n• ", recipe.Ingredients.Select(i => BarLibrary.Ingredient(i)?.Name ?? i));
		return recipe.Name.ToUpperInvariant() + "\n\n• " + ingredients + "\n\nZubereitung: " + (BarLibrary.Method(recipe.Method)?.Name ?? recipe.Method)
			+ "\nPreis: ₡" + recipe.Price + "\n\n" + recipe.Description;
	}

	private void FloatText(string text, Vector2 at, Color color)
	{
		Label label = new Label { Text = text, Position = at + new Vector2(-100f, -10f), Size = new Vector2(200f, 24f), HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", 16);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 5);
		_root.AddChild(label);
		Tween tween = label.CreateTween().SetParallel();
		tween.TweenProperty(label, "position:y", label.Position.Y - 40f, 1.0);
		tween.TweenProperty(label, "modulate:a", 0f, 1.0).SetDelay(0.3);
		tween.Chain().TweenCallback(Callable.From(label.QueueFree));
	}

	private void BuildOverlay()
	{
		_root = new Control { Theme = UiTheme.Get(), MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		_container = new SubViewportContainer { Stretch = true, MouseFilter = Control.MouseFilterEnum.Stop };
		_container.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_container.GuiInput += OnContainerInput;
		_root.AddChild(_container);

		// title + back
		// Kept small and above the bottles (top shelf bottles start at y ≈ 110).
		HBoxContainer top = new HBoxContainer { Position = new Vector2(20f, 14f), Size = new Vector2(420f, 34f) };
		top.AddThemeConstantOverride("separation", 14);
		_root.AddChild(top);
		Button back = Holo.Button("◀ Zurück (Esc)");
		back.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
		back.Pressed += Close;
		top.AddChild(back);
		Label title = Holo.Text_("AM TRESEN", 20, Holo.Accent);
		title.AutowrapMode = TextServer.AutowrapMode.Off;
		title.CustomMinimumSize = Vector2.Zero;
		top.AddChild(title);

		// holo display (right)
		PanelContainer display = new PanelContainer { Position = new Vector2(790f, 18f), CustomMinimumSize = new Vector2(340f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		display.AddThemeStyleboxOverride("panel", Holo.Panel());
		_root.AddChild(display);
		VBoxContainer col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		col.AddThemeConstantOverride("separation", 6);
		display.AddChild(col);
		col.AddChild(Holo.Caption("BESTELLUNG"));
		_orderLabel = Wrapped(13, Holo.Text);
		col.AddChild(_orderLabel);
		col.AddChild(Holo.Caption("MISCHUNG"));
		_mixLabel = Wrapped(13, Holo.Text);
		col.AddChild(_mixLabel);
		_methodLabel = Wrapped(13, Holo.Text);
		col.AddChild(_methodLabel);
		_tasteLabel = Wrapped(13, Holo.Accent);
		_tasteLabel.AddThemeFontOverride("font", UiTheme.BodyFont);
		col.AddChild(_tasteLabel);
		_resultLabel = Wrapped(14, Holo.Strong);
		col.AddChild(_resultLabel);
		_messageLabel = Wrapped(12, Holo.TextDim);
		col.AddChild(_messageLabel);

		// how-to on the counter front
		Label help = new Label
		{
			Position = new Vector2(20f, 592f),
			Size = new Vector2(1110f, 50f),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Text = "Flasche über den Shaker halten = eingießen · Shaker schütteln · Löffel im Shaker hin und her = rühren · Shaker auf den Brenner = erhitzen · "
				+ "Eiskübel über den Shaker = auf Eis · Shaker über das Glas = servieren · über die Spüle = ausgießen · Zettel und Buch anklicken"
		};
		help.AddThemeFontSizeOverride("font_size", 12);
		help.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.75f, 0.75f));
		_root.AddChild(help);

		BuildBook();
	}

	private void BuildBook()
	{
		_book = new Control { Visible = false };
		_book.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(_book);
		ColorRect dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dim.GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true })
			{
				_book.Visible = false;
			}
		};
		_book.AddChild(dim);
		PanelContainer cover = new PanelContainer { Position = new Vector2(156f, 70f), CustomMinimumSize = new Vector2(840f, 480f) };
		cover.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.42f, 0.18f, 0.12f), new Color(0.75f, 0.55f, 0.25f), 3, 8, 16, 14));
		_book.AddChild(cover);
		VBoxContainer column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 8);
		cover.AddChild(column);
		HBoxContainer pages = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		pages.AddThemeConstantOverride("separation", 10);
		column.AddChild(pages);
		_bookLeft = Page(pages);
		_bookRight = Page(pages);
		HBoxContainer nav = new HBoxContainer();
		nav.AddThemeConstantOverride("separation", 10);
		column.AddChild(nav);
		Button prev = Holo.Button("◀");
		prev.Pressed += () => { _bookIndex = Mathf.Max(0, _bookIndex - 2); ShowBookPages(); };
		nav.AddChild(prev);
		_bookPage = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Center };
		_bookPage.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.6f));
		nav.AddChild(_bookPage);
		Button next = Holo.Button("▶");
		next.Pressed += () => { if (_bookIndex + 2 < BarLibrary.Recipes.Count) { _bookIndex += 2; ShowBookPages(); } };
		nav.AddChild(next);
		Button close = Holo.Button("Buch schließen");
		close.Pressed += () => _book.Visible = false;
		nav.AddChild(close);
	}

	private static Label Page(HBoxContainer parent)
	{
		PanelContainer paper = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 390f) };
		paper.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.95f, 0.91f, 0.8f), new Color(0.7f, 0.6f, 0.45f), 1, 4, 18, 14));
		parent.AddChild(paper);
		Label text = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
		text.AddThemeFontSizeOverride("font_size", 15);
		text.AddThemeColorOverride("font_color", new Color(0.2f, 0.15f, 0.1f));
		paper.AddChild(text);
		return text;
	}

	private static Label Wrapped(int size, Color color)
	{
		Label label = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(316f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}
}
