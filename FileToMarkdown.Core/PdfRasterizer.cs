using System.Text;
using FileToMarkdown.Core.Native;

namespace FileToMarkdown.Core;

/// <summary>
/// PDFium-backed helper for the scanned-PDF path: decides whether a PDF already has a
/// usable text layer, and (if not) renders each page to a bitmap for OCR.
/// </summary>
public sealed class PdfRasterizer
{
    /// <summary>Render resolution for OCR, in DPI.</summary>
    public int Dpi { get; set; } = 200;

    /// <summary>A PDF is treated as "born-digital" if it has at least this many text characters per page.</summary>
    public int MinCharsPerPage { get; set; } = 10;

    /// <summary>True if the PDF appears to have a real, extractable text layer.</summary>
    public bool HasTextLayer(string path)
    {
        using var engine = new PdfiumEngine();
        engine.LoadDocument(path);
        if (engine.PageCount == 0) return false;

        long total = 0;
        long threshold = (long)engine.PageCount * MinCharsPerPage;
        for (int i = 0; i < engine.PageCount; i++)
        {
            total += CountNonWhitespace(engine.ExtractText(i));
            if (total >= threshold) return true;
        }
        return false;
    }

    /// <summary>Renders every page and OCRs it, returning assembled Markdown.</summary>
    public string OcrPdf(string path, OcrService ocr)
    {
        using var engine = new PdfiumEngine();
        engine.LoadDocument(path);

        var sb = new StringBuilder();
        for (int i = 0; i < engine.PageCount; i++)
        {
            var (wPt, hPt) = engine.GetPageSize(i);
            int w = Math.Max(1, (int)Math.Round(wPt / 72.0 * Dpi));
            int h = Math.Max(1, (int)Math.Round(hPt / 72.0 * Dpi));

            var (bgra, bw, bh, stride) = engine.RenderPageBgra(i, w, h);
            string text = ocr.OcrBgra(bgra, bw, bh, stride).Trim();

            if (engine.PageCount > 1)
                sb.Append("<!-- page ").Append(i + 1).AppendLine(" -->");
            if (text.Length > 0)
            {
                sb.AppendLine(text);
                sb.AppendLine();
            }
        }
        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private static int CountNonWhitespace(string s)
    {
        int n = 0;
        foreach (var c in s) if (!char.IsWhiteSpace(c)) n++;
        return n;
    }
}
