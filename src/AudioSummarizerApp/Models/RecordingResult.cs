using System;

namespace AudioSummarizerApp.Models;

public class RecordingResult
{
    public required string AudioFilePath { get; init; }
    public string? Transcription { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.Now;
}
