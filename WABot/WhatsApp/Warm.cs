using AdvancedSharpAdbClient.Exceptions;

namespace WABot.WhatsApp;

public class Warm
{
    private readonly Dictionary<int, Device[]> _tetheredDevices;
    private readonly List<string> _busyPhone;

    private string[] _names;

    public Warm()
    {
        _tetheredDevices = new Dictionary<int, Device[]>();
        _busyPhone = new List<string>();
        _names = new[] {""};
    }

    public async Task Start(string text)
    {
        var tasks = new List<Task>();
        var rnd = new Random();

        _names = (await File.ReadAllLinesAsync(Globals.Setup.PathToUserNames))
            .Where(name => new Regex("^[a-zA-Z0-9. -_?]*$").IsMatch(name)).ToArray();

        var busyDevices = new List<int>();

        while (true)
        {
            var devices = Globals.Devices.Where(device => !busyDevices.Contains(device.Index) && device.IsActive)
                .Take(2).ToArray();

            if (devices.Length != 2)
                break;
            
            var id = rnd.Next(0, 10_000);

            foreach (var device in devices)
            {
                device.InUsage = true;
                
                await device.Client.Start();
                await device.Client.GetInstance().Shell("settings put global window_animation_scale 0");
                await device.Client.GetInstance().Shell("settings put global transition_animation_scale 0");
                await device.Client.GetInstance().Shell("settings put global animator_duration_scale 0");
            }

            _tetheredDevices[id] = new[] {devices[0], devices[1]};

            var task = Handler(id, text.Split('\n'));
            await Task.Delay(1_000);

            tasks.Add(task);

            busyDevices.AddRange(new[] {devices[0].Index, devices[1].Index});
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
        _busyPhone.Clear();
    }

    private async Task Handler(int idThread, string[] texts)
    {
        var client1 = _tetheredDevices[idThread][0].Client;
        var client2 = _tetheredDevices[idThread][1].Client;

        var (c1Index, c2Index) = (_tetheredDevices[idThread][0].Index, _tetheredDevices[idThread][1].Index);
        
        _busyPhone.Add(client1.Phone);
        _busyPhone.Add(client2.Phone);

        while (Globals.Devices.Where(device => device.Index == c1Index).ToArray()[0].IsActive &&
               Globals.Devices.Where(device => device.Index == c2Index).ToArray()[0].IsActive)
        {
            //Попытка войти в аккаунт для собеседника 1
            var resultC1 = await Globals.GetAccountsWarm(_busyPhone.ToArray());

            if (resultC1.Length == 0)
                break;

            var c1Account = resultC1[0];

            _busyPhone.Add(c1Account.phone);

            await client1.ReCreate($"+{c1Account.phone}", c1Account.path);
            await client1.LoginFile(name: _names[new Random().Next(0, _names.Length)]);

            if (!await IsValid(client1))
            {
                _busyPhone.Remove(c1Account.phone);
                Directory.Move(client1.Account,
                    @$"{Globals.RemoveAccountsDirectory.FullName}\{client1.Phone.Remove(0, 1)}");
                continue;
            }

            //Попытка войти в аккаунт для собеседника 2
            reCreateC2:
            var resultC2 = await Globals.GetAccountsWarm(_busyPhone.ToArray());

            if (resultC2.Length == 0)
                break;

            var c2Account = resultC2[0];

            _busyPhone.Add(c2Account.phone);

            await client2.ReCreate($"+{c2Account.phone}", c2Account.path);
            await client2.LoginFile(name: _names[new Random().Next(0, _names.Length)]);

            if (!await IsValid(client2))
            {
                _busyPhone.Remove(c2Account.phone);
                Directory.Move(client2.Account,
                    @$"{Globals.RemoveAccountsDirectory.FullName}\{client2.Phone.Remove(0, 1)}");
                goto reCreateC2;
            }

            try
            {
                //Импорт контактов на устройства
                var fileContact = new FileInfo($"{idThread}_contacts.vcf");

                await File.WriteAllTextAsync(fileContact.FullName, ContactManager.Export(
                    new List<CObj>()
                    {
                        new($"Artemiy {new Random().Next(0, 20_000)}", client1.Phone),
                        new($"Artemiy {new Random().Next(0, 20_000)}", client2.Phone)
                    }
                ));

                if (!await client1.ImportContacts(fileContact.FullName))
                {
                    _busyPhone.Remove(c1Account.phone);
                    Directory.Move(client1.Account,
                        @$"{Globals.RemoveAccountsDirectory.FullName}\{client1.Phone.Remove(0, 1)}");
                    continue;
                }

                if (!await client2.ImportContacts(fileContact.FullName))
                {
                    _busyPhone.Remove(c2Account.phone);
                    Directory.Move(client2.Account,
                        @$"{Globals.RemoveAccountsDirectory.FullName}\{client2.Phone.Remove(0, 1)}");
                    goto reCreateC2;
                }

                File.Delete(fileContact.FullName);

                client1.AccountData.TrustLevelAccount++;
                client2.AccountData.TrustLevelAccount++;

                for (var i = 0; i < Globals.Setup.CountMessage; i++)
                    foreach (var text in texts)
                    {
                        if (!await IsValid(client1, false))
                            break;

                        if (!await IsValid(client2, false))
                            goto reCreateC2;
                        
                        await client1.SendMessage(client2.Phone, text);

                        await Task.Delay(500);

                        await client2.SendMessage(client1.Phone, text);
                    }
            }
            catch (DeviceNotFoundException)
            {
                await client1.Start();
                await client2.Start();

                continue;
            }

            client1.AccountData.LastActiveDialog![client2.Phone] = DateTime.Now;
            client2.AccountData.LastActiveDialog![client1.Phone] = DateTime.Now;

            await client1.UpdateData();
            await client2.UpdateData();
        }

        async Task<bool> IsValid(WaClient client, bool isWait = true)
        {
            return !await client.GetInstance().ExistsElement("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']", isWait) && //To-Do
                   !await client.GetInstance().ExistsElement("//node[@text='ДАЛЕЕ']", isWait) && //To-Do
                   !await client.GetInstance().ExistsElement("//node[@resource-id='android:id/message']", isWait);
        }
    }
}