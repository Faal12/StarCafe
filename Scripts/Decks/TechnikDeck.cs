using System.Collections.Generic;
using Godot;

/// <summary>
/// Lower deck (placeholder art): main generator, pipes, crates, toolbox, Floki's core and the
/// sealed door to the cargo hold. Monsters are spawned here by tasks (e.g. the blackout).
/// </summary>
public partial class TechnikDeck : Node2D
{
	public static TechnikDeck Instance { get; private set; }

	public static readonly Vector2 GeneratorCenter = new Vector2(900f, 3230f);

	public static readonly Vector2 BroodSpot = new Vector2(900f, 3365f);

	/// <summary>Where Rohrkriecher hide (pipe openings).</summary>
	public static readonly Vector2[] PipeSpots = { new Vector2(420f, 3440f), new Vector2(1380f, 3440f), new Vector2(1620f, 3820f) };

	/// <summary>Where Kabelfresser swarms start.</summary>
	public static readonly Vector2[] NestSpots = { new Vector2(700f, 3500f), new Vector2(1100f, 3520f), new Vector2(900f, 3800f), new Vector2(1450f, 3600f) };

	public Interactable GeneratorUse { get; private set; }

	private Panel _generatorLamp;

	private PointLight2D _generatorGlow;

	private readonly List<Node2D> _monsters = new List<Node2D>();

