namespace Helios.Attendance.Core.Models;

public sealed class Device
{
    public int Id { get; init; }

    public string DeviceId { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string StoreCode { get; init; } = string.Empty;

    public string DeviceType { get; init; } = AttendanceDeviceTypes.ZkRonaldJack;

    public string DeviceTypeName => AttendanceDeviceTypes.GetDisplayName(DeviceType);

    public string IpAddress { get; init; } = string.Empty;

    public int Port { get; init; } = 4370;

    public int Password { get; init; }

    public bool IsActive { get; init; } = true;

    public string? LastSuccessSyncAt { get; init; }

    public string? LastError { get; init; }

    public string? CreatedAt { get; init; }

    public string? UpdatedAt { get; init; }
}
