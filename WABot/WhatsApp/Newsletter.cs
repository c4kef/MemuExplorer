namespace WABot.WhatsApp;

public class Newsletter
{
    public bool IsWork { get; private set; }

    private readonly Dictionary<int, WAClient> _tetheredDevices;
    private readonly List<string> _usedPhones;
    private FileInfo _fileContacts;
    private string[] _contacts;
    
    public Newsletter()
    {
        _tetheredDevices = new Dictionary<int, WAClient>();
        _usedPhones = new List<string>();
    }

    public async Task Start(string text)
    {
        var tasks = new List<Task>();
        var rnd = new Random();
        IsWork = true;
        
        _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToPhonesUsers);

        _fileContacts = new FileInfo("release_contacts.vcf");

        await File.WriteAllTextAsync(_fileContacts.FullName, ContactManager.Export(
            new List<CObj>(_contacts.Select(phonenumber =>
                new CObj($"Artemiy {new Random().Next(0, 20_000)}", phonenumber)).ToArray())
        ));

        foreach (var t in Globals.Devices)
        {
            var id = rnd.Next(0, 10_000);

            _tetheredDevices[id] = t;

            tasks.Add(Task.Run(() => Handler(id, text)));
        }
        
        Task.WaitAll(tasks.ToArray(), -1);

        Stop();
    }

    public void Stop()
    {
        _tetheredDevices.Clear();
        _usedPhones.Clear();
        IsWork = false;
        File.Delete(_fileContacts.FullName);
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
            await client.LoginFile();

            if (!await IsValid())
            {
                Directory.Move(client.Account, @$"{Globals.RemoveAccountsDirectory.FullName}\{client.Phone.Remove(0, 1)}");
                goto reCreate;
            }

            if (!await client.ImportContacts(_fileContacts.FullName))
            {
                Directory.Move(client.Account, @$"{Globals.RemoveAccountsDirectory.FullName}\{client.Phone.Remove(0, 1)}");
                goto reCreate;
            }

            File.Delete(_fileContacts.FullName);

            foreach (var contact in _contacts)
            {
                await client.SendMessage(contact, text);

                await Task.Delay(500);
            }
        }

        async Task<bool> IsValid() =>
            !await client.GetInstance().ExistsElement("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']") && //To-Do
            !await client.GetInstance().ExistsElement("//node[@text='ДАЛЕЕ']") && //To-Do
            !await client.GetInstance().ExistsElement("//node[@resource-id='android:id/message']");
    }
}