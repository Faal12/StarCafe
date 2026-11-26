using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

/// <summary>What the player can do in a conversation besides talking.</summary>
public enum ConversationAction
{
	Taeuschen,
	Einschuechtern,
	Bestehlen,
	Angreifen,
}

/// <summary>
/// Result of an action, ready for the UI.
/// <para><c>Narration</c>: what happened (narrator). <c>Step</c>: the speaker's reply, may carry the outcome
/// (<c>Step.Outcome != null</c> = the event is decided). <c>StartsBrawl</c>: close the event window –
/// the fight in the tavern starts as soon as it is closed.</para>
/// </summary>
public sealed record ActionReport(ConversationAction Action, bool Success, int Chance, string Narration, ConversationStep Step, bool StartsBrawl)
{
	public EventOutcomeDef Outcome => Step?.Outcome;
}

/// <summary>
/// Deceive, threaten, steal and attack inside event conversations (phase 3). Logic only – the event
/// window calls these methods and shows the <see cref="ActionReport"/>.
///
/// Every action is a gamble: the chance comes from the speaker (<c>wits</c>, <c>strength</c> in the event data),
/// from earlier failures, and – with AI – from how believable the AI finds the lie or threat.
/// Events can define their own outcome per action (<c>"action"</c>/<c>"actionResult"</c>); otherwise the general
/// outcomes below apply, so all events support all actions without extra data.
/// </summary>
public static class ConversationActions
{
	public const string Taeuschen = "taeuschen";
	public const string Einschuechtern = "einschuechtern";
	public const string Bestehlen = "bestehlen";
	public const string Angreifen = "angreifen";

	public const string Erfolg = "erfolg";
	public const string Fehlschlag = "fehlschlag";
	public const string Sieg = "sieg";
	public const string Niederlage = "niederlage";
	public const string Flucht = "flucht";

	public static readonly string[] Ids = { Taeuschen, Einschuechtern, Bestehlen, Angreifen };

	private static readonly RandomNumberGenerator Rng = CreateRng();

	public static string Id(ConversationAction action) => action switch
	{
		ConversationAction.Taeuschen => Taeuschen,
		ConversationAction.Einschuechtern => Einschuechtern,
		ConversationAction.Bestehlen => Bestehlen,
		_ => Angreifen,
	};

	/// <summary>Display name for buttons and logs.</summary>
	public static string Label(ConversationAction action) => action switch
	{
		ConversationAction.Taeuschen => "Täuschen",
		ConversationAction.Einschuechtern => "Einschüchtern",
		ConversationAction.Bestehlen => "Bestehlen",
		_ => "Angreifen",
	};

	/// <summary>Short explanation for tooltips.</summary>
	public static string Hint(ConversationAction action) => action switch
	{
		ConversationAction.Taeuschen => "Schreib eine Lüge ins Feld. Glaubt die Figur sie, gilt sie für sie als wahr. Durchschaut sie dich, wird sie misstrauisch und du verlierst eine Runde.",
		ConversationAction.Einschuechtern => "Schreib eine Drohung ins Feld. Starke Figuren lachen darüber – und werden wütend.",
		ConversationAction.Bestehlen => "Unauffällig in die Tasche greifen. Klappt es, merkt niemand etwas. Wirst du erwischt, ist das Gespräch meist vorbei.",
		_ => "Schlägerei im Schankraum. Gäste gehen nur k.o. – aber das Gastrecht leidet, und alle haben es gesehen.",
	};

	/// <summary>Deceive and threaten need the text from the input field.</summary>
	public static bool NeedsText(ConversationAction action) => action is ConversationAction.Taeuschen or ConversationAction.Einschuechtern;

	public static bool IsKnown(string id) => Ids.Contains(id);

	public static IEnumerable<string> Results(string action) =>
		action == Angreifen ? new[] { Sieg, Niederlage, Flucht } : new[] { Erfolg, Fehlschlag };

	public static bool IsKnownResult(string action, string result) => Results(action).Contains(result);

