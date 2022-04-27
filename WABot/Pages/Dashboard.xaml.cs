namespace WABot.Pages;

public partial class Dashboard : INotifyPropertyChanged
{
    public Dashboard()
    {
        InitializeComponent();
        DataContext = this;
        _warm = new Warm();
        _register = new Register();
        _newsletter = new Newsletter();
        
        if (!_managerDevicesIsRuning)
            _ = Task.Run(ManagerDevices);
    }

    #region Variables

    private static Task _activeTask = null!; //To-Do

    private static bool _isBusy;

    private readonly Warm _warm;

    private readonly Newsletter _newsletter;

    private readonly Register _register;

    private static bool _managerDevicesIsRuning;

    #endregion

    #region Variables UI

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string prop = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    private int _progressValue;

    public int ProgressValue
    {
        get => _progressValue;
        set
        {
            _progressValue = value;
            OnPropertyChanged("ProgressValue");
        }
    }

    private string _textMessage = null!;

    public string TextMessage
    {
        get => _textMessage;
        set
        {
            _textMessage = value;
            OnPropertyChanged("TextMessage");
        }
    }

    #endregion

    private async Task ManagerDevices()
    {
        _managerDevicesIsRuning = true;
        while (_managerDevicesIsRuning)
        {
            await Task.Delay(1_500);
            
            Dispatcher.Invoke(() =>
            {
                if (DataGrid.IsEditing())
                    return;
                
                DataGrid.Items.Refresh();
            });
            
            var indexDevices = (await MemuCmd.ExecMemuc("listvms -r")).Split('\n').Select(line => line.Split(',')[0]).Where(index => int.TryParse(index, out _)).Select(int.Parse).ToArray();

            if (indexDevices.Length == 0)
            {
                Globals.Devices.Clear();
                continue;
            }

            if (Globals.Devices.Count != 0)
            {
                foreach (var newDeviceIndex in indexDevices)
                    if (Globals.Devices.Find(device => device.Index == newDeviceIndex) == null)
                        Globals.Devices.Add(
                            new Device()
                            {
                                Index = newDeviceIndex, Client = new WaClient(deviceId: newDeviceIndex),
                                IsActive = false
                            });

                Globals.Devices.RemoveAll(device => !indexDevices.Contains(device.Index) && !device.InUsage);
            }
            else
                Globals.Devices.Add(
                    new Device() {Index = indexDevices[0], Client = new WaClient(deviceId: indexDevices[0]), IsActive = false});
        }
    }

    private async void Warming(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        if (Globals.Devices.Count == 0 || !Globals.Devices.Any(device => device.IsActive) || !Globals.Setup.EnableWarm)
        {
            MessageBox.Show("Запустите устройства в режиме прогрева");
            return;
        }

        if (string.IsNullOrEmpty(TextMessage))
        {
            MessageBox.Show("Укажите текст сообщений");
            return;
        }
        
        if (Globals.Devices.Count(device => device.IsActive) % 2 != 0)
        {
            MessageBox.Show("Активных устройств должно быть четное кол-во");
            return;
        }

        _isBusy = true;

        try
        {
            ProgressValue = 100;

            _activeTask = Task.Run(async () => await _warm.Start(TextMessage));
            await _activeTask;

            MessageBox.Show("Прогрев завершен");

            ProgressValue = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Произошла ошибка, лог создан на рабочем столе");
            await File.WriteAllTextAsync("Error.txt", $"{ex.Message}");
        }

        _isBusy = false;
    }

    private async void Newsletter(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        if (Globals.Devices.Count == 0 || !Globals.Devices.Any(device => device.IsActive) || Globals.Setup.EnableWarm)
        {
            MessageBox.Show("Запустите устройства или отключите режим прогрева");
            return;
        }

        if (string.IsNullOrEmpty(TextMessage))
        {
            MessageBox.Show("Укажите текст сообщения");
            return;
        }

        _isBusy = true;

        try
        {
            ProgressValue = 100;

            _activeTask = Task.Run(() => _newsletter.Start(TextMessage));
            await _activeTask;

            MessageBox.Show("Рассылка завершена");

            ProgressValue = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Произошла ошибка, лог создан на рабочем столе");
            await File.WriteAllTextAsync("Error.txt", $"{ex.Message}");
        }

        _isBusy = false;
    }

    private async void RegAccounts(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        if (Globals.Devices.Count == 0 || !Globals.Devices.Any(device => device.IsActive) || !Globals.Setup.EnableWarm)
        {
            MessageBox.Show("Запустите устройства и включите режим прогрева");
            return;
        }

        _isBusy = true;

        try
        {
            ProgressValue = 100;

            _activeTask = Task.Run(async () => await _register.Start());
            await _activeTask;

            MessageBox.Show("Регистрация завершена");

            ProgressValue = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Произошла ошибка, лог создан на рабочем столе");
            await File.WriteAllTextAsync("Error.txt", $"{ex.Message}");
        }

        _isBusy = false;
    }

    private void DataGrid_OnRowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
    {
        var index = Globals.Devices.FindIndex(device => device.Index == (e.Row.Item as Device)!.Index);
        Globals.Devices[index].IsActive = (e.Row.Item as Device)!.IsActive;
    }
}