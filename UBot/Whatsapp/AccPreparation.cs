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
using System.Threading;
using UBot.Pages.Dialogs;
using static Microsoft.Maui.ApplicationModel.Permissions;
using Windows.Media.Protection.PlayReady;
using Newtonsoft.Json.Linq;
using Microsoft.Maui.Controls;

namespace UBot.Whatsapp;

public struct TemplateWarm
{
    public int sendedCount;
    public int warmedAccount;
    public int sendedBan;
    public int sendedBanPeople;
}

public class AccPreparation
{
    public AccPreparation()
    {
        _usedPhonesUsers = new List<string>();
        _tetheredDevices = new Dictionary<int, List<Client>>();
        _readyPhones = new Dictionary<int, List<Client>>();
        _usedPhones = new Dictionary<string, bool>();
        _activePhones = new List<string>();
        _warmPhones = new List<Client>();
        //_warmPhones = new Dictionary<Client, bool>();
        _names = new[] { "" };
        _lock = new();
    }

    private readonly Dictionary<int, List<Client>> _tetheredDevices;
    private readonly Dictionary<int, List<Client>> _readyPhones;
    private readonly List<string> _usedPhonesUsers;
    private readonly List<string> _activePhones;
    //private readonly Dictionary<Client, bool> _warmPhones;
    private readonly List<Client> _warmPhones;
    private readonly object _lock;
    private object _lock1;

    private bool _accountsNotFound;
    private FileInfo _logFile;
    private FileInfo _reportFile;
    private DirectoryInfo _directoryAccounts;
    private string[] _messagesWelcome;
    private string[] _contacts;
    private string[] _names;
    private Dictionary<string, bool> _usedPhones;
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

        if (File.Exists(Globals.Setup.PathToFileTextWelcome))
            _messagesWelcome = await File.ReadAllLinesAsync(Globals.Setup.PathToFileTextWelcome);

