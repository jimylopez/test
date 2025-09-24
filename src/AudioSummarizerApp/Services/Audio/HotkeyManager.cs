using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace AudioSummarizerApp.Services.Audio;

public sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 0xB001;
    private const int WmHotkey = 0x0312;

    private IntPtr _windowHandle;
    private HwndSource? _source;
    private bool _registered;

    public event EventHandler? HotkeyPressed;

    public void Register(Window window, string gesture)
    {
        Unregister();

        if (!TryParseGesture(gesture, out var modifiers, out var key))
        {
            throw new ArgumentException($"Combinación de teclas inválida: {gesture}");
        }

        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WindowProc);

        if (!RegisterHotKey(_windowHandle, HotkeyId, modifiers, key))
        {
            throw new InvalidOperationException("No se pudo registrar el hotkey global. Prueba otra combinación.");
        }

        _registered = true;
    }

    public void Unregister()
    {
        if (_registered && _windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HotkeyId);
        }

        if (_source is not null)
        {
            _source.RemoveHook(WindowProc);
            _source = null;
        }

        _registered = false;
        _windowHandle = IntPtr.Zero;
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static bool TryParseGesture(string gesture, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;
        if (string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }

        foreach (var segment in gesture.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = segment.Trim();
            switch (token.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= 0x0002;
                    break;
                case "alt":
                    modifiers |= 0x0001;
                    break;
                case "shift":
                    modifiers |= 0x0004;
                    break;
                case "win":
                case "windows":
                    modifiers |= 0x0008;
                    break;
                default:
                    if (Enum.TryParse<Key>(token, true, out var parsedKey))
                    {
                        key = (uint)KeyInterop.VirtualKeyFromKey(parsedKey);
                    }
                    else if (token.Length == 1)
                    {
                        var character = token.ToUpperInvariant()[0];
                        if (character >= 'A' && character <= 'Z')
                        {
                            key = (uint)KeyInterop.VirtualKeyFromKey((Key)Enum.Parse(typeof(Key), character.ToString()));
                        }
                        else if (character >= '0' && character <= '9')
                        {
                            key = (uint)KeyInterop.VirtualKeyFromKey((Key)Enum.Parse(typeof(Key), "D" + character));
                        }
                    }
                    break;
            }
        }

        return key != 0;
    }

    public void Dispose()
    {
        Unregister();
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
