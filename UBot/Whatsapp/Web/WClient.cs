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

namespace UBot.Whatsapp.Web;

public class WClient
{
    private IWpp _wpp;
    public readonly string NameSession;

    /// <summary>
    /// Инициализация сессии
    /// </summary>
    /// <param name="nameSession">имя сессии</param>
    public WClient(string nameSession)
    {
        NameSession = nameSession;
    }

    /// <summary>
    /// Инициализировать
    /// </summary>
    /// <param name="waitQr">мне следует ждать Qr код?</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task Init(bool waitQr, string pathToWeb)
    {
        _wpp = new ChromeWebApp(false, pathToWeb) as IWpp;
        await _wpp.StartSession();

        var timerLoading = 0;

        while (timerLoading++ < 45)
        {
            if (await IsConnected())
                break;

            if (!string.IsNullOrEmpty(await _wpp.GetAuthCode()))
            {
                if (waitQr)
                {
                    /*var qr = await wpp.GetAuthImage();

                    qr.Save(@$"{Globals.TempDirectory.FullName}\{TaskId}.png");

                    Globals.QrCodeName = TaskId.ToString();

                    if (!await WaitRequest())
                    {
                        _taskQueue.RemoveAll(task => task == TaskId);
                        throw new Exception("cant wait end operation");
                    }

                    Globals.QrCodeName = string.Empty;

                    while (!TryDeleteQR())
                        await Task.Delay(500);*/
                }
                else
                    throw new Exception("Qr code is generated");
            }

            await Task.Delay(500);
        }

        if (timerLoading >= 45)
            throw new Exception("Cant load account");
    }

    public async Task<bool> IsConnected() => await _wpp.IsMainLoaded() && await _wpp.IsAuthenticated() && await _wpp.IsMainReady();

    /// <summary>
    /// Отправить сообщение
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task<bool> SendText(string number, string text, FileInfo? image = null)
    {
        if (!await IsConnected())
            throw new Exception($"SendText: client has disconected");

        try
        {
            var isSended = false;

            if (image != null && image.Exists)
            {
                var base64 = ConvertFileToBase64(image.FullName);
                var type = FileType(image.FullName);
                List<string> options = new List<string>();
                options.Add($"type: '{type}'");
                options.Add($"caption: '{text}'");

                isSended = (await _wpp.SendFileMessage($"{number.Replace("+", string.Empty)}@c.us", base64, options)).Status;
            }
            else
                isSended = (await _wpp.SendMessage($"{number.Replace("+", string.Empty)}@c.us", text)).Status;

            if (!isSended)
                throw new Exception($"SendText: message is not sended");

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
    public async void Free() => await _wpp.Finish();

    /// <summary>
    /// Проверяет контакт на наличие Whatsapp
    /// </summary>
    /// <exception cref="InvalidOperationException">Неверное значение</exception>
    /// <exception cref="Exception">Ошибка сервера</exception>
    public async Task<bool> CheckValidPhone(string phone)
    {
        if (!await IsConnected())
            throw new Exception($"CheckValidPhone: client has disconected");

        return true;//!string.IsNullOrEmpty(await _wpp.ContactGetStatus($"{phone.Replace("+", string.Empty)}@c.us"));To-Do
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