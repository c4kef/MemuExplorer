using System.Drawing;
using Polly;
using WPP4DotNet.WebDriver;
using WPP4DotNet.Utils;
using System;
using System.Threading.Tasks;
using System.IO;
using ZXing.QrCode;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using RestSharp;
using Image = System.Drawing.Image;
using ZXing;
using PuppeteerSharp;

namespace WPP4DotNet
{
    public abstract class IWpp
    {
        #region WPP4DotNet - Library Functions
        /// <summary>
        /// WppPath
        /// </summary>
        public string WppPath = "";

        /// <summary>
        /// DriverStarted
        /// </summary>
        private bool DriverStarted { get; set; }

        /// <summary>
        /// Internal WebDriver Interface
        /// </summary>
        private IBrowser Driver;

        /// <summary>
        /// WebDriver Interface
        /// </summary>
        public IBrowser WebDriver
        {
            get
            {
                if (Driver != null)
                {
                    return Driver;
                }
                throw new NullReferenceException("Could not use WebDriver, you must start the StartDriver() class first!");
            }
        }

        /// <summary>
        /// This is the message class.
        /// </summary>
        public class Messenger : EventArgs
        {
            public Messenger(string id, string sender, string message)
            {
                Date = DateTime.Now;
                Id = id;
                Sender = sender;
                Message = message;
            }
            public DateTime Date { get; }
            public string Id { get; }
            public string Message { get; }
            public string Sender { get; }
        }
        
        /// <summary>
        /// This method delegates.
        /// </summary>
        /// <param name="e">Set the messenger</param>
        public delegate void EventReceived(Messenger e);

        /// <summary>
        /// This is a received event method.
        /// </summary>
        public event EventReceived Received;

        /// <summary>
        /// This method checks incoming messages.
        /// </summary>
        /// <param name="id">Set the id</param>
        /// <param name="sender">Set the sender</param>
        /// <param name="message">Set the message</param>
        protected void CheckReceived(string id, string sender, string message)
        {
            Received?.Invoke(new Messenger(id, sender, message));
        }

