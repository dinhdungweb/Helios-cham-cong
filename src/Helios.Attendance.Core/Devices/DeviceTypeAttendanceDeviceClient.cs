using Helios.Attendance.Core.Models;

namespace Helios.Attendance.Core.Devices;

public sealed class DeviceTypeAttendanceDeviceClient : IAttendanceDeviceClient
{
    private readonly ZkAttendanceDeviceClient _zkClient = new();

    public Task<DeviceConnectionResult> TestConnectionAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        return AttendanceDeviceTypes.Normalize(device.DeviceType) switch
        {
            AttendanceDeviceTypes.ZkRonaldJack => _zkClient.TestConnectionAsync(device, cancellationToken),
            _ => Task.FromResult(DeviceConnectionResult.Fail(GetUnsupportedMessage(device)))
        };
    }

    public Task<IReadOnlyList<AttendancePunch>> ReadLogsAsync(
        Device device,
        DateTime fromTime,
        CancellationToken cancellationToken)
    {
        return AttendanceDeviceTypes.Normalize(device.DeviceType) switch
        {
            AttendanceDeviceTypes.ZkRonaldJack => _zkClient.ReadLogsAsync(device, fromTime, cancellationToken),
            _ => throw new NotSupportedException(GetUnsupportedMessage(device))
        };
    }

    private static string GetUnsupportedMessage(Device device)
    {
        var displayName = AttendanceDeviceTypes.GetDisplayName(device.DeviceType);
        return $"Loại máy {displayName} chưa được hỗ trợ trong bản này. Cần bổ sung SDK/giao thức của hãng trước khi đọc log.";
    }
}
