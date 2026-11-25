using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

/// <summary>What a guest ordered (one order per guest).</summary>
public sealed class GuestOrder
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

	public string GuestId { get; set; } = "";

	public string GuestName { get; set; } = "";

	public string Faction { get; set; } = "";

	public SpeciesDef Species { get; set; }

	public OrderTemplate Template { get; set; }

	public bool HintRevealed { get; set; }

	public RecipeDef TargetRecipe => Template?.Kind == "rezept" ? BarLibrary.Recipe(Template.Recipe) : null;
}

/// <summary>How a served drink went down.</summary>
public sealed record DrinkRating(int Score, string Band, string Reaction, int Tip, bool SpeciesProblem);

/// <summary>Mixing logic: taste profile, recipe matching, rating against an order.</summary>
public sealed class DrinkMixer
{
	public const int MaxIngredients = 4;

	public const int AxisMax = 9;

	private static readonly RandomNumberGenerator Rng = CreateRng();

	private readonly List<string> _ingredients = new List<string>();

	public IReadOnlyList<string> Ingredients => _ingredients;

	public string Method { get; set; }

	public bool IsFull => _ingredients.Count >= MaxIngredients;

	public bool IsEmpty => _ingredients.Count == 0;

	public bool CanFinish => _ingredients.Count > 0 && !string.IsNullOrEmpty(Method);

	public bool Add(string ingredientId)
	{
		if (IsFull || BarLibrary.Ingredient(ingredientId) == null)
		{
			return false;
		}
		_ingredients.Add(ingredientId);
		return true;
	}

	public void RemoveAt(int index)
	{
		if (index >= 0 && index < _ingredients.Count)
		{
			_ingredients.RemoveAt(index);
		}
	}

	public void Clear()
	{
		_ingredients.Clear();
		Method = null;
	}

	public Taste Profile => ProfileOf(_ingredients, Method);

	public bool HasAlcohol => _ingredients.Any(id => BarLibrary.Ingredient(id)?.Alcohol == true);

	public RecipeDef Recipe => CanFinish ? BarLibrary.Match(_ingredients, Method) : null;

	public static Taste ProfileOf(IEnumerable<string> ingredients, string method)
	{
		Taste sum = new Taste(0, 0, 0);
		foreach (string id in ingredients)
		{
			IngredientDef ingredient = BarLibrary.Ingredient(id);
			if (ingredient != null)
			{
				sum += ingredient.Taste;
			}
		}
		MethodDef m = BarLibrary.Method(method ?? "");
		if (m != null)
		{
			sum += m.Taste;
		}
		return new Taste(Math.Clamp(sum.Waerme, 0, AxisMax), Math.Clamp(sum.Suesse, 0, AxisMax), Math.Clamp(sum.Staerke, 0, AxisMax));
	}

	public static Taste RecipeProfile(RecipeDef recipe) => ProfileOf(recipe.Ingredients, recipe.Method);

	/// <summary>Turns the current mix into a drink item.</summary>
	public ItemStack MakeDrink(GuestOrder forOrder)
	{
		RecipeDef recipe = Recipe;
		Taste taste = Profile;
		ItemStack drink = new ItemStack { ItemId = "getraenk", Count = 1 };
		drink.Data["name"] = recipe?.Name ?? "Eigenkreation";
		drink.Data["recipe"] = recipe?.Id ?? "";
		drink.Data["waerme"] = taste.Waerme.ToString(CultureInfo.InvariantCulture);
		drink.Data["suesse"] = taste.Suesse.ToString(CultureInfo.InvariantCulture);
		drink.Data["staerke"] = taste.Staerke.ToString(CultureInfo.InvariantCulture);
		drink.Data["alkohol"] = HasAlcohol ? "1" : "0";
		drink.Data["effekte"] = recipe != null ? string.Join(",", recipe.Effects) : "";
		drink.Data["zutaten"] = string.Join(",", _ingredients);
		drink.Data["methode"] = Method ?? "";
		if (forOrder != null)
		{
			drink.Data["fuer"] = forOrder.GuestId;
			drink.Data["fuerName"] = forOrder.GuestName;
		}
		return drink;
	}

	public static Taste TasteOf(ItemStack drink)
	{
		return new Taste(ParseInt(drink.Get("waerme")), ParseInt(drink.Get("suesse")), ParseInt(drink.Get("staerke")));
	}

