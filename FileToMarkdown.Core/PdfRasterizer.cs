using FileToMarkdown.Core.Native;

namespace FileToMarkdown.Core;

/// <summary>
/// PDFium rendering helper for the OCR path. Page-level routing decisions live in
/// <see cref="PdfPageAnalyzer"/> / <see cref="PdfPageClassifier"/>; assembly lives in
/// <see cref="PdfHybridConverter"/>.
/// </summary>
public static class PdfRasterizer
{
    /// <summary>Renders one page to a BGRA bitmap sized for the requested DPI.</summary>
    public static (byte[] Bgra, int Width, int Height, int Stride) RenderPageAtDpi(
        PdfiumEngine engine, int pageIndex, int dpi)
    {
        var (wPt, hPt) = engine.GetPageSize(pageIndex);
        int w = Math.Max(1, (int)Math.Round(wPt / 72.0 * dpi));
        int h = Math.Max(1, (int)Math.Round(hPt / 72.0 * dpi));
        return engine.RenderPageBgra(pageIndex, w, h);
    }
}
