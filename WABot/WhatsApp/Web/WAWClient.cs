namespace WABot.WhatsApp.Web;

public class WAWClient
{
    private readonly string _nameSession;
    private readonly List<int> _taskQueue;
    private readonly Dictionary<int, JObject> _taskFinished;
    private readonly Random _random;
    private readonly int _taskId;
    private bool _disposedCamera;
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
        _disposedCamera = false;
        _taskId = _random.Next(1_000_000, 10_000_000);

        Task.Run(Camera);

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
    /// Обработчик камеры, передает информацию на виртуалку ввиде qr кода
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    private async Task Camera()
    {
        while (!_disposedCamera)
        {
            if (!File.Exists(@$"{Globals.Setup.PathToQRs}\{_taskId}.png"))
                continue;

            var qr = ConvertTo24Bpp(System.Drawing.Image.FromFile(@$"{Globals.Setup.PathToQRs}\{_taskId}.png"));
            qr.Save($@"{Globals.TempDirectory.FullName}\{_taskId}.bmp", ImageFormat.Bmp);
            var bmpQr = new Bitmap($@"{Globals.TempDirectory.FullName}\{_taskId}.bmp");

            var virtualOutput = new VirtualOutput(bmpQr.Width, bmpQr.Height, 20, FourCC.FOURCC_24BG);
            var converter = new ImageConverter();
            var imageBytes = (byte[]) converter.ConvertTo(bmpQr, typeof(byte[]))!;
            var countSended = 0;

            while (countSended < 1_000 && !_disposedCamera)
            {
                virtualOutput.Send(imageBytes);
                countSended++;
            }
        }

        if (File.Exists(@$"{Globals.Setup.PathToQRs}\{_taskId}.png"))
            File.Delete(@$"{Globals.Setup.PathToQRs}\{_taskId}.png");

        Bitmap ConvertTo24Bpp(System.Drawing.Image img)
        {
            var bmp = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var gr = Graphics.FromImage(bmp);
            gr.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height));
            return bmp;
        }
    }

    /// <summary>
    /// Инициализировать
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task Init()
    {
        while (!QueueProcess.Any(_id => _id == _taskId))
            await Task.Delay(500);

        var socketIoClient = new SocketIOClient();

        _socket = socketIoClient.Connect("http://localhost:3000/");
        _socket.On("data", HandlerDataRequests);

        while (!socketIoClient.Connected)
            await Task.Delay(100);

        _taskQueue.Add(_taskId);

        _socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
            { Type = "create", Values = new List<object>() { $"{_nameSession}@{_taskId}" } }));

        while (_taskQueue.Contains(_taskId))
            await Task.Delay(100);

        var data = _taskFinished[_taskId];

        _taskFinished.Remove(_taskId);
        QueueProcess.Remove(_taskId);

        if ((int)(data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {data["value"]![1]}");
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
        _disposedCamera = true;//Dispose camera
        
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
        _disposedCamera = true;

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
        _disposedCamera = true;

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