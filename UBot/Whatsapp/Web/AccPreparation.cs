using MemuLib.Core;
using UBot.Views.User;

namespace UBot.Whatsapp.Web;

public class AccPreparation
{
    public AccPreparation()
    {
        _usedPhones = new List<string>();
        _activePhones = new List<string>();
        _lock = new();
    }

    private readonly List<string> _activePhones;
    private readonly List<string> _usedPhones;
    private readonly object _lock;

    private bool _accountsNotFound;
    private FileInfo _logFile;
    private ActionProfileWork _currentProfile;

    public bool IsStop;

    public async Task Run(string message, ActionProfileWork actionProfileWork)
    {
        IsStop = false;
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_prep.txt");
        _logFile.Create().Close();

        _currentProfile = actionProfileWork;
        var mainTasks = new List<Task>();

        for (var cycleId = 0; cycleId < Globals.Setup.CountGroups; cycleId++)
        {
            var id = cycleId;
            await Task.Delay(100);
            mainTasks.Add(Task.Run(async () =>
            {
                var tasks = new List<Task>();

                while (!IsStop)
                {
                    DashboardView.GetInstance().AllTasks = Directory.GetDirectories(Globals.Setup.PathToFolderAccounts).Where(dir => File.Exists($@"{dir}\Data.json")).ToArray().Length;

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

                _usedPhones.RemoveAll(obj => obj[0].ToString() == id.ToString());
            }));
        }

        Task.WaitAll(mainTasks.ToArray(), -1);

        _accountsNotFound = false;

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

        if (_usedPhones.Contains(threadId + phone))
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
                        await Globals.TryMove(path, $@"{Globals.BanDirectory.FullName}\{phone}");
                        ++DashboardView.GetInstance().DeniedTasks;
                        return;
                    }

                    foreach (var warmPhone in phones.Where(_phone => _phone != phone))
                    {
                        if (!await client.Web!.SendText(warmPhone, messages[new Random().Next(0, messages.Length - 1)].Replace("\n", "\n").Replace("\r", "\r")))
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
                    var chatbots = allChatBots.Where(group => new Random().Next(0, 100) >= 50).ToList();

                    if (chatbots.Count == 0)
                        chatbots.Add(allChatBots[0]);

                    foreach (var chatbot in chatbots)
                        await client.Web!.SendText(chatbot, messages[new Random().Next(0, messages.Length - 1)]);
                }

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
                }

                await Task.Delay(2_000);

                if (!await client.Web!.IsConnected())
                {
                    await client.Web!.Free();
                    ++DashboardView.GetInstance().DeniedTasks;
                    await Globals.TryMove(path, $@"{Globals.BanDirectory.FullName}\{phone}");
                }
                else
                {
                    await client.Web!.Free();
                    ++client.AccountData.TrustLevelAccount;
                    await client.UpdateData();
                    ++DashboardView.GetInstance().CompletedTasks;
                    await Globals.TryMove(path, $@"{Globals.WarmedDirectory.FullName}\{phone}");
                }
            }

            _activePhones.Remove(threadId + phone);
        }
        catch (Exception ex)
        {
            Log.Write($"[Handler] - крит. ошибка: {ex.Message}\n", _logFile.FullName);
            await client.Web!.Free();
            ++DashboardView.GetInstance().DeniedTasks;
            await Globals.TryMove(path, $@"{Globals.BanDirectory.FullName}\{phone}");
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
    }
}