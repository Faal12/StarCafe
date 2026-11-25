using System.Diagnostics;
using System.Linq;
using Godot;

// Shared by the launcher screen and the pause menu, so the live log can be watched during play.
public partial class LauncherPanel : VBoxContainer
{
	private const string TestSystemPrompt = "Du bist ein Verbindungstest. Antworte auf Deutsch mit genau einem kurzen Satz.";
	private const string TestUserText = "Sag kurz Hallo.";
	private const string VoiceTestText = "Hallo Chef. Das hier ist meine Stimme. Ich klinge besser, als ich bin.";

	private static readonly string[] ProviderNames = { "Gemini (Google)", "Offline-Demo (ohne Internet)" };
	private static readonly string[] FallbackNames = { "Keiner", "Gemini (Google)", "Offline-Demo" };
	private static readonly string[] TtsProviderNames =
		{ "Piper (offline, Standard)", "Gemini (Cloud)", "ElevenLabs (Cloud)", "Windows-Systemstimmen" };

	private Label _keyStatus;
	private Label _testResult;
	private Button _testButton;
	private RichTextLabel _log;
	private bool _testing;
	private VBoxContainer _voiceDetails;
	private Button _voiceTestFloki;
	private Button _voiceTestGuest;
	private Label _voiceTestResult;
	private Label _cacheLabel;
	private bool _voiceTesting;

	public override void _EnterTree()
	{
		LlmLog.EntryAdded += OnLogEntry;
		LlmLog.Cleared += RefillLog;
	}

	public override void _ExitTree()
	{
		LlmLog.EntryAdded -= OnLogEntry;
		LlmLog.Cleared -= RefillLog;
	}

	public override void _Ready()
	{
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		AddThemeConstantOverride("separation", 10);
		BuildAiSection();
		BuildVoiceSection();
		BuildLogSection();
	}

	private void BuildAiSection()
	{
		AddChild(UiKit.Header("Dialog-KI"));

		AddChild(UiKit.Row("Anbieter",
			UiKit.Options(ProviderNames, (int)LauncherConfig.Provider, index => LauncherConfig.Provider = (LlmProviderKind)index)));

		int fallbackIndex = LauncherConfig.Fallback.HasValue ? (int)LauncherConfig.Fallback.Value + 1 : 0;
		AddChild(UiKit.Row("Ersatzanbieter",
			UiKit.Options(FallbackNames, fallbackIndex,
				index => LauncherConfig.Fallback = index == 0 ? null : (LlmProviderKind)(index - 1)),
			"Springt ein, wenn der Hauptanbieter ausfällt."));

		var model = new LineEdit { Text = LauncherConfig.GeminiModel, PlaceholderText = LauncherConfig.DefaultGeminiModel };
		model.TextSubmitted += text => LauncherConfig.GeminiModel = text;
		model.FocusExited += () => LauncherConfig.GeminiModel = model.Text;
		AddChild(UiKit.Row("Gemini-Modell", model));

		var storedKey = new LineEdit
		{
			Text = LauncherConfig.StoredApiKey,
			Secret = true,
			PlaceholderText = "API-Schlüssel",
			Editable = !LauncherConfig.UseEnvironmentKey
		};
		storedKey.TextSubmitted += text =>
		{
			LauncherConfig.StoredApiKey = text;
			RefreshKeyStatus();
		};
		storedKey.FocusExited += () =>
		{
			LauncherConfig.StoredApiKey = storedKey.Text;
			RefreshKeyStatus();
		};

		AddChild(UiKit.Row("Schlüsselquelle",
			UiKit.Toggle("Umgebungsvariable GEMINI_API_KEY verwenden", LauncherConfig.UseEnvironmentKey, on =>
			{
				LauncherConfig.UseEnvironmentKey = on;
				storedKey.Editable = !on;
				RefreshKeyStatus();
			})));
		AddChild(UiKit.Row("Eigener Schlüssel", storedKey));

		_keyStatus = new Label();
		AddChild(UiKit.Row("Status", _keyStatus));
		RefreshKeyStatus();

		AddChild(UiKit.Hint("Ein hier eingetragener Schlüssel wird unverschlüsselt in user://launcher.cfg gespeichert. Die Umgebungsvariable ist sicherer."));

		AddChild(UiKit.SliderRow("Max. Antwortlänge", 256, 2048, 64, LauncherConfig.MaxOutputTokens,
			value => $"{value:0}",
			value => LauncherConfig.MaxOutputTokens = (int)value,
			"Zu niedrig kann bei Denkmodellen zu leeren Antworten führen."));
		AddChild(UiKit.SliderRow("Kreativität", 0, 2, 0.05, LauncherConfig.Temperature,
			value => $"{value:0.00}",
			value => LauncherConfig.Temperature = value,
			"Niedrig: berechenbar. Hoch: überraschender."));

		_testButton = new Button { Text = "KI testen", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
		_testButton.Pressed += OnTestAiPressed;
		_testResult = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		};

		var testRow = new HBoxContainer();
		testRow.AddThemeConstantOverride("separation", 12);
		testRow.AddChild(_testButton);
		testRow.AddChild(_testResult);
		AddChild(testRow);
	}

