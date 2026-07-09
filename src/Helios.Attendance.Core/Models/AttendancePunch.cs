namespace Helios.Attendance.Core.Models;

public sealed class AttendancePunch
{
    public string DeviceId { get; init; } = string.Empty;

    public string StoreCode { get; init; } = string.Empty;

    public string EmployeeCode { get; init; } = string.Empty;

    public DateTime PunchTime { get; init; }

    public string VerifyType { get; init; } = string.Empty;

    public string RawPayload { get; init; } = string.Empty;
}
