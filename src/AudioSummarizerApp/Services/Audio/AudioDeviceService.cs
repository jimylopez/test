using System.Collections.Generic;
using AudioSummarizerApp.Models;
using NAudio.CoreAudioApi;

namespace AudioSummarizerApp.Services.Audio;

public class AudioDeviceService
{
    public IEnumerable<AudioDeviceInfo> GetActiveDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active))
        {
            var description = $"{device.DataFlow} · {device.DeviceFriendlyName}";
            yield return new AudioDeviceInfo(device.ID, device.FriendlyName, description, device.DataFlow);
        }
    }
}
