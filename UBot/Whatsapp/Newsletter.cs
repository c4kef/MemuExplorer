using MemuLib.Core;
using MemuLib.Core.Contacts;
using Microsoft.Maui.ApplicationModel.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UBot.Controls;
using UBot.Pages.Dialogs;
using UBot.Pages.User;
using UBot.Views.User;
using WPP4DotNet;
using static Microsoft.Maui.ApplicationModel.Permissions;
using static System.Net.Mime.MediaTypeNames;

namespace UBot.Whatsapp
{
    public class Newsletter
    {
        public Newsletter()
        {
            SendedMessagesCountFromAccount = new Dictionary<string, int>();
            _usedPhones = new List<string>();
            _usedPhonesUsers = new List<string>();
            _waitingAnswer = new List<string>();
            _badProxyList = new List<string>();

            _lock = new();
            _lock1 = new();
        }

        public readonly Dictionary<string, int> SendedMessagesCountFromAccount;
        private readonly List<string> _usedPhones;
        private readonly List<string> _usedPhonesUsers;
        private readonly List<string> _waitingAnswer;
        private readonly object _lock;
        private readonly object _lock1;

        private string[] _names;
        private string[] _contacts;
        private FileInfo _logFile;
        private FileInfo _reportFile;
        private FileInfo _notSendedFile;
        private List<string> _badProxyList;
        private ActionProfileWork _currentProfile;
        private Client deviceAnswer;

        public bool IsStop;

        public async Task Run(ActionProfileWork actionProfileWork)
        {
            IsStop = false;

            await Globals.InitAccountsFolder();

            _names = (await File.ReadAllLinesAsync(Globals.Setup.PathToFileNames)).Where(name => new Regex("^[a-zA-Z0-9. -_?]*$").IsMatch(name)).ToArray();
            _currentProfile = actionProfileWork;

            _waitingAnswer.Clear();
            _badProxyList.Clear();
            SendedMessagesCountFromAccount.Clear();

            _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_log.txt");
            _logFile.Create().Close();

            var tasks = new List<Task>();

            _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToFilePhones);
            _reportFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{new FileInfo(Globals.Setup.PathToFilePhones).Name}_report.txt");
            _notSendedFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{new FileInfo(Globals.Setup.PathToFilePhones).Name}_not_sended.txt");
            if (!_reportFile.Exists)
                _reportFile.Create().Close();
            if (!_notSendedFile.Exists)
                _notSendedFile.Create().Close();

            DashboardView.GetInstance().CompletedTasks = 0;
            DashboardView.GetInstance().AllTasks = _contacts.Count(contact => !string.IsNullOrEmpty(contact) && contact.Length > 5);

