using System.Threading.Tasks;

public interface ILlmProvider
{
    Task<string> GetReplyAsync(string systemPrompt, string userText);
}
