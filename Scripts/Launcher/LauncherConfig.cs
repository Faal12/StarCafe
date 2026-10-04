using System.Collections.Generic;
using Godot;

public enum LlmProviderKind
{
	Gemini,
	OfflineDemo
}

public static class LauncherConfig
{
	public const string DefaultGeminiModel = "gemini-3.6-flash";
	public const string DefaultVoiceSlot = "Standard";
	public static readonly string[] VoiceSlots = { "Floki", DefaultVoiceSlot };

	private const string FilePath = "user://launcher.cfg";
	private const string Section = "launcher";
	private const string VoiceSection = "voices";
	private const string EnvironmentKeyName = "GEMINI_API_KEY";

	private static LlmProviderKind _provider = LlmProviderKind.Gemini;
	private static LlmProviderKind? _fallback;
	private static string _geminiModel = DefaultGeminiModel;
	private static bool _useEnvironmentKey = true;
	private static string _apiKey = "";
	private static int _maxOutputTokens = 1024;
	private static double _temperature = 1.0;
	private static bool _ttsEnabled;
	private static int _ttsVolume = 80;
	private static double _ttsRate = 1.0;
	private static readonly Dictionary<string, string> _voices = new();

	static LauncherConfig()
	{
		Load();
	}

	public static LlmProviderKind Provider
	{
		get => _provider;
		set { if (_provider != value) { _provider = value; Save(); } }
	}

	public static LlmProviderKind? Fallback
	{
		get => _fallback;
		set { if (_fallback != value) { _fallback = value; Save(); } }
	}

	public static string GeminiModel
	{
		get => string.IsNullOrWhiteSpace(_geminiModel) ? DefaultGeminiModel : _geminiModel;
		set { if (_geminiModel != value) { _geminiModel = value?.Trim() ?? ""; Save(); } }
	}

	public static bool UseEnvironmentKey
	{
		get => _useEnvironmentKey;
		set { if (_useEnvironmentKey != value) { _useEnvironmentKey = value; Save(); } }
	}

	public static string StoredApiKey
	{
		get => _apiKey;
		set { if (_apiKey != value) { _apiKey = value?.Trim() ?? ""; Save(); } }
	}

	public static int MaxOutputTokens
	{
		get => _maxOutputTokens;
		set { value = Mathf.Clamp(value, 256, 2048); if (_maxOutputTokens != value) { _maxOutputTokens = value; Save(); } }
	}

	public static double Temperature
	{
		get => _temperature;
		set { value = Mathf.Clamp(value, 0.0, 2.0); if (_temperature != value) { _temperature = value; Save(); } }
	}

	public static bool TtsEnabled
	{
		get => _ttsEnabled;
		set { if (_ttsEnabled != value) { _ttsEnabled = value; Save(); } }
	}

	public static int TtsVolume
	{
		get => _ttsVolume;
		set { value = Mathf.Clamp(value, 0, 100); if (_ttsVolume != value) { _ttsVolume = value; Save(); } }
	}

	public static double TtsRate
	{
		get => _ttsRate;
		set { value = Mathf.Clamp(value, 0.5, 2.0); if (_ttsRate != value) { _ttsRate = value; Save(); } }
	}

	public static string EnvironmentApiKey => System.Environment.GetEnvironmentVariable(EnvironmentKeyName) ?? "";

	public static string ResolveGeminiKey() => _useEnvironmentKey ? EnvironmentApiKey : _apiKey;

	public static bool HasGeminiKey => !string.IsNullOrWhiteSpace(ResolveGeminiKey());

	public static string GetVoice(string slot) => _voices.TryGetValue(slot, out string id) ? id : "";

	public static void SetVoice(string slot, string voiceId)
	{
		voiceId ??= "";
		if (GetVoice(slot) == voiceId)
			return;

		_voices[slot] = voiceId;
		Save();
	}

	private static void Load()
	{
		ConfigFile config = ConfigIo.LoadOrEmpty(FilePath);

		_provider = ConfigIo.ReadEnum(config, Section, "provider", LlmProviderKind.Gemini);

		int fallbackRaw = ConfigIo.ReadInt(config, Section, "fallback", -1, -1, 99);
		_fallback = System.Enum.IsDefined(typeof(LlmProviderKind), fallbackRaw) ? (LlmProviderKind)fallbackRaw : null;

		_geminiModel = ConfigIo.ReadString(config, Section, "geminiModel", DefaultGeminiModel);
		_useEnvironmentKey = ConfigIo.ReadBool(config, Section, "useEnvironmentKey", true);
		_apiKey = ConfigIo.ReadString(config, Section, "apiKey", "");
		_maxOutputTokens = ConfigIo.ReadInt(config, Section, "maxOutputTokens", 1024, 256, 2048);
		_temperature = ConfigIo.ReadDouble(config, Section, "temperature", 1.0, 0.0, 2.0);
		_ttsEnabled = ConfigIo.ReadBool(config, Section, "ttsEnabled", false);
		_ttsVolume = ConfigIo.ReadInt(config, Section, "ttsVolume", 80, 0, 100);
		_ttsRate = ConfigIo.ReadDouble(config, Section, "ttsRate", 1.0, 0.5, 2.0);

		foreach (string slot in VoiceSlots)
			_voices[slot] = ConfigIo.ReadString(config, VoiceSection, slot, "");
	}

	private static void Save()
	{
		var config = new ConfigFile();
		config.SetValue(Section, "provider", (int)_provider);
		config.SetValue(Section, "fallback", _fallback.HasValue ? (int)_fallback.Value : -1);
		config.SetValue(Section, "geminiModel", _geminiModel);
		config.SetValue(Section, "useEnvironmentKey", _useEnvironmentKey);
		config.SetValue(Section, "apiKey", _apiKey);
		config.SetValue(Section, "maxOutputTokens", _maxOutputTokens);
		config.SetValue(Section, "temperature", _temperature);
		config.SetValue(Section, "ttsEnabled", _ttsEnabled);
		config.SetValue(Section, "ttsVolume", _ttsVolume);
		config.SetValue(Section, "ttsRate", _ttsRate);

		foreach (KeyValuePair<string, string> voice in _voices)
			config.SetValue(VoiceSection, voice.Key, voice.Value);

		Error error = config.Save(FilePath);
		if (error != Error.Ok)
			GD.PrintErr($"Launcher-Einstellungen konnten nicht gespeichert werden: {error}");
	}
}
