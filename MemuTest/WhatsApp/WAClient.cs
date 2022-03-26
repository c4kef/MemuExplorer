using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using MemuLib.Core;

namespace MemuTest.WhatsApp;

public class WAClient
{
    private string _phone;
    private string _account;
    private readonly Client _mem;

    public WAClient(string phone, string account = "", int deviceId = -1)
    {
        _phone = phone;
        _account = account;

        //To-Do fix client 0
        _mem = (deviceId == -1) ? new Client(0) : new Client(deviceId);
    }

    public async Task Start()
    {
        await _mem.Start();
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
        _account = string.Empty;
        
        _phone = obj.Phone;

        return obj.Phone;
    }

    public async Task LoginFile([Optional] string path)
    {
        await _mem.RunApk("com.whatsapp");
        await _mem.StopApk("com.whatsapp");
        await _mem.Push($@"{((_account == string.Empty) ? path : _account)}\.", @"/data/data/com.whatsapp");
        await _mem.RunApk("com.whatsapp");

        if (!await _mem.ExistsElement("//node[@text='Выберите частоту резервного копирования']"))
            return;

        await _mem.Click("//node[@text='Выберите частоту резервного копирования']");
        await _mem.Click("//node[@text='Никогда']");
        await _mem.Click("//node[@text='ГОТОВО']");
        await _mem.StopApk("com.whatsapp");
        await _mem.RunApk("com.whatsapp");
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
}