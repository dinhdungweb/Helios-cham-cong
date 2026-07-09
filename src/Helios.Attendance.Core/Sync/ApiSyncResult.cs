namespace Helios.Attendance.Core.Sync;

public sealed class ApiSyncResult
{
    public bool Success { get; init; }

    public int Inserted { get; init; }

    public int Duplicated { get; init; }

    public int Failed { get; init; }

    public string Error { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<ApiLogError> Errors { get; init; } = [];

    public static ApiSyncResult EmptySuccess() => new()
    {
        Success = true,
        Message = "Không có log cần gửi."
    };

    public static ApiSyncResult Fail(string message) => new()
    {
        Success = false,
        Message = message,
        Error = "CLIENT_ERROR"
    };
}
