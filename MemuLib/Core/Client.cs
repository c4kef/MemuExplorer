namespace MemuLib.Core;

public class Client
{
    /// <summary>
    /// Индекс машины
    /// </summary>
    private readonly int _index;

    /// <summary>
    /// Образ ADB клиента
    /// </summary>
    private readonly AdvancedAdbClient _adbClient;

    /// <summary>
    /// Образ устройства
    /// </summary>
    private DeviceData? _device;

    /// <summary>
    /// Локальное объявление информации о железе
    /// </summary>
    private DeviceInfoGenerated? _deviceInfo;
    
    /// <summary>
    /// Объявление образа машины
    /// </summary>
    /// <param name="index">индекс машины</param>
    public Client(int index)
    {
        _index = index;
        _adbClient = new AdvancedAdbClient();
    }

    /// <summary>
    /// Запуск машины
    /// </summary>
    public async Task Start()
    {
        if (!await Memu.Exists(_index))
        {
            Log.Write($"[{_index}] -> VM not found");
            return;
        }

        await Task.Delay(Settings.WaitingSecs);
        
        await Memu.Start(_index);

        await Task.Delay(2_500);//Ждем наверняка...
        
        var host = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}:\d{1,5}\b")
            .Match(await MemuCmd.ExecMemuc($"-i {_index} adb start-server")).Value;

        if (string.IsNullOrEmpty(host))
        {
            await MemuCmd.ExecMemuc($"-i {_index} adb kill-server");
            throw new Exception($"[{_index}] Can't start server");
        }

        _adbClient.Connect(host);

        _device = _adbClient.GetDevices().FirstOrDefault(deviceData => deviceData?.Serial == host) ?? null;
        
        if (_device is null)
            throw new Exception($"[{_index}] Can't connect to device");
        
