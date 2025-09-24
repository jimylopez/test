using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AudioSummarizerApp.Services.Summarization;

public class OpenAiChatService : ISummarizationService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;

    public OpenAiChatService(HttpClient? httpClient = null)
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

    public string Name => "OpenAI";
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string Model { get; set; } = "gpt-4o-mini";

    public async Task<string> SummarizeAsync(string transcription, string promptTemplate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("Configura la API key de OpenAI para generar minutas.");
        }

        var prompt = promptTemplate.Replace("{transcription}", transcription, StringComparison.OrdinalIgnoreCase);
        var payload = new
        {
            model = Model,
            messages = new object[]
            {
                new { role = "system", content = "Eres un asistente que resume reuniones en español chileno y destaca acuerdos, tareas y riesgos." },
                new { role = "user", content = prompt }
            },
            temperature = 0.2
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(body);
        var choices = document.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("La API no devolvió contenido para el resumen.");
        }

        var message = choices[0].GetProperty("message");
        if (message.TryGetProperty("content", out var content))
        {
            return content.GetString() ?? string.Empty;
        }

        throw new InvalidOperationException("La respuesta de OpenAI no contiene un mensaje de salida.");
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }
}
