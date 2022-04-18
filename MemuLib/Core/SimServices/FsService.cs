namespace MemuLib.Core.SimServices;

public class FsService
{
    /// <summary>
    /// Номер телефона после создания сервиса
    /// </summary>
    public readonly string Phone;
    
    /// <summary>
    /// ID заказа после создания сервиса
    /// </summary>
    private readonly int _id;
    
    /// <summary>
    /// Объявляем новый образ класса
    /// </summary>
    /// <param name="number">номер телефона</param>
    /// <param name="id">id заказа</param>
    private FsService(string number, int id)
    {
        Phone = number;
        _id = id;
    }

    /// <summary>
    /// Создаем сервис
    /// </summary>
    /// <param name="country">страна для создания сервиса</param>
    /// <param name="moperator">мобильный оператор</param>
    /// <param name="service">имя сервиса (для какого сервиса будем полчать смс)</param>
    /// <returns>новый объявленный класс для дальнейшей работы с сервисом</returns>
    /// <exception cref="Exception">евреи зажали номер, кладем болт и идем дальше</exception>
    /// <exception cref="InvalidOperationException">по факту заглушка, но если что... просто не присвоились данные</exception>
    public static async Task<FsService?> Create([Optional]string country, [Optional]string moperator, string service)
    {
        try
        {
            var requestValue =
                await WebReq.HttpGet(
                    $"https://5sim.net/v1/user/buy/activation/{(string.IsNullOrEmpty(country) ? "any" : country)}/{(string.IsNullOrEmpty(moperator) ? "any" : moperator)}/{service}");

            var serviceCreate = JObject.Parse(requestValue);

            if (serviceCreate is null)
                throw new Exception($"cant create service: {requestValue}");

            var phone = serviceCreate["phone"];
            var id = serviceCreate["id"];

            return new FsService(phone?.ToString() ?? throw new InvalidOperationException(),
                int.Parse(id?.ToString() ?? throw new InvalidOperationException()));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Получаем смс'ки
    /// </summary>
    /// <returns>первый из смс</returns>
    /// <exception cref="InvalidOperationException">судя по всеми не присвоилось значение</exception>
    public async Task<string> GetMessage()
    {
        var sms = JObject.Parse(await WebReq.HttpGet($"https://5sim.net/v1/user/check/{_id}"))["sms"];
        
        if (sms is null || sms.ToArray().Length == 0)
            return string.Empty;

        return sms.FirstOrDefault()?["text"]?.ToString() ?? throw new InvalidOperationException();
    }
    
    /// <summary>
    /// Отменяем заказ (баним номер)
    /// </summary>
    public async void Cancel() => await WebReq.HttpGet($"https://5sim.net/v1/user/ban/{_id}");
}