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

namespace WABot;

public static class Globals
{
    private const string NameSetupFile = "Setup.json";
    
    public static Setup Setup = null!;

    public static async Task Init()
    {
        Setup = (File.Exists(NameSetupFile)
            ? JsonConvert.DeserializeObject<Setup>(await File.ReadAllTextAsync(NameSetupFile))
            : new Setup())!;

        if (!File.Exists(NameSetupFile))
            await SaveSetup();
    }
    
    public static async Task SaveSetup()
    {
        await File.WriteAllTextAsync(NameSetupFile, JsonConvert.SerializeObject(Setup));
    }
}

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
    /// Путь до директории с аккаунтами WhhatsApp
    /// </summary>
    public string PathToDirectoryAccounts = string.Empty;
    /// <summary>
    /// Путь до файла с номерами пользователей для рассылки сообщений
    /// </summary>
    public string PathToPhonesUsers = string.Empty;
}