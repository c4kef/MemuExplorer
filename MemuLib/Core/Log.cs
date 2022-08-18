namespace MemuLib.Core;

public static class Log
{
    /// <summary>
    /// Хранение активных заявок на запись
    /// </summary>
    private static readonly List<DataWrite> DataWrites = new List<DataWrite>();

    /// <summary>
    /// Просто переменная для определения статуса работы
    /// </summary>
    private static bool _isRuning;

    /// <summary>
    /// Обработчик заявок на запись
    /// </summary>
    private static async void HandlerWriter()
    {
        _isRuning = true;

        while (Globals.IsLog)
        {
            await Task.Delay(500);

            if (DataWrites.Count == 0) continue;

            await File.AppendAllTextAsync(DataWrites[0].Path, DataWrites[0].Text);
            DataWrites.RemoveAt(0);
        }
    }

    /// <summary>
    /// Скромная реализация дебага
    /// </summary>
    /// <param name="info">что будем записывать</param>
    /// <param name="path">путь до файла в который запишем</param>
    public static async void Write(string info, string path = "")
    {
        if (!Globals.IsLog)
            return;

        if (!_isRuning)
            _ = Task.Run(HandlerWriter);

        var about =
            (info.Contains(';')) ?
            $"{info}\n" :
            $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] {new StackTrace().GetFrame(1)?.GetMethod()?.Name} -> {info}\n";

        DataWrites.Add(new DataWrite() {Path = (string.IsNullOrEmpty(path)) ? "log.txt" : path, Text = about});

        Console.WriteLine(about);
    }
}