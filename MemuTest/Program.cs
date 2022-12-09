using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Text;

var str = string.Join("${NL}", (await File.ReadAllLinesAsync("test.txt")).Select(a => string.Join("", a.Replace("\"", "").Replace("\'", "").ToCharArray())));
await File.WriteAllTextAsync("test.sh",
    $"NL=$'\\n' ; am start -a android.intent.action.SEND --es android.intent.extra.TEXT \"{str}\" -t text/plain -e jid '79772801086@s.whatsapp.net' --eu android.intent.extra.STREAM file:///storage/emulated/0/Download/1.jpg -p com.whatsapp.w4b");//mb fix

return;

var phones = new[] { "79772801086" };
var keyId = await WebReq.HttpPost("https://api.sendpulse.com/oauth/access_token", new Dictionary<string, string>()
{
    { "grant_type", "client_credentials" },
    { "client_id", "56e9f03fb38e05ebe097ab2b223c8bfc" },
    { "client_secret", "129769d6aeccc001f3ef90d0a568edca" }
});
Console.WriteLine(keyId);
var token = JObject.Parse(keyId)["access_token"].ToString();

Console.WriteLine(await WebReq.HttpPost("https://api.sendpulse.com/sms/send", new Dictionary<string, string>()
{
    { "sender", "C4ke" },
    { "phones", JsonConvert.SerializeObject(phones) },
    { "body", "Привет мир!" }
}, token));

public static class WebReq
{
    private const string UserAgent =
        @"Mozilla/5.0 (Windows; Windows NT 6.1) AppleWebKit/534.23 (KHTML, like Gecko) Chrome/11.0.686.3 Safari/534.23";

    public static async Task<string> HttpGet(string url)
    {
        var handler = new HttpClientHandler();

        var request = new HttpClient(handler);

        request.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        /*request.DefaultRequestHeaders.Add("Accept", $"application/json");
        request.DefaultRequestHeaders.Add("Authorization", $"Bearer 1eaed58689bde3c06bc6bf0f475ab205");*/

        return await (await request.GetAsync(url)).Content.ReadAsStringAsync();
    }

    public static async Task<string> HttpPost(string url, Dictionary<string, string> values, string token = "")
    {
        var handler = new HttpClientHandler();

        var request = new HttpClient(handler);

        request.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        request.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        /*request.DefaultRequestHeaders.Add("Accept", "application/json");
        request.DefaultRequestHeaders.Add("Authorization", $"Bearer 1eaed58689bde3c06bc6bf0f475ab205");*/

        var content = new FormUrlEncodedContent(values);

        return await (await request.PostAsync(url, content)).Content.ReadAsStringAsync();
    }

    public static async Task<HttpStatusCode> StatusCode(string url)
    {
        var handler = new HttpClientHandler();
        handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

        var request = new HttpClient(handler);
        request.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        /*request.DefaultRequestHeaders.Add("Accept", $"application/json");
        request.DefaultRequestHeaders.Add("Authorization", $"Bearer 1eaed58689bde3c06bc6bf0f475ab205");*/

        return (await request.GetAsync(url)).StatusCode;
    }
}