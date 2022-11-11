using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MemuLib.Core;
using System.Threading.Tasks;
using WPP4DotNet.WebDriver;
using WPP4DotNet;
using WPP4DotNet.Models;
using ZXing;
using PuppeteerSharp;

namespace UBot.Whatsapp.Web;

public class WClient
{
    private static List<int> Queue = null!;
    private static List<int> QueueProcess = null!;

    private IWpp _wpp;
    public readonly int TaskId;
    public readonly string NameSession;
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Инициализация сессии
    /// </summary>
    /// <param name="nameSession">имя сессии</param>
    public WClient(string nameSession)
    {
        NameSession = nameSession;
        TaskId = new Random().Next(1_000_000, 10_000_000);
    }

    /// <summary>
    /// Добавление в очередь
    /// </summary>
    public void AddToQueue() => Queue.Add(TaskId);

    /// <summary>
    /// Ждем своей очереди
    /// </summary>
    /// <returns>false - если очередь не удалось дождаться и true - если мы смогли дождаться</returns>
    public async Task WaitQueue()
    {
        while (!QueueProcess.ToArray().Any(_id => _id == TaskId))
            await Task.Delay(10);
    }

    /// <summary>
    /// Удаляем наш запрос из очереди
    /// </summary>
    public void RemoveQueue() => QueueProcess.Remove(TaskId);

    /// <summary>
    /// Обработчик запросов на сканирование
    /// </summary>
    public static async Task QueueCameraHandler()
    {
        Queue = new List<int>();
        QueueProcess = new List<int>();

        var lastId = 0;
        var aliveLastId = 0;

        while (true)
        {
            if (Queue.Count > 0 && QueueProcess.Count == 0)
            {
                QueueProcess.Add(Queue[0]);
                Queue.RemoveAt(0);
            }

            if (QueueProcess.Count > 0)
            {
                if (lastId == 0 || lastId != QueueProcess[0])
                {
                    lastId = QueueProcess[0];
                    aliveLastId = 0;
                }
                else if (lastId == QueueProcess[0])
                {
                    //124 = 1 minute - 60 sec
                    //2 = 1 sec
                    if (aliveLastId++ > 620)
                    {
                        QueueProcess.Clear();
                        lastId = 0;
                        aliveLastId = 0;
                    }
                }
            }

            await Task.Delay(500);
        }
    }

    /// <summary>
    /// Инициализировать
    /// </summary>
    /// <param name="waitQr">мне следует ждать Qr код?</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task Init(bool waitQr, string pathToWeb, string proxy = "")
    {
        Directory.CreateDirectory(pathToWeb);
        IsDisposed = false;

        if (!string.IsNullOrEmpty(proxy))
        {
            var proxyAuthData = proxy.Split(':');
            _wpp = new ChromeWebApp(false, pathToWeb, string.Join(':', proxyAuthData.Take(2))) as IWpp;
            await _wpp.StartSession(new Credentials()
            {
                Username = proxyAuthData[2],
                Password = proxyAuthData[3]
            });
        }
        else
        {
            _wpp = new ChromeWebApp(false, pathToWeb) as IWpp;
            await _wpp.StartSession(null);
        }

        var timerLoading = 0;
        while (timerLoading++ < 45)
        {
            if (await IsConnected())
                break;

            if (!waitQr)
            {
                if ((await (await _wpp.WebDriver.PagesAsync())[0].QuerySelectorAsync("#app > div > div > div.landing-window > div.landing-main > div > a")) is not null)
                    throw new Exception("Qr code is generating...");
            }
            else
            {
                if (!string.IsNullOrEmpty(await _wpp.GetAuthCode()))
                {
                    Globals.QrCode = null;
                    Globals.QrCode = await _wpp.GetAuthImage(276, 276);
                    var timerLoadingQr = 0;

                    while (!await IsConnected())
                    {
                        if (timerLoadingQr++ > 50)
                            throw new Exception("We cant scan Qr, retry again");

                        await Task.Delay(500);
                    }

                    Globals.QrCode = null;
                }
            }

            await Task.Delay(500);
        }

        if (timerLoading >= 45)
            throw new Exception("Cant load account");

        if ((Globals.Setup.Latitude ?? 0) > 0 && (Globals.Setup.Longitude ?? 0) > 0)
            await _wpp.SetGeoPosition((decimal)Globals.Setup.Latitude, (decimal)Globals.Setup.Longitude);
    }