	/// <summary>Whether the action is possible right now; <paramref name="reason"/> explains why not.</summary>
	public static bool CanUse(EventConversation conversation, ConversationAction action, out string reason)
	{
		reason = "";
		if (conversation == null || conversation.IsFinished)
		{
			return false;
		}
		EventSpeaker speaker = conversation.Def.Speaker;
		if (speaker.Remote && action is ConversationAction.Bestehlen or ConversationAction.Angreifen)
		{
			reason = speaker.Name + " ist nicht hier im Raum.";
			return false;
		}
		if (action == ConversationAction.Bestehlen && conversation.PocketTried)
		{
			reason = "Du hast es schon versucht.";
			return false;
		}
		if (action == ConversationAction.Angreifen && !DeckManager.InTavern)
		{
			reason = "Nur im Schankraum.";
			return false;
		}
		return true;
	}

	// ───────────── Täuschen ─────────────

	public static async Task<ActionReport> DeceiveAsync(EventConversation c, string lie)
	{
		EventSpeaker speaker = c.Def.Speaker;
		int chance = speaker.Wits switch { 1 => 70, 2 => 50, _ => 30 };
		chance -= 15 * c.FailedLies;
		if (WordCount(lie) >= 8)
		{
			chance += 5;
		}
		int? ai = await c.RateAsync("diese Lüge", lie);
		chance = Math.Clamp(ai.HasValue ? (chance + ai.Value) / 2 : chance, 5, 95);
		bool ok = Roll(chance);
		string name = speaker.Name;
		if (ok)
		{
			c.AddFact($"Du glaubst dem Wirt, was er gerade gesagt hat: „{lie}“ – für dich ist das wahr.");
			PlayerState.Record("luege", $"{name} angelogen – geglaubt: „{Short(lie)}“", Tags(c, "geglaubt"));
			string narration = $"Täuschen ({chance} %): {name} kauft dir das ab.";
			EventOutcomeDef own = c.Def.FindActionOutcome(Taeuschen, Erfolg);
			if (own != null)
			{
				return Report(ConversationAction.Taeuschen, true, chance, narration, c.Conclude(own));
			}
			ConversationStep step = await c.SayAsync(lie, "(nickt langsam) … Wenn das so ist, ändert das einiges.");
			return Report(ConversationAction.Taeuschen, true, chance, narration, step);
		}
		c.FailedLies++;
		c.Anger++;
		c.AddPenalty();
		c.AddFact($"Der Wirt wollte dich gerade anlügen („{lie}“) – du hast es durchschaut. Du bist misstrauisch und verärgert und lässt ihn das spüren.");
		ChangeFaction(c, -2, "Lüge durchschaut: " + name);
		PlayerState.Record("luege", $"{name} angelogen – durchschaut: „{Short(lie)}“", Tags(c, "durchschaut"), witnessed: true);
		string failText = $"Täuschen ({chance} %): {name} durchschaut dich. Das kostet eine Runde.";
		EventOutcomeDef ownFail = c.Def.FindActionOutcome(Taeuschen, Fehlschlag);
		if (ownFail != null)
		{
			return Report(ConversationAction.Taeuschen, false, chance, failText, c.Conclude(ownFail));
		}
		if (c.Anger >= 2)
		{
			return Escalate(c, ConversationAction.Taeuschen, chance, failText);
		}
		ConversationStep reply = await c.SayAsync(lie, "(kneift die Augen zusammen) Für wie blöd hältst du mich, Wirt?");
		return Report(ConversationAction.Taeuschen, false, chance, failText, reply);
	}

	// ───────────── Einschüchtern ─────────────

