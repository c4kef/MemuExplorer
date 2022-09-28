using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MemuLib.Core;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using UBot.Views.User;

namespace UBot.Whatsapp.Web;

public class SClient
{
    public SClient(string cache)
    {
        _chrome = new Chrome(cache);
    }

    private readonly Chrome _chrome;

    public async Task Init()
    {
        _chrome.Start("https://web.whatsapp.com/");

        while (true)
        {
            if (_chrome.Instance().PageSource.Contains("qrcode"))
                throw new Exception("Detect scan qrcode");

            if (_chrome.Instance().PageSource.Contains("Получать оповещения о новых сообщениях"))
                break;

            await Task.Delay(500);
        }
    }

    public async Task SendText(string number, string text)
    {
        _chrome.Instance().Navigate().GoToUrl($"https://web.whatsapp.com/send?phone=+{number}");

        var input_box = await _chrome.FindElement(By.XPath("//*[@id=\"main\"]/footer/div[1]/div/span[2]/div/div[2]/div[1]"));
        Actions action = new Actions(_chrome.Instance());   //*[@id="main"]/footer/div[1]/div/span[2]/div/div[2]/div[1]

        await DashboardView.GetInstance().Dashboard.Dispatcher.DispatchAsync(async () => await Clipboard.SetTextAsync(text));

        //action.KeyDown(input_box, Keys.Control).SendKeys(input_box, "v").KeyUp(input_box, Keys.Control).Perform();
        input_box.SendKeys(text.Replace('\r', '\n'));
        /*foreach (var ch in text)
            if (ch == '\r')
                action.KeyDown(Keys.Shift).KeyDown(Keys.Enter).KeyUp(Keys.Enter).KeyUp(
                    Keys.Shift).KeyUp(Keys.Backspace).Perform();
            else
                input_box.SendKeys(ch.ToString());*/
        //input_box.SendKeys(Keys.Enter);
        // (await _chrome.FindElement(By.XPath("/html/body/div[1]/div/div/div[4]/div/footer/div[1]/div/span[2]/div/div[2]/div[1]/div/div/p/span"))).SendKeys(text);
    }
}
