namespace FileToMarkdown.Core.Updates;

/// <summary>Everything needed to offer and apply one update.</summary>
public sealed class UpdatePlan
{
    public required string LatestVersion { get; init; }
    public required string ReleasePageUrl { get; init; }
    public string? ReleaseNotes { get; init; }
    /// <summary>The Setup.exe asset, when the release carries one.</summary>
    public GitHubReleaseAsset? Installer { get; init; }
    /// <summary>The SHA256SUMS.txt asset, when the release carries one.</summary>
    public GitHubReleaseAsset? Checksums { get; init; }
}

/// <summary>
/// Pure decision logic for the in-app updater: semver comparison against the GitHub
/// Release tag, installer/checksum asset selection, and SHA256SUMS parsing. All
/// network and process work lives in the App-side UpdateService.
/// </summary>
public static class UpdateChecker
{
    public const string InstallerAssetName = "FileToMarkdownConverter-Setup.exe";
    public const string ChecksumsAssetName = "SHA256SUMS.txt";

    /// <summary>Builds an update plan, or null when <paramref name="release"/> is not newer.</summary>
    public static UpdatePlan? Plan(GitHubRelease release, string currentVersion)
    {
        if (!IsNewer(currentVersion, release.TagName)) return null;

        return new UpdatePlan
        {
            LatestVersion = Normalize(release.TagName),
            ReleasePageUrl = release.HtmlUrl,
            ReleaseNotes = release.Body,
            Installer = release.Assets.FirstOrDefault(a =>
                a.Name.Equals(InstallerAssetName, StringComparison.OrdinalIgnoreCase)),
            Checksums = release.Assets.FirstOrDefault(a =>
                a.Name.Equals(ChecksumsAssetName, StringComparison.OrdinalIgnoreCase)),
        };
    }

    /// <summary>True when <paramref name="candidate"/> is a strictly newer version than <paramref name="current"/>.</summary>
    public static bool IsNewer(string current, string candidate)
    {
        if (!TryParse(current, out var cur) || !TryParse(candidate, out var cand)) return false;
        return Compare(cand, cur) > 0;
    }

    /// <summary>Strips a leading 'v' and any '+build' metadata: "v1.2.3+abc" → "1.2.3".</summary>
    public static string Normalize(string version)
    {
        var v = version.Trim();
        if (v.StartsWith('v') || v.StartsWith('V')) v = v[1..];
        int plus = v.IndexOf('+');
        return plus >= 0 ? v[..plus] : v;
    }

    /// <summary>
    /// Finds the SHA-256 hash for <paramref name="fileName"/> in standard
    /// "&lt;hash&gt;  &lt;name&gt;" checksum-file content (a leading '*' on the name is tolerated).
    /// </summary>
    public static string? FindChecksum(string checksumsText, string fileName)
    {
        foreach (var raw in checksumsText.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            int split = line.IndexOfAny(new[] { ' ', '\t' });
            if (split <= 0) continue;

            var hash = line[..split].Trim();
            var name = line[split..].Trim().TrimStart('*');
            if (name.Equals(fileName, StringComparison.OrdinalIgnoreCase) && hash.Length == 64)
                return hash.ToLowerInvariant();
        }
        return null;
    }

    // ---- Version parsing ----------------------------------------------------

    internal readonly record struct Version(int Major, int Minor, int Patch, string Prerelease);

    internal static bool TryParse(string? input, out Version version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var v = Normalize(input);
        string prerelease = string.Empty;
        int dash = v.IndexOf('-');
        if (dash >= 0)
        {
            prerelease = v[(dash + 1)..];
            v = v[..dash];
        }

        var parts = v.Split('.');
        if (parts.Length is < 1 or > 3) return false;

        int[] nums = { 0, 0, 0 };
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out nums[i]) || nums[i] < 0) return false;
        }

        version = new Version(nums[0], nums[1], nums[2], prerelease);
        return true;
    }

    internal static int Compare(Version a, Version b)
    {
        int n = a.Major.CompareTo(b.Major);
        if (n != 0) return n;
        n = a.Minor.CompareTo(b.Minor);
        if (n != 0) return n;
        n = a.Patch.CompareTo(b.Patch);
        if (n != 0) return n;

        // Same numeric version: a release outranks any prerelease of it.
        bool aPre = a.Prerelease.Length > 0, bPre = b.Prerelease.Length > 0;
        if (aPre && !bPre) return -1;
        if (!aPre && bPre) return 1;
        return string.CompareOrdinal(a.Prerelease, b.Prerelease);
    }
}
