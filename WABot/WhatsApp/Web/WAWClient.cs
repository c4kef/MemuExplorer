namespace WABot.WhatsApp.Web;

public class WAWClient
{
    private readonly List<int> _taskQueue;
    private readonly Dictionary<int, JObject> _taskFinished;
    private readonly Random _random;

    private Namespace _socket = null!;
    private SocketIOClient _socketClient = null!;
    private static List<int> Queue = null!;
    private static List<int> QueueProcess = null!;
    public readonly int TaskId;
    public readonly string NameSession;
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Инициализация сессии
    /// </summary>
    /// <param name="nameSession">имя сессии</param>
    public WAWClient(string nameSession)
    {
        NameSession = nameSession;
        _taskQueue = new List<int>();
        _taskFinished = new Dictionary<int, JObject>();
        _random = new Random();
        TaskId = _random.Next(1_000_000, 10_000_000);
    }
    
    /// <summary>
    /// Добавление в очередь
    /// </summary>
    public void AddToQueue() => Queue.Add(TaskId);

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
                    if (aliveLastId++ > 400)
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
        var idTask = data["value"]![0]!.ToString();
        var id = int.Parse(idTask);

        if (_taskFinished.ContainsKey(id))
            _taskFinished.Remove(id);

        _taskFinished.Add(id, data);

