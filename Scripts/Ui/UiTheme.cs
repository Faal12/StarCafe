using Godot;

/// <summary>
/// The look of all menu screens: dark card panels, amber accents, rounded buttons.
/// Built in code (like the screens themselves); assign <see cref="Get"/> to the root control of a screen.
/// </summary>
public static class UiTheme
{
	public const string BigButton = "BigButton";
	public const string PrimaryButton = "PrimaryButton";

	public static readonly Color Background = new(0.04f, 0.045f, 0.06f);
	public static readonly Color Card = new(0.085f, 0.095f, 0.12f, 0.98f);
	public static readonly Color Field = new(0.055f, 0.062f, 0.08f);
	public static readonly Color Raised = new(0.14f, 0.15f, 0.19f);
	public static readonly Color RaisedHover = new(0.19f, 0.20f, 0.25f);
	public static readonly Color Line = new(0.27f, 0.27f, 0.30f);
	public static readonly Color Text = new(0.93f, 0.92f, 0.89f);
	public static readonly Color TextDim = new(0.62f, 0.64f, 0.69f);
	public static readonly Color Cyan = new(0.40f, 0.85f, 0.95f);

	private static Theme _theme;
	private static Font _bodyFont;
	private static Font _headingFont;
	private static Font _titleFont;

	// Bahnschrift ships with Windows 10/11: a clean, slightly technical DIN-style face that fits a space game.
	public static Font BodyFont => _bodyFont ??= MakeFont(400, 0);
	public static Font HeadingFont => _headingFont ??= MakeFont(550, 1);
	public static Font TitleFont => _titleFont ??= MakeFont(700, 7);

	private static Font MakeFont(int weight, int glyphSpacing)
	{
		var system = new SystemFont { FontNames = new[] { "Bahnschrift", "Segoe UI", "Arial" }, FontWeight = weight };
		return new FontVariation { BaseFont = system, SpacingGlyph = glyphSpacing };
	}

	public static Theme Get() => _theme ??= Build();

	private static Theme Build()
	{
		var theme = new Theme { DefaultFontSize = 17, DefaultFont = BodyFont };

		BuildLabels(theme);
		BuildButtons(theme);
		BuildFields(theme);
		BuildToggles(theme);
		BuildSliders(theme);
		BuildTabs(theme);
		BuildScrollBars(theme);
		BuildPopups(theme);
		BuildPanels(theme);
		BuildBars(theme);
		return theme;
	}

	// ---------- pieces

	private static void BuildLabels(Theme theme)
	{
		theme.SetColor("font_color", "Label", Text);
		theme.SetColor("font_color", "RichTextLabel", Text);
		theme.SetColor("default_color", "RichTextLabel", Text);
	}

	private static void BuildButtons(Theme theme)
	{
		SetButtonStyles(theme, "Button", Raised, RaisedHover, Line, UiKit.Amber, 14, 8, 6);
		theme.SetFont("font", "Button", HeadingFont);
		theme.SetColor("font_color", "Button", Text);
		theme.SetColor("font_hover_color", "Button", Colors.White);
		theme.SetColor("font_pressed_color", "Button", UiKit.Amber);
		theme.SetColor("font_focus_color", "Button", Text);
		theme.SetColor("font_disabled_color", "Button", new Color(0.45f, 0.46f, 0.50f));

		// Large menu entries on the title screen.
		theme.SetTypeVariation(BigButton, "Button");
		SetButtonStyles(theme, BigButton, new Color(0.06f, 0.075f, 0.12f, 0.72f), new Color(0.08f, 0.14f, 0.19f, 0.92f),
			new Color(0.20f, 0.26f, 0.34f), Cyan, 28, 15, 8);
		theme.SetFontSize("font_size", BigButton, 22);
		theme.SetFont("font", BigButton, HeadingFont);
		theme.SetColor("font_pressed_color", BigButton, Cyan);
		theme.SetConstant("align_start", BigButton, 1);

		// The one button that should be pressed: filled amber.
		theme.SetTypeVariation(PrimaryButton, "Button");
		SetButtonStyles(theme, PrimaryButton, new Color(0.78f, 0.60f, 0.30f), new Color(0.90f, 0.72f, 0.40f),
			new Color(0.95f, 0.80f, 0.50f), Colors.White, 20, 9, 6);
		theme.SetColor("font_color", PrimaryButton, new Color(0.10f, 0.08f, 0.05f));
		theme.SetColor("font_hover_color", PrimaryButton, new Color(0.05f, 0.04f, 0.02f));
		theme.SetColor("font_pressed_color", PrimaryButton, new Color(0.05f, 0.04f, 0.02f));
		theme.SetColor("font_focus_color", PrimaryButton, new Color(0.10f, 0.08f, 0.05f));
	}

