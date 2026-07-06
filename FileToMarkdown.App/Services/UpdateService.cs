using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using FileToMarkdown.Core.Updates;
using Microsoft.Win32;

namespace FileToMarkdown.App.Services;

public interface IUpdateService
{
    /// <summary>The running app's version, e.g. "0.3.0" ("0.0.0-dev" in local builds).</summary>
    string CurrentVersion { get; }

    /// <summary>True when this copy was installed by the Inno Setup installer (auto-update applies).</summary>
    bool IsInnoInstall { get; }

    /// <summary>Returns an update plan when a newer GitHub Release exists, else null.</summary>
    Task<UpdatePlan?> CheckAsync(CancellationToken ct = default);

    /// <summary>Downloads the plan's Setup.exe to a temp file, verifying SHA-256 when published.</summary>
    Task<string> DownloadInstallerAsync(UpdatePlan plan, CancellationToken ct = default);

    /// <summary>Starts the downloaded installer. The caller should exit the app right after.</summary>
    void LaunchInstaller(string installerPath);

    /// <summary>Opens the release page in the default browser (fallback for MSI/portable installs).</summary>
    void OpenReleasePage(UpdatePlan plan);
}

/// <summary>
/// Lightweight auto-updater over the GitHub Releases feed - no updater framework, so it
/// works cleanly in an unpackaged WinUI 3 app alongside the existing Inno/MSI installers.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/kartikeya-prasad/File-to-Markdown-Converter/releases/latest";

    private const string InnoUninstallKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{A2B5C8D1-6E4F-4A7B-9C3D-1F2E3A4B5C6D}_is1";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("FileToMarkdownConverter", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public string CurrentVersion
    {
        get
        {
            var info = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return UpdateChecker.Normalize(info ?? "0.0.0");
        }
    }

    public bool IsInnoInstall
    {
        get
        {
            foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                using var key = root.OpenSubKey(InnoUninstallKey)
                    ?? root.OpenSubKey(@"SOFTWARE\WOW6432Node" + InnoUninstallKey["SOFTWARE".Length..]);
                if (key is not null) return true;
            }
            return false;
        }
    }

    public async Task<UpdatePlan?> CheckAsync(CancellationToken ct = default)
    {
        using var response = await Http.GetAsync(LatestReleaseApi, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var release = JsonSerializer.Deserialize<GitHubRelease>(json);
        return release is null ? null : UpdateChecker.Plan(release, CurrentVersion);
    }

    public async Task<string> DownloadInstallerAsync(UpdatePlan plan, CancellationToken ct = default)
    {
        var installer = plan.Installer
            ?? throw new InvalidOperationException("This release has no installer asset.");

        var path = Path.Combine(Path.GetTempPath(), installer.Name);
        await using (var file = File.Create(path))
        await using (var stream = await Http.GetStreamAsync(installer.BrowserDownloadUrl, ct).ConfigureAwait(false))
        {
            await stream.CopyToAsync(file, ct).ConfigureAwait(false);
        }

        if (plan.Checksums is not null)
        {
            var sums = await Http.GetStringAsync(plan.Checksums.BrowserDownloadUrl, ct).ConfigureAwait(false);
            var expected = UpdateChecker.FindChecksum(sums, installer.Name);
            if (expected is not null)
            {
                await using var file = File.OpenRead(path);
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, ct).ConfigureAwait(false))
                    .ToLowerInvariant();
                if (actual != expected)
                {
                    File.Delete(path);
                    throw new InvalidOperationException(
                        "Downloaded installer failed SHA-256 verification; the file was discarded.");
                }
            }
        }

        return path;
    }

    public void LaunchInstaller(string installerPath) =>
        Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });

    public void OpenReleasePage(UpdatePlan plan) =>
        Process.Start(new ProcessStartInfo(plan.ReleasePageUrl) { UseShellExecute = true });
}
