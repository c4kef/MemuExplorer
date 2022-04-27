namespace WABot.WhatsApp;

public class Register
{
    private readonly Dictionary<int, Device> _tetheredDevices;

    private string[] _names;

    public Register()
    {
        _names = new[] {""};
        _tetheredDevices = new Dictionary<int, Device>();
    }

    public async Task Start()
    {
        var tasks = new List<Task>();
        var rnd = new Random();

        _names = File.ReadLines(Globals.Setup.PathToUserNames)
            .Where(name => new Regex("^[a-zA-Z0-9. -_?]*$").IsMatch(name)).ToArray();

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
            await devices[0].Client.GetInstance().Spoof("7", true);
            //await devices[0].Client.GetInstance().Stop();
            //await devices[0].Client.GetInstance().Start();

            var task = Handler(id);
            await Task.Delay(1_000);

            tasks.Add(task);

            busyDevices.Add(devices[0].Index);
        }
        
        Task.WaitAll(tasks.ToArray(), -1);

        foreach (var device in Globals.Devices)
            device.InUsage = false;

        busyDevices.Clear();
        Stop();
    }

    public void Stop()
    {
        _tetheredDevices.Clear();
    }

    private async Task Handler(int idThread)
    {
        await Task.Delay(new Random().Next(1_000, 5_000));
        var client = _tetheredDevices[idThread].Client;
        var clientIndex = _tetheredDevices[idThread].Index;
        
        while (Globals.Devices.Where(device => device.Index == clientIndex).ToArray()[0].IsActive)
        {
            var directoryThread = Directory.CreateDirectory($@"{Globals.TempDirectory.FullName}\{idThread}");
            var directoryThreadWhatsApp =
                Directory.CreateDirectory($@"{Globals.TempDirectory.FullName}\{idThread}\com.whatsapp");

            await File.WriteAllTextAsync($@"{directoryThread.FullName}\Data.json",
                JsonConvert.SerializeObject(new AccountData()
                    {LastActiveDialog = new Dictionary<string, DateTime>(), TrustLevelAccount = 0}));

            var phone = await client.Register(directoryThreadWhatsApp.FullName,
                _names[new Random().Next(0, _names.Length)]);

            if (phone is "stop")
                break;
            
            if (phone is "")
                continue;

            Directory.Move(directoryThread.FullName, $@"{Globals.Setup.PathToDirectoryAccounts}\{phone}");
        }
    }
}