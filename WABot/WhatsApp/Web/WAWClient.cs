namespace WABot.WhatsApp.Web;

public class WAWClient
{
    private readonly string _nameSession;
    private readonly List<int> _taskQueue;
    private readonly Dictionary<int, JObject> _taskFinished;
    private readonly Random _random;
    
    private Namespace _socket;
    private SocketIOClient _socketClient;
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

        var lastId = 0;
        var aliveLastId = 0;

        while (true)
        {
            if (Queue.Count > 0 && QueueProcess.Count == 0)
            {
                QueueProcess.Add(Queue[0]);
                Queue.RemoveAt(0);
            }

            if (QueueProcess.Count > 0)
            {
                if (lastId == 0 || lastId != QueueProcess[0])
                {
                    lastId = QueueProcess[0];
                    aliveLastId = 0;
                }
                else if (lastId == QueueProcess[0])
                {
                    //124 = 1 minute - 60 sec
                    //2 = 1 sec
                    if (aliveLastId++ > 372)//3 minute wait
                    {
                        QueueProcess.Clear();
                        lastId = 0;
                        aliveLastId = 0;
                    }
                }
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
    /// <returns>false - если очередь не удалось дождаться и true - если мы смогли дождаться</returns>
    public bool WaitQueue()
    {
        var succesful = false;

        Task.WaitAll(new Task[] { Task.Run(async() =>
        {
            while (!QueueProcess.Any(_id => _id == TaskId))
                await Task.Delay(500);

            succesful = true;
        }) }, 190_000);

        return succesful;
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
        _socketClient = new SocketIOClient();
        _socket = _socketClient.Connect("http://localhost:3000/");

        var isConnected = false;

        Task.WaitAll(new Task[] { Task.Run(async() =>
        {
            while (!_socketClient.Connected)
                await Task.Delay(500);

            isConnected = true;
        }) }, 3_000);

        if (!isConnected)
            throw new Exception("Cant connect to server");

        _socket.On("data", HandlerDataRequests);

        _taskQueue.Add(TaskId);

        _socket.Emit("data",
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

            _socket.Emit("data",
                JsonConvert.SerializeObject(new ServerData()
                { Type = "sendText", Values = new List<object>() { $"{_nameSession}@{id}", $"{number.Replace("+", string.Empty)}@c.us", text } }));

            var result = false;

            Task.WaitAll(new Task[] { Task.Run(async() =>
            {
                while (_taskQueue.Contains(id))
                    await Task.Delay(100);

                result = true;
            }) }, 20_000);

            if (!result)
                return false;

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

        _socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
            { Type = "logout", Values = new List<object>() { $"{_nameSession}@{id}" } }));

        var result = false;

        Task.WaitAll(new Task[] { Task.Run(async() =>
            {
                while (_taskQueue.Contains(id))
                    await Task.Delay(100);

                result = true;
            }) }, 20_000);

        if (!result)
            throw new Exception("cant wait end operation");

        var data = _taskFinished[id];

        _taskFinished.Remove(id);
        
        _socket.RemoveListener("data", HandlerDataRequests);

        _socketClient.Disconnect();

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

        _socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
            { Type = "free", Values = new List<object>() { $"{_nameSession}@{id}" } }));

        var result = false;

        Task.WaitAll(new Task[] { Task.Run(async() =>
            {
                while (_taskQueue.Contains(id))
                    await Task.Delay(100);

                result = true;
            }) }, 20_000);

        if (!result)
            throw new Exception("cant wait end operation");

        var data = _taskFinished[id];

        _taskFinished.Remove(id);

        _socket.RemoveListener("data", HandlerDataRequests);

        _socketClient.Disconnect();

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

            _socket.Emit("data",
                JsonConvert.SerializeObject(new ServerData()
                { Type = "checkValidPhone", Values = new List<object>() { $"{_nameSession}@{id}", phone } }));

            var result = false;

            Task.WaitAll(new Task[] { Task.Run(async() =>
            {
                while (_taskQueue.Contains(id))
                    await Task.Delay(100);

                result = true;
            }) }, 20_000);

            if (!result)
                return false;

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