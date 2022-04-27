using System.Text.RegularExpressions;

var text =
    "Mama s papoy||mamoy myli ramu.\nPapa s mamoy||papoy yeli ramu\nKhuy poymesh' kto-yest' kto, no PApa||mama kuda luchshe";

Console.WriteLine(text);

Console.WriteLine("\nBefore\n");
Console.WriteLine(SelectWord(text));

string SelectWord(string value)
{
    var backValue = value;
    foreach (var match in new Regex(@"(\w+)\|\|(\w+)").Matches(backValue))
        backValue = backValue.Replace(match.ToString()!,  match.ToString()!.Split("||")[new Random().Next(0, 100) >= 50 ? 1 : 0]);

    return backValue;
}