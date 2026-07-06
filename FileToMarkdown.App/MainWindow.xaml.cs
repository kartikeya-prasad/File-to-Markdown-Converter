using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace FileToMarkdown.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Title bar / taskbar icon (unpackaged apps don't inherit the exe icon here).
        var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(icon))
            AppWindow.SetIcon(icon);

        // Mica backdrop where supported (Windows 11); older systems silently keep
        // the opaque greige background from the theme.
        if (MicaController.IsSupported())
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
    }
}
