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
	public const string DefaultGeminiTtsModel = "gemini-3.8-flash-tts";
	public const string DefaultElevenLabsModel = "eleven_multilingual_v2";
	public const string DefaultVoiceSlot = "Standard";
	/// <summary>Voice slot of the narrator (event situations and results).</summary>
	public const string NarratorSlot = "Erzähler";

	public static readonly string[] VoiceSlots = { "Floki", NarratorSlot, DefaultVoiceSlot };

	private const string FilePath = "user://launcher.cfg";
	private const string Section = "launcher";
	private const string VoiceSection = "voices";
	private const string EnvironmentKeyName = "GEMINI_API_KEY";
	private const string ElevenLabsEnvironmentKeyName = "ELEVENLABS_API_KEY";

	private static LlmProviderKind _provider = LlmProviderKind.Gemini;
	private static LlmProviderKind? _fallback;
	private static string _geminiModel = DefaultGeminiModel;
	private static bool _useEnvironmentKey = true;
	private static string _apiKey = "";
	private static int _maxOutputTokens = 1024;
	private static double _temperature = 1.0;
	private static TtsProviderKind _ttsProvider = TtsProviderKind.Piper;
	private static string _geminiTtsModel = DefaultGeminiTtsModel;
	private static string _elevenLabsKey = "";
	private static string _elevenLabsModel = DefaultElevenLabsModel;
	private static bool _ttsEnabled = true;
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

	public static TtsProviderKind TtsProvider
	{
		get => _ttsProvider;
		set { if (_ttsProvider != value) { _ttsProvider = value; Save(); } }
	}

	public static string GeminiTtsModel
	{
		get => string.IsNullOrWhiteSpace(_geminiTtsModel) ? DefaultGeminiTtsModel : _geminiTtsModel;
		set { if (_geminiTtsModel != value) { _geminiTtsModel = value?.Trim() ?? ""; Save(); } }
	}

	public static string ElevenLabsKey
	{
		get => _elevenLabsKey;
		set { if (_elevenLabsKey != value) { _elevenLabsKey = value?.Trim() ?? ""; Save(); } }
	}

	public static string ElevenLabsModel
	{
		get => string.IsNullOrWhiteSpace(_elevenLabsModel) ? DefaultElevenLabsModel : _elevenLabsModel;
		set { if (_elevenLabsModel != value) { _elevenLabsModel = value?.Trim() ?? ""; Save(); } }
	}

	public static string ResolveElevenLabsKey() =>
		!string.IsNullOrWhiteSpace(_elevenLabsKey) ? _elevenLabsKey
			: System.Environment.GetEnvironmentVariable(ElevenLabsEnvironmentKeyName) ?? "";

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

	// Voices are stored per provider ("Piper.Floki"), because every provider has its own voice ids.
	private static string VoiceKey(TtsProviderKind kind, string slot) => $"{kind}.{slot}";

	public static string GetVoice(TtsProviderKind kind, string slot) =>
		_voices.TryGetValue(VoiceKey(kind, slot), out string id) ? id : "";

	public static void SetVoice(TtsProviderKind kind, string slot, string voiceId)
	{
		voiceId ??= "";
		if (GetVoice(kind, slot) == voiceId)
			return;

		_voices[VoiceKey(kind, slot)] = voiceId;
		Save();
	}

	private static void Load()
	{
		ConfigFile config = ConfigIo.LoadOrEmpty(FilePath);

		// First start on a machine without a Gemini key: play with the offline demo instead of showing errors.
		LlmProviderKind firstRunProvider = string.IsNullOrWhiteSpace(EnvironmentApiKey) ? LlmProviderKind.OfflineDemo : LlmProviderKind.Gemini;
		_provider = ConfigIo.ReadEnum(config, Section, "provider", firstRunProvider);

		int fallbackRaw = ConfigIo.ReadInt(config, Section, "fallback", -1, -1, 99);
		_fallback = System.Enum.IsDefined(typeof(LlmProviderKind), fallbackRaw) ? (LlmProviderKind)fallbackRaw : null;

		_geminiModel = ConfigIo.ReadString(config, Section, "geminiModel", DefaultGeminiModel);
		_useEnvironmentKey = ConfigIo.ReadBool(config, Section, "useEnvironmentKey", true);
		_apiKey = ConfigIo.ReadString(config, Section, "apiKey", "");
		_maxOutputTokens = ConfigIo.ReadInt(config, Section, "maxOutputTokens", 1024, 256, 2048);
		_temperature = ConfigIo.ReadDouble(config, Section, "temperature", 1.0, 0.0, 2.0);
		_ttsEnabled = ConfigIo.ReadBool(config, Section, "ttsEnabled", true);
		_ttsProvider = ConfigIo.ReadEnum(config, Section, "ttsProvider", TtsProviderKind.Piper);
		_geminiTtsModel = ConfigIo.ReadString(config, Section, "geminiTtsModel", DefaultGeminiTtsModel);
		_elevenLabsKey = ConfigIo.ReadString(config, Section, "elevenLabsKey", "");
		_elevenLabsModel = ConfigIo.ReadString(config, Section, "elevenLabsModel", DefaultElevenLabsModel);
		_ttsVolume = ConfigIo.ReadInt(config, Section, "ttsVolume", 80, 0, 100);
		_ttsRate = ConfigIo.ReadDouble(config, Section, "ttsRate", 1.0, 0.5, 2.0);

		foreach (TtsProviderKind kind in System.Enum.GetValues<TtsProviderKind>())
			foreach (string slot in VoiceSlots)
			{
				// Voices chosen before the provider choice existed were Windows system voices.
				string legacy = kind == TtsProviderKind.System ? ConfigIo.ReadString(config, VoiceSection, slot, "") : "";
				_voices[VoiceKey(kind, slot)] = ConfigIo.ReadString(config, VoiceSection, VoiceKey(kind, slot), legacy);
			}
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
		config.SetValue(Section, "ttsProvider", (int)_ttsProvider);
		config.SetValue(Section, "geminiTtsModel", _geminiTtsModel);
		config.SetValue(Section, "elevenLabsKey", _elevenLabsKey);
		config.SetValue(Section, "elevenLabsModel", _elevenLabsModel);
		config.SetValue(Section, "ttsVolume", _ttsVolume);
		config.SetValue(Section, "ttsRate", _ttsRate);

		foreach (KeyValuePair<string, string> voice in _voices)
			config.SetValue(VoiceSection, voice.Key, voice.Value);

		Error error = config.Save(FilePath);
		if (error != Error.Ok)
			GD.PrintErr($"Launcher-Einstellungen konnten nicht gespeichert werden: {error}");
	}
}