        if (_currentProfile.WarmMethodIlya)
        {
            _directoryAccounts = new DirectoryInfo($@"{Globals.WarmedDirectory}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
            _directoryAccounts.Create();

            TemplateWarm.warmedAccount = 0;
            TemplateWarm.sendedCount = 0;
            TemplateWarm.sendedBan = 0;
            TemplateWarm.sendedBanPeople = 0;
            _directoryAccounts = new DirectoryInfo($@"{Globals.WarmedDirectory}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
            _directoryAccounts.Create();

            TemplateWarm.warmedAccount = 0;
            TemplateWarm.sendedCount = 0;
            TemplateWarm.sendedBan = 0;
            TemplateWarm.sendedBanPeople = 0;

            Task.WaitAll(new Task[] {
            Task.Run(async () =>
            {
                var tasks = new List<Task>();
                var contactPhones = new List<CObj>();
                var rnd = new Random();
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

                foreach (var phoneForContact in Directory.GetDirectories(Globals.Setup.PathToFolderAccounts).Select(dir => new DirectoryInfo(dir).Name))
                    contactPhones.Add(new(_names[new Random().Next(0, _names.Length)], "+" + phoneForContact));

                foreach (var phoneForContact in await File.ReadAllLinesAsync(Globals.Setup.PathToFilePhonesContacts))
                    if (rnd.Next(0, 100) >= 50)
                        contactPhones.Add(new(_names[new Random().Next(0, _names.Length)], "+" + phoneForContact));

                await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\contacts.vcf", ContactManager.Export(contactPhones));

                foreach (var device in devices)
                {
                    var client = new Client(deviceId: device.Index);
                    await client.ImportContacts($@"{Globals.TempDirectory.FullName}\contacts.vcf");
                    _warmPhones.Add(client);

                    tasks.Add(WarmMethodIlya(string.IsNullOrEmpty(message) ? Array.Empty<string>() : message.Split('\n'), id, client, true));
                    await Task.Delay(1_500);
                }

                File.Delete($@"{Globals.TempDirectory.FullName}\contacts.vcf");

                Task.WaitAll(tasks.ToArray(), -1);//Ждем заполнения устройств аккаунтами... Теперь другое, ждем когда всем аккам установим приветственное сообщение
                DashboardView.GetInstance().CompletedTasks = -1;

                DashboardView.GetInstance().DeniedTasksStart = 0;
                DashboardView.GetInstance().DeniedTasksWork = 0;

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
                    var _usedPhonesWelcome = new List<string>();
                    while(!IsStop && !_accountsNotFound)
                    {
                        foreach (var chatbot in _warmPhones.ToArray())
                        {
                            if (!await chatbot.IsValid())
                            {
                                await Globals.TryMove(chatbot.Account, $@"{Globals.BanWorkDirectory.FullName}\{chatbot.Phone.Remove(0, 1)}");
                                _warmPhones.RemoveAll(c => c.Phone == chatbot.Phone);
                            }

                            if (_warmPhones.Count < Globals.Setup.CountCritAliveAccountsToStopWarm)
                            {
                                IsStop = true;
                                break;
                            }
                        }
                            //await Globals.TryMove(chatbot.Account, $@"{Globals.BanWorkDirectory.FullName}\{chatbot.Phone.Remove(0, 1)}");
                            /*getAccount:
                             if (IsStop || _accountsNotFound)
                                break;

                            var result = Globals.GetAccounts(_usedPhonesWelcome.Select(phone => phone.Remove(0, 1)).ToArray(), true, _lock, _activePhones.Where(phone => phone[0].ToString() == id.ToString()).Select(phone => phone.Remove(0, 1)).ToArray(), Globals.Setup.PathToFolderAccountsAdditional);

                            DashboardView.GetInstance().AllTasks = Directory.GetDirectories(Globals.Setup.PathToFolderAccountsAdditional).Where(dir => File.Exists($@"{dir}\Data.json") && !_usedPhonesWelcome.Contains(dir)).ToArray().Length;

                            if (result.Length < 1)
                            {
                                if (_usedPhonesWelcome.Count > 0)
                                {
                                    _usedPhonesWelcome.Clear();
                                    break;
                                }

                                Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                                _accountsNotFound = true;
                                break;
                            }

                            var (phone, path) = result[0];

                            /*if (_usedPhonesWelcome.Contains(phone))
                            {
                                Log.Write($"[I] - дубликат аккаунта\n", _logFile.FullName);
                                goto getAccount;
                            }
                            _usedPhonesWelcome.Add(phone);

                            if (!await TryLogin(chatbot, phone, path))
                            {
                                ++DashboardView.GetInstance().DeniedTasks;
                                ++DashboardView.GetInstance().DeniedTasksWork;
                                Log.Write($"[{phone}] - не смогли войти\n", _logFile.FullName);
                                goto getAccount;
                            }
                            else
                                Log.Write($"[{phone}] - смогли войти\n", _logFile.FullName);

                            await Task.Delay(Globals.Setup.TakeCountRandomAccountDelay ?? 1 * 1000);
                            var status = await chatbot.IsValid();
                            if (!status)
                                await DeleteAccount(chatbot, true);
                            else
                                await chatbot.UpdateData(true);
                        }*/
                    }
                }));

                Task.WaitAll(tasks.ToArray(), -1);//Ждем окочания переписок...
                DashboardView.GetInstance().CompletedTasks = 0;

                File.WriteAllLines(Globals.Setup.PathToFilePhones, _contacts.Where(cont => !_usedPhonesUsers.Contains(cont)));
                tasks.Clear();
            /*Task.WaitAll(new Task[] {
            Task.Run(async () =>
            {
                var tasks = new List<Task>();
                var contactPhones = new List<CObj>();
                var rnd = new Random();
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

                foreach (var phoneForContact in Directory.GetDirectories(Globals.Setup.PathToFolderAccounts).Select(dir => new DirectoryInfo(dir).Name))
                    contactPhones.Add(new(_names[new Random().Next(0, _names.Length)], "+" + phoneForContact));

                foreach (var phoneForContact in await File.ReadAllLinesAsync(Globals.Setup.PathToFilePhonesContacts))
                    if (rnd.Next(0, 100) >= 50)
                        contactPhones.Add(new(_names[new Random().Next(0, _names.Length)], "+" + phoneForContact));

                await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\contacts.vcf", ContactManager.Export(contactPhones));

                foreach (var device in devices)
                {
                    var client = new Client(deviceId: device.Index);
                    await client.ImportContacts($@"{Globals.TempDirectory.FullName}\contacts.vcf");
                    _warmPhones.Add(client);

                    tasks.Add(Task.Run(async () => await WarmMethodIlya(string.IsNullOrEmpty(message) ? Array.Empty<string>() : message.Split('\n'), id, client, true)));
                    await Task.Delay(1_500);
                }
                _lock1 = new();

                File.Delete($@"{Globals.TempDirectory.FullName}\contacts.vcf");

                Task.WaitAll(tasks.ToArray(), -1);//Ждем заполнения устройств аккаунтами... Теперь другое, ждем когда всем аккам установим приветственное сообщение
                DashboardView.GetInstance().CompletedTasks = -1;

                DashboardView.GetInstance().DeniedTasksStart = 0;
                DashboardView.GetInstance().DeniedTasksWork = 0;

                _usedPhones.Clear();
                tasks.Clear();

                if (_accountsNotFound)
                    return;

                for(var i = 0; i < Globals.Setup.CountThreads; i++)
                {
                    tasks.Add(Task.Run(async () => await WarmMethodIlya(string.IsNullOrEmpty(message) ? Array.Empty<string>() : message.Split('\n'), id, null, false)));
                    await Task.Delay(1_500);
                }

                var _usedPhonesWelcome = new Dictionary<string, bool>();
                Log.Write($"[I] - Количество подготовленных устройств {_warmPhones.Count}\n", _logFile.FullName);
                foreach (var _chatbot in _warmPhones.ToArray())
                {
                    tasks.Add(Task.Run(async() =>
                    {
                        var chatbot = _chatbot;
                        var (phone, path) = ("", "");
                        while(!IsStop && !_accountsNotFound)
                        {
                            try
                            {
                                    var result = Globals.GetAccounts(_usedPhonesWelcome.Keys.ToArray(), true, _lock1, pathToFolderAccounts: Globals.Setup.PathToFolderAccountsAdditional);
                                    if (result.Length < 1)
                                    {
                                        if (_usedPhonesWelcome.Count == 0)
                                        {
                                            Log.Write($"[I] [{chatbot.GetInstance().Index}] - аккаунт не был найден для хандлера\n", _logFile.FullName);
                                            _accountsNotFound = true;
                                            return;
                                        }

                                        _usedPhonesWelcome = _usedPhonesWelcome.ToArray().Where(phone => phone.Value).ToDictionary(phone => phone.Key, phone => phone.Value);//P-s хз, может сча будет работать в многопоток
                                        continue;
                                    }
                                    (phone, path) = result[0];
                                    Log.Write($"[I] [{chatbot.GetInstance().Index}] - взяли аккаунт {phone} {path}\n", _logFile.FullName);

                                    if (_usedPhonesWelcome.Keys.ToArray().Contains(phone))
                                    {
                                        Log.Write($"[I] [{chatbot.GetInstance().Index}] - дубликат аккаунта\n", _logFile.FullName);
                                        continue;
                                    }

                                    _usedPhonesWelcome[phone] = true;

                                if (!await TryLogin(chatbot, phone, path))
                                {
                                    Log.Write($"[{phone}] [{chatbot.GetInstance().Index}] - не смогли войти\n", _logFile.FullName);
                                    _usedPhonesWelcome[phone] = false;
                                    continue;
                                }
                                else
                                    Log.Write($"[{phone}] [{chatbot.GetInstance().Index}] - смогли войти\n", _logFile.FullName);

                                await Task.Delay((Globals.Setup.TakeCountRandomAccountDelay ?? 1) * 1000);
                                if (!await chatbot.IsValid())
                                {
                                 Log.Write($"[IsValid WELCOME] [{chatbot.GetInstance().Index}] - Считаю аккаунт не валидным и кидаю в чс\n", _logFile.FullName);
                                    await DeleteAccount(chatbot, false);
                                }
                                _usedPhonesWelcome[phone] = false;
                            }
                            catch (Exception ex)
                            {
                                Log.Write($"[CRITICAL - HANDLER WELCOME] [{chatbot.GetInstance().Index}] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
                                _usedPhonesWelcome[phone] = false;
                            }
                        }
                    }));
                    await Task.Delay(1_500);
                }

                Task.WaitAll(tasks.ToArray(), -1);//Ждем окочания переписок...
                DashboardView.GetInstance().CompletedTasks = 0;

                File.WriteAllLines(Globals.Setup.PathToFilePhones, _contacts.Where(cont => !_usedPhonesUsers.Contains(cont)));
                tasks.Clear();*/
            })}, -1);
        }
        //Способ через оптравку одного сообщения и одного диалога на каждом из акков запущенных на эмулях. Нужен фикс
        /*
        if (_currentProfile.WarmMethodIlya)
        {
            _directoryAccounts = new DirectoryInfo($@"{Globals.WarmedDirectory}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
            _directoryAccounts.Create();

            TemplateWarm.warmedAccount = 0;
            TemplateWarm.sendedCount = 0;
            TemplateWarm.sendedBan = 0;
            TemplateWarm.sendedBanPeople = 0;

            var tasks = new List<Task>();
            var contactPhones = new List<CObj>();
            var rnd = new Random();
            var id = 0;//Just for groups

            DataEmulator[] devices = null;
            devices = ManagerView.GetInstance().Emulators.Where(device => !busyDevices.Select(device => device.Key.Index).Contains(device.Index) && device.IsEnabled).ToArray();

            if (devices.Length == 0)
            {
                Log.Write($"[I] - Больше девайсов не найдено, остаточное кол-во: {devices.Length}\n", _logFile.FullName);
                return;
            }

            foreach (var device in devices)
                busyDevices[device] = id;

            foreach (var phoneForContact in Directory.GetDirectories(Globals.Setup.PathToFolderAccounts).Select(dir => new DirectoryInfo(dir).Name))
                contactPhones.Add(new(_names[new Random().Next(0, _names.Length)], "+" + phoneForContact));

            await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\contacts.vcf", ContactManager.Export(contactPhones));
            foreach (var device in devices)
            {
                var client = new Client(deviceId: device.Index);
                await client.ImportContacts($@"{Globals.TempDirectory.FullName}\contacts.vcf");
                _warmPhones[client] = false;
            }

            while (!IsStop)
            {
                foreach (var device in _warmPhones.Keys)
                {
                    tasks.Add(Task.Run(async () => await WarmMethodValera2(string.IsNullOrEmpty(message) ? Array.Empty<string>() : message.Split('\n'), id, device)));
                    await Task.Delay(1_500);
                }

                Task.WaitAll(tasks.ToArray(), -1);//Ждем завершения
                tasks.Clear();
                
                if (_accountsNotFound)
                    break;
            }

            File.Delete($@"{Globals.TempDirectory.FullName}\contacts.vcf");

            _usedPhones.Clear();
            _warmPhones.Clear();
        }*/

        if (_currentProfile.WarmMethodValera)
        {
            /*for (var repeatId = 0; repeatId < Globals.Setup.RepeatCounts; repeatId++)
            {
                for (var groupId = 0; groupId < ((_currentProfile.CheckBan || _currentProfile.CheckNumberValid) ? 1 : Globals.Setup.CountGroups); groupId++)
                {
                    var id = groupId;
                    await Task.Delay(100);
                    mainTasks.Add(Task.Run(async () =>
                    {
                        lock (_lock)
                        {
                            var devices = ManagerView.GetInstance().Emulators.Where(device => !busyDevices.Select(device => device.Key.Index).Contains(device.Index) && device.IsEnabled).Take(Globals.Setup.CountThreads.Value).ToArray();
                            if (devices.Length != (int)Globals.Setup.CountThreads)
                            {
                                Log.Write($"[I] - Больше девайсов не найдено, остаточное кол-во: {devices.Length}\n", _logFile.FullName);
                                return;
                            }
                            
                            _tetheredDevices[id] = new List<Client>();
                            _readyPhones[id] = new List<Client>();

                            foreach (var device in devices)
                            {
                                busyDevices[device] = id;
                                _tetheredDevices[id].Add(new Client(deviceId: device.Index));
                            }
                    }
                    }));
                }
            }*/


            for (var repeatId = 0; repeatId < Globals.Setup.RepeatCounts; repeatId++)
            {
                Log.Write($"[I] - Круг: {repeatId}\n", _logFile.FullName);
                for (var groupId = 0; groupId < Globals.Setup.CountGroups; groupId++)
                {
                    var id = groupId;
                    await Task.Delay(100);
                    mainTasks.Add(Task.Run(async () =>
                    {
                        lock (_lock)
                        {
                            var devices = ManagerView.GetInstance().Emulators.Where(device => !busyDevices.Select(device => device.Key.Index).Contains(device.Index) && device.IsEnabled).Take(Globals.Setup.CountThreads.Value).ToArray();
                            if (devices.Length != (int)Globals.Setup.CountThreads)
                            {
                                Log.Write($"[I] - Больше девайсов не найдено, остаточное кол-во: {devices.Length}\n", _logFile.FullName);
                                return;
                            }

                            _tetheredDevices[id] = new List<Client>();
                            _readyPhones[id] = new List<Client>();

                            foreach (var device in devices)
                            {
                                busyDevices[device] = id;
                                _tetheredDevices[id].Add(new Client(deviceId: device.Index));
                            }
                        }

                        await HandlerArtemiy2(message.Split('\n'), id);
                    }));
                }

                Task.WaitAll(mainTasks.ToArray(), -1);

                busyDevices.Clear();
            }

            _usedPhones.Clear();
        }

        if (!_currentProfile.WarmMethodIlya && !_currentProfile.WarmMethodValera)
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

                            _readyPhones[id] = new List<Client>();
                            var lastCountThreads = 0;

                            foreach (var device in devices)
                            {
                                var client = new Client(deviceId: device.Index);
                                _readyPhones[id].Add(client);
                                tasks.Add(Handler(string.IsNullOrEmpty(message) ? Array.Empty<string>() : message.Split('\n'), id, client));
                                while (lastCountThreads == _activePhones.Count(phone => phone[0].ToString() == id.ToString()))
                                {
                                    if (_accountsNotFound || IsStop)
                                        break;

                                    await Task.Delay(500);
                                }

                                lastCountThreads = _activePhones.Count(phone => phone[0].ToString() == id.ToString());
                            }

                            Task.WaitAll(tasks.ToArray(), -1);

                            _readyPhones[id].Clear();
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
        _tetheredDevices.Clear();
    }

    private async Task WarmMethodIlya(string[] messages, int threadId, Client client, bool isFeel)
    {
        try
        {
            Log.Write($"Поток {threadId} запущен\n", _logFile.FullName);
            if (isFeel)
            {
                await client.GetInstance().ShellCmd("settings put global window_animation_scale 0");
                await client.GetInstance().ShellCmd("settings put global transition_animation_scale 0");
                await client.GetInstance().ShellCmd("settings put global animator_duration_scale 0");
            }

        //await client.GetInstance().ClearContacts();
        getAccount:
            if (IsStop || _accountsNotFound)
                return;

            var (phone, path) = ("", "");

            lock (_lock)
            {
                var result = Globals.GetAccounts(_usedPhones.Select(phone => phone.Key.Remove(0, 1)).ToArray(), true, _lock, pathToFolderAccounts: isFeel ? Globals.Setup.PathToFolderAccountsAdditional : Globals.Setup.PathToFolderAccounts);

                DashboardView.GetInstance().AllTasks = Directory.GetDirectories(isFeel ? Globals.Setup.PathToFolderAccountsAdditional : Globals.Setup.PathToFolderAccounts).Where(dir => File.Exists($@"{dir}\Data.json") && !_usedPhones.ContainsKey(dir)).ToArray().Length;

                if (result.Length < 1)
                {
                    if (_usedPhones.Count == 0)
                    {
                        Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                        _accountsNotFound = true;
                        return;
                    }

                    if (isFeel)
                        return;

                    _usedPhones = _usedPhones.Where(phone => phone.Value).ToDictionary(phone => phone.Key, phone => phone.Value);//P-s хз, может сча будет работать в многопоток
                    goto getAccount;
                }

                (phone, path) = result[0];
                Log.Write($"[I] - взяли аккаунт {phone} {path}\n", _logFile.FullName);

                if (_usedPhones.Select(phone => phone.Key.Remove(0, 1)).Contains(phone))
                {
                    Log.Write($"[I] - дубликат аккаунта\n", _logFile.FullName);
                    goto getAccount;
                }

                _usedPhones[threadId + phone] = true;
            }

            if (isFeel)
            {
                if (!await HandlerWelcomeAccount(client, phone, path, messages, threadId))//await HandlerWelcomeAccount(client, phone, path, messages, threadId);
                    goto getAccount;
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
                    if (ex.Message != "Cant load account")
                    {
                        _usedPhones[threadId + phone] = false;
                        await Globals.TryMove(path, $@"{Globals.LogoutDirectory.FullName}\{phone}");
                        Log.Write($"[{phone}] - не смогли войти: {ex.Message}\n", _logFile.FullName);

                        ++DashboardView.GetInstance().DeniedTasks;
                        ++DashboardView.GetInstance().DeniedTasksStart;
                    }
                    else
                        _usedPhones.Remove(threadId + phone);

                    goto getAccount;
                }

                var stepBlock = 0;

                try
                {
                    while (true)
                    {
                        if (!await client.Web!.IsConnected())
                            break;

                        //var arr = Directory.GetDirectories(Globals.Setup.PathToFolderAccountsAdditional).Select(dir => new DirectoryInfo(dir).Name).OrderBy(x => new Random().Next()).Take(Globals.Setup.CountMessageWarm ?? 0).ToArray();

                        //if (!arr.Any())
                           // break;
                        
                        stepBlock = 1;

                        foreach (var chatbot in _warmPhones.ToArray())
                        {
                            while (!await client.Web!.SendText(chatbot.Phone.Remove(0, 1), messages[new Random().Next(0, messages.Length - 1)]))
                                await Task.Delay(1_000);

                            await Task.Delay(new Random().Next((int)Math.Floor((float)Globals.Setup.DelaySendMessageFrom * 1000f), (int)Math.Floor((float)Globals.Setup.DelaySendMessageTo * 1000f)));
                        }

                        /*if (arr.Length != Globals.Setup.CountMessageWarm)
                            break;*/

                        stepBlock = 2;

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

                        break;
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
                        //Убери коменты если надо чтобы по окончанию прогрева оно их перемещало
                        await Globals.TryMove(path, $@"{_directoryAccounts.FullName}\{phone}");
                        ++TemplateWarm.warmedAccount;
                    }
                    _usedPhones[threadId + phone] = false;
                    goto getAccount;

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
                }
                catch (Exception ex)
                {
                    _usedPhones[threadId + phone] = false;
                    Log.Write($"[Handler] - крит. ошибка: {ex.Message}\n", _logFile.FullName);
                    await client.Web!.Free();
                    ++DashboardView.GetInstance().DeniedTasks;

                    switch (stepBlock)
                    {
                        case 1:
                            ++TemplateWarm.sendedBan;
                            break;
                        case 2:
                            ++TemplateWarm.sendedBanPeople;
                            break;
                        default:
                            ++DashboardView.GetInstance().DeniedTasksWork;
                            break;
                    }

                    await Globals.TryMove(path, $@"{Globals.WebBanWorkDirectory.FullName}\{phone}");
                }

                _usedPhones[threadId + phone] = false;
                goto getAccount;
            }
        }
        catch (Exception ex)
        {
            Log.Write($"[CRITICAL] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
        }
    }

    //Способ Ильи с загрузкой акков, тобишь загружаем каждый раз акк, ждем 5 сек условно говоря и он отправляет велкам текст
    /*private async Task WarmMethodIlya(string[] messages, int threadId, Client client, bool isFeel)
    {
        var (phone, path) = ("", "");
        try
        {
            Log.Write($"Поток {threadId} запущен\n", _logFile.FullName);
            if (isFeel)
            {
                await client.GetInstance().ShellCmd("settings put global window_animation_scale 0");
                await client.GetInstance().ShellCmd("settings put global transition_animation_scale 0");
                await client.GetInstance().ShellCmd("settings put global animator_duration_scale 0");
            }

        //await client.GetInstance().ClearContacts();
        getAccount:
            if (IsStop || _accountsNotFound)
                return;

            (phone, path) = ("", "");

            lock (_lock)
            {
                var result = Globals.GetAccounts(_usedPhones.Select(phone => phone.Key.Remove(0, 1)).ToArray(), true, _lock, pathToFolderAccounts: isFeel ? Globals.Setup.PathToFolderAccountsAdditional : Globals.Setup.PathToFolderAccounts);

                DashboardView.GetInstance().AllTasks = Directory.GetDirectories(isFeel ? Globals.Setup.PathToFolderAccountsAdditional : Globals.Setup.PathToFolderAccounts).Where(dir => File.Exists($@"{dir}\Data.json") && !_usedPhones.ContainsKey(dir)).ToArray().Length;

                if (result.Length < 1)
                {
                    if (_usedPhones.Count == 0)
                    {
                        Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                        _accountsNotFound = true;
                        return;
                    }

                    if (isFeel)
                        return;

                    _usedPhones = _usedPhones.Where(phone => phone.Value).ToDictionary(phone => phone.Key, phone => phone.Value);//P-s хз, может сча будет работать в многопоток
                    goto getAccount;
                }

                (phone, path) = result[0];
                Log.Write($"[I] - взяли аккаунт {phone} {path}\n", _logFile.FullName);

                if (_usedPhones.Select(phone => phone.Key.Remove(0, 1)).Contains(phone))
                {
                    Log.Write($"[I] - дубликат аккаунта\n", _logFile.FullName);
                    goto getAccount;
                }

                _usedPhones[threadId + phone] = true;
            }

            if (isFeel)
            {
                await HandlerWelcomeAccount(client, phone, path, messages, threadId);//if (!await HandlerWelcomeAccount(client, phone, path, messages, threadId))
                goto getAccount;
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
                    if (ex.Message != "Cant load account")
                    {
                        _usedPhones[threadId + phone] = false;
                        await Globals.TryMove(path, $@"{Globals.LogoutDirectory.FullName}\{phone}");
                        Log.Write($"[{phone}] - не смогли войти: {ex.Message}\n", _logFile.FullName);

                        ++DashboardView.GetInstance().DeniedTasks;
                        if (client.AccountData.CountMessages > 0)
                            ++DashboardView.GetInstance().DeniedTasksWork;
                        else
                            ++DashboardView.GetInstance().DeniedTasksStart;
                    }
                    else
                        _usedPhones.Remove(threadId + phone);

                    goto getAccount;
                }

                var stepBlock = 0;

                try
                {
                    while (true)
                    {
                        if (!await client.Web!.IsConnected())
                            break;

                        var arr = Directory.GetDirectories(Globals.Setup.PathToFolderAccountsAdditional).Select(dir => new DirectoryInfo(dir).Name).OrderBy(x => new Random().Next()).Take(Globals.Setup.CountMessageWarm ?? 0).ToArray();

                        if (!arr.Any())
                            break;

                        stepBlock = 1;

                        if (arr.Length != (Globals.Setup.CountMessageWarm ?? 1))
                        {
                            _accountsNotFound = true;
                            Log.Write($"[{phone}] - аккаунтов ботов меньше заявленного (требуется >= {Globals.Setup.CountMessageWarm ?? 0}, имеется {arr.Length})\n", _logFile.FullName);
                            break;
                        }

                        foreach (var chatbot in arr)
                        {
                            while (!await client.Web!.SendText(chatbot, messages[new Random().Next(0, messages.Length - 1)]))
                                await Task.Delay(1_000);

                            await Task.Delay(new Random().Next((int)Math.Floor((float)Globals.Setup.DelaySendMessageFrom * 1000f), (int)Math.Floor((float)Globals.Setup.DelaySendMessageTo * 1000f)));
                        }

                        stepBlock = 2;

                        for (var i = 0; i < Globals.Setup.CountMessageWarmNewsletter; i++)
                        {
                        tryGetFreePhone:
                            if (!await client.Web!.IsConnected())
                                break;

                            var freePhone = GetFreeNumberUser();

                            if (string.IsNullOrEmpty(freePhone))
                                break;

                            if (await client.Web!.CheckValidPhone(freePhone))
                            {
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
                            else
                                goto tryGetFreePhone;

                            await Task.Delay(new Random().Next((int)Math.Floor((float)Globals.Setup.DelaySendMessageFrom * 1000f), (int)Math.Floor((float)Globals.Setup.DelaySendMessageTo * 1000f)));
                        }

                        break;
                    }

                    client.AccountData.CountMessages += Globals.Setup.CountMessageWarmNewsletter ?? 0;
                    await Task.Delay(2_000);

                    if (!await client.Web!.IsConnected())
                    {
                        await client.Web!.Free();
                        await client.UpdateData(false);
                        await Globals.TryMove(path, $@"{Globals.WebBanWorkDirectory.FullName}\{phone}");
                        ++DashboardView.GetInstance().DeniedTasks;
                        ++DashboardView.GetInstance().DeniedTasksWork;
                    }
                    else
                    {
                        await client.Web!.Free();
                        client.AccountData.BannedDate = DateTime.Now;
                        await client.UpdateData(false);
                        if (!Globals.Setup.LongWarmSlaughter)
                        {
                            await Globals.TryMove(path, $@"{_directoryAccounts.FullName}\{phone}");
                            ++TemplateWarm.warmedAccount;
                        }
                    }

                    _usedPhones[threadId + phone] = false;
                    goto getAccount;
                }
                catch (Exception ex)
                {
                    Log.Write($"[Handler] - крит. ошибка: {ex.Message}\n", _logFile.FullName);
                    await client.Web!.Free();
                    ++DashboardView.GetInstance().DeniedTasks;

                    switch (stepBlock)
                    {
                        case 1:
                            ++TemplateWarm.sendedBan;
                            break;
                        case 2:
                            ++TemplateWarm.sendedBanPeople;
                            break;
                        default:
                            ++DashboardView.GetInstance().DeniedTasksWork;
                            break;
                    }

                    await Globals.TryMove(path, $@"{Globals.WebBanWorkDirectory.FullName}\{phone}");
                }

                _usedPhones[threadId + phone] = false;
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
        }
        catch (Exception ex)
        {
            _usedPhones[threadId + phone] = false;
            Log.Write($"[CRITICAL] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
        }
    }*/

    /*private async Task WarmMethodValera2(string[] messages, int threadId, Client client)
    {
        var (phone, path) = ("", "");
        try
        {
            Log.Write($"Поток {threadId} запущен\n", _logFile.FullName);
            await client.GetInstance().ShellCmd("settings put global window_animation_scale 0");
            await client.GetInstance().ShellCmd("settings put global transition_animation_scale 0");
            await client.GetInstance().ShellCmd("settings put global animator_duration_scale 0");

        getAccount:
            if (IsStop || _accountsNotFound)
                return;

            (phone, path) = ("", "");

            lock (_lock)
            {
                var result = Globals.GetAccounts(_usedPhones.Select(phone => phone.Key.Remove(0, 1)).ToArray(), true, _lock, pathToFolderAccounts: Globals.Setup.PathToFolderAccounts);

                DashboardView.GetInstance().AllTasks = Directory.GetDirectories(Globals.Setup.PathToFolderAccounts).Where(dir => File.Exists($@"{dir}\Data.json") && !_usedPhones.ContainsKey(dir)).ToArray().Length;

                if (result.Length < 1)
                {
                    Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                    _accountsNotFound = true;
                    return;
                }

                (phone, path) = result[0];
                Log.Write($"[I] - взяли аккаунт {phone} {path}\n", _logFile.FullName);

                if (_usedPhones.Select(phone => phone.Key.Remove(0, 1)).Contains(phone))
                {
                    Log.Write($"[I] - дубликат аккаунта\n", _logFile.FullName);
                    goto getAccount;
                }
            }

            var rnd = new Random();

            if (!await TryLogin(client, phone, path))
            {
                ++DashboardView.GetInstance().DeniedTasks;
                ++DashboardView.GetInstance().DeniedTasksStart;
                Log.Write($"[{phone}] - не смогли войти\n", _logFile.FullName);
                goto getAccount;
            }
            else
                Log.Write($"[{phone}] - смогли войти\n", _logFile.FullName);

            await client.GetInstance().StopApk(client.PackageName);
            await client.GetInstance().RunApk(client.PackageName);
            await Task.Delay(1_000);

            if (!await client.IsValid())
            {
                ++DashboardView.GetInstance().DeniedTasks;
                ++DashboardView.GetInstance().DeniedTasksStart;
                await DeleteAccount(client);
                goto getAccount;
            }

            _usedPhones[threadId + phone] = true;
            _warmPhones[client] = true;
            Log.Write($"[I] - пидарас {_warmPhones[client]} {_warmPhones.Count}\n", _logFile.FullName);

            while (true)
            {
                if (_warmPhones.All(device => device.Value))
                    break;

                if (_accountsNotFound)
                    return;

                await Task.Delay(500);
            }

            foreach (var contact in _warmPhones.Keys.ToArray().Where(device => device.Phone != client.Phone))
            {
                if (await client.SendPreMessage(contact.Phone, messages[new Random().Next(0, messages.Length - 1)], waitDelivered: true) != Client.StatusDelivered.Delivered)
                {
                    if (await client.IsValid())
                        continue;
                    else
                        break;
                }
            }

            _usedPhones[threadId + phone] = false;
            _warmPhones[client] = false;
        }
        catch (Exception ex)
        {
            _usedPhones[threadId + phone] = false;
            _warmPhones[client] = false;
            Log.Write($"[CRITICAL] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
        }
    }*/

    public async Task<bool> HandlerWelcomeAccount(Client client, string phone, string path, string[] messages, int threadId)
    {
        var rnd = new Random();

        if (!await TryLogin(client, phone, path))
        {
            ++DashboardView.GetInstance().DeniedTasks;
            ++DashboardView.GetInstance().DeniedTasksStart;
            Log.Write($"[{phone}] - не смогли войти\n", _logFile.FullName);
            await DeleteAccount(client);
            return false;
            //goto getAccount;
        }
        else
            Log.Write($"[{phone}] - смогли войти\n", _logFile.FullName);

        await client.GetInstance().StopApk(client.PackageName);
        await client.GetInstance().RunApk(client.PackageName);
        await Task.Delay(1_000);

        if (!await client.IsValid())
        {
            ++DashboardView.GetInstance().DeniedTasks;
            ++DashboardView.GetInstance().DeniedTasksStart;
            await DeleteAccount(client);
            return false;
            //goto getAccount;
            //return;
        }

        try
        {
            if (!await MoveToWelcomeMessage(client, messages.Length > 0 ? messages[rnd.Next(0, messages.Length - 1)] : string.Empty))
            {
                if (!await client.IsValid())
                {
                    ++DashboardView.GetInstance().DeniedTasks;
                    ++DashboardView.GetInstance().DeniedTasksWork;
                    await DeleteAccount(client);
                    return false;
                    //goto getAccount;
                }
                else
                {
                    _usedPhones.Remove(threadId + phone);
                    Log.Write($"[Handler - WelcomeMessage] - Аккаунт возвращен в очередь\n", _logFile.FullName);
                    return false;
                    //goto getAccount;
                }
            }
            else
            {
                await client.UpdateData(true);
                ++DashboardView.GetInstance().CompletedTasks;
            }
        }
        catch (Exception ex)
        {
            _usedPhones.Remove(threadId + phone);
            Log.Write($"[Handler - WelcomeMessage] - Произошла ошибка, аккаунт возвращен в очередь: {ex.Message}\n", _logFile.FullName);
            return false;
            //goto getAccount;
        }

        return true;
    }

    private async Task HandlerArtemiy2(string[] messages, int threadId)
    {
    getAccount:
        if (IsStop)
            return;

        var (phone, path) = ("", "");

        lock (_lock)
        {
            var usedphones = _usedPhones.Select(phone => phone.Key.Remove(0, 1)).ToList();

            foreach(var client in _readyPhones[threadId])
                usedphones.AddRange(client.AccountData.MessageHistory.Keys);

            var result = Globals.GetAccounts(usedphones.ToArray(), true, _lock, _readyPhones[threadId].Select(_client => _client.Phone.Replace("+", "")).ToArray());

            if (result.Length == 0)
            {
                Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                _accountsNotFound = true;
                return;
            }

            (phone, path) = result[0];

            if (_usedPhones.Select(phone => phone.Key.Remove(0, 1)).ToArray().Contains(phone))
            {
                Log.Write($"[I] - дубликат аккаунта\n", _logFile.FullName);
                goto getAccount;
            }

            _usedPhones.Add(threadId + phone, false);
        }

        var _client = GetFreeDevice();

        if (_client != null)
        {
            await _client.GetInstance().ShellCmd("settings put global window_animation_scale 0");
            await _client.GetInstance().ShellCmd("settings put global transition_animation_scale 0");
            await _client.GetInstance().ShellCmd("settings put global animator_duration_scale 0");

            await _client.GetInstance().RunApk("net.sourceforge.opencamera");
            await _client.GetInstance().StopApk("net.sourceforge.opencamera");

            await _client.GetInstance().ClearContacts();

            if (!await TryLogin(_client, phone, path))
            {
                ++DashboardView.GetInstance().DeniedTasks;
                ++DashboardView.GetInstance().DeniedTasksStart;
                Log.Write($"[{phone}] - не смогли войти\n", _logFile.FullName);
                goto getAccount;
            }
            else
                Log.Write($"[{phone}] - смогли войти\n", _logFile.FullName);

            _readyPhones[threadId].Add(_client);

            if (_readyPhones[threadId].Count < Globals.Setup.CountThreads)
                goto getAccount;
        }

        var StopCurrentThread = false;

        var phones = _readyPhones[threadId].Select(device => device.Phone).ToArray();

        var contactPhoness = new List<CObj>();
        foreach (var phoneForContact in phones)
            contactPhoness.Add(new(MemuLib.Globals.RandomString(new Random().Next(3, 10), true).ToLower()/*_names[new Random().Next(0, _names.Length)]*/, phoneForContact));

        foreach (var client in _readyPhones[threadId])
        {
            await client.GetInstance().StopApk(client.PackageName);
            var contacts = contactPhoness.Where(contact => contact.NumberPhone != client.Phone).ToList();
            
            foreach (var contact in client.AccountData.MessageHistory.Keys.Select(phone => $"+{phone}").ToArray())
                contacts.Add(new(MemuLib.Globals.RandomString(new Random().Next(3, 10), true).ToLower(), contact));

            await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf", ContactManager.Export(contacts));
            await client.ImportContacts($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf");
            await Task.Delay(1_000);
            await client.GetInstance().RunApk(client.PackageName);
        }

        File.Delete($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf");

        try
        {
            if (_accountsNotFound || IsStop)
                return;

            if (Directory.Exists(Globals.Setup.PathToFolderWarmFiles))
            {
                foreach (var client in _readyPhones[threadId])
                {
                againTry:
                    if (StopCurrentThread)
                        break;

                    if (!await client.IsValid() || IsStop)
                    {
                        if (!IsStop)
                            StopCurrentThread = true;

                        break;
                    }

                    if (!await SetStatus(client))
                        goto againTry;
                }
            }

            await Task.Delay((Globals.Setup.DelayFirstMessageAccount ?? 0) * 1000);
            
            if (IsStop)
                return;

            for (var i = 0; i < Globals.Setup.CountMessages; i++)
            {
                if (StopCurrentThread)
                    break;

                var currentThreads = new List<Task>();

                foreach (var clientTemp in _readyPhones[threadId])
                {
                    var client = clientTemp;
                    currentThreads.Add(Task.Run(async () =>
                    {
                        if (!await client.IsValid() || IsStop)
                        {
                            if (!IsStop)
                                StopCurrentThread = true;

                            return;
                        }

                        if (Directory.Exists(Globals.Setup.PathToFolderWarmFiles))
                        {
                            //Блять, что за хуйню я написал ниже... Ебана в рот...
                            foreach (var warmPhone in _readyPhones[threadId].Where(otherClient => otherClient.Phone != client.Phone))
                            {
                                int[] arrfuck = new[] { 1, 2, 3, 4, 5 }.OrderBy(x => new Random().Next()).ToArray();
                                for (var x = 0; x < ((i == 0) ? 5 : new Random().Next(3, 5)); x++)
                                {
                                    switch (arrfuck[x])
                                    {
                                        case 1:
                                            await SendTextMessage(client, warmPhone.Phone);
                                            break;
                                        case 2:
                                            await SendMusicMessage(client, warmPhone.Phone);
                                            break;
                                        case 3:
                                            await SendFileMessage(client, warmPhone.Phone);
                                            break;
                                        case 4:
                                            await SendImageMessage(client, warmPhone.Phone);
                                            break;
                                        case 5:
                                            await SendVoiceMessage(client, warmPhone.Phone, new Random().Next(11, 21), new Random().Next(22, 41));
                                            break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            foreach (var warmPhone in _readyPhones[threadId].Where(otherClient => otherClient.Phone != client.Phone))
                            {
                            again:
                                if (!await SendTextMessage(client, warmPhone.Phone))
                                    if (!await client.IsValid())
                                        break;
                                    else
                                        goto again;
                            }
                        }
                    }));
                }

                Task.WaitAll(currentThreads.ToArray());
            }

            if (Directory.Exists(Globals.Setup.PathToFolderWarmFiles))
            {
                foreach (var client in _readyPhones[threadId])
                {
                againTry:
                    if (StopCurrentThread)
                        break;

                    if (!await client.IsValid() || IsStop)
                    {
                        if (!IsStop)
                            StopCurrentThread = true;

                        break;
                    }

                    foreach (var warmPhone in _readyPhones[threadId].Where(user => user.Phone != client.Phone))
                        if (!await SendCall(client, warmPhone, new Random().Next(60, 120), false, true))
                            goto againTry;
                }
            }

            Log.Write("Try update info", _logFile.FullName);

            foreach (var client in _readyPhones[threadId])
            {
                foreach (var phoneClient in _readyPhones[threadId].Where(_client => _client.Phone.Replace("+", "") != client.Phone.Replace("+", "")).Select(_client => _client.Phone.Replace("+", "")).ToArray())
                    client.AccountData.MessageHistory[phoneClient] = DateTime.Now;

                await client.UpdateData(true);
            }

            Log.Write("Try unload", _logFile.FullName);

            foreach (var client in _readyPhones[threadId])
            {
                if (!await client.IsValid())
                {
                    await Globals.TryMove(path, $@"{Globals.BanWorkDirectory.FullName}\{phone}");
                    ++DashboardView.GetInstance().DeniedTasks;
                    ++DashboardView.GetInstance().DeniedTasksWork;
                }
                else if (!StopCurrentThread)
                {
                    ++client.AccountData.TrustLevelAccount;
                    ++DashboardView.GetInstance().CompletedTasks;
                }

                await client.UpdateData(true);
            }

            Log.Write("KK", _logFile.FullName);

            _readyPhones[threadId].Clear();
            foreach (var _device in _usedPhones.Keys.Where(_phone => _phone[0].ToString() == threadId.ToString()))
                _usedPhones.Remove(_device);

            Log.Write("KK x2", _logFile.FullName);
        }
        catch (Exception ex)
        {
            Log.Write($"[Handler] - крит. ошибка: {ex.Message}\n", _logFile.FullName);
            foreach (var client in _readyPhones[threadId])
            {
                if (!await client.IsValid())
                {
                    await Globals.TryMove(path, $@"{Globals.WebBanWorkDirectory.FullName}\{phone}");
                    ++DashboardView.GetInstance().DeniedTasks;
                    ++DashboardView.GetInstance().DeniedTasksWork;
                }
            }

            _readyPhones[threadId].Clear();
            foreach (var _device in _usedPhones.Keys.Where(_phone => _phone[0].ToString() == threadId.ToString()))
                _usedPhones.Remove(_device);
        }

        async Task<bool> SendTextMessage(Client client, string phone)
        {
        tryAgain:
            if ((await client.SendPreMessage(phone[0] != '+' ? $"+{phone}" : phone, MemuLib.Globals.RandomEmoji(new Random().Next(3, 12)) + messages[new Random().Next(0, messages.Length - 1)].Replace("\"", "").Replace("\'", "") + MemuLib.Globals.RandomEmoji(new Random().Next(2, 10)), true)) != Client.StatusDelivered.Delivered)
                if (!await client.IsValid())
                    return false;
                else
                    goto tryAgain;

            await Task.Delay(new Random().Next((int)Globals.Setup.DelaySendMessageFrom * 1000, (int)Globals.Setup.DelaySendMessageTo * 1000));
            return true;
        }

        /*async Task<bool> SendGifMessage(Client client, string phone)
        {
            if ((await client.SendMessage(phone[0] != '+' ? $"+{phone}" : phone, messages[new Random().Next(0, messages.Length - 1)].Replace("\"", "").Replace("\'", ""), await GetRandomFile(new string[] { ".gif" }), true)) != Client.StatusDelivered.Delivered)
                return false;

            await Task.Delay(new Random().Next((int)Globals.Setup.DelaySendMessageFrom * 1000, (int)Globals.Setup.DelaySendMessageTo * 1000));
            return true;
        }*/

        async Task<bool> SendFileMessage(Client client, string phone)
        {
        tryAgain:
            if ((await client.SendMessage(phone[0] != '+' ? $"+{phone}" : phone, MemuLib.Globals.RandomEmoji(new Random().Next(3, 12)) + messages[new Random().Next(0, messages.Length - 1)].Replace("\"", "").Replace("\'", "") + MemuLib.Globals.RandomEmoji(new Random().Next(2, 10)), await GetRandomFile(new string[] { ".txt", ".doc", ".rar", ".zip", ".7zip" }), true)) != Client.StatusDelivered.Delivered)
                if (!await client.IsValid())
                    return false;
                else
                    goto tryAgain;

            await Task.Delay(new Random().Next((int)Globals.Setup.DelaySendMessageFrom * 1000, (int)Globals.Setup.DelaySendMessageTo * 1000));
            return true;
        }

        async Task SendMusicMessage(Client client, string phone)
        {
            await client.SendMessage(phone[0] != '+' ? $"+{phone}" : phone, MemuLib.Globals.RandomEmoji(new Random().Next(3, 12)) + messages[new Random().Next(0, messages.Length - 1)].Replace("\"", "").Replace("\'", "") + MemuLib.Globals.RandomEmoji(new Random().Next(2, 10)), await GetRandomFile(new string[] { ".mp3", ".wav" }), false);
            await Task.Delay(new Random().Next((int)Globals.Setup.DelaySendMessageFrom * 1000, (int)Globals.Setup.DelaySendMessageTo * 1000));
        }

        async Task<bool> SendImageMessage(Client client, string phone)
        {
        tryAgain:
            if ((await client.SendMessage(phone[0] != '+' ? $"+{phone}" : phone, MemuLib.Globals.RandomEmoji(new Random().Next(3, 12)) + messages[new Random().Next(0, messages.Length - 1)].Replace("\"", "").Replace("\'", "") + MemuLib.Globals.RandomEmoji(new Random().Next(2, 10)), await GetRandomFile(new string[] { ".jpg", ".jpeg", ".png" }), true)) != Client.StatusDelivered.Delivered)
                if (!await client.IsValid())
                    return false;
                else
                    goto tryAgain;

            await Task.Delay(new Random().Next((int)Globals.Setup.DelaySendMessageFrom * 1000, (int)Globals.Setup.DelaySendMessageTo * 1000));
            return true;
        }

        async Task<bool> SendVoiceMessage(Client client, string phone, int delayFrom, int delayTo)
        {
        tryAgain:
            //Переходим в диалог
            await client.GetInstance().Shell($"am start -a android.intent.action.SEND -e jid '{phone.Replace("+", "")}@s.whatsapp.net' {client.PackageName}/com.whatsapp.Conversation");

            await Task.Delay(1_000);//Ждем-с
            var dump = await client.GetInstance().DumpScreen();
            if (!await client.GetInstance().ExistsElement($"resource-id=\"{client.PackageName}:id/voice_note_btn\"", dump))
                if (!await client.IsValid())
                    return false;
                else
                    goto tryAgain;

            await client.GetInstance().Swipe($"resource-id=\"{client.PackageName}:id/voice_note_btn\"", delay: new Random().Next(delayFrom, delayTo), dump: dump);

            await Task.Delay(new Random().Next((int)Globals.Setup.DelaySendMessageFrom * 1000, (int)Globals.Setup.DelaySendMessageTo * 1000));
            return true;
        }

        async Task<bool> SendCall(Client c1, Client c2, int delay, bool isLineBusy, bool IsAccept)
        {
            for (var i = 0; i < 3; i++)
            {
                //Переходим в диалог
                await c1.GetInstance().Shell($"am start -a android.intent.action.SEND -e jid '{c2.Phone.Replace("+", "")}@s.whatsapp.net' {c1.PackageName}/com.whatsapp.Conversation");

                await Task.Delay(1_000);//Ждем-с
                var dump = await c1.GetInstance().DumpScreen();
                if (!await c1.GetInstance().ExistsElement("content-desc=\"Звонок\"", dump))
                    continue;

                await c1.GetInstance().Click("content-desc=\"Звонок\"", dump);
                await Task.Delay(1_000);//Ждем-с

                dump = await c1.GetInstance().DumpScreen();
                if (!await c1.GetInstance().ExistsElement("text=\"Аудиозвонок\"", dump))
                    continue;

                await c1.GetInstance().Click("text=\"Аудиозвонок\"", dump);
                await Task.Delay(1_000);//Ждем-с

                dump = await c1.GetInstance().DumpScreen();
                if (!await c1.GetInstance().ExistsElement("text=\"Звонок\"", dump))
                    continue;

                await c1.GetInstance().Click("text=\"Звонок\"", dump);
                await Task.Delay(3_000);//Ждем-с

                if (!IsAccept)
                {
                    await Task.Delay(delay * 1000);//Ждем-с
                    if (isLineBusy)
                        await c1.GetInstance().Click("content-desc=\"Покинуть звонок\"");
                    else
                        await c2.GetInstance().ShellCmd("input tap 205 205");
                }
                else
                {
                    await c2.GetInstance().ShellCmd("input tap 405 205");
                    await Task.Delay(delay * 1000);//Ждем-с
                    await c1.GetInstance().Click("content-desc=\"Покинуть звонок\"");
                }
                return true;
            }

            return false;
        }

        async Task<bool> SetStatus(Client client)
        {
            for (var i = 0; i < 3; i++)
            {
                //Устанавливаем текстовый статус
                await client.GetInstance().Shell($"am start -a android.intent.action.SEND --es android.intent.extra.TEXT \"{MemuLib.Globals.RandomEmoji(new Random().Next(3, 10)) + MemuLib.Globals.RandomString(new Random().Next(10, 20), true).ToLower() + MemuLib.Globals.RandomEmoji(new Random().Next(3, 10))}\" -t text/plain {client.PackageName}/com.whatsapp.textstatuscomposer.TextStatusComposerActivity");

                await Task.Delay(1_000);//Ждем-с

                var dump = await client.GetInstance().DumpScreen();
                if (!await client.GetInstance().ExistsElement($"resource-id=\"{client.PackageName}:id/send\"", dump))
                    if (i == 2)
                        return false;
                    else
                        continue;

                await client.GetInstance().Click($"resource-id=\"{client.PackageName}:id/send\"", dump);

                await Task.Delay(1_000);//Ждем-с

                dump = await client.GetInstance().DumpScreen();
                if (await client.GetInstance().ExistsElement("resource-id=\"android:id/button1\"", dump))
                    await client.GetInstance().Click("resource-id=\"android:id/button1\"", dump);

                break;
            }

            //Переходим к фото статусу
            /*for (var i = 0; i < 3; i++)
            {
                while (Globals.QrCode != null)
                    await Task.Delay(1_000);

                await client.GetInstance().Shell($"am start -a android.intent.action.SEND {client.PackageName}/com.whatsapp.camera.CameraActivity");

                await Task.Delay(1_000);//Ждем-с

                var dump = await client.GetInstance().DumpScreen();
                if (!await client.GetInstance().ExistsElement("resource-id=\"{client.PackageName}:id/shutter\"", dump))
                    if (i == 2)
                        return false;
                    else
                        continue;

                await client.GetInstance().Click("resource-id=\"{client.PackageName}:id/shutter\"", dump);

                await Task.Delay(1_000);//Ждем-с

                dump = await client.GetInstance().DumpScreen();
                if (!await client.GetInstance().ExistsElement("resource-id=\"{client.PackageName}:id/send\"", dump))
                    if (i == 2)
                        return false;
                    else
                        continue;

                await client.GetInstance().Click("resource-id=\"{client.PackageName}:id/send\"", dump);

                await Task.Delay(1_000);//Ждем-с

                await client.GetInstance().Click("text=\"Мой статус\"");

                await Task.Delay(1_000);//Ждем-с

                dump = await client.GetInstance().DumpScreen();
                if (!await client.GetInstance().ExistsElement("resource-id=\"{client.PackageName}:id/send\"", dump))
                    if (i == 2)
                        return false;
                    else
                        continue;

                await client.GetInstance().Click("resource-id=\"{client.PackageName}:id/send\"", dump);

                break;
            }*/
            return true;
        }

        async Task<FileInfo?> GetRandomFile(string[] ext)
        {
            if (!Directory.Exists(Globals.Setup.PathToFolderWarmFiles))
                return null;

            var randomFiles = Directory.GetFiles(Globals.Setup.PathToFolderWarmFiles).OrderBy(x => new Random().Next()).ToArray();
            if (ext != null && ext.Length > 0)
                randomFiles = randomFiles.Where(filePath => ext.Contains(Path.GetExtension(filePath))).ToArray();

            return randomFiles.Length == 0 ? null : new FileInfo(randomFiles[0]);
        }

        Client? GetFreeDevice()
        {
            lock (_lock)
            {
                foreach (var client in _tetheredDevices[threadId])
                    if (!_readyPhones[threadId].Contains(client))
                        return client;

                return null;
            }
        }
    }

    private async Task HandlerArtemiy(string[] messages, int threadId)
    {
        var countTrys = 0;
        tryAgain:
        try
        {
            var (c1, c2) = (_tetheredDevices[threadId][0], _tetheredDevices[threadId][1]);
            var (c1Index, c2Index) = (_tetheredDevices[threadId][0].GetInstance().Index, _tetheredDevices[threadId][1].GetInstance().Index);

            await c1.GetInstance().RunApk("net.sourceforge.opencamera");
            await c2.GetInstance().RunApk("net.sourceforge.opencamera");
            await c1.GetInstance().StopApk("net.sourceforge.opencamera");
            await c2.GetInstance().StopApk("net.sourceforge.opencamera");

            var c1Auth = false;
            var c2Auth = false;

            Log.Write($"Поток {threadId} запущен\n", _logFile.FullName);

            while (!IsStop)
            {
                await c1.GetInstance().ClearContacts();
                await c2.GetInstance().ClearContacts();

                var (phone, path) = ("", "");
                var usedphones = _activePhones.Where(phone => phone[0].ToString() == threadId.ToString()).Select(phone => phone.Remove(0, 1)).ToList();

                //lock (_lock)
                //{
                if (c1Auth && _currentProfile.WarmMethodValera)
                    usedphones.AddRange(c1.AccountData.MessageHistory.Keys);

                var result = Globals.GetAccounts(_usedPhones.Select(phone => phone.Key).ToArray(), true, _lock, _currentProfile.WarmMethodValera ? usedphones.ToArray() : null);

                DashboardView.GetInstance().AllTasks = result.Length;

                if (result.Length < 1)
                {
                    Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                    break;
                }

                (phone, path) = result[0];

                if (_usedPhones.ContainsKey(phone))
                    continue;

                _usedPhones[phone] = true;
                //}

                if (!c1Auth)
                {
                    c1Auth = await TryLogin(c1, phone, path);

                    if (!c1Auth)
                    {
                        ++DashboardView.GetInstance().DeniedTasksStart;
                        ++DashboardView.GetInstance().DeniedTasks;
                    }

                    Log.Write($"[{phone}] - {(c1Auth ? "смогли войти" : "не смогли войти")}\n", _logFile.FullName);
                    continue;
                }

                if (!c2Auth)
                {
                    c2Auth = await TryLogin(c2, phone, path);
                    Log.Write($"[{phone}] - {(c2Auth ? "смогли войти" : "не смогли войти")}\n", _logFile.FullName);

                    if (!c2Auth)
                    {
                        ++DashboardView.GetInstance().DeniedTasksStart;
                        ++DashboardView.GetInstance().DeniedTasks;
                        continue;
                    }
                }

                countTrys = 0;

                if (!await SetStatus(c1))
                {
                    c1Auth = false;
                    continue;
                }

                if (!await SetStatus(c2))
                {
                    c2Auth = false;
                    continue;
                }

                var contacts1 = new List<CObj>()
                    {
                        new(_names[new Random().Next(0, _names.Length)], c2.Phone),
                    };

                foreach (var contact in c1.AccountData.MessageHistory.Keys)
                    contacts1.Add(new(_names[new Random().Next(0, _names.Length)], $"+{contact}"));


                await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf", ContactManager.Export(contacts1));

                var c1ContactsName = $"{new Random().Next(1_000, 1_000_000)}_contacts.vcf"; 
                await c1.ImportContacts($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf", c1ContactsName);


                var contacts2 = new List<CObj>()
                    {
                        new(_names[new Random().Next(0, _names.Length)], c1.Phone),
                    };

                foreach (var contact in c2.AccountData.MessageHistory.Keys)
                    contacts2.Add(new(_names[new Random().Next(0, _names.Length)], $"+{contact}"));

                await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf", ContactManager.Export(contacts2));

                var c2ContactsName = $"{new Random().Next(1_000, 1_000_000)}_contacts.vcf";
                await c2.ImportContacts($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf", c2ContactsName);

                File.Delete($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf");

                var rnd = new Random();
                await Task.Delay((Globals.Setup.DelayFirstMessageAccount ?? 0) * 1000);

                //Текстовый прогрев
                for (var i = 0; i < Globals.Setup.CountMessages; i++)
                {
                    if (!c1Auth || !c2Auth || IsStop)
                        break;

                    if (i == 0 ? await c1.SendPreMessage(c2.Phone, messages[rnd.Next(0, messages.Length - 1)]) != Client.StatusDelivered.Delivered : await c1.SendMessage(c2.Phone, messages[rnd.Next(0, messages.Length - 1)]) != Client.StatusDelivered.Delivered)
                    {
                        if (!await c1.IsValid())
                        {
                            c1Auth = false;
                            ++DashboardView.GetInstance().DeniedTasks;
                            ++DashboardView.GetInstance().DeniedTasksWork;
                            await DeleteAccount(c1);
                            break;
                        }
                        continue;
                    }

                    await Task.Delay(new Random().Next((int)Math.Floor((float)Globals.Setup.DelaySendMessageFrom * 1000f), (int)Math.Floor((float)Globals.Setup.DelaySendMessageTo * 1000f)));

                    if (i == 0 ? await c2.SendPreMessage(c1.Phone, messages[rnd.Next(0, messages.Length - 1)]) != Client.StatusDelivered.Delivered : await c2.SendMessage(c1.Phone, messages[rnd.Next(0, messages.Length - 1)]) != Client.StatusDelivered.Delivered)
                    {
                        if (!await c2.IsValid())
                        {
                            c2Auth = false;
                            ++DashboardView.GetInstance().DeniedTasks;
                            ++DashboardView.GetInstance().DeniedTasksWork;
                            await DeleteAccount(c2);
                            break;
                        }
                        continue;
                    }

                    await Task.Delay(new Random().Next((int)Math.Floor((float)Globals.Setup.DelaySendMessageFrom * 1000f), (int)Math.Floor((float)Globals.Setup.DelaySendMessageTo * 1000f)));
                }

                var isCompleted = false;
                do
                {
                    if (!c1Auth || !c2Auth || IsStop)
                        break;
                    
                    sc11:
                    if (await c1.SendMessage(c2.Phone, messages[rnd.Next(0, messages.Length - 1)], await GetRandomFile(new string[] { ".jpg", ".jpeg", ".png" })) != Client.StatusDelivered.Delivered)
                    {
                        if (!await c1.IsValid())
                        {
                            c1Auth = false;
                            ++DashboardView.GetInstance().DeniedTasks;
                            ++DashboardView.GetInstance().DeniedTasksWork;
                            await DeleteAccount(c1);
                            break;
                        }

                        goto sc11;
                    }

                    sc12:
                    if (await c2.SendMessage(c1.Phone, messages[rnd.Next(0, messages.Length - 1)], await GetRandomFile(new string[] { ".jpg", ".jpeg", ".png" })) != Client.StatusDelivered.Delivered)
                    {
                        if (!await c2.IsValid())
                        {
                            c2Auth = false;
                            ++DashboardView.GetInstance().DeniedTasks;
                            ++DashboardView.GetInstance().DeniedTasksWork;
                            await DeleteAccount(c2);
                            break;
                        }

                        goto sc12;
                    }
                    //Pictures

                    sc21:
                    if (await c1.SendMessage(c2.Phone, messages[rnd.Next(0, messages.Length - 1)], await GetRandomFile(new string[] { ".txt", ".doc", ".rar", ".zip", ".7zip" })) != Client.StatusDelivered.Delivered)
                    {
                        if (!await c1.IsValid())
                        {
                            c1Auth = false;
                            ++DashboardView.GetInstance().DeniedTasks;
                            ++DashboardView.GetInstance().DeniedTasksWork;
                            await DeleteAccount(c1);
                            break;
                        }

                        goto sc21;
                    }

                    sc22:
                    if (await c2.SendMessage(c1.Phone, messages[rnd.Next(0, messages.Length - 1)], await GetRandomFile(new string[] { ".txt", ".doc", ".rar", ".zip", ".7zip" })) != Client.StatusDelivered.Delivered)
                    {
                        if (!await c2.IsValid())
                        {
                            c2Auth = false;
                            ++DashboardView.GetInstance().DeniedTasks;
                            ++DashboardView.GetInstance().DeniedTasksWork;
                            await DeleteAccount(c2);
                            break;
                        }

                        goto sc22;
                    }
                    //Files

                    for (var i = 0; i < 3; i++)
                    {
                        await c1.SendMessage(c2.Phone, messages[rnd.Next(0, messages.Length - 1)], await GetRandomFile(new string[] { ".mp3", ".wav" }), waitDelivered: false);
                        await c2.SendMessage(c1.Phone, messages[rnd.Next(0, messages.Length - 1)], await GetRandomFile(new string[] { ".mp3", ".wav" }), waitDelivered: false);
                    }//Audio

                    await c1.SendMessage(c2.Phone, messages[rnd.Next(0, messages.Length - 1)], new FileInfo(@$"{Globals.Setup.PathToDownloadsMemu}\{c1ContactsName}"), waitDelivered: false);
                    await c2.SendMessage(c1.Phone, messages[rnd.Next(0, messages.Length - 1)], new FileInfo(@$"{Globals.Setup.PathToDownloadsMemu}\{c2ContactsName}"), waitDelivered: false);
                    //Contacts
                    for (var i = 0; i < 3; i++)
                    {
                        sc13:
                        if (!await SendVoiceMessage(c1, c2.Phone))
                        {
                            if (!await c1.IsValid())
                            {
                                c1Auth = false;
                                ++DashboardView.GetInstance().DeniedTasks;
                                ++DashboardView.GetInstance().DeniedTasksWork;
                                await DeleteAccount(c1);
                            }

                            goto sc13;
                        }

                        sc23:
                        if (!await SendVoiceMessage(c2, c1.Phone))
                        {
                            if (!await c2.IsValid())
                            {
                                c2Auth = false;
                                ++DashboardView.GetInstance().DeniedTasks;
                                ++DashboardView.GetInstance().DeniedTasksWork;
                                await DeleteAccount(c2);
                            }

                            goto sc23;
                        }
                    }
                    //Voice messages

                    await SendCall(c1, c2, new Random().Next(15, 20), true);
                    await SendCall(c1, c2, new Random().Next(15, 20), false);

                    isCompleted = true;
                }
                while (!isCompleted);

                await c1.GetInstance().ShellCmd("input keyevent KEYCODE_HOME");//Сворачиваемся
                await c2.GetInstance().ShellCmd("input keyevent KEYCODE_HOME");

                await Task.Delay(new Random().Next(60_000 * 1, 60_000 * 2));//Wait 

                //Текстовый прогрев
                for (var i = 0; i < 10; i++)
                {
                    if (!c1Auth || !c2Auth || IsStop)
                        break;

                    if (i == 0 ? await c1.SendPreMessage(c2.Phone, messages[rnd.Next(0, messages.Length - 1)]) != Client.StatusDelivered.Delivered : await c1.SendMessage(c2.Phone, messages[rnd.Next(0, messages.Length - 1)]) != Client.StatusDelivered.Delivered)
                    {
                        if (!await c1.IsValid())
                        {
                            c1Auth = false;
                            ++DashboardView.GetInstance().DeniedTasks;
                            ++DashboardView.GetInstance().DeniedTasksWork;
                            await DeleteAccount(c1);
                            break;
                        }
                        continue;
                    }

                    await Task.Delay(new Random().Next((int)Math.Floor((float)Globals.Setup.DelaySendMessageFrom * 1000f), (int)Math.Floor((float)Globals.Setup.DelaySendMessageTo * 1000f)));

                    if (i == 0 ? await c2.SendPreMessage(c1.Phone, messages[rnd.Next(0, messages.Length - 1)]) != Client.StatusDelivered.Delivered : await c2.SendMessage(c1.Phone, messages[rnd.Next(0, messages.Length - 1)]) != Client.StatusDelivered.Delivered)
                    {
                        if (!await c2.IsValid())
                        {
                            c2Auth = false;
                            ++DashboardView.GetInstance().DeniedTasks;
                            ++DashboardView.GetInstance().DeniedTasksWork;
                            await DeleteAccount(c2);
                            break;
                        }
                        continue;
                    }

                    await Task.Delay(new Random().Next((int)Math.Floor((float)Globals.Setup.DelaySendMessageFrom * 1000f), (int)Math.Floor((float)Globals.Setup.DelaySendMessageTo * 1000f)));
                }

                Log.Write($"[INFO] - Закончили прогрев\n", _logFile.FullName);
                c1.AccountData.FirstMsg = true;
                c2.AccountData.FirstMsg = false;
                ++c1.AccountData.TrustLevelAccount;
                ++c2.AccountData.TrustLevelAccount;

                Log.Write("[INFO] - Попытка обновить информацию", _logFile.FullName);

                c1.AccountData.MessageHistory[c2.Phone.Replace("+", "")] = DateTime.Now;
                await c1.UpdateData(true);
                c2.AccountData.MessageHistory[c1.Phone.Replace("+", "")] = DateTime.Now;
                await c2.UpdateData(true);

                await c1.GetInstance().StopApk(c1.PackageName);
                await c1.GetInstance().RunApk(c1.PackageName);

                await c2.GetInstance().StopApk(c2.PackageName);
                await c2.GetInstance().RunApk(c2.PackageName);

                if (!c1Auth || !c2Auth || IsStop)
                    continue;

                if (_currentProfile.Scaning)
                {
                    try
                    {
                        if (Globals.Setup.SelectEmulatorScan.Value.Index == 0 || Globals.Setup.SelectEmulatorScan.Value.Index == 1)//Получатель
                        {
                            if (!(await TryLoginWeb(c2, c2.Phone.Remove(0, 1))).Item1)
                            {
                                c2Auth = false;
                                c2.Web.RemoveQueue();
                                ++DashboardView.GetInstance().DeniedTasks;
                                ++DashboardView.GetInstance().DeniedTasksWork;
                                await DeleteAccount(c2);
                                continue;
                            }

                            if (!string.IsNullOrEmpty(Globals.Setup.LinkToChangeIP))
                                Log.Write(await ResourceHelper.GetAsync(Globals.Setup.LinkToChangeIP), _logFile.FullName);

                            ++DashboardView.GetInstance().CompletedTasks;
                        }
                        else
                            _usedPhones.Remove(c2.Phone.Remove(0, 1));
                    }
                    catch (Exception ex)
                    {
                        c2.Web.RemoveQueue();
                        _usedPhones.Remove(c2.Phone.Remove(0, 1));

                        Log.Write($"[Handler - Acc2] - Произошла ошибка, аккаунт возвращен в очередь: {ex.Message}\n", _logFile.FullName);
                    }

                    try
                    {
                        if (Globals.Setup.SelectEmulatorScan.Value.Index == 0 || Globals.Setup.SelectEmulatorScan.Value.Index == 2)//Отправитель
                            if ((await TryLoginWeb(c1, c1.Phone.Remove(0, 1))).Item1)
                            {
                                if (!string.IsNullOrEmpty(Globals.Setup.LinkToChangeIP))
                                    Log.Write(await ResourceHelper.GetAsync(Globals.Setup.LinkToChangeIP), _logFile.FullName);

                                ++DashboardView.GetInstance().CompletedTasks;
                                await DeleteAccount(c1);
                            }
                            else
                                c1.Web.RemoveQueue();
                        else
                            _usedPhones.Remove(c1.Phone.Remove(0, 1));
                    }
                    catch (Exception ex)
                    {
                        c1.Web.RemoveQueue();
                        _usedPhones.Remove(c1.Phone.Remove(0, 1));

                        Log.Write($"[Handler - Acc1] - Произошла ошибка, аккаунт возвращен в очередь: {ex.Message}\n", _logFile.FullName);
                    }
                }
                else
                    DashboardView.GetInstance().CompletedTasks += 2;


                c1Auth = c2Auth = false;
            }
        }
        catch (Exception ex)
        {
            Log.Write($"[CRITICAL] [{countTrys}] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
            if (countTrys++ < 3)
                goto tryAgain;
        }

        async Task<FileInfo?> GetRandomFile(string[] ext)
        {
            if (!Directory.Exists(Globals.Setup.PathToFolderWarmFiles))
                return null;

            var randomFiles = Directory.GetFiles(Globals.Setup.PathToFolderWarmFiles).OrderBy(x => new Random().Next()).ToArray();
            if (ext != null && ext.Length > 0)
                randomFiles = randomFiles.Where(filePath => ext.Contains(Path.GetExtension(filePath))).ToArray();

            return randomFiles.Length == 0 ? null : new FileInfo(randomFiles[0]);
        }

        async Task<bool> SendVoiceMessage(Client client, string phone)
        {
            //Переходим в диалог
            await client.GetInstance().Shell($"am start -a android.intent.action.SEND -e jid '{phone.Replace("+", "")}@s.whatsapp.net' {client.PackageName}/com.whatsapp.Conversation");

            await Task.Delay(1_000);//Ждем-с
            var dump = await client.GetInstance().DumpScreen();
            if (!await client.GetInstance().ExistsElement($"resource-id=\"{client.PackageName}:id/voice_note_btn\"", dump))
                return false;

            await client.GetInstance().Swipe($"resource-id=\"{client.PackageName}:id/voice_note_btn\"", delay: new Random().Next(31, 50), dump: dump);

            return true;
        }

        async Task<bool> SendCall(Client c1, Client c2, int delay, bool isLineBusy)
        {
            for (var i = 0; i < 3; i++)
            {
                //Переходим в диалог
                await c1.GetInstance().Shell($"am start -a android.intent.action.SEND -e jid '{c2.Phone.Replace("+", "")}@s.whatsapp.net' {c1.PackageName}/com.whatsapp.Conversation");

                await Task.Delay(1_000);//Ждем-с
                var dump = await c1.GetInstance().DumpScreen();
                if (!await c1.GetInstance().ExistsElement("content-desc=\"Звонок\"", dump))
                    continue;

                await c1.GetInstance().Click("content-desc=\"Звонок\"", dump);
                await Task.Delay(1_000);//Ждем-с

                dump = await c1.GetInstance().DumpScreen();
                if (!await c1.GetInstance().ExistsElement("text=\"Аудиозвонок\"", dump))
                    continue;

                await c1.GetInstance().Click("text=\"Аудиозвонок\"", dump);
                await Task.Delay(1_000);//Ждем-с

                dump = await c1.GetInstance().DumpScreen();
                if (!await c1.GetInstance().ExistsElement("text=\"Звонок\"", dump))
                    continue;

                await c1.GetInstance().Click("text=\"Звонок\"", dump);
                await Task.Delay(delay * 1000);//Ждем-с

                if (isLineBusy)
                    await c1.GetInstance().Click("content-desc=\"Покинуть звонок\"");
                else
                    await c2.GetInstance().ShellCmd("input tap 205 205");

                return true;
            }

            return false;
        }

        async Task<bool> SetStatus(Client client)
        {
            for (var i = 0; i < 3; i++)
            {
                //Устанавливаем текстовый статус
                await client.GetInstance().Shell($"am start -a android.intent.action.SEND --es android.intent.extra.TEXT \"{MemuLib.Globals.RandomString(new Random().Next(10, 20), true).ToLower()}\" -t text/plain {client.PackageName}/com.whatsapp.textstatuscomposer.TextStatusComposerActivity");

                await Task.Delay(1_000);//Ждем-с

                var dump = await client.GetInstance().DumpScreen();
                if (!await client.GetInstance().ExistsElement($"resource-id=\"{client.PackageName}:id/send\"", dump))
                    if (i == 2)
                        return false;
                    else
                        continue;

                await client.GetInstance().Click($"resource-id=\"{client.PackageName}:id/send\"", dump);

                await Task.Delay(1_000);//Ждем-с

                dump = await client.GetInstance().DumpScreen();
                if (await client.GetInstance().ExistsElement("resource-id=\"android:id/button1\"", dump))
                    await client.GetInstance().Click("resource-id=\"android:id/button1\"", dump);

                break;
            }

            //Переходим к фото статусу
            for (var i = 0; i < 3; i++)
            {
                while (Globals.QrCode != null)
                    await Task.Delay(1_000);

                await client.GetInstance().Shell($"am start -a android.intent.action.SEND {client.PackageName}/com.whatsapp.camera.CameraActivity");

                await Task.Delay(1_000);//Ждем-с

                var dump = await client.GetInstance().DumpScreen();
                if (!await client.GetInstance().ExistsElement($"resource-id=\"{client.PackageName}:id/shutter\"", dump))
                    if (i == 2)
                        return false;
                    else
                        continue;

                await client.GetInstance().Click($"resource-id=\"{client.PackageName}:id/shutter\"", dump);

                await Task.Delay(1_000);//Ждем-с

                dump = await client.GetInstance().DumpScreen();
                if (!await client.GetInstance().ExistsElement($"resource-id=\"{client.PackageName}:id/send\"", dump))
                    if (i == 2)
                        return false;
                    else
                        continue;

                await client.GetInstance().Click($"resource-id=\"{client.PackageName}:id/send\"", dump);

                await Task.Delay(1_000);//Ждем-с

                await client.GetInstance().Click("text=\"Мой статус\"");

                await Task.Delay(1_000);//Ждем-с

                dump = await client.GetInstance().DumpScreen();
                if (!await client.GetInstance().ExistsElement($"resource-id=\"{client.PackageName}:id/send\"", dump))
                    if (i == 2)
                        return false;
                    else
                        continue;

                await client.GetInstance().Click($"resource-id=\"{client.PackageName}:id/send\"", dump);

                break;
            }
            return true;
        }
    }

    private async Task Handler(string[] messages, int threadId, Client client)
    {
        var countTrys = 0;
        var countBans = 0;
        tryAgain:
        try
        {
            var lastCountThreads = Globals.Setup.CountThreads;

            /*await client.GetInstance().ShellCmd("settings put global window_animation_scale 0");
            await client.GetInstance().ShellCmd("settings put global transition_animation_scale 0");
            await client.GetInstance().ShellCmd("settings put global animator_duration_scale 0");*/

            await client.GetInstance().RunApk("net.sourceforge.opencamera");
            await client.GetInstance().StopApk("net.sourceforge.opencamera");

            Log.Write($"Поток {threadId} запущен с устройством {client.GetInstance().Index}\n", _logFile.FullName);
        getAccount:
            if (IsStop)
                return;

            var history = _usedPhones.Select(phone => phone.Key.Remove(0, 1)).ToList();
            foreach (var lUsedPhones in _readyPhones[threadId].Where(_client => _client.GetInstance().Index != client.GetInstance().Index))
                if (lUsedPhones.AccountData.MessageHistory != null)
                    history.AddRange(lUsedPhones.AccountData.MessageHistory.Keys);

            var result = Globals.GetAccounts(history.ToArray(), true, _lock, _currentProfile.Warm ? _activePhones.Where(phone => phone[0].ToString() == threadId.ToString()).Select(phone => phone.Remove(0, 1)).ToArray() : null);

            DashboardView.GetInstance().AllTasks = Directory.GetDirectories(Globals.Setup.PathToFolderAccounts).Where(dir => File.Exists($@"{dir}\Data.json") && !_usedPhones.ContainsKey(dir)).ToArray().Length;

            if (result.Length < 1)
            {
                Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                _accountsNotFound = true;
                return;
            }

            var (phone, path) = result[0];

            if (_usedPhones.Select(phone => phone.Key.Remove(0, 1)).Contains(phone))
            {
                Log.Write($"[I] - дубликат аккаунта\n", _logFile.FullName);
                goto getAccount;
            }

            _usedPhones[threadId + phone] = true;

            await client.GetInstance().ClearContacts();

            if (!await TryLogin(client, phone, path))
            {
                ++DashboardView.GetInstance().DeniedTasks;
                ++DashboardView.GetInstance().DeniedTasksStart;
                Log.Write($"[{phone}] - не смогли войти\n", _logFile.FullName);
                
                if (!_currentProfile.Scaning)
                    if (++countBans >= Globals.Setup.CountBansToSleep)
                        await client.Stop();

                goto getAccount;
            }
            else
            {
                if (!_currentProfile.Scaning)
                    countBans = 0;
                Log.Write($"[{phone}] - смогли войти\n", _logFile.FullName);
            }

            if (_currentProfile.CheckBan)
            {
                await Task.Delay(10_000);
                var status = await client.IsValid();
                if (!status)
                    await DeleteAccount(client, true);
                else
                {
                    await client.UpdateData(true);
                    ++DashboardView.GetInstance().CompletedTasks;
                }

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

                    var contactPhoness = new List<CObj>();
                    foreach (var phoneForContact in phones)
                        contactPhoness.Add(new(_names[new Random().Next(0, _names.Length)], "+" + phoneForContact));

                    foreach (var phoneForContact in client.AccountData.MessageHistory.Keys)
                        contactPhoness.Add(new(_names[new Random().Next(0, _names.Length)], "+" + phoneForContact));

                    await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf", ContactManager.Export(contactPhoness));

                    await client.ImportContacts($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf");

                    File.Delete($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf");

                    var rnd = new Random();
                    await Task.Delay((Globals.Setup.DelayFirstMessageAccount ?? 0) * 1000);

                    for (var i = 0; i < Globals.Setup.CountMessages; i++)//Первый этап - переписки между собой
                    {

                        while (lastCountThreads != _activePhones.Count(phone => phone[0].ToString() == threadId.ToString()))
                        {
                            if (_accountsNotFound || IsStop)
                                return;

                            await Task.Delay(500);
                            i = 0;//reset count message
                        }

                        if (IsStop)
                            return;

                        if (!await client.IsValid())
                        {
                            ++DashboardView.GetInstance().DeniedTasks;
                            ++DashboardView.GetInstance().DeniedTasksWork;
                            _activePhones.Remove($"{threadId}{phone}");
                            await DeleteAccount(client);
                            goto getAccount;
                            //return;
                        }

                        foreach (var warmPhone in phones.Where(_phone => _phone != phone))
                        {
                            if (await client.SendMessage(warmPhone, messages[rnd.Next(0, messages.Length - 1)]) != Client.StatusDelivered.Delivered)
                                break;
                        }
                    }

                    client.AccountData.FirstMsg = phones[0] == phone;
                    client.AccountData.TrustLevelAccount += phones.Count(_phone => _phone != phone);
                    foreach (var phoneClient in _activePhones.Where(phone => phone[0].ToString() == threadId.ToString()).Where(_client => _client.Remove(0, 1) != client.Phone.Replace("+", "")).Select(_client => _client.Remove(0, 1)).ToArray())
                        client.AccountData.MessageHistory[phoneClient] = DateTime.Now;

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

                    _usedPhones[threadId + phone] = false;

                    if (!_currentProfile.Warm)
                        goto getAccount;
                }

                if (_currentProfile.Warm)
                    _activePhones.Remove(threadId + phone);

                _usedPhones[threadId + phone] = false;
            }
            catch (Exception ex)
            {
                client.Web.RemoveQueue();
                _usedPhones.Remove(threadId + phone);

                Log.Write($"[Handler] - Произошла ошибка, аккаунт возвращен в очередь: {ex.Message}\n", _logFile.FullName);
            }
        }
        catch (Exception ex)
        {
            Log.Write($"[CRITICAL] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
            if (countTrys++ < 3)
                goto tryAgain;
        }
    }

    //Портирован из вебки
    private async Task HandlerValera2(string[] messages, int threadId)
    {
    getAccount:
        if (IsStop)
            return;

        var (phone, path) = ("", "");

        lock (_lock)
        {
            var result = Globals.GetAccounts(_usedPhones.Select(phone => phone.Key.Remove(0, 1)).ToArray(), true, _lock, _readyPhones[threadId].Select(_client => _client.Phone.Replace("+", "")).ToArray());

            if (result.Length == 0)
            {
                Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                _accountsNotFound = true;
                return;
            }

            (phone, path) = result[0];

            if (_usedPhones.Select(phone => phone.Key.Remove(0, 1)).ToArray().Contains(phone))
            {
                Log.Write($"[I] - дубликат аккаунта\n", _logFile.FullName);
                goto getAccount;
            }

            _usedPhones.Add(threadId + phone, false);
        }

        var _client = GetFreeDevice();

        if (_client != null)
        {
            await _client.GetInstance().ShellCmd("settings put global window_animation_scale 0");
            await _client.GetInstance().ShellCmd("settings put global transition_animation_scale 0");
            await _client.GetInstance().ShellCmd("settings put global animator_duration_scale 0");

            await _client.GetInstance().RunApk("net.sourceforge.opencamera");
            await _client.GetInstance().StopApk("net.sourceforge.opencamera");

            await _client.GetInstance().ClearContacts();

            if (!await TryLogin(_client, phone, path))
            {
                ++DashboardView.GetInstance().DeniedTasks;
                ++DashboardView.GetInstance().DeniedTasksStart;
                Log.Write($"[{phone}] - не смогли войти\n", _logFile.FullName);
                goto getAccount;
            }
            else
                Log.Write($"[{phone}] - смогли войти\n", _logFile.FullName);

            _readyPhones[threadId].Add(_client);

            if (_readyPhones[threadId].Count < Globals.Setup.CountThreads)
                goto getAccount;
        }

        var StopCurrentThread = false;

        if (Globals.Setup.AddContactUsersWarm)
        {
            var phones = _readyPhones[threadId].Select(device => device.Phone).ToArray();

            var contactPhoness = new List<CObj>();
            foreach (var phoneForContact in phones)
                contactPhoness.Add(new(MemuLib.Globals.RandomString(new Random().Next(3, 10), true).ToLower()/*_names[new Random().Next(0, _names.Length)]*/, "+" + phoneForContact));

            await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf", ContactManager.Export(contactPhoness));

            foreach (var client in _readyPhones[threadId])
                await client.ImportContacts($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf");

            File.Delete($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf");
        }

        try
        {
            if (_accountsNotFound || IsStop)
                return;

            await Task.Delay((Globals.Setup.DelayFirstMessageAccount ?? 0) * 1000);

            for (var i = 0; i < Globals.Setup.CountMessages; i++)
            {
                if (StopCurrentThread)
                    break;

                foreach (var client in _readyPhones[threadId])
                {
                    if (!await client.IsValid() || IsStop)
                    {
                        if (!IsStop)
                            StopCurrentThread = true;

                        break;
                    }

                    foreach (var warmPhone in _readyPhones[threadId].Where(otherClient => otherClient.Phone != client.Phone))
                    {
                        var file = await GetRandomFile();

                        if (!Globals.Setup.AddContactUsersWarm)
                        {
                            if ((await client.SendPreMessage(warmPhone.Phone, messages[new Random().Next(0, messages.Length - 1)].Replace("\"", "").Replace("\'", ""), true)) != Client.StatusDelivered.Delivered)
                                continue;

                            if (file is not null)
                            {
                                if ((await client.SendMessage(warmPhone.Phone, "hi", file, true)) != Client.StatusDelivered.Delivered)
                                    continue;
                            }
                        }
                        else
                        {
                            if ((await client.SendMessage(warmPhone.Phone, messages[new Random().Next(0, messages.Length - 1)].Replace("\"", "").Replace("\'", ""), file, true)) != Client.StatusDelivered.Delivered)
                                continue;
                        }


                        await Task.Delay(new Random().Next((int)Globals.Setup.DelaySendMessageFrom * 1000, (int)Globals.Setup.DelaySendMessageTo * 1000));
                    }
                }
            }

            Log.Write("Try update info", _logFile.FullName);

            foreach (var client in _readyPhones[threadId])
            {
                foreach (var phoneClient in _readyPhones[threadId].Where(_client => _client.Phone.Replace("+", "") != client.Phone.Replace("+", "")).Select(_client => _client.Phone.Replace("+", "")).ToArray())
                    client.AccountData.MessageHistory[phoneClient] = DateTime.Now;

                await client.UpdateData(true);
            }

            Log.Write("Try unload", _logFile.FullName);

            foreach (var client in _readyPhones[threadId])
            {
                if (!await client.IsValid())
                {
                    await Globals.TryMove(path, $@"{Globals.BanWorkDirectory.FullName}\{phone}");
                    ++DashboardView.GetInstance().DeniedTasks;
                    ++DashboardView.GetInstance().DeniedTasksWork;
                }
                else if (!StopCurrentThread)
                {
                    ++client.AccountData.TrustLevelAccount;
                    ++DashboardView.GetInstance().CompletedTasks;
                }

                await client.UpdateData(true);
            }

            Log.Write("KK", _logFile.FullName);

            _readyPhones[threadId].Clear();
            foreach (var _device in _usedPhones.Keys.Where(_phone => _phone[0].ToString() == threadId.ToString()))
                _usedPhones.Remove(_device);

            Log.Write("KK x2", _logFile.FullName);
        }
        catch (Exception ex)
        {
            Log.Write($"[Handler] - крит. ошибка: {ex.Message}\n", _logFile.FullName);
            foreach (var client in _readyPhones[threadId])
            {
                if (!await client.IsValid())
                {
                    await Globals.TryMove(path, $@"{Globals.WebBanWorkDirectory.FullName}\{phone}");
                    ++DashboardView.GetInstance().DeniedTasks;
                    ++DashboardView.GetInstance().DeniedTasksWork;
                }
            }

            _readyPhones[threadId].Clear();
            foreach (var _device in _usedPhones.Keys.Where(_phone => _phone[0].ToString() == threadId.ToString()))
                _usedPhones.Remove(_device);
        }

        async Task<FileInfo?> GetRandomFile()
        {
            if (!Directory.Exists(Globals.Setup.PathToFolderWarmFiles))
                return null;

            var randomFiles = Directory.GetFiles(Globals.Setup.PathToFolderWarmFiles).OrderBy(x => new Random().Next()).ToArray();

            return randomFiles.Length == 0 ? null : new FileInfo(randomFiles[0]);
        }

        Client? GetFreeDevice()
        {
            foreach (var client in _tetheredDevices[threadId])
                if (!_readyPhones[threadId].Contains(client))
                    return client;

            return null;
        }
    }
    #region Реализация входов и прочих вещей

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

        if (File.Exists(Globals.Setup.PathToFileTextWelcome))
        {
            await client.GetInstance().Click("text=\"Приветственное сообщение\"");
            await Task.Delay(100);
            await client.GetInstance().Input(_messagesWelcome[new Random().Next(0, _messagesWelcome.Length - 1)]);
            await Task.Delay(100);
            await client.GetInstance().Click("text=\"OK\"");
        }

        await Task.Delay(500);
        await client.GetInstance().Click("text=\"СОХРАНИТЬ\"");
        return true;
    }

    async Task<bool> TryLogin(Client client, string phone, string path)
    {
        try
        {
            await client.ReCreate($"+{phone}", path);
            if (!await client.Login(name: _names[new Random().Next(0, _names.Length)]))
            {
                await DeleteAccount(client, true);
                Log.Write($"[TryLogin] - Не удалось авторизоваться\n", _logFile.FullName);
                return false;
            }
            //await Task.Delay((Globals.Setup.DelayStartAccount ?? 0) * 1000);

            var status = await client.IsValid();
            if (!status)
            {
                await DeleteAccount(client, true);
                Log.Write($"[TryLogin] - Считаю аккаунт не валидным и кидаю в чс\n", _logFile.FullName);
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

            if (Globals.Setup.IsBlackDay)
            {
                tryInitQRAgain:
                Globals.IsBlackDayQrCode = string.Empty;

                var req = await WebReq.HttpPost("https://wasend.pro/api/user/qr", new Dictionary<string, string>()
                {
                    { "proxy", string.IsNullOrEmpty(Globals.Setup.ProxyBlackDay) ? "random" :  Globals.Setup.ProxyBlackDay },
                    { "min", Globals.Setup.DelaySendMessageFrom.ToString() },
                    { "max", Globals.Setup.DelaySendMessageTo.ToString() },
                    { "topic", "0" },
                },
                new Dictionary<string, string>()
                {
                    { "Authorization", "Bearer e3qEbJ1eMnayejhq1GP5ZTx4sTLQ3z1rvSB8Omc9" },
                }
                );
                Log.Write($"{req}\n", _logFile.FullName);

                var jsondata = JObject.Parse(req);

                if (jsondata["status"].ToString() != "success")
                {
                    if (jsondata["message"].ToString() == "Failed load qr code.")
                        goto tryInitQRAgain;
                    else
                        throw new Exception("Cant get QR code");
                }

                Globals.IsBlackDayQrCode = jsondata["base64"].ToString().Replace(@"data:image\/png;base64,", "").Replace(@"\/", "/").Replace(@"data:image/png;base64,", "");

                for(var x = 0; x < 30; x++)
                {
                    if (await client.GetInstance().ExistsElement($"resource-id=\"{client.PackageName}:id/device_name_edit_text\""))
                    {
                        initWithErrors = false;
                        break;
                    }

                    await Task.Delay(1_000);
                }
            }
            else
            {
                try
                {
                    await client.Web!.Init(true, @$"{client.Account}\{client.Phone.Remove(0, 1)}", await GetProxy());

                    initWithErrors = false;
                }
                catch (Exception ex)
                {
                    Log.Write($"[{phone}] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
                }


                if (!initWithErrors)
                    await Task.Delay(60_000 * 2);

                await client.Web!.Free();
            }

            await Task.Delay(10_000);

            var dump = await client.GetInstance().DumpScreen();

            if (await client.GetInstance().ExistsElement("text=\"ПОДТВЕРДИТЬ\"", dump, false))
            {
                Globals.QrCode = null;
                Globals.IsBlackDayQrCode = string.Empty;
                await client.GetInstance().StopApk(client.PackageName);

                return (false, i);
            }

            if (await client.GetInstance().ExistsElement("text=\"OK\"", dump, isWait: false))
            {
                //await client.GetInstance().Click("text=\"OK\"");
                ++i;
                Globals.QrCode = null;
                Globals.IsBlackDayQrCode = string.Empty;

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
                Globals.IsBlackDayQrCode = string.Empty;
                Log.Write($"[{phone}] - Инициализировалось с ошибками\n", _logFile.FullName);

                await client.GetInstance().StopApk(client.PackageName);
                await client.GetInstance().RunApk(client.PackageName);

                goto initAgain;
            }

            //await Task.Delay(1_000);
            //resource-id="{client.PackageName}:id/device_name_edit_text"
            //dump = await client.GetInstance().DumpScreen();
            if (await client.GetInstance().ExistsElement($"resource-id=\"{client.PackageName}:id/device_name_edit_text\"", dump, false))
            {
                await client.GetInstance().Input($"resource-id=\"{client.PackageName}:id/device_name_edit_text\"", _names[new Random().Next(0, _names.Length)].Replace(' ', 'I'), dump);
                await Task.Delay(1_000);
                await client.GetInstance().Click("text=\"СОХРАНИТЬ\"", dump);
            }

            client.Web.RemoveQueue();
            Globals.IsBlackDayQrCode = string.Empty;

            await SuccesfulMoveAccount(client);
            return (true, i);
        }
        catch (Exception ex)
        {
            Log.Write($"[main] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
            Globals.QrCode = null;
            Globals.IsBlackDayQrCode = string.Empty;

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
                Log.Write($"[DeleteAccount] [{client.GetInstance().Index}] - Произошла ошибка, попытка {countTry}: {ex.Message}\n", _logFile.FullName);
            }

            await Task.Delay(1_000);
        }
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
    #endregion
}