using MemuLib.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UBot.Pages.Dialogs;
using UBot.Pages.User;

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

    public async Task Run(string message, ActionProfileWork actionProfileWork)
    {
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_prep.txt");
        _logFile.Create().Close();

        _currentProfile = actionProfileWork;

        for (var cycleId = 0; cycleId < Globals.Setup.NumberRepetitionsActions; cycleId++)
        {
            var tasks = new List<Task>();

            for (var i = 0; i < Globals.Setup.CountThreads; i++)
            {
                var task = Handler(message.Split('\n'), cycleId + 1 == Globals.Setup.NumberRepetitionsActions);
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray(), -1);

            _accountsNotFound = false;
            _usedPhones.Clear();
        }
    }

    public void Stop()
    {
        _logFile = null;
        _usedPhones.Clear();
    }

    private async Task Handler(string[] messages, bool move)
    {
        var usedPhonesForWarm = new List<string>();
        var lastCountThreads = Globals.Setup.CountThreads;
        var waitEnd = false;

        while (true)
        {
            while (_activePhones.Count != 0 && waitEnd)
                await Task.Delay(500);

            waitEnd = false;

            usedPhonesForWarm.Clear();
            var result = Globals.GetAccounts(_usedPhones.ToArray(), true, _lock);

            if (result.Length == 0)
            {
                Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                _accountsNotFound = true;
                return;
            }

            var (phone, path) = result[0];

            if (_usedPhones.Contains(phone))
                return;

            _usedPhones.Add(phone);

            var client = new Client(phone, path);

            try
            {
                await client.Web!.Init(false, path);

                if (!await client.Web!.WaitForInChat())
                    throw new Exception("Cant connect");

                if (!client.Web!.IsConnected)
                    throw new Exception("Disconected");

                Log.Write($"[{phone}] - смогли войти\n", _logFile.FullName);
            }
            catch (Exception)
            {
                await client.Web!.Free(true);
                client.Web!.RemoveQueue();
                await Globals.TryMove(path, $@"{Globals.LogoutDirectory.FullName}\{phone}");
                Log.Write($"[{phone}] - не смогли войти\n", _logFile.FullName);
            }

            if (_currentProfile.CheckBan)
                continue;

            _activePhones.Add(phone);

            if (_currentProfile.Warm)
            {
                while (lastCountThreads != _activePhones.Count)
                {
                    if (_accountsNotFound)
                        break;

                    await Task.Delay(500);
                }

                if (_accountsNotFound)
                {
                    await client.Web!.Free(false);
                    client.Web!.RemoveQueue();
                    return;
                }

                while (true)//Первый этап - переписка каруселькою
                {
                    if (!client.Web!.IsConnected)
                    {
                        await client.Web!.Free(false);
                        client.Web!.RemoveQueue();
                        await Globals.TryMove(path, $@"{Globals.LogoutDirectory.FullName}\{phone}");
                        break;
                    }

                    var warmPhone = TakePhoneForWarm(phone);

                    if (string.IsNullOrEmpty(warmPhone))
                        break;

                    for (var i = 0; i < Globals.Setup.CountMessages; i++)
                        if (!await client.Web!.SendText(warmPhone, messages[new Random().Next(0, messages.Length - 1)]))
                            break;

                    usedPhonesForWarm.Add(warmPhone);
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

                if (!client.Web!.IsConnected)
                {
                    await client.Web!.Free(false);
                    client.Web!.RemoveQueue();
                    await Globals.TryMove(path, $@"{Globals.LogoutDirectory.FullName}\{phone}");
                    continue;
                }

                if (move)
                {
                    await client.Web!.Free(false);
                    client.Web!.RemoveQueue();
                    await Globals.TryMove(path, $@"{Globals.WarmedDirectory.FullName}\{phone}");
                }

                waitEnd = true;
            }

            _activePhones.Remove(phone);
        }
    
        string TakePhoneForWarm(string currentPhone)
        {
            foreach (var phone in _activePhones.ToArray())
                if (!usedPhonesForWarm.Contains(phone) && phone != currentPhone)
                    return phone;

            return string.Empty;
        }
    }
    static int test = 0;

}