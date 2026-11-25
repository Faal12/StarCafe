using Godot;

/// <summary>Helpers to build placeholder decks in code: floor, walls, solid blocks with labels.</summary>
public static class DeckBuilder
{
	private const string FloorTexture = "res://Assets/Ship/floor_hall.png";

	public static void Floor(Node2D parent, Rect2 rect, Color tint)
	{
		Texture2D texture = ResourceLoader.Exists(FloorTexture) ? GD.Load<Texture2D>(FloorTexture) : null;
		if (texture != null)
		{
			Sprite2D floor = new Sprite2D
			{
				Texture = texture,
				Centered = false,
				Position = rect.Position,
				RegionEnabled = true,
				RegionRect = new Rect2(Vector2.Zero, rect.Size),
				TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled,
				Modulate = tint,
				ZIndex = -4000,
				ZAsRelative = false
			};
			parent.AddChild(floor);
		}
		else
		{
			parent.AddChild(new ColorRect { Position = rect.Position, Size = rect.Size, Color = tint.Darkened(0.7f), ZIndex = -4000, ZAsRelative = false, MouseFilter = Control.MouseFilterEnum.Ignore });
		}
	}

	/// <summary>Invisible-ish wall with collision and a dark steel look.</summary>
	public static void Wall(Node2D parent, Rect2 rect, Color color)
	{
		StaticBody2D body = new StaticBody2D { Position = rect.GetCenter() };
		body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } });
		Panel look = new Panel { Position = -rect.Size / 2f, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
		StyleBoxFlat style = new StyleBoxFlat { BgColor = color, BorderColor = color.Lightened(0.15f) };
		style.SetBorderWidthAll(2);
		look.AddThemeStyleboxOverride("panel", style);
		body.AddChild(look);
		body.ZIndex = DeckManager.SortZ(rect.End.Y);
		body.ZAsRelative = false;
		parent.AddChild(body);
	}

	/// <summary>Placeholder object: rounded block with a label, optionally solid.</summary>
	public static Node2D Block(Node2D parent, Rect2 rect, Color color, string label, bool solid = true, int labelSize = 12)
	{
		Node2D root = solid ? new StaticBody2D() : new Node2D();
		root.Position = rect.GetCenter();
		if (solid)
		{
			root.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } });
		}
		Panel look = new Panel { Position = -rect.Size / 2f, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
		StyleBoxFlat style = new StyleBoxFlat { BgColor = color, BorderColor = color.Lightened(0.35f), ShadowColor = new Color(0f, 0f, 0f, 0.35f), ShadowSize = 6, ShadowOffset = new Vector2(0f, 4f) };
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		look.AddThemeStyleboxOverride("panel", style);
		root.AddChild(look);
		if (!string.IsNullOrEmpty(label))
		{
			Label text = new Label
			{
				Text = label,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Position = -rect.Size / 2f,
				Size = rect.Size,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			text.AddThemeFontSizeOverride("font_size", labelSize);
			text.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.85f));
			text.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.7f));
			text.AddThemeConstantOverride("outline_size", 3);
			root.AddChild(text);
		}
		root.ZIndex = DeckManager.SortZ(rect.End.Y);
		root.ZAsRelative = false;
		parent.AddChild(root);
		return root;
	}

	public static Interactable Use(Node2D parent, Vector2 position, string prompt, float radius = 70f, Vector2? promptOffset = null)
	{
		Interactable interactable = new Interactable { Position = position, Prompt = prompt, Radius = radius, PromptOffset = promptOffset ?? new Vector2(0f, -80f) };
		parent.AddChild(interactable);
		return interactable;
	}

	/// <summary>Elevator door placeholder plus the "[E] Aufzug" interaction.</summary>
	public static Interactable Elevator(Node2D parent, Rect2 door, Vector2 usePosition)
	{
		Block(parent, door, new Color(0.25f, 0.3f, 0.36f), "AUFZUG", solid: true, labelSize: 11);
		Interactable use = Use(parent, usePosition, "[E] Aufzug", 80f);
		use.Used += DeckManager.OpenElevator;
		return use;
	}

	public static PointLight2D Glow(Node2D parent, Vector2 position, Color color, float scale = 1f, float energy = 1f)
	{
		Gradient gradient = new Gradient();
		gradient.Offsets = new float[] { 0f, 1f };
		gradient.Colors = new Color[] { Colors.White, new Color(1f, 1f, 1f, 0f) };
		PointLight2D light = new PointLight2D
		{
			Position = position,
			Color = color,
			Energy = energy,
			TextureScale = scale,
			Texture = new GradientTexture2D { Gradient = gradient, Width = 128, Height = 128, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f) }
		};
		parent.AddChild(light);
		return light;
	}
}
