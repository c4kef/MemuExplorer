﻿using System;
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

public struct TemplateWarm
{
    public int sendedCount;
    public int warmedAccount;
}

public class AccPreparation
{
    public AccPreparation()
    {
        _usedPhonesUsers = new List<string>();
        _usedPhones = new List<string>();
        _activePhones = new List<string>();
        _warmPhones = new List<Client>();
        _names = new[] { "" };
        _lock = new();
    }

    private readonly List<string> _usedPhonesUsers;
    private readonly List<string> _usedPhones;
    private readonly List<string> _activePhones;
    private readonly List<Client> _warmPhones;
    private readonly object _lock;

    private bool _accountsNotFound;
    private FileInfo _logFile;
    private FileInfo _reportFile;
    private DirectoryInfo _directoryAccounts;
    private string[] _contacts;
    private string[] _names;
    private ActionProfileWork _currentProfile;

    public bool IsStop;
    public TemplateWarm TemplateWarm;

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

        if (!_currentProfile.WarmMethodIlya)
        {
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
                                tasks.Add(Handler(string.IsNullOrEmpty(message) ? Array.Empty<string>() : message.Split('\n'), id, new Client(deviceId: device.Index)));
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
        }
        else
        {
            _directoryAccounts = new DirectoryInfo($@"{Globals.WarmedDirectory}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
            _directoryAccounts.Create();

            TemplateWarm.warmedAccount = 0;
            TemplateWarm.sendedCount = 0;

            Task.WaitAll(new Task[] {
            Task.Run(async () =>
            {
                var tasks = new List<Task>();
                var id = 0;//Just for groups

                _reportFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{new FileInfo(Globals.Setup.PathToFilePhones).Name}_report.txt");
                if (!_reportFile.Exists)
                    _reportFile.Create().Close();

                DataEmulator[] devices = null;
                _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToFilePhones);

                devices = ManagerView.GetInstance().Emulators.Where(device => !busyDevices.Select(device => device.Key.Index).Contains(device.Index) && device.IsEnabled).ToArray();

                if (devices.Length == 0)
                {
                    Log.Write($"[I] - Больше девайсов не найдено, остаточное кол-во: {devices.Length}\n", _logFile.FullName);
                    return;
                }

                foreach (var device in devices)
                    busyDevices[device] = id;

                foreach (var device in devices)
                {
                    tasks.Add(WarmMethodIlya(string.IsNullOrEmpty(message) ? Array.Empty<string>() : message.Split('\n'), id, new Client(deviceId: device.Index), true));
                    await Task.Delay(1_500);
                }

                Task.WaitAll(tasks.ToArray(), -1);//Ждем заполнения устройств аккаунтами...
                DashboardView.GetInstance().CompletedTasks = -1;

                tasks.Clear();

                if (_accountsNotFound)
                    return;

                for(var i = 0; i < Globals.Setup.CountThreads; i++)
                {
                    tasks.Add(WarmMethodIlya(string.IsNullOrEmpty(message) ? Array.Empty<string>() : message.Split('\n'), id, null, false));
                    await Task.Delay(1_500);
                }

                tasks.Add(Task.Run(async() =>
                {
                    while(!IsStop && !_accountsNotFound)
                    {
                        foreach (var chatbot in _warmPhones.ToArray())
                            if (!await chatbot.IsValid())
                            {
                                await Globals.TryMove(chatbot.Account, $@"{Globals.BanWorkDirectory.FullName}\{chatbot.Phone.Remove(0, 1)}");
                                _warmPhones.RemoveAll(client => client.Phone == chatbot.Phone);
                            }

                        if (_warmPhones.Count <= Globals.Setup.CountCritAliveAccountsToStopWarm)
                        {
                            IsStop = true;
                            break;
                        }
                        else
                            await Task.Delay(5_000);
                    }
                }));

                Task.WaitAll(tasks.ToArray(), -1);//Ждем окочания переписок...
                DashboardView.GetInstance().CompletedTasks = 0;

                File.WriteAllLines(Globals.Setup.PathToFilePhones, _contacts.Where(cont => !_usedPhonesUsers.Contains(cont)));
                tasks.Clear();
            })}, -1);
        }

