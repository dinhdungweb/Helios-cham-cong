using Microsoft.Win32;

namespace Helios.Attendance.App;

public static class AppRegistration
{
    private const string AppName = "HOFFICE";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\HOFFICE";

    public static void RegisterForCurrentUser()
    {
        var executablePath = Application.ExecutablePath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return;
        }

        var installLocation = Path.GetDirectoryName(executablePath) ?? string.Empty;
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath);
        if (key is null)
        {
            return;
        }

        key.SetValue("DisplayName", AppName, RegistryValueKind.String);
        key.SetValue("DisplayVersion", "1.0.0", RegistryValueKind.String);
        key.SetValue("Publisher", "HOFFICE", RegistryValueKind.String);
        key.SetValue("DisplayIcon", $"\"{executablePath}\",0", RegistryValueKind.String);
        key.SetValue("InstallLocation", installLocation, RegistryValueKind.String);
        key.SetValue("UninstallString", $"\"{executablePath}\" --uninstall-app", RegistryValueKind.String);
        key.SetValue("QuietUninstallString", $"\"{executablePath}\" --uninstall-app", RegistryValueKind.String);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", EstimateInstallSizeKb(installLocation), RegistryValueKind.DWord);
    }

    public static void UninstallForCurrentUser()
    {
        if (ServiceInstaller.IsServiceInstalled())
        {
            ServiceInstaller.LaunchElevatedUninstall();
        }

        DeleteShortcut(Environment.GetFolderPath(Environment.SpecialFolder.Programs));
        DeleteShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
    }

    private static void DeleteShortcut(string directory)
    {
        var shortcutPath = Path.Combine(directory, $"{AppName}.lnk");
        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }

    private static int EstimateInstallSizeKb(string installLocation)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation))
            {
                return 0;
            }

            var totalBytes = Directory.EnumerateFiles(installLocation, "*", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .Sum(file => file.Length);

            return (int)Math.Min(int.MaxValue, Math.Max(1, totalBytes / 1024));
        }
        catch
        {
            return 0;
        }
    }
}
