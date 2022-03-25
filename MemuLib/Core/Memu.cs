namespace MemuLib.Core;

public static class Memu
{
    /// <summary>
    /// Запуск ADB сервера
    /// </summary>
    /// <exception cref="Exception">ошибка при запуске, допустим зависло или х** его знает что еще</exception>
    public static void RunAdbServer()
    {
        if (AdbServer.Instance.GetStatus().IsRunning)
            return;

        var server = new AdbServer();
        var result = server.StartServer($@"{Settings.BaseDir}\adb.exe", false);

        if (result != StartServerResult.Started)
            throw new Exception("Can't start adb server");
        
        Log.Write("ADB server is started!");
    }

    /// <summary>
    /// Проверка на существование машины
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <returns>true если есть, и false если нема</returns>
    public static async Task<bool> Exists(int index) =>
        !string.IsNullOrEmpty(await MemuCmd.ExecMemuc($"listvms -i {index}"));

    /// <summary>
    /// Создание машины
    /// </summary>
    /// <returns>вернет индекс созданной машины</returns>
    /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
    public static async Task<int> Create()
    {
        var answer = await MemuCmd.ExecMemuc("create");
        if (!answer.Contains("SUCCESS"))
            throw new Exception($"Error: {answer}");

        return int.Parse(answer.Split('\n')[1].Split(':')[1]);
    }

    /// <summary>
    /// Клонирование машины
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <returns>вернет индекс клонированной машины</returns>
    /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
    public static async Task<int> Clone(int index)
    {
        var answer = await MemuCmd.ExecMemuc($"clone -i {index}");
        if (!answer.Contains("SUCCESS"))
            throw new Exception($"Error: {answer}");

        return int.Parse(answer.Split('\n')[1].Split(':')[1]);
    }

    /// <summary>
    /// Удаление машины
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
    public static async Task Remove(int index)
    {
        var answer = await MemuCmd.ExecMemuc($"remove -i {index}");
        if (!answer.Contains("SUCCESS"))
            throw new Exception($"Error: {answer}");
    }
    
    /// <summary>
    /// Перезагрузка машины
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
    public static async Task Reboot(int index)
    {
        var answer = await MemuCmd.ExecMemuc($"reboot -i {index}");
        if (!answer.Contains("SUCCESS"))
            throw new Exception($"Error: {answer}");
    }

    /// <summary>
    /// Запуск машины
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
    public static async Task Start(int index)
    {
        var answer = await MemuCmd.ExecMemuc($"start -i {index}");
        if (!answer.Contains("SUCCESS"))
            throw new Exception($"Error: {answer}");
    }

    /// <summary>
    /// Остановка машины
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
    public static async Task Stop(int index)
    {
        var answer = await MemuCmd.ExecMemuc($"stop -i {index}");
        if (!answer.Contains("SUCCESS"))
            throw new Exception($"Error: {answer}");
    }

    /// <summary>
    /// Установка apk файла на машину
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <param name="path">путь до apk файла на локальной машине</param>
    /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
    public static async Task InstallApk(int index, string path)
    {
        var newPath = $@"{new FileInfo(path).Directory?.FullName}\{new Random().Next(1_000_000, 5_000_000)}.apk";
        File.Copy(path,newPath);//Решает проблему загруженности
        
        var answer = await MemuCmd.ExecMemuc($"installapp -i {index} {newPath}");
        
        if (!answer.Contains("SUCCESS"))
            throw new Exception($"Error: {answer}");
        
        File.Delete(newPath);//Удалем копию, ибо нехер засирать
    }

    /// <summary>
    /// Запуск apk на машине
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <param name="path">путь до apk файла на удаленной машине</param>
    /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
    public static async Task StartApk(int index, string path)
    {
        var answer = await MemuCmd.ExecMemuc($"startapp -i {index} {path}");
        if (!answer.Contains("SUCCESS"))
            throw new Exception($"Error: {answer}");
    }

    /// <summary>
    /// Завершение apk на машине
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <param name="path">путь до apk файла на удаленной машине</param>
    /// <exception cref="Exception">в случае тотального п***а просто вылезет ошибка</exception>
    public static async Task StopApk(int index, string path)
    {
        var answer = await MemuCmd.ExecMemuc($"stopapp -i {index} {path}");
        if (!answer.Contains("SUCCESS"))
            throw new Exception($"Error: {answer}");
    }

    /// <summary>
    /// Отправка на удаленку
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <param name="local">путь на локальной машине</param>
    /// <param name="remote">путь на удаленной машине</param>
    public static async Task Push(int index, string local, string remote) =>
        await MemuCmd.ExecMemuc($"-i {index} adb push {local} {remote}");

    /// <summary>
    /// Загрузка с удаленки
    /// </summary>
    /// <param name="index">индекс машины</param>
    /// <param name="local">путь на локальной машине</param>
    /// <param name="remote">путь на удаленной машине</param>
    public static async Task Pull(int index, string local, string remote) =>
        await MemuCmd.ExecMemuc($"-i {index} adb pull {remote} {local}");

