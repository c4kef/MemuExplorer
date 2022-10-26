using MemuLib.Core;
using Microsoft.Maui.ApplicationModel.Communication;
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

namespace UBot.Whatsapp.Web;

public class Newsletter
{
    public Newsletter()
    {
        SendedMessagesCountFromAccount = new Dictionary<string, int>();
        _usedPhones = new List<string>();
        _usedPhonesUsers = new List<string>();

        _lock = new();
    }

    public readonly Dictionary<string, int> SendedMessagesCountFromAccount;
    private readonly List<string> _usedPhones;
    private readonly List<string> _usedPhonesUsers;
    private readonly object _lock;

    private string[] _contacts;
    private FileInfo _logFile;
    private FileInfo _reportFile;
    private FileInfo _badProxyFile;

    public bool IsStop;

    public async Task Run()
    {
        IsStop = false;
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_log.txt");
        _reportFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_report.txt");

        if (File.Exists(Globals.Setup.PathToFileProxy))
        {
            _badProxyFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_badproxy.txt");
            _badProxyFile.Create().Close();
        }

        _logFile.Create().Close();
        _reportFile.Create().Close();

        var tasks = new List<Task>();

        _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToFilePhones);

        Log.Write($"Добро пожаловать в логи, текст рассылки:\n{DashboardView.GetInstance().Text}\n\n", _logFile.FullName);

        DashboardView.GetInstance().AllTasks = _contacts.Count(contact => !string.IsNullOrEmpty(contact) && contact.Length > 5);

        for (var i = 0; i < Globals.Setup.CountThreads; i++)
            tasks.Add(Task.Run(async () => await Handler()));

        Task.WaitAll(tasks.ToArray(), -1);

        if (IsStop)
            File.WriteAllLines(Globals.Setup.PathToFilePhones, _contacts.Where(cont => !_usedPhonesUsers.Contains(cont)));

        tasks.Clear();

