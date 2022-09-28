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

namespace UBot.Whatsapp.Web;

public class Newsletter
{
    public Newsletter()
    {
        _usedPhones = new List<string>();
        _usedPhonesUsers = new List<string>();

        _lock = new();
    }

    private readonly List<string> _usedPhones;
    private readonly List<string> _usedPhonesUsers;
    private readonly object _lock;

    private string[] _contacts;
    private FileInfo _logFile;
    private FileInfo _reportFile;

    public async Task Run()
    {
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_log.txt");
        _reportFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_report.txt");
        _logFile.Create().Close();
        _reportFile.Create().Close();

        var tasks = new List<Task>();

        _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToFilePhones);

        DashboardView.GetInstance().AllTasks = _contacts.Length;

        for (var i = 0; i < Globals.Setup.CountThreads; i++)
        {
            /*
            var task = Handler();
            tasks.Add(task);*/
        }

        Task.WaitAll(tasks.ToArray(), -1);

        tasks.Clear();

        Stop();
    }

    public void Stop()
    {
        _logFile = null;
        _reportFile = null;

        _usedPhonesUsers.Clear();
        _usedPhones.Clear();
    }

    /*private async Task Handler()
    {
        try
        {
            while (true)
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
                catch (Exception ex)
                {
                    await client.Web!.Free(true);
                    client.Web!.RemoveQueue();
                    await Globals.TryMove(path, $@"{Globals.LogoutDirectory.FullName}\{phone}");
                    Log.Write($"[{phone}] - не смогли войти: {ex.Message}\n", _logFile.FullName);

                    ++DashboardView.GetInstance().DeniedTasks;
                    continue;
                }

                try
                {
                    var countSendedMessages = 0;
                    while (true)
                    {
                        var peopleReal = GetFreeNumberUser();

                        if (string.IsNullOrEmpty(peopleReal))
                            break;

                        if (!client.Web!.IsConnected)
                            throw new Exception("Client has disconected");

                        if (await client.Web!.CheckValidPhone(peopleReal))
                        {
                            var text = DashboardView.GetInstance().Text.Split('\r').ToList();
                            var file = text.TakeLast(1).ToArray()[0];

                            var isFile = !string.IsNullOrEmpty(file) && File.Exists(file);

                            if (isFile)
                                text.RemoveAll(str => str.Contains(file));

                            if (await client.Web!.SendText(peopleReal, SelectWord(string.Join('\n', text)), isFile ? new FileInfo(file) : null))
                            {
                                ++DashboardView.GetInstance().CompletedTasks;
                                Log.Write(
                                $"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};{peopleReal}",
                                _reportFile.FullName);

                                if (++countSendedMessages >= Globals.Setup.CountMessages)
                                    break;

                                await Task.Delay(new Random().Next((int)Globals.Setup.DelaySendMessageFrom * 1000, (int)Globals.Setup.DelaySendMessageTo * 1000));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    await client.Web!.Free(false);
                    client.Web!.RemoveQueue();
                    ++DashboardView.GetInstance().DeniedTasks;
                    await Globals.TryMove(path, $@"{Globals.BanDirectory.FullName}\{phone}");
                    Log.Write($"[MessageSender] - Ошибка, возможно клиент забанен: {ex.Message}\n", _logFile.FullName);
                    continue;
                }

                //Успех
                await client.Web!.Free(false);
                client.Web!.RemoveQueue();

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
            }
        }
        catch (Exception ex)
        {
            Log.Write($"[Handler] - крит. ошибка: {ex.Message}\n", _logFile.FullName);
        }
    }*/
}