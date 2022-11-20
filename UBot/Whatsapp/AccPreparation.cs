using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UBot.Views.User;
using MemuLib.Core;
using System.Text.RegularExpressions;
using MemuLib.Core.Contacts;
using UBot.Whatsapp.Web;
using System.Runtime.CompilerServices;
using UBot.Controls;
using System.Diagnostics.Metrics;

namespace UBot.Whatsapp;

public class AccPreparation
{
    public AccPreparation()
    {
        _usedPhones = new List<string>();
        _activePhones = new List<string>();
        _names = new[] { "" };
        _lock = new();
    }

    private readonly List<string> _usedPhones;
    private readonly List<string> _activePhones;
    private readonly object _lock;

    private bool _accountsNotFound;
    private FileInfo _logFile;
    private string[] _names;
    private ActionProfileWork _currentProfile;

    public bool IsStop;

    public async Task Run(string message, ActionProfileWork actionProfileWork)
    {
        IsStop = false;
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_prep.txt");
        _logFile.Create().Close();

        await Globals.InitAccountsFolder();

        _names = (await File.ReadAllLinesAsync(Globals.Setup.PathToFileNames)).Where(name => new Regex("^[a-zA-Z0-9. -_?]*$").IsMatch(name)).ToArray();
        _currentProfile = actionProfileWork;
        var mainTasks = new List<Task>();
        var busyDevices = new Dictionary<DataEmulator, int>();

        for (var repeatId = 0; repeatId < Globals.Setup.RepeatCounts; repeatId++)
        {
            for (var groupId = 0; groupId < ((_currentProfile.CheckBan) ? 1 : Globals.Setup.CountGroups); groupId++)
            {
                var id = groupId;
                await Task.Delay(100);
                mainTasks.Add(Task.Run(async () =>
                {
                    var tasks = new List<Task>();

                    while (!IsStop)
                    {
                        DataEmulator[] devices = null;

                        lock (_lock)
                        {
                            devices = ManagerView.GetInstance().Emulators.Where(device => !busyDevices.Select(device => device.Key.Index).Contains(device.Index) && device.IsEnabled)
                               .Take((int)Globals.Setup.CountThreads).ToArray();

                            if (devices.Length != (int)Globals.Setup.CountThreads)
                            {
                                Log.Write($"[I] - Больше девайсов не найдено, остаточное кол-во: {devices.Length}\n", _logFile.FullName);
                                break;
                            }

                            foreach (var device in devices)
                                busyDevices[device] = id;
                        }

                        foreach (var device in devices)
                        {
                            tasks.Add(Handler(message.Split('\n'), id, new Client(deviceId: device.Index)));
                            await Task.Delay(1_500);
                        }

                        Task.WaitAll(tasks.ToArray(), -1);

                        _activePhones.RemoveAll(obj => obj[0].ToString() == id.ToString());
                        tasks.Clear();

                        if (_accountsNotFound)
                            break;

                        foreach (var busyDevice in busyDevices.Where(device => device.Value == id))
                            busyDevices.Remove(busyDevice.Key);
                    }

                    //_usedPhones.RemoveAll(obj => obj[0].ToString() == id.ToString());
                }));
            }

            Task.WaitAll(mainTasks.ToArray(), -1);

            if (IsStop || _currentProfile.Scaning || _currentProfile.CheckBan)
                break;

            if (repeatId < Globals.Setup.RepeatCounts)
                DashboardView.GetInstance().CompletedTasks = repeatId + 1;

            _accountsNotFound = false;
            busyDevices.Clear();
            mainTasks.Clear();
            _usedPhones.Clear();
            _activePhones.Clear();
        }

        Stop();
    }

    public void Stop()
    {
        _logFile = null;
        _accountsNotFound = false;
        IsStop = false;

        _usedPhones.Clear();
        _activePhones.Clear();
    }

