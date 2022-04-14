using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using MemuLib;
using MemuLib.Core;
using Newtonsoft.Json;
using SocketIO.Client;

foreach (var dirPath in Directory.GetDirectories(@"C:\Users\artem\source\repos\MVP\MemuExplorer\Data\Accounts"))
{
    if (!Directory.Exists(@$"{dirPath}\com.whatsapp"))
    {
        Directory.Delete(dirPath);
        continue;
    }
    
    await File.WriteAllTextAsync($@"{dirPath}\Data.json",
        JsonConvert.SerializeObject(new AccountData()
            {LastActiveDialog = new Dictionary<string, DateTime>(), TrustLevelAccount = 0}));
}

[Serializable]
public class AccountData
{
    /// <summary>
    /// Уровень прогрева аккаунта для начала рассылки сообщений
    /// </summary>
    public int TrustLevelAccount = 0;
    /// <summary>
    /// Последняя переписка с аккаунтом
    /// </summary>
    public Dictionary<string, DateTime>? LastActiveDialog;
}

/*
var cls = new List<WAClient>();
var tsks = new List<Task>();
var contacts = new List<CObj>
{
    new CObj($"Artemiy 1", "+12367056432"),
    new CObj($"Artemiy 2", "+12367008836"),
    new CObj($"Artemiy 3", "+14313038685")
};

await File.WriteAllTextAsync(@"D:\contact.vcf", ContactManager.Export(contacts));

for (var i = 1; i < 4; i++)
{
    var client = new WAClient("", deviceId: i);
    await client.Start();
    cls.Add(client);
}


for (var i = 0; i < cls.Count; i++)
{
    var i1 = i;
    tsks.Add(Task.Run(async () =>
    {
        await cls[i1].ImportContacts(@"D:\contact.vcf");
        await cls[i1].LoginFile(@$"D:\Build\accs\{((i1 == 0) ? "12367056432" : (i1 == 1) ? "12367008836" : "14313038685")}\com.whatsapp");
    }));
}

Task.WaitAll(tsks.ToArray(), -1);

await cls[0].SendMessage("+12367008836", "Hello Artemiy!");
await cls[0].SendMessage("+14313038685", "Hello Artemiy!");

await cls[1].SendMessage("+12367056432", "Hello Artemiy!");
await cls[1].SendMessage("+14313038685", "Hello Artemiy!");

await cls[2].SendMessage("+12367008836", "Hello Artemiy!");
await cls[2].SendMessage("+12367056432", "Hello Artemiy!");

return;
/*
await mema.ImportContacts(@"D:\contact.vcf");
await mema.Click("//node[@text='ОК']");
await LoginFile(mema, @"C:\Users\artem\source\repos\MVP\MemuExplorer\Accounts\blabla");
await mema.Shell($@"am start -a android.intent.action.VIEW -d https://wa.me/{phone}/?text={Uri.EscapeDataString("Hello world!")}");
var i = 0;
while (i < 3)
{
    if (!await mema.ExistsElement("//node[@content-desc='Отправить']"))
    {
        i++;
        await Task.Delay(1_500);
        continue;
    }

    await mema.Click("//node[@content-desc='Отправить']");
}

/*
Thread.Sleep(500);

IntPtr id = flash.MainWindowHandle;
Console.Write(id);
WAClient.MoveWindow(flash.MainWindowHandle, 0, 0, 500, 500, true);
return;
Globals.IsLog = true;

var mems = new List<Client>();
var tasks = new List<Task>();
var contacts = new List<CObj>();

for (var i = 0; i < 2; i++)
{
    var mem = new Client(0);
    await mem.Start();

    foreach (var file in Directory.GetFiles(""))
        if (file.Contains("com."))
            await mem.InstallApk(file);

    var num = await Register(@"D:\Account");

    contacts.Add(new CObj($"C4ke_{i}", num));
    mems.Add(mem);
}

File.WriteAllText(ContactManager.Export(contacts), @"D:\contact.vcf");

/*foreach (var VARIABLE in COLLECTION)
{
    
}
await mem.ImportContacts(@"D:\contact.vcf");
await LoginFile(mem, @"C:\Users\artem\source\repos\MVP\MemuExplorer\Accounts\blabla");

var c = new WAClient("c4ke");
await c.Init();

await c.SendText("+79772801086", "Hello world!");

Task.WaitAll()
*/