namespace Helios.Attendance.Core.Models;

public sealed class PendingLog
{
    public int Id { get; init; }

    public string DeviceId { get; init; } = string.Empty;

    public string StoreCode { get; init; } = string.Empty;

    public string EmployeeCode { get; init; } = string.Empty;

    public string PunchTime { get; init; } = string.Empty;

    public string VerifyType { get; init; } = string.Empty;

    public int RetryCount { get; init; }

    public string LastError { get; init; } = string.Empty;

    public string CreatedAt { get; init; } = string.Empty;

    public string UpdatedAt { get; init; } = string.Empty;

    public AttendancePunch ToPunch() => new()
    {
        DeviceId = DeviceId,
        StoreCode = StoreCode,
        EmployeeCode = EmployeeCode,
        PunchTime = DateTimeText.ParseOrDefault(PunchTime, DateTime.Now),
        VerifyType = VerifyType
    };
}
