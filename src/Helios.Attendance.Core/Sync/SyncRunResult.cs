namespace Helios.Attendance.Core.Sync;

public sealed class SyncRunResult
{
    public int DeviceCount { get; init; }

    public int TotalRead { get; init; }

    public int TotalSent { get; init; }

    public int TotalInserted { get; init; }

    public int TotalDuplicated { get; init; }

    public int TotalFailed { get; init; }

    public int PendingCreated { get; init; }

    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;
}
