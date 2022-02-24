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
    public const string DatasDir = @"C:\Users\artem\source\repos\MVP\MemuExplorer\Data";

    /// <summary>
    /// API ключ от сервиса 5SIM
    /// </summary>
    public const string FSimApi = "eyJhbGciOiJSUzUxMiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE2NTU0MDAzNTEsImlhdCI6MTYyMzg2NDM1MSwicmF5IjoiOWI5NTM4ZDc5YjgzYjA2NzE1MTFkYmUwMmU0YTk5YTUiLCJzdWIiOjM4MTUzMn0.ziZH6xWKOSGugh1elg-SR7L9LxJjq0vwSZVmaguNvsqvxx3Bp2LN1gIOaibCuny1hazHvMXGfK09_Gt2nUv_JqTIRc9PCsHtoCbzGYUyDojx49rzh8r5Ok7BIWzK6aASKyzrUtBuWrIPJJPUhl3Dj-PuoaCBM_7O0KSKUQ97i05nzMp4rWo5m8OC3o8d_pac2BMuK3MnS70h_m5qimOgH0HSpKQAquaPT7o-lHlSW5DMZeZj-njKSzJ3MfGFkqNdYCjYYpQhJW99SsT_brgXI5KLvV9HKL6MQgJ1l7-9N4tgqojHYjdsNjc_v4zD9PeB19CfI11Rz03c7a-JRJNT6g";

    /// <summary>
    /// Таймер операций (в секундах)
    /// </summary>
    public const int WaitingSecs = 1_500;
}