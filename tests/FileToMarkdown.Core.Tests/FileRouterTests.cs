using Xunit;

namespace FileToMarkdown.Core.Tests;

public class FileRouterTests
{
    [Theory]
    [InlineData("https://example.com/page")]
    [InlineData("http://example.com")]
    public void IsUrl_AcceptsHttpAndHttps(string source) =>
        Assert.True(FileRouter.IsUrl(source));

    [Theory]
    [InlineData(@"C:\docs\file.pdf")]
    [InlineData("ftp://example.com/file.pdf")]
    [InlineData("not a url")]
    public void IsUrl_RejectsNonHttp(string source) =>
        Assert.False(FileRouter.IsUrl(source));

    [Theory]
    [InlineData(@"C:\pics\scan.png")]
    [InlineData(@"C:\pics\scan.JPG")]
    [InlineData(@"C:\pics\scan.webp")]
    public void IsImage_RecognizesImageExtensions(string source) =>
        Assert.True(FileRouter.IsImage(source));

    [Fact]
    public void IsImage_UrlIsNotAnImage() =>
        Assert.False(FileRouter.IsImage("https://example.com/photo.png"));

    [Theory]
    [InlineData(@"C:\docs\report.docx")]
    [InlineData(@"C:\docs\report.PDF")]
    [InlineData(@"C:\docs\audio.mp3")]
    [InlineData(@"C:\pics\scan.tiff")]
    [InlineData("https://youtube.com/watch?v=abc")]
    public void IsSupported_KnownSources(string source) =>
        Assert.True(FileRouter.IsSupported(source));

    [Theory]
    [InlineData(@"C:\docs\program.exe")]
    [InlineData(@"C:\docs\archive.7z")]
    public void IsSupported_UnknownExtensions(string source) =>
        Assert.False(FileRouter.IsSupported(source));

    [Fact]
    public void Decide_UrlGoesToMarkitdown() =>
        Assert.Equal(ConversionRoute.Markitdown, FileRouter.Decide("https://example.com", new PdfRasterizer()));

    [Fact]
    public void Decide_ImageGoesToImageOcr() =>
        Assert.Equal(ConversionRoute.ImageOcr, FileRouter.Decide(@"C:\pics\scan.png", new PdfRasterizer()));

    [Fact]
    public void Decide_DocumentGoesToMarkitdown() =>
        Assert.Equal(ConversionRoute.Markitdown, FileRouter.Decide(@"C:\docs\report.docx", new PdfRasterizer()));
}
