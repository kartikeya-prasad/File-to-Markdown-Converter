using FileToMarkdown.Core.Updates;
using Xunit;

namespace FileToMarkdown.Core.Tests;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("0.2.0", "v0.3.0", true)]
    [InlineData("0.2.0", "0.2.1", true)]
    [InlineData("0.2.0", "v0.2.0", false)]
    [InlineData("0.3.0", "v0.2.9", false)]
    [InlineData("0.0.0-dev", "v0.1.0", true)]
    [InlineData("1.9.0", "v1.10.0", true)]     // numeric, not lexicographic
    [InlineData("0.3.0-beta.1", "v0.3.0", true)] // release beats its prerelease
    [InlineData("0.3.0", "v0.3.0-beta.1", false)]
    [InlineData("0.2.0", "not-a-version", false)]
    [InlineData("garbage", "v1.0.0", false)]
    public void IsNewer(string current, string candidate, bool expected) =>
        Assert.Equal(expected, UpdateChecker.IsNewer(current, candidate));

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3+abc123", "1.2.3")]
    [InlineData("v0.3.0-beta.1+meta", "0.3.0-beta.1")]
    public void Normalize(string input, string expected) =>
        Assert.Equal(expected, UpdateChecker.Normalize(input));

    [Fact]
    public void Plan_NewerRelease_SelectsInstallerAndChecksums()
    {
        var release = new GitHubRelease
        {
            TagName = "v9.9.9",
            HtmlUrl = "https://example.com/release",
            Body = "notes",
            Assets =
            {
                new GitHubReleaseAsset { Name = "FileToMarkdownConverter-portable-x64.zip" },
                new GitHubReleaseAsset { Name = "FileToMarkdownConverter-Setup.exe" },
                new GitHubReleaseAsset { Name = "FileToMarkdownConverter.msi" },
                new GitHubReleaseAsset { Name = "SHA256SUMS.txt" },
            },
        };

        var plan = UpdateChecker.Plan(release, "0.2.0");

        Assert.NotNull(plan);
        Assert.Equal("9.9.9", plan!.LatestVersion);
        Assert.Equal("FileToMarkdownConverter-Setup.exe", plan.Installer?.Name);
        Assert.Equal("SHA256SUMS.txt", plan.Checksums?.Name);
        Assert.Equal("notes", plan.ReleaseNotes);
    }

    [Fact]
    public void Plan_SameVersion_ReturnsNull() =>
        Assert.Null(UpdateChecker.Plan(new GitHubRelease { TagName = "v0.2.0" }, "0.2.0"));

    [Fact]
    public void FindChecksum_ParsesStandardFormat()
    {
        var hash = new string('a', 64);
        var sums = $"# comment\n{hash}  FileToMarkdownConverter-Setup.exe\n{new string('b', 64)}  other.zip\n";

        Assert.Equal(hash, UpdateChecker.FindChecksum(sums, "FileToMarkdownConverter-Setup.exe"));
        Assert.Equal(new string('b', 64), UpdateChecker.FindChecksum(sums, "OTHER.ZIP"));
        Assert.Null(UpdateChecker.FindChecksum(sums, "missing.exe"));
    }

    [Fact]
    public void FindChecksum_ToleratesBinaryMarkerAndUppercaseHash()
    {
        var sums = $"{new string('C', 64)} *Setup.exe\n";
        Assert.Equal(new string('c', 64), UpdateChecker.FindChecksum(sums, "Setup.exe"));
    }
}
