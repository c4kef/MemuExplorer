namespace WABot.WhatsApp;

public class Newsletter
{
    public bool IsWork { get; private set; }

    private readonly Dictionary<int, WaClient> _tetheredDevices;
    private readonly List<string> _usedPhones;
    private readonly List<string> _usedPhonesUsers;
    private string[] _contacts;
    private string[] _names;
    
    public Newsletter()
    {
        _tetheredDevices = new Dictionary<int, WaClient>();
        _usedPhonesUsers = _usedPhones = new List<string>();
        _contacts = _names = new[] {""};
    }

    public async Task Start(string text)
    {
        var tasks = new List<Task>();
        var rnd = new Random();
        IsWork = true;
        
        _names = (await File.ReadAllLinesAsync(Globals.Setup.PathToUserNames)).Where(name => new Regex("^[a-zA-Z0-9. -_?]*$").IsMatch(name)).ToArray();

        _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToPhonesUsers);

        foreach (var t in Globals.Devices)
        {
            var id = rnd.Next(0, 10_000);

            _tetheredDevices[id] = t;

            var task = Handler(id, text);
            await Task.Delay(1_000);
            
            tasks.Add(task);
        }
        
        Task.WaitAll(tasks.ToArray(), -1);

        Stop();
    }

    public void Stop()
    {
        _tetheredDevices.Clear();
        _usedPhones.Clear();
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

            await Task.Delay(500);
            
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