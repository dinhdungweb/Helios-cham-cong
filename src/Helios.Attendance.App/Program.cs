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
        builder.Services.AddSingleton<IAttendanceDeviceClient, TcpAttendanceDeviceClient>();
        builder.Services.AddHostedService<ServiceWorker>();

        using var host = builder.Build();
        await host.RunAsync();
    }
}
