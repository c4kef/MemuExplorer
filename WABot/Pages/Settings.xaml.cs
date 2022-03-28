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

    #endregion

    private async void SelectAccounts(object sender, RoutedEventArgs e)
    { 
        var dialog = new CommonOpenFileDialog();
        dialog.IsFolderPicker = true;
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            Globals.Setup.PathToDirectoryAccounts = dialog.FileName;
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

    private async void ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => await Globals.SaveSetup();
}