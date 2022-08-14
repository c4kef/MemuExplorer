Console.Write("-> Input path to second accounts: ");
var pathToSecondAccounts = Console.ReadLine()!;
Console.Write("-> Input path to accounts: ");
var pathToAccounts = Console.ReadLine()!;
Console.Write("-> 1 to remove duplicates and 2 to remove similar names: ");
var choose = int.Parse(Console.ReadLine()!);
switch(choose)
{
    case 1:
        {
            var firstAccounts = Directory.GetDirectories(pathToAccounts).Select(directory => new DirectoryInfo(directory)).ToArray();
            var secondAccounts = Directory.GetDirectories(pathToSecondAccounts).Select(directory => new DirectoryInfo(directory)).ToArray();
            foreach (var account in firstAccounts)
                if (secondAccounts.Any(directory => directory.Name == account.Name))
                {
                    Directory.Delete(account.FullName, true);
                    if (File.Exists($@"{account.FullName}.data.json"))
                        File.Delete($@"{account.FullName}.data.json");
                }
            break;
        }
    case 2:
        {
            var accountsScanned = Directory.GetDirectories(pathToSecondAccounts).Select(directory => new DirectoryInfo(directory)).ToArray();
            var currentAccounts = Directory.GetDirectories(pathToAccounts).Select(directory => new DirectoryInfo(directory)).ToArray();
            foreach (var account in currentAccounts)
                if (!accountsScanned.Any(directory => directory.Name == account.Name))
                    Directory.Delete(account.FullName, true);
            break;
        }
}

Console.WriteLine("Successful!");
Console.ReadLine();