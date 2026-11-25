using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A prop or character currently placed in the tavern (saved with the game).</summary>
public sealed class WorldItem
{
	public string Id { get; set; } = "";

	/// <summary>"prop" or "character".</summary>
	public string Kind { get; set; } = "prop";

	public string Spot { get; set; } = "";

	public float OffsetX { get; set; }

	public float OffsetY { get; set; }

	/// <summary>Exact world position (overrides the spot when set).</summary>
	public float? X { get; set; }

	public float? Y { get; set; }

	public string Texture { get; set; } = "";

	public string Label { get; set; } = "";

	public string Faction { get; set; } = "";

	public string Color { get; set; } = "";

	public float Scale { get; set; } = 1f;

	public string Appear { get; set; } = "fade";

	/// <summary>Week from which it is gone (0 = permanent).</summary>
	public int ExpiresWeek { get; set; }
}

/// <summary>Serializable form of <see cref="WorldState"/> in the save file.</summary>
public sealed class WorldSnapshot
{
	public List<string> Flags { get; set; } = new List<string>();

	public List<WorldItem> Items { get; set; } = new List<WorldItem>();
}

/// <summary>A one-off animation waiting to be played once the event window is closed.</summary>
public sealed record WorldAnimation(string Name, string Spot, string Color);

/// <summary>
/// What events changed in the tavern: story flags, placed props/characters, pending animations.
/// The tavern's <see cref="WorldStage"/> shows it.
/// </summary>
public sealed class WorldState
{
	private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, WorldItem> _items = new Dictionary<string, WorldItem>(StringComparer.OrdinalIgnoreCase);

	private readonly Queue<WorldAnimation> _animations = new Queue<WorldAnimation>();

	public IReadOnlyCollection<string> Flags => _flags;

	public IEnumerable<WorldItem> Items => _items.Values;

	/// <summary>Raised when props/characters were added or removed.</summary>
	public event Action Changed;

	public bool HasFlag(string flag)
	{
		return !string.IsNullOrWhiteSpace(flag) && _flags.Contains(flag.Trim());
	}

	public bool TryTakeAnimation(out WorldAnimation animation)
	{
		return _animations.TryDequeue(out animation);
	}

	/// <summary>Applies one effect. Returns a short description for the event summary, or null.</summary>
	public string Apply(WorldEffectDef effect, int week)
	{
		switch (effect.Type)
		{
		case "flag":
			_flags.Add(effect.Id.Trim());
			return null;
		case "removeFlag":
			_flags.Remove(effect.Id.Trim());
			return null;
		case "animation":
			_animations.Enqueue(new WorldAnimation(effect.Animation, effect.Spot, effect.Color));
			return null;
		case "remove":
			if (_items.TryGetValue(effect.Id, out WorldItem removed) && _items.Remove(effect.Id))
			{
				Changed?.Invoke();
				return "Weg: " + DisplayName(removed);
			}
			return null;
		case "prop":
		case "character":
			WorldItem item = new WorldItem
			{
				Id = effect.Id,
				Kind = effect.Type,
				Spot = effect.Spot,
				OffsetX = effect.OffsetX,
				OffsetY = effect.OffsetY,
				X = effect.X,
				Y = effect.Y,
				Texture = effect.Texture ?? "",
				Label = effect.Label ?? "",
				Faction = effect.Faction ?? "",
				Color = effect.Color ?? "",
				Scale = effect.Scale <= 0f ? 1f : effect.Scale,
				Appear = string.IsNullOrWhiteSpace(effect.Appear) ? "fade" : effect.Appear,
				ExpiresWeek = effect.Weeks > 0 ? week + effect.Weeks : 0
			};
			_items[item.Id] = item;
			Changed?.Invoke();
			string duration = effect.Weeks switch
			{
				0 => "",
				1 => " (diese Woche)",
				_ => $" ({effect.Weeks} Wochen)",
			};
			return "Neu im Schankraum: " + DisplayName(item) + duration;
		default:
			return null;
		}
	}

	/// <summary>Removes everything whose time is up. Called when a new week starts.</summary>
	public bool Expire(int currentWeek)
	{
		List<string> gone = _items.Values.Where(i => i.ExpiresWeek > 0 && currentWeek >= i.ExpiresWeek).Select(i => i.Id).ToList();
		foreach (string id in gone)
		{
			_items.Remove(id);
		}
		if (gone.Count > 0)
		{
			Changed?.Invoke();
		}
		return gone.Count > 0;
	}

	public void Reset()
	{
		_flags.Clear();
		_items.Clear();
		_animations.Clear();
		Changed?.Invoke();
	}

	public WorldSnapshot Export()
	{
		return new WorldSnapshot
		{
			Flags = _flags.ToList(),
			Items = _items.Values.ToList()
		};
	}

	public void Import(WorldSnapshot snapshot)
	{
		_flags.Clear();
		_items.Clear();
		_animations.Clear();
		if (snapshot != null)
		{
			foreach (string flag in snapshot.Flags ?? new List<string>())
			{
				_flags.Add(flag);
			}
			foreach (WorldItem item in snapshot.Items ?? new List<WorldItem>())
			{
				if (!string.IsNullOrWhiteSpace(item?.Id))
				{
					_items[item.Id] = item;
				}
			}
		}
		Changed?.Invoke();
	}

	private static string DisplayName(WorldItem item)
	{
		return string.IsNullOrWhiteSpace(item.Label) ? item.Id : item.Label;
	}
}

/// <summary>The one world state of the running game.</summary>
public static class WorldService
{
	public static readonly WorldState State = new WorldState();
}
