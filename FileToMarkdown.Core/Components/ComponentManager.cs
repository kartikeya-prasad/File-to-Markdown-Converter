using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using FileToMarkdown.Core.Updates;

namespace FileToMarkdown.Core.Components;

/// <summary>
/// Manages the optional "Enhanced PDF OCR" component: OCRmyPDF (pip, into the bundled
/// Python) plus the tesseract CLI and Ghostscript it requires. The two native tools are
/// downloaded by the user from their official distribution points at install time and
/// placed under %LOCALAPPDATA% - the app itself never redistributes them (Ghostscript
/// is AGPL-licensed, so bundling it in the installer is off the table).
/// </summary>
public sealed class ComponentManager
{
    private const string TesseractReleaseApi =
        "https://api.github.com/repos/UB-Mannheim/tesseract/releases/latest";
    private const string GhostscriptReleaseApi =
        "https://api.github.com/repos/ArtifexSoftware/ghostpdl-downloads/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("FileToMarkdownConverter", "1.0"));
        return client;
    }

    public static string ComponentsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FileToMarkdownConverter", "components");

    /// <summary>tesseract.exe from the component install, or from PATH, else null.</summary>
    public static string? TesseractExe =>
        FirstExisting(Path.Combine(ComponentsDir, "tesseract", "tesseract.exe"))
        ?? FindOnPath("tesseract.exe");

    /// <summary>The 64-bit Ghostscript console exe from the component install or PATH, else null.</summary>
    public static string? GhostscriptExe =>
        FirstExisting(Path.Combine(ComponentsDir, "gs", "bin", "gswin64c.exe"))
        ?? FindOnPath("gswin64c.exe");

    /// <summary>True when everything OCRmyPDF needs is present.</summary>
    public static bool IsOcrmyPdfReady() =>
        TesseractExe is not null
        && GhostscriptExe is not null
        && PythonPackageInstaller.IsPackagePresent("ocrmypdf");

    /// <summary>
    /// Installs the full component: pip ocrmypdf + silent installs of the official
    /// tesseract (UB Mannheim build) and Ghostscript releases into <see cref="ComponentsDir"/>.
    /// </summary>
    public async Task InstallAsync(IProgress<string>? progress, CancellationToken ct = default)
    {
        Directory.CreateDirectory(ComponentsDir);

        if (!PythonPackageInstaller.IsPackagePresent("ocrmypdf"))
        {
            progress?.Report("Installing ocrmypdf (pip)…");
            await new PythonPackageInstaller().InstallAsync("ocrmypdf", progress, ct).ConfigureAwait(false);
        }

        if (FirstExisting(Path.Combine(ComponentsDir, "tesseract", "tesseract.exe")) is null
            && FindOnPath("tesseract.exe") is null)
        {
            progress?.Report("Downloading Tesseract OCR (official UB Mannheim build)…");
            await InstallFromGitHubAsync(
                TesseractReleaseApi,
                assetMatch: name => name.StartsWith("tesseract-ocr-w64-setup", StringComparison.OrdinalIgnoreCase)
                                    && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase),
                targetDir: Path.Combine(ComponentsDir, "tesseract"),
                progress, ct).ConfigureAwait(false);
        }

        if (FirstExisting(Path.Combine(ComponentsDir, "gs", "bin", "gswin64c.exe")) is null
            && FindOnPath("gswin64c.exe") is null)
        {
            progress?.Report("Downloading Ghostscript (official Artifex release)…");
            await InstallFromGitHubAsync(
                GhostscriptReleaseApi,
                assetMatch: name => name.StartsWith("gs", StringComparison.OrdinalIgnoreCase)
                                    && name.EndsWith("w64.exe", StringComparison.OrdinalIgnoreCase),
                targetDir: Path.Combine(ComponentsDir, "gs"),
                progress, ct).ConfigureAwait(false);
        }

        if (!IsOcrmyPdfReady())
            throw new InvalidOperationException(
                "Enhanced PDF OCR install finished but a required tool is still missing. "
                + "Check the log and try again.");
    }

    private static async Task InstallFromGitHubAsync(
        string releaseApi, Func<string, bool> assetMatch, string targetDir,
        IProgress<string>? progress, CancellationToken ct)
    {
        var json = await Http.GetStringAsync(releaseApi, ct).ConfigureAwait(false);
        var release = JsonSerializer.Deserialize<GitHubRelease>(json)
            ?? throw new InvalidOperationException($"Unexpected response from {releaseApi}.");

        var asset = release.Assets.FirstOrDefault(a => assetMatch(a.Name))
            ?? throw new InvalidOperationException(
                $"No matching Windows installer asset found in {release.TagName}.");

        var installerPath = Path.Combine(Path.GetTempPath(), asset.Name);
        progress?.Report($"Downloading {asset.Name} ({asset.Size / (1024 * 1024)} MB)…");
        await using (var file = File.Create(installerPath))
        await using (var stream = await Http.GetStreamAsync(asset.BrowserDownloadUrl, ct).ConfigureAwait(false))
        {
            await stream.CopyToAsync(file, ct).ConfigureAwait(false);
        }

        progress?.Report($"Installing {asset.Name}…");
        // Both are NSIS-style installers: /S = silent, /D=<dir> must be the last
        // argument and takes the remainder of the line (spaces allowed, no quotes).
        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = $"/S /D={targetDir}",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {asset.Name}.");
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"{asset.Name} exited with code {proc.ExitCode}.");

        try { File.Delete(installerPath); } catch { /* best effort */ }
    }

    private static string? FirstExisting(string path) => File.Exists(path) ? path : null;

    private static string? FindOnPath(string exeName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (path is null) return null;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), exeName);
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* malformed PATH entry */ }
        }
        return null;
    }
}
