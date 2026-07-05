using System.Text;

namespace FileToMarkdown.Core;

/// <summary>
/// Converts a single source (file path or URL) to a Markdown file, choosing between the
/// markitdown engine and the Tesseract OCR paths. OCR resources are created lazily so the
/// engine is only spun up when an image or scanned PDF is actually encountered.
/// </summary>
public sealed class ConversionService : IAsyncDisposable
{
    private readonly ConversionOptions _options;
    private readonly MarkitdownRunner _markitdown;
    private readonly PdfHybridConverter _pdfHybrid;
    private readonly Lazy<OcrService> _ocr;

    public ConversionService(ConversionOptions? options = null)
    {
        _options = options ?? new ConversionOptions();
        _markitdown = new MarkitdownRunner { Timeout = _options.Timeout };
        _ocr = new Lazy<OcrService>(
            () => new OcrService(tessdataDir: null, language: _options.OcrLanguage),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _pdfHybrid = new PdfHybridConverter(_options, _markitdown, () => _ocr.Value);
    }

    public async Task<ConversionResult> ConvertAsync(string source, string outputDir, CancellationToken ct = default)
    {
        var route = FileRouter.Decide(source);
        try
        {
            Directory.CreateDirectory(outputDir);

            // Resolve output up front so a Skip policy avoids doing the work at all.
            var outputPath = ResolveOutputPath(GetBaseName(source), outputDir, _options.Overwrite, out bool skip);
            if (skip)
                return new ConversionResult
                {
                    Source = source, Status = ConversionStatus.Skipped, Route = route, OutputPath = outputPath,
                };

            string markdown = route switch
            {
                ConversionRoute.ImageOcr  => await Task.Run(() => _ocr.Value.OcrImageFile(source), ct).ConfigureAwait(false),
                ConversionRoute.PdfHybrid => await _pdfHybrid.ConvertAsync(source, ct).ConfigureAwait(false),
                _                         => await _markitdown.ConvertAsync(source, ct).ConfigureAwait(false),
            };

            await File.WriteAllTextAsync(outputPath, markdown, new UTF8Encoding(false), ct).ConfigureAwait(false);
            return new ConversionResult
            {
                Source = source, Status = ConversionStatus.Succeeded, Route = route, OutputPath = outputPath,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ConversionResult
            {
                Source = source, Status = ConversionStatus.Failed, Route = route, Error = ex.Message,
            };
        }
    }

    // ---- Output naming -----------------------------------------------------

    internal static string GetBaseName(string source)
    {
        string name;
        if (FileRouter.IsUrl(source))
        {
            var uri = new Uri(source);
            // Prefer a YouTube video id, else the last path segment, else the host.
            name = TryGetQueryValue(uri.Query, "v")
                ?? uri.Segments.LastOrDefault(s => s.Trim('/').Length > 0)?.Trim('/')
                ?? uri.Host;
        }
        else
        {
            name = Path.GetFileNameWithoutExtension(source);
        }

        name = Sanitize(name);
        return string.IsNullOrWhiteSpace(name) ? "converted" : name;
    }

    private static string? TryGetQueryValue(string query, string key)
    {
        // query includes a leading '?'; split on '&' then 'key=value'.
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            if (pair.AsSpan(0, eq).Equals(key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(pair[(eq + 1)..]);
        }
        return null;
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name) sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        return sb.ToString().Trim();
    }

    internal static string ResolveOutputPath(string baseName, string outputDir, OverwritePolicy policy, out bool skip)
    {
        skip = false;
        var target = Path.Combine(outputDir, baseName + ".md");
        if (!File.Exists(target)) return target;

        switch (policy)
        {
            case OverwritePolicy.Overwrite:
                return target;
            case OverwritePolicy.Skip:
                skip = true;
                return target;
            default: // Number
                for (int i = 1; ; i++)
                {
                    var candidate = Path.Combine(outputDir, $"{baseName} ({i}).md");
                    if (!File.Exists(candidate)) return candidate;
                }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _markitdown.DisposeAsync().ConfigureAwait(false);
        if (_ocr.IsValueCreated) _ocr.Value.Dispose();
    }
}
