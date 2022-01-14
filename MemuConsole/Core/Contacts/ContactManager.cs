namespace MemuConsole.Core.Contacts
{
    public class ContactManager
    {
        public string Export(List<CObj> contacts)
        {
            StringBuilder sbContacts = new StringBuilder();
            foreach (CObj contact in contacts)
                sbContacts.AppendLine(contact.GetContact());

            return sbContacts.ToString();
        }
        public async Task Import(int index, string pathToContacts)
        {
            await MemuCmd.ExecMemuc($@"-i {index} adb push {pathToContacts} /storage/emulated/0/contact.vcf");
            await MemuCmd.ExecMemuc($"-i {index} adb shell am start -t \"text/x-vcard\" -d \"file:///storage/emulated/0/contact.vcf\" -a android.intent.action.VIEW cz.psencik.com.android.contacts");

            Console.WriteLine($"[{index}] -> contacts imported");
        }
    }
}
