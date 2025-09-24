using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AudioSummarizerApp.Models;

public class AppSettings
{
    public string OutputFolder { get; set; } = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Aurora Recorder");
    public string WhisperApiKey { get; set; } = string.Empty;
    public string WhisperModel { get; set; } = "gpt-4o-mini-transcribe";
    public string WhisperBaseUrl { get; set; } = "https://api.openai.com/v1/audio/transcriptions";
    public string WhisperLanguage { get; set; } = "es-CL";
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string GeminiApiKey { get; set; } = string.Empty;
    public string SummaryPromptTemplate { get; set; } = "Redacta una minuta ejecutiva con bullet points, acuerdos y tareas pendientes a partir de la transcripción. Mantén el idioma español chileno.";
    public string SelectedSummaryProvider { get; set; } = "OpenAI";
    public string GoogleCredentialsPath { get; set; } = string.Empty;
    public string GoogleDriveFolderId { get; set; } = string.Empty;
    public bool RunOnStartup { get; set; } = false;
    public HotkeySettings Hotkeys { get; set; } = new();
    public Dictionary<string, AudioDeviceSettings> DeviceSettings { get; set; } = new();
}

public class HotkeySettings
{
    public string Gesture { get; set; } = "Ctrl+Alt+R";
}

public class AudioDeviceSettings
{
    public bool IsSelected { get; set; }
    public double Volume { get; set; } = 1.0;
    public bool IsMuted { get; set; }
    public bool IsSolo { get; set; }
}
