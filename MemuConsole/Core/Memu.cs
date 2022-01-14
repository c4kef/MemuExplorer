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
        public static async Task StartApk(int index, string path)
        {
            await Task.Run(async () =>
            {
                await MemuCmd.ExecFrida($"-U -f com.evozi.deviceid -l D:\frida-spoof.js --no-pause");
            });
        }
        /// <summary>
        /// Запуск фредирики
        /// </summary>
        /// <param name="index">индекс машины</param>
        /// <returns>ссылка на таск</returns>
        public static async Task<Thread?> StartFrida(int index)
        {
            Thread? tmpCurrent = null;
            await Task.Run(async () =>
            {
                tmpCurrent = Thread.CurrentThread;
                await MemuCmd.ExecMemuc($"-i {index} adb shell chmod +x ./data/local/tmp/frida-server");
                await MemuCmd.ExecMemuc($"-i {index} adb shell ./data/local/tmp/frida-server");
            });
            return tmpCurrent;
        }
        /// <summary>
        /// Установка фредирики
        /// </summary>
        /// <param name="index">индекс машины</param>
        public static async Task InstallFrida(int index) => await MemuCmd.ExecMemuc($@"-i {index} adb push {Settings.FridaServerPath} /data/local/tmp/frida-server");
    }
}
