namespace WABot.WhatsApp.Web;

public class AccPreparation
{
    private readonly Dictionary<int, Device[]> _tetheredDevices;
    private readonly List<string> _usedPhones;
    private readonly List<string> _usedPhonesUsers;
    private readonly FileInfo _logFile;

    private string[] _names;
    private int _removedAccounts;
    private int _alivesAccounts;

    public AccPreparation()
    {
        _tetheredDevices = new Dictionary<int, Device[]>();
        _usedPhonesUsers = _usedPhones = new List<string>();
        _names = new[] { "" };

        _removedAccounts = _alivesAccounts = 0;

        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_prep_log.txt");
    }

    public async Task Start(string message)
    {
        var tasks = new List<Task>();
        var rnd = new Random();

        _names = (await File.ReadAllLinesAsync(Globals.Setup.PathToUserNames))
            .Where(name => new Regex("^[a-zA-Z0-9. -_?]*$").IsMatch(name)).ToArray();

        var busyDevices = new List<int>();

        await Globals.InitAccountsFolder();

        Log.Write($"Добро пожаловать в логи подготовки аккаунтов, сегодняшний текст:\n{message}\n\n", _logFile.FullName);

        while (true)
        {
            var devices = Globals.Devices.Where(device => !busyDevices.Contains(device.Index) && device.IsActive)
                .Take(2).ToArray();

            if (devices.Length != 2)
                break;

            var id = rnd.Next(0, 10_000);

            foreach (var device in devices)
            {
                device.InUsage = true;

                await device.Client.Start();
            }

            _tetheredDevices[id] = new[] { devices[0], devices[1] };

            var task = Handler(id, message.Split('\n'));
            await Task.Delay(1_000);

            tasks.Add(task);

            busyDevices.AddRange(new[] { devices[0].Index, devices[1].Index });
        }

        Task.WaitAll(tasks.ToArray(), -1);

        foreach (var device in Globals.Devices)
            device.InUsage = false;

        Log.Write($"Завершено\n", _logFile.FullName);
        Log.Write($"Проебов: {_removedAccounts}\n", _logFile.FullName);
        Log.Write($"Живых: {_alivesAccounts}\n", _logFile.FullName);

        busyDevices.Clear();
        Stop();
    }

    public void Stop()
    {
        _tetheredDevices.Clear();
        _usedPhones.Clear();
        _usedPhonesUsers.Clear();
    }

    private async Task Handler(int idThread, string[] messages)
    {
        var (c1, c2) = (_tetheredDevices[idThread][0].Client, _tetheredDevices[idThread][1].Client);
        var (c1Index, c2Index) = (_tetheredDevices[idThread][0].Index, _tetheredDevices[idThread][1].Index);

        var c1Auth = false;
        var c2Auth = false;

        Log.Write($"Поток {idThread} запущен\n", _logFile.FullName);

        while (Globals.Devices.Where(device => device.Index == c1Index).ToArray()[0].IsActive &&
               Globals.Devices.Where(device => device.Index == c2Index).ToArray()[0].IsActive)
        {
            var result = await Globals.GetAccounts(_usedPhones.ToArray(), Globals.Setup.TrustLevelAccount);

            if (result.Length == 0)
                break;

            var (phone, path) = result[0];

            if (_usedPhones.Contains(phone))
                continue;

            _usedPhones.Add(phone);

            if (!c1Auth)
            {
                c1Auth = await TryLogin(c1, phone, path);

                if (!c1Auth)
                    _removedAccounts++;

                Log.Write($"[{phone}] - {(c1Auth ? "смогли войти" : "не смогли войти")}\n", _logFile.FullName);
                continue;
            }

            if (!c2Auth)
            {
                c2Auth = await TryLogin(c2, phone, path);
                Log.Write($"[{phone}] - {(c2Auth ? "смогли войти" : "не смогли войти")}\n", _logFile.FullName);

                if (!c2Auth)
                {
                    _removedAccounts++;
                    continue;
                }
            }

            await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{idThread}_contacts.vcf", ContactManager.Export(
                new List<CObj>()
                {
                        new(MemuLib.Globals.RandomString(new Random().Next(5, 15)), c1.Phone),
                        new(MemuLib.Globals.RandomString(new Random().Next(5, 15)), c2.Phone)
                }
            ));

            await c1.ImportContacts($@"{Globals.TempDirectory.FullName}\{idThread}_contacts.vcf");

            await c2.ImportContacts($@"{Globals.TempDirectory.FullName}\{idThread}_contacts.vcf");

            File.Delete($@"{Globals.TempDirectory.FullName}\{idThread}_contacts.vcf");

            var countMessages = new Random().Next(5, 10);

            var rnd = new Random();

            for (var i = 0; i < countMessages; i++)
            {
                if (!await IsValid(c1))
                {
                    c1Auth = false;
                    _removedAccounts++;
                    break;
                }

                if (!await IsValid(c2))
                {
                    c2Auth = false;
                    _removedAccounts++;
                    break;
                }

                if (i == 0)
                {
                    if (!await c1.SendMessage(c2.Phone, messages[rnd.Next(0, messages.Length - 1)]))
                    {
                        i = -1;
                        continue;
                    }

                    await Task.Delay(500);

                    if (!await c2.SendMessage(c1.Phone, messages[rnd.Next(0, messages.Length - 1)]))
                    {
                        i = -1;
                        continue;
                    }
                }
                else
                {
                    var mc1 = rnd.Next(2, 4);
                    var mc2 = rnd.Next(2, 4);

                    for (var mcc = 0; mcc < mc1; mcc++)
                        await c1.SendMessage(c2.Phone, messages[rnd.Next(0, messages.Length - 1)]);

                    await Task.Delay(500);

                    for (var mcc = 0; mcc < mc2; mcc++)
                        await c2.SendMessage(c1.Phone, messages[rnd.Next(0, messages.Length - 1)]);

                }
            }

