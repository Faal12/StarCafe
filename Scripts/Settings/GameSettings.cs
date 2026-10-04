using System;
using System.Collections.Generic;
using Godot;

public enum CommentFrequency { Aus, Selten, Normal, Oft }
public enum HumorHardness { Mild, Normal, Hart }
public enum ViolenceLevel { Angedeutet, Normal }
public enum ConflictFrequency { Selten, Normal, Oft }
public enum TextSpeed { Langsam, Normal, Schnell, Sofort }
public enum DialogueTextSize { Klein, Normal, Gross }

public static class GameSettings
{
	private const string FilePath = "user://settings.cfg";
	private const string Section = "game";

	private static bool _profanity = true;
	private static HumorHardness _humor = HumorHardness.Normal;
	private static CommentFrequency _flokiComments = CommentFrequency.Normal;
	private static ViolenceLevel _violence = ViolenceLevel.Angedeutet;
	private static ConflictFrequency _conflicts = ConflictFrequency.Normal;
	private static bool _radioEnabled = true;
	private static TextSpeed _textSpeed = TextSpeed.Normal;
	private static double _masterVolume = 0.8;
	private static bool _fullscreen;
	private static DialogueTextSize _dialogueTextSize = DialogueTextSize.Normal;

	static GameSettings()
	{
		Load();
	}

	public static event Action Changed;

	public static bool Profanity
	{
		get => _profanity;
		set { if (_profanity != value) { _profanity = value; Commit(); } }
	}

	public static HumorHardness Humor
	{
		get => _humor;
		set { if (_humor != value) { _humor = value; Commit(); } }
	}

	// Stored for the upcoming Floki comment system; nothing reads it yet.
	public static CommentFrequency FlokiComments
	{
		get => _flokiComments;
		set { if (_flokiComments != value) { _flokiComments = value; Commit(); } }
	}

	public static ViolenceLevel Violence
	{
		get => _violence;
		set { if (_violence != value) { _violence = value; Commit(); } }
	}

	// Stored for the upcoming conflict system; nothing reads it yet.
	public static ConflictFrequency Conflicts
	{
		get => _conflicts;
		set { if (_conflicts != value) { _conflicts = value; Commit(); } }
	}

	// Stored for the upcoming radio; nothing reads it yet.
	public static bool RadioEnabled
	{
		get => _radioEnabled;
		set { if (_radioEnabled != value) { _radioEnabled = value; Commit(); } }
	}

	public static TextSpeed TextSpeed
	{
		get => _textSpeed;
		set { if (_textSpeed != value) { _textSpeed = value; Commit(); } }
	}

	public static double MasterVolume
	{
		get => _masterVolume;
		set { value = Math.Clamp(value, 0.0, 1.0); if (_masterVolume != value) { _masterVolume = value; Commit(); } }
	}

	public static bool Fullscreen
	{
		get => _fullscreen;
		set { if (_fullscreen != value) { _fullscreen = value; Commit(); } }
	}

	public static DialogueTextSize DialogueTextSize
	{
		get => _dialogueTextSize;
		set { if (_dialogueTextSize != value) { _dialogueTextSize = value; Commit(); } }
	}

	// Sized so that three lines still fit into the dialogue box.
	public static int DialogueFontSize => _dialogueTextSize switch
	{
		DialogueTextSize.Klein => 14,
		DialogueTextSize.Gross => 19,
		_ => 16
	};

	public static float CharactersPerSecond => _textSpeed switch
	{
		TextSpeed.Langsam => 25f,
		TextSpeed.Schnell => 110f,
		TextSpeed.Sofort => 0f,
		_ => 50f
	};

