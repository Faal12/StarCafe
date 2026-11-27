using System.Collections.Generic;
using Godot;

/// <summary>
/// Something on the bar counter that can be picked up with the mouse and has physics (falls, tips over, bounces).
/// All props draw themselves as placeholders (<see cref="_Draw"/>) – later replace with sprites.
/// </summary>
public abstract partial class BarProp : RigidBody2D
{
	/// <summary>Size of the collision box (centered).</summary>
	public Vector2 Size { get; protected set; } = new Vector2(40f, 40f);

	/// <summary>Collision layers: 1 = shelves/counter/walls, 2 = props on the counter, 4 = the prop in your hand.</summary>
	public const uint FurnitureLayer = 1, PropLayer = 2, HeldLayer = 4;

	private bool _held;

	/// <summary>
	/// Being dragged right now (set by the view). The prop in your hand passes through everything
	/// (like papers on a desk); once you let go it collides again and falls.
	/// </summary>
	public bool Held
	{
		get => _held;
		set
		{
			_held = value;
			CollisionLayer = value ? HeldLayer : PropLayer;
			CollisionMask = value ? 0u : FurnitureLayer | PropLayer;
		}
	}

	/// <summary>Rotation the prop tries to reach while held (0 = upright, ± = tilted for pouring).</summary>
	public float TiltTarget { get; set; }

	protected BarCounterView View { get; private set; }

	public void Setup(BarCounterView view)
	{
		View = view;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Held = false;
		CollisionShape2D shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = Size } };
		AddChild(shape);
		PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.8f, Bounce = 0.12f };
		ContinuousCd = CcdMode.CastRay;
		CanSleep = true;
	}

	public override void _Process(double delta)
	{
		QueueRedraw();
	}

	/// <summary>Clicked without dragging (book opens, order slip gets selected …).</summary>
	public virtual void OnClick()
	{
	}

	/// <summary>Point where liquid comes out when tilted (top of the prop, in global coordinates).</summary>
	public Vector2 Spout => GlobalPosition + new Vector2(0f, -Size.Y / 2f).Rotated(Rotation);

	/// <summary>Top center when upright (the opening).</summary>
	public Vector2 Mouth => GlobalPosition + new Vector2(0f, -Size.Y / 2f).Rotated(Rotation);

	protected void Box(Vector2 size, Color fill, Color border, Vector2 offset, float radius = 4f)
	{
		Rect2 rect = new Rect2(offset - size / 2f, size);
		DrawRect(rect, fill);
		DrawRect(rect, border, false, 2f);
	}

	protected void Text(string text, Vector2 pos, int size, Color color, float width = -1f, HorizontalAlignment align = HorizontalAlignment.Center, int maxLines = 1)
	{
		Font font = UiTheme.BodyFont;
		if (maxLines <= 1)
		{
			DrawString(font, pos, text, align, width, size, color);
		}
		else
		{
			DrawMultilineString(font, pos, text, align, width, size, maxLines, color);
		}
	}
}

/// <summary>A bottle with one ingredient. Hold it over the shaker – it tilts and pours.</summary>
public partial class BottleProp : BarProp
{
	public IngredientDef Ingredient { get; private set; }

	public Color Liquid { get; private set; }

	public static BottleProp Create(IngredientDef ingredient)
	{
		BottleProp bottle = new BottleProp { Ingredient = ingredient };
		bottle.Size = new Vector2(34f, 86f);
		bottle.Liquid = Color.FromHtml(string.IsNullOrEmpty(ingredient.Color) ? "#888888" : ingredient.Color);
		bottle.Mass = 1f;
		return bottle;
	}

	public bool IsEmpty => View != null && BarService.Available(Ingredient, View.Mixer) <= 0;

