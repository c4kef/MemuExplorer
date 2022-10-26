Console.WriteLine("Hello".Remove(0, "Hello".Length - 1));

/*using System.Text;
//eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJodHRwczpcL1wvbWlrYXdpc2UuY29tXC9ydV9ydVwvIiwiaWF0IjoxNjY2NDU2NjIyLCJuYmYiOjE2NjY0NTY2MjIsImV4cCI6MTY2NzA2MTQyMiwiZGF0YSI6eyJ1c2VyIjp7ImlkIjoiNzIifX19.1SCeDJf9rBWAfid5aAXt7NK7PMKnagc2Z0jtfDp7usI
Console.WriteLine(await GetAsync("https://mikawise.com/wp-json/api/userdata"));

 async Task<HttpResponseMessage> HttpPost(string url, Dictionary<string, string> values)
{
    HttpClientHandler handler = new HttpClientHandler();
    HttpClient request = new HttpClient(handler);

    request.DefaultRequestHeaders.Add("Authorization", "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJodHRwczpcL1wvbWlrYXdpc2UuY29tXC9ydV9ydVwvIiwiaWF0IjoxNjY2NDU2NjIyLCJuYmYiOjE2NjY0NTY2MjIsImV4cCI6MTY2NzA2MTQyMiwiZGF0YSI6eyJ1c2VyIjp7ImlkIjoiNzIifX19.1SCeDJf9rBWAfid5aAXt7NK7PMKnagc2Z0jtfDp7usI");
    request.DefaultRequestHeaders.UserAgent.ParseAdd(@"Mozilla/5.0 (Windows; Windows NT 6.1) AppleWebKit/534.23 (KHTML, like Gecko) Chrome/11.0.686.3 Safari/534.23");

    FormUrlEncodedContent content = new FormUrlEncodedContent(values);

    return await request.PostAsync(url, content);
}

 async Task<string> GetAsync(string url, int timeout = 10)
{
        HttpClient request = new HttpClient();

        request.Timeout = TimeSpan.FromSeconds(timeout);

    request.DefaultRequestHeaders.Add("Authorization", "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJodHRwczpcL1wvbWlrYXdpc2UuY29tXC9ydV9ydVwvIiwiaWF0IjoxNjY2NDU2NjIyLCJuYmYiOjE2NjY0NTY2MjIsImV4cCI6MTY2NzA2MTQyMiwiZGF0YSI6eyJ1c2VyIjp7ImlkIjoiNzIifX19.1SCeDJf9rBWAfid5aAXt7NK7PMKnagc2Z0jtfDp7usI");
        request.DefaultRequestHeaders.UserAgent.ParseAdd(@"Mozilla/5.0 (Windows; Windows NT 6.1) AppleWebKit/534.23 (KHTML, like Gecko) Chrome/11.0.686.3 Safari/534.23");

        HttpResponseMessage response = await request.GetAsync(url);

        //Bypass UTF8 error encoding
        byte[] buf = await response.Content.ReadAsByteArrayAsync();
        return Encoding.UTF8.GetString(buf);
    return string.Empty;
}
*/