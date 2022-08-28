using System.Linq;

var data = await File.ReadAllLinesAsync(@"C:\Users\artem\Downloads\test.txt");
var copedData = data.ToList();
for (int i = 0; i < data.Length; i++)
{
    copedData.RemoveAll(str => str.Split(';')[2] == data[i].Split(';')[2]);
    copedData.Add(data[i]);
}
await File.WriteAllLinesAsync("compared.txt", copedData.ToArray());