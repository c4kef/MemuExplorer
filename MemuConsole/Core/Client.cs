using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemuConsole.Core
{
    public class Client
    {
        private int _index;
        private Thread? _frida;

        public Client(int index)
        {
            _index = index;
        }

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

        public async Task Stop()
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return;
            }

            await Memu.Stop(_index);
            _frida?.Abort();//TO-DO
            Console.WriteLine($"[{_index}] -> VM stoped");
        }

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

        public async Task ExecMemucuteAdb(string comPath)
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return;
            }

            await Memu.StartApk(_index, comPath);
            Console.WriteLine($"[{_index}] -> apk runned");
        }

        public async Task SetupFrida()
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return;
            }

            await Memu.InstallFrida(_index);
            _frida = await Memu.StartFrida(_index);

            Console.WriteLine($"[{_index}] -> frida runned");
        }

        public async Task SetupContacts()
        {
            if (!await Memu.Exists(_index))
            {
                Console.WriteLine($"[{_index}] -> VM not found");
                return;
            }

            await Memu.InstallFrida(_index);
            _frida = await Memu.StartFrida(_index);

            Console.WriteLine($"[{_index}] -> frida runned");
        }
    }
}