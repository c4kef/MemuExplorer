using WABot.Pages;

namespace WABot.WhatsApp.Web;
public class Newsletter
{
    public int MessagesSendedCount { get; private set; }

    private readonly Dictionary<string, int> _sendedMessagesCountFromAccount;
    private readonly List<string> _usedPhones;
    private readonly List<string> _usedPhonesUsers;

    private FileInfo _logFile;
    private string[] _contacts;

    public bool IsStop;
    private int _diedAccounts;
    private bool _checkReadyThreads;

    public Newsletter()
    {
        _sendedMessagesCountFromAccount = new Dictionary<string, int>();
        _usedPhonesUsers = _usedPhones = new List<string>();
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_log.txt");

        _diedAccounts = 0;
        _contacts = new[] { "" };
        _checkReadyThreads = false;
    }

    private async Task HandlerNumberRewrite()
    {
        var contacts = _contacts.ToList();
        var removedPhone = new List<string>();

        while (!IsStop)
        {
            await Task.Delay(500);

            if (_usedPhonesUsers.Count == 0)
                continue;

            var contact = string.Empty;

            foreach(var phone in _usedPhonesUsers)
                if (!removedPhone.Contains(phone))
                {
                    contact = phone;
                    break;
                }

            if (string.IsNullOrEmpty(contact))
                continue;

            contacts.RemoveAll(phone => phone == contact);

            await File.WriteAllLinesAsync(Globals.Setup.PathToPhonesUsers, contacts);

            removedPhone.Add(contact);
        }
    }

    public async Task Start(string text)
    {
        var tasks = new List<Task>();

        IsStop = false;

        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_log.txt");
        MessagesSendedCount = _diedAccounts = 0;

        _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToPhonesUsers);

        Dashboard.GetInstance().CountTasks = _contacts.Length;

        _ = Task.Run(HandlerNumberRewrite);

        Log.Write($"Добро пожаловать в логи, текст рассылки:\n{text}\n\n", _logFile.FullName);

        for (var i = 0; i < Globals.Setup.CountThreadsChrome; i++)
        {
            var task = Handler();
            
            await Task.Delay(1_500);

            tasks.Add(task);
        }

        _checkReadyThreads = true;

        _ = Task.WaitAll(tasks.ToArray(), -1);

        _checkReadyThreads = false;

        IsStop = true;

        Log.Write("\n\nКол-во сообщений с аккаунта:\n", _logFile.FullName);

        foreach (var account in _sendedMessagesCountFromAccount)
            Log.Write($"{account.Key} - {account.Value}\n", _logFile.FullName);

        Log.Write($"\nОбщее количество отправленных сообщений: {MessagesSendedCount}\n", _logFile.FullName);
        Log.Write($"\nОтлетело: {_diedAccounts}\n", _logFile.FullName);
        Stop();
    }

    public void Stop()
    {
        _usedPhones.Clear();
        _usedPhonesUsers.Clear();
    }

    private async Task Handler()
    {
        while (!IsStop)
        {
            var accountsWeb = await Globals.GetAccountsWeb(_usedPhones.ToArray());

            if (accountsWeb.Length == 0)
                break;

            var result = accountsWeb[0];

            var phone = result.Name.Split('.')[0];

            if (_usedPhones.Contains(phone))
                continue;

            if (Directory.Exists($@"{result.Directory!.FullName}\{result.Name.Split('.')[0]}"))
                Directory.Move($@"{result.Directory!.FullName}\{result.Name.Split('.')[0]}", $@"{Globals.Setup.PathToDirectoryAccountsWeb}\{result.Name.Split('.')[0]}");

            result.MoveTo($@"{Globals.Setup.PathToDirectoryAccountsWeb}\{result.Name}", true);

            _usedPhones.Add(phone);

            _sendedMessagesCountFromAccount[phone] = 0;

            var waw = new WAWClient(phone);

            try
            {
                await waw.Init(false);
            }
            catch (Exception)//Скорее всего аккаунт уже не валидный
            {
                await waw.Free();
                if (File.Exists(@$"{result.FullName}"))
                    File.Delete(@$"{result.FullName}");

                waw.RemoveQueue();
                continue;
            }

            var countMsg = 0;

        recurseSendMessageToContact:

            if (IsStop)
            {
                await waw.Free();
                break;
            }

            var contact = GetFreeNumberUser();

            while (!_checkReadyThreads)
                await Task.Delay(500);

            if (string.IsNullOrEmpty(contact))
            {
                await waw.Free();
                break;
            }

            /*if (!await waw.CheckValidPhone(contact))
                goto recurseSendMessageToContact;*/

            var messageSended = await waw.SendText(contact, SelectWord(Dashboard.GetInstance().TextMessage));

            if (messageSended)
            {
                ++_sendedMessagesCountFromAccount[phone];
                Dashboard.GetInstance().CompletedTasks = ++MessagesSendedCount;

                Log.Write(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};{contact}",
                    _logFile.FullName);

                if (++countMsg > Globals.Setup.CountMessagesFromAccount)
                {
                    await waw.Free();
                    continue;
                }
            }
            else
            {
                /*Log.Write(
                     $"[{phone}] Перед блокировкой было разослано {MessagesSendedCount} сообщений\n",
                     _logFile.FullName);*/

                await waw.Free();

                _usedPhonesUsers.Remove(contact);

                Dashboard.GetInstance().BannedAccounts = ++_diedAccounts;

                var count = 0;
                var messages = _sendedMessagesCountFromAccount.TakeLast(10);

                foreach (var msg in messages)
                    count += msg.Value;

                Dashboard.GetInstance().AverageMessages = (int)Math.Floor((decimal)count / messages.Count());

                count = 0;
                foreach (var msg in _sendedMessagesCountFromAccount)
                    count += msg.Value;

                Dashboard.GetInstance().AverageMessagesAll = (int)Math.Floor((decimal)count / _sendedMessagesCountFromAccount.Count());
                try
                {
                    if (File.Exists(@$"{result.FullName}"))
                        File.Delete(@$"{result.FullName}");

                    if (Directory.Exists($@"{Globals.Setup.PathToDirectoryAccountsWeb}\{result.Name.Split('.')[0]}"))
                        Directory.Delete($@"{Globals.Setup.PathToDirectoryAccountsWeb}\{result.Name.Split('.')[0]}", true);
                }
                catch{}
                continue;
            }

            await Task.Delay(new Random().Next(30_000, 60_000));//Ждем 30-60 сек

            goto recurseSendMessageToContact;
        }

        string SelectWord(string value)
        {
            var backValue = value;
            foreach (var match in new Regex(@"(\w+)\|\|(\w+)", RegexOptions.Multiline).Matches(backValue))
                backValue = backValue.Replace(match.ToString()!, match.ToString()!.Split("||")[new Random().Next(0, 100) >= 50 ? 1 : 0]);

            return backValue;
        }

        string GetFreeNumberUser()
        {
            foreach (var contact in _contacts)
                if (!_usedPhonesUsers.Contains(contact))
                {
                    _usedPhonesUsers.Add(contact);
                    return contact[0] == '+' ? contact.Remove(0, 1) : contact;
                }

            return string.Empty;
        }
    }
}