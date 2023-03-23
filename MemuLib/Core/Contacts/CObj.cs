namespace MemuLib.Core.Contacts;

public class CObj
{
    /// <summary>
    /// Имя пользователя (отображение в контактах)
    /// </summary>
    public string UserName { get; private set; }

    /// <summary>
    /// Номер телефона (отображение в контактах)
    /// </summary>
    public string NumberPhone { get; private set; }

    /// <summary>
    /// Меньше знаешь крепче спишь (а если серьезно версия разметки)
    /// </summary>
    private const string VersionFile = "2.1";

    /// <summary>
    /// Инициализация контакта
    /// </summary>
    /// <param name="username">имя пользователя</param>
    /// <param name="phonenumber">номер телефона</param>
    public CObj(string username, string phonenumber)
    {
        UserName = username;
        NumberPhone = phonenumber;
    }

    /// <summary>
    /// Получает контакт ввиде строчки
    /// </summary>
    /// <returns>образ отфармотированный в строку</returns>
    public string GetContact()
    {
        var formatContact = new StringBuilder();
        formatContact.AppendLine("BEGIN:VCARD");
        formatContact.AppendLine($"FN:{UserName}");
        formatContact.AppendLine($"TEL;HOME;VOICE:{NumberPhone}");
        formatContact.AppendLine($"VERSION:{VersionFile}");
        formatContact.AppendLine("END:VCARD");
        return formatContact.ToString();
    }
}