	public static async Task<ActionReport> IntimidateAsync(EventConversation c, string threat)
	{
		EventSpeaker speaker = c.Def.Speaker;
		int chance = speaker.Strength switch { 1 => 75, 2 => 50, _ => 20 };
		if (EventLibrary.TryFaction(speaker.Faction, out Faction faction))
		{
			chance += ReputationService.State.Get(faction) / 5;
		}
		if (PlayerState.Inventory.SelectedStack?.Def?.IsWeapon == true)
		{
			chance += 10;
		}
		int brawlsWon = PlayerState.Deeds.Count(d => d.Kind == "schlaegerei" && d.Tags.Contains(Sieg));
		chance += Math.Min(24, brawlsWon * 8);
		chance -= 10 * c.Anger;
		int? ai = await c.RateAsync("diese Drohung", threat);
		chance = Math.Clamp(ai.HasValue ? (chance + ai.Value) / 2 : chance, 5, 95);
		bool ok = Roll(chance);
		string name = speaker.Name;
		if (ok)
		{
			c.AddFact($"Der Wirt hat dir gedroht („{threat}“) – und du nimmst ihn ernst. Du hast Angst vor ihm und gibst eher nach.");
			ReputationService.State.ChangeGastrecht(-1, "Drohung im Schankraum");
			PlayerState.Record("drohung", $"{name} eingeschüchtert: „{Short(threat)}“", Tags(c, "gewirkt"), witnessed: true);
			string narration = $"Einschüchtern ({chance} %): {name} weicht zurück.";
			EventOutcomeDef own = c.Def.FindActionOutcome(Einschuechtern, Erfolg);
			if (own != null)
			{
				return Report(ConversationAction.Einschuechtern, true, chance, narration, c.Conclude(own));
			}
			ConversationStep step = await c.SayAsync(threat, "(weicht einen Schritt zurück) Schon gut, schon gut … Was willst du?");
			return Report(ConversationAction.Einschuechtern, true, chance, narration, step);
		}
		c.Anger += speaker.Strength >= 3 ? 2 : 1;
		c.AddPenalty();
		c.AddFact($"Der Wirt hat versucht, dich einzuschüchtern („{threat}“). Das beeindruckt dich nicht – es macht dich wütend.");
		ReputationService.State.ChangeGastrecht(-2, "Drohung im Schankraum");
		ChangeFaction(c, -3, "Drohung gegen " + name);
		PlayerState.Record("drohung", $"{name} gedroht – ohne Wirkung: „{Short(threat)}“", Tags(c, "verpufft"), witnessed: true);
		string failText = $"Einschüchtern ({chance} %): {name} lässt sich nicht beeindrucken.";
		EventOutcomeDef ownFail = c.Def.FindActionOutcome(Einschuechtern, Fehlschlag);
		if (ownFail != null)
		{
			return Report(ConversationAction.Einschuechtern, false, chance, failText, c.Conclude(ownFail));
		}
		if (c.Anger >= 2)
		{
			return Escalate(c, ConversationAction.Einschuechtern, chance, failText);
		}
		ConversationStep reply = await c.SayAsync(threat, "(lacht kalt) Du drohst MIR? Versuch's doch.");
		return Report(ConversationAction.Einschuechtern, false, chance, failText, reply);
	}

	// ───────────── Bestehlen ─────────────

	public static ActionReport Steal(EventConversation c)
	{
		c.PocketTried = true;
		EventSpeaker speaker = c.Def.Speaker;
		int chance = Math.Clamp(speaker.Wits switch { 1 => 70, 2 => 50, _ => 30 } - 10 * c.Anger, 5, 90);
		bool ok = Roll(chance);
		string name = speaker.Name;
		if (ok)
		{
			List<string> loot = new List<string>();
			int credits = Rng.RandiRange(speaker.Pockets.CreditsMin, speaker.Pockets.CreditsMax);
			if (credits > 0)
			{
				PlayerState.AddCredits(credits, "Aus der Tasche von " + name);
				loot.Add("₡" + credits);
			}
			foreach (string itemId in speaker.Pockets.Items)
			{
				ItemDef item = ItemLibrary.Get(itemId);
				if (item == null)
				{
					continue;
				}
				loot.Add(PlayerState.Inventory.TryAdd(new ItemStack { ItemId = itemId })
					? item.Name
					: item.Name + " (kein Platz – liegen gelassen)");
			}
			string lootText = loot.Count > 0 ? string.Join(", ", loot) : "nichts – die Taschen sind leer";
			List<string> tags = Tags(c, "unbemerkt");
			tags.AddRange(speaker.Pockets.Items.Select(i => "beute:" + i));
			PlayerState.Record("diebstahl", $"{name} bestohlen: {lootText}.", tags);
			string narration = $"Bestehlen ({chance} %): Unbemerkt wandert {lootText} in deine Tasche.";
			EventOutcomeDef own = c.Def.FindActionOutcome(Bestehlen, Erfolg);
			return Report(ConversationAction.Bestehlen, true, chance, narration, own != null ? c.Conclude(own) : null);
		}
		c.Anger += 2;
		PlayerState.Record("diebstahl", $"Beim Bestehlen von {name} erwischt.", Tags(c, "erwischt"), witnessed: true);
		string failText = $"Bestehlen ({chance} %): {name} packt dein Handgelenk. „Finger weg!“ Alle drehen sich um.";
		EventOutcomeDef ownFail = c.Def.FindActionOutcome(Bestehlen, Fehlschlag);
		if (ownFail != null)
		{
			return Report(ConversationAction.Bestehlen, false, chance, failText, c.Conclude(ownFail));
		}
		if (speaker.Strength >= 2 && !speaker.Remote && Rng.Randf() < 0.6f)
		{
			return Escalate(c, ConversationAction.Bestehlen, chance, failText);
		}
		return Report(ConversationAction.Bestehlen, false, chance, failText, c.Conclude(GeneralOutcome(c.Def, "bestehlen_erwischt")));
	}

