namespace MemuLib.Core.SimServices;

public class SmsCode
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
    private SmsCode(string number, int id)
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
    public static async Task<SmsCode?> Create([Optional]string country, [Optional]string moperator, string service)
    {
        try
        {
            var requestValue =
                await WebReq.HttpGet(
                    $"https://smscode.me/stubs/handler_api.php?api_key={Settings.SimApi}&action=getNumber&operator={(string.IsNullOrEmpty(moperator) ? "any" : moperator)}&country={(string.IsNullOrEmpty(country) ? "0" : country)}&verification=false&service={service}");

            var serviceCreate = requestValue.Split(':');

            if (serviceCreate[0] != "ACCESS_NUMBER")
                throw new Exception($"cant create service: {requestValue[0]}");

            var phone = serviceCreate[2];
            var id = serviceCreate[1];

            return new SmsCode(phone, int.Parse(id));
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
        var sms = (await WebReq.HttpGet($"https://smscode.me/stubs/handler_api.php?api_key={Settings.SimApi}&action=getStatus&id={_id}")).Split(':');
        
        return sms[0] != "STATUS_OK" ? string.Empty : sms[1];
    }
    
    /// <summary>
    /// Отменяем заказ (баним номер)
    /// </summary>
    public async void Cancel() => await WebReq.HttpGet($"https://smscode.me/stubs/handler_api.php?api_key={Settings.SimApi}&action=setStatus&status=8&id={_id}");
}