	public override void _Draw()
	{
		Color glass = IsEmpty ? new Color(0.4f, 0.4f, 0.45f, 0.7f) : Liquid;
		// body + neck + cap
		DrawRect(new Rect2(-17f, -21f, 34f, 64f), new Color(glass, 0.92f));
		DrawRect(new Rect2(-17f, -21f, 34f, 64f), glass.Lightened(0.35f), false, 2f);
		DrawRect(new Rect2(-6f, -43f, 12f, 22f), new Color(glass.Darkened(0.15f), 0.95f));
		DrawRect(new Rect2(-7f, -46f, 14f, 6f), new Color(0.75f, 0.7f, 0.6f));
		// highlight
		DrawLine(new Vector2(-11f, -16f), new Vector2(-11f, 36f), new Color(1f, 1f, 1f, 0.25f), 3f);
		// label
		DrawRect(new Rect2(-15f, -2f, 30f, 22f), new Color(0.95f, 0.92f, 0.85f, 0.95f));
		Text(Short(Ingredient.Name), new Vector2(-15f, 13f), 9, new Color(0.15f, 0.12f, 0.1f), 30f);
		if (!string.IsNullOrEmpty(Ingredient.RequiresItem) && View != null)
		{
			int left = BarService.Available(Ingredient, View.Mixer);
			Text(left > 0 ? "×" + left : "leer", new Vector2(-17f, 56f), 10, left > 0 ? Holo.Accent : new Color(1f, 0.5f, 0.45f), 34f);
		}
	}

	private static string Short(string name)
	{
		string first = (name ?? "").Split(' ', '-')[0];
		return first.Length > 7 ? first.Substring(0, 6) + "." : first;
	}
}

/// <summary>The shaker: ingredients go in here. Shake it while holding, pour it over the glass to serve.</summary>
public partial class ShakerProp : BarProp
{
	public static ShakerProp Create()
	{
		ShakerProp shaker = new ShakerProp { Size = new Vector2(52f, 104f), Mass = 2.5f };
		return shaker;
	}

	public override void _Draw()
	{
		Color metal = new Color(0.72f, 0.76f, 0.8f);
		DrawRect(new Rect2(-26f, -52f, 52f, 104f), new Color(0.55f, 0.6f, 0.66f, 0.45f));
		// liquid layers (bottom up)
		DrinkMixer mixer = View?.Mixer;
		if (mixer != null)
		{
			float layer = 96f / DrinkMixer.MaxIngredients;
			for (int i = 0; i < mixer.Ingredients.Count; i++)
			{
				IngredientDef ingredient = BarLibrary.Ingredient(mixer.Ingredients[i]);
				Color c = ingredient != null ? Color.FromHtml(ingredient.Color) : Colors.Gray;
				DrawRect(new Rect2(-22f, 48f - layer * (i + 1), 44f, layer), new Color(c, 0.85f));
			}
		}
		DrawRect(new Rect2(-26f, -52f, 52f, 104f), metal, false, 3f);
		DrawRect(new Rect2(-28f, -56f, 56f, 8f), metal.Darkened(0.2f));
		DrawLine(new Vector2(-18f, -40f), new Vector2(-18f, 40f), new Color(1f, 1f, 1f, 0.35f), 3f);
		if (View != null && !string.IsNullOrEmpty(mixer?.Method))
		{
			Text(BarLibrary.Method(mixer.Method)?.Name ?? "", new Vector2(-40f, -62f), 11, Holo.Accent, 80f);
		}
	}
}

/// <summary>Ice bucket – hold it over the shaker: "auf Eis".</summary>
public partial class IceBucketProp : BarProp
{
	public static IceBucketProp Create() => new IceBucketProp { Size = new Vector2(66f, 54f), Mass = 1.5f };

	public override void _Draw()
	{
		DrawRect(new Rect2(-33f, -27f, 66f, 54f), new Color(0.3f, 0.45f, 0.6f, 0.9f));
		DrawRect(new Rect2(-33f, -27f, 66f, 54f), new Color(0.6f, 0.8f, 1f), false, 2f);
		for (int i = 0; i < 4; i++)
		{
			DrawRect(new Rect2(-26f + i * 13f, -34f + (i % 2) * 4f, 11f, 11f), new Color(0.85f, 0.95f, 1f, 0.9f));
		}
		Text("EIS", new Vector2(-33f, 8f), 12, Colors.White, 66f);
	}
}

/// <summary>Bar spoon – move it back and forth inside the shaker: "rühren".</summary>
public partial class SpoonProp : BarProp
{
	public static SpoonProp Create() => new SpoonProp { Size = new Vector2(12f, 92f), Mass = 0.4f };

	public override void _Draw()
	{
		DrawRect(new Rect2(-2f, -46f, 4f, 80f), new Color(0.8f, 0.82f, 0.86f));
		DrawRect(new Rect2(-6f, 32f, 12f, 14f), new Color(0.8f, 0.82f, 0.86f));
		Text("Löffel", new Vector2(-30f, -50f), 9, new Color(0.85f, 0.85f, 0.9f, 0.8f), 60f);
	}
}

