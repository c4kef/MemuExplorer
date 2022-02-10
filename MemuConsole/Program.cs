var tasks = new List<Task>();
for (var i = 1; i < 2; i++)
{
    var safeIndex = i;
    tasks.Add(Task.Run(async () =>
    {
        var mem = new Client(safeIndex);
        await mem.Start();

        foreach (var file in Directory.GetFiles(Settings.AppsDir))
            await mem.InstallApk(file);

        await mem.RunApk("com.whatsapp");
        await mem.Click(665, 70);
        await mem.Click(532, 201);
        await mem.Click(349, 457);
    }));
}
Task.WaitAll(tasks.ToArray(), -1);
