using System.Diagnostics;
using Godot;

// Shared by the launcher screen and the pause menu, so the live log can be watched during play.
public partial class LauncherPanel : VBoxContainer
{
	private const string TestSystemPrompt = "Du bist ein Verbindungstest. Antworte auf Deutsch mit genau einem kurzen Satz.";
	private const string TestUserText = "Sag kurz Hallo.";
	private const string VoiceTestText = "Hallo Chef. Das hier ist meine Stimme. Ich klinge besser, als ich bin.";

	private static readonly string[] ProviderNames = { "Gemini (Google)", "Offline-Demo (ohne Internet)" };
	private static readonly string[] FallbackNames = { "Keiner", "Gemini (Google)", "Offline-Demo" };

	private Label _keyStatus;
	private Label _testResult;
	private Button _testButton;
	private RichTextLabel _log;
	private bool _testing;

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

		var voices = TtsService.GetVoices();
		if (voices.Count == 0)
		{
			AddChild(UiKit.Hint("Auf diesem System sind keine Stimmen verfügbar. Unter Windows lassen sich Stimmen in den Spracheinstellungen nachinstallieren."));
			return;
		}

		AddChild(UiKit.Row("Antworten vorlesen",
			UiKit.StateToggle("an", "aus", LauncherConfig.TtsEnabled, on => LauncherConfig.TtsEnabled = on)));

		foreach (string slot in LauncherConfig.VoiceSlots)
			AddChild(UiKit.Row($"Stimme: {slot}", BuildVoiceOptions(slot, voices)));

		AddChild(UiKit.SliderRow("Lautstärke", 0, 100, 5, LauncherConfig.TtsVolume,
			value => $"{value:0} %",
			value => LauncherConfig.TtsVolume = (int)value,
			"Wird mit der Gesamtlautstärke aus den Einstellungen multipliziert."));
		AddChild(UiKit.SliderRow("Sprechtempo", 0.5, 2, 0.1, LauncherConfig.TtsRate,
			value => $"{value:0.0}x",
			value => LauncherConfig.TtsRate = value));

		var test = new Button { Text = "Stimme testen", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
		test.Pressed += () => TtsService.Speak("Floki", VoiceTestText, force: true);
		AddChild(test);
	}

	private static OptionButton BuildVoiceOptions(string slot, System.Collections.Generic.IReadOnlyList<VoiceInfo> voices)
	{
		var options = new OptionButton();
		options.AddItem("Automatisch (erste deutsche Stimme)");
		options.SetItemMetadata(0, "");

		string configured = LauncherConfig.GetVoice(slot);
		int selected = 0;

		for (int i = 0; i < voices.Count; i++)
		{
			options.AddItem($"{voices[i].Name} ({voices[i].Language})");
			options.SetItemMetadata(i + 1, voices[i].Id);
			if (voices[i].Id == configured)
				selected = i + 1;
		}

		options.Selected = selected;
		options.ItemSelected += index => LauncherConfig.SetVoice(slot, options.GetItemMetadata((int)index).AsString());
		return options;
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