	private void BuildVoiceSection()
	{
		AddChild(UiKit.Header("Sprachausgabe"));

		AddChild(UiKit.Row("Antworten vorlesen",
			UiKit.StateToggle("an", "aus", LauncherConfig.TtsEnabled, on => LauncherConfig.TtsEnabled = on)));

		AddChild(UiKit.Row("Stimmen-Anbieter",
			UiKit.Options(TtsProviderNames, (int)LauncherConfig.TtsProvider, index =>
			{
				LauncherConfig.TtsProvider = (TtsProviderKind)index;
				RebuildVoiceDetails();
			}),
			"Piper läuft offline auf dem Rechner. Gemini und ElevenLabs sind Cloud-Dienste."));

		_voiceDetails = new VBoxContainer();
		_voiceDetails.AddThemeConstantOverride("separation", 10);
		AddChild(_voiceDetails);
		RebuildVoiceDetails();

		AddChild(UiKit.SliderRow("Lautstärke", 0, 100, 5, LauncherConfig.TtsVolume,
			value => $"{value:0} %",
			value => LauncherConfig.TtsVolume = (int)value,
			"Wird mit der Gesamtlautstärke aus den Einstellungen multipliziert."));
		AddChild(UiKit.SliderRow("Sprechtempo", 0.5, 2, 0.1, LauncherConfig.TtsRate,
			value => $"{value:0.0}x",
			value => LauncherConfig.TtsRate = value));

		_voiceTestFloki = new Button { Text = "Floki testen", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
		_voiceTestFloki.Pressed += () => OnTestVoicePressed("Floki");
		_voiceTestGuest = new Button { Text = "Gast testen", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
		_voiceTestGuest.Pressed += () => OnTestVoicePressed("Gast");
		_voiceTestResult = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		};

		var testRow = new HBoxContainer();
		testRow.AddThemeConstantOverride("separation", 12);
		testRow.AddChild(_voiceTestFloki);
		testRow.AddChild(_voiceTestGuest);
		testRow.AddChild(_voiceTestResult);
		AddChild(testRow);

		_cacheLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
		var clearCache = new Button { Text = "Stimmen-Cache leeren", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
		clearCache.Pressed += () =>
		{
			TtsService.ClearCache();
			RefreshCacheLabel();
		};
		var cacheRow = new HBoxContainer();
		cacheRow.AddThemeConstantOverride("separation", 12);
		cacheRow.AddChild(clearCache);
		cacheRow.AddChild(_cacheLabel);
		AddChild(UiKit.Row("Zwischenspeicher", cacheRow, "Bereits gesprochene Sätze werden gespeichert und nicht neu erzeugt."));
		RefreshCacheLabel();
	}

	private void RebuildVoiceDetails()
	{
		UiKit.ClearChildren(_voiceDetails);
		TtsProviderKind kind = LauncherConfig.TtsProvider;

		var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_voiceDetails.AddChild(UiKit.Row("Status", status));

		switch (kind)
		{
			case TtsProviderKind.Gemini:
				var ttsModel = new LineEdit { Text = LauncherConfig.GeminiTtsModel, PlaceholderText = LauncherConfig.DefaultGeminiTtsModel };
				ttsModel.TextSubmitted += text => LauncherConfig.GeminiTtsModel = text;
				ttsModel.FocusExited += () => LauncherConfig.GeminiTtsModel = ttsModel.Text;
				_voiceDetails.AddChild(UiKit.Row("Sprachmodell", ttsModel));
				_voiceDetails.AddChild(UiKit.Hint("Nutzt denselben Gemini-Schlüssel wie die Dialog-KI und verbraucht Kontingent. Dauert pro Satz etwa 1–3 Sekunden."));
				break;

			case TtsProviderKind.ElevenLabs:
				var key = new LineEdit { Text = LauncherConfig.ElevenLabsKey, Secret = true, PlaceholderText = "API-Schlüssel (oder Umgebungsvariable ELEVENLABS_API_KEY)" };
				key.TextSubmitted += text => { LauncherConfig.ElevenLabsKey = text; RebuildVoiceDetails(); };
				key.FocusExited += () => LauncherConfig.ElevenLabsKey = key.Text;
				_voiceDetails.AddChild(UiKit.Row("Schlüssel", key));

				var model = new LineEdit { Text = LauncherConfig.ElevenLabsModel, PlaceholderText = LauncherConfig.DefaultElevenLabsModel };
				model.TextSubmitted += text => LauncherConfig.ElevenLabsModel = text;
				model.FocusExited += () => LauncherConfig.ElevenLabsModel = model.Text;
				_voiceDetails.AddChild(UiKit.Row("Modell", model));
				_voiceDetails.AddChild(UiKit.Hint("Für jede Figur die Voice-ID aus deinem ElevenLabs-Konto eintragen. Dieser Anbieter ist noch nicht mit einem echten Schlüssel geprüft."));
				break;

			case TtsProviderKind.System:
				_voiceDetails.AddChild(UiKit.Hint("Windows-Stimmen klingen oft mechanisch. Weitere lassen sich in den Windows-Spracheinstellungen nachinstallieren."));
				break;
		}

		ITtsProvider provider = TtsService.ProviderFor(kind);
		string problem = null;
		bool ready = kind == TtsProviderKind.System
			? TtsService.GetSystemVoices().Count > 0
			: provider.IsReady(out problem);
		if (kind == TtsProviderKind.System && !ready)
			problem = "Auf diesem System sind keine Stimmen verfügbar.";

		status.Text = ready ? "Bereit" : problem;
		status.AddThemeColorOverride("font_color", ready ? UiKit.Good : UiKit.Bad);

		foreach (string slot in LauncherConfig.VoiceSlots)
		{
			if (kind == TtsProviderKind.ElevenLabs)
			{
				var voiceId = new LineEdit { Text = LauncherConfig.GetVoice(kind, slot), PlaceholderText = "Voice-ID" };
				voiceId.TextSubmitted += text => LauncherConfig.SetVoice(kind, slot, text.Trim());
				voiceId.FocusExited += () => LauncherConfig.SetVoice(kind, slot, voiceId.Text.Trim());
				_voiceDetails.AddChild(UiKit.Row($"Stimme: {slot}", voiceId));
				continue;
			}

			System.Collections.Generic.IReadOnlyList<TtsVoice> voices = kind == TtsProviderKind.System
				? TtsService.GetSystemVoices().Select(v => new TtsVoice(v.Id, $"{v.Name} ({v.Language})")).ToList()
				: provider.GetVoices();

			if (voices.Count > 0)
				_voiceDetails.AddChild(UiKit.Row($"Stimme: {slot}", BuildVoiceOptions(kind, slot, voices)));
		}
	}

	private static OptionButton BuildVoiceOptions(TtsProviderKind kind, string slot, System.Collections.Generic.IReadOnlyList<TtsVoice> voices)
	{
		var options = new OptionButton();
		options.AddItem(slot switch
		{
			"Floki" => "Automatisch (Flokis Standardstimme)",
			LauncherConfig.NarratorSlot => "Automatisch (Erzählerstimme)",
			_ => "Automatisch (je Figur eine feste Stimme)",
		});
		options.SetItemMetadata(0, "");

		string configured = LauncherConfig.GetVoice(kind, slot);
		int selected = 0;

		for (int i = 0; i < voices.Count; i++)
		{
			options.AddItem(voices[i].Label);
			options.SetItemMetadata(i + 1, voices[i].Id);
			if (voices[i].Id == configured)
				selected = i + 1;
		}

		options.Selected = selected;
		options.ItemSelected += index => LauncherConfig.SetVoice(kind, slot, options.GetItemMetadata((int)index).AsString());
		return options;
	}

	private async void OnTestVoicePressed(string character)
	{
		if (_voiceTesting)
			return;

		_voiceTesting = true;
		_voiceTestFloki.Disabled = true;
		_voiceTestGuest.Disabled = true;
		SetVoiceResult("Erzeuge Sprache …", UiKit.Muted);
		Stopwatch watch = Stopwatch.StartNew();

		TtsClip clip = await TtsService.PrepareAsync(character, VoiceTestText, force: true);

		if (IsInstanceValid(this) && IsInsideTree())
		{
			if (clip != null)
			{
				TtsService.Play(clip);
				SetVoiceResult($"OK ({watch.ElapsedMilliseconds} ms, {clip.Source})", UiKit.Good);
			}
			else
			{
				SetVoiceResult("Keine Sprache erzeugt – Details im Protokoll unten.", UiKit.Bad);
			}

			_voiceTestFloki.Disabled = false;
			_voiceTestGuest.Disabled = false;
			RefreshCacheLabel();
		}

		_voiceTesting = false;
	}

	private void SetVoiceResult(string text, Color color)
	{
		_voiceTestResult.Text = text;
		_voiceTestResult.AddThemeColorOverride("font_color", color);
	}

	private void RefreshCacheLabel()
	{
		if (_cacheLabel == null)
			return;

		(int files, long bytes) = TtsService.CacheStats();
		_cacheLabel.Text = files == 0 ? "leer" : $"{files} Sätze, {bytes / 1_000_000.0:0.0} MB";
	}

	private void BuildLogSection()
	{
		AddChild(UiKit.Header("Echtzeit-Protokoll"));
		AddChild(UiKit.Hint("Zeigt jede Anfrage an die KI, die Antwort und Fehler – auch während des Spiels, wenn du das Menü öffnest."));

		_log = new RichTextLabel
		{
			BbcodeEnabled = true,
			ScrollFollowing = true,
			SelectionEnabled = true,
			CustomMinimumSize = new Vector2(0, 200),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		AddChild(_log);

		var clear = new Button { Text = "Protokoll leeren", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
		clear.Pressed += LlmLog.Clear;
		AddChild(clear);

		RefillLog();
	}

	private void RefreshKeyStatus()
	{
		if (_keyStatus == null)
			return;

		bool needsKey = LauncherConfig.Provider == LlmProviderKind.Gemini
			|| LauncherConfig.Fallback == LlmProviderKind.Gemini;

		if (LauncherConfig.HasGeminiKey)
		{
			_keyStatus.Text = "Schlüssel gefunden";
			_keyStatus.AddThemeColorOverride("font_color", UiKit.Good);
		}
		else if (needsKey)
		{
			_keyStatus.Text = LauncherConfig.UseEnvironmentKey
				? "Kein Schlüssel: Umgebungsvariable GEMINI_API_KEY ist nicht gesetzt (nach dem Setzen Spiel neu starten)"
				: "Kein Schlüssel eingetragen";
			_keyStatus.AddThemeColorOverride("font_color", UiKit.Bad);
		}
		else
		{
			_keyStatus.Text = "Nicht nötig";
			_keyStatus.AddThemeColorOverride("font_color", UiKit.Muted);
		}
	}

	private async void OnTestAiPressed()
	{
		if (_testing)
			return;

		_testing = true;
		_testButton.Disabled = true;
		SetTestResult("Teste Verbindung …", UiKit.Muted);
		Stopwatch watch = Stopwatch.StartNew();

		string text;
		Color color;
		try
		{
			string reply = await LlmProviderFactory.Create().GetReplyAsync(TestSystemPrompt, TestUserText);
			// The offline demo answers with a mood tag – not part of the test output.
			reply = EmotionTag.Split(reply, null, out _);
			text = $"OK ({watch.ElapsedMilliseconds} ms): {reply}";
			color = UiKit.Good;
		}
		catch (LlmRequestException ex)
		{
			text = $"Fehlgeschlagen: {ex.Message}";
			color = UiKit.Bad;
		}
		catch (System.Exception ex)
		{
			text = $"Unerwarteter Fehler: {ex.Message}";
			color = UiKit.Bad;
		}

		// The screen may have been left (scene change) while the request was running.
		if (IsInstanceValid(this) && IsInsideTree())
		{
			SetTestResult(text, color);
			_testButton.Disabled = false;
		}

		_testing = false;
	}

	private void SetTestResult(string text, Color color)
	{
		_testResult.Text = text;
		_testResult.AddThemeColorOverride("font_color", color);
	}

	private void OnLogEntry(LlmLogEntry entry)
	{
		if (_log != null && IsInstanceValid(_log))
			_log.AppendText(FormatEntry(entry) + "\n");
	}

	private void RefillLog()
	{
		if (_log == null)
			return;

		_log.Clear();
		foreach (LlmLogEntry entry in LlmLog.Entries)
			_log.AppendText(FormatEntry(entry) + "\n");
	}

	private static string FormatEntry(LlmLogEntry entry)
	{
		string kindColor = entry.Kind switch
		{
			LlmLogKind.Anfrage => "#7fb4ff",
			LlmLogKind.Antwort => "#8fd98f",
			LlmLogKind.Fehler => "#ff7a6b",
			_ => "#b0b4bb"
		};

		string duration = entry.DurationMs.HasValue ? $" [color=#8a8f98]({entry.DurationMs} ms)[/color]" : "";
		string text = (entry.Text ?? "").Replace("[", "[lb]");

		return $"[color=#8a8f98]{entry.Time:HH:mm:ss}[/color] [color=#e6bf6e]{entry.Provider}[/color] "
			+ $"[color={kindColor}]{entry.Kind}[/color]{duration} {text}";
	}
}
