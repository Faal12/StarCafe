using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

public class GeminiProvider : ILlmProvider
{
    private static readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient();
    private static readonly JsonSerializerOptions JsonOptions =
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private const int MaxAttempts = 3;
    private const int RetryDelayMs = 1500;

    private readonly string _model;
    private readonly string _apiKey;

    public GeminiProvider(string model = "gemini-3.6-flash", string apiKey = null)
    {
        _model = model;
        _apiKey = apiKey ?? System.Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    }

    public async Task<string> GetReplyAsync(string systemPrompt, string userText)
    {
        var requestBody = new GeminiRequest
        {
            SystemInstruction = new SystemInstruction { Parts = new[] { new Part { Text = systemPrompt } } },
            Contents = new[] { new Content { Role = "user", Parts = new[] { new Part { Text = userText } } } }
        };

        string jsonBody = JsonSerializer.Serialize(requestBody, JsonOptions);
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(url, httpContent);
                string responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    GeminiResponse parsed = JsonSerializer.Deserialize<GeminiResponse>(responseText, JsonOptions);
                    return parsed.Candidates[0].Content.Parts[0].Text;
                }

                int statusCode = (int)response.StatusCode;
                bool isTemporary = statusCode == 429 || statusCode == 503;
                GD.PrintErr($"Gemini API error {statusCode}: {responseText}");

                if (!isTemporary || attempt == MaxAttempts)
                    throw new LlmRequestException($"ошибка запроса к ИИ ({statusCode})");
            }
            catch (HttpRequestException ex)
            {
                GD.PrintErr($"Сетевая ошибка: {ex.Message}");
                if (attempt == MaxAttempts)
                    throw new LlmRequestException("нет связи с интернетом", ex);
            }

            await Task.Delay(RetryDelayMs);
        }

        throw new LlmRequestException("не удалось получить ответ");
    }
}

public class LlmRequestException : Exception
{
    public LlmRequestException(string message) : base(message) { }
    public LlmRequestException(string message, Exception inner) : base(message, inner) { }
}

public class GeminiRequest
{
    public SystemInstruction SystemInstruction { get; set; }
    public Content[] Contents { get; set; }
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
}

public class Candidate
{
    public Content Content { get; set; }
}
