namespace Helios.Attendance.Core.Models;

public sealed class SyncLog
{
    public int Id { get; init; }

    public string DeviceId { get; init; } = string.Empty;

    public string StartedAt { get; init; } = string.Empty;

    public string FinishedAt { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public int TotalRead { get; init; }

    public int TotalSent { get; init; }

    public int TotalInserted { get; init; }

    public int TotalDuplicated { get; init; }

    public int TotalFailed { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}
