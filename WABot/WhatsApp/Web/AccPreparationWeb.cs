using WABot.Pages;

namespace WABot.WhatsApp.Web;

public class AccPreparationWeb
{
    private readonly List<string> _usedPhones;
    private readonly List<string> _usedPhonesUsers;

    private FileInfo _logFile;

    private int _removedAccounts;
    private int _alivesAccounts;
    public bool IsStop;
    private int _countWarms;

    public AccPreparationWeb()
    {
        _usedPhonesUsers = _usedPhones = new List<string>();

        _countWarms = 1;
        _removedAccounts = _alivesAccounts = 0;

        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_prep_log.txt");
    }

    public async Task Start(string message)
    {
        var tasks = new List<Task>();
        var rnd = new Random();

        IsStop = false;
        _countWarms = 1;
        _removedAccounts = _alivesAccounts = 0;
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_prep_log.txt");

        var busyDevices = new List<int>();

        Log.Write($"Добро пожаловать в логи подготовки аккаунтов\n", _logFile.FullName);

        if (Globals.Setup.CountThreadsChrome % 2 != 0)
        {
            MessageBox.Show("Похоже кол-во потоков не кратно двум");
            return;
        }

        Dashboard.GetInstance().CountTasks = (await Globals.GetAccounts(_usedPhones.ToArray(), true)).Length;

        for (var i = 0; i < Globals.Setup.CountThreadsChrome / 2; i++)
        {
            var task = Handler(message.Split('\n'));

            await Task.Delay(2_000);

            tasks.Add(task);
        }

        Task.WaitAll(tasks.ToArray(), -1);

        foreach (var device in Globals.Devices.ToArray())
            device.InUsage = false;

        Log.Write($"Завершено\n", _logFile.FullName);
        Log.Write($"Проебов: {_removedAccounts}\n", _logFile.FullName);
        Log.Write($"Живых: {_alivesAccounts}\n", _logFile.FullName);

        busyDevices.Clear();
        Stop();
    }

    public void Stop()
    {
        _usedPhones.Clear();
        _usedPhonesUsers.Clear();
    }

    private async Task Handler(string[] messages)
    {
        Log.Write($"Поток запущен\n", _logFile.FullName);

        WaClient c1 = null!, c2 = null!;
        bool c1Auth = false, c2Auth = false;
        again:
        while (!IsStop)
        {
            var result = await Globals.GetAccounts(_usedPhones.ToArray(), true);

            Dashboard.GetInstance().CountTasks = result.Length;

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
                var countTryLogin = 0;
            tryAgain:
                c1 = new WaClient($"+{phone}", path);

                try
                {
                    await c1.Web!.Init(false, path);
                    if (!await c1.Web!.WaitForInChat())
                        throw new Exception("Cant connect");
                    if (!c1.Web!.IsConnected)
                        throw new Exception("Is not connected");
                    c1Auth = true;
                }
                catch (Exception)//Скорее всего аккаунт уже не валидный
                {
                    await c1.Web!.Free();
                    Directory.Move(path, $@"{Globals.LogoutAccountsDirectory}\{phone}");
                    c1.Web!.RemoveQueue();
                    if (countTryLogin++ > 2)
                        goto tryAgain;
                }

                Log.Write($"[{phone}] - {(c1Auth ? "смогли войти" : "не смогли войти")}\n", _logFile.FullName);

                if (!c1Auth)
                    Dashboard.GetInstance().BannedAccounts = ++_removedAccounts;

                continue;
            }


            if (!c2Auth)
            {
                var countTryLogin = 0;
            tryAgain:
                c2 = new WaClient($"+{phone}", path);

                try
                {
                    await c2.Web!.Init(false, path);
                    if (!await c2.Web!.WaitForInChat())
                        throw new Exception("Cant connect");
                    if (!c2.Web!.IsConnected)
                        throw new Exception("Is not connected");
                    c2Auth = true;
                }
                catch (Exception)//Скорее всего аккаунт уже не валидный
                {
                    await c2.Web!.Free();
                    Directory.Move(path, $@"{Globals.LogoutAccountsDirectory}\{phone}");
                    c2.Web!.RemoveQueue();
                    if (countTryLogin++ > 2)
                        goto tryAgain;
                }

                Log.Write($"[{phone}] - {(c2Auth ? "смогли войти" : "не смогли войти")}\n", _logFile.FullName);

                if (!c2Auth)
                    Dashboard.GetInstance().BannedAccounts = ++_removedAccounts;
            }

            var countMessages = new Random().Next(5, 10);

            var rnd = new Random();

            for (var i = 0; i < countMessages; i++)
            {
                if (!c1Auth || !c2Auth || !c1.Web!.IsConnected || !c2.Web!.IsConnected)
                    break;

                if (i == 0)
                {
                    if (!await c1.Web!.SendText(c2.Web!.NameSession, messages[rnd.Next(0, messages.Length - 1)]))
                    {
                        i = -1;
                        continue;
                    }

                    await Task.Delay(rnd.Next(2_000, 10_000));

                    if (!await c2.Web!.SendText(c1.Web!.NameSession, messages[rnd.Next(0, messages.Length - 1)]))
                    {
                        i = -1;
                        continue;
                    }
                }
                else
                {
                    var mc1 = rnd.Next(2, 4);
                    var mc2 = rnd.Next(2, 4);

                    for (var mcc = 0; mcc < mc1; mcc++)
                    {
                        if (!await c1.Web!.SendText(c2.Web!.NameSession, messages[rnd.Next(0, messages.Length - 1)]))
                        {
                            c1Auth = false;
                            Dashboard.GetInstance().BannedAccounts = ++_removedAccounts;
                            break;
                        }

                        await Task.Delay(rnd.Next(2_000, 10_000));
                    }

                    for (var mcc = 0; mcc < mc2; mcc++)
                    {
                        if (!await c2.Web!.SendText(c1.Web!.NameSession, messages[rnd.Next(0, messages.Length - 1)]))
                        {
                            c2Auth = false;
                            Dashboard.GetInstance().BannedAccounts = ++_removedAccounts;
                            break;
                        }

                        await Task.Delay(rnd.Next(2_000, 10_000));
                    }
                }
            }

            c1Auth = c2Auth = false;
            
            await c1.Web!.Free();
            await c2.Web!.Free();

            c1.AccountData.TrustLevelAccount++;
            c2.AccountData.TrustLevelAccount++;

            await c1.UpdateData();
            await c2.UpdateData();

            await Task.Delay(2_000);

            Dashboard.GetInstance().CompletedTasks = _alivesAccounts += 2;
        }

        if (!IsStop && ++_countWarms < Globals.Setup.CountWarmsOnWeb)
            goto again;
    }
}