    private async Task Handler(string[] messages, int threadId, Client client)
    {
        try
        {
            var lastCountThreads = Globals.Setup.CountThreads;
            var countBans = 0;

            await client.GetInstance().ShellCmd("settings put global window_animation_scale 0");
            await client.GetInstance().ShellCmd("settings put global transition_animation_scale 0");
            await client.GetInstance().ShellCmd("settings put global animator_duration_scale 0");

            await client.GetInstance().RunApk("net.sourceforge.opencamera");
            await client.GetInstance().StopApk("net.sourceforge.opencamera");

            Log.Write($"Поток {threadId} запущен с устройством {client.GetInstance().Index}\n", _logFile.FullName);

        getAccount:
            if (IsStop)
                return;

            var result = Globals.GetAccounts(_usedPhones.Select(phone => phone.Remove(0, 1)).ToArray(), true, _lock, _currentProfile.Warm ? _activePhones.Where(phone => phone[0].ToString() == threadId.ToString()).Select(phone => phone.Remove(0, 1)).ToArray() : null);

            DashboardView.GetInstance().AllTasks = Directory.GetDirectories(Globals.Setup.PathToFolderAccounts).Where(dir => File.Exists($@"{dir}\Data.json") && !_usedPhones.Contains(dir)).ToArray().Length;

            if (result.Length < 1)
            {
                Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                _accountsNotFound = true;
                return;
            }

            var (phone, path) = result[0];

            if (_usedPhones.Select(phone => phone.Remove(0, 1)).Contains(phone))
            {
                Log.Write($"[I] - дубликат аккаунта\n", _logFile.FullName);
                goto getAccount;
            }

            _usedPhones.Add(threadId + phone);

            if (!await TryLogin(client, phone, path))
            {
                ++DashboardView.GetInstance().DeniedTasks;
                Log.Write($"[{phone}] - не смогли войти\n", _logFile.FullName);
                goto getAccount;
            }
            else
                Log.Write($"[{phone}] - смогли войти\n", _logFile.FullName);

            if (_currentProfile.TouchAccount)
            {
                var contactPhones = new List<CObj>();
                foreach (var phoneForContact in await File.ReadAllLinesAsync(Globals.Setup.PathToFilePeoples))
                    contactPhones.Add(new(MemuLib.Globals.RandomString(new Random().Next(5, 15)), "+" + phoneForContact));

                foreach (var phoneForContact in await File.ReadAllLinesAsync(Globals.Setup.PathToFilePhonesContacts))
                    if (new Random().Next(0, 100) >= 50)
                        contactPhones.Add(new(MemuLib.Globals.RandomString(new Random().Next(5, 15)), "+" + phoneForContact));

                await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf", ContactManager.Export(contactPhones));

                await client.ImportContacts($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf");

                await Task.Delay((Globals.Setup.DelayTouchAccount ?? 0) * 1000);

                if (!await client.IsValid())
                {
                    ++DashboardView.GetInstance().DeniedTasks;
                    await DeleteAccount(client);
                }
                else
                    ++DashboardView.GetInstance().CompletedTasks;

                goto getAccount;
            }

            if (_currentProfile.CheckBan)
            {
                ++DashboardView.GetInstance().CompletedTasks;
                goto getAccount;
            }

            try
            {
                if (await client.GetInstance().ExistsElement("text=\"ЧАТЫ\""))
                    await client.GetInstance().Click("text=\"ЧАТЫ\"");

                if (_currentProfile.Warm)
                    _activePhones.Add(threadId + phone);

                if (_currentProfile.WelcomeMessage)
                {
                    var rnd = new Random();
                    await client.GetInstance().StopApk(client.PackageName);
                    await client.GetInstance().RunApk(client.PackageName);
                    await Task.Delay(1_000);

                    if (!await client.IsValid())
                    {
                        ++DashboardView.GetInstance().DeniedTasks;
                        await DeleteAccount(client);
                        goto getAccount;
                        //return;
                    }

                    var contactPhones = new List<CObj>();
                    foreach (var phoneForContact in await File.ReadAllLinesAsync(Globals.Setup.PathToFilePeoples))
                        contactPhones.Add(new(MemuLib.Globals.RandomString(rnd.Next(5, 15)), "+" + phoneForContact));

                    foreach (var phoneForContact in await File.ReadAllLinesAsync(Globals.Setup.PathToFilePhonesContacts))
                        if (rnd.Next(0, 100) >= 50)
                            contactPhones.Add(new(MemuLib.Globals.RandomString(rnd.Next(5, 15)), "+" + phoneForContact));

                    await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf", ContactManager.Export(contactPhones));

                    await client.ImportContacts($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf");

                    try
                    {
                        if (!await MoveToWelcomeMessage(client, messages[rnd.Next(0, messages.Length - 1)]))
                        {
                            if (!await client.IsValid())
                            {
                                ++DashboardView.GetInstance().DeniedTasks;
                                await DeleteAccount(client);
                                goto getAccount;
                            }
                        }
                        else if (!_currentProfile.Warm)
                        {
                            await client.UpdateData(true);
                            ++DashboardView.GetInstance().CompletedTasks;
                            goto getAccount;
                        }
                    }
                    catch (Exception ex)
                    {
                        _usedPhones.Remove(threadId + phone);
                        Log.Write($"[Handler - WelcomeMessage] - Произошла ошибка, аккаунт возвращен в очередь: {ex.Message}\n", _logFile.FullName);
                        goto getAccount;
                    }
                }

                if (_currentProfile.Warm)
                {
                    while (lastCountThreads != _activePhones.Count(phone => phone[0].ToString() == threadId.ToString()))
                    {
                        if (_accountsNotFound || IsStop)
                            break;

                        await Task.Delay(500);
                    }

                    if (_accountsNotFound || IsStop)
                    {
                        await client.Web!.Free();
                        return;
                    }

                    var phones = _activePhones.Where(phone => phone[0].ToString() == threadId.ToString()).Select(phone => phone.Remove(0, 1)).ToArray();

                    var contactPhones = new List<CObj>();
                    foreach (var phoneForContact in phones)
                        contactPhones.Add(new(MemuLib.Globals.RandomString(new Random().Next(5, 15)), "+" + phoneForContact));

                    await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf", ContactManager.Export(contactPhones));

                    await client.ImportContacts($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf");

                    File.Delete($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf");

                    var rnd = new Random();

                    for (var i = 0; i < Globals.Setup.CountMessages; i++)//Первый этап - переписки между собой
                    {
                        if (IsStop)
                            return;

                        if (!await client.IsValid())
                        {
                            ++DashboardView.GetInstance().DeniedTasks;
                            await DeleteAccount(client);
                            return;
                        }

                        foreach (var warmPhone in phones.Where(_phone => _phone != phone).Select(_phone => "+" + _phone))
                        {
                            if (!await client.SendMessage(warmPhone, messages[rnd.Next(0, messages.Length - 1)]))
                                break;
                        }
                    }

                    client.AccountData.FirstMsg = phones[0] == phone;
                    ++client.AccountData.TrustLevelAccount;

                    await client.UpdateData(true);

                    await client.GetInstance().StopApk(client.PackageName);
                    await client.GetInstance().RunApk(client.PackageName);
                }

                if (IsStop)
                    return;

                if (_currentProfile.Scaning)
                {
                    if (_currentProfile.Warm)
                        if (!(Globals.Setup.SelectEmulatorScan.Value.Index == 0 || Globals.Setup.SelectEmulatorScan.Value.Index == 1 && !client.AccountData.FirstMsg)//Получатель
                        && !(Globals.Setup.SelectEmulatorScan.Value.Index == 0 || Globals.Setup.SelectEmulatorScan.Value.Index == 2 && client.AccountData.FirstMsg))//Отправитель
                            return;

                    var resultWeb = await TryLoginWeb(client, phone);
                    if (!resultWeb.Item1)
                    {
                        client.Web.RemoveQueue();
                        if (resultWeb.Item2 >= 3)
                        {
                            _usedPhones.Remove(threadId + phone);
                        }
                        else
                        {
                            ++DashboardView.GetInstance().DeniedTasks;
                            await DeleteAccount(client);
                        }

                        if (++countBans >= Globals.Setup.CountBansToSleep)
                        {
                            await client.Stop();
                            return;
                        }
                    }
                    else
                    {
                        countBans = 0;
                        ++DashboardView.GetInstance().CompletedTasks;
                    }

                    if (!string.IsNullOrEmpty(Globals.Setup.LinkToChangeIP))
                        Log.Write(await ResourceHelper.GetAsync(Globals.Setup.LinkToChangeIP), _logFile.FullName);

                    if (!_currentProfile.Warm)
                        goto getAccount;
                }

                if (_currentProfile.Warm)
                    _activePhones.Remove(threadId + phone);
            }
            catch (Exception ex)
            {
                client.Web.RemoveQueue();
                _usedPhones.Remove(threadId + phone);

                Log.Write($"[Handler] - Произошла ошибка, аккаунт возвращен в очередь: {ex.Message}\n", _logFile.FullName);
            }

            async Task<(bool, int)> TryLoginWeb(Client client, string phone)
            {
                await client.GetInstance().StopApk(client.PackageName);
                await client.GetInstance().RunApk(client.PackageName);
                await Task.Delay(1_000);

                await MoveToScan(client, true);
                /*{
                    client.Web!.RemoveQueue();
                    Log.Write($"[{phone}] - Почему то не смогли найти кнопки :(\n", _logFile.FullName);
                    return false;
                }*/

                int i = 0;
                try
                {
                initAgain:
                    var initWithErrors = false;

                    try
                    {
                        if (Directory.Exists($@"{client.Account}\{client.Phone.Remove(0, 1)}"))
                            Directory.Delete($@"{client.Account}\{client.Phone.Remove(0, 1)}", true);

                        if (File.Exists($@"{client.Account}\{client.Phone.Remove(0, 1)}.data.json"))
                            File.Delete($@"{client.Account}\{client.Phone.Remove(0, 1)}.data.json");
                    }
                    catch
                    {

                    }

                    if (!await client.IsValid() || i >= 3)
                    {
                        client.Web!.RemoveQueue();
                        Log.Write($"[{phone}] - Аккаунт оказался не валидным\n", _logFile.FullName);
                        return (false, i);
                    }

                    if (i > 0)
                        if (!await MoveToScan(client, false))
                        {
                            ++i;
                            goto initAgain;
                        }

                    initWithErrors = true;

                    /*//await client.GetInstance().Click(360, 571);
                    var dump = await client.GetInstance().DumpScreen();
                    if (await client.GetInstance().ExistsElement("text=\"ПРИВЯЗКА УСТРОЙСТВА\"", dump, false))
                        await client.GetInstance().Click("text=\"ПРИВЯЗКА УСТРОЙСТВА\"", dump);*/

                    try
                    {
                        await client.Web!.Init(true, @$"{client.Account}\{client.Phone.Remove(0, 1)}", await GetProxy());

                        initWithErrors = false;
                    }
                    catch (Exception ex)
                    {
                        Log.Write($"[{phone}] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
                    }

                    await client.Web!.Free();

                    await Task.Delay(1_000);

                    var dump = await client.GetInstance().DumpScreen();

                    if (await client.GetInstance().ExistsElement("text=\"ПОДТВЕРДИТЬ\"", dump, false))
                    {
                        Globals.QrCode = null;
                        await client.GetInstance().StopApk(client.PackageName);

                        return (false, i);
                    }

                    if (await client.GetInstance().ExistsElement("text=\"OK\"", dump, isWait: false))
                    {
                        //await client.GetInstance().Click("text=\"OK\"");
                        ++i;
                        Globals.QrCode = null;

                        /*if (await client.GetInstance().ExistsElement("text=\"ПРИВЯЗКА УСТРОЙСТВА\""))
                        {
                            //await Task.Delay(1_000);
                            //await client.GetInstance().Click("text=\"ПРИВЯЗКА УСТРОЙСТВА\"");
                        }*/


                        await client.GetInstance().StopApk(client.PackageName);
                        await client.GetInstance().RunApk(client.PackageName);

                        goto initAgain;
                    }

                    if (initWithErrors)
                    {
                        ++i;
                        Globals.QrCode = null;
                        Log.Write($"[{phone}] - Инициализировалось с ошибками\n", _logFile.FullName);

                        await client.GetInstance().StopApk(client.PackageName);
                        await client.GetInstance().RunApk(client.PackageName);

                        goto initAgain;
                    }

                    //await Task.Delay(1_000);
                    //resource-id="com.whatsapp.w4b:id/device_name_edit_text"
                    //dump = await client.GetInstance().DumpScreen();
                    if (await client.GetInstance().ExistsElement("resource-id=\"com.whatsapp.w4b:id/device_name_edit_text\"", dump, false))
                    {
                        await client.GetInstance().Input("resource-id=\"com.whatsapp.w4b:id/device_name_edit_text\"", _names[new Random().Next(0, _names.Length)].Replace(' ', 'I'), dump);
                        await client.GetInstance().Click("text=\"СОХРАНИТЬ\"", dump);
                    }

                    client.Web.RemoveQueue();

                    await SuccesfulMoveAccount(client);
                    return (true, i);
                }
                catch (Exception ex)
                {
                    Log.Write($"[main] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
                    Globals.QrCode = null;
                    
                    client.Web.RemoveQueue();

                    await client.Web.Free();
                    await client.GetInstance().StopApk(client.PackageName);

                    return (false, i);
                }
            }

            async Task<bool> MoveToScan(Client client, bool isWaitQueue)
            {
                var countTry = 0;
            tryAgain:
                var dump = await client.GetInstance().DumpScreen();

                if (await client.GetInstance().ExistsElement("text=\"Выберите частоту резервного копирования\"", dump))
                {
                    await client.GetInstance().Click("text=\"Выберите частоту резервного копирования\"", dump);
                    await client.GetInstance().Click("text=\"Никогда\"", dump);
                    await client.GetInstance().Click("text=\"ГОТОВО\"", dump);
                    await Task.Delay(1_000);
                    await client.GetInstance().StopApk(client.PackageName);
                    await client.GetInstance().RunApk(client.PackageName);
                    await Task.Delay(1_000);
                    dump = await client.GetInstance().DumpScreen();
                }

                if (await client.GetInstance().ExistsElement("text=\"НЕ СЕЙЧАС\"", dump, false))
                {
                    await client.GetInstance().Click("text=\"НЕ СЕЙЧАС\"", dump);
                    await Task.Delay(500);
                    dump = await client.GetInstance().DumpScreen();
                }

                if (await client.GetInstance().ExistsElement($"resource-id=\"{client.PackageName}:id/code\"", dump))
                {
                    await client.GetInstance().Input($"resource-id=\"{client.PackageName}:id/code\"", Globals.Setup.PinCodeAccount.ToString(), dump);
                    await client.GetInstance().StopApk(client.PackageName);
                    await client.GetInstance().RunApk(client.PackageName);
                    await Task.Delay(1_000);
                    dump = await client.GetInstance().DumpScreen();
                }

                if (!await client.GetInstance().ExistsElement("content-desc=\"Ещё\"", dump))
                {
                    Log.Write($"Не можем найти кнопку ещё, пробуем сдампить еще раз\n", _logFile.FullName);

                    if (++countTry >= 3)
                        return false;

                    await Task.Delay(1_000);
                    goto tryAgain;
                }

                await client.GetInstance().Click("content-desc=\"Ещё\"", dump);
                await client.GetInstance().Click("text=\"Связанные устройства\"");

                dump = await client.GetInstance().DumpScreen();
                if (await client.GetInstance().ExistsElement("resource-id=\"android:id/button1\"", dump))
                    await client.GetInstance().Click("resource-id=\"android:id/button1\"", dump);

                if (isWaitQueue)
                {
                    client.Web.AddToQueue();
                    await client.Web.WaitQueue();
                }

                await client.GetInstance().Click("text=\"ПРИВЯЗКА УСТРОЙСТВА\"");

                await Task.Delay(500);

                dump = await client.GetInstance().DumpScreen();
                if (await client.GetInstance().ExistsElement("text=\"OK\"", dump))
                    await client.GetInstance().Click("text=\"OK\"", dump);

                return true;
            }

            async Task<bool> MoveToWelcomeMessage(Client client, string text)
            {
                var countTry = 0;
            tryAgain:
                var dump = await client.GetInstance().DumpScreen();

                if (await client.GetInstance().ExistsElement("text=\"Выберите частоту резервного копирования\"", dump))
                {
                    await client.GetInstance().Click("text=\"Выберите частоту резервного копирования\"", dump);
                    await client.GetInstance().Click("text=\"Никогда\"", dump);
                    await client.GetInstance().Click("text=\"ГОТОВО\"", dump);
                    await Task.Delay(1_000);
                    await client.GetInstance().StopApk(client.PackageName);
                    await client.GetInstance().RunApk(client.PackageName);
                    await Task.Delay(1_000);
                    dump = await client.GetInstance().DumpScreen();
                }

                if (await client.GetInstance().ExistsElement("text=\"НЕ СЕЙЧАС\"", dump, false))
                {
                    await client.GetInstance().Click("text=\"НЕ СЕЙЧАС\"", dump);
                    await Task.Delay(500);
                    dump = await client.GetInstance().DumpScreen();
                }

                if (await client.GetInstance().ExistsElement($"resource-id=\"{client.PackageName}:id/code\"", dump))
                {
                    await client.GetInstance().Input($"resource-id=\"{client.PackageName}:id/code\"", Globals.Setup.PinCodeAccount.ToString(), dump);
                    await client.GetInstance().StopApk(client.PackageName);
                    await client.GetInstance().RunApk(client.PackageName);
                    await Task.Delay(1_000);
                    dump = await client.GetInstance().DumpScreen();
                }

                if (!await client.GetInstance().ExistsElement("content-desc=\"Ещё\"", dump))
                {
                    Log.Write($"Не можем найти кнопку ещё, пробуем сдампить еще раз\n", _logFile.FullName);

                    if (++countTry >= 3)
                        return false;

                    await Task.Delay(1_000);
                    goto tryAgain;
                }

                await client.GetInstance().Click("content-desc=\"Ещё\"", dump);

                await client.GetInstance().Click("text=\"Инструменты для бизнеса\"");
                await client.GetInstance().Click("text=\"Приветственное сообщение\"");

                dump = await client.GetInstance().DumpScreen();
                if (await client.GetInstance().ExistsElement("text=\"ВЫКЛ\"", dump))
                    await client.GetInstance().Click("text=\"ВЫКЛ\"", dump);

                await Task.Delay(500);
                await client.GetInstance().Click("resource-id=\"com.whatsapp.w4b:id/greeting_settings_edit_greeting_message_btn\"", dump);
                dump = await client.GetInstance().DumpScreen();
                await client.GetInstance().Input("resource-id=\"com.whatsapp.w4b:id/edit_text\"", text, clickToFieldInput: false);
                await client.GetInstance().Click("text=\"OK\"", dump);
                await Task.Delay(500);
                await client.GetInstance().Click("text=\"СОХРАНИТЬ\"");
                return true;
            }

            async Task<string> GetProxy()
            {
                if (!File.Exists(Globals.Setup.PathToFileProxy))
                    return "";

                var proxyList = await File.ReadAllLinesAsync(Globals.Setup.PathToFileProxy);

                if (proxyList.Length == 0)
                    return "";

                return proxyList.OrderBy(x => new Random().Next()).ToArray()[0];
            }

            async Task SuccesfulMoveAccount(Client client)
            {
                var countTry = 0;
                while (countTry++ < 3)
                {
                    try
                    {
                        if (Directory.Exists(@$"{Globals.ScannedDirectory.FullName}\{client.Phone.Remove(0, 1)}") && Directory.Exists(client.Account))
                            Directory.Delete(client.Account, true);
                        else if (Directory.Exists(client.Account))
                        {
                            await client.UpdateData(true);
                            Directory.Move(client.Account,
                                @$"{Globals.ScannedDirectory.FullName}\{client.Phone.Remove(0, 1)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Write($"[SuccesfulMoveAccount] - Произошла ошибка, попытка {countTry}: {ex.Message}\n", _logFile.FullName);
                    }

                    await Task.Delay(1_000);
                }
            }

            async Task DeleteAccount(Client client, bool isStartBan = false)
            {
                var countTry = 0;
                while (countTry++ < 3)
                {
                    try
                    {
                        if (Directory.Exists(@$"{((isStartBan) ? Globals.BanStartDirectory.FullName : Globals.BanWorkDirectory.FullName)}\{client.Phone.Remove(0, 1)}") && Directory.Exists(client.Account))
                            Directory.Delete(client.Account, true);
                        else if (Directory.Exists(client.Account))
                        {
                            client!.AccountData.BannedDate = DateTime.Now;
                            await client.UpdateData(true);
                            Directory.Move(client.Account, @$"{((isStartBan) ? Globals.BanStartDirectory.FullName : Globals.BanWorkDirectory.FullName)}\{client.Phone.Remove(0, 1)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Write($"[DeleteAccount] - Произошла ошибка, попытка {countTry}: {ex.Message}\n", _logFile.FullName);
                    }

                    await Task.Delay(1_000);
                }
            }

            async Task<bool> TryLogin(Client client, string phone, string path)
            {
                try
                {
                    await client.ReCreate($"+{phone}", path);
                    if (!await client.Login(name: _names[new Random().Next(0, _names.Length)]))
                    {
                        await DeleteAccount(client, true);
                        return false;
                    }

                    var status = await client.IsValid();
                    if (!status)
                    {
                        await DeleteAccount(client, true);
                        return false;
                    }

                    return status;
                }
                catch (Exception ex)
                {
                    Log.Write($"[TryLogin] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Write($"[CRITICAL] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
        }
    }
}
