using System.Collections.Generic;
using Godot;

/// <summary>
/// Shows the consequences of events in the tavern: props and characters from <see cref="WorldState"/>
/// at named spots (Marker2D children of the "WorldSpots" node), with entrance/exit animations,
/// plus one-off effects (shake, flash, blackout, confetti) once the event window is closed.
/// Without a texture, things appear as labelled placeholders.
/// </summary>
public partial class WorldStage : Node2D
{
	[Export]
	public NodePath SpotsPath { get; set; } = new NodePath("../WorldSpots");

	private static readonly Vector2 FallbackSpot = new Vector2(936f, 640f);

	private readonly Dictionary<string, Vector2> _spots = new Dictionary<string, Vector2>(System.StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, (Node2D Node, WorldItem Item)> _shown = new Dictionary<string, (Node2D, WorldItem)>(System.StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _warnedSpots = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

	private CanvasLayer _overlay;

	public override void _Ready()
	{
		Node spots = GetNodeOrNull(SpotsPath);
		if (spots != null)
		{
			foreach (Node child in spots.GetChildren())
			{
				if (child is Node2D marker)
				{
					_spots[marker.Name] = marker.GlobalPosition;
				}
			}
		}
		_overlay = new CanvasLayer { Layer = 25 };
		AddChild(_overlay);
		Sync(animate: false);
	}

	public override void _EnterTree()
	{
		WorldService.State.Changed += OnChanged;
	}

	public override void _ExitTree()
	{
		WorldService.State.Changed -= OnChanged;
	}

	public override void _Process(double delta)
	{
		// Pausable node: this only runs once the event window has closed and the game continues.
		if (EventPanel.IsOpen)
		{
			return;
		}
		while (WorldService.State.TryTakeAnimation(out WorldAnimation animation))
		{
			Play(animation);
		}
	}

	/// <summary>World position of a named spot (or the middle of the room if it does not exist).</summary>
	public Vector2 SpotPosition(string spot)
	{
		if (!string.IsNullOrWhiteSpace(spot) && _spots.TryGetValue(spot, out Vector2 position))
		{
			return position;
		}
		if (!string.IsNullOrWhiteSpace(spot) && _warnedSpots.Add(spot))
		{
			GD.PushWarning($"Welt: Ort \"{spot}\" gibt es nicht (vorhanden: {string.Join(", ", _spots.Keys)}). Benutze die Raummitte.");
		}
		return FallbackSpot;
	}

	private void OnChanged()
	{
		if (IsInsideTree())
		{
			Sync(animate: true);
		}
	}

	private void Sync(bool animate)
	{
		HashSet<string> wanted = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
		foreach (WorldItem item in WorldService.State.Items)
		{
			wanted.Add(item.Id);
			if (_shown.TryGetValue(item.Id, out var shown))
			{
				if (ReferenceEquals(shown.Item, item))
				{
					continue;
				}
				Remove(item.Id, animate: false);
			}
			Node2D node = Build(item);
			AddChild(node);
			_shown[item.Id] = (node, item);
			if (animate)
			{
				Appear(node, item.Appear);
			}
		}
		foreach (string id in new List<string>(_shown.Keys))
		{
			if (!wanted.Contains(id))
			{
				Remove(id, animate);
			}
		}
	}

	private void Remove(string id, bool animate)
	{
		if (!_shown.TryGetValue(id, out var shown))
		{
			return;
		}
		_shown.Remove(id);
		if (!animate)
		{
			shown.Node.QueueFree();
			return;
		}
		Tween tween = shown.Node.CreateTween();
		tween.TweenProperty(shown.Node, "modulate:a", 0f, 0.4);
		tween.TweenCallback(Callable.From(shown.Node.QueueFree));
	}

	private Node2D Build(WorldItem item)
	{
		Vector2 basePosition = item.X.HasValue && item.Y.HasValue ? new Vector2(item.X.Value, item.Y.Value) : SpotPosition(item.Spot);
		Vector2 position = basePosition + new Vector2(item.OffsetX, item.OffsetY);
		Node2D root = new Node2D
		{
			Name = item.Id,
			Position = position,
			ZIndex = Mathf.Clamp(Mathf.RoundToInt(position.Y), -3999, 4000)
		};
		Texture2D texture = !string.IsNullOrWhiteSpace(item.Texture) && ResourceLoader.Exists(item.Texture) ? GD.Load<Texture2D>(item.Texture) : null;
		if (texture != null)
		{
			root.AddChild(new Sprite2D
			{
				Texture = texture,
				Offset = new Vector2(0f, -texture.GetHeight() / 2f),
				Scale = Vector2.One * item.Scale,
				TextureFilter = TextureFilterEnum.LinearWithMipmaps
			});
		}
		else if (item.Kind == "character")
		{
			BuildCharacterPlaceholder(root, item);
		}
		else
		{
			BuildPropPlaceholder(root, item);
		}
		return root;
	}

	public static void BuildPropPlaceholder(Node2D root, WorldItem item)
	{
		Color color = ParseColor(item.Color, new Color(0.62f, 0.45f, 0.3f));
		Vector2 size = new Vector2(56f, 38f) * item.Scale;
		Panel box = new Panel { Position = new Vector2(-size.X / 2f, -size.Y), Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
		box.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(color, 0.85f), color.Lightened(0.4f), 2, 5, 0, 0));
		root.AddChild(box);
		root.AddChild(PlaceholderLabel(item.Label, -size.Y - 24f));
	}

	public static void BuildCharacterPlaceholder(Node2D root, WorldItem item)
	{
		Color color = ParseColor(item.Color, FactionColor(item.Faction));
		float s = item.Scale;
		Panel body = new Panel { Position = new Vector2(-15f, -58f) * s, Size = new Vector2(30f, 46f) * s, MouseFilter = Control.MouseFilterEnum.Ignore };
		body.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(color, 0.9f), color.Lightened(0.45f), 2, (int)(12 * s), 0, 0));
		root.AddChild(body);
		Panel head = new Panel { Position = new Vector2(-12f, -82f) * s, Size = new Vector2(24f, 24f) * s, MouseFilter = Control.MouseFilterEnum.Ignore };
		head.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(color.Lightened(0.2f), 0.95f), color.Lightened(0.5f), 2, (int)(12 * s), 0, 0));
		root.AddChild(head);
		root.AddChild(PlaceholderLabel(item.Label, -82f * s - 24f));
	}

	private static Label PlaceholderLabel(string text, float y)
	{
		Label label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(-80f, y),
			Size = new Vector2(160f, 20f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.85f));
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 4);
		return label;
	}

	private static void Appear(Node2D node, string appear)
	{
		switch (appear)
		{
		case "none":
			return;
		case "drop":
		{
			Vector2 target = node.Position;
			node.Position = target + new Vector2(0f, -140f);
			node.Modulate = new Color(1f, 1f, 1f, 0f);
			Tween tween = node.CreateTween().SetParallel();
			tween.TweenProperty(node, "position", target, 0.7).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(node, "modulate:a", 1f, 0.25);
			return;
		}
		case "pop":
		{
			node.Scale = new Vector2(0.2f, 0.2f);
			node.CreateTween().TweenProperty(node, "scale", Vector2.One, 0.6).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
			return;
		}
		default:
			node.Modulate = new Color(1f, 1f, 1f, 0f);
			node.CreateTween().TweenProperty(node, "modulate:a", 1f, 0.8);
			return;
		}
	}

	private void Play(WorldAnimation animation)
	{
		switch (animation.Name)
		{
		case "shake":
			Shake();
			break;
		case "flash":
			Flash(ParseColor(animation.Color, Colors.White), 0.7f, 0.08, 0.0, 0.5);
			break;
		case "blackout":
			Flash(ParseColor(animation.Color, Colors.Black), 0.88f, 0.25, 1.3, 0.9);
			break;
		case "confetti":
			Confetti(string.IsNullOrWhiteSpace(animation.Spot) ? PlayerPosition() : SpotPosition(animation.Spot));
			break;
		}
	}

	private void Shake()
	{
		Camera2D camera = GetViewport().GetCamera2D();
		if (camera == null)
		{
			return;
		}
		Vector2 rest = camera.Offset;
		Tween tween = camera.CreateTween();
		for (int i = 12; i > 0; i--)
		{
			float strength = i * 2.2f;
			tween.TweenProperty(camera, "offset", rest + new Vector2((float)GD.RandRange(-strength, strength), (float)GD.RandRange(-strength, strength)), 0.045);
		}
		tween.TweenProperty(camera, "offset", rest, 0.05);
	}

	private void Flash(Color color, float peak, double rise, double hold, double fall)
	{
		ColorRect rect = new ColorRect { Color = new Color(color, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_overlay.AddChild(rect);
		Tween tween = rect.CreateTween();
		tween.TweenProperty(rect, "color:a", peak, rise);
		if (hold > 0)
		{
			tween.TweenInterval(hold);
		}
		tween.TweenProperty(rect, "color:a", 0f, fall);
		tween.TweenCallback(Callable.From(rect.QueueFree));
	}

	private void Confetti(Vector2 position)
	{
		Gradient colors = new Gradient();
		colors.Offsets = new float[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
		colors.Colors = new Color[] { new Color(0.95f, 0.75f, 0.3f), new Color(0.4f, 0.85f, 0.95f), new Color(0.95f, 0.45f, 0.6f), new Color(0.55f, 0.9f, 0.5f), new Color(0.8f, 0.6f, 0.95f) };
		CpuParticles2D particles = new CpuParticles2D
		{
			Position = position + new Vector2(0f, -40f),
			ZIndex = 4000,
			Amount = 90,
			Lifetime = 1.8,
			OneShot = true,
			Explosiveness = 0.95f,
			Direction = Vector2.Up,
			Spread = 70f,
			InitialVelocityMin = 220f,
			InitialVelocityMax = 420f,
			Gravity = new Vector2(0f, 650f),
			ScaleAmountMin = 3f,
			ScaleAmountMax = 6f,
			ColorInitialRamp = colors,
			AngularVelocityMin = -360f,
			AngularVelocityMax = 360f
		};
		AddChild(particles);
		particles.Emitting = true;
		GetTree().CreateTimer(3.0).Timeout += particles.QueueFree;
	}

	private Vector2 PlayerPosition()
	{
		return GetTree().GetFirstNodeInGroup("Player") is Node2D player ? player.GlobalPosition : FallbackSpot;
	}

	public static Color ParseColor(string hex, Color fallback)
	{
		return !string.IsNullOrWhiteSpace(hex) && Color.HtmlIsValid(hex) ? Color.FromHtml(hex) : fallback;
	}

	public static Color FactionColor(string faction)
	{
		if (!EventLibrary.TryFaction(faction, out Faction f))
		{
			return new Color(0.6f, 0.6f, 0.65f);
		}
		return f switch
		{
			Faction.Imperium => new Color(0.78f, 0.25f, 0.25f),
			Faction.Rebellen => new Color(0.95f, 0.6f, 0.2f),
			Faction.Soeldner => new Color(0.55f, 0.5f, 0.42f),
			Faction.Silberkiel => new Color(0.75f, 0.8f, 0.88f),
			Faction.GraueHaende => new Color(0.5f, 0.52f, 0.55f),
			Faction.Sternenwacht => new Color(0.3f, 0.5f, 0.9f),
			Faction.Funkenwerkstatt => new Color(0.95f, 0.85f, 0.3f),
			Faction.Weitwanderer => new Color(0.4f, 0.75f, 0.55f),
			_ => new Color(0.6f, 0.6f, 0.65f),
		};
	}
}
