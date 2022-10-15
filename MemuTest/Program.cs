using System.Text;

Console.OutputEncoding = Console.InputEncoding = Encoding.Unicode;

while (true)
{
    var document = await File.ReadAllTextAsync(@"C:\Users\artem\Downloads\MEmu Download\window_dump.xml");
    var xpath = Console.ReadLine()!.Replace(@"\", "");

    if (document.Contains(xpath))
    {
        string Cord = document.Split(xpath)[1].Split("bounds=\"")[1].Split('\"')[0].Replace("[", "");

        Console.WriteLine($"{(Convert.ToInt32(Cord.Split(']')[0].Split(',')[0]) + Convert.ToInt32(Cord.Split(']')[1].Split(',')[0])) / 2}, {(Convert.ToInt32(Cord.Split(']')[0].Split(',')[1]) + Convert.ToInt32(Cord.Split(']')[1].Split(',')[1])) / 2}");
        continue;
    }

    Console.WriteLine("Not found");
}
/*using PuppeteerSharp;

using var browserFetcher = new BrowserFetcher();
await browserFetcher.DownloadAsync();
await using var browser = await Puppeteer.LaunchAsync(
    new LaunchOptions { Headless = false, Args = new string[] { 
    
        // `--app=${WAUrl}`,
    "--log-level=3", // fatal only
    "--start-maximized",
    "--no-default-browser-check",
    "--disable-site-isolation-trials",
    "--no-experiments",
    "--ignore-gpu-blacklist",
    "--ignore-certificate-errors",
    "--ignore-certificate-errors-spki-list",
    "--disable-gpu",
    "--disable-extensions",
    "--disable-default-apps",
    "--enable-features=NetworkService",
    "--disable-setuid-sandbox",
    "--no-sandbox",
    // Extras
    "--disable-webgl",
    "--disable-infobars",
    "--window-position=0,0",
    "--ignore-certifcate-errors",
    "--ignore-certifcate-errors-spki-list",
    "--disable-threaded-animation",
    "--disable-threaded-scrolling",
    "--disable-in-process-stack-traces",
    "--disable-histogram-customizer",
    "--disable-gl-extensions",
    "--disable-composited-antialiasing",
    "--disable-canvas-aa",
    "--disable-3d-apis",
    "--disable-accelerated-2d-canvas",
    "--disable-accelerated-jpeg-decoding",
    "--disable-accelerated-mjpeg-decode",
    "--disable-app-list-dismiss-on-blur",
    "--disable-accelerated-video-decode",
    "--user-data-dir=C:\\Users\\artem\\source\\repos\\MemuExplorer\\UBot\\bin\\Debug\\net6.0-windows10.0.19041.0\\win10-x64\\WppConnect\\test",
    "--disable-dev-shm-usage",
    "--proxy-server=https=217.29.62.212:10451"
    } });
await using var page = await browser.NewPageAsync();
//await page.EvaluateExpressionOnNewDocumentAsync(await File.ReadAllTextAsync(@"C:\Users\artem\Downloads\removeWorkers.js"));
//await page.SetRequestInterceptionAsync(true);
await page.AuthenticateAsync(new Credentials()
{
    Username = "C37jer",
    Password = "dSEdPU"
});
//page.Request += Page_Request;
await page.GoToAsync("https://web.whatsapp.com");
await page.ReloadAsync();

while (true)
{
}

async void Page_Request(object? sender, RequestEventArgs e)
{
    var req = e.Request;
    Console.WriteLine($"Url: {req.Url}");
    if (req.Url.StartsWith("https://web.whatsapp.com/check-update"))
    {
        await req.AbortAsync();
        return;
    }
    if (req.Url != "https://web.whatsapp.com/")
    {
        await req.ContinueAsync();
        return;
    }

    Console.WriteLine("Redirect");
    await req.RespondAsync(new ResponseData
    {
        Status = System.Net.HttpStatusCode.OK,
        ContentType = "text/html",
        Body = await File.ReadAllTextAsync(@"C:\Users\artem\Downloads\2.2238.7-beta.html")
    });
}*/