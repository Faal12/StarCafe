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

        // Floki sees everything on board and remembers the Wirt's deeds (phase 4).
        if (persona.DisplayName == FlokiAdvisor.Name)
        {
            string memory = MemoryService.DescribeForFloki();
            if (memory.Length > 0)
                builder.Append("\n\n").Append(memory);
        }

        // Personas with facial expressions tag every reply with a mood (see EmotionTag).
        if (persona.HasEmotions)
            builder.Append(EmotionTag.PromptRule(persona));

        return builder.ToString();
    }
}
