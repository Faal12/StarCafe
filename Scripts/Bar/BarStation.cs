using Godot;

/// <summary>The place behind the counter where drinks are mixed. Stand close and press E.</summary>
public partial class BarStation : Node2D
{
	[Export]
	public float Radius { get; set; } = 75f;

	private Label _prompt;

	private bool _playerInRange;

	public override void _Ready()
	{
		Area2D area = new Area2D { CollisionMask = 1, Monitorable = false };
		area.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
		area.BodyEntered += body => { if (body.IsInGroup("Player")) { _playerInRange = true; _prompt.Visible = true; } };
		area.BodyExited += body => { if (body.IsInGroup("Player")) { _playerInRange = false; _prompt.Visible = false; } };
		AddChild(area);
		_prompt = new Label { Text = "[E] Mixen", Position = new Vector2(-60f, -70f), Size = new Vector2(120f, 20f), HorizontalAlignment = HorizontalAlignment.Center, Visible = false };
		_prompt.AddThemeFontSizeOverride("font_size", 13);
		_prompt.AddThemeColorOverride("font_color", Holo.Accent);
		_prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
		_prompt.AddThemeConstantOverride("outline_size", 4);
		_prompt.ZIndex = 4000;
		AddChild(_prompt);
		// A soft holo glow on the counter so the station is easy to find (placeholder).
		Panel glow = new Panel { Position = new Vector2(-34f, -34f), Size = new Vector2(68f, 14f), MouseFilter = Control.MouseFilterEnum.Ignore };
		StyleBoxFlat style = new StyleBoxFlat { BgColor = new Color(0.3f, 0.9f, 1f, 0.25f), ShadowColor = new Color(0.3f, 0.9f, 1f, 0.35f), ShadowSize = 8 };
		style.SetCornerRadiusAll(6);
		glow.AddThemeStyleboxOverride("panel", style);
		AddChild(glow);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_playerInRange && @event.IsActionPressed("interact") && !@event.IsEcho() && !DialogueController.IsOpen)
		{
			GetViewport().SetInputAsHandled();
			BarPanel.Open();
		}
	}
}
