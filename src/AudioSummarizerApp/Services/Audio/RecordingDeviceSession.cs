using System;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AudioSummarizerApp.Services.Audio;

public sealed class RecordingDeviceSession : IDisposable
{
    private readonly IWaveIn _capture;
    private readonly WaveFileWriter _writer;
    private readonly Func<float> _gainProvider;
    private readonly object _sync = new();
    private bool _isDisposed;

    public RecordingDeviceSession(MMDevice device, string tempFilePath, Func<float> gainProvider)
    {
        _gainProvider = gainProvider;
        if (device.DataFlow == DataFlow.Render)
        {
            _capture = new WasapiLoopbackCapture(device);
        }
        else
        {
            _capture = new WasapiCapture(device);
        }

        if (_capture.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
        {
            _capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, Math.Max(1, _capture.WaveFormat.Channels));
        }

        TempFilePath = tempFilePath;
        _writer = new WaveFileWriter(tempFilePath, _capture.WaveFormat);
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
    }

    public string TempFilePath { get; }

    public void Start()
    {
        _capture.StartRecording();
    }

    public void Stop()
    {
        try
        {
            _capture.StopRecording();
        }
        catch
        {
            // ignored
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            var buffer = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, buffer, e.BytesRecorded);
            var waveBuffer = new WaveBuffer(buffer);
            var gain = _gainProvider();
            if (gain != 1f)
            {
                int samples = e.BytesRecorded / sizeof(float);
                for (int i = 0; i < samples; i++)
                {
                    waveBuffer.FloatBuffer[i] *= gain;
                }
            }

            _writer.Write(buffer, 0, e.BytesRecorded);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            System.Diagnostics.Debug.WriteLine(e.Exception);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.Dispose();
            _writer.Dispose();
        }
    }
}
