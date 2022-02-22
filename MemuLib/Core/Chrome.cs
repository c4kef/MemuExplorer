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
    public Chrome()
    {
        var options = new ChromeOptions();
        var service = ChromeDriverService.CreateDefaultService();
        service.HideCommandPromptWindow = true;
        options.AddArgument("no-sandbox");
        options.AddArgument("remote-debugging-port=0");
        options.AddArgument("disable-extensions");
        _webDriver = new ChromeDriver(service, options);
    }

    /// <summary>
    /// Запуск страницы с QR кодом
    /// </summary>
    public void Start() => _webDriver.Navigate().GoToUrl("https://web.whatsapp.com/");
    
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