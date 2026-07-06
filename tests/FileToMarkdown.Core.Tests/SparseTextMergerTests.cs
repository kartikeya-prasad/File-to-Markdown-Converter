using Xunit;

namespace FileToMarkdown.Core.Tests;

public class SparseTextMergerTests
{
    [Fact]
    public void EmptyDigitalText_ReturnsOcrUnchanged() =>
        Assert.Equal("scanned words", SparseTextMerger.Merge("scanned words", ""));

    [Fact]
    public void EmptyOcrText_ReturnsDigitalText() =>
        Assert.Equal("17", SparseTextMerger.Merge("", "17"));

    [Fact]
    public void PageNumberMissedByOcr_IsAppended()
    {
        var merged = SparseTextMerger.Merge("The quick brown fox jumps over the lazy dog.", "17");
        Assert.EndsWith("17", merged);
        Assert.StartsWith("The quick brown fox", merged);
    }

    [Fact]
    public void PageNumberAlreadyReadByOcr_IsNotDuplicated()
    {
        var ocr = "The quick brown fox.\n17";
        var merged = SparseTextMerger.Merge(ocr, "17");
        Assert.Equal(ocr, merged);
    }

    [Fact]
    public void PageNumberMisreadByOcr_CountsAsPresent()
    {
        // OCR read "17" as "l7" — close enough; don't append a duplicate.
        var ocr = "Some scanned text\nl7";
        Assert.Equal(ocr, SparseTextMerger.Merge(ocr, "17"));
    }

    [Fact]
    public void PunctuationAndCaseDifferences_DoNotCauseDuplication()
    {
        var ocr = "CHAPTER SEVEN: The Return";
        Assert.Equal(ocr, SparseTextMerger.Merge(ocr, "Chapter Seven - the return"));
    }

    [Fact]
    public void MultiWordFragmentMostlyRecognized_IsNotAppended()
    {
        var ocr = "Annual Report 2024 - Fnancial Summary";  // one word garbled
        Assert.Equal(ocr, SparseTextMerger.Merge(ocr, "Annual Report 2024 Financial Summary"));
    }

    [Fact]
    public void GenuinelyMissingOverlayText_IsAppended()
    {
        var merged = SparseTextMerger.Merge("scanned body content", "CONFIDENTIAL DRAFT COPY 42");
        Assert.Contains("CONFIDENTIAL DRAFT COPY 42", merged);
    }

    [Fact]
    public void MultipleDigitalLines_OnlyMissingOnesAppended()
    {
        var merged = SparseTextMerger.Merge("body text mentions page 17 here", "17\nExhibit A-113");
        Assert.DoesNotContain("\n17", merged);
        Assert.Contains("Exhibit A-113", merged);
    }
}
