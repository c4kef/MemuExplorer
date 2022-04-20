namespace WABot.WhatsApp;

public class Newsletter
{
    public bool IsWork { get; private set; }
    public int MessagesSendedCount { get; private set; }

    private readonly Dictionary<int, WaClient> _tetheredDevices;
    private readonly Dictionary<string, int> _sendedMessagesCountFromAccount;
    private readonly List<string> _usedPhones;
    private readonly List<string> _usedPhonesUsers;
    private readonly FileInfo _logFile;

    private string[] _contacts;
    private string[] _names;
    
    public Newsletter()
    {
        _sendedMessagesCountFromAccount = new Dictionary<string, int>();
        _tetheredDevices = new Dictionary<int, WaClient>();
        _usedPhonesUsers = _usedPhones = new List<string>();
        _contacts = _names = new[] {""};
        _logFile = new FileInfo($"{DateTime.Now:MMddyyyyHHmmss}_log.txt");
    }

    public async Task Start(string text)
    {
        var tasks = new List<Task>();
        var rnd = new Random();
        IsWork = true;
        
        _names = (await File.ReadAllLinesAsync(Globals.Setup.PathToUserNames)).Where(name => new Regex("^[a-zA-Z0-9. -_?]*$").IsMatch(name)).ToArray();

        _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToPhonesUsers);

        await File.AppendAllTextAsync(_logFile.FullName,
            $"Добро пожаловать в логи, текст рассылки:\n{text}\n\n");
        
        foreach (var t in Globals.Devices)
        {
            var id = rnd.Next(0, 10_000);

            _tetheredDevices[id] = t;

            var task = Handler(id, text);
            await Task.Delay(1_000);
            
            tasks.Add(task);
        }
        
        Task.WaitAll(tasks.ToArray(), -1);
        
        await File.AppendAllTextAsync(_logFile.FullName, "\n\nКол-во сообщений с аккаунта:\n");
        
        foreach (var account in _sendedMessagesCountFromAccount)
            await File.AppendAllTextAsync(_logFile.FullName, $"{account.Key} - {account.Value}\n");
        
        await File.AppendAllTextAsync(_logFile.FullName,
            $"\nОбщее количество отправленных сообщений: {MessagesSendedCount}\n");
        
        Stop();
    }

    public void Stop()
    {
        _tetheredDevices.Clear();
        _usedPhones.Clear();
        _usedPhonesUsers.Clear();
        IsWork = false;
    }

    private async Task Handler(int idThread, string text)
    {
        var client = _tetheredDevices[idThread];

        while (IsWork)
        {
            reCreate:
            var result = (await Globals.GetAccounts(_usedPhones.ToArray(), Globals.Setup.TrustLevelAccount));

            if (result.Length == 0)
                break;

            var (phone, path) = result[0];

            _usedPhones.Add(phone);

            _sendedMessagesCountFromAccount[phone] = 0;
            
            await client.ReCreate(phone: $"+{phone}", account: path);
            await client.LoginFile(name: _names[new Random().Next(0, _names.Length)]);
            if (!await IsValid())
            {
                Directory.Move(client.Account,
                    @$"{Globals.RemoveAccountsDirectory.FullName}\{client.Phone.Remove(0, 1)}");
                goto reCreate;
            }

            var countMsg = 0;

            recurseSendMessageToContact:
            var contact = GetFreeNumberUser();

            if (string.IsNullOrEmpty(contact))
                break;

            if (!await IsValid())
            {
                Directory.Move(client.Account,
                    @$"{Globals.RemoveAccountsDirectory.FullName}\{client.Phone.Remove(0, 1)}");

                goto reCreate;
            }

            await client.SendMessage(contact, text);

            if (++countMsg > Globals.Setup.CountMessageFromAccount)
                goto reCreate;

            ++_sendedMessagesCountFromAccount[phone];
            ++MessagesSendedCount;
            await File.AppendAllTextAsync(_logFile.FullName,
                $"[{DateTime.Now:HH:mm:ss}] - Отправлено сообщение с номера {client.Phone.Remove(0, 1)} на номер {contact}\n");

            await Task.Delay(500);

            if (!IsWork)
                break;

            goto recurseSendMessageToContact;
        }

        string GetFreeNumberUser()
        {
            foreach (var contact in _contacts)
                if (!_usedPhonesUsers.Contains(contact))
                {
                    _usedPhonesUsers.Add(contact);
                    return (contact[0] == '+') ? contact.Remove(0, 1) : contact;
                }

            return string.Empty;
        }

        async Task<bool> IsValid() =>
            !await client.GetInstance().ExistsElement("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']") && //To-Do
            !await client.GetInstance().ExistsElement("//node[@text='ДАЛЕЕ']") && //To-Do
            !await client.GetInstance().ExistsElement("//node[@resource-id='android:id/message']");
    }
}