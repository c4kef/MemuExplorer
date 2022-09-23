int x = 0;
// запускаем пять потоков
var list = new List<Task>();
object lockerr = new object();
bool thradsReady = false;

for (int i = 1; i < 6; i++)
{
    var r =await Task.Factory.StartNew(async () => await Print(lockerr));
    list.Add(r);
}

thradsReady = true;
Task.WaitAll(list.ToArray());
thradsReady = false;
Console.WriteLine($"Finally: {thradsReady}");


async Task Print(object locker)
{
    lock (locker)
    {
        x = 1;
        Console.WriteLine($"Thread: {thradsReady}");
        for (int i = 1; i < 6; i++)
        {
            Console.WriteLine($" {x}");
            x++;
        }
    }
}