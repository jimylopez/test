using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AudioSummarizerApp.Services.Summarization;

public class GeminiChatService : ISummarizationService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;

    public GeminiChatService(HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            _httpClient = new HttpClient();
            _disposeClient = true;
        }
        else
        {
            _httpClient = httpClient;
        }
    }

    public string Name => "Gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-1.5-flash";

    public async Task<string> SummarizeAsync(string transcription, string promptTemplate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("Configura la API key de Gemini para generar minutas.");
        }

        var prompt = promptTemplate.Replace("{transcription}", transcription, StringComparison.OrdinalIgnoreCase);
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = "Eres un asistente que resume reuniones en español chileno destacando acuerdos, riesgos y próximos pasos." },
                        new { text = prompt }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var endpoint = $"https://generativelanguage.googleapis.com/v1/models/{Model}:generateContent?key={ApiKey}";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var first = candidates[0];
            if (first.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
            {
                var builder = new StringBuilder();
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text))
                    {
                        builder.AppendLine(text.GetString());
                    }
                }

                return builder.ToString().Trim();
            }
        }

        throw new InvalidOperationException("La respuesta de Gemini no contiene texto utilizable.");
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }
}
