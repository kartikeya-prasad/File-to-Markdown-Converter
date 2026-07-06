namespace FileToMarkdown.Core;

/// <summary>
/// Locates the bundled conversion runtime assets (embedded Python, the markitdown
/// worker script, and Tesseract language data).
///
/// In a published/installed app these sit next to the executable. During local
/// development they live at the repository root (produced by tools/setup-python.ps1),
/// so we also walk up the directory tree from the app base directory to find them.
/// </summary>
public static class RuntimeLocator
{
    public static string BaseDirectory => AppContext.BaseDirectory;

    /// <summary>Full path to the bundled <c>python\python.exe</c>, or null if not found.</summary>
    public static string? PythonExe
    {
        get
        {
            var dir = FindUpwards("python", isDirectory: true);
            if (dir is null) return null;
            var exe = Path.Combine(dir, "python.exe");
            return File.Exists(exe) ? exe : null;
        }
    }

    /// <summary>Full path to the markitdown worker script, or null if not found.</summary>
    public static string? WorkerScript
    {
        get
        {
            // Next to the exe (production) takes priority.
            var local = Path.Combine(BaseDirectory, "worker.py");
            if (File.Exists(local)) return local;
            // Dev: tools/worker.py somewhere up the tree.
            return FindUpwards(Path.Combine("tools", "worker.py"), isDirectory: false);
        }
    }

    /// <summary>Full path to the Surya OCR worker script, or null if not found.</summary>
    public static string? SuryaWorkerScript
    {
        get
        {
            var local = Path.Combine(BaseDirectory, "surya_worker.py");
            if (File.Exists(local)) return local;
            return FindUpwards(Path.Combine("tools", "surya_worker.py"), isDirectory: false);
        }
    }

    /// <summary>Full path to the <c>tessdata</c> directory, or null if not found.</summary>
    public static string? TessdataDir => FindUpwards("tessdata", isDirectory: true);

    /// <summary>True when every asset required for full conversion is present.</summary>
    public static bool IsRuntimeReady => PythonExe is not null && WorkerScript is not null;

    private static string? FindUpwards(string relative, bool isDirectory)
    {
        var dir = new DirectoryInfo(BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (isDirectory ? Directory.Exists(candidate) : File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
