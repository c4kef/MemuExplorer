using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UBot.Views.User;
using MemuLib.Core;
using System.Text.RegularExpressions;
using MemuLib.Core.Contacts;

namespace UBot.Whatsapp;

public class AccPreparation
{
    public AccPreparation()
    {
        _tetheredDevices = new Dictionary<int, Client[]>();
        _usedPhones = new List<string>();
        _names = new[] { "" };
        _lock = new();
    }

    private readonly Dictionary<int, Client[]> _tetheredDevices;
    private readonly List<string> _usedPhones;
    private readonly object _lock;

    private FileInfo _logFile;
    private string[] _names;
    private ActionProfileWork _currentProfile;

    public bool IsStop;

    public async Task Run(string message, ActionProfileWork actionProfileWork)
    {
        IsStop = false;
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_prep.txt");
        _logFile.Create().Close();

        await Globals.InitAccountsFolder();

        _names = (await File.ReadAllLinesAsync(Globals.Setup.PathToFileNames)).Where(name => new Regex("^[a-zA-Z0-9. -_?]*$").IsMatch(name)).ToArray();
        _currentProfile = actionProfileWork;
        var mainTasks = new List<Task>();
        var busyDevices = new List<int>();

        while (true)
        {
            var devices = ManagerView.GetInstance().Emulators.Where(device => !busyDevices.Contains(device.Index) && device.IsEnabled)
                .Take(2).ToArray();

            if (devices.Length != 2)
                break;

            var id = new Random().Next(0, 10_000);

            _tetheredDevices[id] = new[] { new Client(deviceId: devices[0].Index), new Client(deviceId: devices[1].Index) };

            var task = Handler(message.Split('\n'), id);

            await Task.Delay(1_000);

            mainTasks.Add(task);

            busyDevices.AddRange(new[] { devices[0].Index, devices[1].Index });
        }

        Task.WaitAll(mainTasks.ToArray(), -1);

        Stop();
    }

    public void Stop()
    {
        _logFile = null;
        IsStop = false;

        _usedPhones.Clear();
        _tetheredDevices.Clear();
    }

    private async Task Handler(string[] messages, int threadId)
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

