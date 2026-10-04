using System.Threading.Tasks;

public sealed class FallbackProvider : ILlmProvider
{
    private readonly ILlmProvider _primary;
    private readonly ILlmProvider _secondary;

    public FallbackProvider(ILlmProvider primary, ILlmProvider secondary)
    {
        _primary = primary;
        _secondary = secondary;
    }

    public string Name => $"{_primary.Name} → {_secondary.Name}";

    public async Task<string> GetReplyAsync(string systemPrompt, string userText)
    {
        try
        {
            return await _primary.GetReplyAsync(systemPrompt, userText);
        }
        catch (LlmRequestException ex)
        {
            LlmLog.Add(Name, LlmLogKind.Info, $"{_primary.Name} ausgefallen ({ex.Message}) – wechsle zu {_secondary.Name}.");
            return await _secondary.GetReplyAsync(systemPrompt, userText);
        }
    }
}
