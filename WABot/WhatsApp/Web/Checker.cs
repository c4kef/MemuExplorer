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

        Dashboard.GetInstance().CountTasks = Directory.GetFiles($@"{Globals.Setup.PathToDirectoryAccountsWeb}\First").Length;

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
            var accountsWeb = await Globals.GetAccountsWeb(_usedPhones.ToArray());

            if (accountsWeb.Length == 0)
                break;

            var result = accountsWeb[0];

            var phone = result.Name.Split('.')[0];

            if (_usedPhones.Contains(phone))
                continue;

            if (Directory.Exists($@"{result.Directory!.FullName}\{result.Name.Split('.')[0]}"))
                Directory.Move($@"{result.Directory!.FullName}\{result.Name.Split('.')[0]}", $@"{Globals.Setup.PathToDirectoryAccountsWeb}\{result.Name.Split('.')[0]}");

            result.MoveTo($@"{Globals.Setup.PathToDirectoryAccountsWeb}\{result.Name}", true);

            _usedPhones.Add(phone);

            var waw = new WAWClient(phone);

            try
            {
                await waw.Init(false);
                if (!await waw.WaitForInChat())
                    throw new Exception("Cant connect");
            }
            catch (Exception)
            {
                await waw.Free();
                if (File.Exists(@$"{result.FullName}"))
                    File.Delete(@$"{result.FullName}");

                waw.RemoveQueue();
                ++Dashboard.GetInstance().BannedAccounts;
                Dashboard.GetInstance().CountTasks = Directory.GetFiles($@"{Globals.Setup.PathToDirectoryAccountsWeb}\First").Length;
                continue;
            }

            ++Dashboard.GetInstance().CompletedTasks;
            await waw.Free();
            waw.RemoveQueue();
        }
    }
}