using WABot.Pages;

namespace WABot.WhatsApp.Web;
public class Checker
{
    private readonly List<string> _usedPhones;
    private readonly List<string> _usedPhonesUsers;
    private string[] _contacts;
    
    private FileInfo _logFile;
    public bool IsStop;

    public Checker() => _usedPhonesUsers = _usedPhones = new List<string>();

    public async Task Start()
    {
        var tasks = new List<Task>();

        IsStop = false; 
        
        _contacts = await File.ReadAllLinesAsync(Globals.Setup.PathToPhonesUsers);
        _logFile = new FileInfo($@"{Globals.TempDirectory.FullName}\{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_phones.txt");
        
        Dashboard.GetInstance().CountTasks = (Globals.Setup.EnablePhoneCheck) ? _contacts.Length : (await Globals.GetAccounts(_usedPhones.ToArray(), true)).Length;

        for (var i = 0; i < Globals.Setup.CountThreadsChrome; i++)
        {
            var task = Handler();
            
            await Task.Delay(2_500);

            tasks.Add(task);
        }

        _ = Task.WaitAll(tasks.ToArray(), -1);

        IsStop = true;
        Stop();
    }

    public void Stop()
    {
        _usedPhones.Clear();
        _usedPhonesUsers.Clear();
    }

    private async Task Handler()
    {
        while (!IsStop)
        {
            await Task.Delay(1_500);
            var accountsWeb = await Globals.GetAccounts(_usedPhones.ToArray(), true);

            if (accountsWeb.Length == 0)
                break;

            var (phone, path) = accountsWeb[0];

            if (_usedPhones.Contains(phone))
                continue;

            _usedPhones.Add(phone);

            var countTryLogin = 0;
            tryAgain:
            var waw = new WaClient(phone, path);

            try
            {
                await waw.Web!.Init(false, path);
                if (!await waw.Web!.WaitForInChat())
                    throw new Exception("Cant connect");
            }
            catch (Exception)
            {
                await waw.Web!.Free(true);
                Directory.Move(path, $@"{Globals.LogoutAccountsDirectory}\{phone}");
                waw.Web!.RemoveQueue();
                ++Dashboard.GetInstance().BannedAccounts;
                Dashboard.GetInstance().CountTasks = (await Globals.GetAccounts(_usedPhones.ToArray(), true)).Length;
                if (countTryLogin++ > 2)
                    goto tryAgain;
                else
                    continue;
            }


            if (Globals.Setup.EnablePhoneCheck)
            {
            recurseSendMessageToContact:
                var contact = GetFreeNumberUser();

                if (string.IsNullOrEmpty(contact))
                {
                    await waw.Web!.Free(false);
                    break;
                }

                if (!waw.Web!.IsConnected)
                {
                    await BanAccount(waw.Web!, contact);
                    continue;
                }

                try
                {
                    if (await waw.Web!.CheckValidPhone(contact))
                    {
                        Log.Write(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{contact}",
                        _logFile.FullName);
                        ++Dashboard.GetInstance().CompletedTasks;
                    }

                    goto recurseSendMessageToContact;
                }
                catch
                {
                    await BanAccount(waw.Web!, contact);
                    continue;
                }
            }
            
            await waw.Web!.Free(false);
            waw.Web!.RemoveQueue();
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

        async Task BanAccount(WAWClient waw, string contact)
        {
            await waw.Free(false);
            waw.RemoveQueue();
            _usedPhonesUsers.Remove(contact);
        }
    }
}