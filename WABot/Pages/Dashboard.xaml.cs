namespace WABot.Pages;

public partial class Dashboard : INotifyPropertyChanged
{
    public Dashboard()
    {
        InitializeComponent();
        DataContext = this;
        DeviceBtnText = "Запустить";
    }

    #region Variables

    private static bool _isBusy;

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
            return;

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
            var phones = new List<string>();

            async Task Setup()
            {
                while (true)
                {
                    var accounts = (await Globals.GetAccountsWarm(phones.ToArray())).Take(Globals.Devices.Count)
                        .ToArray();

                    if (accounts.Length < 2) break;

                    phones.Add(accounts[0].phone);

                    for (var i = 0; i < accounts.Length; i++)
                    {
                        await Globals.Devices[i].ReCreate(account: accounts[i].path, phone: $"+{accounts[i].phone}");
                        await Globals.Devices[i].Start();
                    }

                    await StartPromise();

                    ProgressValue = (int) (((float) phones.Count / (float) Directory.GetDirectories(Globals.Setup.PathToDirectoryAccounts).Count(accountDirectory => File.Exists($@"{accountDirectory}\Data.json"))) * 100);
                }
            }

            await Setup();
            
            MessageBox.Show("Прогрев завершен");
            ProgressValue = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Произошла ошибка, лог создан на рабочем столе");
            await File.WriteAllTextAsync("Error.txt", $"{ex.Message}");
        }

        _isBusy = false;
        
        async Task Promise(WAClient client1, WAClient client2, IEnumerable<string> texts)
        {
            foreach (var text in texts)
            {
                await client1.SendMessage(client2.Phone, text);

                await Task.Delay(500);
            
                await client2.SendMessage(client1.Phone, text);
            }
            
            client1.AccountData.LastActiveDialog![client2.Phone] = DateTime.Now;
            client2.AccountData.LastActiveDialog![client1.Phone] = DateTime.Now;
            
            await client1.UpdateData();
            await client2.UpdateData();

        }

        async Task StartPromise()
        {
            var tasks = new List<Task>();

            if (tasks is null) throw new ArgumentNullException(nameof(tasks));

            await File.WriteAllTextAsync(@"contact.vcf",
                ContactManager.Export(Globals.Devices.Select(client => client.Phone)
                    .Select(phone => new CObj($"Artemiy {new Random().Next(0, 20_000)}", phone)).ToList()));

            foreach (var device in Globals.Devices)
            {
                await device.ImportContacts(new FileInfo(@"contact.vcf").FullName);
                if (!await device.LoginFile())
                    Directory.Delete(device.Account, true);
            }

            //foreach (var from in Globals.Devices)
            //{
            Globals.Devices[0].AccountData.TrustLevelAccount++;

            tasks.AddRange(Globals.Devices.Select(to =>
                Task.Run(async () => await Promise(Globals.Devices[0], to, TextMessage.Split('\n')))));

            await Globals.Devices[0].UpdateData();
            //}

            Task.WaitAll(tasks.ToArray(), -1);
        }
    }

    private void Newsletter(object sender, RoutedEventArgs e)
    {
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
                //await Memu.RemoveAll();

                for (var i = 0; i < Globals.Setup.CountDevices; i++)
                {
                    ProgressValue = (int)(((i + 1f) / Globals.Setup.CountDevices) * 100);
                    //await Memu.Import(Globals.Setup.PathToImageDevice);

                    var (phone, path) = await Globals.GetRandomAccount(Globals.Setup.TrustLevelAccount);

                    if (path == string.Empty)
                        throw new Exception("Аккаунтов больше не найдено, попробуйте снова");

                    var device = new WAClient(deviceId: i,
                        account: path, phone: $"+{phone}");
                    
                    await device.Start();

                    Globals.Devices.Add(device);
                }

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

    private async void CheckAccounts(object sender, RoutedEventArgs e)
    {
        var isChecked = new List<string>();
        var max = (float)Directory.GetDirectories(Globals.Setup.PathToDirectoryAccounts).Length;
        var current = (float)0;
        
        while (true)
        {
            var accounts = new List<(string phone, string path)>();
            
            foreach (var accountDirectory in Directory.GetDirectories(Globals.Setup.PathToDirectoryAccounts))
            {
                if (!File.Exists($@"{accountDirectory}\Data.json") ||
                    !Directory.Exists($@"{accountDirectory}\com.whatsapp"))
                {
                    Directory.Delete(accountDirectory, true);
                    continue;
                }

                if (isChecked.Contains(accountDirectory))
                    continue;

                accounts.Add((new DirectoryInfo(accountDirectory).Name, accountDirectory));
            }

            if (accounts.Count == 0)
                break;
            
            var firstAccs = accounts.Take(Globals.Devices.Count).ToArray();

            for (var i = 0; i < firstAccs.Length; i++)
            {
                await Globals.Devices[i].ReCreate(account: firstAccs[i].path, phone: $"+{firstAccs[i].phone}");
                await Globals.Devices[i].Start();
            }


            Task.WaitAll(firstAccs.Select((t, i) => i)
                .Select(i1 => Task.Run(async () =>
                {
                    isChecked.Add(firstAccs[i1].path);

                    if (await Globals.Devices[i1].LoginFile()) 
                        return;

                    Directory.Delete(firstAccs[i1].path, true);
                }))
                .ToArray(), -1);

            current += Globals.Devices.Count;

            var current1 = current;
            
            Dispatcher.Invoke(() => ProgressValue = (int) (current1 / max) * 100);
        }

        MessageBox.Show("Проверка окончена");
    }
}