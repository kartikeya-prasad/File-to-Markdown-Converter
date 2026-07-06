using TesseractOCR.Enums;

namespace FileToMarkdown.Core;

/// <summary>How a given source is converted to Markdown.</summary>
public enum ConversionRoute
{
    /// <summary>markitdown engine (documents, audio, YouTube, born-digital PDFs).</summary>
    Markitdown,
    /// <summary>Tesseract OCR of an image file.</summary>
    ImageOcr,
    /// <summary>Legacy whole-file OCR route; superseded by <see cref="PdfHybrid"/>.</summary>
    PdfOcr,
    /// <summary>
    /// Per-page PDF pipeline: digital pages keep their extracted text, scanned pages
    /// (including scans with typed page numbers) are OCR'd, assembled in page order.
    /// </summary>
    PdfHybrid,
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

/// <summary>Which OCR backend recognizes scanned pages and images.</summary>
public enum OcrEngineKind
{
    /// <summary>In-process Tesseract - lightweight, always available.</summary>
    Tesseract,
    /// <summary>Surya (Python transformer models) - better table/layout fidelity; installed on demand.</summary>
    Surya,
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

    /// <summary>OCR backend. Surya silently falls back to Tesseract when not installed.</summary>
    public OcrEngineKind OcrEngine { get; set; } = OcrEngineKind.Tesseract;

    /// <summary>OCR language for Tesseract (requires the matching tessdata file).</summary>
    public Language OcrLanguage { get; set; } = Language.English;

    /// <summary>Render DPI used when OCR-ing scanned PDF pages.</summary>
    public int OcrDpi { get; set; } = 200;

    /// <summary>
    /// A PDF page with at least this many non-whitespace characters of embedded text is
    /// treated as digital (no OCR) regardless of layout.
    /// </summary>
    public int PdfDigitalMinChars { get; set; } = 100;

    /// <summary>
    /// Minimum non-whitespace characters for the coverage-based digital test. Below this
    /// (e.g. a typed page number on a scan) the page is always OCR'd, with the sparse
    /// text merged back in afterwards.
    /// </summary>
    public int PdfSparseMinChars { get; set; } = 25;

    /// <summary>
    /// Minimum fraction (0..1) of the page area covered by text rectangles for a
    /// moderately-sparse page to count as digital. Distinguishes a real title page from
    /// a page-number stamp on a scan.
    /// </summary>
    public double PdfDigitalMinCoverage { get; set; } = 0.02;

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
