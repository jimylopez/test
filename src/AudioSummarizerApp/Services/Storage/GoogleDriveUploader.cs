using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace AudioSummarizerApp.Services.Storage;

public class GoogleDriveUploader : IDisposable
{
    private const string ApplicationName = "Aurora Recorder";
    private static readonly string[] Scopes = { DriveService.ScopeDriveFile };
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private DriveService? _driveService;
    private string? _credentialsPath;

    public async Task InitializeAsync(string credentialsPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(credentialsPath) || !File.Exists(credentialsPath))
        {
            throw new FileNotFoundException("No se encontraron credenciales de Google Drive", credentialsPath);
        }

        if (_driveService != null && string.Equals(_credentialsPath, credentialsPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_driveService != null && string.Equals(_credentialsPath, credentialsPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                "user",
                cancellationToken,
                new FileDataStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AuroraRecorder", "google-token"), true));

            _driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
            });

            _credentialsPath = credentialsPath;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<string> UploadTextAsync(string fileName, string content, string? folderId, CancellationToken cancellationToken)
    {
        if (_driveService is null)
        {
            throw new InvalidOperationException("Debes inicializar Google Drive antes de subir archivos.");
        }

        var fileMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = fileName,
            MimeType = "text/plain"
        };

        if (!string.IsNullOrWhiteSpace(folderId))
        {
            fileMetadata.Parents = new[] { folderId };
        }

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var request = _driveService.Files.Create(fileMetadata, stream, "text/plain");
        request.Fields = "id, webViewLink";
        await request.UploadAsync(cancellationToken);
        var file = request.ResponseBody;
        return file?.Id ?? string.Empty;
    }

    public void Dispose()
    {
        _driveService?.Dispose();
    }
}