    public async Task<bool> IsConnected()
    {
        try
        {
            if (_wpp is null)
                return false;

            if (_wpp.WebDriver is null)
                return false;

            if ((await _wpp.WebDriver.PagesAsync()).Length == 0)
                return false;

            return !IsDisposed && (await (await _wpp.WebDriver.PagesAsync())[0].QuerySelectorAsync("#app > div > div > div.landing-window > div.landing-main > div > a")) is null && await _wpp.IsMainLoaded() && await _wpp.IsAuthenticated() && await _wpp.IsMainReady();
        }
        catch
        {
            return false;
        }
    }
    /// <summary>
    /// Отправить сообщение
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task<bool> SendText(string number, string text, FileInfo? image = null, string? buttonText = null, string title = "", string footer = "")
    {
        if (!await IsConnected())
            throw new Exception($"SendText: client has disconected");

        try
        {
            SendReturnModels Status = null;

            if (image != null && image.Exists)
            {
                var base64 = ConvertFileToBase64(image.FullName);
                var type = FileType(image.FullName);
                List<string> options = new List<string>();
                options.Add($"type: '{type}'");
                options.Add($"caption: '{text}'");
                options.Add($"createChat: true");

                Status = (await _wpp.SendFileMessage($"{number.Replace("+", string.Empty)}@c.us", base64, options));
            }
            else
            {
                List<string> options = new List<string>();
                options.Add($"createChat: true");
                var msgid = string.Empty;
                if (buttonText is not null)
                {
                    options.Add($"useTemplateButtons: false");
                    options.Add($"buttons: [ {{ id: '', text: '{buttonText}' }} ]");
                    options.Add($"title: '{title}'");
                    options.Add($"footer: '{footer}'");
                }

                Status = await _wpp.SendMessage($"{number.Replace("+", string.Empty)}@c.us", text, options);
            }

            if (!Status.Status)
                throw new Exception($"SendText: message not sended by {Status.Error}");

            return true;
        }
        catch (Exception ex)
        {
            Log.Write($"Ошибка отправки сообщения:\n{ex.Message}\n");
            return false;
        }
    }

    /// <summary>
    /// Освобождает текущею сессию из пула на сервере
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task Free()
    {
        IsDisposed = true;
        try
        {
            /*await _wpp.WebDriver.CloseAsync();
            _wpp.WebDriver.Dispose();*/

            _wpp.WebDriver.Process.Kill();
        }
        catch(Exception ex)
        {
            Log.Write($"Ошибка при очистке сессии:\n{ex.Message}\n");
        }
    }
    /// <summary>
    /// Проверяет контакт на наличие Whatsapp
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task<bool> CheckValidPhone(string phone)
    {
        if (!await IsConnected())
            throw new Exception($"CheckValidPhone: client has disconected");

        return await _wpp.ContactExists($"{phone.Replace("+", string.Empty)}@c.us");
    }

    public async Task RemoveAvatar()
    {
        if (!await IsConnected())
            throw new Exception($"RemoveAvatar: client has disconected");
    }

    /// <summary>
    /// Вступаем в группу Whatsapp
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task<bool> JoinGroup(string group)
    {
        if (!await IsConnected())
            throw new Exception($"JoinGroup: client has disconected");

        return !string.IsNullOrEmpty(await _wpp.GroupJoin(group));
    }

    private string FileType(string fileName)
    {
        string Extension = Path.GetExtension(fileName);
        switch (Extension.ToLower())
        {
            case ".jpg":
            case ".jpeg":
            case ".gif":
            case ".png":
            case ".bmp":
            case ".ico":
                return "image";
            case ".mp3":
                return "audio";
            case ".mp4":
            case ".mpeg":
                return "video";
            default:
                return "document";
        }
    }

    private string ConvertFileToBase64(string path)
    {
        string Extension = Path.GetExtension(path);
        string MimeType;
        switch (Extension.ToLower())
        {
            case ".jpg":
                MimeType = "data:image/jpg;base64,";
                break;
            case ".jpeg":
                MimeType = "data:image/jpeg;base64,";
                break;
            case ".gif":
                MimeType = "data:image/gif;base64,";
                break;
            case ".png":
                MimeType = "data:image/png;base64,";
                break;
            case ".bmp":
                MimeType = "data:image/bmp;base64,";
                break;
            case ".ico":
                MimeType = "data:image/x-icon;base64,";
                break;
            case ".pdf":
                MimeType = "data:application/pdf;base64,";
                break;
            case ".mp3":
                MimeType = "data:audio/mp3;base64,";
                break;
            case ".mp4":
                MimeType = "data:video/mp4;base64,";
                break;
            case ".mpeg":
                MimeType = "data:application/mpeg;base64,";
                break;
            case ".txt":
                MimeType = "data:text/plain;base64,";
                break;
            default:
                MimeType = "data:application/octet-stream;base64,";
                break;
        }
        return MimeType + Convert.ToBase64String(File.ReadAllBytes(path));
    }
}