using Newtonsoft.Json;
using System.Runtime.InteropServices;
using UBot.Whatsapp.Web;
using MemuLib.Core;
using System.Reflection.Metadata;
using static Microsoft.Maui.ApplicationModel.Permissions;

namespace UBot.Whatsapp;

public class Client
{
    public string Phone { private set; get; }
    public string Account { private set; get; }
    public bool IsW4B { private set; get; }
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
        {
            AccountData = JsonConvert.DeserializeObject<AccountData>(File.ReadAllText($@"{account}\Data.json"))!;
            AccountData.MessageHistory ??= new Dictionary<string, DateTime>();
        }

        if (!string.IsNullOrEmpty(phone))
            Web = new WClient(phone[0] == '+' ? phone.Remove(0, 1) : phone);

        Mem = deviceId == -1 ? new MemuLib.Core.Client(0) : new MemuLib.Core.Client(deviceId);
    }

    public MemuLib.Core.Client GetInstance() => Mem;

    public async Task Start() => await Mem.Start();

    public async Task Stop() => await Mem.Stop();

    public async Task UpdateData(bool PullAccount)
    {
        if (!File.Exists($@"{Account}\Data.json"))
            return;

        await File.WriteAllTextAsync($@"{Account}\Data.json", JsonConvert.SerializeObject(AccountData, Formatting.Indented));

        if (!PullAccount)
            return;

        await Mem.Pull($@"{Account}\{PackageName}", @$"/data/data/{PackageName}/databases/");
        await Mem.Pull($@"{Account}\{PackageName}", @$"/data/data/{PackageName}/files/");
        await Mem.Pull($@"{Account}\{PackageName}", @$"/data/data/{PackageName}/shared_prefs/");

    }

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

            await Task.Delay(1_500);
            var lastCalled = false;
            while (true)
            {
                var dump = await Mem.DumpScreen();

                if (!await Mem.ExistsElement("text=\"ПРОПУСТИТЬ\"", dump))
                    goto s1;

                await Mem.Click("text=\"ПРОПУСТИТЬ\"");
                await Task.Delay(1_000);
                await Mem.StopApk(PackageName);
                await Mem.RunApk(PackageName);
                await Task.Delay(1_000);
                lastCalled = true;
                continue;
            s1:
                if (!await Mem.ExistsElement($"resource-id=\"{PackageName}:id/registration_name\"", !lastCalled ? dump : null))
                    goto s2;

                await Mem.Input("text=\"Введите своё имя\"", name.Replace(' ', 'I'), !lastCalled ? dump : null);
                await Mem.Click("text=\"ДАЛЕЕ\"", !lastCalled ? dump : null);
                await Task.Delay(1_000);
                await Mem.StopApk(PackageName);
                await Mem.RunApk(PackageName);
                await Task.Delay(1_000);
                lastCalled = true;
                continue;
            s2:
                if (!await Mem.ExistsElement($"resource-id=\"{PackageName}:id/code\"", !lastCalled ? dump : null))
                    goto s3;

                await Mem.Input($"resource-id=\"{PackageName}:id/code\"", Globals.Setup.PinCodeAccount.ToString(), !lastCalled ? dump : null);
                await Mem.StopApk(PackageName);
                await Mem.RunApk(PackageName);
                await Task.Delay(1_000);
                lastCalled = true;
                continue;
            s3:
                if (!await Mem.ExistsElement("text=\"НЕ СЕЙЧАС\"", !lastCalled ? dump : null))
                    goto s4;

                await Mem.Click("text=\"НЕ СЕЙЧАС\"", !lastCalled ? dump : null);
                await Task.Delay(1_000);
                await Mem.StopApk(PackageName);
                await Mem.RunApk(PackageName);
                await Task.Delay(1_000);
                lastCalled = true;
                continue;
            s4:
                if (!await Mem.ExistsElement("text=\"Выберите частоту резервного копирования\"", !lastCalled ? dump : null))
                    return true;

                //await Mem.Click(374, 513);//Выберите частоту резервного копирования
                //await Mem.Click(414, 846);//text=\"Никогда\"
                //await Mem.Click(616, 1238);//text=\"ГОТОВО\"
                await Mem.Click("text=\"Выберите частоту резервного копирования\"", !lastCalled ? dump : null);
                await Mem.Click("text=\"Никогда\"", !lastCalled ? dump : null);
                await Mem.Click("text=\"ГОТОВО\"", !lastCalled ? dump : null);
                await Task.Delay(1_000);
                await Mem.StopApk(PackageName);
                await Mem.RunApk(PackageName);
                continue;
            }
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

        var rndName = $"{new Random().Next(1_000, 1_000_000)}_contacts.vcf";

        var file = new FileInfo(path);
        file.CopyTo(@$"{Globals.Setup.PathToDownloadsMemu}\{rndName}");

        await MemuCmd.ExecMemuc(
            $"-i {Mem.Index} execcmd am start -t \"text/x-vcard\" -d \"file:///storage/emulated/0/Download/{rndName}\" -a android.intent.action.VIEW cz.psencik.com.android.contacts");//com.android.contacts");

        var dump = await Mem.DumpScreen();

        if (!await Mem.ExistsElement("text=\"ОК\"", dump, false))
            return false;

        await Mem.Click("text=\"ОК\"", dump);

        await Mem.StopApk(PackageName);
        await Mem.RunApk(PackageName);

        return true;
    }

    public async Task<bool> SendPreMessage(string toPhone, string text)
    {
        var to = (toPhone[0] == '+') ? toPhone : $"+{toPhone}";
        //await Mem.Shell($@"am start -a android.intent.action.VIEW -d https://wa.me/{to}/?text={Uri.EscapeDataString(text)}");
        var command = new FileInfo($@"{Globals.Setup.PathToDownloadsMemu}\{to}.sh");
        var isSended = false;
        await File.WriteAllTextAsync(command.FullName,
            $"am start -a android.intent.action.VIEW -d https://wa.me/{to}/?text={Uri.EscapeDataString(text)} {PackageName}");//mb fix
        Log.Write(await Mem.Shell($@"sh /storage/emulated/0/Download/{command.Name}"));
        //await Mem.Push(command.FullName, "/data/local/tmp");
        //await Mem.Shell($@"sh /data/local/tmp/{to}.sh");
        for (var i = 0; i < 3; i++)
        {
            var dump = await Mem.DumpScreen();
            if (!await Mem.ExistsElement("content-desc=\"Отправить\"", dump, false))
            {
                if (await Mem.ExistsElement("text=\"ОК\"", dump, false))
                    await Mem.Click("text=\"ОК\"", dump);
                if (await Mem.ExistsElement("text=\"OK\"", dump, false))
                    await Mem.Click("text=\"OK\"", dump);
                await Task.Delay(1_500);
                continue;
            }
            await Mem.Click("content-desc=\"Отправить\"", dump);
            isSended = true;
            //AccountData.MessageHistory[to] = DateTime.Now;
            break;
        }
        //await Mem.Shell($"rm /data/local/tmp/{to}.sh");
        File.Delete(command.FullName);
        return isSended;
    }

    /*
     * Нужно сделать хотфикс на обновление контактов
     * Чтобы работал метод
     */
    public async Task<bool> SendMessage(string contact, string text, FileInfo image = null)
    {
        var to = contact.Replace("+", "");
        //await Mem.Shell($@"am start -a android.intent.action.VIEW -d https://wa.me/{to}/?text={Uri.EscapeDataString(text)}");
        var command = new FileInfo($@"{Globals.Setup.PathToDownloadsMemu}\{to}.sh");
        var isSended = false;
        var commandImage = string.Empty;

        if (image != null && image.Exists)
        {
            if (!File.Exists($@"{Globals.Setup.PathToDownloadsMemu}\{image.Name}"))
                new FileInfo(image.FullName).CopyTo($@"{Globals.Setup.PathToDownloadsMemu}\{image.Name}");

            commandImage = $" --eu android.intent.extra.STREAM file:///storage/emulated/0/Download/{image.Name}";//Тут добавлен пробел для синхроности, я мудак идите нахуй
        }
        //execcmd am start -e jid 79772801086@s.whatsapp.net com.whatsapp.w4b/com.whatsapp.Conversation
        await File.WriteAllTextAsync(command.FullName,
        //NL=$'\n' ; am start -a android.intent.action.SEND --es android.intent.extra.TEXT "Hello ${NL} World" -t text/plain -e jid '79772801086@s.whatsapp.net' --eu android.intent.extra.STREAM file:///storage/emulated/0/Download/1.jpg com.whatsapp.w4b/com.whatsapp.Conversation 
        //NL=$'\\n' ; am start -a android.intent.action.SEND --es android.intent.extra.TEXT \"{text.Replace("\r", "${NL}").Replace("\n", "${NL}")}\" -t text/plain -e jid '{to}@s.whatsapp.net'{commandImage} com.whatsapp.w4b/com.whatsapp.Conversation 
        $"NL=$'\\n' ; am start -a android.intent.action.SEND --es android.intent.extra.TEXT \"{text.Replace("\r", "${NL}").Replace("\n", "${NL}")}\" -t text/plain -e jid '{to}@s.whatsapp.net'{commandImage} com.whatsapp.w4b");

        Log.Write(await Mem.Shell($@"sh /storage/emulated/0/Download/{command.Name}"));

        for (var i = 0; i < 3; i++)
        {
            var dump = await Mem.DumpScreen();
            if (!await Mem.ExistsElement("content-desc=\"Отправить\"", dump, false))
            {
                if (await Mem.ExistsElement("text=\"ОК\"", dump, false))
                    await Mem.Click("text=\"ОК\"", dump);

                if (await Mem.ExistsElement("text=\"OK\"", dump, false))
                    await Mem.Click("text=\"OK\"", dump);

                await Task.Delay(1_000);
                continue;
            }

            await Mem.Click("content-desc=\"Отправить\"", dump);
            isSended = true;
            //AccountData.MessageHistory[to] = DateTime.Now;
            break;
        }

        //Log.Write(await Mem.Shell($"rm /data/local/tmp/{to}.sh"));
        File.Delete(command.FullName);

        return isSended;
    }

    public async Task<bool> IsValid()
    {
        await Task.Delay(1_000);

        var dump = await Mem.DumpScreen();

        if (await Mem.ExistsElement("text=\"Перезапустить приложение\"", dump, false))
        {
            await Mem.Click("text=\"Перезапустить приложение\"", dump);
            await Mem.StopApk(PackageName);
            await Mem.RunApk(PackageName);
            await Task.Delay(500);
            dump = await Mem.DumpScreen();
        }

        if (await Mem.ExistsElement("text=\"ОК\"", dump, false))
        {
            await Mem.Click("text=\"ОК\"", dump);
            await Task.Delay(500);
            dump = await Mem.DumpScreen();
        }

        if (await Mem.ExistsElement("text=\"OK\"", dump, false))
        {
            await Mem.Click("text=\"OK\"", dump);
            await Task.Delay(500);
            dump = await Mem.DumpScreen();
        }

        if (await Mem.ExistsElement("text=\"ПРОПУСТИТЬ\"", dump, false))
        {
            await Mem.Click("text=\"ПРОПУСТИТЬ\"", dump);
            await Task.Delay(500);
            dump = await Mem.DumpScreen();
        }

        if (await Mem.ExistsElement("text=\"Закрыть приложение\"", dump, false))
        {
            await Mem.Click("text=\"Закрыть приложение\"", dump);
            await Mem.StopApk(PackageName);
            await Mem.RunApk(PackageName);
            await Task.Delay(500);
            dump = await Mem.DumpScreen();
        }

        return !await Mem.ExistsElements(new string[] {
        "text=\"ПРИНЯТЬ И ПРОДОЛЖИТЬ\"",//Думаешь ничем не отличается? А вот хуй тебе " "
        "text=\"ПРИНЯТЬ И ПРОДОЛЖИТЬ\"",
        "text=\"ПРИНЯТЬ И ПРОДОЛЖИТЬ\"",//Думаешь ничем не отличается? А вот хуй тебе " "
        "text=\"Принять и продолжить\"",
        "text=\"AGREE AND CONTINUE\"",
        "text=\"ДАЛЕЕ\"",
        "text=\"Перезапустить приложение\"",
        "text=\"Закрыть приложение\"",
        "content-desc=\"Неверный номер?\"",
        "text=\"ЗАПРОСИТЬ РАССМОТРЕНИЕ\"",
        "text=\"WA Business\"",
        "resource-id=\"com.whatsapp.w4b:id/btn_play_store\"",
        "text=\"WhatsApp\"",
        "resource-id=\"android:id/progress\"",
        "text=\"ПОДТВЕРДИТЬ\""}, dump, false);
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