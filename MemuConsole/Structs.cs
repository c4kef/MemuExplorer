namespace MemuConsole;

public struct AndroidRelease
{
    public string Version;
    public string BuildId;
    public string ApiVersion;
}

public struct Resolution
{
    public string Height;
    public string Width;
    public string Dpi;
}

public struct MccMnc
{
    public string Mcc;
    public string Mnc;
    public string Iso;
    public string CountryCode;
    public string MobileOperator;
}

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