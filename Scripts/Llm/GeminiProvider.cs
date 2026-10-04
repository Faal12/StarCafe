using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public sealed class GeminiProvider : ILlmProvider
{
    private static readonly System.Net.Http.HttpClient _httpClient =
        new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(45) };

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const int MaxAttempts = 3;
    private const int RetryDelayMs = 1500;
    private const int LoggedErrorLength = 600;

    private readonly string _model;
    private readonly string _apiKey;
    private readonly int _maxOutputTokens;
    private readonly double _temperature;

    public GeminiProvider(string model, string apiKey, int maxOutputTokens, double temperature)
    {
        _model = model;
        _apiKey = apiKey;
        _maxOutputTokens = maxOutputTokens;
        _temperature = temperature;
    }

    public string Name => "Gemini";

    public async Task<string> GetReplyAsync(string systemPrompt, string userText)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            LlmLog.Add(Name, LlmLogKind.Fehler,
                "Kein API-Schlüssel: im Launcher eintragen oder die Umgebungsvariable GEMINI_API_KEY setzen.");
            throw new LlmRequestException("kein API-Schlüssel hinterlegt");
        }

        var requestBody = new GeminiRequest
        {
            SystemInstruction = new SystemInstruction { Parts = new[] { new Part { Text = systemPrompt } } },
            Contents = new[] { new Content { Role = "user", Parts = new[] { new Part { Text = userText } } } },
            GenerationConfig = new GenerationConfig { MaxOutputTokens = _maxOutputTokens, Temperature = _temperature }
        };

        string jsonBody = JsonSerializer.Serialize(requestBody, JsonOptions);
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";

        LlmLog.Add(Name, LlmLogKind.Anfrage,
            $"{_model} · Systemprompt {systemPrompt.Length} Zeichen · „{userText}“");
        Stopwatch watch = Stopwatch.StartNew();

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            bool isLastAttempt = attempt == MaxAttempts;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("x-goog-api-key", _apiKey);

                using HttpResponseMessage response = await _httpClient.SendAsync(request);
                string responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    string reply = ParseReply(responseText, out string problem);
                    if (reply == null)
                    {
                        LlmLog.Add(Name, LlmLogKind.Fehler, problem, watch.ElapsedMilliseconds);
                        throw new LlmRequestException(problem);
                    }

                    LlmLog.Add(Name, LlmLogKind.Antwort, reply, watch.ElapsedMilliseconds);
                    return reply;
                }

                int statusCode = (int)response.StatusCode;
                LlmLog.Add(Name, LlmLogKind.Fehler,
                    $"HTTP {statusCode}: {Shorten(Redact(DescribeError(responseText)))}", watch.ElapsedMilliseconds);

                bool isTemporary = statusCode == 429 || statusCode == 503;
                if (!isTemporary || isLastAttempt)
                    throw new LlmRequestException($"Fehler bei der KI-Anfrage ({statusCode})");
            }
            catch (HttpRequestException ex)
            {
                LlmLog.Add(Name, LlmLogKind.Fehler, $"Netzwerkfehler: {Redact(ex.Message)}", watch.ElapsedMilliseconds);
                if (isLastAttempt)
                    throw new LlmRequestException("keine Internetverbindung", ex);
            }
            catch (TaskCanceledException ex)
            {
                LlmLog.Add(Name, LlmLogKind.Fehler, "Zeitüberschreitung: keine Antwort innerhalb von 45 Sekunden.", watch.ElapsedMilliseconds);
                if (isLastAttempt)
                    throw new LlmRequestException("Zeitüberschreitung", ex);
            }

            LlmLog.Add(Name, LlmLogKind.Info, $"Neuer Versuch ({attempt + 1}/{MaxAttempts}) in {RetryDelayMs / 1000.0:0.#} s …");
            await Task.Delay(RetryDelayMs);
        }

        throw new LlmRequestException("keine Antwort erhalten");
    }

    private static string ParseReply(string responseText, out string problem)
    {
        problem = null;

        GeminiResponse response;
        try
        {
            response = JsonSerializer.Deserialize<GeminiResponse>(responseText, JsonOptions);
        }
        catch (JsonException)
        {
            problem = "ungültige Antwort des Servers";
            return null;
        }

        if (response?.Candidates == null || response.Candidates.Length == 0)
        {
            string blockReason = response?.PromptFeedback?.BlockReason;
            problem = string.IsNullOrEmpty(blockReason) ? "keine Antwort erhalten" : $"Anfrage blockiert ({blockReason})";
            return null;
        }

        Candidate candidate = response.Candidates[0];
        string text = candidate.Content?.Parts == null
            ? ""
            : string.Concat(candidate.Content.Parts.Select(part => part.Text));

        if (string.IsNullOrWhiteSpace(text))
        {
            problem = candidate.FinishReason == "MAX_TOKENS"
                ? "Antwort leer: Token-Limit zu niedrig (im Launcher erhöhen)"
                : $"Antwort leer ({candidate.FinishReason ?? "unbekannt"})";
            return null;
        }

        return text.Trim();
    }

    // Google answers errors with a long JSON document; the log only needs its message and status.
    private static string DescribeError(string body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out JsonElement error))
            {
                string message = ReadString(error, "message");
                string status = ReadString(error, "status");
                if (!string.IsNullOrWhiteSpace(message))
                    return string.IsNullOrEmpty(status) ? message : $"{message} ({status})";
            }
        }
        catch (Exception)
        {
            // Not JSON: fall through to the plain text below.
        }

        return Regex.Replace(body ?? "", @"\s+", " ").Trim();
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private string Redact(string text) =>
        string.IsNullOrEmpty(_apiKey) || text == null ? text : text.Replace(_apiKey, "***");

    private static string Shorten(string text) =>
        text != null && text.Length > LoggedErrorLength ? text.Substring(0, LoggedErrorLength) + "…" : text;
}

public sealed class LlmRequestException : Exception
{
    public LlmRequestException(string message) : base(message) { }
    public LlmRequestException(string message, Exception inner) : base(message, inner) { }
}

public class GeminiRequest
{
    public SystemInstruction SystemInstruction { get; set; }
    public Content[] Contents { get; set; }
    public GenerationConfig GenerationConfig { get; set; }
}

public class GenerationConfig
{
    public int? MaxOutputTokens { get; set; }
    public double? Temperature { get; set; }
}

public class SystemInstruction
{
    public Part[] Parts { get; set; }
}

public class Content
{
    public string Role { get; set; }
    public Part[] Parts { get; set; }
}

public class Part
{
    public string Text { get; set; }
}

public class GeminiResponse
{
    public Candidate[] Candidates { get; set; }
    public PromptFeedback PromptFeedback { get; set; }
}

public class Candidate
{
    public Content Content { get; set; }
    public string FinishReason { get; set; }
}

public class PromptFeedback
{
    public string BlockReason { get; set; }
}
