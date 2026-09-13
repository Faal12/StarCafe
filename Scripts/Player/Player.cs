using Godot;

public partial class Player : CharacterBody2D
{
	[Export] public float Speed = 220f;
	[Export] private Sprite2D _sprite;
	[Export] private Texture2D _frontTexture;
	[Export] private Texture2D _backTexture;

	public bool CanMove = true;

	public override void _Ready()
	{
		AddToGroup("Player");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!CanMove)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		Velocity = direction * Speed;
		MoveAndSlide();

		UpdateFacing(direction);
	}

	private void UpdateFacing(Vector2 direction)
	{
		if (direction == Vector2.Zero || _sprite == null)
			return;

		if (Mathf.Abs(direction.Y) > Mathf.Abs(direction.X))
		{
			_sprite.Texture = direction.Y < 0 ? _backTexture : _frontTexture;
			_sprite.FlipH = false;
		}
		else
		{
			_sprite.Texture = _frontTexture;
			_sprite.FlipH = direction.X < 0;
		}
	}
}