	private static void SetButtonStyles(Theme theme, string type, Color normal, Color hover, Color border, Color accent, int marginX, int marginY, int radius)
	{
		theme.SetStylebox("normal", type, Box(normal, border, 1, radius, marginX, marginY));
		theme.SetStylebox("hover", type, Box(hover, accent, 1, radius, marginX, marginY));
		theme.SetStylebox("pressed", type, Box(normal.Darkened(0.2f), accent, 2, radius, marginX, marginY));
		theme.SetStylebox("disabled", type, Box(normal.Darkened(0.35f), border.Darkened(0.4f), 1, radius, marginX, marginY));
		theme.SetStylebox("focus", type, Outline(accent, 2, radius));
	}

	private static void BuildFields(Theme theme)
	{
		theme.SetStylebox("normal", "LineEdit", Box(Field, Line, 1, 5, 10, 7));
		theme.SetStylebox("focus", "LineEdit", Box(Field, UiKit.Amber, 2, 5, 10, 7));
		theme.SetStylebox("read_only", "LineEdit", Box(Field.Darkened(0.2f), Line.Darkened(0.4f), 1, 5, 10, 7));
		theme.SetColor("font_color", "LineEdit", Text);
		theme.SetColor("font_placeholder_color", "LineEdit", new Color(0.45f, 0.47f, 0.52f));
		theme.SetColor("caret_color", "LineEdit", UiKit.Amber);
		theme.SetColor("selection_color", "LineEdit", new Color(0.90f, 0.75f, 0.43f, 0.35f));

		SetButtonStyles(theme, "OptionButton", Field, Raised, Line, UiKit.Amber, 12, 8, 5);
		theme.SetColor("font_color", "OptionButton", Text);
		theme.SetColor("font_hover_color", "OptionButton", Colors.White);
		theme.SetColor("font_focus_color", "OptionButton", Text);
		theme.SetColor("font_pressed_color", "OptionButton", UiKit.Amber);
		theme.SetIcon("arrow", "OptionButton", ArrowIcon(UiKit.Amber));
		theme.SetConstant("arrow_margin", "OptionButton", 10);
	}

	private static void BuildToggles(Theme theme)
	{
		Texture2D unchecked_ = BoxIcon(false);
		Texture2D checked_ = BoxIcon(true);

		foreach (string type in new[] { "CheckBox", "CheckButton" })
		{
			theme.SetIcon("unchecked", type, unchecked_);
			theme.SetIcon("checked", type, checked_);
			theme.SetIcon("unchecked_disabled", type, unchecked_);
			theme.SetIcon("checked_disabled", type, checked_);
			theme.SetColor("font_color", type, Text);
			theme.SetColor("font_hover_color", type, Colors.White);
			theme.SetColor("font_pressed_color", type, Text);
			theme.SetConstant("h_separation", type, 10);
			theme.SetStylebox("normal", type, new StyleBoxEmpty());
			theme.SetStylebox("hover", type, new StyleBoxEmpty());
			theme.SetStylebox("pressed", type, new StyleBoxEmpty());
			theme.SetStylebox("focus", type, Outline(UiKit.Amber, 1, 4));
		}
	}

