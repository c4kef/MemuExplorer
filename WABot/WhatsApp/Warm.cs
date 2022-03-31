namespace WABot.WhatsApp;

public class Warm
{
    public bool IsWork { get; private set; }

    private readonly Dictionary<int, WAClient[]> _tetheredDevices;
    private readonly List<string> _busyPhone;

    public Warm()
    {
        _tetheredDevices = new Dictionary<int, WAClient[]>();
        _busyPhone = new List<string>();
    }

    public Task Start(string text)
    {
        var tasks = new List<Task>();
        var rnd = new Random();
        IsWork = true;

        for (var i = 0; i < Globals.Devices.Count; i += 2)
        {
            var id = rnd.Next(0, 10_000);

            _tetheredDevices[id] = new[] {Globals.Devices[i], Globals.Devices[i + 1]};

            tasks.Add(Task.Run(() => Handler(id, text.Split('\n'))));
        }

        Task.WaitAll(tasks.ToArray(), -1);

        Stop();
        
        return Task.CompletedTask;
    }

    public void Stop()
    {
        _tetheredDevices.Clear();
        _busyPhone.Clear();
        IsWork = false;
    }

    private async Task Handler(int idThread, string[] texts)
    {
        var client1 = _tetheredDevices[idThread][0];
        var client2 = _tetheredDevices[idThread][1];
        
        _busyPhone.Add(client1.Phone);
        _busyPhone.Add(client2.Phone);
        
        while (IsWork)
        {
            //Попытка войти в аккаунт для собеседника 1
            reCreateC1:
            
            var resultC1 = (await Globals.GetAccountsWarm(_busyPhone.ToArray()));
            
            if (resultC1.Length == 0)
                break;
            
            var c1Account = resultC1[0];
            
            _busyPhone.Add(c1Account.phone);
            
            await client1.ReCreate(phone: $"+{c1Account.phone}", account: c1Account.path);
            await client1.LoginFile();
            
            if (!await IsValid(client1))
            {
                _busyPhone.Remove(c1Account.phone);
                Directory.Delete(client1.Account, true);
                goto reCreateC1;
            }
            
            //Попытка войти в аккаунт для собеседника 2
            reCreateC2:
            var resultC2 = (await Globals.GetAccountsWarm(_busyPhone.ToArray()));
            
            if (resultC2.Length == 0)
                break;
            
            var c2Account = resultC2[0];

            _busyPhone.Add(c2Account.phone);
            
            await client2.ReCreate(phone: $"+{c2Account.phone}", account: c2Account.path);
            await client2.LoginFile();
            
            if (!await IsValid(client2))
            {
                _busyPhone.Remove(c2Account.phone);
                Directory.Delete(client2.Account, true);
                goto reCreateC2;
            }

            //Импорт контактов на устройства
            var fileContact = new FileInfo($"{idThread}_contacts.vcf");

            await File.WriteAllTextAsync(fileContact.FullName, ContactManager.Export(
                new List<CObj>()
                {
                    new CObj($"Artemiy {new Random().Next(0, 20_000)}", client1.Phone),
                    new CObj($"Artemiy {new Random().Next(0, 20_000)}", client2.Phone),
                }
            ));

            if (!await client1.ImportContacts(fileContact.FullName))
            {
                _busyPhone.Remove(c1Account.phone);
                Directory.Delete(client1.Account, true);
                goto reCreateC1;
            }
            
            if (!await client2.ImportContacts(fileContact.FullName))
            {
                _busyPhone.Remove(c2Account.phone);
                Directory.Delete(client2.Account, true);
                goto reCreateC2;
            }
            
            File.Delete(fileContact.FullName);
            
            client1.AccountData.TrustLevelAccount++;
            client2.AccountData.TrustLevelAccount++;

            for (var i = 0; i < Globals.Setup.CountMessage; i++)
            {
                foreach (var text in texts)
                {
                    await client1.SendMessage(client2.Phone, text);

                    await Task.Delay(500);

                    await client2.SendMessage(client1.Phone, text);
                }
            }

            client1.AccountData.LastActiveDialog![client2.Phone] = DateTime.Now;
            client2.AccountData.LastActiveDialog![client1.Phone] = DateTime.Now;
            
            await client1.UpdateData();
            await client2.UpdateData();
        }

        async Task<bool> IsValid(WAClient client) =>
            !await client.GetInstance().ExistsElement("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']") &&//To-Do
            !await client.GetInstance().ExistsElement("//node[@text='ДАЛЕЕ']") &&//To-Do
            !await client.GetInstance().ExistsElement("//node[@resource-id='android:id/message']");
    }
}