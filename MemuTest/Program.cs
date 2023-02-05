using Newtonsoft.Json;
using System.Runtime.CompilerServices;

Console.Write("-> Input Trust Level: ");
var trustLevel = int.Parse(Console.ReadLine());
Console.Write("-> Input Path To Accounts: ");
var dir = Console.ReadLine();
DirectoryInfo directory1 = Directory.CreateDirectory("UpperTrustLevel");
foreach (string directory2 in Directory.GetDirectories(dir))
{
    if (File.Exists(directory2 + "\\Data.json"))
    {
        string name = new DirectoryInfo(directory2).Name;
        if (JsonConvert.DeserializeObject<AccountData>(File.ReadAllText(directory2 + "\\Data.json")).TrustLevelAccount > trustLevel)
        {
            string sourceDirName = directory2;
            DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
            interpolatedStringHandler.AppendFormatted<DirectoryInfo>(directory1);
            interpolatedStringHandler.AppendLiteral("\\");
            interpolatedStringHandler.AppendFormatted(name);
            string stringAndClear = interpolatedStringHandler.ToStringAndClear();
            Directory.Move(sourceDirName, stringAndClear);
        }
    }
}

[Serializable]
public class AccountData
{
    public int TrustLevelAccount = 0;
    public int CountMessages = 0;
    public DateTime CreatedDate = DateTime.Now;
    public DateTime? BannedDate;
    public Dictionary<string, DateTime> MessageHistory = new Dictionary<string, DateTime>();
    public bool FirstMsg = false;
}