            for (var repeatId = 0; repeatId < Globals.Setup.RepeatCounts; repeatId++)
            {
                var busyDevices = new Dictionary<DataEmulator, int>();
                DataEmulator[] devices = null;

                devices = ManagerView.GetInstance().Emulators.Where(device => !busyDevices.Select(device => device.Key.Index).Contains(device.Index) && device.IsEnabled).ToArray();

                if (devices.Length < 2)
                {
                    Log.Write($"[I] - Больше девайсов не найдено, остаточное кол-во: {devices.Length}\n", _logFile.FullName);
                    return;
                }

                deviceAnswer = new Client(deviceId: devices[0].Index);

                foreach (var device in devices)
                    busyDevices[device] = repeatId;

                var tasksAnswer = new List<Task>();

                tasksAnswer.Add(Task.Run(async () =>
                {
                    var _usedPhonesAnswer = new Dictionary<string, bool>();
                    var contactPhones = new List<CObj>();
                    foreach (var phoneForAnswer in Directory.GetDirectories(Globals.Setup.PathToFolderAccounts).Select(dir => new DirectoryInfo(dir).Name))
                        contactPhones.Add(new(_names[new Random().Next(0, _names.Length)], $"+{phoneForAnswer}"));

                    await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{deviceAnswer.Phone.Replace("+", "")}_contacts.vcf", ContactManager.Export(contactPhones));

                    await deviceAnswer.ImportContacts($@"{Globals.TempDirectory.FullName}\{deviceAnswer.Phone.Replace("+", "")}_contacts.vcf");

                    while (!IsStop)
                    {
                        var (phone, path) = ("", "");
                        try
                        {
                            var result = Globals.GetAccounts(_usedPhonesAnswer.Keys.ToArray(), true, _lock1, pathToFolderAccounts: Globals.Setup.PathToFolderAccountsAdditional);
                            if (result.Length < 1)
                            {
                                if (_usedPhonesAnswer.Count == 0)
                                {
                                    Log.Write($"[I] [{deviceAnswer.GetInstance().Index}] - аккаунт не был найден для хандлера\n", _logFile.FullName);
                                    IsStop = true;
                                    return;
                                }

                                _usedPhonesAnswer = _usedPhonesAnswer.ToArray().Where(phone => phone.Value).ToDictionary(phone => phone.Key, phone => phone.Value);//P-s хз, может сча будет работать в многопоток
                                continue;
                            }
                            (phone, path) = result[0];
                            Log.Write($"[I] [{deviceAnswer.GetInstance().Index}] - взяли аккаунт {phone} {path}\n", _logFile.FullName);

                            if (_usedPhonesAnswer.Keys.ToArray().Contains(phone))
                            {
                                Log.Write($"[I] [{deviceAnswer.GetInstance().Index}] - дубликат аккаунта\n", _logFile.FullName);
                                continue;
                            }

                            _usedPhonesAnswer[phone] = true;

                            if (!await TryLogin(deviceAnswer, phone, path))
                            {
                                Log.Write($"[{phone}] [{deviceAnswer.GetInstance().Index}] - не смогли войти\n", _logFile.FullName);
                                _usedPhonesAnswer[phone] = false;
                                continue;
                            }
                            else
                                Log.Write($"[{phone}] [{deviceAnswer.GetInstance().Index}] - смогли войти\n", _logFile.FullName);

                            await Task.Delay((Globals.Setup.TakeCountRandomAccountDelay ?? 1) * 1000);

                            if (!await deviceAnswer.IsValid())
                            {
                                Log.Write($"[IsValid ANSWER] [{deviceAnswer.GetInstance().Index}] - Считаю аккаунт не валидным и кидаю в чс\n", _logFile.FullName);
                                await DeleteAccount(deviceAnswer, false);
                            }

                            while (!IsStop)
                            {
                                foreach (var botPhone in _waitingAnswer.ToArray())
                                {
                                    if (!await deviceAnswer.SendMessage(botPhone, "Hi!", waitDelivered: true))
                                        break;
                                    else
                                        _waitingAnswer.Remove(botPhone);
                                }

                                if (!await deviceAnswer.IsValid())
                                {
                                    Log.Write($"[IsValid ANSWER] [{deviceAnswer.GetInstance().Index}] - Считаю аккаунт не валидным и кидаю в чс\n", _logFile.FullName);
                                    await DeleteAccount(deviceAnswer, false);
                                    break;
                                }
                            }

                            _usedPhonesAnswer[phone] = false;
                        }
                        catch (Exception ex)
                        {
                            Log.Write($"[CRITICAL - HANDLER ANSWER] [{deviceAnswer.GetInstance().Index}] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
                            _usedPhonesAnswer[phone] = false;
                        }
                    }

                }));

                foreach (var device in devices)
                {
                    if (device.Index == deviceAnswer.GetInstance().Index)
                        continue;

                    tasks.Add(Handler(new Client(deviceId: device.Index)));
                    await Task.Delay(1_500);
                }

                Task.WaitAll(tasks.ToArray(), -1);
                
                IsStop = true;

                Task.WaitAll(tasksAnswer.ToArray(), -1);

                tasks.Clear();
                tasksAnswer.Clear();

                if (IsStop || _usedPhones.Count == 0)
                    break;

                _usedPhones.Clear();

                foreach (var busyDevice in busyDevices.Where(device => device.Value == repeatId))
                    busyDevices.Remove(busyDevice.Key);
            }

            File.WriteAllLines(Globals.Setup.PathToFilePhones, _contacts.Where(cont => !_usedPhonesUsers.Contains(cont)));
            Stop();
        }

        public void Stop()
        {
            _logFile = null;
            _reportFile = null;
            IsStop = false;

            _usedPhonesUsers.Clear();
            _usedPhones.Clear();
            _waitingAnswer.Clear();
            _badProxyList.Clear();
        }

