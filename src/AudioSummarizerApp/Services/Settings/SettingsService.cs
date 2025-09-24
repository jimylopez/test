using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AudioSummarizerApp.Models;

namespace AudioSummarizerApp.Services.Settings;

public class SettingsService
{
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    public string SettingsDirectory { get; } = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "AuroraRecorder");
    public string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(SettingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _serializerOptions);
        return settings ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, _serializerOptions);
    }
}
