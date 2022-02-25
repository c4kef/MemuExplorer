namespace MemuLib.Core;

public static class WebReq
{
    /// <summary>
    /// Заголовок типа браузера
    /// </summary>
    private const string UserAgent = @"Mozilla/5.0 (Windows; Windows NT 6.1) AppleWebKit/534.23 (KHTML, like Gecko) Chrome/11.0.686.3 Safari/534.23";

    /// <summary>
    /// Отправляем запрос на сервер в формате Get
    /// </summary>
    /// <param name="url">ссылка на запрос</param>
    /// <returns>ответ от сервера</returns>
    public static async Task<string> HttpGet(string url)
    {
        var handler = new HttpClientHandler();

        var request = new HttpClient(handler);
            
        request.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        request.DefaultRequestHeaders.Add("Accept", $"application/json");
        request.DefaultRequestHeaders.Add("Authorization", $"Bearer {Settings.FSimApi}");

        return await (await request.GetAsync(url)).Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Отправляем запрос на сервер в формате Post
    /// </summary>
    /// <param name="url">ссылка на запрос</param>
    /// <param name="values">параметры</param>
    /// <returns>ответ от сервера</returns>
    public static async Task<string> HttpPost(string url, Dictionary<string, string> values)
    {
        var handler = new HttpClientHandler();

        var request = new HttpClient(handler);

        request.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        request.DefaultRequestHeaders.Add("Accept", "application/json");
        request.DefaultRequestHeaders.Add("Authorization", $"Bearer {Settings.FSimApi}");

        var content = new FormUrlEncodedContent(values);

        return await (await request.PostAsync(url, content)).Content.ReadAsStringAsync();
    }
}