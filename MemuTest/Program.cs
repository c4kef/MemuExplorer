using System.Linq;

var removed = Directory.GetDirectories(Console.ReadLine()!).Select(dir => new DirectoryInfo(dir));
var newAcc = Directory.GetDirectories(Console.ReadLine()!).Select(dir => new DirectoryInfo(dir));
for (var i = 0; i < newAcc.Count(); i++)
{
    if (removed.Any(acc => acc.Name == newAcc.ToArray()[i].Name))
        Directory.Delete(newAcc.ToArray()[i].FullName, true);
}
Console.WriteLine("OK");