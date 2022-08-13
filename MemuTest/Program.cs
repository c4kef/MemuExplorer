Console.Write("-> Input path to second accounts: ");
var pathToSecondAccounts = Console.ReadLine()!;
Console.Write("-> Input path to accounts: ");
var pathToAccounts = Console.ReadLine()!;
var accountsScanned = Directory.GetDirectories(pathToSecondAccounts).Select(directory => new DirectoryInfo(directory)).ToArray();
var currentAccounts = Directory.GetDirectories(pathToAccounts).Select(directory => new DirectoryInfo(directory)).ToArray();
foreach (var account in currentAccounts)
    if (!accountsScanned.Any(directory => directory.Name == account.Name))
        Directory.Delete(account.FullName, true);

Console.WriteLine("Successful!");
Console.ReadLine();     