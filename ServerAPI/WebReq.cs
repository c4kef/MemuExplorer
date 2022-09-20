namespace Server;

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

    public static async Task<string> HttpPost(string url, Dictionary<string, string> values)
    {
        var handler = new HttpClientHandler();

        var request = new HttpClient(handler);

        request.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
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