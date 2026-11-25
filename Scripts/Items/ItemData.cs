using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

/// <summary>Item definition from res://Data/Items/items.json.</summary>
public sealed class ItemDef
{
	public string Id { get; set; } = "";

	public string Name { get; set; } = "";

	/// <summary>"Getraenk", "Zutat", "Waffe", "Werkzeug" or "Beweis".</summary>
	public string Category { get; set; } = "Werkzeug";

	public string Description { get; set; } = "";

	/// <summary>Max stack size (1 = not stackable).</summary>
	public int Stack { get; set; } = 1;

	/// <summary>Placeholder icon colour until there is art.</summary>
	public string Color { get; set; } = "#40c8e0";

	/// <summary>Placeholder icon text (1–2 letters).</summary>
	public string Icon { get; set; } = "?";

	/// <summary>Optional icon image (res://…png).</summary>
	public string Texture { get; set; } = "";

	/// <summary>Value in credits (selling/stealing – placeholder).</summary>
	public int Value { get; set; }

	/// <summary>Weapon damage (0 = not a weapon; fists do 1).</summary>
	public int Damage { get; set; }

	/// <summary>Seconds between swings.</summary>
	public float Cooldown { get; set; } = 0.4f;

	/// <summary>Swing reach in pixels.</summary>
	public float Reach { get; set; } = 48f;

	/// <summary>Knockback strength.</summary>
	public float Knockback { get; set; } = 260f;

	public bool IsWeapon => Damage > 0;
}

internal sealed class ItemFile
{
	public List<ItemDef> Items { get; set; } = new List<ItemDef>();
}

public static class ItemLibrary
{
	private static List<ItemDef> _items;

	public static IReadOnlyList<ItemDef> Items
	{
		get
		{
			if (_items == null)
			{
				Load();
			}
			return _items;
		}
	}

	public static ItemDef Get(string id)
	{
		return Items.FirstOrDefault(i => i.Id == id);
	}

	private static void Load()
	{
		_items = new List<ItemDef>();
		const string path = "res://Data/Items/items.json";
		try
		{
			ItemFile file = JsonSerializer.Deserialize<ItemFile>(FileAccess.GetFileAsString(path), new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				ReadCommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true
			});
			_items = file?.Items ?? new List<ItemDef>();
		}
		catch (Exception ex)
		{
			GD.PushWarning("Gegenstände: " + path + " nicht lesbar: " + ex.Message);
		}
	}
}

/// <summary>A stack of items in one inventory slot. Drinks carry their details in <see cref="Data"/>.</summary>
public sealed class ItemStack
{
	public string ItemId { get; set; } = "";

	public int Count { get; set; } = 1;

	/// <summary>Extra values, e.g. for drinks: name, recipe, taste, guest it was made for.</summary>
	public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();

	public ItemDef Def => ItemLibrary.Get(ItemId);

	public string DisplayName => Data.TryGetValue("name", out string name) && !string.IsNullOrEmpty(name) ? name : (Def?.Name ?? ItemId);

	public string Get(string key) => Data.TryGetValue(key, out string value) ? value : null;
}

/// <summary>24 slots; the first 8 are the hotbar (keys 1–8).</summary>
public sealed class Inventory
{
	public const int Size = 24;

	public const int HotbarSize = 8;

	private readonly ItemStack[] _slots = new ItemStack[Size];

	private int _selected;

	public event Action Changed;

	public int Selected
	{
		get => _selected;
		set
		{
			int clamped = Math.Clamp(value, 0, HotbarSize - 1);
			if (clamped != _selected)
			{
				_selected = clamped;
				Changed?.Invoke();
			}
		}
	}

	public ItemStack this[int slot] => slot >= 0 && slot < Size ? _slots[slot] : null;

	public ItemStack SelectedStack => _slots[_selected];

	public IEnumerable<(int Slot, ItemStack Stack)> All()
	{
		for (int i = 0; i < Size; i++)
		{
			if (_slots[i] != null)
			{
				yield return (i, _slots[i]);
			}
		}
	}

	public int Count(string itemId) => All().Where(s => s.Stack.ItemId == itemId).Sum(s => s.Stack.Count);

	/// <summary>Adds a stack (fills existing stacks first, then free slots). False if it does not fit.</summary>
	public bool TryAdd(ItemStack stack)
	{
		ItemDef def = stack.Def;
		int max = Math.Max(1, def?.Stack ?? 1);
		int left = stack.Count;
		if (max > 1 && stack.Data.Count == 0)
		{
			for (int i = 0; i < Size && left > 0; i++)
			{
				ItemStack s = _slots[i];
				if (s != null && s.ItemId == stack.ItemId && s.Data.Count == 0 && s.Count < max)
				{
					int add = Math.Min(max - s.Count, left);
					s.Count += add;
					left -= add;
				}
			}
		}
		while (left > 0)
		{
			int free = Array.FindIndex(_slots, s => s == null);
			if (free < 0)
			{
				Changed?.Invoke();
				return false;
			}
			int add = Math.Min(max, left);
			_slots[free] = new ItemStack { ItemId = stack.ItemId, Count = add, Data = new Dictionary<string, string>(stack.Data) };
			left -= add;
		}
		Changed?.Invoke();
		return true;
	}

	public bool HasFreeSlot => Array.Exists(_slots, s => s == null);

	/// <summary>Removes count items of a type. False if there are not enough.</summary>
	public bool TryRemove(string itemId, int count = 1)
	{
		if (Count(itemId) < count)
		{
			return false;
		}
		for (int i = Size - 1; i >= 0 && count > 0; i--)
		{
			ItemStack s = _slots[i];
			if (s != null && s.ItemId == itemId)
			{
				int take = Math.Min(s.Count, count);
				s.Count -= take;
				count -= take;
				if (s.Count <= 0)
				{
					_slots[i] = null;
				}
			}
		}
		Changed?.Invoke();
		return true;
	}

	public ItemStack TakeSlot(int slot)
	{
		ItemStack stack = this[slot];
		if (stack != null)
		{
			_slots[slot] = null;
			Changed?.Invoke();
		}
		return stack;
	}

	public void Swap(int a, int b)
	{
		if (a < 0 || b < 0 || a >= Size || b >= Size || a == b)
		{
			return;
		}
		(_slots[a], _slots[b]) = (_slots[b], _slots[a]);
		Changed?.Invoke();
	}

	public void Clear()
	{
		Array.Clear(_slots);
		_selected = 0;
		Changed?.Invoke();
	}

	public List<InventorySlotSnapshot> Export()
	{
		return All().Select(s => new InventorySlotSnapshot { Slot = s.Slot, ItemId = s.Stack.ItemId, Count = s.Stack.Count, Data = new Dictionary<string, string>(s.Stack.Data) }).ToList();
	}

	public void Import(List<InventorySlotSnapshot> slots)
	{
		Array.Clear(_slots);
		foreach (InventorySlotSnapshot s in slots ?? new List<InventorySlotSnapshot>())
		{
			if (s.Slot >= 0 && s.Slot < Size && !string.IsNullOrEmpty(s.ItemId))
			{
				_slots[s.Slot] = new ItemStack { ItemId = s.ItemId, Count = Math.Max(1, s.Count), Data = s.Data ?? new Dictionary<string, string>() };
			}
		}
		Changed?.Invoke();
	}
}

public sealed class InventorySlotSnapshot
{
	public int Slot { get; set; }

	public string ItemId { get; set; } = "";

	public int Count { get; set; } = 1;

	public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
}
