using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FileToMarkdown.Core;

/// <summary>
/// Drives the bundled markitdown engine.
///
/// Primary path: a persistent Python worker process (tools/worker.py) that imports
/// markitdown once and answers conversion requests over a stdin/stdout JSON protocol.
/// This avoids the ~1-2s interpreter + import cost on every file in a batch.
///
/// Fallback path: if the worker can't start or dies mid-batch, a one-shot
/// "python -m markitdown &lt;source&gt;" invocation is used per file.
/// </summary>
public sealed class MarkitdownRunner : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _proc;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private int _nextId;
    private bool _workerUsable;

    /// <summary>markitdown version reported by the worker, if known.</summary>
    public string? Version { get; private set; }

    /// <summary>Default per-file conversion timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Converts a file path or URL to Markdown text. Thread-safe; requests are serialized
    /// onto the single worker.
    /// </summary>
    public async Task<string> ConvertAsync(string source, CancellationToken ct = default)
    {
        if (RuntimeLocator.PythonExe is null)
            throw new InvalidOperationException(
                "Bundled Python runtime not found. Run tools/setup-python.ps1.");

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureWorkerAsync(ct).ConfigureAwait(false);
            if (_workerUsable)
            {
                try
                {
                    return await ConvertViaWorkerAsync(source, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // Worker died or misbehaved — drop it and fall back for this request.
                    KillWorker();
                }
            }
            return await ConvertOneShotAsync(source, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Extracts the embedded text of specific (0-based) PDF pages via pdfminer in the
    /// worker. Returns a map of page index to text; throws when the worker is
    /// unavailable so callers can fall back to PDFium extraction.
    /// </summary>
    public async Task<Dictionary<int, string>> ExtractPdfPagesAsync(
        string path, IReadOnlyList<int> pages, CancellationToken ct = default)
    {
        if (RuntimeLocator.PythonExe is null)
            throw new InvalidOperationException(
                "Bundled Python runtime not found. Run tools/setup-python.ps1.");

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureWorkerAsync(ct).ConfigureAwait(false);
            if (!_workerUsable)
                throw new MarkitdownException("markitdown worker unavailable for pdf_pages.");

            int id = ++_nextId;
            var request = JsonSerializer.Serialize(new { id, cmd = "pdf_pages", source = path, pages });
            await _stdin!.WriteLineAsync(request.AsMemory(), ct).ConfigureAwait(false);
            await _stdin.FlushAsync(ct).ConfigureAwait(false);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);

            while (true)
            {
                var line = await _stdout!.ReadLineAsync(cts.Token).ConfigureAwait(false);
                if (line is null) throw new IOException("markitdown worker closed unexpectedly.");
                if (line.Length == 0) continue;

                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (!root.TryGetProperty("id", out var idEl)) continue;
                if (idEl.ValueKind != JsonValueKind.Number || idEl.GetInt32() != id) continue;

                bool ok = root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean();
                if (!ok)
                {
                    var err = root.TryGetProperty("error", out var e) ? e.GetString() : "unknown error";
                    throw new MarkitdownException(err ?? "unknown error");
                }

                var result = new Dictionary<int, string>();
                foreach (var prop in root.GetProperty("pages").EnumerateObject())
                {
                    if (int.TryParse(prop.Name, out int pageIndex))
                        result[pageIndex] = prop.Value.GetString() ?? string.Empty;
                }
                return result;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (MarkitdownException)
        {
            // Clean error reply from a healthy worker — keep it running.
            throw;
        }
        catch
        {
            // Stream/protocol failure — restart the worker on the next request.
            KillWorker();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    // ---- Worker lifecycle --------------------------------------------------

    private async Task EnsureWorkerAsync(CancellationToken ct)
    {
        if (_proc is { HasExited: false } && _workerUsable) return;
        KillWorker();

        var worker = RuntimeLocator.WorkerScript;
        if (worker is null) { _workerUsable = false; return; }

        var psi = NewPythonStartInfo();
        psi.ArgumentList.Add(worker);

        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!proc.Start()) { _workerUsable = false; return; }

        _proc = proc;
        _stdin = proc.StandardInput;
        _stdout = proc.StandardOutput;
        DrainStdErr(proc);

        // First line must be the readiness handshake.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(60));
        var ready = await _stdout.ReadLineAsync(cts.Token).ConfigureAwait(false);
        if (ready is null) { _workerUsable = false; KillWorker(); return; }

        using var doc = JsonDocument.Parse(ready);
        var root = doc.RootElement;
        if (root.TryGetProperty("ready", out var r) && r.GetBoolean())
        {
            _workerUsable = true;
            if (root.TryGetProperty("version", out var v)) Version = v.GetString();
        }
        else
        {
            _workerUsable = false;
            KillWorker();
        }
    }

    private async Task<string> ConvertViaWorkerAsync(string source, CancellationToken ct)
    {
        int id = ++_nextId;
        var request = JsonSerializer.Serialize(new { id, source });
        await _stdin!.WriteLineAsync(request.AsMemory(), ct).ConfigureAwait(false);
        await _stdin.FlushAsync(ct).ConfigureAwait(false);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);

        // Read until we get the line for our id (protocol is serialized, so it's the next one).
        while (true)
        {
            var line = await _stdout!.ReadLineAsync(cts.Token).ConfigureAwait(false);
            if (line is null) throw new IOException("markitdown worker closed unexpectedly.");
            if (line.Length == 0) continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("id", out var idEl)) continue;
            if (idEl.ValueKind != JsonValueKind.Number || idEl.GetInt32() != id) continue;

            bool ok = root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean();
            if (ok)
                return root.GetProperty("markdown").GetString() ?? string.Empty;

            var err = root.TryGetProperty("error", out var e) ? e.GetString() : "unknown error";
            throw new MarkitdownException(err ?? "unknown error");
        }
    }

    // ---- One-shot fallback -------------------------------------------------

    private async Task<string> ConvertOneShotAsync(string source, CancellationToken ct)
    {
        var psi = NewPythonStartInfo();
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add("markitdown");
        psi.ArgumentList.Add(source);

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        try
        {
            await proc.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw;
        }

        var output = await stdoutTask.ConfigureAwait(false);
        if (proc.ExitCode != 0)
        {
            var err = await stderrTask.ConfigureAwait(false);
            throw new MarkitdownException(
                $"markitdown exited with code {proc.ExitCode}: {err.Trim()}");
        }
        return output;
    }

    // ---- Helpers -----------------------------------------------------------

    private static ProcessStartInfo NewPythonStartInfo()
    {
        var psi = new ProcessStartInfo
        {
            FileName = RuntimeLocator.PythonExe!,
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
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUNBUFFERED"] = "1";
        PythonPackageInstaller.ApplyPythonPath(psi);
        return psi;
    }

    private static void DrainStdErr(Process proc)
    {
        // Consume stderr so the pipe never fills and blocks the worker.
        _ = Task.Run(async () =>
        {
            try { _ = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false); }
            catch { /* ignore */ }
        });
    }

    private void KillWorker()
    {
        _workerUsable = false;
        if (_proc is null) return;
        try { if (!_proc.HasExited) _proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
        try { _stdin?.Dispose(); } catch { /* ignore */ }
        try { _proc.Dispose(); } catch { /* ignore */ }
        _proc = null; _stdin = null; _stdout = null;
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_proc is { HasExited: false } && _stdin is not null)
            {
                try
                {
                    await _stdin.WriteLineAsync("{\"cmd\":\"shutdown\"}").ConfigureAwait(false);
                    await _stdin.FlushAsync().ConfigureAwait(false);
                    _proc.WaitForExit(2000);
                }
                catch { /* ignore */ }
            }
            KillWorker();
        }
        finally { _gate.Release(); }
    }
}

/// <summary>Raised when markitdown fails to convert a source.</summary>
public sealed class MarkitdownException : Exception
{
    public MarkitdownException(string message) : base(message) { }
}
