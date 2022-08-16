﻿using System.Drawing;
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
        _newsletter = new WhatsApp.Newsletter();
        _preparation = new AccPreparation();
        _preparationWeb = new AccPreparationWeb();
        _newsletterWeb = new WhatsApp.Web.Newsletter();
        _dashboard = this;

        if (!_managerDevicesIsRuning)
            _ = Task.Run(ManagerDevices);
    }

    #region Variables

    private static Dashboard _dashboard = null!;

    /// <summary>
    /// Активный таск (определяем завершение работы)
    /// </summary>
    private static Task _activeTask = null!;
    
    /// <summary>
    /// Активна задача?
    /// </summary>
    private static bool _isBusy;

    /// <summary>
    /// Подготовка аккаунтов
    /// </summary>
    private readonly AccPreparation _preparation;

    /// <summary>
    /// Подготовка аккаунтов
    /// </summary>
    private readonly AccPreparationWeb _preparationWeb;

    /// <summary>
    /// Рассылка сообщений Web
    /// </summary>
    private readonly WhatsApp.Web.Newsletter _newsletterWeb;

    /// <summary>
    /// Рассылка сообщений
    /// </summary>
    private readonly WhatsApp.Newsletter _newsletter;

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

    private int _averageMessages;

    /// <summary>
    /// Отображение среднее кол-во сообщений с аккаунта
    /// </summary>
    public int AverageMessages
    {
        get => _averageMessages;
        set
        {
            Dispatcher.Invoke(() =>
            {
                _averageMessages = value;
                OnPropertyChanged("AverageMessages");
            });
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

    public static Dashboard GetInstance() => _dashboard;

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
        {
            _newsletterWeb.IsStop = true;
            MessageBox.Show("Дождитесь завершения задачи");
            return;
        }
        
        if (Directory.GetFiles($@"{Globals.Setup.PathToDirectoryAccountsWeb}\First").Length < Globals.Setup.CountThreadsChrome)
        {
            MessageBox.Show("Слишком мало аккаунтов для рассылки");
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

            AverageMessages = ProgressValue = 0;
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
        {
            _preparationWeb.IsStop = true;
            _preparation.IsStop = true;
            MessageBox.Show("Дождитесь завершения задачи");
            return;
        }

        if (!Globals.Setup.EnableWarm)
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

        if (string.IsNullOrEmpty(TextMessage) && !File.Exists(Globals.Setup.PathToTextForWarm))
        {
            MessageBox.Show("Укажите текст прогрева или укажите путь до файла с этим текстом");
            return;
        }

        _isBusy = true;

        try
        {
            ProgressValue = 100;

            _activeTask = Task.Run(async () =>
            {
                if (Globals.Setup.EnableWarm)
                    await _preparationWeb.Start(string.IsNullOrEmpty(TextMessage) ? await File.ReadAllTextAsync(Globals.Setup.PathToTextForWarm) : TextMessage);

                else
                    await _preparation.Start(string.IsNullOrEmpty(TextMessage) ? await File.ReadAllTextAsync(Globals.Setup.PathToTextForWarm) : TextMessage);
            });
         
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
    /// Запуск рассылки
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void Newsletter(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            _newsletter.IsStop = true;
            MessageBox.Show("Дождитесь завершения задачи");
            return;
        }

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