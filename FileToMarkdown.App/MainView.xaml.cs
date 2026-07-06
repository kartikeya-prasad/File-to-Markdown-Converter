using FileToMarkdown.App.Services;
using FileToMarkdown.App.ViewModels;
using FileToMarkdown.Core;
using FileToMarkdown.Core.Components;
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
        OcrEngineCombo.SelectedIndex =
            s.OcrEngine.Equals(nameof(OcrEngineKind.Surya), StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        OcrEngineStatusText.Text = PythonPackageInstaller.IsPackagePresent("surya")
            ? "Surya is installed and ready."
            : "Surya is not installed yet — selecting it downloads ~2 GB (PyTorch) plus model files on first use.";
        UseOcrmyPdfCheck.IsChecked = s.UseOcrmyPdf;
        SavePdfCheck.IsChecked = s.SaveSearchablePdf;
        RefreshOcrmyPdfStatus();
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

        bool wantsSurya = OcrEngineCombo.SelectedIndex == 1;
        s.OcrEngine = wantsSurya ? nameof(OcrEngineKind.Surya) : nameof(OcrEngineKind.Tesseract);
        s.UseOcrmyPdf = UseOcrmyPdfCheck.IsChecked == true;
        s.SaveSearchablePdf = SavePdfCheck.IsChecked == true;

        ViewModel.SaveSettings();
        ApplyTheme();

        if (wantsSurya && !PythonPackageInstaller.IsPackagePresent("surya"))
            await InstallSuryaAsync();
    }

    /// <summary>On-demand Surya install: confirm, disk-space precheck, streamed pip log,
    /// cancellation, and a clean fallback to Tesseract on failure.</summary>
    private async Task InstallSuryaAsync()
    {
        const long RequiredFreeBytes = 6L * 1024 * 1024 * 1024; // pip cache + torch + models

        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Install Surya OCR?",
            Content = "Surya needs a one-time download of roughly 2 GB (PyTorch and dependencies), "
                    + "plus 1–2 GB of model files fetched automatically on first use.\n\n"
                    + "Tesseract remains available either way.",
            PrimaryButtonText = "Download and install",
            CloseButtonText = "Not now",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            RevertToTesseract("Surya install declined.");
            return;
        }

        if (PythonPackageInstaller.GetFreeDiskBytes() < RequiredFreeBytes)
        {
            RevertToTesseract(null);
            await new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Not enough disk space",
                Content = "Surya needs about 6 GB free during installation. Free some space and try again; "
                        + "the OCR engine stays on Tesseract for now.",
                CloseButtonText = "OK",
            }.ShowAsync();
            return;
        }

        bool installed = await RunComponentInstallAsync(
            "Installing Surya OCR…",
            (progress, ct) => new PythonPackageInstaller().InstallAsync("surya-ocr", progress, ct));

        if (installed)
        {
            await new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Surya installed",
                Content = "Surya OCR is ready. Its models (~1–2 GB) download automatically the first "
                        + "time it runs, so the first conversion will take noticeably longer.",
                CloseButtonText = "OK",
            }.ShowAsync();
        }
        else
        {
            RevertToTesseract("Surya not installed — using Tesseract.");
        }
    }

    /// <summary>Runs a long install with a cancellable progress dialog streaming its log.
    /// Returns true on success; failures show an error dialog, cancellation is silent.</summary>
    private async Task<bool> RunComponentInstallAsync(
        string title, Func<IProgress<string>, CancellationToken, Task> install)
    {
        var logText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Text = "Starting…",
        };
        using var cts = new CancellationTokenSource();
        var progressDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new ProgressBar { IsIndeterminate = true },
                    new ScrollViewer { MaxHeight = 160, Content = logText },
                },
            },
            CloseButtonText = "Cancel",
        };
        progressDialog.CloseButtonClick += (_, _) => cts.Cancel();

        _ = progressDialog.ShowAsync();
        try
        {
            await install(new Progress<string>(line => logText.Text = line), cts.Token);
            progressDialog.Hide();
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            progressDialog.Hide();
            await new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Install failed",
                Content = ex.Message,
                CloseButtonText = "OK",
            }.ShowAsync();
            return false;
        }
    }

    private void RevertToTesseract(string? status)
    {
        ViewModel.Settings.OcrEngine = nameof(OcrEngineKind.Tesseract);
        ViewModel.SaveSettings();
        if (status is not null) ViewModel.StatusSummary = status;
    }

    // ---- Enhanced PDF OCR (OCRmyPDF) component -----------------------------

    private void RefreshOcrmyPdfStatus()
    {
        bool ready = ComponentManager.IsOcrmyPdfReady();
        OcrmyPdfStatusText.Text = ready
            ? "Installed — PDFs get a proper OCR text layer via OCRmyPDF's --redo-ocr."
            : "Not installed. Adds OCRmyPDF plus the Tesseract CLI and Ghostscript "
            + "(~100 MB, fetched from their official release pages).";
        InstallOcrmyPdfButton.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnInstallOcrmyPdfClick(object sender, RoutedEventArgs e)
    {
        SettingsDialog.Hide();

        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Install Enhanced PDF OCR?",
            Content = "This downloads OCRmyPDF into the app's Python, plus the official Tesseract "
                    + "(UB Mannheim) and Ghostscript (Artifex) Windows builds — about 100 MB total. "
                    + "Ghostscript is AGPL-licensed, which is why it is fetched from its official "
                    + "source instead of shipping inside this app.",
            PrimaryButtonText = "Download and install",
            CloseButtonText = "Not now",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        bool installed = await RunComponentInstallAsync(
            "Installing Enhanced PDF OCR…",
            (progress, ct) => new ComponentManager().InstallAsync(progress, ct));

        if (installed)
        {
            await new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Enhanced PDF OCR installed",
                Content = "PDF conversions now run through OCRmyPDF, and you can enable the "
                        + "searchable-PDF output in Settings.",
                CloseButtonText = "OK",
            }.ShowAsync();
        }
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
