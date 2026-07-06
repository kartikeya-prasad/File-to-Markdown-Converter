namespace FileToMarkdown.Core;

/// <summary>Decides how each source should be converted.</summary>
public static class FileRouter
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".gif", ".webp",
    };

    /// <summary>File extensions (lowercase, with dot) handled by the markitdown engine.</summary>
    public static readonly IReadOnlySet<string> MarkitdownExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".pptx", ".ppt", ".xlsx", ".xls", ".html", ".htm",
        ".csv", ".json", ".xml", ".epub", ".msg", ".zip",
        ".mp3", ".wav", ".m4a", ".flac", ".ogg", ".txt", ".rtf",
    };

    public static bool IsUrl(string source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static bool IsImage(string source) =>
        !IsUrl(source) && ImageExtensions.Contains(Path.GetExtension(source));

    /// <summary>
    /// Whether the app recognizes this source at all (used to filter dropped folders).
    /// </summary>
    public static bool IsSupported(string source)
    {
        if (IsUrl(source)) return true;
        var ext = Path.GetExtension(source);
        return ImageExtensions.Contains(ext) || MarkitdownExtensions.Contains(ext);
    }

    /// <summary>
    /// Decides the route. Every PDF takes the hybrid route, which analyzes pages
    /// individually (fully-digital documents are still handed to markitdown whole).
    /// </summary>
    public static ConversionRoute Decide(string source)
    {
        if (IsUrl(source)) return ConversionRoute.Markitdown;

        var ext = Path.GetExtension(source);
        if (ImageExtensions.Contains(ext)) return ConversionRoute.ImageOcr;
        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return ConversionRoute.PdfHybrid;

        return ConversionRoute.Markitdown;
    }
}
