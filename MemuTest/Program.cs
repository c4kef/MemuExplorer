using MemuLib.Core.SimServices;

var sms = await SmsCode.Create(service: "wa");

Console.WriteLine(sms!.Phone);

while(await sms!.GetMessage() == string.Empty)
    await Task.Delay(500);
    
Console.WriteLine(await sms!.GetMessage());