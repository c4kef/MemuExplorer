namespace WABot.WhatsApp;

public class WaWebClient
{
    private readonly string _nameSession;
    private readonly Namespace _socket;
    private readonly List<int> _taskQueue;
    private readonly Dictionary<int, JObject> _taskFinished;
    private readonly Random _random;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);
    
    public WaWebClient(string nameSession)
    {
        _nameSession = nameSession;
        _taskQueue = new List<int>();
        _taskFinished = new Dictionary<int, JObject>();
        _random = new Random();

        var socketIoClient = new SocketIOClient();
        
        _socket = socketIoClient.Connect("http://localhost:3000/");
        _socket.On("data", HandlerDataRequests);
    }

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

    public async Task Init()
    {
        var id = _random.Next(1_000_000, 10_000_000);

        _taskQueue.Add(id);

        _socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
                {Type = "create", Values = new List<object>() {$"{_nameSession}@{id}"}}));

        while (_taskQueue.Contains(id))
            await Task.Delay(100);

        var data = _taskFinished[id];

        if ((int) (data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {data["value"]![1]}");
    }
    
    public async Task SendText(string number, string text)
    {
        var id = _random.Next(1_000_000, 10_000_000);

        _taskQueue.Add(id);

        _socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
                {Type = "sendText", Values = new List<object>() {$"{_nameSession}@{id}", $"{number}@c.us", text}}));

        while (_taskQueue.Contains(id))
            await Task.Delay(100);

        var data = _taskFinished[id];

        if ((int) (data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {data["value"]![1]}");
    }
    
    public async Task Logout()
    {
        var id = _random.Next(1_000_000, 10_000_000);

        _taskQueue.Add(id);

        _socket.Emit("data",
            JsonConvert.SerializeObject(new ServerData()
                {Type = "logout", Values = new List<object>() {$"{_nameSession}@{id}"}}));

        while (_taskQueue.Contains(id))
            await Task.Delay(100);

        var data = _taskFinished[id];

        if ((int) (data["status"] ?? throw new InvalidOperationException()) != 200)
            throw new Exception($"Error: {data["value"]![1]}");
    }
}