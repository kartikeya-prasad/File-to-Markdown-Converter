using FileToMarkdown.App.ViewModels;
using FileToMarkdown.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.ViewManagement;

namespace FileToMarkdown.App;

public sealed partial class MainView : UserControl
{
    // Kept as a field: UISettings raises no events once garbage-collected.
    private readonly UISettings _uiSettings = new();

    public MainViewModel ViewModel { get; } = App.Services.GetRequiredService<MainViewModel>();

    public MainView()
    {
        InitializeComponent();
        ApplyTheme();

        // Follow live OS light/dark switches while the preference is "System".
        _uiSettings.ColorValuesChanged += (_, _) =>
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel.Settings.Theme == ElementTheme.Default) ApplyTheme();
            });
    }

    private void ApplyTheme() => Root.RequestedTheme = ViewModel.Settings.Theme;

    private async void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var s = ViewModel.Settings;

        ThemeCombo.SelectedIndex = s.Theme switch
        {
            ElementTheme.Light => 1,
            ElementTheme.Dark => 2,
            _ => 0,
        };
        ConcurrencyBox.Value = s.Concurrency;
        DpiBox.Value = s.OcrDpi;
        OverwriteCombo.SelectedIndex = (int)s.Overwrite;

        SettingsDialog.XamlRoot = XamlRoot;
        var result = await SettingsDialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        s.Theme = ThemeCombo.SelectedIndex switch
        {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        if (!double.IsNaN(ConcurrencyBox.Value)) s.Concurrency = (int)ConcurrencyBox.Value;
        if (!double.IsNaN(DpiBox.Value)) s.OcrDpi = (int)DpiBox.Value;
        s.Overwrite = (OverwritePolicy)Math.Max(0, OverwriteCombo.SelectedIndex);

        ViewModel.SaveSettings();
        ApplyTheme();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Add to queue";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var paths = new List<string>();
            foreach (var item in items)
            {
                if (item is Windows.Storage.StorageFolder folder)
                    paths.AddRange(Directory.EnumerateFiles(folder.Path, "*", SearchOption.AllDirectories));
                else
                    paths.Add(item.Path);
            }
            ViewModel.AddPaths(paths);
        }
        finally
        {
            deferral.Complete();
        }
    }
}
