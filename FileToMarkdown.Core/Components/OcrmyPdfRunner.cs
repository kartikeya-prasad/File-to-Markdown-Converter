using System.Diagnostics;
using System.Text;

namespace FileToMarkdown.Core.Components;

/// <summary>
/// Runs OCRmyPDF (in the bundled Python) with --redo-ocr, producing a searchable PDF
/// with a correct text layer even for documents that mix scanned images with real
/// digital text - OCRmyPDF analyzes content per element, so typed page numbers on
/// scans neither suppress OCR nor get duplicated.
/// </summary>
public static class OcrmyPdfRunner
{
    public static bool IsAvailable() => ComponentManager.IsOcrmyPdfReady();

    /// <summary>OCRs <paramref name="inputPdf"/> into <paramref name="outputPdf"/>.</summary>
    public static async Task RunAsync(
        string inputPdf, string outputPdf, TimeSpan timeout, CancellationToken ct = default)
    {
        var python = RuntimeLocator.PythonExe
            ?? throw new InvalidOperationException("Bundled Python runtime not found.");
        var tesseract = ComponentManager.TesseractExe
            ?? throw new InvalidOperationException("tesseract.exe not found - install the Enhanced PDF OCR component.");
        var ghostscript = ComponentManager.GhostscriptExe
            ?? throw new InvalidOperationException("Ghostscript not found - install the Enhanced PDF OCR component.");

        var psi = new ProcessStartInfo
        {
            FileName = python,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add("ocrmypdf");
        psi.ArgumentList.Add("--redo-ocr");
        psi.ArgumentList.Add("--output-type");
        psi.ArgumentList.Add("pdf");
        psi.ArgumentList.Add(inputPdf);
        psi.ArgumentList.Add(outputPdf);

        // OCRmyPDF locates tesseract and ghostscript via PATH.
        var extraPath = Path.GetDirectoryName(tesseract) + Path.PathSeparator
                      + Path.GetDirectoryName(ghostscript);
        psi.Environment["PATH"] = extraPath + Path.PathSeparator
                                + (Environment.GetEnvironmentVariable("PATH") ?? string.Empty);

        using var proc = new Process { StartInfo = psi };
        if (!proc.Start())
            throw new InvalidOperationException("Failed to start ocrmypdf.");

        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        _ = proc.StandardOutput.ReadToEndAsync(ct); // drain

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await proc.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw;
        }

        if (proc.ExitCode != 0)
        {
            var stderr = await stderrTask.ConfigureAwait(false);
            var tail = string.Join('\n', stderr.Split('\n').TakeLast(15));
            throw new InvalidOperationException(
                $"ocrmypdf exited with code {proc.ExitCode}:\n{tail}");
        }
    }
}
