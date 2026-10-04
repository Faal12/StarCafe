using System.Threading.Tasks;

public interface ILlmProvider
{
    string Name { get; }

    Task<string> GetReplyAsync(string systemPrompt, string userText);
}