        /// <summary>
        /// This method transforms the text into a qrcode image.
        /// </summary>
        /// <param name="width">Set the width</param>
        /// <param name="height">Set the height</param>
        /// <param name="text">Set the text</param>
        /// <param name="margin">Set the margin</param>
        /// <returns>Returns an object of type Bitmap</returns>
        private Bitmap CreateQRCode(int width, int height, string text, int margin = 0)
        {
            try
            {
                Bitmap bmp;
                var qrCodeWriter = new ZXing.BarcodeWriterPixelData
                {
                    Format = ZXing.BarcodeFormat.QR_CODE,
                    Options = new QrCodeEncodingOptions
                    {
                        Height = height,
                        Width = width,
                        Margin = margin
                    }
                };
                var pixelData = qrCodeWriter.Write(text);
                using (var bitmap = new Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
                {
                    using (var ms = new MemoryStream())
                    {
                        var bitmapData = bitmap.LockBits(new Rectangle(0, 0, pixelData.Width, pixelData.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                        try
                        {
                            System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
                        }
                        finally
                        {
                            bitmap.UnlockBits(bitmapData);
                        }
                        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        bmp = new Bitmap(ms);
                    }
                }

                return bmp;
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// This method starts the selenium session, allowing you to save the cache and hide the browser.
        /// </summary>
        /// <param name="cache">If necessary define the directory to save the session data.</param>
        /// <param name="hidden">Set true or false if you want to hide the browser.</param>
        public virtual async Task StartSession()
        {
            CheckDriverStarted();
            DriverStarted = true;
        }

        /// <summary>
        /// This method starts browsing and inserting JS scripts.
        /// </summary>
        /// <param name="driver">Insert the IWebDriver object.</param>
        public virtual async Task StartSession(IBrowser driver)
        {
            this.Driver = driver;

            await (await Driver.PagesAsync())[0].SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36");

            await (await Driver.PagesAsync())[0].EvaluateExpressionOnNewDocumentAsync("navigator.serviceWorker.getRegistrations().then((registrations)=>{for(let registration of registrations){registration.unregister()}}).catch((err)=>null);navigator.serviceWorker.register=new Promise(()=>{});");
            await (await Driver.PagesAsync())[0].EvaluateFunctionOnNewDocumentAsync("()=>{navigator.serviceWorker.getRegistrations().then((registrations)=>{for(let registration of registrations){registration.unregister()}}).catch((err)=>null);navigator.serviceWorker.register=new Promise(()=>{})}");
            await (await Driver.PagesAsync())[0].SetRequestInterceptionAsync(true);

            (await Driver.PagesAsync())[0].Request += async (sender, e) =>
            {
                var req = e.Request;
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

                await req.RespondAsync(new ResponseData
                {
                    Status = HttpStatusCode.OK,
                    ContentType = "text/html",
                    Body = await File.ReadAllTextAsync(@"Data\whatsapp-page.html")
                });
            };

            await (await Driver.PagesAsync())[0].GoToAsync("https://web.whatsapp.com");
            await (await Driver.PagesAsync())[0].ReloadAsync();

            await GetWppJS();
        }

        public static async Task DownloadChromium()
        {
            using var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();
        }

        /// <summary>
        /// This method checks if the selenium driver is started.
        /// </summary>
        /// <param name="invert"></param>
        /// <exception cref="NotSupportedException"></exception>
        protected void CheckDriverStarted(bool invert = false)
        {
            if (DriverStarted ^ invert)
            {
                throw new NotSupportedException(String.Format("Driver has been {0} started.", invert ? "not" : ""));
            }
        }

        /// <summary>
        /// This method ends the selenium session.
        /// </summary>
        /// <returns>Return True or False</returns>
        public async Task<bool> Finish()
        {
            try
            {
                await Driver.CloseAsync();
                Driver.Dispose();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// This method in conjunction with "SearchMessage" allows sending a POST request to an external URL.
        /// </summary>
        /// <param name="url">Enter your external URL that will receive the POST.</param>
        /// <param name="msg">Insert the "Messenger" object returned by the "SearchMessage" task.</param>
        /// <returns>Return True or False</returns>
        public bool WebHook(string url, Messenger msg)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest();
                request.Method = Method.Post;
                request.RequestFormat = DataFormat.Json;
                request.AddHeader("Content-Type", "application/json");
                request.AddJsonBody(new { Sender = msg.Sender, Message = msg.Message, Date = msg.Date });
                RestResponse response = client.PostAsync(request).Result;
                return response.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content) ? true : false;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        #region WPP4DotNet - JavaScript
        /// <summary>
        /// This method downloads and updates the latest version of wppconnect-wa.js.
        /// </summary>
        /// <returns>Return True or False</returns>
        public async Task<bool> GetWppJS()
        {
            try
            {
                GitHub github = new GitHub();
                github.CheckUpdate(WppPath);
                string path = string.IsNullOrEmpty(WppPath) ? Path.Combine(Environment.CurrentDirectory, "WppConnect") : WppPath;
                string file = Path.Combine(path, "wppconnect-wa.js");
                if (File.Exists(file))
                {
                    using (StreamReader sr = new StreamReader(file))
                    {
                        //await js.Execute((await Driver.PagesAsync())[0], "WPPConfig = {poweredBy: 'WPP4DotNet'}");
                        string wppjs = sr.ReadToEnd();
                        await (await Driver.PagesAsync())[0].AddScriptTagAsync(new AddTagOptions(){ Path = file });
                        // Wait WA-JS load
                        await (await Driver.PagesAsync())[0].WaitForFunctionAsync("() => window.WPP?.isReady");
                        //await js.Execute((await Driver.PagesAsync())[0], wppjs);
                        //await js.Execute((await Driver.PagesAsync())[0], CustomJS());
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// This method customizes some calls to WPP JS generating new JS functions.
        /// </summary>
        /// <returns>Returns STRING from JS functions.</returns>
        private string CustomJS()
        {
            return "window.WPP.chatList=async function(s,t){let a=[];switch(s){case\"user\":a=await window.WPP.chat.list({onlyUsers:!0});break;case\"group\":a=await window.WPP.chat.list({onlyGroups:!0});break;case\"label\":a=await window.WPP.chat.list({withLabels:t});break;case\"unread\":a=await window.WPP.chat.list({onlyWithUnreadMessage:!0});break;default:a=await window.WPP.chat.list()}let e=[];for(let s=0;s<a.length;s++)if(a[s]){let t={hasUnread:a[s].hasUnread,type:a[s].kind,messages:a[s].msgs._models,lastMessage:a[s].lastReceivedKey,contact:{id:a[s].id.user,server:a[s].id.server,name:a[s].formattedTitle,pushname:a[s].contact.pushname,isUser:a[s].isUser,isGroup:a[s].isGroup,isBroadcast:a[s].isBroadcast,isMe:a[s].contact.isMe,isBusiness:a[s].contact.isBusiness,isMyContact:a[s].contact.isMyContact,isWAContact:a[s].contact.isWAContact,image:\"\"}};e.push(t)}return e},window.WPP.chatFind=async function(s){let t=await window.WPP.chat.find(s);return{hasUnread:t.hasUnread,type:t.kind,messages:t.msgs._models,lastMessage:t.lastReceivedKey,contact:{id:t.id.user,server:t.id.server,name:t.formattedTitle,pushname:t.contact.pushname,isUser:t.isUser,isGroup:t.isGroup,isBroadcast:t.isBroadcast,isMe:t.contact.isMe,isBusiness:t.contact.isBusiness,isMyContact:t.contact.isMyContact,isWAContact:t.contact.isWAContact,image:\"\"}}},window.WPP.contactList=async function(s,t){let a=[];switch(s){case\"my\":a=await window.WPP.contact.list({onlyMyContacts:!0});break;case\"label\":a=await window.WPP.contact.list({withLabels:t});break;default:a=await window.WPP.contact.list()}let e=[];for(let s=0;s<a.length;s++)if(a[s]){let t={id:a[s].id.user,server:a[s].id.server,name:a[s].name,pushname:a[s].formattedName,isUser:a[s].isUser,isGroup:a[s].isGroup,isBroadcast:a[s].isBroadcast,isMe:a[s].isMe,isBusiness:a[s].isBusiness,isMyContact:a[s].isMyContact,isWAContact:a[s].isWAContact,image:\"\"};e.push(t)}return e};";
        }
        #endregion

        #region WPPJS CONN - Functions
        /// <summary>
        /// This method get the authentication code from the qr code.
        /// </summary>
        /// <returns>Returns STRING with authentication information.</returns>
        public async Task<string> GetAuthCode()
        {
            try
            {
                var code = await (await Driver.PagesAsync())[0].EvaluateFunctionAsync<object>("() => window.WPP?.conn.getAuthCode()");
                if (code is null)
                    return "";

                var response = JObject.Parse(code.ToString()!);
                return (string)response["fullCode"]!;
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// This method reloads the authentication code from the qr code.
        /// </summary>
        /// <returns>Returns STRING with authentication information.</returns>
        public async Task<string> GetAuthCodeRefresh()
        {
            try
            {
                var response = JObject.Parse((await (await Driver.PagesAsync())[0].EvaluateFunctionAsync<object>("() => window.WPP?.conn.refreshQR()")).ToString()!);
                return (string)response["fullCode"]!;
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// This method return my number.
        /// </summary>
        /// <returns>Returns STRING with my id (Phone Number)</returns>
        public async Task<string> GetMyNumber()
        {
            try
            {
                var response = JObject.Parse((await (await Driver.PagesAsync())[0].EvaluateFunctionAsync<object>("() => window.WPP?.conn.getMyUserId()")).ToString()!);
                return (string)response["user"]!;
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// This method checks if the WPPJS script has been entered in the browser.
        /// </summary>
        /// <returns>Return True or False</returns>
        public async Task<bool> IsInjected()
        {
            try
            {
                return await (await Driver.PagesAsync())[0].EvaluateFunctionAsync<bool>("() => window.WPP?.isInjected");
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// This method checks if the qr code authentication has already been done.
        /// </summary>
        /// <returns>Return True or False</returns>
        public async Task<bool> IsAuthenticated()
        {
            try
            {
                return await (await Driver.PagesAsync())[0].EvaluateFunctionAsync<bool>("() => window.WPP?.conn.isAuthenticated()");
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// This method checks if whatsapp Main has been loaded.
        /// </summary>
        /// <returns>Return True or False</returns>
        public async Task<bool> IsMainLoaded()
        {
            try
            {
                return await (await Driver.PagesAsync())[0].EvaluateFunctionAsync<bool>("() => window.WPP?.conn.isMainLoaded()");
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// This method checks if whatsapp Main has been ready.
        /// </summary>
        /// <returns>Return True or False</returns>
        public async Task<bool> IsMainReady()
        {
            try
            {
                return await (await Driver.PagesAsync())[0].EvaluateFunctionAsync<bool>("() => window.WPP?.conn.isMainReady()");
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region WPPJS CHAT - Chat Functions

        /// <summary>
        /// This method takes the generated authentication code and creates a qrcode image of type "Image".
        /// </summary>
        /// <param name="width">Enter the width by default, it is already set to 300.</param>
        /// <param name="height">Enter the height by default, it is already set to 300.</param>
        /// <param name="refresh">Enter true or false if you want to reload the image.</param>
        /// <returns>Returns the Image object</returns>
        /// <exception cref="Exception"></exception>
        public async Task<Image> GetAuthImage(int width = 300, int height = 300, bool refresh = false)
        {
            try
            {
                if (await IsInjected())
                {
                    var pol = Policy<Image>
                    .Handle<Exception>()
                    .WaitAndRetry(new[]
                    {
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(3)
                    });
                    while (true)
                    {
                        string qrcode;
                        if (!refresh)
                        {
                            qrcode = await GetAuthCode();
                        }
                        else
                        {
                            qrcode = await GetAuthCodeRefresh();
                        }
                        if (!string.IsNullOrEmpty(qrcode))
                        {
                            return pol.Execute(() =>
                            {
                                try
                                {
                                    Bitmap objBitmap = new Bitmap(CreateQRCode(width, height, qrcode));
                                    Image objImage = (Image)objBitmap;
                                    return objImage;
                                }
                                catch (Exception)
                                {
                                    //throw new Exception("Image not found!");
                                    return null;
                                }
                            });
                        }
                    }
                }
                else
                {
                    //throw new Exception("WPP not found!");
                    return null;
                }
            }
            catch (Exception)
            {
                //throw new Exception("Image not found!");
                return null;
            }
        }

        /// <summary>
        /// This method mark a chat to composing state and keep sending "is writting a message".
        /// </summary>
        /// <param name="chat">Inform the chat.</param>
        /// <param name="time">Enter milliseconds or leave empty.</param>
        /// <returns>Return True or False</returns>
        public async Task<bool> MarkIsComposing(string chat, int time = 0)
        {
            try
            {
                var timer = time > 0 ? ", " + time : "";
                await (await Driver.PagesAsync())[0].EvaluateFunctionAsync("(chat, timer) => window.WPP?.chat.markIsComposing(chat, timer)", chat, timer);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region WPPJS CHAT - Message Functions
        /// <summary>
        /// This method send a text message.
        /// </summary>
        /// <param name="chat">Inform the chat.</param>
        /// <param name="message">Inform the Message.</param>
        /// <param name="simulateTyping">Inform true or false.</param>
        /// <returns>Returns the Models.SendReturnModels object</returns>
        public async Task<Models.SendReturnModels> SendMessage(string chat, string message, bool simulateTyping=false)
        {
            var resp = $"return await WPP.chat.sendTextMessage('{chat}', '{message}', {{\n  createChat: true\n}});";
            try
            {
                Models.SendReturnModels ret = new Models.SendReturnModels();
                if (simulateTyping)
                {
                    await MarkIsComposing(chat, 5000);
                }

                var response = JObject.Parse((await (await Driver.PagesAsync())[0].EvaluateFunctionAsync<object>("(chat, message) => window.WPP?.chat.sendTextMessage(chat, message, {\n  createChat: true\n})", chat, message)).ToString()!);
                if (!string.IsNullOrEmpty((string)response["id"]!))
                { 
                    ret.Id = (string)response["id"]!;
                    ret.Sender = await GetMyNumber();
                    ret.Recipient = chat;
                    ret.Status = true;
                }
                else
                {
                    ret.Status = false;
                    ret.Error = "Error trying to send message.";
                }
                return ret;
            }
            catch (Exception ex)
            {
                Models.SendReturnModels ret = new Models.SendReturnModels();
                ret.Error = ex.Message;
                ret.Status = false;
                return ret;
            }
        }

        /// <summary>
        /// This method send a file message, that can be an audio, document, image, sticker or video.
        /// </summary>
        /// <param name="chat">Inform the chat.</param>
        /// <param name="content">Inform the content.</param>
        /// <param name="options">Inform the Options List.</param>
        /// <param name="simulateTyping">Inform true or false.</param>
        /// <returns>Returns the Models.SendReturnModels object</returns>
        public async Task<Models.SendReturnModels> SendFileMessage(string chat, string content, List<string> options, bool simulateTyping = false)
        {
            try
            {
                Models.SendReturnModels ret = new Models.SendReturnModels();
                if (simulateTyping)
                {
                    await MarkIsComposing(chat, 5000);
                }
                var option = "";
                foreach (var item in options)
                {
                    option += string.Format(",{0}", item);
                }
                option = "{"+ option.TrimStart(',') + "}";
                var response = JObject.Parse((await (await Driver.PagesAsync())[0].EvaluateFunctionAsync<object>("(chat, content, option) => window.WPP?.chat.sendFileMessage(chat, content, option)", chat, content, option)).ToString()!);
                if (!string.IsNullOrEmpty((string)response["id"]!))
                {
                    ret.Id = (string)response["id"]!;
                    ret.Sender = await GetMyNumber();
                    ret.Recipient = chat;
                    ret.Status = true;
                }
                else
                {
                    ret.Status = false;
                    ret.Error = "Error trying to send message.";
                }
                return ret;
            }
            catch (Exception ex)
            {
                Models.SendReturnModels ret = new Models.SendReturnModels();
                ret.Error = ex.Message;
                ret.Status = false;
                return ret;
            }
        }
        #endregion

        #region WPPJS CONTACT - Functions

        /// <summary>
        /// This method check contact exist.
        /// </summary>
        /// <param name="chat">Inform the chat.</param>
        /// <returns>Return True or False</returns>
        public async Task<bool> ContactExists(string chat)
        {
            try
            {
                dynamic response = await (await Driver.PagesAsync())[0].EvaluateFunctionAsync<object>("(chat) => window.WPP?.contact.queryExists(chat)", chat);
                return response == null ? false : true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        #region WPPJS GROUP - Functions
        /// <summary>
        /// This method join group from invite code.
        /// </summary>
        /// <param name="code">Inform the invite code.</param>
        /// <returns>Return String Chat ID</returns>
        public async Task<string> GroupJoin(string code)
        {
            try
            {
                var response = JObject.Parse((await (await Driver.PagesAsync())[0].EvaluateFunctionAsync<object>("(code) => window.WPP?.group.join(code)", code)).ToString()!);
                return (string)response["id"]!;
            }
            catch (Exception)
            {
                return "";
            }
        }
        #endregion
    }
}