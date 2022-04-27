using MemuLib.Core;

var client = new Client(1);
await client.Start();
Console.WriteLine(await client.ExistsElement(""));