using Godot;

public partial class Main : Node2D
{
	[Export] private Label _dialogueLabel;
	[Export] private LineEdit _inputField;
	[Export] private Button _sendButton;
	[Export] private NpcPersona _persona;

	private ILlmProvider _llmProvider;
	private bool _isRequestInFlight;

	public override void _Ready()
	{
		GD.Print("Труппа готова.");
		_llmProvider = LlmProviderFactory.Create();
	}

	private async void _on_send_button_pressed()
	{
		if (_isRequestInFlight)
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
			_dialogueLabel.Text = $"{_persona.DisplayName}: {reply}";
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
		}
	}
}
