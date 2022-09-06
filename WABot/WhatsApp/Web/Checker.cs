using WABot.Pages;

namespace WABot.WhatsApp.Web;
public class Checker
{
    private readonly List<string> _usedPhones;

    public bool IsStop;

    public Checker() => _usedPhones = new List<string>();

    public async Task Start()
    {
        var tasks = new List<Task>();

        IsStop = false;

        Dashboard.GetInstance().CountTasks = (await Globals.GetAccounts(_usedPhones.ToArray(), true)).Length;

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

            ++Dashboard.GetInstance().CompletedTasks;
            await waw.Web!.Free(false);
            waw.Web!.RemoveQueue();
        }
    }
}