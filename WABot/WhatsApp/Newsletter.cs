using MS.WindowsAPICodePack.Internal;
using WABot.Pages;

namespace WABot.WhatsApp;

public class Newsletter
{
    public int MessagesSendedCount { get; private set; }

    private readonly Dictionary<int, Device> _tetheredDevices;
    private readonly Dictionary<string, int> _sendedMessagesCountFromAccount;
    private readonly List<string> _usedPhones;
    private readonly List<string> _usedPhonesUsers;

    private FileInfo _logFile;
    public bool IsStop;
    private FileInfo _pathToContacts = null!;
    private string[] _contacts;
    private string[] _names;
    private int _diedAccounts;

    public Newsletter()
    {
        _sendedMessagesCountFromAccount = new Dictionary<string, int>();
        _tetheredDevices = new Dictionary<int, Device>();
        _usedPhonesUsers = _usedPhones = new List<string>();
        _contacts = _names = new[] { "" };
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_log.txt");
    }

    private async Task HandlerNumberRewrite()
    {
        var contacts = _contacts.ToList();
        var removedPhone = new List<string>();

        while (!IsStop)
        {
            await Task.Delay(500);

            if (_usedPhonesUsers.Count() == 0)
                continue;

            if (removedPhone.Count() % 100 != 0)
                continue;

            var contact = string.Empty;

            foreach (var phone in _usedPhonesUsers)
                if (!removedPhone.Contains(phone))
                {
                    contact = phone;
                    break;
                }

            if (string.IsNullOrEmpty(contact))
                continue;

            contacts.RemoveAll(phone => phone == contact);

            await File.WriteAllLinesAsync(Globals.Setup.PathToPhonesUsers, contacts);

            removedPhone.Add(contact);
        }
    }

    public async Task Start(string text)
    {
        var tasks = new List<Task>();
        var rnd = new Random();

        IsStop = false;
        MessagesSendedCount = _diedAccounts = 0;
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_log.txt");

        _names = (await File.ReadAllLinesAsync(Globals.Setup.PathToUserNames))
            .Where(name => new Regex("^[a-zA-Z0-9. -_?]*$").IsMatch(name)).ToArray();

        _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToPhonesUsers);

        _ = Task.Run(HandlerNumberRewrite);

        var cObjs = new List<CObj>();

        foreach (var contact in _contacts)
            cObjs.Add(new CObj(MemuLib.Globals.RandomString(rnd.Next(5_15)), $"{(contact[0] == '+' ? "" : "+")}{contact}"));

        var pathToFileContacts = $@"{Globals.TempDirectory}\contact_{rnd.Next(10_000, 20_000)}.vcf";

        await File.WriteAllTextAsync(pathToFileContacts, ContactManager.Export(cObjs));

        _pathToContacts = new FileInfo(pathToFileContacts);

        Log.Write($"Добро пожаловать в логи, текст рассылки:\n{text}\n\n", _logFile.FullName);

        var busyDevices = new List<int>();

        Dashboard.GetInstance().CountTasks = _contacts.Length;

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

        foreach (var device in Globals.Devices.ToArray())
            device.InUsage = false;

        IsStop = true;

        Log.Write("\n\nКол-во сообщений с аккаунта:\n", _logFile.FullName);

        foreach (var account in _sendedMessagesCountFromAccount)
            Log.Write($"{account.Key} - {account.Value}\n", _logFile.FullName);

        Log.Write($"\nОбщее количество отправленных сообщений: {MessagesSendedCount}\n", _logFile.FullName);
        Log.Write($"\nОтлетело: {_diedAccounts}\n", _logFile.FullName);
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

        await client.ImportContacts(_pathToContacts.FullName);

        while (Globals.Devices.ToArray().Where(device => device.Index == clientIndex).ToArray()[0].IsActive && !IsStop)
        {
            var result = await Globals.GetAccounts(_usedPhones.ToArray());

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

                Dashboard.GetInstance().BannedAccounts = ++_diedAccounts;
                continue;
            }

            var countMsg = 0;

        recurseSendMessageToContact:

            if (!Globals.Devices.ToArray().Where(device => device.Index == clientIndex).ToArray()[0].IsActive || IsStop)
                break;

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

                Dashboard.GetInstance().BannedAccounts = ++_diedAccounts;
                continue;
            }

            var messageSended = await client.SendMessage(contact, SelectWord(Dashboard.GetInstance().TextMessage));

            switch (messageSended)
            {
                case false when await client.GetInstance().ExistsElement("//node[@text='OK']", false):
                    await client.GetInstance().Click("//node[@text='OK']");

                    _usedPhonesUsers.Remove(contact);
                    Dashboard.GetInstance().BannedAccounts = ++_diedAccounts;
                    var count = 0;
                    var messages = _sendedMessagesCountFromAccount.TakeLast(10);

                    foreach (var msg in messages)
                        count += msg.Value;

                    Dashboard.GetInstance().AverageMessages = (int)Math.Floor((decimal)count / messages.Count());

                    count = 0;
                    foreach (var msg in _sendedMessagesCountFromAccount)
                        count += msg.Value;

                    Dashboard.GetInstance().AverageMessagesAll = (int)Math.Floor((decimal)count / _sendedMessagesCountFromAccount.Count());
                    break;
                case true:
                    {
                        ++_sendedMessagesCountFromAccount[phone];
                        Dashboard.GetInstance().CompletedTasks = ++MessagesSendedCount;

                        Log.Write(
                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};{contact}",
                            _logFile.FullName);

                        if (++countMsg > Globals.Setup.CountMessagesFromAccount)
                            continue;

                        break;
                    }
            }

            await Task.Delay(new Random().Next(30_000, 60_000));//Ждем 30-60 сек
            goto recurseSendMessageToContact;
        }

        string SelectWord(string value)
        {
            var backValue = value;
            foreach (var match in new Regex(@"(\w+)\|\|(\w+)", RegexOptions.Multiline).Matches(backValue))
                backValue = backValue.Replace(match.ToString()!, match.ToString()!.Split("||")[new Random().Next(0, 100) >= 50 ? 1 : 0]);

            return backValue;
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
            return !await client.GetInstance().ExistsElement("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']", false) &&
                   !await client.GetInstance().ExistsElement("//node[@text='ДАЛЕЕ']", false) &&
                   !await client.GetInstance().ExistsElement("//node[@text='ЗАПРОСИТЬ РАССМОТРЕНИЕ']", false) &&
                   !await client.GetInstance().ExistsElement("//node[@resource-id='android:id/message']", false);
        }
    }
}