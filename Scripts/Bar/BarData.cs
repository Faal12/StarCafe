using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

// Data for the bar mini-game, loaded from res://Data/Bar/*.json (see Data/Bar/README.md).

/// <summary>Taste profile of a drink: three axes.</summary>
public readonly record struct Taste(int Waerme, int Suesse, int Staerke)
{
	public static Taste operator +(Taste a, Taste b) => new Taste(a.Waerme + b.Waerme, a.Suesse + b.Suesse, a.Staerke + b.Staerke);

	public override string ToString() => $"Wärme {Waerme} · Süße {Suesse} · Stärke {Staerke}";
}

public sealed class IngredientDef
{
	public string Id { get; set; } = "";

	public string Name { get; set; } = "";

	public string Description { get; set; } = "";

	public int Waerme { get; set; }

	public int Suesse { get; set; }

	public int Staerke { get; set; }

	public bool Alcohol { get; set; }

	public string Color { get; set; } = "#888888";

	/// <summary>Purchase price per portion. Placeholder: not charged yet (see Economy).</summary>
	public int Price { get; set; }

	/// <summary>If set, each use consumes one of this item from the inventory.</summary>
	public string RequiresItem { get; set; }

	public Taste Taste => new Taste(Waerme, Suesse, Staerke);
}

public sealed class MethodDef
{
	public string Id { get; set; } = "";

	public string Name { get; set; } = "";

	public string Description { get; set; } = "";

	public int Waerme { get; set; }

	public int Suesse { get; set; }

	public int Staerke { get; set; }

	public Taste Taste => new Taste(Waerme, Suesse, Staerke);
}

public sealed class RecipeDef
{
	public string Id { get; set; } = "";

	public string Name { get; set; } = "";

	public string Description { get; set; } = "";

	/// <summary>Exact ingredients (order does not matter, duplicates count).</summary>
	public List<string> Ingredients { get; set; } = new List<string>();

	public string Method { get; set; } = "";

	/// <summary>Base price in credits; the tip depends on it.</summary>
	public int Price { get; set; } = 8;

	/// <summary>Special effects for later systems: "wahrheit", "beruhigend", "heilung", "mutig".</summary>
	public List<string> Effects { get; set; } = new List<string>();

	/// <summary>Canonical key of ingredients + method for matching.</summary>
	public string Key => BarLibrary.MixKey(Ingredients, Method);
}

public sealed class SpeciesDef
{
	public string Id { get; set; } = "";

	public string Name { get; set; } = "";

	public int Weight { get; set; } = 1;

	public List<string> Roles { get; set; } = new List<string>();

	public string Description { get; set; } = "";

	public int? MaxStaerke { get; set; }

	public int? MinStaerke { get; set; }

	public int? MinSuesse { get; set; }

	public int? MaxWaerme { get; set; }

	public string TooStrong { get; set; }

	public string TooWeak { get; set; }

	public string TooBitter { get; set; }

	public string TooHot { get; set; }
}

public sealed class OrderTemplate
{
	/// <summary>"rezept" (a named drink), "geschmack" (taste limits) or "ueberraschung" (any known recipe).</summary>
	public string Kind { get; set; } = "rezept";

	public string Recipe { get; set; }

	public string Text { get; set; } = "";

	/// <summary>Revealed when the player asks ("Nachfragen").</summary>
	public string Hint { get; set; } = "";

	public Dictionary<string, int> Min { get; set; } = new Dictionary<string, int>();

	public Dictionary<string, int> Max { get; set; } = new Dictionary<string, int>();

	public bool NoAlcohol { get; set; }
}

public sealed class Reactions
{
	public List<string> Perfekt { get; set; } = new List<string>();

	public List<string> Gut { get; set; } = new List<string>();

	public List<string> Mittel { get; set; } = new List<string>();

	public List<string> Schlecht { get; set; } = new List<string>();
}

internal sealed class IngredientFile
{
	public List<IngredientDef> Ingredients { get; set; } = new List<IngredientDef>();
}

internal sealed class RecipeFile
{
	public List<MethodDef> Methods { get; set; } = new List<MethodDef>();

	public List<RecipeDef> Recipes { get; set; } = new List<RecipeDef>();
}

internal sealed class GuestFile
{
	public List<SpeciesDef> Species { get; set; } = new List<SpeciesDef>();

	public List<OrderTemplate> Orders { get; set; } = new List<OrderTemplate>();

	public Reactions Reactions { get; set; } = new Reactions();
}

