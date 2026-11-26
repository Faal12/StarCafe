using System;
using Godot;

/// <summary>
/// Shows one event as a conversation: placeholder portrait, possible outcomes, situation,
/// chat with the speaker and an input field. The player has to talk the situation to an
/// outcome; "Gehen lassen" ends it with the default outcome. Pauses the game while open.
/// Everything is voiced in order: narrator (situation, result), speaker, Floki.
/// Floki chimes in on his own now and then and gives a hint when asked ("Floki fragen").
/// </summary>
public partial class EventPanel : CanvasLayer
{
	private const int FlokiHintsPerEvent = 2;

	private Control _root;

	private PanelContainer _portrait;

	private Label _portraitInitial;

	private Label _portraitTag;

	private VBoxContainer _outcomeList;

	private Label _kind;

	private Label _title;

	private Label _speaker;

	private Label _situation;

	private RichTextLabel _chat;

	private Label _status;

	private HBoxContainer _inputRow;

	private LineEdit _input;

	private Button _send;

	private HBoxContainer _footer;

	/// <summary>Täuschen · Einschüchtern · Bestehlen · Angreifen (placeholder buttons – free for a real design).</summary>
	private HBoxContainer _actionRow;

	private readonly System.Collections.Generic.List<(ConversationAction Action, Button Button)> _actionButtons = new();

	private Label _turns;

	private Button _askFloki;

	private Button _walkAway;

	private Label _effects;

	private Label _world;

	private Label _radioHint;

	private Button _continue;

	private EventConversation _conversation;

	private Color _speakerColor;

	private bool _busy;

	private bool _flokiBusy;

	private int _flokiHintsLeft;

	private Action _onClosed;

	/// <summary>Task to start when the panel is closed ("Los!").</summary>
	private (GameEventDef Def, EventTaskDef Task)? _pendingTask;

	private static EventPanel _instance;

	public static bool IsOpen { get => UiBus.IsOpen(UiWindow.Event); private set => UiBus.SetOpen(UiWindow.Event, value); }

	/// <summary>Shows the final result of an event after its task (no conversation).</summary>
	public static void ShowAftermath(GameEventDef def, EventOutcomeDef outcome, EventResolution resolution)
	{
		_instance?.OpenAftermath(def, outcome, resolution);
	}

	public override void _Ready()
	{
		_instance = this;
		Layer = 30;
		ProcessMode = ProcessModeEnum.Always;
		BuildUi();
		_root.Visible = false;
	}

	public override void _ExitTree()
	{
		if (IsOpen)
		{
			IsOpen = false;
			VoiceQueue.Clear();
			GetTree().Paused = false;
		}
	}

