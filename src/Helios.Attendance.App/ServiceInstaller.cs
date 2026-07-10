using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;
using Helios.Attendance.Core;

namespace Helios.Attendance.App;

public static class ServiceInstaller
{
    private const string DisplayName = "HELIOS Attendance Sync Service";

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void InstallOrUpdateService()
    {
        if (!IsAdministrator())
        {
            throw new InvalidOperationException("Cần quyền Administrator để cài Windows Service.");
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new FileNotFoundException("Không xác định được file app để đăng ký service.", executablePath);
        }

        StopServiceIfExists();
        DeleteServiceIfExists();

        RunSc(
            "create",
            AppPaths.ServiceName,
            "binPath=",
            $"\"{executablePath}\" --service",
            "start=",
            "auto",
            "DisplayName=",
            DisplayName);

        RunSc(
            "description",
            AppPaths.ServiceName,
            "Đồng bộ dữ liệu máy chấm công HELIOS lên web.");

        using var service = new ServiceController(AppPaths.ServiceName);
        service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
    }

    public static void UninstallService()
    {
        if (!IsAdministrator())
        {
            throw new InvalidOperationException("Cần quyền Administrator để gỡ Windows Service.");
        }

        StopServiceIfExists();
        DeleteServiceIfExists();
    }

    public static void LaunchElevatedInstall()
    {
        LaunchElevated("--install-service");
    }

    public static void LaunchElevatedUninstall()
    {
        LaunchElevated("--uninstall-service");
    }

    private static void LaunchElevated(string arguments)
    {
        var executablePath = Application.ExecutablePath;
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas"
            });

            if (process is null)
            {
                throw new InvalidOperationException("Không thể mở tiến trình cài service.");
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("Cài service không thành công hoặc đã bị hủy.");
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("Bạn đã hủy yêu cầu cấp quyền Administrator.", ex);
        }
    }

    private static void StopServiceIfExists()
    {
        if (!IsServiceInstalled())
        {
            return;
        }

        using var service = new ServiceController(AppPaths.ServiceName);
        if (service.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
        {
            return;
        }

        service.Stop();
        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
    }

    private static void DeleteServiceIfExists()
    {
        if (!IsServiceInstalled())
        {
            return;
        }

        RunSc("delete", AppPaths.ServiceName);
        Thread.Sleep(1500);
    }

    public static bool IsServiceInstalled()
    {
        var services = ServiceController.GetServices();
        try
        {
            return services.Any(service =>
                string.Equals(service.ServiceName, AppPaths.ServiceName, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            foreach (var service in services)
            {
                service.Dispose();
            }
        }
    }

    private static void RunSc(params string[] arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = "sc.exe";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Lệnh sc.exe lỗi ({process.ExitCode}). {output} {error}".Trim());
        }
    }
}
