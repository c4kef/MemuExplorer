namespace WABot.WhatsApp;

public class Newsletter
{
    public int MessagesSendedCount { get; private set; }

    private readonly Dictionary<int, Device> _tetheredDevices;
    private readonly Dictionary<string, int> _sendedMessagesCountFromAccount;
    private readonly List<string> _usedPhones;
    private readonly List<string> _usedPhonesUsers;
    private readonly FileInfo _logFile;

    private string[] _contacts;
    private string[] _names;

    public Newsletter()
    {
        _sendedMessagesCountFromAccount = new Dictionary<string, int>();
        _tetheredDevices = new Dictionary<int, Device>();
        _usedPhonesUsers = _usedPhones = new List<string>();
        _contacts = _names = new[] {""};
        _logFile = new FileInfo($"{DateTime.Now:MMddyyyyHHmmss}_log.txt");
    }

    public async Task Start(string text)
    {
        var tasks = new List<Task>();
        var rnd = new Random();

        _names = (await File.ReadAllLinesAsync(Globals.Setup.PathToUserNames))
            .Where(name => new Regex("^[a-zA-Z0-9. -_?]*$").IsMatch(name)).ToArray();

        _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToPhonesUsers);

        await File.AppendAllTextAsync(_logFile.FullName,
            $"Добро пожаловать в логи, текст рассылки:\n{text}\n\n");

        var busyDevices = new List<int>();
        
        while (true)
        {
            var devices = Globals.Devices.Where(device => !busyDevices.Contains(device.Index) && device.IsActive)
                .Take(1).ToArray();

            if (devices.Length != 1)
                break;
            
            var id = rnd.Next(0, 10_000);

            devices[0].InUsage = true;
            
            _tetheredDevices[id] = devices[0];
            await devices[0].Client.Start();

            var task = Handler(id, SelectWord(text));
            await Task.Delay(1_000);

            tasks.Add(task);

            busyDevices.Add(devices[0].Index);
        }
        
        Task.WaitAll(tasks.ToArray(), -1);

        foreach (var device in Globals.Devices)
            device.InUsage = false;

        await File.AppendAllTextAsync(_logFile.FullName, "\n\nКол-во сообщений с аккаунта:\n");

        foreach (var account in _sendedMessagesCountFromAccount)
            await File.AppendAllTextAsync(_logFile.FullName, $"{account.Key} - {account.Value}\n");

        await File.AppendAllTextAsync(_logFile.FullName,
            $"\nОбщее количество отправленных сообщений: {MessagesSendedCount}\n");

        busyDevices.Clear();
        Stop();
        
        string SelectWord(string value)
        {
            var backValue = value;
            foreach (var match in new Regex(@"(\w+)\|\|(\w+)").Matches(backValue))
                backValue = backValue.Replace(match.ToString()!,  match.ToString()!.Split("||")[new Random().Next(0, 100) >= 50 ? 1 : 0]);

            return backValue;
        }
    }

    public void Stop()
    {
        _tetheredDevices.Clear();
        _usedPhones.Clear();
        _usedPhonesUsers.Clear();
    }

    private async Task Handler(int idThread, string text)
    {
        var client = _tetheredDevices[idThread].Client;
        var clientIndex = _tetheredDevices[idThread].Index;
        
        while (Globals.Devices.Where(device => device.Index == clientIndex).ToArray()[0].IsActive)
        {
            var result = await Globals.GetAccounts(_usedPhones.ToArray(), Globals.Setup.TrustLevelAccount);

            if (result.Length == 0)
                break;

            var (phone, path) = result[0];

            if (_usedPhones.Contains(phone))
                continue;

            _usedPhones.Add(phone);

            _sendedMessagesCountFromAccount[phone] = 0;

            await client.ReCreate($"+{phone}", path);
            await client.LoginFile(name: _names[new Random().Next(0, _names.Length)]);
            if (!await IsValid())
            {
                if (Directory.Exists(@$"{Globals.RemoveAccountsDirectory.FullName}\{client.Phone.Remove(0, 1)}") &&
                    Directory.Exists(client.Account))
                    Directory.Delete(client.Account, true);
                else if (Directory.Exists(client.Account))
                    Directory.Move(client.Account,
                        @$"{Globals.RemoveAccountsDirectory.FullName}\{client.Phone.Remove(0, 1)}");
                continue;
            }

            var countMsg = 0;

            recurseSendMessageToContact:
            var contact = GetFreeNumberUser();

            if (string.IsNullOrEmpty(contact))
                break;

            if (!await IsValid())
            {
                if (Directory.Exists(@$"{Globals.RemoveAccountsDirectory.FullName}\{client.Phone.Remove(0, 1)}") &&
                    Directory.Exists(client.Account))
                    Directory.Delete(client.Account, true);
                else if (Directory.Exists(client.Account))
                    Directory.Move(client.Account,
                        @$"{Globals.RemoveAccountsDirectory.FullName}\{client.Phone.Remove(0, 1)}");

                continue;
            }

            await client.SendMessage(contact, text);

            if (++countMsg > Globals.Setup.CountMessageFromAccount)
                continue;

            ++_sendedMessagesCountFromAccount[phone];
            ++MessagesSendedCount;
            await File.AppendAllTextAsync(_logFile.FullName,
                $"[{DateTime.Now:HH:mm:ss}] - Отправлено сообщение с номера {client.Phone.Remove(0, 1)} на номер {contact}\n");

            await Task.Delay(500);

            if (!Globals.Devices.Where(device => device.Index == clientIndex).ToArray()[0].IsActive)
                break;

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

        async Task<bool> IsValid()
        {
            return !await client.GetInstance().ExistsElement("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']") && //To-Do
                   !await client.GetInstance().ExistsElement("//node[@text='ДАЛЕЕ']") && //To-Do
                   !await client.GetInstance().ExistsElement("//node[@resource-id='android:id/message']");
        }
    }
}