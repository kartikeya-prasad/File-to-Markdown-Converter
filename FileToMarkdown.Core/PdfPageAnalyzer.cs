using FileToMarkdown.Core.Native;

namespace FileToMarkdown.Core;

/// <summary>Per-page statistics of a PDF's embedded text layer.</summary>
/// <param name="PageIndex">Zero-based page index.</param>
/// <param name="Text">The page's embedded (digital) text, may be empty.</param>
/// <param name="NonWsChars">Count of non-whitespace characters in <paramref name="Text"/>.</param>
/// <param name="TextAreaRatio">Fraction (0..1) of the page area covered by text rectangles.</param>
public sealed record PageStats(int PageIndex, string Text, int NonWsChars, double TextAreaRatio);

/// <summary>
/// Produces per-page <see cref="PageStats"/> for a loaded PDF in a single pass.
/// This replaces the old whole-file "has a text layer" probe, which misrouted
/// scanned documents carrying machine-typed page numbers on every page.
/// </summary>
public static class PdfPageAnalyzer
{
    public static IReadOnlyList<PageStats> Analyze(PdfiumEngine engine, CancellationToken ct = default)
    {
        var stats = new List<PageStats>(engine.PageCount);
        for (int i = 0; i < engine.PageCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (text, ratio) = engine.GetTextWithCoverage(i);
            stats.Add(new PageStats(i, text, CountNonWhitespace(text), ratio));
        }
        return stats;
    }

    private static int CountNonWhitespace(string s)
    {
        int n = 0;
        foreach (var c in s) if (!char.IsWhiteSpace(c)) n++;
        return n;
    }
}