	/// <summary>Rates a served drink against the guest's order and species.</summary>
	public static DrinkRating Rate(GuestOrder order, ItemStack drink)
	{
		Taste taste = TasteOf(drink);
		string recipeId = drink.Get("recipe") ?? "";
		RecipeDef served = BarLibrary.Recipe(recipeId);
		bool alcohol = drink.Get("alkohol") == "1";
		OrderTemplate t = order.Template;
		int score;
		switch (t.Kind)
		{
		case "rezept":
		{
			RecipeDef target = order.TargetRecipe;
			if (target != null && recipeId == target.Id)
			{
				score = 100;
			}
			else
			{
				Taste want = target != null ? RecipeProfile(target) : taste;
				int distance = Math.Abs(want.Waerme - taste.Waerme) + Math.Abs(want.Suesse - taste.Suesse) + Math.Abs(want.Staerke - taste.Staerke);
				score = Math.Max(10, 75 - distance * 15);
			}
			break;
		}
		case "geschmack":
		{
			int penalty = 0;
			foreach ((string axis, int min) in t.Min)
			{
				int have = Axis(taste, axis);
				if (have < min)
				{
					penalty += 20 + (min - have) * 10;
				}
			}
			foreach ((string axis, int max) in t.Max)
			{
				int have = Axis(taste, axis);
				if (have > max)
				{
					penalty += 20 + (have - max) * 10;
				}
			}
			if (t.NoAlcohol && alcohol)
			{
				penalty += 60;
			}
			// A named classic that fits is a little better than a random mix.
			score = Math.Clamp((served != null ? 100 : 92) - penalty, 5, 100);
			break;
		}
		default:
			score = served != null ? 100 : 40;
			break;
		}
		string speciesLine = SpeciesProblem(order.Species, taste);
		if (speciesLine != null)
		{
			score = Math.Min(score, 25);
		}
		string band = score >= 90 ? "perfekt" : score >= 65 ? "gut" : score >= 40 ? "mittel" : "schlecht";
		string reaction = speciesLine ?? Pick(band);
		int price = served?.Price ?? 6;
		int tip = (int)Math.Round(price * score / 100.0) + (score >= 90 ? 2 : 0);
		return new DrinkRating(score, band, reaction, Math.Max(0, tip), speciesLine != null);
	}

	/// <summary>Species rule broken by this taste? Returns the guest's complaint, or null.</summary>
	public static string SpeciesProblem(SpeciesDef species, Taste taste)
	{
		if (species == null)
		{
			return null;
		}
		if (species.MaxStaerke.HasValue && taste.Staerke > species.MaxStaerke.Value)
		{
			return species.TooStrong ?? "Zu stark für mich.";
		}
		if (species.MinStaerke.HasValue && taste.Staerke < species.MinStaerke.Value)
		{
			return species.TooWeak ?? "Zu schwach für mich.";
		}
		if (species.MinSuesse.HasValue && taste.Suesse < species.MinSuesse.Value)
		{
			return species.TooBitter ?? "Zu bitter für mich.";
		}
		if (species.MaxWaerme.HasValue && taste.Waerme > species.MaxWaerme.Value)
		{
			return species.TooHot ?? "Zu heiß für mich.";
		}
		return null;
	}

	/// <summary>Can this species be satisfied by this order at all? (No impossible orders.)</summary>
	public static bool Compatible(OrderTemplate order, SpeciesDef species)
	{
		switch (order.Kind)
		{
		case "rezept":
		{
			RecipeDef recipe = BarLibrary.Recipe(order.Recipe);
			return recipe != null && SpeciesProblem(species, RecipeProfile(recipe)) == null;
		}
		case "geschmack":
			// At least one known recipe must satisfy both the order and the species.
			return BarLibrary.Recipes.Any(r =>
			{
				Taste taste = RecipeProfile(r);
				bool alcohol = r.Ingredients.Any(i => BarLibrary.Ingredient(i)?.Alcohol == true);
				return SpeciesProblem(species, taste) == null
					&& order.Min.All(p => Axis(taste, p.Key) >= p.Value)
					&& order.Max.All(p => Axis(taste, p.Key) <= p.Value)
					&& (!order.NoAlcohol || !alcohol);
			});
		default:
			return BarLibrary.Recipes.Any(r => SpeciesProblem(species, RecipeProfile(r)) == null);
		}
	}

	public static int Axis(Taste taste, string axis)
	{
		return axis?.ToLowerInvariant() switch
		{
			"waerme" => taste.Waerme,
			"suesse" => taste.Suesse,
			"staerke" => taste.Staerke,
			_ => 0,
		};
	}

	private static string Pick(string band)
	{
		List<string> lines = band switch
		{
			"perfekt" => BarLibrary.Reactions.Perfekt,
			"gut" => BarLibrary.Reactions.Gut,
			"mittel" => BarLibrary.Reactions.Mittel,
			_ => BarLibrary.Reactions.Schlecht,
		};
		return lines.Count == 0 ? "…" : lines[Rng.RandiRange(0, lines.Count - 1)];
	}

	private static int ParseInt(string text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

	private static RandomNumberGenerator CreateRng()
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();
		return rng;
	}
}
