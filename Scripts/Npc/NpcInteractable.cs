using Godot;

public partial class NpcInteractable : Area2D
{
	[Export] public NpcPersona Persona;
	[Export] private Label _promptLabel;
	[Export] private DialogueController _dialogueController;

	private bool _playerInRange;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;

		if (_promptLabel != null)
			_promptLabel.Visible = false;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("Player"))
			return;

		_playerInRange = true;
		if (_promptLabel != null)
			_promptLabel.Visible = true;
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("Player"))
			return;

		_playerInRange = false;
		if (_promptLabel != null)
			_promptLabel.Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// E talks (action "interact" in the Input Map).
		if (_playerInRange && @event.IsActionPressed("interact") && !@event.IsEcho() && !DialogueController.IsOpen)
		{
			_dialogueController?.OpenDialogue(Persona);
			GetViewport().SetInputAsHandled();
		}
	}
}
