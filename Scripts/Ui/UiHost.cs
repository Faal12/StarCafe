using System;
using Godot;

/// <summary>
/// Root of <c>Scenes/Ui/TavernUi.tscn</c> – the front end's side of <see cref="UiBus"/>.
/// Listens to the requests from game logic and hands them to the windows below it.
///
/// To replace a window (e.g. a new bar screen built as a scene): put the new node into TavernUi.tscn
/// and change the matching line in <see cref="Connect"/>. Game logic does not need to change.
/// </summary>
public partial class UiHost : Node
{
	[Export]
	public NodePath DialoguePath { get; set; } = new NodePath("DialogueUI");

	[Export]
	public NodePath EventPanelPath { get; set; } = new NodePath("EventPanel");

	private DialogueController _dialogue;

	private EventPanel _eventPanel;

	public override void _Ready()
	{
		_dialogue = GetNodeOrNull<DialogueController>(DialoguePath);
		_eventPanel = GetNodeOrNull<EventPanel>(EventPanelPath);
		Connect(true);
	}

	public override void _ExitTree()
	{
		Connect(false);
		UiBus.ResetOpen();
	}

	private void Connect(bool on)
	{
		if (on)
		{
			UiBus.ToastRequested += OnToast;
			UiBus.DialogueRequested += OnDialogue;
			UiBus.BarRequested += BarCounterView.Open;
			UiBus.OrderRequested += OrderPanel.ShowOrder;
			UiBus.WaitingRequested += OrderPanel.ShowWaiting;
			UiBus.RatingRequested += OrderPanel.ShowRating;
			UiBus.EventRequested += OnEvent;
			UiBus.AftermathRequested += EventPanel.ShowAftermath;
			UiBus.PlayerHurt += CombatHud.FlashDamage;
			UiBus.MinigameRequested += OnMinigame;
		}
		else
		{
			UiBus.ToastRequested -= OnToast;
			UiBus.DialogueRequested -= OnDialogue;
			UiBus.BarRequested -= BarCounterView.Open;
			UiBus.OrderRequested -= OrderPanel.ShowOrder;
			UiBus.WaitingRequested -= OrderPanel.ShowWaiting;
			UiBus.RatingRequested -= OrderPanel.ShowRating;
			UiBus.EventRequested -= OnEvent;
			UiBus.AftermathRequested -= EventPanel.ShowAftermath;
			UiBus.PlayerHurt -= CombatHud.FlashDamage;
			UiBus.MinigameRequested -= OnMinigame;
		}
	}

	private static void OnToast(string text, Color? color) => InventoryHud.Toast(text, color);

	private void OnDialogue(NpcPersona persona) => _dialogue?.OpenDialogue(persona);

	private void OnEvent(GameEventDef def) => _eventPanel?.ShowEvent(def);

	private static void OnMinigame(string id, Action<bool> done)
	{
		switch (id)
		{
			case MinigameId.Circuit:
				CircuitGame.Open(done);
				break;
			case MinigameId.Pressure:
				PressureGame.Open(done);
				break;
			default:
				GD.PushWarning($"UiHost: unknown mini-game '{id}'.");
				done?.Invoke(false);
				break;
		}
	}
}
