var tasks = new List<Task>();
for (var i = 0; i < 100; i++)
{
    var _i = i;
    tasks.Add(Task.Run(async () => Console.WriteLine($"{_i} {await test()}")));
}

Task.WaitAll(tasks.ToArray(), -1);

async Task<string> test() => await File.ReadAllTextAsync(@"C:\Users\artem\source\repos\MemuExplorer\MemuTest\bin\Debug\f1.txt");
