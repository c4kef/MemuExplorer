namespace MemuLib.Core;

internal static class Log
{
    /// <summary>
    /// Скромная реализация дебага
    /// </summary>
    /// <param name="info">что будем записывать</param>
    internal static async void Write(string info)
    {
        if (!Globals.IsLog)
            return;

        var about = $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] {new StackTrace().GetFrame(1)?.GetMethod()?.Name} -> {info}\n";
        await File.AppendAllTextAsync("log.txt", about);
        Console.WriteLine(about);
    }
}