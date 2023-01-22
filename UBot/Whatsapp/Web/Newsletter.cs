using MemuLib.Core;
using Microsoft.Maui.ApplicationModel.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UBot.Controls;
using UBot.Pages.Dialogs;
using UBot.Pages.User;
using UBot.Views.User;
using WPP4DotNet;

namespace UBot.Whatsapp.Web;

public class Newsletter
{
    public struct InfoTemplateNewsletter
    {
        public int AllPhones { get; set; }
        public int CurrentPhones { get; set; }
        public List<string> UsedPhonesUsers { get; set; }
        public string[] Contacts { get; set; }
        public FileInfo ReportFile { get; set; }
    }

    public Newsletter()
    {
        SendedMessagesCountFromAccount = new Dictionary<string, int>();
        TemplateMessagesInfo = new Dictionary<string, InfoTemplateNewsletter>();
        _usedPhones = new List<string>();
        _usedPhonesUsers = new List<string>();

        _lock = new();
    }

    public readonly Dictionary<string, int> SendedMessagesCountFromAccount;
    public readonly Dictionary<string, InfoTemplateNewsletter> TemplateMessagesInfo;
    private readonly List<string> _usedPhones;
    private readonly List<string> _usedPhonesUsers;
    private readonly object _lock;

    private string[] _contacts;
    private FileInfo _logFile;
    private FileInfo _reportFile;
    private FileInfo _badProxyFile;
    private ActionProfileWork _currentProfile;


    public bool IsStop;

    public async Task Run(ActionProfileWork actionProfileWork)
    {
        IsStop = false;
        _currentProfile = actionProfileWork;

        SendedMessagesCountFromAccount.Clear();
        TemplateMessagesInfo.Clear();

        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_log.txt");

        if (File.Exists(Globals.Setup.PathToFileProxy))
        {
            _badProxyFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_badproxy.txt");
            _badProxyFile.Create().Close();
        }

        _logFile.Create().Close();

        var tasks = new List<Task>();

        if (_currentProfile.TemplateMessages.Count != 0)
        {
            DashboardView.GetInstance().CompletedTasks = -1;
            DashboardView.GetInstance().AllTasks = -1;

            foreach (var template in _currentProfile.TemplateMessages)
            {
                var tmpContacts = await File.ReadAllLinesAsync(template.PathPhones.FullName);
                TemplateMessagesInfo[template.Tag] = new InfoTemplateNewsletter()
                {
                    UsedPhonesUsers = new List<string>(),
                    AllPhones = tmpContacts.Count(contact => !string.IsNullOrEmpty(contact) && contact.Length > 5),
                    Contacts = tmpContacts,
                    CurrentPhones = 0,
                    ReportFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{template.PathPhones.Name}_report.txt")
                };

                if (!TemplateMessagesInfo[template.Tag].ReportFile.Exists)
                    TemplateMessagesInfo[template.Tag].ReportFile.Create().Close();
            }
        }
        else
        {
            _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToFilePhones);
            _reportFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{new FileInfo(Globals.Setup.PathToFilePhones).Name}_report.txt");

            if (!_reportFile.Exists)
                _reportFile.Create().Close();

            DashboardView.GetInstance().CompletedTasks = 0;
            DashboardView.GetInstance().AllTasks = _contacts.Count(contact => !string.IsNullOrEmpty(contact) && contact.Length > 5);
        }

        for (var repeatId = 0; repeatId < Globals.Setup.RepeatCounts; repeatId++)
        {
            for (var i = 0; i < Globals.Setup.CountThreads; i++)
                tasks.Add(Task.Run(async () => await Handler()));

            Task.WaitAll(tasks.ToArray(), -1);

            tasks.Clear();

            if (IsStop || _usedPhones.Count == 0)
                break;

            _usedPhones.Clear();
        }

        if (TemplateMessagesInfo.Count == 0)
            File.WriteAllLines(Globals.Setup.PathToFilePhones, _contacts.Where(cont => !_usedPhonesUsers.Contains(cont)));
        else
            foreach (var template in _currentProfile.TemplateMessages)
                File.WriteAllLines(template.PathPhones.FullName, TemplateMessagesInfo[template.Tag].Contacts.Where(cont => !TemplateMessagesInfo[template.Tag].UsedPhonesUsers.Contains(cont)));

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
        var messagesToWarm = File.Exists(Globals.Setup.PathToFileTextWarm) ? (await File.ReadAllTextAsync(Globals.Setup.PathToFileTextWarm)).Split('\n') : null;

