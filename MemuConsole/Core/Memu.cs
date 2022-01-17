namespace MemuConsole.Core
{
    public static class Memu
    {
        /// <summary>
        /// Проверка на существование машины
        /// </summary>
        /// <param name="index">индекс машины</param>
        /// <returns>true если есть, и false если нема</returns>
        public static async Task<bool> Exists(int index) => !string.IsNullOrEmpty(await MemuCmd.ExecMemuc($"listvms -i {index}"));
        /// <summary>
        /// Создание машины
        /// </summary>
        /// <returns>вернет индекс созданной машины</returns>
        /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
        public static async Task<int> Create()
        {
            string answer = await MemuCmd.ExecMemuc("create");
            if (!answer.Contains("SUCCESS"))
                throw new Exception($"Error: {answer}");

            return int.Parse(answer.Split('\n')[1].Split(':')[1]);
        }
        /// <summary>
        /// Удаление машины
        /// </summary>
        /// <param name="index">индекс машины</param>
        /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
        public static async Task Remove(int index)
        {
            string answer = await MemuCmd.ExecMemuc($"remove -i {index}");
            if (!answer.Contains("SUCCESS"))
                throw new Exception($"Error: {answer}");
        }
        /// <summary>
        /// Запуск машины
        /// </summary>
        /// <param name="index">индекс машины</param>
        /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
        public static async Task Start(int index)
        {
            string answer = await MemuCmd.ExecMemuc($"start -i {index}");
            if (!answer.Contains("SUCCESS"))
                throw new Exception($"Error: {answer}");
        }
        /// <summary>
        /// Остановка машины
        /// </summary>
        /// <param name="index">индекс машины</param>
        /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
        public static async Task Stop(int index)
        {
            string answer = await MemuCmd.ExecMemuc($"stop -i {index}");
            if (!answer.Contains("SUCCESS"))
                throw new Exception($"Error: {answer}");
        }
        /// <summary>
        /// Установка apk файла на машину
        /// </summary>
        /// <param name="index">индекс машины</param>
        /// <param name="path">путь до apk файла на локальной машине</param>
        /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
        public static async Task InstallApk(int index, string path)
        {
            string answer = await MemuCmd.ExecMemuc($"installapp -i {index} {path}");
            if (!answer.Contains("SUCCESS"))
                throw new Exception($"Error: {answer}");
        }
        /// <summary>
        /// Запуск apk на машине
        /// </summary>
        /// <param name="index">индекс машины</param>
        /// <param name="path">путь до apk файла на удаленной машине</param>
        /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
        public static async Task StartApk(int index, string path)
        {
            string answer = await MemuCmd.ExecMemuc($"startapp -i {index} {path}");
            if (!answer.Contains("SUCCESS"))
                throw new Exception($"Error: {answer}");
        }
        /// <summary>
        /// Спуф устройства
        /// </summary>
        /// <param name="index">индекс машины</param>
        public static async Task Spoof(int index)
        {
            string imei = RandomNumbers(15);
            string imsi = RandomNumbers(15);
            string number = $"+7{RandomNumbers(10)}";
            string simserial = RandomNumbers(20);
            string brand = RandomAlphabet(8);
            string manufacturer = RandomAlphabet(8);
            string model = RandomAlphabet(8);
            string mac = RandomMacAddress();

            Handler(await MemuCmd.ExecMemuc($"setconfigex -i {index} imei {imei}"));
            Handler(await MemuCmd.ExecMemuc($"setconfigex -i {index} imsi {imsi}"));
            Handler(await MemuCmd.ExecMemuc($"setconfigex -i {index} linenum {number}"));
            Handler(await MemuCmd.ExecMemuc($"setconfigex -i {index} simserial {simserial}"));
            Handler(await MemuCmd.ExecMemuc($"setconfigex -i {index} microvirt_vm_brand {brand}"));
            Handler(await MemuCmd.ExecMemuc($"setconfigex -i {index} microvirt_vm_manufacturer {manufacturer}"));
            Handler(await MemuCmd.ExecMemuc($"setconfigex -i {index} microvirt_vm_model {model}"));
            Handler(await MemuCmd.ExecMemuc($"setconfigex -i {index} macaddress {mac}"));

            string RandomNumbers(int length)
            {
                const string chars = "0123456789";
                return new string(Enumerable.Repeat(chars, length)
                    .Select(s => s[new Random().Next(s.Length)]).ToArray());
            }

            string RandomAlphabet(int length)
            {
                const string chars = "QWERTYUIOPASDFGHJKLZXCVBNMqwertyuiopasdfghjklzxcvbnm";
                return new string(Enumerable.Repeat(chars, length)
                    .Select(s => s[new Random().Next(s.Length)]).ToArray());
            }

            string RandomMacAddress()
            {
                var random = new Random();
                var buffer = new byte[6];
                random.NextBytes(buffer);
                var result = String.Concat(buffer.Select(x => string.Format("{0}-", x.ToString("X2"))).ToArray());
                return result.TrimEnd('-');
            }

            void Handler(string answer)
            {
                if (!answer.Contains("SUCCESS"))
                    throw new Exception($"Error: {answer}");
            }
        }
    }
}
