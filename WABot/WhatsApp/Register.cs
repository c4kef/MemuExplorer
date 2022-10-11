using WABot.Pages;

namespace WABot.WhatsApp;

public class Register
{
    private readonly Dictionary<int, Device> _tetheredDevices;
    public bool IsStop;

    public Register()
    {
        _tetheredDevices = new Dictionary<int, Device>();
    }

    public async Task Start()
    {
        var tasks = new List<Task>();
        var rnd = new Random();

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
        client.IsW4B = true;

        var clientIndex = _tetheredDevices[idThread].Index;

        while (Globals.Devices.Where(device => device.Index == clientIndex).ToArray()[0].IsActive && !IsStop)
        {
            /*if (await client.Register())
                ++Dashboard.GetInstance().CompletedTasks;
            else
                ++Dashboard.GetInstance().BannedAccounts;*/

            Dashboard.GetInstance().CountTasks++;
        }
    }
}