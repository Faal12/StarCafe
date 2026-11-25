using Godot;

/// <summary>
/// Base for monsters: health, hit flash + knockback, contact damage, death with slime drops.
/// Subclasses build their placeholder look and AI (<see cref="Think"/>).
/// Collision layer 2 (monsters) – they bump into walls (layer 1) but not into the player.
/// </summary>
public abstract partial class Enemy : CharacterBody2D
{
	public int MaxHealth { get; protected set; } = 4;

	public int Health { get; protected set; }

	/// <summary>Body radius for hits and contact damage.</summary>
	public float Radius { get; protected set; } = 14f;

	public float ContactDamage { get; protected set; } = 6f;

	/// <summary>Slime drops on death (min, max).</summary>
	public Vector2I Drops { get; protected set; } = new Vector2I(0, 1);

	public string DisplayName { get; protected set; } = "Monster";

	/// <summary>Hidden/armoured monsters cannot be hit.</summary>
	public virtual bool CanBeHit => !IsDead;

	public bool IsDead { get; private set; }

	public Vector2 HitCenter => GlobalPosition + new Vector2(0f, -Radius);

	public event System.Action<Enemy> Died;

	protected Node2D Body;

	protected PlayerCombat PlayerCombat => PlayerCombat.Instance;

	protected Player Player { get; private set; }

	private Vector2 _knockback;

	private double _contactCooldown;

	public override void _Ready()
	{
		Health = MaxHealth;
		AddToGroup("Enemy");
		CollisionLayer = 2;
		CollisionMask = 1;
		AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius * 0.8f }, Position = new Vector2(0f, -Radius * 0.6f) });
		Body = new Node2D();
		AddChild(Body);
		BuildLook();
		Player = GetTree().GetFirstNodeInGroup("Player") as Player;
	}

	protected abstract void BuildLook();

	/// <summary>Desired velocity this frame (without knockback).</summary>
	protected abstract Vector2 Think(double delta);

	public override void _PhysicsProcess(double delta)
	{
		if (IsDead)
		{
			return;
		}
		ZIndex = DeckManager.SortZ(GlobalPosition.Y);
		_knockback = _knockback.Lerp(Vector2.Zero, Mathf.Clamp((float)delta * 9f, 0f, 1f));
		Velocity = Think(delta) + _knockback;
		MoveAndSlide();
		_contactCooldown -= delta;
		if (ContactDamage > 0 && _contactCooldown <= 0 && Player != null && CanTouch)
		{
			if (Player.GlobalPosition.DistanceTo(GlobalPosition) < Radius + 16f)
			{
				_contactCooldown = 0.6;
				PlayerCombat?.TakeDamage(ContactDamage, GlobalPosition, DisplayName);
			}
		}
	}

	/// <summary>Can it hurt by touching right now?</summary>
	protected virtual bool CanTouch => true;

	protected float DistanceToPlayer => Player == null ? float.MaxValue : Player.GlobalPosition.DistanceTo(GlobalPosition);

	protected Vector2 DirectionToPlayer => Player == null ? Vector2.Zero : (Player.GlobalPosition - GlobalPosition).Normalized();

	public virtual void Hurt(int damage, Vector2 impulse)
	{
		if (!CanBeHit)
		{
			return;
		}
		Health -= damage;
		_knockback += impulse * KnockbackTaken;
		CombatFx.Flash(Body);
		CombatFx.FloatText(GetParent(), GlobalPosition, damage.ToString(), new Color(1f, 0.95f, 0.6f));
		CombatFx.Burst(GetParent(), HitCenter, new Color(0.5f, 1f, 0.45f), 6, 120f);
		if (Health <= 0)
		{
			Die();
		}
	}

	protected virtual float KnockbackTaken => 1f;

	protected virtual void Die()
	{
		IsDead = true;
		RemoveFromGroup("Enemy");
		CombatFx.Burst(GetParent(), HitCenter, new Color(0.45f, 1f, 0.4f), 22, 220f);
		int drops = (int)GD.RandRange(Drops.X, Drops.Y);
		for (int i = 0; i < drops; i++)
		{
			Pickup.Spawn(GetParent(), GlobalPosition, "alienschleim");
		}
		Died?.Invoke(this);
		Tween tween = CreateTween().SetParallel();
		tween.TweenProperty(this, "scale", new Vector2(1.4f, 0.2f), 0.18);
		tween.TweenProperty(this, "modulate:a", 0f, 0.2);
		tween.Chain().TweenCallback(Callable.From(QueueFree));
	}

	/// <summary>Blob placeholder: rounded body with glowing eyes.</summary>
	protected Panel Blob(Vector2 size, Color color, Vector2 offset, int eyes = 2)
	{
		Panel blob = new Panel { Size = size, Position = offset - size / 2f, MouseFilter = Control.MouseFilterEnum.Ignore };
		StyleBoxFlat style = new StyleBoxFlat { BgColor = color, BorderColor = color.Lightened(0.3f), ShadowColor = new Color(color, 0.5f), ShadowSize = 6 };
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll((int)(Mathf.Min(size.X, size.Y) / 2f));
		blob.AddThemeStyleboxOverride("panel", style);
		Body.AddChild(blob);
		for (int i = 0; i < eyes; i++)
		{
			float x = eyes == 1 ? 0f : (i == 0 ? -size.X * 0.18f : size.X * 0.18f);
			Panel eye = new Panel { Size = new Vector2(6f, 6f), Position = offset + new Vector2(x - 3f, -size.Y * 0.12f - 3f), MouseFilter = Control.MouseFilterEnum.Ignore };
			StyleBoxFlat eyeStyle = new StyleBoxFlat { BgColor = new Color(1f, 0.95f, 0.4f), ShadowColor = new Color(1f, 0.9f, 0.3f, 0.8f), ShadowSize = 4 };
			eyeStyle.SetCornerRadiusAll(3);
			eye.AddThemeStyleboxOverride("panel", eyeStyle);
			Body.AddChild(eye);
		}
		return blob;
	}
}

