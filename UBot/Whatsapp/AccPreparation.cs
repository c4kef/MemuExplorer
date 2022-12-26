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
using System.Threading;
using UBot.Pages.Dialogs;
using static Microsoft.Maui.ApplicationModel.Permissions;

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
        _tetheredDevices = new Dictionary<int, Client[]>();
        _usedPhones = new Dictionary<string, bool>();
        _activePhones = new List<string>();
        _warmPhones = new List<Client>();
        _names = new[] { "" };
        _lock = new();
    }

    private readonly Dictionary<int, Client[]> _tetheredDevices;
    private readonly List<string> _usedPhonesUsers;
    private readonly List<string> _activePhones;
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
                tasks.Clear();
            })}, -1);
        }

        if (_currentProfile.WarmMethodValera)
        {
            while (true)
            {
                var devices = ManagerView.GetInstance().Emulators.Where(device => !busyDevices.Select(device => device.Key.Index).Contains(device.Index) && device.IsEnabled)
                    .Take(2).ToArray();

                if (devices.Length != 2)
                    break;

                var id = new Random().Next(0, 10_000);

                _tetheredDevices[id] = new[] { new Client(deviceId: devices[0].Index), new Client(deviceId: devices[1].Index) };

                var task = HandlerValera(message.Split('\n'), id);

                await Task.Delay(1_000);

                mainTasks.Add(task);

                foreach (var device in devices)
                    busyDevices[device] = id;
            }

            Task.WaitAll(mainTasks.ToArray(), -1);
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

                            var countedDevice = 0;
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
    }

    public async Task<bool> HandlerWelcomeAccount(Client client, string phone, string path, string[] messages, int threadId)
    {
        var rnd = new Random();

        if (!await TryLogin(client, phone, path))
        {
            ++DashboardView.GetInstance().DeniedTasks;
            ++DashboardView.GetInstance().DeniedTasksStart;
            Log.Write($"[{phone}] - не смогли войти\n", _logFile.FullName);
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

    private async Task HandlerValera(string[] messages, int threadId)
    {
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
                var (phone, path) = ("", "");

                //lock (_lock)
                //{
                var usedPhones = _usedPhones.Select(phone => phone.Key).ToList();
                /*if (c1Auth && _currentProfile.Warm)
                    usedPhones.AddRange(c1.AccountData.MessageHistory.Keys);*/

                var result = Globals.GetAccounts(usedPhones.ToArray(), true, _lock);

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

                await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf", ContactManager.Export(
                    new List<CObj>()
                    {
                        new(_names[new Random().Next(0, _names.Length)], c1.Phone),
                        new(_names[new Random().Next(0, _names.Length)], c2.Phone)
                    }
                ));

                await c1.ImportContacts($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf");

                await c2.ImportContacts($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf");

                File.Delete($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf");

                var rnd = new Random();
                await Task.Delay((Globals.Setup.DelayFirstMessageAccount ?? 0) * 1000);

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

                await c1.UpdateData(true);
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

            var result = Globals.GetAccounts(_usedPhones.Select(phone => phone.Key.Remove(0, 1)).ToArray(), true, _lock, _currentProfile.Warm ? _activePhones.Where(phone => phone[0].ToString() == threadId.ToString()).Select(phone => phone.Remove(0, 1)).ToArray() : null);

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
                goto getAccount;
            }
            else
                Log.Write($"[{phone}] - смогли войти\n", _logFile.FullName);

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

                    await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf", ContactManager.Export(contactPhoness));

                    await client.ImportContacts($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf");

                    File.Delete($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf");

                    var rnd = new Random();
                    await Task.Delay((Globals.Setup.DelayFirstMessageAccount ?? 0) * 1000);

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

                        foreach (var warmPhone in phones.Where(_phone => _phone != phone))
                        {
                            if (await client.SendMessage(warmPhone, messages[rnd.Next(0, messages.Length - 1)]) != Client.StatusDelivered.Delivered)
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