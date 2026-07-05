using FileToMarkdown.Core;
using Microsoft.UI.Xaml;

namespace FileToMarkdown.App.Services;

/// <summary>User-facing, persisted application settings.</summary>
public sealed class AppSettings
{
    /// <summary>App theme. <see cref="ElementTheme.Default"/> means "follow system".</summary>
    public ElementTheme Theme { get; set; } = ElementTheme.Default;

    /// <summary>Max files converted concurrently.</summary>
    public int Concurrency { get; set; } = Math.Max(1, Environment.ProcessorCount);

    /// <summary>DPI used when OCR-ing scanned PDF pages.</summary>
    public int OcrDpi { get; set; } = 200;

    /// <summary>
    /// OCR language as a <see cref="TesseractOCR.Enums.Language"/> name (e.g. "English").
    /// Requires the matching tessdata file to be installed.
    /// </summary>
    public string OcrLanguage { get; set; } = nameof(TesseractOCR.Enums.Language.English);

    /// <summary>What to do when an output .md already exists.</summary>
    public OverwritePolicy Overwrite { get; set; } = OverwritePolicy.Number;

    /// <summary>Last-used output folder (empty = default to Documents\Markdown Output).</summary>
    public string OutputFolder { get; set; } = string.Empty;

    /// <summary>Last time the startup update check ran (UTC); throttled to once per day.</summary>
    public DateTime? LastUpdateCheckUtc { get; set; }

    /// <summary>A version the user chose to skip; the startup prompt stays quiet for it.</summary>
    public string? SkippedVersion { get; set; }

    /// <summary>Projects these settings onto the engine's options.</summary>
    public ConversionOptions ToConversionOptions() => new()
    {
        Concurrency = Math.Max(1, Concurrency),
        OcrDpi = Math.Clamp(OcrDpi, 72, 600),
        OcrLanguage = Enum.TryParse<TesseractOCR.Enums.Language>(OcrLanguage, ignoreCase: true, out var lang)
            ? lang
            : TesseractOCR.Enums.Language.English,
        Overwrite = Overwrite,
    };
}
