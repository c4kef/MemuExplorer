using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemuConsole.Core
{
    public class Client
    {
        /// <summary>
        /// Индекс машины
        /// </summary>
        private int _index;
        /// <summary>
        /// Объявление образа машины
        /// </summary>
        /// <param name="index">индекс машины</param>
        public Client(int index)
        {
            _index = index;
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
            Console.WriteLine($"[{_index}] -> apk runned");
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
            Console.WriteLine($"[{_index}] -> input tap {x} {y}");
        }
        /*/// <summary>
        /// Спуфинг машины
        /// </summary>
        public async Task Spoof()
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return;
            }

            await Memu.Spoof(_index);
            Console.WriteLine($"[{_index}] -> vm spoofed");
        }*/
    }
}