using System.Text;

public static class PromptComposer
{
    public static string Compose(NpcPersona persona)
    {
        var builder = new StringBuilder(persona.SystemPrompt);

        builder.Append("\n\nSPIELEINSTELLUNGEN (vom Spieler gewählt, verbindlich)");
        foreach (string rule in GameSettings.BuildPromptRules())
            builder.Append("\n- ").Append(rule);

        string worldState = ReputationText.DescribeForPrompt(ReputationService.State);
        if (worldState.Length > 0)
            builder.Append("\n\n").Append(worldState);

        return builder.ToString();
    }
}
