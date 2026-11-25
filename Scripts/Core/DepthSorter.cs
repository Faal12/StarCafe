using Godot;
using System.Collections.Generic;

/// <summary>
/// Gives the hall its 45-degree "Among Us" depth: whoever stands lower on the screen is drawn in front.
/// Every object is sorted by the y of its footprint (collision bottom, or sprite bottom), the floor stays
/// behind everything, and moving actors get a soft ground shadow and are re-sorted every frame.
/// Must be a child of the node whose children it sorts, and come after them in the scene.
/// </summary>
public partial class DepthSorter : Node
{
	private const int FloorZ = -4000;

	private const string ActorGroup = "Actor";
	private const string FloorName = "Floor";

	private readonly List<Node2D> _movers = new();

	public override void _Ready()
	{
		var world = GetParent();
		if (world == null)
			return;

		foreach (var child in world.GetChildren())
		{
			if (child is not Node2D node)
				continue;

			if (node.Name == FloorName)
			{
				node.ZIndex = FloorZ;
				continue;
			}

			bool isActor = node.IsInGroup(ActorGroup) || node.IsInGroup("Player");
			if (isActor)
			{
				_movers.Add(node);
				AddShadow(node);
			}

			node.ZIndex = ToZ(FootprintBottom(node, isActor));
		}
	}

	public override void _Process(double delta)
	{
		foreach (var mover in _movers)
			mover.ZIndex = ToZ(mover.GlobalPosition.Y);
	}

	// Decks are stacked far apart in one scene; sort by the height inside the deck.
	private static int ToZ(float y) => DeckManager.SortZ(y);

	/// <summary>World y of the lowest point that touches the ground.</summary>
	private static float FootprintBottom(Node2D node, bool originIsFeet)
	{
		if (originIsFeet)
			return node.GlobalPosition.Y;

		float? best = null;
		foreach (var shapeNode in FindShapes(node))
		{
			float bottom = shapeNode.GlobalPosition.Y + HalfHeight(shapeNode.Shape) * Mathf.Abs(shapeNode.GlobalScale.Y);
			if (best == null || bottom > best)
				best = bottom;
		}
		if (best != null)
			return best.Value;

		var sprite = node as Sprite2D ?? node.GetNodeOrNull<Sprite2D>("Sprite2D");
		return sprite != null ? SpriteBottom(sprite) : node.GlobalPosition.Y;
	}

	private static float SpriteBottom(Sprite2D sprite)
	{
		if (sprite.Texture == null)
			return sprite.GlobalPosition.Y;

		float height = sprite.Texture.GetHeight();
		float half = sprite.Centered ? height / 2f : height;
		return sprite.GlobalPosition.Y + (sprite.Offset.Y + half) * Mathf.Abs(sprite.GlobalScale.Y);
	}

	private static IEnumerable<CollisionShape2D> FindShapes(Node node)
	{
		foreach (var child in node.GetChildren())
		{
			if (child is CollisionShape2D shape && shape.Shape != null && !shape.Disabled)
				yield return shape;
			else if (child is CollisionObject2D)
				foreach (var nested in FindShapes(child))
					yield return nested;
		}
	}

	private static float HalfHeight(Shape2D shape) => shape switch
	{
		RectangleShape2D rect => rect.Size.Y / 2f,
		CircleShape2D circle => circle.Radius,
		CapsuleShape2D capsule => capsule.Height / 2f,
		_ => 0f,
	};

	private static void AddShadow(Node2D actor)
	{
		if (actor.GetNodeOrNull("GroundShadow") != null)
			return;

		var gradient = new Gradient
		{
			Offsets = new[] { 0f, 1f },
			Colors = new[] { new Color(0, 0, 0, 0.38f), new Color(0, 0, 0, 0f) },
		};
		var texture = new GradientTexture2D
		{
			Gradient = gradient,
			Width = 64,
			Height = 64,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 0.5f),
		};

		actor.AddChild(new Sprite2D
		{
			Name = "GroundShadow",
			Texture = texture,
			Scale = new Vector2(0.85f, 0.32f),
			Position = new Vector2(0, -2),
			ShowBehindParent = true,
		});
	}
}
