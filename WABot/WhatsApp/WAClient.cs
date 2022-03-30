namespace WABot.WhatsApp;

public class WAClient
{
    private Client _mem;
    private int temp;
    public string Phone { private set; get; }
    public string Account { private set; get; }
    public AccountData AccountData;

    public WAClient(string phone = "", string account = "", int deviceId = -1)
    {
        if (phone[0] != '+')
            throw new Exception("Not correct format number phone");
        
        Phone = phone;
        Account = account;
        AccountData = JsonConvert.DeserializeObject<AccountData>(File.ReadAllText($@"{account}\Data.json"))!;
        
        //To-Do fix client 0
        _mem = (deviceId == -1) ? new Client(0) : new Client(deviceId);
        temp = deviceId;
    }

    public async Task Start()
    {
        await _mem.Start();
    }
    
    public async Task Stop()
    {
        await _mem.Stop();
    }

    public async Task<string> Register(string to)
    {
        again:
        await _mem.StopApk("com.whatsapp");
        await _mem.Shell("pm clear com.whatsapp");
        await _mem.RunApk("com.whatsapp");

        await _mem.Click("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']");

        var obj = await FsService.Create(service: "whatsapp", country: "russia");

        await _mem.Input("//node[@text='номер тел.']", obj.Phone.Remove(0, 2));

        Console.WriteLine($"Number: {obj.Phone}");

        await _mem.Click("//node[@text='ДАЛЕЕ']");

        await _mem.Click("//node[@text='OK']");

        if (await _mem.ExistsElement("//node[@resource-id='android:id/message']"))
        {
            obj.Cancel();
            goto again;
        }

        var count = 0;

        while (await obj.GetMessage() == string.Empty)
        {
            ++count;
            Thread.Sleep(1_500);
            if (count > 15)
            {
                obj.Cancel();
                goto again;
            }
        }

        var code = new string(new Regex(@"\b\d{3}\-\d{3}\b").Match(await obj.GetMessage()).Value.Where(char.IsDigit)
            .ToArray());

        await _mem.Input("//node[@text='––– –––']", code);

        if (await _mem.ExistsElement("//node[@resource-id='android:id/message']"))
            goto again; //To-Do

        await _mem.Input("//node[@text='Введите своё имя']", "Тамара");
        await _mem.Click("//node[@text='ДАЛЕЕ']");

        count = 0;

        while (await _mem.ExistsElement("//node[@text='Инициализация…']"))
        {
            ++count;
            Thread.Sleep(1_500);
            if (count > 5)
                goto again;
        }

        await _mem.Pull(to, "/data/data/com.whatsapp/");

        //To-Do get path to directory account and set that
        Account = string.Empty;

        Phone = obj.Phone;

        return obj.Phone;
    }

    public async Task<bool> LoginFile([Optional] string path)
    {
        await _mem.RunApk("com.whatsapp");
        await _mem.StopApk("com.whatsapp");
        await _mem.Push($@"{((Account == string.Empty) ? path : Account)}\com.whatsapp\.", @"/data/data/com.whatsapp");
        await _mem.RunApk("com.whatsapp");

        /*if (!await _mem.ExistsElement("//node[@text='Выберите частоту резервного копирования']"))
            goto end;

        await _mem.Click("//node[@text='Выберите частоту резервного копирования']");
        await _mem.Click("//node[@text='Никогда']");
        await _mem.Click("//node[@text='ГОТОВО']");
        await _mem.StopApk("com.whatsapp");
        await _mem.RunApk("com.whatsapp");
        
        end:*/
        return !await _mem.ExistsElement("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']") && !await _mem.ExistsElement("//node[@text='ДАЛЕЕ']") && !await _mem.ExistsElement("//node[@resource-id='android:id/message']");

        /*await _mem.Click("//node[@content-desc='Ещё']");
        await _mem.Click("//node[@text='Связанные устройства']");
        await _mem.Click("//node[@text='ОК']");
        await _mem.Click("//node[@text='ПРИВЯЗКА УСТРОЙСТВА']");
        await _mem.Click("//node[@text='OK']");*/
    }

    public async Task ImportContacts(string path)
    {
        await _mem.ImportContacts(path);
        await _mem.Click("//node[@text='ОК']");
    }

    public async Task SendMessage(string to, string text)
    {
        await _mem.Shell($@"am start -a android.intent.action.VIEW -d https://wa.me/{to}/?text={Uri.EscapeDataString(text)}");
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
    }

    public async Task ReCreate([Optional] string? phone, [Optional] string? account, [Optional] int? deviceId)
    {
        await _mem.Stop();
        
        if (deviceId is not null)
            _mem = new Client((int)deviceId);

        if (account is not null)
        {
            Account = account;
            AccountData = JsonConvert.DeserializeObject<AccountData>(await File.ReadAllTextAsync($@"{account}\Data.json"))!;
        }

        if (phone is not null)
            Phone = phone;
    }

    public async Task UpdateData() => await File.WriteAllTextAsync($@"{Account}\Data.json", JsonConvert.SerializeObject(AccountData));
}