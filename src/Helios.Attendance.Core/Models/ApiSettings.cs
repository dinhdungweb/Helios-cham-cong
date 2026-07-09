namespace Helios.Attendance.Core.Models;

public sealed class ApiSettings
{
    public string ApiUrl { get; init; } = string.Empty;

    public string ApiToken { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 30;
}
