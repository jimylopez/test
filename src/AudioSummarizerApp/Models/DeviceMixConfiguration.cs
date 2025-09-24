namespace AudioSummarizerApp.Models;

public class DeviceMixConfiguration
{
    public required string DeviceId { get; init; }
    public bool IsSelected { get; init; }
    public bool IsMuted { get; init; }
    public bool IsSolo { get; init; }
    public double Volume { get; init; } = 1.0;
}
