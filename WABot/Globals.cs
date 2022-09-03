global using System.Windows.Navigation;
global using ModernWpf.Controls;
global using System.Windows;
global using System;
global using System.Threading.Tasks;
global using System.IO;
global using Newtonsoft.Json;
global using System.ComponentModel;
global using System.Windows.Media;
global using Microsoft.WindowsAPICodePack.Dialogs;
global using System.Runtime.InteropServices;
global using System.Text;
global using Newtonsoft.Json.Linq;
global using SocketIO.Client;
global using System.Text.RegularExpressions;
global using MemuLib.Core;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using WABot.WhatsApp;
global using MemuLib.Core.Contacts;
global using ModernWpf;
global using System.Windows.Controls;
global using System.Drawing;
global using System.Drawing.Imaging;
global using VirtualCameraOutput;
global using Image = System.Windows.Controls.Image;
using WABot.WhatsApp.Web;

namespace WABot;

public static class Globals
{
    private const string NameSetupFile = "Setup.json";
    public static List<Device> Devices { get; set; } = null!;
    public static Setup Setup { get; private set; } = null!;
    public static DirectoryInfo RemoveAccountsDirectory { get; private set; } = null!;
    public static DirectoryInfo ScannedAccountsDirectory { get; private set; } = null!;
    public static DirectoryInfo LogoutAccountsDirectory { get; private set; } = null!;
    public static DirectoryInfo TempDirectory { get; private set; } = null!;
    public static VirtualOutput Camera { get; private set; } = null!;
    public static string QrCodeName { get; set; } = string.Empty;

    public static async Task Init()
    {
        Devices = new List<Device>();

        _ = Task.Run(WAWClient.QueueCameraHandler);

        TempDirectory = Directory.CreateDirectory("Temp");

        RemoveAccountsDirectory = Directory.CreateDirectory("RemovedAccounts");
        ScannedAccountsDirectory = Directory.CreateDirectory("ScannedAccounts");
        LogoutAccountsDirectory = Directory.CreateDirectory("LogoutAccounts");

        Setup = (File.Exists(NameSetupFile)
            ? JsonConvert.DeserializeObject<Setup>(await File.ReadAllTextAsync(NameSetupFile))
            : new Setup())!;

        if (!File.Exists(NameSetupFile))
            await SaveSetup();

        Camera = new VirtualOutput(276, 276, 20, FourCC.FOURCC_24BG);

        _ = Task.Run(OBSCamera);

        MemuLib.Globals.IsLog = true;
    }

    public static async Task OBSCamera()
    {
        while (true)
        {
            var fileName = File.Exists(@$"{Setup.PathToQRs}\{QrCodeName}.png") ? QrCodeName : "neutral";

            var img = System.Drawing.Image.FromFile(@$"{Setup.PathToQRs}\{fileName}.png");
            
            var qr = ConvertTo24Bpp(img);
            qr.Save($@"{TempDirectory.FullName}\{fileName}.bmp", ImageFormat.Bmp);
            var bmpQr = new Bitmap($@"{TempDirectory.FullName}\{fileName}.bmp");

            var converter = new ImageConverter();
            var imageBytes = (byte[])converter.ConvertTo(bmpQr, typeof(byte[]))!;

            var count = 0;

            while (count < 40)
            {
                Camera.Send(imageBytes);
                await Task.Delay(50);
                count++;
            }

            qr.Dispose();
            bmpQr.Dispose();
            img.Dispose();

            File.Delete($@"{TempDirectory.FullName}\{fileName}.bmp");
        }

        Bitmap ConvertTo24Bpp(System.Drawing.Image img)
        {
            var bmp = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var gr = Graphics.FromImage(bmp);
            gr.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height));
            return bmp;
        }
    }

    public static async Task InitAccountsFolder()
    {
        foreach (var directory in Directory.GetDirectories(Setup.PathToDirectoryAccounts))
            if (!File.Exists($@"{directory}\Data.json") && (Directory.Exists($@"{directory}\com.whatsapp") || Directory.Exists($@"{directory}\com.whatsapp.w4b")))
                await File.WriteAllTextAsync($@"{directory}\Data.json",
                    JsonConvert.SerializeObject(new AccountData()
                    { TrustLevelAccount = 0 }, Formatting.Indented));
    }

    public static async Task SaveSetup()
    {
        await File.WriteAllTextAsync(NameSetupFile, JsonConvert.SerializeObject(Setup));
    }

    public static async Task<(string phone, string path)[]> GetAccounts(string[] phoneFrom, bool isWarming = false)
    {
        var accounts = new List<(string phone, string path)>();

        foreach (var accountDirectory in Directory.GetDirectories(Setup.PathToDirectoryAccounts))
        {
            if (!File.Exists($@"{accountDirectory}\Data.json"))
                continue;

            var phone = new DirectoryInfo(accountDirectory).Name;
            var dataAccount =
                JsonConvert.DeserializeObject<AccountData>(
                    await File.ReadAllTextAsync($@"{accountDirectory}\Data.json"));

            if (!phoneFrom.Contains(phone) && (isWarming || dataAccount!.TrustLevelAccount >= Setup.WarmLevelForNewsletter))
                accounts.Add((phone, accountDirectory));
        }

        return accounts.ToArray();
    }
}

#region Images

[Serializable]
public class Setup
{
    /// <summary>
    /// Задержка оптравки сообщения от
    /// </summary>
    public int DelaySendMessageFrom = 0;

    /// <summary>
    /// Задержка оптравки сообщения до
    /// </summary>
    public int DelaySendMessageTo = 0;

    /// <summary>
    /// Кол-во прогонов через вебку
    /// </summary>
    public int CountWarmsOnWeb = 1;

    /// <summary>
    /// Кол-во сообщений с аккаунта
    /// </summary>
    public int CountMessagesFromAccount = 50;

    /// <summary>
    /// Кол-во потоков хрома
    /// </summary>
    public int CountThreadsChrome = 1;

    /// <summary>
    /// Уровень прогрева при рассылке
    /// </summary>
    public int WarmLevelForNewsletter = 0;

    /// <summary>
    /// Включить подготовку аккаунта через веб?
    /// </summary>
    public bool EnableWeb = false;

    /// <summary>
    /// Включить сканирование qr кода?
    /// </summary>
    public bool EnableScanQr = false;

    /// <summary>
    /// Включить проверку только на бан?
    /// </summary>
    public bool EnableCheckBan = false;

    /// <summary>
    /// Включить минимальный прогрев?
    /// </summary>
    public bool EnableMinWarm = false;

    /// <summary>
    /// Путь до директории с аккаунтами WhatsApp
    /// </summary>
    public string PathToDirectoryAccounts = string.Empty;

    /// <summary>
    /// Путь до файла с текстом прогрева
    /// </summary>
    public string PathToTextForWarm = string.Empty;

    /// <summary>
    /// Путь до файла с номерами пользователей для рассылки сообщений
    /// </summary>
    public string PathToPhonesUsers = string.Empty;

    /// <summary>
    /// Путь до файла с именами пользователей
    /// </summary>
    public string PathToUserNames = string.Empty;

    /// <summary>
    /// Путь до папки с QR кодами
    /// </summary>
    public string PathToQRs = string.Empty;
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

public class Device
{
    public int Index { get; set; }
    public bool IsActive { get; set; }

    public bool InUsage;
    public WaClient Client = null!;
}

#endregion