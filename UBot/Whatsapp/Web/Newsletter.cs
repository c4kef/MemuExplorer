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
        _sendedMessagesCountFromAccount = new Dictionary<string, int>();
        _usedPhones = new List<string>();
        _usedPhonesUsers = new List<string>();

        _lock = new();
    }

    private readonly Dictionary<string, int> _sendedMessagesCountFromAccount;
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

        DashboardView.GetInstance().AllTasks = _contacts.Length;

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

            var peopleReal = string.Empty;

            try
            {
                if (Globals.Setup.RemoveAvatar)
                    await client.Web!.RemoveAvatar();

                while (!IsStop)
                {
                    peopleReal = GetFreeNumberUser();
                    Log.Write($"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};1", _logFile.FullName);
                    if (string.IsNullOrEmpty(peopleReal))
                        break;

                    Log.Write($"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};2", _logFile.FullName);
                    if (!await client.Web!.IsConnected())
                        throw new Exception("Client has disconected");

                    Log.Write($"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};3", _logFile.FullName);
                    if (await client.Web!.CheckValidPhone(peopleReal))
                    {
                        Log.Write($"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};4", _logFile.FullName);
                        var text = DashboardView.GetInstance().Text.Split('\r').ToList();
                        var file = text.TakeLast(1).ToArray()[0];

                        var isFile = !string.IsNullOrEmpty(file) && File.Exists(file);

                        if (isFile)
                            text.RemoveAll(str => str.Contains(file));

                        if (await client.Web!.SendText(peopleReal, SelectWord(string.Join('\n', text).Replace("\n", "\n").Replace("\r", "\r")), isFile ? new FileInfo(file) : null))
                        {
                            ++DashboardView.GetInstance().CompletedTasks;
                            Log.Write($"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};5-1", _logFile.FullName);
                            Log.Write(
                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};{peopleReal}",
                            _reportFile.FullName);

                            if (++countSendedMessages >= Globals.Setup.CountMessages)
                                break;

                            await Task.Delay(new Random().Next((int)Globals.Setup.DelaySendMessageFrom * 1000, (int)Globals.Setup.DelaySendMessageTo * 1000));
                        }
                        else
                        {
                            Log.Write($"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};6", _logFile.FullName);
                            _usedPhonesUsers.Remove(peopleReal);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write($"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};5-2", _logFile.FullName);
                _sendedMessagesCountFromAccount[phone] = countSendedMessages;

                if (countSendedMessages < 10 && File.Exists(Globals.Setup.PathToFileProxy))
                {
                    Log.Write($"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{proxy}", _badProxyFile.FullName);
                    badProxyList.Add(proxy);
                }

                _usedPhonesUsers.Remove(peopleReal);

                await client.Web!.Free();
                ++DashboardView.GetInstance().DeniedTasks;
                await Globals.TryMove(path, $@"{Globals.WebBanDirectory.FullName}\{phone}");

                var count = 0;
                var messages = _sendedMessagesCountFromAccount.TakeLast(10);

                foreach (var msg in messages)
                    count += msg.Value;

                DashboardView.GetInstance().AverageMessages = (int)Math.Floor((decimal)count / messages.Count());

                count = 0;
                foreach (var msg in _sendedMessagesCountFromAccount)
                    count += msg.Value;

                DashboardView.GetInstance().AverageAllMessages = (int)Math.Floor((decimal)count / _sendedMessagesCountFromAccount.Count);

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
                            if (contact.Length < 5)
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
        }
    }
}