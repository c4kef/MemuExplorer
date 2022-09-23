namespace UBot;

public struct SelectEmulatorScan
{
    public string Name { get; set; }
    public int Index { get; set; }
}

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

public struct ActionProfileWork
{
    public bool IsNewsLetter { get; set; }
    public bool IsWeb { get; set; }
    public bool CheckBan { get; set; }
    public bool Warm { get; set; }
    public bool Registration { get; set; }
    public bool Scaning { get; set; }
}

[Serializable]
public class AccountData
{
    /// <summary>
    /// Уровень прогрева аккаунта для начала рассылки сообщений
    /// </summary>
    public int TrustLevelAccount = 0;

    /// <summary>
    /// Кол-во сообщений
    /// </summary>
    public int CountMessages = 0;

    /// <summary>
    /// Дата создания
    /// </summary>
    public DateTime CreatedDate = DateTime.Now;

    /// <summary>
    /// Аккаунт первый начал переписку?
    /// </summary>
    public bool FirstMsg = false;
}