        private async Task Handler(Client client)
        {
            await client.GetInstance().ShellCmd("settings put global window_animation_scale 0");
            await client.GetInstance().ShellCmd("settings put global transition_animation_scale 0");
            await client.GetInstance().ShellCmd("settings put global animator_duration_scale 0");

            while (!IsStop)
            {
                var result = Globals.GetAccounts(_usedPhones.ToArray(), true, _lock);

                if (result.Length < 1)
                {
                    Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                    IsStop = true;
                    return;
                }

                var (phone, path) = result[0];

                if (_usedPhones.Select(phone => phone.Remove(0, 1)).Contains(phone))
                {
                    Log.Write($"[I] - дубликат аккаунта\n", _logFile.FullName);
                    continue;
                }

                _usedPhones.Add(phone);
                var countSendedMessages = 0;

                await client.GetInstance().StopApk(client.PackageName);

                var contactPhones = new List<CObj>();
                foreach (var phoneForAnswer in Directory.GetDirectories(Globals.Setup.PathToFolderAccountsAdditional).Select(dir => new DirectoryInfo(dir).Name))
                    contactPhones.Add(new(_names[new Random().Next(0, _names.Length)], $"+{phoneForAnswer}"));

                var _currentContacts = _contacts.ToArray().OrderBy(x => new Random().Next()).Take(500);
                foreach (var phoneForContact in _currentContacts)
                    contactPhones.Add(new(_names[new Random().Next(0, _names.Length)], "+" + phoneForContact));

                await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf", ContactManager.Export(contactPhones));

                await client.ImportContacts($@"{Globals.TempDirectory.FullName}\{phone}_contacts.vcf");

                await client.GetInstance().Shell($"pm clear {client.PackageName}");

                if (!await TryLogin(client, phone, path))
                {
                    ++DashboardView.GetInstance().DeniedTasks;
                    ++DashboardView.GetInstance().DeniedTasksStart;
                    Log.Write($"[{phone}] - не смогли войти\n", _logFile.FullName);
                    continue;
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
                    continue;
                }

                var peopleReal = string.Empty;

                try
                {
                    var currentMinus = (int)Math.Floor((float)Globals.Setup.DelaySendMessageTo * 1000f);
                    await Task.Delay((Globals.Setup.DelayFirstMessageAccount ?? 0) * 1000);

                    while (!IsStop)
                    {
                        if (countSendedMessages >= Globals.Setup.CountMessages * ((_currentProfile.TemplateMessages.Count == 0) ? 1 : _currentProfile.TemplateMessages.Count))
                            break;

                        if (_contacts.Except(_usedPhonesUsers).ToArray().Length == 0)
                            break;

                        if (!await client.IsValid())
                            throw new Exception("Client can be banned");
                        else if (!string.IsNullOrEmpty(peopleReal))
                        {
                            ++DashboardView.GetInstance().CompletedTasks;
                            Log.Write($"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};{peopleReal}", _reportFile.FullName);

                            if (++countSendedMessages >= Globals.Setup.CountMessages * ((_currentProfile.TemplateMessages.Count == 0) ? 1 : _currentProfile.TemplateMessages.Count))
                                break;
                        }

                        peopleReal = GetFreeNumberUser(_currentContacts.ToArray());

                        if (string.IsNullOrEmpty(peopleReal))
                            break;

                        var text = string.Join('\n', DashboardView.GetInstance().Text.Split('\r').ToList());
                        FileInfo image = null;

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
                        }

                        //await client.SendMessage("380674279706", SelectWord(text), image);
                        await client.SendMessage(deviceAnswer.Phone.Replace("+", ""), "Hi!", waitDelivered: true);
                        _waitingAnswer.Add(phone);

                        while (_waitingAnswer.Contains(phone))
                            await Task.Delay(500);

                        await Task.Delay(new Random().Next(3_000, 5_000));

                        if (!await client.SendMessage(peopleReal, SelectWord(text), image, true))
                            Log.Write(peopleReal, _notSendedFile.FullName);

                        if (string.IsNullOrEmpty(peopleReal))
                        {
                            IsStop = true;
                            break;
                        }

                        var minus = (int)((Globals.Setup.DynamicDelaySendMessageMinus ?? 0) * 1000f);

                        await Task.Delay(minus <= 0 ? new Random().Next((int)Math.Floor((float)Globals.Setup.DelaySendMessageFrom * 1000f), (int)Math.Floor((float)Globals.Setup.DelaySendMessageTo * 1000f)) : currentMinus -= minus);
                    }
                }
                catch (Exception ex)
                {
                    SendedMessagesCountFromAccount[phone] = countSendedMessages;

                    if (!string.IsNullOrEmpty(Globals.Setup.LinkToChangeIP))
                        Log.Write(await ResourceHelper.GetAsync(Globals.Setup.LinkToChangeIP), _logFile.FullName);

                    _usedPhonesUsers.Remove(peopleReal);

                    ++DashboardView.GetInstance().DeniedTasks;
                    ++DashboardView.GetInstance().DeniedTasksWork;
                    await Globals.TryMove(path, $@"{Globals.BanWorkDirectory.FullName}\{phone}");

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
                await client.UpdateData(true);

                if (!_contacts.Any(cont => !_usedPhonesUsers.Contains(cont)))
                    break;
            }
        }

        string GetFreeNumberUser(string[] contacts)
        {
            lock (_lock)
            {
                foreach (var contact in contacts.OrderBy(x => new Random().Next()).ToArray())
                {
                    if (!_usedPhonesUsers.Contains(contact))
                    {
                        if (contact.Length < 5 || string.IsNullOrEmpty(contact))
                            continue;

                        _usedPhonesUsers.Add(contact);

                        var newContacts = _contacts.Except(_usedPhonesUsers).ToArray();

                        DashboardView.GetInstance().AllTasks = newContacts.Length;

                        File.WriteAllLines(Globals.Setup.PathToFilePhones, newContacts);

                        return contact.Replace("+", "");
                    }
                }

                return string.Empty;
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
}