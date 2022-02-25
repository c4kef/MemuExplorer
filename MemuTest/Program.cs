using System.Drawing;
using System.Text.RegularExpressions;
using MemuLib;
using MemuLib.Core;

Globals.IsLog = true;

Memu.RunAdbServer();
var mem = new Client(0);
await mem.Start();
Console.WriteLine(await WebReq.HttpGet($"https://5sim.net/v1/user/orders?category=activation"));

again:
await mem.StopApk("com.whatsapp");
await mem.Shell("pm clear com.whatsapp");
await mem.RunApk("com.whatsapp");

await mem.Click("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']");

var obj = await FsService.Create(service: "whatsapp", country: "russia");

await mem.Input("//node[@text='номер тел.']", obj.Phone.Remove(0, 2));

Console.WriteLine($"Number: {obj.Phone}");

await mem.Click("//node[@text='ДАЛЕЕ']");

await mem.Click("//node[@text='OK']");

if (await mem.ExistsElement("//node[@resource-id='android:id/message']"))
{
    obj.Cancel();
    goto again;
}

var _count = 0;

while (await obj.GetMessage() == string.Empty)
{
    ++_count;
    Thread.Sleep(1_500);
    if (_count > 15)
    {
        obj.Cancel();
        goto again;
    }
}

var code = new string(new Regex(@"\b\d{3}\-\d{3}\b").Match(await obj.GetMessage()).Value.Where(char.IsDigit).ToArray());

await mem.Input("//node[@text='––– –––']", code);

Console.WriteLine(code);

if (await mem.ExistsElement("//node[@resource-id='android:id/message']"))
    goto again;//To-Do

await mem.Input("//node[@text='Введите своё имя']", "Тамара");
await mem.Click("//node[@text='ДАЛЕЕ']");

_count = 0;

while (await mem.ExistsElement("//node[@text='Инициализация…']"))
{
    ++_count;
    Thread.Sleep(1_500);
    if (_count > 5)
        goto again;
}

await mem.Pull(@"D:\test", "/data/data/com.whatsapp/");


/*//Auth
        var chrome = new Chrome();
        chrome.SetSize(new Point(200, 1080));
        chrome.SetPosition(new Point(0, 0));
        chrome.Start();

        var mem = new Client(await Memu.Create());
        await mem.Start();

        foreach (var file in Directory.GetFiles(Settings.AppsDir))
            if (file.Contains("com."))
                await mem.InstallApk(file);

        await mem.RunApk("com.whatsapp");
        await mem.StopApk("com.whatsapp");
        await mem.Push($@"{Settings.AccsDir}\blabla\.", @"/data/data/com.whatsapp");
        await mem.RunApk("com.whatsapp");
        await mem.Click("//node[@text='Выберите частоту резервного копирования']");
        await mem.Click("//node[@text='Никогда']");
        await mem.Click("//node[@text='ГОТОВО']");
        await mem.Click("//node[@content-desc='Ещё']");
        await mem.Click("//node[@text='Связанные устройства']");
        await mem.Click("//node[@text='ОК']");
        await mem.Click("//node[@text='ПРИВЯЗКА УСТРОЙСТВА']");
        await mem.Click("//node[@text='OK']");
*/