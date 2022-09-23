using Newtonsoft.Json;

namespace UBot
{
    public class Globals
    {
        public const string NameSetupFile = "Setup.data";

        public static string QrCodeName { get; set; }
        public static DirectoryInfo DataDirectory { get; private set; }
        public static DirectoryInfo TempDirectory { get; private set; }
        public static DirectoryInfo WarmedDirectory { get; private set; }
        public static DirectoryInfo LogoutDirectory { get; private set; }
        public static FileInfo SetupFile { get; private set; }
        public static Setup Setup { get; private set; }
        private static object Locker { get; set; }

        public static async Task Init()
        {
            DataDirectory = Directory.CreateDirectory("Data");
            TempDirectory = Directory.CreateDirectory("Temp");
            WarmedDirectory = Directory.CreateDirectory("Warmed");
            LogoutDirectory = Directory.CreateDirectory("Logout");

            SetupFile = new FileInfo($@"{DataDirectory.FullName}\{NameSetupFile}");

            Locker = new();

            Setup = (File.Exists(SetupFile.FullName)
                ? JsonConvert.DeserializeObject<Setup>(await File.ReadAllTextAsync(SetupFile.FullName))
                : new Setup())!;
        }

        public static async Task SaveSetup() => await File.WriteAllTextAsync(SetupFile.FullName, JsonConvert.SerializeObject(Setup, Formatting.Indented));

        public static (string phone, string path)[] GetAccounts(string[] phoneFrom, bool isWarming = false, object locker = null)
        {
            lock (locker is null ? Locker : locker)
            {
                var accounts = new List<(string phone, string path)>();

                foreach (var accountDirectory in Directory.GetDirectories(Setup.PathToFolderAccounts))
                {
                    if (!File.Exists($@"{accountDirectory}\Data.json"))
                        continue;

                    var phone = new DirectoryInfo(accountDirectory).Name;
                    var dataAccount =
                        JsonConvert.DeserializeObject<AccountData>(
                            File.ReadAllText($@"{accountDirectory}\Data.json"));

                    if (!phoneFrom.Contains(phone) && (isWarming || dataAccount!.TrustLevelAccount >= Setup.MinTrustLevel))
                        accounts.Add((phone, accountDirectory));
                }

                return accounts.ToArray();
            }
        }

        public static async Task<bool> TryMove(string from, string to, int countTry = 5)
        {
            var current = 0;
            while (current++ <= countTry)
            {
                try
                {
                    if (Directory.Exists(from))
                        Directory.Move(from, to);

                    return true;
                }
                catch
                {
                    await Task.Delay(1_000);
                }
            }

            return false;
        }
    }

    [Serializable]
    public class Setup
    {
        public string PathToFileGroups;
        public string PathToFileChatBots;
        public string PathToFilePeoples;
        public SelectEmulatorScan? SelectEmulatorScan;
        public string PathToFileNames;
        public string PathToFileImage;
        public string PathToFolderAccounts;
        public string PathToFileTextWarm;
        public string PathToFileTextPeopleWarm;
        public int? PinCodeAccount;

        public int? CountMessages;
        public int? NumberRepetitionsActions;
        public int? CountThreads;
        public int? MinTrustLevel;

        public int? DelaySendMessageFrom;
        public int? DelaySendMessageTo;

        public string NumbersForNewsletter;
    }
}