        Log.Write($"[{_index}] -> VM started");
    }

    /// <summary>
    /// Остановка машины
    /// </summary>
    public async Task Stop()
    {
        if (!await Memu.Exists(_index))
        {
            Log.Write($"[{_index}] -> VM not found");
            return;
        }

        await Memu.Stop(_index);

        Log.Write($"[{_index}] -> VM stoped");
    }

    /// <summary>
    /// Установка приложения на машину
    /// </summary>
    /// <param name="path">путь до приложения на локальном компе</param>
    public async Task InstallApk(string path)
    {
        if (!await Memu.Exists(_index))
        {
            Log.Write($"[{_index}] -> VM not found");
            return;
        }

        if (!File.Exists(path))
        {
            Log.Write($"[{_index}] -> apk file not found");
            return;
        }

        await Memu.InstallApk(_index, path);

        Log.Write($"[{_index}] -> installed apk");
    }

    /// <summary>
    /// Запуск приложения на машине
    /// </summary>
    /// <param name="comPath">com-путь до установленного приложения на машине</param>
    public async Task RunApk(string comPath)
    {
        if (!await Memu.Exists(_index))
        {
            Log.Write($"[{_index}] -> VM not found");
            return;
        }

        await Task.Delay(Settings.WaitingSecs);
        await Memu.StartApk(_index, comPath);

        Log.Write($"[{_index}] -> apk runned");
    }

    /// <summary>
    /// Остановка приложения на машине
    /// </summary>
    /// <param name="comPath">com-путь до установленного приложения на машине</param>
    public async Task StopApk(string comPath)
    {
        if (!await Memu.Exists(_index))
        {
            Log.Write($"[{_index}] -> VM not found");
            return;
        }

        await Task.Delay(Settings.WaitingSecs);
        await Memu.StopApk(_index, comPath);

        Log.Write($"[{_index}] -> apk stopped");
    }

    /// <summary>
    /// Симуляция кликов по экрану
    /// </summary>
    /// <param name="x">по горизонтали</param>
    /// <param name="y">по вертикали</param>
    public async Task Click(int x, int y)
    {
        if (!await Memu.Exists(_index))
        {
            Log.Write($"[{_index}] -> VM not found");
            return;
        }

        await Task.Delay(500);
        await MemuCmd.ExecMemuc($"-i {_index} adb shell input tap {x} {y}");

        Log.Write($"[{_index}] -> input tap {x} {y}");
    }
    
    /// <summary>
    /// Проверка элемента на существование
    /// </summary>
    /// <param name="uiElement">название элемента в интерфейсе</param>
    public async Task<bool> ExistsElement(string uiElement)
    {
        if (!await Memu.Exists(_index))
        {
            Log.Write($"[{_index}] -> VM not found");
            return false;
        }

        var element = _adbClient.FindElement(_device, uiElement, TimeSpan.FromSeconds(1.5f));

        return element is not null;
    }

    /// <summary>
    /// Симуляция кликов по экрану
    /// </summary>
    /// <param name="uiElement">название элемента в интерфейсе</param>
    public async Task Click(string uiElement)
    {
        if (!await Memu.Exists(_index))
        {
            Log.Write($"[{_index}] -> VM not found");
            return;
        }

        var element = _adbClient.FindElement(_device, uiElement, TimeSpan.FromSeconds(1.5f));
       
        if (element is null)
            throw new Exception($"[{_index}] Can't found element by name \"{uiElement}\"");

        element.Click();

        Log.Write($"[{_index}] -> input tap uiElement");
    }
    
    /// <summary>
    /// Симуляция ввода текста
    /// </summary>
    /// <param name="uiElement">название элемента в интерфейсе</param>
    /// <param name="text">текст передаваемый на интерфейс</param>
    public async Task Input(string uiElement, string text)
    {
        if (!await Memu.Exists(_index))
        {
            Log.Write($"[{_index}] -> VM not found");
            return;
        }

        var element = _adbClient.FindElement(_device, uiElement, TimeSpan.FromSeconds(1.5f));
       
        if (element is null)
            throw new Exception($"[{_index}] Can't found element by name \"{uiElement}\"");

        element.SendText(text);

        Log.Write($"[{_index}] -> input text uiElement");
    }

    /// <summary>
    /// Отправка на удаленку
    /// </summary>
    /// <param name="local">путь на локальной машине</param>
    /// <param name="remote">путь на удаленной машине</param>
    public async Task Push(string local, string remote)
    {
        if (!await Memu.Exists(_index))
        {
            Log.Write($"[{_index}] -> VM not found");
            return;
        }

        await Memu.Push(_index, local, remote);

        Log.Write($"[{_index}] -> files pushed");
    }

    /// <summary>
    /// Загрузка с удаленки
    /// </summary>
    /// <param name="local">путь на локальной машине</param>
    /// <param name="remote">путь на удаленной машине</param>
    public async Task Pull(string local, string remote)
    {
        if (!await Memu.Exists(_index))
        {
            Log.Write($"[{_index}] -> VM not found");
            return;
        }

        await Memu.Pull(_index, local, remote);

        Log.Write($"[{_index}] -> files pulled");
    }
    
    /// <summary>
    /// Выполнение команды в консоли андроида
    /// </summary>
    /// <param name="cmd">команда (без adb shell)</param>
    public async Task<string> Shell(string cmd)
    {
        if (!await Memu.Exists(_index))
        {
            Log.Write($"[{_index}] -> VM not found");
            return string.Empty;
        }

        var result = await MemuCmd.ExecMemuc($"-i {_index} adb shell {cmd}");

        Log.Write($"[{_index}] -> shell be called");

        return result;
    }

    /// <summary>
    /// Получение разрешения экрана
    /// </summary>
    /// <returns>Разрешение экрана</returns>
    public async Task<Point> GetResoultion()
    {
        if (!await Memu.Exists(_index))
        {
            Log.Write($"[{_index}] -> VM not found");
            return Point.Empty;
        }

        var x = int.Parse((await MemuCmd.ExecMemuc($"-i {_index} getconfigex resolution_width")).Split(' ')[1]);
        var y = int.Parse((await MemuCmd.ExecMemuc($"-i {_index} getconfigex resolution_height")).Split(' ')[1]);

        Log.Write($"[{_index}] -> requested resolution VM");

        return new Point(x, y);
    }

    /// <summary>
    /// Изменить информацию об устройстве (устройство должно быть активно и после применения перезагруженно)
    /// </summary>
    /// <param name="countryCode">код страны (допустим Россия +7)</param>
    /// <param name="genNewHardware">сгенирировать новое оборудование или воспользоваться ранее сгенерированным?</param>
    public async Task Spoof(int countryCode, bool genNewHardware = false)
    {
        var random = new Random();
        var microvirt = await Globals.MicrovirtInfoGet();
        var mcc = await Globals.MccMncGet(countryCode.ToString());

        if (_deviceInfo is null || genNewHardware)
            _deviceInfo = new DeviceInfoGenerated()
            {
                Latitude = random.Next(0,100).ToString(),
                Longitude = random.Next(0,100).ToString(),
                Mac = Globals.GetRandomMacAddress(),
                Ssid = Globals.GetRandomMacAddress(),
                Imei = Globals.GeneratorImei(microvirt.Tac),
                Imsi = Globals.GeneratoImsi(mcc.Mnc, mcc.Mcc),
                MccMnc = mcc,
                ManualDiskSize = random.Next(16, 56).ToString(),
                MicrovirtInfo = microvirt,
                Simserial = Globals.GetIccid(mcc.Mnc),
                Resolution = await Globals.GetResolution(),
                TimeZone = await Globals.GetTimeZone(),
                AndroidRelease = await Globals.GetAndroidRelease(),
                SerialNo = random.Next(10_000_000, 100_000_000).ToString(),
                BoardPlatform = Globals.RandomString(random.Next(5,10)),
                GoogleFrameworkId = Globals.RandomHexString(16),
                Language = await Globals.GetLanguage(),
                AndroidId = Globals.RandomHexString(16),
                ZenModeConfigEtag = random.Next(-100_000, 1_000_000).ToString(),
                BootCount = random.Next(0, 1_000).ToString(),
                PBootCount = random.Next(0, 50).ToString()
            };

        await Memu.Spoof(_index, _deviceInfo ?? new DeviceInfoGenerated());
        Log.Write($"[{_index}] -> VM spoofed, do not forget reload machine");
    }
}