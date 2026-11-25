using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>What happened after an outcome was reached, ready to show in the event panel.</summary>
public sealed record EventResolution(string Result, string EffectsLine, string Floki, bool HasRadio, string WorldLine);

/// <summary>Picks eligible events and applies the effects of a choice.</summary>
public static class EventService
{
	public static readonly EventState State = new EventState();

	private static readonly RandomNumberGenerator Rng = CreateRng();

	/// <summary>How many events one shift brings, from the "Konflikte" setting; +1 during a conjunction.</summary>
	public static int EventsPerShift(bool conjunction)
	{
		int count = GameSettings.Conflicts switch
		{
			ConflictFrequency.Selten => 1,
			ConflictFrequency.Oft => 3,
			_ => 2,
		};
		return conjunction ? count + 1 : count;
	}

	/// <summary>Chance that Floki's optional lines are shown, from the "Kommentare" setting.</summary>
	public static bool ShouldFlokiComment()
	{
		float chance = GameSettings.FlokiComments switch
		{
			CommentFrequency.Aus => 0f,
			CommentFrequency.Selten => 0.35f,
			CommentFrequency.Oft => 1f,
			_ => 0.75f,
		};
		return chance > 0f && Rng.Randf() < chance;
	}

	public static bool IsEligible(GameEventDef def, bool ignoreCooldown = false)
	{
		GameClock clock = GameSession.Clock;
		ReputationState rep = ReputationService.State;
		int week = clock.Week;
		if (week < def.MinWeek || (def.MaxWeek > 0 && week > def.MaxWeek))
		{
			return false;
		}
		string conj = def.Conjunction?.ToLowerInvariant() ?? "egal";
		if ((conj == "nur" && !clock.ConjunctionShift) || (conj == "nie" && clock.ConjunctionShift))
		{
			return false;
		}
		if (!ignoreCooldown)
		{
			if (def.Once && State.TimesHappened(def.Id) > 0)
			{
				return false;
			}
			int? last = State.LastWeek(def.Id);
			if (last.HasValue && week - last.Value < Math.Max(1, def.CooldownWeeks))
			{
				return false;
			}
		}
		if (rep.Gastrecht < def.MinGastrecht || rep.Gastrecht > def.MaxGastrecht)
		{
			return false;
		}
		foreach (KeyValuePair<string, int> pair in def.MinReputation)
		{
			if (EventLibrary.TryFaction(pair.Key, out Faction f) && rep.Get(f) < pair.Value)
			{
				return false;
			}
		}
		foreach (KeyValuePair<string, int> pair in def.MaxReputation)
		{
			if (EventLibrary.TryFaction(pair.Key, out Faction f) && rep.Get(f) > pair.Value)
			{
				return false;
			}
		}
		foreach (string name in def.RequiresAlive)
		{
			if (EventLibrary.TryFaction(name, out Faction f) && !rep.IsKeyFigureAlive(f))
			{
				return false;
			}
		}
		WorldState world = WorldService.State;
		if (def.RequiresFlags.Any(flag => !world.HasFlag(flag)) || def.BlockedByFlags.Any(world.HasFlag))
		{
			return false;
		}
		return true;
	}

	/// <summary>Weighted random pick among eligible events; null if none fits.</summary>
	public static GameEventDef Pick(bool ignoreCooldown = false, ISet<string> exclude = null)
	{
		List<GameEventDef> pool = EventLibrary.Events
			.Where(e => (exclude == null || !exclude.Contains(e.Id)) && IsEligible(e, ignoreCooldown))
			.ToList();
		if (pool.Count == 0)
		{
			return null;
		}
		int roll = Rng.RandiRange(1, pool.Sum(e => e.Weight));
		foreach (GameEventDef def in pool)
		{
			roll -= def.Weight;
			if (roll <= 0)
			{
				return def;
			}
		}
		return pool[^1];
	}

	/// <summary>Applies an outcome: standing, Gastrecht, key figure, radio, history.</summary>
	public static EventResolution Resolve(GameEventDef def, EventOutcomeDef choice)
	{
		ReputationState rep = ReputationService.State;
		int week = GameSession.Clock.Week;
		Dictionary<Faction, int> before = FactionInfo.All.ToDictionary(f => f, rep.Get);
		int gastrechtBefore = rep.Gastrecht;
		string reason = def.Title;
		foreach (KeyValuePair<string, int> pair in choice.Reputation)
		{
			if (EventLibrary.TryFaction(pair.Key, out Faction f) && pair.Value != 0)
			{
				rep.Change(f, pair.Value, reason, choice.Rivalry);
			}
		}
		if (choice.Gastrecht != 0)
		{
			rep.ChangeGastrecht(choice.Gastrecht, reason);
		}
		List<string> effects = new List<string>();
		if (!string.IsNullOrEmpty(choice.KillKeyFigure) && EventLibrary.TryFaction(choice.KillKeyFigure, out Faction dead) && rep.KillKeyFigure(dead, reason + ": " + FactionInfo.KeyFigure(dead) + " ist tot."))
		{
			effects.Add(FactionInfo.KeyFigure(dead) + " ist tot");
		}
		foreach (Faction f in FactionInfo.All)
		{
			int delta = rep.Get(f) - before[f];
			if (delta != 0)
			{
				effects.Add($"{FactionInfo.DisplayName(f)} {Signed(delta)}");
			}
		}
		int gastrechtDelta = rep.Gastrecht - gastrechtBefore;
		if (gastrechtDelta != 0)
		{
			effects.Add("Gastrecht " + Signed(gastrechtDelta));
		}
		State.RecordHappened(def.Id, week);
		bool hasRadio = !string.IsNullOrWhiteSpace(choice.Radio);
		if (hasRadio)
		{
			State.AddRadio(week, choice.Radio);
		}
		List<string> worldLines = new List<string>();
		foreach (WorldEffectDef effect in choice.World)
		{
			string line = WorldService.State.Apply(effect, week);
			if (line != null)
			{
				worldLines.Add(line);
			}
		}
		string floki = (!string.IsNullOrWhiteSpace(choice.Floki) && ShouldFlokiComment()) ? choice.Floki : null;
		return new EventResolution(choice.Result, effects.Count > 0 ? string.Join("  ·  ", effects) : "Keine Auswirkungen auf das Ansehen.", floki, hasRadio, worldLines.Count > 0 ? string.Join("\n", worldLines) : null);
	}

	private static string Signed(int value)
	{
		return value > 0 ? "+" + value : "−" + Math.Abs(value);
	}

	private static RandomNumberGenerator CreateRng()
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();
		return rng;
	}
}
