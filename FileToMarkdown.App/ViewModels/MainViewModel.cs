using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileToMarkdown.App.Models;
using FileToMarkdown.Core;
using Windows.Storage.Pickers;

namespace FileToMarkdown.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

    public ObservableCollection<JobItem> Jobs { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasJobs))]
    private bool _hasJobsTrigger; // bumped when collection changes

    [ObservableProperty] private string _outputFolder;
    [ObservableProperty] private string _urlInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isConverting;

    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _statusSummary = "Ready.";

    public bool HasJobs => Jobs.Count > 0;
    public bool IsIdle => !IsConverting;

    public MainViewModel()
    {
        OutputFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Markdown Output");
        Jobs.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasJobs));
            ConvertCommand.NotifyCanExecuteChanged();
        };
    }

    // Resolved on demand (App.MainWindow is set by the time a picker is invoked).
    private IntPtr Hwnd => WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);

    // ---- Adding sources ----------------------------------------------------

    [RelayCommand]
    private async Task AddFilesAsync()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, Hwnd);

        var files = await picker.PickMultipleFilesAsync();
        if (files is { Count: > 0 })
            AddPaths(files.Select(f => f.Path));
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, Hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        var files = Directory.EnumerateFiles(folder.Path, "*", SearchOption.AllDirectories);
        AddPaths(files);
    }

    [RelayCommand]
    private void AddUrl()
    {
        var url = UrlInput.Trim();
        if (FileRouter.IsUrl(url)) AddPaths(new[] { url });
        UrlInput = string.Empty;
    }

    /// <summary>Adds supported paths/URLs, skipping duplicates. Used by pickers and drag-drop.</summary>
    public void AddPaths(IEnumerable<string> paths)
    {
        var existing = Jobs.Select(j => j.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var p in paths)
        {
            if (!FileRouter.IsSupported(p) || !existing.Add(p)) continue;
            Jobs.Add(new JobItem(p));
        }
        if (Jobs.Count > 0 && StatusSummary == "Ready.")
            StatusSummary = $"{Jobs.Count} file(s) queued.";
    }

    [RelayCommand]
    private void Remove(JobItem item) => Jobs.Remove(item);

    [RelayCommand]
    private void Clear()
    {
        Jobs.Clear();
        ProgressValue = 0;
        StatusSummary = "Ready.";
    }

    // ---- Output folder -----------------------------------------------------

    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, Hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null) OutputFolder = folder.Path;
    }

    [RelayCommand]
    private void OpenOutput()
    {
        if (Directory.Exists(OutputFolder))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{OutputFolder}\"") { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenFile(JobItem item)
    {
        if (item.HasOutput)
            Process.Start(new ProcessStartInfo(item.OutputPath!) { UseShellExecute = true });
    }

    // ---- Conversion --------------------------------------------------------

    private bool CanConvert() => HasJobs && !IsConverting;

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        var pending = Jobs.Where(j => j.Status is not ConversionStatus.Succeeded).ToList();
        if (pending.Count == 0) { StatusSummary = "Nothing to convert."; return; }

        foreach (var j in pending) j.Reset();
        var bySource = pending.ToDictionary(j => j.Source, StringComparer.OrdinalIgnoreCase);

        IsConverting = true;
        ConvertCommand.NotifyCanExecuteChanged();
        ProgressValue = 0;
        int total = pending.Count, done = 0;
        StatusSummary = $"Converting 0/{total}…";
        _cts = new CancellationTokenSource();

        var progress = new Progress<JobProgress>(p =>
        {
            if (!bySource.TryGetValue(p.Source, out var item)) return;
            item.Status = p.Status;
            item.Route = p.Route;
            item.OutputPath = p.OutputPath;
            item.Error = p.Error;

            if (p.Status is ConversionStatus.Succeeded or ConversionStatus.Failed or ConversionStatus.Skipped)
            {
                done++;
                ProgressValue = total == 0 ? 0 : (double)done / total * 100;
                StatusSummary = $"Converting {done}/{total}…";
            }
        });

        try
        {
            var options = new ConversionOptions();
            await using var batch = new BatchConverter(options);
            var results = await batch.RunAsync(pending.Select(j => j.Source).ToList(), OutputFolder, progress, _cts.Token);

            int ok = results.Count(r => r.Status == ConversionStatus.Succeeded);
            int failed = results.Count(r => r.Status == ConversionStatus.Failed);
            StatusSummary = $"Done — {ok} converted" + (failed > 0 ? $", {failed} failed." : ".");
        }
        catch (OperationCanceledException)
        {
            StatusSummary = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusSummary = $"Error: {ex.Message}";
        }
        finally
        {
            IsConverting = false;
            ConvertCommand.NotifyCanExecuteChanged();
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();
}
