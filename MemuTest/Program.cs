using MemuLib;
using MemuLib.Core.Contacts;

var contacts = new List<CObj>();

foreach (var contact in await File.ReadAllLinesAsync("real.txt"))
    contacts.Add(new CObj(Globals.RandomString(new Random().Next(10, 20)), $"+{contact}"));

foreach (var contact in (await File.ReadAllLinesAsync("fake.txt")).OrderBy(x => new Random().Next()).Take(new Random().Next(50, 100)))
    contacts.Add(new CObj(Globals.RandomString(new Random().Next(10, 20)), $"+{contact}"));


await File.WriteAllTextAsync("contacts.vcf", ContactManager.Export(contacts));
Console.WriteLine("OK");