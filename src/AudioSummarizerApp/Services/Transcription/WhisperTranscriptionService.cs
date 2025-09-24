using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AudioSummarizerApp.Services.Transcription;

public class WhisperTranscriptionService : ITranscriptionService, IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposeClient;

    public WhisperTranscriptionService(HttpClient? httpClient = null)
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

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/audio/transcriptions";
    public string Model { get; set; } = "gpt-4o-mini-transcribe";

    public async Task<string> TranscribeAsync(string audioFilePath, string language, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("Configura la API key de Whisper antes de solicitar una transcripción.");
        }

        await using var stream = File.OpenRead(audioFilePath);
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        form.Add(streamContent, "file", Path.GetFileName(audioFilePath));
        form.Add(new StringContent(Model), "model");
        form.Add(new StringContent(language), "language");

        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
        if (document.RootElement.TryGetProperty("text", out var textProperty))
        {
            return textProperty.GetString() ?? string.Empty;
        }

        throw new InvalidOperationException("La respuesta de la API no contiene el campo 'text'.");
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }
}
