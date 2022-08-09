using System.Drawing;
using System.Drawing.Imaging;
using VirtualCameraOutput;
using WABot.WhatsApp.Web;

namespace WABot.Pages;

public partial class Dashboard : INotifyPropertyChanged
{
    public Dashboard()
    {
        InitializeComponent();
        DataContext = this;
        _warm = new Warm();
        _register = new Register();
        _newsletter = new WhatsApp.Newsletter();
        _preparation = new AccPreparation();

        if (!_managerDevicesIsRuning)
            _ = Task.Run(ManagerDevices);
    }

    #region Variables

    /// <summary>
    /// Активный таск (определяем завершение работы)
    /// </summary>
    private static Task _activeTask = null!; //To-Do
    
    /// <summary>
    /// Активна задача?
    /// </summary>
    private static bool _isBusy;

    /// <summary>
    /// Прогрев аккаунтов
    /// </summary>
    private readonly Warm _warm;

    /// <summary>
    /// Подготовка аккаунтов
    /// </summary>
    private readonly AccPreparation _preparation;

    /// <summary>
    /// Рассылка сообщений Web
    /// </summary>
    private readonly WhatsApp.Web.Newsletter _newsletterWeb;

    /// <summary>
    /// Рассылка сообщений
    /// </summary>
    private readonly WhatsApp.Newsletter _newsletter;

    /// <summary>
    /// Регистрация аккаунтов
    /// </summary>
    private readonly Register _register;

    /// <summary>
    /// Обработчик устройств запущен
    /// </summary>
    private static bool _managerDevicesIsRuning;

    #endregion

    #region Variables UI

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string prop = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    private int _progressValue;

    /// <summary>
    /// Отображение статуса завершения задачи
    /// </summary>
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

    /// <summary>
    /// Тест для прогрева/рассылки
    /// </summary>
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

    /// <summary>
    /// Обработчик устройств (показывает или скрывает активные/неактивные устройства)
    /// </summary>
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

    /// <summary>
    /// Запуск рассылки Web
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void NewsletterWeb(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        if (Directory.GetFiles(Globals.Setup.PathToDirectoryAccountsWeb).Length <= 8 || Globals.Setup.EnableWarm)
        {
            MessageBox.Show("Слишком мало аккаунтов для рассылки или включен режим прогрева");
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

            _activeTask = Task.Run(() => _newsletterWeb.Start(TextMessage));
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

    /// <summary>
    /// Подготовка аккаунтов к рассылке в Web
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void Preparation(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        if (Globals.Devices.Count == 0 || !Globals.Devices.Any(device => device.IsActive))
        {
            MessageBox.Show("Запустите устройства");
            return;
        }

        if (string.IsNullOrEmpty(Globals.Setup.PathToQRs))
        {
            MessageBox.Show("Проведите настройку приложения");
            return;
        }

        _isBusy = true;

        try
        {
            ProgressValue = 100;

            _activeTask = Task.Run(async () => await _preparation.Start());
            await _activeTask;

            MessageBox.Show("Настройка завершена");

        }
        catch (Exception ex)
        {
            MessageBox.Show("Произошла ошибка, лог создан на рабочем столе");
            await File.WriteAllTextAsync("Error.txt", $"{ex.Message}");
        }

        ProgressValue = 0;
        _isBusy = false;
    }

    /// <summary>
    /// Запуск прогрева
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
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

    /// <summary>
    /// Запуск рассылки
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void Newsletter(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        if (Globals.Devices.Count == 0 || !Globals.Devices.Any(device => device.IsActive) || Globals.Setup.EnableWarm)
        {
            MessageBox.Show("Запустите устройства или отключите режим прогрева");
            return;
        }

        if (string.IsNullOrEmpty(TextMessage) || TextMessage.ToLower().Contains("https") || TextMessage.ToLower().Contains("http") || TextMessage.ToLower().Contains("www"))
        {
            MessageBox.Show("Укажите корректный текст сообщения");
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

    /// <summary>
    /// Запуск регистрации
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
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

    /// <summary>
    /// Применяем выбранный пользователем девайс для работы, или отключаем от работы
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void DataGrid_OnRowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
    {
        var index = Globals.Devices.FindIndex(device => device.Index == (e.Row.Item as Device)!.Index);
        Globals.Devices[index].IsActive = (e.Row.Item as Device)!.IsActive;
    }
}