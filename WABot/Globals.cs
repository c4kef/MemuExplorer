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
    public static List<WAClient> Devices = null!;
    public static Setup Setup = null!;

    public static async Task Init()
    {
        Devices = new List<WAClient>();
        
        Setup = (File.Exists(NameSetupFile)
            ? JsonConvert.DeserializeObject<Setup>(await File.ReadAllTextAsync(NameSetupFile))
            : new Setup())!;

        if (!File.Exists(NameSetupFile))
            await SaveSetup();
        
        //MemuLib.Globals.IsLog = true;
        Memu.RunAdbServer();
    }
    
    public static async Task SaveSetup()
    {
        await File.WriteAllTextAsync(NameSetupFile, JsonConvert.SerializeObject(Setup));
    }
    
    public static async Task<(string phone, string path)> GetRandomAccount([Optional]int trustLevel)
    {
        foreach (var accountDirectory in Directory.GetDirectories(Setup.PathToDirectoryAccounts))
        {
            if (!File.Exists($@"{accountDirectory}\Data.json") || !Directory.Exists($@"{accountDirectory}\com.whatsapp"))
                continue;

            var dataAccount =
                JsonConvert.DeserializeObject<AccountData>(
                    await File.ReadAllTextAsync($@"{accountDirectory}\Data.json"));

            if (Setup.EnableWarm)
                return (new DirectoryInfo(accountDirectory).Name, accountDirectory);
            else if (trustLevel != 0 && trustLevel >= dataAccount!.TrustLevelAccount)
                return (new DirectoryInfo(accountDirectory).Name, accountDirectory);
        }

        return (string.Empty, string.Empty);
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
                     (DateTime.Now - dataAccount.LastActiveDialog[phone]).TotalMilliseconds >= Setup.DelayBetweenUsers)
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
                     (DateTime.Now - dataAccount.LastActiveDialog[phone]).TotalMilliseconds >= Setup.DelayBetweenUsers)
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
    /// Глубина сообщений
    /// </summary>
    public int CountMessage = 0;
    /// <summary>
    /// Задержка между отправкой сообщений между двумя пользователями (срабатывает по окончанию цикла)
    /// </summary>
    public int DelayBetweenUsers = 0;
    /// <summary>
    /// Уровень прогрева аккаунта для начала рассылки сообщений
    /// </summary>
    public int TrustLevelAccount = 0;
    /// <summary>
    /// Кол-во виртуальный устройств
    /// </summary>
    public int CountDevices = 0;
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