        Stop();
    }

    public void Stop()
    {
        _logFile = null;
        _reportFile = null;
        IsStop = false;

        _usedPhonesUsers.Clear();
        _usedPhones.Clear();
    }

    private async Task Handler()
    {
        var badProxyList = new List<string>();
        var messagesToWam = File.Exists(Globals.Setup.PathToFileTextWarm) ? (await File.ReadAllTextAsync(Globals.Setup.PathToFileTextWarm)).Split('\n') : null;
        while (!IsStop)
        {
            var result = Globals.GetAccounts(_usedPhones.ToArray(), true, _lock);

            if (result.Length == 0)
            {
                Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                return;
            }

            var (phone, path) = result[0];

            if (_usedPhones.Contains(phone))
            {
                Log.Write($"[I] - дубликат аккаунта\n", _logFile.FullName);
                continue;
            }

            _usedPhones.Add(phone);

            var countSendedMessages = 0;

            var client = new Client(phone, path);
            var proxy = GetProxy();

            if (proxy == "0")
            {
                Log.Write($"[I] - прокси не было найдено\n", _logFile.FullName);
                return;
            }

            try
            {
                await client.Web!.Init(false, $@"{path}\{new DirectoryInfo(path).Name}", proxy);

                Log.Write($"[{phone}] - смогли войти\n", _logFile.FullName);
            }
            catch (Exception ex)
            {
                await client.Web!.Free();
                await Globals.TryMove(path, $@"{Globals.LogoutDirectory.FullName}\{phone}");
                Log.Write($"[{phone}] - не смогли войти: {ex.Message}\n", _logFile.FullName);
                continue;
            }

            if (Globals.Setup.AdditionalWarm)
            {
                var stepsAdditionalWarm = new List<Task>();
                stepsAdditionalWarm.Add(WarmChatBots());
                stepsAdditionalWarm.Add(WarmGroups());
                stepsAdditionalWarm.Add(WarmPeoples());

                foreach (var step in stepsAdditionalWarm.OrderBy(x => new Random().Next()).ToArray())
                    await step;
            }

            var peopleReal = string.Empty;

            try
            {
                if (Globals.Setup.RemoveAvatar)
                    await client.Web!.RemoveAvatar();

                var currentMinus = (int)Math.Floor((float)Globals.Setup.DelaySendMessageTo * 1000f);

                while (!IsStop)
                {
                    peopleReal = GetFreeNumberUser();
                    if (string.IsNullOrEmpty(peopleReal))
                        break;

                    if (!await client.Web!.IsConnected())
                        throw new Exception("Client has disconected");

                    if (await client.Web!.CheckValidPhone(peopleReal))
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
                            else if(match.Contains(Globals.TagFooter))
                            {
                                footer = match.Remove(0, Globals.TagFooter.Length);
                            }
                            else if (match.Contains(Globals.TagButton))
                            {
                                buttonText = match.Remove(0, Globals.TagButton.Length);
                            }
                        }

                        if (await client.Web!.SendText(peopleReal, SelectWord(new Regex(@"\{([^)]*)\}").Replace(text, "").Replace("\"", "").Replace("\'", "")), image, buttonText, title, footer))
                        {
                            ++DashboardView.GetInstance().CompletedTasks;
                            Log.Write(
                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};{peopleReal}",
                            _reportFile.FullName);

                            if (++countSendedMessages >= Globals.Setup.CountMessages)
                                break;

                            var minus = (int)((Globals.Setup.DynamicDelaySendMessageMinus ?? 0) * 1000f);

                            await Task.Delay(minus != 0 ? new Random().Next((int)Math.Floor((float)Globals.Setup.DelaySendMessageFrom * 1000f), (int)Math.Floor((float)Globals.Setup.DelaySendMessageTo * 1000f)) : currentMinus -= minus);
                        }
                        else
                        {
                            _usedPhonesUsers.Remove(peopleReal);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendedMessagesCountFromAccount[phone] = countSendedMessages;

                if (countSendedMessages < 10 && File.Exists(Globals.Setup.PathToFileProxy))
                {
                    badProxyList.Add(proxy);
                }

                _usedPhonesUsers.Remove(peopleReal);

                await client.Web!.Free();
                ++DashboardView.GetInstance().DeniedTasks;
                await Globals.TryMove(path, $@"{Globals.WebBanDirectory.FullName}\{phone}");

                var count = 0;
                var messages = SendedMessagesCountFromAccount.TakeLast(10);

                foreach (var msg in messages)
                    count += msg.Value;

                DashboardView.GetInstance().AverageMessages = (int)Math.Floor((decimal)count / messages.Count());

                count = 0;
                foreach (var msg in SendedMessagesCountFromAccount)
                    count += msg.Value;

                DashboardView.GetInstance().AverageAllMessages = (int)Math.Floor((decimal)count / SendedMessagesCountFromAccount.Count);

                Log.Write($"[MessageSender] - Ошибка, возможно клиент забанен: {ex.Message}\n", _logFile.FullName);
                continue;
            }

            //Успех
            await Task.Delay(2_000);
            await client.Web!.Free();

            if (!_contacts.Any(cont => !_usedPhonesUsers.Contains(cont)))
                break;

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

                            DashboardView.GetInstance().AllTasks = _contacts.Count(cont => !_usedPhonesUsers.Contains(cont));

                            if (_usedPhonesUsers.Count % 100 == 0)
                                File.WriteAllLines(Globals.Setup.PathToFilePhones, _contacts.Where(cont => !_usedPhonesUsers.Contains(cont)));

                            return contact[0] == '+' ? contact.Remove(0, 1) : contact;
                        }
                    }

                    return string.Empty;
                }
            }

            string SelectWord(string value)
            {
                var backValue = value;
                foreach (var match in new Regex(@"(\w+)\|\|(\w+)", RegexOptions.Multiline).Matches(backValue))
                    backValue = backValue.Replace(match.ToString()!, match.ToString()!.Split("||")[new Random().Next(0, 100) >= 50 ? 1 : 0]);

                return backValue;
            }

            string GetProxy()
            {
                lock (_lock)
                {
                    if (!File.Exists(Globals.Setup.PathToFileProxy))
                        return "";

                    var proxyList = File.ReadAllLines(Globals.Setup.PathToFileProxy).ToList();

                    proxyList.RemoveAll(badProxyList.ToArray().Contains);

                    File.WriteAllLines(Globals.Setup.PathToFileProxy, proxyList.ToArray());

                    if (proxyList.Count == 0)
                        return "0";

                    return proxyList.OrderBy(x => new Random().Next()).ToArray()[0];
                }
            }

            async Task WarmGroups()
            {
                if (File.Exists(Globals.Setup.PathToFileGroups))
                {
                    var allGroups = await File.ReadAllLinesAsync(Globals.Setup.PathToFileGroups);
                    var groups = allGroups.Where(group => new Random().Next(0, 100) >= 50).ToList().Take(new Random().Next(Globals.Setup.JoinToGroupsFrom ?? 1, Globals.Setup.JoinToGroupsTo ?? 1)).ToList();

                    if (groups.Count == 0)
                        groups.Add(allGroups[0]);

                    foreach (var group in groups)
                        await client.Web!.JoinGroup(group);
                }
            }

            async Task WarmChatBots()
            {
                if (File.Exists(Globals.Setup.PathToFileChatBots) && messagesToWam != null && messagesToWam.Length > 0)
                {
                    var allChatBots = await File.ReadAllLinesAsync(Globals.Setup.PathToFileChatBots);
                    var chatbots = allChatBots.Where(group => new Random().Next(0, 100) >= 50).ToList().Take(new Random().Next(Globals.Setup.WriteChatBotsFrom ?? 1, Globals.Setup.WriteChatBotsTo ?? 1)).ToList();

                    if (chatbots.Count == 0)
                        chatbots.Add(allChatBots[0]);

                    foreach (var chatbot in chatbots)
                        await client.Web!.SendText(chatbot, messagesToWam[new Random().Next(0, messagesToWam.Length - 1)]);
                }
            }

            async Task WarmPeoples()
            {
                if (File.Exists(Globals.Setup.PathToFilePeoples) && messagesToWam != null && messagesToWam.Length > 0)
                {
                    var allPeoples = await File.ReadAllLinesAsync(Globals.Setup.PathToFilePeoples);
                    var localMessages = new List<string>();

                    if (File.Exists(Globals.Setup.PathToFileTextPeopleWarm))
                        localMessages.AddRange(await File.ReadAllLinesAsync(Globals.Setup.PathToFileTextPeopleWarm));

                    var peoples = allPeoples.Where(group => new Random().Next(0, 100) >= 50).ToList().Take(new Random().Next(Globals.Setup.WritePeoplesWarmFrom ?? 1, Globals.Setup.WritePeoplesWarmTo ?? 1)).ToList();

                    if (peoples.Count == 0)
                        peoples.Add(allPeoples[0]);

                    foreach (var people in peoples)
                        await client.Web!.SendText(people, localMessages.Count == 0 ? messagesToWam[new Random().Next(0, messagesToWam.Length - 1)] : localMessages[new Random().Next(0, localMessages.Count - 1)]);
                }
            }
        }
    }
}