using Godot;

/// <summary>
/// Blackout task: the power is off (whole ship dark, flashlight on). Aliens have taken over the
/// Technikdeck – a Brutknoten sits on the main generator. Go down by elevator, destroy it,
/// then repair the generator (needs the Rohrzange from the toolbox) with two mini-games:
/// Schaltkreis (route the current) and Druckventil (hold the pressure in the green zone).
/// Fainting fails the task.
/// </summary>
public sealed class GeneratorTask : GameTask
{
	private TaskStep _goDown;

	private TaskStep _killBrood;

	private TaskStep _repair;

	private Brutknoten _brood;

	private bool _repairing;

	public GeneratorTask()
	{
		Title = "Strom wiederherstellen";
	}

	public override void Start()
	{
		_goDown = Step("Mit dem Aufzug ins Technikdeck");
		_killBrood = Step("Brutknoten am Generator vernichten");
		_repair = Step("Generator reparieren (Rohrzange)");
		DeckManager.SetPower(false);
		TechnikDeck deck = TechnikDeck.Instance;
		if (deck == null)
		{
			GD.PushWarning("Aufgabe Generator: kein Technikdeck in der Szene.");
			Complete(false);
			return;
		}
		deck.SetGeneratorBroken(true);
		_brood = new Brutknoten { GlobalPosition = TechnikDeck.BroodSpot };
		_brood.Died += _ =>
		{
			_killBrood.Done = true;
			VoiceQueue.Enqueue(FlokiAdvisor.Name, "Der Knoten ist Matsch. Jetzt die Rohrzange an den Generator, Chef.");
		};
		deck.Track(_brood);
		foreach (Vector2 spot in TechnikDeck.NestSpots)
		{
			deck.Track(new Kabelfresser { GlobalPosition = spot });
		}
		foreach (Vector2 spot in TechnikDeck.PipeSpots)
		{
			deck.Track(new Rohrkriecher { GlobalPosition = spot });
		}
		deck.GeneratorUse.Used += OnGenerator;
		if (PlayerCombat.Instance != null)
		{
			PlayerCombat.Instance.Fainted += OnFainted;
		}
		VoiceQueue.Enqueue(FlokiAdvisor.Name, "Meine Sensoren im Technikdeck melden … Bewegung. Viel Bewegung. Nimm etwas Hartes mit, Chef.");
	}

	public override void Tick(double delta)
	{
		if (!_goDown.Done && DeckManager.Current == DeckManager.Technikdeck)
		{
			_goDown.Done = true;
			VoiceQueue.Enqueue(FlokiAdvisor.Name, "Willkommen im Keller. Die grünen Dinger sind nicht von mir.");
		}
	}

	private void OnGenerator()
	{
		if (Finished || _repairing)
		{
			return;
		}
		if (!_killBrood.Done)
		{
			InventoryHud.Toast("Der Brutknoten klebt am Generator – erst weg damit!", new Color(1f, 0.6f, 0.5f));
			return;
		}
		if (PlayerState.Inventory.Count("rohrzange") == 0)
		{
			InventoryHud.Toast("Ohne Rohrzange geht nichts. Die Werkzeugkiste steht am Aufzug.", Holo.Strong);
			VoiceQueue.Enqueue(FlokiAdvisor.Name, "Mit bloßen Händen? Mutig. Dumm, aber mutig. Werkzeugkiste, links hinten.");
			return;
		}
		_repairing = true;
		CircuitGame.Open(circuitOk =>
		{
			if (!circuitOk)
			{
				_repairing = false;
				return;
			}
			PressureGame.Open(pressureOk =>
			{
				_repairing = false;
				if (pressureOk)
				{
					_repair.Done = true;
					Complete(true);
				}
			});
		});
	}

	private void OnFainted()
	{
		Complete(false);
	}

	public override void Cleanup(bool success)
	{
		TechnikDeck deck = TechnikDeck.Instance;
		if (deck != null)
		{
			deck.GeneratorUse.Used -= OnGenerator;
			deck.SetGeneratorBroken(false);
			deck.ClearMonsters();
		}
		if (PlayerCombat.Instance != null)
		{
			PlayerCombat.Instance.Fainted -= OnFainted;
		}
		DeckManager.SetPower(true);
		WorldService.State.Apply(new WorldEffectDef { Type = "animation", Animation = "flash" }, GameSession.Clock.Week);
		VoiceQueue.Enqueue(FlokiAdvisor.Name, success
			? "Licht! Die restlichen Viecher kriechen zurück in die Rohre. Ich hasse Rohre."
			: "Notstrom an. Ich habe das geregelt. Wie immer. Bitte nicht klatschen.");
	}
}