/// <summary>Order slip (paper) – click to choose whom the next drink is for.</summary>
public partial class OrderSlipProp : BarProp
{
	public GuestOrder Order { get; private set; }

	public static OrderSlipProp Create(GuestOrder order)
	{
		return new OrderSlipProp { Order = order, Size = new Vector2(150f, 86f), Mass = 0.3f, LinearDamp = 2.5f, AngularDamp = 4f };
	}

	public bool Selected => View != null && View.SelectedOrder == Order;

	public override void OnClick()
	{
		View?.SelectOrder(Order);
	}

	public override void _Draw()
	{
		Color paper = Order == null ? new Color(0.82f, 0.82f, 0.78f) : new Color(0.96f, 0.93f, 0.84f);
		DrawRect(new Rect2(-75f, -43f, 150f, 86f), paper);
		DrawRect(new Rect2(-75f, -43f, 150f, 86f), Selected ? new Color(0.95f, 0.35f, 0.25f) : new Color(0.6f, 0.55f, 0.45f), false, Selected ? 3f : 1f);
		Color ink = new Color(0.18f, 0.15f, 0.12f);
		if (Order == null)
		{
			Text("Für niemanden", new Vector2(-70f, -24f), 12, ink, 140f, HorizontalAlignment.Left);
			Text("Frei mixen – neue Rezepte landen im Buch.", new Vector2(-70f, -8f), 10, ink, 140f, HorizontalAlignment.Left, 3);
		}
		else
		{
			bool ready = BarService.HasDrinkFor(Order);
			Text((ready ? "✓ " : "") + Order.GuestName, new Vector2(-70f, -24f), 12, ink, 140f, HorizontalAlignment.Left);
			Text("„" + Order.Template.Text + "“", new Vector2(-70f, -8f), 10, ink, 140f, HorizontalAlignment.Left, 3);
			if (Order.HintRevealed && !string.IsNullOrWhiteSpace(Order.Template.Hint))
			{
				Text(Order.Template.Hint, new Vector2(-70f, 36f), 9, new Color(0.1f, 0.35f, 0.5f), 140f, HorizontalAlignment.Left);
			}
		}
		if (Selected)
		{
			DrawCircle(new Vector2(62f, -32f), 6f, new Color(0.9f, 0.2f, 0.15f));
		}
	}
}

/// <summary>The recipe book lying on the counter – click to open it.</summary>
public partial class RecipeBookProp : BarProp
{
	public static RecipeBookProp Create() => new RecipeBookProp { Size = new Vector2(130f, 34f), Mass = 1.2f };

	public override void OnClick()
	{
		View?.OpenBook();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(-65f, -17f, 130f, 34f), new Color(0.42f, 0.18f, 0.12f));
		DrawRect(new Rect2(-60f, -13f, 120f, 6f), new Color(0.92f, 0.88f, 0.78f));
		DrawRect(new Rect2(-65f, -17f, 130f, 34f), new Color(0.75f, 0.55f, 0.25f), false, 2f);
		Text("REZEPTE", new Vector2(-65f, 11f), 11, new Color(0.95f, 0.8f, 0.45f), 130f);
	}
}

