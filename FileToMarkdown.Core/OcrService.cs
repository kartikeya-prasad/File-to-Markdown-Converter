using System.Drawing;
using System.Drawing.Imaging;
using TesseractOCR;
using TesseractOCR.Enums;

namespace FileToMarkdown.Core;

/// <summary>
/// Tesseract-based OCR. Replaces markitdown's LLM image-description path: image files
/// (and rasterized scanned-PDF pages) are turned into real text, fully offline.
///
/// A single Tesseract <see cref="Engine"/> is reused across calls (constructing one
/// loads the language model, which is expensive). Tesseract is not safe for concurrent
/// recognition on one engine, so <see cref="Process"/> calls are serialized.
/// </summary>
public sealed class OcrService : IDisposable
{
    private readonly Engine _engine;
    private readonly object _lock = new();
    private bool _disposed;

    public OcrService(string? tessdataDir = null, Language language = Language.English)
    {
        var dir = tessdataDir ?? RuntimeLocator.TessdataDir
            ?? throw new InvalidOperationException(
                "tessdata directory not found. Run tools/setup-python.ps1.");
        _engine = new Engine(dir, language, EngineMode.Default);
    }

    /// <summary>OCRs an image file (PNG/JPG/TIFF/BMP/GIF/WEBP) and returns recognized text.</summary>
    public string OcrImageFile(string path)
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            using var img = TesseractOCR.Pix.Image.LoadFromFile(path);
            using var page = _engine.Process(img);
            return page.Text ?? string.Empty;
        }
    }

    /// <summary>OCRs a 32-bpp BGRA bitmap (e.g. a rasterized PDF page).</summary>
    public string OcrBgra(byte[] bgra, int width, int height, int stride)
    {
        byte[] png = EncodeBgraToPng(bgra, width, height, stride);
        lock (_lock)
        {
            ThrowIfDisposed();
            using var img = TesseractOCR.Pix.Image.LoadFromMemory(png);
            using var page = _engine.Process(img);
            return page.Text ?? string.Empty;
        }
    }

    private static unsafe byte[] EncodeBgraToPng(byte[] bgra, int width, int height, int stride)
    {
        fixed (byte* p = bgra)
        {
            using var bmp = new Bitmap(width, height, stride, PixelFormat.Format32bppArgb, (IntPtr)p);
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _engine.Dispose();
    }
}
