using FileToMarkdown.App.Services;
using FileToMarkdown.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FileToMarkdown.App
{
    /// <summary>Application entry point. Owns the single main window and the DI container.</summary>
    public partial class App : Application
    {
        /// <summary>The main application window (used for file-picker HWND interop).</summary>
        public static Window? MainWindow { get; private set; }

        /// <summary>Application-wide service provider.</summary>
        public static IServiceProvider Services { get; private set; } = null!;

        public App()
        {
            InitializeComponent();
            Services = ConfigureServices();
            // Capture any otherwise-silent startup/runtime crash to a log file so
            // failures are diagnosable instead of the window just vanishing.
            UnhandledException += (_, e) => LogCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject as Exception);
        }

        private static IServiceProvider ConfigureServices() =>
            new ServiceCollection()
                .AddSingleton<ISettingsService, JsonSettingsService>()
                .AddSingleton<IUpdateService, UpdateService>()
                .AddSingleton<MainViewModel>()
                .BuildServiceProvider();

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }

        /// <summary>Appends an exception to %LOCALAPPDATA%\FileToMarkdownConverter\crash.log.</summary>
        private static void LogCrash(Exception? ex)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FileToMarkdownConverter");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, "crash.log"),
                    $"[{DateTime.Now:O}] {ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never itself throw during a crash.
            }
        }
    }
}
