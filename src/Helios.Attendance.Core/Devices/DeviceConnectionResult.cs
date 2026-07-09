namespace Helios.Attendance.Core.Devices;

public sealed class DeviceConnectionResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public static DeviceConnectionResult Ok(string message) => new()
    {
        Success = true,
        Message = message
    };

    public static DeviceConnectionResult Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}
