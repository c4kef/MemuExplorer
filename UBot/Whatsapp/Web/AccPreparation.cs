using MemuLib.Core;
using MemuLib.Core.Contacts;
using System.Linq;
using UBot.Views.User;
using static System.Net.Mime.MediaTypeNames;

namespace UBot.Whatsapp.Web;

public class AccPreparation
{
    public AccPreparation()
    {
        _usedPhones = new List<string>();
        _usedPhonesUsers = new List<string>();
        _activePhones = new List<string>();
        _lock = new();
    }

    private readonly List<string> _usedPhonesUsers;
    private readonly List<string> _activePhones;
    private readonly List<string> _usedPhones;
    private readonly object _lock;

    private string[] _contacts;
    private bool _accountsNotFound;
    private FileInfo _logFile;
    private FileInfo _checkedFile;
    private ActionProfileWork _currentProfile;

    public bool IsStop;

    public async Task Run(string message, ActionProfileWork actionProfileWork)
    {
        IsStop = false;
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_prep.txt");
        _logFile.Create().Close();

        _currentProfile = actionProfileWork;

        if (_currentProfile.CheckNumberValid)
        {
            _checkedFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{new FileInfo(Globals.Setup.PathToCheckNumbers).Name}_checked.txt");
            if (!_checkedFile.Exists)
                _checkedFile.Create().Close();
            _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToCheckNumbers);
        }

        var mainTasks = new List<Task>();

        for (var repeatId = 0; repeatId < Globals.Setup.RepeatCounts; repeatId++)
        {
            for (var groupId = 0; groupId < ((_currentProfile.CheckBan || _currentProfile.CheckNumberValid) ? 1 : Globals.Setup.CountGroups); groupId++)
            {
                var id = groupId;
                await Task.Delay(100);
                mainTasks.Add(Task.Run(async () =>
                {
                    var tasks = new List<Task>();

                    while (!IsStop)
                    {
                        if (!_currentProfile.CheckNumberValid)
                            DashboardView.GetInstance().AllTasks = Directory.GetDirectories(Globals.Setup.PathToFolderAccounts).Where(dir => File.Exists($@"{dir}\Data.json") && !_usedPhones.Select(phone => phone.Remove(0, 1)).Contains(dir)).ToArray().Length;

                        for (var i = 0; i < Globals.Setup.CountThreads; i++)
                        {
                            await Task.Delay(100);
                            tasks.Add(Task.Run(async () => await Handler(message.Split('\n'), id)));
                        }

                        Task.WaitAll(tasks.ToArray(), -1);

                        _activePhones.RemoveAll(obj => obj[0].ToString() == id.ToString());
                        tasks.Clear();

                        if (_accountsNotFound)
                            break;
                    }

                    //_usedPhones.RemoveAll(obj => obj[0].ToString() == id.ToString());
                }));
            }

            Task.WaitAll(mainTasks.ToArray(), -1);

            mainTasks.Clear();

            if (repeatId < Globals.Setup.RepeatCounts)
                DashboardView.GetInstance().CompletedTasks = 0;

            _accountsNotFound = false;
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

        _usedPhonesUsers.Clear();
        _usedPhones.Clear();
        _activePhones.Clear();
    }

