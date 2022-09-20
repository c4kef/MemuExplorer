using HttpMultipartParser;
using ServerAPI;

namespace Server.Api;

internal class Global : WebApiController
{
    [Route(HttpVerbs.Get, "/sendMessage/{number}&{text}")]
    public async Task<string> SendMessage(string number, string text)
    {
        try
        {
            var accountsWeb = await GetAccounts();

            if (accountsWeb.Length == 0)
                return JsonConvert.SerializeObject("accounts not found");

            var (phone, path) = accountsWeb[0];

            var waw = new WAWClient(phone);

            try
            {
                await waw.Init(path);
                if (!await waw.WaitForInChat())
                    throw new Exception("Cant connect");
                if (!waw.IsConnected)
                    throw new Exception("Is not connected");
            }
            catch (Exception ex)//Скорее всего аккаунт уже не валидный
            {
                await waw.Free();
                waw.RemoveQueue();
                return JsonConvert.SerializeObject($"account cant login: {ex.Message}");
            }

            var countMsg = 0;

            var contact = number;

            if (string.IsNullOrEmpty(contact))
            {
                await waw.Free();
                return JsonConvert.SerializeObject($"number is empty");
            }

            if (!waw.IsConnected)
            {
                await waw.Free();
                return JsonConvert.SerializeObject($"account is not avaible, try again");
            }

            try
            {
                if (!await waw.CheckValidPhone(contact))
                    return JsonConvert.SerializeObject($"number is not valid for whatsapp");

            }
            catch(Exception ex)
            {
                return JsonConvert.SerializeObject($"error check number: {ex.Message}");
            }

            var messageSended = await waw.SendText(contact, text);

            if (messageSended)
            {
                Console.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss};{phone.Remove(5, phone.Length - 5)};{contact}");

                await waw.Free();
            }
            else
                return JsonConvert.SerializeObject($"error send message, try again");

            return JsonConvert.SerializeObject($"OK");
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject($"Error send text: {ex.Message}");
        }
    }

    public static async Task<(string phone, string path)[]> GetAccounts()
    {
        var accounts = new List<(string phone, string path)>();

        Directory.CreateDirectory("Accounts");

        foreach (var accountDirectory in Directory.GetDirectories("Accounts"))
        {
            if (!File.Exists($@"{accountDirectory}\Data.json") && Directory.GetFiles(accountDirectory).Any(file => file.Contains("data.json")))
                continue;

            var phone = new DirectoryInfo(accountDirectory).Name;

            accounts.Add((phone, accountDirectory));
        }

        return accounts.ToArray();
    }
}