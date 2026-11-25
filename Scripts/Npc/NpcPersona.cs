using Godot;
using Godot.Collections;

[GlobalClass]
public partial class NpcPersona : Resource
{
	[Export(PropertyHint.None, "")]
	public string DisplayName { get; set; } = "NPC";

	[Export(PropertyHint.MultilineText, "")]
	public string SystemPrompt { get; set; } = "";

	/// <summary>Base portrait (body). With emotions set, this should be the body without eyes.</summary>
	[Export(PropertyHint.None, "")]
	public Texture2D Portrait { get; set; }

	/// <summary>
	/// Optional emotion overlays drawn on top of <see cref="Portrait"/> (same canvas size).
	/// Key = emotion name the AI may use, e.g. "neutral", "froh", "verlegen", "genervt".
	/// </summary>
	[Export(PropertyHint.None, "")]
	public Dictionary<string, Texture2D> Emotions { get; set; } = new Dictionary<string, Texture2D>();

	/// <summary>Emotion shown when the dialogue opens or the AI gives no valid tag.</summary>
	[Export(PropertyHint.None, "")]
	public string DefaultEmotion { get; set; } = "neutral";

	public bool HasEmotions => Emotions != null && Emotions.Count > 0;

	public Texture2D GetEmotion(string emotion)
	{
		if (!HasEmotions)
		{
			return null;
		}
		if (emotion != null && Emotions.TryGetValue(emotion, out Texture2D texture))
		{
			return texture;
		}
		return Emotions.TryGetValue(DefaultEmotion, out Texture2D fallback) ? fallback : null;
	}
}
