global using System.Diagnostics;
global using System.Text;
global using System.Drawing;
global using System.Net;
global using System.Text.RegularExpressions;
global using OpenQA.Selenium.Chrome;
global using AdvancedSharpAdbClient;
global using Newtonsoft.Json;
global using System.Runtime.InteropServices;
global using Newtonsoft.Json.Linq;
namespace MemuLib;

public static class Globals
{
    /// <summary>
    /// Включить логирование?
    /// </summary>
    public static bool IsLog;
    
    /// <summary>
    /// Сгенирировать случайны MAC адрес
    /// </summary>
    /// <returns>строка с новым MAC адресом</returns>
    public static string GetRandomMacAddress()
    {
        var buffer = new byte[6];
        new Random().NextBytes(buffer);
        var result = string.Concat(buffer.Select(x => $"{x:X2}:".ToString()).ToArray());
        return result.TrimEnd(':');
    }

    /// <summary>
    /// Сгенирировать случайную HEX строку
    /// </summary>
    /// <param name="length">длина строки</param>
    /// <returns>новая строка</returns>
    public static string RandomHexString(int length) => new string(Enumerable.Repeat("ABCDEF0123456789", length)
        .Select(s => s[new Random().Next(s.Length)]).ToArray());
    
    /// <summary>
    /// Сгенирировать строку только с числами
    /// </summary>
    /// <param name="length">длина строки</param>
    /// <returns>новая строка</returns>
    private static string RandomNumberString(int length) => new string(Enumerable.Repeat("0123456789", length)
        .Select(s => s[new Random().Next(s.Length)]).ToArray());
    
