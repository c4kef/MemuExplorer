﻿namespace WABot.WhatsApp.Web
{
    public class AccPreparation
    {
        private readonly Dictionary<int, Device> _tetheredDevices;
        private readonly List<string> _usedPhones;
        private readonly List<string> _usedPhonesUsers;

        private string[] _contacts;
        private string[] _names;

        public AccPreparation()
        {
            _tetheredDevices = new Dictionary<int, Device>();
            _usedPhonesUsers = _usedPhones = new List<string>();
            _contacts = _names = new[] { "" };
        }

        public async Task Start()
        {
            var tasks = new List<Task>();
            var rnd = new Random();

            _names = (await File.ReadAllLinesAsync(Globals.Setup.PathToUserNames))
                .Where(name => new Regex("^[a-zA-Z0-9. -_?]*$").IsMatch(name)).ToArray();

            _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToPhonesUsers);

            var busyDevices = new List<int>();

            await Globals.InitAccountsFolder();

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
            _usedPhones.Clear();
            _usedPhonesUsers.Clear();
        }

        private async Task Handler(int idThread)
        {
            var client = _tetheredDevices[idThread].Client;
            var clientIndex = _tetheredDevices[idThread].Index;

            while (Globals.Devices.Where(device => device.Index == clientIndex).ToArray()[0].IsActive)
            {
                /*var result = await Globals.GetAccounts(_usedPhones.ToArray(), Globals.Setup.TrustLevelAccount);

                if (result.Length == 0)
                    break;

                var (phone, path) = result[0];

                if (_usedPhones.Contains(phone))
                    continue;

                _usedPhones.Add(phone);

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
                }*///Условно мы успешно авторизовались и все в этом духе

                await client.GetInstance().Click("//node[@content-desc='Ещё']");
                await client.GetInstance().Click("//node[@text='Связанные устройства']");
                await client.GetInstance().Click("//node[@text='ПРИВЯЗКА УСТРОЙСТВА']");
                if (await client.GetInstance().ExistsElement("//node[@text='OK']"))
                    await client.GetInstance().Click("//node[@text='OK']");

                var wClient = new WAWClient("7736066737");
                await wClient.Init(); 
                await wClient.SendText("79772801086", "Hello world!");
                await wClient.Free();
            }

            async Task<bool> IsValid()
            {
                return !await client.GetInstance().ExistsElement("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']", false) && //To-Do
                       !await client.GetInstance().ExistsElement("//node[@text='ДАЛЕЕ']", false) && //To-Do
                       !await client.GetInstance().ExistsElement("//node[@resource-id='android:id/message']", false);
            }
        }
    }
}
