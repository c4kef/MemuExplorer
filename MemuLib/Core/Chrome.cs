using OpenQA.Selenium;

namespace MemuLib.Core;

public class Chrome
{
    /// <summary>
    /// Образ веб драйвера (хром)
    /// </summary>
    private readonly ChromeDriver _webDriver;
    
    /// <summary>
    /// Объявляем класс и инициализируем настройки
    /// </summary>
    public Chrome(string PathToCache)
    {
        var options = new ChromeOptions();
        var service = ChromeDriverService.CreateDefaultService();
        service.HideCommandPromptWindow = true;
        options.AddArgument("no-sandbox");
        options.AddArgument($"--user-data-dir={PathToCache}");
        options.AddArgument("remote-debugging-port=0");
        options.AddArgument("disable-extensions");
        _webDriver = new ChromeDriver(service, options);
    }

    /// <summary>
    /// Запуск страницы
    /// </summary>
    public void Start(string url) => _webDriver.Navigate().GoToUrl(url);

    /// <summary>
    /// Остановка работы
    /// </summary>
    public void Stop()
    {
        _webDriver.Close();
        _webDriver.Dispose();
    }

    public async Task<IWebElement> FindElement(By by, bool check = true)
    {
        while (!IsElementExist(by) && check)
            await Task.Delay(500);

        return _webDriver.FindElement(by);
    }

    public bool IsElementExist(By by)
    {
        try
        {
            var element = _webDriver.FindElement(by);
            return element.Displayed;
        }
        catch (NoSuchElementException)
        {
            return false;
        }
    }

    /// <summary>
    /// Устанавливаем размер окна хрома
    /// </summary>
    /// <param name="size">передаем размер</param>
    public void SetSize(Point size) => _webDriver.Manage().Window.Size = new Size(size);

    /// <summary>
    /// Устанавливаем позицию окна хрома
    /// </summary>
    /// <param name="pos">передаем позицию</param>
    public void SetPosition(Point pos) => _webDriver.Manage().Window.Position = pos;

    /// <summary>
    /// Получить линк на объект
    /// </summary>
    /// <returns>уже созданный объект для текущего устройства</returns>
    public ChromeDriver Instance() => _webDriver;
}