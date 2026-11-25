using System.Diagnostics;
using System.Threading.Tasks;

public sealed class OfflineDemoProvider : ILlmProvider
{
    private const int FakeLatencyMs = 350;

    private static readonly string[] Lines =
    {
        "[neutral] Ich höre dich, Chef. Ohne echte KI kann ich nur so tun, als wäre ich schlau. (Offline-Demo)",
        "[genervt] Interessant. Falsch, aber interessant. (Offline-Demo)",
        "[froh] Ich würde ja antworten, aber mein Gehirn liegt gerade im Funkloch. (Offline-Demo)",
        "[verlegen] Notiz an mich: später etwas Gescheites sagen. (Offline-Demo)",
        "[genervt] Das ist eine Standardantwort. Gewöhn dich nicht dran. (Offline-Demo)"
    };

    private static int _next;

    public string Name => "Offline-Demo";

    public async Task<string> GetReplyAsync(string systemPrompt, string userText)
    {
        LlmLog.Add(Name, LlmLogKind.Anfrage, $"„{userText}“");
        Stopwatch watch = Stopwatch.StartNew();

        await Task.Delay(FakeLatencyMs);

        string reply = Lines[_next++ % Lines.Length];
        LlmLog.Add(Name, LlmLogKind.Antwort, reply, watch.ElapsedMilliseconds);
        return reply;
    }
}
