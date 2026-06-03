using TesseractOCR.Enums;

namespace FileToMarkdown.Core;

/// <summary>How a given source is converted to Markdown.</summary>
public enum ConversionRoute
{
    /// <summary>markitdown engine (documents, audio, YouTube, born-digital PDFs).</summary>
    Markitdown,
    /// <summary>Tesseract OCR of an image file.</summary>
    ImageOcr,
    /// <summary>Tesseract OCR of a scanned/image-only PDF (rendered with PDFium).</summary>
    PdfOcr,
}

/// <summary>Lifecycle status of a conversion job.</summary>
public enum ConversionStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Skipped,
}

/// <summary>What to do when the target .md file already exists.</summary>
public enum OverwritePolicy
{
    /// <summary>Write a numbered variant: name.md, name (1).md, ...</summary>
    Number,
    /// <summary>Overwrite the existing file.</summary>
    Overwrite,
    /// <summary>Skip the conversion.</summary>
    Skip,
}

/// <summary>Tunable conversion settings.</summary>
public sealed class ConversionOptions
{
    /// <summary>Max files processed concurrently. Defaults to processor count.</summary>
    public int Concurrency { get; set; } = Math.Max(1, Environment.ProcessorCount);

    /// <summary>OCR language (requires the matching tessdata file).</summary>
    public Language OcrLanguage { get; set; } = Language.English;

    /// <summary>Render DPI used when OCR-ing scanned PDF pages.</summary>
    public int OcrDpi { get; set; } = 200;

    /// <summary>Behavior when the output .md already exists.</summary>
    public OverwritePolicy Overwrite { get; set; } = OverwritePolicy.Number;

    /// <summary>Per-file conversion timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>Result of converting one source.</summary>
public sealed class ConversionResult
{
    public required string Source { get; init; }
    public required ConversionStatus Status { get; init; }
    public ConversionRoute Route { get; init; }
    public string? OutputPath { get; init; }
    public string? Error { get; init; }
}

/// <summary>Progress update for a single job, suitable for marshaling to the UI.</summary>
public sealed class JobProgress
{
    public required string Source { get; init; }
    public required ConversionStatus Status { get; init; }
    public ConversionRoute? Route { get; init; }
    public string? OutputPath { get; init; }
    public string? Error { get; init; }
}
