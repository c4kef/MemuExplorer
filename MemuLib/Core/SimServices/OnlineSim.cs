namespace MemuLib.Core.SimServices;

public class OnlineSim
{
    /// <summary>
    /// ID заказа после создания сервиса
    /// </summary>
    private readonly int _id;

    /// <summary>
    /// Объявляем новый образ класса
    /// </summary>
    /// <param name="number">номер телефона</param>
    /// <param name="id">id заказа</param>
    private OnlineSim(int id)
    {
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
    public static async Task<OnlineSim?> Create([Optional] string country, string service)
    {
        try
        {
            var requestValue =
                JObject.Parse(await WebReq.HttpGet(
                    $"https://onlinesim.io/api/getNum.php?apikey={Settings.SimApi}&country={(string.IsNullOrEmpty(country) ? "0" : country)}&service={service}"));

            if (requestValue["response"]!.ToString() != "1")
                throw new Exception($"cant create service: {requestValue["response"]!.ToString()}");

            var id = requestValue["tzid"]!.ToString();

            return new OnlineSim(int.Parse(id));
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
    public async Task<JObject> GetMessage()
    {
        var request = await WebReq.HttpGet($"https://onlinesim.io/api/getState.php?apikey={Settings.SimApi}&tzid={_id}&message_to_code=1");

        return JObject.Parse((request[0] == '[') ? request.Remove(request.Length - 1, 1).Remove(0, 1) : request);
    }

    /// <summary>
    /// Устаналиваем статус номеру
    /// </summary>
    /// <param name="isSuccesful">успешно получили смс</param>
    public async Task<string> SetStatus(bool isSuccesful) => await WebReq.HttpGet($"https://onlinesim.io/api/{((isSuccesful) ? "setOperationOk" : "setOperationRevise")}.php?apikey={Settings.SimApi}&tzid={_id}");
}