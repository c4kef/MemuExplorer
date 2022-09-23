using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UBot.Whatsapp.Web;

namespace UBot.Whatsapp;

public class Client
{
    private MemuLib.Core.Client Mem { get; set; }
    public string Phone { private set; get; }
    public string Account { private set; get; }
    public AccountData AccountData { get; set; }
    public bool IsW4B{private set; get; }
    public WClient Web { private set; get; }

    public string PackageName
    {
        get => (IsW4B) ? "com.whatsapp.w4b" : "com.whatsapp";
    }

    public Client(string phone = "", string account = "", int deviceId = -1, bool isW4B = false)
    {
        Phone = string.IsNullOrEmpty(phone) ? string.Empty : phone[0] == '+' ? phone : "+" + phone;
        Account = account;

        IsW4B = isW4B;

        AccountData = new AccountData();

        if (account != string.Empty)
            AccountData = JsonConvert.DeserializeObject<AccountData>(File.ReadAllText($@"{account}\Data.json"))!;

        if (!string.IsNullOrEmpty(phone))
            Web = new WClient(phone[0] == '+' ? phone.Remove(0, 1) : phone);

        Mem = deviceId == -1 ? new MemuLib.Core.Client(0) : new MemuLib.Core.Client(deviceId);
    }

    public MemuLib.Core.Client GetInstance()
    {
        return Mem;
    }

    public async Task Start()
    {
        await Mem.Start();
    }

    public async Task Stop()
    {
        await Mem.Stop();
    }
}