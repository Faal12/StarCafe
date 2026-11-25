using Godot;

/// <summary>
/// Minimal futuristic "holo glass" look for inventory, bar and order UI:
/// dark translucent glass, thin cyan lines, soft glow, small uppercase captions.
/// </summary>
public static class Holo
{
	public static readonly Color Glass = new Color(0.03f, 0.06f, 0.09f, 0.82f);

	public static readonly Color GlassLight = new Color(0.06f, 0.11f, 0.15f, 0.88f);

	public static readonly Color Line = new Color(0.35f, 0.85f, 0.95f, 0.55f);

	public static readonly Color LineStrong = new Color(0.45f, 0.95f, 1f, 1f);

	public static readonly Color Text = new Color(0.86f, 0.95f, 0.98f);

	public static readonly Color TextDim = new Color(0.55f, 0.68f, 0.74f);

	public static readonly Color Accent = new Color(0.45f, 0.95f, 1f);

	public static readonly Color Warm = new Color(0.95f, 0.55f, 0.3f);

	public static readonly Color Sweet = new Color(0.85f, 0.5f, 0.95f);

	public static readonly Color Strong = new Color(0.95f, 0.85f, 0.35f);

	public static StyleBoxFlat Panel(float glow = 10f)
	{
		StyleBoxFlat box = new StyleBoxFlat
		{
			BgColor = Glass,
			BorderColor = Line,
			ShadowColor = new Color(0.3f, 0.9f, 1f, 0.12f),
			ShadowSize = (int)glow
		};
		box.SetBorderWidthAll(1);
		box.SetCornerRadiusAll(6);
		box.SetContentMarginAll(14);
		return box;
	}

	public static StyleBoxFlat Slot(bool selected, bool hover = false)
	{
		StyleBoxFlat box = new StyleBoxFlat
		{
			BgColor = selected ? new Color(0.08f, 0.2f, 0.26f, 0.9f) : (hover ? GlassLight : new Color(0.03f, 0.06f, 0.09f, 0.7f)),
			BorderColor = selected ? LineStrong : new Color(0.35f, 0.85f, 0.95f, hover ? 0.6f : 0.3f),
			ShadowColor = new Color(0.3f, 0.9f, 1f, selected ? 0.35f : 0f),
			ShadowSize = selected ? 8 : 0
		};
		box.SetBorderWidthAll(selected ? 2 : 1);
		box.SetCornerRadiusAll(5);
		box.SetContentMarginAll(4);
		return box;
	}

	public static Label Caption(string text, int size = 11)
	{
		Label label = new Label { Text = text.ToUpperInvariant() };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", TextDim);
		label.AddThemeConstantOverride("line_spacing", 0);
		return label;
	}

	public static Label Text_(string text, int size = 14, Color? color = null)
	{
		Label label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color ?? Text);
		return label;
	}

	/// <summary>Flat holo button.</summary>
	public static Button Button(string text, bool primary = false)
	{
		Button button = new Button { Text = text, FocusMode = Control.FocusModeEnum.All };
		StyleBoxFlat normal = Slot(false);
		StyleBoxFlat hover = Slot(false, hover: true);
		StyleBoxFlat pressed = Slot(true);
		if (primary)
		{
			normal = Slot(true);
			normal.BgColor = new Color(0.1f, 0.3f, 0.36f, 0.95f);
			hover = Slot(true);
			hover.BgColor = new Color(0.14f, 0.38f, 0.44f, 1f);
		}
		foreach (StyleBoxFlat s in new[] { normal, hover, pressed })
		{
			s.SetContentMarginAll(8);
			s.ContentMarginLeft = 12;
			s.ContentMarginRight = 12;
		}
		button.AddThemeStyleboxOverride("normal", normal);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", pressed);
		button.AddThemeStyleboxOverride("focus", Slot(false, hover: true));
		StyleBoxFlat disabled = Slot(false);
		disabled.BgColor = new Color(0.03f, 0.05f, 0.07f, 0.5f);
		disabled.BorderColor = new Color(0.3f, 0.4f, 0.45f, 0.2f);
		disabled.SetContentMarginAll(8);
		button.AddThemeStyleboxOverride("disabled", disabled);
		button.AddThemeColorOverride("font_color", Text);
		button.AddThemeColorOverride("font_hover_color", LineStrong);
		button.AddThemeColorOverride("font_disabled_color", new Color(0.4f, 0.48f, 0.52f));
		button.AddThemeFontSizeOverride("font_size", 14);
		return button;
	}

	/// <summary>Placeholder icon: rounded tile in the item's colour with 1–2 letters.</summary>
	public static Control Icon(string colorHex, string letters, float size)
	{
		Color color = Color.HtmlIsValid(colorHex ?? "") ? Color.FromHtml(colorHex) : Accent;
		PanelContainer tile = new PanelContainer { CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore };
		StyleBoxFlat box = new StyleBoxFlat
		{
			BgColor = new Color(color.Darkened(0.35f), 0.9f),
			BorderColor = color.Lightened(0.25f)
		};
		box.SetBorderWidthAll(1);
		box.SetCornerRadiusAll((int)(size * 0.28f));
		tile.AddThemeStyleboxOverride("panel", box);
		Label label = new Label
		{
			Text = letters ?? "?",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", (int)(size * 0.36f));
		label.AddThemeColorOverride("font_color", Colors.White);
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.6f));
		label.AddThemeConstantOverride("outline_size", 3);
		tile.AddChild(label);
		return tile;
	}

	/// <summary>Icon for an item stack (drinks use their recipe's initials).</summary>
	public static Control ItemIcon(ItemStack stack, float size)
	{
		ItemDef def = stack.Def;
		if (def != null && !string.IsNullOrWhiteSpace(def.Texture) && ResourceLoader.Exists(def.Texture))
		{
			return new TextureRect
			{
				Texture = GD.Load<Texture2D>(def.Texture),
				CustomMinimumSize = new Vector2(size, size),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
		}
		string letters = def?.Icon ?? "?";
		if (stack.ItemId == "getraenk")
		{
			string name = stack.DisplayName;
			letters = name.Length >= 2 ? name.Substring(0, 2) : name;
		}
		return Icon(def?.Color, letters, size);
	}

	/// <summary>A thin labelled bar for a taste axis (0…max).</summary>
	public static Control AxisBar(string name, int value, int max, Color color, float width = 160f)
	{
		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		Label caption = Caption(name, 10);
		caption.CustomMinimumSize = new Vector2(52f, 0f);
		row.AddChild(caption);
		ProgressBar bar = new ProgressBar
		{
			MinValue = 0,
			MaxValue = max,
			Value = value,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(width, 8f),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		StyleBoxFlat bg = new StyleBoxFlat { BgColor = new Color(1f, 1f, 1f, 0.06f) };
		bg.SetCornerRadiusAll(3);
		StyleBoxFlat fill = new StyleBoxFlat { BgColor = color, ShadowColor = new Color(color, 0.4f), ShadowSize = 4 };
		fill.SetCornerRadiusAll(3);
		bar.AddThemeStyleboxOverride("background", bg);
		bar.AddThemeStyleboxOverride("fill", fill);
		row.AddChild(bar);
		Label number = Caption(value.ToString(), 11);
		number.AddThemeColorOverride("font_color", Text);
		row.AddChild(number);
		return row;
	}
}
