namespace MemuLib.Core.Contacts;

public class ContactManager
{
    /// <summary>
    /// Экспорт контактов из образов
    /// </summary>
    /// <param name="contacts">список образов</param>
    /// <returns>строчку готовую к сохранению</returns>
    public static string Export(List<CObj> contacts)
    {
        var sbContacts = new StringBuilder();
        foreach (var contact in contacts)
            sbContacts.AppendLine(contact.GetContact());

        return sbContacts.ToString();
    }
}