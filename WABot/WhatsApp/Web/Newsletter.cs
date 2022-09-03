using MS.WindowsAPICodePack.Internal;
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

    public async Task Start(string text)
    {
        var tasks = new List<Task>();

        IsStop = false;

        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_log.txt");
        MessagesSendedCount = _diedAccounts = 0;

        _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToPhonesUsers);

        Dashboard.GetInstance().CountTasks = _contacts.Length;

        Log.Write($"Добро пожаловать в логи, текст рассылки:\n{text}\n\n", _logFile.FullName);

        for (var i = 0; i < Globals.Setup.CountThreadsChrome; i++)
        {
            var task = Handler();
            
            await Task.Delay(2_500);

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
            await Task.Delay(1_500);
            var accountsWeb = await Globals.GetAccounts(_usedPhones.ToArray(), false);

            if (accountsWeb.Length == 0)
                break;

            var (phone, path) = accountsWeb[0];

            if (_usedPhones.Contains(phone))
                continue;

            _usedPhones.Add(phone);

            _sendedMessagesCountFromAccount[phone] = 0;

            var countTryLogin = 0;
        tryAgain:
            var waw = new WaClient(phone, path);

            try
            {
                await waw.Web!.Init(false, path);
                if (!await waw.Web!.WaitForInChat())
                    throw new Exception("Cant connect");
                if (!waw.Web!.IsConnected)
                    throw new Exception("Is not connected");
            }
            catch (Exception)//Скорее всего аккаунт уже не валидный
            {
                await waw.Web!.Free();
                waw.Web!.RemoveQueue();
                if (countTryLogin++ > 2)
                    goto tryAgain;
                else
                {
                    var countTry = 0;
                    while (!TryMove(path, $@"{Globals.LogoutAccountsDirectory}\{phone}") && ++countTry > 75)
                        await Task.Delay(500);
                    continue;
                }
            }

            var countMsg = 0;

        recurseSendMessageToContact:

            if (IsStop)
            {
                await waw.Web!.Free();
                break;
            }

            var contact = GetFreeNumberUser();

            while (!_checkReadyThreads)
                await Task.Delay(500);

            if (string.IsNullOrEmpty(contact))
            {
                await waw.Web!.Free();
                break;
            }

            if (!waw.Web!.IsConnected)
            {
                await BanAccount(waw.Web!, contact);
                continue;
            }

            try
            {
                if (!await waw.Web!.CheckValidPhone(contact))
                    goto recurseSendMessageToContact;
            }
            catch
            {
                await BanAccount(waw.Web!, contact);
                continue;
            }

            var text = Dashboard.GetInstance().TextMessage.Split('\n').ToList();
            var file = text.TakeLast(1).ToArray()[0];

            var isFile = !string.IsNullOrEmpty(file) && (File.Exists(file) || file.Contains("http"));

            if (isFile)
                text.RemoveAll(str => str.Contains(file));

            var messageSended = await waw.Web!.SendText(contact, SelectWord(string.Join('\n', text)), isFile ? new FileInfo(file) : null);

            if (messageSended)
            {
                ++_sendedMessagesCountFromAccount[phone];
                Dashboard.GetInstance().CompletedTasks = ++MessagesSendedCount;

                Log.Write(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};{contact}",
                    _logFile.FullName);
                
                waw.AccountData.CountMessages++;

                if (++countMsg > Globals.Setup.CountMessagesFromAccount)
                {
                    await waw.UpdateData();
                    await waw.Web!.Free();
                    continue;
                }
            }
            else
            {
                await waw.UpdateData();
                await BanAccount(waw.Web!, contact);

                var countTry = 0;
                while (!TryMove(path, $@"{Globals.LogoutAccountsDirectory}\{phone}") && ++countTry > 75)
                    await Task.Delay(500);

                continue;
            }

            await waw.UpdateData();
            await Task.Delay(new Random().Next(Globals.Setup.DelaySendMessageFrom * 1_000, Globals.Setup.DelaySendMessageTo * 1_000));
            goto recurseSendMessageToContact;
        }

        async Task BanAccount(WAWClient waw, string contact)
        {
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

        bool TryMove(string from, string to)
        {
            try
            {
                if (Directory.Exists(from))
                    Directory.Move(from, to);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}