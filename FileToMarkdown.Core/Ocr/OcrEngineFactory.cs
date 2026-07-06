namespace FileToMarkdown.Core.Ocr;

public static class OcrEngineFactory
{
    /// <summary>
    /// Creates the configured OCR engine. Surya is used only when its Python package is
    /// actually installed; otherwise Tesseract keeps conversions working (the UI warns
    /// at selection time, not mid-batch).
    /// </summary>
    public static IOcrEngine Create(ConversionOptions options) =>
        options.OcrEngine == OcrEngineKind.Surya && SuryaOcrEngine.IsAvailable()
            ? new SuryaOcrEngine(options.Timeout)
            : new TesseractOcrEngine(options.OcrLanguage);
}
