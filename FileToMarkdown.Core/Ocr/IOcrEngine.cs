namespace FileToMarkdown.Core.Ocr;

/// <summary>
/// An OCR backend. Implementations: <see cref="TesseractOcrEngine"/> (in-process,
/// always available) and <see cref="SuryaOcrEngine"/> (Python model, better layout,
/// installed on demand).
/// </summary>
public interface IOcrEngine : IDisposable
{
    /// <summary>Recognizes text in an image file (PNG/JPG/TIFF/BMP/GIF/WEBP).</summary>
    Task<string> OcrImageFileAsync(string path, CancellationToken ct = default);

    /// <summary>Recognizes text in a 32-bpp BGRA bitmap (a rasterized PDF page).</summary>
    Task<string> OcrBgraAsync(byte[] bgra, int width, int height, int stride, CancellationToken ct = default);
}
