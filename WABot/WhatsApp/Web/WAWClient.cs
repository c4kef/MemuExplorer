namespace WABot.WhatsApp.Web;

public class WAWClient
{
    private readonly string _nameSession;
    private readonly List<int> _taskQueue;
    private readonly Dictionary<int, JObject> _taskFinished;
    private readonly Random _random;
    private readonly int _taskId;

    private static List<int> Queue;
    private static List<int> QueueProcess;

    /// <summary>
    /// Инициализация сессии
    /// </summary>
    /// <param name="nameSession">имя сессии</param>
    public WAWClient(string nameSession)
    {
        _nameSession = nameSession;
        _taskQueue = new List<int>();
        _taskFinished = new Dictionary<int, JObject>();
        _random = new Random();
        _taskId = _random.Next(1_000_000, 10_000_000);

        Queue.Add(_taskId);
    }

    /// <summary>
    /// Обработчик запросов на сканирование
    /// </summary>
    public static async Task QueueCameraHandler()
    {
        Queue = new List<int>();
        QueueProcess = new List<int>();

        while (true)
        {
            if (Queue.Count > 0 && QueueProcess.Count == 0)
            {
                QueueProcess.Add(Queue[0]);
                Queue.RemoveAt(0);
            }

            await Task.Delay(500);
        }
    }

    /// <summary>
    /// Главный обработчик команд с сервера
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    private void HandlerDataRequests(object[] args, Action<string> callback)
    {
        var builder = new StringBuilder();
        foreach (var line in args)
            builder.AppendLine(line.ToString());

        var data = JObject.Parse(builder.ToString());
        var id = int.Parse(data["value"]![0]!.ToString());

        _taskFinished.Add(id, data);
        
        _taskQueue.RemoveAll(task => task == id);
    }

    /// <summary>
    /// Ждем своей очереди
    /// </summary>
    public async Task WaitQueue()
    {
        while (!QueueProcess.Any(_id => _id == _taskId))
            await Task.Delay(500);
    }

    /// <summary>
    /// Инициализировать
    /// </summary>
    /// <param name="waitQr">мне следует ждать Qr код?</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task Init(bool waitQr)
    {
        Globals.Socket.On("data", HandlerDataRequests);

        _taskQueue.Add(_taskId);

        Globals.Socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
            { Type = "create", Values = new List<object>() { $"{_nameSession}@{_taskId}" } }));

        if (waitQr)
        {
            while (!File.Exists(@$"{Globals.Setup.PathToQRs}\{_taskId}.png"))
                await Task.Delay(500);

            Globals.QrCodeName = _taskId.ToString();

            while (_taskQueue.Contains(_taskId))
                await Task.Delay(500);

            Globals.QrCodeName = string.Empty;

            while (!TryDeleteQR())
                await Task.Delay(500);
        }
        else
            while (_taskQueue.Contains(_taskId))
                await Task.Delay(500);

        var data = _taskFinished[_taskId];
        _taskFinished.Remove(_taskId);

        if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {(data["value"]?.Count() >= 2 ? data["value"]![1] : "undocumented error :(")}");

        bool TryDeleteQR()
        {
            try
            {
                if (File.Exists(@$"{Globals.Setup.PathToQRs}\{_taskId}.png"))
                    File.Delete(@$"{Globals.Setup.PathToQRs}\{_taskId}.png");

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Удаляем наш запрос из очереди
    /// </summary>
    public void RemoveQueue() => QueueProcess.Remove(_taskId);

    /// <summary>
    /// Отправить сообщение
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task<bool> SendText(string number, string text)
    {
        try
        {
            var id = _random.Next(1_000_000, 10_000_000);

            _taskQueue.Add(id);

            Globals.Socket.Emit("data",
                JsonConvert.SerializeObject(new ServerData()
                { Type = "sendText", Values = new List<object>() { $"{_nameSession}@{id}", $"{number.Replace("+", string.Empty)}@c.us", text } }));

            while (_taskQueue.Contains(id))
                await Task.Delay(100);

            var data = _taskFinished[id];

            _taskFinished.Remove(id);

            if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
                throw new Exception($"Error: {data["value"]![1]}");

            return true;
        }
        catch(Exception ex)
        {
            Log.Write($"Ошибка отправки сообщения:\n{ex.Message}\n");
            return false;
        }
    }

    /// <summary>
    /// Разлогиниться
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task Logout()
    {
        var id = _random.Next(1_000_000, 10_000_000);

        _taskQueue.Add(id);

        Globals.Socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
            { Type = "logout", Values = new List<object>() { $"{_nameSession}@{id}" } }));

        while (_taskQueue.Contains(id))
            await Task.Delay(100);

        var data = _taskFinished[id];

        _taskFinished.Remove(id);
        
        Globals.Socket.RemoveListener("data", HandlerDataRequests);

        if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {data["value"]![1]}");
    }

    /// <summary>
    /// Освобождает текущею сессию из пула на сервере
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task Free()
    {
        var id = _random.Next(1_000_000, 10_000_000);

        _taskQueue.Add(id);

        Globals.Socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
            { Type = "free", Values = new List<object>() { $"{_nameSession}@{id}" } }));

        while (_taskQueue.Contains(id))
            await Task.Delay(100);

        var data = _taskFinished[id];

        _taskFinished.Remove(id);

        Globals.Socket.RemoveListener("data", HandlerDataRequests);

        if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {data["value"]![1]}");
    }
}