            await c1.GetInstance().StopApk(c1.PackageName);
            await c1.GetInstance().RunApk(c1.PackageName);

            await c2.GetInstance().StopApk(c2.PackageName);
            await c2.GetInstance().RunApk(c2.PackageName);

            if (!c1Auth || !c2Auth)
                continue;

            if (!await TryLoginWeb(c2, c2.Phone.Remove(0, 1)))
            {
                c2Auth = false;
                _removedAccounts++;
                continue;
            }

            await TryLoginWeb(c1, c1.Phone.Remove(0, 1));

            _alivesAccounts += 2;

            c1Auth = c2Auth = false;
        }

        async Task<bool> TryLogin(WaClient client, string phone, string path)
        {
            await client.ReCreate($"+{phone}", path);
            await client.LoginFile(name: _names[new Random().Next(0, _names.Length)]);

            return await IsValidCheck(client);
        }

        async Task<bool> IsValidCheck(WaClient client)
        {
            if (!await IsValid(client))
            {
                if (Directory.Exists(@$"{Globals.RemoveAccountsDirectory.FullName}\{client.Phone.Remove(0, 1)}") &&
                    Directory.Exists(client.Account))
                    Directory.Delete(client.Account, true);
                else if (Directory.Exists(client.Account))
                    Directory.Move(client.Account,
                        @$"{Globals.RemoveAccountsDirectory.FullName}\{client.Phone.Remove(0, 1)}");

                return false;
            }

            return true;
        }

        async Task<bool> TryLoginWeb(WaClient client, string phone)
        {
            if (await client.GetInstance().ExistsElement("//node[@text='НЕ СЕЙЧАС']", false))
            {
                await client.GetInstance().Click("//node[@text='НЕ СЕЙЧАС']");
                await Task.Delay(500);
            }
            
            await client.GetInstance().Click("//node[@content-desc='Ещё']");
            await client.GetInstance().Click("//node[@text='Связанные устройства']");

            if (await client.GetInstance().ExistsElement("//node[@resource-id='android:id/button1']"))
                await client.GetInstance().Click("//node[@resource-id='android:id/button1']");

            var wClient = new WAWClient(phone);

            await wClient.WaitQueue();

            await client.GetInstance().Click("//node[@text='ПРИВЯЗКА УСТРОЙСТВА']");

            if (await client.GetInstance().ExistsElement("//node[@text='OK']"))
                await client.GetInstance().Click("//node[@text='OK']");

            int i = 0;
        initAgain:
            var initWithErrors = false;

            if (!await IsValidCheck(client) || i > 3)
            {
                wClient.RemoveQueue();
                return false;
            }

            try
            {
                await wClient.Init(true);
            }
            catch
            {
                initWithErrors = true;
            }

            await wClient.Free();

            if (await client.GetInstance().ExistsElement("//node[@text='ПОДТВЕРДИТЬ']", false))
                return false;

            if (await client.GetInstance().ExistsElement("//node[@text='OK']", false))
            {
                await client.GetInstance().Click("//node[@text='OK']");
                i++;

                if (await client.GetInstance().ExistsElement("//node[@text='ПРИВЯЗКА УСТРОЙСТВА']"))
                {
                    while (!string.IsNullOrEmpty(Globals.QrCodeName))
                        await Task.Delay(100);

                    await Task.Delay(1_000);
                    await client.GetInstance().Click("//node[@text='ПРИВЯЗКА УСТРОЙСТВА']");
                }

                goto initAgain;
            }

            if (initWithErrors)
            {
                i++;
                await Task.Delay(1_500);
                goto initAgain;
            }

            wClient.RemoveQueue();

            if (Directory.Exists(@$"{Globals.RemoveAccountsDirectory.FullName}\{client.Phone.Remove(0, 1)}") && Directory.Exists(client.Account))
                Directory.Delete(client.Account, true);
            else if (Directory.Exists(client.Account))
                Directory.Move(client.Account,
                    @$"{Globals.RemoveAccountsDirectory.FullName}\{client.Phone.Remove(0, 1)}");

            Log.Write($"[{phone}] - Пара пошла со счетом проебов {_removedAccounts} и живых {_alivesAccounts}\n", _logFile.FullName);
            return true;
        }

        async Task<bool> IsValid(WaClient client)
        {
            await Task.Delay(500);
            return !await client.GetInstance().ExistsElement("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']", false) && //To-Do
                   !await client.GetInstance().ExistsElement("//node[@text='ДАЛЕЕ']", false) && //To-Do
                   !await client.GetInstance().ExistsElement("//node[@text='ЗАПРОСИТЬ РАССМОТРЕНИЕ']", false) &&
                   !await client.GetInstance().ExistsElement("//node[@text='ПОДТВЕРДИТЬ']", false);
            //!await client.GetInstance().ExistsElement("//node[@resource-id='android:id/message']", false);
        }
    }
}