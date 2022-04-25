namespace WABot;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class MainWindow
{
    public MainWindow()
    {
        Task.Run(Globals.Init).Wait();

        InitializeComponent();
    }
}