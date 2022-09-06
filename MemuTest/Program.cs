using Newtonsoft.Json;
using System.Security.Principal;

var dirFrom = Directory.GetDirectories(Console.ReadLine()!);//Откуда берем оригинал
var dirAllTmp = Directory.GetDirectories(Console.ReadLine()!).ToList();//Общая папка акков
var dirAll = new List<TempData>();
var dirTo = Directory.CreateDirectory("SortAccounts");

foreach (var dirs in dirAllTmp)
{
    Console.WriteLine($@"{dirs}\Data.json");
    dirAll.Add(new TempData() { pathDir = dirs, account = JsonConvert.DeserializeObject<AccountData>(File.ReadAllText($@"{dirs}\Data.json"))! });
}

dirAll.RemoveAll(accountDir => !dirFrom.Select(accountDirFrom => new DirectoryInfo(accountDirFrom).Name).Contains(new DirectoryInfo(accountDir.pathDir).Name));
//dirAll.RemoveAll(accounts => accounts.account.TrustLevelAccount < 3);
dirAll.RemoveAll(accounts => !Directory.GetFiles(accounts.pathDir).Any(file => file.Contains("data.json")));

foreach (var account in dirAll)
{
    var countTry = 0;
    while (countTry++ < 3)
    {
        try
        {
            Directory.Move(account.pathDir, $@"{dirTo.FullName}\{new DirectoryInfo(account.pathDir).Name}");
            break;
        }
        catch (Exception ex)
        {
        }
        await Task.Delay(1_000);
    }
}

public struct TempData
{
    public string pathDir;
    public AccountData account;
}

[Serializable]
public class AccountData
{
    /// <summary>
    /// Уровень прогрева аккаунта для начала рассылки сообщений
    /// </summary>
    public int TrustLevelAccount = 0;

    /// <summary>
    /// Кол-во сообщений
    /// </summary>
    public int CountMessages = 0;

    /// <summary>
    /// Дата создания
    /// </summary>
    public DateTime CreatedDate = DateTime.Now;

    /// <summary>
    /// Аккаунт первый начал переписку?
    /// </summary>
    public bool FirstMsg = false;
}

/*var dirNew = Directory.CreateDirectory("NotScanned");

foreach (var dirs in Directory.GetDirectories(Console.ReadLine()!))
{
    if (!Directory.GetFiles(dirs).Any(file => file.Contains("data.json")))
        if (Directory.Exists($@"{dirNew}/{new DirectoryInfo(dirs).Name}"))
            Directory.Delete(dirs, true);
        else
            Directory.Move(dirs, $@"{dirNew}/{new DirectoryInfo(dirs).Name}");
}*/