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
using Windows.ApplicationModel.Contacts;
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
            _tetheredDevices = new Dictionary<int, Client[]>();

            _lock = new();
        }

        public readonly Dictionary<string, int> SendedMessagesCountFromAccount;

        private readonly Dictionary<int, Client[]> _tetheredDevices;
        private readonly List<string> _usedPhonesUsers;
        private readonly object _lock;

        private FileInfo _logFile;
        private FileInfo _reportFile;
        private string[] _contacts;
        private string[] _names;
        private List<string> _usedPhones;
        private ActionProfileWork _currentProfile;

        public bool IsStop;

        public async Task Run(ActionProfileWork actionProfileWork)
        {
            await Globals.InitAccountsFolder();

            IsStop = false;
            
            SendedMessagesCountFromAccount.Clear();
            _tetheredDevices.Clear();

            _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_prep.txt");
            _logFile.Create().Close();
            _names = (await File.ReadAllLinesAsync(Globals.Setup.PathToFileNames)).Where(name => new Regex("^[a-zA-Z0-9. -_?]*$").IsMatch(name)).ToArray();
            _currentProfile = actionProfileWork;
            _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToFilePhones);
            _reportFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{new FileInfo(Globals.Setup.PathToFilePhones).Name}_report.txt");

            if (!_reportFile.Exists)
                _reportFile.Create().Close();

            DashboardView.GetInstance().CompletedTasks = 0;
            DashboardView.GetInstance().AllTasks = _contacts.Count(contact => !string.IsNullOrEmpty(contact) && contact.Length > 5);

            var mainTasks = new List<Task>();
            var busyDevices = new Dictionary<DataEmulator, int>();

            while (true)
            {
                var devices = ManagerView.GetInstance().Emulators.Where(device => !busyDevices.Select(device => device.Key.Index).Contains(device.Index) && device.IsEnabled)
                    .Take(2).ToArray();

                if (devices.Length != 2)
                    break;

                var id = new Random().Next(0, 10_000);

                _tetheredDevices[id] = new[] { new Client(deviceId: devices[0].Index), new Client(deviceId: devices[1].Index) };

                var task = Handler(id);

                await Task.Delay(1_000);

                mainTasks.Add(task);

                foreach (var device in devices)
                    busyDevices[device] = id;
            }

            Task.WaitAll(mainTasks.ToArray(), -1);

            Stop();
        }

        public void Stop()
        {
            _logFile = null;
            IsStop = false;

            _usedPhonesUsers.Clear();
            _usedPhones.Clear();
            _tetheredDevices.Clear();
        }

        private async Task Handler(int threadId)
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

                    var result = Globals.GetAccounts(_usedPhones.ToArray(), true, _lock);

                    if (result.Length < 1)
                    {
                        Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                        break;
                    }

                    (phone, path) = result[0];

                    if (_usedPhones.Contains(phone))
                        continue;

                    _usedPhones.Add(phone);

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

                    var importContactsObject = new List<CObj>()
                        {
                        new(_names[new Random().Next(0, _names.Length)], c1.Phone),
                        new(_names[new Random().Next(0, _names.Length)], c2.Phone)
                        };

                    var currentContacts = _contacts.ToArray().OrderBy(x => new Random().Next()).Take(500).ToList();
                    foreach (var phoneForContact in currentContacts)
                        importContactsObject.Add(new(_names[new Random().Next(0, _names.Length)], "+" + phoneForContact));

                    await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf", MemuLib.Core.Contacts.ContactManager.Export(importContactsObject));

                    await c1.ImportContacts($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf");

                    await c2.ImportContacts($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf");

                    File.Delete($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf");

                    var c1CountSendedMessages = 0;
                    var c2CountSendedMessages = 0;
                    var peopleReal = string.Empty;

                    await Task.Delay((Globals.Setup.DelayFirstMessageAccount ?? 0) * 1000);
                    try
                    {
                        while (!IsStop)
                        {
                            if (c1CountSendedMessages >= Globals.Setup.CountMessages && c2CountSendedMessages >= Globals.Setup.CountMessages)
                                break;

                            if (_contacts.Except(_usedPhonesUsers).ToArray().Length == 0)
                                break;

                            if (c1Auth && !await c1.IsValid())
                            {
                                if (c1CountSendedMessages > 0)
                                {
                                    ++DashboardView.GetInstance().DeniedTasks;
                                    ++DashboardView.GetInstance().DeniedTasksWork;
                                }

                                c1Auth = false;
                            }

                            if (c2Auth && !await c2.IsValid())
                            {
                                if (c2CountSendedMessages > 0)
                                {
                                    ++DashboardView.GetInstance().DeniedTasks;
                                    ++DashboardView.GetInstance().DeniedTasksWork;
                                }

                                c2Auth = false;
                            }

                            if (!c1Auth && !c2Auth)
                                throw new Exception("Clients banned");

                            if (!c2Auth)
                            {
                                _usedPhones.Remove(c1.Phone.Remove(0, 1));
                                break;
                            }

                            if (!string.IsNullOrEmpty(peopleReal))
                            {
                                ++DashboardView.GetInstance().CompletedTasks;
                                Log.Write($"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};{peopleReal}", _reportFile.FullName);

                                if (c1Auth)
                                {
                                    if (++c1CountSendedMessages >= Globals.Setup.CountMessages)
                                        continue;
                                }
                                else
                                {
                                    if (++c2CountSendedMessages >= Globals.Setup.CountMessages)
                                        continue;
                                }
                            }

                            peopleReal = GetFreeNumberUser(currentContacts.ToArray());

                            if (string.IsNullOrEmpty(peopleReal))
                            {
                                _usedPhones.Remove(c1.Phone.Remove(0, 1));
                                _usedPhones.Remove(c2.Phone.Remove(0, 1));
                                break;
                            }

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

                            if (c1Auth)
                            {
                                if (c1CountSendedMessages > 0)
                                    await c1.SendMessage(c2.Phone.Remove(0, 1), "Hi!", waitDelivered: true);
                                else
                                    await c1.SendPreMessage(c2.Phone.Remove(0, 1), "Hi!", waitDelivered: true);

                                if (c1CountSendedMessages > 0)
                                    await c2.SendMessage(c1.Phone.Remove(0, 1), "Hi!", waitDelivered: true);
                                else
                                    await c2.SendPreMessage(c1.Phone.Remove(0, 1), "Hi!", waitDelivered: true);

                                await Task.Delay(new Random().Next(3_000, 5_000));

                                var status = await c1.SendPreMessage(peopleReal, "Hi!", waitDelivered: true);

                                if (status != Client.StatusDelivered.Delivered)
                                {
                                    if (status == Client.StatusDelivered.ContactNotFound)
                                        _usedPhonesUsers.Remove(peopleReal);
                                    currentContacts.Remove(peopleReal);
                                    continue;
                                }

                                status = await c1.SendMessage(peopleReal, SelectWord(text), image, true);

                                if (status != Client.StatusDelivered.Delivered)
                                {
                                    if (status == Client.StatusDelivered.ContactNotFound)
                                        _usedPhonesUsers.Remove(peopleReal);
                                    currentContacts.Remove(peopleReal);
                                    continue;
                                }
                            }
                            else
                            {
                                var status = await c2.SendPreMessage(peopleReal, "Hi!", waitDelivered: true);

                                if (status != Client.StatusDelivered.Delivered)
                                {
                                    if (status == Client.StatusDelivered.ContactNotFound)
                                        _usedPhonesUsers.Remove(peopleReal);
                                    currentContacts.Remove(peopleReal);
                                    continue;
                                }

                                status = await c2.SendMessage(peopleReal, SelectWord(text), image, true);

                                if (status != Client.StatusDelivered.Delivered)
                                {
                                    if (status == Client.StatusDelivered.ContactNotFound)
                                        _usedPhonesUsers.Remove(peopleReal);
                                    currentContacts.Remove(peopleReal);
                                    continue;
                                }
                            }

                            await Task.Delay(new Random().Next((int)Math.Floor((float)Globals.Setup.DelaySendMessageFrom * 1000f), (int)Math.Floor((float)Globals.Setup.DelaySendMessageTo * 1000f)));
                        }
                        await HandlerAccountsEnd(false);
                    }
                    catch (Exception ex)
                    {
                        await HandlerAccountsEnd(true, ex.Message);
                    }

                    async Task HandlerAccountsEnd(bool isBan, string messageEx = "")
                    {
                        SendedMessagesCountFromAccount[c1.Phone.Remove(0, 1)] = c1CountSendedMessages;
                        SendedMessagesCountFromAccount[c2.Phone.Remove(0, 1)] = c2CountSendedMessages;

                        if (!string.IsNullOrEmpty(Globals.Setup.LinkToChangeIP))
                            Log.Write(await ResourceHelper.GetAsync(Globals.Setup.LinkToChangeIP), _logFile.FullName);

                        _usedPhonesUsers.Remove(peopleReal);

                        if (isBan)
                        {
                            await Globals.TryMove(c1.Account, $@"{Globals.BanWorkDirectory.FullName}\{phone}");
                            await Globals.TryMove(c2.Account, $@"{Globals.BanWorkDirectory.FullName}\{phone}");
                        }

                        var count = 0;
                        var lastMessages = SendedMessagesCountFromAccount.Where(val => val.Value > 0).TakeLast(10);

                        foreach (var msg in lastMessages)
                            count += msg.Value;

                        DashboardView.GetInstance().AverageMessages = (int)Math.Floor((decimal)count / lastMessages.Count());

                        if (Globals.Setup.CritNewsLetter != null && DashboardView.GetInstance().AverageMessages < Globals.Setup.CritNewsLetter && !IsStop)
                            IsStop = true;

                        count = 0;
                        foreach (var msg in SendedMessagesCountFromAccount)
                            count += msg.Value;

                        DashboardView.GetInstance().AverageAllMessages = (int)Math.Floor((decimal)count / SendedMessagesCountFromAccount.Where(val => val.Value > 0).Count());

                        if (isBan)
                            Log.Write($"[MessageSender] - Ошибка, возможно клиенты забанены: {messageEx}\n", _logFile.FullName);
                    }

                    if (!_contacts.Any(cont => !_usedPhonesUsers.Contains(cont)))
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Write($"[CRITICAL] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
            }
        }

        #region Реализация входов и прочих вещей
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
                        
                        //if (_usedPhonesUsers.Count % 100 == 0)
                        File.WriteAllLines(Globals.Setup.PathToFilePhones, newContacts);

                        return contact[0] == '+' ? contact.Remove(0, 1) : contact;
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
        #endregion
    }
}