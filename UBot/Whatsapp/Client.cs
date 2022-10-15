using Newtonsoft.Json;
using System.Runtime.InteropServices;
using UBot.Whatsapp.Web;
using MemuLib.Core;
using System.Reflection.Metadata;

namespace UBot.Whatsapp;

public class Client
{
    public string Phone { private set; get; }
    public string Account { private set; get; }
    public bool IsW4B{private set; get; }
    private MemuLib.Core.Client Mem { get; set; }
    public AccountData AccountData { get; set; }
    public WClient Web { private set; get; }

    public string PackageName
    {
        get => (IsW4B) ? "com.whatsapp.w4b" : "com.whatsapp";
    }

    public Client(string phone = "", string account = "", int deviceId = -1, bool isW4B = false)
    {
        Phone = string.IsNullOrEmpty(phone) ? string.Empty : phone[0] == '+' ? phone : "+" + phone;
        Account = account;

        IsW4B = isW4B;

        AccountData = new AccountData();

        if (account != string.Empty)
            AccountData = JsonConvert.DeserializeObject<AccountData>(File.ReadAllText($@"{account}\Data.json"))!;

        if (!string.IsNullOrEmpty(phone))
            Web = new WClient(phone[0] == '+' ? phone.Remove(0, 1) : phone);

        Mem = deviceId == -1 ? new MemuLib.Core.Client(0) : new MemuLib.Core.Client(deviceId);
    }

    public MemuLib.Core.Client GetInstance() => Mem;

    public async Task Start() => await Mem.Start();

    public async Task Stop() => await Mem.Stop();

    public async Task UpdateData() => await File.WriteAllTextAsync($@"{Account}\Data.json", JsonConvert.SerializeObject(AccountData, Formatting.Indented));

    public async Task<bool> Login([Optional] string path, [Optional] string name)
    {
        try
        {
            await Mem.Shell($"pm clear {PackageName}");
            await Mem.RunApk(PackageName);
            await Mem.StopApk(PackageName);
            await Mem.RunApk(PackageName);
            //await Task.Delay(2_000);
            //memuc -i 0 adb push "D:\Data\Ru\79361879319\com.whatsapp.w4b" "/data/data/com.whatsapp.w4b/"
            await Mem.Push($@"{(Account == string.Empty ? path : Account)}\{PackageName}", @$"/data/data/");//{PackageName}/");
            //await Mem.Shell($"rm /data/data/{PackageName}/databases/*msgstore*");
            await Mem.StopApk(PackageName);
            await Mem.RunApk(PackageName);
            await Mem.StopApk(PackageName);
            await Mem.RunApk(PackageName);

            var dump = await Mem.DumpScreen();

            if (!await Mem.ExistsElement("text=\"ПРОПУСТИТЬ\"", dump))
                goto s1;

            await Mem.Click("text=\"ПРОПУСТИТЬ\"");
            await Task.Delay(1_000);
            await Mem.StopApk(PackageName);
            await Mem.RunApk(PackageName);
            await Task.Delay(1_000);
            dump = await Mem.DumpScreen();

        s1:
            if (!await Mem.ExistsElement($"resource-id=\"{PackageName}:id/registration_name\"", dump))
                goto s2;

            await Mem.Input("text=\"Введите своё имя\"", name.Replace(' ', 'I'), dump);
            await Mem.Click("text=\"ДАЛЕЕ\"", dump);
            await Task.Delay(1_000);
            await Mem.StopApk(PackageName);
            await Mem.RunApk(PackageName);
            await Task.Delay(1_000);
            dump = await Mem.DumpScreen();

        s2:
            if (!await Mem.ExistsElement("text=\"Выберите частоту резервного копирования\"", dump))
                goto s3;

            await Mem.Click(374, 513);//Выберите частоту резервного копирования
            await Mem.Click(414, 846);//text=\"Никогда\"
            await Mem.Click(616, 1238);//text=\"ГОТОВО\"
            await Task.Delay(1_000);
            await Mem.StopApk(PackageName);
            await Mem.RunApk(PackageName);
            await Task.Delay(1_000);
            dump = await Mem.DumpScreen();

        s3:
            if (!await Mem.ExistsElement("text=\"НЕ СЕЙЧАС\"", dump))
                goto s4;

            await Mem.Click("text=\"НЕ СЕЙЧАС\"", dump);
            await Task.Delay(1_000);
            await Mem.StopApk(PackageName);
            await Mem.RunApk(PackageName);
            await Task.Delay(1_000);
            dump = await Mem.DumpScreen();

        s4:
            if (!await Mem.ExistsElement($"resource-id=\"{PackageName}:id/code\"", dump))
                return true;

            await Mem.Input($"resource-id=\"{PackageName}:id/code\"", "120638", dump);
            await Mem.StopApk(PackageName);
            await Mem.RunApk(PackageName);
            return true;
        }
        catch (Exception ex)
        {
            Log.Write(ex.Message);
            return false;
        }
    }

