using System;
using Godot;

/// <summary>Generic "walk up and press E" object (elevator, generator, bed, toolbox, doors …).</summary>
public partial class Interactable : Node2D
{
	[Export]
	public string Prompt { get; set; } = "[E] Benutzen";

	[Export]
	public float Radius { get; set; } = 70f;

	[Export]
	public Vector2 PromptOffset { get; set; } = new Vector2(0f, -80f);

	/// <summary>Raised when the player presses E nearby.</summary>
	public event Action Used;

	/// <summary>Optional check; when false, the prompt is hidden and E does nothing.</summary>
	public Func<bool> IsAvailable;

	private Label _label;

	private bool _inRange;

	public override void _Ready()
	{
		Area2D area = new Area2D { CollisionMask = 1, Monitorable = false };
		area.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
		area.BodyEntered += body => { if (body.IsInGroup("Player")) { _inRange = true; Refresh(); } };
		area.BodyExited += body => { if (body.IsInGroup("Player")) { _inRange = false; Refresh(); } };
		AddChild(area);
		_label = new Label { Text = Prompt, HorizontalAlignment = HorizontalAlignment.Center, Size = new Vector2(220f, 20f), Position = PromptOffset - new Vector2(110f, 10f), Visible = false, ZIndex = 4090, ZAsRelative = false };
		_label.AddThemeFontSizeOverride("font_size", 13);
		_label.AddThemeColorOverride("font_color", Holo.Accent);
		_label.AddThemeColorOverride("font_outline_color", Colors.Black);
		_label.AddThemeConstantOverride("outline_size", 4);
		AddChild(_label);
	}

	public void SetPrompt(string text)
	{
		Prompt = text;
		if (_label != null)
		{
			_label.Text = text;
		}
	}

	public override void _Process(double delta)
	{
		if (_inRange)
		{
			Refresh();
		}
	}

	private void Refresh()
	{
		_label.Visible = _inRange && (IsAvailable?.Invoke() ?? true);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_inRange && @event.IsActionPressed("interact") && !@event.IsEcho() && (IsAvailable?.Invoke() ?? true))
		{
			GetViewport().SetInputAsHandled();
			Used?.Invoke();
		}
	}
}