        Stop();
    }

    public void Stop()
    {
        _logFile = null;
        _accountsNotFound = false;
        IsStop = false;

        _warmPhones.Clear();
        _usedPhonesUsers.Clear();
        _usedPhones.Clear();
        _activePhones.Clear();
    }

    private async Task WarmMethodIlya(string[] messages, int threadId, Client client, bool isFeel)
    {
        try
        {
            Log.Write($"Поток {threadId} запущен\n", _logFile.FullName);

        getAccount:
            if (IsStop)
                return;

            var result = Globals.GetAccounts(_usedPhones.Select(phone => phone.Remove(0, 1)).ToArray(), true, _lock, _activePhones.Where(phone => phone[0].ToString() == threadId.ToString()).Select(phone => phone.Remove(0, 1)).ToArray(), isFeel ? Globals.Setup.PathToFolderAccountsAdditional : Globals.Setup.PathToFolderAccounts);

            DashboardView.GetInstance().AllTasks = Directory.GetDirectories(isFeel ? Globals.Setup.PathToFolderAccountsAdditional : Globals.Setup.PathToFolderAccounts).Where(dir => File.Exists($@"{dir}\Data.json") && !_usedPhones.Contains(dir)).ToArray().Length;

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

            if (isFeel)
            {
                await client.GetInstance().ShellCmd("settings put global window_animation_scale 0");
                await client.GetInstance().ShellCmd("settings put global transition_animation_scale 0");
                await client.GetInstance().ShellCmd("settings put global animator_duration_scale 0");

                await client.GetInstance().ClearContacts();

                if (!await TryLogin(client, phone, path))
                {
                    ++DashboardView.GetInstance().DeniedTasks;
                    ++DashboardView.GetInstance().DeniedTasksStart;
                    Log.Write($"[{phone}] - не смогли войти\n", _logFile.FullName);
                    goto getAccount;
                }
                else
                    Log.Write($"[{phone}] - смогли войти\n", _logFile.FullName);

                var rnd = new Random();
                await client.GetInstance().StopApk(client.PackageName);
                await client.GetInstance().RunApk(client.PackageName);
                await Task.Delay(1_000);

                if (!await client.IsValid())
                {
                    ++DashboardView.GetInstance().DeniedTasks;
                    ++DashboardView.GetInstance().DeniedTasksStart;
                    await DeleteAccount(client);
                    goto getAccount;
                    //return;
                }

                var contactPhones = new List<CObj>();
                foreach (var phoneForContact in Directory.GetDirectories(Globals.Setup.PathToFolderAccounts).Select(dir => new DirectoryInfo(dir).Name))
                    contactPhones.Add(new(_names[new Random().Next(0, _names.Length)], "+" + phoneForContact));

                foreach (var phoneForContact in await File.ReadAllLinesAsync(Globals.Setup.PathToFilePhonesContacts))
                    if (rnd.Next(0, 100) >= 50)
                        contactPhones.Add(new(_names[new Random().Next(0, _names.Length)], "+" + phoneForContact));

                await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf", ContactManager.Export(contactPhones));

                await client.ImportContacts($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf");

                try
                {
                    if (!await MoveToWelcomeMessage(client, messages.Length > 0 ? messages[rnd.Next(0, messages.Length - 1)] : string.Empty))
                    {
                        if (!await client.IsValid())
                        {
                            ++DashboardView.GetInstance().DeniedTasks;
                            ++DashboardView.GetInstance().DeniedTasksWork;
                            await DeleteAccount(client);
                            goto getAccount;
                        }
                        else
                        {
                            _usedPhones.Remove(threadId + phone);
                            Log.Write($"[Handler - WelcomeMessage] - Аккаунт возвращен в очередь\n", _logFile.FullName);
                            goto getAccount;
                        }
                    }
                    else
                    {
                        await client.UpdateData(true);
                        ++DashboardView.GetInstance().CompletedTasks;
                        _warmPhones.Add(client);
                    }
                }
                catch (Exception ex)
                {
                    _usedPhones.Remove(threadId + phone);
                    Log.Write($"[Handler - WelcomeMessage] - Произошла ошибка, аккаунт возвращен в очередь: {ex.Message}\n", _logFile.FullName);
                    goto getAccount;
                }
            }
            else
            {
                client = new Client(phone, path);

                try
                {
                    await client.Web!.Init(false, $@"{path}\{new DirectoryInfo(path).Name}", await GetProxy());
                    Log.Write($"[{phone}] - смогли войти\n", _logFile.FullName);
                }
                catch (Exception ex)
                {
                    await client.Web!.Free();
                    await Globals.TryMove(path, $@"{Globals.LogoutDirectory.FullName}\{phone}");
                    Log.Write($"[{phone}] - не смогли войти: {ex.Message}\n", _logFile.FullName);

                    ++DashboardView.GetInstance().DeniedTasks;
                    ++DashboardView.GetInstance().DeniedTasksStart;
                    goto getAccount;
                }

                try
                {
                    var usedWarmPhonesTmp = new List<Client>();

                    while (true)
                    {
                        if (!await client.Web!.IsConnected())
                            break;

                        var arr = _warmPhones.ToArray().Where(bot => !usedWarmPhonesTmp.Select(element => element.Phone).Contains(bot.Phone)).Take(Globals.Setup.CountMessageWarm ?? 0).ToArray();

                        if (!arr.Any())
                            break;

                        usedWarmPhonesTmp.AddRange(arr);

                        foreach (var chatbot in arr)
                            await client.Web!.SendText(chatbot.Phone.Remove(0, 1), messages[new Random().Next(0, messages.Length - 1)]);

                        if (arr.Length != Globals.Setup.CountMessageWarm)
                            break;

                        for (var i = 0; i < Globals.Setup.CountMessageWarmNewsletter; i++)
                        {
                            if (!await client.Web!.IsConnected())
                                break;

                            var freePhone = GetFreeNumberUser();
                            if (string.IsNullOrEmpty(freePhone))
                                break;

                            var text = string.Join('\n', DashboardView.GetInstance().Text.Split('\r').ToList());
                            FileInfo? image = null;

                            string? buttonText = null;
                            string title = string.Empty;
                            string footer = string.Empty;

                            foreach (var match in new Regex(@"\{(.*?)\}").Matches(text).Select(match => match.Value.Replace("{", "").Replace("}", "")))
                            {
                                if (match.Contains(Globals.TagPicture))
                                {
                                    var tmpImage = new FileInfo(match.Remove(0, Globals.TagPicture.Length));
                                    if (tmpImage.Exists)
                                    {
                                        image = tmpImage;
                                        break;
                                    }
                                }
                                else if (match.Contains(Globals.TagTitle))
                                {
                                    title = match.Remove(0, Globals.TagTitle.Length);
                                }
                                else if (match.Contains(Globals.TagFooter))
                                {
                                    footer = match.Remove(0, Globals.TagFooter.Length);
                                }
                                else if (match.Contains(Globals.TagButton))
                                {
                                    buttonText = match.Remove(0, Globals.TagButton.Length);
                                }
                            }

                            if (await client.Web!.SendText(freePhone, SelectWord(text), image, buttonText, title, footer))
                            {
                                Log.Write($"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};{freePhone}", _reportFile.FullName);
                                ++TemplateWarm.sendedCount;
                            }
                            else
                                _usedPhonesUsers.Remove(freePhone);
                        }
                    }

                    if (!await client.Web!.IsConnected())
                    {
                        await Task.Delay(1_000);
                        await client.Web!.Free();
                        await Globals.TryMove(path, $@"{Globals.WebBanWorkDirectory.FullName}\{phone}");
                        ++DashboardView.GetInstance().DeniedTasks;
                        ++DashboardView.GetInstance().DeniedTasksWork;
                    }
                    else
                    {
                        await Task.Delay(1_000);
                        await client.Web!.Free();
                        await Globals.TryMove(path, $@"{_directoryAccounts.FullName}\{phone}");
                        ++TemplateWarm.warmedAccount;
                    }
                }
                catch (Exception ex)
                {
                    Log.Write($"[Handler] - крит. ошибка: {ex.Message}\n", _logFile.FullName);
                    await client.Web!.Free();
                    ++DashboardView.GetInstance().DeniedTasks;
                    ++DashboardView.GetInstance().DeniedTasksWork;
                    await Globals.TryMove(path, $@"{Globals.WebBanWorkDirectory.FullName}\{phone}");
                }
             
                goto getAccount;
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

            string SelectWord(string value)
            {
                var backValue = value;
                foreach (var match in new Regex(@"\{random=(.*?)\}", RegexOptions.Multiline).Matches(backValue))
                {
                    var arrText = match.ToString()!.Split("||").Select(val => val.Replace("{", "").Replace("}", "").Replace(Globals.TagRandom, "")).ToArray();
                    backValue = backValue.Replace(match.ToString()!, arrText[new Random().Next(0, arrText.Length)]);
                }
                return new Regex(@"\{([^)]*)\}").Replace(backValue, "").Replace("\"", "").Replace("\'", "");
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
                /*if (!string.IsNullOrEmpty(text))
                {
                    await client.GetInstance().Click("resource-id=\"com.whatsapp.w4b:id/greeting_settings_edit_greeting_message_btn\"", dump);
                    dump = await client.GetInstance().DumpScreen();
                    await client.GetInstance().Input("resource-id=\"com.whatsapp.w4b:id/edit_text\"", text, clickToFieldInput: false);
                    await client.GetInstance().Click("text=\"OK\"", dump);
                    await Task.Delay(500);
                }*/
                await client.GetInstance().Click("text=\"СОХРАНИТЬ\"");
                return true;
            }

            string GetFreeNumberUser()
            {
                lock (_lock)
                {
                    foreach (var contact in _contacts)
                    {
                        if (!_usedPhonesUsers.Contains(contact))
                        {
                            if (contact.Length < 5 || string.IsNullOrEmpty(contact))
                                continue;

                                _usedPhonesUsers.Add(contact);

                            var newContacts = _contacts.Except(_usedPhonesUsers).ToArray();

                            if (_usedPhonesUsers.Count % 100 == 0)
                                File.WriteAllLines(Globals.Setup.PathToFilePhones, newContacts);

                            return contact[0] == '+' ? contact.Remove(0, 1) : contact;
                        }
                    }

                    return string.Empty;
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

            await client.GetInstance().ClearContacts();

            if (!await TryLogin(client, phone, path))
            {
                ++DashboardView.GetInstance().DeniedTasks;
                ++DashboardView.GetInstance().DeniedTasksStart;
                Log.Write($"[{phone}] - не смогли войти\n", _logFile.FullName);
                goto getAccount;
            }
            else
                Log.Write($"[{phone}] - смогли войти\n", _logFile.FullName);

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
                        contactPhones.Add(new(_names[new Random().Next(0, _names.Length)], "+" + phoneForContact));

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
                            ++DashboardView.GetInstance().DeniedTasksWork;
                            await DeleteAccount(client);
                            return;
                        }

                        foreach (var warmPhone in phones.Where(_phone => _phone != phone).Select(_phone => _phone))
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
                            ++DashboardView.GetInstance().DeniedTasksWork;
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
                    return (false, 0);
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


                    if (i >= 3)
                    {
                        client.Web!.RemoveQueue();
                        Log.Write($"[{phone}] - Аккаунт оказался не валидным\n", _logFile.FullName);
                        return (false, i);
                    }
                    //Костыль
                    if (!await client.IsValid())
                    {
                        client.Web!.RemoveQueue();
                        Log.Write($"[{phone}] - Аккаунт оказался не валидным\n", _logFile.FullName);
                        return (false, i);
                    }
                    else
                        Log.Write($"[{phone}] - Аккаунт оказался валидным\n", _logFile.FullName);


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
