using MemuLib.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UBot.Pages.Dialogs;
using UBot.Pages.User;
using UBot.Views.User;
using WPP4DotNet;
using WPP4DotNet.WebDriver;
using ZXing.QrCode.Internal;
using static System.Net.Mime.MediaTypeNames;

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

        for (var cycleId = 0; cycleId < Globals.Setup.NumberRepetitionsActions; cycleId++)
        {
            var tasks = new List<Task>();

            while (!IsStop)
            {
                DashboardView.GetInstance().AllTasks = Directory.GetDirectories(Globals.Setup.PathToFolderAccounts).Where(dir => File.Exists($@"{dir}\Data.json")).ToArray().Length;

                for (var i = 0; i < Globals.Setup.CountThreads; i++)
                    tasks.Add(Task.Run(async () => await Handler(message.Split('\n'), cycleId + 1 == Globals.Setup.NumberRepetitionsActions)));

                Task.WaitAll(tasks.ToArray(), -1);

                _activePhones.Clear();
                tasks.Clear();

                if (_accountsNotFound)
                    break;
            }

            _accountsNotFound = false;

            _usedPhones.Clear();
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

    private async Task Handler(string[] messages, bool move)
    {
        var lastCountThreads = Globals.Setup.CountThreads;
    getAccount:
        if (IsStop)
            return;

        var result = Globals.GetAccounts(_usedPhones.ToArray(), true, _lock);

        if (result.Length == 0)
        {
            Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
            _accountsNotFound = true;
            return;
        }

        var (phone, path) = result[0];

        if (_usedPhones.Contains(phone))
        {
            Log.Write($"[I] - дубликат аккаунта\n", _logFile.FullName);
            goto getAccount;
        }

        _usedPhones.Add(phone);

        var client = new Client(phone, path);

        try
        {
            await client.Web!.Init(false, $@"{path}\{new DirectoryInfo(path).Name}");

            Log.Write($"[{phone}] - смогли войти\n", _logFile.FullName);
        }
        catch (Exception ex)
        {
            client.Web!.Free();
            await Globals.TryMove(path, $@"{Globals.LogoutDirectory.FullName}\{phone}");
            Log.Write($"[{phone}] - не смогли войти: {ex.Message}\n", _logFile.FullName);

            ++DashboardView.GetInstance().DeniedTasks;
            goto getAccount;
        }

        if (_currentProfile.CheckBan)
        {
            client.Web!.Free();
            ++DashboardView.GetInstance().CompletedTasks;
            goto getAccount;
        }

        try
        {
            _activePhones.Add(phone);

            if (_currentProfile.Warm)
            {
                while (lastCountThreads != _activePhones.Count)
                {
                    if (_accountsNotFound || IsStop)
                        break;

                    await Task.Delay(500);
                }

                if (_accountsNotFound || IsStop)
                {
                    client.Web!.Free();
                    return;
                }

                var phones = _activePhones.ToArray();

                for (var i = 0; i < Globals.Setup.CountMessages; i++)//Первый этап - переписки между собой
                {
                    if (!await client.Web!.IsConnected())
                    {
                        client.Web!.Free();
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

                if (!await client.Web!.IsConnected())
                {
                    client.Web!.Free();
                    ++DashboardView.GetInstance().DeniedTasks;
                    await Globals.TryMove(path, $@"{Globals.BanDirectory.FullName}\{phone}");
                }
                else
                {
                    client.Web!.Free();

                    if (move)
                    {
                        ++DashboardView.GetInstance().CompletedTasks;
                        await Globals.TryMove(path, $@"{Globals.WarmedDirectory.FullName}\{phone}");
                    }
                }
            }

            _activePhones.Remove(phone);
        }
        catch (Exception ex)
        {
            Log.Write($"[Handler] - крит. ошибка: {ex.Message}\n", _logFile.FullName);
            client.Web!.Free();
            ++DashboardView.GetInstance().DeniedTasks;
            await Globals.TryMove(path, $@"{Globals.BanDirectory.FullName}\{phone}");
        }
    }
}