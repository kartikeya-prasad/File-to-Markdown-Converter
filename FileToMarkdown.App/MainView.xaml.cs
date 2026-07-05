using FileToMarkdown.App.Services;
using FileToMarkdown.App.ViewModels;
using FileToMarkdown.Core;
using FileToMarkdown.Core.Updates;
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
    private readonly IUpdateService _updates = App.Services.GetRequiredService<IUpdateService>();

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

        Loaded += OnLoadedCheckForUpdates;
    }

    private void ApplyTheme() => Root.RequestedTheme = ViewModel.Settings.Theme;

    // ---- Updates -----------------------------------------------------------

    private async void OnLoadedCheckForUpdates(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedCheckForUpdates;

        var s = ViewModel.Settings;
        if (s.LastUpdateCheckUtc is { } last && DateTime.UtcNow - last < TimeSpan.FromHours(24))
            return;
        s.LastUpdateCheckUtc = DateTime.UtcNow;
        ViewModel.SaveSettings();

        UpdatePlan? plan;
        try
        {
            plan = await _updates.CheckAsync();
        }
        catch
        {
            return; // Offline or rate-limited - stay quiet, try again tomorrow.
        }

        if (plan is null || plan.LatestVersion == s.SkippedVersion) return;
        await ShowUpdateDialogAsync(plan);
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking…";
        try
        {
            var plan = await _updates.CheckAsync();
            if (plan is null)
            {
                UpdateStatusText.Text = $"You're up to date (v{_updates.CurrentVersion}).";
                return;
            }

            UpdateStatusText.Text = $"v{plan.LatestVersion} available.";
            SettingsDialog.Hide();
            await ShowUpdateDialogAsync(plan);
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"Check failed: {ex.Message}";
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private async Task ShowUpdateDialogAsync(UpdatePlan plan)
    {
        bool canAutoInstall = plan.Installer is not null && _updates.IsInnoInstall;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Update available — v{plan.LatestVersion}",
            PrimaryButtonText = canAutoInstall ? "Install now" : "Open download page",
            SecondaryButtonText = "Skip this version",
            CloseButtonText = "Later",
            DefaultButton = ContentDialogButton.Primary,
            Content = new ScrollViewer
            {
                MaxHeight = 320,
                Content = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Text = string.IsNullOrWhiteSpace(plan.ReleaseNotes)
                        ? "A new version is ready. The app restarts through the installer; your settings and queue folder are kept."
                        : plan.ReleaseNotes,
                },
            },
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary)
        {
            ViewModel.Settings.SkippedVersion = plan.LatestVersion;
            ViewModel.SaveSettings();
            return;
        }
        if (result != ContentDialogResult.Primary) return;

        if (!canAutoInstall)
        {
            _updates.OpenReleasePage(plan);
            return;
        }

        var progressDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Downloading update…",
            Content = new ProgressBar { IsIndeterminate = true, Margin = new Thickness(0, 12, 0, 0) },
        };
        _ = progressDialog.ShowAsync();
        try
        {
            var installerPath = await _updates.DownloadInstallerAsync(plan);
            progressDialog.Hide();
            _updates.LaunchInstaller(installerPath);
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            progressDialog.Hide();
            var error = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Update failed",
                Content = ex.Message,
                CloseButtonText = "OK",
            };
            await error.ShowAsync();
        }
    }

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
        UpdateStatusText.Text = string.Empty;
        AppVersionText.Text = $"Current version: v{_updates.CurrentVersion}"
            + (_updates.IsInnoInstall ? "" : " (updates via download page for this install type)");

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
