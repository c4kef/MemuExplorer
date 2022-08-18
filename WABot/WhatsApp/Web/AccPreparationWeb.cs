using WABot.Pages;

namespace WABot.WhatsApp.Web;

public class AccPreparationWeb
{
    private readonly List<string> _usedPhones;
    private readonly List<string> _usedPhonesUsers;
    private readonly List<FileInfo> _accounts;

    private FileInfo _logFile;

    private int _removedAccounts;
    private int _alivesAccounts;
    public bool IsStop;

    public AccPreparationWeb()
    {
        _usedPhonesUsers = _usedPhones = new List<string>();
        _accounts = new List<FileInfo>();

        _removedAccounts = _alivesAccounts = 0;

        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_prep_log.txt");
    }

    public async Task Start(string message)
    {
        var tasks = new List<Task>();
        var rnd = new Random();

        IsStop = false;
        _removedAccounts = _alivesAccounts = 0;
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_prep_log.txt");

        foreach (var account in Directory.GetFiles($@"{Globals.Setup.PathToDirectoryAccountsWeb}\Second"))
            _accounts.Add(new FileInfo(account));

        var busyDevices = new List<int>();

        Log.Write($"Добро пожаловать в логи подготовки аккаунтов\n", _logFile.FullName);

        if (_accounts.Count < Globals.Setup.CountThreadsChrome || Globals.Setup.CountThreadsChrome % 2 != 0)
        {
            MessageBox.Show("Похоже кол-во потоков не кратно двум либо аккаунтов очень мало");
            return;
        }

        Dashboard.GetInstance().CountTasks = _accounts.Count;

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
        _accounts.Clear();
    }

    /* if (Directory.Exists($@"{Globals.Setup.PathToDirectoryAccountsWeb}\{client.Phone.Remove(0, 1)}") && File.Exists($@"{Globals.Setup.PathToDirectoryAccountsWeb}\{client.Phone.Remove(0, 1)}.data.json"))
 {
     Directory.Move($@"{Globals.Setup.PathToDirectoryAccountsWeb}\{client.Phone.Remove(0, 1)}", $@"{Globals.Setup.PathToDirectoryAccountsWeb}\{(firstMsg ? "First" : "Second")}\{client.Phone.Remove(0, 1)}");
     File.Move($@"{Globals.Setup.PathToDirectoryAccountsWeb}\{client.Phone.Remove(0, 1)}.data.json", $@"{Globals.Setup.PathToDirectoryAccountsWeb}\{(firstMsg ? "First" : "Second")}\{client.Phone.Remove(0, 1)}.data.json");
 }*/

    private async Task Handler(string[] messages)
    {
        Log.Write($"Поток запущен\n", _logFile.FullName);

        WAWClient c1 = null!, c2 = null!;
        bool c1Auth = false, c2Auth = false;

        while (_accounts.Count > 1 && !IsStop)
        {
            var result = _accounts[0];

            var phone = result.Name.Split('.')[0];

            if (_usedPhones.Contains(phone))
                continue;

            _usedPhones.Add(phone);
            _accounts.RemoveAt(0);

            if (Directory.Exists($@"{result.Directory!.FullName}\{result.Name.Split('.')[0]}"))
                Directory.Move($@"{result.Directory!.FullName}\{result.Name.Split('.')[0]}", $@"{Globals.Setup.PathToDirectoryAccountsWeb}\{result.Name.Split('.')[0]}");

            result.MoveTo($@"{Globals.Setup.PathToDirectoryAccountsWeb}\{result.Name}", true);

            if (!c1Auth)
            {
                c1 = new WAWClient(phone);

                try
                {
                    await c1.Init(false);
                    c1Auth = true;
                }
                catch (Exception)//Скорее всего аккаунт уже не валидный
                {
                    await c1.Free();
                    if (File.Exists(@$"{result.FullName}"))
                        File.Delete(@$"{result.FullName}");

                    c1.RemoveQueue();
                }

                Log.Write($"[{phone}] - {(c1Auth ? "смогли войти" : "не смогли войти")}\n", _logFile.FullName);

                if (!c1Auth)
                    Dashboard.GetInstance().BannedAccounts = ++_removedAccounts;

                continue;
            }

            if (!c2Auth)
            {
                c2 = new WAWClient(phone);

                try
                {
                    await c2.Init(false);
                    c2Auth = true;
                }
                catch (Exception)//Скорее всего аккаунт уже не валидный
                {
                    await c2.Free();
                    if (File.Exists(@$"{result.FullName}"))
                        File.Delete(@$"{result.FullName}");

                    c2.RemoveQueue();
                }

                Log.Write($"[{phone}] - {(c2Auth ? "смогли войти" : "не смогли войти")}\n", _logFile.FullName);

                if (!c2Auth)
                {
                    Dashboard.GetInstance().BannedAccounts = ++_removedAccounts;
                    continue;
                }
            }

            var countMessages = new Random().Next(5, 10);

            var rnd = new Random();

            for (var i = 0; i < countMessages; i++)
            {
                if (!c1Auth || !c2Auth)
                    break;

                if (i == 0)
                {
                    if (!await c1.SendText(c2.NameSession, messages[rnd.Next(0, messages.Length - 1)]))
                    {
                        i = -1;
                        continue;
                    }

                    await Task.Delay(rnd.Next(2_000, 10_000));

                    if (!await c2.SendText(c1.NameSession, messages[rnd.Next(0, messages.Length - 1)]))
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
                        if (!await c1.SendText(c2.NameSession, messages[rnd.Next(0, messages.Length - 1)]))
                        {
                            c1Auth = false;
                            Dashboard.GetInstance().BannedAccounts = ++_removedAccounts;
                            break;
                        }

                        await Task.Delay(rnd.Next(2_000, 10_000));
                    }

                    for (var mcc = 0; mcc < mc2; mcc++)
                    {
                        if (!await c2.SendText(c1.NameSession, messages[rnd.Next(0, messages.Length - 1)]))
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
            
            await c1.Free();
            await c2.Free();

            await Task.Delay(2_000);

            Move(c1.NameSession, true);
            Move(c2.NameSession, false);

            Dashboard.GetInstance().CompletedTasks = _alivesAccounts += 2;
        }

        void Move(string phone, bool firstMsg)
        {
            if (Directory.Exists($@"{Globals.Setup.PathToDirectoryAccountsWeb}\{phone}") && File.Exists($@"{Globals.Setup.PathToDirectoryAccountsWeb}\{phone}.data.json"))
            {
                Directory.Move($@"{Globals.Setup.PathToDirectoryAccountsWeb}\{phone}", $@"{Globals.Setup.PathToDirectoryAccountsWeb}\{(firstMsg ? "First" : "Second")}\{phone}");
                File.Move($@"{Globals.Setup.PathToDirectoryAccountsWeb}\{phone}.data.json", $@"{Globals.Setup.PathToDirectoryAccountsWeb}\{(firstMsg ? "First" : "Second")}\{phone}.data.json");
            }
        }
    }
}