	public static IReadOnlyList<string> BuildPromptRules()
	{
		var rules = new List<string>();

		if (!_profanity)
			rules.Add("Verwende keine Kraftausdrücke oder Flüche, auch wenn deine Rolle sonst derb ist.");

		switch (_humor)
		{
			case HumorHardness.Mild:
				rules.Add("Halte deinen Humor mild: neckend statt beißend, kein schwarzer Humor – auch nicht, wenn der Spieler darauf eingeht.");
				break;
			case HumorHardness.Hart:
				rules.Add("Du darfst bissiger und härter werden; schwarzer Humor ist erlaubt, wenn der Spieler das Thema selbst dorthin lenkt.");
				break;
		}

		rules.Add(_violence == ViolenceLevel.Angedeutet
			? "Gewalt und Tod nur andeuten, ohne Details."
			: "Gewalt und Tod darfst du konkreter benennen, aber ohne detaillierte Schilderung von Verletzungen.");

		return rules;
	}

	public static void ResetToDefaults()
	{
		_profanity = true;
		_humor = HumorHardness.Normal;
		_flokiComments = CommentFrequency.Normal;
		_violence = ViolenceLevel.Angedeutet;
		_conflicts = ConflictFrequency.Normal;
		_radioEnabled = true;
		_textSpeed = TextSpeed.Normal;
		_masterVolume = 0.8;
		_fullscreen = false;
		_dialogueTextSize = DialogueTextSize.Normal;
		Commit();
	}

	public static void Apply()
	{
		int bus = AudioServer.GetBusIndex("Master");
		if (bus >= 0)
		{
			AudioServer.SetBusMute(bus, _masterVolume <= 0.001);
			AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb((float)Math.Max(_masterVolume, 0.001)));
		}

		// Only touch the window mode when it differs, so a maximized window is not reset on every call.
		DisplayServer.WindowMode mode = DisplayServer.WindowGetMode();
		bool isFullscreen = mode == DisplayServer.WindowMode.Fullscreen || mode == DisplayServer.WindowMode.ExclusiveFullscreen;
		if (isFullscreen != _fullscreen)
			DisplayServer.WindowSetMode(_fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
	}

	private static void Commit()
	{
		Save();
		Apply();
		Changed?.Invoke();
	}

	private static void Load()
	{
		ConfigFile config = ConfigIo.LoadOrEmpty(FilePath);

		_profanity = ConfigIo.ReadBool(config, Section, "profanity", true);
		_humor = ConfigIo.ReadEnum(config, Section, "humor", HumorHardness.Normal);
		_flokiComments = ConfigIo.ReadEnum(config, Section, "flokiComments", CommentFrequency.Normal);
		_violence = ConfigIo.ReadEnum(config, Section, "violence", ViolenceLevel.Angedeutet);
		_conflicts = ConfigIo.ReadEnum(config, Section, "conflicts", ConflictFrequency.Normal);
		_radioEnabled = ConfigIo.ReadBool(config, Section, "radioEnabled", true);
		_textSpeed = ConfigIo.ReadEnum(config, Section, "textSpeed", TextSpeed.Normal);
		_masterVolume = ConfigIo.ReadDouble(config, Section, "masterVolume", 0.8, 0.0, 1.0);
		_fullscreen = ConfigIo.ReadBool(config, Section, "fullscreen", false);
		_dialogueTextSize = ConfigIo.ReadEnum(config, Section, "dialogueTextSize", DialogueTextSize.Normal);
	}

	private static void Save()
	{
		var config = new ConfigFile();
		config.SetValue(Section, "profanity", _profanity);
		config.SetValue(Section, "humor", (int)_humor);
		config.SetValue(Section, "flokiComments", (int)_flokiComments);
		config.SetValue(Section, "violence", (int)_violence);
		config.SetValue(Section, "conflicts", (int)_conflicts);
		config.SetValue(Section, "radioEnabled", _radioEnabled);
		config.SetValue(Section, "textSpeed", (int)_textSpeed);
		config.SetValue(Section, "masterVolume", _masterVolume);
		config.SetValue(Section, "fullscreen", _fullscreen);
		config.SetValue(Section, "dialogueTextSize", (int)_dialogueTextSize);

		Error error = config.Save(FilePath);
		if (error != Error.Ok)
			GD.PrintErr($"Einstellungen konnten nicht gespeichert werden: {error}");
	}
}
