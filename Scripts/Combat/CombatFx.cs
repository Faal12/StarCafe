using Godot;

/// <summary>Small combat effects: floating numbers, flashes, camera shake, hit sparks.</summary>
public static class CombatFx
{
	public static void FloatText(Node parent, Vector2 globalPosition, string text, Color color, int size = 16)
	{
		if (parent == null || !GodotObject.IsInstanceValid(parent))
		{
			return;
		}
		Label label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			Size = new Vector2(80f, 24f),
			ZIndex = 4090,
			ZAsRelative = false
		};
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 5);
		Node2D holder = new Node2D { GlobalPosition = globalPosition + new Vector2((float)GD.RandRange(-8, 8), -40f), ZIndex = 4090, ZAsRelative = false };
		holder.AddChild(label);
		label.Position = new Vector2(-40f, -12f);
		parent.AddChild(holder);
		Tween tween = holder.CreateTween().SetParallel();
		tween.TweenProperty(holder, "position", holder.Position + new Vector2(0f, -36f), 0.7).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(holder, "modulate:a", 0f, 0.7).SetDelay(0.25);
		tween.Chain().TweenCallback(Callable.From(holder.QueueFree));
	}

	public static void Shake(Camera2D camera, float strength, int steps = 6)
	{
		if (camera == null)
		{
			return;
		}
		Tween tween = camera.CreateTween();
		for (int i = steps; i > 0; i--)
		{
			float s = strength * i / steps;
			tween.TweenProperty(camera, "offset", new Vector2((float)GD.RandRange(-s, s), (float)GD.RandRange(-s, s)), 0.03);
		}
		tween.TweenProperty(camera, "offset", Vector2.Zero, 0.03);
	}

	/// <summary>Flashes a node white for a moment (hit feedback).</summary>
	public static void Flash(CanvasItem item, Color? color = null)
	{
		if (item == null)
		{
			return;
		}
		item.Modulate = color ?? new Color(3f, 3f, 3f);
		item.CreateTween().TweenProperty(item, "modulate", Colors.White, 0.15);
	}

	/// <summary>Burst of small particles (hits, deaths, sparks).</summary>
	public static void Burst(Node parent, Vector2 globalPosition, Color color, int amount = 14, float speed = 180f)
	{
		CpuParticles2D particles = new CpuParticles2D
		{
			GlobalPosition = globalPosition,
			ZIndex = 4080,
			ZAsRelative = false,
			Amount = amount,
			Lifetime = 0.5,
			OneShot = true,
			Explosiveness = 1f,
			Spread = 180f,
			InitialVelocityMin = speed * 0.5f,
			InitialVelocityMax = speed,
			Gravity = new Vector2(0f, 300f),
			ScaleAmountMin = 2f,
			ScaleAmountMax = 4f,
			Color = color
		};
		parent.AddChild(particles);
		particles.Emitting = true;
		parent.GetTree().CreateTimer(1.2).Timeout += particles.QueueFree;
	}
}
