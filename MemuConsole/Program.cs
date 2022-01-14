
return;
List<Task> tasks = new List<Task>();
for (int i = 1; i < 3; i++)
{
    tasks.Add(Task.Run(async () =>
    {
        Client memu = new Client(await Memu.Create());
        await memu.Start();
        foreach (string file in Directory.GetFiles(Settings.AppsDir))
            await memu.InstallApk(file);



        await memu.RunApk("com.whatsapp");
    }));
}
Task.WaitAll(tasks.ToArray(), -1);
Console.Read();