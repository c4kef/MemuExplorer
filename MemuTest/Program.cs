using MemuLib;
using MemuLib.Core.Contacts;
using System.Threading.Tasks;

int? countedDevice = 0;
for (int i = 0; i < 10; i++)
    Console.WriteLine(countedDevice is null or 0);