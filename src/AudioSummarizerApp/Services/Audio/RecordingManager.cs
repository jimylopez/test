using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AudioSummarizerApp.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioSummarizerApp.Services.Audio;

public class RecordingManager
{
    private readonly Dictionary<string, DeviceState> _deviceStates = new();
    private readonly List<RecordingDeviceSession> _activeSessions = new();
    private readonly object _gate = new();

    public bool IsRecording { get; private set; }
    public string? CurrentOutputPath { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }

    public void UpdateDeviceState(DeviceMixConfiguration configuration)
    {
        lock (_gate)
        {
            if (_deviceStates.TryGetValue(configuration.DeviceId, out var state))
            {
                state.IsSelected = configuration.IsSelected;
                state.IsMuted = configuration.IsMuted;
                state.IsSolo = configuration.IsSolo;
                state.Volume = configuration.Volume;
            }
            else
            {
                _deviceStates[configuration.DeviceId] = new DeviceState
                {
                    IsSelected = configuration.IsSelected,
                    IsMuted = configuration.IsMuted,
                    IsSolo = configuration.IsSolo,
                    Volume = configuration.Volume
                };
            }
        }
    }

    public async Task StartAsync(IEnumerable<DeviceMixConfiguration> configurations, string outputFolder)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("La grabación ya está en curso.");
        }

        Directory.CreateDirectory(outputFolder);
        CurrentOutputPath = Path.Combine(outputFolder, $"Aurora_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.mp3");

        lock (_gate)
        {
            _deviceStates.Clear();
        }

        var selectedConfigs = configurations.Where(c => c.IsSelected).ToList();
        if (!selectedConfigs.Any())
        {
            throw new InvalidOperationException("Selecciona al menos una fuente de audio antes de grabar.");
        }

        var enumerator = new MMDeviceEnumerator();
        try
        {
            foreach (var config in selectedConfigs)
            {
                UpdateDeviceState(config);
                var device = enumerator.GetDevice(config.DeviceId);
                var tempPath = Path.Combine(Path.GetTempPath(), $"aurora_{Guid.NewGuid():N}.wav");
                var session = new RecordingDeviceSession(device, tempPath, () => GetEffectiveGain(config.DeviceId));
                _activeSessions.Add(session);
            }

            foreach (var session in _activeSessions)
            {
                session.Start();
            }
        }
        catch
        {
            foreach (var session in _activeSessions)
            {
                session.Dispose();
            }
            _activeSessions.Clear();
            throw;
        }
        finally
        {
            enumerator.Dispose();
        }

        StartedAt = DateTimeOffset.Now;
        IsRecording = true;
        await Task.CompletedTask;
    }

    public async Task<string> StopAsync(CancellationToken cancellationToken)
    {
        if (!IsRecording)
        {
            return string.Empty;
        }

        foreach (var session in _activeSessions)
        {
            session.Stop();
        }

        foreach (var session in _activeSessions)
        {
            session.Dispose();
        }

        var tempFiles = _activeSessions.Select(s => s.TempFilePath).ToList();
        _activeSessions.Clear();

        try
        {
            if (CurrentOutputPath == null)
            {
                throw new InvalidOperationException("No se definió la ruta de salida.");
            }

            await MixDownAsync(tempFiles, CurrentOutputPath, cancellationToken);
            return CurrentOutputPath;
        }
        finally
        {
            foreach (var file in tempFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // ignore deletion errors
                }
            }

            IsRecording = false;
            StartedAt = null;
        }
    }

    public float GetEffectiveGain(string deviceId)
    {
        lock (_gate)
        {
            if (!_deviceStates.TryGetValue(deviceId, out var state))
            {
                return 0f;
            }

            if (!state.IsSelected)
            {
                return 0f;
            }

            if (state.IsMuted)
            {
                return 0f;
            }

            var anySolo = _deviceStates.Values.Any(s => s.IsSolo && s.IsSelected);
            if (anySolo && !state.IsSolo)
            {
                return 0f;
            }

            return (float)Math.Clamp(state.Volume, 0, 4);
        }
    }

    private async Task MixDownAsync(IReadOnlyCollection<string> tempFiles, string outputFilePath, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            var finalFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            var inputs = new List<ISampleProvider>();
            var disposables = new List<IDisposable>();

            try
            {
                foreach (var file in tempFiles)
                {
                    if (!File.Exists(file))
                    {
                        continue;
                    }

                    var reader = new AudioFileReader(file);
                    disposables.Add(reader);
                    ISampleProvider provider = reader;

                    if (reader.WaveFormat.SampleRate != finalFormat.SampleRate)
                    {
                        provider = new WdlResamplingSampleProvider(provider, finalFormat.SampleRate);
                    }

                    if (provider.WaveFormat.Channels != finalFormat.Channels)
                    {
                        provider = provider.WaveFormat.Channels switch
                        {
                            1 when finalFormat.Channels == 2 => new MonoToStereoSampleProvider(provider),
                            2 when finalFormat.Channels == 1 => new StereoToMonoSampleProvider(provider),
                            _ => provider
                        };
                    }

                    inputs.Add(provider);
                }

                if (!inputs.Any())
                {
                    throw new InvalidOperationException("No se pudo generar el archivo de audio final.");
                }

                var mixer = new MixingSampleProvider(inputs) { ReadFully = true };
                var waveProvider = new SampleToWaveProvider16(mixer);
                Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);

                using var fileStream = File.Create(outputFilePath);
                using var mp3Writer = new NAudio.Lame.LameMP3FileWriter(fileStream, waveProvider.WaveFormat, NAudio.Lame.LAMEPreset.V2);
                var buffer = new byte[waveProvider.WaveFormat.AverageBytesPerSecond];
                int read;
                while ((read = waveProvider.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    mp3Writer.Write(buffer, 0, read);
                }

                mp3Writer.Flush();
            }
            finally
            {
                foreach (var disposable in disposables)
                {
                    disposable.Dispose();
                }
            }
        }, cancellationToken);
    }

    private sealed class DeviceState
    {
        public bool IsSelected { get; set; }
        public bool IsMuted { get; set; }
        public bool IsSolo { get; set; }
        public double Volume { get; set; } = 1.0;
    }
}
