namespace WABot.WhatsApp.Web;

public class WAWClient
{
    private readonly string _nameSession;
    private readonly List<int> _taskQueue;
    private readonly Dictionary<int, JObject> _taskFinished;
    private readonly Random _random;

    private static List<int> Queue;
    private static List<int> QueueProcess;
    public readonly int TaskId;

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
        TaskId = _random.Next(1_000_000, 10_000_000);

        Queue.Add(TaskId);
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
        while (!QueueProcess.Any(_id => _id == TaskId))
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

        _taskQueue.Add(TaskId);

        Globals.Socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
            { Type = "create", Values = new List<object>() { $"{_nameSession}@{TaskId}" } }));

        if (waitQr)
        {
            Task.WaitAll(new Task[] { Task.Run(WaitQr) }, 10_000);

            Globals.QrCodeName = TaskId.ToString();

            Task.WaitAll(new Task[] { Task.Run(WaitRequest) }, 15_000);

            if (_taskQueue.Contains(TaskId))
                throw new Exception("Error: request is not accepted");

            Globals.QrCodeName = string.Empty;

            while (!TryDeleteQR())
                await Task.Delay(500);
        }
        else
        {
            Task.WaitAll(new Task[] { Task.Run(WaitRequest) }, 15_000);

            if (_taskQueue.Contains(TaskId))
                throw new Exception("Error: request is not accepted");
        }

        var data = _taskFinished[TaskId];
        _taskFinished.Remove(TaskId);

        if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {(data["value"]?.Count() >= 2 ? data["value"]![1] : "undocumented error :(")}");

        async Task WaitRequest()
        {
            while (_taskQueue.Contains(TaskId))
                await Task.Delay(500);
        }

        async Task WaitQr()
        {
            while (!File.Exists(@$"{Globals.Setup.PathToQRs}\{TaskId}.png"))
                await Task.Delay(500);
        }

        bool TryDeleteQR()
        {
            try
            {
                if (File.Exists(@$"{Globals.Setup.PathToQRs}\{TaskId}.png"))
                    File.Delete(@$"{Globals.Setup.PathToQRs}\{TaskId}.png");

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
    public void RemoveQueue() => QueueProcess.Remove(TaskId);

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

    /// <summary>
    /// Проверяет контакт на наличие Whatsapp
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task<bool> CheckValidPhone(string phone)
    {
        try
        {
            var id = _random.Next(1_000_000, 10_000_000);

            _taskQueue.Add(id);

            Globals.Socket.Emit("data",
                JsonConvert.SerializeObject(new ServerData()
                { Type = "checkValidPhone", Values = new List<object>() { $"{_nameSession}@{id}", phone } }));

            while (_taskQueue.Contains(id))
                await Task.Delay(100);

            var data = _taskFinished[id];

            _taskFinished.Remove(id);

            if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
                throw new Exception($"Error: {data["value"]![1]}");

            return (bool)(data["value"]![1] ?? false);
        }
        catch (Exception ex)
        {
            Log.Write($"Ошибка проверки номера:\n{ex.Message}\n");
            return false;
        }
    }
}