    /// <summary>
    /// Сгенирировать случайную строчку
    /// </summary>
    /// <param name="length">длина строки</param>
    /// <returns>новая строка</returns>
    public static string RandomString(int length) => new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", length)
        .Select(s => s[new Random().Next(s.Length)]).ToArray());

    /// <summary>
    /// Чтение и получение случайеного сотового оператора
    /// </summary>
    /// <param name="cc">код страны</param>
    /// <returns>информацию о случайном операторе</returns>
    /// <exception cref="Exception">файл для выбора не найден</exception>
    public static async Task<MccMnc> MccMncGet(string cc)
    {
        var rnd = new Random();

        if (!File.Exists($@"{Settings.DatasDir}\mcc.json"))
            throw new Exception("Error: mcc.json not found");

        var array = JsonConvert.DeserializeObject<MccMnc[]>(await File.ReadAllTextAsync($@"{Settings.DatasDir}\mcc.json"));
        var vrem = array.Where(x => x.CountryCode == cc).ToArray();//To-Do
        
        return (vrem.Length > 0) ? vrem[rnd.Next(0, vrem.Length)] : array[rnd.Next(0, array.Length)];
    }
    
    /// <summary>
    /// Генерация случайного Imei
    /// </summary>
    /// <param name="tac">восьмизначная часть информации устройства</param>
    /// <returns>новый Imei</returns>
    public static string GeneratorImei(string tac)
    {
        var rnd = new Random();
        
        var imei = tac;
        while (!ValidateImei(imei))
        {
            imei = "";
            while (imei.Length != 15)
                imei += Convert.ToString(rnd.Next(0, 10));
        }
        
        return imei;
    }
    
    /// <summary>
    /// Проверка Imei на валидность
    /// </summary>
    /// <param name="imei">Imei для проверки</param>
    /// <returns>true - проверка прошла успешно/false - проверка не прошла</returns>
    private static bool ValidateImei(string imei)
    {
        if (imei.Length != 15)
            return false;
        else
        {
            var posImei = new int[15];
            for (var innlop = 0; innlop < 15; innlop++)
            {
                posImei[innlop] = Convert.ToInt32(imei.Substring(innlop, 1));
                if (innlop % 2 != 0) posImei[innlop] = posImei[innlop] * 2;
                while (posImei[innlop] > 9) posImei[innlop] = (posImei[innlop] % 10) + (posImei[innlop] / 10);
            }
            
            var totalVal = posImei.Sum();
            
            return totalVal % 10 == 0;
        }
    }
    
    /// <summary>
    /// Генератор случайного Imsi
    /// </summary>
    /// <param name="mnc">код мобильной сети</param>
    /// <param name="mcc">мобильный код страны</param>
    /// <returns>новый Imsi</returns>
    public static string GeneratoImsi(string mnc, string mcc) => mcc + mnc + RandomNumberString(10);

    /// <summary>
    /// Генерация случайной информации оборудования
    /// </summary>
    /// <returns>новая информация оборудования</returns>
    /// <exception cref="Exception">файл с устройствами не был найден</exception>
    public static async Task<MicrovirtInfo> MicrovirtInfoGet()
    {
        var rnd = new Random();

        if (!File.Exists($@"{Settings.DatasDir}\samsungs.txt"))
            throw new Exception("Error: samsungs.txt not found");
        
        foreach (var line in await File.ReadAllLinesAsync($@"{Settings.DatasDir}\samsungs.txt"))
            if (rnd.Next(0, 100) >= 70)
            {
                var data = line.Split(',');

                return new MicrovirtInfo()
                {
                    MicrovirtVmBoard = data[2],
                    MicrovirtVmBrand = data[0],
                    MicrovirtVmGsm = data[6],
                    MicrovirtVmHardware = "samsungexynos",
                    MicrovirtVmManufacturer = data[0],
                    MicrovirtVmModel = data[3],
                    SecurityPatchDate = data[8],
                    Tac = data[4]
                };
            }

        throw new Exception("Error: The creator of this library is an asshole");
    }
    
    /// <summary>
    /// Генерация уникального номера сим-карты
    /// </summary>
    /// <param name="mnc">код мобильной сети</param>
    /// <returns>новый уникальный номер</returns>
    public static string GetIccid(string mnc)
    {
        var rnd = new Random();
        var pr = false;
        var iccid = string.Empty;
        
        while (!pr)
        {
            iccid = "89" + mnc + rnd.Next(10000, 100000) + rnd.Next(10000, 100000) + rnd.Next(10000, 100000);
            while (iccid.Length < 20)
            {
                iccid += rnd.Next(10);
            }
            pr = CalculateLuhnAlgorithm(iccid);
        }
        
        return iccid;
    }
    
    /// <summary>
    /// Калькулятор контрольных сум
    /// </summary>
    /// <param name="snumber">значение для подсчета</param>
    /// <returns>является ли сумма положительной</returns>
    private static bool CalculateLuhnAlgorithm(string snumber)
    {
        var newListOfNumbers = snumber;

        for (var i = snumber.Length - 2; i > 0; i -= 2)
        {
            var number = (int)char.GetNumericValue(snumber[i]) * 2;
            if (number >= 10)
            {
                var concatinatedNumber = number.ToString();
                var firstNumber = (int)char.GetNumericValue(concatinatedNumber[0]);
                var secondNumber = (int)char.GetNumericValue(concatinatedNumber[1]);
                number = firstNumber + secondNumber;
            }
            newListOfNumbers = newListOfNumbers.Remove(i, 1);
            newListOfNumbers = newListOfNumbers.Insert(i, number.ToString());
        }

        var sumOfAllValues = newListOfNumbers.Sum(c => (int) char.GetNumericValue(c));

        return (sumOfAllValues % 10) == 0;
    }

    /// <summary>
    /// Генерация случайного разрешения экрана
    /// </summary>
    /// <returns>новое разрешение экрана</returns>
    /// <exception cref="Exception">файл с разрешениями не был найден</exception>
    public static async Task<Resolution> GetResolution()
    {
        if (!File.Exists($@"{Settings.DatasDir}\resolutions.txt"))
            throw new Exception("Error: resolutions.txt not found");

        var rnd = new Random();
        
        var resolutions = await File.ReadAllLinesAsync($@"{Settings.DatasDir}\resolutions.txt");
        var selectedRes = resolutions[rnd.Next(0, resolutions.Length)].Split(',');

        return new Resolution()
        {
            Width = selectedRes[0],
            Height = selectedRes[1],
            Dpi = selectedRes[2]
        };
    }

    /// <summary>
    /// Генерация случайной временой зоны
    /// </summary>
    /// <returns>новая временая зона</returns>
    /// <exception cref="Exception">файл с зонами не был найден</exception>
    public static async Task<string> GetTimeZone()
    {
        if (!File.Exists($@"{Settings.DatasDir}\timezones.txt"))
            throw new Exception("Error: timezones.txt not found");

        var rnd = new Random();
        var timezones = await File.ReadAllLinesAsync($@"{Settings.DatasDir}\timezones.txt");

        return timezones[rnd.Next(0, timezones.Length)];
    }
    
    /// <summary>
    /// Генерация случайных андроид релизов
    /// </summary>
    /// <returns>новая версия ведра</returns>
    /// <exception cref="Exception">файл с релизами не был найден</exception>
    public static async Task<AndroidRelease> GetAndroidRelease()
    {
        if (!File.Exists($@"{Settings.DatasDir}\androidreleases.txt"))
            throw new Exception("Error: androidreleases.txt not found");

        var rnd = new Random();
        var dataRelease = await File.ReadAllLinesAsync($@"{Settings.DatasDir}\androidreleases.txt");
        var dataReleaseInfo = dataRelease[rnd.Next(0, dataRelease.Length)].Split(';');
        
        return new AndroidRelease()
        {
            Version = dataReleaseInfo[0],
            BuildId = dataReleaseInfo[1],
            ApiVersion = dataReleaseInfo[2]
        };
    }
    
    /// <summary>
    /// Генерация случайного языка
    /// </summary>
    /// <returns>новый язык</returns>
    /// <exception cref="Exception">файл с языками не был найден</exception>
    public static async Task<string> GetLanguage()
    {
        if (!File.Exists($@"{Settings.DatasDir}\languages.csv"))
            throw new Exception("Error: languages.csv not found");

        var rnd = new Random();
        var languages = await File.ReadAllLinesAsync($@"{Settings.DatasDir}\languages.csv");

        return languages[rnd.Next(0, languages.Length)];
    }
}