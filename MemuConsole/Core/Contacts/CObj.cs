namespace MemuConsole.Core.Contacts
{
    public class CObj
    {
        /// <summary>
        /// Имя пользователя (отображение в контактах)
        /// </summary>
        private string userName { get; set; }
        /// <summary>
        /// Номер телефона (отображение в контактах)
        /// </summary>
        private string numberPhone { get; set; }
        /// <summary>
        /// Меньше знаешь крепче спишь (а если серьезно версия разметки)
        /// </summary>
        private const string versionFile = "2.1";
        /// <summary>
        /// Инициализация контакта
        /// </summary>
        /// <param name="username">имя пользователя</param>
        /// <param name="phonenumber">номер телефона</param>
        public CObj(string username, string phonenumber)
        {
            userName = username; 
            numberPhone = phonenumber;
        }

        /// <summary>
        /// Получает контакт ввиде строчки
        /// </summary>
        /// <returns>образ отфармотированный в строку</returns>
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
