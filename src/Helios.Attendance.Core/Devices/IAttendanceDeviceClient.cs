using Helios.Attendance.Core.Models;

namespace Helios.Attendance.Core.Devices;

public interface IAttendanceDeviceClient
{
    Task<DeviceConnectionResult> TestConnectionAsync(Device device, CancellationToken cancellationToken);

    Task<IReadOnlyList<AttendancePunch>> ReadLogsAsync(
        Device device,
        DateTime fromTime,
        CancellationToken cancellationToken);
}
