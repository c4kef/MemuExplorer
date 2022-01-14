namespace MemuConsole
{
    public static class Settings
    {
        /// <summary>
        /// Путь до папки с мемом
        /// </summary>
        public const string BaseDir = @"D:\Program Files\Microvirt\MEmu\";
        /// <summary>
        /// Путь до папки с приложениями для установки
        /// </summary>
        public const string AppsDir = @"C:\Users\artem\source\repos\MVP\MemuExplorer\Apps";
        /// <summary>
        /// Путь до сервера фредирики
        /// </summary>
        public const string FridaServerPath = @"C:\Users\artem\source\repos\MVP\MemuExplorer\Mobile\frida-server";
        /// <summary>
        /// Путь до фредерики (ну если эта херня в системных переменых не записалась... то мы знаем где её искать)
        /// </summary>
        public const string FridaPath = @"C:\Users\artem\AppData\Local\Packages\PythonSoftwareFoundation.Python.3.9_qbz5n2kfra8p0\LocalCache\local-packages\Python39\Scripts";
    }
}