    public async Task<bool> ImportContacts(string path)
    {
        await Mem.ClearContacts();
        await Mem.ImportContacts(path);

        var dump = await Mem.DumpScreen();

        if (!await Mem.ExistsElement("text=\"ОК\"", dump, false))
            return false;

        await Mem.Click("text=\"ОК\"", dump);

        await Mem.StopApk(PackageName);
        await Mem.RunApk(PackageName);

        return true;
    }

    public async Task<bool> SendMessage(string to, string text)
    {
        //await Mem.Shell($@"am start -a android.intent.action.VIEW -d https://wa.me/{to}/?text={Uri.EscapeDataString(text)}");
        var command = new FileInfo($@"{Globals.TempDirectory.FullName}\{to}.sh");
        var isSended = false;

        await File.WriteAllTextAsync(command.FullName,
            $"am start -a android.intent.action.VIEW -d https://wa.me/{to}/?text={Uri.EscapeDataString(text)} {PackageName}");//mb fix

        await Mem.Push(command.FullName, "/data/local/tmp");
        await Mem.Shell($@"sh /data/local/tmp/{to}.sh");

        for (var i = 0; i < 3; i++)
        {
            var dump = await Mem.DumpScreen();
            if (!await Mem.ExistsElement("content-desc=\"Отправить\"", dump, false))
            {
                if (await Mem.ExistsElement("text=\"ОК\"", dump, false))
                    await Mem.Click("text=\"ОК\"", dump);

                if (await Mem.ExistsElement("text=\"OK\"", dump, false))
                    await Mem.Click("text=\"OK\"", dump);

                /*var document = await Mem.DumpScreen();
                if (await Mem.ExistsElement("text=\"ОК\"", document))
                    await Mem.Click("text=\"ОК\"", document);

                if (await Mem.ExistsElement("text=\"OK\"", document))
                    await Mem.Click("text=\"OK\"", document);*/

                continue;
            }

            await Mem.Click("content-desc=\"Отправить\"", dump);
            isSended = true;
            break;
        }

        await Mem.Shell($"rm /data/local/tmp/{to}.sh");
        File.Delete(command.FullName);

        return isSended;
    }

    public async Task<bool> IsValid()
    {
        await Task.Delay(MemuLib.Settings.WaitingSecs);

        var dump = await Mem.DumpScreen();

        if (await Mem.ExistsElement("text=\"Перезапустить приложение\"", dump, false))
        {
            await Mem.Click("text=\"Перезапустить приложение\"", dump);
            await Mem.StopApk(PackageName);
            await Mem.RunApk(PackageName);
            dump = await Mem.DumpScreen();
        }

        if (await Mem.ExistsElement("text=\"ОК\"", dump, false))
        {
            await Mem.Click("text=\"ОК\"", dump);
            dump = await Mem.DumpScreen();
        }

        if (await Mem.ExistsElement("text=\"OK\"", dump, false))
        {
            await Mem.Click("text=\"OK\"", dump);
            dump = await Mem.DumpScreen();
        }

        if (await Mem.ExistsElement("text=\"ПРОПУСТИТЬ\"", dump, false))
        {
            await Mem.Click("text=\"ПРОПУСТИТЬ\"", dump);
            dump = await Mem.DumpScreen();
        }

        if (await Mem.ExistsElement("text=\"Закрыть приложение\"", dump, false))
        {
            await Mem.Click("text=\"Закрыть приложение\"", dump);
            await Mem.StopApk(PackageName);
            await Mem.RunApk(PackageName);
            dump = await Mem.DumpScreen();
        }

        return !await Mem.ExistsElement("text=\"ПРИНЯТЬ И ПРОДОЛЖИТЬ\"", dump, false) &&
               !await Mem.ExistsElement("text=\"ДАЛЕЕ\"", dump, false) &&
               !await Mem.ExistsElement("text=\"Перезапустить приложение\"", dump, false) &&
               !await Mem.ExistsElement("text=\"Закрыть приложение\"", dump, false) &&
               !await Mem.ExistsElement("content-desc=\"Неверный номер?\"", dump, false) &&
               !await Mem.ExistsElement("text=\"ЗАПРОСИТЬ РАССМОТРЕНИЕ\"", dump, false) &&
               !await Mem.ExistsElement("text=\"WA Business\"", dump, false) &&
               !await Mem.ExistsElement("text=\"WhatsApp\"", dump, false) &&
               !await Mem.ExistsElement("resource-id=\"android:id/progress\"", dump, false) &&
               !await Mem.ExistsElement("text=\"ПОДТВЕРДИТЬ\"", dump, false);
    }

    public async Task ReCreate([Optional] string phone, [Optional] string account, [Optional] int? deviceId)
    {
        IsW4B = false;

        if (deviceId is not null)
        {
            await Mem.Stop();
            Mem = new MemuLib.Core.Client((int)deviceId);
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
            Web = new WClient(phone[0] == '+' ? phone.Remove(0, 1) : phone);
            Phone = string.IsNullOrEmpty(phone) ? string.Empty : phone[0] == '+' ? phone : "+" + phone;
        }
    }
}