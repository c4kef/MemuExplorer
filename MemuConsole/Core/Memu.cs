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
            var answer = await MemuCmd.ExecMemuc("create");
            if (!answer.Contains("SUCCESS"))
                throw new Exception($"Error: {answer}");

            return int.Parse(answer.Split('\n')[1].Split(':')[1]);
        }
        /// <summary>
        /// Клонирование машины
        /// </summary>
        /// <param name="index">индекс машины</param>
        /// <returns>вернет индекс клонированной машины</returns>
        /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
        public static async Task<int> Clone(int index)
        {
            var answer = await MemuCmd.ExecMemuc($"clone -i {index}");
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
            var answer = await MemuCmd.ExecMemuc($"remove -i {index}");
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
            var answer = await MemuCmd.ExecMemuc($"start -i {index}");
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
            var answer = await MemuCmd.ExecMemuc($"stop -i {index}");
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
            var answer = await MemuCmd.ExecMemuc($"installapp -i {index} {path}");
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
            var answer = await MemuCmd.ExecMemuc($"startapp -i {index} {path}");
            if (!answer.Contains("SUCCESS"))
                throw new Exception($"Error: {answer}");
        }
    }
}
