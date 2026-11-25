using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Something the player did that the world may remember later ("Gedächtnis", phase 4):
/// served a drink, let a guest wait, spilled a tray, stole, lied, fought …
/// Tags make it searchable: e.g. "fraktion:Soeldner", "spezies:ghel", "rezept:sandsturm".
/// </summary>
public sealed class Deed
{
	public int Week { get; set; }

	/// <summary>Kind of deed, e.g. "serviert", "warten_lassen", "verschuettet", "gestohlen", "gelogen".</summary>
	public string Kind { get; set; } = "";

	/// <summary>Short text for logs and later for NPC prompts.</summary>
	public string Text { get; set; } = "";

	public List<string> Tags { get; set; } = new List<string>();

	/// <summary>Who saw it (later: rumours, wanted level).</summary>
	public bool Witnessed { get; set; }
}

/// <summary>One line in the shift's cash book.</summary>
public sealed class LedgerEntry
{
	public string Text { get; set; } = "";

	public int Amount { get; set; }
}

public sealed class PlayerSnapshot
{
	public int Credits { get; set; }

	public List<InventorySlotSnapshot> Inventory { get; set; } = new List<InventorySlotSnapshot>();

	public List<string> Recipes { get; set; } = new List<string>();

	public List<Deed> Deeds { get; set; } = new List<Deed>();
}

/// <summary>Credits, inventory, known recipes and the deed log of the player.</summary>
public static class PlayerState
{
	public const int StartCredits = 50;

	private const int DeedLimit = 400;

	public static readonly Inventory Inventory = new Inventory();

	private static readonly HashSet<string> _recipes = new HashSet<string>();

	private static readonly List<Deed> _deeds = new List<Deed>();

	private static readonly List<LedgerEntry> _ledger = new List<LedgerEntry>();

	public static int Credits { get; private set; } = StartCredits;

	public static IReadOnlyCollection<string> KnownRecipes => _recipes;

	public static IReadOnlyList<Deed> Deeds => _deeds;

	/// <summary>Income and expenses of the current shift.</summary>
	public static IReadOnlyList<LedgerEntry> Ledger => _ledger;

	/// <summary>Credits changed (amount, reason).</summary>
	public static event Action<int, string> CreditsChanged;

	public static event Action<RecipeDef> RecipeDiscovered;

	static PlayerState()
	{
		Reset();
	}

	public static void AddCredits(int amount, string reason)
	{
		if (amount == 0)
		{
			return;
		}
		Credits = Math.Max(0, Credits + amount);
		_ledger.Add(new LedgerEntry { Text = reason, Amount = amount });
		CreditsChanged?.Invoke(amount, reason);
	}

	/// <summary>Pays if possible. False if the player cannot afford it.</summary>
	public static bool TrySpend(int amount, string reason)
	{
		if (amount <= 0)
		{
			return true;
		}
		if (Credits < amount)
		{
			return false;
		}
		AddCredits(-amount, reason);
		return true;
	}

	public static bool KnowsRecipe(string id) => _recipes.Contains(id);

	/// <summary>True if the recipe was new.</summary>
	public static bool LearnRecipe(RecipeDef recipe)
	{
		if (recipe == null || !_recipes.Add(recipe.Id))
		{
			return false;
		}
		RecipeDiscovered?.Invoke(recipe);
		return true;
	}

	public static void Record(string kind, string text, IEnumerable<string> tags = null, bool witnessed = false)
	{
		_deeds.Add(new Deed { Week = GameSession.Clock.Week, Kind = kind, Text = text, Tags = tags?.ToList() ?? new List<string>(), Witnessed = witnessed });
		if (_deeds.Count > DeedLimit)
		{
			_deeds.RemoveAt(0);
		}
	}

	/// <summary>Takes the cash book of the shift (shown at shift end) and starts a new one.</summary>
	public static List<LedgerEntry> TakeLedger()
	{
		List<LedgerEntry> lines = new List<LedgerEntry>(_ledger);
		_ledger.Clear();
		return lines;
	}

	public static void Reset()
	{
		Credits = StartCredits;
		Inventory.Clear();
		_recipes.Clear();
		_deeds.Clear();
		_ledger.Clear();
		// Start with something to defend yourself.
		Inventory.TryAdd(new ItemStack { ItemId = "barhockerbein" });
		// Every barkeeper knows the basics of the house.
		foreach (string basic in new[] { "kaldor_glut", "raumfahrertee", "milchwolke" })
		{
			RecipeDef recipe = BarLibrary.Recipe(basic);
			if (recipe != null)
			{
				_recipes.Add(recipe.Id);
			}
		}
		CreditsChanged?.Invoke(0, "");
	}

	public static PlayerSnapshot Export()
	{
		return new PlayerSnapshot
		{
			Credits = Credits,
			Inventory = Inventory.Export(),
			Recipes = _recipes.ToList(),
			Deeds = _deeds.ToList()
		};
	}

	public static void Import(PlayerSnapshot snapshot)
	{
		if (snapshot == null)
		{
			Reset();
			return;
		}
		Credits = Math.Max(0, snapshot.Credits);
		Inventory.Import(snapshot.Inventory);
		_recipes.Clear();
		foreach (string id in snapshot.Recipes ?? new List<string>())
		{
			_recipes.Add(id);
		}
		_deeds.Clear();
		_deeds.AddRange(snapshot.Deeds ?? new List<Deed>());
		_ledger.Clear();
		CreditsChanged?.Invoke(0, "");
	}
}
