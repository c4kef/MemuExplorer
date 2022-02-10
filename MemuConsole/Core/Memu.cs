namespace MemuConsole.Core;

public static class Memu
{
    /// <summary>
    /// Запуск ADB сервера
    /// </summary>
    /// <exception cref="Exception">ошибка при запуске, допустим зависло или х** его знает что еще</exception>
    public static void RunAdbServer()
    {
        if (AdbServer.Instance.GetStatus().IsRunning)
            return;

        var server = new AdbServer();
        var result = server.StartServer($@"{Settings.BaseDir}\adb.exe", false);

        if (result != StartServerResult.Started)
            throw new Exception("Can't start adb server");
    }

    /// <summary>
    /// Проверка на существование машины
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <returns>true если есть, и false если нема</returns>
    public static async Task<bool> Exists(int index) =>
        !string.IsNullOrEmpty(await MemuCmd.ExecMemuc($"listvms -i {index}"));

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
        var newPath = $@"{new FileInfo(path).Directory?.FullName}\{new Random().Next(1_000_000, 5_000_000)}.apk";
        File.Copy(path,newPath);//Решает проблему загруженности
        
        var answer = await MemuCmd.ExecMemuc($"installapp -i {index} {newPath}");
        
        if (!answer.Contains("SUCCESS"))
            throw new Exception($"Error: {answer}");
        
        File.Delete(newPath);//Удалем копию, ибо нехер засирать
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

    /// <summary>
    /// Завершение apk на машине
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <param name="path">путь до apk файла на удаленной машине</param>
    /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
    public static async Task StopApk(int index, string path)
    {
        var answer = await MemuCmd.ExecMemuc($"stopapp -i {index} {path}");
        if (!answer.Contains("SUCCESS"))
            throw new Exception($"Error: {answer}");
    }

    /// <summary>
    /// Отправка на удаленку
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <param name="local">путь на локальной машине</param>
    /// <param name="remote">путь на удаленной машине</param>
    public static async Task Push(int index, string local, string remote) =>
        await MemuCmd.ExecMemuc($"-i {index} adb push {local} {remote}");

    /// <summary>
    /// Загрузка с удаленки
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <param name="local">путь на локальной машине</param>
    /// <param name="remote">путь на удаленной машине</param>
    public static async Task Pull(int index, string local, string remote) =>
        await MemuCmd.ExecMemuc($"-i {index} adb pull {remote} {local}");
}