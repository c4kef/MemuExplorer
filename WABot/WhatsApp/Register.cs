namespace WABot.WhatsApp;

public class Register
{
    public bool IsWork { get; private set; }

    private readonly Dictionary<int, WaClient> _tetheredDevices;

    private string[] _names;

    public Register()
    {
        _names = new[] {""};
        _tetheredDevices = new Dictionary<int, WaClient>();
    }

    public async Task Start()
    {
        var tasks = new List<Task>();
        var rnd = new Random();
        IsWork = true;
        
        _names = File.ReadLines(Globals.Setup.PathToUserNames).Where(name => new Regex("^[a-zA-Z0-9. -_?]*$").IsMatch(name)).ToArray();

        foreach (var client in Globals.Devices)
        {
            var id = rnd.Next(0, 10_000);

            _tetheredDevices[id] = client;
            await client.GetInstance().Spoof("7", true);
            await client.GetInstance().Stop();
            await client.GetInstance().Start();

            var task = Handler(id);
            await Task.Delay(1_000);
            
            tasks.Add(task);
        }

        Task.WaitAll(tasks.ToArray(), -1);

        Stop();
    }

    public void Stop()
    {
        _tetheredDevices.Clear();
        IsWork = false;
    }

    private async Task Handler(int idThread)
    {
        await Task.Delay(new Random().Next(1_000, 5_000));
        var client = _tetheredDevices[idThread];
        
        while (IsWork)
        {
            var directoryThread = Directory.CreateDirectory($@"{Globals.TempAccountsDirectory.FullName}\{idThread}");
            var directoryThreadWhatsApp = Directory.CreateDirectory($@"{Globals.TempAccountsDirectory.FullName}\{idThread}\com.whatsapp");

            await File.WriteAllTextAsync($@"{directoryThread.FullName}\Data.json",
                JsonConvert.SerializeObject(new AccountData()
                    {LastActiveDialog = new Dictionary<string, DateTime>(), TrustLevelAccount = 0}));

            var phone = await client.Register(directoryThreadWhatsApp.FullName, _names[new Random().Next(0, _names.Length)]);

            if (string.IsNullOrEmpty(phone))
                break;
            
            Directory.Move(directoryThread.FullName, $@"{Globals.Setup.PathToDirectoryAccounts}\{phone}");
        }
    }
}