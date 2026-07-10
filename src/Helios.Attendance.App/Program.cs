using Helios.Attendance.Core;
using Helios.Attendance.Core.Data;
using Helios.Attendance.Core.Devices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Helios.Attendance.App;

static class Program
{
    [STAThread]
    static async Task Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--install-service", StringComparison.OrdinalIgnoreCase)))
        {
            RunServiceInstall();
            return;
        }

        if (args.Any(arg => string.Equals(arg, "--uninstall-service", StringComparison.OrdinalIgnoreCase)))
        {
            RunServiceUninstall();
            return;
        }

        if (args.Any(arg => string.Equals(arg, "--register-app", StringComparison.OrdinalIgnoreCase)))
        {
            RunAppRegister();
            return;
        }

        if (args.Any(arg => string.Equals(arg, "--uninstall-app", StringComparison.OrdinalIgnoreCase)))
        {
            RunAppUninstall();
            return;
        }

        if (args.Any(arg => string.Equals(arg, "--service", StringComparison.OrdinalIgnoreCase)))
        {
            await RunServiceAsync(args);
            return;
        }

        RunGui();
    }

    private static void RunGui()
    {
        ApplicationConfiguration.Initialize();
        AppRegistration.RegisterForCurrentUser();
        Application.Run(new MainForm());
    }

    private static async Task RunServiceAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = AppPaths.ServiceName;
        });
        builder.Services.AddSingleton<AttendanceSyncStore>();
        builder.Services.AddSingleton<IAttendanceDeviceClient, ZkAttendanceDeviceClient>();
        builder.Services.AddHostedService<ServiceWorker>();

        using var host = builder.Build();
        await host.RunAsync();
    }

    private static void RunServiceInstall()
    {
        try
        {
            ServiceInstaller.InstallOrUpdateService();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Không cài được service", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }

    private static void RunServiceUninstall()
    {
        try
        {
            ServiceInstaller.UninstallService();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Không gỡ được service", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }

    private static void RunAppRegister()
    {
        try
        {
            AppRegistration.RegisterForCurrentUser();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Không đăng ký được app", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }

    private static void RunAppUninstall()
    {
        try
        {
            AppRegistration.UninstallForCurrentUser();
            MessageBox.Show(
                "Đã gỡ HOFFICE khỏi Start Menu và Installed Apps. Nếu muốn xóa hẳn file app, hãy xóa thư mục chứa app sau khi đóng cửa sổ này.",
                "Đã gỡ HOFFICE",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Không gỡ được app", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }
}
