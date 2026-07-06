using System.Text;
using FileToMarkdown.Core.Native;
using FileToMarkdown.Core.Ocr;

namespace FileToMarkdown.Core;

/// <summary>
/// Converts a PDF page by page: pages with a substantive digital text layer keep their
/// extracted text (pdfminer via the markitdown worker, PDFium as fallback), pages that
/// are scans — even scans carrying a typed page number — are rasterized and OCR'd, with
/// the sparse digital text merged back in so nothing is lost or doubled.
/// Documents where every page is digital are handed to markitdown whole, preserving the
/// existing best-quality path.
/// </summary>
public sealed class PdfHybridConverter
{
    private readonly ConversionOptions _options;
    private readonly MarkitdownRunner _markitdown;
    private readonly Func<IOcrEngine> _ocr;

    public PdfHybridConverter(ConversionOptions options, MarkitdownRunner markitdown, Func<IOcrEngine> ocr)
    {
        _options = options;
        _markitdown = markitdown;
        _ocr = ocr;
    }

    public async Task<string> ConvertAsync(string path, CancellationToken ct = default)
    {
        PdfiumEngine engine;
        IReadOnlyList<PageStats> stats;
        try
        {
            engine = new PdfiumEngine();
            engine.LoadDocument(path);
            stats = await Task.Run(() => PdfPageAnalyzer.Analyze(engine, ct), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Unreadable via PDFium (encrypted, malformed, ...) — let markitdown try.
            return await _markitdown.ConvertAsync(path, ct).ConfigureAwait(false);
        }

        using (engine)
        {
            bool[] digital = stats.Select(s => PdfPageClassifier.IsDigital(s, _options)).ToArray();

            if (stats.Count == 0 || digital.All(d => d))
                return await _markitdown.ConvertAsync(path, ct).ConfigureAwait(false);

            // Digital pages: prefer pdfminer's extraction (markitdown-equivalent quality);
            // fall back to the PDFium text we already have.
            var digitalPages = Enumerable.Range(0, stats.Count).Where(i => digital[i]).ToArray();
            IReadOnlyDictionary<int, string> digitalText = new Dictionary<int, string>();
            if (digitalPages.Length > 0)
            {
                try
                {
                    digitalText = await _markitdown.ExtractPdfPagesAsync(path, digitalPages, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Worker unavailable — PDFium fallback below covers these pages.
                }
            }

            var ocr = _ocr();
            var sb = new StringBuilder();
            for (int i = 0; i < stats.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                string text;
                if (digital[i])
                {
                    text = digitalText.TryGetValue(i, out var t) && !string.IsNullOrWhiteSpace(t)
                        ? t
                        : stats[i].Text;
                }
                else
                {
                    var (bgra, w, h, stride) = PdfRasterizer.RenderPageAtDpi(engine, i, _options.OcrDpi);
                    string ocrText = await ocr.OcrBgraAsync(bgra, w, h, stride, ct).ConfigureAwait(false);
                    text = SparseTextMerger.Merge(ocrText, stats[i].Text);
                }

                if (stats.Count > 1)
                    sb.Append("<!-- page ").Append(i + 1).AppendLine(" -->");
                text = text.Trim();
                if (text.Length > 0)
                {
                    sb.AppendLine(text);
                    sb.AppendLine();
                }
            }
            return sb.ToString().TrimEnd() + Environment.NewLine;
        }
    }
}
