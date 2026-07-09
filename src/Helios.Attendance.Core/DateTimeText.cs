using System.Globalization;

namespace Helios.Attendance.Core;

public static class DateTimeText
{
    public const string StorageFormat = "yyyy-MM-dd HH:mm:ss";

    public static string Now() => Format(DateTime.Now);

    public static string Format(DateTime value) =>
        value.ToString(StorageFormat, CultureInfo.InvariantCulture);

    public static DateTime ParseOrDefault(string? value, DateTime fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return DateTime.TryParseExact(
            value,
            new[] { StorageFormat, "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.fff" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var parsed)
            ? parsed
            : fallback;
    }
}
