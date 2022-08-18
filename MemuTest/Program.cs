var r1 = await File.ReadAllLinesAsync(Console.ReadLine()!);
var r2 = await File.ReadAllLinesAsync(Console.ReadLine()!);

foreach (var r in r1)
    if (r2.Contains(r))
        Console.WriteLine(r);