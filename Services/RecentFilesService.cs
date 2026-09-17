using System.IO;
using System.Text.Json;

namespace PlistExplorer.Services;

public interface IRecentFilesService
{
    List<string> LoadRecentFiles();
    void SaveRecentFiles(IEnumerable<string> recentFiles);
}

public class RecentFilesService : IRecentFilesService
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PlistExplorer"
    );
    private static readonly string FilePath = Path.Combine(AppDataFolder, "recent_files.json");

    public List<string> LoadRecentFiles()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
        }
        catch
        {
            // Fallback gracefully on read errors
        }

        return [];
    }

    public void SaveRecentFiles(IEnumerable<string> recentFiles)
    {
        try
        {
            Directory.CreateDirectory(AppDataFolder);
            string json = JsonSerializer.Serialize(recentFiles.ToList());
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Ignore write/permission errors
        }
    }
}