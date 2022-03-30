using System.Windows.Controls;

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
    private void OnPropertyChanged(string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

    public int CountMessage
    {
        get => Globals.Setup.CountMessage;
        set => Globals.Setup.CountMessage = value;
    }

    public int DelayBetweenUsers
    {
        get => Globals.Setup.DelayBetweenUsers;
        set => Globals.Setup.DelayBetweenUsers = value;
    }

    public int TrustLevelAccount
    {
        get => Globals.Setup.TrustLevelAccount;
        set => Globals.Setup.TrustLevelAccount = value;
    }
    
    public int CountDevices
    {
        get => Globals.Setup.CountDevices;
        set => Globals.Setup.CountDevices = value;
    }
    
    public bool EnableWarm
    {
        get => Globals.Setup.EnableWarm;
        set => Globals.Setup.EnableWarm = value;
    }

    public Brush ColorPathToDirectoryAccounts =>
        (string.IsNullOrEmpty(Globals.Setup.PathToDirectoryAccounts) ||
         !Directory.Exists(Globals.Setup.PathToDirectoryAccounts))
            ? Brushes.Red
            : Brushes.GreenYellow;
    
    public Brush ColorPathToPhonesUsers =>
        (string.IsNullOrEmpty(Globals.Setup.PathToPhonesUsers) ||
         !File.Exists(Globals.Setup.PathToPhonesUsers))
            ? Brushes.Red
            : Brushes.GreenYellow;
    
    public Brush ColorPathToImageDevice =>
        (string.IsNullOrEmpty(Globals.Setup.PathToImageDevice) ||
         !File.Exists(Globals.Setup.PathToImageDevice))
            ? Brushes.Red
            : Brushes.GreenYellow;

    #endregion

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

    private async void ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => await Globals.SaveSetup();

    private async void EnableWarmClicked(object sender, RoutedEventArgs e)
    {
        EnableWarm = ((sender as CheckBox)!).IsChecked!.Value;
        OnPropertyChanged("EnableWarm");

        await Globals.SaveSetup();
    }
}