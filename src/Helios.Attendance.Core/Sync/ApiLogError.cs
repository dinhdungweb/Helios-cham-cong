using System.Text.Json.Serialization;

namespace Helios.Attendance.Core.Sync;

public sealed class ApiLogError
{
    [JsonPropertyName("employee_code")]
    public string EmployeeCode { get; init; } = string.Empty;

    [JsonPropertyName("punch_time")]
    public string PunchTime { get; init; } = string.Empty;

    [JsonPropertyName("error_type")]
    public string ErrorType { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}