    private async Task Handler(string[] messages, int threadId)
    {
        var lastCountThreads = Globals.Setup.CountThreads;
    getAccount:
        if (IsStop)
            return;

        var result = Globals.GetAccounts(_usedPhones.Select(phone => phone.Remove(0, 1)).ToArray(), true, _lock, _activePhones.Where(phone => phone[0].ToString() == threadId.ToString()).Select(phone => phone.Remove(0, 1)).ToArray());

        if (result.Length == 0)
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

        var client = new Client(phone, path);

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
        
        if (_currentProfile.CheckBan)
        {
            await client.Web!.Free();
            ++DashboardView.GetInstance().CompletedTasks;
            goto getAccount;
        }
        
        try
        {
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

                for (var i = 0; i < Globals.Setup.CountMessages; i++)//Первый этап - переписки между собой
                {
                    if (!await client.Web!.IsConnected())
                    {
                        await client.Web!.Free();
                        await Globals.TryMove(path, $@"{Globals.WebBanWorkDirectory.FullName}\{phone}");
                        ++DashboardView.GetInstance().DeniedTasks;
                        ++DashboardView.GetInstance().DeniedTasksWork;
                        return;
                    }

                    foreach (var warmPhone in phones.Where(_phone => _phone != phone))
                    {
                        if (!await client.Web!.SendText(warmPhone, messages[new Random().Next(0, messages.Length - 1)].Replace("\"", "").Replace("\'", "")))
                            continue;

                        await Task.Delay(new Random().Next((int)Globals.Setup.DelaySendMessageFrom * 1000, (int)Globals.Setup.DelaySendMessageTo * 1000));
                    }
                }

                if (File.Exists(Globals.Setup.PathToFileGroups))//Второй этап - начинаем вступать в группы
                {
                    var allGroups = await File.ReadAllLinesAsync(Globals.Setup.PathToFileGroups);
                    var groups = allGroups.Where(group => new Random().Next(0, 100) >= 50).ToList();

                    if (groups.Count == 0)
                        groups.Add(allGroups[0]);

                    foreach (var group in groups)
                        await client.Web!.JoinGroup(group);
                }

                if (File.Exists(Globals.Setup.PathToFileChatBots))//Третий этап - начинаем писать чат ботам
                {
                    var allChatBots = await File.ReadAllLinesAsync(Globals.Setup.PathToFileChatBots);
                    /*var chatbots = allChatBots.Where(group => new Random().Next(0, 100) >= 50).ToList();

                    if (chatbots.Count == 0)
                        chatbots.Add(allChatBots[0]);
                    */
                    foreach (var chatbot in allChatBots)
                        await client.Web!.SendText(chatbot, messages[new Random().Next(0, messages.Length - 1)]);
                }

                /*
                 * PathToFilePeoples используется для совсем другого
                if (File.Exists(Globals.Setup.PathToFilePeoples))//Четвертый этап - начинаем писать людишкам
                {
                    var allPeoples = await File.ReadAllLinesAsync(Globals.Setup.PathToFilePeoples);
                    var localMessages = new List<string>();

                    if (File.Exists(Globals.Setup.PathToFileTextPeopleWarm))
                        localMessages.AddRange(await File.ReadAllLinesAsync(Globals.Setup.PathToFileTextPeopleWarm));

                    var peoples = allPeoples.Where(group => new Random().Next(0, 100) >= 50).ToList();

                    if (peoples.Count == 0)
                        peoples.Add(allPeoples[0]);

                    foreach (var people in peoples)
                        await client.Web!.SendText(people, localMessages.Count == 0 ? messages[new Random().Next(0, messages.Length - 1)] : localMessages[new Random().Next(0, localMessages.Count - 1)]);
                }*/

                await Task.Delay(2_000);

                if (!await client.Web!.IsConnected())
                {
                    await client.Web!.Free();
                    ++DashboardView.GetInstance().DeniedTasks;
                    ++DashboardView.GetInstance().DeniedTasksWork;
                    await Globals.TryMove(path, $@"{Globals.WebBanWorkDirectory.FullName}\{phone}");
                }
                else
                {
                    await client.Web!.Free();
                    ++client.AccountData.TrustLevelAccount;
                    await client.UpdateData(false);
                    ++DashboardView.GetInstance().CompletedTasks;
                }
            }

            if (_currentProfile.CheckNumberValid)
            {
                if (_contacts.Except(_usedPhonesUsers.ToArray()).Count() == 0)
                {
                    _accountsNotFound = true;
                    await client.Web!.Free();
                    return;
                }

                var checkedCount = 0;
                while (true)
                {
                    var _tasks = new List<Task>();
                    var peopleReal = GetFreeNumbersUser();
                    if (peopleReal.Length == 0)
                    {
                        _accountsNotFound = true;
                        await client.Web!.Free();
                        return;
                    }

                    if (IsStop)
                    {
                        await client.Web!.Free();
                        return;
                    }

                    foreach (var value in peopleReal)
                    {
                        var _value = value;
                        _tasks.Add(Task.Run(async () =>
                        {
                            var res = await client.Web!.CheckValidPhone(_value);
                            if (!res && !await client.Web!.IsConnected())
                            {
                                _usedPhonesUsers.Remove(_value);
                                return;
                            }

                            lock (_lock)
                            {
                                File.AppendAllText(_checkedFile.FullName, $"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{_value};{res}\n");
                                ++DashboardView.GetInstance().CompletedTasks;

                                if (++checkedCount >= (Globals.Setup.CountCheckedPhonesFromAccount ?? 1000))
                                    return;
                            }
                        }));
                    }

                    Task.WaitAll(_tasks.ToArray(), -1);
                    _tasks.Clear();

                    if (!await client.Web!.IsConnected())
                    {
                        await client.Web!.Free();
                        await Globals.TryMove(path, $@"{Globals.WebBanWorkDirectory.FullName}\{phone}");
                        ++DashboardView.GetInstance().DeniedTasks;
                        ++DashboardView.GetInstance().DeniedTasksWork;
                        return;
                    }

                    if (checkedCount >= (Globals.Setup.CountCheckedPhonesFromAccount ?? 1000) || IsStop)
                    {
                        await client.Web!.Free();
                        return;
                    }

                    await Task.Delay((Globals.Setup.DelayBetweenStacks ?? 0) * 1000);
                }
            }

            _activePhones.Remove(threadId + phone);
        }
        catch (Exception ex)
        {
            Log.Write($"[Handler] - крит. ошибка: {ex.Message}\n", _logFile.FullName);
            await client.Web!.Free();
            ++DashboardView.GetInstance().DeniedTasks;
            ++DashboardView.GetInstance().DeniedTasksWork;
            await Globals.TryMove(path, $@"{Globals.WebBanWorkDirectory.FullName}\{phone}");
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

        string[] GetFreeNumbersUser()
        {
            lock (_lock)
            {
                var contacts = _contacts.Except(_usedPhonesUsers.ToArray()).Take(Globals.Setup.CountPhonesFromStack ?? 5).ToArray();
                //var contacts = _contacts.Where(con => !_usedPhonesUsers.Contains(con)).Take(Globals.Setup.CountPhonesFromStack ?? 5).ToArray();

                _usedPhonesUsers.AddRange(contacts);

                var newContacts = _contacts.Except(_usedPhonesUsers.ToArray());// _contacts.Where(con => !_usedPhonesUsers.Contains(con)).ToList();
                File.WriteAllLines(Globals.Setup.PathToCheckNumbers, newContacts);
                DashboardView.GetInstance().AllTasks = newContacts.Count();

                return contacts;
            }
        }
    }
}