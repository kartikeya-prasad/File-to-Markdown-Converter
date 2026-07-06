namespace FileToMarkdown.Core;

/// <summary>
/// Decides, per page, whether the embedded text layer is the real content (digital page)
/// or just an incidental stamp (page number / header on a scan) that must not suppress OCR.
/// </summary>
public static class PdfPageClassifier
{
    /// <summary>
    /// A page is digital when it has plenty of text, or a moderate amount of text that
    /// also occupies a meaningful share of the page area. A scanned page with a typed
    /// page number has very few characters covering a tiny area, so it fails both tests
    /// and is routed to OCR.
    /// </summary>
    public static bool IsDigital(int nonWsChars, double textAreaRatio, ConversionOptions options)
    {
        if (nonWsChars >= options.PdfDigitalMinChars) return true;
        return nonWsChars >= options.PdfSparseMinChars
            && textAreaRatio >= options.PdfDigitalMinCoverage;
    }

    public static bool IsDigital(PageStats stats, ConversionOptions options) =>
        IsDigital(stats.NonWsChars, stats.TextAreaRatio, options);
}
