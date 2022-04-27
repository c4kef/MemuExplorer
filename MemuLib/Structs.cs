namespace MemuLib;

/// <summary>
/// Информация для логирования асинка
/// </summary>
struct DataWrite
{
    public string Path;
    public string Text;
}

/// <summary>
/// Информация о релизе андроида
/// </summary>
public struct AndroidRelease
{
    public string Version;
    public string BuildId;
    public string ApiVersion;
}

/// <summary>
/// Информация о разрешение экрана
/// </summary>
public struct Resolution
{
    public string Height;
    public string Width;
    public string Dpi;
}

/// <summary>
/// Информация о сотовом операторе
/// </summary>
public struct MccMnc
{
    public string Mcc;
    public string Mnc;
    public string Iso;
    public string CountryCode;
    public string MobileOperator;
}

/// <summary>
/// Информация о железе устройства
/// </summary>
public struct MicrovirtInfo
{
    public string MicrovirtVmBoard;
    public string MicrovirtVmBrand;
    public string MicrovirtVmGsm;
    public string MicrovirtVmHardware;
    public string MicrovirtVmManufacturer;
    public string MicrovirtVmModel;
    public string SecurityPatchDate;
    public string Tac;
}

/// <summary>
/// Общая инфорация об устройстве
/// </summary>
public struct DeviceInfoGenerated
{
    public string Latitude;
    public string Longitude;
    public string Mac;
    public string Ssid;
    public string Imei;
    public string Imsi;
    public MccMnc MccMnc;
    public string ManualDiskSize;
    public MicrovirtInfo MicrovirtInfo;
    public string Simserial;
    public Resolution Resolution;
    public string TimeZone;
    public AndroidRelease AndroidRelease;
    public string SerialNo;
    public string BoardPlatform;
    public string GoogleFrameworkId;
    public string Language;
    public string AndroidId;
    public string ZenModeConfigEtag;
    public string BootCount;
    public string PBootCount;
}