        _taskQueue.RemoveAll(task => task == id);
    }

    /// <summary>
    /// Главный обработчик статуса с сервера
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    private void HandlerStateRequests(object[] args, Action<string> callback)
    {
        var builder = new StringBuilder();
        foreach (var line in args)
            builder.AppendLine(line.ToString());

        var data = JObject.Parse(builder.ToString());
        var val = data["value"]![0]!.ToString();
        IsConnected = val.Contains("CONNECTED");
    }

    /// <summary>
    /// Ждем своей очереди
    /// </summary>
    /// <returns>false - если очередь не удалось дождаться и true - если мы смогли дождаться</returns>
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
    public async Task Init(bool waitQr, string pathToWeb)
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
        _socket.On("state", HandlerStateRequests);

        _taskQueue.Add(TaskId);

        _socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
            { Type = "create", Values = new List<object>() { $"{NameSession}@{TaskId}", pathToWeb, waitQr } }));

        if (waitQr)
        {
            if (!await WaitQr())
            {
                _taskQueue.RemoveAll(task => task == TaskId);
                throw new Exception("cant wait qr code");
            }

            Globals.QrCodeName = TaskId.ToString();

            if (!await WaitRequest())
            {
                _taskQueue.RemoveAll(task => task == TaskId);
                throw new Exception("cant wait end operation");
            }

            Globals.QrCodeName = string.Empty;

            while (!TryDeleteQR())
                await Task.Delay(500);
        }
        else
        {
            if (!await WaitRequest())
            {
                _taskQueue.RemoveAll(task => task == TaskId);
                throw new Exception("cant wait end operation");
            }
        }

        var data = _taskFinished[TaskId];
        _taskFinished.Remove(TaskId);

        if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {(data["value"]?.Count() >= 2 ? data["value"]![1] : "undocumented error :(")}");

        async Task<bool> WaitRequest()
        {
            var status = 0;
            while (_taskQueue.Contains(TaskId) && status++ < 60)
                await Task.Delay(500);

            return status < 60;
        }
        async Task<bool> WaitQr()
        {
            var status = 0;
            while (!File.Exists(@$"{Globals.Setup.PathToQRs}\{TaskId}.png") && status++ < 60)
                await Task.Delay(500);

            return status < 60;
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
    public async Task<bool> SendText(string number, string text, FileInfo? image = null)
    {
        if (!IsConnected)
            return false;

        try
        {
            var id = _random.Next(1_000_000, 10_000_000);

            _taskQueue.Add(id);

            if (image != null && image.Exists)
                _socket.Emit("data",
                    JsonConvert.SerializeObject(new ServerData()
                    { Type = "sendText", Values = new List<object>() { $"{NameSession}@{id}", $"{number.Replace("+", string.Empty)}@c.us", image.FullName, text } }));
            else
                _socket.Emit("data",
                    JsonConvert.SerializeObject(new ServerData()
                    { Type = "sendText", Values = new List<object>() { $"{NameSession}@{id}", $"{number.Replace("+", string.Empty)}@c.us", text } }));

            while (_taskQueue.Contains(id))
                await Task.Delay(100);

            var data = _taskFinished[id];

            _taskFinished.Remove(id);

            if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
                throw new Exception($"Error: {data["value"]![1]}");

            return true;
        }
        catch (Exception ex)
        {
            Log.Write($"Ошибка отправки сообщения:\n{ex.Message}\n");
            return false;
        }
    }

    /// <summary>
    /// Разлогиниться
    /// </summary>
    /// <param name="removeDir">очищать кеш после закрытия?</param>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task Logout(bool removeDir)
    {
        var id = _random.Next(1_000_000, 10_000_000);

        _taskQueue.Add(id);

        _socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
            { Type = "logout", Values = new List<object>() { $"{NameSession}@{id}", removeDir } }));

        while (_taskQueue.Contains(id))
            await Task.Delay(100);

        var data = _taskFinished[id];

        _taskFinished.Remove(id);

        _socket.RemoveListener("data", HandlerDataRequests);
        _socket.RemoveListener("state", HandlerStateRequests);

        IsConnected = false;

        _socketClient.Disconnect();

        if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {data["value"]![1]}");
    }

    /// <summary>
    /// Освобождает текущею сессию из пула на сервере
    /// </summary>
    /// <param name="removeDir">очищать кеш после закрытия?</param>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task Free(bool removeDir)
    {
        var id = _random.Next(1_000_000, 10_000_000);

        _taskQueue.Add(id);

        _socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
            { Type = "free", Values = new List<object>() { $"{NameSession}@{id}", removeDir } }));

        while (_taskQueue.Contains(id))
            await Task.Delay(100);

        var data = _taskFinished[id];

        _taskFinished.Remove(id);

        _socket.RemoveListener("data", HandlerDataRequests);
        _socket.RemoveListener("state", HandlerStateRequests);
        
        IsConnected = false;

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
        if (!IsConnected)
            return false;

        var id = _random.Next(1_000_000, 10_000_000);

        _taskQueue.Add(id);

        _socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
            { Type = "checkValidPhone", Values = new List<object>() { $"{NameSession}@{id}", phone } }));

        while (_taskQueue.Contains(id))
            await Task.Delay(100);

        var data = _taskFinished[id];

        _taskFinished.Remove(id);

        if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {data["value"]![1]}");

        return (bool)(data["value"]![1] ?? false);
    }

    /// <summary>
    /// Проверяет контакт на наличие Whatsapp
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task<bool> WaitForInChat()
    {
        try
        {
            var id = _random.Next(1_000_000, 10_000_000);

            _taskQueue.Add(id);

            _socket.Emit("data",
                JsonConvert.SerializeObject(new ServerData()
                { Type = "waitForInChat", Values = new List<object>() { $"{NameSession}@{id}" } }));

            if (!await WaitRequest())
            {
                _taskQueue.RemoveAll(task => task == id);
                throw new Exception("cant wait end operation");
            }

            var data = _taskFinished[id];

            _taskFinished.Remove(id);

            if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
                throw new Exception($"Error: {data["value"]![1]}");

            return (bool)(data["value"]![1] ?? false);

            async Task<bool> WaitRequest()
            {
                var status = 0;
                while (_taskQueue.Contains(id) && status++ < 60)
                    await Task.Delay(500);

                return status < 60;
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Ошибка проверки номера:\n{ex.Message}\n");
            return false;
        }
    }
}