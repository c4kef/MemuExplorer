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
    public static DirectoryInfo TempDirectory { get; private set; } = null!;
    public static VirtualOutput Camera { get; private set; } = null!;
    public static string QrCodeName { get; set; } = string.Empty;
    public static Namespace Socket { get; private set; } = null!;

    public static async Task Init()
    {
        Devices = new List<Device>();

        _ = Task.Run(WAWClient.QueueCameraHandler);

        TempDirectory = Directory.CreateDirectory("Temp");
        RemoveAccountsDirectory = Directory.CreateDirectory("RemovedAccounts");

        Setup = (File.Exists(NameSetupFile)
            ? JsonConvert.DeserializeObject<Setup>(await File.ReadAllTextAsync(NameSetupFile))
            : new Setup())!;

        if (!File.Exists(NameSetupFile))
            await SaveSetup();


        Socket = new SocketIOClient().Connect("http://localhost:3000/");

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
                    { LastActiveDialog = new Dictionary<string, DateTime>(), TrustLevelAccount = 0 }));
    }

    public static async Task SaveSetup()
    {
        await File.WriteAllTextAsync(NameSetupFile, JsonConvert.SerializeObject(Setup));
    }

    public static async Task<(string phone, string path)[]> GetAccountsWarm(string[] phoneFrom)
    {
        var accounts = new List<(string phone, string path)>();

        foreach (var accountDirectory in Directory.GetDirectories(Setup.PathToDirectoryAccounts))
        {
            if (!File.Exists($@"{accountDirectory}\Data.json") ||
                !Directory.Exists($@"{accountDirectory}\com.whatsapp"))
                continue;

            var phone = new DirectoryInfo(accountDirectory).Name;
            var dataAccount =
                JsonConvert.DeserializeObject<AccountData>(
                    await File.ReadAllTextAsync($@"{accountDirectory}\Data.json"));

            switch (phoneFrom.Contains(phone))
            {
                case false when !dataAccount!.LastActiveDialog!.ContainsKey(phone):
                case false when dataAccount.LastActiveDialog!.ContainsKey(phone) &&
                                (DateTime.Now - dataAccount.LastActiveDialog[phone]).TotalMilliseconds >=
                                Setup.DelayBetweenUsers * 1000:
                    accounts.Add((phone, accountDirectory));
                    break;
            }
        }

        return accounts.ToArray();
    }

    public static async Task<(string phone, string path)[]> GetAccounts(string[] phoneFrom, int trustLevelAccount)
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

            if (!phoneFrom.Contains(phone) && dataAccount!.TrustLevelAccount >= trustLevelAccount)
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
    /// Кол-во циклов на блок сообщений
    /// </summary>
    public int CountMessage = 1;

    /// <summary>
    /// Задержка между отправкой сообщений между двумя пользователями (срабатывает по окончанию цикла)
    /// </summary>
    public int DelayBetweenUsers = 24000;

    /// <summary>
    /// Уровень прогрева аккаунта для начала рассылки сообщений
    /// </summary>
    public int TrustLevelAccount = 0;

    /// <summary>
    /// Кол-во сообщений с аккаунта при рассылке
    /// </summary>
    public int CountMessageFromAccount = 2;

    /// <summary>
    /// Индекс страны для регистрации
    /// </summary>
    public int CountryIndexRegister = 0;

    /// <summary>
    /// Включить режим прогрева
    /// </summary>
    public bool EnableWarm = false;

    /// <summary>
    /// Путь до директории с аккаунтами WhatsApp
    /// </summary>
    public string PathToDirectoryAccounts = string.Empty;

    /// <summary>
    /// Путь до директории с аккаунтами WhatsApp Web
    /// </summary>
    public string PathToDirectoryAccountsWeb = string.Empty;

    /// <summary>
    /// Путь до файла с номерами пользователей для рассылки сообщений
    /// </summary>
    public string PathToPhonesUsers = string.Empty;

    /// <summary>
    /// Путь до образа виртуального устройства
    /// </summary>
    public string PathToImageDevice = string.Empty;

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
    /// 
    /// </summary>
    public int CountMessages = 0;

    /// <summary>
    /// Дата создания
    /// </summary>
    public DateTime CreatedDate = DateTime.Now;

    /// <summary>
    /// Последняя переписка с аккаунтом
    /// </summary>
    public Dictionary<string, DateTime>? LastActiveDialog = new Dictionary<string, DateTime>();
}

public class Device
{
    public int Index { get; set; }
    public bool IsActive { get; set; }

    public bool InUsage;
    public WaClient Client = null!;
}

#endregion