	// ───────────── Angreifen ─────────────

	public static ActionReport Attack(EventConversation c)
	{
		c.End();
		BrawlService.Queue(c.Def, startedByPlayer: true);
		string narration = $"Du gehst auf {c.Def.Speaker.Name} los. Stühle kippen, Gläser fliegen – Schlägerei!";
		return new ActionReport(ConversationAction.Angreifen, true, 0, narration, null, true);
	}

	/// <summary>The outcome after a brawl: the event's own (action "angreifen") or the general one.</summary>
	public static EventOutcomeDef BrawlOutcome(GameEventDef def, string result)
	{
		return def.FindActionOutcome(Angreifen, result) ?? GeneralOutcome(def, "angreifen_" + result);
	}

	/// <summary>Too much anger: strong speakers attack, weak ones storm off.</summary>
	private static ActionReport Escalate(EventConversation c, ConversationAction action, int chance, string narration)
	{
		EventSpeaker speaker = c.Def.Speaker;
		if (speaker.Strength >= 2 && !speaker.Remote && DeckManager.InTavern)
		{
			c.End();
			BrawlService.Queue(c.Def, startedByPlayer: false);
			return new ActionReport(action, false, chance, narration + $" Jetzt reicht es: {speaker.Name} springt auf und geht auf dich los!", null, true);
		}
		return Report(action, false, chance, narration + $" {speaker.Name} hat genug von dir.", c.Conclude(GeneralOutcome(c.Def, "eskaliert")));
	}

