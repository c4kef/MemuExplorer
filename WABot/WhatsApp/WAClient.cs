﻿using MemuLib.Core.SimServices;

namespace WABot.WhatsApp;

public class WaClient
{
    private Client _mem;
    public string Phone { private set; get; }
    public string Account { private set; get; }
    public AccountData AccountData;

    public WaClient(string phone = "", string account = "", int deviceId = -1)
    {
        Phone = phone;
        Account = account;

        AccountData = new AccountData();

        if (account != string.Empty)
            AccountData = JsonConvert.DeserializeObject<AccountData>(File.ReadAllText($@"{account}\Data.json"))!;

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

    public async Task<string> Register(string to, string name)
    {
        await _mem.StopApk("com.whatsapp");
        await _mem.Shell("pm clear com.whatsapp");
        await _mem.RunApk("com.whatsapp");


        if (!await _mem.ExistsElement("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']"))
            return string.Empty;

        await _mem.Click("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']");

        var obj = await SmsCode.Create(service: "wa", country: "0");

        if (obj is null)
            return "stop";

        if (!await _mem.ExistsElement("//node[@text='номер тел.']"))
            return string.Empty;

        //await _mem.ClearInput("//node[@resource-id='com.whatsapp:id/registration_cc']");
        //await _mem.Input("//node[@resource-id='com.whatsapp:id/registration_cc']", obj.Phone[0].ToString());
        
        await _mem.Click("//node[@resource-id='com.whatsapp:id/registration_country']");
        await _mem.Click("//node[@resource-id='com.whatsapp:id/menuitem_search']");
        await _mem.Input("//node[@resource-id='com.whatsapp:id/search_src_text']", "Canada");//text="Канада"
        await _mem.Click("//node[@text='Канада']");

        await _mem.Input("//node[@text='номер тел.']", obj.Phone.Remove(0, 1));

        await _mem.Click("//node[@text='ДАЛЕЕ']");

        if (!await _mem.ExistsElement("//node[@text='OK']"))
        {
            obj.Cancel();
            return string.Empty;
        }

        await _mem.Click("//node[@text='OK']");

        if (await _mem.ExistsElement("//node[@resource-id='android:id/message']"))
        {
            obj.Cancel();
            return string.Empty;
        }

        if (await _mem.ExistsElement("//node[@resource-id='android:id/aerr_restart']"))
        {
            await _mem.Click("//node[@resource-id='android:id/aerr_restart']");
            return string.Empty;
        }

        var count = 0;

        while (await obj.GetMessage() == string.Empty)
        {
            ++count;
            Thread.Sleep(1_500);
            if (count <= 15) continue;
            obj.Cancel();
            return string.Empty;
        }

        var code = await obj
            .GetMessage(); /*new string(new Regex(@"\b\d{3}\-\d{3}\b").Match(await obj.GetMessage()).Value.Where(char.IsDigit)
            .ToArray());*/

        //await _mem.Input("//node[@text='––– –––']", code); - нихуя не работает из-за текста который меняется...
        await _mem.Input(code); //Костыль, иначе не придумал как можно

        if (await _mem.ExistsElement("//node[@resource-id='android:id/message']"))
            return string.Empty; //To-Do

        if (await _mem.ExistsElement("//node[@resource-id='android:id/aerr_restart']"))
        {
            await _mem.Click("//node[@resource-id='android:id/aerr_restart']");
            return string.Empty;
        }

        await _mem.Input("//node[@text='Введите своё имя']", name.Replace(' ', 'I'));
        await _mem.Click("//node[@text='ДАЛЕЕ']");

        if (await _mem.ExistsElement("//node[@resource-id='android:id/aerr_restart']"))
        {
            await _mem.Click("//node[@resource-id='android:id/aerr_restart']");
            return string.Empty;
        }

        count = 0;

        while (await _mem.ExistsElement("//node[@text='Инициализация…']"))
        {
            ++count;
            Thread.Sleep(1_500);
            if (count > 5)
                return string.Empty;
        }

        await _mem.Pull(to, "/data/data/com.whatsapp/");

        Account = string.Empty;

        Phone = obj.Phone;

        return obj.Phone.Remove(0, 1);
    }

    public async Task LoginFile([Optional] string path, [Optional] string name)
    {
        await _mem.Shell("pm clear com.whatsapp");
        await _mem.RunApk("com.whatsapp");
        await _mem.StopApk("com.whatsapp");
        await _mem.Push($@"{(Account == string.Empty ? path : Account)}\com.whatsapp\.", @"/data/data/com.whatsapp");
        await _mem.Shell("rm -r /data/data/com.whatsapp/databases");
        await _mem.RunApk("com.whatsapp");

        if (!await _mem.ExistsElement("//node[@text='Выберите частоту резервного копирования']"))
            goto s1;

        await _mem.Click("//node[@text='Выберите частоту резервного копирования']");
        await _mem.Click("//node[@text='Никогда']");
        await _mem.Click("//node[@text='ГОТОВО']");
        await Task.Delay(2_000);
        await _mem.StopApk("com.whatsapp");
        await _mem.RunApk("com.whatsapp");

        s1:
        if (!await _mem.ExistsElement("//node[@resource-id='com.whatsapp:id/registration_name']"))
            return;

        await _mem.Input("//node[@text='Введите своё имя']", name.Replace(' ', 'I'));
        await _mem.Click("//node[@text='ДАЛЕЕ']");
        await Task.Delay(2_000);
        await _mem.StopApk("com.whatsapp");
        await _mem.RunApk("com.whatsapp");
    }

    public async Task<bool> ImportContacts(string path)
    {
        await _mem.ClearContacts();
        await _mem.ImportContacts(path);
        if (!await _mem.ExistsElement("//node[@text='ОК']"))
            return false;

        await _mem.Click("//node[@text='ОК']");

        await _mem.StopApk("com.whatsapp");
        await _mem.RunApk("com.whatsapp");

        return true;
    }

    public async Task SendMessage(string to, string text)
    {
        //await _mem.Shell($@"am start -a android.intent.action.VIEW -d https://wa.me/{to}/?text={Uri.EscapeDataString(text)}");
        var command = new FileInfo($"{to}.sh");

        await File.WriteAllTextAsync(command.FullName,
            $"am start -a android.intent.action.VIEW -d https://wa.me/{to}/?text={Uri.EscapeDataString(text)}");

        await _mem.Push(command.FullName, "/data/local/tmp");
        await _mem.Shell($@"sh /data/local/tmp/{to}.sh");

        for (var i = 0; i < 3; i++)
        {
            if (!await _mem.ExistsElement("//node[@content-desc='Отправить']"))
            {
                await Task.Delay(1_500);
                continue;
            }

            await _mem.Click("//node[@content-desc='Отправить']");
            break;
        }

        await _mem.Shell($"rm /data/local/tmp/{to}.sh");
        File.Delete(command.FullName);
    }

    public async Task ReCreate([Optional] string? phone, [Optional] string? account, [Optional] int? deviceId)
    {
        if (deviceId is not null)
        {
            await _mem.Stop();
            _mem = new Client((int) deviceId);
        }

        if (account is not null)
        {
            Account = account;
            AccountData =
                JsonConvert.DeserializeObject<AccountData>(await File.ReadAllTextAsync($@"{account}\Data.json"))!;
        }

        if (phone is not null)
            Phone = phone;
    }

    public async Task UpdateData()
    {
        await File.WriteAllTextAsync($@"{Account}\Data.json", JsonConvert.SerializeObject(AccountData));
    }
}