using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Floki listens to event conversations through the headset. He comments on his own
/// (frequency from the "Kommentare" setting) or gives a hint when the player asks.
/// Only the player hears him – his lines never reach the speaker's prompt.
/// </summary>
public static class FlokiAdvisor
{
	public const string Name = "Floki";

	private const string PersonaPath = "res://Resources/Npc/Floki.tres";

	private static NpcPersona _persona;

	private static readonly RandomNumberGenerator Rng = CreateRng();

	public static NpcPersona Persona => _persona ??= GD.Load<NpcPersona>(PersonaPath);

	/// <summary>Chance that Floki chimes in after a speaker's line.</summary>
	public static bool ShouldInterject()
	{
		float chance = GameSettings.FlokiComments switch
		{
			CommentFrequency.Aus => 0f,
			CommentFrequency.Selten => 0.15f,
			CommentFrequency.Oft => 0.5f,
			_ => 0.3f,
		};
		return chance > 0f && Rng.Randf() < chance;
	}

	/// <summary>A hint (player asked) or an unasked comment. Null if Floki has nothing to say.</summary>
	public static async Task<string> SpeakAsync(EventConversation conversation, bool hint)
	{
		if (conversation.AiMode && Persona != null)
		{
			try
			{
				ILlmProvider provider = new GeminiProvider(LauncherConfig.GeminiModel, LauncherConfig.ResolveGeminiKey(), EventConversation.OutputTokens, LauncherConfig.Temperature);
				string reply = await provider.GetReplyAsync(BuildSystemPrompt(conversation, hint), BuildUserText(conversation));
				string line = Clean(reply);
				if (line.Length > 0)
				{
					return line;
				}
			}
			catch (LlmRequestException)
			{
				// Falls through to the offline hint.
			}
		}
		return hint ? OfflineHint(conversation) : null;
	}

	private static string Clean(string reply)
	{
		string line = EmotionTag.Split(reply ?? "", Persona, out _);
		if (line.StartsWith(Name + ":"))
		{
			line = line.Substring(Name.Length + 1);
		}
		return line.Replace("*", "").Trim().Trim('"', '„', '“');
	}

	private static string OfflineHint(EventConversation conversation)
	{
		var options = conversation.Def.TalkOutcomes.Where(o => !o.Default && o.Keywords.Count > 0).ToList();
		if (options.Count == 0)
		{
			return "Psst, Chef. Ich habe auch keine Ahnung. Improvisier. Das kannst du doch so gut.";
		}
		EventOutcomeDef pick = options[Rng.RandiRange(0, options.Count - 1)];
		string keyword = pick.Keywords[Rng.RandiRange(0, pick.Keywords.Count - 1)];
		return $"Psst, Chef. Wenn du willst, dass es so ausgeht – „{pick.Label}“ –, sag was mit „{keyword}“. Ich sag ja nur.";
	}

	private static string BuildSystemPrompt(EventConversation conversation, bool hint)
	{
		GameEventDef def = conversation.Def;
		StringBuilder sb = new StringBuilder(PromptComposer.Compose(Persona));
		sb.Append("\n\nGERADE PASSIERT IN DER TAVERNE (du hörst über das Headset mit; nur der Chef hört dich, ")
			.Append(def.Speaker.Name).Append(" nicht)\n");
		sb.Append("Situation: ").Append(def.Text).Append('\n');
		sb.Append("Mögliche Ausgänge und was dafür nötig ist:\n");
		foreach (EventOutcomeDef outcome in def.TalkOutcomes)
		{
			sb.Append("- ").Append(outcome.Label).Append(": ").Append(outcome.Condition).Append('\n');
		}
		sb.Append($"Noch {conversation.TurnsLeft} Antworten, bis sich die Sache entscheidet.\n\n");
		sb.Append(hint
			? "DEINE AUFGABE: Der Chef fragt dich um Rat. Gib ihm EINEN konkreten, brauchbaren Tipp, mit welchem Argument, Angebot oder welcher Tat er einen der Ausgänge erreichen kann – verpackt in Spott. Höchstens zwei kurze Sätze."
			: "DEINE AUFGABE: Wirf ungefragt einen einzigen kurzen, sarkastischen Satz zum Gespräch ein. Kein ausführlicher Rat.");
		sb.Append(" Antworte ohne Sternchen und ohne deinen Namen voranzustellen.");
		return sb.ToString();
	}

	private static string BuildUserText(EventConversation conversation)
	{
		StringBuilder sb = new StringBuilder("BISHERIGES GESPRÄCH:\n");
		foreach ((string speaker, string text) in conversation.History)
		{
			sb.Append(speaker == EventConversation.PlayerName ? "Chef" : speaker).Append(": ").Append(text).Append('\n');
		}
		sb.Append("\nWas sagst du dem Chef über das Headset?");
		return sb.ToString();
	}

	private static RandomNumberGenerator CreateRng()
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();
		return rng;
	}
}
