using System.Threading;
using System.Threading.Tasks;

namespace AudioSummarizerApp.Services.Transcription;

public interface ITranscriptionService
{
    Task<string> TranscribeAsync(string audioFilePath, string language, CancellationToken cancellationToken = default);
}
