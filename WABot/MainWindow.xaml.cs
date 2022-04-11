namespace WABot;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Task.Run(Globals.Init).Wait();
    }
}