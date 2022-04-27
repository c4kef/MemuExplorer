namespace WABot.Pages;

public partial class Manager : INotifyPropertyChanged
{
    public Manager()
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

    #endregion
}