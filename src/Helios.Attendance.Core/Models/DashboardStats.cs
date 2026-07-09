namespace Helios.Attendance.Core.Models;

public sealed class DashboardStats
{
    public int TotalDevices { get; init; }

    public int ActiveDevices { get; init; }

    public int DeviceErrors { get; init; }

    public int PendingLogs { get; init; }

    public int MappingErrorsToday { get; init; }

    public int SentToday { get; init; }

    public string LastSyncAt { get; init; } = string.Empty;
}
