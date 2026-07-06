using System.Diagnostics;
using System.Text;

namespace FileToMarkdown.Core;

/// <summary>
/// Installs Python packages into the app's bundled Python at runtime (used for the
/// on-demand Surya OCR and OCRmyPDF components), streaming pip's output so the UI can
/// show real progress for multi-gigabyte downloads.
/// </summary>
public sealed class PythonPackageInstaller
{
    /// <summary>
    /// True when <paramref name="importName"/> is importable in the bundled Python.
    /// Uses find_spec so heavyweight packages (torch) are not actually loaded.
    /// </summary>
    public static bool IsPackagePresent(string importName)
    {
        var python = RuntimeLocator.PythonExe;
        if (python is null) return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = python,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(
                $"import importlib.util, sys; sys.exit(0 if importlib.util.find_spec('{importName}') else 1)");

            using var proc = Process.Start(psi);
            if (proc is null) return false;
            proc.WaitForExit(30_000);
            return proc.HasExited && proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Free bytes on the drive hosting the bundled Python (for pre-install checks).</summary>
    public static long GetFreeDiskBytes()
    {
        var python = RuntimeLocator.PythonExe;
        if (python is null) return 0;
        try
        {
            return new DriveInfo(Path.GetPathRoot(python)!).AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Runs "python -m pip install <paramref name="pipSpec"/>", forwarding each output
    /// line to <paramref name="progress"/>. Throws with pip's tail output on failure.
    /// </summary>
    public async Task InstallAsync(string pipSpec, IProgress<string>? progress, CancellationToken ct = default)
    {
        var python = RuntimeLocator.PythonExe
            ?? throw new InvalidOperationException("Bundled Python runtime not found.");

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
        psi.ArgumentList.Add("pip");
        psi.ArgumentList.Add("install");
        psi.ArgumentList.Add("--no-warn-script-location");
        foreach (var part in pipSpec.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            psi.ArgumentList.Add(part);

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tail = new Queue<string>();

        void OnLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            progress?.Report(line);
            lock (tail)
            {
                tail.Enqueue(line);
                while (tail.Count > 20) tail.Dequeue();
            }
        }

        proc.OutputDataReceived += (_, e) => OnLine(e.Data);
        proc.ErrorDataReceived += (_, e) => OnLine(e.Data);

        if (!proc.Start())
            throw new InvalidOperationException("Failed to start pip.");
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        try
        {
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw;
        }

        if (proc.ExitCode != 0)
        {
            string detail;
            lock (tail) detail = string.Join('\n', tail);
            throw new InvalidOperationException(
                $"pip install '{pipSpec}' failed (exit {proc.ExitCode}):\n{detail}");
        }
    }
}