	private static void BuildSliders(Theme theme)
	{
		theme.SetStylebox("slider", "HSlider", Box(Field, Line, 1, 3, 0, 3));
		theme.SetStylebox("grabber_area", "HSlider", Box(new Color(0.72f, 0.55f, 0.28f), new Color(0.72f, 0.55f, 0.28f), 0, 3, 0, 3));
		theme.SetStylebox("grabber_area_highlight", "HSlider", Box(UiKit.Amber, UiKit.Amber, 0, 3, 0, 3));
		theme.SetIcon("grabber", "HSlider", Dot(UiKit.Amber, 9));
		theme.SetIcon("grabber_highlight", "HSlider", Dot(Colors.White, 10));
		theme.SetIcon("grabber_disabled", "HSlider", Dot(TextDim, 9));
	}

	private static void BuildTabs(Theme theme)
	{
		var selected = Box(Card, UiKit.Amber, 0, 6, 22, 10);
		selected.BorderWidthBottom = 3;
		selected.CornerRadiusBottomLeft = 0;
		selected.CornerRadiusBottomRight = 0;

		var unselected = Box(new Color(0.06f, 0.065f, 0.085f), Line, 0, 6, 22, 10);
		unselected.CornerRadiusBottomLeft = 0;
		unselected.CornerRadiusBottomRight = 0;

		var hovered = Box(new Color(0.11f, 0.12f, 0.15f), Line, 0, 6, 22, 10);
		hovered.CornerRadiusBottomLeft = 0;
		hovered.CornerRadiusBottomRight = 0;

		theme.SetStylebox("tab_selected", "TabContainer", selected);
		theme.SetStylebox("tab_unselected", "TabContainer", unselected);
		theme.SetStylebox("tab_hovered", "TabContainer", hovered);
		theme.SetStylebox("tab_focus", "TabContainer", new StyleBoxEmpty());
		theme.SetStylebox("panel", "TabContainer", Box(new Color(0.07f, 0.078f, 0.10f), Line, 1, 6, 16, 14));
		theme.SetColor("font_selected_color", "TabContainer", UiKit.Amber);
		theme.SetColor("font_unselected_color", "TabContainer", TextDim);
		theme.SetColor("font_hovered_color", "TabContainer", Colors.White);
		theme.SetConstant("side_margin", "TabContainer", 0);
	}

	private static void BuildScrollBars(Theme theme)
	{
		foreach (string type in new[] { "VScrollBar", "HScrollBar" })
		{
			theme.SetStylebox("scroll", type, Box(new Color(0, 0, 0, 0.25f), Colors.Transparent, 0, 4, 0, 0));
			theme.SetStylebox("grabber", type, Box(new Color(0.40f, 0.38f, 0.33f), Colors.Transparent, 0, 4, 0, 0));
			theme.SetStylebox("grabber_highlight", type, Box(UiKit.Amber.Darkened(0.1f), Colors.Transparent, 0, 4, 0, 0));
			theme.SetStylebox("grabber_pressed", type, Box(UiKit.Amber, Colors.Transparent, 0, 4, 0, 0));
		}
	}

	private static void BuildPopups(Theme theme)
	{
		theme.SetStylebox("panel", "PopupMenu", Box(new Color(0.07f, 0.078f, 0.10f), UiKit.Amber.Darkened(0.4f), 1, 6, 6, 6));
		theme.SetStylebox("hover", "PopupMenu", Box(new Color(0.20f, 0.17f, 0.12f), Colors.Transparent, 0, 4, 8, 4));
		theme.SetColor("font_color", "PopupMenu", Text);
		theme.SetColor("font_hover_color", "PopupMenu", Colors.White);
		theme.SetConstant("v_separation", "PopupMenu", 6);
	}

	private static void BuildBars(Theme theme)
	{
		theme.SetStylebox("background", "ProgressBar", Box(Field, Line, 1, 4, 0, 0));
		theme.SetStylebox("fill", "ProgressBar", Box(new Color(0.78f, 0.60f, 0.30f), Colors.Transparent, 0, 4, 0, 0));
		theme.SetColor("font_color", "ProgressBar", Text);
		theme.SetStylebox("separator", "HSeparator", Box(Line, Colors.Transparent, 0, 0, 0, 0));
		theme.SetConstant("separation", "HSeparator", 10);
	}

