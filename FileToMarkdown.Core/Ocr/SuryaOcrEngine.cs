using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FileToMarkdown.Core.Ocr;

/// <summary>
/// Surya OCR engine: drives a persistent Python worker (tools/surya_worker.py) over the
/// same line-delimited JSON protocol as the markitdown worker. Surya's transformer
/// models read layouts (tables, columns) far better than Tesseract, at the cost of a
/// large on-demand install (PyTorch) and model downloads on first use.
/// </summary>
public sealed class SuryaOcrEngine : IOcrEngine
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _timeout;
    private Process? _proc;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private int _nextId;

    public SuryaOcrEngine(TimeSpan? timeout = null) =>
        _timeout = timeout ?? TimeSpan.FromMinutes(10);

    /// <summary>Cheap availability probe: the surya package is present in the bundled Python.</summary>
    public static bool IsAvailable() =>
        RuntimeLocator.PythonExe is not null
        && RuntimeLocator.SuryaWorkerScript is not null
        && PythonPackageInstaller.IsPackagePresent("surya");

    public async Task<string> OcrImageFileAsync(string path, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureWorkerAsync(ct).ConfigureAwait(false);
            return await RequestAsync(path, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> OcrBgraAsync(byte[] bgra, int width, int height, int stride, CancellationToken ct = default)
    {
        // The worker consumes image files; hand rasterized pages over as a temp PNG.
        var png = OcrService.EncodeBgraToPng(bgra, width, height, stride);
        var tmp = Path.Combine(Path.GetTempPath(), $"f2md-surya-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(tmp, png, ct).ConfigureAwait(false);
        try
        {
            return await OcrImageFileAsync(tmp, ct).ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* temp cleanup is best-effort */ }
        }
    }

    private async Task EnsureWorkerAsync(CancellationToken ct)
    {
        if (_proc is { HasExited: false }) return;
        KillWorker();

        var python = RuntimeLocator.PythonExe
            ?? throw new InvalidOperationException("Bundled Python runtime not found.");
        var script = RuntimeLocator.SuryaWorkerScript
            ?? throw new InvalidOperationException("surya_worker.py not found.");

        var psi = new ProcessStartInfo
        {
            FileName = python,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("-X");
        psi.ArgumentList.Add("utf8");
        psi.ArgumentList.Add(script);
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUNBUFFERED"] = "1";
        PythonPackageInstaller.ApplyPythonPath(psi);

        var proc = new Process { StartInfo = psi };
        if (!proc.Start())
            throw new InvalidOperationException("Failed to start the Surya OCR worker.");

        _proc = proc;
        _stdin = proc.StandardInput;
        _stdout = proc.StandardOutput;
        _ = Task.Run(async () =>
        {
            try { _ = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false); }
            catch { /* drain only */ }
        });

        // Model loading can take minutes on first run (weights are downloaded).
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(15));
        while (true)
        {
            var line = await _stdout.ReadLineAsync(cts.Token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Surya worker exited during startup.");
            if (line.Length == 0) continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.TryGetProperty("status", out _)) continue; // progress line, keep waiting
            if (root.TryGetProperty("ready", out var ready) && ready.GetBoolean()) return;

            var err = root.TryGetProperty("error", out var e) ? e.GetString() : "unknown error";
            KillWorker();
            throw new InvalidOperationException($"Surya worker failed to start: {err}");
        }
    }

    private async Task<string> RequestAsync(string imagePath, CancellationToken ct)
    {
        int id = ++_nextId;
        var request = JsonSerializer.Serialize(new { id, path = imagePath });
        try
        {
            await _stdin!.WriteLineAsync(request.AsMemory(), ct).ConfigureAwait(false);
            await _stdin.FlushAsync(ct).ConfigureAwait(false);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeout);
            while (true)
            {
                var line = await _stdout!.ReadLineAsync(cts.Token).ConfigureAwait(false)
                    ?? throw new IOException("Surya worker closed unexpectedly.");
                if (line.Length == 0) continue;

                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("status", out _)) continue;
                if (!root.TryGetProperty("id", out var idEl)
                    || idEl.ValueKind != JsonValueKind.Number
                    || idEl.GetInt32() != id) continue;

                if (root.TryGetProperty("ok", out var ok) && ok.GetBoolean())
                    return root.GetProperty("text").GetString() ?? string.Empty;

                var err = root.TryGetProperty("error", out var e) ? e.GetString() : "unknown error";
                throw new InvalidOperationException($"Surya OCR failed: {err}");
            }
        }
        catch (OperationCanceledException)
        {
            KillWorker(); // the in-flight request would poison the stream
            throw;
        }
        catch (IOException)
        {
            KillWorker();
            throw;
        }
    }

    private void KillWorker()
    {
        if (_proc is null) return;
        try { if (!_proc.HasExited) _proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
        try { _stdin?.Dispose(); } catch { /* ignore */ }
        try { _proc.Dispose(); } catch { /* ignore */ }
        _proc = null; _stdin = null; _stdout = null;
    }

    public void Dispose()
    {
        try
        {
            if (_proc is { HasExited: false } && _stdin is not null)
            {
                _stdin.WriteLine("{\"cmd\":\"shutdown\"}");
                _stdin.Flush();
                _proc.WaitForExit(2000);
            }
        }
        catch { /* ignore */ }
        KillWorker();
        _gate.Dispose();
    }
}
