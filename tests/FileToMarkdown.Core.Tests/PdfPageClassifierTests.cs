using Xunit;

namespace FileToMarkdown.Core.Tests;

public class PdfPageClassifierTests
{
    private static readonly ConversionOptions Defaults = new();

    [Fact]
    public void ScannedPageWithTypedPageNumber_IsNotDigital()
    {
        // The bug this pipeline fixes: a scan carrying only a machine-typed page
        // number ("17") must be OCR'd, not treated as a digital page.
        Assert.False(PdfPageClassifier.IsDigital(nonWsChars: 2, textAreaRatio: 0.0004, Defaults));
    }

    [Fact]
    public void ScannedPageWithTypedHeaderLine_IsNotDigital()
    {
        // ~30 chars of stamped header text covering almost no page area.
        Assert.False(PdfPageClassifier.IsDigital(nonWsChars: 30, textAreaRatio: 0.005, Defaults));
    }

    [Fact]
    public void PageWithLotsOfText_IsDigital_RegardlessOfCoverage() =>
        Assert.True(PdfPageClassifier.IsDigital(nonWsChars: 1500, textAreaRatio: 0.0, Defaults));

    [Fact]
    public void SparseTitlePageWithRealLayout_IsDigital()
    {
        // A title page: modest character count but large text (high area coverage).
        Assert.True(PdfPageClassifier.IsDigital(nonWsChars: 40, textAreaRatio: 0.05, Defaults));
    }

    [Fact]
    public void EmptyPage_IsNotDigital() =>
        Assert.False(PdfPageClassifier.IsDigital(nonWsChars: 0, textAreaRatio: 0.0, Defaults));

    [Fact]
    public void ThresholdsAreConfigurable()
    {
        var strict = new ConversionOptions { PdfDigitalMinChars = 10 };
        Assert.True(PdfPageClassifier.IsDigital(nonWsChars: 12, textAreaRatio: 0.0, strict));
        Assert.False(PdfPageClassifier.IsDigital(nonWsChars: 12, textAreaRatio: 0.0, Defaults));
    }

    [Fact]
    public void BoundaryValues_MatchInclusiveSemantics()
    {
        var o = Defaults;
        Assert.True(PdfPageClassifier.IsDigital(o.PdfDigitalMinChars, 0.0, o));
        Assert.False(PdfPageClassifier.IsDigital(o.PdfDigitalMinChars - 1, 0.0, o));
        Assert.True(PdfPageClassifier.IsDigital(o.PdfSparseMinChars, o.PdfDigitalMinCoverage, o));
        Assert.False(PdfPageClassifier.IsDigital(o.PdfSparseMinChars - 1, o.PdfDigitalMinCoverage, o));
    }
}
