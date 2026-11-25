using Godot;

/// <summary>
/// Deck 2 – living quarters (placeholder): corridor, two staff rooms (locked until Act II),
/// the owner's room with the bed. Sleeping ends the shift; fainting wakes you up here.
/// </summary>
public partial class WohnDeck : Node2D
{
	public static readonly Vector2 BedSpot = new Vector2(1150f, 2330f);

	public override void _Ready()
	{
		Rect2 room = DeckManager.DeckById(DeckManager.Wohndeck).Bounds;
		Color steel = new Color(0.16f, 0.15f, 0.17f);
		DeckBuilder.Floor(this, room, new Color(0.85f, 0.75f, 0.65f));
		DeckBuilder.Wall(this, new Rect2(room.Position, new Vector2(room.Size.X, 90f)), steel);
		DeckBuilder.Wall(this, new Rect2(room.Position.X, room.End.Y - 40f, room.Size.X, 40f), steel);
		DeckBuilder.Wall(this, new Rect2(room.Position.X, room.Position.Y, 36f, room.Size.Y), steel);
		DeckBuilder.Wall(this, new Rect2(room.End.X - 36f, room.Position.Y, 36f, room.Size.Y), steel);

		DeckBuilder.Elevator(this, new Rect2(40f, 2380f, 70f, 110f), new Vector2(130f, 2440f));

		// Staff rooms (Act II).
		foreach ((float x, string name) in new[] { (360f, "Personal links"), (620f, "Personal rechts") })
		{
			DeckBuilder.Block(this, new Rect2(x - 55f, 2040f, 110f, 90f), new Color(0.35f, 0.28f, 0.22f), name.ToUpperInvariant(), labelSize: 10);
			Interactable door = DeckBuilder.Use(this, new Vector2(x, 2175f), "[E] Tür", 60f);
			door.Used += () =>
			{
				UiBus.Toast("Noch leer. Hier ziehen später Angestellte ein.", Holo.TextDim);
				VoiceQueue.Enqueue(FlokiAdvisor.Name, "Leer. Wie dein Personalplan, Chef.");
			};
		}

		// Owner's room: divider, rug, locker, bed.
		DeckBuilder.Wall(this, new Rect2(860f, 2090f, 24f, 260f), steel);
		Node2D rug = DeckBuilder.Block(this, new Rect2(950f, 2380f, 360f, 160f), new Color(0.45f, 0.2f, 0.2f, 0.55f), "", solid: false);
		rug.ZIndex = -3990;
		DeckBuilder.Block(this, new Rect2(1270f, 2100f, 90f, 120f), new Color(0.3f, 0.32f, 0.36f), "SPIND", labelSize: 10);
		DeckBuilder.Block(this, new Rect2(1060f, 2140f, 180f, 110f), new Color(0.32f, 0.4f, 0.55f), "BETT", labelSize: 12);
		Interactable bed = DeckBuilder.Use(this, BedSpot, "[E] Schlafen (Schicht beenden)", 75f);
		bed.IsAvailable = () => !TaskService.IsActive;
		bed.Used += () =>
		{
			GameSession.EndShift();
		};
		DeckBuilder.Glow(this, new Vector2(1150f, 2250f), new Color(1f, 0.8f, 0.55f), 3f, 0.6f);
	}
}
