namespace MemuConsole;

public static class Settings
{
    /// <summary>
    /// Путь до папки с мемом
    /// </summary>
    public const string BaseDir = @"D:\Program Files\Microvirt\MEmu\";

    /// <summary>
    /// Путь до папки с приложениями для установки
    /// </summary>
    public const string AppsDir = @"C:\Users\artem\source\repos\MVP\MemuExplorer\Apps";

    /// <summary>
    /// Путь до папки с аккаунтами для WhatsApp
    /// </summary>
    public const string AccsDir = @"C:\Users\artem\source\repos\MVP\MemuExplorer\Accounts";
    
    /// <summary>
    /// Путь до папки с данными
    /// </summary>
    public const string DatasDir = @"C:\Users\artem\source\repos\MVP\MemuExplorer\Data";

    /// <summary>
    /// Таймер операций (в секундах)
    /// </summary>
    public const int WaitingSecs = 1_500;
}