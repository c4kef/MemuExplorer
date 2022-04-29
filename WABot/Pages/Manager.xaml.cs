using System.Data;
using System.Windows.Input;

namespace WABot.Pages;

public partial class Manager : INotifyPropertyChanged
{
    public Manager()
    {
        InitializeComponent();
        DataContext = this;

        Sort = new List<string>()
        {
            "Валидные",
            "Помеченные"
        };

        Accounts = new List<string>();
    }

    #region Variables

    private List<string> _sort = null!;

    public List<string> Sort
    {
        get => _sort;
        set
        {
            _sort = value;
            OnPropertyChanged("Sort");
        }
    }
    
    private List<string> _accounts = null!;

    public List<string> Accounts
    {
        get => _accounts;
        set
        {
            _accounts = value;
            OnPropertyChanged("Accounts");
        }
    }
    
    private string _headerSelectedAccount = string.Empty;

    public string HeaderSelectedAccount
    {
        get => _headerSelectedAccount;
        set
        {
            _headerSelectedAccount = $"Аккаунт: {value}";
            OnPropertyChanged("HeaderSelectedAccount");
        }
    }
    
    private string _levelWarmAccount = string.Empty;

    public string LevelWarmAccount
    {
        get => _levelWarmAccount;
        set
        {
            _levelWarmAccount = $"Уровень прогрева: {value}";
            OnPropertyChanged("LevelWarmAccount");
        }
    }
    
    private string _allCountMessages = string.Empty;

    public string AllCountMessages
    {
        get => _allCountMessages;
        set
        {
            _allCountMessages = $"Всего сообщений: {value}";
            OnPropertyChanged("AllCountMessages");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string prop = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    #endregion

    private void FillAccounts(int indexSort)
    {
        Accounts.Clear();

        foreach (var accountDirectory in Directory.GetDirectories((indexSort == 0 ) ? Globals.Setup.PathToDirectoryAccounts : Globals.RemoveAccountsDirectory.FullName))
        {
            if (File.Exists($@"{accountDirectory}\Data.json") &&
                Directory.Exists($@"{accountDirectory}\com.whatsapp"))
                Accounts.Add(new DirectoryInfo(accountDirectory).Name);
        }
        
        DataGrid.Items.Refresh();
    }
    
    private void FillInfo(string? phone)
    {
        if (string.IsNullOrEmpty(phone))
            return;

        HeaderSelectedAccount = phone;
        AllCountMessages = "0";
        LevelWarmAccount = "0";
    }

    private void SelectSort(object sender, SelectionChangedEventArgs e) => FillAccounts((sender as ComboBox)!.SelectedIndex);

    private void SelectAccount(object sender, SelectionChangedEventArgs e) => FillInfo((sender as DataGrid)!.SelectedItem as string);

    private void Search(object sender, KeyEventArgs e)
    {
        var text = (sender as TextBox)!.Text;

        foreach (var dr in DataGrid.ItemsSource)
        {
            if (DataGrid.ItemContainerGenerator.ContainerFromItem(dr) is not DataGridRow row)
                continue;

            row.Visibility = Visibility.Visible;

            if (text != string.Empty && !dr.ToString()!.ToLower().Contains(text.ToLower()))
                row.Visibility = Visibility.Collapsed;
        }
    }
}