/// <summary>Spit from a Rohrkriecher.</summary>
public partial class Projectile : Node2D
{
	public Vector2 Velocity;

	public float Damage = 8f;

	public string Source = "Säurespucke";

	private double _life = 2.6;

	public override void _Ready()
	{
		Panel ball = new Panel { Size = new Vector2(12f, 12f), Position = new Vector2(-6f, -6f), MouseFilter = Control.MouseFilterEnum.Ignore };
		StyleBoxFlat style = new StyleBoxFlat { BgColor = new Color(0.7f, 1f, 0.3f), ShadowColor = new Color(0.6f, 1f, 0.3f, 0.8f), ShadowSize = 7 };
		style.SetCornerRadiusAll(6);
		ball.AddThemeStyleboxOverride("panel", style);
		AddChild(ball);
	}

	public override void _PhysicsProcess(double delta)
	{
		Position += Velocity * (float)delta;
		ZIndex = DeckManager.SortZ(GlobalPosition.Y + 30f);
		_life -= delta;
		Player player = GetTree().GetFirstNodeInGroup("Player") as Player;
		if (player != null && (player.GlobalPosition + new Vector2(0f, -28f)).DistanceTo(GlobalPosition) < 18f)
		{
			PlayerCombat.Instance?.TakeDamage(Damage, GlobalPosition, Source);
			CombatFx.Burst(GetParent(), GlobalPosition, new Color(0.7f, 1f, 0.3f), 10, 140f);
			QueueFree();
			return;
		}
		if (_life <= 0)
		{
			QueueFree();
		}
	}
}

/// <summary>An item lying on the floor; flies to the player when close (Stardew style).</summary>
public partial class Pickup : Node2D
{
	public string ItemId = "alienschleim";

	private double _age;

	private Vector2 _bounce;

	public static void Spawn(Node parent, Vector2 at, string itemId)
	{
		Pickup pickup = new Pickup { ItemId = itemId, GlobalPosition = at + new Vector2((float)GD.RandRange(-10, 10), -10f) };
		pickup._bounce = new Vector2((float)GD.RandRange(-120, 120), (float)GD.RandRange(-180, -90));
		parent.CallDeferred(Node.MethodName.AddChild, pickup);
	}

	public override void _Ready()
	{
		ItemDef def = ItemLibrary.Get(ItemId);
		Control icon = Holo.Icon(def?.Color, def?.Icon ?? "?", 18f);
		icon.Position = new Vector2(-9f, -18f);
		AddChild(icon);
	}

	public override void _PhysicsProcess(double delta)
	{
		_age += delta;
		ZIndex = DeckManager.SortZ(GlobalPosition.Y);
		if (_age < 0.45)
		{
			_bounce.Y += 520f * (float)delta;
			Position += _bounce * (float)delta;
			return;
		}
		Player player = GetTree().GetFirstNodeInGroup("Player") as Player;
		if (player == null)
		{
			return;
		}
		float distance = player.GlobalPosition.DistanceTo(GlobalPosition);
		if (distance < 90f)
		{
			GlobalPosition = GlobalPosition.MoveToward(player.GlobalPosition, (float)(360.0 * delta));
		}
		if (distance < 16f)
		{
			if (PlayerState.Inventory.TryAdd(new ItemStack { ItemId = ItemId }))
			{
				UiBus.Toast("+1 " + (ItemLibrary.Get(ItemId)?.Name ?? ItemId), new Color(0.6f, 1f, 0.6f));
				QueueFree();
			}
		}
	}
}
