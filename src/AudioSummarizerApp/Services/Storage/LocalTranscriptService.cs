using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AudioSummarizerApp.Services.Storage;

public class LocalTranscriptService
{
    public async Task<string> SaveAsync(string folder, string fileName, string content)
    {
        Directory.CreateDirectory(folder);
        var fullPath = Path.Combine(folder, fileName);
        await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8);
        return fullPath;
    }
}
