﻿namespace WABot.WhatsApp.Web;
public class Newsletter
{
    public int MessagesSendedCount { get; private set; }

    private readonly Dictionary<string, int> _sendedMessagesCountFromAccount;
    private readonly List<FileInfo> _accounts;
    private readonly List<string> _usedPhones;
    private readonly List<string> _usedPhonesUsers;
    private readonly FileInfo _logFile;
    private string[] _contacts;

    public Newsletter()
    {
        _sendedMessagesCountFromAccount = new Dictionary<string, int>();
        _usedPhonesUsers = _usedPhones = new List<string>();
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_log.txt");
        _accounts = new List<FileInfo>();

        _contacts = new[] { "" };
    }

    public async Task Start(string text)
    {
        var tasks = new List<Task>();
        var rnd = new Random();

        _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToPhonesUsers);

        foreach (var account in Directory.GetFiles(Globals.Setup.PathToDirectoryAccountsWeb))
            _accounts.Add(new FileInfo(account));

        Log.Write($"Добро пожаловать в логи, текст рассылки:\n{text}\n\n", _logFile.FullName);

        for (var i = 0; i < 8; i++)
        {
            var task = Handler(SelectWord(text));
            
            await Task.Delay(1_500);

            tasks.Add(task);
        }

        _ = Task.WaitAll(tasks.ToArray(), -1);

        Log.Write("\n\nКол-во сообщений с аккаунта:\n", _logFile.FullName);

        foreach (var account in _sendedMessagesCountFromAccount)
            Log.Write($"{account.Key} - {account.Value}\n", _logFile.FullName);

        Log.Write($"\nОбщее количество отправленных сообщений: {MessagesSendedCount}\n", _logFile.FullName);
        Log.Write($"\nОтлетело: {_sendedMessagesCountFromAccount.Count(account => account.Value == 0)}\n", _logFile.FullName);
        Stop();

        string SelectWord(string value)
        {
            var backValue = value;
            foreach (var match in new Regex(@"(\w+)\|\|(\w+)", RegexOptions.Multiline).Matches(backValue))
                backValue = backValue.Replace(match.ToString()!, match.ToString()!.Split("||")[new Random().Next(0, 100) >= 50 ? 1 : 0]);

            return backValue;
        }
    }

    public void Stop()
    {
        _usedPhones.Clear();
        _usedPhonesUsers.Clear();
    }

    private async Task Handler(string text)
    {
        while (_accounts.Count > 0)
        {
            var result = _accounts[0];
            var phone = result.Name.Split('.')[0];

            if (_usedPhones.Contains(phone))
                continue;

            _usedPhones.Add(phone);
            _accounts.RemoveAt(0);

            _sendedMessagesCountFromAccount[phone] = 0;

            var waw = new WAWClient(phone);

            try
            {
                await waw.Init(false);
            }
            catch (Exception)//Скорее всего аккаунт уже не валидный
            {
                await waw.Free();
                continue;
            }

            var countMsg = 0;

        recurseSendMessageToContact:

            var contact = GetFreeNumberUser();

            if (string.IsNullOrEmpty(contact))
            {
                await waw.Free();
                break;
            }

            var messageSended = await waw.SendText(contact, text);

            if (messageSended)
            {
                ++_sendedMessagesCountFromAccount[phone];
                ++MessagesSendedCount;

                Log.Write(
                    $"Отправлено сообщение с номера {phone} на номер {contact}\n",
                    _logFile.FullName);

                if (++countMsg > Globals.Setup.CountMessageFromAccount)
                {
                    await waw.Free();
                    continue;
                }
            }

            goto recurseSendMessageToContact;
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