    /// <summary>
    /// Изменить информацию об устройстве (устройство должно быть активно и после применения перезагруженно)
    /// </summary>
    /// <param name="index">индекс устройства</param>
    /// <param name="deviceInfoGenerated">новая информация об оборудование</param>
    public static async Task Spoof(int index, DeviceInfoGenerated deviceInfoGenerated)
    {
        //setconfigex
        await MemuCmd.ExecMemuc($"setconfigex -i {index} enable_audio 0");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} latitude {deviceInfoGenerated.Latitude}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} longitude {deviceInfoGenerated.Longitude}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} is_customed_resolution 1");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} macaddress {deviceInfoGenerated.Mac}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} ssid {deviceInfoGenerated.Ssid}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} imei {deviceInfoGenerated.Imei}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} imsi {deviceInfoGenerated.Imsi}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} manual_disk_size {deviceInfoGenerated.ManualDiskSize}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} microvirt_vm_board {deviceInfoGenerated.MicrovirtInfo.MicrovirtVmBoard}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} microvirt_vm_brand {deviceInfoGenerated.MicrovirtInfo.MicrovirtVmBrand}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} microvirt_vm_gsm {deviceInfoGenerated.MicrovirtInfo.MicrovirtVmGsm}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} microvirt_vm_hardware {deviceInfoGenerated.MicrovirtInfo.MicrovirtVmHardware}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} microvirt_vm_manufacturer {deviceInfoGenerated.MicrovirtInfo.MicrovirtVmManufacturer}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} microvirt_vm_model {deviceInfoGenerated.MicrovirtInfo.MicrovirtVmModel}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} operator_countrycode {deviceInfoGenerated.MccMnc.CountryCode}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} operator_iso {deviceInfoGenerated.MccMnc.Iso}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} simserial {deviceInfoGenerated.Simserial}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} manual_disk_size {deviceInfoGenerated.ManualDiskSize}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} resolution_height {deviceInfoGenerated.Resolution.Height}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} resolution_width {deviceInfoGenerated.Resolution.Width}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} vbox_dpi {deviceInfoGenerated.Resolution.Dpi}");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} linenum null");
        await MemuCmd.ExecMemuc($"setconfigex -i {index} fps 20");
        
        //setprop
        await MemuCmd.ExecMemuc($"-i {index} execcmd \"setprop persist.sys.timezone {deviceInfoGenerated.TimeZone}\"");
        await MemuCmd.ExecMemuc($"-i {index} execcmd \"setprop ro.build.version.incremental {deviceInfoGenerated.MicrovirtInfo.MicrovirtVmBrand}-{deviceInfoGenerated.MicrovirtInfo.MicrovirtVmManufacturer}{deviceInfoGenerated.AndroidRelease.Version}release-keys\"");
        await MemuCmd.ExecMemuc($"-i {index} execcmd \"setprop ro.build.version.release {deviceInfoGenerated.AndroidRelease.Version}\"");
        await MemuCmd.ExecMemuc($"-i {index} execcmd \"setprop ro.build.version.sdk {deviceInfoGenerated.AndroidRelease.ApiVersion}\"");
        await MemuCmd.ExecMemuc($"-i {index} execcmd \"setprop ro.build.version.security_patch {deviceInfoGenerated.MicrovirtInfo.SecurityPatchDate}\"");
        await MemuCmd.ExecMemuc($"-i {index} execcmd \"setprop ro.serialno {deviceInfoGenerated.SerialNo}\"");
        await MemuCmd.ExecMemuc($"-i {index} execcmd \"setprop ro.board.platform {deviceInfoGenerated.BoardPlatform}\"");
        await MemuCmd.ExecMemuc($"-i {index} execcmd \"setprop ro.build.id {deviceInfoGenerated.AndroidRelease.BuildId}\"");
        await MemuCmd.ExecMemuc($"-i {index} execcmd \"setprop ro.google.service.framework.id {deviceInfoGenerated.GoogleFrameworkId}\"");
        
        //additional
        await MemuCmd.ExecMemuc($"-i {index} execcmd pm grant net.sanapeli.adbchangelanguage android.permission.CHANGE_CONFIGURATION");
        await MemuCmd.ExecMemuc($"-i {index} execcmd \"am start -n net.sanapeli.adbchangelanguage/.AdbChangeLanguage -e language {deviceInfoGenerated.Language}\"");
        await MemuCmd.ExecMemuc($"-i {index} execcmd \"setprop persist.sys.language {deviceInfoGenerated.Language}\"");
        await MemuCmd.ExecMemuc($"-i {index} execcmd \"setprop persist.sys.country {deviceInfoGenerated.Language.Split('-')[0]}\"");
        await MemuCmd.ExecMemuc($"-i {index} execcmd \"setprop ro.product.locale {deviceInfoGenerated.Language}\"");

        await MemuCmd.ExecMemuc($"-i {index} adb shell settings put secure android_id {deviceInfoGenerated.AndroidId}");
        await MemuCmd.ExecMemuc($"-i {index} adb shell settings put secure zen_mode_config_etag {deviceInfoGenerated.ZenModeConfigEtag}");
        await MemuCmd.ExecMemuc($"-i {index} adb shell settings put secure boot_count {deviceInfoGenerated.BootCount}");
        await MemuCmd.ExecMemuc($"-i {index} adb shell settings put secure Phenotype_boot_count {deviceInfoGenerated.PBootCount}");
    }
}