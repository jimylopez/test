using NAudio.CoreAudioApi;

namespace AudioSummarizerApp.Models;

public record AudioDeviceInfo(string Id, string Name, string Description, DataFlow Flow);