	public void ShowEvent(GameEventDef def, Action onClosed = null)
	{
		_conversation = new EventConversation(def);
		_onClosed = onClosed;
		_busy = false;
		_flokiBusy = false;
		_flokiHintsLeft = FlokiHintsPerEvent;
		IsOpen = true;
		VoiceQueue.Clear();

		_speakerColor = SpeakerColor(def.Speaker.Faction);
		_portrait.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(_speakerColor, 0.28f), _speakerColor, 2, 10, 8, 8));
		_portraitInitial.Text = Initial(def.Speaker.Name);
		_portraitInitial.AddThemeColorOverride("font_color", _speakerColor.Lightened(0.35f));
		_portraitTag.Visible = def.Speaker.Placeholder;

		_kind.Text = def.IsConflict ? "KONFLIKT" : "EREIGNIS";
		_kind.AddThemeColorOverride("font_color", def.IsConflict ? UiKit.Bad : UiTheme.Cyan);
		_title.Text = def.Title;
		string faction = EventLibrary.TryFaction(def.Speaker.Faction, out Faction f) ? "  ·  " + FactionInfo.DisplayName(f) : "";
		_speaker.Text = def.Speaker.Name + faction;
		_situation.Text = def.Text;

		UiKit.ClearChildren(_outcomeList);
		foreach (EventOutcomeDef outcome in def.TalkOutcomes)
		{
			// ★ = hidden outcome the player unlocked (item, deed, flag …).
			Label label = new Label
			{
				Text = (outcome.IsSecret ? "★ " : "• ") + outcome.Label + (outcome.Default ? "  (wenn du dich heraushältst)" : ""),
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(220f, 0f),
				TooltipText = outcome.IsSecret ? "Erschlossen: " + outcome.Requires.Reason : "",
				MouseFilter = outcome.IsSecret ? Control.MouseFilterEnum.Pass : Control.MouseFilterEnum.Ignore
			};
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", outcome.IsSecret ? UiTheme.Cyan : UiTheme.Text);
			label.SetMeta("outcome", outcome.Id);
			_outcomeList.AddChild(label);
		}
		// Hidden outcomes the player cannot reach yet.
		foreach (EventOutcomeDef outcome in def.LockedOutcomes)
		{
			Label label = new Label
			{
				Text = "• ??? (verborgen)" + (string.IsNullOrWhiteSpace(outcome.Requires.Hint) ? "" : " – " + outcome.Requires.Hint),
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(220f, 0f)
			};
			label.AddThemeFontSizeOverride("font_size", 13);
			label.AddThemeColorOverride("font_color", UiTheme.TextDim);
			label.SetMeta("outcome", outcome.Id);
			_outcomeList.AddChild(label);
		}
		// Outcomes this event has for actions (deceive, threaten, steal, attack).
		foreach (EventOutcomeDef outcome in def.ActionOutcomes)
		{
			Label label = new Label
			{
				Text = "• " + outcome.Label + "  (" + ActionName(outcome.Action) + ")",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(220f, 0f)
			};
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", UiKit.Warn);
			label.SetMeta("outcome", outcome.Id);
			_outcomeList.AddChild(label);
		}

		// Narrator sets the scene, the speaker opens, Floki may add his two cents – in this order.
		_chat.Clear();
		VoiceQueue.Enqueue(LauncherConfig.NarratorSlot, def.Text);
		AppendSpeaker(def.Opening);
		VoiceQueue.Enqueue(def.Speaker.Name, def.Opening);
		if (!string.IsNullOrWhiteSpace(def.Floki) && EventService.ShouldFlokiComment())
		{
			SayFloki(def.Floki);
		}

		SetStatus(_conversation.StartNotice);
		_walkAway.TooltipText = "Du hältst dich heraus: " + def.DefaultOutcome.Label;
		UpdateTurns();
		UpdateFlokiButton();

		_inputRow.Visible = true;
		_actionRow.Visible = true;
		_footer.Visible = true;
		_input.Text = "";
		_input.Editable = true;
		_send.Disabled = false;
		_walkAway.Disabled = false;
		UpdateActions();
		_effects.Visible = false;
		_world.Visible = false;
		_radioHint.Visible = false;
		_continue.Visible = false;
		_root.Visible = true;
		GetTree().Paused = true;
		_input.CallDeferred(Control.MethodName.GrabFocus);
	}

	private async void OnSend()
	{
		string text = _input.Text.Trim();
		if (_busy || _conversation == null || _conversation.IsFinished || text.Length == 0)
		{
			return;
		}
		_busy = true;
		VoiceQueue.Clear();
		_input.Text = "";
		_input.Editable = false;
		_send.Disabled = true;
		_walkAway.Disabled = true;
		UpdateFlokiButton();
		UpdateActions();
		AppendPlayer(text);
		SetStatus(_conversation.Def.Speaker.Name + " überlegt …");
		EventConversation conversation = _conversation;
		ConversationStep step;
		try
		{
			step = await conversation.SayAsync(text);
		}
		catch (Exception ex)
		{
			LlmLog.Add("Ereignis", LlmLogKind.Fehler, "Unerwarteter Fehler: " + ex.Message);
			step = conversation.WalkAway();
		}
		if (!IsOpen || conversation != _conversation)
		{
			return;
		}
		SetStatus(step.Notice);
		if (!string.IsNullOrWhiteSpace(step.SpeakerLine))
		{
			AppendSpeaker(step.SpeakerLine);
			VoiceQueue.Enqueue(conversation.Def.Speaker.Name, step.SpeakerLine);
		}
		_busy = false;
		if (step.Outcome != null)
		{
			ShowResolution(step.Outcome);
			return;
		}
		ReadyForInput();
	}

	/// <summary>Back to the player after a reply: input, buttons, maybe a remark from Floki.</summary>
	private void ReadyForInput()
	{
		UpdateTurns();
		_input.Editable = true;
		_send.Disabled = false;
		_walkAway.Disabled = false;
		UpdateFlokiButton();
		UpdateActions();
		_input.GrabFocus();
		if (FlokiAdvisor.ShouldInterject())
		{
			FlokiSpeaks(hint: false);
		}
	}

	/// <summary>Deceive, threaten, steal, attack – the logic is in <see cref="ConversationActions"/>.</summary>
	private async void OnAction(ConversationAction action)
	{
		if (_busy || _conversation == null || _conversation.IsFinished || !ConversationActions.CanUse(_conversation, action, out _))
		{
			return;
		}
		string text = _input.Text.Trim();
		if (ConversationActions.NeedsText(action) && text.Length == 0)
		{
			SetStatus(action == ConversationAction.Taeuschen
				? "Schreib zuerst deine Lüge ins Feld – dann „Täuschen“."
				: "Schreib zuerst deine Drohung ins Feld – dann „Einschüchtern“.");
			_input.GrabFocus();
			return;
		}
		_busy = true;
		VoiceQueue.Clear();
		_input.Text = "";
		_input.Editable = false;
		_send.Disabled = true;
		_walkAway.Disabled = true;
		UpdateFlokiButton();
		UpdateActions();
		if (text.Length > 0 && ConversationActions.NeedsText(action))
		{
			AppendPlayer(text, ConversationActions.Label(action));
		}
		EventConversation conversation = _conversation;
		SetStatus(action == ConversationAction.Angreifen ? null : conversation.Def.Speaker.Name + " reagiert …");
		ActionReport report;
		try
		{
			report = action switch
			{
				ConversationAction.Taeuschen => await ConversationActions.DeceiveAsync(conversation, text),
				ConversationAction.Einschuechtern => await ConversationActions.IntimidateAsync(conversation, text),
				ConversationAction.Bestehlen => ConversationActions.Steal(conversation),
				_ => ConversationActions.Attack(conversation),
			};
		}
		catch (Exception ex)
		{
			LlmLog.Add("Ereignis", LlmLogKind.Fehler, "Handlung fehlgeschlagen: " + ex.Message);
			_busy = false;
			SetStatus("Das hat nicht geklappt – versuch etwas anderes.");
			ReadyForInput();
			return;
		}
		if (!IsOpen || conversation != _conversation)
		{
			return;
		}
		_busy = false;
		AppendAction(report.Narration, report.Success);
		VoiceQueue.Enqueue(LauncherConfig.NarratorSlot, StripChance(report.Narration));
		SetStatus(report.Step?.Notice);
		if (!string.IsNullOrWhiteSpace(report.Step?.SpeakerLine))
		{
			AppendSpeaker(report.Step.SpeakerLine);
			VoiceQueue.Enqueue(conversation.Def.Speaker.Name, report.Step.SpeakerLine);
		}
		if (report.StartsBrawl)
		{
			ShowBrawlStart();
			return;
		}
		if (report.Outcome != null)
		{
			ShowResolution(report.Outcome);
			return;
		}
		ReadyForInput();
	}

	/// <summary>The conversation turned into a fight: only "Kämpfen!" is left; the brawl starts when the window closes.</summary>
	private void ShowBrawlStart()
	{
		_inputRow.Visible = false;
		_actionRow.Visible = false;
		_footer.Visible = false;
		SetStatus(null);
		_effects.Text = "Schlägerei! Schlag mit Linksklick oder Leertaste zu, weich den Schlägen aus. Getränke heilen (Rechtsklick/F).";
		_effects.Visible = true;
		_pendingTask = null;
		_continue.Text = "Kämpfen!";
		_continue.Visible = true;
		_continue.GrabFocus();
	}

	private void UpdateActions()
	{
		foreach ((ConversationAction action, Button button) in _actionButtons)
		{
			bool usable = ConversationActions.CanUse(_conversation, action, out string reason);
			button.Disabled = _busy || !usable;
			button.TooltipText = ConversationActions.Hint(action) + (string.IsNullOrEmpty(reason) ? "" : "\n\n" + reason);
		}
	}

	private static string ActionName(string id)
	{
		foreach (ConversationAction action in Enum.GetValues<ConversationAction>())
		{
			if (ConversationActions.Id(action) == id)
			{
				return ConversationActions.Label(action);
			}
		}
		return id;
	}

	/// <summary>"Täuschen (55 %): …" → "…" for the narrator voice.</summary>
	private static string StripChance(string narration)
	{
		int i = (narration ?? "").IndexOf("): ", StringComparison.Ordinal);
		return i > 0 ? narration.Substring(i + 3) : narration;
	}

	private void OnAskFloki()
	{
		if (_flokiHintsLeft <= 0 || _flokiBusy || _busy || _conversation == null || _conversation.IsFinished)
		{
			return;
		}
		_flokiHintsLeft--;
		FlokiSpeaks(hint: true);
	}

	/// <summary>Floki comments (hint=false) or gives advice (hint=true) over the headset.</summary>
	private async void FlokiSpeaks(bool hint)
	{
		if (_flokiBusy)
		{
			return;
		}
		_flokiBusy = true;
		UpdateFlokiButton();
		EventConversation conversation = _conversation;
		if (hint)
		{
			SetStatus("Floki überlegt …");
		}
		string line = null;
		try
		{
			line = await FlokiAdvisor.SpeakAsync(conversation, hint);
		}
		catch (Exception ex)
		{
			LlmLog.Add("Floki", LlmLogKind.Fehler, "Unerwarteter Fehler: " + ex.Message);
		}
		_flokiBusy = false;
		if (!IsOpen || conversation != _conversation || conversation.IsFinished)
		{
			return;
		}
		if (hint)
		{
			SetStatus(null);
		}
		if (!string.IsNullOrWhiteSpace(line))
		{
			SayFloki(line);
		}
		UpdateFlokiButton();
	}

	private void OnWalkAway()
	{
		if (_busy || _conversation == null || _conversation.IsFinished)
		{
			return;
		}
		VoiceQueue.Clear();
		AppendNarration("Du wendest dich ab und lässt die Sache laufen.");
		ShowResolution(_conversation.WalkAway().Outcome);
	}

	private void ShowResolution(EventOutcomeDef outcome)
	{
		EventResolution resolution = EventService.Resolve(_conversation.Def, outcome);
		foreach (Node child in _outcomeList.GetChildren())
		{
			if (child is Label label)
			{
				bool reached = (string)label.GetMeta("outcome") == outcome.Id;
				label.AddThemeColorOverride("font_color", reached ? UiKit.Good : UiTheme.TextDim);
				if (reached)
				{
					label.Text = "✓ " + outcome.Label;
				}
			}
		}
		if (!string.IsNullOrWhiteSpace(resolution.Result))
		{
			AppendNarration(resolution.Result);
			VoiceQueue.Enqueue(LauncherConfig.NarratorSlot, resolution.Result);
		}
		if (!string.IsNullOrWhiteSpace(resolution.Floki))
		{
			SayFloki(resolution.Floki);
		}
		_inputRow.Visible = false;
		_actionRow.Visible = false;
		_footer.Visible = false;
		SetStatus(null);
		_effects.Text = "Ausgang: " + outcome.Label + "\n" + resolution.EffectsLine;
		_effects.Visible = true;
		_world.Text = resolution.WorldLine ?? "";
		_world.Visible = !string.IsNullOrEmpty(resolution.WorldLine);
		_radioHint.Visible = resolution.HasRadio && GameSettings.RadioEnabled;
		_pendingTask = outcome.Task != null ? (_conversation.Def, outcome.Task) : null;
		_continue.Text = outcome.Task != null ? "Los!" : "Weiter";
		if (outcome.Task != null)
		{
			_effects.Text += "\nAufgabe: " + (string.IsNullOrWhiteSpace(outcome.Task.Title) ? outcome.Task.Id : outcome.Task.Title)
				+ (outcome.Task.TimeLimit > 0 ? $" – du hast etwa {Mathf.RoundToInt(outcome.Task.TimeLimit)} Sekunden." : ".");
		}
		_continue.Visible = true;
		_continue.GrabFocus();
	}

	private void OpenAftermath(GameEventDef def, EventOutcomeDef outcome, EventResolution resolution)
	{
		VoiceQueue.Clear();
		_conversation = null;
		_pendingTask = null;
		IsOpen = true;
		_speakerColor = SpeakerColor(def.Speaker.Faction);
		_portrait.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(_speakerColor, 0.28f), _speakerColor, 2, 10, 8, 8));
		_portraitInitial.Text = Initial(def.Speaker.Name);
		_kind.Text = "FOLGEN";
		_kind.AddThemeColorOverride("font_color", UiKit.Warn);
		_title.Text = def.Title;
		_speaker.Text = def.Speaker.Name;
		_situation.Text = "";
		UiKit.ClearChildren(_outcomeList);
		Label reached = new Label { Text = "✓ " + outcome.Label, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(220f, 0f) };
		reached.AddThemeColorOverride("font_color", UiKit.Good);
		_outcomeList.AddChild(reached);
		_chat.Clear();
		if (!string.IsNullOrWhiteSpace(resolution.Result))
		{
			AppendNarration(resolution.Result);
			VoiceQueue.Enqueue(LauncherConfig.NarratorSlot, resolution.Result);
		}
		if (!string.IsNullOrWhiteSpace(resolution.Floki))
		{
			SayFloki(resolution.Floki);
		}
		_inputRow.Visible = false;
		_actionRow.Visible = false;
		_footer.Visible = false;
		SetStatus(null);
		_effects.Text = "Ausgang: " + outcome.Label + "\n" + resolution.EffectsLine;
		_effects.Visible = true;
		_world.Text = resolution.WorldLine ?? "";
		_world.Visible = !string.IsNullOrEmpty(resolution.WorldLine);
		_radioHint.Visible = resolution.HasRadio && GameSettings.RadioEnabled;
		_continue.Text = "Weiter";
		_continue.Visible = true;
		_root.Visible = true;
		GetTree().Paused = true;
		_continue.CallDeferred(Control.MethodName.GrabFocus);
	}

	private void Close()
	{
		VoiceQueue.Clear();
		_root.Visible = false;
		IsOpen = false;
		_conversation = null;
		GetTree().Paused = false;
		var task = _pendingTask;
		_pendingTask = null;
		if (task.HasValue)
		{
			TaskService.Start(task.Value.Def, task.Value.Task);
		}
		Action onClosed = _onClosed;
		_onClosed = null;
		onClosed?.Invoke();
	}

	private void UpdateTurns()
	{
		int left = _conversation.TurnsLeft;
		_turns.Text = left == 1 ? "Letzte Antwort – danach entscheidet sich die Sache." : $"Noch {left} Antworten, bis sich die Sache entscheidet.";
		_turns.AddThemeColorOverride("font_color", left <= 1 ? UiKit.Warn : UiTheme.TextDim);
	}

	private void UpdateFlokiButton()
	{
		_askFloki.Text = $"Floki fragen ({_flokiHintsLeft})";
		_askFloki.Disabled = _flokiHintsLeft <= 0 || _flokiBusy || _busy || _conversation == null || _conversation.IsFinished;
	}

	private void SetStatus(string text)
	{
		_status.Text = text ?? "";
		_status.Visible = _status.Text.Length > 0;
	}

	private void SayFloki(string line)
	{
		string clean = line.Trim().Trim('*').Trim();
		_chat.AppendText($"[color=#{UiTheme.Cyan.ToHtml(false)}][i]*Floki: {Escape(clean)}*[/i][/color]\n");
		VoiceQueue.Enqueue(FlokiAdvisor.Name, clean);
	}

	private void AppendSpeaker(string text)
	{
		string name = _conversation.Def.Speaker.Name;
		_chat.AppendText($"[color=#{_speakerColor.Lightened(0.35f).ToHtml(false)}][b]{Escape(name)}:[/b][/color] {Escape(text)}\n");
	}

	private void AppendPlayer(string text, string actionTag = null)
	{
		string tag = string.IsNullOrEmpty(actionTag) ? "" : $" [color=#{UiKit.Warn.ToHtml(false)}]({Escape(actionTag)})[/color]";
		_chat.AppendText($"[color=#{UiKit.Amber.ToHtml(false)}][b]Du:[/b][/color]{tag} {Escape(text)}\n");
	}

	/// <summary>Result of an action: green when it worked, red when it went wrong.</summary>
	private void AppendAction(string text, bool success)
	{
		Color color = success ? UiKit.Good : UiKit.Bad;
		_chat.AppendText($"[color=#{color.ToHtml(false)}][i]▸ {Escape(text)}[/i][/color]\n");
	}

	private void AppendNarration(string text)
	{
		_chat.AppendText($"[color=#{UiTheme.TextDim.ToHtml(false)}][i]{Escape(text)}[/i][/color]\n");
	}

	private static string Escape(string text)
	{
		return (text ?? "").Replace("[", "[lb]");
	}

	private void BuildUi()
	{
		_root = new Control { Theme = UiTheme.Get() };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);
		ColorRect dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f), MouseFilter = Control.MouseFilterEnum.Stop };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(dim);
		CenterContainer center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(center);
		PanelContainer panel = new PanelContainer { CustomMinimumSize = new Vector2(1080f, 610f) };
		panel.AddThemeStyleboxOverride("panel", UiKit.PanelStyle());
		center.AddChild(panel);
		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 20);
		panel.AddChild(row);

		// Left: placeholder portrait + possible outcomes.
		VBoxContainer left = new VBoxContainer { CustomMinimumSize = new Vector2(240f, 0f) };
		left.AddThemeConstantOverride("separation", 10);
		row.AddChild(left);
		_portrait = new PanelContainer { CustomMinimumSize = new Vector2(150f, 190f), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
		left.AddChild(_portrait);
		VBoxContainer portraitBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		_portrait.AddChild(portraitBox);
		_portraitInitial = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		_portraitInitial.AddThemeFontOverride("font", UiTheme.TitleFont);
		_portraitInitial.AddThemeFontSizeOverride("font_size", 84);
		portraitBox.AddChild(_portraitInitial);
		_portraitTag = new Label { Text = "Platzhalter", HorizontalAlignment = HorizontalAlignment.Center };
		_portraitTag.AddThemeFontSizeOverride("font_size", 12);
		_portraitTag.AddThemeColorOverride("font_color", UiTheme.TextDim);
		portraitBox.AddChild(_portraitTag);
		Label outcomesHeader = new Label { Text = "Mögliche Ausgänge" };
		outcomesHeader.AddThemeColorOverride("font_color", UiKit.Amber);
		left.AddChild(outcomesHeader);
		_outcomeList = new VBoxContainer();
		_outcomeList.AddThemeConstantOverride("separation", 6);
		left.AddChild(_outcomeList);
		Label how = new Label
		{
			Text = "Kein Knopf entscheidet: Führ das Gespräch so, dass es dort endet, wo du hinwillst. Lügen, Drohen, Stehlen oder Zuschlagen sind riskant – und haben Folgen.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(220f, 0f)
		};
		how.AddThemeFontSizeOverride("font_size", 12);
		how.AddThemeColorOverride("font_color", UiTheme.TextDim);
		left.AddChild(how);

		// Right: situation, chat, input.
		VBoxContainer body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 6);
		row.AddChild(body);
		_kind = new Label();
		_kind.AddThemeFontSizeOverride("font_size", 13);
		body.AddChild(_kind);
		_title = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_title.AddThemeFontOverride("font", UiTheme.HeadingFont);
		_title.AddThemeFontSizeOverride("font_size", 24);
		_title.AddThemeColorOverride("font_color", UiKit.Amber);
		body.AddChild(_title);
		_speaker = new Label();
		_speaker.AddThemeColorOverride("font_color", UiTheme.TextDim);
		body.AddChild(_speaker);
		_situation = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600f, 0f) };
		_situation.AddThemeFontSizeOverride("font_size", 15);
		body.AddChild(_situation);
		_chat = new RichTextLabel
		{
			BbcodeEnabled = true,
			ScrollFollowing = true,
			SelectionEnabled = true,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 150f)
		};
		_chat.AddThemeFontSizeOverride("normal_font_size", 16);
		_chat.AddThemeFontSizeOverride("bold_font_size", 16);
		_chat.AddThemeFontSizeOverride("italics_font_size", 15);
		_chat.AddThemeStyleboxOverride("normal", UiTheme.Box(UiTheme.Field, UiTheme.Line, 1, 6, 10, 8));
		body.AddChild(_chat);
		_status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_status.AddThemeFontSizeOverride("font_size", 13);
		_status.AddThemeColorOverride("font_color", UiTheme.TextDim);
		body.AddChild(_status);
		_inputRow = new HBoxContainer();
		_inputRow.AddThemeConstantOverride("separation", 8);
		body.AddChild(_inputRow);
		_input = new LineEdit { PlaceholderText = "Was sagst oder tust du?", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_input.TextSubmitted += _ => OnSend();
		_inputRow.AddChild(_input);
		_send = new Button { Text = "Sagen", ThemeTypeVariation = UiTheme.PrimaryButton, CustomMinimumSize = new Vector2(110f, 0f) };
		_send.Pressed += OnSend;
		_inputRow.AddChild(_send);
		_actionRow = new HBoxContainer();
		_actionRow.AddThemeConstantOverride("separation", 6);
		body.AddChild(_actionRow);
		Label orAct = new Label { Text = "Oder handle:" };
		orAct.AddThemeFontSizeOverride("font_size", 13);
		orAct.AddThemeColorOverride("font_color", UiTheme.TextDim);
		_actionRow.AddChild(orAct);
		foreach (ConversationAction action in Enum.GetValues<ConversationAction>())
		{
			Button button = new Button { Text = ConversationActions.Label(action), CustomMinimumSize = new Vector2(120f, 0f) };
			button.AddThemeColorOverride("font_color", action == ConversationAction.Angreifen ? UiKit.Bad : UiKit.Warn);
			ConversationAction captured = action;
			button.Pressed += () => OnAction(captured);
			_actionRow.AddChild(button);
			_actionButtons.Add((action, button));
		}
		_footer = new HBoxContainer();
		_footer.AddThemeConstantOverride("separation", 8);
		body.AddChild(_footer);
		_turns = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_turns.AddThemeFontSizeOverride("font_size", 13);
		_footer.AddChild(_turns);
		_askFloki = new Button { TooltipText = "Floki flüstert dir über das Headset einen Tipp zu. Nur du hörst ihn." };
		_askFloki.AddThemeColorOverride("font_color", UiTheme.Cyan);
		_askFloki.Pressed += OnAskFloki;
		_footer.AddChild(_askFloki);
		_walkAway = new Button { Text = "Gehen lassen" };
		_walkAway.Pressed += OnWalkAway;
		_footer.AddChild(_walkAway);
		_effects = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_effects.AddThemeColorOverride("font_color", UiKit.Warn);
		body.AddChild(_effects);
		_world = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_world.AddThemeColorOverride("font_color", UiKit.Good);
		body.AddChild(_world);
		_radioHint = new Label { Text = "Das Galaktische Radio wird darüber berichten." };
		_radioHint.AddThemeFontSizeOverride("font_size", 13);
		_radioHint.AddThemeColorOverride("font_color", UiTheme.TextDim);
		body.AddChild(_radioHint);
		_continue = new Button
		{
			Text = "Weiter",
			ThemeTypeVariation = UiTheme.PrimaryButton,
			CustomMinimumSize = new Vector2(160f, 40f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
		};
		_continue.Pressed += Close;
		body.AddChild(_continue);
	}

	private static string Initial(string name)
	{
		string trimmed = (name ?? "").Trim();
		return trimmed.Length > 0 ? trimmed.Substring(0, 1).ToUpperInvariant() : "?";
	}

	/// <summary>Placeholder colours per faction until real portraits exist.</summary>
	private static Color SpeakerColor(string factionName)
	{
		if (!EventLibrary.TryFaction(factionName, out Faction faction))
		{
			return new Color(0.6f, 0.6f, 0.65f);
		}
		return faction switch
		{
			Faction.Imperium => new Color(0.78f, 0.25f, 0.25f),
			Faction.Rebellen => new Color(0.95f, 0.6f, 0.2f),
			Faction.Soeldner => new Color(0.55f, 0.5f, 0.42f),
			Faction.Silberkiel => new Color(0.75f, 0.8f, 0.88f),
			Faction.GraueHaende => new Color(0.5f, 0.52f, 0.55f),
			Faction.Sternenwacht => new Color(0.3f, 0.5f, 0.9f),
			Faction.Funkenwerkstatt => new Color(0.95f, 0.85f, 0.3f),
			Faction.Weitwanderer => new Color(0.4f, 0.75f, 0.55f),
			_ => new Color(0.6f, 0.6f, 0.65f),
		};
	}
}
