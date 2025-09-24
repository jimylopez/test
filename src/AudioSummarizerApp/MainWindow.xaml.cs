using System;
using System.Windows;
using System.Windows.Threading;
using AudioSummarizerApp.ViewModels;
using AudioSummarizerApp.Services.Audio;

namespace AudioSummarizerApp;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly HotkeyManager _hotkeyManager = new();

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closing += OnClosingAsync;
        _hotkeyManager.HotkeyPressed += (_, _) => Dispatcher.Invoke(() => _viewModel.ToggleRecordingHotkeyCommand.Execute(null));
        _viewModel.HotkeyChanged += (_, gesture) => RegisterHotkey(gesture);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        RegisterHotkey(_viewModel.HotkeyText);
    }

    private async void OnClosingAsync(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Closing -= OnClosingAsync;
        await _viewModel.HandleClosingAsync();
        _hotkeyManager.Dispose();
        Close();
    }

    private void RegisterHotkey(string gesture)
    {
        if (!IsLoaded)
        {
            return;
        }

        try
        {
            _hotkeyManager.Register(this, gesture);
        }
        catch (Exception ex)
        {
            _viewModel.FooterMessage = $"No se pudo registrar el atajo: {ex.Message}";
        }
    }
}
