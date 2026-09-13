using Godot;

public partial class DialogueController : CanvasLayer
{
	[Export] private Control _panel;
	[Export] private Label _nameLabel;
	[Export] private Label _dialogueLabel;
	[Export] private LineEdit _inputField;
	[Export] private Button _sendButton;
	[Export] private Player _player;

	private ILlmProvider _llmProvider;
	private NpcPersona _persona;
	private bool _isRequestInFlight;

	public override void _Ready()
	{
		_llmProvider = new GeminiProvider();
		_panel.Visible = false;
		_sendButton.Pressed += OnSendPressed;
		_inputField.TextSubmitted += _ => OnSendPressed();
	}

	public void OpenDialogue(NpcPersona persona)
	{
		_persona = persona;
		_nameLabel.Text = persona.DisplayName;
		_dialogueLabel.Text = "...";
		_inputField.Text = "";
		_panel.Visible = true;
		_inputField.GrabFocus();

		if (_player != null)
			_player.CanMove = false;
	}

	public void CloseDialogue()
	{
		_panel.Visible = false;

		if (_player != null)
			_player.CanMove = true;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_panel.Visible && @event.IsActionPressed("ui_cancel"))
		{
			CloseDialogue();
			GetViewport().SetInputAsHandled();
		}
	}

	private async void OnSendPressed()
	{
		if (_isRequestInFlight || _persona == null)
			return;

		string playerText = _inputField.Text.Trim();
		if (string.IsNullOrEmpty(playerText))
			return;

		_isRequestInFlight = true;
		_sendButton.Disabled = true;
		_dialogueLabel.Text = $"{_persona.DisplayName} задумался...";

		try
		{
			string reply = await _llmProvider.GetReplyAsync(_persona.SystemPrompt, playerText);
			_dialogueLabel.Text = reply;
		}
		catch (LlmRequestException ex)
		{
			GD.PrintErr(ex.Message);
			_dialogueLabel.Text = $"{_persona.DisplayName} молчит — {ex.Message}.";
		}
		finally
		{
			_isRequestInFlight = false;
			_sendButton.Disabled = false;
			_inputField.Text = "";
		}
	}
}
