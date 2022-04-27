namespace WABot;

public partial class MainWindow
{
    public MainWindow()
    {
        Task.Run(Globals.Init).Wait();

        InitializeComponent();

        ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
    }
}