	public override void _Ready()
	{
		Instance = this;
		Rect2 room = DeckManager.DeckById(DeckManager.Technikdeck).Bounds;
		Color steel = new Color(0.12f, 0.13f, 0.15f);
		DeckBuilder.Floor(this, room, new Color(0.55f, 0.62f, 0.68f));
		DeckBuilder.Wall(this, new Rect2(room.Position, new Vector2(room.Size.X, 90f)), steel);
		DeckBuilder.Wall(this, new Rect2(room.Position.X, room.End.Y - 60f, room.Size.X, 60f), steel);
		DeckBuilder.Wall(this, new Rect2(room.Position.X, room.Position.Y, 36f, room.Size.Y), steel);
		DeckBuilder.Wall(this, new Rect2(room.End.X - 36f, room.Position.Y, 36f, room.Size.Y), steel);

		DeckBuilder.Elevator(this, new Rect2(40f, 3920f, 70f, 110f), new Vector2(130f, 3975f));

		// Pipes (decoration) – Rohrkriecher live in the openings.
		Color pipe = new Color(0.32f, 0.36f, 0.3f);
		DeckBuilder.Block(this, new Rect2(60f, 3095f, 1680f, 18f), pipe, "", solid: false);
		DeckBuilder.Block(this, new Rect2(409f, 3100f, 22f, 330f), pipe, "", solid: false);
		DeckBuilder.Block(this, new Rect2(1369f, 3100f, 22f, 330f), pipe, "", solid: false);
		DeckBuilder.Block(this, new Rect2(1609f, 3600f, 22f, 210f), pipe, "", solid: false);
		foreach (Vector2 spot in PipeSpots)
		{
			DeckBuilder.Block(this, new Rect2(spot - new Vector2(22f, 14f), new Vector2(44f, 22f)), new Color(0.08f, 0.09f, 0.08f), "", solid: false);
		}

		// Main generator.
		Node2D generator = DeckBuilder.Block(this, new Rect2(GeneratorCenter - new Vector2(130f, 75f), new Vector2(260f, 150f)), new Color(0.24f, 0.28f, 0.34f), "HAUPTGENERATOR", labelSize: 14);
		_generatorLamp = new Panel { Position = new Vector2(-12f, -64f), Size = new Vector2(24f, 12f), MouseFilter = Control.MouseFilterEnum.Ignore };
		generator.AddChild(_generatorLamp);
		_generatorGlow = DeckBuilder.Glow(this, GeneratorCenter + new Vector2(0f, 40f), new Color(0.4f, 0.9f, 1f), 4f, 0.8f);
		GeneratorUse = DeckBuilder.Use(this, GeneratorCenter + new Vector2(0f, 110f), "[E] Generator", 80f, new Vector2(0f, -40f));
		GeneratorUse.Used += () =>
		{
			if (!TaskService.IsActive)
			{
				UiBus.Toast("Der Generator brummt zufrieden.", Holo.TextDim);
			}
		};

		// Crates as cover.
		DeckBuilder.Block(this, new Rect2(560f, 3660f, 90f, 70f), new Color(0.42f, 0.33f, 0.22f), "KISTE", labelSize: 10);
		DeckBuilder.Block(this, new Rect2(1090f, 3640f, 120f, 80f), new Color(0.42f, 0.33f, 0.22f), "KISTE", labelSize: 10);
		DeckBuilder.Block(this, new Rect2(1310f, 3880f, 80f, 80f), new Color(0.42f, 0.33f, 0.22f), "KISTE", labelSize: 10);

		// Toolbox with the pipe wrench.
		DeckBuilder.Block(this, new Rect2(140f, 3560f, 80f, 50f), new Color(0.65f, 0.3f, 0.18f), "WERKZEUG", labelSize: 9);
		Interactable toolbox = DeckBuilder.Use(this, new Vector2(180f, 3650f), "[E] Werkzeugkiste", 70f);
		toolbox.IsAvailable = () => !WorldService.State.HasFlag("rohrzange_genommen");
		toolbox.Used += () =>
		{
			if (PlayerState.Inventory.TryAdd(new ItemStack { ItemId = "rohrzange" }))
			{
				WorldService.State.Apply(new WorldEffectDef { Type = "flag", Id = "rohrzange_genommen" }, GameSession.Clock.Week);
				UiBus.Toast("Rohrzange genommen – Werkzeug und Waffe.", Holo.Accent);
				VoiceQueue.Enqueue(FlokiAdvisor.Name, "Eine Rohrzange. Repariert Rohre. Und Meinungen.");
			}
			else
			{
				UiBus.Toast("Kein Platz im Inventar.", new Color(1f, 0.6f, 0.5f));
			}
		};

		// Floki's core.
		DeckBuilder.Block(this, new Rect2(1590f, 3220f, 130f, 170f), new Color(0.16f, 0.3f, 0.36f), "FLOKIS RECHENKERN", labelSize: 11);
		DeckBuilder.Glow(this, new Vector2(1655f, 3300f), new Color(0.4f, 0.95f, 1f), 2.5f, 0.9f);
		Interactable core = DeckBuilder.Use(this, new Vector2(1560f, 3430f), "[E] Rechenkern", 70f);
		core.Used += () => VoiceQueue.Enqueue(FlokiAdvisor.Name, "Ja, das bin ich. Nein, du darfst mich nicht anfassen. Ja, auch nicht mit der Rohrzange.");

		// Sealed door to the cargo hold (story goal, phase 5).
		DeckBuilder.Block(this, new Rect2(820f, 4000f, 160f, 40f), new Color(0.55f, 0.36f, 0.2f), "VERSIEGELT · FRACHTRAUM", labelSize: 10);
		DeckBuilder.Glow(this, new Vector2(900f, 3990f), new Color(1f, 0.6f, 0.3f), 1.5f, 0.6f);
		Interactable door = DeckBuilder.Use(this, new Vector2(900f, 3950f), "[E] Versiegelte Tür", 70f);
		door.Used += () =>
		{
			bool seal = WorldService.State.HasFlag("spant_geoeffnet");
			UiBus.Toast(seal ? "Das Lerchen-Siegel passt in die Vertiefung … aber das Schloss klemmt noch." : "Versiegelt. In der Mitte eine Vertiefung in Form einer Lerche.", Holo.Strong);
			VoiceQueue.Enqueue(FlokiAdvisor.Name, seal ? "Das Siegel passt! Jetzt fehlt nur noch … alles andere." : "Die Tür. Mein Lieblingsrätsel. Ich hasse sie.");
		};

		SetGeneratorBroken(!DeckManager.PowerOn);
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public void SetGeneratorBroken(bool broken)
	{
		StyleBoxFlat lamp = new StyleBoxFlat { BgColor = broken ? new Color(1f, 0.25f, 0.2f) : new Color(0.35f, 1f, 0.55f), ShadowColor = broken ? new Color(1f, 0.2f, 0.1f, 0.7f) : new Color(0.3f, 1f, 0.5f, 0.6f), ShadowSize = 8 };
		lamp.SetCornerRadiusAll(4);
		_generatorLamp.AddThemeStyleboxOverride("panel", lamp);
		_generatorGlow.Color = broken ? new Color(1f, 0.3f, 0.2f) : new Color(0.4f, 0.9f, 1f);
		_generatorGlow.Energy = broken ? 0.5f : 0.9f;
	}

	public void Track(Node2D monster)
	{
		_monsters.Add(monster);
		AddChild(monster);
	}

	public void ClearMonsters()
	{
		foreach (Node2D monster in _monsters)
		{
			if (IsInstanceValid(monster))
			{
				monster.QueueFree();
			}
		}
		_monsters.Clear();
	}
}
