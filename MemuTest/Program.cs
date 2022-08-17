var arr = new List<string>();
arr.AddRange(await File.ReadAllLinesAsync(Console.ReadLine()!));
arr.AddRange(await File.ReadAllLinesAsync(Console.ReadLine()!));

await File.WriteAllLinesAsync("checked.txt", arr.Distinct());