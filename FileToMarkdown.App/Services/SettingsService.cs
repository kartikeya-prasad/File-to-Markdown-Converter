using System.Text.Json;

namespace FileToMarkdown.App.Services;

/// <summary>
/// Loads/saves <see cref="AppSettings"/> to a JSON file under %LOCALAPPDATA%.
/// (Unpackaged WinUI apps have no <c>ApplicationData.Current</c>, so we use a plain file.)
/// </summary>
public static class SettingsService
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "FileToMarkdownConverter");

    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { /* fall through to defaults */ }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
        }
        catch { /* best-effort persistence */ }
    }
}
