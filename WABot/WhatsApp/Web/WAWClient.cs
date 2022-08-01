namespace WABot.WhatsApp.Web;

public class WAWClient
{
    private readonly string _nameSession;
    private readonly List<int> _taskQueue;
    private readonly Dictionary<int, JObject> _taskFinished;
    private readonly Random _random;
    private readonly int _taskId;
    private Namespace _socket;

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
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task Init()
    {
        var socketIoClient = new SocketIOClient();

        _socket = socketIoClient.Connect("http://localhost:3000/");
        _socket.On("data", HandlerDataRequests);

        while (!socketIoClient.Connected)
            await Task.Delay(100);

        _taskQueue.Add(_taskId);

        _socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
            { Type = "create", Values = new List<object>() { $"{_nameSession}@{_taskId}" } }));

        while (!File.Exists(@$"{Globals.Setup.PathToQRs}\{_taskId}.png"))
            await Task.Delay(500);

        Globals.QrCodeName = _taskId.ToString();

        while (_taskQueue.Contains(_taskId))
            await Task.Delay(500);

        var data = _taskFinished[_taskId];

        Globals.QrCodeName = string.Empty;

        while (!TryDeleteQR())
            await Task.Delay(500);
        
        _taskFinished.Remove(_taskId);
        QueueProcess.Remove(_taskId);
        
        if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {data["value"]![1]}");

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
    /// Отправить сообщение
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task SendText(string number, string text)
    {
        var id = _random.Next(1_000_000, 10_000_000);

        _taskQueue.Add(id);

        _socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
            { Type = "sendText", Values = new List<object>() { $"{_nameSession}@{id}", $"{number}@c.us", text } }));

        while (_taskQueue.Contains(id))
            await Task.Delay(100);

        var data = _taskFinished[id];
        
        _taskFinished.Remove(id);

        if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {data["value"]![1]}");
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

        while (_taskQueue.Contains(id))
            await Task.Delay(100);

        var data = _taskFinished[id];

        _taskFinished.Remove(id);

        if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {data["value"]![1]}");
    }

    /// <summary>
    /// Освобождает сессию, требуется вызывать как заканчиваем работать с текущей сессией
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

        while (_taskQueue.Contains(id))
            await Task.Delay(100);

        var data = _taskFinished[id];

        _taskFinished.Remove(id);

        if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {data["value"]![1]}");
    }
}