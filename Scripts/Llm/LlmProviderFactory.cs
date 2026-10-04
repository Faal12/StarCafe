public static class LlmProviderFactory
{
    // Built per request so changes in the launcher apply to the very next message.
    public static ILlmProvider Create()
    {
        ILlmProvider primary = Create(LauncherConfig.Provider);

        LlmProviderKind? fallback = LauncherConfig.Fallback;
        if (fallback == null || fallback == LauncherConfig.Provider)
            return primary;

        return new FallbackProvider(primary, Create(fallback.Value));
    }

    public static ILlmProvider Create(LlmProviderKind kind) => kind switch
    {
        LlmProviderKind.Gemini => new GeminiProvider(
            LauncherConfig.GeminiModel,
            LauncherConfig.ResolveGeminiKey(),
            LauncherConfig.MaxOutputTokens,
            LauncherConfig.Temperature),
        _ => new OfflineDemoProvider()
    };
}
