namespace Helios.Attendance.Core.Models;

public sealed class AppError
{
    public int Id { get; init; }

    public string ErrorType { get; init; } = string.Empty;

    public string DeviceId { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public string CreatedAt { get; init; } = string.Empty;
}
