namespace WABot.Pages;

public partial class Dashboard : INotifyPropertyChanged
{
    public Dashboard()
    {
        InitializeComponent();
        DataContext = this;
        DeviceBtnText = "Запустить";
        _warm = new Warm();
        _newsletter = new Newsletter();
    }

    #region Variables

    private static bool _isBusy;

    private readonly Warm _warm;
    
    private readonly Newsletter _newsletter;

    #endregion
    
    #region Variables UI
    
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

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
    
    private string _deviceBtnText = null!;

    public string DeviceBtnText
    {
        get => _deviceBtnText;
        set
        {
            _deviceBtnText = value;
            OnPropertyChanged("DeviceBtnText");
        }
    }
    
    #endregion

    private async void Warming(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            if (!_warm.IsWork) 
                return;
            
            _warm.Stop();
            _isBusy = false;
            MessageBox.Show("Вы отменили прогрев");
            ProgressValue = 0;

            return;
        }

        if (Globals.Devices.Count == 0 || !Globals.Setup.EnableWarm)
        {
            MessageBox.Show("Запустите устройства в режиме прогрева");
            return;
        }
        
        if (string.IsNullOrEmpty(TextMessage))
        {
            MessageBox.Show("Укажите текст сообщений");
            return;
        }

        _isBusy = true;

        try
        {

            ProgressValue = 100;

            await Task.Run(() => _warm.Start(TextMessage));
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
        {
            if (!_newsletter.IsWork) 
                return;
            
            MessageBox.Show("Дождитесь завершения рассылки");
            return;
        }

        if (Globals.Devices.Count == 0 || Globals.Setup.EnableWarm)
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

            await Task.Run(() => _newsletter.Start(TextMessage));
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

    private async void DevicesSetup(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;
        
        if (Globals.Setup.CountDevices == 0 || !File.Exists(Globals.Setup.PathToImageDevice))
        {
            MessageBox.Show("Похоже вы не указали все настройки для запуска устройств");
            return;
        }
        
        _isBusy = true;
        
        try
        {
            DeviceBtnText = (DeviceBtnText == "Запустить") ? "Отключить" : "Запустить";
            
            if (DeviceBtnText == "Отключить")//Та самая карта-обраточка из uno
            {
                await Memu.RemoveAll();

                for (var i = 0; i < Globals.Setup.CountDevices; i++)
                {
                    ProgressValue = (int)(((i + 1f) / Globals.Setup.CountDevices) * 100);
                    await Memu.Import(Globals.Setup.PathToImageDevice);

                    var device = new WAClient(deviceId: i);

                    await device.GetInstance().Spoof("7", true);
                    await device.Start();

                    await device.GetInstance().Shell("settings put global window_animation_scale 0");
                    await device.GetInstance().Shell("settings put global transition_animation_scale 0");
                    await device.GetInstance().Shell("settings put global animator_duration_scale 0");
                    
                    Globals.Devices.Add(device);
                }

                await Task.Run(Memu.RunAdbServer);

                MessageBox.Show(
                    $"Устройства были запущены со следующими параметрами:\nМин. уровень прогрева: {Globals.Setup.TrustLevelAccount}\nРежим прогрева: {Globals.Setup.EnableWarm}");
            }
            else
            {
                foreach (var device in Globals.Devices)
                    await device.Stop();
                
                Globals.Devices.Clear();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Произошла ошибка, лог создан на рабочем столе");
            await File.WriteAllTextAsync("Error.txt", $"{ex.Message}");
        }
        
        ProgressValue = 0;
        _isBusy = false;
    }
}