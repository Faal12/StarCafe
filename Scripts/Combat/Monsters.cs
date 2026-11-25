using Godot;

/// <summary>
/// Kabelfresser – small, fast cable-eaters that come in swarms. They chase you, shiver briefly
/// (warning!) and then lunge. Step aside after the shiver.
/// </summary>
public partial class Kabelfresser : Enemy
{
	private enum Mode { Wander, Chase, Windup, Lunge, Recover }

	private Mode _mode = Mode.Wander;

	private double _timer;

	private Vector2 _wander;

	private Vector2 _lungeDirection;

	public Kabelfresser()
	{
		MaxHealth = 4;
		Radius = 13f;
		ContactDamage = 6f;
		Drops = new Vector2I(0, 1);
		DisplayName = "Kabelfresser";
	}

	protected override void BuildLook()
	{
		Blob(new Vector2(30f, 22f), new Color(0.18f, 0.32f, 0.16f), new Vector2(0f, -12f));
	}

	protected override Vector2 Think(double delta)
	{
		_timer -= delta;
		float distance = DistanceToPlayer;
		switch (_mode)
		{
		case Mode.Wander:
			if (distance < 330f)
			{
				_mode = Mode.Chase;
			}
			if (_timer <= 0)
			{
				_timer = GD.RandRange(0.8, 1.8);
				_wander = Vector2.FromAngle((float)GD.RandRange(0, Mathf.Tau)) * 40f;
			}
			return _wander;
		case Mode.Chase:
			if (distance > 480f)
			{
				_mode = Mode.Wander;
			}
			if (distance < 115f && _timer <= 0)
			{
				_mode = Mode.Windup;
				_timer = 0.38;
			}
			Body.Position = Vector2.Zero;
			return DirectionToPlayer * 92f;
		case Mode.Windup:
			Body.Position = new Vector2((float)GD.RandRange(-2.5, 2.5), 0f);
			if (_timer <= 0)
			{
				_mode = Mode.Lunge;
				_timer = 0.24;
				_lungeDirection = DirectionToPlayer;
			}
			return Vector2.Zero;
		case Mode.Lunge:
			Body.Position = Vector2.Zero;
			if (_timer <= 0)
			{
				_mode = Mode.Recover;
				_timer = 0.7;
			}
			return _lungeDirection * 400f;
		default:
			if (_timer <= 0)
			{
				_mode = Mode.Chase;
				_timer = GD.RandRange(0.4, 1.0);
			}
			return DirectionToPlayer * 20f;
		}
	}
}

/// <summary>
/// Rohrkriecher – lives in pipe openings. Pops out when you come close, charges (glows) and spits
/// acid, then hides again. Only vulnerable while outside.
/// </summary>
public partial class Rohrkriecher : Enemy
{
	private enum Mode { Hidden, Emerging, Charging, Exposed, Hiding }

	private Mode _mode = Mode.Hidden;

	private double _timer = 1.0;

	private Panel _head;

	public Rohrkriecher()
	{
		MaxHealth = 8;
		Radius = 17f;
		ContactDamage = 0f;
		Drops = new Vector2I(1, 2);
		DisplayName = "Rohrkriecher";
	}

	public override bool CanBeHit => base.CanBeHit && (_mode == Mode.Charging || _mode == Mode.Exposed);

	protected override float KnockbackTaken => 0f;

	protected override void BuildLook()
	{
		_head = Blob(new Vector2(28f, 40f), new Color(0.38f, 0.16f, 0.4f), new Vector2(0f, -20f));
		Body.Scale = new Vector2(1f, 0.05f);
		Body.Modulate = new Color(1f, 1f, 1f, 0.2f);
	}

	protected override Vector2 Think(double delta)
	{
		_timer -= delta;
		switch (_mode)
		{
		case Mode.Hidden:
			if (_timer <= 0 && DistanceToPlayer < 420f)
			{
				_mode = Mode.Emerging;
				_timer = 0.3;
				CreateTween().TweenProperty(Body, "scale", Vector2.One, 0.3);
				CreateTween().TweenProperty(Body, "modulate:a", 1f, 0.2);
			}
			break;
		case Mode.Emerging:
			if (_timer <= 0)
			{
				_mode = Mode.Charging;
				_timer = 0.65;
			}
			break;
		case Mode.Charging:
			Body.Modulate = new Color(1f + (float)(0.65 - _timer) * 2f, 1f, 1f);
			if (_timer <= 0)
			{
				Spit();
				Body.Modulate = Colors.White;
				_mode = Mode.Exposed;
				_timer = 1.3;
			}
			break;
		case Mode.Exposed:
			if (_timer <= 0)
			{
				_mode = Mode.Hiding;
				_timer = 0.3;
				CreateTween().TweenProperty(Body, "scale", new Vector2(1f, 0.05f), 0.3);
				CreateTween().TweenProperty(Body, "modulate:a", 0.2f, 0.3);
			}
			break;
		case Mode.Hiding:
			if (_timer <= 0)
			{
				_mode = Mode.Hidden;
				_timer = GD.RandRange(1.6, 2.6);
			}
			break;
		}
		return Vector2.Zero;
	}