/// <summary>Painted background: wall, shelves, counter, sink, burner, serving glass (no physics itself).</summary>
public partial class BarBackdrop : Node2D
{
	public BarCounterView View { get; set; }

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		// wall
		DrawRect(new Rect2(0f, 0f, 1152f, 540f), new Color(0.11f, 0.09f, 0.1f));
		for (int x = 0; x < 1152; x += 96)
		{
			DrawLine(new Vector2(x, 0f), new Vector2(x, 540f), new Color(1f, 1f, 1f, 0.025f), 2f);
		}
		// shelves
		foreach (float y in BarCounterView.ShelfTops)
		{
			DrawRect(new Rect2(BarCounterView.ShelfLeft, y, BarCounterView.ShelfRight - BarCounterView.ShelfLeft, 14f), new Color(0.45f, 0.29f, 0.17f));
			DrawRect(new Rect2(BarCounterView.ShelfLeft, y + 14f, BarCounterView.ShelfRight - BarCounterView.ShelfLeft, 6f), new Color(0f, 0f, 0f, 0.35f));
			DrawLine(new Vector2(BarCounterView.ShelfLeft, y), new Vector2(BarCounterView.ShelfRight, y), new Color(0.95f, 0.65f, 0.35f, 0.35f), 2f);
		}
		// counter
		DrawRect(new Rect2(0f, BarCounterView.CounterTop, 1152f, 648f - BarCounterView.CounterTop), new Color(0.36f, 0.22f, 0.13f));
		DrawRect(new Rect2(0f, BarCounterView.CounterTop, 1152f, 8f), new Color(0.58f, 0.38f, 0.22f));
		DrawLine(new Vector2(0f, BarCounterView.CounterTop), new Vector2(1152f, BarCounterView.CounterTop), new Color(1f, 0.8f, 0.5f, 0.4f), 2f);
		// sink
		Rect2 sink = BarCounterView.SinkRect;
		DrawRect(new Rect2(sink.Position.X, BarCounterView.CounterTop, sink.Size.X, 18f), new Color(0.2f, 0.24f, 0.28f));
		DrawString(UiTheme.BodyFont, new Vector2(sink.Position.X, BarCounterView.CounterTop + 36f), "SPÜLE", HorizontalAlignment.Center, sink.Size.X, 11, new Color(0.8f, 0.85f, 0.9f, 0.7f));
		// burner
		Vector2 burner = BarCounterView.BurnerPos;
		DrawRect(new Rect2(burner.X - 36f, BarCounterView.CounterTop - 10f, 72f, 10f), new Color(0.25f, 0.25f, 0.28f));
		bool heating = View != null && View.Heating;
		for (int i = 0; i < 5; i++)
		{
			float h = heating ? 14f + 6f * Mathf.Sin(Time.GetTicksMsec() / 60f + i) : 5f;
			DrawRect(new Rect2(burner.X - 26f + i * 11f, BarCounterView.CounterTop - 10f - h, 7f, h), heating ? new Color(1f, 0.55f, 0.15f, 0.9f) : new Color(0.3f, 0.55f, 1f, 0.8f));
		}
		DrawString(UiTheme.BodyFont, new Vector2(burner.X - 50f, BarCounterView.CounterTop + 36f), "BRENNER", HorizontalAlignment.Center, 100f, 11, new Color(0.8f, 0.85f, 0.9f, 0.7f));
		// serving glass on its coaster
		Vector2 glass = BarCounterView.GlassPos;
		Color fill = View?.GlassFill ?? new Color(0f, 0f, 0f, 0f);
		DrawRect(new Rect2(glass.X - 34f, BarCounterView.CounterTop - 4f, 68f, 4f), new Color(0.15f, 0.4f, 0.45f));
		if (fill.A > 0f)
		{
			DrawRect(new Rect2(glass.X - 20f, glass.Y - 10f, 40f, 46f), fill);
		}
		DrawRect(new Rect2(glass.X - 22f, glass.Y - 36f, 44f, 72f), new Color(0.85f, 0.95f, 1f, 0.8f), false, 2f);
		DrawString(UiTheme.BodyFont, new Vector2(glass.X - 60f, BarCounterView.CounterTop + 36f), "SERVIEREN", HorizontalAlignment.Center, 120f, 11, new Color(0.8f, 0.85f, 0.9f, 0.7f));
	}
}

/// <summary>Drops of liquid while pouring.</summary>
public partial class PourFx : Node2D
{
	private readonly List<(Vector2 Pos, Vector2 Vel, Color Color, float Life)> _drops = new();

	public void Spawn(Vector2 from, Vector2 to, Color color)
	{
		Vector2 dir = (to - from);
		for (int i = 0; i < 3; i++)
		{
			Vector2 vel = new Vector2(dir.X * 2.2f + (float)GD.RandRange(-20, 20), (float)GD.RandRange(-30, 30));
			_drops.Add((from, vel, color, 0.9f));
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		for (int i = _drops.Count - 1; i >= 0; i--)
		{
			var d = _drops[i];
			d.Vel += new Vector2(0f, 900f * dt);
			d.Pos += d.Vel * dt;
			d.Life -= dt;
			if (d.Life <= 0f || d.Pos.Y > BarCounterView.CounterTop)
			{
				_drops.RemoveAt(i);
			}
			else
			{
				_drops[i] = d;
			}
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (var d in _drops)
		{
			DrawCircle(d.Pos, 3.5f, d.Color);
		}
	}
}
