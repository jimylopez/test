using System.Threading;
using System.Threading.Tasks;

namespace AudioSummarizerApp.Services.Summarization;

public interface ISummarizationService
{
    string Name { get; }
    Task<string> SummarizeAsync(string transcription, string promptTemplate, CancellationToken cancellationToken = default);
}
