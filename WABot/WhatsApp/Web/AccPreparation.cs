using MemuLib.Core;
using WABot.Pages;

namespace WABot.WhatsApp.Web;

public class AccPreparation
{
    private readonly Dictionary<int, Device[]> _tetheredDevices;
    private readonly List<string> _usedPhones;
    private readonly List<string> _usedPhonesUsers;
  
    private FileInfo _logFile;
    private string[] _names;
    private int _removedAccounts;
    private int _alivesAccounts;
    public bool IsStop;

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

        IsStop = false;
        _removedAccounts = _alivesAccounts = 0;
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_prep_log.txt");

        _names = (await File.ReadAllLinesAsync(Globals.Setup.PathToUserNames))
            .Where(name => new Regex("^[a-zA-Z0-9. -_?]*$").IsMatch(name)).ToArray();

        var busyDevices = new List<int>();

        await Globals.InitAccountsFolder();

        Log.Write($"Добро пожаловать в логи подготовки аккаунтов\n", _logFile.FullName);
        Dashboard.GetInstance().CountTasks = (await Globals.GetAccounts(_usedPhones.ToArray(), true)).Length;

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

            await Task.Delay(2_000);

            tasks.Add(task);

            busyDevices.AddRange(new[] { devices[0].Index, devices[1].Index });
        }

        Task.WaitAll(tasks.ToArray(), -1);

        foreach (var device in Globals.Devices.ToArray())
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

        await c1.GetInstance().RunApk("net.sourceforge.opencamera");
        await c2.GetInstance().RunApk("net.sourceforge.opencamera");
        await c1.GetInstance().StopApk("net.sourceforge.opencamera");
        await c2.GetInstance().StopApk("net.sourceforge.opencamera");

        var c1Auth = false;
        var c2Auth = false;

        Log.Write($"Поток {idThread} запущен\n", _logFile.FullName);

        while (Globals.Devices.ToArray().Where(device => device.Index == c1Index).ToArray()[0].IsActive &&
               Globals.Devices.ToArray().Where(device => device.Index == c2Index).ToArray()[0].IsActive && !IsStop)
        {
            var result = await Globals.GetAccounts(_usedPhones.ToArray(), true);

            Dashboard.GetInstance().CountTasks = result.Length;

            if (result.Length < 2)
            {
                Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                break;
            }

            var (phone, path) = result[0];

            if (_usedPhones.Contains(phone))
                continue;

            _usedPhones.Add(phone);

            if (!c1Auth)
            {
                c1Auth = await TryLogin(c1, phone, path);

                if (!c1Auth)
                    Dashboard.GetInstance().BannedAccounts = ++_removedAccounts;

                Log.Write($"[{phone}] - {(c1Auth ? "смогли войти" : "не смогли войти")}\n", _logFile.FullName);
                continue;
            }

            if (!c2Auth)
            {
                c2Auth = await TryLogin(c2, phone, path);
                Log.Write($"[{phone}] - {(c2Auth ? "смогли войти" : "не смогли войти")}\n", _logFile.FullName);

                if (!c2Auth)
                {
                    Dashboard.GetInstance().BannedAccounts = ++_removedAccounts;
                    continue;
                }
            }

            if (Globals.Setup.EnableCheckBan)
            {
                await SuccesfulMoveAccount(c1);
                await SuccesfulMoveAccount(c2);
                Log.Write($"[Handler] - Аккаунты перемещены\n", _logFile.FullName);
                Dashboard.GetInstance().CompletedTasks = _alivesAccounts += 2;

                c1Auth = c2Auth = false;

                continue;
            }

            if (Globals.Setup.EnableMinWarm)
            {
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
                    if (!c1Auth || !c2Auth)
                        break;

                    if (!await IsValid(c1))
                    {
                        c1Auth = false;
                        Dashboard.GetInstance().BannedAccounts = ++_removedAccounts;
                        break;
                    }

                    if (!await IsValid(c2))
                    {
                        c2Auth = false;
                        Dashboard.GetInstance().BannedAccounts = ++_removedAccounts;
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
                        {
                            await c1.SendMessage(c2.Phone, messages[rnd.Next(0, messages.Length - 1)]);

                            if (!await IsValid(c1))
                            {
                                c1Auth = false;
                                Dashboard.GetInstance().BannedAccounts = ++_removedAccounts;
                                break;
                            }
                        }

                        await Task.Delay(500);

                        for (var mcc = 0; mcc < mc2; mcc++)
                        {
                            await c2.SendMessage(c1.Phone, messages[rnd.Next(0, messages.Length - 1)]);

                            if (!await IsValid(c2))
                            {
                                c2Auth = false;
                                Dashboard.GetInstance().BannedAccounts = ++_removedAccounts;
                                break;
                            }
                        }
                    }
                }
            }

            c1.AccountData.FirstMsg = true;
            c2.AccountData.FirstMsg = false;
            ++c1.AccountData.TrustLevelAccount;
            ++c2.AccountData.TrustLevelAccount;

            await c1.GetInstance().StopApk(c1.PackageName);
            await c1.GetInstance().RunApk(c1.PackageName);

            await c2.GetInstance().StopApk(c2.PackageName);
            await c2.GetInstance().RunApk(c2.PackageName);

            if (!c1Auth || !c2Auth)
                continue;

            if (Globals.Setup.EnableScanQr)
            {
                try
                {
                    if (!await TryLoginWeb(c2, c2.Phone.Remove(0, 1)))
                    {
                        c2Auth = false;
                        Dashboard.GetInstance().BannedAccounts = ++_removedAccounts;
                        continue;
                    }
                    Dashboard.GetInstance().CompletedTasks = ++_alivesAccounts;

                    if (await TryLoginWeb(c1, c1.Phone.Remove(0, 1)))
                        Dashboard.GetInstance().CompletedTasks = ++_alivesAccounts;
                }
                catch (Exception ex)
                {
                    Log.Write($"[Handler] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
                }
            }
            else
            {
                await SuccesfulMoveAccount(c1);
                await SuccesfulMoveAccount(c2);
                Log.Write($"[Handler] - Аккаунты перемещены\n", _logFile.FullName);
            }

            c1Auth = c2Auth = false;
        }

        async Task SuccesfulMoveAccount(WaClient client)
        {
            await client.UpdateData();
            if (Directory.Exists(@$"{Globals.ScannedAccountsDirectory.FullName}\{client.Phone.Remove(0, 1)}") && Directory.Exists(client.Account))
                Directory.Delete(client.Account, true);
            else if (Directory.Exists(client.Account))
                Directory.Move(client.Account,
                    @$"{Globals.ScannedAccountsDirectory.FullName}\{client.Phone.Remove(0, 1)}");
        }

        async Task<bool> TryLogin(WaClient client, string phone, string path)
        {
            try
            {
                await client.ReCreate($"+{phone}", path);
                await client.LoginFile(name: _names[new Random().Next(0, _names.Length)]);

                return await IsValidCheck(client);
            }
            catch (Exception ex)
            {
                Log.Write($"[TryLogin] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
                return false;
            }
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
            await MoveToScan(client, true);
            int i = 0;
            try
            {
            initAgain:
                var initWithErrors = false;

                if (Directory.GetFiles(client.Account).Any(_phone => _phone.Contains(phone)) && i > 0)
                {
                    client.Web!.RemoveQueue();
                    Log.Write($"[{phone}] - Аккаунт уже был авторизован и мы положительно отвечаем на результат\n", _logFile.FullName);
                    return true;
                }

                if (!await IsValidCheck(client) || i > 4)
                {
                    client.Web!.RemoveQueue();
                    Log.Write($"[{phone}] - Аккаунт оказался не валидным\n", _logFile.FullName);
                    return false;
                }

                initWithErrors = true;

                if (await client.GetInstance().ExistsElement("//node[@text='ПРИВЯЗКА УСТРОЙСТВА']"))
                    await client.GetInstance().Click("//node[@text='ПРИВЯЗКА УСТРОЙСТВА']");

                try
                {
                    await client.Web!.Init(true, client.Account);

                    initWithErrors = false;
                }
                catch (Exception ex)
                {
                    Log.Write($"[{phone}] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
                }

                await client.Web!.Free();

                if (await client.GetInstance().ExistsElement("//node[@text='ПОДТВЕРДИТЬ']", isWait: false))
                    return false;

                if (await client.GetInstance().ExistsElement("//node[@text='OK']", isWait: false))
                {
                    //await client.GetInstance().Click("//node[@text='OK']");
                    i++;

                    if (await client.GetInstance().ExistsElement("//node[@text='ПРИВЯЗКА УСТРОЙСТВА']"))
                    {
                        await SetZero(client.Web);
                        //await Task.Delay(1_000);
                        //await client.GetInstance().Click("//node[@text='ПРИВЯЗКА УСТРОЙСТВА']");
                    }

                    await client.GetInstance().StopApk(c1.PackageName);
                    await client.GetInstance().RunApk(c1.PackageName);
                    await MoveToScan(client, false);

                    goto initAgain;
                }

                if (initWithErrors)
                {
                    i++;
                    await SetZero(client.Web);
                    Log.Write($"[{phone}] - Инициализировалось с ошибками\n", _logFile.FullName);

                    await client.GetInstance().StopApk(c1.PackageName);
                    await client.GetInstance().RunApk(c1.PackageName);
                    await MoveToScan(client, false);
                    
                    goto initAgain;
                }

                client.Web.RemoveQueue();

                await SuccesfulMoveAccount(client);

                Log.Write($"[{phone}] - Пара пошла со счетом проебов {_removedAccounts} и живых {_alivesAccounts}\n", _logFile.FullName);
                return true;
            }
            catch (Exception ex)
            {
                Log.Write($"[main] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
                await SetZero(client.Web);
                client.Web.RemoveQueue();
                await client.Web.Free();
                return false;
            }

            async Task SetZero(WAWClient wClient)
            {
                Globals.QrCodeName = string.Empty;

                while (!TryDeleteQR(wClient.TaskId))
                    await Task.Delay(500);
            }
        }

        bool TryDeleteQR(int taskId)
        {
            try
            {
                if (File.Exists(@$"{Globals.Setup.PathToQRs}\{taskId}.png"))
                    File.Delete(@$"{Globals.Setup.PathToQRs}\{taskId}.png");

                return true;
            }
            catch
            {
                return false;
            }
        }

        async Task MoveToScan(WaClient client, bool isWaitQueue)
        {
            if (await client.GetInstance().ExistsElement("//node[@text='НЕ СЕЙЧАС']", isWait: false))
            {
                await client.GetInstance().Click("//node[@text='НЕ СЕЙЧАС']");
                await Task.Delay(500);
            }

            if (await client.GetInstance().ExistsElement("//node[@text='Выберите частоту резервного копирования']"))
            {
                await client.GetInstance().Click("//node[@text='Выберите частоту резервного копирования']");
                await client.GetInstance().Click("//node[@text='Никогда']");
                await client.GetInstance().Click("//node[@text='ГОТОВО']");
                await Task.Delay(2_000);
                await client.GetInstance().StopApk(client.PackageName);
                await client.GetInstance().RunApk(client.PackageName);
            }

            await client.GetInstance().Click("//node[@content-desc='Ещё']");
            await client.GetInstance().Click("//node[@text='Связанные устройства']");

            if (await client.GetInstance().ExistsElement("//node[@resource-id='android:id/button1']"))
                await client.GetInstance().Click("//node[@resource-id='android:id/button1']");

            if (isWaitQueue)
            {
                client.Web.AddToQueue();
                await client.Web.WaitQueue();
            }
            await client.GetInstance().Click("//node[@text='ПРИВЯЗКА УСТРОЙСТВА']");

            if (await client.GetInstance().ExistsElement("//node[@text='OK']"))
                await client.GetInstance().Click("//node[@text='OK']");
        }

        async Task<bool> IsValid(WaClient client)
        {
            await Task.Delay(MemuLib.Settings.WaitingSecs);

            if (await client.GetInstance().ExistsElement("//node[@text='Перезапустить приложение']"))
            {
                await client.GetInstance().Click("//node[@text='Перезапустить приложение']");
                await client.GetInstance().StopApk(client.PackageName);
                await client.GetInstance().RunApk(client.PackageName);
            }

            if (await client.GetInstance().ExistsElement("//node[@text='ОК']"))
                await client.GetInstance().Click("//node[@text='ОК']");

            if (await client.GetInstance().ExistsElement("//node[@text='OK']"))
                await client.GetInstance().Click("//node[@text='OK']");

            if (await client.GetInstance().ExistsElement("//node[@text='ПРОПУСТИТЬ']"))
                await client.GetInstance().Click("//node[@text='ПРОПУСТИТЬ']");

            if (await client.GetInstance().ExistsElement("//node[@text='Закрыть приложение']"))
            {
                await client.GetInstance().Click("//node[@text='Закрыть приложение']");
                await client.GetInstance().StopApk(client.PackageName);
                await client.GetInstance().RunApk(client.PackageName);
            }

            await Task.Delay(MemuLib.Settings.WaitingSecs);

            var dump = client.GetInstance().DumpScreen();

            return !await client.GetInstance().ExistsElement("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']", dump, false) &&
                   !await client.GetInstance().ExistsElement("//node[@text='ДАЛЕЕ']", dump, false) &&
                   !await client.GetInstance().ExistsElement("//node[@text='Перезапустить приложение']", dump, false) &&
                   !await client.GetInstance().ExistsElement("//node[@text='Закрыть приложение']", dump, false) &&
                   !await client.GetInstance().ExistsElement("//node[@content-desc='Неверный номер?']", dump, false) &&
                   !await client.GetInstance().ExistsElement("//node[@text='ЗАПРОСИТЬ РАССМОТРЕНИЕ']", dump, false) &&
                   !await client.GetInstance().ExistsElement("//node[@text='WA Business']", dump, false) &&
                   !await client.GetInstance().ExistsElement("//node[@text='WhatsApp']", dump, false) &&
                   !await client.GetInstance().ExistsElement("//node[@resource-id='android:id/progress']", dump, false) &&
                   !await client.GetInstance().ExistsElement("//node[@text='ПОДТВЕРДИТЬ']", dump, false);
        }
    }
}