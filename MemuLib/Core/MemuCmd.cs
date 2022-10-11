namespace MemuLib.Core;

public static class MemuCmd
{
    /// <summary>
    /// Обращение к мему
    /// </summary>
    /// <param name="arg">аргументы для мема</param>
    /// <returns>ответ от мема</returns>
    public static async Task<string> ExecMemuc(string arg)
    {
        var cmd = new Process();
        cmd.StartInfo.FileName = @$"{Settings.BaseDir}\memuc.exe";
        cmd.StartInfo.RedirectStandardOutput = true;
        cmd.StartInfo.CreateNoWindow = true;
        cmd.StartInfo.UseShellExecute = false;
        cmd.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        cmd.StartInfo.Arguments = arg;
        cmd.StartInfo.WorkingDirectory = Settings.BaseDir;
        cmd.Start();
        return await cmd.StandardOutput.ReadToEndAsync();
    }
}