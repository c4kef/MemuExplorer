using MemuLib.Core;
using MemuLib.Core.SimServices;
using System.Xml.Linq;
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

    public WaClient(string phone = "", string account = "", int deviceId = -1, bool isW4B = false)
    {
        Phone = string.IsNullOrEmpty(phone) ? string.Empty : phone[0] == '+' ? phone : "+" + phone;
        Account = account;

        IsW4B = isW4B;

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

    public async Task<bool> Register()
    {
        try
        {
            var codeCountry = "225";
            await _mem.StopApk(PackageName);
            await _mem.Shell($"pm clear {PackageName}");
            await _mem.RunApk(PackageName);

            if (!await _mem.ExistsElement("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']"))
                return false;

            await _mem.Click("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']");

            var obj = await OnlineSim.Create(codeCountry, "WhatsApp");

            if (obj is null)
                return false;

            if (!await _mem.ExistsElement("//node[@text='номер тел.']"))
                return false;

            await _mem.ClearInput($"//node[@resource-id='{PackageName}:id/registration_cc']");
            await _mem.Input($"//node[@resource-id='{PackageName}:id/registration_cc']", codeCountry);

            /*await _mem.Click($"//node[@resource-id='{PackageName}:id/registration_country']");
            await _mem.Click($"//node[@resource-id='{PackageName}:id/menuitem_search']");
            await _mem.Input($"//node[@resource-id='{PackageName}:id/search_src_text']", "Sierra");
            await _mem.Click("//node[@text='Сьерра-Леоне']");*/

            var phone = (await obj!.GetMessage())["number"]!.ToString().Remove(0, codeCountry.Length + 1);

            await _mem.Input("//node[@text='номер тел.']", phone);

            await _mem.Click("//node[@text='ДАЛЕЕ']");

            if (!await _mem.ExistsElement("//node[@text='ИЗМЕНИТЬ']"))
                return false;

            await _mem.Click("//node[@text='OK']");

            if (await _mem.ExistsElement("//node[@resource-id='android:id/message']"))
                return false;

            if (await _mem.ExistsElement("//node[@resource-id='android:id/aerr_restart']"))
            {
                await _mem.Click("//node[@resource-id='android:id/aerr_restart']");
                return false;
            }

            var countTryToGetCode = 0;
            var code = string.Empty;

            while (countTryToGetCode++ < 135)
            {
                var result = await obj!.GetMessage();
                if (result["response"]!.ToString() != "TZ_NUM_ANSWER")
                {
                    await Task.Delay(1_000);
                    continue;
                }

                code = result["msg"]!.ToString();
                break;
            }

            if (string.IsNullOrEmpty(code))
            {
                await obj.SetStatus(false);
                return false;
            }

            await _mem.Input(code);

            await Task.Delay(3_500);

            if (await _mem.ExistsElement("//node[@resource-id='android:id/message']"))
                return false; //To-Do

            if (await _mem.ExistsElement("//node[@resource-id='android:id/aerr_restart']"))
            {
                await _mem.Click("//node[@resource-id='android:id/aerr_restart']");
                return false;
            }

            if (await _mem.ExistsElement("//node[@text='Введите своё имя']"))
            {
                await _mem.Input("//node[@text='Введите своё имя']", "Valeron3000");
                await _mem.Click("//node[@text='ДАЛЕЕ']");
            }
            else if (await _mem.ExistsElement("//node[@text='Название компании']"))
            {
                await _mem.Input("//node[@text='Название компании']", MemuLib.Globals.RandomString(new Random().Next(5, 10), true).ToLower());
                await _mem.Click("//node[@text='Вид деятельности']");
                await _mem.Click("//node[@text='Одежда']");
                await _mem.Click("//node[@text='ДАЛЕЕ']");
            }

            if (await _mem.ExistsElement("//node[@resource-id='android:id/aerr_restart']"))
            {
                await _mem.Click("//node[@resource-id='android:id/aerr_restart']");
                return false;
            }

            var count = 0;

            while (await _mem.ExistsElement("//node[@text='Инициализация…']") || await _mem.ExistsElement("//node[@text='ОК']") || await _mem.ExistsElement("//node[@text='OK']"))
            {
                ++count;
                await Task.Delay(1_500);
                if (count > 5)
                    return false;
            }

            await obj.SetStatus(true);

            Directory.CreateDirectory($@"{Globals.Setup.PathToDirectoryAccounts}\{codeCountry}{phone}");
            Directory.CreateDirectory($@"{Globals.Setup.PathToDirectoryAccounts}\{codeCountry}{phone}\{PackageName}");
            
            await _mem.Pull($@"{Globals.Setup.PathToDirectoryAccounts}\{codeCountry}{phone}\{PackageName}\databases\", $"/data/data/{PackageName}/databases");
            await _mem.Pull($@"{Globals.Setup.PathToDirectoryAccounts}\{codeCountry}{phone}\{PackageName}\files\", $"/data/data/{PackageName}/files");
            await _mem.Pull($@"{Globals.Setup.PathToDirectoryAccounts}\{codeCountry}{phone}\{PackageName}\shared_prefs\", $"/data/data/{PackageName}/shared_prefs");
            
            await File.WriteAllTextAsync($@"{Globals.Setup.PathToDirectoryAccounts}\{codeCountry}{phone}\Data.json", JsonConvert.SerializeObject(new AccountData()));

            Account = string.Empty;

            Phone = phone;

            return true;
        }
        catch (Exception ex)
        {
            Log.Write(ex.Message);
            return false;
        }
        /*await _mem.StopApk(PackageName);
        await _mem.Shell($"pm clear {PackageName}");
        await _mem.RunApk(PackageName);

        await _mem.Click("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']");

        await _mem.ClearInput("//node[@resource-id='com.whatsapp.w4b:id/registration_cc']");
        await _mem.Input("//node[@resource-id='com.whatsapp.w4b:id/registration_cc']", "232");

        var service = await OnlineSim.Create("232", "WhatsApp");
        
        if (service is null)
            return false;

        var phone = (await service!.GetMessage())["number"]!.ToString().Remove(0, 4);

        await _mem.ClearInput("//node[@resource-id='com.whatsapp.w4b:id/registration_phone']");
        await _mem.Input("//node[@resource-id='com.whatsapp.w4b:id/registration_phone']", phone);
        await _mem.Click("//node[@resource-id='com.whatsapp.w4b:id/registration_submit']");
        await _mem.Click("//node[@text='OK']");

        await Task.Delay(3_500);
        var dump = _mem.DumpScreen();

        if (!await _mem.ExistsElement("//node[@resource-id='com.whatsapp.w4b:id/verify_sms_code_input']", dump, false) && dump != null)
            goto repeat;

        var countTryToGetCode = 0;
        var code = string.Empty;

        while (countTryToGetCode++ < 70)
        {
            var result = await service!.GetMessage();
            if (result["response"]!.ToString() != "TZ_NUM_ANSWER")
            {
                await Task.Delay(1_000);
                continue;
            }

            code = result["msg"]!.ToString();
        }

        if (string.IsNullOrEmpty(code))
            goto repeat;

        await _mem.Input(code);

        MessageBox.Show((await IsValid()).ToString());
        return true;*/
    }

    async Task<bool> IsValid()
    {
        var dump = _mem.DumpScreen();

        return !await _mem.ExistsElement("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']", dump, false) &&
               !await _mem.ExistsElement("//node[@text='ДАЛЕЕ']", dump, false) &&
               !await _mem.ExistsElement("//node[@text='Перезапустить приложение']", dump, false) &&
               !await _mem.ExistsElement("//node[@text='Закрыть приложение']", dump, false) &&
               !await _mem.ExistsElement("//node[@content-desc='Неверный номер?']", dump, false) &&
               !await _mem.ExistsElement("//node[@text='ЗАПРОСИТЬ РАССМОТРЕНИЕ']", dump, false) &&
               !await _mem.ExistsElement("//node[@text='WA Business']", dump, false) &&
               !await _mem.ExistsElement("//node[@text='WhatsApp']", dump, false) &&
               !await _mem.ExistsElement("//node[@resource-id='android:id/progress']", dump, false) &&
               !await _mem.ExistsElement("//node[@text='ПОДТВЕРДИТЬ']", dump, false);
    }

    public async Task<bool> LoginFile([Optional] string path, [Optional] string name)
    {
        try
        {
            await _mem.Shell($"pm clear {PackageName}");
            await _mem.RunApk(PackageName);
            await _mem.StopApk(PackageName);
            await _mem.Push($@"{(Account == string.Empty ? path : Account)}\{PackageName}\.", @$"/data/data/{PackageName}/");
            //await _mem.Shell($"rm /data/data/{PackageName}/databases/*msgstore*");
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
            if (!await _mem.ExistsElement($"//node[@resource-id='{PackageName}:id/registration_name']"))
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
                return true;

            await _mem.Click("//node[@text='НЕ СЕЙЧАС']");
            await Task.Delay(2_000);
            await _mem.StopApk(PackageName);
            await _mem.RunApk(PackageName);
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