namespace WABot.WhatsApp;

public struct ServerData
{
    public ServerData()
    {
        Values = new List<object>();
        Type = string.Empty;
    }
    
    public string Type;
    public List<object> Values;
}