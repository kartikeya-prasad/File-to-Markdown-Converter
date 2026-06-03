using FileToMarkdown.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace FileToMarkdown.App;

public sealed partial class MainView : UserControl
{
    public MainViewModel ViewModel { get; } = new();

    public MainView()
    {
        InitializeComponent();
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
