using FileToMarkdown.Core.Native;
using Xunit;

namespace FileToMarkdown.Core.Tests;

/// <summary>
/// End-to-end coverage of the per-page analysis over real PDFium: the scenario that
/// motivated the hybrid pipeline is a scanned book whose pages carry only machine-typed
/// page numbers — those pages must classify as non-digital while true text pages stay
/// digital.
/// </summary>
public class PdfPageAnalyzerTests : IDisposable
{
    private const string LongParagraph =
        "This is a genuinely digital page with a full paragraph of embedded text. " +
        "It contains far more than one hundred non-whitespace characters, which is " +
        "what the classifier requires to call a page digital without a layout check.";

    private readonly string _dir = Directory.CreateTempSubdirectory("f2md-pdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private IReadOnlyList<PageStats> Analyze(params string?[] pageTexts)
    {
        var path = TestPdf.CreateFile(_dir, "test.pdf", pageTexts);
        using var engine = new PdfiumEngine();
        engine.LoadDocument(path);
        return PdfPageAnalyzer.Analyze(engine);
    }

    [Fact]
    public void DigitalPage_ExtractsTextAndCoverage()
    {
        var stats = Analyze(LongParagraph);

        Assert.Single(stats);
        Assert.Contains("genuinely digital page", stats[0].Text);
        Assert.True(stats[0].NonWsChars > 100);
        Assert.True(stats[0].TextAreaRatio > 0);
    }

    [Fact]
    public void BlankPage_HasNoText()
    {
        var stats = Analyze((string?)null);

        Assert.Single(stats);
        Assert.Equal(0, stats[0].NonWsChars);
        Assert.Equal(0, stats[0].TextAreaRatio);
    }

    [Fact]
    public void MixedDocument_ClassifiesPerPage()
    {
        // Page 1: real text. Page 2: scan-shaped (only a typed page number).
        // Page 3: completely blank scan.
        var stats = Analyze(LongParagraph, "17", null);
        var options = new ConversionOptions();

        Assert.Equal(3, stats.Count);
        Assert.True(PdfPageClassifier.IsDigital(stats[0], options));
        Assert.False(PdfPageClassifier.IsDigital(stats[1], options));
        Assert.False(PdfPageClassifier.IsDigital(stats[2], options));
        Assert.Contains("17", stats[1].Text);
    }

    [Fact]
    public void OldHeuristicWouldHaveMisrouted_NewClassifierDoesNot()
    {
        // Ten scanned pages, each stamped with a typed page number. The old
        // whole-file probe counted >= 10 chars/page on average and routed the whole
        // file to text extraction, silently dropping the scanned content.
        var pages = Enumerable.Range(1, 10).Select(n => $"Page {n:D3}").Cast<string?>().ToArray();
        var stats = Analyze(pages);
        var options = new ConversionOptions();

        Assert.All(stats, s => Assert.False(PdfPageClassifier.IsDigital(s, options)));
    }

    [Fact]
    public void RenderPageAtDpi_ProducesCorrectlySizedBitmap()
    {
        var path = TestPdf.CreateFile(_dir, "render.pdf", "some text");
        using var engine = new PdfiumEngine();
        engine.LoadDocument(path);

        var (bgra, w, h, stride) = PdfRasterizer.RenderPageAtDpi(engine, 0, 72);

        Assert.Equal(612, w);   // 612pt at 72 DPI = 612px
        Assert.Equal(792, h);
        Assert.Equal(w * 4, stride);
        Assert.Equal(stride * h, bgra.Length);
    }
}
