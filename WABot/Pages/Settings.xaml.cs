using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace WABot.Pages;

public partial class Settings : INotifyPropertyChanged
{
    public Settings()
    {
        InitializeComponent();
        DataContext = this;
    }

    #region Variables

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string prop = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    /// <summary>
    /// Кол-во потоков для хрома
    /// </summary>
    public int CountThreadsChrome
    {
        get => Globals.Setup.CountThreadsChrome;
        set => Globals.Setup.CountThreadsChrome = value;
    }

    /// <summary>
    /// Кол-во потоков для хрома
    /// </summary>
    public int CountMessagesFromAccount
    {
        get => Globals.Setup.CountMessagesFromAccount;
        set => Globals.Setup.CountMessagesFromAccount = value;
    }

    /// <summary>
    /// Включить прогрев?
    /// </summary>
    public bool EnableWarm
    {
        get => Globals.Setup.EnableWarm;
        set => Globals.Setup.EnableWarm = value;
    }

    public Brush ColorPathToDirectoryAccounts =>
        string.IsNullOrEmpty(Globals.Setup.PathToDirectoryAccounts) ||
        !Directory.Exists(Globals.Setup.PathToDirectoryAccounts)
            ? Brushes.Red
            : Brushes.GreenYellow;

    public Brush ColorPathToTextForWarm =>
    string.IsNullOrEmpty(Globals.Setup.PathToTextForWarm) ||
    !File.Exists(Globals.Setup.PathToTextForWarm)
        ? Brushes.Red
        : Brushes.GreenYellow;

    public Brush ColorPathToPhonesUsers =>
        string.IsNullOrEmpty(Globals.Setup.PathToPhonesUsers) ||
        !File.Exists(Globals.Setup.PathToPhonesUsers)
            ? Brushes.Red
            : Brushes.GreenYellow;

    public Brush ColorPathToImageDevice =>
        string.IsNullOrEmpty(Globals.Setup.PathToImageDevice) ||
        !File.Exists(Globals.Setup.PathToImageDevice)
            ? Brushes.Red
            : Brushes.GreenYellow;

    public Brush ColorPathToUserNames =>
        string.IsNullOrEmpty(Globals.Setup.PathToUserNames) ||
        !File.Exists(Globals.Setup.PathToUserNames)
            ? Brushes.Red
            : Brushes.GreenYellow;

    public Brush ColorPathToQRs =>
        string.IsNullOrEmpty(Globals.Setup.PathToQRs) ||
        !Directory.Exists(Globals.Setup.PathToQRs)
            ? Brushes.Red
            : Brushes.GreenYellow;

    public Brush ColorPathToDirectoryAccountsWeb =>
    string.IsNullOrEmpty(Globals.Setup.PathToDirectoryAccountsWeb) ||
    !Directory.Exists(Globals.Setup.PathToDirectoryAccountsWeb)
        ? Brushes.Red
        : Brushes.GreenYellow;

    #endregion

    private async void SelectQrCodes(object sender, RoutedEventArgs e)
    {
        var dialog = new CommonOpenFileDialog();
        dialog.IsFolderPicker = true;
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            Globals.Setup.PathToQRs = dialog.FileName;
            await Globals.SaveSetup();
        }

        OnPropertyChanged("ColorPathToQRs");
    }

    private async void SelectAccounts(object sender, RoutedEventArgs e)
    {
        var dialog = new CommonOpenFileDialog();
        dialog.IsFolderPicker = true;
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            Globals.Setup.PathToDirectoryAccounts = dialog.FileName;
            foreach (var variaDirectory in Directory.GetDirectories(dialog.FileName))
                if (!File.Exists($@"{variaDirectory}\Data.json"))
                    await File.WriteAllTextAsync($@"{variaDirectory}\Data.json",
                        JsonConvert.SerializeObject(new AccountData()
                        {
                            LastActiveDialog = new Dictionary<string, DateTime>()
                        }));

            await Globals.SaveSetup();
        }

        OnPropertyChanged("ColorPathToDirectoryAccounts");
    }

    private async void SelectAccountsWeb(object sender, RoutedEventArgs e)
    {
        var dialog = new CommonOpenFileDialog();
        dialog.IsFolderPicker = true;
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            Globals.Setup.PathToDirectoryAccountsWeb = dialog.FileName;
            await Globals.SaveSetup();
        }

        OnPropertyChanged("ColorPathToDirectoryAccountsWeb");
    }

    private async void SelectNumbers(object sender, RoutedEventArgs e)
    {
        var dialog = new CommonOpenFileDialog();
        dialog.Filters.Add(new CommonFileDialogFilter("Файл с номерами", ".txt"));
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            Globals.Setup.PathToPhonesUsers = dialog.FileName;
            await Globals.SaveSetup();
        }

        OnPropertyChanged("ColorPathToPhonesUsers");
    }

    private async void SelectImageDevice(object sender, RoutedEventArgs e)
    {
        var dialog = new CommonOpenFileDialog();
        dialog.Filters.Add(new CommonFileDialogFilter("Образ устройства", ".ova"));
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            Globals.Setup.PathToImageDevice = dialog.FileName;
            await Globals.SaveSetup();
        }

        OnPropertyChanged("ColorPathToImageDevice");
    }

    private async void SelectUserNames(object sender, RoutedEventArgs e)
    {
        var dialog = new CommonOpenFileDialog();
        dialog.Filters.Add(new CommonFileDialogFilter("Имена пользователей", ".csv"));
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            Globals.Setup.PathToUserNames = dialog.FileName;
            await Globals.SaveSetup();
        }

        OnPropertyChanged("ColorPathToUserNames");
    }

    private async void ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        tryAgain:
        try
        {
            await Globals.SaveSetup();
        }
        catch
        {
            goto tryAgain;
        }
    }

    private async void EnableWarmClicked(object sender, RoutedEventArgs e)
    {
        EnableWarm = (sender as CheckBox)!.IsChecked!.Value;
        OnPropertyChanged("EnableWarm");

        await Globals.SaveSetup();
    }

    private async void SelectFileTextForWarm(object sender, RoutedEventArgs e)
    {
        var dialog = new CommonOpenFileDialog();
        dialog.Filters.Add(new CommonFileDialogFilter("Текст прогрева", ".txt"));
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            Globals.Setup.PathToTextForWarm = dialog.FileName;
            await Globals.SaveSetup();
        }

        OnPropertyChanged("ColorPathToTextForWarm");
    }
}