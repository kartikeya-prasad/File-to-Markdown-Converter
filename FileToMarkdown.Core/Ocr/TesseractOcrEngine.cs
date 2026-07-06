using TesseractOCR.Enums;

namespace FileToMarkdown.Core.Ocr;

/// <summary>The default engine: in-process Tesseract via <see cref="OcrService"/>.</summary>
public sealed class TesseractOcrEngine : IOcrEngine
{
    private readonly OcrService _ocr;

    public TesseractOcrEngine(Language language = Language.English) =>
        _ocr = new OcrService(tessdataDir: null, language);

    public Task<string> OcrImageFileAsync(string path, CancellationToken ct = default) =>
        Task.Run(() => _ocr.OcrImageFile(path), ct);

    public Task<string> OcrBgraAsync(byte[] bgra, int width, int height, int stride, CancellationToken ct = default) =>
        Task.Run(() => _ocr.OcrBgra(bgra, width, height, stride), ct);

    public void Dispose() => _ocr.Dispose();
}
