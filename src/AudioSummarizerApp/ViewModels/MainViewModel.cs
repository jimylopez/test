using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AudioSummarizerApp.Models;
using AudioSummarizerApp.Services.Audio;
using AudioSummarizerApp.Services.Settings;
using AudioSummarizerApp.Services.Storage;
using AudioSummarizerApp.Services.Summarization;
using AudioSummarizerApp.Services.Transcription;

namespace AudioSummarizerApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AudioDeviceService _audioDeviceService = new();
    private readonly RecordingManager _recordingManager;
    private readonly SettingsService _settingsService = new();
    private readonly LocalTranscriptService _localTranscriptService = new();
    private readonly GoogleDriveUploader _googleDriveUploader = new();
    private readonly WhisperTranscriptionService _whisperTranscriptionService = new();
    private readonly OpenAiChatService _openAiChatService = new();
    private readonly GeminiChatService _geminiChatService = new();
    private readonly Timer _statusTimer;
    private AppSettings _settings = new();
    private CancellationTokenSource? _processingCts;

    public MainViewModel()
    {
        _recordingManager = new RecordingManager();
        Devices = new ObservableCollection<AudioDeviceViewModel>();
        AvailableSummaryProviders = new ObservableCollection<string> { "OpenAI", "Gemini" };
        ToggleRecordingCommand = new AsyncRelayCommand(ToggleRecordingAsync);
        RefreshDevicesCommand = new AsyncRelayCommand(LoadDevicesAsync);
        BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ToggleRecordingHotkeyCommand = new AsyncRelayCommand(ToggleRecordingAsync);
        _statusTimer = new Timer(1000);
        _statusTimer.Elapsed += (_, _) => UpdateRecordingStatus();
    }

    public ObservableCollection<AudioDeviceViewModel> Devices { get; }
    public ObservableCollection<string> AvailableSummaryProviders { get; }

    [ObservableProperty]
    private string _recordingStatus = "Listo";

    [ObservableProperty]
    private string _footerMessage = "Aurora Recorder listo";

    [ObservableProperty]
    private string _outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Aurora Recorder");

    [ObservableProperty]
    private string _lastTranscription = string.Empty;

    [ObservableProperty]
    private string _lastSummary = string.Empty;

    [ObservableProperty]
    private string _whisperApiKey = string.Empty;

    [ObservableProperty]
    private string _whisperModel = "gpt-4o-mini-transcribe";

    [ObservableProperty]
    private string _whisperBaseUrl = "https://api.openai.com/v1/audio/transcriptions";

    [ObservableProperty]
    private string _whisperLanguage = "es-CL";

    [ObservableProperty]
    private string _summaryPromptTemplate = "Genera una minuta breve en español chileno con acuerdos, decisiones, tareas con responsables y próximos pasos. Usa viñetas claras. Fuente: {transcription}";

    [ObservableProperty]
    private string _openAiApiKey = string.Empty;

    [ObservableProperty]
    private string _geminiApiKey = string.Empty;

    [ObservableProperty]
    private string _googleCredentialsPath = string.Empty;

    [ObservableProperty]
    private string _googleDriveFolderId = string.Empty;

    [ObservableProperty]
    private string _hotkeyText = "Ctrl+Alt+R";

    [ObservableProperty]
    private bool _runOnStartup;

    [ObservableProperty]
    private string _selectedSummaryProvider = "OpenAI";

    [ObservableProperty]
    private bool _isRecording;

    public string RecordButtonText => IsRecording ? "Detener" : "Grabar";

    public IAsyncRelayCommand ToggleRecordingCommand { get; }
    public IAsyncRelayCommand RefreshDevicesCommand { get; }
    public IRelayCommand BrowseOutputFolderCommand { get; }
    public IAsyncRelayCommand SaveSettingsCommand { get; }
    public IAsyncRelayCommand ToggleRecordingHotkeyCommand { get; }

    public event EventHandler<string>? HotkeyChanged;

    public async Task InitializeAsync()
    {
        _settings = await _settingsService.LoadAsync();
        ApplySettingsToView();
        await LoadDevicesAsync();
        FooterMessage = "Listo para grabar";
    }

    public async Task HandleClosingAsync()
    {
        if (IsRecording)
        {
            await StopRecordingInternalAsync();
        }

        await SaveSettingsAsync();
    }
    private void ApplySettingsToView()
    {
        OutputFolder = _settings.OutputFolder;
        WhisperApiKey = _settings.WhisperApiKey;
        WhisperModel = _settings.WhisperModel;
        WhisperBaseUrl = _settings.WhisperBaseUrl;
        WhisperLanguage = _settings.WhisperLanguage;
        _whisperTranscriptionService.ApiKey = WhisperApiKey;
        _whisperTranscriptionService.Model = WhisperModel;
        _whisperTranscriptionService.BaseUrl = WhisperBaseUrl;
        SummaryPromptTemplate = _settings.SummaryPromptTemplate;
        OpenAiApiKey = _settings.OpenAiApiKey;
        _openAiChatService.ApiKey = OpenAiApiKey;
        GeminiApiKey = _settings.GeminiApiKey;
        _geminiChatService.ApiKey = GeminiApiKey;
        SelectedSummaryProvider = string.IsNullOrWhiteSpace(_settings.SelectedSummaryProvider) ? "OpenAI" : _settings.SelectedSummaryProvider;
        GoogleCredentialsPath = _settings.GoogleCredentialsPath;
        GoogleDriveFolderId = _settings.GoogleDriveFolderId;
        RunOnStartup = _settings.RunOnStartup;
        HotkeyText = _settings.Hotkeys.Gesture;
    }

    private async Task LoadDevicesAsync()
    {
        Devices.Clear();
        foreach (var device in _audioDeviceService.GetActiveDevices())
        {
            var vm = new AudioDeviceViewModel(device.Id, device.Name, device.Description, device.Flow);
            vm.SelectionChanged += (_, _) => HandleDeviceChanged(vm);
            vm.VolumeChanged += (_, _) => HandleDeviceChanged(vm);
            vm.MuteChanged += (_, _) => HandleDeviceChanged(vm);
            vm.SoloChanged += (_, _) => HandleDeviceChanged(vm);

            if (_settings.DeviceSettings.TryGetValue(device.Id, out var stored))
            {
                vm.ApplySettings(stored.IsSelected, stored.Volume, stored.IsMuted, stored.IsSolo);
            }
            else
            {
                vm.ApplySettings(device.Flow == NAudio.CoreAudioApi.DataFlow.Capture, 1.0, false, false);
            }

            Devices.Add(vm);
            HandleDeviceChanged(vm);
        }

        RecordingStatus = $"Dispositivos detectados: {Devices.Count}";
    }

    private void HandleDeviceChanged(AudioDeviceViewModel deviceViewModel)
    {
        _recordingManager.UpdateDeviceState(new DeviceMixConfiguration
        {
            DeviceId = deviceViewModel.Id,
            IsSelected = deviceViewModel.IsSelected,
            IsMuted = deviceViewModel.IsMuted,
            IsSolo = deviceViewModel.IsSolo,
            Volume = deviceViewModel.Volume
        });

        _settings.DeviceSettings[deviceViewModel.Id] = new AudioDeviceSettings
        {
            IsSelected = deviceViewModel.IsSelected,
            IsMuted = deviceViewModel.IsMuted,
            IsSolo = deviceViewModel.IsSolo,
            Volume = deviceViewModel.Volume
        };
    }

    private void BrowseOutputFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = OutputFolder,
            Description = "Selecciona la carpeta donde guardar las grabaciones"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            OutputFolder = dialog.SelectedPath;
            _settings.OutputFolder = OutputFolder;
        }
    }
    private async Task ToggleRecordingAsync()
    {
        if (IsRecording)
        {
            await StopRecordingInternalAsync();
        }
        else
        {
            await StartRecordingAsync();
        }
    }

    private async Task StartRecordingAsync()
    {
        try
        {
            var configs = Devices.Select(d => new DeviceMixConfiguration
            {
                DeviceId = d.Id,
                IsSelected = d.IsSelected,
                IsMuted = d.IsMuted,
                IsSolo = d.IsSolo,
                Volume = d.Volume
            }).ToList();

            if (!configs.Any(c => c.IsSelected))
            {
                RecordingStatus = "Selecciona al menos una entrada";
                return;
            }

            _whisperTranscriptionService.ApiKey = WhisperApiKey;
            _whisperTranscriptionService.Model = WhisperModel;
            _whisperTranscriptionService.BaseUrl = WhisperBaseUrl;
            _openAiChatService.ApiKey = OpenAiApiKey;
            _geminiChatService.ApiKey = GeminiApiKey;

            await _recordingManager.StartAsync(configs, OutputFolder);
            IsRecording = true;
            RecordingStatus = "Grabando...";
            FooterMessage = "Grabando en segundo plano";
            OnPropertyChanged(nameof(RecordButtonText));
            _statusTimer.Start();
        }
        catch (Exception ex)
        {
            RecordingStatus = ex.Message;
            FooterMessage = "Error al iniciar la grabación";
        }
    }

    private async Task StopRecordingInternalAsync()
    {
        try
        {
            RecordingStatus = "Finalizando grabación...";
            _statusTimer.Stop();
            var path = await _recordingManager.StopAsync(CancellationToken.None);
            IsRecording = false;
            OnPropertyChanged(nameof(RecordButtonText));
            if (!string.IsNullOrWhiteSpace(path))
            {
                RecordingStatus = $"Audio guardado: {Path.GetFileName(path)}";
                FooterMessage = "Procesando transcripción";
                _processingCts?.Cancel();
                _processingCts = new CancellationTokenSource();
                _ = ProcessRecordingAsync(path, _processingCts.Token);
            }
            else
            {
                RecordingStatus = "Grabación detenida";
            }
        }
        catch (Exception ex)
        {
            RecordingStatus = ex.Message;
            FooterMessage = "Error al detener la grabación";
        }
    }
    private async Task ProcessRecordingAsync(string audioFilePath, CancellationToken cancellationToken)
    {
        try
        {
            var transcription = await _whisperTranscriptionService.TranscribeAsync(audioFilePath, WhisperLanguage, cancellationToken);
            LastTranscription = transcription;
            var summaryService = ResolveSummaryService();
            var summary = await summaryService.SummarizeAsync(transcription, SummaryPromptTemplate, cancellationToken);
            LastSummary = summary;

            var baseName = Path.GetFileNameWithoutExtension(audioFilePath);
            var transcriptContent = $"Archivo: {Path.GetFileName(audioFilePath)}{Environment.NewLine}Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}{Environment.NewLine}{Environment.NewLine}--- TRANSCRIPCIÓN ---{Environment.NewLine}{transcription}{Environment.NewLine}{Environment.NewLine}--- MINUTA ---{Environment.NewLine}{summary}";
            await _localTranscriptService.SaveAsync(OutputFolder, $"{baseName}_minuta.txt", transcriptContent);

            if (!string.IsNullOrWhiteSpace(GoogleCredentialsPath))
            {
                await _googleDriveUploader.InitializeAsync(GoogleCredentialsPath, cancellationToken);
                await _googleDriveUploader.UploadTextAsync($"{baseName}_minuta.txt", transcriptContent, GoogleDriveFolderId, cancellationToken);
            }

            FooterMessage = "Transcripción lista";
        }
        catch (Exception ex)
        {
            FooterMessage = $"Error en post-proceso: {ex.Message}";
        }
    }

    private ISummarizationService ResolveSummaryService()
    {
        return SelectedSummaryProvider switch
        {
            "Gemini" => _geminiChatService,
            _ => _openAiChatService
        };
    }

    private void UpdateRecordingStatus()
    {
        if (IsRecording && _recordingManager.StartedAt is { } started)
        {
            var elapsed = DateTimeOffset.Now - started;
            RecordingStatus = $"Grabando · {elapsed:mm\\:ss}";
        }
    }

    private async Task SaveSettingsAsync()
    {
        _settings.OutputFolder = OutputFolder;
        _settings.WhisperApiKey = WhisperApiKey;
        _settings.WhisperModel = WhisperModel;
        _settings.WhisperBaseUrl = WhisperBaseUrl;
        _settings.WhisperLanguage = WhisperLanguage;
        _settings.SummaryPromptTemplate = SummaryPromptTemplate;
        _settings.OpenAiApiKey = OpenAiApiKey;
        _settings.GeminiApiKey = GeminiApiKey;
        _settings.SelectedSummaryProvider = SelectedSummaryProvider;
        _settings.GoogleCredentialsPath = GoogleCredentialsPath;
        _settings.GoogleDriveFolderId = GoogleDriveFolderId;
        _settings.RunOnStartup = RunOnStartup;
        _settings.Hotkeys.Gesture = HotkeyText;
        await _settingsService.SaveAsync(_settings);
        HotkeyChanged?.Invoke(this, HotkeyText);
        FooterMessage = "Configuración guardada";
    }

    partial void OnHotkeyTextChanged(string value)
    {
        HotkeyChanged?.Invoke(this, value);
    }

    partial void OnIsRecordingChanged(bool value)
    {
        OnPropertyChanged(nameof(RecordButtonText));
    }

    partial void OnWhisperApiKeyChanged(string value)
    {
        _whisperTranscriptionService.ApiKey = value;
    }

    partial void OnWhisperModelChanged(string value)
    {
        _whisperTranscriptionService.Model = value;
    }

    partial void OnWhisperBaseUrlChanged(string value)
    {
        _whisperTranscriptionService.BaseUrl = value;
    }

    partial void OnOpenAiApiKeyChanged(string value)
    {
        _openAiChatService.ApiKey = value;
    }

    partial void OnGeminiApiKeyChanged(string value)
    {
        _geminiChatService.ApiKey = value;
    }
}
