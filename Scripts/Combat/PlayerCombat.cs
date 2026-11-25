using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Stardew-like combat for the player (child node of Player):
/// - Attack (left mouse = towards the cursor, Space = in walking direction) swings the weapon in the
///   selected hotbar slot (or fists) in an arc. Hits flash, knock back and show damage numbers.
/// - Health and energy; swinging costs energy, energy recovers when resting.
/// - Short invulnerability after being hit (flicker), knockback, small camera shake.
/// - Right mouse / F uses the selected item: drinks heal ("heilung" heals a lot).
/// - At 0 health you faint: wake up in your bed on the Wohndeck, lose some credits.
/// </summary>
public partial class PlayerCombat : Node
{
	public const float MaxHealth = 100f;

	public const float MaxEnergy = 100f;

	private const float SwingEnergy = 3f;

	private const float EnergyRegen = 9f;

	private const double InvulnerableTime = 0.8;

	private const float SwingArcDegrees = 75f;

	public static PlayerCombat Instance { get; private set; }

	public float Health { get; private set; } = MaxHealth;

	public float Energy { get; private set; } = MaxEnergy;

	public bool IsFainted { get; private set; }

	public event System.Action Fainted;

	private Player _player;

	private double _cooldown;

	private double _invulnerable;

	private double _restClock;

	public override void _Ready()
	{
		Instance = this;
		_player = GetParent<Player>();
		GameSession.ShiftStarted += RestoreFull;
	}

