using Newtonsoft.Json;

namespace UBot
{
    public class Globals
    {
        public const string NameSetupFile = "Setup.data";

        public static DirectoryInfo DataDirectory { get; private set; }
        public static FileInfo SetupFile { get; private set; }
        public static Setup Setup { get; private set; }

        public static async Task Init()
        {
            DataDirectory = Directory.CreateDirectory("Data");
            SetupFile = new FileInfo($@"{DataDirectory.FullName}\{NameSetupFile}");

            Setup = (File.Exists(SetupFile.FullName)
                ? JsonConvert.DeserializeObject<Setup>(await File.ReadAllTextAsync(SetupFile.FullName))
                : new Setup())!;
        }

        public static async Task SaveSetup() => await File.WriteAllTextAsync(SetupFile.FullName, JsonConvert.SerializeObject(Setup, Formatting.Indented));
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
