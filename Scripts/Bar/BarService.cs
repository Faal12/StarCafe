using System.Linq;

/// <summary>Result of finishing a drink at the bar.</summary>
public sealed record ServeResult(bool Ok, string Message, ItemStack Drink, RecipeDef NewRecipe, GuestOrder Order);

/// <summary>
/// The bar's rules, independent of how the bar looks (logic only). Any bar screen – the counter view with physics,
/// or anything else the front end builds – works with a <see cref="DrinkMixer"/> and these methods.
/// </summary>
public static class BarService
{
	/// <summary>How many more portions of this ingredient may go into the mix (stock items like Alienschleim are limited).</summary>
	public static int Available(IngredientDef ingredient, DrinkMixer mixer)
	{
		if (ingredient == null)
		{
			return 0;
		}
		if (string.IsNullOrEmpty(ingredient.RequiresItem))
		{
			return int.MaxValue;
		}
		int used = mixer?.Ingredients.Count(i => i == ingredient.Id) ?? 0;
		return PlayerState.Inventory.Count(ingredient.RequiresItem) - used;
	}

	/// <summary>Adds one portion. False with a reason when the shaker is full or the stock is empty.</summary>
	public static bool TryAdd(DrinkMixer mixer, string ingredientId, out string reason)
	{
		reason = null;
		IngredientDef ingredient = BarLibrary.Ingredient(ingredientId);
		if (ingredient == null)
		{
			reason = "Unbekannte Zutat.";
			return false;
		}
		if (mixer.IsFull)
		{
			reason = $"Der Shaker ist voll (höchstens {DrinkMixer.MaxIngredients} Zutaten).";
			return false;
		}
		if (Available(ingredient, mixer) <= 0)
		{
			reason = ingredient.Name + " ist leer.";
			return false;
		}
		mixer.Add(ingredientId);
		return true;
	}

	/// <summary>Accepted orders that have no drink on the tray yet, oldest first.</summary>
	public static GuestOrder NextOrder() => OrderBook.Open.FirstOrDefault(o => !HasDrinkFor(o));

	/// <summary>A drink for this guest is already on the tray (inventory).</summary>
	public static bool HasDrinkFor(GuestOrder order)
	{
		return order != null && PlayerState.Inventory.All().Any(s => s.Stack.ItemId == "getraenk" && s.Stack.Get("fuer") == order.GuestId);
	}

	/// <summary>
	/// Finishes the mix: uses up stock items, pays ingredient costs (economy placeholder), learns a new recipe,
	/// puts the drink on the tray (inventory) for <paramref name="order"/> (may be null) and empties the mixer.
	/// </summary>
	public static ServeResult Finish(DrinkMixer mixer, GuestOrder order)
	{
		if (mixer.IsEmpty)
		{
			return new ServeResult(false, "Der Shaker ist leer.", null, null, order);
		}
		if (string.IsNullOrEmpty(mixer.Method))
		{
			return new ServeResult(false, "Erst zubereiten: schütteln, rühren, erhitzen oder auf Eis.", null, null, order);
		}
		if (!PlayerState.Inventory.HasFreeSlot)
		{
			return new ServeResult(false, "Kein Platz auf dem Tablett (Inventar voll).", null, null, order);
		}
		foreach (string id in mixer.Ingredients)
		{
			IngredientDef ingredient = BarLibrary.Ingredient(id);
			if (!string.IsNullOrEmpty(ingredient?.RequiresItem))
			{
				PlayerState.Inventory.TryRemove(ingredient.RequiresItem);
			}
			int cost = Economy.IngredientCost(ingredient);
			if (cost > 0)
			{
				PlayerState.AddCredits(-cost, "Einkauf: " + ingredient.Name);
			}
		}
		RecipeDef recipe = mixer.Recipe;
		bool isNew = recipe != null && PlayerState.LearnRecipe(recipe);
		ItemStack drink = mixer.MakeDrink(order);
		PlayerState.Inventory.TryAdd(drink);
		string message = (isNew ? "Neues Rezept: " + recipe.Name + "! " : "")
			+ drink.DisplayName + " steht auf dem Tablett" + (order != null ? " – für " + order.GuestName + "." : ".");
		if (isNew)
		{
			VoiceQueue.Enqueue(FlokiAdvisor.Name, "Ein neues Rezept. Ich trage es ein. Widerwillig. " + recipe.Name + ".");
		}
		mixer.Clear();
		return new ServeResult(true, message, drink, isNew ? recipe : null, order);
	}
}
