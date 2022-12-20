using System.Threading.Tasks;
var locker = new object();
var tasks = new List<Task>();
foreach (var _chatbot in new[] { 1, 2, 3, 4, 5, 6 })
{
    var chatbot = _chatbot;
    tasks.Add(Task.Run(async () =>
    {
        lock (locker)
        {
            Console.WriteLine("Hi " + chatbot);
        }
        Console.WriteLine(chatbot);
    }));
}
Task.WaitAll(tasks.ToArray(), -1);

