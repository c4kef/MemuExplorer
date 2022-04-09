namespace WABot.WhatsApp;

public class Register
{
    public bool IsWork { get; private set; }

    private readonly Dictionary<int, WAClient> _tetheredDevices;

    private string[] _names;
    
    public Register() => _tetheredDevices = new Dictionary<int, WAClient>();

    public Task Start()
    {
        var tasks = new List<Task>();
        var rnd = new Random();
        IsWork = true;
        
        _names = File.ReadLines(Globals.Setup.PathToUserNames).ToArray();

        foreach (var client in Globals.Devices)
        {
            var id = rnd.Next(0, 10_000);

            _tetheredDevices[id] = client;

            tasks.Add(Task.Run(() => Handler(id)));
        }

        Task.WaitAll(tasks.ToArray(), -1);

        Stop();
        
        return Task.CompletedTask;
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
            var directoryThreadWhatsApp = Directory.CreateDirectory($@"{Globals.TempAccountsDirectory.FullName}\{idThread}\com.whatsap");

            await File.WriteAllTextAsync($@"{directoryThread.FullName}\Data.json",
                JsonConvert.SerializeObject(new AccountData()
                    {LastActiveDialog = new Dictionary<string, DateTime>(), TrustLevelAccount = 0}));

            var phone = await client.Register(directoryThreadWhatsApp.FullName, _names[new Random().Next(0, _names.Length)]);
            
            Directory.Move(directoryThread.FullName, $@"{Globals.Setup.PathToDirectoryAccounts}\{phone}");
        }
    }
}