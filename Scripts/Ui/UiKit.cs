using System;
using Godot;

// Small helpers so menu screens can be built in code instead of hand-written .tscn files.
public static class UiKit
{
	public static readonly Color Amber = new Color(0.90f, 0.75f, 0.43f);
	public static readonly Color Muted = new Color(0.68f, 0.70f, 0.74f);
	public static readonly Color Good = new Color(0.55f, 0.85f, 0.55f);
	public static readonly Color Bad = new Color(0.95f, 0.45f, 0.40f);
	public static readonly Color Warn = new Color(0.95f, 0.70f, 0.35f);

	private const float LabelColumnWidth = 280f;

	public static StyleBoxFlat PanelStyle()
	{
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.11f, 0.97f),
			BorderColor = Amber
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(8);
		style.SetContentMarginAll(16);
		return style;
	}

	public static StyleBoxFlat FillStyle(Color color)
	{
		var style = new StyleBoxFlat { BgColor = color };
		style.SetCornerRadiusAll(3);
		return style;
	}

	public static Label Header(string text)
	{
		var label = new Label { Text = text };
		label.AddThemeFontSizeOverride("font_size", 20);
		label.AddThemeColorOverride("font_color", Amber);
		label.AddThemeConstantOverride("line_spacing", 4);
		return label;
	}

	public static Label Hint(string text)
	{
		var label = new Label
		{
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		label.AddThemeFontSizeOverride("font_size", 13);
		label.AddThemeColorOverride("font_color", Muted);
		return label;
	}

	public static HBoxContainer Row(string labelText, Control control, string tooltip = null)
	{
		var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 12);

		var label = new Label
		{
			Text = labelText,
			CustomMinimumSize = new Vector2(LabelColumnWidth, 0),
			VerticalAlignment = VerticalAlignment.Center
		};
		row.AddChild(label);

		control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(control);

		if (!string.IsNullOrEmpty(tooltip))
		{
			row.TooltipText = tooltip;
			label.TooltipText = tooltip;
			control.TooltipText = tooltip;
		}

		return row;
	}

	public static OptionButton Options(string[] items, int selected, Action<int> onSelected)
	{
		var button = new OptionButton();
		foreach (string item in items)
			button.AddItem(item);

		button.Selected = Math.Clamp(selected, 0, Math.Max(items.Length - 1, 0));
		button.ItemSelected += index => onSelected((int)index);
		return button;
	}

	public static OptionButton EnumOptions<T>(T current, Action<T> onSelected) where T : struct, Enum
	{
		string[] names = Enum.GetNames(typeof(T));
		var prettyNames = new string[names.Length];
		for (int i = 0; i < names.Length; i++)
			prettyNames[i] = Pretty(names[i]);

		T[] values = (T[])Enum.GetValues(typeof(T));
		return Options(prettyNames, Array.IndexOf(values, current), index => onSelected(values[index]));
	}

	public static CheckBox Toggle(string text, bool value, Action<bool> onToggled)
	{
		var box = new CheckBox { Text = text, ButtonPressed = value };
		box.Toggled += pressed => onToggled(pressed);
		return box;
	}

	// The caption always names the current state, so an unchecked box never reads "an".
	public static CheckBox StateToggle(string onText, string offText, bool value, Action<bool> onToggled)
	{
		var box = new CheckBox { Text = value ? onText : offText, ButtonPressed = value };
		box.Toggled += pressed =>
		{
			box.Text = pressed ? onText : offText;
			onToggled(pressed);
		};
		return box;
	}

	public static HSlider Slider(double min, double max, double step, double value, Action<double> onChanged)
	{
		var slider = new HSlider
		{
			MinValue = min,
			MaxValue = max,
			Step = step,
			Value = value,
			CustomMinimumSize = new Vector2(220, 24),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		slider.ValueChanged += newValue => onChanged(newValue);
		return slider;
	}

	public static HBoxContainer SliderRow(string labelText, double min, double max, double step, double value,
		Func<double, string> format, Action<double> onChanged, string tooltip = null)
	{
		var valueLabel = new Label
		{
			Text = format(value),
			CustomMinimumSize = new Vector2(64, 0),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center
		};

		HSlider slider = Slider(min, max, step, value, newValue =>
		{
			valueLabel.Text = format(newValue);
			onChanged(newValue);
		});

		var sliderAndValue = new HBoxContainer();
		sliderAndValue.AddThemeConstantOverride("separation", 8);
		sliderAndValue.AddChild(slider);
		slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		sliderAndValue.AddChild(valueLabel);

		return Row(labelText, sliderAndValue, tooltip);
	}

	public static ScrollContainer Scroll(Control content)
	{
		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.AddChild(content);
		return scroll;
	}

	public static void ClearChildren(Node parent)
	{
		foreach (Node child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}

	public static string Pretty(string enumName) => enumName switch
	{
		"Gross" => "Groß",
		_ => enumName
	};
}