	/// <summary>
	/// General outcomes for actions – used when an event does not define its own.
	/// Keys: angreifen_sieg, angreifen_niederlage, angreifen_flucht, bestehlen_erwischt, eskaliert.
	/// </summary>
	public static EventOutcomeDef GeneralOutcome(GameEventDef def, string key)
	{
		EventSpeaker speaker = def.Speaker;
		string name = speaker.Name;
		string faction = EventLibrary.TryFaction(speaker.Faction, out _) ? speaker.Faction : null;
		EventOutcomeDef o = new EventOutcomeDef { Id = "_" + key, Rivalry = false };
		void Rep(int value)
		{
			if (faction != null)
			{
				o.Reputation[faction] = value;
			}
		}
		switch (key)
		{
		case "angreifen_sieg":
			o.Label = $"Schlägerei: {name} liegt k.o.";
			o.Gastrecht = -10;
			Rep(-8);
			o.Result = $"{name} geht zu Boden. Zwei Gäste schleifen die Gestalt zur Schleuse hinaus. Im Schankraum ist es auf einmal sehr still.";
			o.Floki = "Chef. Wir sind eine Taverne. Keine Arena. … Aber das war ein ordentlicher linker Haken.";
			o.Radio = "Knotensender: In der „Letzten Runde“ am Tresk-Knoten musste ein Gast nach einer Schlägerei hinausgetragen werden.";
			o.World.Add(BrokenChair());
			o.World.Add(new WorldEffectDef { Type = "flag", Id = "schlaegerei", Weeks = 2 });
			break;
		case "angreifen_flucht":
			o.Label = $"Schlägerei: {name} flieht";
			o.Gastrecht = -8;
			Rep(-6);
			o.Result = $"{name} stolpert rückwärts zur Schleuse und ist weg. An den Tischen wird getuschelt.";
			o.Floki = "Der kommt nicht wieder. Oder er kommt wieder – mit Freunden. Ich führe eine Liste.";
			o.World.Add(BrokenChair());
			o.World.Add(new WorldEffectDef { Type = "flag", Id = "schlaegerei", Weeks = 2 });
			break;
		case "angreifen_niederlage":
			o.Label = "Schlägerei: Du gehst k.o.";
			o.Gastrecht = -15;
			Rep(-2);
			o.Result = $"Als du wieder zu dir kommst, ist {name} längst weg – und alle Gäste haben gesehen, wie der Wirt auf dem Boden lag.";
			o.Floki = "Ich habe die Stelle markiert, an der du gelegen hast. Für die Versicherung. Wir haben keine Versicherung.";
			o.World.Add(BrokenChair());
			o.World.Add(new WorldEffectDef { Type = "flag", Id = "schlaegerei", Weeks = 2 });
			break;
		case "bestehlen_erwischt":
			o.Label = "Beim Stehlen erwischt";
			o.Gastrecht = -8;
			Rep(-6);
			o.Result = $"{name} reißt sich los, wirft ein paar Münzen auf den Tresen – „für den Dreck hier“ – und geht. Die Gäste rücken ihre Taschen zurecht.";
			o.Floki = "Chef. Die Kameras zeichnen auf. Ich auch. Ich sag ja nur.";
			o.World.Add(new WorldEffectDef { Type = "flag", Id = "dieb_erwischt", Weeks = 3 });
			break;
		default:
			o.Label = $"{name} geht wütend";
			o.Gastrecht = -3;
			Rep(-5);
			o.Result = $"{name} knallt das Glas auf den Tisch und verlässt die Taverne, ohne sich umzudrehen.";
			break;
		}
		return o;
	}

	private static WorldEffectDef BrokenChair() => new WorldEffectDef
	{
		Type = "prop",
		Id = "zerbrochener_stuhl",
		Label = "Zerbrochener Stuhl",
		Spot = "Mitte",
		OffsetX = (float)GD.RandRange(-80, 80),
		OffsetY = (float)GD.RandRange(-30, 30),
		Color = "#7a5a40",
		Weeks = 1,
		Appear = "drop",
	};

	private static ActionReport Report(ConversationAction action, bool success, int chance, string narration, ConversationStep step) =>
		new ActionReport(action, success, chance, narration, step, false);

	private static void ChangeFaction(EventConversation c, int value, string reason)
	{
		if (EventLibrary.TryFaction(c.Def.Speaker.Faction, out Faction faction))
		{
			ReputationService.State.Change(faction, value, reason, applyRivalry: false);
		}
	}

	/// <summary>Deed tags: who, which faction, which event, how it went.</summary>
	private static List<string> Tags(EventConversation c, string result)
	{
		List<string> tags = new List<string> { "opfer:" + c.Def.Speaker.Name, "ereignis:" + c.Def.Id, result };
		if (!string.IsNullOrEmpty(c.Def.Speaker.Faction))
		{
			tags.Add("fraktion:" + c.Def.Speaker.Faction);
		}
		return tags;
	}

	private static bool Roll(int chance)
	{
		// STARCAFE_ACTION_ROLL=hit|miss forces the result (tests).
		string forced = OS.GetEnvironment("STARCAFE_ACTION_ROLL");
		if (forced == "hit")
		{
			return true;
		}
		if (forced == "miss")
		{
			return false;
		}
		return Rng.RandiRange(1, 100) <= chance;
	}

	private static int WordCount(string text) => (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

	private static string Short(string text)
	{
		string t = (text ?? "").Trim();
		return t.Length <= 80 ? t : t.Substring(0, 77) + "…";
	}

	private static RandomNumberGenerator CreateRng()
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();
		return rng;
	}
}
