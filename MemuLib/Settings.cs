namespace MemuLib;

public static class Settings
{
    /// <summary>
    /// Путь до папки с мемом
    /// </summary>
    public const string BaseDir = @"D:\Program Files\Microvirt\MEmu\";

    /// <summary>
    /// Путь до папки с данными
    /// </summary>
    public const string DatasDir = @"D:\C4ke\Whatsapp\MemuExplorer\Data\Accounts";//D:\C4ke\Whatsapp\MemuExplorer\Data\Accounts//C:\Users\artem\source\repos\MVP\MemuExplorer\Data

    /// <summary>
    /// API ключ от сервиса для приема смс
    /// </summary>
    public const string SimApi = "72deadac6b4f05dcd06f65534a0eeec2";

    /// <summary>
    /// Таймер операций (в м.секундах)
    /// </summary>
    public const int WaitingSecs = 400;
}