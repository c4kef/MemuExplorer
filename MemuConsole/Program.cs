var tasks = new List<Task>();

Memu.RunAdbServer();

tasks.Add(Task.Run(async () =>
{
    var mem = new Client(await Memu.Create());
    await mem.Start();

    foreach (var file in Directory.GetFiles(Settings.AppsDir))
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
    await mem.Click("//node[@text='ОК']");
}));

Task.WaitAll(tasks.ToArray(), -1);