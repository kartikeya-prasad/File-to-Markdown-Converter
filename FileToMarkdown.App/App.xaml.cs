namespace FileToMarkdown.App
{
    /// <summary>Application entry point. Owns the single main window.</summary>
    public partial class App : Application
    {
        /// <summary>The main application window (used for file-picker HWND interop).</summary>
        public static Window? MainWindow { get; private set; }

        public App() => InitializeComponent();

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
    }
}