        while (ManagerView.GetInstance().Emulators.Where(device => device.Index == c1Index).ToArray()[0].IsEnabled &&
               ManagerView.GetInstance().Emulators.Where(device => device.Index == c1Index).ToArray()[0].IsEnabled && !IsStop)
        {
            var result = Globals.GetAccounts(_usedPhones.ToArray(), true, _lock);

            DashboardView.GetInstance().AllTasks = result.Length;

            if (result.Length < 2)
            {
                Log.Write($"[I] - аккаунт не был найден\n", _logFile.FullName);
                break;
            }

            var (phone, path) = result[0];

            if (_usedPhones.Contains(phone))
                continue;

            _usedPhones.Add(phone);

            if (!c1Auth)
            {
                c1Auth = await TryLogin(c1, phone, path);

                if (!c1Auth)
                    ++DashboardView.GetInstance().DeniedTasks;

                Log.Write($"[{phone}] - {(c1Auth ? "смогли войти" : "не смогли войти")}\n", _logFile.FullName);
                continue;
            }

            if (!c2Auth)
            {
                c2Auth = await TryLogin(c2, phone, path);
                Log.Write($"[{phone}] - {(c2Auth ? "смогли войти" : "не смогли войти")}\n", _logFile.FullName);

                if (!c2Auth)
                {
                    ++DashboardView.GetInstance().DeniedTasks;
                    continue;
                }
            }

            if (_currentProfile.CheckBan)
            {
                await SuccesfulMoveAccount(c1);
                await SuccesfulMoveAccount(c2);
                Log.Write($"[Handler] - Аккаунты перемещены\n", _logFile.FullName);
                DashboardView.GetInstance().CompletedTasks += 2;

                c1Auth = c2Auth = false;
                continue;
            }

            if (_currentProfile.Warm)
            {
                await File.WriteAllTextAsync($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf", ContactManager.Export(
                    new List<CObj>()
                    {
                        new(MemuLib.Globals.RandomString(new Random().Next(5, 15)), c1.Phone),
                        new(MemuLib.Globals.RandomString(new Random().Next(5, 15)), c2.Phone)
                    }
                ));

                await c1.ImportContacts($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf");

                await c2.ImportContacts($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf");

                File.Delete($@"{Globals.TempDirectory.FullName}\{threadId}_contacts.vcf");

                var countMessages = new Random().Next(5, 10);

                var rnd = new Random();

                for (var i = 0; i < countMessages; i++)
                {
                    if (!c1Auth || !c2Auth || IsStop)
                        break;

                    if (i == 0)
                    {
                        if (!await c1.SendMessage(c2.Phone, messages[rnd.Next(0, messages.Length - 1)]))
                        {
                            i = -1;
                            if (!await c1.IsValid())
                            {
                                c1Auth = false;
                                ++DashboardView.GetInstance().DeniedTasks;
                                await DeleteAccount(c1);
                                break;
                            }
                            continue;
                        }

                        if (!await c2.SendMessage(c1.Phone, messages[rnd.Next(0, messages.Length - 1)]))
                        {
                            i = -1;
                            if (!await c2.IsValid())
                            {
                                c2Auth = false;
                                ++DashboardView.GetInstance().DeniedTasks;
                                await DeleteAccount(c2);
                                break;
                            }
                            continue;
                        }
                    }
                    else
                    {
                        var isBanned = false;
                        var mc1 = rnd.Next(2, 4);
                        var mc2 = rnd.Next(2, 4);

                        for (var mcc = 0; mcc < mc1; mcc++)
                        {
                            if (!await c1.SendMessage(c2.Phone, messages[rnd.Next(0, messages.Length - 1)]))
                                if (!await c1.IsValid())
                                {
                                    c1Auth = false;
                                    ++DashboardView.GetInstance().DeniedTasks;
                                    await DeleteAccount(c1);
                                    isBanned = true;
                                    break;
                                }
                        }

                        if (isBanned)
                            break;

                        for (var mcc = 0; mcc < mc2; mcc++)
                        {
                            if (!await c2.SendMessage(c1.Phone, messages[rnd.Next(0, messages.Length - 1)]))
                                if (!await c2.IsValid())
                                {
                                    c2Auth = false;
                                    ++DashboardView.GetInstance().DeniedTasks;
                                    await DeleteAccount(c2);
                                    break;
                                }
                        }
                    }
                }
            }

            c1.AccountData.FirstMsg = true;
            c2.AccountData.FirstMsg = false;
            ++c1.AccountData.TrustLevelAccount;
            ++c2.AccountData.TrustLevelAccount;

            await c1.GetInstance().StopApk(c1.PackageName);
            await c1.GetInstance().RunApk(c1.PackageName);

            await c2.GetInstance().StopApk(c2.PackageName);
            await c2.GetInstance().RunApk(c2.PackageName);

            if (!c1Auth || !c2Auth || IsStop)
                continue;

            if (_currentProfile.Scaning)
            {
                /*try
                {
                    if (Globals.Setup.SelectDeviceScan == 0 || Globals.Setup.SelectDeviceScan == 2)
                    {
                        if (!await TryLoginWeb(c2, c2.Phone.Remove(0, 1)))
                        {
                            c2Auth = false;
                            ++DashboardView.GetInstance().DeniedTasks;
                            await DeleteAccount(c2);
                            continue;
                        }
                        Dashboard.GetInstance().CompletedTasks = ++_alivesAccounts;
                    }

                    if (Globals.Setup.SelectDeviceScan == 0 || Globals.Setup.SelectDeviceScan == 1)
                        if (await TryLoginWeb(c1, c1.Phone.Remove(0, 1)))
                        {
                            Dashboard.GetInstance().CompletedTasks = ++_alivesAccounts;
                            await DeleteAccount(c1);
                        }
                }
                catch (Exception ex)
                {
                    Log.Write($"[Handler] - Произошла ошибка: {ex.Message}\n", _logFile.FullName);
                }*/
            }
            else
            {
                await SuccesfulMoveAccount(c1);
                await SuccesfulMoveAccount(c2);
                Log.Write($"[Handler] - Аккаунты перемещены\n", _logFile.FullName);
            }

            c1Auth = c2Auth = false;
        }

        async Task SuccesfulMoveAccount(Client client)
        {
            var countTry = 0;
            while (countTry++ < 3)
            {
                try
                {
                    if (Directory.Exists(@$"{Globals.WarmedDirectory.FullName}\{client.Phone.Remove(0, 1)}") && Directory.Exists(client.Account))
                        Directory.Delete(client.Account, true);
                    else if (Directory.Exists(client.Account))
                    {
                        await client.UpdateData();
                        Directory.Move(client.Account,
                            @$"{Globals.WarmedDirectory.FullName}\{client.Phone.Remove(0, 1)}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Write($"[SuccesfulMoveAccount] - Произошла ошибка, попытка {countTry}: {ex.Message}\n", _logFile.FullName);
                }

                await Task.Delay(1_000);
            }
        }

        async Task DeleteAccount(Client client)
        {
            var countTry = 0;
            while (countTry++ < 3)
            {
                try
                {
                    if (Directory.Exists(@$"{Globals.BanDirectory.FullName}\{client.Phone.Remove(0, 1)}") &&
                        Directory.Exists(client.Account))
                        Directory.Delete(client.Account, true);
                    else if (Directory.Exists(client.Account))
                    {
                        await client.UpdateData();
                        Directory.Move(client.Account,
                            @$"{Globals.BanDirectory.FullName}\{client.Phone.Remove(0, 1)}");
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
                    await DeleteAccount(client);
                    return false;
                }

                var status = await client.IsValid();
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
