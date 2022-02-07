List<Task> tasks = new List<Task>();
for (int i = 1; i < 2; i++)
{
    tasks.Add(Task.Run(async () =>
    {
        Client memu = new Client(0);
        await memu.Start();
        foreach (string file in Directory.GetFiles(Settings.AppsDir))
            await memu.InstallApk(file);

        await memu.RunApk("com.whatsapp");
        await memu.Click(665, 70);
        await memu.Click(532, 201);
        await memu.Click(349, 457);
    }));
}
Task.WaitAll(tasks.ToArray(), -1);
Console.Read();