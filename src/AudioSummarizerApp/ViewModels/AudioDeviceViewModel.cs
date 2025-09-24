using CommunityToolkit.Mvvm.ComponentModel;
using NAudio.CoreAudioApi;

namespace AudioSummarizerApp.ViewModels;

public partial class AudioDeviceViewModel : ObservableObject
{
    private bool _suspendNotifications;

    public AudioDeviceViewModel(string id, string name, string description, DataFlow flow)
    {
        Id = id;
        Name = name;
        Description = description;
        Flow = flow;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public DataFlow Flow { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isSolo;

    private double _volume = 1.0;
    public double Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, value))
            {
                VolumeChanged?.Invoke(this, value);
            }
        }
    }

    public event EventHandler<bool>? SelectionChanged;
    public event EventHandler<double>? VolumeChanged;
    public event EventHandler<bool>? MuteChanged;
    public event EventHandler<bool>? SoloChanged;

    partial void OnIsSelectedChanged(bool value)
    {
        if (_suspendNotifications) return;
        SelectionChanged?.Invoke(this, value);
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (_suspendNotifications) return;
        MuteChanged?.Invoke(this, value);
    }

    partial void OnIsSoloChanged(bool value)
    {
        if (_suspendNotifications) return;
        SoloChanged?.Invoke(this, value);
    }

    public void ApplySettings(bool isSelected, double volume, bool isMuted, bool isSolo)
    {
        _suspendNotifications = true;
        IsSelected = isSelected;
        Volume = volume;
        IsMuted = isMuted;
        IsSolo = isSolo;
        _suspendNotifications = false;
    }
}
