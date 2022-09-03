using System.Diagnostics;
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
    /// Уровень прогрева при рассылке
    /// </summary>
    public int WarmLevelForNewsletter
    {
        get => Globals.Setup.WarmLevelForNewsletter;
        set => Globals.Setup.WarmLevelForNewsletter = value;
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
    /// Кол-во прогонов через вебку
    /// </summary>
    public int CountWarmsOnWeb
    {
        get => Globals.Setup.CountWarmsOnWeb;
        set => Globals.Setup.CountWarmsOnWeb = value;
    }

    /// <summary>
    /// Кол-во прогонов через вебку
    /// </summary>
    public int DelaySendMessageFrom
    {
        get => Globals.Setup.DelaySendMessageFrom;
        set => Globals.Setup.DelaySendMessageFrom = value;
    }

    /// <summary>
    /// Кол-во прогонов через вебку
    /// </summary>
    public int DelaySendMessageTo
    {
        get => Globals.Setup.DelaySendMessageTo;
        set => Globals.Setup.DelaySendMessageTo = value;
    }

    /// <summary>
    /// Включить подготовку через веб?
    /// </summary>
    public bool EnableWeb
    {
        get => Globals.Setup.EnableWeb;
        set => Globals.Setup.EnableWeb = value;
    }

    /// <summary>
    /// Включить минимальный прогрев?
    /// </summary>
    public bool EnableMinWarm
    {
        get => Globals.Setup.EnableMinWarm;
        set => Globals.Setup.EnableMinWarm = value;
    }

    /// <summary>
    /// Включить подготовку через веб?
    /// </summary>
    public bool EnableCheckBan
    {
        get => Globals.Setup.EnableCheckBan;
        set => Globals.Setup.EnableCheckBan = value;
    }

    /// <summary>
    /// Включить сканирование qr кода при подготовке?
    /// </summary>
    public bool EnableScanQr
    {
        get => Globals.Setup.EnableScanQr;
        set => Globals.Setup.EnableScanQr = value;
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
                        JsonConvert.SerializeObject(new AccountData()));

            await Globals.SaveSetup();
        }

        OnPropertyChanged("ColorPathToDirectoryAccounts");
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

    private async void EnableWebClicked(object sender, RoutedEventArgs e)
    {
        EnableWeb = (sender as CheckBox)!.IsChecked!.Value;
        OnPropertyChanged("EnableWeb");

        await Globals.SaveSetup();
    }

    private async void EnableScanQrClicked(object sender, RoutedEventArgs e)
    {
        EnableScanQr = (sender as CheckBox)!.IsChecked!.Value;
        OnPropertyChanged("EnableScanQr");

        await Globals.SaveSetup();
    }

    private async void EnableCheckBanClicked(object sender, RoutedEventArgs e)
    {
        EnableCheckBan = (sender as CheckBox)!.IsChecked!.Value;
        OnPropertyChanged("EnableCheckBan");

        await Globals.SaveSetup();
    }

    private async void EnableMinWarmClicked(object sender, RoutedEventArgs e)
    {
        EnableMinWarm = (sender as CheckBox)!.IsChecked!.Value;
        OnPropertyChanged("EnableMinWarm");

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