using PuppeteerSharp;

namespace WPP4DotNet.WebDriver
{
    public class ChromeWebApp : IWpp
    {
        /// <summary>
        /// 
        /// </summary>
        LaunchOptions ChromeOpt;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hidden"></param>
        /// <param name="path"></param>
        /// <param name="proxy">В формате IP:PORT</param>
        public ChromeWebApp(bool hidden = true, string path = "", string proxy = "")
        {
            ChromeOpt = new LaunchOptions();

            ChromeOpt.ExecutablePath = @$"{Directory.GetCurrentDirectory()}\Chromium\chrome.exe";
            ChromeOpt.Headless = hidden;
            ChromeOpt.UserDataDir = path;

            var args = new List<string>();
            args.Add("--log-level=3");
            args.Add("--no-default-browser-check");
            args.Add("--disable-site-isolation-trials");
            args.Add("--no-experiments");
            args.Add("--ignore-gpu-blacklist");
            args.Add("--ignore-ssl-errors");
            args.Add("--ignore-certificate-errors");
            args.Add("--ignore-certificate-errors-spki-list");
            args.Add("--disable-gpu");
            args.Add("--disable-extensions");
            args.Add("--disable-default-apps");
            args.Add("--enable-features=NetworkService");
            args.Add("--disable-setuid-sandbox");
            args.Add("--no-sandbox");
            args.Add("--disable-webgl");
            args.Add("--disable-threaded-animation");
            args.Add("--disable-threaded-scrolling");
            args.Add("--disable-in-process-stack-traces");
            args.Add("--disable-histogram-customizer");
            args.Add("--disable-gl-extensions");
            args.Add("--disable-composited-antialiasing");
            args.Add("--disable-canvas-aa");
            args.Add("--disable-3d-apis");
            args.Add("--disable-accelerated-2d-canvas");
            args.Add("--disable-accelerated-jpeg-decoding");
            args.Add("--disable-accelerated-mjpeg-decode");
            args.Add("--disable-app-list-dismiss-on-blur");
            args.Add("--disable-accelerated-video-decode");
            args.Add("--disable-infobars");
            args.Add("--ignore-certifcate-errors");
            args.Add("--ignore-certifcate-errors-spki-list");
            args.Add("--disable-dev-shm-usage");
            args.Add("--disable-gl-drawing-for-tests");
            //args.Add("--incognito");
            //args.Add("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.114 Safari/537.36");
            args.Add("--disable-web-security");
            args.Add("--aggressive-cache-discard");
            args.Add("--disable-cache");
            args.Add("--disable-application-cache");
            args.Add("--disable-offline-load-stale-cache");
            args.Add("--disk-cache-size=0");
            args.Add("--disable-background-networking");
            args.Add("--disable-sync");
            args.Add("--disable-translate");
            args.Add("--hide-scrollbars");
            args.Add("--metrics-recording-only");
            args.Add("--mute-audio");
            args.Add("--no-first-run");
            args.Add("--safebrowsing-disable-auto-update");
            args.Add("--no-zygote");
            args.Add("--js-flags=\"--max-old-space-size=4096\"");
            args.Add("--js-flags=--max-old-space-size=4096");
            args.Add("--js-flags= --max-old-space-size=4096");
            args.Add("--window-size=800,600");

            if (!string.IsNullOrEmpty(proxy))
                args.Add($"--proxy-server=https={proxy}");

            ChromeOpt.Args = args.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        public override async Task StartSession(Credentials? auth = null)
        {
            CheckDriverStarted();

            var drive = await Puppeteer.LaunchAsync(ChromeOpt);

            await base.StartSession(drive, auth);
        }
    }
}
