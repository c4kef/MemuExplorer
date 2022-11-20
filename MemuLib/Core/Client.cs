using System.Xml;

namespace MemuLib.Core;

public class Client
{
    /// <summary>
    /// Индекс машины
    /// </summary>
    public readonly int Index;

    /// <summary>
    /// Локальное объявление информации о железе
    /// </summary>
    private DeviceInfoGenerated? _deviceInfo;

    /// <summary>
    /// Объявление образа машины
    /// </summary>
    /// <param name="index">индекс машины</param>
    public Client(int index) => Index = index;

    /// <summary>
    /// Запуск машины
    /// </summary>
    public async Task Start()
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return;
        }

        await Memu.Start(Index);

        Log.Write($"[{Index}] -> VM started");
    }

    /// <summary>
    /// Остановка машины
    /// </summary>
    public async Task Stop()
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return;
        }

        await Memu.Stop(Index);

        Log.Write($"[{Index}] -> VM stoped");
    }

    /// <summary>
    /// Установка приложения на машину
    /// </summary>
    /// <param name="path">путь до приложения на локальном компе</param>
    public async Task InstallApk(string path)
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return;
        }

        if (!File.Exists(path))
        {
            Log.Write($"[{Index}] -> apk file not found");
            return;
        }

        await Memu.InstallApk(Index, path);

        Log.Write($"[{Index}] -> installed apk");
    }

    /// <summary>
    /// Импорт контактов
    /// </summary>
    /// <param name="path">путь до контакта</param>
    public async Task ImportContacts(string path)
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return;
        }

        if (!File.Exists(path))
        {
            Log.Write($"[{Index}] -> contact file not found");
            return;
        }

        await ContactManager.Import(Index, path);

        Log.Write($"[{Index}] -> contacts imported");
    }

    /// <summary>
    /// Очистка контактов
    /// </summary>
    public async Task ClearContacts()
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return;
        }

        var result = await MemuCmd.ExecMemuc($@"-i {Index} execcmd pm clear com.android.providers.contacts");

        Log.Write($"[{Index}] -> contacts cleaned with results: {result}");
    }

    /// <summary>
    /// Запуск приложения на машине
    /// </summary>
    /// <param name="comPath">com-путь до установленного приложения на машине</param>
    public async Task RunApk(string comPath)
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return;
        }

        await Task.Delay(Settings.WaitingSecs);
        await Memu.StartApk(Index, comPath);

        Log.Write($"[{Index}] -> apk runned");
    }

    /// <summary>
    /// Остановка приложения на машине
    /// </summary>
    /// <param name="comPath">com-путь до установленного приложения на машине</param>
    public async Task StopApk(string comPath)
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return;
        }

        await Task.Delay(Settings.WaitingSecs);
        await Memu.StopApk(Index, comPath);

        Log.Write($"[{Index}] -> apk stopped");
    }

    /// <summary>
    /// Симуляция кликов по экрану
    /// </summary>
    /// <param name="x">по горизонтали</param>
    /// <param name="y">по вертикали</param>
    public async Task Click(int x, int y)
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return;
        }

        await MemuCmd.ExecMemuc($"-i {Index} adb shell input tap {x} {y}");

        Log.Write($"[{Index}] -> input tap {x} {y}");
    }

    /// <summary>
    /// Проверка элемента на существование
    /// </summary>
    /// <param name="uiElement">название элемента в интерфейсе</param>
    public async Task<bool> ExistsElement(string uiElement, string? dump = null, bool isWait = true)
    {
        try
        {
            if (!await Memu.Exists(Index))
            {
                Log.Write($"[{Index}] -> VM not found");
                return false;
            }

            if (isWait)
                await Task.Delay(Settings.WaitingSecs);

            var (x, y) = await FindElement(uiElement, dump ?? await DumpScreen());

            return x != -1 && y != -1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Проверка элементов на существование
    /// </summary>
    /// <param name="uiElement">название элементов в интерфейсе</param>
    public async Task<bool> ExistsElements(string[] uiElements, string? dump = null, bool isWait = true)
    {
        try
        {
            if (!await Memu.Exists(Index))
            {
                Log.Write($"[{Index}] -> VM not found");
                return false;
            }

            if (isWait)
                await Task.Delay(Settings.WaitingSecs);

            foreach (var uiElement in uiElements)
            {
                var (x, y) = await FindElement(uiElement, dump ?? await DumpScreen());

                if (x != -1 && y != -1)
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> DumpScreen()
    {
        var result = await ShellCmd("uiautomator dump");
        var document = await ShellCmd("cat /storage/emulated/0/window_dump.xml");
        if (document != "" && !result.Contains("ERROR"))
            return document;

        return string.Empty;
    }

    public async Task<(int x, int y)> FindElement(string xpath, string? xdocument)
    {
        var document = xdocument ?? await DumpScreen();

        if (document.Contains(xpath))
        {
            string Cord = document.Split(xpath)[1].Split("bounds=\"")[1].Split('\"')[0].Replace("[", "");

            return ((Convert.ToInt32(Cord.Split(']')[0].Split(',')[0]) + Convert.ToInt32(Cord.Split(']')[1].Split(',')[0])) / 2, (Convert.ToInt32(Cord.Split(']')[0].Split(',')[1]) + Convert.ToInt32(Cord.Split(']')[1].Split(',')[1])) / 2);
        }

        return (-1, -1);
    }

    /// <summary>
    /// Симуляция кликов по экрану
    /// </summary>
    /// <param name="uiElement">название элемента в интерфейсе</param>
    public async Task Click(string uiElement, string? dump = null)
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return;
        }

        var (x, y) = await FindElement(uiElement, dump ?? await DumpScreen());
        
        if (x == -1 && y == -1)
            throw new Exception($"[{Index}] Can't found element by name \"{uiElement}\"");

        await Click(x, y);

        Log.Write($"[{Index}] -> input tap uiElement");
    }

    /// <summary>
    /// Симуляция ввода текста
    /// </summary>
    /// <param name="uiElement">название элемента в интерфейсе</param>
    /// <param name="text">текст передаваемый на интерфейс</param>
    public async Task Input(string uiElement, string text, string? dump = null, bool clickToFieldInput = true)
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return;
        }

        await Task.Delay(Settings.WaitingSecs);

        if (clickToFieldInput)
        {
            var (x, y) = await FindElement(uiElement, dump ?? await DumpScreen());

            if (x == -1 && y == -1)
                throw new Exception($"[{Index}] Can't found element by name \"{uiElement}\"");

            await Click(x, y);
        }

        await Input(text);

        Log.Write($"[{Index}] -> input text uiElement");
    }
    
    /// <summary>
    /// Симуляция ввода текста
    /// </summary>
    /// <param name="text">текст передаваемый на интерфейс</param>
    public async Task Input(string text)
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return;
        }
        
        await Task.Delay(Settings.WaitingSecs);
        
        await Memu.SendText(Index, text);

        Log.Write($"[{Index}] -> input text uiElement");
    }

    /// <summary>
    /// Отправка на удаленку
    /// </summary>
    /// <param name="local">путь на локальной машине</param>
    /// <param name="remote">путь на удаленной машине</param>
    public async Task Push(string local, string remote)
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return;
        }

        await Memu.Push(Index, local, remote);

        Log.Write($"[{Index}] -> files pushed");
    }

    /// <summary>
    /// Загрузка с удаленки
    /// </summary>
    /// <param name="local">путь на локальной машине</param>
    /// <param name="remote">путь на удаленной машине</param>
    public async Task<string> Pull(string local, string remote)
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return "";
        }

        Log.Write($"[{Index}] -> files pulled");
        return await Memu.Pull(Index, local, remote);
    }

    /// <summary>
    /// Выполнение команды в консоли андроида
    /// </summary>
    /// <param name="cmd">команда (без adb shell)</param>
    public async Task<string> Shell(string cmd)
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return string.Empty;
        }

        var result = await MemuCmd.ExecMemuc($"-i {Index} adb shell {cmd}");

        Log.Write($"[{Index}] -> shell be called");

        return result;
    }

    /// <summary>
    /// Выполнение команды execcmd
    /// </summary>
    /// <param name="cmd">команда</param>
    public async Task<string> ShellCmd(string cmd)
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return string.Empty;
        }

        var result = await MemuCmd.ExecMemuc($"-i {Index} execcmd {cmd}");

        Log.Write($"[{Index}] -> shellCmd be called");

        return result;
    }

    /// <summary>
    /// Получение разрешения экрана
    /// </summary>
    /// <returns>Разрешение экрана</returns>
    public async Task<Point> GetResoultion()
    {
        if (!await Memu.Exists(Index))
        {
            Log.Write($"[{Index}] -> VM not found");
            return Point.Empty;
        }

        var x = int.Parse((await MemuCmd.ExecMemuc($"-i {Index} getconfigex resolution_width")).Split(' ')[1]);
        var y = int.Parse((await MemuCmd.ExecMemuc($"-i {Index} getconfigex resolution_height")).Split(' ')[1]);

        Log.Write($"[{Index}] -> requested resolution VM");

        return new Point(x, y);
    }

    /// <summary>
    /// Изменить информацию об устройстве (устройство должно быть активно и после применения перезагруженно)
    /// </summary>
    /// <param name="countryCode">код страны (допустим Россия +7)</param>
    /// <param name="genNewHardware">сгенирировать новое оборудование или воспользоваться ранее сгенерированным?</param>
    public async Task Spoof(string countryCode, bool genNewHardware = false)
    {
        var random = new Random();
        var microvirt = await Globals.MicrovirtInfoGet();
        var mcc = await Globals.MccMncGet(countryCode);

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

        await Memu.Spoof(Index, _deviceInfo ?? new DeviceInfoGenerated());
        Log.Write($"[{Index}] -> VM spoofed, do not forget reload machine");
    }
}