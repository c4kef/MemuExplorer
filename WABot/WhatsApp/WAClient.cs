using MemuLib.Core.SimServices;
using WABot.WhatsApp.Web;

namespace WABot.WhatsApp;

public class WaClient
{
    private Client _mem;
    public string Phone { private set; get; }
    public string Account { private set; get; }
    public AccountData AccountData;
    public bool IsW4B;
    public WAWClient? Web;
    public string PackageName
    {
        get => (IsW4B) ? "com.whatsapp.w4b"  : "com.whatsapp";
    }

    public WaClient(string phone = "", string account = "", int deviceId = -1)
    {
        Phone = phone;
        Account = account;

        AccountData = new AccountData();

        if (account != string.Empty)
            AccountData = JsonConvert.DeserializeObject<AccountData>(File.ReadAllText($@"{account}\Data.json"))!;

        if (!string.IsNullOrEmpty(phone))
            Web = new WAWClient(phone[0] == '+' ? phone.Remove(0, 1) : phone);

        _mem = deviceId == -1 ? new Client(0) : new Client(deviceId);
    }

    public Client GetInstance()
    {
        return _mem;
    }

    public async Task Start()
    {
        await _mem.Start();
    }

    public async Task Stop()
    {
        await _mem.Stop();
    }

    public async Task LoginFile([Optional] string path, [Optional] string name)
    {
        try
        {
            await _mem.Shell($"pm clear {PackageName}");
            await _mem.RunApk(PackageName);
            await _mem.StopApk(PackageName);
            await _mem.Push($@"{(Account == string.Empty ? path : Account)}\{PackageName}\.", @$"/data/data/{PackageName}/");
            await _mem.Shell($"rm /data/data/{PackageName}/databases/*msgstore*");
            await _mem.RunApk(PackageName);
            await _mem.StopApk(PackageName);
            await _mem.RunApk(PackageName);

            if (!await _mem.ExistsElement("//node[@text='ПРОПУСТИТЬ']"))
                goto s1;

            await _mem.Click("//node[@text='ПРОПУСТИТЬ']");
            await Task.Delay(2_000);
            await _mem.StopApk(PackageName);
            await _mem.RunApk(PackageName);

        s1:
            if (!await _mem.ExistsElement("//node[@resource-id='com.whatsapp:id/registration_name']"))
                goto s2;

            await _mem.Input("//node[@text='Введите своё имя']", name.Replace(' ', 'I'));
            await _mem.Click("//node[@text='ДАЛЕЕ']");
            await Task.Delay(2_000);
            await _mem.StopApk(PackageName);
            await _mem.RunApk(PackageName);

        s2:
            if (!await _mem.ExistsElement("//node[@text='Выберите частоту резервного копирования']"))
                goto s3;

            await _mem.Click("//node[@text='Выберите частоту резервного копирования']");
            await _mem.Click("//node[@text='Никогда']");
            await _mem.Click("//node[@text='ГОТОВО']");
            await Task.Delay(2_000);
            await _mem.StopApk(PackageName);
            await _mem.RunApk(PackageName);

        s3:
            if (!await _mem.ExistsElement("//node[@text='НЕ СЕЙЧАС']"))
                return;

            await _mem.Click("//node[@text='НЕ СЕЙЧАС']");
            await Task.Delay(2_000);
            await _mem.StopApk(PackageName);
            await _mem.RunApk(PackageName);
        }
        catch(Exception ex)
        {
            Log.Write(ex.Message);
        }
    }

    public async Task<bool> ImportContacts(string path)
    {
        await _mem.ClearContacts();
        await _mem.ImportContacts(path);
        
        if (!await _mem.ExistsElement("//node[@text='ОК']"))
            return false;

        await _mem.Click("//node[@text='ОК']");

        await _mem.StopApk(PackageName);
        await _mem.RunApk(PackageName);

        return true;
    }

    public async Task<bool> SendMessage(string to, string text)
    {
        //await _mem.Shell($@"am start -a android.intent.action.VIEW -d https://wa.me/{to}/?text={Uri.EscapeDataString(text)}");
        var command = new FileInfo($@"{Globals.TempDirectory.FullName}\{to}.sh");
        var isSended = false;

        await File.WriteAllTextAsync(command.FullName,
            $"am start -a android.intent.action.VIEW -d https://wa.me/{to}/?text={Uri.EscapeDataString(text)} {PackageName}");//mb fix

        await _mem.Push(command.FullName, "/data/local/tmp");
        await _mem.Shell($@"sh /data/local/tmp/{to}.sh");
        
        for (var i = 0; i < 3; i++)
        {
            if (!await _mem.ExistsElement("//node[@content-desc='Отправить']"))
            {
                if (await _mem.ExistsElement("//node[@text='ОК']"))
                    await _mem.Click("//node[@text='ОК']");

                if (await _mem.ExistsElement("//node[@text='OK']"))
                    await _mem.Click("//node[@text='OK']");
             
                continue;
            }

            await _mem.Click("//node[@content-desc='Отправить']");
            isSended = true;
            break;
        }

        await _mem.Shell($"rm /data/local/tmp/{to}.sh");
        File.Delete(command.FullName);

        return isSended;
    }

    public async Task ReCreate([Optional] string? phone, [Optional] string? account, [Optional] int? deviceId)
    {
        IsW4B = false;

        if (deviceId is not null)
        {
            await _mem.Stop();
            _mem = new Client((int)deviceId);
        }

        if (account is not null)
        {
            Account = account;
            IsW4B = Directory.Exists($@"{account}\com.whatsapp.w4b");
            AccountData =
                JsonConvert.DeserializeObject<AccountData>(await File.ReadAllTextAsync($@"{account}\Data.json"))!;
        }

        if (phone is not null)
        {
            Web = new WAWClient(phone.Remove(0, 1));
            Phone = phone;
        }
    }

    public async Task UpdateData()
    {
        await File.WriteAllTextAsync($@"{Account}\Data.json", JsonConvert.SerializeObject(AccountData, Formatting.Indented));
    }
}