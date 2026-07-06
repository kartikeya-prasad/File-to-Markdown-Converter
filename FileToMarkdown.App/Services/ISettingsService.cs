namespace FileToMarkdown.App.Services;

/// <summary>Persistence for <see cref="AppSettings"/> (DI-friendly seam over the JSON store).</summary>
public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}

/// <summary>Default implementation backed by the %LOCALAPPDATA% JSON file.</summary>
public sealed class JsonSettingsService : ISettingsService
{
    public AppSettings Load() => SettingsService.Load();
    public void Save(AppSettings settings) => SettingsService.Save(settings);
}
