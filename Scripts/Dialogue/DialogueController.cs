using Godot;

public partial class DialogueController : CanvasLayer
{
	[Export] private Control _panel;
	[Export] private Label _nameLabel;
	[Export] private Label _dialogueLabel;
	[Export] private LineEdit _inputField;
	[Export] private Button _sendButton;
	// The UI lives in its own scene (Scenes/Ui/TavernUi.tscn), so the player is found by group, not by path.
	private Player _player => GetTree().GetFirstNodeInGroup("Player") as Player;

	[Export] private Control _dimBackground;
	[Export] private TextureRect _playerPortraitBig;
	[Export] private TextureRect _npcPortraitBig;
	// Eye/emotion layer on top of the NPC portrait (child of _npcPortraitBig, same rect).
	[Export] private TextureRect _npcEmotion;

	private const float ForegroundScale = 1.0f;
	// 0.9 keeps the hero (632 px) taller than Floki (535 px) even while Floki is speaking.
	private const float BackgroundScale = 0.9f;
	private static readonly Color ForegroundColor = Colors.White;
	private static readonly Color BackgroundColor = new Color(0.42f, 0.42f, 0.42f);
	private const double SpeakerTransitionSeconds = 0.25;
	private const double EntranceSeconds = 0.4;
	// The text finishes slightly before the voice does, which reads more naturally than lagging behind it.
	private const double TextLeadFactor = 0.92;
	private const float EntranceSlideDistance = 500f;

	private NpcPersona _persona;
	private bool _isRequestInFlight;
	private Tween _textTween;

	private Vector2 _playerRestPosition;
	private Vector2 _npcRestPosition;

	// The pause menu uses this to leave Escape to the dialogue while one is open.
	public static bool IsOpen { get => UiBus.IsOpen(UiWindow.Dialogue); private set => UiBus.SetOpen(UiWindow.Dialogue, value); }

	public override void _Ready()
	{
		GameSettings.Apply();

		_panel.Visible = false;
		_dimBackground.Visible = false;
		_playerPortraitBig.Visible = false;
		_npcPortraitBig.Visible = false;

		// Pivot at bottom-center so foreground/background scaling grows from the "feet", not the top-left corner.
		_playerPortraitBig.PivotOffset = new Vector2(_playerPortraitBig.Size.X / 2f, _playerPortraitBig.Size.Y);
		_npcPortraitBig.PivotOffset = new Vector2(_npcPortraitBig.Size.X / 2f, _npcPortraitBig.Size.Y);

		_playerRestPosition = _playerPortraitBig.Position;
		_npcRestPosition = _npcPortraitBig.Position;

		_sendButton.Pressed += OnSendPressed;
		_inputField.TextSubmitted += _ => OnSendPressed();
	}

	public override void _ExitTree()
	{
		IsOpen = false;
		TtsService.Stop();
	}

	public void OpenDialogue(NpcPersona persona)
	{
		IsOpen = true;
		_persona = persona;
		_nameLabel.Text = persona.DisplayName;
		_dialogueLabel.AddThemeFontSizeOverride("font_size", GameSettings.DialogueFontSize);
		ShowText("...", animate: false);
		_inputField.Text = "";
		_panel.Visible = true;
		_dimBackground.Visible = true;
		_playerPortraitBig.Visible = true;
		_npcPortraitBig.Visible = true;
		_npcPortraitBig.Texture = persona.Portrait;
		SetEmotion(persona.DefaultEmotion);
		_inputField.GrabFocus();

		SetSpeaker(playerSpeaking: true, animate: false);

		// Slide both characters in from off-screen for an entrance beat.
		_playerPortraitBig.Position = _playerRestPosition + new Vector2(-EntranceSlideDistance, 0);
		_npcPortraitBig.Position = _npcRestPosition + new Vector2(EntranceSlideDistance, 0);

		Tween entrance = CreateTween();
		entrance.SetParallel(true);
		entrance.TweenProperty(_playerPortraitBig, "position", _playerRestPosition, EntranceSeconds)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		entrance.TweenProperty(_npcPortraitBig, "position", _npcRestPosition, EntranceSeconds)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

		if (_player != null)
			_player.CanMove = false;
	}

	public void CloseDialogue()
	{
		IsOpen = false;
		_textTween?.Kill();
		TtsService.Stop();

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

	// spokenSeconds: length of the voice line. The text then types along with it instead of at the fixed speed.
	private void ShowText(string text, bool animate = true, double? spokenSeconds = null)
	{
		_textTween?.Kill();
		_dialogueLabel.Text = text;

		float charactersPerSecond = GameSettings.CharactersPerSecond;
		if (!animate || charactersPerSecond <= 0f || text.Length == 0)
		{
			_dialogueLabel.VisibleRatio = 1f;
			return;
		}

		double duration = spokenSeconds.HasValue && spokenSeconds.Value > 0.5
			? spokenSeconds.Value * TextLeadFactor
			: text.Length / charactersPerSecond;

		_dialogueLabel.VisibleRatio = 0f;
		_textTween = CreateTween();
		_textTween.TweenProperty(_dialogueLabel, "visible_ratio", 1.0f, duration);
	}

	private void SetEmotion(string emotion)
	{
		if (_npcEmotion == null)
			return;
		Texture2D texture = _persona?.GetEmotion(emotion);
		_npcEmotion.Texture = texture;
		_npcEmotion.Visible = texture != null;
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
		tween.TweenProperty(portrait, "scale", new Vector2(targetScale, targetScale), SpeakerTransitionSeconds)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
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
		TtsService.Stop();
		ShowText($"{_persona.DisplayName} denkt nach...", animate: false);
		SetSpeaker(playerSpeaking: true);

		try
		{
			string systemPrompt = PromptComposer.Compose(_persona);
			string rawReply = await LlmProviderFactory.Create().GetReplyAsync(systemPrompt, playerText);
			// The AI starts with a mood tag ([genervt] …): it drives the eyes and is neither shown nor spoken.
			string reply = EmotionTag.Split(rawReply, _persona, out string emotion);

			// The voice is made first (cache, Piper, cloud), so text and sound start together.
			TtsClip clip = IsOpen ? await TtsService.PrepareAsync(_persona.DisplayName, reply) : null;

			// The player may have closed the dialogue while the reply was on its way.
			if (IsOpen)
			{
				SetEmotion(emotion);
				ShowText(reply, spokenSeconds: clip?.Seconds);
				SetSpeaker(playerSpeaking: false);
				TtsService.Play(clip);
			}
		}
		catch (LlmRequestException ex)
		{
			if (IsOpen)
				ShowText($"{_persona.DisplayName} schweigt – {ex.Message}.", animate: false);
		}
		catch (System.Exception ex)
		{
			LlmLog.Add("Dialog", LlmLogKind.Fehler, $"Unerwarteter Fehler: {ex.Message}");
			if (IsOpen)
				ShowText($"{_persona.DisplayName} schweigt – unerwarteter Fehler.", animate: false);
		}
		finally
		{
			_isRequestInFlight = false;
			_sendButton.Disabled = false;
			_inputField.Text = "";
		}
	}
}
