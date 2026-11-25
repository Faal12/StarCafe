using System.Collections.Generic;
using Godot;

/// <summary>
/// An outcome of a conversation can start a task (JSON "task" in an outcome):
/// { "id": "generator", "title": "Strom wiederherstellen", "timeLimit": 150,
///   "success": "strom_schnell", "late": "strom_spaet", "fail": "strom_ohnmacht" }
/// The task result picks the final outcome of the event (outcomes marked "taskOnly").
/// </summary>
public sealed class EventTaskDef
{
	public string Id { get; set; } = "";

	public string Title { get; set; } = "";

	/// <summary>Seconds; finishing later counts as "late". 0 = no limit.</summary>
	public float TimeLimit { get; set; }

	/// <summary>Outcome id when finished in time.</summary>
	public string Success { get; set; } = "";

	/// <summary>Outcome id when finished after the time limit (default: success).</summary>
	public string Late { get; set; }

	/// <summary>Outcome id when the task fails (e.g. you fainted).</summary>
	public string Fail { get; set; } = "";
}

public sealed class TaskStep
{
	public string Text { get; set; } = "";

	public bool Done { get; set; }
}

/// <summary>A running task: steps, timer, success/failure.</summary>
public abstract class GameTask
{
	public string Title { get; set; } = "";

	public List<TaskStep> Steps { get; } = new List<TaskStep>();

	public float TimeLimit { get; set; }

	public double Elapsed { get; private set; }

	public bool Finished { get; private set; }

	/// <summary>Set by the service: called once with success/failure.</summary>
	internal System.Action<bool> OnFinished;

	public abstract void Start();

	/// <summary>Called every frame while the game is not paused.</summary>
	public virtual void Tick(double delta)
	{
	}

	/// <summary>Removes monsters, restores power etc. Called after finishing.</summary>
	public virtual void Cleanup(bool success)
	{
	}

	internal void Advance(double delta)
	{
		if (Finished)
		{
			return;
		}
		Elapsed += delta;
		Tick(delta);
	}

	protected TaskStep Step(string text)
	{
		TaskStep step = new TaskStep { Text = text };
		Steps.Add(step);
		return step;
	}

	protected void Complete(bool success)
	{
		if (Finished)
		{
			return;
		}
		Finished = true;
		Cleanup(success);
		OnFinished?.Invoke(success);
	}

	/// <summary>Ends the task as failed (e.g. the shift ends).</summary>
	public void Abort() => Complete(false);
}

/// <summary>Runs at most one task at a time and turns its result into the event's final outcome.</summary>
public static class TaskService
{
	public static GameTask Active { get; private set; }

	public static bool IsActive => Active != null && !Active.Finished;

	/// <summary>Starts the task of an event outcome. False if the task id is unknown.</summary>
	public static bool Start(GameEventDef def, EventTaskDef taskDef)
	{
		GameTask task = Create(taskDef.Id);
		if (task == null || IsActive)
		{
			GD.PushWarning($"Aufgaben: unbekannte Aufgabe \"{taskDef.Id}\" oder schon eine aktiv.");
			return false;
		}
		task.Title = string.IsNullOrWhiteSpace(taskDef.Title) ? task.Title : taskDef.Title;
		task.TimeLimit = taskDef.TimeLimit;
		task.OnFinished = success =>
		{
			string outcomeId = !success ? taskDef.Fail : (task.TimeLimit > 0 && task.Elapsed > task.TimeLimit ? (taskDef.Late ?? taskDef.Success) : taskDef.Success);
			EventOutcomeDef outcome = def.FindOutcome(outcomeId);
			Active = null;
			if (outcome != null)
			{
				EventResolution resolution = EventService.Resolve(def, outcome);
				EventPanel.ShowAftermath(def, outcome, resolution);
			}
			PlayerState.Record(success ? "aufgabe_geschafft" : "aufgabe_gescheitert", task.Title + (success ? " geschafft." : " gescheitert."), new[] { "aufgabe:" + taskDef.Id, "ereignis:" + def.Id });
		};
		Active = task;
		task.Start();
		return true;
	}

	public static void Update(double delta)
	{
		if (IsActive && delta > 0)
		{
			Active.Advance(delta);
		}
	}

	/// <summary>Known task types. New task kinds are added here.</summary>
	private static GameTask Create(string id)
	{
		return id switch
		{
			"generator" => new GeneratorTask(),
			_ => null,
		};
	}

	public static bool IsKnown(string id) => Create(id) != null;
}