	private static void BuildPanels(Theme theme)
	{
		theme.SetStylebox("panel", "PanelContainer", UiKit.PanelStyle());
		theme.SetStylebox("panel", "Panel", UiKit.PanelStyle());
	}

	// ---------- drawing helpers

	public static StyleBoxFlat Box(Color fill, Color border, int borderWidth, int radius, int marginX, int marginY)
	{
		var style = new StyleBoxFlat { BgColor = fill, BorderColor = border };
		style.SetBorderWidthAll(borderWidth);
		style.SetCornerRadiusAll(radius);
		style.ContentMarginLeft = marginX;
		style.ContentMarginRight = marginX;
		style.ContentMarginTop = marginY;
		style.ContentMarginBottom = marginY;
		style.AntiAliasing = true;
		return style;
	}

	private static StyleBoxFlat Outline(Color color, int width, int radius)
	{
		var style = new StyleBoxFlat { DrawCenter = false, BorderColor = color, AntiAliasing = true };
		style.SetBorderWidthAll(width);
		style.SetCornerRadiusAll(radius);
		return style;
	}

	private static Texture2D Dot(Color color, int radius)
	{
		int size = radius * 2 + 2;
		var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		var center = new Vector2(size / 2f - 0.5f, size / 2f - 0.5f);

		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				float edge = Mathf.Clamp(radius - new Vector2(x, y).DistanceTo(center) + 0.5f, 0f, 1f);
				if (edge > 0f)
					image.SetPixel(x, y, new Color(color, edge));
			}

		return ImageTexture.CreateFromImage(image);
	}

	private static Texture2D BoxIcon(bool isChecked)
	{
		const int size = 22;
		var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		Color border = isChecked ? UiKit.Amber : new Color(0.50f, 0.50f, 0.54f);
		Color fill = isChecked ? UiKit.Amber : Field;

		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				float dx = Mathf.Max(Mathf.Abs(x - (size - 1) / 2f) - (size / 2f - 5f), 0f);
				float dy = Mathf.Max(Mathf.Abs(y - (size - 1) / 2f) - (size / 2f - 5f), 0f);
				float distance = Mathf.Sqrt(dx * dx + dy * dy) - 4f;   // rounded square, radius 4
				float outer = Mathf.Clamp(0.5f - distance, 0f, 1f);
				float inner = Mathf.Clamp(0.5f - (distance + 2f), 0f, 1f);

				if (outer <= 0f)
					continue;

				Color pixel = border.Lerp(fill, inner);
				image.SetPixel(x, y, new Color(pixel, outer));
			}

		if (isChecked)
			DrawCheckMark(image, size);

		return ImageTexture.CreateFromImage(image);
	}

	private static void DrawCheckMark(Image image, int size)
	{
		var ink = new Color(0.10f, 0.08f, 0.05f);
		var a = new Vector2(size * 0.27f, size * 0.52f);
		var b = new Vector2(size * 0.44f, size * 0.70f);
		var c = new Vector2(size * 0.75f, size * 0.30f);

		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				var p = new Vector2(x, y);
				float distance = Mathf.Min(SegmentDistance(p, a, b), SegmentDistance(p, b, c));
				float alpha = Mathf.Clamp(1.4f - distance, 0f, 1f);
				if (alpha > 0f)
					image.SetPixel(x, y, image.GetPixel(x, y).Lerp(ink, alpha));
			}
	}

	private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 ab = b - a;
		float t = Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
		return p.DistanceTo(a + ab * t);
	}

	private static Texture2D ArrowIcon(Color color)
	{
		const int width = 14, height = 9;
		var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		for (int y = 0; y < height; y++)
			for (int x = 0; x < width; x++)
			{
				float half = (width - 1) / 2f - y * ((width - 1) / 2f) / (height - 1);
				float edge = Mathf.Clamp(half - Mathf.Abs(x - (width - 1) / 2f) + 0.5f, 0f, 1f);
				if (edge > 0f)
					image.SetPixel(x, y, new Color(color, edge));
			}

		return ImageTexture.CreateFromImage(image);
	}
}
