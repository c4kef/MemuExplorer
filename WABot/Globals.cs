global using System.Windows.Navigation;
global using ModernWpf.Controls;
global using System.Windows;
global using System;
global using System.Threading.Tasks;
global using System.IO;
global using Newtonsoft.Json;
global using System.ComponentModel;
global using System.Runtime.CompilerServices;
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

namespace WABot;

public static class Globals
{
    private const string NameSetupFile = "Setup.json";
    public static List<WAClient> Devices { get; private set; } = null!;
    public static Setup Setup { get; private set; } = null!;
    public static Dictionary<char, string> Alphabet{ get; private set; } = null!;
    public static DirectoryInfo RemoveAccountsDirectory { get; private set; } = null!;

    public static async Task Init()
    {
        Devices = new List<WAClient>();
        Alphabet = new Dictionary<char, string>();

        RemoveAccountsDirectory = Directory.CreateDirectory("RemoveAccounts");

        #region Init alphabet

        Alphabet.Add('а', "a");
        Alphabet.Add('б', "b");
        Alphabet.Add('в', "v");
        Alphabet.Add('г', "g");
        Alphabet.Add('д', "d");
        Alphabet.Add('е', "e");
        Alphabet.Add('ё', "yo");
        Alphabet.Add('ж', "zh");
        Alphabet.Add('з', "z");
        Alphabet.Add('и', "i");
        Alphabet.Add('й', "j");
        Alphabet.Add('к', "k");
        Alphabet.Add('л', "l");
        Alphabet.Add('м', "m");
        Alphabet.Add('н', "n");
        Alphabet.Add('о', "o");
        Alphabet.Add('п', "p");
        Alphabet.Add('р', "r");
        Alphabet.Add('с', "s");
        Alphabet.Add('т', "t");
        Alphabet.Add('у', "u");
        Alphabet.Add('ф', "f");
        Alphabet.Add('х', "h");
        Alphabet.Add('ц', "c");
        Alphabet.Add('ч', "ch");
        Alphabet.Add('ш', "sh");
        Alphabet.Add('щ', "sch");
        Alphabet.Add('ъ', "j");
        Alphabet.Add('ы', "i");
        Alphabet.Add('ь', "j");
        Alphabet.Add('э', "e");
        Alphabet.Add('ю', "yu");
        Alphabet.Add('я', "ya");
        Alphabet.Add('А', "A");
        Alphabet.Add('Б', "B");
        Alphabet.Add('В', "V");
        Alphabet.Add('Г', "G");
        Alphabet.Add('Д', "D");
        Alphabet.Add('Е', "E");
        Alphabet.Add('Ё', "Yo");
        Alphabet.Add('Ж', "Zh");
        Alphabet.Add('З', "Z");
        Alphabet.Add('И', "I");
        Alphabet.Add('Й', "J");
        Alphabet.Add('К', "K");
        Alphabet.Add('Л', "L");
        Alphabet.Add('М', "M");
        Alphabet.Add('Н', "N");
        Alphabet.Add('О', "O");
        Alphabet.Add('П', "P");
        Alphabet.Add('Р', "R");
        Alphabet.Add('С', "S");
        Alphabet.Add('Т', "T");
        Alphabet.Add('У', "U");
        Alphabet.Add('Ф', "F");
        Alphabet.Add('Х', "H");
        Alphabet.Add('Ц', "C");
        Alphabet.Add('Ч', "Ch");
        Alphabet.Add('Ш', "Sh");
        Alphabet.Add('Щ', "Sch");
        Alphabet.Add('Ъ', "J");
        Alphabet.Add('Ы', "I");
        Alphabet.Add('Ь', "J");
        Alphabet.Add('Э', "E");
        Alphabet.Add('Ю', "Yu");
        Alphabet.Add('Я', "Ya");

        #endregion

        Setup = (File.Exists(NameSetupFile)
            ? JsonConvert.DeserializeObject<Setup>(await File.ReadAllTextAsync(NameSetupFile))
            : new Setup())!;

        if (!File.Exists(NameSetupFile))
            await SaveSetup();

        //MemuLib.Globals.IsLog = true;
    }

    public static async Task SaveSetup()
    {
        await File.WriteAllTextAsync(NameSetupFile, JsonConvert.SerializeObject(Setup));
    }

    public static async Task<(string phone, string path)[]> GetAccountsWarm(string phoneFrom)
    {
        var accounts = new List<(string phone, string path)>();

        foreach (var accountDirectory in Directory.GetDirectories(Setup.PathToDirectoryAccounts))
        {
            if (!File.Exists($@"{accountDirectory}\Data.json") || !Directory.Exists($@"{accountDirectory}\com.whatsapp"))
                continue;

            var phone = new DirectoryInfo(accountDirectory).Name;
            var dataAccount =
                JsonConvert.DeserializeObject<AccountData>(
                    await File.ReadAllTextAsync($@"{accountDirectory}\Data.json"));

            if (phone != phoneFrom && !dataAccount!.LastActiveDialog!.ContainsKey(phone))
                accounts.Add((phone, accountDirectory));
            else if (phone != phoneFrom && dataAccount!.LastActiveDialog!.ContainsKey(phone) &&
                     (DateTime.Now - dataAccount.LastActiveDialog[phone]).TotalMilliseconds >= Setup.DelayBetweenUsers * 1000)
                accounts.Add((phone, accountDirectory));
        }

        return accounts.ToArray();
    }
    
    public static async Task<(string phone, string path)[]> GetAccountsWarm(string[] phoneFrom)
    {
        var accounts = new List<(string phone, string path)>();

        foreach (var accountDirectory in Directory.GetDirectories(Setup.PathToDirectoryAccounts))
        {
            if (!File.Exists($@"{accountDirectory}\Data.json") || !Directory.Exists($@"{accountDirectory}\com.whatsapp"))
                continue;

            var phone = new DirectoryInfo(accountDirectory).Name;
            var dataAccount =
                JsonConvert.DeserializeObject<AccountData>(
                    await File.ReadAllTextAsync($@"{accountDirectory}\Data.json"));

            if (!phoneFrom.Contains(phone) && !dataAccount!.LastActiveDialog!.ContainsKey(phone))
                accounts.Add((phone, accountDirectory));
            else if (!phoneFrom.Contains(phone) && dataAccount!.LastActiveDialog!.ContainsKey(phone) &&
                     (DateTime.Now - dataAccount.LastActiveDialog[phone]).TotalMilliseconds >= Setup.DelayBetweenUsers * 1000)
                accounts.Add((phone, accountDirectory));
        }

        return accounts.ToArray();
    }
    
    public static async Task<(string phone, string path)[]> GetAccounts(string[] phoneFrom, int trustLevelAccount)
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
    /// Кол-во виртуальный устройств
    /// </summary>
    public int CountDevices = 2;
    /// <summary>
    /// Включить режим прогрева
    /// </summary>
    public bool EnableWarm = false;
    /// <summary>
    /// Путь до директории с аккаунтами WhhatsApp
    /// </summary>
    public string PathToDirectoryAccounts = string.Empty;
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
}

[Serializable]
public class AccountData
{
    /// <summary>
    /// Уровень прогрева аккаунта для начала рассылки сообщений
    /// </summary>
    public int TrustLevelAccount = 0;
    /// <summary>
    /// Последняя переписка с аккаунтом
    /// </summary>
    public Dictionary<string, DateTime>? LastActiveDialog;
}

#endregion