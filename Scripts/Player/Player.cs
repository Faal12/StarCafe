using Godot;
using Godot.Collections;

public partial class Player : CharacterBody2D
{
	private const float WalkFramesPerSecond = 8f;
	private const float BackBobHeight = 1.5f;

	[Export] public float Speed = 220f;
	[Export] private Sprite2D _sprite;
	[Export] private Camera2D _camera;
	[Export] public float CameraSmoothing = 7f;
	[Export] private Texture2D _idleFront;
	[Export] private Texture2D _idleBack;
	[Export] private Array<Texture2D> _walkFrames = new();

	public bool CanMove = true;

	/// <summary>Last direction the player walked in (for attacks without a mouse).</summary>
	public Vector2 Facing { get; private set; } = Vector2.Down;

	public Camera2D Camera => _camera;

	/// <summary>Speed multiplier (e.g. while swinging a weapon).</summary>
	public float SpeedFactor { get; set; } = 1f;

	private Vector2 _knockback;

	private float _walkClock;
	private float _spriteBaseY;

	public override void _Ready()
	{
		AddToGroup("Player");
		_spriteBaseY = _sprite?.Position.Y ?? 0f;

		if (_camera != null)
		{
			_camera.PositionSmoothingEnabled = true;
			_camera.PositionSmoothingSpeed = CameraSmoothing;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		_knockback = _knockback.Lerp(Vector2.Zero, Mathf.Clamp((float)delta * 10f, 0f, 1f));
		if (!CanMove)
		{
			Velocity = _knockback;
			MoveAndSlide();
			UpdateAnimation(Vector2.Zero, delta);
			return;
		}

		// WASD (actions in Project Settings → Input Map; physical keys, so it works on any keyboard layout).
		Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		if (direction != Vector2.Zero)
			Facing = direction.Normalized();

		// Shift: walk carefully (slower, steadier tray).
		Velocity = direction * Speed * SpeedFactor * (Input.IsActionPressed("walk_careful") ? 0.55f : 1f) + _knockback;
		MoveAndSlide();

		UpdateAnimation(direction, delta);
	}

	/// <summary>Pushes the player away (hit by a monster).</summary>
	public void Knock(Vector2 impulse) => _knockback += impulse;

	/// <summary>Moves the player instantly (elevator, fainting) and stops all motion.</summary>
	public void Teleport(Vector2 position)
	{
		GlobalPosition = position;
		_knockback = Vector2.Zero;
		Velocity = Vector2.Zero;
		_camera?.ResetSmoothing();
	}

	// The hand-drawn walk frames face right, so moving left mirrors them. Walking straight up shows
	// the back view (there is no back walk cycle, so it gets a small bob); standing still shows the front.
	private void UpdateAnimation(Vector2 direction, double delta)
	{
		if (_sprite == null)
			return;

		bool moving = direction != Vector2.Zero;
		bool movingUp = moving && direction.Y < 0 && Mathf.Abs(direction.Y) > Mathf.Abs(direction.X);

		if (Mathf.Abs(direction.X) > 0.01f)
			_sprite.FlipH = direction.X < 0;

		if (!moving)
		{
			_walkClock = 0f;
			_sprite.Texture = _idleFront;
			_sprite.FlipH = false;
			_sprite.Position = new Vector2(_sprite.Position.X, _spriteBaseY);
			return;
		}

		_walkClock += (float)delta * WalkFramesPerSecond;

		if (movingUp)
		{
			_sprite.Texture = _idleBack;
			_sprite.FlipH = false;
			float bob = Mathf.Abs(Mathf.Sin(_walkClock * Mathf.Pi / 2f)) * BackBobHeight;
			_sprite.Position = new Vector2(_sprite.Position.X, _spriteBaseY - bob);
			return;
		}

		_sprite.Position = new Vector2(_sprite.Position.X, _spriteBaseY);
		if (_walkFrames.Count > 0)
			_sprite.Texture = _walkFrames[(int)_walkClock % _walkFrames.Count];
	}
}
