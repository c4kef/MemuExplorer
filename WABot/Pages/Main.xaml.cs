namespace WABot.Pages;

public partial class Main
{
    private static Settings _settings = null!;
    private static Dashboard _dashboard = null!;

    public Main()
    {
        InitializeComponent();
        _settings = new Settings();
        _dashboard = new Dashboard();

        RootFrame.Navigate(_dashboard);
    }

    private void RootFrame_Navigating(object sender, NavigatingCancelEventArgs e)
    {
        if (e.NavigationMode == NavigationMode.Back)
        {
            RootFrame.RemoveBackEntry();
        }
    }

    private void OpenSettings(object sender, RoutedEventArgs e) => RootFrame.Navigate(_settings);

    private void OpenDashboard(object sender, RoutedEventArgs e) => RootFrame.Navigate(_dashboard);
}