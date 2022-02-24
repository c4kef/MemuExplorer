using System.Drawing;
using MemuLib;
using MemuLib.Core;

Globals.IsLog = true;

var test = await WebReq.HttpGet("https://5sim.net/v1/user/profile");
Console.WriteLine(test);
return;
Memu.RunAdbServer();
var mem = new Client(0);
await mem.Start();
await mem.Click("//node[@text='ПРИНЯТЬ И ПРОДОЛЖИТЬ']");

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