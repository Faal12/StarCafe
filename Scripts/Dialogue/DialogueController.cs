using Godot;

public partial class DialogueController : CanvasLayer
{
	[Export] private Control _panel;
	[Export] private Label _nameLabel;
	[Export] private Label _dialogueLabel;
	[Export] private LineEdit _inputField;
	[Export] private Button _sendButton;
	[Export] private Player _player;

	[Export] private Control _dimBackground;
	[Export] private TextureRect _playerPortraitBig;
	[Export] private TextureRect _npcPortraitBig;

	private const float ForegroundScale = 1.0f;
	private const float BackgroundScale = 0.85f;
	private static readonly Color ForegroundColor = Colors.White;
	private static readonly Color BackgroundColor = new Color(0.42f, 0.42f, 0.42f);
	private const double SpeakerTransitionSeconds = 0.25;

	private ILlmProvider _llmProvider;
	private NpcPersona _persona;
	private bool _isRequestInFlight;

	public override void _Ready()
	{
		_llmProvider = new GeminiProvider();
		_panel.Visible = false;
		_dimBackground.Visible = false;
		_playerPortraitBig.Visible = false;
		_npcPortraitBig.Visible = false;

		// Pivot at bottom-center so foreground/background scaling grows from the "feet", not the top-left corner.
		_playerPortraitBig.PivotOffset = new Vector2(_playerPortraitBig.Size.X / 2f, _playerPortraitBig.Size.Y);
		_npcPortraitBig.PivotOffset = new Vector2(_npcPortraitBig.Size.X / 2f, _npcPortraitBig.Size.Y);

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
		_dimBackground.Visible = true;
		_playerPortraitBig.Visible = true;
		_npcPortraitBig.Visible = true;
		_npcPortraitBig.Texture = persona.Portrait;
		_inputField.GrabFocus();

		SetSpeaker(playerSpeaking: true, animate: false);

		if (_player != null)
			_player.CanMove = false;
	}

	public void CloseDialogue()
	{
		_panel.Visible = false;
		_dimBackground.Visible = false;
		_playerPortraitBig.Visible = false;
		_npcPortraitBig.Visible = false;

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

	private void SetSpeaker(bool playerSpeaking, bool animate = true)
	{
		TweenPortrait(_playerPortraitBig, playerSpeaking, animate);
		TweenPortrait(_npcPortraitBig, !playerSpeaking, animate);
	}

	private void TweenPortrait(TextureRect portrait, bool foreground, bool animate)
	{
		float targetScale = foreground ? ForegroundScale : BackgroundScale;
		Color targetColor = foreground ? ForegroundColor : BackgroundColor;

		if (!animate)
		{
			portrait.Scale = new Vector2(targetScale, targetScale);
			portrait.Modulate = targetColor;
			return;
		}

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(portrait, "scale", new Vector2(targetScale, targetScale), SpeakerTransitionSeconds);
		tween.TweenProperty(portrait, "modulate", targetColor, SpeakerTransitionSeconds);
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
		SetSpeaker(playerSpeaking: true);

		try
		{
			string reply = await _llmProvider.GetReplyAsync(_persona.SystemPrompt, playerText);
			_dialogueLabel.Text = reply;
			SetSpeaker(playerSpeaking: false);
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
