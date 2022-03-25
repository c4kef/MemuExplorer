using System.Diagnostics;
using System.Drawing;
using WebSocketSharp;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using MemuLib;
using MemuLib.Core;
using SocketIO.Client;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MemuLib.Core.Contacts;
using MemuTest.WhatsApp;
using Newtonsoft.Json;

var c = new WAClient("c4ke");
await c.Init();

Globals.IsLog = true;

var mem = new Client(await Memu.Create());
await mem.Start();

foreach (var file in Directory.GetFiles(""))
    if (file.Contains("com."))
        await mem.InstallApk(file);

var num = await Register(@"D:\Account");

var contacts = new List<CObj>();

contacts.Add(new CObj("C4ke", num));

File.WriteAllText(ContactManager.Export(contacts), @"D:\contact.vcf");

await mem.ImportContacts(@"D:\contact.vcf");

await LoginFile(mem, @"D:\Account");

await c.SendText(num, "Hello world!");

async Task<string> Register(string to)
{
    Memu.RunAdbServer();
    var mem = new Client(0);
    await mem.Start();

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

    await mem.Pull(to, "/data/data/com.whatsapp/");

    return obj.Phone;
}

async Task LoginFile(Client mem, string path)
{
    await mem.RunApk("com.whatsapp");
    await mem.StopApk("com.whatsapp");
    await mem.Push($@"{path}\.", @"/data/data/com.whatsapp");
    await mem.RunApk("com.whatsapp");
    await mem.Click("//node[@text='Выберите частоту резервного копирования']");
    await mem.Click("//node[@text='Никогда']");
    await mem.Click("//node[@text='ГОТОВО']");
    await mem.Click("//node[@content-desc='Ещё']");
    await mem.Click("//node[@text='Связанные устройства']");
    await mem.Click("//node[@text='ОК']");
    await mem.Click("//node[@text='ПРИВЯЗКА УСТРОЙСТВА']");
    await mem.Click("//node[@text='OK']");
}