	private void Spit()
	{
		if (Player == null)
		{
			return;
		}
		Vector2 from = GlobalPosition + new Vector2(0f, -30f);
		Vector2 to = Player.GlobalPosition + new Vector2(0f, -28f);
		GetParent().AddChild(new Projectile { GlobalPosition = from, Velocity = (to - from).Normalized() * 250f, Damage = 8f });
	}
}

/// <summary>
/// Brutknoten – a pulsing growth on the generator. Does not move, breeds Kabelfresser and
/// lets off a shockwave (glows first – get out of range!). Must die before the generator can be fixed.
/// </summary>
public partial class Brutknoten : Enemy
{
	private const float PulseRange = 95f;

	private double _breedTimer = 4.0;

	private double _pulseTimer = 3.0;

	private bool _charging;

	private int _alive;

	private Panel _core;

	private Line2D _ring;

	public Brutknoten()
	{
		MaxHealth = 28;
		Radius = 38f;
		ContactDamage = 8f;
		Drops = new Vector2I(3, 5);
		DisplayName = "Brutknoten";
	}

	protected override float KnockbackTaken => 0f;

	protected override void BuildLook()
	{
		_core = Blob(new Vector2(86f, 66f), new Color(0.3f, 0.45f, 0.12f), new Vector2(0f, -34f), eyes: 3);
		_ring = new Line2D { Width = 3f, DefaultColor = new Color(0.8f, 1f, 0.3f, 0f), Closed = true };
		for (int i = 0; i < 32; i++)
		{
			_ring.AddPoint(Vector2.FromAngle(Mathf.Tau * i / 32f) * PulseRange + new Vector2(0f, -20f));
		}
		AddChild(_ring);
		Tween breathe = CreateTween().SetLoops();
		breathe.TweenProperty(Body, "scale", new Vector2(1.06f, 0.94f), 0.7);
		breathe.TweenProperty(Body, "scale", new Vector2(0.96f, 1.05f), 0.7);
	}

	protected override Vector2 Think(double delta)
	{
		if (DistanceToPlayer > 700f)
		{
			return Vector2.Zero;
		}
		_breedTimer -= delta;
		_pulseTimer -= delta;
		if (_breedTimer <= 0)
		{
			_breedTimer = 7.0;
			if (_alive < 3)
			{
				Kabelfresser child = new Kabelfresser { GlobalPosition = GlobalPosition + Vector2.FromAngle((float)GD.RandRange(0, Mathf.Tau)) * 60f };
				_alive++;
				child.Died += _ => _alive--;
				TechnikDeck.Instance?.Track(child);
				CombatFx.Burst(GetParent(), child.GlobalPosition, new Color(0.5f, 1f, 0.4f), 10, 120f);
			}
		}
		if (!_charging && _pulseTimer <= 0.9)
		{
			_charging = true;
		}
		if (_charging)
		{
			float t = (float)Mathf.Clamp(1.0 - _pulseTimer / 0.9, 0, 1);
			_ring.DefaultColor = new Color(0.8f, 1f, 0.3f, 0.2f + 0.7f * t);
			Body.Modulate = new Color(1f + t, 1f + t * 0.5f, 1f);
		}
		if (_pulseTimer <= 0)
		{
			_charging = false;
			_pulseTimer = 3.6;
			Body.Modulate = Colors.White;
			_ring.DefaultColor = new Color(0.8f, 1f, 0.3f, 0f);
			CombatFx.Burst(GetParent(), GlobalPosition + new Vector2(0f, -20f), new Color(0.7f, 1f, 0.3f), 26, 260f);
			if (DistanceToPlayer < PulseRange)
			{
				PlayerCombat?.TakeDamage(12f, GlobalPosition, "Schockwelle");
			}
		}
		return Vector2.Zero;
	}
}
