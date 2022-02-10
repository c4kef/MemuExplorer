using System.Text.RegularExpressions;

namespace MemuConsole.Core
{
    public class Client
    {
        /// <summary>
        /// Индекс машины
        /// </summary>
        private readonly int _index;
        /// <summary>
        /// Образ ADB клиента
        /// </summary>
        private readonly AdvancedAdbClient _adbClient;
        /// <summary>
        /// Образ устройства
        /// </summary>
        private DeviceData? _device;
        /// <summary>
        /// Объявление образа машины
        /// </summary>
        /// <param name="index">индекс машины</param>
        public Client(int index)
        {
            _index = index;
            _adbClient = new AdvancedAdbClient();
        }
        /// <summary>
        /// Запуск машины
        /// </summary>
        public async Task Start()
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return;
            }

            await Memu.Start(_index);
            
            var host = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}:\d{1,5}\b").Match(await MemuCmd.ExecMemuc($"-i {_index} adb start-server")).Value;
            
            if (string.IsNullOrEmpty(host))
            {
                await MemuCmd.ExecMemuc($"-i {_index} adb kill-server");
                throw new Exception($"[{_index}] Can't start server");
            }
            
            _adbClient.Connect(host);
            _device = _adbClient.GetDevices().FirstOrDefault() ?? null;
            
            if (_device is null)
                throw new Exception($"[{_index}] Can't connect to device");
            
            await Task.Delay(Settings.WaitingSecs);

            Console.WriteLine($"[{_index}] -> VM started");
        }
        /// <summary>
        /// Остановка машины
        /// </summary>
        public async Task Stop()
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return;
            }

            await Memu.Stop(_index);

            Console.WriteLine($"[{_index}] -> VM stoped");
        }
        /// <summary>
        /// Установка приложения на машину
        /// </summary>
        /// <param name="path">путь до приложения на локальном компе</param>
        public async Task InstallApk(string path)
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return;
            }

            if (!File.Exists(path))
            {
                Console.WriteLine($"[{_index}] -> apk file not found");
                return;
            }

            await Memu.InstallApk(_index, path);

            Console.WriteLine($"[{_index}] -> installed apk");
        }
        /// <summary>
        /// Запуск приложения на машине
        /// </summary>
        /// <param name="comPath">com-путь до установленного приложения на машине</param>
        public async Task RunApk(string comPath)
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return;
            }

            await Memu.StartApk(_index, comPath);
            await Task.Delay(Settings.WaitingSecs);

            Console.WriteLine($"[{_index}] -> apk runned");
        }
        /// <summary>
        /// Остановка приложения на машине
        /// </summary>
        /// <param name="comPath">com-путь до установленного приложения на машине</param>
        public async Task StopApk(string comPath)
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return;
            }

            await Memu.StopApk(_index, comPath);
            await Task.Delay(Settings.WaitingSecs);

            Console.WriteLine($"[{_index}] -> apk stopped");
        }
        /// <summary>
        /// Симуляция кликов по экрану
        /// </summary>
        /// <param name="x">по горизонтали</param>
        /// <param name="y">по вертикали</param>
        public async Task Click(int x, int y)
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return;
            }

            await MemuCmd.ExecMemuc($"-i {_index} adb shell input tap {x} {y}");
            await Task.Delay(500);

            Console.WriteLine($"[{_index}] -> input tap {x} {y}");
        }
        /// <summary>
        /// Симуляция кликов по экрану
        /// </summary>
        /// <param name="uiElement">название элемента в интерфейсе</param>
        public async Task Click(string uiElement)
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return;
            }
            
            var element = _adbClient.FindElement(_device, uiElement, TimeSpan.FromSeconds(0.5f));
            
            if (element is null)
                throw new Exception($"[{_index}] Can't found element by name \"{uiElement}\"");

            element.Click();
            
            Console.WriteLine($"[{_index}] -> input tap uiElement");
        }
        /// <summary>
        /// Отправка на удаленку
        /// </summary>
        /// <param name="local">путь на локальной машине</param>
        /// <param name="remote">путь на удаленной машине</param>
        public async Task Push(string local, string remote)
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return;
            }

            await Memu.Push(_index, local, remote);

            Console.WriteLine($"[{_index}] -> files pushed");
        }
        /// <summary>
        /// Загрузка с удаленки
        /// </summary>
        /// <param name="local">путь на локальной машине</param>
        /// <param name="remote">путь на удаленной машине</param>
        public async Task Pull(string local, string remote)
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return;
            }

            await Memu.Pull(_index, local, remote);
            
            Console.WriteLine($"[{_index}] -> files pushed");
        }
        /// <summary>
        /// Получение разрешения экрана
        /// </summary>
        /// <returns>Разрешение экрана</returns>
        public async Task<Point> GetResoultion()
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return Point.Empty;
            }

            var x = int.Parse((await MemuCmd.ExecMemuc($"-i {_index} getconfigex resolution_width")).Split(' ')[1]);
            var y = int.Parse((await MemuCmd.ExecMemuc($"-i {_index} getconfigex resolution_height")).Split(' ')[1]);
            
            Console.WriteLine($"[{_index}] -> requested resolution VM");

            return new Point(x, y);
        }
    }
}