/// <summary>Loads and validates the bar data once.</summary>
public static class BarLibrary
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	private static bool _loaded;

	public static List<IngredientDef> Ingredients { get; private set; } = new List<IngredientDef>();

	public static List<MethodDef> Methods { get; private set; } = new List<MethodDef>();

	public static List<RecipeDef> Recipes { get; private set; } = new List<RecipeDef>();

	public static List<SpeciesDef> Species { get; private set; } = new List<SpeciesDef>();

	public static List<OrderTemplate> Orders { get; private set; } = new List<OrderTemplate>();

	public static Reactions Reactions { get; private set; } = new Reactions();

	public static void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}
		_loaded = true;
		Ingredients = Load<IngredientFile>("res://Data/Bar/ingredients.json")?.Ingredients ?? new List<IngredientDef>();
		RecipeFile recipes = Load<RecipeFile>("res://Data/Bar/recipes.json");
		Methods = recipes?.Methods ?? new List<MethodDef>();
		Recipes = recipes?.Recipes ?? new List<RecipeDef>();
		GuestFile guests = Load<GuestFile>("res://Data/Bar/guests.json");
		Species = guests?.Species ?? new List<SpeciesDef>();
		Orders = guests?.Orders ?? new List<OrderTemplate>();
		Reactions = guests?.Reactions ?? new Reactions();
		Validate();
		GD.Print($"Bar geladen: {Ingredients.Count} Zutaten, {Recipes.Count} Rezepte, {Species.Count} Spezies, {Orders.Count} Bestellungen");
	}

	public static IngredientDef Ingredient(string id)
	{
		EnsureLoaded();
		return Ingredients.FirstOrDefault(i => i.Id == id);
	}

	public static MethodDef Method(string id)
	{
		EnsureLoaded();
		return Methods.FirstOrDefault(m => m.Id == id);
	}

	public static RecipeDef Recipe(string id)
	{
		EnsureLoaded();
		return Recipes.FirstOrDefault(r => r.Id == id);
	}

	public static SpeciesDef SpeciesById(string id)
	{
		EnsureLoaded();
		return Species.FirstOrDefault(s => s.Id == id);
	}

	/// <summary>Recipe for exactly these ingredients and method, or null.</summary>
	public static RecipeDef Match(IEnumerable<string> ingredients, string method)
	{
		EnsureLoaded();
		string key = MixKey(ingredients, method);
		return Recipes.FirstOrDefault(r => r.Key == key);
	}

	public static string MixKey(IEnumerable<string> ingredients, string method)
	{
		return string.Join("+", ingredients.OrderBy(i => i, StringComparer.Ordinal)) + "|" + method;
	}

	private static T Load<T>(string path) where T : class
	{
		if (!FileAccess.FileExists(path))
		{
			GD.PushWarning("Bar: Datei fehlt: " + path);
			return null;
		}
		try
		{
			return JsonSerializer.Deserialize<T>(FileAccess.GetFileAsString(path), Options);
		}
		catch (JsonException ex)
		{
			GD.PushWarning($"Bar: JSON-Fehler in {path}: {ex.Message}");
			return null;
		}
	}

	private static void Validate()
	{
		HashSet<string> keys = new HashSet<string>();
		foreach (RecipeDef recipe in Recipes.ToList())
		{
			string missing = recipe.Ingredients.FirstOrDefault(i => Ingredients.All(x => x.Id != i));
			if (missing != null || Methods.All(m => m.Id != recipe.Method))
			{
				GD.PushWarning($"Bar: Rezept \"{recipe.Id}\" hat unbekannte Zutat \"{missing}\" oder Methode \"{recipe.Method}\" – übersprungen.");
				Recipes.Remove(recipe);
				continue;
			}
			if (recipe.Ingredients.Count < 1 || recipe.Ingredients.Count > DrinkMixer.MaxIngredients)
			{
				GD.PushWarning($"Bar: Rezept \"{recipe.Id}\" braucht 1–{DrinkMixer.MaxIngredients} Zutaten – übersprungen.");
				Recipes.Remove(recipe);
				continue;
			}
			if (!keys.Add(recipe.Key))
			{
				GD.PushWarning($"Bar: Rezept \"{recipe.Id}\" hat dieselben Zutaten und Methode wie ein anderes – übersprungen.");
				Recipes.Remove(recipe);
			}
		}
		foreach (OrderTemplate order in Orders.ToList())
		{
			if (order.Kind == "rezept" && Recipe(order.Recipe) == null)
			{
				GD.PushWarning($"Bar: Bestellung \"{order.Text}\" verweist auf unbekanntes Rezept \"{order.Recipe}\" – übersprungen.");
				Orders.Remove(order);
			}
		}
	}
}
