using CommunityToolkit.Mvvm.ComponentModel;
using FileToMarkdown.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace FileToMarkdown.App.Models;

/// <summary>One row in the conversion queue. Wraps a Core source with observable UI state.</summary>
public partial class JobItem : ObservableObject
{
    public string Source { get; }
    public bool IsUrl { get; }
    public string DisplayName { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusBrush))]
    [NotifyPropertyChangedFor(nameof(IsDone))]
    private ConversionStatus _status = ConversionStatus.Pending;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutput))]
    private string? _outputPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RouteText))]
    private ConversionRoute? _route;

    public JobItem(string source)
    {
        Source = source;
        IsUrl = FileRouter.IsUrl(source);
        DisplayName = IsUrl ? source : Path.GetFileName(source);
    }

    public string TypeLabel => IsUrl
        ? "URL"
        : Path.GetExtension(Source).TrimStart('.').ToUpperInvariant() is { Length: > 0 } ext ? ext : "FILE";

    public string RouteText => Route switch
    {
        ConversionRoute.Markitdown => "markitdown",
        ConversionRoute.ImageOcr => "OCR (image)",
        ConversionRoute.PdfOcr => "OCR (scanned PDF)",
        _ => "",
    };

    public string StatusText => Status switch
    {
        ConversionStatus.Pending => "Pending",
        ConversionStatus.Running => "Converting…",
        ConversionStatus.Succeeded => "Done",
        ConversionStatus.Failed => "Failed",
        ConversionStatus.Skipped => "Skipped",
        _ => "",
    };

    public Brush StatusBrush => new SolidColorBrush(Status switch
    {
        ConversionStatus.Succeeded => Colors.SeaGreen,
        ConversionStatus.Failed => Colors.IndianRed,
        ConversionStatus.Running => Colors.DodgerBlue,
        ConversionStatus.Skipped => Colors.Goldenrod,
        _ => Colors.Gray,
    });

    public bool IsDone => Status == ConversionStatus.Succeeded;
    public bool HasOutput => !string.IsNullOrEmpty(OutputPath) && File.Exists(OutputPath);
    public bool HasError => !string.IsNullOrEmpty(Error);

    public void Reset()
    {
        Status = ConversionStatus.Pending;
        Error = null;
        OutputPath = null;
        Route = null;
    }
}