        while (!IsStop)
        {
            var result = Globals.GetAccounts(_usedPhones.ToArray(), true, _lock);
            Log.Write($"[I] - кол-во аккаунтов {result.Length}\n", _logFile.FullName);

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
                if (ex.Message != "Cant load account" || result.Length <= 1)
                {
                    await Globals.TryMove(path, $@"{Globals.LogoutDirectory.FullName}\{phone}");
                    ++DashboardView.GetInstance().DeniedTasksStart;
                    Log.Write($"[{phone}] - не смогли войти: {ex.Message}\n", _logFile.FullName);
                }
                else
                    _usedPhones.Remove(phone);

                continue;
            }

            if (Globals.Setup.AdditionalWarm)
            {
                var stepsAdditionalWarm = new List<Task>();
                stepsAdditionalWarm.Add(WarmChatBots());
                stepsAdditionalWarm.Add(WarmGroups());
                //stepsAdditionalWarm.Add(WarmPeoples());

                foreach (var step in stepsAdditionalWarm.OrderBy(x => new Random().Next()).ToArray())
                    await step;
            }

            var peopleReal = string.Empty;
            var index = 0;

            try
            {
                if (Globals.Setup.RemoveAvatar)
                    await client.Web!.RemoveAvatar();

                var currentMinus = (int)Math.Floor((float)Globals.Setup.DelaySendMessageTo * 1000f);
                if (await client.Web!.CountOfChats() >= (Globals.Setup.CountOfChatCrit ?? 0))
                {
                    await Task.Delay((Globals.Setup.DelayFirstMessageAccount ?? 0) * 1000);
                    while (!IsStop)
                    {
                        if (countSendedMessages >= Globals.Setup.CountMessages * ((_currentProfile.TemplateMessages.Count == 0) ? 1 : _currentProfile.TemplateMessages.Count))
                            break;

                        if (TemplateMessagesInfo.Count != 0)
                        {
                            var foundNumber = false;
                            foreach (var template in TemplateMessagesInfo)
                                if (template.Value.Contacts.Except(template.Value.UsedPhonesUsers).ToArray().Length > 0)
                                {
                                    foundNumber = true;
                                    break;
                                }

                            if (!foundNumber)
                                break;
                        }
                        else
                        {
                            if (_contacts.Except(_usedPhonesUsers).ToArray().Length == 0)
                                break;
                        }

                        for (var i = 0; i < (TemplateMessagesInfo.Count == 0 ? 1 : TemplateMessagesInfo.Count); i++)
                        {
                            index = i;
                            (string contactPhone, string contactMessage) = GetFreeNumberUser(i);
                            peopleReal = contactPhone;

                            if (string.IsNullOrEmpty(peopleReal))
                                continue;

                            if (!await client.Web!.IsConnected())
                                throw new Exception("Client has disconected");

                            if (await client.Web!.CheckValidPhone(peopleReal))
                            {
                                var text = string.Join('\n', (!string.IsNullOrEmpty(contactMessage) ? contactMessage.Split('\r').ToList() : (TemplateMessagesInfo.Count == 0) ? DashboardView.GetInstance().Text.Split('\r').ToList() : _currentProfile.TemplateMessages[i].Text.Split('\r').ToList()));
                                FileInfo? image = null;

                                string? buttonText = null;
                                string title = string.Empty;
                                string footer = string.Empty;
                                //\{path(.*?)=(.*?)\}

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

                                if (await client.Web!.SendText(peopleReal, SelectWord(text), image, buttonText, title, footer))
                                {
                                    if (_currentProfile.TemplateMessages.Count == 0)
                                    {
                                        ++DashboardView.GetInstance().CompletedTasks;
                                        Log.Write($"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};{peopleReal}", _reportFile.FullName);
                                    }
                                    else
                                    {
                                        InfoTemplateNewsletter profile = TemplateMessagesInfo[_currentProfile.TemplateMessages[i].Tag];
                                        ++profile.CurrentPhones;
                                        TemplateMessagesInfo[_currentProfile.TemplateMessages[i].Tag] = profile;
                                        Log.Write($"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};{peopleReal}", profile.ReportFile.FullName);
                                    }

                                    if (++countSendedMessages >= Globals.Setup.CountMessages * ((_currentProfile.TemplateMessages.Count == 0) ? 1 : _currentProfile.TemplateMessages.Count))
                                        break;
                                }
                                else
                                {
                                    if (_currentProfile.TemplateMessages.Count == 0)
                                        _usedPhonesUsers.Remove(peopleReal);
                                    else
                                        TemplateMessagesInfo[_currentProfile.TemplateMessages[i].Tag].UsedPhonesUsers.Remove(peopleReal);
                                }
                            }
                        }

                        var minus = (int)((Globals.Setup.DynamicDelaySendMessageMinus ?? 0) * 1000f);

                        await Task.Delay(minus <= 0 ? new Random().Next((int)Math.Floor((float)Globals.Setup.DelaySendMessageFrom * 1000f), (int)Math.Floor((float)Globals.Setup.DelaySendMessageTo * 1000f)) : currentMinus -= minus);
                    }
                }
            }
            catch (Exception ex)
            {
                SendedMessagesCountFromAccount[phone] = countSendedMessages;

                if (countSendedMessages < (Globals.Setup.BlackProxyDeleteBefore is null or 0 ? 10 : Globals.Setup.BlackProxyDeleteBefore) && File.Exists(Globals.Setup.PathToFileProxy))
                {
                    badProxyList.Add(proxy);
                }

                if (!string.IsNullOrEmpty(Globals.Setup.LinkToChangeIP))
                    Log.Write(await ResourceHelper.GetAsync(Globals.Setup.LinkToChangeIP), _logFile.FullName);

                if (_currentProfile.TemplateMessages.Count == 0)
                    _usedPhonesUsers.Remove(peopleReal);
                else
                    TemplateMessagesInfo[_currentProfile.TemplateMessages[index].Tag].UsedPhonesUsers.Remove(peopleReal);

                await client.Web!.Free();
                ++DashboardView.GetInstance().DeniedTasks;
                ++DashboardView.GetInstance().DeniedTasksWork;
                await Globals.TryMove(path, $@"{Globals.WebBanWorkDirectory.FullName}\{phone}");

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
            if (_currentProfile.TemplateMessages.Count == 0)
            {
                if (!_contacts.Any(cont => !_usedPhonesUsers.Contains(cont)))
                    break;
            }
            else
            {
                var template = TemplateMessagesInfo[_currentProfile.TemplateMessages[index].Tag];
                if (!template.Contacts.Any(cont => !template.UsedPhonesUsers.Contains(cont)))
                    break;
            }

            (string phone, string msg) GetFreeNumberUser(int index)
            {
                lock (_lock)
                {
                    var tag = (TemplateMessagesInfo.Count == 0) ? "" : _currentProfile.TemplateMessages[index].Tag;
                    InfoTemplateNewsletter profile = (TemplateMessagesInfo.Count == 0) ? default : TemplateMessagesInfo[tag];

                    foreach (var contact in (TemplateMessagesInfo.Count == 0) ? _contacts : profile.Contacts)
                    {
                        if (!((TemplateMessagesInfo.Count == 0) ? _usedPhonesUsers.Contains(contact) : profile.UsedPhonesUsers.Contains(contact)))
                        {
                            if (contact.Length < 5 || string.IsNullOrEmpty(contact))
                                continue;

                            if (TemplateMessagesInfo.Count == 0)
                                _usedPhonesUsers.Add(contact);
                            else
                                profile.UsedPhonesUsers.Add(contact);

                            var newContacts = (TemplateMessagesInfo.Count == 0) ? _contacts.Except(_usedPhonesUsers).ToArray() : profile.Contacts.Except(profile.UsedPhonesUsers).ToArray();

                            if (TemplateMessagesInfo.Count == 0)
                                DashboardView.GetInstance().AllTasks = newContacts.Length;
                            else
                                profile.AllPhones = newContacts.Length;

                            if (_usedPhonesUsers.Count % 100 == 0)
                                File.WriteAllLines((TemplateMessagesInfo.Count == 0) ? Globals.Setup.PathToFilePhones : _currentProfile.TemplateMessages[index].PathPhones.FullName, newContacts);

                            if (TemplateMessagesInfo.Count != 0)
                                TemplateMessagesInfo[tag] = profile;

                            var dataContact = contact.Split(';');

                            return (dataContact[0][0] == '+' ? dataContact[0].Remove(0, 1) : dataContact[0], dataContact.Length > 1 ? dataContact[1] : string.Empty);
                        }
                    }

                    return (string.Empty, string.Empty);
                }
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
                if (File.Exists(Globals.Setup.PathToFileChatBots) && messagesToWarm != null && messagesToWarm.Length > 0)
                {
                    var allChatBots = await File.ReadAllLinesAsync(Globals.Setup.PathToFileChatBots);
                    var chatbots = allChatBots.Where(group => new Random().Next(0, 100) >= 50).ToList().Take(new Random().Next(Globals.Setup.WriteChatBotsFrom ?? 1, Globals.Setup.WriteChatBotsTo ?? 1)).ToList();

                    if (chatbots.Count == 0)
                        chatbots.Add(allChatBots[0]);

                    foreach (var chatbot in chatbots)
                        await client.Web!.SendText(chatbot, messagesToWarm[new Random().Next(0, messagesToWarm.Length - 1)]);
                }
            }

            /*async Task WarmPeoples()
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
            }*/
        }
    }
}