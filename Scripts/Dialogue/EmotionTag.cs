using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// The AI starts each reply with a mood tag like "[genervt]". This splits it off
/// so the tag drives the portrait and never shows up in text or speech.
/// </summary>
public static class EmotionTag
{
	private static readonly Regex LeadingTag = new Regex(@"^\s*\[\s*([\p{L}_-]+)\s*\]\s*", RegexOptions.Compiled);

	private static readonly Regex AnyKnownTag = new Regex(@"\[\s*[\p{L}_-]+\s*\]\s*", RegexOptions.Compiled);

	public static string PromptRule(NpcPersona persona)
	{
		string list = string.Join(", ", persona.Emotions.Keys.Select(k => "[" + k + "]"));
		return "\n\nMIMIK (Pflicht)\n- Beginne JEDE Antwort mit genau einem Stimmungs-Tag in eckigen Klammern, passend zu deinem Gesichtsausdruck: "
			+ list + ". Beispiel: \"[genervt] Schon wieder du, Chef?\"\n- Der Tag wird nicht vorgelesen. Benutze sonst keine eckigen Klammern.";
	}

	/// <summary>Returns the reply without the tag; <paramref name="emotion"/> is null if none was found.</summary>
	public static string Split(string reply, NpcPersona persona, out string emotion)
	{
		emotion = null;
		if (string.IsNullOrEmpty(reply))
		{
			return reply;
		}
		Match match = LeadingTag.Match(reply);
		if (!match.Success)
		{
			return reply;
		}
		string tag = match.Groups[1].Value.ToLowerInvariant();
		if (persona != null && persona.HasEmotions && !persona.Emotions.ContainsKey(tag))
		{
			return reply;
		}
		emotion = tag;
		string rest = reply.Substring(match.Length);
		// Strip stray tags of known emotions the model may repeat mid-text.
		if (persona != null && persona.HasEmotions)
		{
			rest = AnyKnownTag.Replace(rest, m =>
			{
				string inner = m.Value.Trim().Trim('[', ']').Trim().ToLowerInvariant();
				return persona.Emotions.ContainsKey(inner) ? "" : m.Value;
			});
		}
		return rest.Trim();
	}
}
