namespace MemuConsole.Core.Contacts;

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

    /// <summary>
    /// Импорт контаков в приложение
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <param name="pathToContacts">путь до файла с контактами</param>
    public static async Task Import(int index, string pathToContacts)
    {
        await MemuCmd.ExecMemuc($@"-i {index} adb push {pathToContacts} /storage/emulated/0/contact.vcf");
        await MemuCmd.ExecMemuc(
            $"-i {index} adb shell am start -t \"text/x-vcard\" -d \"file:///storage/emulated/0/contact.vcf\" -a android.intent.action.VIEW cz.psencik.com.android.contacts");

        Console.WriteLine($"[{index}] -> contacts imported");
    }
}