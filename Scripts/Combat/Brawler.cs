using Godot;

/// <summary>
/// A guest in a tavern brawl. Same combat rules as monsters (hit, knockback, contact damage),
/// but guests never die: at 0 health they go down k.o. and are carried out. Weak ones may flee
/// to the airlock when they are losing.
/// Approaches, winds up (shakes – step aside!) and throws a punch; only the punch hurts.
/// </summary>
public partial class Brawler : Enemy
{
	private enum Mode { Approach, Windup, Punch, Recover, Flee, Down }

	private Mode _mode = Mode.Approach;

	private double _timer = 0.6;

	private Vector2 _punchDirection;

	private bool _triedToFlee;

	private string _faction = "";

	private int _strength = 2;

	/// <summary>Lies on the floor (counts as beaten).</summary>
	public bool KnockedOut { get; private set; }

	/// <summary>Ran out through the airlock.</summary>
	public bool Fled { get; private set; }

	/// <summary>Joined to help the main opponent.</summary>
	public bool IsAlly { get; private set; }

	public bool OutOfFight => KnockedOut || Fled;

	public static Brawler Create(string name, string faction, int strength, bool ally)
	{
		Brawler brawler = new Brawler { _faction = faction ?? "", _strength = Mathf.Clamp(strength, 1, 3), IsAlly = ally };
		brawler.DisplayName = name;
		brawler.MaxHealth = ally ? 5 : brawler._strength switch { 1 => 6, 2 => 10, _ => 16 };
		brawler.ContactDamage = brawler._strength switch { 1 => 6f, 2 => 9f, _ => 13f };
		brawler.Radius = 16f;
		brawler.Drops = Vector2I.Zero;
		return brawler;
	}

	protected override Color HitColor => new Color(1f, 0.85f, 0.55f);

	protected override bool CanTouch => _mode == Mode.Punch;

	protected override void BuildLook()
	{
		WorldStage.BuildCharacterPlaceholder(Body, new WorldItem { Label = DisplayName, Faction = _faction });
	}

	protected override Vector2 Think(double delta)
	{
		_timer -= delta;
		float speed = 95f + 15f * _strength;
		switch (_mode)
		{
		case Mode.Approach:
			Body.Position = Vector2.Zero;
			if (DistanceToPlayer < 72f && _timer <= 0)
			{
				_mode = Mode.Windup;
				_timer = 0.5 - 0.05 * _strength;
			}
			return DistanceToPlayer < 50f ? Vector2.Zero : DirectionToPlayer * speed;
		case Mode.Windup:
			Body.Position = new Vector2((float)GD.RandRange(-2.5, 2.5), 0f);
			if (_timer <= 0)
			{
				_mode = Mode.Punch;
				_timer = 0.18;
				_punchDirection = DirectionToPlayer;
			}
			return Vector2.Zero;
		case Mode.Punch:
			Body.Position = Vector2.Zero;
			if (_timer <= 0)
			{
				_mode = Mode.Recover;
				_timer = 0.75;
			}
			return _punchDirection * 320f;
		case Mode.Recover:
			if (_timer <= 0)
			{
				_mode = Mode.Approach;
				_timer = GD.RandRange(0.2, 0.7);
			}
			return -DirectionToPlayer * 30f;
		case Mode.Flee:
			Vector2 to = GuestDirector.Entrance - GlobalPosition;
			if (to.Length() < 30f)
			{
				Escape();
				return Vector2.Zero;
			}
			return to.Normalized() * 175f;
		default:
			return Vector2.Zero;
		}
	}

	public override void Hurt(int damage, Vector2 impulse)
	{
		base.Hurt(damage, impulse);
		if (OutOfFight || IsDead || _triedToFlee || Health > MaxHealth * 0.3f)
		{
			return;
		}
		_triedToFlee = true;
		if (_strength == 1 || GD.Randf() < 0.3f)
		{
			_mode = Mode.Flee;
			CombatFx.FloatText(GetParent(), GlobalPosition, "Flucht!", new Color(1f, 0.9f, 0.6f), 16);
		}
	}

	/// <summary>Guests are never killed: they go down k.o. and are carried out after a moment.</summary>
	protected override void Die()
	{
		IsDead = true;
		KnockedOut = true;
		_mode = Mode.Down;
		RemoveFromGroup("Enemy");
		CombatFx.FloatText(GetParent(), GlobalPosition, "k.o.", new Color(1f, 0.95f, 0.7f), 20);
		Tween fall = CreateTween();
		fall.TweenProperty(Body, "rotation", Mathf.Pi / 2f, 0.25).SetTrans(Tween.TransitionType.Back);
		fall.TweenProperty(Body, "modulate", new Color(0.7f, 0.7f, 0.7f), 0.2);
		fall.TweenInterval(2.5);
		fall.TweenProperty(this, "modulate:a", 0f, 0.6);
		fall.TweenCallback(Callable.From(QueueFree));
	}

	/// <summary>Leaves the fight (fled, or gave up after the main opponent went down).</summary>
	public void Escape()
	{
		if (OutOfFight)
		{
			return;
		}
		Fled = true;
		IsDead = true;
		_mode = Mode.Down;
		RemoveFromGroup("Enemy");
		Tween fade = CreateTween();
		fade.TweenProperty(this, "modulate:a", 0f, 0.5);
		fade.TweenCallback(Callable.From(QueueFree));
	}
}
