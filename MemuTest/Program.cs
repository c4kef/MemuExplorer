var arr = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

foreach (var r in arr.OrderBy(x => new Random().Next()).ToArray())
    Console.WriteLine(r);