	public override void _ExitTree()
	{
		GameSession.ShiftStarted -= RestoreFull;
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public void RestoreFull()
	{
		Health = MaxHealth;
		Energy = MaxEnergy;
		IsFainted = false;
	}

	public override void _Process(double delta)
	{
		_cooldown -= delta;
		_restClock += delta;
		if (_restClock > 1.0)
		{
			Energy = Mathf.Min(MaxEnergy, Energy + EnergyRegen * (float)delta);
		}
		if (_invulnerable > 0)
		{
			_invulnerable -= delta;
			Sprite()?.Set("modulate", new Color(1f, 1f, 1f, ((int)(_invulnerable * 20) % 2 == 0) ? 0.35f : 1f));
			if (_invulnerable <= 0)
			{
				Sprite()?.Set("modulate", Colors.White);
			}
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (IsFainted || !_player.CanMove || DeckManager.IsTraveling)
		{
			return;
		}
		if (@event.IsActionPressed("attack") && !@event.IsEcho())
		{
			Vector2 direction = @event is InputEventMouseButton
				? (_player.GetGlobalMousePosition() - _player.GlobalPosition + new Vector2(0f, 30f)).Normalized()
				: _player.Facing;
			Swing(direction);
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionPressed("use_item") && !@event.IsEcho())
		{
			UseSelected();
			GetViewport().SetInputAsHandled();
		}
	}

	private ItemDef Weapon
	{
		get
		{
			ItemDef def = PlayerState.Inventory.SelectedStack?.Def;
			return def != null && def.IsWeapon ? def : null;
		}
	}

	private void Swing(Vector2 direction)
	{
		if (_cooldown > 0 || direction == Vector2.Zero)
		{
			return;
		}
		ItemDef weapon = Weapon;
		float damage = weapon?.Damage ?? 1;
		float reach = weapon?.Reach ?? 40f;
		float knock = weapon?.Knockback ?? 180f;
		float cooldown = weapon?.Cooldown ?? 0.35f;
		bool tired = Energy < SwingEnergy;
		if (tired)
		{
			damage = Mathf.Max(1f, damage * 0.5f);
			cooldown *= 1.8f;
		}
		else
		{
			Energy -= SwingEnergy;
		}
		_restClock = 0;
		_cooldown = cooldown;
		Vector2 origin = _player.GlobalPosition + new Vector2(0f, -28f);
		DrawArc(origin, direction, reach, weapon != null ? Holo.Accent : new Color(1f, 0.9f, 0.7f));
		bool hitAny = false;
		foreach (Node node in GetTree().GetNodesInGroup("Enemy").ToList())
		{
			if (node is not Enemy enemy || !enemy.CanBeHit)
			{
				continue;
			}
			Vector2 to = enemy.HitCenter - origin;
			float distance = to.Length();
			if (distance > reach + enemy.Radius)
			{
				continue;
			}
			if (distance > enemy.Radius && Mathf.RadToDeg(Mathf.Abs(direction.AngleTo(to))) > SwingArcDegrees)
			{
				continue;
			}
			enemy.Hurt((int)Mathf.Round(damage), to.Normalized() * knock);
			hitAny = true;
		}
		if (hitAny)
		{
			CombatFx.Shake(_player.Camera, 3f, 4);
		}
	}

	private void DrawArc(Vector2 origin, Vector2 direction, float reach, Color color)
	{
		List<Vector2> points = new List<Vector2>();
		int steps = 10;
		float half = Mathf.DegToRad(SwingArcDegrees);
		for (int i = 0; i <= steps; i++)
		{
			float a = -half + 2f * half * i / steps;
			points.Add(direction.Rotated(a) * reach);
		}
		for (int i = steps; i >= 0; i--)
		{
			float a = -half + 2f * half * i / steps;
			points.Add(direction.Rotated(a) * reach * 0.62f);
		}
		Polygon2D arc = new Polygon2D
		{
			Polygon = points.ToArray(),
			Color = new Color(color, 0.55f),
			GlobalPosition = origin,
			ZIndex = 4070,
			ZAsRelative = false
		};
		_player.GetParent().AddChild(arc);
		Tween tween = arc.CreateTween().SetParallel();
		tween.TweenProperty(arc, "rotation", direction.Angle() > 0 ? 0.25f : -0.25f, 0.14);
		tween.TweenProperty(arc, "modulate:a", 0f, 0.16);
		tween.Chain().TweenCallback(Callable.From(arc.QueueFree));
	}

	/// <summary>Damage from monsters, projectiles, sparks, steam …</summary>
	public void TakeDamage(float amount, Vector2 fromPosition, string source = "")
	{
		if (IsFainted || _invulnerable > 0 || amount <= 0)
		{
			return;
		}
		Health = Mathf.Max(0f, Health - amount);
		_invulnerable = InvulnerableTime;
		Vector2 away = (_player.GlobalPosition - fromPosition);
		_player.Knock((away == Vector2.Zero ? Vector2.Down : away.Normalized()) * 300f);
		CombatFx.FloatText(_player.GetParent(), _player.GlobalPosition, "-" + Mathf.RoundToInt(amount), new Color(1f, 0.35f, 0.3f), 18);
		CombatFx.Shake(_player.Camera, 6f);
		UiBus.NotifyPlayerHurt();
		if (Health <= 0f)
		{
			Faint(source);
		}
	}

	public void Heal(float health, float energy)
	{
		Health = Mathf.Min(MaxHealth, Health + health);
		Energy = Mathf.Min(MaxEnergy, Energy + energy);
		if (health > 0)
		{
			CombatFx.FloatText(_player.GetParent(), _player.GlobalPosition, "+" + Mathf.RoundToInt(health), new Color(0.45f, 1f, 0.55f), 16);
		}
	}

	private void UseSelected()
	{
		ItemStack stack = PlayerState.Inventory.SelectedStack;
		if (stack == null)
		{
			return;
		}
		if (stack.ItemId == "getraenk")
		{
			bool healing = (stack.Get("effekte") ?? "").Contains("heilung");
			PlayerState.Inventory.TakeSlot(PlayerState.Inventory.Selected);
			Heal(healing ? 60f : 12f, healing ? 40f : 20f);
			PlayerState.Record("getrunken", $"{stack.DisplayName} selbst getrunken.", new[] { "rezept:" + (stack.Get("recipe") ?? "") });
			UiBus.Toast($"{stack.DisplayName} getrunken.", new Color(0.55f, 1f, 0.7f));
		}
	}

	private void Faint(string source)
	{
		IsFainted = true;
		_player.CanMove = false;
		int loss = Mathf.Min(30, Mathf.RoundToInt(PlayerState.Credits * 0.2f));
		PlayerState.Record("ohnmacht", "Im Kampf zusammengebrochen" + (string.IsNullOrEmpty(source) ? "." : $" ({source})."), new[] { "ort:" + DeckManager.Current });
		Fainted?.Invoke();
		VoiceQueue.Clear();
		VoiceQueue.Enqueue(FlokiAdvisor.Name, "Chef? Chef! … Ich hole den Servicerobot. Halt still, ich bin nicht gebaut fürs Tragen.");
		GetTree().CreateTimer(1.0).Timeout += () =>
		{
			DeckManager.Travel(DeckManager.Wohndeck, WohnDeck.BedSpot, () =>
			{
				Health = MaxHealth * 0.6f;
				Energy = MaxEnergy * 0.6f;
				IsFainted = false;
				_invulnerable = 0;
				Sprite()?.Set("modulate", Colors.White);
				if (loss > 0)
				{
					PlayerState.AddCredits(-loss, "Ohnmacht – Flokis Bergungsgebühr");
				}
				UiBus.Toast("Du wachst in deinem Bett auf.", Holo.TextDim);
			});
		};
	}

	private CanvasItem Sprite() => _player.GetNodeOrNull<CanvasItem>("Sprite2D");
}
