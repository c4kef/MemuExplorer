namespace MemuConsole.Core.Contacts
{
    public class CObj
    {
        private string userName { get; set; }
        private string numberPhone { get; set; }
        private const string versionFile = "2.1";
        
        public CObj(string username, string phonenumber)
        {
            userName = username; 
            numberPhone = phonenumber;
        }

        public string GetContact()
        {
            StringBuilder formatContact = new StringBuilder();
            formatContact.AppendLine("BEGIN:VCARD");
            formatContact.AppendLine($"FN:{userName}");
            formatContact.AppendLine($"TEL;HOME;VOICE:{numberPhone}");
            formatContact.AppendLine($"VERSION:{versionFile}");
            formatContact.AppendLine("END:VCARD");
            return formatContact.ToString();
        }
    }
}
