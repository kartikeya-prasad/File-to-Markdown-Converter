using Xunit;

namespace FileToMarkdown.Core.Tests;

public class OutputNamingTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("f2md-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void GetBaseName_LocalFileUsesFileNameWithoutExtension() =>
        Assert.Equal("report", ConversionService.GetBaseName(Path.Combine(_dir, "report.pdf")));

    [Fact]
    public void GetBaseName_YouTubeUrlPrefersVideoId() =>
        Assert.Equal("dQw4w9WgXcQ", ConversionService.GetBaseName("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));

    [Fact]
    public void GetBaseName_UrlFallsBackToLastPathSegment() =>
        Assert.Equal("article", ConversionService.GetBaseName("https://example.com/blog/article"));

    [Fact]
    public void GetBaseName_BareHostFallsBackToHost() =>
        Assert.Equal("example.com", ConversionService.GetBaseName("https://example.com"));

    [Fact]
    public void ResolveOutputPath_NewFileUsesPlainName()
    {
        var path = ConversionService.ResolveOutputPath("doc", _dir, OverwritePolicy.Number, out bool skip);
        Assert.False(skip);
        Assert.Equal(Path.Combine(_dir, "doc.md"), path);
    }

    [Fact]
    public void ResolveOutputPath_NumberPolicyAppendsCounter()
    {
        File.WriteAllText(Path.Combine(_dir, "doc.md"), "x");
        File.WriteAllText(Path.Combine(_dir, "doc (1).md"), "x");

        var path = ConversionService.ResolveOutputPath("doc", _dir, OverwritePolicy.Number, out bool skip);
        Assert.False(skip);
        Assert.Equal(Path.Combine(_dir, "doc (2).md"), path);
    }

    [Fact]
    public void ResolveOutputPath_OverwritePolicyKeepsName()
    {
        File.WriteAllText(Path.Combine(_dir, "doc.md"), "x");

        var path = ConversionService.ResolveOutputPath("doc", _dir, OverwritePolicy.Overwrite, out bool skip);
        Assert.False(skip);
        Assert.Equal(Path.Combine(_dir, "doc.md"), path);
    }

    [Fact]
    public void ResolveOutputPath_SkipPolicySignalsSkip()
    {
        File.WriteAllText(Path.Combine(_dir, "doc.md"), "x");

        ConversionService.ResolveOutputPath("doc", _dir, OverwritePolicy.Skip, out bool skip);
        Assert.True(skip);
    }
}
