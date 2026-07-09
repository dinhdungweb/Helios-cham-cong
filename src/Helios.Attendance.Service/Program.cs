using Helios.Attendance.Core;
using Helios.Attendance.Core.Data;
using Helios.Attendance.Core.Devices;
using Helios.Attendance.Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = AppPaths.ServiceName;
});
builder.Services.AddSingleton<AttendanceSyncStore>();
builder.Services.AddSingleton<IAttendanceDeviceClient, TcpAttendanceDeviceClient>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
